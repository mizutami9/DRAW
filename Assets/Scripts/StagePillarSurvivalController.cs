using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StagePillarSurvivalController : StageEliminationChallengeController
    {
        private const string StageId = "8-1";
        private const string KindState = "pillar_survival_state";
        private const string KindWave = "pillar_survival_wave";
        private const string KindEnemy = "pillar_survival_enemy";
        private const string KindEliminateRequest = "pillar_survival_eliminate_request";
        private const string KindEliminated = "pillar_survival_eliminated";
        private const float FloorY = -2f;
        private const float ArenaHalfWidth = 21f;
        private const float FloorWidth = ArenaHalfWidth * 2f;
        private const int LaneCount = 10;
        private const float IntroSeconds = 2.8f;
        private const float CountdownSeconds = 4f;

        private enum Phase { Intro, Countdown, Playing, Finished, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Phase;
            public float Remaining;
            public float Elapsed;
            public float PhaseRemaining;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class WaveState
        {
            public int Sequence;
            public int[] Lanes;
            public float WarningSeconds;
            public float FallSpeed;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        [System.Serializable]
        private sealed class EnemyState
        {
            public int Sequence;
            public int Type;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Speed;
        }

        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<int> appliedWaveSequences = new HashSet<int>();
        private readonly HashSet<int> appliedEnemySequences = new HashSet<int>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly List<float> lanePositions = new List<float>();
        private readonly List<StageFallingPillar> pillars = new List<StageFallingPillar>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private CameraFollow2D cameraFollow;
        private TextMesh monitorTitle;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private Phase phase = Phase.Intro;
        private float durationSeconds = 60f;
        private float remainingSeconds = 60f;
        private float elapsedSeconds;
        private float phaseRemaining = IntroSeconds;
        private float nextWaveAt = 1.2f;
        private float nextStateAt;
        private float nextBombAt = 12f;
        private float nextEnemyAt = 17f;
        private float restartRemaining;
        private float previousCameraMinimum = 8f;
        private int waveSequence;
        private int bombSequence;
        private int enemySequence;
        private int stateSequence;
        private int lastStateSequence;
        private bool restoredPlayers;

        public void Configure(float seconds)
        {
            durationSeconds = Mathf.Clamp(seconds > 0f ? seconds : 60f, 30f, 180f);
            remainingSeconds = durationSeconds;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
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
            RestoreHiddenPlayers();
        }

        private void Start()
        {
            BuildArena();
            if (cameraFollow != null)
            {
                previousCameraMinimum = cameraFollow.MinimumOrthographicSize;
                cameraFollow.SetMinimumOrthographicSize(12.3f);
            }
            CaptureParticipants();
            SetLocalControls(false);
            RefreshMonitor();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (IsOnlineActive() && !HasAuthority())
            {
                UpdateReplica();
                return;
            }

            BroadcastState();
            ApplyPendingEliminations();
            CheckFalls();

            if (phase == Phase.Finished) return;
            if (phase == Phase.Failed)
            {
                restartRemaining -= Time.deltaTime;
                RefreshMonitor();
                if (restartRemaining <= 0f) stageManager.Retry();
                return;
            }
            if (phase == Phase.Intro)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    phase = Phase.Countdown;
                    phaseRemaining = CountdownSeconds;
                    GameSfx.Play(SfxId.UiButtonPress);
                }
                RefreshMonitor();
                return;
            }
            if (phase == Phase.Countdown)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    phase = Phase.Playing;
                    phaseRemaining = 0f;
                    SetLocalControls(true);
                    GameSfx.Play(SfxId.DrawConfirm);
                    BroadcastState(true);
                }
                RefreshMonitor();
                return;
            }

            elapsedSeconds += Time.deltaTime;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (elapsedSeconds >= nextWaveAt) SpawnNextWave();
            UpdateAttackDirector();
            if (AreAllPlayersEliminated())
            {
                BeginFailure();
                return;
            }
            if (remainingSeconds <= 0f)
            {
                phase = Phase.Finished;
                SetLocalControls(false);
                RefreshMonitor();
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            RefreshMonitor();
        }

        private void UpdateReplica()
        {
            ApplyPendingEliminations();
            CheckFalls();
            if (phase == Phase.Intro || phase == Phase.Countdown)
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
            else if (phase == Phase.Playing)
            {
                elapsedSeconds += Time.deltaTime;
                remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            }
            RefreshMonitor();
        }

        private void SpawnNextWave()
        {
            float progress = Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            int count = Mathf.Clamp(5 + Mathf.FloorToInt(progress * 4.99f), 5, 9);
            float warning = Mathf.Lerp(2.25f, 0.85f, progress);
            float fallSpeed = Mathf.Lerp(18f, 29f, progress);
            float interval = Mathf.Lerp(5.2f, 3.15f, progress);
            nextWaveAt = elapsedSeconds + interval;

            List<int> candidates = new List<int>();
            for (int i = 0; i < LaneCount; i++) candidates.Add(i);
            for (int i = 0; i < candidates.Count; i++)
            {
                int swap = Random.Range(i, candidates.Count);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }
            int[] lanes = candidates.GetRange(0, count).ToArray();
            WaveState wave = new WaveState
            {
                Sequence = ++waveSequence,
                Lanes = lanes,
                WarningSeconds = warning,
                FallSpeed = fallSpeed
            };
            ApplyWave(wave);
            if (IsOnlineActive() && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindWave,
                    Json = JsonUtility.ToJson(wave)
                });
            }
        }

        private void ApplyWave(WaveState wave)
        {
            if (wave == null || wave.Lanes == null || !appliedWaveSequences.Add(wave.Sequence)) return;
            for (int i = 0; i < wave.Lanes.Length; i++)
            {
                int lane = Mathf.Clamp(wave.Lanes[i], 0, lanePositions.Count - 1);
                if (lane >= 0 && lane < pillars.Count)
                    pillars[lane]?.Activate(wave.WarningSeconds, wave.FallSpeed, wave.Sequence * 16 + i);
            }
            GameSfx.Play(SfxId.CrumblingFloorWarning);
        }

        private void UpdateAttackDirector()
        {
            if (!HasAuthority()) return;
            float progress = Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            if (elapsedSeconds >= nextBombAt)
            {
                int count = 1 + Mathf.FloorToInt(progress * 2.4f);
                for (int i = 0; i < count; i++) SpawnBomb(progress);
                nextBombAt = elapsedSeconds + Mathf.Lerp(7.2f, 2.8f, progress);
            }
            if (elapsedSeconds >= nextEnemyAt)
            {
                int count = progress > 0.72f ? 2 : 1;
                for (int i = 0; i < count; i++) SpawnEnemy(progress);
                nextEnemyAt = elapsedSeconds + Mathf.Lerp(10f, 4.8f, progress);
            }
        }

        private void SpawnBomb(float progress)
        {
            bool left = (bombSequence++ & 1) == 0;
            Vector2 position = new Vector2(left ? -ArenaHalfWidth + 0.8f : ArenaHalfWidth - 0.8f, Random.Range(1.4f, 5.2f));
            Vector2 velocity = new Vector2(left ? Random.Range(7.5f, 11f) : Random.Range(-11f, -7.5f), Random.Range(2.2f, 5.2f));
            float size = Random.Range(0.72f, 1.15f);
            float fuse = Random.Range(3.8f, 6.2f);
            string id = "pillar_survival_bomb_" + bombSequence;
            GameObject bomb = IsOnlineActive() && syncManager != null
                ? syncManager.SpawnDropperBox(id, StageObjectType.Bomb, position, size, 0f, fuse)
                : objectFactory != null
                    ? objectFactory.CreateDroppedBox(StageObjectType.Bomb, id, position, size, transform, fuse)
                    : null;
            Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
            if (body != null)
            {
                body.linearVelocity = velocity;
                body.AddTorque(Random.Range(-2.4f, 2.4f), ForceMode2D.Impulse);
            }
        }

        private void SpawnEnemy(float progress)
        {
            bool left = (enemySequence & 1) == 0;
            StageObjectType type = progress > 0.68f && Random.value < 0.4f
                ? StageObjectType.EnemyJumper
                : progress > 0.4f && Random.value < 0.35f
                    ? StageObjectType.EnemyCharger
                    : StageObjectType.EnemyWalker;
            EnemyState state = new EnemyState
            {
                Sequence = ++enemySequence,
                Type = (int)type,
                Position = new Vector2(left ? -ArenaHalfWidth + 0.9f : ArenaHalfWidth - 0.9f, Random.Range(5.2f, 7.2f)),
                Velocity = new Vector2(left ? Random.Range(3.2f, 5.2f) : Random.Range(-5.2f, -3.2f), Random.Range(-2.8f, -1.2f)),
                Size = Random.Range(0.78f, 1.15f),
                Speed = Mathf.Lerp(1.8f, 3.8f, progress)
            };
            ApplyEnemy(state);
            if (IsOnlineActive() && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindEnemy,
                    Json = JsonUtility.ToJson(state)
                });
            }
        }

        private void ApplyEnemy(EnemyState state)
        {
            if (state == null || !appliedEnemySequences.Add(state.Sequence) || objectFactory == null) return;
            GameObject enemy = objectFactory.CreateSpawnedEnemy(
                (StageObjectType)state.Type,
                "pillar_survival_enemy_" + state.Sequence,
                state.Position,
                state.Size,
                transform,
                state.Speed,
                state.Velocity.x);
            Rigidbody2D body = enemy != null ? enemy.GetComponent<Rigidbody2D>() : null;
            if (body != null) body.linearVelocity = state.Velocity;
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != Phase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (!IsOnlineActive()) participantIds.Add(id);
            if (IsOnlineActive())
            {
                string localId = onlineManager != null ? onlineManager.LocalPlayerId : null;
                if (id != localId) return;
                if (!HasAuthority())
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = KindEliminateRequest,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                    });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id, IsOnlineActive());
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindEliminated,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
            }
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminatedIds.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || string.IsNullOrEmpty(data.Kind)) return;
            if (data.Kind == KindState && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyState(JsonUtility.FromJson<NetworkState>(data.Json));
            else if (data.Kind == KindWave && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyWave(JsonUtility.FromJson<WaveState>(data.Json));
            else if (data.Kind == KindEnemy && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyEnemy(JsonUtility.FromJson<EnemyState>(data.Json));
            else if (data.Kind == KindEliminateRequest && HasAuthority())
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(data.Json);
                string id = request != null && !string.IsNullOrEmpty(request.PlayerId) ? request.PlayerId : data.PlayerId;
                if (id == data.PlayerId) ConfirmElimination(id, true);
            }
            else if (data.Kind == KindEliminated && IsHostPlayer(data.PlayerId))
            {
                EliminationState eliminated = JsonUtility.FromJson<EliminationState>(data.Json);
                if (eliminated != null) ApplyElimination(eliminated.PlayerId);
            }
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnlineActive() || !HasAuthority() || onlineManager == null
                || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence,
                Phase = (int)phase,
                Remaining = remainingSeconds,
                Elapsed = elapsedSeconds,
                PhaseRemaining = phaseRemaining,
                EliminatedIds = new List<string>(eliminatedIds).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = KindState,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            phase = (Phase)Mathf.Clamp(state.Phase, 0, (int)Phase.Failed);
            remainingSeconds = state.Remaining;
            elapsedSeconds = state.Elapsed;
            phaseRemaining = state.PhaseRemaining;
            if (state.EliminatedIds != null)
                for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            SetLocalControls(phase == Phase.Playing);
            RefreshMonitor();
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("8-1 Pillar Survival Arena");
            arena.transform.SetParent(transform, false);
            CreateFloor(arena.transform);
            CreateBoundary(arena.transform);
            CreateMonitor(arena.transform);
            float step = FloorWidth / LaneCount;
            for (int i = 0; i < LaneCount; i++)
            {
                float x = -ArenaHalfWidth + step * (i + 0.5f);
                lanePositions.Add(x);
                CreateLaneMarker(arena.transform, x, i + 1);
                pillars.Add(StageFallingPillar.CreateWaiting(
                    arena.transform,
                    this,
                    x,
                    i + 1,
                    step + 0.04f));
            }
        }

        private void CreateFloor(Transform parent)
        {
            GameObject floor = new GameObject("Pillar Survival Floor");
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector2(0f, FloorY);
            floor.layer = 6;
            floor.tag = "Ground";
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(FloorWidth, 0.75f);
            CreateRect(floor.transform, new Vector2(FloorWidth, 0.75f), new Color(0.9f, 0.86f, 0.72f, 1f), 12);
            AddBoxOutline(floor.transform, new Vector2(FloorWidth, 0.75f), new Color(0.16f, 0.11f, 0.07f, 1f), 13);
        }

        private void CreateBoundary(Transform parent)
        {
            if (objectFactory == null) return;
            StageObjectData boundary = StageObjectFactory.CreateDefaultData(
                StageObjectType.StageBoundary,
                new Vector2(0f, 3.7f));
            boundary.objectId = "pillar_survival_boundary";
            boundary.size = new Vector2(FloorWidth + 0.8f, 13f);
            boundary.pathThickness = 0.65f;
            objectFactory.Create(boundary, parent);
        }

        private void CreateLaneMarker(Transform parent, float x, int number)
        {
            GameObject marker = new GameObject("Pillar Lane " + number);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(x, FloorY + 0.45f, 0f);
            CreateRect(marker.transform, new Vector2(0.06f, 0.24f), new Color(0.25f, 0.55f, 0.75f, 0.22f), 14);
        }

        private void CreateMonitor(Transform parent)
        {
            GameObject monitor = new GameObject("Pillar Survival Monitor");
            monitor.transform.SetParent(parent, false);
            monitor.transform.localPosition = new Vector3(0f, 3.8f, 0.5f);
            CreateRect(monitor.transform, new Vector2(11.2f, 2.8f), new Color(0.18f, 0.22f, 0.27f, 0.82f), -32);
            CreateRect(monitor.transform, new Vector2(10.6f, 2.25f), new Color(0.01f, 0.03f, 0.045f, 0.84f), -31);
            monitorTitle = CreateText(monitor.transform, "Title", new Vector3(0f, 0.78f, -0.02f), 46, 0.12f, new Color(0.55f, 0.9f, 1f, 1f), -28);
            monitorMain = CreateText(monitor.transform, "Main", new Vector3(0f, 0f, -0.03f), 68, 0.17f, new Color(0.2f, 1f, 0.72f, 1f), -27);
            monitorSub = CreateText(monitor.transform, "Sub", new Vector3(0f, -0.78f, -0.04f), 42, 0.1f, new Color(1f, 0.82f, 0.24f, 1f), -26);
        }

        private void RefreshMonitor()
        {
            if (monitorTitle == null) return;
            if (phase == Phase.Intro)
            {
                monitorTitle.text = LocalizationManager.T("pillar_survival_title");
                SetMonitorMain(LocalizationManager.T("survival_goal"), 0.105f);
                SetMonitorSub(LocalizationManager.T("pillar_survival_goal_sub"), 0.09f);
                return;
            }
            if (phase == Phase.Countdown)
            {
                monitorTitle.text = LocalizationManager.T("survival_get_ready");
                if (phaseRemaining > 3f) SetMonitorMain("3", 0.2f);
                else if (phaseRemaining > 2f) SetMonitorMain("2", 0.2f);
                else if (phaseRemaining > 1f) SetMonitorMain("1", 0.2f);
                else SetMonitorMain(LocalizationManager.T("survival_start"), 0.14f);
                SetMonitorSub(LocalizationManager.T("pillar_survival_watch_up"), 0.09f);
                return;
            }
            if (phase == Phase.Failed)
            {
                monitorTitle.text = LocalizationManager.T("survival_all_dead");
                SetMonitorMain("GAME OVER", 0.14f);
                SetMonitorSub(LocalizationManager.T("survival_retrying"), 0.09f);
                return;
            }
            if (phase == Phase.Finished)
            {
                monitorTitle.text = LocalizationManager.T("pillar_survival_clear");
                SetMonitorMain("CLEAR!", 0.17f);
                SetMonitorSub(LocalizationManager.T("survival_clear_sub"), 0.1f);
                return;
            }
            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            float seconds = remainingSeconds - minutes * 60f;
            monitorTitle.text = LocalizationManager.T("pillar_survival_title");
            SetMonitorMain(string.Format("{0:00}:{1:00.0}", minutes, seconds), 0.17f);
            SetMonitorSub(LocalizationManager.T("pillar_survival_watch_up"), 0.1f);
        }

        private void SetMonitorMain(string value, float size)
        {
            monitorMain.text = value;
            monitorMain.characterSize = Mathf.Min(size, 1.65f / Mathf.Max(1, value != null ? value.Length : 0));
        }

        private void SetMonitorSub(string value, float size)
        {
            monitorSub.text = value;
            monitorSub.characterSize = Mathf.Min(size, 1.9f / Mathf.Max(1, value != null ? value.Length : 0));
        }

        private void CheckFalls()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].transform.position.y < FloorY - 4f) RequestElimination(players[i]);
        }

        private void CaptureParticipants()
        {
            if (IsOnlineActive())
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players != null)
                    for (int i = 0; i < players.Length; i++)
                        if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) participantIds.Add(players[i].PlayerId);
                return;
            }
            PlayerController2D[] locals = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < locals.Length; i++) participantIds.Add(ResolvePlayerId(locals[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds) if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private void BeginFailure()
        {
            phase = Phase.Failed;
            restartRemaining = 2.8f;
            SetLocalControls(false);
            RefreshMonitor();
            BroadcastState(true);
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

        private void RestoreHiddenPlayers()
        {
            if (restoredPlayers) return;
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++)
                if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private void ApplyPendingEliminations()
        {
            foreach (string id in eliminatedIds)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player != null && player.gameObject.activeSelf) HidePlayer(player);
            }
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode && !IsEliminated(active));
            if (!IsOnlineActive())
                stageManager.RemotePlayerController?.SetControlsEnabled(enabled && !IsEliminated(stageManager.RemotePlayerController));
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminatedIds.Contains(id);
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnlineActive() ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnlineActive()) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsOnlineActive() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnlineActive() || stageManager.IsOnlineStageHost;

        private bool IsHostPlayer(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        internal static void CreateRect(Transform parent, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject("Paper Rect");
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = StageSurvivalController.GetSquareSpriteForChallenges();
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        internal static TextMesh CreateText(Transform parent, string name, Vector3 position, int fontSize, float size, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            Font font = StageSurvivalController.FindHandwrittenFont();
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }

        internal static void AddBoxOutline(Transform parent, Vector2 size, Color color, int order)
        {
            Vector2 half = size * 0.5f;
            AddPolyline(parent, new[]
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y),
                new Vector2(-half.x, -half.y)
            }, 0.075f, color, order);
        }

        internal static void AddPolyline(Transform parent, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject("Crayon Line");
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }
    }

    public sealed class StageFallingPillar : MonoBehaviour
    {
        private enum PillarPhase { Waiting, Warning, Falling, Impact, Retracting }
        private const float FloorTop = -1.625f;
        private const float PillarHeight = 8.4f;
        private const float StartCenterY = 10.8f;

        private StagePillarSurvivalController owner;
        private GameObject pillarVisual;
        private PillarPhase phase;
        private float remaining;
        private float fallSpeed;
        private float impactRemaining;
        private float pulseTime;
        private Vector3 impactPosition;
        private float pillarWidth = 4.2f;
        private readonly List<SpriteRenderer> pillarRenderers = new List<SpriteRenderer>();
        private readonly List<Color> pillarBaseColors = new List<Color>();

        public static StageFallingPillar CreateWaiting(
            Transform parent,
            StagePillarSurvivalController owner,
            float x,
            int lane,
            float width)
        {
            GameObject root = new GameObject("Waiting Pillar " + lane);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, 0f, 0f);
            StageFallingPillar pillar = root.AddComponent<StageFallingPillar>();
            pillar.owner = owner;
            pillar.pillarWidth = Mathf.Max(2f, width);
            pillar.BuildVisual();
            return pillar;
        }

        public void Activate(float warningSeconds, float speed, int sequence)
        {
            if (phase != PillarPhase.Waiting) return;
            remaining = Mathf.Max(0.65f, warningSeconds);
            fallSpeed = Mathf.Max(12f, speed);
            phase = PillarPhase.Warning;
            gameObject.name = "Active Pillar " + sequence;
            ApplyWarningTint(0.75f);
        }

        private void BuildVisual()
        {
            pillarVisual = new GameObject("Heavy Crayon Pillar");
            pillarVisual.transform.SetParent(transform, false);
            pillarVisual.transform.localPosition = new Vector3(0f, StartCenterY, 0f);
            StagePillarSurvivalController.CreateRect(
                pillarVisual.transform,
                new Vector2(pillarWidth, PillarHeight),
                new Color(0.42f, 0.52f, 0.59f, 1f),
                47);
            StagePillarSurvivalController.AddBoxOutline(
                pillarVisual.transform,
                new Vector2(pillarWidth, PillarHeight),
                new Color(0.08f, 0.16f, 0.22f, 1f),
                49);
            GameObject topCap = new GameObject("Top Cap");
            topCap.transform.SetParent(pillarVisual.transform, false);
            topCap.transform.localPosition = new Vector3(0f, PillarHeight * 0.5f - 0.17f, 0f);
            StagePillarSurvivalController.CreateRect(
                topCap.transform,
                new Vector2(pillarWidth + 0.12f, 0.34f),
                new Color(0.19f, 0.35f, 0.47f, 1f),
                50);
            GameObject bottomCap = new GameObject("Bottom Cap");
            bottomCap.transform.SetParent(pillarVisual.transform, false);
            bottomCap.transform.localPosition = new Vector3(0f, -PillarHeight * 0.5f + 0.17f, 0f);
            StagePillarSurvivalController.CreateRect(
                bottomCap.transform,
                new Vector2(pillarWidth + 0.12f, 0.34f),
                new Color(0.19f, 0.35f, 0.47f, 1f),
                50);
            for (int i = -2; i <= 2; i++)
            {
                float y = i * 1.45f;
                StagePillarSurvivalController.AddPolyline(
                    pillarVisual.transform,
                    new[] { new Vector2(-pillarWidth * 0.38f, y - 0.48f), new Vector2(pillarWidth * 0.38f, y + 0.48f) },
                    0.055f,
                    new Color(0.75f, 0.87f, 0.92f, 0.34f),
                    48);
            }
            for (int i = -2; i <= 2; i++)
            {
                GameObject joint = new GameObject("Stone Joint");
                joint.transform.SetParent(pillarVisual.transform, false);
                joint.transform.localPosition = new Vector3(0f, i * 1.6f, 0f);
                StagePillarSurvivalController.CreateRect(
                    joint.transform,
                    new Vector2(pillarWidth - 0.12f, 0.075f),
                    new Color(0.12f, 0.2f, 0.25f, 0.58f),
                    49);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject edge = new GameObject("Beveled Edge");
                edge.transform.SetParent(pillarVisual.transform, false);
                edge.transform.localPosition = new Vector3(side * (pillarWidth * 0.5f - 0.13f), 0f, 0f);
                StagePillarSurvivalController.CreateRect(
                    edge.transform,
                    new Vector2(0.22f, PillarHeight - 0.45f),
                    side < 0
                        ? new Color(0.18f, 0.3f, 0.38f, 0.82f)
                        : new Color(0.68f, 0.77f, 0.8f, 0.65f),
                    49);
            }
            StagePillarSurvivalController.AddPolyline(
                pillarVisual.transform,
                new[] { new Vector2(-0.7f, 2.8f), new Vector2(-0.15f, 2.25f), new Vector2(-0.52f, 1.7f) },
                0.07f,
                new Color(0.12f, 0.18f, 0.22f, 0.72f),
                51);
            StagePillarSurvivalController.AddPolyline(
                pillarVisual.transform,
                new[] { new Vector2(0.85f, -1.5f), new Vector2(0.25f, -2.05f), new Vector2(0.62f, -2.75f) },
                0.07f,
                new Color(0.12f, 0.18f, 0.22f, 0.72f),
                51);

            pillarRenderers.AddRange(pillarVisual.GetComponentsInChildren<SpriteRenderer>(true));
            for (int i = 0; i < pillarRenderers.Count; i++) pillarBaseColors.Add(pillarRenderers[i].color);
            pillarVisual.SetActive(true);
        }

        private void Update()
        {
            pulseTime += Time.deltaTime;
            if (phase == PillarPhase.Waiting) return;
            if (phase == PillarPhase.Warning)
            {
                remaining -= Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime * 11f);
                ApplyWarningTint(Mathf.Lerp(0.48f, 0.9f, pulse));
                if (remaining <= 0f)
                {
                    phase = PillarPhase.Falling;
                }
                return;
            }

            if (phase == PillarPhase.Falling)
            {
                float landedY = FloorTop + PillarHeight * 0.5f;
                Vector3 position = pillarVisual.transform.localPosition;
                position.y = Mathf.MoveTowards(position.y, landedY, fallSpeed * Time.deltaTime);
                pillarVisual.transform.localPosition = position;
                CheckPlayerHits();
                if (Mathf.Approximately(position.y, landedY))
                {
                    phase = PillarPhase.Impact;
                    impactRemaining = 0.72f;
                    impactPosition = position;
                    GameSfx.Play(SfxId.CrumblingFloorCollapse);
                    CheckPlayerHits();
                }
                return;
            }

            if (phase == PillarPhase.Impact)
            {
                impactRemaining -= Time.deltaTime;
                pillarVisual.transform.localPosition = impactPosition
                    + new Vector3(Mathf.Sin(pulseTime * 54f) * 0.055f, 0f, 0f);
                CheckPlayerHits();
                if (impactRemaining <= 0f) phase = PillarPhase.Retracting;
                return;
            }

            Vector3 retract = pillarVisual.transform.localPosition;
            retract.y = Mathf.MoveTowards(retract.y, StartCenterY, fallSpeed * 0.55f * Time.deltaTime);
            pillarVisual.transform.localPosition = retract;
            if (Mathf.Approximately(retract.y, StartCenterY))
            {
                phase = PillarPhase.Waiting;
                RestoreBaseColors();
            }
        }

        private void ApplyWarningTint(float strength)
        {
            Color warning = new Color(1f, 0.26f, 0.06f, 1f);
            for (int i = 0; i < pillarRenderers.Count && i < pillarBaseColors.Count; i++)
            {
                Color source = pillarBaseColors[i];
                Color tint = Color.Lerp(source, warning, strength);
                tint.a = source.a;
                pillarRenderers[i].color = tint;
            }
        }

        private void RestoreBaseColors()
        {
            for (int i = 0; i < pillarRenderers.Count && i < pillarBaseColors.Count; i++)
                pillarRenderers[i].color = pillarBaseColors[i];
        }

        private void CheckPlayerHits()
        {
            if (owner == null || pillarVisual == null) return;
            Vector2 center = pillarVisual.transform.position;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(pillarWidth * 0.96f, PillarHeight * 0.98f), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerController2D player = hits[i] != null
                    ? hits[i].GetComponentInParent<PlayerController2D>()
                    : null;
                if (player != null) owner.RequestElimination(player);
            }
        }
    }
}
