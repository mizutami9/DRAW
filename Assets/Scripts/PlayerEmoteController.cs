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
        private const float DisplaySeconds = 3f;
        private const int EmoteCount = 9;

        [Serializable]
        private sealed class EmotePayload
        {
            public int Id;
        }

        private sealed class Bubble
        {
            public Transform Target;
            public GameObject Root;
            public SpriteRenderer Mark;
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
        private Font handwrittenFont;
        private static Sprite[] emoteIcons;

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
            if (id < 0 || id >= EmoteCount || stageManager == null || !stageManager.IsGameplayActive)
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
            if (payload != null && remotePlayer != null && payload.Id >= 0 && payload.Id < EmoteCount)
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

            bubble.Mark.sprite = GetEmoteIcon(id);
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

            GameObject markObject = new GameObject("Picture Mark");
            markObject.transform.SetParent(root.transform, false);
            markObject.transform.localPosition = new Vector3(0f, 0.01f, -0.05f);
            markObject.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer mark = markObject.AddComponent<SpriteRenderer>();
            mark.sortingOrder = 497;

            root.SetActive(false);
            return new Bubble { Target = target, Root = root, Mark = mark, Inner = inner };
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
                new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(104f, 506f));
            panel.pivot = new Vector2(0f, 0.5f);
            Image paper = panel.gameObject.AddComponent<Image>();
            paper.color = new Color(1f, 0.96f, 0.76f, 0.88f);
            Outline panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.88f);
            panelOutline.effectDistance = new Vector2(3f, -3f);

            for (int i = 0; i < EmoteCount; i++)
            {
                int emoteId = i;
                RectTransform buttonRect = CreateRect("Emote " + (i + 1), panel,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 212f - i * 53f), new Vector2(84f, 44f));
                Image image = buttonRect.gameObject.AddComponent<Image>();
                image.color = i % 2 == 0
                    ? new Color(1f, 0.82f, 0.34f, 0.98f)
                    : new Color(0.64f, 0.88f, 1f, 0.98f);
                Button button = buttonRect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                button.onClick.AddListener(() =>
                {
                    SendLocalEmote(emoteId);
                    // Unity's selected Button treats Space as Submit. Clear the
                    // pointer-selected emote so jumping cannot replay it.
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                });
                Outline outline = buttonRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
                CreateText("Key", buttonRect, (i + 1).ToString(), 16,
                    new Vector2(-30f, 12f), new Vector2(20f, 20f), new Color(0.12f, 0.1f, 0.08f));
                RectTransform iconRect = CreateRect("Picture Mark", buttonRect,
                    new Vector2(0.5f, 0.5f), new Vector2(7f, 0f), new Vector2(36f, 36f));
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = GetEmoteIcon(i);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            paletteCanvas.enabled = false;
        }

        private static Sprite GetEmoteIcon(int id)
        {
            if (emoteIcons == null)
            {
                emoteIcons = new Sprite[EmoteCount];
            }
            id = Mathf.Clamp(id, 0, EmoteCount - 1);
            if (emoteIcons[id] != null)
            {
                return emoteIcons[id];
            }

            const int size = 96;
            Color32[] pixels = new Color32[size * size];
            Color32 ink = GlyphColors[id];
            if (id <= 3)
            {
                DrawArrowIcon(pixels, size, id, ink);
            }
            else
            {
                DrawSpeciesIcon(pixels, size, id, ink);
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Emote Picture " + (id + 1),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            emoteIcons[id] = sprite;
            return sprite;
        }

        private static void DrawArrowIcon(Color32[] pixels, int size, int id, Color32 color)
        {
            Vector2Int tail;
            Vector2Int tip;
            Vector2Int headA;
            Vector2Int headB;
            if (id == 0)
            {
                tail = new Vector2Int(48, 17); tip = new Vector2Int(48, 79);
                headA = new Vector2Int(28, 57); headB = new Vector2Int(68, 57);
            }
            else if (id == 1)
            {
                tail = new Vector2Int(48, 79); tip = new Vector2Int(48, 17);
                headA = new Vector2Int(28, 39); headB = new Vector2Int(68, 39);
            }
            else if (id == 2)
            {
                tail = new Vector2Int(17, 48); tip = new Vector2Int(79, 48);
                headA = new Vector2Int(57, 68); headB = new Vector2Int(57, 28);
            }
            else
            {
                tail = new Vector2Int(79, 48); tip = new Vector2Int(17, 48);
                headA = new Vector2Int(39, 68); headB = new Vector2Int(39, 28);
            }
            DrawLine(pixels, size, tail, tip, 4, color);
            DrawLine(pixels, size, tip, headA, 4, color);
            DrawLine(pixels, size, tip, headB, 4, color);
        }

        private static void DrawSpeciesIcon(Color32[] pixels, int size, int id, Color32 color)
        {
            if (id == 4) // Human
            {
                DrawCircle(pixels, size, new Vector2Int(48, 72), 11, 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 60), new Vector2Int(48, 34), 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 53), new Vector2Int(27, 43), 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 53), new Vector2Int(69, 43), 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 34), new Vector2Int(33, 15), 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 34), new Vector2Int(63, 15), 3, color);
                return;
            }
            if (id == 5) // Cat
            {
                Vector2Int[] face =
                {
                    new Vector2Int(24, 57), new Vector2Int(27, 79), new Vector2Int(41, 69),
                    new Vector2Int(55, 69), new Vector2Int(69, 79), new Vector2Int(72, 57),
                    new Vector2Int(66, 31), new Vector2Int(48, 23), new Vector2Int(30, 31),
                    new Vector2Int(24, 57)
                };
                DrawPolyline(pixels, size, face, 3, color);
                DrawDot(pixels, size, 39, 50, 3, color);
                DrawDot(pixels, size, 57, 50, 3, color);
                DrawLine(pixels, size, new Vector2Int(48, 43), new Vector2Int(48, 38), 2, color);
                DrawLine(pixels, size, new Vector2Int(34, 39), new Vector2Int(15, 43), 2, color);
                DrawLine(pixels, size, new Vector2Int(62, 39), new Vector2Int(81, 43), 2, color);
                return;
            }
            if (id == 6) // Bird
            {
                DrawEllipse(pixels, size, new Vector2Int(45, 47), 27, 21, 3, color);
                DrawCircle(pixels, size, new Vector2Int(63, 65), 12, 3, color);
                DrawPolyline(pixels, size, new[]
                {
                    new Vector2Int(75, 67), new Vector2Int(88, 61),
                    new Vector2Int(75, 57), new Vector2Int(75, 67)
                }, 3, color);
                DrawPolyline(pixels, size, new[]
                {
                    new Vector2Int(28, 50), new Vector2Int(46, 58), new Vector2Int(51, 39)
                }, 3, color);
                DrawDot(pixels, size, 66, 69, 2, color);
                DrawLine(pixels, size, new Vector2Int(39, 26), new Vector2Int(36, 15), 2, color);
                DrawLine(pixels, size, new Vector2Int(52, 27), new Vector2Int(55, 15), 2, color);
                return;
            }
            if (id == 7) // Turtle
            {
                DrawEllipse(pixels, size, new Vector2Int(44, 49), 29, 24, 3, color);
                DrawCircle(pixels, size, new Vector2Int(76, 52), 10, 3, color);
                DrawLine(pixels, size, new Vector2Int(26, 34), new Vector2Int(18, 22), 3, color);
                DrawLine(pixels, size, new Vector2Int(57, 32), new Vector2Int(64, 20), 3, color);
                DrawLine(pixels, size, new Vector2Int(44, 72), new Vector2Int(44, 27), 2, color);
                DrawLine(pixels, size, new Vector2Int(18, 49), new Vector2Int(69, 49), 2, color);
                DrawDot(pixels, size, 79, 55, 2, color);
                return;
            }

            // Slime
            DrawPolyline(pixels, size, new[]
            {
                new Vector2Int(18, 25), new Vector2Int(18, 43), new Vector2Int(24, 61),
                new Vector2Int(35, 72), new Vector2Int(48, 77), new Vector2Int(61, 72),
                new Vector2Int(72, 61), new Vector2Int(78, 43), new Vector2Int(78, 25),
                new Vector2Int(18, 25)
            }, 3, color);
            DrawDot(pixels, size, 38, 49, 3, color);
            DrawDot(pixels, size, 58, 49, 3, color);
            DrawPolyline(pixels, size, new[]
            {
                new Vector2Int(37, 38), new Vector2Int(43, 34),
                new Vector2Int(50, 34), new Vector2Int(58, 39)
            }, 2, color);
        }

        private static void DrawPolyline(Color32[] pixels, int size, Vector2Int[] points, int radius, Color32 color)
        {
            for (int i = 1; i < points.Length; i++) DrawLine(pixels, size, points[i - 1], points[i], radius, color);
        }

        private static void DrawCircle(Color32[] pixels, int size, Vector2Int center, int radius, int thickness, Color32 color)
        {
            DrawEllipse(pixels, size, center, radius, radius, thickness, color);
        }

        private static void DrawEllipse(Color32[] pixels, int size, Vector2Int center, int radiusX, int radiusY, int thickness, Color32 color)
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

        private static void DrawLine(Color32[] pixels, int size, Vector2Int from, Vector2Int to, int radius, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps > 0 ? i / (float)steps : 0f;
                DrawDot(pixels, size, Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)), radius, color);
            }
        }

        private static void DrawDot(Color32[] pixels, int size, int x, int y, int radius, Color32 color)
        {
            int radiusSqr = radius * radius;
            for (int py = y - radius; py <= y + radius; py++)
            {
                if (py < 0 || py >= size) continue;
                for (int px = x - radius; px <= x + radius; px++)
                {
                    if (px < 0 || px >= size || (px - x) * (px - x) + (py - y) * (py - y) > radiusSqr) continue;
                    pixels[py * size + px] = color;
                }
            }
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
