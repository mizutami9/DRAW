using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>Marker and shared zero-friction material for editable ice terrain.</summary>
    [DisallowMultipleComponent]
    public sealed class StageIceSurface : MonoBehaviour
    {
        private static PhysicsMaterial2D iceMaterial;

        public static PhysicsMaterial2D GetMaterial()
        {
            if (iceMaterial == null)
            {
                iceMaterial = new PhysicsMaterial2D("Stage Ice")
                {
                    friction = 0f,
                    bounciness = 0f,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return iceMaterial;
        }
    }

    /// <summary>
    /// Host-authoritative, individual-lane team time trial for 10-1.
    /// Every participant must finish; the sum of the individual finish times
    /// must be no greater than one minute per participant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageIceSpeedrunController : MonoBehaviour
    {
        private const string StageId = "10-1";
        private const string StateKind = "ice_race_state";
        private const string FinishRequestKind = "ice_race_finish_request";
        private const float IntroSeconds = 3f;
        private const float RetryDelay = 3.5f;
        private const float SecondsPerPlayer = 60f;
        private const int MaxLanes = 4;
        private const float FirstLaneTop = 18f;
        private const float LaneSpacing = 23f;
        private const float StartX = -123.6f;

        [Serializable]
        private sealed class RaceState
        {
            public int Sequence;
            public float Elapsed;
            public float IntroRemaining;
            public float StartFlashRemaining;
            public float RetryRemaining;
            public bool Started;
            public bool Failed;
            public int PlayerCount;
            public string[] PlayerIds;
            public float[] FinishTimes;
        }

        private readonly string[] participantIds = new string[MaxLanes];
        private readonly float[] finishTimes = new float[MaxLanes];
        private readonly List<TextMesh> monitorTexts = new List<TextMesh>();
        private readonly List<TextMesh> monitorSubTexts = new List<TextMesh>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageGimmickSyncManager syncManager;
        private UIManager uiManager;
        private float elapsed;
        private float introRemaining = IntroSeconds;
        private float startFlashRemaining;
        private float retryRemaining;
        private float nextBroadcastAt;
        private int stateSequence;
        private int receivedStateSequence;
        private int playerCount = 1;
        private bool started;
        private bool failed;
        private bool rosterReady;
        private int lastCountdownCue = int.MinValue;
        private readonly HashSet<string> localFinishRequested = new HashSet<string>();

        public bool CanFinish => started && !failed;
        private bool IsOnline => syncManager != null && syncManager.IsOnlineActive;
        private bool HasAuthority => !IsOnline || syncManager.IsHost;

        // Retained for StageLoader/backward compatibility. This race has no
        // single global time limit; its limit is evaluated from the finish sum.
        public void Configure(float unusedSeconds) { }

        private void Awake()
        {
            stageManager = FindFirstObjectByType<StageManager>();
            onlineManager = FindFirstObjectByType<OnlineManager>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            uiManager = FindFirstObjectByType<UIManager>();
            ResetFinishTimes();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        private void Start()
        {
            RuntimeStageEditor editor = FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            BuildParticipantRoster();
            ConfigureActiveLanes();
            BuildMonitors();
            PositionLocalPlayersAtStarts();
            SetLocalControls(false);
            RefreshMonitors();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (!rosterReady) BuildParticipantRoster();
            UpdateCountdownAudio();

            if (!stageManager.IsGameplayActive)
            {
                if (!started) PositionLocalPlayersAtStarts();
                RefreshMonitors();
                return;
            }

            if (!started)
            {
                PositionLocalPlayersAtStarts();
                SetLocalControls(false);
            }

            if (HasAuthority)
            {
                UpdateAuthoritativeRace();
            }
            else if (started && !failed)
            {
                // Extrapolate between host snapshots so the monitor does not
                // visibly tick at the network snapshot frequency.
                elapsed += Time.deltaTime;
                startFlashRemaining = Mathf.Max(0f, startFlashRemaining - Time.deltaTime);
                ApplyLocalControlState();
            }

            RefreshMonitors();
        }

        public void SetPlayerGoalState(PlayerController2D goalPlayer, bool inside)
        {
            if (!inside || goalPlayer == null || !started || failed) return;

            if (IsOnline)
            {
                string gamePlayerId = stageManager != null ? stageManager.GetOnlinePlayerId(goalPlayer) : null;
                if (stageManager == null || !stageManager.IsLocallyControlledPlayer(goalPlayer)
                    || onlineManager == null || string.IsNullOrEmpty(gamePlayerId)
                    || localFinishRequested.Contains(gamePlayerId))
                    return;

                localFinishRequested.Add(gamePlayerId);
                goalPlayer.ResetMotion();
                goalPlayer.SetControlsEnabled(false);
                if (HasAuthority)
                {
                    RecordFinish(gamePlayerId);
                }
                else
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = FinishRequestKind,
                        PlayerId = gamePlayerId,
                        Json = "{}"
                    });
                }
                return;
            }

            string id = GetOfflinePlayerId(goalPlayer);
            if (!string.IsNullOrEmpty(id)) RecordFinish(id);
        }

        private void UpdateAuthoritativeRace()
        {
            if (!started)
            {
                introRemaining = Mathf.Max(0f, introRemaining - Time.deltaTime);
                if (introRemaining <= 0f)
                {
                    started = true;
                    GameSfx.Play(SfxId.StageCountdownGo);
                    elapsed = 0f;
                    startFlashRemaining = 0.8f;
                    ApplyLocalControlState();
                    Broadcast(true);
                }
            }
            else if (!failed)
            {
                elapsed += Time.deltaTime;
                startFlashRemaining = Mathf.Max(0f, startFlashRemaining - Time.deltaTime);
                ApplyLocalControlState();
            }
            else
            {
                retryRemaining = Mathf.Max(0f, retryRemaining - Time.deltaTime);
                ShowFailureOverlay();
                if (retryRemaining <= 0f) stageManager.Retry();
            }
            Broadcast(false);
        }

        private void BuildParticipantRoster()
        {
            Array.Clear(participantIds, 0, participantIds.Length);
            if (IsOnline && onlineManager != null)
            {
                OnlineLobbyInfo lobby = onlineManager.CurrentLobby;
                if (lobby == null || lobby.Players == null || lobby.Players.Length == 0) return;
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    OnlinePlayerInfo member = lobby.Players[i];
                    if (member == null || string.IsNullOrEmpty(member.PlayerId)) continue;
                    int lane = PlayerColorPalette.GetLobbyPlayerSlot(lobby, member.PlayerId);
                    if (lane < 0 || lane >= MaxLanes || !string.IsNullOrEmpty(participantIds[lane]))
                        lane = FindFreeLane();
                    if (lane >= 0) participantIds[lane] = member.PlayerId;
                }
            }
            else
            {
                int count = Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, MaxLanes);
                for (int i = 0; i < count; i++) participantIds[i] = "local-" + (i + 1);
            }

            playerCount = CountParticipants();
            rosterReady = playerCount > 0;
        }

        private int FindFreeLane()
        {
            for (int i = 0; i < participantIds.Length; i++)
                if (string.IsNullOrEmpty(participantIds[i])) return i;
            return -1;
        }

        private int CountParticipants()
        {
            int count = 0;
            for (int i = 0; i < participantIds.Length; i++)
                if (!string.IsNullOrEmpty(participantIds[i])) count++;
            return Mathf.Max(1, count);
        }

        private void ConfigureActiveLanes()
        {
            StageEditorObject[] objects = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                int lane = ParseLane(objects[i] != null ? objects[i].objectId : null);
                if (lane >= 0) objects[i].gameObject.SetActive(!string.IsNullOrEmpty(participantIds[lane]));
            }
        }

        private static int ParseLane(string objectId)
        {
            const string prefix = "10-1-lane-";
            if (string.IsNullOrEmpty(objectId) || !objectId.StartsWith(prefix, StringComparison.Ordinal)
                || objectId.Length <= prefix.Length)
                return -1;
            int lane = objectId[prefix.Length] - '1';
            return lane >= 0 && lane < MaxLanes ? lane : -1;
        }

        private void PositionLocalPlayersAtStarts()
        {
            if (stageManager == null) return;
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D current = players[i];
                if (current == null || !current.gameObject.activeInHierarchy) continue;
                int lane = GetPlayerLane(current);
                if (lane < 0) continue;
                float destinationY = GetLaneTop(lane) + 0.06f;
                if (TryGetPlayerBottom(current, out float currentBottom))
                    destinationY = current.transform.position.y + destinationY - currentBottom;
                else
                    destinationY += 0.65f;
                Vector3 destination = new Vector3(StartX, destinationY, current.transform.position.z);
                Rigidbody2D body = current.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = destination;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                else current.transform.position = destination;
                current.ResetMotion();
                stageManager.RecordAssignedPlayerStart(current, destination);
            }
        }

        private static bool TryGetPlayerBottom(PlayerController2D player, out float bottom)
        {
            bottom = player != null ? player.transform.position.y : 0f;
            if (player == null) return false;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>();
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D current = colliders[i];
                if (current == null || !current.enabled || current.isTrigger) continue;
                if (!found || current.bounds.min.y < bottom) bottom = current.bounds.min.y;
                found = true;
            }
            return found;
        }

        private int GetPlayerLane(PlayerController2D current)
        {
            if (IsOnline)
            {
                if (!stageManager.IsLocallyControlledPlayer(current)) return -1;
                return FindParticipant(stageManager.GetOnlinePlayerId(current));
            }
            if (!stageManager.IsLocallyControlledPlayer(current)) return -1;
            int localIndex = stageManager.GetLocalPlayerIndex(current);
            return FindParticipant("local-" + (localIndex + 1));
        }

        private string GetOfflinePlayerId(PlayerController2D current)
        {
            if (stageManager == null || current == null) return null;
            if (!stageManager.IsLocallyControlledPlayer(current)) return null;
            return "local-" + (stageManager.GetLocalPlayerIndex(current) + 1);
        }

        private void RecordFinish(string playerId)
        {
            if (!HasAuthority || string.IsNullOrEmpty(playerId)) return;
            int lane = FindParticipant(playerId);
            if (lane < 0 || finishTimes[lane] >= 0f) return;
            finishTimes[lane] = elapsed;
            GameSfx.Play(SfxId.GoalReached, 0.82f);
            FreezeFinishedLocalPlayer(lane);
            Broadcast(true);

            if (!AllParticipantsFinished()) return;
            float total = GetFinishedTotal();
            if (total <= GetScoredPlayerCount() * SecondsPerPlayer + 0.001f)
            {
                stageManager.ClearStage();
            }
            else
            {
                failed = true;
                GameSfx.Play(SfxId.StageFailed);
                retryRemaining = RetryDelay;
                SetLocalControls(false);
                ShowFailureOverlay();
                Broadcast(true);
            }
        }

        private void FreezeFinishedLocalPlayer(int finishedLane)
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < players.Length; i++)
            {
                if (GetPlayerLane(players[i]) != finishedLane) continue;
                players[i].ResetMotion();
                players[i].SetControlsEnabled(false);
                if (!IsOnline && players[i].transform == stageManager.ActivePlayerTransform && !AllParticipantsFinished())
                    stageManager.SwitchCharacter();
            }
            ApplyLocalControlState();
        }

        private void SetLocalControls(bool value)
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (!value) players[i].ResetMotion();
                players[i].SetControlsEnabled(value && stageManager.IsLocallyControlledPlayer(players[i]));
            }
        }

        private void ApplyLocalControlState()
        {
            if (!started || failed)
            {
                SetLocalControls(false);
                return;
            }
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                int lane = GetPlayerLane(players[i]);
                bool canMove = lane >= 0 && finishTimes[lane] < 0f
                    && stageManager.IsLocallyControlledPlayer(players[i]);
                players[i].SetControlsEnabled(canMove);
            }
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == FinishRequestKind && HasAuthority)
            {
                if (CanAcceptRemoteFinish(data.PlayerId)) RecordFinish(data.PlayerId);
                return;
            }
            if (data.Kind != StateKind || HasAuthority || onlineManager == null
                || !onlineManager.IsHostPlayer(data.PlayerId)) return;

            RaceState state = JsonUtility.FromJson<RaceState>(data.Json);
            if (state == null || state.Sequence <= receivedStateSequence) return;
            bool wasStarted = started;
            bool wasFailed = failed;
            float[] previousFinishTimes = (float[])finishTimes.Clone();
            receivedStateSequence = state.Sequence;
            elapsed = state.Elapsed;
            introRemaining = state.IntroRemaining;
            startFlashRemaining = state.StartFlashRemaining;
            retryRemaining = state.RetryRemaining;
            started = state.Started;
            failed = state.Failed;
            playerCount = Mathf.Clamp(state.PlayerCount, 1, MaxLanes);
            CopyArray(state.PlayerIds, participantIds, null);
            CopyArray(state.FinishTimes, finishTimes, -1f);
            if (!wasStarted && started) GameSfx.Play(SfxId.StageCountdownGo);
            if (!wasFailed && failed) GameSfx.Play(SfxId.StageFailed);
            for (int i = 0; i < finishTimes.Length; i++)
                if (previousFinishTimes[i] < 0f && finishTimes[i] >= 0f)
                    GameSfx.Play(SfxId.GoalReached, 0.82f);
            rosterReady = true;
            ConfigureActiveLanes();
            if (monitorTexts.Count == 0) BuildMonitors();
            localFinishRequested.Clear();
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++)
            {
                string id = stageManager.GetOnlinePlayerId(locals[i]);
                int localLane = FindParticipant(id);
                if (localLane >= 0 && finishTimes[localLane] >= 0f) localFinishRequested.Add(id);
            }
            ApplyLocalControlState();
            if (failed) ShowFailureOverlay();
        }

        private void UpdateCountdownAudio()
        {
            if (started || failed || !stageManager.IsGameplayActive) return;
            int cue = Mathf.CeilToInt(introRemaining);
            if (cue < 1 || cue > 3 || cue == lastCountdownCue) return;
            lastCountdownCue = cue;
            GameSfx.Play(SfxId.StageCountdownTick);
        }

        private void Broadcast(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null) return;
            if (!force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.15f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new RaceState
                {
                    Sequence = ++stateSequence,
                    Elapsed = elapsed,
                    IntroRemaining = introRemaining,
                    StartFlashRemaining = startFlashRemaining,
                    RetryRemaining = retryRemaining,
                    Started = started,
                    Failed = failed,
                    PlayerCount = playerCount,
                    PlayerIds = (string[])participantIds.Clone(),
                    FinishTimes = (float[])finishTimes.Clone()
                })
            });
        }

        private bool CanAcceptRemoteFinish(string playerId)
        {
            if (!started || failed || stageManager == null || string.IsNullOrEmpty(playerId)) return false;
            int lane = FindParticipant(playerId);
            if (lane < 0 || finishTimes[lane] >= 0f) return false;
            PlayerController2D player = stageManager.GetOnlinePlayerController(playerId);
            if (player == null) return false;

            Vector2 position = player.transform.position;
            string expectedId = "10-1-lane-" + (lane + 1) + "-goal";
            StageEditorObject[] objects = UnityEngine.Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject marker = objects[i];
                if (marker == null || marker.objectId != expectedId) continue;
                Collider2D goalCollider = marker.GetComponent<Collider2D>();
                if (goalCollider == null) goalCollider = marker.GetComponentInChildren<Collider2D>();
                if (goalCollider == null) return Vector2.Distance(position, marker.transform.position) <= 3.5f;
                // The host sees a delayed replica of the remote player, so allow
                // one short movement window beyond the trigger itself.
                return Vector2.Distance(goalCollider.ClosestPoint(position), position) <= 6f;
            }
            return false;
        }

        private void BuildMonitors()
        {
            for (int i = monitorTexts.Count - 1; i >= 0; i--)
                if (monitorTexts[i] != null) Destroy(monitorTexts[i].gameObject);
            for (int i = monitorSubTexts.Count - 1; i >= 0; i--)
                if (monitorSubTexts[i] != null) Destroy(monitorSubTexts[i].gameObject);
            monitorTexts.Clear();
            monitorSubTexts.Clear();

            StageEditorObject[] authoredObjects = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < authoredObjects.Length; i++)
            {
                StageEditorObject marker = authoredObjects[i];
                if (marker == null || !marker.gameObject.activeInHierarchy
                    || marker.type != StageObjectType.ChallengeClock
                    || string.IsNullOrEmpty(marker.objectId)
                    || !marker.objectId.StartsWith("10-1-lane-", StringComparison.Ordinal))
                    continue;
                StageChallengeClock clock = marker.GetComponent<StageChallengeClock>();
                if (clock != null) clock.enabled = false;
                DoodleMonitorTextReadability readability = marker.GetComponent<DoodleMonitorTextReadability>();
                if (readability != null) readability.enabled = false;
                TextMesh[] oldTexts = marker.GetComponentsInChildren<TextMesh>(true);
                for (int textIndex = 0; textIndex < oldTexts.Length; textIndex++)
                {
                    MeshRenderer oldRenderer = oldTexts[textIndex].GetComponent<MeshRenderer>();
                    if (oldRenderer != null) oldRenderer.enabled = false;
                    oldTexts[textIndex].gameObject.SetActive(false);
                }
                CreateMonitorTexts(marker.transform);
            }
            if (monitorTexts.Count > 0) return;

            float[] xPositions = { -113f, 20f, 75f, 191f };
            for (int lane = 0; lane < MaxLanes; lane++)
            {
                if (string.IsNullOrEmpty(participantIds[lane])) continue;
                for (int station = 0; station < xPositions.Length; station++)
                {
                    GameObject monitor = new GameObject("10-1 Race Monitor L" + (lane + 1) + "-" + station);
                    monitor.transform.SetParent(transform, false);
                    monitor.transform.position = new Vector3(xPositions[station], GetLaneTop(lane) + 5.2f, 0.3f);
                    DoodleMonitorVisuals.Build(monitor.transform, new Vector2(8.5f, 2.2f), 1);
                    CreateMonitorTexts(monitor.transform);
                }
            }
        }

        private void RefreshMonitors()
        {
            string value;
            string subValue;
            Color color;
            if (!started)
            {
                value = Mathf.Max(1, Mathf.CeilToInt(introRemaining)).ToString();
                subValue = string.Empty;
                color = new Color(0.08f, 0.09f, 0.1f);
            }
            else if (startFlashRemaining > 0f)
            {
                value = LocalizationManager.T("ice_speedrun_start");
                subValue = string.Empty;
                color = new Color(0.08f, 0.09f, 0.1f);
            }
            else if (failed)
            {
                value = LocalizationManager.T("game_over") + "  "
                    + LocalizationManager.Format("ice_race_team", FormatTime(GetFinishedTotal()), FormatTime(GetScoredPlayerCount() * SecondsPerPlayer));
                subValue = LocalizationManager.Format("ice_race_retry", Mathf.CeilToInt(retryRemaining));
                color = new Color(0.08f, 0.09f, 0.1f);
            }
            else
            {
                value = LocalizationManager.Format("ice_race_team", FormatTime(GetProjectedTotal()), FormatTime(GetScoredPlayerCount() * SecondsPerPlayer));
                subValue = BuildPlayerTimes();
                color = new Color(0.08f, 0.09f, 0.1f);
            }

            for (int i = 0; i < monitorTexts.Count; i++)
            {
                if (monitorTexts[i] == null) continue;
                monitorTexts[i].text = value;
                monitorTexts[i].color = color;
            }
            for (int i = 0; i < monitorSubTexts.Count; i++)
            {
                if (monitorSubTexts[i] == null) continue;
                monitorSubTexts[i].text = subValue;
                monitorSubTexts[i].color = color;
            }
        }

        private string BuildPlayerTimes()
        {
            List<string> values = new List<string>();
            for (int lane = 0; lane < MaxLanes; lane++)
            {
                if (!IsScoredParticipant(lane)) continue;
                float value = finishTimes[lane] >= 0f ? finishTimes[lane] : elapsed;
                string suffix = finishTimes[lane] >= 0f ? "  OK" : string.Empty;
                values.Add("P" + (lane + 1) + " " + FormatTime(value) + suffix);
            }
            if (values.Count <= 2) return string.Join("     ", values);
            int split = (values.Count + 1) / 2;
            return string.Join("     ", values.GetRange(0, split).ToArray())
                + "\n" + string.Join("     ", values.GetRange(split, values.Count - split).ToArray());
        }

        private float GetFinishedTotal()
        {
            float total = 0f;
            for (int i = 0; i < MaxLanes; i++)
                if (IsScoredParticipant(i) && finishTimes[i] >= 0f) total += finishTimes[i];
            return total;
        }

        private void ShowFailureOverlay()
        {
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
            uiManager?.SetChallengeCountdown(true,
                LocalizationManager.Format("ice_race_game_over",
                    FormatTime(GetFinishedTotal()),
                    FormatTime(GetScoredPlayerCount() * SecondsPerPlayer)));
        }

        private float GetProjectedTotal()
        {
            float total = 0f;
            for (int i = 0; i < MaxLanes; i++)
            {
                if (!IsScoredParticipant(i)) continue;
                total += finishTimes[i] >= 0f ? finishTimes[i] : elapsed;
            }
            return total;
        }

        private bool AllParticipantsFinished()
        {
            bool found = false;
            for (int i = 0; i < MaxLanes; i++)
            {
                if (!IsScoredParticipant(i)) continue;
                found = true;
                if (finishTimes[i] < 0f) return false;
            }
            return found;
        }

        private bool IsScoredParticipant(int lane)
        {
            if (lane < 0 || lane >= participantIds.Length
                || string.IsNullOrEmpty(participantIds[lane])) return false;
            return !IsOnline || onlineManager != null
                && onlineManager.IsCurrentLobbyPlayer(participantIds[lane]);
        }

        private int GetScoredPlayerCount()
        {
            int count = 0;
            for (int i = 0; i < MaxLanes; i++)
                if (IsScoredParticipant(i)) count++;
            return Mathf.Max(1, count);
        }

        private int FindParticipant(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < participantIds.Length; i++)
                if (participantIds[i] == id) return i;
            return -1;
        }

        private void ResetFinishTimes()
        {
            for (int i = 0; i < finishTimes.Length; i++) finishTimes[i] = -1f;
        }

        private static void CopyArray<T>(T[] source, T[] destination, T fallback)
        {
            for (int i = 0; i < destination.Length; i++)
                destination[i] = source != null && i < source.Length ? source[i] : fallback;
        }

        private static float GetLaneTop(int lane) => FirstLaneTop - lane * LaneSpacing;

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return minutes + ":" + remainder.ToString("00.0");
        }

        private void CreateMonitorTexts(Transform parent)
        {
            Color graphite = new Color(0.08f, 0.09f, 0.1f);
            monitorTexts.Add(StageEscortController.CreateText(
                parent, "Race Total", new Vector3(0f, 0.34f, -0.08f),
                60, 0.075f, graphite, 7));
            monitorSubTexts.Add(StageEscortController.CreateText(
                parent, "Player Times", new Vector3(0f, -0.42f, -0.08f),
                48, 0.052f, graphite, 7));
        }
    }
}
