using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSurvivalController : StageEliminationChallengeController
    {
        private const string StageId = "11-2";
        private const string KindRound = "survival_round";
        private const string KindState = "survival_state";
        private const string KindCannon = "survival_cannon";
        private const string KindEliminateRequest = "survival_eliminate_request";
        private const string KindEliminated = "survival_eliminated";
        private const int FloorCount = 14;
        private const float ArenaHalfWidth = 16.8f;
        private const float FloorY = -2f;
        private const float FloorWidth = ArenaHalfWidth * 2f / FloorCount;
        private const float IntroDuration = 2.8f;
        private const float StartCountdownDuration = 4f;

        private enum SurvivalPhase
        {
            Intro,
            StartCountdown,
            Warning,
            Collapsed,
            Finished,
            Failed
        }

        [System.Serializable]
        private sealed class RoundState
        {
            public int Round;
            public int[] SafeFloors;
            public float WarningSeconds;
        }

        [System.Serializable]
        private sealed class SurvivalState
        {
            public int Sequence;
            public int Phase;
            public int Round;
            public int[] SafeFloors;
            public string[] EliminatedIds;
            public float RemainingSeconds;
            public float ElapsedSeconds;
            public float PhaseRemaining;
        }

        [System.Serializable]
        private sealed class CannonState
        {
            public int Sequence;
            public Vector2 Position;
            public Vector2 Direction;
            public float Speed;
        }

        private sealed class CannonPoint
        {
            public Vector2 Position;
            public Vector2 Direction;
        }

        [System.Serializable]
        private sealed class EliminationState
        {
            public string PlayerId;
        }

        private sealed class FloorPiece
        {
            public GameObject Root;
            public BoxCollider2D Collider;
            public SpriteRenderer Fill;
            public Color BaseColor;
        }

        private readonly List<FloorPiece> floors = new List<FloorPiece>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly HashSet<int> safeFloors = new HashSet<int>();
        private readonly List<CannonPoint> cannons = new List<CannonPoint>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private CameraFollow2D cameraFollow;
        private TextMesh monitorMain;
        private SurvivalPhase phase = SurvivalPhase.Intro;
        private float durationSeconds = 60f;
        private float remainingSeconds;
        private float phaseRemaining = IntroDuration;
        private float elapsedSeconds;
        private float nextTopBombAt;
        private float nextSideBombAt;
        private float nextCannonAt;
        private float failedRestartRemaining;
        private int roundNumber;
        private int lastSafeFloor = -1;
        private int bombSequence;
        private int cannonSequence;
        private int stateSequence;
        private int lastReceivedStateSequence;
        private int lastReceivedCannonSequence;
        private float nextStateBroadcastAt;
        private float previousCameraMinimum = 8f;
        private bool configured;
        private bool restoredPlayers;

        public void Configure(float seconds)
        {
            durationSeconds = Mathf.Clamp(seconds > 0f ? seconds : 60f, 20f, 180f);
            remainingSeconds = durationSeconds;
            configured = true;
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
            if (onlineManager == null)
            {
                onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            }
            if (onlineManager != null)
            {
                onlineManager.GimmickDataReceived += HandleNetworkData;
            }
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.GimmickDataReceived -= HandleNetworkData;
            }
            if (cameraFollow != null)
            {
                cameraFollow.SetMinimumOrthographicSize(previousCameraMinimum);
            }
            RestoreHiddenPlayers();
        }

        private void Start()
        {
            if (!configured)
            {
                Configure(60f);
            }

            BuildArena();
            if (cameraFollow != null)
            {
                previousCameraMinimum = cameraFollow.MinimumOrthographicSize;
                cameraFollow.SetMinimumOrthographicSize(10.2f);
            }
            CaptureParticipants();
            SetLocalControls(false);
            RefreshMonitor();
            nextTopBombAt = 7f;
            nextSideBombAt = 16f;
            nextCannonAt = 10f;
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId)
            {
                return;
            }

            if (IsOnlineActive() && !HasAuthority())
            {
                UpdateNetworkReplica();
                return;
            }

            BroadcastStateIfDue();

            if (phase == SurvivalPhase.Finished)
            {
                return;
            }
            if (phase == SurvivalPhase.Failed)
            {
                failedRestartRemaining -= Time.deltaTime;
                RefreshMonitor();
                if (failedRestartRemaining <= 0f && HasAuthority())
                {
                    stageManager.Retry();
                }
                return;
            }

            ApplyPendingOnlineEliminations();
            CheckLocalPlayerFalls();

            if (phase == SurvivalPhase.Intro)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    phase = SurvivalPhase.StartCountdown;
                    phaseRemaining = StartCountdownDuration;
                    GameSfx.Play(SfxId.StageCountdownTick);
                }
                RefreshMonitor();
                return;
            }

            if (phase == SurvivalPhase.StartCountdown)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    SetLocalControls(true);
                    if (HasAuthority())
                    {
                        StartNextRound();
                    }
                }
                RefreshMonitor();
                return;
            }

            elapsedSeconds += Time.deltaTime;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            UpdateFloorPulse();
            UpdateAttackDirector();

            if (phase == SurvivalPhase.Warning)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    CollapseUnsafeFloors();
                    phase = SurvivalPhase.Collapsed;
                    phaseRemaining = GetCollapsedDuration();
                    GameSfx.Play(SfxId.EditorObjectDrop);
                    BroadcastStateIfDue(true);
                }
            }
            else if (phase == SurvivalPhase.Collapsed)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    RestoreFloors();
                    if (HasAuthority())
                    {
                        StartNextRound();
                    }
                }
            }

            if (HasAuthority() && AreAllPlayersEliminated())
            {
                BeginAllDeadFailure();
                return;
            }

            if (remainingSeconds <= 0f && HasAuthority())
            {
                phase = SurvivalPhase.Finished;
                RestoreFloors();
                SetMonitorMain(string.Empty, 0.17f);
                stageManager.ClearStage();
                return;
            }

            RefreshMonitor();
        }

        private void UpdateNetworkReplica()
        {
            ApplyPendingOnlineEliminations();
            CheckLocalPlayerFalls();

            if (phase == SurvivalPhase.Intro
                || phase == SurvivalPhase.StartCountdown
                || phase == SurvivalPhase.Warning
                || phase == SurvivalPhase.Collapsed)
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
            }
            if (phase == SurvivalPhase.Warning || phase == SurvivalPhase.Collapsed)
            {
                elapsedSeconds += Time.deltaTime;
                remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            }
            if (phase == SurvivalPhase.Warning)
            {
                UpdateFloorPulse();
            }

            RefreshMonitor();
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase == SurvivalPhase.Intro || phase == SurvivalPhase.StartCountdown
                || phase == SurvivalPhase.Finished || phase == SurvivalPhase.Failed)
            {
                return;
            }

            string playerId = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(playerId) || eliminatedIds.Contains(playerId))
            {
                return;
            }
            if (!IsOnlineActive())
            {
                participantIds.Add(playerId);
            }

            if (IsOnlineActive())
            {
                string localId = onlineManager != null ? onlineManager.LocalPlayerId : null;
                if (playerId != localId)
                {
                    return;
                }

                if (!HasAuthority())
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = KindEliminateRequest,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = playerId })
                    });
                    ApplyElimination(playerId);
                    return;
                }
            }

            ConfirmElimination(playerId, IsOnlineActive());
        }

        private void ConfirmElimination(string playerId, bool broadcast)
        {
            ApplyElimination(playerId);
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindEliminated,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = playerId })
                });
            }
            BroadcastStateIfDue(true);
        }

        private void ApplyElimination(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || !eliminatedIds.Add(playerId))
            {
                return;
            }

            PlayerController2D target = ResolvePlayer(playerId);
            if (target != null)
            {
                HideEliminatedPlayer(target);
            }
            GameSfx.Play(SfxId.PlayerDeath);
            RefreshMonitor();
        }

        private void HideEliminatedPlayer(PlayerController2D target)
        {
            if (target == null || hiddenPlayers.Contains(target))
            {
                return;
            }

            target.GetComponent<PlayerCarryController>()?.ForceDrop();
            target.ResetMotion();
            target.SetControlsEnabled(false);
            hiddenPlayers.Add(target);
            target.gameObject.SetActive(false);
        }

        private void RestoreHiddenPlayers()
        {
            if (restoredPlayers)
            {
                return;
            }
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++)
            {
                if (hiddenPlayers[i] != null)
                {
                    hiddenPlayers[i].gameObject.SetActive(true);
                }
            }
            hiddenPlayers.Clear();
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || string.IsNullOrEmpty(data.Kind))
            {
                return;
            }

            if (data.Kind == KindRound && IsHostPlayer(data.PlayerId) && !HasAuthority())
            {
                RoundState state = JsonUtility.FromJson<RoundState>(data.Json);
                if (state != null && state.Round > roundNumber)
                {
                    ApplyRound(state);
                }
            }
            else if (data.Kind == KindState && IsHostPlayer(data.PlayerId) && !HasAuthority())
            {
                ApplyAuthoritativeState(JsonUtility.FromJson<SurvivalState>(data.Json));
            }
            else if (data.Kind == KindCannon && IsHostPlayer(data.PlayerId) && !HasAuthority())
            {
                ApplyCannonState(JsonUtility.FromJson<CannonState>(data.Json));
            }
            else if (data.Kind == KindEliminateRequest && HasAuthority())
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(data.Json);
                string requestedId = state != null && !string.IsNullOrEmpty(state.PlayerId)
                    ? state.PlayerId
                    : data.PlayerId;
                if (requestedId == data.PlayerId)
                {
                    ConfirmElimination(requestedId, true);
                }
            }
            else if (data.Kind == KindEliminated && IsHostPlayer(data.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(data.Json);
                if (state != null)
                {
                    ApplyElimination(state.PlayerId);
                }
            }
        }

        private void StartNextRound()
        {
            int safeCount = GetSafeFloorCount();
            List<int> selected = new List<int>(safeCount);
            int firstSafe = Random.Range(0, FloorCount);
            if (firstSafe == lastSafeFloor)
            {
                firstSafe = (firstSafe + Random.Range(1, FloorCount)) % FloorCount;
            }
            selected.Add(firstSafe);
            while (selected.Count < safeCount)
            {
                int candidate = Random.Range(0, FloorCount);
                if (!selected.Contains(candidate))
                {
                    selected.Add(candidate);
                }
            }
            RoundState state = new RoundState
            {
                Round = roundNumber + 1,
                SafeFloors = selected.ToArray(),
                WarningSeconds = GetWarningDuration()
            };
            ApplyRound(state);
            if (IsOnlineActive() && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindRound,
                    Json = JsonUtility.ToJson(state)
                });
            }
            BroadcastStateIfDue(true);
        }

        private void ApplyRound(RoundState state)
        {
            roundNumber = state.Round;
            safeFloors.Clear();
            if (state.SafeFloors != null)
            {
                for (int i = 0; i < state.SafeFloors.Length; i++)
                {
                    safeFloors.Add(Mathf.Clamp(state.SafeFloors[i], 0, FloorCount - 1));
                }
            }
            if (safeFloors.Count == 0)
            {
                safeFloors.Add(0);
            }
            foreach (int index in safeFloors)
            {
                lastSafeFloor = index;
                break;
            }
            phase = SurvivalPhase.Warning;
            phaseRemaining = Mathf.Max(1.5f, state.WarningSeconds);
            RestoreFloors();
            UpdateFloorPulse();
            GameSfx.Play(SfxId.UiToggleOn);
        }

        private void CollapseUnsafeFloors()
        {
            for (int i = 0; i < floors.Count; i++)
            {
                bool active = safeFloors.Contains(i);
                floors[i].Collider.enabled = active;
                floors[i].Fill.enabled = active;
                SetChildRenderersEnabled(floors[i].Root.transform, active, floors[i].Fill);
            }
        }

        private void RestoreFloors()
        {
            for (int i = 0; i < floors.Count; i++)
            {
                floors[i].Collider.enabled = true;
                floors[i].Fill.enabled = true;
                floors[i].Fill.color = floors[i].BaseColor;
                SetChildRenderersEnabled(floors[i].Root.transform, true, floors[i].Fill);
            }
        }

        private void UpdateFloorPulse()
        {
            if (phase != SurvivalPhase.Warning || safeFloors.Count == 0)
            {
                return;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            foreach (int index in safeFloors)
            {
                if (index >= 0 && index < floors.Count)
                {
                    floors[index].Fill.color = Color.Lerp(
                        new Color(0.25f, 0.95f, 1f, 1f),
                        new Color(1f, 0.94f, 0.2f, 1f),
                        pulse);
                }
            }
        }

        private int GetSafeFloorCount()
        {
            float progress = durationSeconds > 0f
                ? Mathf.Clamp01(remainingSeconds / durationSeconds)
                : 0f;
            if (progress > 0.75f)
            {
                return 5;
            }
            if (progress > 0.5f)
            {
                return 4;
            }
            return progress > 0.25f ? 3 : 2;
        }

        private void UpdateAttackDirector()
        {
            float progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            if (elapsedSeconds >= nextCannonAt)
            {
                int volley = 1 + Mathf.FloorToInt(progress * 3f);
                for (int i = 0; i < volley; i++)
                {
                    FireNextCannon();
                }
                nextCannonAt = elapsedSeconds + Mathf.Lerp(5.2f, 1.05f, progress);
            }

            if (!HasAuthority())
            {
                return;
            }

            if (elapsedSeconds >= nextTopBombAt)
            {
                int count = 1 + Mathf.FloorToInt(progress * 5f);
                for (int i = 0; i < count; i++)
                {
                    SpawnBomb(
                        new Vector2(Random.Range(-ArenaHalfWidth + 0.8f, ArenaHalfWidth - 0.8f), Random.Range(5.7f, 6.7f)),
                        new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0f)),
                        Random.Range(2.8f, 4f));
                }
                nextTopBombAt = elapsedSeconds + Mathf.Lerp(4.4f, 0.8f, progress);
            }
            if (elapsedSeconds >= nextSideBombAt)
            {
                int count = 1 + Mathf.FloorToInt(progress * 3f);
                for (int i = 0; i < count; i++)
                {
                    bool fromLeft = (bombSequence + i) % 2 == 0;
                    Vector2 position = new Vector2(fromLeft ? -ArenaHalfWidth + 0.55f : ArenaHalfWidth - 0.55f, Random.Range(-0.1f, 4.9f));
                    Vector2 velocity = new Vector2(fromLeft ? Random.Range(8f, 12f) : Random.Range(-12f, -8f), Random.Range(0.8f, 4.2f));
                    SpawnBomb(position, velocity, Random.Range(2.4f, 3.5f));
                }
                nextSideBombAt = elapsedSeconds + Mathf.Lerp(5.8f, 1.25f, progress);
            }
        }

        private void SpawnBomb(Vector2 position, Vector2 velocity, float fuseSeconds)
        {
            string objectId = "survival_bomb_" + (++bombSequence);
            StageObjectType type = StageObjectType.Bomb;
            GameObject bomb = IsOnlineActive() && syncManager != null
                ? syncManager.SpawnDropperBox(objectId, type, position, 0.85f, 0f, fuseSeconds)
                : objectFactory != null
                    ? objectFactory.CreateDroppedBox(type, objectId, position, 0.85f, transform, fuseSeconds)
                    : null;
            Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
            if (body != null)
            {
                body.linearVelocity = velocity;
                body.AddTorque(Random.Range(-2.2f, 2.2f), ForceMode2D.Impulse);
            }
        }

        private void FireNextCannon()
        {
            if (cannons.Count == 0)
            {
                return;
            }

            CannonPoint cannon = cannons[cannonSequence % cannons.Count];
            cannonSequence++;
            float speed = Mathf.Lerp(7.5f, 12.5f, Mathf.Clamp01(elapsedSeconds / durationSeconds));
            CannonState state = new CannonState
            {
                Sequence = cannonSequence,
                Position = cannon.Position + cannon.Direction * 0.85f,
                Direction = cannon.Direction,
                Speed = speed
            };
            ApplyCannonState(state);
            if (IsOnlineActive() && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = KindCannon,
                    Json = JsonUtility.ToJson(state)
                });
            }
        }

        private void ApplyCannonState(CannonState state)
        {
            if (state == null || state.Sequence <= lastReceivedCannonSequence)
            {
                return;
            }

            lastReceivedCannonSequence = state.Sequence;
            SurvivalCannonball.Create(transform, state.Position, state.Direction, state.Speed);
            GameSfx.PlayAt(SfxId.CannonFire, state.Position);
        }

        private void BroadcastStateIfDue(bool force = false)
        {
            if (!IsOnlineActive() || !HasAuthority() || onlineManager == null
                || !force && Time.unscaledTime < nextStateBroadcastAt)
            {
                return;
            }

            nextStateBroadcastAt = Time.unscaledTime + 0.2f;
            SurvivalState state = new SurvivalState
            {
                Sequence = ++stateSequence,
                Phase = (int)phase,
                Round = roundNumber,
                SafeFloors = new List<int>(safeFloors).ToArray(),
                EliminatedIds = new List<string>(eliminatedIds).ToArray(),
                RemainingSeconds = remainingSeconds,
                ElapsedSeconds = elapsedSeconds,
                PhaseRemaining = phaseRemaining
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = KindState,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyAuthoritativeState(SurvivalState state)
        {
            if (state == null || state.Sequence <= lastReceivedStateSequence)
            {
                return;
            }

            lastReceivedStateSequence = state.Sequence;
            SurvivalPhase previousPhase = phase;
            int previousRound = roundNumber;
            phase = (SurvivalPhase)Mathf.Clamp(state.Phase, 0, (int)SurvivalPhase.Failed);
            roundNumber = Mathf.Max(roundNumber, state.Round);
            remainingSeconds = Mathf.Max(0f, state.RemainingSeconds);
            elapsedSeconds = Mathf.Max(0f, state.ElapsedSeconds);
            phaseRemaining = Mathf.Max(0f, state.PhaseRemaining);

            safeFloors.Clear();
            if (state.SafeFloors != null)
            {
                for (int i = 0; i < state.SafeFloors.Length; i++)
                {
                    safeFloors.Add(Mathf.Clamp(state.SafeFloors[i], 0, FloorCount - 1));
                }
            }
            if (state.EliminatedIds != null)
            {
                for (int i = 0; i < state.EliminatedIds.Length; i++)
                {
                    ApplyElimination(state.EliminatedIds[i]);
                }
            }

            if (phase == SurvivalPhase.Collapsed)
            {
                CollapseUnsafeFloors();
            }
            else if (previousPhase != phase || previousRound != roundNumber)
            {
                RestoreFloors();
            }
            SetLocalControls(phase == SurvivalPhase.Warning || phase == SurvivalPhase.Collapsed);
            if (previousPhase != phase && phase == SurvivalPhase.Collapsed)
            {
                GameSfx.Play(SfxId.EditorObjectDrop);
            }
            RefreshMonitor();
        }

        private void CheckLocalPlayerFalls()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || player.transform.position.y > FloorY - 4.5f)
                {
                    continue;
                }
                RequestElimination(player);
            }
        }

        private void ApplyPendingOnlineEliminations()
        {
            if (!IsOnlineActive())
            {
                return;
            }
            foreach (string id in eliminatedIds)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player != null && player.gameObject.activeSelf)
                {
                    HideEliminatedPlayer(player);
                }
            }
        }

        private bool AreAllPlayersEliminated()
        {
            if (IsOnlineActive())
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players == null || players.Length == 0)
                {
                    return false;
                }
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)
                        && !eliminatedIds.Contains(players[i].PlayerId))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (participantIds.Count == 0)
            {
                return false;
            }
            foreach (string id in participantIds)
            {
                if (!eliminatedIds.Contains(id))
                {
                    return false;
                }
            }
            return true;
        }

        private void CaptureParticipants()
        {
            if (IsOnlineActive())
            {
                OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
                if (lobbyPlayers == null)
                {
                    return;
                }
                for (int i = 0; i < lobbyPlayers.Length; i++)
                {
                    if (lobbyPlayers[i] != null && !string.IsNullOrEmpty(lobbyPlayers[i].PlayerId))
                    {
                        participantIds.Add(lobbyPlayers[i].PlayerId);
                    }
                }
                return;
            }

            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                string id = ResolvePlayerId(players[i]);
                if (!string.IsNullOrEmpty(id))
                {
                    participantIds.Add(id);
                }
            }
        }

        private void BeginAllDeadFailure()
        {
            phase = SurvivalPhase.Failed;
            failedRestartRemaining = 2.2f;
            RestoreFloors();
            SetLocalControls(false);
            SetMonitorMain(string.Empty, 0.14f);
            GameSfx.Play(SfxId.PlayerHit);
        }

        private float GetWarningDuration()
        {
            float progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            return Mathf.Lerp(5f, 2.2f, progress);
        }

        private float GetCollapsedDuration()
        {
            return Mathf.Lerp(2f, 1.25f, Mathf.Clamp01(elapsedSeconds / durationSeconds));
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null)
            {
                return;
            }

            if (phase == SurvivalPhase.StartCountdown)
            {
                if (phaseRemaining > 3f) SetMonitorMain("3", 0.19f);
                else if (phaseRemaining > 2f) SetMonitorMain("2", 0.19f);
                else if (phaseRemaining > 1f) SetMonitorMain("1", 0.19f);
                else SetMonitorMain(LocalizationManager.T("survival_start"), 0.145f);
                return;
            }

            if (phase == SurvivalPhase.Warning || phase == SurvivalPhase.Collapsed)
            {
                string remaining = string.Format(
                    LocalizationManager.T("challenge_time_remaining"),
                    Mathf.Max(0f, remainingSeconds));
                SetMonitorMain(remaining, 0.17f);
                return;
            }

            SetMonitorMain(string.Empty, 0.17f);
        }

        private void SetMonitorMain(string value, float characterSize)
        {
            if (monitorMain == null)
            {
                return;
            }
            monitorMain.text = value;
            float fitSize = 1.55f / Mathf.Max(1, value != null ? value.Length : 0);
            monitorMain.characterSize = Mathf.Min(characterSize, fitSize);
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("11-2 Survival Arena");
            arena.transform.SetParent(transform, false);

            for (int i = 0; i < FloorCount; i++)
            {
                float x = -ArenaHalfWidth + FloorWidth * (i + 0.5f);
                floors.Add(CreateFloorPiece(arena.transform, i, new Vector2(x, FloorY)));
            }

            CreateStandardStageBoundary(arena.transform);
            CreateCannons(arena.transform);
            CreateMonitor(arena.transform);
        }

        private void CreateCannons(Transform parent)
        {
            AddCannon(parent, new Vector2(-ArenaHalfWidth + 0.65f, 4.8f), new Vector2(1f, -0.24f));
            AddCannon(parent, new Vector2(-ArenaHalfWidth + 0.65f, 1.5f), new Vector2(1f, 0.12f));
            AddCannon(parent, new Vector2(ArenaHalfWidth - 0.65f, 4.8f), new Vector2(-1f, -0.24f));
            AddCannon(parent, new Vector2(ArenaHalfWidth - 0.65f, 1.5f), new Vector2(-1f, 0.12f));
            AddCannon(parent, new Vector2(-8.5f, 6.65f), new Vector2(0.46f, -1f));
            AddCannon(parent, new Vector2(0f, 6.65f), new Vector2(0.18f, -1f));
            AddCannon(parent, new Vector2(8.5f, 6.65f), new Vector2(-0.46f, -1f));
        }

        private void AddCannon(Transform parent, Vector2 position, Vector2 direction)
        {
            direction.Normalize();
            cannons.Add(new CannonPoint { Position = position, Direction = direction });

            GameObject root = new GameObject("Survival Cannon");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            GameObject baseObject = new GameObject("Cannon Base");
            baseObject.transform.SetParent(root.transform, false);
            baseObject.transform.localScale = Vector3.one * 0.82f;
            SpriteRenderer baseRenderer = baseObject.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            baseRenderer.color = new Color(0.22f, 0.24f, 0.27f, 1f);
            baseRenderer.sortingOrder = 37;

            GameObject barrel = new GameObject("Cannon Barrel");
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = direction * 0.5f;
            barrel.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            barrel.transform.localScale = new Vector3(1.35f, 0.48f, 1f);
            SpriteRenderer barrelRenderer = barrel.AddComponent<SpriteRenderer>();
            barrelRenderer.sprite = DoodleRuntimeAssets.SquareSprite;
            barrelRenderer.color = new Color(0.29f, 0.31f, 0.34f, 1f);
            barrelRenderer.sortingOrder = 38;

            AddBoxOutline(barrel.transform, new Vector2(1f, 1f), new Color(0.04f, 0.05f, 0.06f, 1f), 39);
        }

        private void CreateStandardStageBoundary(Transform parent)
        {
            if (objectFactory == null)
            {
                return;
            }

            StageObjectData boundary = StageObjectFactory.CreateDefaultData(
                StageObjectType.StageBoundary,
                new Vector2(0f, 2.35f));
            boundary.objectId = "survival_stage_boundary";
            boundary.size = new Vector2(ArenaHalfWidth * 2f + 1.2f, 10.6f);
            boundary.pathThickness = 0.65f;
            objectFactory.Create(boundary, parent);
        }

        private FloorPiece CreateFloorPiece(Transform parent, int index, Vector2 position)
        {
            GameObject root = new GameObject("Survival Floor " + (index + 1));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.layer = 6;
            root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(FloorWidth - 0.08f, 0.62f);

            GameObject fillObject = new GameObject("Paper Fill");
            fillObject.transform.SetParent(root.transform, false);
            fillObject.transform.localScale = new Vector3(FloorWidth - 0.08f, 0.62f, 1f);
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            fill.sprite = DoodleRuntimeAssets.SquareSprite;
            Color baseColor = index % 2 == 0
                ? new Color(0.93f, 0.89f, 0.77f, 1f)
                : new Color(0.88f, 0.84f, 0.72f, 1f);
            fill.color = baseColor;
            fill.sortingOrder = 12;
            AddBoxOutline(root.transform, new Vector2(FloorWidth - 0.08f, 0.62f), new Color(0.18f, 0.12f, 0.08f, 1f), 13);
            return new FloorPiece { Root = root, Collider = collider, Fill = fill, BaseColor = baseColor };
        }

        private void CreateMonitor(Transform parent)
        {
            GameObject monitor = new GameObject("Survival Center Monitor");
            monitor.transform.SetParent(parent, false);
            monitor.transform.localPosition = new Vector3(0f, 2.65f, 0.4f);

            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(8.4f, 2.3f), -32);
            monitorMain = CreateMonitorText(monitor.transform, "Monitor Main", new Vector3(0f, -0.02f, -0.03f), 78, 0.23f, new Color(0.04f, 0.43f, 0.58f, 1f), -25);
        }

        private static void CreateMonitorRect(Transform parent, string name, Vector2 size, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static TextMesh CreateMonitorText(Transform parent, string name, Vector3 position, int fontSize, float characterSize, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            Font font = DoodleRuntimeAssets.HandwrittenFont;
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
            return text;
        }

        private static void AddBoxOutline(Transform parent, Vector2 size, Color color, int sortingOrder)
        {
            Vector2 half = size * 0.5f;
            AddPolyline(parent, new[]
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y),
                new Vector2(-half.x, -half.y)
            }, 0.055f, color, sortingOrder);
        }

        private static void AddPolyline(Transform parent, Vector2[] points, float width, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject("Pencil Line");
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }
        }

        private static void SetChildRenderersEnabled(Transform root, bool enabled, SpriteRenderer mainFill)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != mainFill)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null)
            {
                return;
            }
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode && !IsEliminated(active));
            if (!IsOnlineActive())
            {
                PlayerController2D secondary = stageManager.RemotePlayerController;
                secondary?.SetControlsEnabled(enabled && !IsEliminated(secondary));
            }
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminatedIds.Contains(id);
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null)
            {
                return null;
            }
            if (IsOnlineActive())
            {
                return stageManager != null ? stageManager.GetOnlinePlayerId(player) : null;
            }
            return "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return null;
            }
            if (IsOnlineActive())
            {
                return stageManager != null ? stageManager.GetOnlinePlayerController(playerId) : null;
            }
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (ResolvePlayerId(players[i]) == playerId)
                {
                    return players[i];
                }
            }
            return null;
        }

        private bool IsOnlineActive()
        {
            return stageManager != null && stageManager.IsOnlineStageActive;
        }

        private bool HasAuthority()
        {
            return !IsOnlineActive() || stageManager.IsOnlineStageHost;
        }

        private bool IsHostPlayer(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null)
            {
                return false;
            }
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId)
                {
                    return true;
                }
            }
            return false;
        }

    }

    [DisallowMultipleComponent]
    public sealed class SurvivalCannonball : MonoBehaviour
    {
        private Rigidbody2D body;
        private Vector2 direction;
        private float speed;
        private float lifeRemaining = 7f;

        public static SurvivalCannonball Create(Transform parent, Vector2 position, Vector2 shotDirection, float shotSpeed)
        {
            GameObject root = new GameObject("Survival Cannonball");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = 0;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;
            collider.isTrigger = true;

            GameObject visual = new GameObject("Cannonball Ink");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.68f;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.08f, 0.085f, 0.1f, 1f);
            renderer.sortingOrder = 46;

            SurvivalCannonball ball = root.AddComponent<SurvivalCannonball>();
            ball.body = body;
            ball.direction = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : Vector2.right;
            ball.speed = Mathf.Max(1f, shotSpeed);
            body.linearVelocity = ball.direction * ball.speed;
            return ball;
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }
            body.linearVelocity = direction * speed;
            lifeRemaining -= Time.fixedDeltaTime;
            if (lifeRemaining <= 0f
                || Mathf.Abs(transform.position.x) > 24f
                || transform.position.y < -9f
                || transform.position.y > 11f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null)
            {
                return;
            }
            if (player.IsInvulnerable)
            {
                Destroy(gameObject);
                return;
            }
            Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
        }
    }
}
