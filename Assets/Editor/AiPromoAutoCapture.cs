using System;
using System.Collections.Generic;
using System.IO;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static class AiPromoAutoCapture
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        private const string OutputDir = @"C:\Users\mizut\.gemini\antigravity-ide\brain\4e135370-f0f3-4495-bb84-494e6ebd2ebd\promo_shots";
        private const string MainOutputPath = @"C:\Users\mizut\.gemini\antigravity-ide\brain\4e135370-f0f3-4495-bb84-494e6ebd2ebd\ai_generated_promo_4players.png";

        private static Material lineMat;

        private static Material GetLineMaterial()
        {
            if (lineMat == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                lineMat = new Material(shader);
            }
            return lineMat;
        }

        private static void AddLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, int order)
        {
            GameObject obj = new GameObject("Line");
            obj.transform.SetParent(parent, false);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = order;
            lr.sharedMaterial = GetLineMaterial();
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }

        [MenuItem("Tools/PICO/Execute Real Gameplay & DRAW Capture")]
        public static void Run()
        {
            Debug.Log("[AiPromoCapture] Starting Authentic Real Gameplay & DRAW Screens Capture...");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            DrawManager drawManager = UnityEngine.Object.FindFirstObjectByType<DrawManager>();
            StageManager stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>();
            StageLoader stageLoader = UnityEngine.Object.FindFirstObjectByType<StageLoader>();
            PlayerController2D sourcePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController2D>();
            UIManager uiManager = UnityEngine.Object.FindFirstObjectByType<UIManager>();
            StageObjectFactory objectFactory = UnityEngine.Object.FindFirstObjectByType<StageObjectFactory>();

            if (drawManager == null || sourcePlayer == null || stageLoader == null)
            {
                Debug.LogError("[AiPromoCapture] Missing required components in GameScene!");
                return;
            }

            sourcePlayer.gameObject.SetActive(false);

            // Camera setup
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject camObj = new GameObject("CaptureCamera");
                camera = camObj.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.aspect = (float)CaptureWidth / CaptureHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;

            CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
            if (follow != null) follow.enabled = false;

            // Prepare RenderTexture
            RenderTexture rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = camera.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            camera.targetTexture = rt;

            Texture2D screenShot = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);

            // Configure Canvases to render onto camera so UI/HUD is fully visible in FHD
            ConfigureCanvasesForCamera(camera);

            // =========================================================================
            // PART 3: DRAW SCREEN CAPTURES (Actual in-game character creation screen)
            // =========================================================================
            CaptureDrawScreen(drawManager, uiManager, stageLoader, camera, rt, screenShot,
                DrawManager.Species.Human, BuildLongLegHumanState(), "promo_3_draw_human_longlegs.png");
            CaptureDrawScreen(drawManager, uiManager, stageLoader, camera, rt, screenShot,
                DrawManager.Species.Cat, BuildBowlHeadCatState(), "promo_3_draw_cat_bowlhead.png");
            CaptureDrawScreen(drawManager, uiManager, stageLoader, camera, rt, screenShot,
                DrawManager.Species.Slime, BuildMuscleHornSlimeState(), "promo_3_draw_slime_muscles.png");

            // =========================================================================
            // PART 2: REAL IN-GAME PLAY ACTION CAPTURES (with authentic in-game HUD)
            // =========================================================================
            // Close DRAW panel, enable game HUD
            if (drawManager != null) drawManager.SetActive(false);
            if (uiManager != null)
            {
                uiManager.gameObject.SetActive(true);
                uiManager.SetDrawing(false);
            }

            string[] actionStages = new string[] { "1-1", "2-2", "5-1", "9-2", "8-2", "3-1" };
            for (int i = 0; i < actionStages.Length; i++)
            {
                CaptureActionGameplay(actionStages[i], stageLoader, drawManager, sourcePlayer, objectFactory, camera, rt, screenShot);
            }

            camera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(screenShot);

            Debug.Log($"[AiPromoCapture] All Authentic Gameplay and DRAW captures completed in: {OutputDir}");
        }

        private static void ConfigureCanvasesForCamera(Camera cam)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 5f;
            }
        }

        private static GameObject FindCanvasPanel(string name)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Transform t = canvases[i].transform.Find(name);
                if (t != null) return t.gameObject;
            }
            return GameObject.Find(name);
        }

        private static void HideAllPanels()
        {
            string[] panelNames = new string[] {
                "TitlePanel", "StageSelectPanel", "MultiPanel", "OptionPanel",
                "MenuPanel", "StageEditorPanel", "ClearPanel", "DrawPanel",
                "GameplayHudPanel", "StageSelectLockedPanel"
            };
            for (int i = 0; i < panelNames.Length; i++)
            {
                GameObject obj = FindCanvasPanel(panelNames[i]);
                if (obj != null) obj.SetActive(false);
            }
        }

        private static void CaptureDrawScreen(
            DrawManager drawManager,
            UIManager uiManager,
            StageLoader stageLoader,
            Camera camera,
            RenderTexture rt,
            Texture2D screenShot,
            DrawManager.Species species,
            DrawManager.DrawingState state,
            string filename)
        {
            // 1. Hide all UI panels except DrawPanel
            HideAllPanels();

            // Hide stage roots so stage geometry does not show during DRAW screen
            GameObject debugStage = GameObject.Find("DebugStageRoot");
            if (debugStage != null) debugStage.SetActive(false);
            GameObject runtimeStage = GameObject.Find("RuntimeStageRoot");
            if (runtimeStage != null) runtimeStage.SetActive(false);

            // Set natural warm notebook paper color
            camera.backgroundColor = new Color(0.98f, 0.965f, 0.91f);

            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographicSize = 5.4f;

            GameObject drawPanelObj = FindCanvasPanel("DrawPanel");
            if (drawPanelObj != null)
            {
                drawPanelObj.SetActive(true);
            }

            if (drawManager != null)
            {
                drawManager.gameObject.SetActive(true);
                drawManager.SetSpecies(species);
                drawManager.LoadState(state, true);
                drawManager.SetCurrentPart(DrawManager.BodyPart.Torso);
            }

            Physics2D.SyncTransforms();

            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            screenShot.Apply();

            byte[] bytes = screenShot.EncodeToPNG();
            string outPath = Path.Combine(OutputDir, filename);
            File.WriteAllBytes(outPath, bytes);
            Debug.Log($"[AiPromoCapture] Captured DRAW Screen: {outPath}");
        }

        private static void CaptureActionGameplay(
            string stageId,
            StageLoader stageLoader,
            DrawManager drawManager,
            PlayerController2D sourcePlayer,
            StageObjectFactory objectFactory,
            Camera camera,
            RenderTexture rt,
            Texture2D screenShot)
        {
            // 1. Hide all other UI panels, enable authentic gameplay HUD
            HideAllPanels();

            GameObject hudObj = FindCanvasPanel("GameplayHudPanel");
            if (hudObj != null)
            {
                hudObj.SetActive(true);
            }

            GameObject runtimeStage = GameObject.Find("RuntimeStageRoot");
            if (runtimeStage != null) runtimeStage.SetActive(true);

            TextAsset asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset == null) return;
            StageData stageData = JsonUtility.FromJson<StageData>(asset.text);
            if (stageData == null) return;

            // 1. Load actual stage
            stageLoader.LoadStage(stageData);
            Color paperColor = StageBackgroundAppearance.Parse(stageData.backgroundColorHex);
            camera.backgroundColor = paperColor;
            StageBackgroundAppearance.Apply(paperColor);

            // 2. Find anchor
            Vector2 spawnPos = Vector2.zero;
            if (stageData.objects != null)
            {
                for (int i = 0; i < stageData.objects.Length; i++)
                {
                    var obj = stageData.objects[i];
                    if (obj != null && obj.type == StageObjectType.Spawn)
                    {
                        spawnPos = obj.position;
                        break;
                    }
                }
            }

            // Adjust spawnPos so players land on solid floor (avoid falling into chasms)
            float bx = spawnPos.x >= 0 ? spawnPos.x : (spawnPos.x + 3.5f);
            float by = spawnPos.y;

            // 3. Spawn 4 characters
            GameObject playersRoot = new GameObject($"ActionPlayers_{stageId}");

            // 1P: Human (Running / leaping forward)
            GameObject p1 = CreateCharacter(sourcePlayer.gameObject, playersRoot.transform, DrawManager.Species.Human, 0, BuildLongLegHumanState(), drawManager);
            p1.name = "P1_Human_LongLeg";
            p1.transform.position = new Vector2(bx - 3.2f, by + 1.2f);
            p1.transform.localScale = Vector3.one * 1.15f;

            // 2P: Cat (Huge Bowl Head with a bomb resting in head)
            GameObject p2 = CreateCharacter(sourcePlayer.gameObject, playersRoot.transform, DrawManager.Species.Cat, 1, BuildBowlHeadCatState(), drawManager);
            p2.name = "P2_Cat_BowlHead";
            p2.transform.position = new Vector2(bx - 0.7f, by + 0.5f);
            p2.transform.localScale = Vector3.one * 1.25f;

            // Place bomb into P2's bowl head
            if (objectFactory != null)
            {
                StageObjectData bombData = StageObjectFactory.CreateDefaultData(StageObjectType.Bomb, new Vector2(bx - 0.7f, by + 1.7f));
                bombData.objectId = "action_bomb_" + stageId;
                bombData.size = new Vector2(1.25f, 1.25f);
                GameObject bomb = objectFactory.Create(bombData, playersRoot.transform);
                if (bomb != null)
                {
                    Rigidbody2D bb = bomb.GetComponent<Rigidbody2D>();
                    if (bb != null) bb.bodyType = RigidbodyType2D.Dynamic;
                }
            }

            // 3P: Bird (Giant Wing flapping mid-air)
            GameObject p3 = CreateCharacter(sourcePlayer.gameObject, playersRoot.transform, DrawManager.Species.Bird, 2, BuildGiantWingBirdState(), drawManager);
            p3.name = "P3_Bird_GiantWing";
            p3.transform.position = new Vector2(bx + 1.8f, by + 1.8f);
            p3.transform.rotation = Quaternion.Euler(0f, 0f, -15f);
            p3.transform.localScale = Vector3.one * 1.3f;

            // 4P: Slime (Muscular Horn Slime bouncing)
            GameObject p4 = CreateCharacter(sourcePlayer.gameObject, playersRoot.transform, DrawManager.Species.Slime, 3, BuildMuscleHornSlimeState(), drawManager);
            p4.name = "P4_Slime_Muscle";
            p4.transform.position = new Vector2(bx + 3.2f, by + 0.8f);
            p4.transform.localScale = Vector3.one * 1.2f;

            // 4. Set live gameplay velocities: Sprinting, leaping, carrying, and flapping!
            Rigidbody2D rb1 = p1.GetComponent<Rigidbody2D>();
            Rigidbody2D rb2 = p2.GetComponent<Rigidbody2D>();
            Rigidbody2D rb3 = p3.GetComponent<Rigidbody2D>();
            Rigidbody2D rb4 = p4.GetComponent<Rigidbody2D>();

            // Initial landing settle
            Physics2D.simulationMode = SimulationMode2D.Script;
            for (int step = 0; step < 15; step++)
            {
                Physics2D.Simulate(0.02f);
            }

            // Apply live action velocities (1P sprinting, 2P leaping with bomb, 3P gliding, 4P bouncing)
            if (rb1 != null) rb1.linearVelocity = new Vector2(7.8f, 1.2f);
            if (rb2 != null) rb2.linearVelocity = new Vector2(6.2f, 11.5f); // High leap with bomb!
            if (rb3 != null) rb3.linearVelocity = new Vector2(4.5f, 5.0f);  // Mid-air glide!
            if (rb4 != null) rb4.linearVelocity = new Vector2(5.5f, 7.5f);  // Bouncing!

            // Simulate forward into the dynamic leap & sprint frame
            for (int step = 0; step < 14; step++)
            {
                Physics2D.Simulate(0.02f);
            }

            // Add overhead markers tracking active characters in action
            AddPlayerMarker(playersRoot.transform, (Vector2)p1.transform.position + new Vector2(0f, 2.7f), "1P [YOU]", new Color(0.95f, 0.28f, 0.28f));
            AddPlayerMarker(playersRoot.transform, (Vector2)p2.transform.position + new Vector2(0f, 2.2f), "2P", new Color(0.25f, 0.55f, 0.95f));
            AddPlayerMarker(playersRoot.transform, (Vector2)p3.transform.position + new Vector2(0f, 2.0f), "3P", new Color(0.25f, 0.85f, 0.35f));
            AddPlayerMarker(playersRoot.transform, (Vector2)p4.transform.position + new Vector2(0f, 1.8f), "4P", new Color(0.95f, 0.75f, 0.2f));

            // Dynamic closer camera framing focusing on the live action
            camera.transform.position = new Vector3(bx + 0.6f, by + 1.8f, -10f);
            camera.orthographicSize = 5.2f;

            Physics2D.SyncTransforms();

            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            screenShot.Apply();

            byte[] bytes = screenShot.EncodeToPNG();
            string outPath = Path.Combine(OutputDir, $"real_gameplay_live_stage_{stageId}.png");
            File.WriteAllBytes(outPath, bytes);

            string legacyPath = Path.Combine(OutputDir, $"promo_2_action_stage_{stageId}.png");
            File.WriteAllBytes(legacyPath, bytes);

            if (stageId == "1-1")
            {
                File.WriteAllBytes(MainOutputPath, bytes);
            }

            Debug.Log($"[AiPromoCapture] Captured Real Dynamic Gameplay Action: {outPath}");

            UnityEngine.Object.DestroyImmediate(playersRoot);
        }

        private static GameObject CreateCharacter(
            GameObject source,
            Transform parent,
            DrawManager.Species species,
            int colorIndex,
            DrawManager.DrawingState state,
            DrawManager drawManager)
        {
            GameObject instance = UnityEngine.Object.Instantiate(source, parent);
            instance.SetActive(true);

            PlayerController2D controller = instance.GetComponent<PlayerController2D>();
            PlayerAbilityController abilities = instance.GetComponent<PlayerAbilityController>();
            BodyBuilder builder = instance.GetComponent<BodyBuilder>();

            if (drawManager != null && state != null && builder != null && abilities != null)
            {
                drawManager.SetBuildTarget(builder, abilities);
                drawManager.LoadState(state, true);
            }

            builder?.SetPlayerColor(PlayerColorPalette.GetColor(colorIndex));

            Transform marker = instance.transform.Find("Controlled Player Marker");
            if (marker != null) marker.gameObject.SetActive(false);

            if (controller != null) controller.enabled = false;

            Rigidbody2D rb = instance.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 2.0f;
            }

            return instance;
        }

        private static void AddPlayerMarker(Transform parent, Vector2 pos, string labelText, Color color)
        {
            GameObject marker = new GameObject($"Marker_{labelText}");
            marker.transform.SetParent(parent, false);
            marker.transform.position = pos;

            // Marker Arrow
            AddLine(marker.transform, new Vector2(-0.25f, 0.35f), new Vector2(0f, 0.05f), 0.09f, color, 120);
            AddLine(marker.transform, new Vector2(0.25f, 0.35f), new Vector2(0f, 0.05f), 0.09f, color, 120);
            AddLine(marker.transform, new Vector2(-0.25f, 0.35f), new Vector2(0.25f, 0.35f), 0.09f, color, 120);

            // Text Label
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(marker.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = labelText;
            tm.fontSize = 32;
            tm.characterSize = 0.08f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;

            MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 125;
        }

        // --- 1P: Human (超ロングレッグ星人) ---
        private static DrawManager.DrawingState BuildLongLegHumanState()
        {
            DrawManager.DrawingState s = new DrawManager.DrawingState { Species = DrawManager.Species.Human, Part = DrawManager.BodyPart.Head };
            Dictionary<DrawManager.BodyPart, List<Vector2>> p = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            Dictionary<DrawManager.BodyPart, List<float>> w = new Dictionary<DrawManager.BodyPart, List<float>>();

            p[DrawManager.BodyPart.Head] = new List<Vector2>
            {
                new Vector2(-25f, 70f), new Vector2(25f, 70f), new Vector2(25f, 120f), new Vector2(-25f, 120f), new Vector2(-25f, 70f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-12f, 102f), new Vector2(-8f, 102f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(8f, 102f), new Vector2(12f, 102f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-12f, 85f), new Vector2(0f, 92f), new Vector2(12f, 85f)
            };
            w[DrawManager.BodyPart.Head] = new List<float> { 8f, 8f, 8f, 8f };

            p[DrawManager.BodyPart.Torso] = new List<Vector2> { new Vector2(0f, 70f), new Vector2(0f, 15f) };
            w[DrawManager.BodyPart.Torso] = new List<float> { 10f };

            p[DrawManager.BodyPart.LeftArm] = new List<Vector2> { new Vector2(0f, 50f), new Vector2(-40f, 30f) };
            w[DrawManager.BodyPart.LeftArm] = new List<float> { 8f };

            p[DrawManager.BodyPart.RightArm] = new List<Vector2> { new Vector2(0f, 50f), new Vector2(40f, 30f) };
            w[DrawManager.BodyPart.RightArm] = new List<float> { 8f };

            p[DrawManager.BodyPart.LeftLeg] = new List<Vector2>
            {
                new Vector2(0f, 15f), new Vector2(-25f, -60f), new Vector2(15f, -140f),
                new Vector2(-35f, -220f), new Vector2(20f, -320f), new Vector2(-30f, -440f), new Vector2(-70f, -520f)
            };
            w[DrawManager.BodyPart.LeftLeg] = new List<float> { 10f };

            p[DrawManager.BodyPart.RightLeg] = new List<Vector2>
            {
                new Vector2(0f, 15f), new Vector2(25f, -55f), new Vector2(-15f, -135f),
                new Vector2(35f, -215f), new Vector2(-10f, -310f), new Vector2(35f, -420f), new Vector2(75f, -510f)
            };
            w[DrawManager.BodyPart.RightLeg] = new List<float> { 10f };

            s.Points[DrawManager.Species.Human] = p;
            s.StrokeWidths[DrawManager.Species.Human] = w;
            return s;
        }

        // --- 2P: Cat (巨大お椀頭猫) ---
        private static DrawManager.DrawingState BuildBowlHeadCatState()
        {
            DrawManager.DrawingState s = new DrawManager.DrawingState { Species = DrawManager.Species.Cat, Part = DrawManager.BodyPart.Head };
            Dictionary<DrawManager.BodyPart, List<Vector2>> p = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            Dictionary<DrawManager.BodyPart, List<float>> w = new Dictionary<DrawManager.BodyPart, List<float>>();

            List<Vector2> head = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float angle = Mathf.PI + (Mathf.PI * i / 24f);
                float rx = 140f * Mathf.Cos(angle);
                float ry = 70f * 110f / 100f + 65f * Mathf.Sin(angle);
                head.Add(new Vector2(rx, ry));
            }
            head.Add(new Vector2(-140f, 75f));
            head.Add(new Vector2(float.NaN, float.NaN));
            head.Add(new Vector2(-130f, 75f)); head.Add(new Vector2(-110f, 115f)); head.Add(new Vector2(-90f, 75f));
            head.Add(new Vector2(float.NaN, float.NaN));
            head.Add(new Vector2(90f, 75f)); head.Add(new Vector2(110f, 115f)); head.Add(new Vector2(130f, 75f));
            head.Add(new Vector2(float.NaN, float.NaN));
            head.Add(new Vector2(-35f, 40f)); head.Add(new Vector2(-25f, 40f));
            head.Add(new Vector2(float.NaN, float.NaN));
            head.Add(new Vector2(25f, 40f)); head.Add(new Vector2(35f, 40f));
            head.Add(new Vector2(float.NaN, float.NaN));
            head.Add(new Vector2(-10f, 25f)); head.Add(new Vector2(0f, 18f)); head.Add(new Vector2(10f, 25f));

            p[DrawManager.BodyPart.Head] = head;
            w[DrawManager.BodyPart.Head] = new List<float> { 8f, 8f, 8f, 8f, 8f, 8f };

            p[DrawManager.BodyPart.Torso] = new List<Vector2> { new Vector2(0f, 15f), new Vector2(0f, -25f) };
            w[DrawManager.BodyPart.Torso] = new List<float> { 10f };

            p[DrawManager.BodyPart.LeftArm] = new List<Vector2> { new Vector2(-10f, 0f), new Vector2(-45f, 25f) };
            w[DrawManager.BodyPart.LeftArm] = new List<float> { 8f };

            p[DrawManager.BodyPart.RightArm] = new List<Vector2> { new Vector2(10f, 0f), new Vector2(45f, 25f) };
            w[DrawManager.BodyPart.RightArm] = new List<float> { 8f };

            p[DrawManager.BodyPart.LeftLeg] = new List<Vector2> { new Vector2(-15f, -25f), new Vector2(-20f, -65f) };
            w[DrawManager.BodyPart.LeftLeg] = new List<float> { 9f };

            p[DrawManager.BodyPart.RightLeg] = new List<Vector2> { new Vector2(15f, -25f), new Vector2(20f, -65f) };
            w[DrawManager.BodyPart.RightLeg] = new List<float> { 9f };

            s.Points[DrawManager.Species.Cat] = p;
            s.StrokeWidths[DrawManager.Species.Cat] = w;
            return s;
        }

        // --- 3P: Bird (アシンメトリー巨大片翼バード) ---
        private static DrawManager.DrawingState BuildGiantWingBirdState()
        {
            DrawManager.DrawingState s = new DrawManager.DrawingState { Species = DrawManager.Species.Bird, Part = DrawManager.BodyPart.Head };
            Dictionary<DrawManager.BodyPart, List<Vector2>> p = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            Dictionary<DrawManager.BodyPart, List<float>> w = new Dictionary<DrawManager.BodyPart, List<float>>();

            p[DrawManager.BodyPart.Head] = new List<Vector2>
            {
                new Vector2(-20f, 40f), new Vector2(0f, 65f), new Vector2(20f, 40f), new Vector2(0f, 20f), new Vector2(-20f, 40f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(20f, 45f), new Vector2(55f, 40f), new Vector2(20f, 32f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-5f, 48f), new Vector2(2f, 48f)
            };
            w[DrawManager.BodyPart.Head] = new List<float> { 8f, 9f, 6f };

            p[DrawManager.BodyPart.Torso] = new List<Vector2> { new Vector2(0f, 20f), new Vector2(-10f, -20f) };
            w[DrawManager.BodyPart.Torso] = new List<float> { 10f };

            p[DrawManager.BodyPart.LeftArm] = new List<Vector2> { new Vector2(-8f, 15f), new Vector2(-30f, 25f), new Vector2(-45f, 18f) };
            w[DrawManager.BodyPart.LeftArm] = new List<float> { 7f };

            p[DrawManager.BodyPart.RightArm] = new List<Vector2>
            {
                new Vector2(5f, 15f), new Vector2(50f, 50f), new Vector2(110f, 100f), new Vector2(170f, 140f),
                new Vector2(185f, 90f), new Vector2(150f, 60f), new Vector2(195f, 30f), new Vector2(130f, 10f),
                new Vector2(50f, 0f), new Vector2(5f, 15f)
            };
            w[DrawManager.BodyPart.RightArm] = new List<float> { 10f };

            p[DrawManager.BodyPart.LeftLeg] = new List<Vector2> { new Vector2(-15f, -20f), new Vector2(-20f, -55f), new Vector2(-30f, -60f) };
            w[DrawManager.BodyPart.LeftLeg] = new List<float> { 7f };

            p[DrawManager.BodyPart.RightLeg] = new List<Vector2> { new Vector2(-5f, -20f), new Vector2(0f, -55f), new Vector2(10f, -60f) };
            w[DrawManager.BodyPart.RightLeg] = new List<float> { 7f };

            s.Points[DrawManager.Species.Bird] = p;
            s.StrokeWidths[DrawManager.Species.Bird] = w;
            return s;
        }

        // --- 4P: Slime (極太マッスル角スライム) ---
        private static DrawManager.DrawingState BuildMuscleHornSlimeState()
        {
            DrawManager.DrawingState s = new DrawManager.DrawingState { Species = DrawManager.Species.Slime, Part = DrawManager.BodyPart.Head };
            Dictionary<DrawManager.BodyPart, List<Vector2>> p = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            Dictionary<DrawManager.BodyPart, List<float>> w = new Dictionary<DrawManager.BodyPart, List<float>>();

            p[DrawManager.BodyPart.Head] = new List<Vector2>
            {
                new Vector2(0f, 35f), new Vector2(35f, 20f), new Vector2(50f, -10f),
                new Vector2(35f, -35f), new Vector2(-35f, -35f), new Vector2(-50f, -10f),
                new Vector2(-35f, 20f), new Vector2(0f, 35f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(0f, 35f), new Vector2(8f, 75f), new Vector2(0f, 65f), new Vector2(-8f, 75f), new Vector2(0f, 35f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-20f, 5f), new Vector2(-10f, 0f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(20f, 5f), new Vector2(10f, 0f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-15f, -18f), new Vector2(0f, -12f), new Vector2(15f, -18f)
            };
            w[DrawManager.BodyPart.Head] = new List<float> { 9f, 9f, 7f, 7f, 8f };

            p[DrawManager.BodyPart.Torso] = new List<Vector2> { new Vector2(0f, -10f), new Vector2(0f, -35f) };
            w[DrawManager.BodyPart.Torso] = new List<float> { 12f };

            p[DrawManager.BodyPart.LeftArm] = new List<Vector2>
            {
                new Vector2(-35f, 0f), new Vector2(-75f, -5f), new Vector2(-110f, 35f),
                new Vector2(-85f, 75f), new Vector2(-60f, 55f), new Vector2(-75f, 25f), new Vector2(-35f, 0f)
            };
            w[DrawManager.BodyPart.LeftArm] = new List<float> { 12f };

            p[DrawManager.BodyPart.RightArm] = new List<Vector2>
            {
                new Vector2(35f, 0f), new Vector2(75f, -5f), new Vector2(110f, 35f),
                new Vector2(85f, 75f), new Vector2(60f, 55f), new Vector2(75f, 25f), new Vector2(35f, 0f)
            };
            w[DrawManager.BodyPart.RightArm] = new List<float> { 12f };

            p[DrawManager.BodyPart.LeftLeg] = new List<Vector2> { new Vector2(-20f, -35f), new Vector2(-30f, -50f) };
            w[DrawManager.BodyPart.LeftLeg] = new List<float> { 11f };

            p[DrawManager.BodyPart.RightLeg] = new List<Vector2> { new Vector2(20f, -35f), new Vector2(30f, -50f) };
            w[DrawManager.BodyPart.RightLeg] = new List<float> { 11f };

            s.Points[DrawManager.Species.Slime] = p;
            s.StrokeWidths[DrawManager.Species.Slime] = w;
            return s;
        }
    }
}
