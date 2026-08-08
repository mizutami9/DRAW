using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class MultiMenuController : MonoBehaviour
    {
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private GameObject choiceScreen;
        [SerializeField] private GameObject randomScreen;
        [SerializeField] private GameObject roomScreen;
        [SerializeField] private GameObject createRoomScreen;
        [SerializeField] private GameObject joinRoomScreen;
        [SerializeField] private GameObject lobbyScreen;
        [SerializeField] private Text randomStatusText;
        [SerializeField] private Text lobbyStatusText;
        [SerializeField] private Text lobbyNoticeText;
        [SerializeField] private Text createRoomStatusText;
        [SerializeField] private InputField joinAddressInput;
        [SerializeField] private Button startStageButton;
        [SerializeField] private Button copyLobbyIdButton;
        private MultiMenuVisualPolisher visualPolisher;
        private Button randomReadyButton;
        private Text randomReadyLabel;
        private Text randomSearchingLabel;
        private Text randomSearchingDotsLabel;
        private GameObject leaveConfirmPanel;
        private Text createRoomMaxPlayersText;
        private Text createRoomVisibilityText;
        private bool stageStartedFromOnline;
        private bool randomStartRequested;
        private int createRoomMaxPlayers = 4;
        private bool createRoomPrivate;
        private float randomMatchStartedAt = -1f;
        private float randomStatusRefreshTimer;
        private float lobbyNoticeTimer;

        private void OnEnable()
        {
            EnsureVisualPolisher();

            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (onlineManager != null)
            {
                onlineManager.StateChanged += RefreshOnlineText;
            }

            if (onlineManager != null && onlineManager.CurrentLobby != null && onlineManager.State == OnlineConnectionState.InLobby)
            {
                ShowLobby();
            }
            else
            {
                ShowChoice();
            }
        }

        private void EnsureVisualPolisher()
        {
            if (visualPolisher == null)
            {
                visualPolisher = GetComponent<MultiMenuVisualPolisher>();
                if (visualPolisher == null)
                {
                    visualPolisher = gameObject.AddComponent<MultiMenuVisualPolisher>();
                }
            }

            visualPolisher.Polish();
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged -= RefreshOnlineText;
            }
        }

        private void Update()
        {
            UpdateLobbyNotice();

            if (randomScreen == null || !randomScreen.activeInHierarchy || randomStatusText == null)
            {
                return;
            }

            randomStatusRefreshTimer -= Time.unscaledDeltaTime;
            if (randomStatusRefreshTimer > 0f)
            {
                return;
            }

            randomStatusRefreshTimer = 0.25f;
            UpdateRandomSearchHeader();
            randomStatusText.text = FormatRandomMatchStatus(onlineManager != null ? onlineManager.CurrentLobby : null, string.Empty, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
        }

        public void ShowChoice()
        {
            ShowOnly(choiceScreen);
        }

        public void ShowRandom()
        {
            randomStartRequested = false;
            randomMatchStartedAt = Time.unscaledTime;
            onlineManager?.StartRandomMatch();
            if (randomStatusText != null)
            {
                UpdateRandomSearchHeader();
                randomStatusText.text = FormatRandomMatchStatus(onlineManager != null ? onlineManager.CurrentLobby : null, string.Empty, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
            }

            ShowOnly(randomScreen);
            RefreshRandomReadyButton(onlineManager != null ? onlineManager.CurrentLobby : null, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
        }

        public void ShowRoom()
        {
            ShowOnly(roomScreen);
        }

        public void ShowCreateRoom()
        {
            ShowOnly(createRoomScreen);
            RefreshCreateRoomText();
        }

        public void ShowJoinRoom()
        {
            ShowOnly(joinRoomScreen);
        }

        public void CreateRoom()
        {
            onlineManager?.CreateRoom(LocalizationManager.T("multi_default_room_name"), createRoomMaxPlayers, createRoomPrivate);
            SetLobbyButtonState(false);
            ShowLobby();
        }

        public void ChangeCreateRoomMaxPlayers(int delta)
        {
            createRoomMaxPlayers = Mathf.Clamp(createRoomMaxPlayers + delta, 2, 4);
            RefreshCreateRoomText();
        }

        public void ToggleCreateRoomVisibility()
        {
            createRoomPrivate = !createRoomPrivate;
            RefreshCreateRoomText();
        }

        public void JoinRoom()
        {
            string roomId = joinAddressInput != null ? joinAddressInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                if (joinAddressInput != null)
                {
                    joinAddressInput.text = string.Empty;
                    Text placeholder = joinAddressInput.placeholder != null ? joinAddressInput.placeholder.GetComponent<Text>() : null;
                    if (placeholder != null)
                    {
                        placeholder.text = LocalizationManager.T("multi_lobby_id_placeholder");
                    }
                }
                return;
            }

            onlineManager?.JoinRoom(roomId);
            SetLobbyButtonState(false);
            ShowLobby();
        }

        public void ShowLobby()
        {
            ShowOnly(lobbyScreen);
            RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null, string.Empty);
        }

        public void ToggleReady()
        {
            onlineManager?.ToggleReady();
            RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null, string.Empty);
            TryAutoStartRandomMatch(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null);
        }

        public void StartStage()
        {
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            if (IsLocalHost(lobby))
            {
                if (!AreAllPlayersReady(lobby))
                {
                    ShowLobbyNotice(LocalizationManager.T("multi_all_ready_required"), 2.4f);
                    return;
                }

                stageManager?.OpenStageSelectFromMultiLobby();
            }
            else
            {
                RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, lobby, LocalizationManager.T("multi_host_only_start"));
            }
        }

        public void CopyLobbyId()
        {
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            string copyValue = lobby != null && !string.IsNullOrEmpty(lobby.RoomCode) ? lobby.RoomCode : lobby != null ? lobby.LobbyId : string.Empty;
            if (onlineManager == null || onlineManager.EffectiveBackendMode == OnlineBackendMode.Fake)
            {
                ShowLobbyNotice(LocalizationManager.T("multi_no_online_lobby_id"), 2.8f);
                return;
            }

            if (string.IsNullOrEmpty(copyValue))
            {
                RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null, LocalizationManager.T("multi_no_lobby_id"));
                return;
            }

            GUIUtility.systemCopyBuffer = copyValue;
            string noticeKey = onlineManager.EffectiveBackendMode == OnlineBackendMode.Eos
                ? "multi_copied_room_code"
                : "multi_copied_connection_id";
            ShowLobbyNotice(LocalizationManager.T(noticeKey), 2.2f);
            RefreshOnlineText(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
        }

        public void LeaveLobby()
        {
            ShowLeaveConfirm();
        }

        public void ConfirmLeaveLobby()
        {
            HideLeaveConfirm();
            onlineManager?.LeaveLobby();
            ShowChoice();
        }

        public void CancelLeaveLobby()
        {
            HideLeaveConfirm();
        }

        private void ShowOnly(GameObject activeScreen)
        {
            SetScreen(choiceScreen, activeScreen);
            SetScreen(randomScreen, activeScreen);
            SetScreen(roomScreen, activeScreen);
            SetScreen(createRoomScreen, activeScreen);
            SetScreen(joinRoomScreen, activeScreen);
            SetScreen(lobbyScreen, activeScreen);
        }

        private static void SetScreen(GameObject screen, GameObject activeScreen)
        {
            if (screen != null)
            {
                screen.SetActive(screen == activeScreen);
            }
        }

        private void ShowLeaveConfirm()
        {
            EnsureLeaveConfirmPanel();
            if (leaveConfirmPanel != null)
            {
                leaveConfirmPanel.transform.SetAsLastSibling();
                leaveConfirmPanel.SetActive(true);
            }
        }

        private void HideLeaveConfirm()
        {
            if (leaveConfirmPanel != null)
            {
                leaveConfirmPanel.SetActive(false);
            }
        }

        private void EnsureLeaveConfirmPanel()
        {
            if (leaveConfirmPanel != null)
            {
                return;
            }

            Transform parent = lobbyScreen != null ? lobbyScreen.transform : transform;
            Font font = lobbyStatusText != null && lobbyStatusText.font != null
                ? lobbyStatusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            leaveConfirmPanel = new GameObject("MultiLeaveConfirmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            leaveConfirmPanel.transform.SetParent(parent, false);
            RectTransform rect = leaveConfirmPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(460f, 210f);
            leaveConfirmPanel.GetComponent<Image>().color = new Color(0.96f, 0.92f, 0.82f, 0.98f);
            Outline outline = leaveConfirmPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.78f);
            outline.effectDistance = new Vector2(2f, -2f);
            Shadow shadow = leaveConfirmPanel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.06f, 0.04f, 0.24f);
            shadow.effectDistance = new Vector2(7f, -8f);

            Text message = CreateRuntimeText("MultiLeaveConfirmText", leaveConfirmPanel.transform, font, LocalizationManager.T("multi_leave_confirm"), 24, new Vector2(0f, 52f), new Vector2(400f, 64f));
            message.fontStyle = FontStyle.Bold;
            message.resizeTextForBestFit = true;
            message.resizeTextMinSize = 18;
            message.resizeTextMaxSize = 24;

            Button leave = CreateRuntimeButton("MultiLeaveConfirmYes", leaveConfirmPanel.transform, font, LocalizationManager.T("multi_leave_yes"), new Vector2(-105f, -50f), new Color(0.98f, 0.62f, 0.52f, 0.96f));
            leave.onClick.AddListener(ConfirmLeaveLobby);

            Button cancel = CreateRuntimeButton("MultiLeaveConfirmNo", leaveConfirmPanel.transform, font, LocalizationManager.T("multi_leave_no"), new Vector2(105f, -50f), new Color(0.88f, 0.84f, 0.74f, 0.96f));
            cancel.onClick.AddListener(CancelLeaveLobby);
            leaveConfirmPanel.SetActive(false);
        }

        private static Text CreateRuntimeText(string name, Transform parent, Font font, string value, int size, Vector2 position, Vector2 dimensions)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return text;
        }

        private static Button CreateRuntimeButton(string name, Transform parent, Font font, string label, Vector2 position, Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 50f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            Button button = buttonObject.GetComponent<Button>();

            Text text = CreateRuntimeText(name + "Text", buttonObject.transform, font, label, 20, Vector2.zero, new Vector2(168f, 44f));
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 20;
            return button;
        }

        private void ShowLobbyNotice(string text, float seconds)
        {
            ResolveLobbyNoticeText();
            if (lobbyNoticeText == null)
            {
                return;
            }

            lobbyNoticeText.text = text;
            lobbyNoticeText.gameObject.SetActive(true);
            lobbyNoticeTimer = seconds;
        }

        private void UpdateLobbyNotice()
        {
            if (lobbyNoticeTimer <= 0f)
            {
                return;
            }

            lobbyNoticeTimer -= Time.unscaledDeltaTime;
            if (lobbyNoticeTimer > 0f)
            {
                return;
            }

            ResolveLobbyNoticeText();
            if (lobbyNoticeText != null)
            {
                lobbyNoticeText.text = string.Empty;
                lobbyNoticeText.gameObject.SetActive(false);
            }
        }

        private void ResolveLobbyNoticeText()
        {
            if (lobbyNoticeText != null)
            {
                return;
            }

            Transform root = lobbyScreen != null ? lobbyScreen.transform : transform;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "MultiLobbyNotice")
                {
                    lobbyNoticeText = texts[i];
                    return;
                }
            }
        }

        private void RefreshOnlineText(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            if (state == OnlineConnectionState.Playing && !stageStartedFromOnline)
            {
                stageStartedFromOnline = true;
                stageManager?.SelectStage(lobby != null && !string.IsNullOrEmpty(lobby.StageId) ? lobby.StageId : "1-1");
                return;
            }

            if (state != OnlineConnectionState.Playing)
            {
                stageStartedFromOnline = false;
            }

            if (randomScreen != null && randomScreen.activeInHierarchy && randomStatusText != null)
            {
                UpdateRandomSearchHeader();
                randomStatusText.text = FormatRandomMatchStatus(lobby, message, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
                RefreshRandomReadyButton(lobby, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
                TryAutoStartRandomMatch(state, lobby);
            }

            if (lobbyScreen != null && lobbyScreen.activeInHierarchy && lobbyStatusText != null)
            {
                OnlineBackendMode mode = onlineManager != null ? onlineManager.EffectiveBackendMode : OnlineBackendMode.Fake;
                lobbyStatusText.text = FormatLobbyStatus(lobby, message, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty, mode);
                SetLobbyButtonState(lobby != null);
            }
        }

        private void RefreshCreateRoomText()
        {
            ResolveCreateRoomTexts();

            if (createRoomMaxPlayersText != null)
            {
                createRoomMaxPlayersText.supportRichText = true;
                createRoomMaxPlayersText.text = $"<color=#1F63D8><b>{createRoomMaxPlayers}</b></color>";
            }

            if (createRoomVisibilityText != null)
            {
                string visibilityValue = createRoomPrivate ? LocalizationManager.T("multi_private") : LocalizationManager.T("multi_public");
                createRoomVisibilityText.supportRichText = true;
                createRoomVisibilityText.text = $"<color=#0E7A2A><b>{visibilityValue}</b></color>";
            }

            if (createRoomStatusText == null || createRoomMaxPlayersText != null)
            {
                return;
            }

            string visibility = createRoomPrivate ? LocalizationManager.T("multi_private") : LocalizationManager.T("multi_public");
            createRoomStatusText.supportRichText = true;
            createRoomStatusText.text =
                $"{LocalizationManager.T("multi_max_players")}\n" +
                $"\n<color=#1F63D8><b>{createRoomMaxPlayers}</b></color>\n\n" +
                $"{LocalizationManager.T("multi_visibility_short")}\n" +
                $"\n<color=#0E7A2A><b>{visibility}</b></color>";
        }

        private void ResolveCreateRoomTexts()
        {
            if (createRoomStatusText == null)
            {
                GameObject statusObject = GameObject.Find("MultiCreateRoomBody");
                if (statusObject != null)
                {
                    createRoomStatusText = statusObject.GetComponent<Text>();
                }
            }

            if (createRoomMaxPlayersText == null)
            {
                GameObject maxPlayersObject = GameObject.Find("MultiCreateMaxPlayersValue");
                if (maxPlayersObject != null)
                {
                    createRoomMaxPlayersText = maxPlayersObject.GetComponent<Text>();
                }
            }

            if (createRoomVisibilityText == null)
            {
                GameObject visibilityObject = GameObject.Find("MultiCreateVisibilityValue");
                if (visibilityObject != null)
                {
                    createRoomVisibilityText = visibilityObject.GetComponent<Text>();
                }
            }
        }

        private void SetLobbyButtonState(bool hasLobby)
        {
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            bool localHost = hasLobby && IsLocalHost(lobby);
            bool allReady = localHost && AreAllPlayersReady(lobby);
            if (startStageButton != null)
            {
                startStageButton.gameObject.SetActive(localHost);
                startStageButton.interactable = allReady;
            }

            if (copyLobbyIdButton != null)
            {
                copyLobbyIdButton.interactable = hasLobby;
            }
        }

        private static bool AreAllPlayersReady(OnlineLobbyInfo lobby)
        {
            if (lobby == null || lobby.Players == null || lobby.Players.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < lobby.Players.Length; i++)
            {
                if (lobby.Players[i] == null || !lobby.Players[i].IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLocalHost(OnlineLobbyInfo lobby)
        {
            if (onlineManager == null || lobby == null || lobby.Players == null)
            {
                return false;
            }

            string localPlayerId = onlineManager.LocalPlayerId;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                if (player != null && player.IsHost && player.PlayerId == localPlayerId)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryAutoStartRandomMatch(OnlineConnectionState state, OnlineLobbyInfo lobby)
        {
            if (randomStartRequested
                || state != OnlineConnectionState.Matching
                || lobby == null
                || lobby.Mode != OnlineLobbyMode.Random
                || lobby.Players == null
                || lobby.Players.Length < 2
                || !IsLocalHost(lobby))
            {
                return;
            }

            for (int i = 0; i < lobby.Players.Length; i++)
            {
                if (lobby.Players[i] == null || !lobby.Players[i].IsReady)
                {
                    return;
                }
            }

            randomStartRequested = true;
            stageManager?.OpenStageSelectFromMultiLobby();
        }

        private static string FormatLobbyStatus(OnlineLobbyInfo lobby, string message, string localPlayerId, OnlineBackendMode mode)
        {
            if (lobby == null)
            {
                return LocalizationManager.T("multi_connecting");
            }

            string label = LocalizationManager.T("multi_room_code_label");
            string displayId = mode == OnlineBackendMode.Fake
                ? LocalizationManager.T("multi_offline_lobby_id")
                : !string.IsNullOrEmpty(lobby.RoomCode) ? lobby.RoomCode : ShortId(lobby.LobbyId);
            string text = $"{label}: {displayId}\n{LocalizationManager.T("multi_players")} {lobby.Players.Length} / {lobby.MaxPlayers}\n";

            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                if (player == null)
                {
                    continue;
                }

                string ready = player.IsReady ? LocalizationManager.T("multi_ready") : LocalizationManager.T("multi_wait");
                string host = player.IsHost ? " " + LocalizationManager.T("multi_host") : string.Empty;
                string playerLabel = player.PlayerId == localPlayerId ? "P1" : $"P{i + 1}";
                text += $"\n{playerLabel}  {player.DisplayName}  {ready}{host}";
            }

            if (ShouldShowLobbyMessage(message))
            {
                text += "\n" + message;
            }

            return text;
        }

        private static bool ShouldShowLobbyMessage(string message)
        {
            return message == LocalizationManager.T("multi_no_lobby_id")
                || message == LocalizationManager.T("multi_host_only_start");
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            return id.Length <= 8 ? id : id.Substring(0, 4) + "..." + id.Substring(id.Length - 4);
        }

        private string FormatRandomMatchStatus(OnlineLobbyInfo lobby, string message, string localPlayerId)
        {
            int maxPlayers = lobby != null ? Mathf.Max(1, lobby.MaxPlayers) : 4;
            OnlinePlayerInfo[] players = lobby?.Players ?? System.Array.Empty<OnlinePlayerInfo>();
            int count = players.Length;
            int elapsed = randomMatchStartedAt > 0f ? Mathf.FloorToInt(Time.unscaledTime - randomMatchStartedAt) : 0;
            int dotCount = Mathf.FloorToInt(Time.unscaledTime * 2.4f) % 4;
            string dots = new string('.', dotCount).PadRight(3, ' ');

            string text = $"{LocalizationManager.T("multi_players")}  {count} / {maxPlayers}     {elapsed / 60:00}:{elapsed % 60:00}\n\n";

            for (int i = 0; i < maxPlayers; i++)
            {
                if (i < count && players[i] != null)
                {
                    OnlinePlayerInfo player = players[i];
                    bool local = player.PlayerId == localPlayerId;
                    string icon = local ? "P1" : $"P{i + 1}";
                    string ready = player.IsReady ? LocalizationManager.T("multi_ready") : LocalizationManager.T("multi_wait");
                    text += $"{icon}  {player.DisplayName}    {ready}\n";
                }
                else
                {
                    text += $"P{i + 1}  {LocalizationManager.T("multi_searching_slot")}{dots}\n";
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                text += "\n" + message;
            }

            return text;
        }

        private void UpdateRandomSearchHeader()
        {
            ResolveRandomSearchHeader();
            int dotCount = Mathf.FloorToInt(Time.unscaledTime * 2.4f) % 4;
            if (randomSearchingLabel != null)
            {
                randomSearchingLabel.text = LocalizationManager.T("multi_searching_players");
            }

            if (randomSearchingDotsLabel != null)
            {
                randomSearchingDotsLabel.text = new string('.', dotCount);
            }
        }

        private void ResolveRandomSearchHeader()
        {
            if (randomSearchingLabel != null && randomSearchingDotsLabel != null)
            {
                return;
            }

            GameObject label = GameObject.Find("MultiRandomSearchingLabel");
            if (label != null)
            {
                randomSearchingLabel = label.GetComponent<Text>();
            }

            GameObject dots = GameObject.Find("MultiRandomSearchingDots");
            if (dots != null)
            {
                randomSearchingDotsLabel = dots.GetComponent<Text>();
            }
        }

        private void RefreshRandomReadyButton(OnlineLobbyInfo lobby, string localPlayerId)
        {
            if (randomReadyButton == null)
            {
                GameObject buttonObject = GameObject.Find("MultiRandomReadyButton");
                if (buttonObject != null)
                {
                    randomReadyButton = buttonObject.GetComponent<Button>();
                    randomReadyLabel = buttonObject.GetComponentInChildren<Text>(true);
                }
            }

            if (randomReadyButton == null)
            {
                return;
            }

            bool ready = false;
            OnlinePlayerInfo[] players = lobby?.Players;
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].PlayerId == localPlayerId)
                    {
                        ready = players[i].IsReady;
                        break;
                    }
                }
            }

            Image image = randomReadyButton.GetComponent<Image>();
            Color normalColor = ready ? new Color(0.5f, 0.92f, 0.48f, 0.96f) : new Color(0.82f, 0.82f, 0.76f, 0.94f);
            Color hoverColor = ready ? new Color(0.62f, 1f, 0.58f, 1f) : new Color(0.76f, 0.94f, 0.72f, 1f);
            if (image != null)
            {
                image.color = normalColor;
            }

            MultiMenuButtonHover hover = randomReadyButton.GetComponent<MultiMenuButtonHover>();
            if (hover != null)
            {
                hover.Configure(image, normalColor, hoverColor, 1.04f, 4f);
            }
            if (randomReadyLabel != null)
            {
                randomReadyLabel.text = ready ? "\u2713 READY" : "READY";
                randomReadyLabel.color = Color.black;
                randomReadyLabel.fontStyle = FontStyle.Bold;
            }
        }
    }
}
