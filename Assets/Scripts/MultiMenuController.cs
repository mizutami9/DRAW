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
        [SerializeField] private InputField joinAddressInput;
        [SerializeField] private Button startStageButton;
        [SerializeField] private Button copyLobbyIdButton;
        private bool stageStartedFromOnline;

        private void OnEnable()
        {
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

            ShowChoice();
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged -= RefreshOnlineText;
            }
        }

        public void ShowChoice()
        {
            ShowOnly(choiceScreen);
        }

        public void ShowRandom()
        {
            onlineManager?.StartRandomMatch();
            if (randomStatusText != null)
            {
                randomStatusText.text = "Matching...";
            }

            ShowOnly(randomScreen);
        }

        public void ShowRoom()
        {
            ShowOnly(roomScreen);
        }

        public void ShowCreateRoom()
        {
            ShowOnly(createRoomScreen);
        }

        public void ShowJoinRoom()
        {
            ShowOnly(joinRoomScreen);
        }

        public void CreateRoom()
        {
            onlineManager?.CreateRoom("Draw Together", 4, false);
            SetLobbyButtonState(false);
            ShowLobby();
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
                        placeholder.text = "Lobby IDを入力";
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
            ShowLobby();
        }

        public void StartStage()
        {
            if (IsLocalHost(onlineManager != null ? onlineManager.CurrentLobby : null))
            {
                onlineManager?.StartGame("1-1");
            }
            else
            {
                RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null, "ホストだけが開始できます。");
            }
        }

        public void CopyLobbyId()
        {
            string lobbyId = onlineManager != null && onlineManager.CurrentLobby != null ? onlineManager.CurrentLobby.LobbyId : string.Empty;
            if (string.IsNullOrEmpty(lobbyId))
            {
                RefreshOnlineText(onlineManager != null ? onlineManager.State : OnlineConnectionState.Offline, onlineManager != null ? onlineManager.CurrentLobby : null, "コピーできるLobby IDがありません。");
                return;
            }

            GUIUtility.systemCopyBuffer = lobbyId;
            RefreshOnlineText(onlineManager.State, onlineManager.CurrentLobby, "Lobby IDをコピーしました。");
        }

        public void LeaveLobby()
        {
            onlineManager?.LeaveLobby();
            ShowChoice();
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
                randomStatusText.text = FormatLobbyStatus("Matching...", lobby, message, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
            }

            if (lobbyScreen != null && lobbyScreen.activeInHierarchy && lobbyStatusText != null)
            {
                lobbyStatusText.text = FormatLobbyStatus("Room Lobby", lobby, message, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
                SetLobbyButtonState(lobby != null);
            }
        }

        private void SetLobbyButtonState(bool hasLobby)
        {
            bool localHost = hasLobby && IsLocalHost(onlineManager != null ? onlineManager.CurrentLobby : null);
            if (startStageButton != null)
            {
                startStageButton.gameObject.SetActive(localHost);
                startStageButton.interactable = localHost;
            }

            if (copyLobbyIdButton != null)
            {
                copyLobbyIdButton.interactable = hasLobby;
            }
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

        private static string FormatLobbyStatus(string title, OnlineLobbyInfo lobby, string message, string localPlayerId)
        {
            if (lobby == null)
            {
                return title + "\n\n接続中...";
            }

            string text = $"{title}\n\nRoom: {lobby.RoomName}\nID: {lobby.LobbyId}\nPlayers {lobby.Players.Length} / {lobby.MaxPlayers}\n";
            if (!string.IsNullOrEmpty(localPlayerId))
            {
                text += $"Local: {ShortId(localPlayerId)}\n";
            }

            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                string ready = player.IsReady ? "READY" : "WAIT";
                string host = player.IsHost ? " HOST" : string.Empty;
                text += $"\n{ready}  {player.DisplayName}{host}  {ShortId(player.PlayerId)}";
            }

            if (!string.IsNullOrEmpty(message))
            {
                text += "\n\n" + message;
            }

            return text;
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            return id.Length <= 8 ? id : id.Substring(0, 4) + "..." + id.Substring(id.Length - 4);
        }
    }
}
