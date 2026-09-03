using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageCoinDescentController : StageEliminationChallengeController
    {
        private const string StageId = "9-2";
        private const string StateKind = "coin_descent_state";
        private const string EliminateRequestKind = "coin_descent_eliminate_request";
        private const string EliminatedKind = "coin_descent_eliminated";
        private const float FloorDelay = 3f;
        private const float BottomY = -159f;

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public float FloorRemaining;
            public bool FloorGone;
            public float RestartRemaining;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private Transform startFloor;
        private Collider2D[] floorColliders;
        private SpriteRenderer[] floorSprites;
        private Color[] floorColors;
        private float floorRemaining = FloorDelay;
        private float restartRemaining;
        private float nextStateAt;
        private int stateSequence;
        private int lastStateSequence;
        private bool floorGone;
        private bool retrying;
        private bool gameOverShown;
        private bool restored;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
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
            uiManager?.SetChallengeCountdown(false, string.Empty);
            RestorePlayers();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            FindStartFloor();
            CaptureParticipants();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId || !stageManager.IsGameplayActive) return;
            if (!floorGone) KeepLocalPlayersMovable();
            ApplyPendingEliminations();
            CheckFalls();

            if (!HasAuthority())
            {
                if (!floorGone)
                {
                    floorRemaining = Mathf.Max(0f, floorRemaining - Time.deltaTime);
                    RefreshFloorVisual();
                    if (floorRemaining <= 0f) HideFloor();
                }
                return;
            }

            if (!floorGone)
            {
                floorRemaining = Mathf.Max(0f, floorRemaining - Time.deltaTime);
                RefreshFloorVisual();
                if (floorRemaining <= 0f)
                {
                    HideFloor();
                    GameSfx.Play(SfxId.CrumblingFloorCollapse);
                    BroadcastState(true);
                }
            }

            if (!retrying && AreAllEliminated())
            {
                retrying = true;
                restartRemaining = 3f;
                BroadcastState(true);
            }
            if (retrying)
            {
                ShowGameOver();
                restartRemaining = Mathf.Max(0f, restartRemaining - Time.deltaTime);
                if (restartRemaining <= 0f) stageManager.Retry();
            }
            BroadcastState();
        }

        private void KeepLocalPlayersMovable()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform localPlayer = stageManager.ActivePlayerTransform;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || IsEliminated(player)) continue;
                if (IsOnline() && player.transform != localPlayer) continue;
                player.SetControlsEnabled(true);
            }
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || retrying) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (!IsOnline()) participants.Add(id);
            if (IsOnline() && !HasAuthority())
            {
                if (id != onlineManager?.LocalPlayerId) return;
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId, Kind = EliminateRequestKind,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
                ApplyElimination(id);
                return;
            }
            ConfirmElimination(id, IsOnline());
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId, Kind = EliminatedKind,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void FindStartFloor()
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(false);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].objectId != "9-2_start_floor") continue;
                startFloor = markers[i].transform;
                break;
            }
            if (startFloor == null) return;
            floorColliders = startFloor.GetComponentsInChildren<Collider2D>(true);
            floorSprites = startFloor.GetComponentsInChildren<SpriteRenderer>(true);
            floorColors = new Color[floorSprites.Length];
            for (int i = 0; i < floorSprites.Length; i++)
                floorColors[i] = floorSprites[i] != null ? floorSprites[i].color : Color.white;
        }

        private void RefreshFloorVisual()
        {
            if (floorSprites == null) return;
            float urgency = 1f - Mathf.Clamp01(floorRemaining / FloorDelay);
            float pulse = 0.45f + Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(4f, 15f, urgency))) * 0.4f;
            for (int i = 0; i < floorSprites.Length; i++)
            {
                if (floorSprites[i] == null) continue;
                Color original = floorColors != null && i < floorColors.Length ? floorColors[i] : Color.white;
                floorSprites[i].color = Color.Lerp(original, new Color(1f, 0.18f, 0.08f, original.a), urgency * pulse);
            }
        }

        private void HideFloor()
        {
            if (floorGone) return;
            floorGone = true;
            if (floorColliders != null)
                for (int i = 0; i < floorColliders.Length; i++) if (floorColliders[i] != null) floorColliders[i].enabled = false;
            if (startFloor != null) startFloor.gameObject.SetActive(false);
        }

        private void CheckFalls()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && (players[i].transform.position.y < BottomY || Mathf.Abs(players[i].transform.position.x) > 11f))
                    RequestElimination(players[i]);
        }

        private void CaptureParticipants()
        {
            if (IsOnline())
            {
                OnlinePlayerInfo[] list = onlineManager?.CurrentLobby?.Players;
                if (list != null)
                    for (int i = 0; i < list.Length; i++)
                        if (list[i] != null && !string.IsNullOrEmpty(list[i].PlayerId)) participants.Add(list[i].PlayerId);
                return;
            }
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) participants.Add(ResolvePlayerId(players[i]));
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

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminated.Contains(id);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyState(JsonUtility.FromJson<NetworkState>(data.Json));
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
            nextStateAt = Time.unscaledTime + 0.2f;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence,
                FloorRemaining = floorRemaining,
                FloorGone = floorGone,
                RestartRemaining = restartRemaining,
                EliminatedIds = new List<string>(eliminated).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = StateKind, Json = JsonUtility.ToJson(state) });
        }

        private void ApplyState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            floorRemaining = state.FloorRemaining;
            restartRemaining = state.RestartRemaining;
            retrying = restartRemaining > 0f;
            if (retrying) ShowGameOver();
            if (state.FloorGone) HideFloor();
            if (state.EliminatedIds != null)
                for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
        }

        private void ShowGameOver()
        {
            if (gameOverShown) return;
            gameOverShown = true;
            if (uiManager == null) uiManager = Object.FindFirstObjectByType<UIManager>();
            uiManager?.SetChallengeCountdown(true, LocalizationManager.T("game_over"));
        }

        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : IsOnline()
            ? stageManager.GetOnlinePlayerId(player)
            : "local_" + player.GetInstanceID();

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline()) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnline() || stageManager.IsOnlineStageHost;
        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }
    }
}
