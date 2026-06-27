using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class MultiMenuController : MonoBehaviour
    {
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private GameObject choiceScreen;
        [SerializeField] private GameObject randomScreen;
        [SerializeField] private GameObject roomScreen;
        [SerializeField] private GameObject createRoomScreen;
        [SerializeField] private GameObject joinRoomScreen;
        [SerializeField] private GameObject lobbyScreen;
        [SerializeField] private Text randomStatusText;
        [SerializeField] private Text lobbyStatusText;

        private void OnEnable()
        {
            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
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
            ShowLobby();
        }

        public void JoinRoom()
        {
            onlineManager?.JoinRoom("ABC123");
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
            if (randomScreen != null && randomScreen.activeInHierarchy && randomStatusText != null)
            {
                randomStatusText.text = FormatLobbyStatus("Matching...", lobby, message);
            }

            if (lobbyScreen != null && lobbyScreen.activeInHierarchy && lobbyStatusText != null)
            {
                lobbyStatusText.text = FormatLobbyStatus("Room Lobby", lobby, message);
            }
        }

        private static string FormatLobbyStatus(string title, OnlineLobbyInfo lobby, string message)
        {
            if (lobby == null)
            {
                return title + "\n\nNot connected.";
            }

            string text = $"{title}\n\nRoom: {lobby.RoomName}\nID: {lobby.LobbyId}\nPlayers {lobby.Players.Length} / {lobby.MaxPlayers}\n";
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                string ready = player.IsReady ? "READY" : "WAIT";
                string host = player.IsHost ? " HOST" : string.Empty;
                text += $"\n{ready}  {player.DisplayName}{host}";
            }

            if (!string.IsNullOrEmpty(message))
            {
                text += "\n\n" + message;
            }

            return text;
        }
    }
}
