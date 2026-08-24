using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Builds a brand-focused, game-authentic Steam header composition and
    /// renders it directly at Steam's 920x430 header-capsule resolution.
    /// </summary>
    public sealed class SteamHeaderCaptureController : MonoBehaviour
    {
        private const int CaptureWidth = 920;
        private const int CaptureHeight = 430;
        private const float CaptureAspect = CaptureWidth / (float)CaptureHeight;

        private StageManager stageManager;
        private GameObject sourcePlayer;
        private CameraFollow2D cameraFollow;
        private DrawManager drawManager;
        private Camera captureCamera;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private float previousCameraAspect;
        private Color previousBackgroundColor;
        private DrawManager.DrawingState originalDrawingState;
        private bool sceneRestored;
        private Text statusLabel;

        public void Configure(
            StageManager manager,
            GameObject playerObject,
            CameraFollow2D follow,
            DrawManager drawingManager)
        {
            stageManager = manager;
            sourcePlayer = playerObject;
            cameraFollow = follow;
            drawManager = drawingManager;
            captureCamera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
            if (drawManager != null)
            {
                originalDrawingState = drawManager.CreateState();
            }

            ConfigureCamera();
            BuildPaperBackdrop();
            BuildMarketingStage();
            BuildFourActualCharacters();
            BuildBrandLogo();
            CreateCaptureUi();
            if (sourcePlayer != null)
            {
                sourcePlayer.SetActive(false);
            }
            Physics2D.SyncTransforms();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ExportHeader();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                stageManager?.ExitSteamHeaderCapture();
            }
        }

        public void RestoreScene()
        {
            if (sceneRestored)
            {
                return;
            }
            sceneRestored = true;
            RestoreDrawTarget();
            if (sourcePlayer != null)
            {
                sourcePlayer.SetActive(true);
            }
            if (captureCamera != null)
            {
                captureCamera.transform.position = previousCameraPosition;
                captureCamera.orthographicSize = previousCameraSize;
                captureCamera.aspect = previousCameraAspect;
                captureCamera.backgroundColor = previousBackgroundColor;
            }
            if (cameraFollow != null)
            {
                cameraFollow.enabled = true;
            }
        }

        private void ConfigureCamera()
        {
            if (captureCamera == null)
            {
                return;
            }
            previousCameraPosition = captureCamera.transform.position;
            previousCameraSize = captureCamera.orthographicSize;
            previousCameraAspect = captureCamera.aspect;
            previousBackgroundColor = captureCamera.backgroundColor;
            captureCamera.transform.position = new Vector3(0f, 0.15f, -10f);
            captureCamera.orthographicSize = 4.35f;
            captureCamera.aspect = CaptureAspect;
            captureCamera.backgroundColor = new Color(0.98f, 0.95f, 0.83f, 1f);
            if (cameraFollow != null)
            {
                cameraFollow.enabled = false;
            }
        }

        private void BuildPaperBackdrop()
        {
            Sprite square = GetSquareSprite();
            GameObject paper = new GameObject("Capsule Paper");
            paper.transform.SetParent(transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, 2f);
            paper.transform.localScale = new Vector3(20f, 9f, 1f);
            SpriteRenderer paperRenderer = paper.AddComponent<SpriteRenderer>();
            paperRenderer.sprite = square;
            paperRenderer.color = new Color(1f, 0.965f, 0.83f, 1f);
            paperRenderer.sortingOrder = -120;

            for (int i = -5; i <= 5; i++)
            {
                float y = i * 0.72f;
                AddLine(
                    "Notebook Rule",
                    new[] { new Vector2(-10f, y), new Vector2(10f, y) },
                    0.025f,
                    new Color(0.24f, 0.68f, 0.86f, 0.24f),
                    -110);
            }
            AddLine(
                "Notebook Margin",
                new[] { new Vector2(-8.65f, -4.5f), new Vector2(-8.65f, 4.5f) },
                0.04f,
                new Color(0.95f, 0.35f, 0.42f, 0.28f),
                -109);

            CreateCrayonBlob(new Vector2(-5.25f, 0.45f), new Vector2(6.2f, 5.6f), new Color(1f, 0.76f, 0.2f, 0.18f), -105);
            CreateCrayonBlob(new Vector2(3.25f, 0.1f), new Vector2(8.6f, 6.7f), new Color(0.18f, 0.72f, 0.96f, 0.13f), -104);
            AddDoodleStar(new Vector2(-8.05f, 2.9f), 0.45f, new Color(1f, 0.46f, 0.18f, 0.75f));
            AddDoodleStar(new Vector2(-3.15f, -2.55f), 0.32f, new Color(0.2f, 0.72f, 0.9f, 0.7f));
            AddDoodleStar(new Vector2(7.6f, 2.9f), 0.38f, new Color(0.96f, 0.58f, 0.18f, 0.7f));
        }

        private void BuildMarketingStage()
        {
            StageObjectFactory factory = FindFirstObjectByType<StageObjectFactory>();
            if (factory == null)
            {
                factory = gameObject.AddComponent<StageObjectFactory>();
            }
            Transform stage = new GameObject("Capsule Gameplay Objects").transform;
            stage.SetParent(transform, false);

            // Keep gameplay on the right so the real title logo stays readable.
            CreateStageObject(factory, stage, StageObjectType.Platform, new Vector2(2.75f, -2.75f), new Vector2(9.65f, 0.55f), 0f);
            CreateStageObject(factory, stage, StageObjectType.Platform, new Vector2(3.25f, -1.05f), new Vector2(2.75f, 0.42f), 0f);
            CreateStageObject(factory, stage, StageObjectType.Wall, new Vector2(7.35f, -0.2f), new Vector2(0.5f, 5.35f), 0f);
            CreateStageObject(factory, stage, StageObjectType.Spike, new Vector2(5.55f, -2.28f), new Vector2(1.25f, 0.72f), 0f);
        }

        private static void CreateStageObject(
            StageObjectFactory factory,
            Transform parent,
            StageObjectType type,
            Vector2 position,
            Vector2 size,
            float rotation)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = "capsule_" + type + "_" + Guid.NewGuid().ToString("N");
            data.size = size;
            data.rotation = rotation;
            factory.Create(data, parent);
        }

        private void BuildFourActualCharacters()
        {
            if (sourcePlayer == null)
            {
                return;
            }
            DrawManager.DrawingState preset = CharacterDrawingPresetStore.Load(
                Mathf.Clamp(PlayerPrefs.GetInt("trailer_character_preset", 0), 0, CharacterDrawingPresetStore.SlotCount - 1));
            DrawManager.DrawingState state = preset ?? originalDrawingState;
            Transform actors = new GameObject("Capsule Four Players").transform;
            actors.SetParent(transform, false);

            GameObject human = CreateActualCharacter(actors, DrawManager.Species.Human, 0, state);
            GameObject bird = CreateActualCharacter(actors, DrawManager.Species.Bird, 2, state);
            GameObject cat = CreateActualCharacter(actors, DrawManager.Species.Cat, 1, state);
            GameObject slime = CreateActualCharacter(actors, DrawManager.Species.Slime, 3, state);

            ScaleActor(human, 1.12f);
            ScaleActor(bird, 1.12f);
            ScaleActor(cat, 1.12f);
            ScaleActor(slime, 1.12f);

            PlaceOnSurface(human, -0.75f, -2.43f);
            PlaceAt(bird, new Vector2(2.35f, 0.15f), -4f);
            PlaceOnActor(cat, bird, 0.15f);
            PlaceAt(slime, new Vector2(6.75f, 0.35f), 0f);
        }

        private GameObject CreateActualCharacter(
            Transform parent,
            DrawManager.Species species,
            int colorIndex,
            DrawManager.DrawingState state)
        {
            GameObject instance = Instantiate(sourcePlayer, parent);
            instance.name = "Capsule " + species;
            instance.SetActive(true);
            PlayerController2D controller = instance.GetComponent<PlayerController2D>();
            PlayerAbilityController abilities = instance.GetComponent<PlayerAbilityController>();
            BodyBuilder builder = instance.GetComponent<BodyBuilder>();
            if (drawManager != null && state != null && builder != null && abilities != null)
            {
                drawManager.SetBuildTarget(builder, abilities);
                drawManager.LoadState(CloneStateForSpecies(state, species), true);
            }
            builder?.SetPlayerColor(PlayerColorPalette.GetColor(colorIndex));
            Transform marker = instance.transform.Find("Controlled Player Marker");
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }
            Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            controller?.SetControlsEnabled(false);
            return instance;
        }

        private void BuildBrandLogo()
        {
            Sprite titleLogo = FindTitleLogoSprite();
            if (titleLogo == null)
            {
                Debug.LogError("Steam header capture could not find the TitleNicoDrowLogo sprite.");
                return;
            }

            GameObject root = new GameObject("Title Logo (same sprite as title screen)");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-4.7f, 0.45f, 0f);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = titleLogo;
            renderer.color = Color.white;
            renderer.sortingOrder = 80;

            float sourceWidth = Mathf.Max(0.01f, titleLogo.bounds.size.x);
            // The PNG intentionally contains generous transparent padding. A ten-unit
            // sprite width makes the painted logo itself occupy roughly one third of
            // the 920 px capsule without stretching or cropping the artwork.
            float uniformScale = 10f / sourceWidth;
            root.transform.localScale = Vector3.one * uniformScale;
        }

        private static Sprite FindTitleLogoSprite()
        {
            Image[] images = Resources.FindObjectsOfTypeAll<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.name == "TitleNicoDrowLogo" && image.sprite != null)
                {
                    return image.sprite;
                }
            }

            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == "NICO_DROW")
                {
                    return sprites[i];
                }
            }
            return null;
        }

        private void CreateCaptureUi()
        {
            GameObject canvasObject = new GameObject("Steam Header Capture UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Font font = FindFirstObjectByType<Text>()?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform panel = CreateUiRect("Panel", canvasObject.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(980f, 112f));
            panel.pivot = new Vector2(0.5f, 0f);
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.98f, 0.95f, 0.83f, 0.95f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.05f, 0.04f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            statusLabel = CreateUiText("Status", panel, LocalizationManager.T("steam_header_ready"), font, 21, new Vector2(0f, 32f), new Vector2(900f, 34f));
            CreateUiButton("Capture", panel, LocalizationManager.T("steam_header_capture") + "  [F12]", font, new Vector2(-185f, -17f), new Vector2(510f, 48f), new Color(0.96f, 0.46f, 0.7f, 1f), ExportHeader);
            CreateUiButton("Exit", panel, LocalizationManager.T("steam_header_exit") + "  [ESC]", font, new Vector2(300f, -17f), new Vector2(280f, 48f), new Color(1f, 0.72f, 0.3f, 1f), () => stageManager?.ExitSteamHeaderCapture());
        }

        private void ExportHeader()
        {
            if (captureCamera == null)
            {
                return;
            }
            RenderTexture renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = captureCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D image = null;
            try
            {
                captureCamera.targetTexture = renderTexture;
                captureCamera.aspect = CaptureAspect;
                captureCamera.Render();
                RenderTexture.active = renderTexture;
                image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, false);
                image.Apply(false, false);

                string directory = GetCaptureDirectory();
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "NICO_DROW_header_capsule_920x430.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                if (statusLabel != null)
                {
                    statusLabel.text = LocalizationManager.Format("steam_header_saved", path);
                }
                Debug.Log("Steam header capsule saved: " + path);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not export Steam header capsule: " + exception.Message);
                if (statusLabel != null)
                {
                    statusLabel.text = exception.Message;
                }
            }
            finally
            {
                captureCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null) Destroy(image);
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        private static string GetCaptureDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(Directory.GetCurrentDirectory(), "SteamCaptures");
#else
            return Path.Combine(Application.persistentDataPath, "SteamCaptures");
#endif
        }

        private void RestoreDrawTarget()
        {
            if (drawManager == null || sourcePlayer == null)
            {
                return;
            }
            drawManager.SetBuildTarget(
                sourcePlayer.GetComponent<BodyBuilder>(),
                sourcePlayer.GetComponent<PlayerAbilityController>());
            if (originalDrawingState != null)
            {
                drawManager.LoadState(originalDrawingState, true);
            }
        }

        private static DrawManager.DrawingState CloneStateForSpecies(
            DrawManager.DrawingState source,
            DrawManager.Species species)
        {
            DrawManager.DrawingState clone = new DrawManager.DrawingState { Species = species, Part = source.Part };
            foreach (KeyValuePair<DrawManager.Species, Dictionary<DrawManager.BodyPart, List<Vector2>>> speciesPair in source.Points)
            {
                Dictionary<DrawManager.BodyPart, List<Vector2>> parts = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
                foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> partPair in speciesPair.Value)
                {
                    parts[partPair.Key] = partPair.Value != null ? new List<Vector2>(partPair.Value) : new List<Vector2>();
                }
                clone.Points[speciesPair.Key] = parts;
            }
            return clone;
        }

        private static void PlaceAt(GameObject actor, Vector2 position, float rotation)
        {
            if (actor == null) return;
            actor.transform.position = position;
            actor.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
        }

        private static void ScaleActor(GameObject actor, float scale)
        {
            if (actor == null) return;
            actor.transform.localScale = actor.transform.localScale * scale;
            Physics2D.SyncTransforms();
        }

        private static void PlaceOnActor(GameObject rider, GameObject carrier, float gap)
        {
            if (rider == null || carrier == null) return;
            Physics2D.SyncTransforms();
            Bounds riderBounds = GetBounds(rider);
            Bounds carrierBounds = GetBounds(carrier);
            Vector3 position = rider.transform.position;
            position.x += carrierBounds.center.x - riderBounds.center.x;
            position.y += carrierBounds.max.y + gap - riderBounds.min.y;
            PlaceAt(rider, position, 0f);
        }

        private static void PlaceOnSurface(GameObject actor, float x, float surfaceY)
        {
            if (actor == null) return;
            Bounds bounds = GetBounds(actor);
            Vector3 position = actor.transform.position;
            position.x = x;
            position.y += surfaceY - bounds.min.y + 0.035f;
            PlaceAt(actor, position, 0f);
        }

        private static Bounds GetBounds(GameObject root)
        {
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(false);
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
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

        private static Sprite GetSquareSprite()
        {
            return DoodleRuntimeAssets.SquareSprite;
        }

        private void CreateCrayonBlob(Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            GameObject blob = new GameObject("Crayon Color Wash");
            blob.transform.SetParent(transform, false);
            blob.transform.localPosition = position;
            blob.transform.localScale = size;
            SpriteRenderer renderer = blob.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private void AddDoodleStar(Vector2 center, float radius, Color color)
        {
            Vector2[] points = new Vector2[11];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float currentRadius = i % 2 == 0 ? radius : radius * 0.42f;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentRadius;
            }
            points[10] = points[0];
            AddLine("Doodle Star", points, 0.055f, color, -90);
        }

        private void AddLine(string name, Vector2[] points, float width, Color color, int sortingOrder)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform, false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private static RectTransform CreateUiRect(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = root.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text CreateUiText(string name, RectTransform parent, string value, Font font, int fontSize, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateUiRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void CreateUiButton(
            string name,
            RectTransform parent,
            string label,
            Font font,
            Vector2 position,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateUiRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.07f, 0.06f, 0.05f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            CreateUiText("Label", rect, label, font, 22, Vector2.zero, size);
        }
    }
}
