using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Nine quick emotes for co-op communication. Local input and the right-side
    /// palette use the same path; online peers receive only the emote id.
    /// </summary>
    public sealed class PlayerEmoteController : MonoBehaviour
    {
        private const string NetworkKind = "player_emote";
        private const float DisplaySeconds = 2.6f;

        [Serializable]
        private sealed class EmotePayload
        {
            public int Id;
        }

        private sealed class Bubble
        {
            public Transform Target;
            public GameObject Root;
            public TextMesh Glyph;
            public SpriteRenderer Inner;
            public float ShownAt;
            public float HideAt;
        }

        private static readonly KeyCode[] NumberKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
        };

        private static readonly KeyCode[] KeypadKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
            KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
            KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
        };

        private static readonly string[] Glyphs = { "↑", "↓", "→", "←", "人", "猫", "鳥", "亀", "●" };
        private static readonly Color[] GlyphColors =
        {
            new Color(0.1f, 0.48f, 0.9f), new Color(0.1f, 0.48f, 0.9f),
            new Color(0.1f, 0.48f, 0.9f), new Color(0.1f, 0.48f, 0.9f),
            new Color(0.92f, 0.25f, 0.2f), new Color(0.95f, 0.54f, 0.12f),
            new Color(0.08f, 0.55f, 0.92f), new Color(0.2f, 0.62f, 0.24f),
            new Color(0.18f, 0.82f, 0.45f)
        };

        private readonly Dictionary<Transform, Bubble> bubbles = new Dictionary<Transform, Bubble>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Canvas paletteCanvas;
        private Text paletteTitle;
        private Font handwrittenFont;

        private void Awake()
        {
            stageManager = GetComponent<StageManager>();
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            onlineManager = FindFirstObjectByType<OnlineManager>();
            handwrittenFont = StageSurvivalController.FindHandwrittenFont();
            BuildPalette();
        }

        private void OnEnable()
        {
            ResolveOnlineManager();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkEmote;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkEmote;
        }

        private void Update()
        {
            bool gameplayVisible = stageManager != null && stageManager.IsGameplayActive;
            if (paletteCanvas != null) paletteCanvas.enabled = gameplayVisible;
            if (paletteTitle != null)
            {
                string localizedTitle = LocalizationManager.T("emote_palette_title");
                if (paletteTitle.text != localizedTitle) paletteTitle.text = localizedTitle;
            }
            if (gameplayVisible && !IsTypingInInputField())
            {
                for (int i = 0; i < NumberKeys.Length; i++)
                {
                    if (Input.GetKeyDown(NumberKeys[i]) || Input.GetKeyDown(KeypadKeys[i]))
                    {
                        SendLocalEmote(i);
                        break;
                    }
                }
            }

            UpdateBubbles(gameplayVisible);
        }

        private void SendLocalEmote(int id)
        {
            if (id < 0 || id >= Glyphs.Length || stageManager == null || !stageManager.IsGameplayActive)
            {
                return;
            }

            Transform localPlayer = stageManager.ActivePlayerTransform;
            if (localPlayer != null) ShowBubble(localPlayer, id);

            ResolveOnlineManager();
            if (onlineManager != null && onlineManager.State == OnlineConnectionState.Playing)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    Kind = NetworkKind,
                    ObjectId = stageManager.CurrentStageId,
                    Json = JsonUtility.ToJson(new EmotePayload { Id = id })
                });
            }
        }

        private void HandleNetworkEmote(OnlineGimmickData data)
        {
            if (data == null || data.Kind != NetworkKind || onlineManager == null
                || data.PlayerId == onlineManager.LocalPlayerId
                || stageManager == null || data.ObjectId != stageManager.CurrentStageId)
            {
                return;
            }

            EmotePayload payload = JsonUtility.FromJson<EmotePayload>(data.Json);
            Transform remotePlayer = stageManager.GetOnlinePlayerTransform(data.PlayerId);
            if (payload != null && remotePlayer != null && payload.Id >= 0 && payload.Id < Glyphs.Length)
            {
                ShowBubble(remotePlayer, payload.Id);
            }
        }

        private void ShowBubble(Transform target, int id)
        {
            if (!bubbles.TryGetValue(target, out Bubble bubble) || bubble.Root == null)
            {
                bubble = CreateBubble(target);
                bubbles[target] = bubble;
            }

            bubble.Glyph.text = Glyphs[id];
            bubble.Glyph.color = GlyphColors[id];
            bubble.Inner.color = id == 8
                ? new Color(0.9f, 1f, 0.88f, 0.97f)
                : new Color(1f, 0.96f, 0.72f, 0.97f);
            bubble.ShownAt = Time.unscaledTime;
            bubble.HideAt = Time.unscaledTime + DisplaySeconds;
            bubble.Root.SetActive(true);
            PositionBubble(bubble, 0f);
        }

        private Bubble CreateBubble(Transform target)
        {
            GameObject root = new GameObject("Player Emote Bubble");
            Sprite circle = StageSurvivalController.GetCircleSprite();

            GameObject outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(root.transform, false);
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sprite = circle;
            outline.color = new Color(0.12f, 0.1f, 0.08f, 0.94f);
            outline.sortingOrder = 495;
            outlineObject.transform.localScale = Vector3.one * 1.16f;

            GameObject innerObject = new GameObject("Paper");
            innerObject.transform.SetParent(root.transform, false);
            SpriteRenderer inner = innerObject.AddComponent<SpriteRenderer>();
            inner.sprite = circle;
            inner.sortingOrder = 496;
            innerObject.transform.localScale = Vector3.one * 1.02f;

            GameObject glyphObject = new GameObject("Mark");
            glyphObject.transform.SetParent(root.transform, false);
            glyphObject.transform.localPosition = new Vector3(0f, 0.01f, -0.05f);
            TextMesh glyph = glyphObject.AddComponent<TextMesh>();
            glyph.anchor = TextAnchor.MiddleCenter;
            glyph.alignment = TextAlignment.Center;
            glyph.fontSize = 80;
            glyph.characterSize = 0.1f;
            glyph.fontStyle = FontStyle.Bold;
            if (handwrittenFont != null)
            {
                glyph.font = handwrittenFont;
                glyphObject.GetComponent<MeshRenderer>().sharedMaterial = handwrittenFont.material;
            }
            glyphObject.GetComponent<MeshRenderer>().sortingOrder = 497;

            root.SetActive(false);
            return new Bubble { Target = target, Root = root, Glyph = glyph, Inner = inner };
        }

        private void UpdateBubbles(bool gameplayVisible)
        {
            List<Transform> stale = null;
            foreach (KeyValuePair<Transform, Bubble> pair in bubbles)
            {
                Bubble bubble = pair.Value;
                if (pair.Key == null || bubble.Root == null)
                {
                    stale ??= new List<Transform>();
                    stale.Add(pair.Key);
                    continue;
                }

                bool visible = gameplayVisible && Time.unscaledTime < bubble.HideAt;
                bubble.Root.SetActive(visible);
                if (visible) PositionBubble(bubble, Time.unscaledTime - bubble.ShownAt);
            }
            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++) bubbles.Remove(stale[i]);
            }
        }

        private void PositionBubble(Bubble bubble, float age)
        {
            Bounds bounds = GetPlayerBounds(bubble.Target);
            Camera camera = Camera.main;
            float cameraScale = camera != null && camera.orthographic
                ? Mathf.Clamp(camera.orthographicSize / 8f, 1f, 1.8f)
                : 1f;
            float pop = Mathf.Clamp01(age / 0.16f);
            pop = 1f + 0.16f * Mathf.Sin(pop * Mathf.PI);
            float bob = Mathf.Sin(Time.unscaledTime * 4.5f) * 0.06f * cameraScale;
            bubble.Root.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.78f * cameraScale + bob, -0.5f);
            bubble.Root.transform.localScale = Vector3.one * cameraScale * pop;
        }

        private static Bounds GetPlayerBounds(Transform target)
        {
            Bounds bounds = new Bounds(target.position, Vector3.one);
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(false);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return bounds;
        }

        private void BuildPalette()
        {
            GameObject canvasObject = new GameObject("Emote Palette Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            paletteCanvas = canvasObject.GetComponent<Canvas>();
            paletteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            paletteCanvas.sortingOrder = 260;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = CreateRect("Emote Palette", canvasObject.transform as RectTransform,
                new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(118f, 592f));
            panel.pivot = new Vector2(1f, 0.5f);
            Image paper = panel.gameObject.AddComponent<Image>();
            paper.color = new Color(1f, 0.96f, 0.76f, 0.88f);
            Outline panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.88f);
            panelOutline.effectDistance = new Vector2(3f, -3f);

            paletteTitle = CreateText("Title", panel, LocalizationManager.T("emote_palette_title"), 20,
                new Vector2(0f, 268f), new Vector2(104f, 34f), new Color(0.12f, 0.1f, 0.08f));

            for (int i = 0; i < Glyphs.Length; i++)
            {
                int emoteId = i;
                RectTransform buttonRect = CreateRect("Emote " + (i + 1), panel,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 218f - i * 53f), new Vector2(88f, 44f));
                Image image = buttonRect.gameObject.AddComponent<Image>();
                image.color = i % 2 == 0
                    ? new Color(1f, 0.82f, 0.34f, 0.98f)
                    : new Color(0.64f, 0.88f, 1f, 0.98f);
                Button button = buttonRect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SendLocalEmote(emoteId));
                Outline outline = buttonRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
                CreateText("Label", buttonRect, (i + 1) + "  " + Glyphs[i], 23,
                    Vector2.zero, new Vector2(84f, 40f), GlyphColors[i]);
            }

            paletteCanvas.enabled = false;
        }

        private Text CreateText(string name, RectTransform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = handwrittenFont != null ? handwrittenFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = root.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void ResolveOnlineManager()
        {
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
        }

        private static bool IsTypingInInputField()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
    }
}
