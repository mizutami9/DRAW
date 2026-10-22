using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageIcicleSeesawSurvivalController : StageEliminationChallengeController
    {
        private const string StageId = "14-1";
        private const string StateKind = "icicle_seesaw_state";
        private const string HazardKind = "icicle_seesaw_hazard";
        private const string EliminateRequestKind = "icicle_seesaw_eliminate_request";
        private const string EliminatedKind = "icicle_seesaw_eliminated";
        private const float SeesawY = -2f;
        private const float SeesawWidth = 30f;
        private const float SeesawHalfWidth = SeesawWidth * 0.5f;
        private const float SeesawThickness = 0.3f;
        private const float SeesawSurfaceY = SeesawThickness * 0.5f;
        // Deliberately allows an almost vertical board. If everyone crowds one
        // side, the seesaw can dump the whole team instead of stopping safely.
        private const float MaximumTilt = 86f;
        private const float CountdownSeconds = 3f;
        private const int MaximumFreezePatchCount = 8;
        private const float FreezePatchWidth = 1.8f;
        private const int MaximumSettledSnowCount = 420;

        private enum Phase
        {
            Waiting,
            Countdown,
            Running,
            Failed,
            Finished
        }

        private enum BlizzardPhase
        {
            None,
            Warning,
            Active
        }

        internal enum HazardType
        {
            Icicle,
            FrostIcicle,
            ShockIcicle,
            EnemyDrop
        }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int CurrentPhase;
            public float RemainingSeconds;
            public float CountdownRemaining;
            public float SeesawAngle;
            public float[] FreezeLocalX;
            public float[] FreezeRemaining;
            public int BlizzardSequence;
            public int CurrentBlizzardPhase;
            public float BlizzardRemaining;
            public Vector2 BlizzardDirection;
            public float BlizzardStrength;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class HazardState
        {
            public int Sequence;
            public int Type;
            public float X;
            public float WarningSeconds;
            public float FallSpeed;
        }

        [System.Serializable]
        private sealed class EliminationState
        {
            public string PlayerId;
        }

        private sealed class FreezePatch
        {
            public Transform Root;
            public SpriteRenderer Fill;
            public BoxCollider2D Collider;
            public float LocalX;
            public float Remaining;
        }

        private readonly List<FreezePatch> freezePatches = new List<FreezePatch>();
        private readonly List<Stage14SnowParticle> snowParticles = new List<Stage14SnowParticle>();
        private readonly List<LineRenderer> blizzardLines = new List<LineRenderer>();
        private readonly List<SpriteRenderer> blizzardFlakes = new List<SpriteRenderer>();
        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageLoader stageLoader;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager gimmickSyncManager;
        private CameraFollow2D cameraFollow;
        private UIManager uiManager;
        private Transform seesawRoot;
        private Rigidbody2D seesawBody;
        private Collider2D seesawCollider;
        private TextMesh monitorText;
        private GameObject blizzardVisualRoot;
        private SpriteRenderer blizzardVeil;
        private TextMesh blizzardDirectionText;
        private Phase phase = Phase.Waiting;
        private float durationSeconds = 60f;
        private float remainingSeconds = 60f;
        private float countdownRemaining = CountdownSeconds;
        private float failureRemaining;
        private float elapsedSeconds;
        private float nextHazardAt = 1.4f;
        private float currentAngle;
        private float networkAngle;
        private float impactTilt;
        private float previousCameraMinimum = 8f;
        private float nextStateAt;
        private float nextSnowParticleAt;
        private float nextBlizzardAt = 5f;
        private float blizzardRemaining;
        private float blizzardStrength;
        private Vector2 blizzardDirection;
        private int stateSequence;
        private int lastStateSequence;
        private int hazardSequence;
        private int lastHazardSequence;
        private int snowParticleSequence;
        private int blizzardSequence;
        private int lastPresentedBlizzardSequence;
        private string lastCountdownText = string.Empty;
        private bool configured;
        private bool playersPlaced;
        private bool restoredPlayers;
        private BlizzardPhase blizzardPhase;

        public override bool UsesGlobalFallBoundary => false;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        public void Configure(float seconds)
        {
            durationSeconds = Mathf.Clamp(seconds > 0f ? seconds : 60f, 30f, 180f);
            remainingSeconds = durationSeconds;
            configured = true;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            gimmickSyncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (cameraFollow != null) cameraFollow.SetMinimumOrthographicSize(previousCameraMinimum);
            uiManager?.SetChallengeCountdown(false, string.Empty);
            RestorePlayers();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            if (!configured) Configure(60f);
            BuildArena();
            CaptureParticipants();
            stageLoader?.SetRuntimeSpawnPosition(new Vector2(0f, SeesawY + 1.5f));
            if (cameraFollow != null)
            {
                previousCameraMinimum = cameraFollow.MinimumOrthographicSize;
                cameraFollow.SetMinimumOrthographicSize(9.5f);
            }
            SetLocalControls(false);
            RefreshPresentation();
            StartCoroutine(PlacePlayersWhenReady());
        }

        private IEnumerator PlacePlayersWhenReady()
        {
            // The ready-room component can become active one frame after this
            // controller starts, so wait for gameplay as well as room closure.
            while (stageManager != null
                && (!stageManager.IsGameplayActive || stageManager.IsChallengeReadyRoomActive))
                yield return null;
            yield return new WaitForFixedUpdate();
            PlacePlayersOnSeesaw();
            playersPlaced = true;
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            UpdateFreezePatches();
            UpdateSnowSurfaceState();
            UpdateAmbientSnow();
            RefreshBlizzardPresentation();
            ApplyPendingEliminations();

            if (phase == Phase.Waiting)
            {
                if (!stageManager.IsGameplayActive || stageManager.IsChallengeReadyRoomActive || !playersPlaced)
                {
                    RefreshPresentation();
                    return;
                }
                phase = Phase.Countdown;
                countdownRemaining = CountdownSeconds;
                BroadcastState(true);
            }

            CheckFalls();

            if (!HasAuthority)
            {
                UpdateReplicaClock();
                RefreshPresentation();
                return;
            }

            if (phase == Phase.Countdown)
            {
                countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.deltaTime);
                if (countdownRemaining <= 0f)
                {
                    phase = Phase.Running;
                    SetLocalControls(true);
                    GameSfx.Play(SfxId.StageCountdownGo);
                    BroadcastState(true);
                }
            }
            else if (phase == Phase.Running)
            {
                elapsedSeconds += Time.deltaTime;
                if (!LocalMultiplayerDebugMode.NoTimeLimit)
                    remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
                UpdateBlizzardAuthority();
                DirectHazards();

                if (AreAllEliminated())
                {
                    BeginFailure();
                }
                else if (remainingSeconds <= 0f && !LocalMultiplayerDebugMode.NoTimeLimit)
                {
                    phase = Phase.Finished;
                    BroadcastState(true);
                    stageManager.ClearStage();
                }
            }
            else if (phase == Phase.Failed)
            {
                failureRemaining = Mathf.Max(0f, failureRemaining - Time.deltaTime);
                if (failureRemaining <= 0f)
                {
                    // Retry rebuilds the ready room immediately. Do not let this
                    // obsolete controller paint GAME OVER over the new room.
                    uiManager?.SetChallengeCountdown(false, string.Empty);
                    stageManager.Retry();
                    return;
                }
            }

            BroadcastState();
            RefreshPresentation();
        }

        private void FixedUpdate()
        {
            if (seesawBody == null || stageManager == null || stageManager.CurrentStageId != StageId) return;
            float targetAngle = HasAuthority ? CalculateHostTargetAngle() : networkAngle;
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, 70f * Time.fixedDeltaTime);
            seesawBody.MoveRotation(currentAngle);
            seesawBody.angularVelocity = 0f;
            impactTilt = Mathf.MoveTowards(impactTilt, 0f, 24f * Time.fixedDeltaTime);
            if (blizzardPhase == BlizzardPhase.Active) ApplyBlizzardPush();
        }

        private float CalculateHostTargetAngle()
        {
            if (phase != Phase.Running && phase != Phase.Countdown) return impactTilt;
            float torque = 0f;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || IsEliminated(player) || !player.gameObject.activeInHierarchy) continue;
                Vector3 local = seesawRoot.InverseTransformPoint(player.transform.position);
                if (Mathf.Abs(local.x) > SeesawHalfWidth + 1f || local.y < -0.4f || local.y > 3.2f) continue;
                if (!player.IsGrounded && local.y > 1.8f) continue;

                PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
                float ink = ability != null ? Mathf.Max(0f, ability.CurrentProfile.TotalInk) : 175f;
                float inkWeight = Mathf.Clamp(0.55f + ink / 170f, 0.55f, 3.5f);
                torque += Mathf.Clamp(local.x / SeesawHalfWidth, -1f, 1f) * inkWeight;
            }
            return Mathf.Clamp(-torque * 14f + impactTilt, -MaximumTilt, MaximumTilt);
        }

        private void DirectHazards()
        {
            if (elapsedSeconds < nextHazardAt) return;
            float progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            IssueNextHazard(progress, false);
            if (progress > 0.55f && hazardSequence % 4 == 0)
                IssueNextHazard(progress, true);
            nextHazardAt = elapsedSeconds + Mathf.Lerp(2.65f, 1.25f, progress);
        }

        private void UpdateBlizzardAuthority()
        {
            if (!HasAuthority) return;
            if (blizzardPhase == BlizzardPhase.None)
            {
                if (elapsedSeconds >= nextBlizzardAt) BeginBlizzardWarning();
                return;
            }

            blizzardRemaining = Mathf.Max(0f, blizzardRemaining - Time.deltaTime);
            if (blizzardRemaining > 0f) return;
            if (blizzardPhase == BlizzardPhase.Warning)
            {
                blizzardPhase = BlizzardPhase.Active;
                blizzardRemaining = 3.2f;
                BroadcastState(true);
            }
            else
            {
                blizzardPhase = BlizzardPhase.None;
                blizzardRemaining = 0f;
                float spacing = 10.5f + (blizzardSequence % 3) * 1.25f;
                nextBlizzardAt = elapsedSeconds + spacing;
                BroadcastState(true);
            }
        }

        private void BeginBlizzardWarning()
        {
            blizzardSequence++;
            int pattern = blizzardSequence % 4;
            blizzardDirection = pattern == 0
                ? Vector2.right
                : pattern == 1
                    ? new Vector2(-0.92f, -0.38f).normalized
                    : pattern == 2 ? Vector2.left : new Vector2(0.92f, -0.38f).normalized;
            blizzardStrength = Mathf.Lerp(10f, 14f, Mathf.Clamp01(elapsedSeconds / durationSeconds));
            blizzardPhase = BlizzardPhase.Warning;
            blizzardRemaining = 1.8f;
            BroadcastState(true);
        }

        private void UpdateAmbientSnow()
        {
            float interval = blizzardPhase == BlizzardPhase.Active ? 0.04f : 0.14f;
            if (Time.time < nextSnowParticleAt) return;
            nextSnowParticleAt = Time.time + interval;
            SpawnSnowParticle(++snowParticleSequence);
        }

        private void SpawnSnowParticle(int sequence)
        {
            float x01 = Deterministic01(sequence, 17);
            float y01 = Deterministic01(sequence, 43);
            bool blown = blizzardPhase == BlizzardPhase.Active && Mathf.Abs(blizzardDirection.x) > 0.1f;
            Vector2 position;
            Vector2 velocity;
            if (blown)
            {
                float sourceX = blizzardDirection.x > 0f ? -18f : 18f;
                position = new Vector2(sourceX, Mathf.Lerp(-5.5f, 9.5f, y01));
                velocity = blizzardDirection * Mathf.Lerp(9f, 13f, x01) + Vector2.down * 0.8f;
            }
            else
            {
                position = new Vector2(Mathf.Lerp(-16.8f, 16.8f, x01), Mathf.Lerp(9f, 11.5f, y01));
                velocity = new Vector2(Mathf.Lerp(-0.45f, 0.45f, y01), -Mathf.Lerp(2.1f, 3.5f, x01));
            }
            Stage14SnowParticle.Create(transform, this, sequence, position, velocity);
        }

        private static float Deterministic01(int sequence, int salt)
        {
            uint value = (uint)(sequence * 747796405 + salt * 2891336453u);
            value = (value >> ((int)(value >> 28) + 4)) ^ value;
            value *= 277803737u;
            value = (value >> 22) ^ value;
            return (value & 0x00FFFFFF) / 16777215f;
        }

        private void ApplyBlizzardPush()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform onlineLocal = IsOnline ? stageManager.ActivePlayerTransform : null;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || IsEliminated(player)
                    || onlineLocal != null && player.transform != onlineLocal) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;
                PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
                float ink = ability != null ? Mathf.Max(0f, ability.CurrentProfile.TotalInk) : 175f;
                float resistance = Mathf.Lerp(1.22f, 0.62f, Mathf.Clamp01(ink / 500f));
                Vector2 pushDirection = new Vector2(
                    blizzardDirection.x,
                    blizzardDirection.y * 0.38f).normalized;
                player.ApplyEnvironmentalPush(
                    pushDirection * (blizzardStrength * resistance * Time.fixedDeltaTime), 0.12f);
                Vector2 velocity = body.linearVelocity;
                body.linearVelocity = new Vector2(
                    Mathf.Clamp(velocity.x, -24f, 24f),
                    Mathf.Clamp(velocity.y, -18f, 24f));
            }
        }

        private void IssueNextHazard(float progress, bool secondary)
        {
            int sequence = ++hazardSequence;
            HazardType type = sequence % 8 == 0
                ? HazardType.EnemyDrop
                : sequence % 5 == 0
                    ? HazardType.ShockIcicle
                    : sequence % 3 == 0 ? HazardType.FrostIcicle : HazardType.Icicle;
            float x = DeterministicX(sequence + (secondary ? 101 : 0));
            if (secondary) x = Mathf.Clamp(-x + Mathf.Sign(x) * 2.2f, -13.6f, 13.6f);
            HazardState state = new HazardState
            {
                Sequence = sequence,
                Type = (int)type,
                X = x,
                WarningSeconds = Mathf.Lerp(1.45f, 0.8f, progress),
                FallSpeed = Mathf.Lerp(10.5f, 16f, progress)
            };
            StartHazard(state);
            if (IsOnline && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = HazardKind,
                    Json = JsonUtility.ToJson(state)
                });
            }
        }

        private static float DeterministicX(int sequence)
        {
            uint value = (uint)(sequence * 747796405 + 2891336453u);
            value = (value >> ((int)(value >> 28) + 4)) ^ value;
            value *= 277803737u;
            value = (value >> 22) ^ value;
            return Mathf.Lerp(-13.4f, 13.4f, (value & 0x00FFFFFF) / 16777215f);
        }

        private void StartHazard(HazardState state)
        {
            if (state == null || state.Sequence <= lastHazardSequence) return;
            lastHazardSequence = state.Sequence;
            StartCoroutine(RunHazard(state));
        }

        private IEnumerator RunHazard(HazardState state)
        {
            HazardType type = (HazardType)Mathf.Clamp(state.Type, 0, 3);
            GameObject warning = CreateWarningLine(state.X, type);
            float remaining = Mathf.Max(0.35f, state.WarningSeconds);
            while (remaining > 0f && phase != Phase.Failed && phase != Phase.Finished)
            {
                remaining -= Time.deltaTime;
                if (warning != null)
                {
                    LineRenderer line = warning.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        Color color = GetHazardColor(type);
                        color.a = 0.22f + Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.72f;
                        line.startColor = color;
                        line.endColor = color;
                        Vector3 end = GetSeesawSurfacePoint(state.X);
                        line.SetPosition(0, new Vector3(state.X, 9.2f, -0.2f));
                        line.SetPosition(1, new Vector3(end.x, end.y, -0.2f));
                    }
                }
                yield return null;
            }
            if (warning != null) Destroy(warning);
            if (phase == Phase.Failed || phase == Phase.Finished) yield break;

            if (type == HazardType.EnemyDrop)
            {
                SpawnEnemy(state);
            }
            else
            {
                Stage14IcicleProjectile.Create(
                    transform, this, state.Sequence, type, new Vector2(state.X, 9.1f), state.FallSpeed);
                GameSfx.Play(SfxId.EditorObjectDrop, 0.82f);
            }
        }

        private GameObject CreateWarningLine(float x, HazardType type)
        {
            GameObject root = new GameObject("14-1 Icicle Warning");
            root.transform.SetParent(transform, false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = type == HazardType.EnemyDrop ? 0.16f : 0.1f;
            line.endWidth = line.startWidth;
            line.numCapVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.sortingOrder = 48;
            Color color = GetHazardColor(type);
            line.startColor = color;
            line.endColor = color;
            Vector3 end = GetSeesawSurfacePoint(x);
            line.SetPosition(0, new Vector3(x, 9.2f, -0.2f));
            line.SetPosition(1, new Vector3(end.x, end.y, -0.2f));
            return root;
        }

        private static Color GetHazardColor(HazardType type)
        {
            switch (type)
            {
                case HazardType.FrostIcicle: return new Color(0.1f, 0.72f, 1f, 0.9f);
                case HazardType.ShockIcicle: return new Color(0.72f, 0.3f, 1f, 0.9f);
                case HazardType.EnemyDrop: return new Color(1f, 0.48f, 0.08f, 0.9f);
                default: return new Color(1f, 0.16f, 0.12f, 0.9f);
            }
        }

        private void SpawnEnemy(HazardState state)
        {
            StageObjectType type = state.Sequence % 3 == 0
                ? StageObjectType.EnemyCharger
                : state.Sequence % 2 == 0 ? StageObjectType.EnemyJumper : StageObjectType.EnemyWalker;
            string objectId = $"14-1_drop_enemy_{state.Sequence:000}";
            Vector2 position = new Vector2(state.X, 8.6f);
            float speed = 2.3f + Mathf.Clamp01(elapsedSeconds / durationSeconds) * 1.4f;
            float facing = state.X > 0f ? -1f : 1f;
            if (gimmickSyncManager == null)
                gimmickSyncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            if (gimmickSyncManager != null)
            {
                // This path creates the enemy only on the authority, broadcasts
                // its spawn, and registers its rigidbody for transform snapshots.
                gimmickSyncManager.SpawnDropperEnemy(
                    objectId, type, position, 1.15f, speed, facing, new Vector2(0f, -1.2f));
            }
            else if (!IsOnline)
            {
                if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
                objectFactory?.CreateSpawnedEnemy(
                    type, objectId, position, 1.15f, transform, speed, facing);
            }
            GameSfx.Play(SfxId.EditorObjectDrop, 0.9f);
        }

        internal bool IsSeesawCollider(Collider2D other)
        {
            return other != null && seesawRoot != null
                && (other == seesawCollider || other.transform == seesawRoot || other.transform.IsChildOf(seesawRoot));
        }

        internal bool ShouldHandlePlayerHit(PlayerController2D player)
        {
            if (player == null) return false;
            if (!IsOnline) return true;
            return stageManager != null
                && stageManager.ActivePlayerTransform != null
                && player.transform == stageManager.ActivePlayerTransform;
        }

        internal void HandleIcicleImpact(HazardType type, Vector2 worldPosition)
        {
            if (type == HazardType.FrostIcicle)
            {
                if (HasAuthority) ActivateFreezePatch(worldPosition, 9f);
            }
            else if (type == HazardType.ShockIcicle)
            {
                if (HasAuthority)
                    impactTilt = Mathf.Clamp(impactTilt - Mathf.Sign(worldPosition.x) * 12f, -MaximumTilt, MaximumTilt);
                ApplyLocalShockwave(worldPosition.x);
            }
            GameSfx.Play(SfxId.EditorObjectDrop, 0.88f);
        }

        private void ActivateFreezePatch(Vector2 worldPosition, float seconds)
        {
            if (seesawRoot == null) return;
            float localX = Mathf.Clamp(
                seesawRoot.InverseTransformPoint(worldPosition).x,
                -SeesawHalfWidth + FreezePatchWidth * 0.5f,
                SeesawHalfWidth - FreezePatchWidth * 0.5f);
            FreezePatch nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < freezePatches.Count; i++)
            {
                float distance = Mathf.Abs(freezePatches[i].LocalX - localX);
                if (distance < nearestDistance)
                {
                    nearest = freezePatches[i];
                    nearestDistance = distance;
                }
            }

            if (nearest != null && nearestDistance <= FreezePatchWidth * 0.45f)
            {
                nearest.LocalX = Mathf.Lerp(nearest.LocalX, localX, 0.5f);
                nearest.Remaining = Mathf.Max(nearest.Remaining, seconds);
                RefreshFreezePatch(nearest);
            }
            else
            {
                if (freezePatches.Count >= MaximumFreezePatchCount)
                {
                    int oldest = 0;
                    for (int i = 1; i < freezePatches.Count; i++)
                        if (freezePatches[i].Remaining < freezePatches[oldest].Remaining) oldest = i;
                    DestroyFreezePatchAt(oldest);
                }
                CreateFreezePatch(localX, seconds);
            }
            BroadcastState(true);
        }

        private void UpdateFreezePatches()
        {
            for (int i = freezePatches.Count - 1; i >= 0; i--)
            {
                FreezePatch patch = freezePatches[i];
                if (patch.Remaining > 0f)
                {
                    patch.Remaining = Mathf.Max(0f, patch.Remaining - Time.deltaTime);
                    RefreshFreezePatch(patch);
                }
                if (HasAuthority && patch.Remaining <= 0f) DestroyFreezePatchAt(i);
            }
        }

        private FreezePatch CreateFreezePatch(float localX, float remaining)
        {
            GameObject patchRoot = new GameObject("Icicle Freeze Spot");
            patchRoot.transform.SetParent(seesawRoot, false);
            patchRoot.layer = seesawRoot.gameObject.layer;
            SpriteRenderer fill = patchRoot.AddComponent<SpriteRenderer>();
            fill.sprite = DoodleRuntimeAssets.SquareSprite;
            fill.color = new Color(0.25f, 0.82f, 1f, 0.58f);
            fill.sortingOrder = 19;
            BoxCollider2D collider = patchRoot.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.sharedMaterial = StageIceSurface.GetMaterial();
            patchRoot.AddComponent<StageIceSurface>();
            FreezePatch patch = new FreezePatch
            {
                Root = patchRoot.transform,
                Fill = fill,
                Collider = collider,
                LocalX = localX,
                Remaining = remaining
            };
            freezePatches.Add(patch);
            RefreshFreezePatch(patch);
            return patch;
        }

        private void DestroyFreezePatchAt(int index)
        {
            if (index < 0 || index >= freezePatches.Count) return;
            FreezePatch patch = freezePatches[index];
            freezePatches.RemoveAt(index);
            if (patch?.Root != null) Destroy(patch.Root.gameObject);
        }

        private static void RefreshFreezePatch(FreezePatch patch)
        {
            bool active = patch != null && patch.Remaining > 0f;
            if (patch?.Root != null)
            {
                // Keep the ice collider's top exactly coplanar with the board so
                // freezing never creates a step that lifts players or snow.
                const float iceThickness = 0.08f;
                patch.Root.localPosition = new Vector3(
                    patch.LocalX,
                    SeesawSurfaceY - iceThickness * 0.5f,
                    -0.03f);
                patch.Root.localScale = new Vector3(FreezePatchWidth, iceThickness, 1f);
            }
            if (patch?.Fill != null)
            {
                patch.Fill.enabled = active;
                Color color = patch.Fill.color;
                color.a = active ? 0.52f + Mathf.Sin(Time.time * 5f) * 0.08f : 0f;
                patch.Fill.color = color;
            }
            if (patch?.Collider != null) patch.Collider.enabled = active;
        }

        private void UpdateSnowSurfaceState()
        {
            if (seesawRoot == null) return;
            for (int i = snowParticles.Count - 1; i >= 0; i--)
            {
                Stage14SnowParticle particle = snowParticles[i];
                if (particle == null)
                {
                    snowParticles.RemoveAt(i);
                    continue;
                }
                Vector3 localPosition = seesawRoot.InverseTransformPoint(particle.transform.position);
                bool frozen = false;
                if (localPosition.y > -0.1f && localPosition.y < 1.8f)
                {
                    for (int j = 0; j < freezePatches.Count; j++)
                    {
                        FreezePatch patch = freezePatches[j];
                        if (patch.Remaining > 0f
                            && Mathf.Abs(localPosition.x - patch.LocalX) <= FreezePatchWidth * 0.5f)
                        {
                            frozen = true;
                            break;
                        }
                    }
                }
                particle.SetFrozen(frozen);
            }
        }

        internal void RegisterSnowParticle(Stage14SnowParticle particle)
        {
            if (particle == null) return;
            snowParticles.Add(particle);
            while (snowParticles.Count > MaximumSettledSnowCount)
            {
                Stage14SnowParticle oldest = snowParticles[0];
                snowParticles.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        internal void UnregisterSnowParticle(Stage14SnowParticle particle)
        {
            if (particle != null) snowParticles.Remove(particle);
        }

        private void RefreshBlizzardPresentation()
        {
            if (blizzardVisualRoot == null) return;
            bool visible = blizzardPhase != BlizzardPhase.None;
            blizzardVisualRoot.SetActive(visible);
            if (!visible) return;

            if (lastPresentedBlizzardSequence != blizzardSequence)
            {
                lastPresentedBlizzardSequence = blizzardSequence;
                GameSfx.Play(SfxId.UiToggleOn, 0.8f);
            }

            Vector2 direction = blizzardDirection.sqrMagnitude > 0.01f
                ? blizzardDirection.normalized : Vector2.right;
            bool warning = blizzardPhase == BlizzardPhase.Warning;
            float sourceX = direction.x > 0f ? -16.5f : 16.5f;
            float stormSpeed = warning ? 5f : 13f + blizzardStrength * 0.45f;
            if (blizzardVeil != null)
            {
                float veilAlpha = warning
                    ? 0.035f + Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.035f
                    : 0.13f + Mathf.Abs(Mathf.Sin(Time.time * 4.5f)) * 0.07f;
                blizzardVeil.color = new Color(0.82f, 0.94f, 1f, veilAlpha);
            }

            for (int i = 0; i < blizzardLines.Count; i++)
            {
                LineRenderer line = blizzardLines[i];
                if (line == null) continue;
                float phaseOffset = Deterministic01(i + 1, 211) * 38f;
                float travel = Mathf.Repeat(Time.time * stormSpeed
                    * Mathf.Lerp(0.82f, 1.24f, Deterministic01(i + 1, 223)) + phaseOffset, 38f);
                float x = direction.x > 0f ? -19f + travel : 19f - travel;
                float baseY = Mathf.Lerp(-6.5f, 10.5f, Deterministic01(i + 1, 227));
                float y = Mathf.Repeat(baseY + direction.y * travel * 0.42f + 6.5f, 17f) - 6.5f;
                y += Mathf.Sin(Time.time * 6f + i * 1.71f) * 0.16f;
                Vector2 start = new Vector2(x, y);
                float length = warning
                    ? Mathf.Lerp(1.2f, 2.4f, Deterministic01(i + 1, 229))
                    : Mathf.Lerp(2.2f, 5.2f, Deterministic01(i + 1, 229));
                Vector2 end = start + direction * length;
                float alpha = warning
                    ? 0.13f + Deterministic01(i + 1, 233) * 0.18f
                    : 0.42f + Deterministic01(i + 1, 233) * 0.46f;
                Color color = new Color(0.72f, 0.91f, 1f, alpha);
                line.startColor = color;
                line.endColor = new Color(color.r, color.g, color.b, color.a * 0.12f);
                float width = Mathf.Lerp(0.035f, 0.14f, Deterministic01(i + 1, 239));
                line.startWidth = warning ? width * 0.7f : width;
                line.endWidth = width * 0.25f;
                line.SetPosition(0, new Vector3(start.x, start.y, -0.25f));
                line.SetPosition(1, new Vector3(end.x, end.y, -0.25f));
            }

            for (int i = 0; i < blizzardFlakes.Count; i++)
            {
                SpriteRenderer flake = blizzardFlakes[i];
                if (flake == null) continue;
                float phaseOffset = Deterministic01(i + 1, 307) * 39f;
                float travel = Mathf.Repeat(Time.time * stormSpeed
                    * Mathf.Lerp(0.68f, 1.34f, Deterministic01(i + 1, 311)) + phaseOffset, 39f);
                float x = direction.x > 0f ? -19.5f + travel : 19.5f - travel;
                float baseY = Mathf.Lerp(-6.8f, 10.8f, Deterministic01(i + 1, 313));
                float y = Mathf.Repeat(baseY + direction.y * travel * 0.45f + 6.8f, 17.6f) - 6.8f;
                y += Mathf.Sin(Time.time * 7.5f + i * 2.13f) * 0.22f;
                flake.transform.position = new Vector3(x, y, -0.28f);
                float size = warning
                    ? Mathf.Lerp(0.18f, 0.72f, Deterministic01(i + 1, 317))
                    : Mathf.Lerp(0.85f, 3.4f, Deterministic01(i + 1, 317));
                float wobble = 0.82f + Mathf.Sin(Time.time * 5f + i * 0.73f) * 0.12f;
                flake.transform.localScale = new Vector3(size * wobble, size, 1f);
                float alpha = warning
                    ? 0.12f + Deterministic01(i + 1, 337) * 0.2f
                    : 0.84f + Deterministic01(i + 1, 337) * 0.14f;
                flake.color = new Color(0.96f, 0.995f, 1f, alpha);
            }

            if (blizzardDirectionText != null)
            {
                blizzardDirectionText.gameObject.SetActive(warning);
                blizzardDirectionText.transform.position = new Vector3(sourceX, 1.8f, -0.3f);
                blizzardDirectionText.text = direction.x > 0f
                    ? direction.y < -0.1f ? "↘  ↘  ↘" : "→  →  →"
                    : direction.y < -0.1f ? "↙  ↙  ↙" : "←  ←  ←";
                blizzardDirectionText.color = warning
                    ? new Color(0.16f, 0.68f, 0.94f, 0.88f)
                    : new Color(0.76f, 0.94f, 1f, 0.7f);
            }
        }

        private void ApplyLocalShockwave(float worldX)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform onlineLocal = IsOnline ? stageManager.ActivePlayerTransform : null;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || onlineLocal != null && player.transform != onlineLocal) continue;
                float distance = Mathf.Abs(player.transform.position.x - worldX);
                if (distance > 6f) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body == null) continue;
                float strength = 1f - distance / 6f;
                Vector2 velocity = body.linearVelocity;
                velocity.y = Mathf.Max(velocity.y, 7f + strength * 7f);
                velocity.x += Mathf.Sign(player.transform.position.x - worldX) * (3f + strength * 5f);
                body.linearVelocity = velocity;
            }
        }

        private Vector3 GetSeesawSurfacePoint(float worldX)
        {
            if (seesawRoot == null) return new Vector3(worldX, SeesawY + SeesawSurfaceY, 0f);
            Vector3 local = seesawRoot.InverseTransformPoint(new Vector3(worldX, SeesawY, 0f));
            local.x = Mathf.Clamp(local.x, -SeesawHalfWidth, SeesawHalfWidth);
            local.y = SeesawSurfaceY;
            return seesawRoot.TransformPoint(local);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != Phase.Running) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (!IsOnline) participants.Add(id);

            if (IsOnline)
            {
                string localId = onlineManager != null ? onlineManager.LocalPlayerId : null;
                if (id != localId) return;
                if (!HasAuthority)
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = EliminateRequestKind,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                    });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id, IsOnline);
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = EliminatedKind,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
            }
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void HidePlayer(PlayerController2D player)
        {
            if (player == null || hiddenPlayers.Contains(player)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.ResetMotion();
            player.SetControlsEnabled(false);
            hiddenPlayers.Add(player);
            player.gameObject.SetActive(false);
        }

        private void RestorePlayers()
        {
            if (restoredPlayers) return;
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++)
                if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private void ApplyPendingEliminations()
        {
            foreach (string id in eliminated)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player != null && player.gameObject.activeSelf) HidePlayer(player);
            }
        }

        private void CheckFalls()
        {
            if (phase != Phase.Running) return;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player != null && (player.transform.position.y < -8.5f || Mathf.Abs(player.transform.position.x) > 19f))
                    RequestElimination(player);
            }
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed) return;
            phase = Phase.Failed;
            blizzardPhase = BlizzardPhase.None;
            blizzardRemaining = 0f;
            failureRemaining = 2.8f;
            SetLocalControls(false);
            BroadcastState(true);
        }

        private bool AreAllEliminated()
        {
            if (IsOnline)
                return onlineManager != null && onlineManager.AreAllCurrentLobbyPlayersIn(eliminated);
            if (participants.Count == 0) return false;
            foreach (string id in participants)
                if (!eliminated.Contains(id)) return false;
            return true;
        }

        private void CaptureParticipants()
        {
            if (IsOnline)
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players == null) return;
                for (int i = 0; i < players.Length; i++)
                    if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) participants.Add(players[i].PlayerId);
                return;
            }
            PlayerController2D[] local = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < local.Length; i++)
            {
                string id = ResolvePlayerId(local[i]);
                if (!string.IsNullOrEmpty(id)) participants.Add(id);
            }
        }

        private void PlacePlayersOnSeesaw()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform onlineLocal = IsOnline ? stageManager.ActivePlayerTransform : null;
            int placed = 0;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || onlineLocal != null && player.transform != onlineLocal) continue;
                float x = onlineLocal != null
                    ? GetOnlineSpawnX()
                    : (placed - (players.Length - 1) * 0.5f) * 2f;
                Vector2 position = new Vector2(x, SeesawY + 1.55f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                player.transform.position = position;
                player.ResetMotion();
                placed++;
            }
            Physics2D.SyncTransforms();
        }

        private float GetOnlineSpawnX()
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null || lobbyPlayers.Length <= 1) return 0f;
            string localId = onlineManager.LocalPlayerId;
            for (int i = 0; i < lobbyPlayers.Length; i++)
            {
                if (lobbyPlayers[i] != null && lobbyPlayers[i].PlayerId == localId)
                    return (i - (lobbyPlayers.Length - 1) * 0.5f) * 2.2f;
            }
            return 0f;
        }

        private void SetLocalControls(bool enabled)
        {
            PlayerController2D active = stageManager != null && stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode && !IsEliminated(active));
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminated.Contains(id);
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            float[] freezePositions = new float[freezePatches.Count];
            float[] frozen = new float[freezePatches.Count];
            for (int i = 0; i < freezePatches.Count; i++)
            {
                freezePositions[i] = freezePatches[i].LocalX;
                frozen[i] = freezePatches[i].Remaining;
            }
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new NetworkState
                {
                    Sequence = ++stateSequence,
                    CurrentPhase = (int)phase,
                    RemainingSeconds = remainingSeconds,
                    CountdownRemaining = countdownRemaining,
                    SeesawAngle = currentAngle,
                    FreezeLocalX = freezePositions,
                    FreezeRemaining = frozen,
                    BlizzardSequence = blizzardSequence,
                    CurrentBlizzardPhase = (int)blizzardPhase,
                    BlizzardRemaining = blizzardRemaining,
                    BlizzardDirection = blizzardDirection,
                    BlizzardStrength = blizzardStrength,
                    EliminatedIds = new List<string>(eliminated).ToArray()
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority)
            {
                ApplyNetworkState(JsonUtility.FromJson<NetworkState>(data.Json));
            }
            else if (data.Kind == HazardKind && IsHostPlayer(data.PlayerId) && !HasAuthority)
            {
                StartHazard(JsonUtility.FromJson<HazardState>(data.Json));
            }
            else if (data.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(data.Json);
                if (request != null && request.PlayerId == data.PlayerId) ConfirmElimination(request.PlayerId, true);
            }
            else if (data.Kind == EliminatedKind && IsHostPlayer(data.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(data.Json);
                if (state != null) ApplyElimination(state.PlayerId);
            }
        }

        private void ApplyNetworkState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            Phase oldPhase = phase;
            phase = (Phase)Mathf.Clamp(state.CurrentPhase, 0, (int)Phase.Finished);
            remainingSeconds = Mathf.Max(0f, state.RemainingSeconds);
            countdownRemaining = Mathf.Max(0f, state.CountdownRemaining);
            networkAngle = Mathf.Clamp(state.SeesawAngle, -MaximumTilt, MaximumTilt);
            if (seesawRoot != null && state.FreezeLocalX != null && state.FreezeRemaining != null)
            {
                int count = Mathf.Min(state.FreezeLocalX.Length, state.FreezeRemaining.Length);
                while (freezePatches.Count < count)
                    CreateFreezePatch(0f, 0f);
                while (freezePatches.Count > count)
                    DestroyFreezePatchAt(freezePatches.Count - 1);
                for (int i = 0; i < count; i++)
                {
                    freezePatches[i].LocalX = Mathf.Clamp(
                        state.FreezeLocalX[i],
                        -SeesawHalfWidth + FreezePatchWidth * 0.5f,
                        SeesawHalfWidth - FreezePatchWidth * 0.5f);
                    freezePatches[i].Remaining = Mathf.Max(0f, state.FreezeRemaining[i]);
                    RefreshFreezePatch(freezePatches[i]);
                }
            }
            blizzardSequence = Mathf.Max(blizzardSequence, state.BlizzardSequence);
            blizzardPhase = (BlizzardPhase)Mathf.Clamp(
                state.CurrentBlizzardPhase, 0, (int)BlizzardPhase.Active);
            blizzardRemaining = Mathf.Max(0f, state.BlizzardRemaining);
            blizzardDirection = state.BlizzardDirection;
            blizzardStrength = Mathf.Clamp(state.BlizzardStrength, 0f, 18f);
            if (state.EliminatedIds != null)
                for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            if (oldPhase != phase) SetLocalControls(phase == Phase.Running);
        }

        private void UpdateReplicaClock()
        {
            if (phase == Phase.Countdown) countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.deltaTime);
            else if (phase == Phase.Running && !LocalMultiplayerDebugMode.NoTimeLimit)
                remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (blizzardPhase != BlizzardPhase.None)
                blizzardRemaining = Mathf.Max(0f, blizzardRemaining - Time.deltaTime);
        }

        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        private void RefreshPresentation()
        {
            string countdown = string.Empty;
            if (phase == Phase.Countdown)
            {
                countdown = Mathf.CeilToInt(countdownRemaining).ToString();
                if (countdown != lastCountdownText)
                {
                    lastCountdownText = countdown;
                    GameSfx.Play(SfxId.StageCountdownTick);
                }
            }
            else if (phase == Phase.Failed)
            {
                countdown = LocalizationManager.T("game_over");
            }
            uiManager?.SetChallengeCountdown(!string.IsNullOrEmpty(countdown), countdown);

            if (monitorText != null)
            {
                monitorText.text = phase == Phase.Failed
                    ? LocalizationManager.T("game_over")
                    : phase == Phase.Countdown
                        ? Mathf.CeilToInt(countdownRemaining).ToString()
                        : $"{remainingSeconds:0.0}";
                monitorText.color = remainingSeconds <= 10f && phase == Phase.Running
                    ? new Color(0.92f, 0.15f, 0.1f, 1f)
                    : new Color(0.04f, 0.43f, 0.58f, 1f);
            }
        }

        private void BuildArena()
        {
            GameObject seesaw = new GameObject("14-1 Ink Seesaw");
            seesaw.transform.SetParent(transform, false);
            seesaw.transform.position = new Vector3(0f, SeesawY, 0f);
            seesaw.layer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            seesaw.tag = "Ground";
            seesawRoot = seesaw.transform;
            seesawBody = seesaw.AddComponent<Rigidbody2D>();
            seesawBody.bodyType = RigidbodyType2D.Kinematic;
            seesawBody.useFullKinematicContacts = true;
            seesawBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            seesawCollider = seesaw.AddComponent<BoxCollider2D>();
            ((BoxCollider2D)seesawCollider).size = new Vector2(SeesawWidth, SeesawThickness);

            GameObject fill = new GameObject("Blue Pencil Seesaw");
            fill.transform.SetParent(seesaw.transform, false);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            Sprite seesawArt = LoadStageArt("stage-14-1-seesaw-plank");
            renderer.sprite = seesawArt != null ? seesawArt : DoodleRuntimeAssets.SquareSprite;
            renderer.color = seesawArt != null ? Color.white : new Color(0.28f, 0.62f, 0.9f, 0.86f);
            renderer.sortingOrder = 15;
            SetSpriteWorldSize(fill.transform, renderer.sprite, new Vector2(SeesawWidth + 0.35f, 1.15f));
            if (seesawArt == null)
                AddOutline(seesaw.transform, new Vector2(SeesawWidth, SeesawThickness), new Color(0.12f, 0.2f, 0.34f, 1f), 17);

            GameObject pivot = new GameObject("Seesaw Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(0f, SeesawY - 1.25f, 0.15f);
            Sprite pivotArt = LoadStageArt("stage-14-1-seesaw-pivot");
            if (pivotArt != null)
            {
                SpriteRenderer pivotRenderer = pivot.AddComponent<SpriteRenderer>();
                pivotRenderer.sprite = pivotArt;
                pivotRenderer.color = Color.white;
                pivotRenderer.sortingOrder = 16;
                SetSpriteWorldSize(pivot.transform, pivotArt, new Vector2(3.1f, 2.55f));
            }
            else
            {
                AddOutline(pivot.transform, new Vector2(2.2f, 1.5f), new Color(0.18f, 0.2f, 0.24f, 1f), 16);
            }

            BuildBlizzardVisual();

            GameObject monitor = new GameObject("14-1 Survival Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(0f, 5.8f, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(6.8f, 2.1f), 22);
            monitorText = CreateText(monitor.transform, "Survival Time", Vector3.zero, 72, 0.2f, 28);
        }

        private void BuildBlizzardVisual()
        {
            blizzardVisualRoot = new GameObject("14-1 Blizzard Visual");
            blizzardVisualRoot.transform.SetParent(transform, false);

            GameObject veil = new GameObject("Blizzard Whiteout Veil");
            veil.transform.SetParent(blizzardVisualRoot.transform, false);
            veil.transform.position = new Vector3(0f, 1.8f, -0.2f);
            veil.transform.localScale = new Vector3(39f, 18.5f, 1f);
            blizzardVeil = veil.AddComponent<SpriteRenderer>();
            blizzardVeil.sprite = DoodleRuntimeAssets.SquareSprite;
            blizzardVeil.sortingOrder = 12;

            for (int i = 0; i < 64; i++)
            {
                GameObject streak = new GameObject("Blizzard Streak " + (i + 1));
                streak.transform.SetParent(blizzardVisualRoot.transform, false);
                LineRenderer line = streak.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 3;
                line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                line.sortingOrder = 44 + i % 3;
                blizzardLines.Add(line);
            }

            for (int i = 0; i < 180; i++)
            {
                GameObject flakeObject = new GameObject("Foreground Blizzard Flake " + (i + 1));
                flakeObject.transform.SetParent(blizzardVisualRoot.transform, false);
                SpriteRenderer flake = flakeObject.AddComponent<SpriteRenderer>();
                flake.sprite = DoodleRuntimeAssets.CircleSprite;
                flake.sortingOrder = 47 + i % 3;
                blizzardFlakes.Add(flake);
            }

            blizzardDirectionText = CreateText(
                blizzardVisualRoot.transform, "Blizzard Direction", Vector3.zero, 62, 0.12f, 52);
            blizzardVisualRoot.SetActive(false);
        }

        private static TextMesh CreateText(Transform parent, string name, Vector3 localPosition, int fontSize, float characterSize, int sortingOrder)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            TextMesh text = root.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.fontStyle = FontStyle.Bold;
            Font font = DoodleRuntimeAssets.HandwrittenFont;
            if (font != null)
            {
                text.font = font;
                root.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            root.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
            return text;
        }

        private static Sprite LoadStageArt(string name)
        {
            return Resources.Load<Sprite>("StageObjects/NicoDraw/" + name);
        }

        private static void SetSpriteWorldSize(Transform target, Sprite sprite, Vector2 worldSize)
        {
            if (target == null || sprite == null) return;
            Vector2 source = sprite.bounds.size;
            target.localScale = new Vector3(
                source.x > 0.001f ? worldSize.x / source.x : 1f,
                source.y > 0.001f ? worldSize.y / source.y : 1f,
                1f);
        }

        private static void AddOutline(Transform parent, Vector2 size, Color color, int order)
        {
            Vector2 half = size * 0.5f;
            GameObject root = new GameObject("Seesaw Pencil Outline");
            root.transform.SetParent(parent, false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.startWidth = 0.08f;
            line.endWidth = 0.08f;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            line.SetPosition(0, new Vector3(-half.x, -half.y, 0f));
            line.SetPosition(1, new Vector3(-half.x, half.y, 0f));
            line.SetPosition(2, new Vector3(half.x, half.y, 0f));
            line.SetPosition(3, new Vector3(half.x, -half.y, 0f));
            line.SetPosition(4, new Vector3(-half.x, -half.y, 0f));
        }
    }

    [DisallowMultipleComponent]
    public sealed class Stage14IcicleProjectile : MonoBehaviour
    {
        private StageIcicleSeesawSurvivalController owner;
        private Rigidbody2D body;
        private float speed;
        private StageIcicleSeesawSurvivalController.HazardType hazardType;
        private bool consumed;

        internal static Stage14IcicleProjectile Create(
            Transform parent,
            StageIcicleSeesawSurvivalController controller,
            int sequence,
            StageIcicleSeesawSurvivalController.HazardType type,
            Vector2 position,
            float fallSpeed)
        {
            GameObject root = new GameObject($"14-1 Icicle {sequence:000}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.useFullKinematicContacts = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.55f, 1.75f);
            collider.isTrigger = true;

            GameObject fill = new GameObject("Icicle Pencil Fill");
            fill.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            Sprite icicleArt = Resources.Load<Sprite>("StageObjects/NicoDraw/stage-14-1-icicle");
            renderer.sprite = icicleArt != null ? icicleArt : DoodleRuntimeAssets.SquareSprite;
            int typeValue = (int)type;
            renderer.color = typeValue == 1
                ? new Color(0.5f, 0.95f, 1f, 1f)
                : typeValue == 2
                    ? new Color(0.86f, 0.56f, 1f, 1f)
                    : Color.white;
            renderer.sortingOrder = 47;
            if (icicleArt != null)
            {
                Vector2 source = icicleArt.bounds.size;
                fill.transform.localScale = new Vector3(
                    source.x > 0.001f ? 0.9f / source.x : 1f,
                    source.y > 0.001f ? 2.15f / source.y : 1f,
                    1f);
            }
            else
            {
                fill.transform.localScale = new Vector3(0.5f, 1.7f, 1f);
            }

            GameObject tip = new GameObject("Icicle Tip");
            tip.transform.SetParent(root.transform, false);
            LineRenderer outline = tip.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.positionCount = 4;
            outline.loop = true;
            outline.startWidth = 0.055f;
            outline.endWidth = 0.055f;
            outline.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            outline.startColor = new Color(0.12f, 0.28f, 0.42f, 1f);
            outline.endColor = outline.startColor;
            outline.sortingOrder = 48;
            outline.SetPosition(0, new Vector3(-0.3f, 0.85f, 0f));
            outline.SetPosition(1, new Vector3(0.3f, 0.85f, 0f));
            outline.SetPosition(2, new Vector3(0f, -1.05f, 0f));
            outline.SetPosition(3, new Vector3(-0.3f, 0.85f, 0f));
            tip.SetActive(icicleArt == null);

            Stage14IcicleProjectile projectile = root.AddComponent<Stage14IcicleProjectile>();
            projectile.owner = controller;
            projectile.body = body;
            projectile.speed = Mathf.Max(4f, fallSpeed);
            projectile.hazardType = type;
            return projectile;
        }

        private void FixedUpdate()
        {
            if (consumed || body == null) return;
            body.MovePosition(body.position + Vector2.down * speed * Time.fixedDeltaTime);
            if (body.position.y < -10f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed || other == null) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                // Every peer has colliders for remote avatars. Only the peer
                // owning this avatar may consume the projectile and request
                // its elimination, otherwise a remote replica could shield it.
                if (owner != null && !owner.ShouldHandlePlayerHit(player)) return;
                consumed = true;
                StageManager manager = Object.FindFirstObjectByType<StageManager>();
                manager?.RespawnFromHazard(player);
                Destroy(gameObject);
                return;
            }
            if (owner != null && owner.IsSeesawCollider(other))
            {
                consumed = true;
                owner.HandleIcicleImpact(hazardType, transform.position);
                Destroy(gameObject);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class Stage14SnowParticle : MonoBehaviour
    {
        private static PhysicsMaterial2D snowMaterial;
        private static bool snowCollisionConfigured;
        private StageIcicleSeesawSurvivalController owner;
        private SpriteRenderer outerRenderer;
        private SpriteRenderer centerRenderer;
        private Color normalOuterColor;
        private bool frozen;

        internal static Stage14SnowParticle Create(
            Transform parent,
            StageIcicleSeesawSurvivalController controller,
            int sequence,
            Vector2 position,
            Vector2 initialVelocity)
        {
            GameObject root = new GameObject("14-1 Snow Particle " + sequence);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, -0.18f);
            root.layer = 31;
            if (!snowCollisionConfigured)
            {
                snowCollisionConfigured = true;
                Physics2D.IgnoreLayerCollision(31, 31, false);
            }
            float size = 0.12f + (sequence % 6) * 0.025f;
            root.transform.localScale = Vector3.one * size;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.48f, 0.82f, 1f, 0.78f + (sequence % 4) * 0.05f);
            renderer.sortingOrder = 43;
            GameObject center = new GameObject("Pencil Snow Center");
            center.transform.SetParent(root.transform, false);
            center.transform.localScale = Vector3.one * 0.52f;
            SpriteRenderer centerRenderer = center.AddComponent<SpriteRenderer>();
            centerRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            centerRenderer.color = new Color(0.96f, 0.99f, 1f, 0.96f);
            centerRenderer.sortingOrder = 44;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 0.00055f;
            body.gravityScale = 0.72f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = initialVelocity;
            body.angularVelocity = (sequence % 2 == 0 ? 1f : -1f) * (35f + sequence % 7 * 9f);
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.46f;
            collider.sharedMaterial = GetSnowMaterial();

            Stage14SnowParticle particle = root.AddComponent<Stage14SnowParticle>();
            particle.owner = controller;
            particle.outerRenderer = renderer;
            particle.centerRenderer = centerRenderer;
            particle.normalOuterColor = renderer.color;
            controller?.RegisterSnowParticle(particle);
            return particle;
        }

        internal void SetFrozen(bool value)
        {
            if (frozen == value) return;
            frozen = value;
            if (outerRenderer != null)
                outerRenderer.color = value ? new Color(0.18f, 0.73f, 1f, 0.96f) : normalOuterColor;
            if (centerRenderer != null)
                centerRenderer.color = value
                    ? new Color(0.72f, 0.95f, 1f, 1f)
                    : new Color(0.96f, 0.99f, 1f, 0.96f);
        }

        private void Update()
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 position = transform.position;
            if (position.y < -9f || Mathf.Abs(position.x) > 20.5f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            owner?.UnregisterSnowParticle(this);
        }

        private static PhysicsMaterial2D GetSnowMaterial()
        {
            if (snowMaterial == null)
            {
                snowMaterial = new PhysicsMaterial2D("Stage 14-1 Rolling Snow")
                {
                    friction = 0.18f,
                    bounciness = 0.03f,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return snowMaterial;
        }
    }
}
