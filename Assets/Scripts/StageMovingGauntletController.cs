using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Owns the rebuilt 11-1 ride: a long horizontal bomb/ghost run followed by
    /// a ten-second vertical crossfire and a short walk to the goal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageMovingGauntletController : StageEliminationChallengeController
    {
        private const string PlatformId = "11-1_ride_platform";
        private const float HorizontalEndX = 80f;
        private const float VerticalEndY = 36f;
        private const float HorizontalSpeed = 1.75f;
        private const float VerticalSpeed = 1.2f;
        private const float AttackSwapSeconds = 5f;
        private const float ChaseEndX = 188f;
        private const string EliminationSystemId = "11-1_elimination";
        private const string EliminationRequestKind = "11-1_elimination_request";
        private const string EliminationStateKind = "11-1_elimination_state";

        [System.Serializable]
        private sealed class EliminationMessage
        {
            public string PlayerId;
        }

        private Rigidbody2D platformBody;
        private Transform platformTransform;
        private GameObject leftWall;
        private GameObject rightWall;
        private StageGimmickSyncManager syncManager;
        private StageLoader stageLoader;
        private StageManager stageManager;
        private float nextBombAt;
        private float nextGhostAt;
        private float nextMissileAt;
        private float verticalStartedAt = -1f;
        private int bombSequence;
        private int ghostSequence;
        private int missileSequence;
        private Vector2 lastObservedPlatformPosition;
        private bool chaseStarted;
        private float chaseStartedAt;
        private float nextChaseAttackAt;
        private StageMovingGauntletGiantGhost giantGhost;
        private Stage11DarknessController darknessController;
        private OnlineManager onlineManager;
        private readonly HashSet<string> eliminatedPlayerIds = new HashSet<string>();
        private bool retryQueued;

        private bool HasAuthority => syncManager == null || !syncManager.ShouldAskHost;

        private void Awake()
        {
            syncManager = GetComponent<StageGimmickSyncManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleEliminationMessage;
            // StageLoader destroys the previous stage children at the end of the
            // frame.  Ignore those inactive objects, otherwise a same-frame retry
            // can attach all runtime pieces to the platform that is about to die.
            StageEditorObject[] objects = GetComponentsInChildren<StageEditorObject>(false);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null || objects[i].objectId != PlatformId) continue;
                platformTransform = objects[i].transform;
                platformBody = objects[i].GetComponent<Rigidbody2D>();
                if (objects[i].GetComponent<StageMovingGauntletPlatform>() == null)
                    objects[i].gameObject.AddComponent<StageMovingGauntletPlatform>();
                break;
            }

            if (platformTransform == null) return;
            if (platformBody != null)
            {
                platformBody.bodyType = RigidbodyType2D.Kinematic;
                platformBody.gravityScale = 0f;
                platformBody.freezeRotation = true;
                platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            BuildRideWalls();
            BuildVerticalGuideLights();
            StageFlashlight.Create(platformTransform.parent, new Vector2(-5.8f, 1.05f));
            darknessController = Stage11DarknessController.Ensure(platformTransform.parent);
            lastObservedPlatformPosition = platformTransform.position;
            nextBombAt = Time.time + 1.1f;
            nextGhostAt = Time.time + 2f;
            nextMissileAt = Time.time + 0.8f;
        }

        private void OnDestroy()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleEliminationMessage;
        }

        private void Update()
        {
            if (platformTransform == null) return;
            bool waiting = stageManager != null && stageManager.IsChallengeReadyRoomActive;
            if (darknessController != null && darknessController.gameObject.activeSelf == waiting)
                darknessController.gameObject.SetActive(!waiting);
            if (waiting) return;
            Vector2 platformPosition = platformTransform.position;
            bool vertical = platformPosition.x >= HorizontalEndX - 0.2f || platformPosition.y > 0.15f;
            SetRideWallsVisible(!vertical);
            stageLoader?.SetRuntimeSpawnPosition(platformPosition + new Vector2(-4.5f, 1.05f));

            if (!HasAuthority) return;
            if (!vertical)
            {
                UpdateHorizontalAttacks(platformPosition);
            }
            else if (platformPosition.y < VerticalEndY - 0.1f)
            {
                UpdateVerticalAttacks(platformPosition);
            }
            else
            {
                UpdateFinalChase(platformPosition);
            }
        }

        private void FixedUpdate()
        {
            if (platformTransform == null) return;
            if (stageManager != null && stageManager.IsChallengeReadyRoomActive) return;
            Vector2 current = platformBody != null ? platformBody.position : (Vector2)platformTransform.position;
            if (!HasAuthority)
            {
                Vector2 observedDelta = current - lastObservedPlatformPosition;
                if (observedDelta.sqrMagnitude > 0.000001f)
                    MoveLocalPassengers(observedDelta, lastObservedPlatformPosition);
                lastObservedPlatformPosition = current;
                return;
            }
            Vector2 target;
            float speed;
            if (current.x < HorizontalEndX - 0.01f)
            {
                target = new Vector2(HorizontalEndX, 0f);
                speed = HorizontalSpeed;
            }
            else if (current.y < VerticalEndY - 0.01f)
            {
                if (verticalStartedAt < 0f) verticalStartedAt = Time.time;
                target = new Vector2(HorizontalEndX, VerticalEndY);
                speed = VerticalSpeed;
            }
            else
            {
                target = new Vector2(HorizontalEndX, VerticalEndY);
                speed = VerticalSpeed;
            }

            Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);
            if (platformBody != null) platformBody.MovePosition(next);
            else platformTransform.position = next;
            Vector2 delta = next - current;
            if (delta.sqrMagnitude > 0.000001f) MoveLocalPassengers(delta, current);
            lastObservedPlatformPosition = next;
        }

        private void UpdateHorizontalAttacks(Vector2 platformPosition)
        {
            float progress = Mathf.Clamp01(platformPosition.x / HorizontalEndX);
            if (HasProjectileAttacks && Time.time >= nextBombAt)
            {
                float interval = Mathf.Lerp(2.85f, 1.05f, progress);
                nextBombAt = Time.time + Random.Range(interval * 0.88f, interval * 1.12f);
                SpawnBomb(platformPosition, progress);
            }
            if (progress < 0.24f || Time.time < nextGhostAt) return;
            float ghostInterval = Mathf.Lerp(4.1f, 1.55f, Mathf.InverseLerp(0.24f, 1f, progress));
            nextGhostAt = Time.time + Random.Range(ghostInterval * 0.88f, ghostInterval * 1.12f);
            float side = Random.value < 0.5f ? -1f : 1f;
            SpawnGhost(
                platformPosition + new Vector2(side * Random.Range(16f, 22f), Random.Range(1.6f, 5.4f)),
                -side);
        }

        private void UpdateVerticalAttacks(Vector2 platformPosition)
        {
            if (verticalStartedAt < 0f) verticalStartedAt = Time.time;
            float progress = Mathf.Clamp01(platformPosition.y / VerticalEndY);
            if (Time.time >= nextGhostAt)
            {
                float interval = Mathf.Lerp(3f, 1.05f, progress);
                nextGhostAt = Time.time + Random.Range(interval * 0.88f, interval * 1.12f);
                SpawnGhost(
                    platformPosition + new Vector2(Random.Range(-7.8f, 7.8f), Random.Range(12f, 17f)),
                    Random.value < 0.5f ? -1f : 1f,
                    Random.value < 0.48f);
            }
            if (HasProjectileAttacks && Time.time >= nextMissileAt)
            {
                float interval = Mathf.Lerp(2.25f, 0.78f, progress);
                nextMissileAt = Time.time + Random.Range(interval * 0.9f, interval * 1.1f);
                bool fromLeft = Mathf.FloorToInt((Time.time - verticalStartedAt) / AttackSwapSeconds) % 2 == 0;
                float side = fromLeft ? 1f : -1f;
                SpawnMissile(platformPosition, side, progress);
            }
        }

        private void SpawnBomb(Vector2 platformPosition, float progress)
        {
            string id = "11-1_bomb_" + bombSequence.ToString("D5");
            bombSequence++;
            Vector2 position = platformPosition + new Vector2(Random.Range(-7.7f, 7.7f), Random.Range(11.5f, 14f));
            if (syncManager != null)
                syncManager.SpawnDropperBox(
                    id,
                    StageObjectType.Bomb,
                    position,
                    0.9f,
                    0f,
                    3f);
        }

        private void SpawnGhost(Vector2 position, float facing, bool phasing = false)
        {
            if (syncManager == null) return;
            string id = (phasing ? "11-1_phase_ghost_" : "11-1_ghost_")
                + ghostSequence.ToString("D5");
            ghostSequence++;
            syncManager.SpawnDropperEnemy(
                id,
                StageObjectType.EnemyFlyer,
                position,
                Random.Range(0.72f, 1.75f),
                Random.Range(0.875f, 2.175f),
                facing,
                Vector2.zero);
        }

        private void UpdateFinalChase(Vector2 platformPosition)
        {
            if (!chaseStarted)
            {
                chaseStarted = true;
                chaseStartedAt = Time.time;
                nextChaseAttackAt = Time.time + 1.2f;
                giantGhost = StageMovingGauntletGiantGhost.Create(
                    transform,
                    new Vector2(HorizontalEndX - 22f, VerticalEndY + 3.4f));
                CreateVerticalGhostGates();
            }

            if (giantGhost != null) giantGhost.SetChaseActive(true);
            if (Time.time < nextChaseAttackAt) return;
            float intensity = Mathf.Clamp01((Time.time - chaseStartedAt) / 35f);
            nextChaseAttackAt = Time.time + Random.Range(
                Mathf.Lerp(2.4f, 0.95f, intensity),
                Mathf.Lerp(3.2f, 1.35f, intensity));
            float sourceX = Mathf.Min(ChaseEndX + 6f, FindLeadingPlayerX() + Random.Range(17f, 25f));
            if (!HasProjectileAttacks || Random.value < 0.52f)
            {
                SpawnGhost(
                    new Vector2(sourceX, VerticalEndY + Random.Range(1.4f, 6.2f)),
                    -1f,
                    Random.value < 0.38f);
            }
            else
            {
                SpawnMissile(new Vector2(sourceX - 14f, VerticalEndY), 1f, intensity);
            }
        }

        private void CreateVerticalGhostGates()
        {
            float[] positions = { 112f, 134f, 156f, 177f };
            for (int i = 0; i < positions.Length; i++)
            {
                StageMovingGauntletVerticalGhost.Create(
                    transform,
                    positions[i],
                    VerticalEndY + 1.4f,
                    VerticalEndY + 11.5f,
                    1.75f + i * 0.18f,
                    i * 2.1f);
            }
        }

        private bool HasProjectileAttacks => stageManager != null
            && stageManager.GetInkBudgetPlayerCount() >= 3;

        private float FindLeadingPlayerX()
        {
            float result = HorizontalEndX;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].isActiveAndEnabled)
                    result = Mathf.Max(result, players[i].transform.position.x);
            return result;
        }

        private void SpawnMissile(Vector2 platformPosition, float sourceSide, float progress)
        {
            if (syncManager == null || platformTransform == null) return;
            string id = "11-1_crossfire_" + missileSequence.ToString("D5");
            missileSequence++;
            Vector2 origin = platformPosition + new Vector2(sourceSide * 14f, Random.Range(1.2f, 5.4f));
            Vector2 target = platformPosition + new Vector2(Random.Range(-5f, 5f), Random.Range(0.8f, 3.8f));
            syncManager.SpawnMissile(
                id,
                PlatformId,
                platformTransform,
                origin,
                (target - origin).normalized,
                Mathf.Lerp(5.2f, 8.2f, progress));
        }

        private void MoveLocalPassengers(Vector2 delta, Vector2 platformPosition)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            Transform localPlayer = stageManager != null ? stageManager.ActivePlayerTransform : null;
            float platformTop = platformPosition.y + 0.5f;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy
                    || online && player.transform != localPlayer) continue;

                Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
                Bounds bounds = new Bounds(player.transform.position, Vector3.zero);
                bool found = false;
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider2D collider = colliders[colliderIndex];
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    if (!found) bounds = collider.bounds;
                    else bounds.Encapsulate(collider.bounds);
                    found = true;
                }

                float feetY = found ? bounds.min.y : player.transform.position.y;
                bool overlapsWidth = (found ? bounds.max.x : player.transform.position.x) >= platformPosition.x - 10.15f
                    && (found ? bounds.min.x : player.transform.position.x) <= platformPosition.x + 10.15f;
                bool ridesPlatform = overlapsWidth
                    && feetY >= platformTop - 0.7f
                    && feetY <= platformTop + 8.5f;
                if (!ridesPlatform) continue;

                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null && body.simulated) body.position += delta;
                else player.transform.position += (Vector3)delta;
            }
        }

        private void BuildRideWalls()
        {
            leftWall = CreateRideWall("Ride Wall Left", -9.65f);
            rightWall = CreateRideWall("Ride Wall Right", 9.65f);
        }

        private void BuildVerticalGuideLights()
        {
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Stage11DarknessController.CreateFixedLamp(
                    platformTransform.parent,
                    new Vector2(HorizontalEndX + side * 7.1f, 3.5f + i * 7.8f));
            }
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            if (online && target.transform != stageManager.ActivePlayerTransform) return;
            string playerId = online
                ? stageManager.GetOnlinePlayerId(target)
                : "local_" + target.GetInstanceID();
            if (string.IsNullOrEmpty(playerId)) return;

            if (online && !HasAuthority)
            {
                SendElimination(EliminationRequestKind, playerId);
                return;
            }

            ApplyElimination(playerId, target);
            if (online) SendElimination(EliminationStateKind, playerId);
            CheckForAllPlayersEliminated();
        }

        private void ApplyElimination(string playerId, PlayerController2D target = null)
        {
            if (string.IsNullOrEmpty(playerId) || !eliminatedPlayerIds.Add(playerId)) return;
            if (target == null) target = FindPlayer(playerId);
            if (target == null) return;
            target.SetControlsEnabled(false);
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        }

        private PlayerController2D FindPlayer(string playerId)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                string candidateId = online
                    ? stageManager.GetOnlinePlayerId(players[i])
                    : "local_" + players[i].GetInstanceID();
                if (candidateId == playerId) return players[i];
            }
            return null;
        }

        private void SendElimination(string kind, string playerId)
        {
            if (onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = EliminationSystemId,
                Kind = kind,
                Json = JsonUtility.ToJson(new EliminationMessage { PlayerId = playerId })
            });
        }

        private void HandleEliminationMessage(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != EliminationSystemId) return;
            EliminationMessage message = JsonUtility.FromJson<EliminationMessage>(data.Json);
            string playerId = message != null && !string.IsNullOrEmpty(message.PlayerId)
                ? message.PlayerId
                : data.PlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            if (data.Kind == EliminationRequestKind && HasAuthority)
            {
                ApplyElimination(playerId);
                SendElimination(EliminationStateKind, playerId);
                CheckForAllPlayersEliminated();
            }
            else if (data.Kind == EliminationStateKind && !HasAuthority)
            {
                ApplyElimination(playerId);
            }
        }

        private void CheckForAllPlayersEliminated()
        {
            if (retryQueued || !HasAuthority || stageManager == null) return;
            int expected = Mathf.Max(1, stageManager.GetInkBudgetPlayerCount());
            if (eliminatedPlayerIds.Count < expected) return;
            retryQueued = true;
            StartCoroutine(ReturnToReadyRoomAfterDelay());
        }

        private IEnumerator ReturnToReadyRoomAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1.25f);
            stageManager?.RetryCurrentStageAfterElimination();
        }

        private GameObject CreateRideWall(string name, float x)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(platformTransform, false);
            wall.transform.localPosition = new Vector3(x, 4.75f, 0f);
            wall.layer = platformTransform.gameObject.layer;
            wall.tag = "Ground";
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.72f, 8.5f);
            GameObject fillObject = new GameObject("Paper Wall Fill");
            fillObject.transform.SetParent(wall.transform, false);
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            fill.sprite = DoodleRuntimeAssets.SquareSprite;
            fill.color = new Color(0.98f, 0.965f, 0.88f, 1f);
            fillObject.transform.localScale = new Vector3(collider.size.x, collider.size.y, 1f);
            fill.sortingOrder = 20;
            AddRectangleOutline(wall.transform, collider.size, new Color(0.13f, 0.14f, 0.16f, 0.95f), 0.09f, 21);
            AddWallHatching(wall.transform, collider.size);
            return wall;
        }

        private static void AddWallHatching(Transform parent, Vector2 size)
        {
            Color pencil = new Color(0.3f, 0.35f, 0.42f, 0.26f);
            for (int i = 0; i < 10; i++)
            {
                float y = Mathf.Lerp(-size.y * 0.43f, size.y * 0.43f, i / 9f);
                GameObject strokeObject = new GameObject("Wall Pencil Hatch");
                strokeObject.transform.SetParent(parent, false);
                LineRenderer stroke = strokeObject.AddComponent<LineRenderer>();
                stroke.material = DoodleRuntimeAssets.LineMaterial;
                stroke.useWorldSpace = false;
                stroke.positionCount = 2;
                stroke.startWidth = 0.035f;
                stroke.endWidth = 0.035f;
                stroke.startColor = pencil;
                stroke.endColor = pencil;
                stroke.sortingOrder = 21;
                stroke.SetPosition(0, new Vector3(-size.x * 0.42f, y - 0.12f, -0.03f));
                stroke.SetPosition(1, new Vector3(size.x * 0.42f, y + 0.12f, -0.03f));
            }
        }

        private void SetRideWallsVisible(bool visible)
        {
            if (leftWall != null && leftWall.activeSelf != visible) leftWall.SetActive(visible);
            if (rightWall != null && rightWall.activeSelf != visible) rightWall.SetActive(visible);
        }

        internal static LineRenderer AddRectangleOutline(
            Transform parent,
            Vector2 size,
            Color color,
            float width,
            int order)
        {
            GameObject lineObject = new GameObject("Pencil Outline");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.material = DoodleRuntimeAssets.LineMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            line.SetPosition(0, new Vector3(-hx, -hy, -0.02f));
            line.SetPosition(1, new Vector3(-hx, hy, -0.02f));
            line.SetPosition(2, new Vector3(hx, hy, -0.02f));
            line.SetPosition(3, new Vector3(hx, -hy, -0.02f));
            return line;
        }
    }

    /// <summary>Marker preventing the ordinary ping-pong mover from owning 11-1's platform.</summary>
    public sealed class StageMovingGauntletPlatform : MonoBehaviour { }

    /// <summary>
    /// Wall-phasing, hand-drawn ghost layered over an ordinary damageable enemy.
    /// Keeping StageEnemyCharacter means bullets, bombs and cat scratches use the
    /// same host-authoritative defeat path as every other placed enemy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageMovingGauntletGhost : MonoBehaviour
    {
        private StageEnemyCharacter enemy;
        private Rigidbody2D body;
        private Collider2D ghostCollider;
        private StageGimmickSyncManager syncManager;
        private StageManager stageManager;
        private float speed;
        private float wobblePhase;
        private float removeAt;
        private bool phasing;
        private SpriteRenderer ghostRenderer;

        private bool HasAuthority => syncManager == null || !syncManager.ShouldAskHost;

        public void Configure(float movementSpeed, bool usePhasing)
        {
            speed = Mathf.Clamp(movementSpeed, 0.9f, 5f);
            phasing = usePhasing;
            ApplyGhostAppearance(1f);
        }

        private void Awake()
        {
            enemy = GetComponent<StageEnemyCharacter>();
            body = GetComponent<Rigidbody2D>();
            ghostCollider = GetComponent<Collider2D>();
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            wobblePhase = Mathf.Abs(gameObject.name.GetHashCode() % 1000) * 0.013f;
            removeAt = Time.time + 14f;
            enemy?.SetStationaryTarget();
            if (ghostCollider != null) ghostCollider.isTrigger = true;
            Transform oldVisual = transform.Find("Enemy Visual");
            if (oldVisual != null) oldVisual.gameObject.SetActive(false);
            BuildGhostVisual();
        }

        private void FixedUpdate()
        {
            if (!HasAuthority || enemy == null || enemy.IsDefeated || body == null) return;
            PlayerController2D target = FindNearestPlayer();
            if (target == null)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 delta = (Vector2)target.transform.position - body.position;
            Vector2 direction = delta.sqrMagnitude > 0.01f ? delta.normalized : Vector2.zero;
            Vector2 side = new Vector2(-direction.y, direction.x)
                * Mathf.Sin(Time.time * 3.1f + wobblePhase) * 0.42f;
            Vector2 next = body.position + (direction * speed + side) * Time.fixedDeltaTime;
            body.MovePosition(next);
        }

        private void Update()
        {
            float visibility = 1f;
            if (phasing)
            {
                float cycle = Mathf.Repeat(Time.time + wobblePhase, 4.2f);
                visibility = cycle < 1.45f
                    ? Mathf.SmoothStep(0.08f, 0.9f, cycle / 0.42f)
                    : cycle < 2.45f
                        ? Mathf.SmoothStep(0.9f, 0.08f, (cycle - 1.45f) / 0.55f)
                        : 0.08f;
                if (ghostCollider != null) ghostCollider.enabled = visibility > 0.48f;
                ApplyGhostAppearance(visibility);
            }
            Transform visual = transform.Find("11-1 Ghost Visual");
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 4f + wobblePhase) * 0.1f, 0f);
                float squash = 1f + Mathf.Sin(Time.time * 3.2f + wobblePhase) * 0.045f;
                visual.localScale = new Vector3(2f - squash, squash, 1f);
            }
            if (!HasAuthority || Time.time < removeAt || enemy == null || enemy.IsDefeated) return;
            syncManager?.RemoveDropperEnemy(enemy.ObjectId);
        }

        private PlayerController2D FindNearestPlayer()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            PlayerController2D nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy) continue;
                float sqr = ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr >= best) continue;
                best = sqr;
                nearest = player;
            }
            return nearest;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null || enemy == null || enemy.IsDefeated) return;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager != null && stageManager.IsOnlineStageActive
                && player.transform != stageManager.ActivePlayerTransform) return;
            stageManager?.RespawnFromHazard(player);
        }

        private void BuildGhostVisual()
        {
            Transform root = new GameObject("11-1 Ghost Visual").transform;
            root.SetParent(transform, false);
            Sprite sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/enemy-ghost");
            if (sprite == null) sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/enemy-flyer");
            if (sprite == null) return;

            GameObject spriteObject = new GameObject("Child Pencil Ghost Sprite");
            spriteObject.transform.SetParent(root, false);
            spriteObject.transform.localPosition = new Vector3(0f, 0.08f, -0.03f);
            ghostRenderer = spriteObject.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = sprite;
            ghostRenderer.color = new Color(1f, 1f, 1f, 0.92f);
            ghostRenderer.sortingOrder = 40;
            Vector2 bounds = sprite.bounds.size;
            spriteObject.transform.localScale = new Vector3(
                2.15f / Mathf.Max(0.01f, bounds.x),
                2.35f / Mathf.Max(0.01f, bounds.y),
                1f);
        }

        private void ApplyGhostAppearance(float visibility)
        {
            if (ghostRenderer == null) return;
            Color tint = phasing
                ? new Color(0.56f, 0.82f, 1f, 0.9f * visibility)
                : new Color(1f, 1f, 1f, 0.92f);
            ghostRenderer.color = tint;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageFlashlight : MonoBehaviour
    {
        private PlayerCarryController holder;

        public Vector2 BeamOrigin => (Vector2)transform.position + (Vector2)transform.right * 0.72f;
        public Vector2 BeamDirection => transform.right;

        public static StageFlashlight Create(Transform parent, Vector2 position)
        {
            Transform existing = parent != null ? parent.Find("11-1 Flashlight") : null;
            if (existing != null && existing.gameObject.activeInHierarchy)
                return existing.GetComponent<StageFlashlight>();
            GameObject root = new GameObject("11-1 Flashlight");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = 9;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 0.7f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.25f, 0.58f);
            root.AddComponent<CarryableObject>();

            Sprite flashlightSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/flashlight");
            GameObject art = new GameObject("Colored Pencil Flashlight Art");
            art.transform.SetParent(root.transform, false);
            SpriteRenderer artRenderer = art.AddComponent<SpriteRenderer>();
            artRenderer.sprite = flashlightSprite;
            artRenderer.sortingOrder = 49;
            if (flashlightSprite != null)
            {
                art.transform.localScale = new Vector3(
                    1.5f / Mathf.Max(0.01f, flashlightSprite.bounds.size.x),
                    0.78f / Mathf.Max(0.01f, flashlightSprite.bounds.size.y),
                    1f);
            }

            StageFlashlight flashlight = root.AddComponent<StageFlashlight>();
            return flashlight;
        }

        public void SetHolder(PlayerCarryController value) => holder = value;

        public void UpdateHeldPose(Vector3 anchor, Vector2 aimWorld)
        {
            transform.position = anchor;
            Vector2 direction = aimWorld - (Vector2)anchor;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }

    public sealed class Stage11FixedLight : MonoBehaviour
    {
        public float Radius = 2.15f;
        public float Feather = 1.35f;
    }

    [DisallowMultipleComponent]
    public sealed class Stage11DarknessController : MonoBehaviour
    {
        private const int MaxPointLights = 16;
        private readonly Vector4[] pointLights = new Vector4[MaxPointLights];
        private Material darknessMaterial;

        public static Stage11DarknessController Ensure(Transform parent)
        {
            Stage11DarknessController existing = parent != null
                ? parent.GetComponentInChildren<Stage11DarknessController>(false)
                : null;
            if (existing != null) return existing;
            GameObject root = new GameObject("11-1 Darkness");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(80f, 24f, 0f);
            root.transform.localScale = new Vector3(280f, 105f, 1f);
            SpriteRenderer darkness = root.AddComponent<SpriteRenderer>();
            darkness.sprite = DoodleRuntimeAssets.SquareSprite;
            darkness.color = Color.black;
            darkness.sortingOrder = 180;
            darkness.maskInteraction = SpriteMaskInteraction.None;
            Shader shader = Resources.Load<Shader>("Shaders/Stage11Darkness");
            if (shader == null) shader = Shader.Find("DrawBody/Stage11Darkness");
            if (shader != null)
            {
                Material material = new Material(shader) { name = "11-1 Soft Darkness Material" };
                darkness.material = material;
                Stage11DarknessController controller = root.AddComponent<Stage11DarknessController>();
                controller.darknessMaterial = material;
                return controller;
            }
            return root.AddComponent<Stage11DarknessController>();
        }

        public static void CreateFixedLamp(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("11-1 Guide Lamp");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<Stage11FixedLight>();
            Sprite lampSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/guide-lamp");
            GameObject lampArt = new GameObject("Colored Pencil Guide Lamp Art");
            lampArt.transform.SetParent(root.transform, false);
            SpriteRenderer lampRenderer = lampArt.AddComponent<SpriteRenderer>();
            lampRenderer.sprite = lampSprite;
            lampRenderer.sortingOrder = 175;
            if (lampSprite != null)
            {
                lampArt.transform.localScale = new Vector3(
                    2.2f / Mathf.Max(0.01f, lampSprite.bounds.size.x),
                    2.2f / Mathf.Max(0.01f, lampSprite.bounds.size.y),
                    1f);
            }
        }

        private void Update()
        {
            if (darknessMaterial == null) return;
            int lightCount = 0;
            Stage11FixedLight[] fixedLights = Object.FindObjectsByType<Stage11FixedLight>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < fixedLights.Length && lightCount < MaxPointLights; i++)
            {
                Stage11FixedLight light = fixedLights[i];
                if (light == null || !light.transform.IsChildOf(transform.parent)) continue;
                Vector2 position = light.transform.position;
                pointLights[lightCount++] = new Vector4(position.x, position.y, light.Radius, light.Feather);
            }

            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length && lightCount < MaxPointLights; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || player.CurrentSpecies != DrawManager.Species.Slime) continue;
                Vector2 position = (Vector2)player.transform.position + Vector2.up * 0.35f;
                pointLights[lightCount++] = new Vector4(position.x, position.y, 4.1f, 1.8f);
            }

            StageFlashlight flashlight = Object.FindFirstObjectByType<StageFlashlight>();
            if (flashlight != null && flashlight.gameObject.activeInHierarchy)
            {
                Vector2 origin = flashlight.BeamOrigin;
                Vector2 direction = flashlight.BeamDirection.normalized;
                // A soft pool around the handle keeps its holder and a little of
                // the space behind them visible, independently of the beam aim.
                if (lightCount < MaxPointLights)
                    pointLights[lightCount++] = new Vector4(origin.x, origin.y, 2.65f, 1.5f);
                darknessMaterial.SetVector("_ConeOriginDirection",
                    new Vector4(origin.x, origin.y, direction.x, direction.y));
                darknessMaterial.SetVector("_ConeShape", new Vector4(28f, 6.2f, 2.6f, 1f));
            }
            else darknessMaterial.SetVector("_ConeShape", Vector4.zero);

            darknessMaterial.SetInt("_PointLightCount", lightCount);
            darknessMaterial.SetVectorArray("_PointLights", pointLights);
        }

        private void OnDestroy()
        {
            if (darknessMaterial != null) Destroy(darknessMaterial);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageMovingGauntletVerticalGhost : MonoBehaviour
    {
        private float fixedX;
        private float minY;
        private float maxY;
        private float speed;
        private float phaseOffset;
        private StageManager stageManager;

        public static StageMovingGauntletVerticalGhost Create(
            Transform parent, float x, float lowerY, float upperY, float movementSpeed, float phase)
        {
            GameObject root = new GameObject("11-1 Vertical Invulnerable Ghost");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(x, lowerY, 0f);
            CapsuleCollider2D hitbox = root.AddComponent<CapsuleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(2.35f, 3.2f);

            Sprite sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/enemy-ghost");
            GameObject art = new GameObject("Vertical Ghost Art");
            art.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.48f, 0.78f, 0.82f);
            renderer.sortingOrder = 72;
            if (sprite != null)
                art.transform.localScale = new Vector3(3.05f / sprite.bounds.size.x, 3.5f / sprite.bounds.size.y, 1f);

            StageMovingGauntletVerticalGhost ghost = root.AddComponent<StageMovingGauntletVerticalGhost>();
            ghost.fixedX = x;
            ghost.minY = lowerY;
            ghost.maxY = upperY;
            ghost.speed = movementSpeed;
            ghost.phaseOffset = phase;
            return ghost;
        }

        private void Update()
        {
            float range = Mathf.Max(0.1f, maxY - minY);
            float distance = Mathf.Repeat((Time.time + phaseOffset) * speed, range * 2f);
            float y = distance <= range ? minY + distance : maxY - (distance - range);
            transform.position = new Vector3(fixedX, y, transform.position.z);
            Transform art = transform.Find("Vertical Ghost Art");
            if (art != null) art.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.8f + phaseOffset) * 4f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null) return;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager != null && stageManager.IsOnlineStageActive
                && player.transform != stageManager.ActivePlayerTransform) return;
            Object.FindFirstObjectByType<StageMovingGauntletController>()?.RequestElimination(player);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageMovingGauntletGiantGhost : MonoBehaviour
    {
        private bool activeChase;
        private float startedAt;
        private StageManager stageManager;

        public static StageMovingGauntletGiantGhost Create(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("11-1 Giant Unstoppable Ghost");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            CapsuleCollider2D hitbox = root.AddComponent<CapsuleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(7.4f, 10.5f);
            Sprite sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/enemy-ghost-giant");
            GameObject art = new GameObject("Giant Ghost Art");
            art.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 75;
            if (sprite != null)
            {
                art.transform.localScale = new Vector3(12f / sprite.bounds.size.x, 12f / sprite.bounds.size.y, 1f);
            }
            return root.AddComponent<StageMovingGauntletGiantGhost>();
        }

        public void SetChaseActive(bool value)
        {
            if (value && !activeChase) startedAt = Time.time;
            activeChase = value;
        }

        private void Update()
        {
            if (!activeChase) return;
            float speed = Mathf.Lerp(3.25f, 5.525f, Mathf.Clamp01((Time.time - startedAt) / 42f));
            transform.position += Vector3.right * speed * Time.deltaTime;
            Transform art = transform.Find("Giant Ghost Art");
            if (art != null)
            {
                art.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.4f) * 0.34f, 0f);
                art.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.7f) * 2.5f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null) return;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            if (stageManager != null && stageManager.IsOnlineStageActive
                && player.transform != stageManager.ActivePlayerTransform) return;
            Object.FindFirstObjectByType<StageMovingGauntletController>()?.RequestElimination(player);
        }
    }
}
