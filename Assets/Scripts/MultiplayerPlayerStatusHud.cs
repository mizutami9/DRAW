using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Compact multiplayer roster shown in the upper-right corner during gameplay.
    /// It reuses the authoritative redraw/player state and the existing emote event.
    /// </summary>
    public sealed class MultiplayerPlayerStatusHud : MonoBehaviour
    {
        private const int MaximumPlayers = 4;
        private const float EmoteSeconds = 3f;

        private enum StatusKind
        {
            None,
            Emote,
            Redrawing,
            Dead
        }

        private sealed class PlayerSlot
        {
            public RectTransform Root;
            public Image PortraitBackground;
            public Image Portrait;
            public Text Number;
            public RectTransform StatusIconRect;
            public Image StatusIcon;
            public CanvasGroup StatusCanvas;
            public string PlayerId;
            public int EmoteId = -1;
            public float EmoteUntil;
            public float StatusChangedAt;
            public StatusKind Status;
        }

        private readonly PlayerSlot[] slots = new PlayerSlot[MaximumPlayers];
        private readonly Dictionary<string, bool> remoteRespawning = new Dictionary<string, bool>();
        private OnlineManager onlineManager;
        private StageManager stageManager;
        private Canvas canvas;
        private RectTransform panel;
        private Font font;
        private string rosterSignature = string.Empty;
        private float nextRosterRefreshAt;
        private static Sprite pencilIcon;
        private static Sprite deadIcon;

        private void Awake()
        {
            onlineManager = FindFirstObjectByType<OnlineManager>();
            stageManager = GetComponent<StageManager>();
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            font = DoodleRuntimeAssets.HandwrittenFont;
            BuildHud();
        }

        private void OnEnable()
        {
            ResolveManagers();
            if (onlineManager != null)
            {
                onlineManager.StateChanged += HandleOnlineStateChanged;
                onlineManager.PlayerStateReceived += HandlePlayerState;
            }
            PlayerEmoteController.EmoteShown += HandleEmoteShown;
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged -= HandleOnlineStateChanged;
                onlineManager.PlayerStateReceived -= HandlePlayerState;
            }
            PlayerEmoteController.EmoteShown -= HandleEmoteShown;
        }

        private void Update()
        {
            ResolveManagers();
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            bool visible = onlineManager != null
                && onlineManager.State == OnlineConnectionState.Playing
                && lobby?.Players != null
                && lobby.Players.Length > 1
                && stageManager != null
                && stageManager.IsGameplayActive;
            if (canvas != null) canvas.enabled = visible;
            if (!visible) return;

            if (Time.unscaledTime >= nextRosterRefreshAt || GetRosterSignature(lobby) != rosterSignature)
            {
                RefreshRoster(lobby);
                nextRosterRefreshAt = Time.unscaledTime + 0.35f;
            }
            RefreshStatuses(lobby);
        }

        private void ResolveManagers()
        {
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
        }

        private void HandleOnlineStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            rosterSignature = string.Empty;
            if (state != OnlineConnectionState.Playing)
            {
                remoteRespawning.Clear();
                if (canvas != null) canvas.enabled = false;
            }
        }

        private void HandlePlayerState(OnlinePlayerState state)
        {
            if (state == null || string.IsNullOrEmpty(state.PlayerId)) return;
            remoteRespawning[state.PlayerId] = state.Respawning;
        }

        private void HandleEmoteShown(string playerId, int emoteId)
        {
            if (string.IsNullOrEmpty(playerId) || emoteId < 0 || emoteId > 8) return;
            for (int i = 0; i < slots.Length; i++)
            {
                PlayerSlot slot = slots[i];
                if (slot == null || slot.PlayerId != playerId) continue;
                slot.EmoteId = emoteId;
                slot.EmoteUntil = Time.unscaledTime + EmoteSeconds;
                SetStatus(slot, StatusKind.Emote, PlayerEmoteController.GetEmoteIcon(emoteId));
                break;
            }
        }

        private void RefreshRoster(OnlineLobbyInfo lobby)
        {
            rosterSignature = GetRosterSignature(lobby);
            OnlinePlayerInfo[] players = lobby?.Players;
            for (int i = 0; i < slots.Length; i++)
            {
                PlayerSlot slot = slots[i];
                OnlinePlayerInfo player = players != null && i < players.Length ? players[i] : null;
                bool active = player != null && !string.IsNullOrEmpty(player.PlayerId);
                slot.Root.gameObject.SetActive(active);
                if (!active)
                {
                    slot.PlayerId = null;
                    continue;
                }

                if (slot.PlayerId != player.PlayerId)
                {
                    slot.PlayerId = player.PlayerId;
                    slot.EmoteId = -1;
                    slot.EmoteUntil = 0f;
                    slot.Status = StatusKind.None;
                    slot.StatusIconRect.gameObject.SetActive(false);
                }
                slot.Number.text = (i + 1) + "P";
                int colorIndex = PlayerColorPalette.GetLobbyColorIndex(lobby, player.PlayerId, i);
                Color playerColor = PlayerColorPalette.GetColor(colorIndex);
                slot.Number.color = playerColor;
                slot.PortraitBackground.color = Color.Lerp(playerColor, Color.white, 0.72f);
                slot.Portrait.color = Color.white;
                RefreshPortrait(slot, player.PlayerId);
            }
        }

        private void RefreshPortrait(PlayerSlot slot, string playerId)
        {
            PlayerController2D player = stageManager != null ? stageManager.GetOnlinePlayerController(playerId) : null;
            PlayerAbilityController ability = player != null ? player.GetComponent<PlayerAbilityController>() : null;
            int speciesIcon = 4;
            if (ability != null)
            {
                speciesIcon += Mathf.Clamp((int)ability.CurrentProfile.Species, 0, 4);
            }
            slot.Portrait.sprite = PlayerEmoteController.GetEmoteIcon(speciesIcon);
            slot.Portrait.preserveAspect = true;
        }

        private void RefreshStatuses(OnlineLobbyInfo lobby)
        {
            OnlinePlayerInfo[] players = lobby?.Players;
            for (int i = 0; i < slots.Length; i++)
            {
                if (players == null || i >= players.Length || players[i] == null) continue;
                PlayerSlot slot = slots[i];
                PlayerController2D player = stageManager.GetOnlinePlayerController(slot.PlayerId);
                bool local = onlineManager != null && slot.PlayerId == onlineManager.LocalPlayerId;
                bool respawning = local
                    ? stageManager.IsPlayerRespawning(player)
                    : remoteRespawning.TryGetValue(slot.PlayerId, out bool remoteValue) && remoteValue;
                bool dead = respawning || player != null && !player.gameObject.activeInHierarchy;
                PlayerRedrawStateController redraw = player != null
                    ? player.GetComponent<PlayerRedrawStateController>()
                    : null;
                bool redrawing = local ? stageManager.IsDrawingMode : redraw != null && redraw.IsRedrawing;

                StatusKind kind;
                Sprite icon;
                if (dead)
                {
                    kind = StatusKind.Dead;
                    icon = GetDeadIcon();
                }
                else if (redrawing)
                {
                    kind = StatusKind.Redrawing;
                    icon = GetPencilIcon();
                }
                else if (slot.EmoteId >= 0 && Time.unscaledTime < slot.EmoteUntil)
                {
                    kind = StatusKind.Emote;
                    icon = PlayerEmoteController.GetEmoteIcon(slot.EmoteId);
                }
                else
                {
                    kind = StatusKind.None;
                    icon = null;
                }
                SetStatus(slot, kind, icon);
                AnimateStatus(slot);
                RefreshPortrait(slot, slot.PlayerId);
            }
        }

        private static void SetStatus(PlayerSlot slot, StatusKind kind, Sprite icon)
        {
            if (slot.Status != kind || slot.StatusIcon.sprite != icon)
            {
                slot.Status = kind;
                slot.StatusChangedAt = Time.unscaledTime;
                slot.StatusIcon.sprite = icon;
            }
            slot.StatusIconRect.gameObject.SetActive(kind != StatusKind.None && icon != null);
        }

        private static void AnimateStatus(PlayerSlot slot)
        {
            if (!slot.StatusIconRect.gameObject.activeSelf) return;
            float t = Mathf.Clamp01((Time.unscaledTime - slot.StatusChangedAt) / 0.22f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            slot.StatusIconRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-42f, 0f, eased));
            slot.StatusIconRect.localScale = Vector3.one * Mathf.Lerp(0.76f, 1f, eased);
            slot.StatusCanvas.alpha = eased;
        }

        private void BuildHud()
        {
            GameObject canvasObject = new GameObject("Multiplayer Player Status Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 245;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateRect("Player Status List", canvasObject.transform as RectTransform,
                new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(248f, 276f));
            panel.pivot = new Vector2(1f, 1f);
            for (int i = 0; i < slots.Length; i++) slots[i] = BuildSlot(i);
            canvas.enabled = false;
        }

        private PlayerSlot BuildSlot(int index)
        {
            RectTransform row = CreateRect("Player " + (index + 1) + " Status", panel,
                new Vector2(1f, 1f), new Vector2(0f, -index * 66f), new Vector2(238f, 58f));
            row.pivot = new Vector2(1f, 1f);
            Image paper = row.gameObject.AddComponent<Image>();
            paper.color = index % 2 == 0
                ? new Color(1f, 0.965f, 0.79f, 0.93f)
                : new Color(0.91f, 0.97f, 1f, 0.93f);
            paper.raycastTarget = false;
            Outline outline = row.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.12f, 0.08f, 0.76f);
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform portraitPaper = CreateRect("Portrait Paper", row,
                new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(46f, 46f));
            portraitPaper.pivot = new Vector2(0f, 0.5f);
            Image portraitBackground = portraitPaper.gameObject.AddComponent<Image>();
            portraitBackground.sprite = DoodleRuntimeAssets.CircleSprite;
            portraitBackground.color = new Color(1f, 0.99f, 0.9f, 1f);
            portraitBackground.raycastTarget = false;

            RectTransform portraitRect = CreateRect("Character Icon", portraitPaper,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            Image portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;

            Text number = CreateText("Player Number", row, (index + 1) + "P", 27,
                new Vector2(66f, 0f), new Vector2(82f, 44f), TextAnchor.MiddleLeft);
            number.rectTransform.anchorMin = number.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            number.rectTransform.pivot = new Vector2(0f, 0.5f);

            RectTransform viewport = CreateRect("Status Pop Window", row,
                new Vector2(1f, 0.5f), new Vector2(-7f, 0f), new Vector2(58f, 48f));
            viewport.pivot = new Vector2(1f, 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform statusPaperRect = CreateRect("Status Paper", viewport,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(45f, 45f));
            Image statusPaper = statusPaperRect.gameObject.AddComponent<Image>();
            statusPaper.sprite = DoodleRuntimeAssets.CircleSprite;
            statusPaper.color = new Color(1f, 0.985f, 0.88f, 0.98f);
            statusPaper.raycastTarget = false;
            Outline statusOutline = statusPaperRect.gameObject.AddComponent<Outline>();
            statusOutline.effectColor = new Color(0.2f, 0.15f, 0.08f, 0.62f);
            statusOutline.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform statusRect = CreateRect("Status Icon", statusPaperRect,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            Image statusIcon = statusRect.gameObject.AddComponent<Image>();
            statusIcon.preserveAspect = true;
            statusIcon.raycastTarget = false;
            CanvasGroup statusCanvas = statusRect.gameObject.AddComponent<CanvasGroup>();
            statusPaperRect.gameObject.SetActive(false);

            row.gameObject.SetActive(false);
            return new PlayerSlot
            {
                Root = row,
                PortraitBackground = portraitBackground,
                Portrait = portrait,
                Number = number,
                StatusIconRect = statusPaperRect,
                StatusIcon = statusIcon,
                StatusCanvas = statusCanvas
            };
        }

        private Text CreateText(string name, RectTransform parent, string value, int size,
            Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = new Color(0.12f, 0.1f, 0.08f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = obj.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static string GetRosterSignature(OnlineLobbyInfo lobby)
        {
            StringBuilder builder = new StringBuilder();
            OnlinePlayerInfo[] players = lobby?.Players;
            if (players == null) return string.Empty;
            for (int i = 0; i < players.Length && i < MaximumPlayers; i++)
            {
                builder.Append(players[i]?.PlayerId).Append('|');
            }
            return builder.ToString();
        }

        private static Sprite GetPencilIcon()
        {
            if (pencilIcon != null) return pencilIcon;
            pencilIcon = CreateStatusSprite("Redrawing Pencil", (pixels, size) =>
            {
                Color32 outline = new Color32(64, 48, 31, 255);
                Color32 yellow = new Color32(246, 177, 39, 255);
                Color32 coral = new Color32(242, 91, 72, 255);
                DrawLine(pixels, size, new Vector2Int(22, 19), new Vector2Int(73, 70), 8, outline);
                DrawLine(pixels, size, new Vector2Int(24, 21), new Vector2Int(70, 67), 5, yellow);
                DrawLine(pixels, size, new Vector2Int(64, 61), new Vector2Int(73, 70), 5, coral);
                DrawLine(pixels, size, new Vector2Int(18, 15), new Vector2Int(27, 20), 4, outline);
            });
            return pencilIcon;
        }

        private static Sprite GetDeadIcon()
        {
            if (deadIcon != null) return deadIcon;
            deadIcon = CreateStatusSprite("Player Down", (pixels, size) =>
            {
                Color32 ink = new Color32(180, 42, 42, 255);
                DrawEllipse(pixels, size, new Vector2Int(48, 49), 28, 27, 3, ink);
                DrawLine(pixels, size, new Vector2Int(31, 58), new Vector2Int(42, 47), 3, ink);
                DrawLine(pixels, size, new Vector2Int(42, 58), new Vector2Int(31, 47), 3, ink);
                DrawLine(pixels, size, new Vector2Int(54, 58), new Vector2Int(65, 47), 3, ink);
                DrawLine(pixels, size, new Vector2Int(65, 58), new Vector2Int(54, 47), 3, ink);
                DrawLine(pixels, size, new Vector2Int(35, 34), new Vector2Int(43, 39), 3, ink);
                DrawLine(pixels, size, new Vector2Int(43, 39), new Vector2Int(51, 33), 3, ink);
                DrawLine(pixels, size, new Vector2Int(51, 33), new Vector2Int(61, 38), 3, ink);
            });
            return deadIcon;
        }

        private delegate void StatusIconDrawer(Color32[] pixels, int size);

        private static Sprite CreateStatusSprite(string name, StatusIconDrawer drawer)
        {
            const int size = 96;
            Color32[] pixels = new Color32[size * size];
            drawer(pixels, size);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DrawEllipse(Color32[] pixels, int size, Vector2Int center,
            int radiusX, int radiusY, int thickness, Color32 color)
        {
            Vector2Int previous = new Vector2Int(center.x + radiusX, center.y);
            const int segments = 64;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2Int next = new Vector2Int(
                    Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radiusX),
                    Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radiusY));
                DrawLine(pixels, size, previous, next, thickness, color);
                previous = next;
            }
        }

        private static void DrawLine(Color32[] pixels, int size, Vector2Int from,
            Vector2Int to, int radius, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps > 0 ? i / (float)steps : 0f;
                DrawDot(pixels, size,
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)), radius, color);
            }
        }

        private static void DrawDot(Color32[] pixels, int size, int x, int y, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int py = y - radius; py <= y + radius; py++)
            {
                if (py < 0 || py >= size) continue;
                for (int px = x - radius; px <= x + radius; px++)
                {
                    if (px < 0 || px >= size) continue;
                    int dx = px - x;
                    int dy = py - y;
                    if (dx * dx + dy * dy <= radiusSquared) pixels[py * size + px] = color;
                }
            }
        }
    }
}
