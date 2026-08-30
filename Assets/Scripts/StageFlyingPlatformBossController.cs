using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageFlyingPlatformBossController : StageEliminationChallengeController
    {
        private const string StageId = "15-1";
        private const string StateKind = "flying_boss_state";
        private const string PadKind = "flying_boss_pad";
        private const string ShotRequestKind = "flying_boss_shot_request";
        private const string ShotKind = "flying_boss_shot";
        private const string BombRequestKind = "flying_boss_bomb_request";
        private const string BombKind = "flying_boss_bomb";
        private const string AttackKind = "flying_boss_attack";
        private const string HomingVolleyKind = "flying_boss_homing_volley";
        private const string EliminateKind = "flying_boss_eliminate";
        private const int PlayerMissileDamage = 1;
        private const int PlayerBombDamage = 5;
        private const float ArenaHalfWidth = 17f;
        private const float ArenaHalfHeight = 8.5f;

        private enum BattlePhase { Ready, Fighting, Defeated, Failed }
        private enum AttackType { Dash, Beam, Homing, Chase, Suction }

        [System.Serializable] private sealed class PadMessage { public int Room; public Vector2 Position; }
        [System.Serializable] private sealed class ShotRequest { public Vector2 Direction; }
        [System.Serializable] private sealed class EliminationMessage { public string PlayerId; }
        [System.Serializable]
        private sealed class ShotState
        {
            public int Sequence;
            public int OwnerRoom;
            public Vector2 Position;
            public Vector2 Direction;
        }
        [System.Serializable]
        private sealed class BombState
        {
            public int Sequence;
            public int OwnerRoom;
            public Vector2 Position;
        }
        [System.Serializable]
        private sealed class AttackState
        {
            public int Sequence;
            public int Type;
            public int TargetRoom;
            public Vector2 Origin;
            public Vector2 Direction;
            public float Lane;
        }
        [System.Serializable]
        private sealed class HomingVolleyState
        {
            public int Sequence;
            public Vector2 Origin;
        }
        [System.Serializable]
        private sealed class BossState
        {
            public int Sequence;
            public int Phase;
            public int Health;
            public int MaximumHealth;
            public Vector2 BossPosition;
            public Vector2[] PadPositions;
            public string[] RoomPlayerIds;
            public string[] EliminatedIds;
        }

        private static readonly Vector2[] StartPositions =
        {
            new Vector2(-8f, 3.5f), new Vector2(8f, 3.5f),
            new Vector2(-8f, -3.5f), new Vector2(8f, -3.5f)
        };
        private static readonly Color[] PlayerColors =
        {
            new Color(1f, 0.35f, 0.22f), new Color(0.18f, 0.68f, 1f),
            new Color(1f, 0.72f, 0.15f), new Color(0.32f, 0.86f, 0.45f)
        };

        private readonly string[] roomPlayerIds = new string[4];
        private readonly StageFlyingPlayerPad[] pads = new StageFlyingPlayerPad[4];
        private readonly float[] nextShotAt = new float[4];
        private readonly float[] nextBombAt = new float[4];
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly HashSet<int> receivedShots = new HashSet<int>();
        private readonly HashSet<int> receivedAttacks = new HashSet<int>();
        private readonly HashSet<int> receivedHomingVolleys = new HashSet<int>();
        private readonly List<PlayerController2D> mountedPlayers = new List<PlayerController2D>();
        private readonly List<StageFlyingBossShot> shots = new List<StageFlyingBossShot>();
        private readonly List<Transform> bossOrbitShards = new List<Transform>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousFollowEnabled;
        private Transform boss;
        private Transform bossFace;
        private SpriteRenderer bossCore;
        private Transform hpFill;
        private TextMesh hpText;
        private TextMesh statusText;
        private LineRenderer aimLine;
        private BattlePhase phase = BattlePhase.Ready;
        private Vector2 bossPosition = new Vector2(0f, 10.5f);
        private int playerCount = 1;
        private int maximumHealth;
        private int health;
        private int stateSequence;
        private int lastStateSequence;
        private int shotSequence;
        private int bombSequence;
        private int attackSequence;
        private int homingVolleySequence;
        private float readyRemaining = 3f;
        private float nextAttackAt;
        private float nextStateAt;
        private float nextPadSendAt;
        private float nextPressureHomingAt;
        private float lastDashAt = -100f;
        private bool attackRunning;
        private bool retryStarted;
        private int attackCursor;
        private float bossMoodScale = 1f;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;
        public override bool UsesGlobalFallBoundary => false;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            RestorePlayers();
            RestoreCamera();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildRoster();
            maximumHealth = 100 * playerCount;
            health = maximumHealth;
            BuildArena();
            BuildBoss();
            BuildMonitor();
            BuildAimGuide();
            LockCamera();
            MountLocalPlayers();
            RefreshMonitor();
            BroadcastState(true);
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            UpdateLocalPadInput();
            shots.RemoveAll(item => item == null);
            if (boss != null)
            {
                Vector3 desiredBossPosition = new Vector3(bossPosition.x, bossPosition.y, -0.25f);
                boss.position = attackRunning
                    ? desiredBossPosition
                    : Vector3.Lerp(boss.position, desiredBossPosition, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            AnimateBossVisuals();

            if (HasAuthority)
            {
                if (phase == BattlePhase.Ready)
                {
                    readyRemaining -= Time.deltaTime;
                    if (readyRemaining <= 0f)
                    {
                        phase = BattlePhase.Fighting;
                        nextAttackAt = Time.time + 1.8f;
                        BroadcastState(true);
                        GameSfx.Play(SfxId.StageCountdownGo);
                    }
                }
                else if (phase == BattlePhase.Fighting)
                {
                    float healthRatio = health / (float)Mathf.Max(1, maximumHealth);
                    if (!attackRunning)
                    {
                        Vector2 idleTarget = new Vector2(
                            Mathf.Sin(Time.time * 0.37f) * 19.5f,
                            6.8f + Mathf.Cos(Time.time * 0.53f) * 3.8f);
                        bossPosition = Vector2.MoveTowards(bossPosition, idleTarget, 2.4f * Time.deltaTime);
                    }
                    if (!attackRunning && Time.time >= nextAttackAt) BeginNextAttack();
                    if (healthRatio <= 0.5f && Time.time >= nextPressureHomingAt)
                    {
                        LaunchPressureHomingVolley(healthRatio);
                    }
                    if (AreAllEliminated() && !retryStarted) StartCoroutine(RetryAfterFailure());
                }
                BroadcastState(false);
            }
            RefreshMonitor();
        }

        private void BuildRoster()
        {
            if (IsOnline)
            {
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                if (roster != null)
                    for (int i = 0; i < roster.Length; i++)
                    {
                        if (roster[i] == null || string.IsNullOrEmpty(roster[i].PlayerId)) continue;
                        int room = PlayerColorPalette.GetLobbyPlayerSlot(onlineManager.CurrentLobby, roster[i].PlayerId);
                        if (room >= 0 && room < roomPlayerIds.Length) roomPlayerIds[room] = roster[i].PlayerId;
                    }
                playerCount = 0;
                for (int i = 0; i < roomPlayerIds.Length; i++)
                    if (!string.IsNullOrEmpty(roomPlayerIds[i])) playerCount = i + 1;
                playerCount = Mathf.Clamp(playerCount, 1, 4);
                return;
            }
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            playerCount = Mathf.Clamp(players.Length, 1, 4);
            for (int i = 0; i < players.Length && i < 4; i++) roomPlayerIds[i] = ResolvePlayerId(players[i]);
        }

        private void BuildArena()
        {
            GameObject backdrop = new GameObject("15-1 Sky Arena");
            backdrop.transform.SetParent(transform, false);
            StageEscortController.AddBoxOutline(backdrop.transform, Vector2.zero, new Vector2(34f, 17f), new Color(0.18f, 0.45f, 0.72f, 0.5f), -20);
            for (int i = 0; i < 4; i++)
            {
                pads[i] = StageFlyingPlayerPad.Create(transform, i, StartPositions[i], PlayerColors[i]);
                pads[i].gameObject.SetActive(i < playerCount);
            }
        }

        private void BuildBoss()
        {
            boss = new GameObject("15-1 Flying Boss").transform;
            boss.SetParent(transform, false);
            boss.position = bossPosition;
            bossFace = new GameObject("Boss Face").transform;
            bossFace.SetParent(boss, false);
            AddDisc(bossFace, "Ink Outline", Vector2.zero, new Vector2(3.7f, 3.25f), new Color(0.12f, 0.05f, 0.18f), 160);
            bossCore = AddDisc(bossFace, "Purple Body", Vector2.zero, new Vector2(3.45f, 3f), new Color(0.55f, 0.18f, 0.82f), 161);
            AddDisc(bossFace, "Left Eye", new Vector2(-0.7f, 0.42f), new Vector2(0.54f, 0.68f), Color.white, 163);
            AddDisc(bossFace, "Right Eye", new Vector2(0.7f, 0.42f), new Vector2(0.54f, 0.68f), Color.white, 163);
            AddDisc(bossFace, "Left Pupil", new Vector2(-0.63f, 0.36f), new Vector2(0.2f, 0.28f), new Color(0.08f, 0.04f, 0.12f), 164);
            AddDisc(bossFace, "Right Pupil", new Vector2(0.77f, 0.36f), new Vector2(0.2f, 0.28f), new Color(0.08f, 0.04f, 0.12f), 164);
            StageEscortController.AddLine(bossFace, new Vector2(-0.82f, -0.55f), new Vector2(0f, -0.85f), 0.12f, Color.white, 164);
            StageEscortController.AddLine(bossFace, new Vector2(0f, -0.85f), new Vector2(0.82f, -0.55f), 0.12f, Color.white, 164);
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                Vector2 from = new Vector2(Mathf.Cos(angle) * 1.55f, Mathf.Sin(angle) * 1.34f);
                Vector2 to = new Vector2(Mathf.Cos(angle) * 2.18f, Mathf.Sin(angle) * 1.88f);
                StageEscortController.AddLine(bossFace, from, to, 0.18f, new Color(0.3f, 0.06f, 0.5f), 159);
            }
            Color bossInk = new Color(0.12f, 0.03f, 0.2f);
            Color neon = new Color(0.2f, 0.9f, 1f);
            // Uneven horns, torn wings and crayon scars keep the silhouette
            // readable while preserving the notebook-doodle style.
            StageEscortController.AddLine(bossFace, new Vector2(-1.12f, 1.18f), new Vector2(-1.65f, 2.02f), 0.2f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(-1.65f, 2.02f), new Vector2(-0.72f, 1.62f), 0.2f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(0.9f, 1.3f), new Vector2(1.38f, 2.15f), 0.2f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(1.38f, 2.15f), new Vector2(1.7f, 1.28f), 0.2f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(-1.62f, 0.55f), new Vector2(-2.75f, 1.15f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(-2.75f, 1.15f), new Vector2(-2.35f, 0.05f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(-2.35f, 0.05f), new Vector2(-2.9f, -0.72f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(1.62f, 0.55f), new Vector2(2.8f, 1.05f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(2.8f, 1.05f), new Vector2(2.32f, -0.02f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(2.32f, -0.02f), new Vector2(2.92f, -0.62f), 0.22f, bossInk, 158);
            StageEscortController.AddLine(bossFace, new Vector2(-1.02f, 0.95f), new Vector2(-0.42f, 0.68f), 0.09f, neon, 167);
            StageEscortController.AddLine(bossFace, new Vector2(-0.78f, 0.56f), new Vector2(-0.28f, 0.36f), 0.07f, neon, 167);
            StageEscortController.AddLine(bossFace, new Vector2(0.25f, -1.08f), new Vector2(0.82f, -1.38f), 0.08f, new Color(1f, 0.28f, 0.55f), 167);

            // Heavy brows, fangs and layered jaw armor give the boss a stronger
            // silhouette while keeping every stroke in the notebook-doodle style.
            StageEscortController.AddLine(bossFace, new Vector2(-1.12f, 0.92f), new Vector2(-0.34f, 0.72f), 0.17f, bossInk, 168);
            StageEscortController.AddLine(bossFace, new Vector2(1.12f, 0.92f), new Vector2(0.34f, 0.72f), 0.17f, bossInk, 168);
            StageEscortController.AddLine(bossFace, new Vector2(-0.62f, -0.65f), new Vector2(-0.38f, -1.18f), 0.14f, Color.white, 168);
            StageEscortController.AddLine(bossFace, new Vector2(0.62f, -0.65f), new Vector2(0.38f, -1.18f), 0.14f, Color.white, 168);
            StageEscortController.AddLine(bossFace, new Vector2(-1.3f, -1.02f), new Vector2(0f, -1.52f), 0.16f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(0f, -1.52f), new Vector2(1.3f, -1.02f), 0.16f, bossInk, 166);
            StageEscortController.AddLine(bossFace, new Vector2(-1.82f, -0.42f), new Vector2(-2.25f, -1.4f), 0.18f, new Color(0.82f, 0.2f, 0.95f), 160);
            StageEscortController.AddLine(bossFace, new Vector2(1.82f, -0.42f), new Vector2(2.25f, -1.4f), 0.18f, new Color(0.82f, 0.2f, 0.95f), 160);
            BuildBossOrbitShards(neon, bossInk);
            SpriteRenderer messyBoss = NicoDrawBossArt.Apply(
                bossFace, "boss-flying", new Vector2(6.2f, 5.2f), 161);
            if (messyBoss != null) bossCore = messyBoss;
        }

        private void BuildBossOrbitShards(Color neon, Color outline)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject shard = new GameObject("Orbiting Ink Blade " + (i + 1));
                shard.transform.SetParent(boss, false);
                StageEscortController.AddLine(shard.transform, new Vector2(-0.42f, 0f), new Vector2(0f, 0.3f), 0.13f, outline, 157);
                StageEscortController.AddLine(shard.transform, new Vector2(0f, 0.3f), new Vector2(0.42f, 0f), 0.13f, neon, 158);
                StageEscortController.AddLine(shard.transform, new Vector2(0.42f, 0f), new Vector2(0f, -0.3f), 0.13f, outline, 157);
                StageEscortController.AddLine(shard.transform, new Vector2(0f, -0.3f), new Vector2(-0.42f, 0f), 0.13f, neon, 158);
                bossOrbitShards.Add(shard.transform);
            }
        }

        private void AnimateBossVisuals()
        {
            if (bossFace != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3.1f) * 0.025f;
                bossFace.localScale = Vector3.one * bossMoodScale * pulse;
                bossFace.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.7f) * 2.2f);
            }
            for (int i = 0; i < bossOrbitShards.Count; i++)
            {
                Transform shard = bossOrbitShards[i];
                if (shard == null) continue;
                float angle = Time.time * (0.72f + i * 0.08f) + i * Mathf.PI * 2f / bossOrbitShards.Count;
                float radiusX = 2.8f + Mathf.Sin(Time.time * 1.4f + i) * 0.18f;
                float radiusY = 2.25f + Mathf.Cos(Time.time * 1.1f + i) * 0.14f;
                shard.localPosition = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0.08f);
                shard.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg + 45f);
            }
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("15-1 Boss HP Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(0f, 7.35f, 0.4f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(15f, 2.35f), 218);
            StageEscortController.AddFilledRect(monitor.transform, "HP Track", new Vector2(0f, -0.25f), new Vector2(11.8f, 0.48f), new Color(0.15f, 0.16f, 0.18f), 222);
            GameObject fill = new GameObject("HP Fill");
            fill.transform.SetParent(monitor.transform, false);
            fill.transform.localPosition = new Vector3(-5.9f, -0.25f, -0.03f);
            fill.transform.localScale = new Vector3(11.8f, 0.34f, 1f);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
            renderer.color = new Color(0.95f, 0.2f, 0.32f);
            renderer.sortingOrder = 224;
            hpFill = fill.transform;
            hpText = StageEscortController.CreateText(monitor.transform, "HP", new Vector3(0f, 0.5f, -0.04f), 54, 0.13f, new Color(0.08f, 0.25f, 0.48f), 225);
        }

        private void BuildAimGuide()
        {
            GameObject root = new GameObject("Local Missile Aim Guide");
            root.transform.SetParent(transform, false);
            aimLine = root.AddComponent<LineRenderer>();
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.startWidth = 0.075f;
            aimLine.endWidth = 0.025f;
            aimLine.numCapVertices = 3;
            aimLine.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            aimLine.startColor = new Color(0.2f, 0.9f, 1f, 0.85f);
            aimLine.endColor = new Color(0.2f, 0.9f, 1f, 0.12f);
            aimLine.sortingOrder = 138;
            aimLine.enabled = false;
        }

        private void UpdateLocalPadInput()
        {
            if (phase == BattlePhase.Defeated || phase == BattlePhase.Failed) return;
            int localRoom = GetLocalRoom();
            if (localRoom < 0 || localRoom >= playerCount || eliminated.Contains(roomPlayerIds[localRoom])) return;
            StageFlyingPlayerPad pad = pads[localRoom];
            if (pad == null) return;
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            pad.SetPosition(ClampPadPosition(pad.Position + input * 7f * Time.deltaTime));
            SyncMountedPlayer(localRoom);

            Vector2 cursor = gameCamera != null
                ? (Vector2)gameCamera.ScreenToWorldPoint(Input.mousePosition)
                : pad.Position + Vector2.right;
            Vector2 aimDirection = (cursor - pad.Position).normalized;
            if (aimDirection.sqrMagnitude < 0.5f) aimDirection = Vector2.right;
            if (aimLine != null)
            {
                aimLine.enabled = phase == BattlePhase.Fighting;
                aimLine.SetPosition(0, pad.Position + aimDirection * 0.9f);
                aimLine.SetPosition(1, pad.Position + aimDirection * 3.4f);
            }

            if (IsOnline && !HasAuthority && Time.unscaledTime >= nextPadSendAt)
            {
                nextPadSendAt = Time.unscaledTime + 0.05f;
                Send(PadKind, new PadMessage { Room = localRoom, Position = pad.Position });
            }
            if (Input.GetMouseButtonDown(0) && phase == BattlePhase.Fighting)
            {
                if (HasAuthority) TryFire(localRoom, aimDirection);
                else Send(ShotRequestKind, new ShotRequest { Direction = aimDirection });
            }
            if (Input.GetKeyDown(KeyCode.F) && phase == BattlePhase.Fighting)
            {
                if (HasAuthority) TryDropBomb(localRoom);
                else Send(BombRequestKind, new PadMessage { Room = localRoom, Position = pad.Position });
            }
        }

        private void TryFire(int room, Vector2 direction)
        {
            if (phase != BattlePhase.Fighting || room < 0 || room >= playerCount || Time.time < nextShotAt[room] || eliminated.Contains(roomPlayerIds[room])) return;
            nextShotAt[room] = Time.time + 0.32f;
            ShotState state = new ShotState { Sequence = ++shotSequence, OwnerRoom = room, Position = pads[room].Position + direction.normalized * 0.8f, Direction = direction.normalized };
            ApplyShot(state);
            if (IsOnline) Send(ShotKind, state);
        }

        private void ApplyShot(ShotState state)
        {
            if (state == null || !receivedShots.Add(state.Sequence)) return;
            shots.Add(StageFlyingBossShot.Create(transform, this, state.Sequence, state.OwnerRoom, state.Position, state.Direction, HasAuthority));
            GameSfx.PlayAt(SfxId.MissileLaunch, state.Position, 0.55f);
        }

        internal bool ResolvePlayerShot(int sequence, int ownerRoom, Vector2 position)
        {
            if (!HasAuthority || phase != BattlePhase.Fighting) return false;
            if (Vector2.Distance(position, bossPosition) <= 1.9f)
            {
                GameSfx.PlayAt(SfxId.MissileImpact, position, 0.9f);
                DamageBoss(PlayerMissileDamage, position);
                return true;
            }
            for (int room = 0; room < playerCount; room++)
            {
                // The shooter is immune to their own missile, but teammates are
                // deliberately valid collision targets for the co-op challenge.
                if (room == ownerRoom || eliminated.Contains(roomPlayerIds[room])) continue;
                if (Vector2.Distance(position, GetRoomHitPosition(room)) > 0.62f) continue;
                GameSfx.PlayAt(SfxId.MissileImpact, position, 0.9f);
                EliminateRoom(room);
                return true;
            }
            return false;
        }

        private void TryDropBomb(int room)
        {
            if (phase != BattlePhase.Fighting || room < 0 || room >= playerCount || Time.time < nextBombAt[room] || eliminated.Contains(roomPlayerIds[room])) return;
            nextBombAt[room] = Time.time + 1.15f;
            BombState state = new BombState { Sequence = ++bombSequence, OwnerRoom = room, Position = pads[room].Position + Vector2.down * 0.65f };
            ApplyBomb(state);
            if (IsOnline) Send(BombKind, state);
        }

        private void ApplyBomb(BombState state)
        {
            if (state == null) return;
            StageFlyingPlayerBomb.Create(transform, this, state.Sequence, state.OwnerRoom, state.Position, HasAuthority);
            GameSfx.PlayAt(SfxId.BombFuseStart, state.Position, 0.7f);
        }

        internal void ResolvePlayerBomb(int ownerRoom, Vector2 position)
        {
            if (!HasAuthority || phase != BattlePhase.Fighting) return;
            if (Vector2.Distance(position, bossPosition) <= 2.7f) DamageBoss(PlayerBombDamage, position);
            for (int room = 0; room < playerCount; room++)
            {
                // Player weapons never eliminate their owner. Other players remain
                // valid friendly-fire targets, matching the missile rule.
                if (room == ownerRoom || eliminated.Contains(roomPlayerIds[room])) continue;
                if (Vector2.Distance(position, GetRoomHitPosition(room)) <= 2.25f) EliminateRoom(room);
            }
        }

        private void DamageBoss(int amount, Vector2 position)
        {
            if (health <= 0) return;
            health = Mathf.Max(0, health - Mathf.Max(1, amount));
            StartCoroutine(BossHitFlash());
            StageBossImpactFlash.Create(transform, position, new Color(1f, 0.78f, 0.18f));
            BroadcastState(true);
            if (health <= 0) StartCoroutine(DefeatBoss());
        }

        private void BeginNextAttack()
        {
            float ratio = health / (float)Mathf.Max(1, maximumHealth);
            List<AttackType> choices = new List<AttackType> { AttackType.Dash, AttackType.Beam };
            if (ratio <= 0.7f) { choices.Add(AttackType.Homing); choices.Add(AttackType.Chase); }
            if (ratio <= 0.5f) choices.Add(AttackType.Suction);
            AttackType type = choices[attackCursor++ % choices.Count];
            if (Time.time - lastDashAt >= 11f) type = AttackType.Dash;
            else if (type != AttackType.Dash && choices.Count > 2 && Random.value < 0.42f)
                type = choices[Random.Range(1, choices.Count)];
            if (type == AttackType.Dash) lastDashAt = Time.time;
            AttackState state = BuildAttackState(type);
            ApplyAttack(state);
            if (IsOnline) Send(AttackKind, state);
        }

        private void LaunchPressureHomingVolley(float healthRatio)
        {
            float pressure = Mathf.InverseLerp(0.5f, 0f, healthRatio);
            nextPressureHomingAt = Time.time + Mathf.Lerp(3.2f, 1.05f, pressure);
            HomingVolleyState state = new HomingVolleyState
            {
                Sequence = ++homingVolleySequence,
                Origin = bossPosition
            };
            ApplyPressureHomingVolley(state);
            if (IsOnline) Send(HomingVolleyKind, state);
        }

        private void ApplyPressureHomingVolley(HomingVolleyState state)
        {
            if (state == null || !receivedHomingVolleys.Add(state.Sequence)) return;
            StartCoroutine(RunPressureHomingVolley(state));
        }

        private IEnumerator RunPressureHomingVolley(HomingVolleyState state)
        {
            GameSfx.PlayAt(SfxId.BossAttackWarning, state.Origin, 0.9f);
            yield return new WaitForSeconds(0.75f);
            GameSfx.PlayAt(SfxId.MissileLaunch, state.Origin, 0.95f);
            for (int i = 0; i < playerCount; i++)
            {
                if (!eliminated.Contains(roomPlayerIds[i]))
                    StageFlyingHomingHazard.Create(transform, this, i, state.Origin, HasAuthority);
            }
        }

        private AttackState BuildAttackState(AttackType type)
        {
            int target = RandomLivingRoom();
            Vector2 origin = Vector2.zero;
            Vector2 direction = Vector2.right;
            float lane = 0f;
            if (type == AttackType.Dash)
            {
                if (Random.value < 0.58f)
                {
                    direction = RandomVariedDiagonalDirection();
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    Vector2 center = perpendicular * Random.Range(-2.6f, 2.6f);
                    origin = center - direction * 21f;
                    lane = center.y;
                }
                else
                {
                    bool left = Random.value < 0.5f;
                    lane = Random.Range(-5.8f, 5.8f);
                    origin = new Vector2(left ? -20f : 20f, lane);
                    direction = left ? Vector2.right : Vector2.left;
                }
            }
            else if (type == AttackType.Beam)
            {
                if (Random.value < 0.68f)
                {
                    direction = RandomVariedDiagonalDirection();
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    Vector2 center = perpendicular * Random.Range(-2.9f, 2.9f);
                    origin = center - direction * 21f;
                    lane = center.y;
                }
                else
                {
                    int side = Random.Range(0, 4);
                    origin = side == 0 ? new Vector2(-18.5f, 0f) : side == 1 ? new Vector2(18.5f, 0f)
                        : side == 2 ? new Vector2(0f, 10f) : new Vector2(0f, -10f);
                    direction = -origin.normalized;
                    lane = Random.Range(-4.8f, 4.8f);
                    if (side < 2) origin.y = lane; else origin.x = lane;
                }
            }
            else if (type == AttackType.Suction)
            {
                int side = Random.Range(0, 4);
                origin = side == 0 ? new Vector2(-18.5f, 0f) : side == 1 ? new Vector2(18.5f, 0f)
                    : side == 2 ? new Vector2(0f, 10f) : new Vector2(0f, -10f);
                direction = -origin.normalized;
                lane = Random.Range(-4.8f, 4.8f);
                if (side < 2) origin.y = lane; else origin.x = lane;
            }
            else origin = bossPosition;
            return new AttackState { Sequence = ++attackSequence, Type = (int)type, TargetRoom = target, Origin = origin, Direction = direction, Lane = lane };
        }

        private static Vector2 RandomVariedDiagonalDirection()
        {
            // Deliberately avoid a mechanical 45-degree diagonal. Both shallow
            // and steep angles can be selected in every quadrant.
            float angle = Random.value < 0.5f ? Random.Range(14f, 34f) : Random.Range(56f, 76f);
            float xSign = Random.value < 0.5f ? -1f : 1f;
            float ySign = Random.value < 0.5f ? -1f : 1f;
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * xSign, Mathf.Sin(radians) * ySign).normalized;
        }

        private void ApplyAttack(AttackState state)
        {
            if (state == null || !receivedAttacks.Add(state.Sequence)) return;
            attackRunning = true;
            StartCoroutine(RunAttack(state));
        }

        private IEnumerator RunAttack(AttackState state)
        {
            AttackType type = (AttackType)state.Type;
            if (type == AttackType.Dash) yield return RunDash(state);
            else if (type == AttackType.Beam) yield return RunBeam(state);
            else if (type == AttackType.Homing) yield return RunHoming(state);
            else if (type == AttackType.Chase) yield return RunChase(state);
            else yield return RunSuction(state);
            attackRunning = false;
            float ratio = health / (float)Mathf.Max(1, maximumHealth);
            nextAttackAt = Time.time + (ratio <= 0.3f ? 0.55f : ratio <= 0.7f ? 1.15f : 1.8f);
        }

        private IEnumerator RunDash(AttackState state)
        {
            SetBossMood(new Color(1f, 0.22f, 0.15f), 1.22f);
            Vector2 dashDirection = state.Direction.sqrMagnitude > 0.01f
                ? state.Direction.normalized
                : state.Origin.x < 0f ? Vector2.right : Vector2.left;
            Vector2 warningCenter = state.Origin + dashDirection * 20f;
            GameObject warning = CreateDirectionalWarning(warningCenter, dashDirection, 40f, 2.2f, new Color(1f, 0.16f, 0.1f, 0.3f));
            GameSfx.PlayAt(SfxId.BossAttackWarning, bossPosition, 1.05f);
            // The idle path can leave the boss nearly an arena-width away from
            // the selected dash side. Give that setup travel enough time, then
            // snap to the synchronized start so the actual charge can never be
            // swallowed by an unfinished approach.
            float setupDistance = Vector2.Distance(bossPosition, state.Origin);
            float setupDuration = Mathf.Clamp(setupDistance / 18f, 0.7f, 2.35f);
            yield return MoveBossForAttackSetup(state.Origin, setupDuration);
            bossPosition = state.Origin;
            if (boss != null) boss.position = new Vector3(bossPosition.x, bossPosition.y, -0.25f);
            yield return new WaitForSeconds(0.12f);
            if (warning != null) Destroy(warning);
            GameSfx.PlayAt(SfxId.BossDash, bossPosition, 1.15f);
            Vector2 end = state.Origin + dashDirection * 40f;
            LineRenderer trail = CreateDashTrail(bossPosition, dashDirection);
            float dashEndsAt = Time.time + 1.25f;
            while (Vector2.Distance(bossPosition, end) > 0.05f && Time.time < dashEndsAt)
            {
                bossPosition = Vector2.MoveTowards(bossPosition, end, 43f * Time.deltaTime);
                if (trail != null)
                {
                    trail.SetPosition(0, bossPosition - dashDirection * 4.2f);
                    trail.SetPosition(1, bossPosition - dashDirection * 1.45f);
                }
                if (HasAuthority) HitPadsNear(bossPosition, 1.8f);
                yield return null;
            }
            bossPosition = end;
            if (trail != null) Destroy(trail.gameObject);
            SetBossMood(new Color(0.55f, 0.18f, 0.82f), 1f);
        }

        private LineRenderer CreateDashTrail(Vector2 position, Vector2 direction)
        {
            GameObject trailObject = new GameObject("Crayon Devil Dash Trail");
            trailObject.transform.SetParent(transform, false);
            LineRenderer trail = trailObject.AddComponent<LineRenderer>();
            trail.useWorldSpace = true;
            trail.positionCount = 2;
            trail.numCapVertices = 5;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.startWidth = 0.18f;
            trail.endWidth = 1.15f;
            trail.startColor = new Color(1f, 0.75f, 0.12f, 0f);
            trail.endColor = new Color(0.85f, 0.14f, 0.95f, 0.8f);
            trail.sortingOrder = 156;
            trail.SetPosition(0, position - direction * 4.2f);
            trail.SetPosition(1, position - direction * 1.45f);
            return trail;
        }

        private IEnumerator RunBeam(AttackState state)
        {
            SetBossMood(new Color(1f, 0.38f, 0.08f), 1.12f);
            Vector2 direction = state.Direction.sqrMagnitude > 0.01f ? state.Direction.normalized : Vector2.right;
            Vector2 center = state.Origin + direction * 20f;
            GameObject warning = CreateDirectionalWarning(center, direction, 40f, 2.5f, new Color(1f, 0.65f, 0.05f, 0.22f));
            GameSfx.PlayAt(SfxId.BossBeamCharge, state.Origin, 1.05f);
            yield return MoveBossForAttackSetup(state.Origin, 1.7f);
            SetWarningColor(warning, new Color(1f, 0.08f, 0.03f, 0.78f));
            GameObject innerBeam = CreateDirectionalWarning(center, direction, 40f, 1.25f, new Color(1f, 0.2f, 0.04f, 0.98f));
            GameObject beamCore = CreateDirectionalWarning(center, direction, 40f, 0.38f, new Color(1f, 0.96f, 0.68f, 1f));
            GameSfx.PlayAt(SfxId.BeamFire, state.Origin, 1f);
            float elapsed = 0f;
            while (elapsed < 0.75f)
            {
                elapsed += Time.deltaTime;
                if (HasAuthority)
                    for (int i = 0; i < playerCount; i++)
                        if (!eliminated.Contains(roomPlayerIds[i]) &&
                            DistanceToSegment(GetRoomHitPosition(i), state.Origin, state.Origin + direction * 40f) < 1.25f)
                            EliminateRoom(i);
                yield return null;
            }
            Destroy(warning);
            Destroy(innerBeam);
            Destroy(beamCore);
            SetBossMood(new Color(0.55f, 0.18f, 0.82f), 1f);
        }

        private IEnumerator RunHoming(AttackState state)
        {
            SetBossMood(new Color(0.82f, 0.2f, 0.92f), 1.08f);
            Vector2 launchPoint = new Vector2(0f, 10.2f);
            GameSfx.PlayAt(SfxId.BossAttackWarning, launchPoint);
            yield return MoveBossForAttackSetup(launchPoint, 1.45f);
            GameSfx.PlayAt(SfxId.MissileLaunch, bossPosition, 1.05f);
            for (int i = 0; i < playerCount; i++)
                if (!eliminated.Contains(roomPlayerIds[i])) StageFlyingHomingHazard.Create(transform, this, i, bossPosition, HasAuthority);
            yield return new WaitForSeconds(4.2f);
            SetBossMood(new Color(0.55f, 0.18f, 0.82f), 1f);
        }

        private IEnumerator RunChase(AttackState state)
        {
            int target = Mathf.Clamp(state.TargetRoom, 0, playerCount - 1);
            SetBossMood(new Color(1f, 0.12f, 0.2f), 1.15f);
            GameSfx.PlayAt(SfxId.BossAttackWarning, GetRoomHitPosition(target), 1.05f);
            yield return new WaitForSeconds(1.35f);
            float elapsed = 0f;
            while (elapsed < 2.5f)
            {
                elapsed += Time.deltaTime;
                bossPosition = Vector2.MoveTowards(bossPosition, GetRoomHitPosition(target), 5f * Time.deltaTime);
                yield return null;
            }
            Vector2 direction = (GetRoomHitPosition(target) - bossPosition).normalized;
            Vector2 end = bossPosition + direction * 13f;
            Vector2 start = bossPosition;
            GameSfx.PlayAt(SfxId.BossDash, bossPosition, 1.1f);
            elapsed = 0f;
            while (elapsed < 0.65f)
            {
                elapsed += Time.deltaTime;
                bossPosition = Vector2.Lerp(start, end, elapsed / 0.65f);
                if (HasAuthority) HitPadsNear(bossPosition, 1.8f);
                yield return null;
            }
            SetBossMood(new Color(0.55f, 0.18f, 0.82f), 1f);
        }

        private IEnumerator RunSuction(AttackState state)
        {
            SetBossMood(new Color(0.18f, 0.05f, 0.28f), 1.3f);
            GameObject warning = CreateWarningRect(Vector2.zero, new Vector2(34f, 17f), new Color(0.45f, 0.15f, 0.8f, 0.12f));
            GameSfx.PlayAt(SfxId.BossAttackWarning, state.Origin, 1.05f);
            yield return MoveBossForAttackSetup(state.Origin, 1.5f);
            GameSfx.PlayAt(SfxId.BossSuction, bossPosition, 1.1f);
            float elapsed = 0f;
            while (elapsed < 3.2f)
            {
                elapsed += Time.deltaTime;
                if (HasAuthority)
                    for (int i = 0; i < playerCount; i++)
                        if (!eliminated.Contains(roomPlayerIds[i])) pads[i].SetPosition(ClampPadPosition(Vector2.MoveTowards(pads[i].Position, bossPosition, 7.8f * Time.deltaTime)));
                yield return null;
            }
            Destroy(warning);
            SetBossMood(new Color(0.55f, 0.18f, 0.82f), 1f);
        }

        private IEnumerator MoveBossForAttackSetup(Vector2 target, float duration)
        {
            Vector2 start = bossPosition;
            Vector2 delta = target - start;
            Vector2 perpendicular = delta.sqrMagnitude > 0.01f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            float arc = Mathf.Clamp(delta.magnitude * 0.18f, 1.2f, 3.5f);
            Vector2 control = (start + target) * 0.5f + perpendicular * arc;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                float inverse = 1f - t;
                bossPosition = inverse * inverse * start + 2f * inverse * t * control + t * t * target;
                yield return null;
            }
            bossPosition = target;
        }

        internal Vector2 GetRoomHitPosition(int room)
        {
            return room >= 0 && room < pads.Length && pads[room] != null
                ? pads[room].Position + Vector2.up * 0.72f
                : Vector2.zero;
        }
        internal void HazardHitRoom(int room) { if (HasAuthority) EliminateRoom(room); }

        private void HitPadsNear(Vector2 point, float radius)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (eliminated.Contains(roomPlayerIds[i])) continue;
                if (Vector2.Distance(GetRoomHitPosition(i), point) <= radius) EliminateRoom(i);
            }
        }

        private void EliminateRoom(int room)
        {
            if (room < 0 || room >= playerCount) return;
            string id = roomPlayerIds[room];
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null && (player.IsInvulnerable || player.IsTurtleShelled)) return;
            ApplyElimination(id);
            if (IsOnline) Send(EliminateKind, new EliminationMessage { PlayerId = id });
            BroadcastState(true);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            int room = FindRoom(ResolvePlayerId(target));
            if (HasAuthority) EliminateRoom(room);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            int room = FindRoom(id);
            if (room >= 0 && pads[room] != null) pads[room].SetDefeated(true);
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) SetPlayerVisible(player, false);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private IEnumerator DefeatBoss()
        {
            if (phase == BattlePhase.Defeated) yield break;
            phase = BattlePhase.Defeated;
            attackRunning = true;
            SetBossMood(Color.white, 1.35f);
            BroadcastState(true);
            for (int i = 0; i < 7; i++)
            {
                Vector2 point = bossPosition + Random.insideUnitCircle * 1.5f;
                GameObject explosion = new GameObject("Boss Defeat Burst");
                explosion.transform.position = point;
                explosion.AddComponent<BombExplosionVisual>().Configure(Random.Range(0.8f, 1.5f), false);
                GameSfx.PlayAt(SfxId.BombExplosion, point, 0.65f);
                yield return new WaitForSeconds(0.22f);
            }
            yield return new WaitForSeconds(1.4f);
            stageManager.ClearStage();
        }

        private IEnumerator RetryAfterFailure()
        {
            retryStarted = true;
            phase = BattlePhase.Failed;
            BroadcastState(true);
            yield return new WaitForSeconds(3f);
            stageManager.Retry();
        }

        private IEnumerator BossHitFlash()
        {
            if (bossCore == null) yield break;
            Color old = bossCore.color;
            bossCore.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (bossCore != null) bossCore.color = old;
        }

        private void SetBossMood(Color color, float scale)
        {
            if (bossCore != null) bossCore.color = color;
            bossMoodScale = scale;
        }

        private void MountLocalPlayers()
        {
            if (IsOnline)
            {
                // Every client mounts every representation to the synchronized
                // craft. Otherwise remote body interpolation visibly trails the pad.
                for (int room = 0; room < playerCount; room++)
                    MountPlayer(ResolvePlayer(roomPlayerIds[room]), room);
                return;
            }
            for (int i = 0; i < playerCount; i++) MountPlayer(ResolvePlayer(roomPlayerIds[i]), i);
        }

        private void MountPlayer(PlayerController2D player, int room)
        {
            if (player == null || room < 0 || room >= playerCount) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.SetControlsEnabled(false);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
            if (!mountedPlayers.Contains(player)) mountedPlayers.Add(player);
            SyncMountedPlayer(room);
        }

        private void SyncMountedPlayer(int room)
        {
            if (room < 0 || room >= playerCount || pads[room] == null) return;
            PlayerController2D player = ResolvePlayer(roomPlayerIds[room]);
            if (player == null || eliminated.Contains(roomPlayerIds[room])) return;
            player.transform.position = new Vector3(pads[room].Position.x, pads[room].Position.y + 0.72f, -0.2f);
        }

        private void LateUpdate()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            for (int room = 0; room < playerCount; room++) SyncMountedPlayer(room);
        }

        private void RestorePlayers()
        {
            for (int i = 0; i < mountedPlayers.Count; i++)
            {
                PlayerController2D player = mountedPlayers[i];
                if (player == null) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.simulated = true;
                SetPlayerVisible(player, true);
            }
            mountedPlayers.Clear();
        }

        private static void SetPlayerVisible(PlayerController2D player, bool value)
        {
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = value;
        }

        private void RefreshMonitor()
        {
            if (hpText == null) return;
            hpText.text = LocalizationManager.Format("flying_boss_hp", health, maximumHealth);
            float ratio = Mathf.Clamp01(health / (float)Mathf.Max(1, maximumHealth));
            if (hpFill != null)
            {
                hpFill.localScale = new Vector3(11.8f * ratio, 0.34f, 1f);
                hpFill.localPosition = new Vector3(-5.9f + 5.9f * ratio, -0.25f, -0.03f);
            }
            if (statusText != null) statusText.text = string.Empty;
        }

        private int RandomLivingRoom()
        {
            List<int> living = new List<int>();
            for (int i = 0; i < playerCount; i++) if (!eliminated.Contains(roomPlayerIds[i])) living.Add(i);
            return living.Count > 0 ? living[Random.Range(0, living.Count)] : 0;
        }

        private bool AreAllEliminated()
        {
            for (int i = 0; i < playerCount; i++) if (!eliminated.Contains(roomPlayerIds[i])) return false;
            return true;
        }

        private int GetLocalRoom() => FindRoom(IsOnline ? onlineManager?.LocalPlayerId : roomPlayerIds[0]);
        private int FindRoom(string id) { for (int i = 0; i < playerCount; i++) if (roomPlayerIds[i] == id) return i; return -1; }
        private static Vector2 ClampPadPosition(Vector2 value) => new Vector2(Mathf.Clamp(value.x, -15.5f, 15.5f), Mathf.Clamp(value.y, -7.2f, 6.2f));

        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
            if (roster == null) return false;
            for (int i = 0; i < roster.Length; i++) if (roster[i] != null && roster[i].IsHost && roster[i].PlayerId == id) return true;
            return false;
        }

        private void BroadcastState(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.05f;
            Vector2[] positions = new Vector2[4];
            for (int i = 0; i < 4; i++) positions[i] = pads[i] != null ? pads[i].Position : StartPositions[i];
            Send(StateKind, new BossState
            {
                Sequence = ++stateSequence, Phase = (int)phase, Health = health, MaximumHealth = maximumHealth,
                BossPosition = bossPosition, PadPositions = positions, RoomPlayerIds = (string[])roomPlayerIds.Clone(),
                EliminatedIds = new List<string>(eliminated).ToArray()
            });
        }

        private void ApplyState(BossState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            phase = (BattlePhase)Mathf.Clamp(state.Phase, 0, (int)BattlePhase.Failed);
            health = state.Health;
            maximumHealth = state.MaximumHealth;
            // Attack packets contain the complete deterministic path. While a
            // client is playing that path, an older 10 Hz snapshot must not
            // pull the boss back toward its setup position and visually erase
            // the dash. Normal snapshot correction resumes after the attack.
            if (!attackRunning) bossPosition = state.BossPosition;
            if (state.RoomPlayerIds != null) System.Array.Copy(state.RoomPlayerIds, roomPlayerIds, Mathf.Min(4, state.RoomPlayerIds.Length));
            if (state.PadPositions != null)
            {
                int localRoom = GetLocalRoom();
                for (int i = 0; i < Mathf.Min(4, state.PadPositions.Length); i++)
                {
                    if (pads[i] == null) continue;
                    if (i == localRoom)
                    {
                        // The participant predicts their own craft locally. Only
                        // correct a genuine large divergence; applying every old
                        // host echo caused a visible tug every snapshot.
                        if (Vector2.Distance(pads[i].Position, state.PadPositions[i]) > 2.5f)
                            pads[i].SetPosition(Vector2.Lerp(pads[i].Position, state.PadPositions[i], 0.18f));
                    }
                    else pads[i].SetNetworkTarget(state.PadPositions[i]);
                }
            }
            if (state.EliminatedIds != null) for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyState(JsonUtility.FromJson<BossState>(data.Json));
            else if (data.Kind == PadKind && HasAuthority)
            {
                PadMessage pad = JsonUtility.FromJson<PadMessage>(data.Json);
                int room = FindRoom(data.PlayerId);
                if (pad != null && room == pad.Room && room >= 0) pads[room].SetNetworkTarget(ClampPadPosition(pad.Position));
            }
            else if (data.Kind == ShotRequestKind && HasAuthority)
            {
                ShotRequest request = JsonUtility.FromJson<ShotRequest>(data.Json);
                int room = FindRoom(data.PlayerId);
                if (request != null && room >= 0) TryFire(room, request.Direction.normalized);
            }
            else if (data.Kind == BombRequestKind && HasAuthority)
            {
                int room = FindRoom(data.PlayerId);
                if (room >= 0) TryDropBomb(room);
            }
            else if (data.Kind == ShotKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyShot(JsonUtility.FromJson<ShotState>(data.Json));
            else if (data.Kind == BombKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyBomb(JsonUtility.FromJson<BombState>(data.Json));
            else if (data.Kind == AttackKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyAttack(JsonUtility.FromJson<AttackState>(data.Json));
            else if (data.Kind == HomingVolleyKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyPressureHomingVolley(JsonUtility.FromJson<HomingVolleyState>(data.Json));
            else if (data.Kind == EliminateKind && IsHostPlayer(data.PlayerId))
            {
                EliminationMessage message = JsonUtility.FromJson<EliminationMessage>(data.Json);
                if (message != null) ApplyElimination(message.PlayerId);
            }
        }

        private void Send(string kind, object state)
        {
            if (onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = kind, Json = JsonUtility.ToJson(state) });
        }

        private void LockCamera()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            if (cameraFollow != null) { previousFollowEnabled = cameraFollow.enabled; cameraFollow.enabled = false; }
            gameCamera.transform.position = new Vector3(0f, 0f, previousCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(10.8f, 19f / Mathf.Max(0.1f, gameCamera.aspect));
        }

        private void RestoreCamera()
        {
            if (gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousFollowEnabled;
        }

        private GameObject CreateWarningRect(Vector2 position, Vector2 size, Color color)
        {
            GameObject root = new GameObject("Boss Attack Warning");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            StageEscortController.AddFilledRect(root.transform, "Warning", Vector2.zero, size, color, 135);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(1f, 0.15f, 0.08f, 0.9f), 136);
            return root;
        }

        private GameObject CreateDirectionalWarning(Vector2 center, Vector2 direction, float length, float width, Color color)
        {
            GameObject warning = CreateWarningRect(center, new Vector2(length, width), color);
            if (warning != null)
                warning.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return warning;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float denominator = segment.sqrMagnitude;
            if (denominator <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / denominator);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void SetWarningColor(GameObject warning, Color color)
        {
            if (warning == null) return;
            SpriteRenderer[] renderers = warning.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].color = color;
        }

        private static SpriteRenderer AddDisc(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }

    public sealed class StageFlyingPlayerPad : MonoBehaviour
    {
        private SpriteRenderer fill;
        private Transform craftVisual;
        private Transform playerColorLamp;
        private float craftScaleX;
        private TextMesh label;
        private bool hasNetworkTarget;
        private Vector2 networkTarget;
        private Vector2 networkTargetVelocity;
        private Vector2 previousNetworkTarget;
        private float networkTargetReceivedAt = -1f;
        public Vector2 Position => transform.position;

        public static StageFlyingPlayerPad Create(Transform parent, int room, Vector2 position, Color color)
        {
            GameObject root = new GameObject("Player Flying Pad P" + (room + 1));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            StageFlyingPlayerPad pad = root.AddComponent<StageFlyingPlayerPad>();
            Sprite craftSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/flying-player-craft");
            if (craftSprite != null && craftSprite.bounds.size.x > 0f && craftSprite.bounds.size.y > 0f)
            {
                GameObject visual = new GameObject("Colored Pencil Player Hovercraft");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                visual.transform.localScale = new Vector3(
                    4.35f / craftSprite.bounds.size.x,
                    1.45f / craftSprite.bounds.size.y,
                    1f);
                pad.craftVisual = visual.transform;
                pad.craftScaleX = Mathf.Abs(visual.transform.localScale.x);
                pad.fill = visual.AddComponent<SpriteRenderer>();
                pad.fill.sprite = craftSprite;
                pad.fill.color = Color.white;
                pad.fill.sortingOrder = 83;

                GameObject playerLamp = new GameObject("Player Color Lamp");
                playerLamp.transform.SetParent(root.transform, false);
                playerLamp.transform.localPosition = new Vector3(0.92f, 0.02f, -0.03f);
                pad.playerColorLamp = playerLamp.transform;
                playerLamp.transform.localScale = Vector3.one * 0.22f;
                SpriteRenderer lampRenderer = playerLamp.AddComponent<SpriteRenderer>();
                lampRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
                lampRenderer.color = color;
                lampRenderer.sortingOrder = 85;
                pad.SetFacing(position.x > 0f ? -1f : 1f);
            }
            else
            {
                GameObject visual = new GameObject("Pad Fill");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(3.2f, 0.62f, 1f);
                pad.fill = visual.AddComponent<SpriteRenderer>();
                pad.fill.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
                pad.fill.color = color;
                pad.fill.sortingOrder = 80;
                StageEscortController.AddBoxOutline(root.transform, Vector2.zero, new Vector2(3.25f, 0.68f), new Color(0.08f, 0.12f, 0.16f), 82);
                StageEscortController.AddFilledRect(root.transform, "Cockpit Panel", Vector2.zero, new Vector2(1.08f, 0.44f), new Color(0.05f, 0.16f, 0.24f, 0.9f), 81);
                StageEscortController.AddLine(root.transform, new Vector2(-1.58f, 0.08f), new Vector2(-2.05f, 0.48f), 0.12f, color, 79);
                StageEscortController.AddLine(root.transform, new Vector2(-2.05f, 0.48f), new Vector2(-1.38f, -0.18f), 0.12f, color, 79);
                StageEscortController.AddLine(root.transform, new Vector2(1.58f, 0.08f), new Vector2(2.05f, 0.48f), 0.12f, color, 79);
                StageEscortController.AddLine(root.transform, new Vector2(2.05f, 0.48f), new Vector2(1.38f, -0.18f), 0.12f, color, 79);
                AddThruster(root.transform, new Vector2(-0.92f, -0.52f), color);
                AddThruster(root.transform, new Vector2(0.92f, -0.52f), color);
            }
            pad.label = StageEscortController.CreateText(root.transform, "Player Label", new Vector3(0f, 0.02f, -0.04f), 34, 0.078f, Color.white, 86);
            pad.label.text = "P" + (room + 1);
            return pad;
        }

        private static void AddThruster(Transform parent, Vector2 position, Color color)
        {
            GameObject outer = new GameObject("Thruster Ink");
            outer.transform.SetParent(parent, false);
            outer.transform.localPosition = position;
            outer.transform.localScale = new Vector3(0.55f, 0.42f, 1f);
            SpriteRenderer rim = outer.AddComponent<SpriteRenderer>();
            rim.sprite = DoodleRuntimeAssets.CircleSprite;
            rim.color = new Color(0.07f, 0.1f, 0.14f);
            rim.sortingOrder = 82;
            GameObject glow = new GameObject("Thruster Glow");
            glow.transform.SetParent(outer.transform, false);
            glow.transform.localScale = Vector3.one * 0.66f;
            SpriteRenderer light = glow.AddComponent<SpriteRenderer>();
            light.sprite = DoodleRuntimeAssets.CircleSprite;
            light.color = color;
            light.sortingOrder = 83;
        }

        public void SetPosition(Vector2 position)
        {
            hasNetworkTarget = false;
            ApplyPosition(position);
        }

        public void SetNetworkTarget(Vector2 position)
        {
            float now = Time.unscaledTime;
            if (hasNetworkTarget && networkTargetReceivedAt >= 0f)
            {
                float elapsed = now - networkTargetReceivedAt;
                if (elapsed >= 0.015f && elapsed <= 0.3f)
                {
                    Vector2 measuredVelocity = (position - previousNetworkTarget) / elapsed;
                    networkTargetVelocity = Vector2.Lerp(networkTargetVelocity, measuredVelocity, 0.65f);
                }
            }
            else
            {
                networkTargetVelocity = Vector2.zero;
            }
            previousNetworkTarget = position;
            networkTarget = position;
            networkTargetReceivedAt = now;
            hasNetworkTarget = true;
        }

        private void Update()
        {
            if (!hasNetworkTarget) return;
            float age = Mathf.Max(0f, Time.unscaledTime - networkTargetReceivedAt);
            Vector2 predicted = networkTarget + Vector2.ClampMagnitude(networkTargetVelocity, 9f) * Mathf.Min(age, 0.075f);
            Vector2 current = transform.position;
            if (Vector2.Distance(current, predicted) > 4f)
            {
                ApplyPosition(predicted);
                return;
            }
            float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
            ApplyPosition(Vector2.Lerp(current, predicted, blend));
        }

        private void ApplyPosition(Vector2 position)
        {
            float horizontalMovement = position.x - transform.position.x;
            if (Mathf.Abs(horizontalMovement) >= 0.025f)
            {
                SetFacing(Mathf.Sign(horizontalMovement));
            }
            transform.position = new Vector3(position.x, position.y, -0.1f);
        }

        private void SetFacing(float direction)
        {
            if (craftVisual == null || craftScaleX <= 0f) return;
            float facing = direction < 0f ? -1f : 1f;
            Vector3 scale = craftVisual.localScale;
            scale.x = craftScaleX * facing;
            craftVisual.localScale = scale;
            if (playerColorLamp != null)
            {
                Vector3 lampPosition = playerColorLamp.localPosition;
                lampPosition.x = Mathf.Abs(lampPosition.x) * facing;
                playerColorLamp.localPosition = lampPosition;
            }
        }
        public void SetDefeated(bool value) { if (fill != null) fill.color = value ? new Color(0.25f, 0.25f, 0.28f, 0.55f) : fill.color; }
    }

    public sealed class StageFlyingBossShot : MonoBehaviour
    {
        private StageFlyingPlatformBossController owner;
        private int sequence;
        private int ownerRoom;
        private Vector2 velocity;
        private float expiresAt;
        private bool authoritative;

        public static StageFlyingBossShot Create(Transform parent, StageFlyingPlatformBossController owner, int sequence, int ownerRoom, Vector2 position, Vector2 direction, bool authoritative)
        {
            GameObject root = new GameObject("Player Missile " + sequence);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            StageFlyingBossShot shot = root.AddComponent<StageFlyingBossShot>();
            shot.owner = owner; shot.sequence = sequence; shot.ownerRoom = ownerRoom; shot.velocity = direction.normalized * 13f; shot.expiresAt = Time.time + 4.5f; shot.authoritative = authoritative;
            BossAttackVisuals.AddMissile(root.transform, 1.05f, 0.36f,
                new Color(0.18f, 0.8f, 1f), new Color(1f, 0.82f, 0.18f), 145);
            return shot;
        }

        private void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (authoritative && owner != null && owner.ResolvePlayerShot(sequence, ownerRoom, transform.position))
            {
                Destroy(gameObject);
                return;
            }
            if (Time.time >= expiresAt || Mathf.Abs(transform.position.x) > 24f || Mathf.Abs(transform.position.y) > 15f) Destroy(gameObject);
        }
    }

    public sealed class StageFlyingHomingHazard : MonoBehaviour
    {
        private StageFlyingPlatformBossController owner;
        private int targetRoom;
        private Vector2 velocity;
        private float expiresAt;
        private bool authoritative;

        public static void Create(Transform parent, StageFlyingPlatformBossController owner, int room, Vector2 position, bool authoritative)
        {
            GameObject root = new GameObject("Boss Homing Missile P" + (room + 1));
            root.transform.SetParent(parent, false);
            root.transform.position = position + new Vector2((room - 1.5f) * 0.28f, -room * 0.08f);
            StageFlyingHomingHazard hazard = root.AddComponent<StageFlyingHomingHazard>();
            hazard.owner = owner; hazard.targetRoom = room; hazard.authoritative = authoritative;
            hazard.velocity = Vector2.down * 3f; hazard.expiresAt = Time.time + 15f;
            BossAttackVisuals.AddMissile(root.transform, 1.2f, 0.44f,
                new Color(1f, 0.2f, 0.48f), new Color(0.2f, 0.95f, 1f), 150);
        }

        private void Update()
        {
            Vector2 target = owner != null ? owner.GetRoomHitPosition(targetRoom) : Vector2.zero;
            Vector2 desired = (target - (Vector2)transform.position).normalized * 5.2f;
            velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-2.3f * Time.deltaTime));
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            if (Vector2.Distance(transform.position, target) < 0.72f)
            {
                if (authoritative) owner?.HazardHitRoom(targetRoom);
                GameObject explosion = new GameObject("Homing Missile Burst");
                explosion.transform.position = transform.position;
                explosion.AddComponent<BombExplosionVisual>().Configure(0.9f, false);
                GameSfx.PlayAt(SfxId.MissileImpact, transform.position, 0.9f);
                Destroy(gameObject);
            }
            else if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }

    public sealed class StageFlyingPlayerBomb : MonoBehaviour
    {
        private StageFlyingPlatformBossController owner;
        private int ownerRoom;
        private float explodeAt;
        private float verticalSpeed;
        private bool authoritative;
        private TextMesh timer;

        public static void Create(Transform parent, StageFlyingPlatformBossController owner, int sequence,
            int ownerRoom, Vector2 position, bool authoritative)
        {
            GameObject root = new GameObject("Player Drop Bomb " + sequence);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            StageFlyingPlayerBomb bomb = root.AddComponent<StageFlyingPlayerBomb>();
            bomb.owner = owner;
            bomb.ownerRoom = ownerRoom;
            bomb.authoritative = authoritative;
            bomb.explodeAt = Time.time + 1.65f;
            bomb.verticalSpeed = -2.3f;

            GameObject outline = new GameObject("Bomb Ink Outline");
            outline.transform.SetParent(root.transform, false);
            outline.transform.localScale = Vector3.one * 0.92f;
            SpriteRenderer outer = outline.AddComponent<SpriteRenderer>();
            outer.sprite = DoodleRuntimeAssets.CircleSprite;
            outer.color = new Color(0.12f, 0.05f, 0.06f);
            outer.sortingOrder = 148;
            GameObject body = new GameObject("Bomb Red Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer inner = body.AddComponent<SpriteRenderer>();
            inner.sprite = DoodleRuntimeAssets.CircleSprite;
            inner.color = new Color(0.88f, 0.18f, 0.12f);
            inner.sortingOrder = 149;
            StageEscortController.AddLine(root.transform, new Vector2(0.18f, 0.36f), new Vector2(0.42f, 0.72f), 0.1f, new Color(0.18f, 0.12f, 0.05f), 150);
            StageEscortController.AddLine(root.transform, new Vector2(0.42f, 0.72f), new Vector2(0.58f, 0.62f), 0.08f, new Color(1f, 0.72f, 0.08f), 151);
            bomb.timer = StageEscortController.CreateText(root.transform, "Fuse", new Vector3(0f, 0f, -0.04f), 30, 0.065f, Color.white, 152);
        }

        private void Update()
        {
            verticalSpeed -= 4.5f * Time.deltaTime;
            transform.position += Vector3.up * (verticalSpeed * Time.deltaTime);
            float remaining = Mathf.Max(0f, explodeAt - Time.time);
            if (timer != null) timer.text = remaining.ToString("0.0");
            if (remaining > 0f) return;
            if (authoritative) owner?.ResolvePlayerBomb(ownerRoom, transform.position);
            GameObject explosion = new GameObject("Player Bomb Explosion");
            explosion.transform.position = transform.position;
            explosion.AddComponent<BombExplosionVisual>().Configure(2.25f, false);
            GameSfx.PlayAt(SfxId.BombExplosion, transform.position, 0.9f);
            Destroy(gameObject);
        }
    }
}
