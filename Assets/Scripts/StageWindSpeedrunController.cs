using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageWindSpeedrunController : MonoBehaviour
    {
        private const string StageId = "14-1";
        private const string StateKind = "wind_speedrun_state";
        private const float StartDelay = 3f;
        private const float GustSeconds = 6f;
        private const float MaximumInk = 350f;
        private const float TimeLimitSeconds = 30f;

        [System.Serializable]
        private sealed class WindState
        {
            public int Sequence;
            public float Elapsed;
            public int Direction;
            public bool Failed;
            public float RetryRemaining;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private StageHorizontalWindVisual visual;
        private TextMesh signTitle;
        private TextMesh signHint;
        private readonly List<TextMesh> timerDisplays = new List<TextMesh>();
        private float elapsed;
        private float nextBroadcastAt;
        private float nextPlayerRefreshAt;
        private int sequence;
        private int receivedSequence;
        private int lastDirection;
        private bool controlsReleased;
        private bool failed;
        private float retryRemaining;
        private PlayerController2D[] players = System.Array.Empty<PlayerController2D>();

        private bool HasAuthority => stageManager == null
            || !stageManager.IsOnlineStageActive
            || stageManager.IsOnlineStageHost;

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
            SetLocalControls(true);
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            visual = StageHorizontalWindVisual.Create(transform);
            BuildTimeMonitors();
            RefreshPlayers();
            SetLocalControls(false);
            RefreshPresentation();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (failed)
            {
                retryRemaining = Mathf.Max(0f, retryRemaining - Time.deltaTime);
                RefreshPresentation();
                if (HasAuthority)
                {
                    BroadcastState();
                    if (retryRemaining <= 0f) stageManager.Retry();
                }
                return;
            }
            elapsed += Time.deltaTime;
            if (HasAuthority && elapsed >= StartDelay + TimeLimitSeconds) BeginFailure();
            RefreshPresentation();
            if (HasAuthority) BroadcastState();
        }

        private void FixedUpdate()
        {
            if (failed || elapsed < StartDelay || stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (Time.unscaledTime >= nextPlayerRefreshAt) RefreshPlayers();

            int direction = GetDirection();
            float gust = GetGustStrength();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy || !player.ControlsEnabled) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;

                PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
                float ink = ability != null ? Mathf.Max(0f, ability.CurrentProfile.TotalInk) : MaximumInk;
                float inkRatio = Mathf.Clamp01(ink / MaximumInk);
                float influence = Mathf.Lerp(1.85f, 0.48f, inkRatio);
                float opposingMultiplier = body.linearVelocity.x * direction < -0.25f ? 1.28f : 1f;
                float airborneMultiplier = player.IsGrounded ? 1f : 1.18f;
                float acceleration = 34f * influence * gust * opposingMultiplier * airborneMultiplier;
                Vector2 velocity = body.linearVelocity;
                velocity.x = Mathf.Clamp(velocity.x + direction * acceleration * Time.fixedDeltaTime, -36f, 36f);
                body.linearVelocity = velocity;
            }
        }

        private int GetDirection()
        {
            if (elapsed < StartDelay) return 0;
            int index = Mathf.FloorToInt((elapsed - StartDelay) / GustSeconds);
            return (index & 1) == 0 ? 1 : -1;
        }

        private float GetGustStrength()
        {
            float cycle = Mathf.Repeat(elapsed - StartDelay, GustSeconds);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cycle / 0.55f));
            float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((GustSeconds - cycle) / 0.35f));
            return Mathf.Min(fadeIn, fadeOut);
        }

        private void RefreshPresentation()
        {
            if (failed)
            {
                uiManager?.SetChallengeCountdown(true, LocalizationManager.T("wind_speedrun_time_up"));
                visual?.SetWind(0, 0f);
                RefreshTimerDisplays(0f, true);
                return;
            }
            float remaining = StartDelay - elapsed;
            if (!controlsReleased && remaining <= 0f)
            {
                controlsReleased = true;
                SetLocalControls(true);
            }

            string countdown = remaining > 0f
                ? Mathf.CeilToInt(remaining).ToString()
                : elapsed < StartDelay + 0.65f ? LocalizationManager.T("survival_start") : string.Empty;
            uiManager?.SetChallengeCountdown(!string.IsNullOrEmpty(countdown), countdown);

            int direction = GetDirection();
            visual?.SetWind(direction, direction == 0 ? 0f : GetGustStrength());
            RefreshTimerDisplays(Mathf.Max(0f, TimeLimitSeconds - Mathf.Max(0f, elapsed - StartDelay)), false);
            if (signTitle != null) signTitle.text = LocalizationManager.T("wind_speedrun_title");
            if (signHint != null) signHint.text = LocalizationManager.T("wind_speedrun_hint");
            if (direction != 0 && direction != lastDirection)
            {
                lastDirection = direction;
                GameSfx.Play(SfxId.UiToggleOn, 0.82f);
            }
        }

        private void RefreshPlayers()
        {
            players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            nextPlayerRefreshAt = Time.unscaledTime + 0.35f;
        }

        private void BuildStartSign()
        {
            GameObject board = new GameObject("14-1 Wind Rule Sign");
            board.transform.SetParent(transform, false);
            board.transform.position = new Vector3(-25f, 3.4f, 0.18f);
            StageEscortController.AddFilledRect(board.transform, "Paper", Vector2.zero,
                new Vector2(11.2f, 2.25f), new Color(1f, 0.94f, 0.68f, 0.94f), 24);
            signTitle = StageEscortController.CreateText(board.transform, "Title",
                new Vector3(0f, 0.38f, -0.04f), 46, 0.075f, new Color(0.05f, 0.38f, 0.72f), 27);
            signHint = StageEscortController.CreateText(board.transform, "Hint",
                new Vector3(0f, -0.48f, -0.04f), 42, 0.062f, new Color(0.18f, 0.16f, 0.12f), 27);
        }

        private void BuildTimeMonitors()
        {
            const float firstMonitorX = -25f;
            const float monitorSpacing = 42f;
            const int monitorCount = 6;
            for (int i = 0; i < monitorCount; i++)
            {
                GameObject board = new GameObject("14-1 Time Monitor " + (i + 1));
                board.transform.SetParent(transform, false);
                board.transform.position = new Vector3(firstMonitorX + monitorSpacing * i, -7.9f, 0.22f);
                DoodleMonitorVisuals.Build(board.transform, new Vector2(8.4f, 2.0f), 22);
                TextMesh display = StageEscortController.CreateText(board.transform, "Time",
                    new Vector3(0f, -0.03f, -0.04f), 58, 0.13f, new Color(0.04f, 0.43f, 0.58f), 28);
                timerDisplays.Add(display);
            }
            RefreshTimerDisplays(TimeLimitSeconds, false);
        }

        private void RefreshTimerDisplays(float seconds, bool timeUp)
        {
            string value = timeUp
                ? LocalizationManager.T("wind_speedrun_time_up")
                : LocalizationManager.Format("wind_speedrun_timer", seconds);
            for (int i = 0; i < timerDisplays.Count; i++)
                if (timerDisplays[i] != null) timerDisplays[i].text = value;
        }

        private void BeginFailure()
        {
            if (failed) return;
            failed = true;
            retryRemaining = 3f;
            SetLocalControls(false);
            GameSfx.Play(SfxId.PlayerHit);
            BroadcastState(true);
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            if (!stageManager.IsOnlineStageActive)
                stageManager.RemotePlayerController?.SetControlsEnabled(enabled);
        }

        private void BroadcastState(bool force = false)
        {
            if (onlineManager == null || stageManager == null || !stageManager.IsOnlineStageActive || !HasAuthority
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.15f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new WindState
                {
                    Sequence = ++sequence,
                    Elapsed = elapsed,
                    Direction = GetDirection(),
                    Failed = failed,
                    RetryRemaining = retryRemaining
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId || message.Kind != StateKind
                || HasAuthority || !IsHost(message.PlayerId)) return;
            WindState state = JsonUtility.FromJson<WindState>(message.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;
            if (state.Failed && !failed)
            {
                failed = true;
                SetLocalControls(false);
            }
            failed = state.Failed;
            retryRemaining = state.RetryRemaining;
            // A short correction keeps the local wind smooth while preventing
            // different clients from spending a gust in opposite directions.
            if (state.Direction != GetDirection()) elapsed = state.Elapsed;
            else elapsed = Mathf.Lerp(elapsed, state.Elapsed, 0.35f);
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null) return false;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].IsHost && lobbyPlayers[i].PlayerId == playerId) return true;
            return false;
        }
    }

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

            if (!stageManager.IsDrawingMode)
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            if (HasAuthority && remaining <= 0f)
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

    public sealed class StageHorizontalWindVisual : MonoBehaviour
    {
        private const int RibbonCount = 11;
        private readonly LineRenderer[] ribbons = new LineRenderer[RibbonCount];
        private Camera targetCamera;
        private TextMesh directionText;
        private int direction;
        private float strength;

        public static StageHorizontalWindVisual Create(Transform parent)
        {
            GameObject root = new GameObject("14-1 Alternating Wind Visual");
            root.transform.SetParent(parent, false);
            StageHorizontalWindVisual visual = root.AddComponent<StageHorizontalWindVisual>();
            visual.Build();
            return visual;
        }

        public void SetWind(int value, float amount)
        {
            direction = Mathf.Clamp(value, -1, 1);
            strength = Mathf.Clamp01(amount);
            if (directionText != null)
            {
                directionText.text = direction > 0 ? "\u2192  \u2192  \u2192" : direction < 0 ? "\u2190  \u2190  \u2190" : string.Empty;
                directionText.color = new Color(0.08f, 0.55f, 0.9f, Mathf.Lerp(0.25f, 0.9f, strength));
            }
        }

        private void Build()
        {
            targetCamera = Camera.main;
            Material material = DoodleRuntimeAssets.LineMaterial;
            for (int i = 0; i < ribbons.Length; i++)
            {
                GameObject ribbonObject = new GameObject("Wind Ribbon " + i);
                ribbonObject.transform.SetParent(transform, false);
                LineRenderer line = ribbonObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 5;
                line.startWidth = 0.055f;
                line.endWidth = 0.035f;
                line.numCapVertices = 3;
                line.sharedMaterial = material;
                line.sortingOrder = 35;
                ribbons[i] = line;
            }

            GameObject label = new GameObject("Wind Direction Display");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 4.15f, -0.12f);
            directionText = label.AddComponent<TextMesh>();
            directionText.anchor = TextAnchor.MiddleCenter;
            directionText.alignment = TextAlignment.Center;
            directionText.fontSize = 64;
            directionText.characterSize = 0.095f;
            label.GetComponent<MeshRenderer>().sortingOrder = 52;
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
                transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 0f);
            float alpha = Mathf.Lerp(0.06f, 0.42f, strength);
            Color color = new Color(0.12f, 0.65f, 0.95f, direction == 0 ? 0f : alpha);
            for (int i = 0; i < ribbons.Length; i++)
            {
                LineRenderer line = ribbons[i];
                if (line == null) continue;
                float travel = Mathf.Repeat(Time.time * Mathf.Lerp(5f, 14f, strength) + i * 2.37f, 25f) - 12.5f;
                float x = direction >= 0 ? travel : -travel;
                float y = Mathf.Lerp(-5.2f, 5.1f, i / (RibbonCount - 1f)) + Mathf.Sin(Time.time * 1.4f + i) * 0.18f;
                float length = Mathf.Lerp(1.1f, 2.4f, strength);
                Vector2 tip = new Vector2(direction * length, 0f);
                Vector2 neck = new Vector2(direction * (length - 0.38f), 0f);
                line.SetPosition(0, new Vector3(x, y, -0.08f));
                line.SetPosition(1, new Vector3(x + tip.x, y, -0.08f));
                line.SetPosition(2, new Vector3(x + neck.x, y + 0.18f, -0.08f));
                line.SetPosition(3, new Vector3(x + tip.x, y, -0.08f));
                line.SetPosition(4, new Vector3(x + neck.x, y - 0.18f, -0.08f));
                line.startColor = color;
                line.endColor = color;
            }
        }
    }
}
