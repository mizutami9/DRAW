using System.Collections.Generic;
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
            int slot = GetLobbyPlayerSlot(lobby, playerId);
            return slot >= 0 ? slot : fallbackIndex;
        }

        public static int GetLobbyPlayerSlot(OnlineLobbyInfo lobby, string playerId)
        {
            if (lobby == null || lobby.Players == null || string.IsNullOrEmpty(playerId))
            {
                return -1;
            }

            List<OnlinePlayerInfo> ordered = new List<OnlinePlayerInfo>();
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo player = lobby.Players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }
                bool duplicate = false;
                for (int known = 0; known < ordered.Count; known++)
                {
                    if (ordered[known].PlayerId == player.PlayerId)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    ordered.Add(player);
                }
            }

            // EOS may expose the roster in local-first order. Every peer must
            // derive the same P-number and color from stable data instead.
            ordered.Sort((left, right) =>
            {
                if (left.IsHost != right.IsHost) return left.IsHost ? -1 : 1;
                return string.CompareOrdinal(left.PlayerId, right.PlayerId);
            });
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].PlayerId == playerId) return i;
            }
            return -1;
        }
    }
}
