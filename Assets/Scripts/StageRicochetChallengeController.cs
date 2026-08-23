using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageRicochetChallengeController : MonoBehaviour
    {
        private const string StageId = "10-3";
        private const string StateKind = "ricochet_state";
        private const int TotalRounds = 5;
        private const int ShotsPerRound = 3;

        [System.Serializable]
        private sealed class ChallengeState
        {
            public int Sequence;
            public int RoundVersion;
            public int Round;
            public int Ammo;
            public int Phase;
            public int EnemyPattern;
            public Vector2 GunPosition;
            public Vector2 EnemyPosition;
        }

        private readonly HashSet<StageGunBullet> activeBullets = new HashSet<StageGunBullet>();
        private readonly List<Vector2> cellCenters = new List<Vector2>();
        private readonly List<float> cellFloors = new List<float>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private StageGun activeGun;
        private StageRicochetTarget activeTarget;
        private TextMesh titleText;
        private TextMesh roundText;
        private TextMesh ammoText;
        private TextMesh helpText;
        private ChallengeState state = new ChallengeState();
        private int playerCount;
        private int appliedRoundVersion = -1;
        private float phaseEndsAt;
        private float nextStateBroadcastAt;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool cameraFollowWasEnabled;
        private bool cameraLocked;
        private StageRicochetBulletPassage verticalPassage;
        private StageRicochetBulletPassage horizontalPassage;
        private StageRicochetBulletPassage leftPassage;
        private StageRicochetBulletPassage rightPassage;
        private StageRicochetBulletPassage ceilingPassage;
        private StageRicochetBulletPassage floorPassage;

        public bool IsRoundActive => state != null && state.Phase == 0;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            gameCamera = Camera.main;
            cameraFollow = gameCamera != null ? gameCamera.GetComponent<CameraFollow2D>() : null;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            RestoreCamera();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            int reportedPlayers = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            int spawnedPlayers = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None).Length;
            playerCount = Mathf.Clamp(Mathf.Max(reportedPlayers, spawnedPlayers), 1, 4);
            BuildArena();
            LockCameraToArena();
            PositionLocalPlayers();
            CreateMonitor();
            if (HasAuthority()) BeginRound(1);
            RefreshMonitor();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId || !HasAuthority()) return;

            activeBullets.RemoveWhere(bullet => bullet == null);
            if (state.Phase == 0 && state.Ammo <= 0 && activeBullets.Count == 0)
            {
                state.Phase = 2;
                phaseEndsAt = Time.time + 2.2f;
                BroadcastState(true);
                RefreshMonitor();
            }
            else if (state.Phase == 1 && Time.time >= phaseEndsAt)
            {
                if (state.Round >= TotalRounds)
                {
                    state.Phase = 3;
                    BroadcastState(true);
                    RefreshMonitor();
                    stageManager.ClearStage();
                }
                else
                {
                    BeginRound(state.Round + 1);
                }
            }
            else if (state.Phase == 2 && Time.time >= phaseEndsAt)
            {
                stageManager.Retry();
            }

            BroadcastState(false);
        }

        public bool TryConsumeShot()
        {
            if (!HasAuthority() || !IsRoundActive || state.Ammo <= 0) return false;
            state.Ammo--;
            BroadcastState(true);
            RefreshMonitor();
            return true;
        }

        public void RegisterBullet(StageGunBullet bullet)
        {
            if (HasAuthority() && bullet != null) activeBullets.Add(bullet);
        }

        public void UnregisterBullet(StageGunBullet bullet)
        {
            if (bullet != null) activeBullets.Remove(bullet);
        }

        public bool HandleTargetHit(StageRicochetTarget target, int reflectionCount, Vector2 hitPoint)
        {
            if (!HasAuthority() || !IsRoundActive || target == null || target != activeTarget) return true;
            int requiredReflections = playerCount > 1 ? 1 : 0;
            if (reflectionCount < requiredReflections)
            {
                GameSfx.PlayAt(SfxId.EnemyShellBounce, hitPoint, 0.7f);
                return true;
            }

            target.Defeat(hitPoint);
            state.Phase = 1;
            phaseEndsAt = Time.time + 1.35f;
            BroadcastState(true);
            RefreshMonitor();
            return true;
        }

        public void NotifyReflection(Vector2 point)
        {
            GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.55f);
        }

        private void BeginRound(int round)
        {
            RemoveRoundObjects();
            state.Round = Mathf.Clamp(round, 1, TotalRounds);
            state.RoundVersion++;
            state.Ammo = ShotsPerRound;
            state.Phase = 0;
            state.EnemyPattern = Random.Range(0, 3);

            // The gun must always remain reachable. Players are assigned to the
            // first N cells, while the target may use any of the four rooms.
            int occupiedCellCount = Mathf.Clamp(playerCount, 1, cellCenters.Count);
            int gunCell = Random.Range(0, occupiedCellCount);

            state.GunPosition = RandomPointInCell(gunCell, true);
            if (playerCount >= 4)
            {
                state.EnemyPosition = RandomExternalTargetPoint(gunCell);
            }
            else
            {
                // Empty rooms are used as target rooms, so the gun never competes
                // with an enemy in a player's starting room.
                int enemyCell = Random.Range(occupiedCellCount, cellCenters.Count);
                state.EnemyPosition = RandomPointInCell(enemyCell, false);
            }
            ApplyRoundState();
            BroadcastState(true);
            RefreshMonitor();
        }

        private Vector2 RandomPointInCell(int index, bool gun)
        {
            Vector2 center = cellCenters[Mathf.Clamp(index, 0, cellCenters.Count - 1)];
            float floor = cellFloors[Mathf.Clamp(index, 0, cellFloors.Count - 1)];
            float horizontal = 3.3f;
            float x = center.x + Random.Range(-horizontal, horizontal);
            return new Vector2(x, floor + (gun ? 0.72f : 0.9f));
        }

        private Vector2 RandomExternalTargetPoint(int gunCell)
        {
            gunCell = Mathf.Clamp(gunCell, 0, cellCenters.Count - 1);
            Vector2 roomCenter = cellCenters[gunCell];
            bool leftColumn = gunCell == 0 || gunCell == 2;
            bool topRow = gunCell == 0 || gunCell == 1;

            // Always send the shot through at least one other player's room.
            // This keeps the four-player version a real body-reflection relay.
            if (Random.value < 0.5f)
            {
                return new Vector2(leftColumn ? 17.55f : -17.55f, roomCenter.y);
            }
            return new Vector2(roomCenter.x, topRow ? -9.35f : 9.35f);
        }

        private void ApplyRoundState()
        {
            if (state == null || state.RoundVersion == appliedRoundVersion) return;
            appliedRoundVersion = state.RoundVersion;
            RemoveRoundObjects();
            ConfigureBulletPassages();

            StageObjectData gunData = StageObjectFactory.CreateDefaultData(StageObjectType.Handgun, state.GunPosition);
            gunData.objectId = StageId + "_gun_round_" + state.RoundVersion;
            gunData.size = new Vector2(0.82f, 0.48f);
            GameObject gunObject = factory != null ? factory.Create(gunData, transform) : null;
            activeGun = gunObject != null ? gunObject.GetComponent<StageGun>() : null;

            StageObjectType enemyType = state.EnemyPattern == 1
                ? StageObjectType.EnemyJumper
                : state.EnemyPattern == 2 ? StageObjectType.EnemyCharger : StageObjectType.EnemyWalker;
            StageObjectData enemyData = StageObjectFactory.CreateDefaultData(enemyType, state.EnemyPosition);
            enemyData.objectId = StageId + "_target_round_" + state.RoundVersion;
            enemyData.size = Vector2.one * 1.15f;
            enemyData.movementSpeed = 0.5f;
            GameObject enemyObject = factory != null ? factory.Create(enemyData, transform) : null;
            if (enemyObject != null)
            {
                StageEnemyCharacter enemy = enemyObject.GetComponent<StageEnemyCharacter>();
                enemy?.SetStationaryTarget();
                activeTarget = enemyObject.AddComponent<StageRicochetTarget>();
                activeTarget.Configure(this, enemy);
                CreateTargetRing(enemyObject.transform);
            }
        }

        private void RemoveRoundObjects()
        {
            PlayerCarryController[] carries = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carries.Length; i++) carries[i]?.ForceDrop();
            if (activeGun != null)
            {
                activeGun.gameObject.SetActive(false);
                Destroy(activeGun.gameObject);
                activeGun = null;
            }
            if (activeTarget != null)
            {
                activeTarget.gameObject.SetActive(false);
                Destroy(activeTarget.gameObject);
                activeTarget = null;
            }
            StageGunBullet[] bullets = Object.FindObjectsByType<StageGunBullet>(FindObjectsSortMode.None);
            for (int i = 0; i < bullets.Length; i++) if (bullets[i] != null) Destroy(bullets[i].gameObject);
            activeBullets.Clear();
        }

        private void BuildArena()
        {
            cellCenters.Clear();
            cellFloors.Clear();
            verticalPassage = CreateDivider("Center Vertical", Vector2.zero, new Vector2(0.62f, 16.6f));
            horizontalPassage = CreateDivider("Center Horizontal", Vector2.zero, new Vector2(32.6f, 0.62f));
            FindOuterPassages();
            AddCell(new Vector2(-8f, 4.3f), 0.31f);
            AddCell(new Vector2(8f, 4.3f), 0.31f);
            AddCell(new Vector2(-8f, -4.3f), -8.3f);
            AddCell(new Vector2(8f, -4.3f), -8.3f);
        }

        private void AddCell(Vector2 center, float floor)
        {
            cellCenters.Add(center);
            cellFloors.Add(floor);
        }

        private StageRicochetBulletPassage CreateDivider(string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name) { layer = 6, tag = "Ground" };
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.AddComponent<BoxCollider2D>().size = size;
            StageRicochetBulletPassage passage = root.AddComponent<StageRicochetBulletPassage>();
            Color fill = new Color(0.72f, 0.9f, 1f, 0.9f);
            Color ink = new Color(0.08f, 0.38f, 0.72f, 1f);
            StageEscortController.AddFilledRect(root.transform, "Bullet Glass", Vector2.zero, size, fill, 12);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, ink, 13);
            float length = size.x > size.y ? size.x : size.y;
            bool horizontal = size.x > size.y;
            for (float value = -length * 0.42f; value <= length * 0.42f; value += 1.25f)
            {
                Vector2 from = horizontal ? new Vector2(value - 0.28f, 0f) : new Vector2(0f, value - 0.28f);
                Vector2 to = horizontal ? new Vector2(value + 0.28f, 0f) : new Vector2(0f, value + 0.28f);
                StageEscortController.AddLine(root.transform, from, to, 0.085f, new Color(0.1f, 0.6f, 0.95f, 0.75f), 14);
            }
            return passage;
        }

        private void FindOuterPassages()
        {
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject editorObject = objects[i];
                if (editorObject == null) continue;
                switch (editorObject.objectId)
                {
                    case "10-3_left_wall": leftPassage = GetOrAddPassage(editorObject.gameObject); break;
                    case "10-3_right_wall": rightPassage = GetOrAddPassage(editorObject.gameObject); break;
                    case "10-3_ceiling": ceilingPassage = GetOrAddPassage(editorObject.gameObject); break;
                    case "10-3_floor": floorPassage = GetOrAddPassage(editorObject.gameObject); break;
                }
            }
        }

        private static StageRicochetBulletPassage GetOrAddPassage(GameObject target)
        {
            StageRicochetBulletPassage passage = target.GetComponent<StageRicochetBulletPassage>();
            return passage != null ? passage : target.AddComponent<StageRicochetBulletPassage>();
        }

        private void ConfigureBulletPassages()
        {
            SetPassage(verticalPassage, Mathf.Sign(state.GunPosition.x) != Mathf.Sign(state.EnemyPosition.x));
            SetPassage(horizontalPassage, Mathf.Sign(state.GunPosition.y) != Mathf.Sign(state.EnemyPosition.y));
            SetPassage(leftPassage, state.EnemyPosition.x < -16.3f);
            SetPassage(rightPassage, state.EnemyPosition.x > 16.3f);
            SetPassage(ceilingPassage, state.EnemyPosition.y > 8.3f);
            SetPassage(floorPassage, state.EnemyPosition.y < -8.3f);
        }

        private static void SetPassage(StageRicochetBulletPassage passage, bool allowsBullet)
        {
            if (passage != null) passage.SetAllowsBullet(allowsBullet);
        }

        private void PositionLocalPlayers()
        {
            if (stageManager == null || cellCenters.Count == 0) return;
            if (stageManager.IsOnlineStageActive)
            {
                int index = 0;
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                if (roster != null)
                    for (int i = 0; i < roster.Length; i++)
                        if (roster[i] != null && roster[i].PlayerId == onlineManager.LocalPlayerId) { index = i; break; }
                PlayerController2D local = stageManager.ActivePlayerTransform != null
                    ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
                PlacePlayer(local, index);
                return;
            }

            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            for (int i = 0; i < players.Length && i < cellCenters.Count; i++) PlacePlayer(players[i], i);
        }

        private void PlacePlayer(PlayerController2D player, int index)
        {
            if (player == null) return;
            index = Mathf.Clamp(index, 0, cellCenters.Count - 1);
            Vector2 destination = new Vector2(cellCenters[index].x, cellFloors[index] + 3f);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = destination;
                body.linearVelocity = Vector2.zero;
            }
            player.transform.position = destination;
            Physics2D.SyncTransforms();

            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            float lowestPoint = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                lowestPoint = Mathf.Min(lowestPoint, collider.bounds.min.y);
            }
            if (!float.IsPositiveInfinity(lowestPoint))
            {
                destination.y += cellFloors[index] + 0.06f - lowestPoint;
                if (body != null) body.position = destination;
                player.transform.position = destination;
                Physics2D.SyncTransforms();
            }
            player.ResetMotion();
        }

        private void LockCameraToArena()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null || cameraLocked) return;

            cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            if (cameraFollow != null)
            {
                cameraFollowWasEnabled = cameraFollow.enabled;
                cameraFollow.enabled = false;
            }

            gameCamera.transform.position = new Vector3(0f, 0f, previousCameraPosition.z);
            // Keep the complete 34 x 18 arena visible even on narrower window aspects.
            gameCamera.orthographicSize = Mathf.Max(10.6f, 19f / Mathf.Max(0.1f, gameCamera.aspect));
            cameraLocked = true;
        }

        private void RestoreCamera()
        {
            if (!cameraLocked || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = cameraFollowWasEnabled;
            cameraLocked = false;
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("10-3 Ricochet Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(0f, 7.15f, 0.4f);
            StageEscortController.AddFilledRect(monitor.transform, "Frame", Vector2.zero, new Vector2(12f, 2.6f), new Color(0.18f, 0.22f, 0.27f, 0.92f), -32);
            StageEscortController.AddFilledRect(monitor.transform, "Screen", Vector2.zero, new Vector2(11.3f, 2f), new Color(0.01f, 0.035f, 0.045f, 0.94f), -31);
            titleText = StageEscortController.CreateText(monitor.transform, "Title", new Vector3(0f, 0.68f, -0.02f), 48, 0.12f, new Color(0.55f, 0.9f, 1f), -28);
            roundText = StageEscortController.CreateText(monitor.transform, "Round", new Vector3(-2.5f, 0f, -0.03f), 58, 0.15f, new Color(0.2f, 1f, 0.72f), -27);
            ammoText = StageEscortController.CreateText(monitor.transform, "Ammo", new Vector3(2.5f, 0f, -0.03f), 58, 0.15f, new Color(1f, 0.75f, 0.2f), -27);
            helpText = StageEscortController.CreateText(monitor.transform, "Help", new Vector3(0f, -0.7f, -0.04f), 42, 0.09f, new Color(1f, 0.88f, 0.42f), -26);
        }

        private void RefreshMonitor()
        {
            if (titleText == null || state == null) return;
            titleText.text = LocalizationManager.T("ricochet_title");
            roundText.text = LocalizationManager.Format("ricochet_round", state.Round, TotalRounds);
            ammoText.text = LocalizationManager.Format("ricochet_ammo", state.Ammo);
            helpText.text = LocalizationManager.T(state.Phase == 2
                ? "ricochet_failed" : state.Phase == 3 ? "ricochet_clear" : "ricochet_instruction");
        }

        private void CreateTargetRing(Transform target)
        {
            const int segments = 28;
            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.82f;
            }
            StageGun.AddLine(target, "Round Target Ring", points, 0.075f, new Color(1f, 0.2f, 0.16f, 0.9f), 42);
        }

        private void BroadcastState(bool immediate)
        {
            if (!HasAuthority() || onlineManager == null || !stageManager.IsOnlineStageActive) return;
            if (!immediate && Time.time < nextStateBroadcastAt) return;
            nextStateBroadcastAt = Time.time + 0.75f;
            state.Sequence++;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId || message.Kind != StateKind || HasAuthority()
                || onlineManager == null || !onlineManager.IsHostPlayer(message.PlayerId)) return;
            ChallengeState incoming = JsonUtility.FromJson<ChallengeState>(message.Json);
            if (incoming == null || incoming.Sequence <= state.Sequence) return;
            state = incoming;
            ApplyRoundState();
            RefreshMonitor();
        }

        private bool HasAuthority() => stageManager == null || !stageManager.IsOnlineStageActive || stageManager.IsOnlineStageHost;
    }

    public sealed class StageRicochetBulletPassage : MonoBehaviour
    {
        public bool AllowsBullet { get; private set; }

        public void SetAllowsBullet(bool allowsBullet)
        {
            AllowsBullet = allowsBullet;
        }
    }

    public sealed class StageRicochetTarget : MonoBehaviour
    {
        private StageRicochetChallengeController controller;
        private StageEnemyCharacter enemy;

        public void Configure(StageRicochetChallengeController owner, StageEnemyCharacter targetEnemy)
        {
            controller = owner;
            enemy = targetEnemy;
        }

        public bool Hit(int reflectionCount, Vector2 point)
        {
            return controller == null || controller.HandleTargetHit(this, reflectionCount, point);
        }

        public void Defeat(Vector2 point)
        {
            enemy?.RequestDefeat();
            GameSfx.PlayAt(SfxId.EnemyDefeat, point, 0.9f);
        }
    }
}
