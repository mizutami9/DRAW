using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSlimeMissileSurvivalController : StageEliminationChallengeController
    {
        private const string StageId = "9-1";
        private const string StateKind = "slime_missile_state";
        private const string MissileKind = "slime_missile_launch";
        private const string EliminateRequestKind = "slime_missile_eliminate_request";
        private const string EliminatedKind = "slime_missile_eliminated";
        private const float PreparationSeconds = 7f;
        private const float BottomY = -41f;

        private enum Phase { Preparation, Playing, Finished, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Phase;
            public float Remaining;
            public float Preparation;
            public float Restart;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class MissileState
        {
            public int Sequence;
            public Vector2 Position;
            public Vector2 Direction;
            public float Speed;
            public float Size;
            public bool Homing;
            public string TargetPlayerId;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly HashSet<int> receivedMissiles = new HashSet<int>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly List<TextMesh> sideTimers = new List<TextMesh>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Camera gameCamera;
        private Transform startFloor;
        private Collider2D[] startFloorColliders;
        private SpriteRenderer[] startFloorRenderers;
        private Color[] startFloorColors;
        private Transform hud;
        private TextMesh hudTitle;
        private TextMesh hudTimer;
        private TextMesh hudSub;
        private Phase phase = Phase.Preparation;
        private float duration = 60f;
        private float remaining = 60f;
        private float preparation = PreparationSeconds;
        private float elapsed;
        private float nextVolleyAt;
        private float nextStateAt;
        private float nextHomingAt = float.PositiveInfinity;
        private float restartRemaining;
        private int missileSequence;
        private int stateSequence;
        private int lastStateSequence;
        private bool restored;

        public void Configure(float seconds)
        {
            duration = Mathf.Clamp(seconds > 0f ? seconds : 60f, 30f, 180f);
            remaining = duration;
        }

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
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            FindStartFloor();
            BuildSideMonitors();
            CaptureParticipants();
            SetLocalControls(true);
            nextVolleyAt = Time.time + PreparationSeconds + 1.4f;
            RefreshHud();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            ApplyPendingEliminations();
            CheckOutOfBounds();

            if (!HasAuthority())
            {
                if (phase == Phase.Preparation) preparation = Mathf.Max(0f, preparation - Time.deltaTime);
                else if (phase == Phase.Playing) remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                RefreshStartFloorVisual();
                RefreshHud();
                return;
            }

            if (phase == Phase.Preparation)
            {
                preparation -= Time.deltaTime;
                RefreshStartFloorVisual();
                if (preparation <= 0f)
                {
                    phase = Phase.Playing;
                    HideStartFloor();
                    BroadcastState(true);
                    GameSfx.Play(SfxId.CrumblingFloorCollapse);
                }
            }
            else if (phase == Phase.Playing)
            {
                elapsed += Time.deltaTime;
                remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                if (Time.time >= nextVolleyAt) FireVolley();
                if (remaining <= 20f)
                {
                    if (float.IsPositiveInfinity(nextHomingAt)) nextHomingAt = Time.time;
                    if (Time.time >= nextHomingAt) FireHomingRocket();
                }
                if (AreAllEliminated()) BeginFailure();
                else if (remaining <= 0f)
                {
                    phase = Phase.Finished;
                    SetLocalControls(false);
                    BroadcastState(true);
                    stageManager.ClearStage();
                }
            }
            else if (phase == Phase.Failed)
            {
                restartRemaining -= Time.deltaTime;
                if (restartRemaining <= 0f) stageManager.Retry();
            }

            BroadcastState();
            RefreshHud();
        }

        private void FireVolley()
        {
            float progress = 1f - remaining / Mathf.Max(1f, duration);
            List<PlayerController2D> targets = GetLivingPlayers();
            if (targets.Count == 0) return;
            // The authored volley is the four-player baseline. Reduce the number of
            // missiles for smaller parties without changing the late-stage ramp.
            int participantCount = Mathf.Clamp(
                stageManager != null ? stageManager.GetInkBudgetPlayerCount() : targets.Count,
                1,
                4);
            int fourPlayerCount = Mathf.Clamp(Mathf.Max(4, 1 + Mathf.FloorToInt(progress * 4.2f)), 1, 8);
            int count = Mathf.Max(1, Mathf.CeilToInt(fourPlayerCount * participantCount / 4f));
            int volleyStart = missileSequence;
            int targetStart = volleyStart % targets.Count;
            for (int i = 0; i < count; i++)
            {
                int side = (volleyStart + i) % 4;
                PlayerController2D targetPlayer = targets[(targetStart + i) % targets.Count];
                Vector2 target = (Vector2)targetPlayer.transform.position
                    + Random.insideUnitCircle * Mathf.Lerp(1.35f, 0.45f, progress);
                Vector2 position = GetOutsideSpawn(side, target);
                MissileState state = new MissileState
                {
                    Sequence = ++missileSequence,
                    Position = position,
                    Direction = (target - position).normalized,
                    Speed = Mathf.Lerp(5.2f, 10.5f, progress),
                    Size = Random.Range(Mathf.Lerp(0.68f, 1.15f, progress), Mathf.Lerp(0.9f, 2.25f, progress)),
                    TargetPlayerId = ResolvePlayerId(targetPlayer)
                };
                ApplyMissile(state);
                if (IsOnline() && onlineManager != null)
                    onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = MissileKind, Json = JsonUtility.ToJson(state) });
            }
            nextVolleyAt = Time.time + Mathf.Lerp(3.1f, 0.72f, progress);
        }

        private void FireHomingRocket()
        {
            List<PlayerController2D> targets = GetLivingPlayers();
            if (targets.Count == 0) return;
            int side = Random.Range(0, 4);
            Vector2 probe = GetOutsideSpawn(side, GetLivingPlayerCenter());
            PlayerController2D target = targets[0];
            float nearest = Vector2.SqrMagnitude((Vector2)target.transform.position - probe);
            for (int i = 1; i < targets.Count; i++)
            {
                float distance = Vector2.SqrMagnitude((Vector2)targets[i].transform.position - probe);
                if (distance >= nearest) continue;
                nearest = distance;
                target = targets[i];
            }
            Vector2 targetPosition = target.transform.position;
            Vector2 position = probe;
            MissileState state = new MissileState
            {
                Sequence = ++missileSequence,
                Position = position,
                Direction = (targetPosition - position).normalized,
                Speed = 4.1f,
                Size = Random.Range(1.0f, 1.45f),
                Homing = true,
                TargetPlayerId = ResolvePlayerId(target)
            };
            ApplyMissile(state);
            if (IsOnline() && onlineManager != null)
                onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = MissileKind, Json = JsonUtility.ToJson(state) });
            nextHomingAt = Time.time + 5f;
        }

        private static Vector2 GetOutsideSpawn(int side, Vector2 target)
        {
            if (side == 0) return new Vector2(-10.8f, Mathf.Clamp(target.y + Random.Range(-3.5f, 3.5f), -39.2f, -3.1f));
            if (side == 1) return new Vector2(10.8f, Mathf.Clamp(target.y + Random.Range(-3.5f, 3.5f), -39.2f, -3.1f));
            if (side == 2) return new Vector2(Mathf.Clamp(target.x + Random.Range(-5f, 5f), -8.6f, 8.6f), 2.2f);
            return new Vector2(Mathf.Clamp(target.x + Random.Range(-5f, 5f), -8.6f, 8.6f), -41.2f);
        }

        private void ApplyMissile(MissileState state)
        {
            if (state == null || !receivedMissiles.Add(state.Sequence)) return;
            Transform trackingTarget = null;
            if (state.Homing && !string.IsNullOrEmpty(state.TargetPlayerId))
            {
                PlayerController2D player = ResolvePlayer(state.TargetPlayerId);
                if (player != null) trackingTarget = player.transform;
            }
            StageMissileProjectile.Create(
                transform, transform, state.Position, state.Direction, state.Speed, true, state.Size,
                trackingTarget, state.Homing ? 95f : 0f);
            GameSfx.PlayAt(SfxId.CannonFire, state.Position, 0.78f);
        }

        private List<PlayerController2D> GetLivingPlayers()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            List<PlayerController2D> living = new List<PlayerController2D>(players.Length);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && !IsEliminated(players[i])) living.Add(players[i]);
            return living;
        }

        private Vector2 GetLivingPlayerCenter()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || IsEliminated(players[i])) continue;
                sum += (Vector2)players[i].transform.position;
                count++;
            }
            return count > 0 ? sum / count : new Vector2(0f, -20f);
        }

        private void FindStartFloor()
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(false);
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].objectId == "9-1_start_floor") { startFloor = markers[i].transform; break; }
            if (startFloor == null) return;
            startFloorColliders = startFloor.GetComponentsInChildren<Collider2D>(true);
            startFloorRenderers = startFloor.GetComponentsInChildren<SpriteRenderer>(true);
            startFloorColors = new Color[startFloorRenderers.Length];
            for (int i = 0; i < startFloorRenderers.Length; i++)
                startFloorColors[i] = startFloorRenderers[i] != null ? startFloorRenderers[i].color : Color.white;
        }

        private void RefreshStartFloorVisual()
        {
            if (startFloorRenderers == null) return;
            float urgency = 1f - Mathf.Clamp01(preparation / PreparationSeconds);
            float pulse = 0.5f + Mathf.Sin(Time.time * Mathf.Lerp(5f, 16f, urgency)) * 0.25f;
            for (int i = 0; i < startFloorRenderers.Length; i++)
                if (startFloorRenderers[i] != null)
                {
                    Color original = startFloorColors != null && i < startFloorColors.Length
                        ? startFloorColors[i]
                        : Color.white;
                    startFloorRenderers[i].color = Color.Lerp(original, new Color(1f, 0.2f, 0.12f, original.a), urgency * pulse);
                }
        }

        private void HideStartFloor()
        {
            if (startFloorColliders != null) for (int i = 0; i < startFloorColliders.Length; i++) if (startFloorColliders[i] != null) startFloorColliders[i].enabled = false;
            if (startFloorRenderers != null) for (int i = 0; i < startFloorRenderers.Length; i++) if (startFloorRenderers[i] != null) startFloorRenderers[i].enabled = false;
            if (startFloor != null) startFloor.gameObject.SetActive(false);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != Phase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (!IsOnline()) participants.Add(id);
            if (IsOnline() && !HasAuthority())
            {
                if (id != onlineManager?.LocalPlayerId) return;
                onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = EliminateRequestKind, Json = JsonUtility.ToJson(new EliminationState { PlayerId = id }) });
                ApplyElimination(id);
                return;
            }
            ConfirmElimination(id, IsOnline());
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
                onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = EliminatedKind, Json = JsonUtility.ToJson(new EliminationState { PlayerId = id }) });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void CheckOutOfBounds()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && (players[i].transform.position.y < BottomY || Mathf.Abs(players[i].transform.position.x) > 10f))
                    RequestElimination(players[i]);
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed) return;
            phase = Phase.Failed;
            restartRemaining = 3f;
            SetLocalControls(false);
            BroadcastState(true);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority()) ApplyState(JsonUtility.FromJson<NetworkState>(data.Json));
            else if (data.Kind == MissileKind && IsHostPlayer(data.PlayerId) && !HasAuthority()) ApplyMissile(JsonUtility.FromJson<MissileState>(data.Json));
            else if (data.Kind == EliminateRequestKind && HasAuthority())
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

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline() || !HasAuthority() || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence, Phase = (int)phase, Remaining = remaining,
                Preparation = preparation, Restart = restartRemaining,
                EliminatedIds = new List<string>(eliminated).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = StateKind, Json = JsonUtility.ToJson(state) });
        }

        private void ApplyState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            Phase old = phase;
            phase = (Phase)Mathf.Clamp(state.Phase, 0, (int)Phase.Failed);
            remaining = state.Remaining;
            preparation = state.Preparation;
            restartRemaining = state.Restart;
            if (state.EliminatedIds != null) for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            if (old == Phase.Preparation && phase != Phase.Preparation) HideStartFloor();
            SetLocalControls(phase == Phase.Preparation || phase == Phase.Playing);
        }

        private void BuildHud()
        {
            GameObject root = new GameObject("9-1 Missile Survival HUD");
            root.transform.SetParent(transform, false);
            hud = root.transform;
            DoodleMonitorVisuals.Build(hud, new Vector2(10.5f, 2.1f), 278);
            hudTimer = StagePillarSurvivalController.CreateText(hud, "Timer", new Vector3(0f, -0.03f, -0.03f), 64, 0.17f, new Color(0.04f, 0.43f, 0.58f, 1f), 284);
        }

        private void BuildSideMonitors()
        {
            float[] heights = { -8f, -18f, -28f, -37f };
            for (int y = 0; y < heights.Length; y++)
            {
                CreateSideMonitor(-10.65f, heights[y]);
                CreateSideMonitor(10.65f, heights[y]);
            }
        }

        private void CreateSideMonitor(float x, float y)
        {
            GameObject monitor = new GameObject("9-1 Side Timer");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(x, y, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(2.35f, 1.45f), 14);
            TextMesh timer = StagePillarSurvivalController.CreateText(monitor.transform, "Timer", new Vector3(0f, -0.02f, -0.03f), 42, 0.075f, new Color(0.04f, 0.43f, 0.58f, 1f), 20);
            sideTimers.Add(timer);
        }

        private void RefreshHud()
        {
            if (hudTimer != null && phase == Phase.Preparation)
            {
                hudTimer.text = Mathf.Max(1, Mathf.CeilToInt(preparation)).ToString();
            }
            else if (hudTimer != null && phase == Phase.Failed)
            {
                hudTimer.text = LocalizationManager.T("game_over");
            }
            else if (hudTimer != null && phase == Phase.Finished)
            {
                hudTimer.text = LocalizationManager.T("clear");
            }
            else if (hudTimer != null)
            {
                int minutes = Mathf.FloorToInt(remaining / 60f);
                float seconds = remaining - minutes * 60f;
                hudTimer.text = string.Format("{0:00}:{1:00.0}", minutes, seconds);
            }
            RefreshSideTimers();
        }

        private void RefreshSideTimers()
        {
            string value;
            if (phase == Phase.Preparation) value = Mathf.Max(1, Mathf.CeilToInt(preparation)).ToString();
            else if (phase == Phase.Failed) value = "NG";
            else if (phase == Phase.Finished) value = "OK";
            else value = remaining.ToString("00.0");
            for (int i = 0; i < sideTimers.Count; i++)
                if (sideTimers[i] != null) sideTimers[i].text = value;
        }

        private void CaptureParticipants()
        {
            if (IsOnline())
            {
                OnlinePlayerInfo[] list = onlineManager?.CurrentLobby?.Players;
                if (list != null) for (int i = 0; i < list.Length; i++) if (list[i] != null && !string.IsNullOrEmpty(list[i].PlayerId)) participants.Add(list[i].PlayerId);
            }
            else
            {
                PlayerController2D[] list = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < list.Length; i++) participants.Add(ResolvePlayerId(list[i]));
            }
        }

        private bool AreAllEliminated()
        {
            if (participants.Count == 0) return false;
            foreach (string id in participants) if (!eliminated.Contains(id)) return false;
            return true;
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
            if (restored) return;
            restored = true;
            for (int i = 0; i < hiddenPlayers.Count; i++) if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
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

        private void SetLocalControls(bool value)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(value && !stageManager.IsDrawingMode && !IsEliminated(active));
            if (!IsOnline()) stageManager.RemotePlayerController?.SetControlsEnabled(value && !IsEliminated(stageManager.RemotePlayerController));
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminated.Contains(id);
        }

        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : IsOnline() ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline()) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] list = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++) if (ResolvePlayerId(list[i]) == id) return list[i];
            return null;
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnline() || stageManager.IsOnlineStageHost;
        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] list = onlineManager?.CurrentLobby?.Players;
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++) if (list[i] != null && list[i].IsHost && list[i].PlayerId == id) return true;
            return false;
        }
    }
}
