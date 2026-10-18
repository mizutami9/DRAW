using UnityEngine;

namespace DrawBody.Prototype
{
    // The former 14-1 wind controller was removed when the stage became the
    // icicle seesaw survival challenge. This shared timer is still used by 7-3.
    [DisallowMultipleComponent]
    public sealed class StageTimedGoalController : MonoBehaviour
    {
        private const string StateKind = "timed_goal_state";
        private const float RetryDelay = 2.5f;

        [System.Serializable]
        private sealed class TimerState
        {
            public int Sequence;
            public float Remaining;
            public bool Started;
            public bool Failed;
            public float RetryRemaining;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private string stageId;
        private float duration = 180f;
        private float remaining = 180f;
        private float retryRemaining;
        private float nextBroadcastAt;
        private int sequence;
        private int receivedSequence;
        private bool started;
        private bool failed;
        private GameObject canvasObject;
        private TextMesh timerText;

        private bool HasAuthority => stageManager == null
            || !stageManager.IsOnlineStageActive
            || stageManager.IsOnlineStageHost;

        public void Configure(string id, float seconds)
        {
            stageId = id;
            duration = Mathf.Clamp(seconds, 5f, 1800f);
            remaining = duration;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (canvasObject != null) Destroy(canvasObject);
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildTimerHud();
            RefreshHud();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != stageId) return;
            if (!started)
            {
                if (!stageManager.IsGameplayActive) return;
                started = true;
                BroadcastState(true);
            }

            if (failed)
            {
                retryRemaining = Mathf.Max(0f, retryRemaining - Time.unscaledDeltaTime);
                RefreshHud();
                if (HasAuthority)
                {
                    BroadcastState();
                    if (retryRemaining <= 0f) stageManager.Retry();
                }
                return;
            }

            if (!stageManager.IsDrawingMode && !LocalMultiplayerDebugMode.NoTimeLimit)
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            if (HasAuthority && !LocalMultiplayerDebugMode.NoTimeLimit && remaining <= 0f)
            {
                failed = true;
                retryRemaining = RetryDelay;
                GameSfx.Play(SfxId.PlayerHit);
                BroadcastState(true);
            }
            RefreshHud();
            if (HasAuthority) BroadcastState();
        }

        private void BuildTimerHud()
        {
            canvasObject = new GameObject("7-3 Countdown Monitor");
            Camera camera = Camera.main;
            if (camera != null)
            {
                canvasObject.transform.SetParent(camera.transform, false);
                canvasObject.transform.localPosition = new Vector3(
                    0f,
                    Mathf.Max(2.8f, camera.orthographicSize - 1.45f),
                    -camera.transform.position.z);
            }
            else
            {
                canvasObject.transform.SetParent(transform, false);
                canvasObject.transform.position = new Vector3(0f, 8f, 0f);
            }

            DoodleMonitorVisuals.Build(canvasObject.transform, new Vector2(5.4f, 1.55f), -32);
            timerText = StageEscortController.CreateText(
                canvasObject.transform,
                "Remaining Time",
                new Vector3(0f, 0.02f, -0.03f),
                72,
                0.14f,
                new Color(0.04f, 0.31f, 0.5f, 1f),
                -28);
            timerText.fontStyle = FontStyle.Bold;
            DoodleMonitorVisuals.KeepBehindPlayers(canvasObject.transform);
        }

        private void RefreshHud()
        {
            if (timerText == null) return;
            if (failed)
            {
                timerText.text = LocalizationManager.T("challenge_time_up");
                timerText.color = new Color(0.85f, 0.08f, 0.08f, 1f);
                return;
            }
            int totalSeconds = Mathf.CeilToInt(remaining);
            timerText.text = (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }

        private void BroadcastState(bool force = false)
        {
            if (onlineManager == null || stageManager == null || !stageManager.IsOnlineStageActive || !HasAuthority
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.2f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new TimerState
                {
                    Sequence = ++sequence,
                    Remaining = remaining,
                    Started = started,
                    Failed = failed,
                    RetryRemaining = retryRemaining
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != stageId || message.Kind != StateKind || HasAuthority
                || !IsHost(message.PlayerId)) return;
            TimerState state = JsonUtility.FromJson<TimerState>(message.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;
            started = state.Started;
            failed = state.Failed;
            retryRemaining = state.RetryRemaining;
            remaining = Mathf.Abs(remaining - state.Remaining) > 1f
                ? state.Remaining
                : Mathf.Lerp(remaining, state.Remaining, 0.35f);
            RefreshHud();
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            return false;
        }
    }
}
