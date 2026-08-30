using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageRicochetEnemyChallengeController : MonoBehaviour
    {
        private const string StageId = "13-2";
        private const string StateKind = "ricochet_enemy_state";
        private const float OuterHalfWidth = 15.5f;
        private const float OuterHalfHeight = 9f;
        private const float InnerHalfWidth = 12.35f;
        private const float InnerHalfHeight = 5.7f;
        private const float BallSpeed = 7.2f;
        private const float ServeCountdownSeconds = 3f;

        private enum Phase { Intro, Serve, Playing, Intermission, Failed, Clear }

        [System.Serializable]
        internal sealed class EnemyState
        {
            public string Id;
            public int Type;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Hp;
            public int MaxHp;
            public bool Boss;
            public int Index;
        }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int PhaseValue;
            public int Round;
            public int BallsLost;
            public float PhaseRemaining;
            public Vector2 BallPosition;
            public Vector2 BallVelocity;
            public Vector2 BallDirection;
            public bool BallActive;
            public int BallGeneration;
            public EnemyState[] Enemies;
        }

        private readonly Dictionary<string, StageRicochetEnemyTarget> enemies =
            new Dictionary<string, StageRicochetEnemyTarget>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private StageRicochetBall ball;
        private TextMesh roundText;
        private TextMesh ballText;
        private TextMesh statusText;
        private Phase phase = Phase.Intro;
        private int round;
        private int ballsLost;
        private int ballGeneration;
        private int sequence;
        private int lastSequence;
        private float phaseRemaining = 3f;
        private float nextBallAt;
        private float nextStateAt;
        private Vector2 preparedBallDirection = Vector2.up;
        private bool oldCameraEnabled;
        private bool cameraLocked;
        private Vector3 oldCameraPosition;
        private float oldCameraSize;

        internal bool HasAuthority => stageManager == null
            || !stageManager.IsOnlineStageActive
            || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (!cameraLocked) return;
            if (cameraFollow != null) cameraFollow.enabled = oldCameraEnabled;
            if (gameCamera != null)
            {
                gameCamera.transform.position = oldCameraPosition;
                gameCamera.orthographicSize = oldCameraSize;
            }
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildArena();
            LockCamera();
            if (HasAuthority) BeginRound(1);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (!HasAuthority)
            {
                if (phase == Phase.Serve)
                {
                    phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                    ball?.UpdateLaunchCountdown(Mathf.CeilToInt(phaseRemaining));
                }
                RefreshDisplay();
                return;
            }

            if (phase == Phase.Intro)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phaseRemaining <= 0f) BeginRound(1);
            }
            else if (phase == Phase.Serve)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                ball?.UpdateLaunchCountdown(Mathf.CeilToInt(phaseRemaining));
                if (phaseRemaining <= 0f)
                {
                    phase = Phase.Playing;
                    ball?.LaunchPrepared();
                    BroadcastState(true);
                }
            }
            else if (phase == Phase.Intermission)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phaseRemaining <= 0f) BeginRound(round + 1);
            }
            else if (phase == Phase.Failed)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phaseRemaining <= 0f) stageManager.Retry();
            }
            else if (phase == Phase.Clear)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phaseRemaining <= 0f) stageManager.ClearStage();
            }
            else if (phase == Phase.Playing)
            {
                if (ball != null && (Mathf.Abs(ball.transform.position.x) > OuterHalfWidth + 3f
                    || Mathf.Abs(ball.transform.position.y) > OuterHalfHeight + 3f))
                {
                    LoseBall();
                }
                else if (ball == null && ballsLost < 3 && Time.time >= nextBallAt)
                {
                    BeginServe();
                }
                if (enemies.Count == 0) CompleteRound();
            }

            BroadcastState();
            RefreshDisplay();
        }

        private void BeginRound(int nextRound)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            ClearEnemies();
            SpawnRoundEnemies(round);
            BeginServe();
            GameSfx.Play(SfxId.EmotePop);
            BroadcastState(true);
        }

        private void SpawnRoundEnemies(int targetRound)
        {
            int count = targetRound == 1 ? 1 : 3;
            for (int i = 0; i < count; i++)
            {
                bool boss = targetRound == 3;
                StageObjectType type = boss
                    ? i == 0 ? StageObjectType.EnemyFlyerZigzag
                        : i == 1 ? StageObjectType.EnemyFlyerOrbit
                        : StageObjectType.EnemyFlyer
                    : StageObjectType.EnemyFlyer;
                Vector2 position = count == 1
                    ? Vector2.zero
                    : new Vector2(Mathf.Lerp(-6.5f, 6.5f, i / 2f), i == 1 ? 2.4f : -2.2f);
                int hp = boss ? 4 : 2;
                float size = boss ? 0.72f : 1.2f;
                CreateEnemy($"13-2_r{targetRound}_enemy_{i}", type, position, hp, boss, i, true);
            }
        }

        private StageRicochetEnemyTarget CreateEnemy(string id, StageObjectType type, Vector2 position,
            int hp, bool boss, int index, bool authoritative)
        {
            if (factory == null) return null;
            float size = boss ? 0.72f : 1.2f;
            GameObject obj = factory.CreateSpawnedEnemy(type, id, position, size, transform, boss ? 7f : 3.2f, -1f);
            if (obj == null) return null;
            StageEnemyCharacter enemy = obj.GetComponent<StageEnemyCharacter>();
            if (enemy != null) enemy.enabled = false;
            StageRicochetEnemyTarget target = obj.AddComponent<StageRicochetEnemyTarget>();
            target.Configure(this, id, hp, boss, index, authoritative);
            enemies[id] = target;
            return target;
        }

        internal void HitEnemy(StageRicochetEnemyTarget target, Vector2 impact)
        {
            if (!HasAuthority || phase != Phase.Playing || target == null) return;
            target.ApplyBallHit(impact);
            BroadcastState(true);
        }

        internal void NotifyEnemyDefeated(StageRicochetEnemyTarget target)
        {
            if (target == null) return;
            enemies.Remove(target.Id);
        }

        internal void NotifyPlayerReflection(Vector2 point)
        {
            GameSfx.PlayAt(SfxId.Ricochet, point, 0.92f);
            StageRicochetImpactPulse.Create(transform, point);
        }

        private void CompleteRound()
        {
            DestroyBall();
            if (round >= 3)
            {
                phase = Phase.Clear;
                phaseRemaining = 2.4f;
                GameSfx.Play(SfxId.EmotePop);
            }
            else
            {
                phase = Phase.Intermission;
                phaseRemaining = 2f;
            }
            BroadcastState(true);
        }

        private void LoseBall()
        {
            DestroyBall();
            ballsLost++;
            GameSfx.Play(SfxId.PlayerHit);
            if (ballsLost >= 3)
            {
                phase = Phase.Failed;
                phaseRemaining = 3f;
            }
            else nextBallAt = Time.time + 0.9f;
            BroadcastState(true);
        }

        private void BeginServe()
        {
            if (!HasAuthority || ball != null) return;
            int corner = Random.Range(0, 4);
            bool top = corner >= 2;
            bool right = (corner & 1) == 1;
            Vector2 position = new Vector2((right ? 1f : -1f) * (InnerHalfWidth - 0.75f),
                (top ? 1f : -1f) * (InnerHalfHeight - 0.75f));
            preparedBallDirection = top ? Vector2.down : Vector2.up;
            phase = Phase.Serve;
            phaseRemaining = ServeCountdownSeconds;
            ball = StageRicochetBall.Create(transform, this, position, true);
            ballGeneration++;
            ball.PrepareLaunch(preparedBallDirection, BallSpeed, Mathf.CeilToInt(phaseRemaining));
            BroadcastState(true);
        }

        private void DestroyBall()
        {
            if (ball != null) Destroy(ball.gameObject);
            ball = null;
        }

        private void ClearEnemies()
        {
            foreach (StageRicochetEnemyTarget target in enemies.Values)
                if (target != null) Destroy(target.gameObject);
            enemies.Clear();
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("13-2 Enemy Ricochet Arena");
            arena.transform.SetParent(transform, false);
            CreateSolid(arena.transform, "outer_left", StageObjectType.Wall,
                new Vector2(-OuterHalfWidth, 0f), new Vector2(0.7f, OuterHalfHeight * 2f + 0.7f), true);
            CreateSolid(arena.transform, "outer_right", StageObjectType.Wall,
                new Vector2(OuterHalfWidth, 0f), new Vector2(0.7f, OuterHalfHeight * 2f + 0.7f), true);
            RegisterExistingPassSurface("13-2_outer_bottom");
            CreateSolid(arena.transform, "outer_top", StageObjectType.Platform,
                new Vector2(0f, OuterHalfHeight), new Vector2(OuterHalfWidth * 2f + 0.7f, 0.7f), true);
            CreateSolid(arena.transform, "inner_left", StageObjectType.Wall,
                new Vector2(-InnerHalfWidth, 0f), new Vector2(0.62f, InnerHalfHeight * 2f), false);
            CreateSolid(arena.transform, "inner_right", StageObjectType.Wall,
                new Vector2(InnerHalfWidth, 0f), new Vector2(0.62f, InnerHalfHeight * 2f), false);
            CreateSolid(arena.transform, "inner_bottom", StageObjectType.OneWayPlatform,
                new Vector2(0f, -InnerHalfHeight), new Vector2(InnerHalfWidth * 2f, 0.45f), true);
            CreateSolid(arena.transform, "inner_top", StageObjectType.OneWayPlatform,
                new Vector2(0f, InnerHalfHeight), new Vector2(InnerHalfWidth * 2f, 0.45f), true);
            CreateJumpPad(arena.transform, "left_high_jump", new Vector2(-14.15f, -8.25f));
            CreateJumpPad(arena.transform, "right_high_jump", new Vector2(14.15f, -8.25f));
            CreateSolid(arena.transform, "left_upper_landing", StageObjectType.OneWayPlatform,
                new Vector2(-13.8f, 5.7f), new Vector2(3.1f, 0.45f), true);
            CreateSolid(arena.transform, "right_upper_landing", StageObjectType.OneWayPlatform,
                new Vector2(13.8f, 5.7f), new Vector2(3.1f, 0.45f), true);
            BuildMonitor(arena.transform);
        }

        private void CreateJumpPad(Transform parent, string id, Vector2 position)
        {
            if (factory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.JumpPad, position);
            data.objectId = "13-2_" + id;
            data.size = new Vector2(1.55f, 0.68f);
            data.actionStrength = 82f;
            GameObject created = factory.Create(data, parent);
            if (created != null) StageRicochetBallPassSurface.Mark(created);
        }

        private void CreateSolid(Transform parent, string id, StageObjectType type,
            Vector2 position, Vector2 size, bool passThrough)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = "13-2_" + id;
            data.size = size;
            data.keepSeparate = true;
            GameObject obj = factory?.Create(data, parent);
            if (passThrough && obj != null) StageRicochetBallPassSurface.Mark(obj);
        }

        private void RegisterExistingPassSurface(string id)
        {
            StageEditorObject[] markers = Object.FindObjectsByType<StageEditorObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].objectId != id) continue;
                StageRicochetBallPassSurface.Mark(markers[i].gameObject);
                return;
            }
        }

        private void BuildMonitor(Transform parent)
        {
            GameObject board = new GameObject("13-2 Status Board");
            board.transform.SetParent(parent, false);
            board.transform.position = new Vector3(0f, 10.15f, 0.2f);
            DoodleMonitorVisuals.KeepBehindPlayers(board.transform);
            StageEscortController.AddFilledRect(board.transform, "Frame", Vector2.zero,
                new Vector2(19f, 1.9f), new Color(0.04f, 0.07f, 0.08f, 0.92f), 24);
            roundText = StageEscortController.CreateText(board.transform, "Round",
                new Vector3(-5.5f, 0.25f, -0.03f), 48, 0.09f, new Color(0.4f, 0.9f, 1f), 27);
            ballText = StageEscortController.CreateText(board.transform, "Balls",
                new Vector3(5.5f, 0.25f, -0.03f), 48, 0.09f, new Color(1f, 0.78f, 0.18f), 27);
            statusText = StageEscortController.CreateText(board.transform, "Status",
                new Vector3(0f, -0.55f, -0.03f), 48, 0.09f, new Color(0.2f, 1f, 0.7f), 27);
        }

        private void RefreshDisplay()
        {
            if (roundText == null) return;
            roundText.text = LocalizationManager.Format("ricochet_enemy_round", Mathf.Max(1, round), 3);
            ballText.text = LocalizationManager.Format("ricochet_enemy_balls", Mathf.Max(0, 3 - ballsLost), 3);
            if (phase == Phase.Intro || phase == Phase.Serve) statusText.text = Mathf.Max(1, Mathf.CeilToInt(phaseRemaining)).ToString();
            else if (phase == Phase.Intermission) statusText.text = LocalizationManager.Format("ricochet_enemy_next", Mathf.CeilToInt(phaseRemaining));
            else if (phase == Phase.Failed) statusText.text = LocalizationManager.T("ricochet_enemy_failed");
            else if (phase == Phase.Clear) statusText.text = LocalizationManager.T("ricochet_enemy_clear");
            else statusText.text = LocalizationManager.Format("ricochet_enemy_remaining", enemies.Count);
        }

        private void LockCamera()
        {
            if (gameCamera == null) return;
            oldCameraPosition = gameCamera.transform.position;
            oldCameraSize = gameCamera.orthographicSize;
            oldCameraEnabled = cameraFollow != null && cameraFollow.enabled;
            if (cameraFollow != null) cameraFollow.enabled = false;
            float widthSize = (OuterHalfWidth + 1.2f) / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 0.75f, oldCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(11.5f, widthSize);
            cameraLocked = true;
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline() || !HasAuthority || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.1f;
            List<EnemyState> states = new List<EnemyState>();
            foreach (StageRicochetEnemyTarget enemy in enemies.Values)
            {
                if (enemy == null || enemy.Hp <= 0) continue;
                states.Add(enemy.CreateState());
            }
            Rigidbody2D body = ball != null ? ball.Body : null;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new NetworkState
                {
                    Sequence = ++sequence,
                    PhaseValue = (int)phase,
                    Round = round,
                    BallsLost = ballsLost,
                    PhaseRemaining = phaseRemaining,
                    BallPosition = ball != null ? (Vector2)ball.transform.position : Vector2.zero,
                    BallVelocity = body != null ? body.linearVelocity : Vector2.zero,
                    BallDirection = preparedBallDirection,
                    BallActive = ball != null,
                    BallGeneration = ballGeneration,
                    Enemies = states.ToArray()
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastSequence) return;
            lastSequence = state.Sequence;
            phase = (Phase)Mathf.Clamp(state.PhaseValue, 0, (int)Phase.Clear);
            round = state.Round;
            ballsLost = state.BallsLost;
            phaseRemaining = state.PhaseRemaining;
            preparedBallDirection = state.BallDirection.sqrMagnitude > 0.01f
                ? state.BallDirection.normalized
                : Vector2.up;
            if (state.BallActive)
            {
                if (ball == null || ballGeneration != state.BallGeneration)
                {
                    DestroyBall();
                    ball = StageRicochetBall.Create(transform, this, state.BallPosition, false);
                    ballGeneration = state.BallGeneration;
                }
                if (phase == Phase.Serve)
                    ball.PrepareLaunch(preparedBallDirection, BallSpeed, Mathf.CeilToInt(phaseRemaining));
                else ball.HideLaunchPreview();
                ball.SetReplicaTarget(state.BallPosition, state.BallVelocity);
            }
            else DestroyBall();
            ApplyEnemyStates(state.Enemies);
            RefreshDisplay();
        }

        private void ApplyEnemyStates(EnemyState[] states)
        {
            HashSet<string> seen = new HashSet<string>();
            if (states != null)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    EnemyState state = states[i];
                    if (state == null || string.IsNullOrEmpty(state.Id)) continue;
                    seen.Add(state.Id);
                    if (!enemies.TryGetValue(state.Id, out StageRicochetEnemyTarget target) || target == null)
                        target = CreateEnemy(state.Id, (StageObjectType)state.Type, state.Position,
                            state.MaxHp, state.Boss, state.Index, false);
                    target?.ApplyReplica(state.Position, state.Velocity, state.Hp);
                }
            }
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, StageRicochetEnemyTarget> pair in enemies)
                if (!seen.Contains(pair.Key)) { if (pair.Value != null) Destroy(pair.Value.gameObject); remove.Add(pair.Key); }
            for (int i = 0; i < remove.Count; i++) enemies.Remove(remove[i]);
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        internal EnemyState BuildEnemyState(StageRicochetEnemyTarget target)
        {
            return target != null ? target.CreateState() : null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageRicochetEnemyTarget : MonoBehaviour
    {
        private StageRicochetEnemyChallengeController owner;
        private StageEnemyCharacter enemy;
        private Rigidbody2D body;
        private Collider2D hitbox;
        private TextMesh hpText;
        private Vector2 velocity;
        private Vector2 replicaTarget;
        private Vector2 replicaVelocity;
        private bool hasReplicaTarget;
        private string id;
        private int hp;
        private int maxHp;
        private int index;
        private bool boss;
        private bool authoritative;
        private float nextTurnAt;
        private float nextHitAt;

        public string Id => id;
        public int Hp => hp;

        internal void Configure(StageRicochetEnemyChallengeController controller, string targetId,
            int health, bool isBoss, int targetIndex, bool hasAuthority)
        {
            owner = controller;
            id = targetId;
            hp = maxHp = Mathf.Max(1, health);
            boss = isBoss;
            index = targetIndex;
            authoritative = hasAuthority;
            enemy = GetComponent<StageEnemyCharacter>();
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
            }
            if (hitbox != null) hitbox.isTrigger = false;
            float angle = (35f + targetIndex * 117f) * Mathf.Deg2Rad;
            velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized * (boss ? 7f : 3.2f);
            nextTurnAt = Time.time + 0.35f + targetIndex * 0.12f;
            BuildBadge();
        }

        private void FixedUpdate()
        {
            if (!authoritative || body == null || hp <= 0) return;
            if (boss && Time.time >= nextTurnAt)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(6.2f, 8.2f);
                nextTurnAt = Time.time + Random.Range(0.32f, 0.72f);
            }
            Vector2 next = body.position + velocity * Time.fixedDeltaTime;
            if (next.x < -11.1f || next.x > 11.1f) { velocity.x = -velocity.x; next.x = Mathf.Clamp(next.x, -11.1f, 11.1f); }
            if (next.y < -4.5f || next.y > 4.5f) { velocity.y = -velocity.y; next.y = Mathf.Clamp(next.y, -4.5f, 4.5f); }
            if (!boss) velocity = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.7f + index) * 0.5f) * velocity;
            body.MovePosition(next);
        }

        private void Update()
        {
            if (authoritative || !hasReplicaTarget || hp <= 0) return;
            Vector2 predicted = replicaTarget + replicaVelocity * 0.07f;
            float blend = 1f - Mathf.Exp(-16f * Time.deltaTime);
            if (body != null) body.position = Vector2.Lerp(body.position, predicted, blend);
            else transform.position = Vector2.Lerp(transform.position, predicted, blend);
        }

        internal void ApplyBallHit(Vector2 point)
        {
            if (!authoritative || hp <= 0 || Time.time < nextHitAt) return;
            nextHitAt = Time.time + 0.14f;
            hp--;
            RefreshBadge();
            StageRicochetImpactPulse.Create(transform.parent, point);
            GameSfx.PlayAt(SfxId.Ricochet, point, 1f);
            if (hp > 0) return;
            owner?.NotifyEnemyDefeated(this);
            if (enemy != null) enemy.ApplyDefeated();
            else gameObject.SetActive(false);
        }

        internal void ApplyReplica(Vector2 position, Vector2 targetVelocity, int health)
        {
            velocity = targetVelocity;
            replicaVelocity = targetVelocity;
            replicaTarget = position;
            hasReplicaTarget = true;
            hp = Mathf.Clamp(health, 0, maxHp);
            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            if ((current - position).sqrMagnitude > 16f)
            {
                if (body != null) body.position = position;
                else transform.position = position;
            }
            RefreshBadge();
        }

        internal StageRicochetEnemyChallengeController.EnemyState CreateState()
        {
            return new StageRicochetEnemyChallengeController.EnemyState
            {
                Id = id,
                Type = (int)(GetComponent<StageEditorObject>()?.type ?? StageObjectType.EnemyFlyer),
                Position = transform.position,
                Velocity = velocity,
                Hp = hp,
                MaxHp = maxHp,
                Boss = boss,
                Index = index
            };
        }

        private void BuildBadge()
        {
            GameObject badge = new GameObject(boss ? "Boss HP" : "Enemy HP");
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = new Vector3(0f, boss ? 0.85f : 1.05f, -0.08f);
            hpText = badge.AddComponent<TextMesh>();
            hpText.anchor = TextAnchor.MiddleCenter;
            hpText.alignment = TextAlignment.Center;
            hpText.fontSize = 40;
            hpText.characterSize = 0.065f;
            hpText.color = boss ? new Color(1f, 0.22f, 0.62f) : new Color(0.7f, 0.08f, 0.12f);
            badge.GetComponent<MeshRenderer>().sortingOrder = 48;
            if (boss)
            {
                StageGun.AddLine(transform, "Boss Crown", new[]
                {
                    new Vector2(-0.4f, 0.55f), new Vector2(-0.25f, 0.88f), new Vector2(0f, 0.62f),
                    new Vector2(0.25f, 0.88f), new Vector2(0.4f, 0.55f)
                }, 0.08f, new Color(1f, 0.65f, 0.05f), 47);
            }
            RefreshBadge();
        }

        private void RefreshBadge()
        {
            if (hpText != null) hpText.text = "♥" + Mathf.Max(0, hp);
        }
    }
}
