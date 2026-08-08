using UnityEngine;

namespace DrawBody.Prototype
{
    public static class PlayerColorPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.95f, 0.12f, 0.10f, 1f),
            new Color(0.08f, 0.36f, 1f, 1f),
            new Color(0.08f, 0.72f, 0.24f, 1f),
            new Color(0.95f, 0.62f, 0.05f, 1f)
        };

        public static Color GetColor(int playerIndex)
        {
            int index = Mathf.Abs(playerIndex) % Colors.Length;
            return Colors[index];
        }

        public static int GetLobbyColorIndex(OnlineLobbyInfo lobby, string playerId, int fallbackIndex)
        {
            if (lobby == null || lobby.Players == null || string.IsNullOrEmpty(playerId))
            {
                return fallbackIndex;
            }

            int nonHostIndex = 1;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.PlayerId == playerId)
                {
                    return player.IsHost ? 0 : nonHostIndex;
                }

                if (!player.IsHost)
                {
                    nonHostIndex++;
                }
            }

            return fallbackIndex;
        }
    }
}
