using System.Collections.Generic;
using System.IO;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrawBody.EditorTools
{
    public static class RealGameplayCapture
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        private const string OutputDir = @"C:\Users\mizut\.gemini\antigravity-ide\brain\4e135370-f0f3-4495-bb84-494e6ebd2ebd\real_gameplay";

        [MenuItem("Tools/PICO/Capture Real Gameplay (Pure Authentic)")]
        public static void Run()
        {
            Debug.Log("[RealGameplayCapture] Opening GameScene...");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            StageManager stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>();
            PlayerController2D player = UnityEngine.Object.FindFirstObjectByType<PlayerController2D>();
            DrawManager drawManager = UnityEngine.Object.FindFirstObjectByType<DrawManager>();
            Camera camera = Camera.main;

            if (stageManager == null || player == null || camera == null)
            {
                Debug.LogError("[RealGameplayCapture] Required components missing in GameScene!");
                return;
            }

            // Ensure full-HD RenderTexture on main camera
            RenderTexture rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = camera.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            camera.targetTexture = rt;
            camera.aspect = (float)CaptureWidth / CaptureHeight;

            Texture2D screenShot = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);

            // Configure Canvases to render onto camera so UI/HUD is visible in RenderTexture
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int c = 0; c < canvases.Length; c++)
            {
                canvases[c].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[c].worldCamera = camera;
                canvases[c].planeDistance = 5f;
            }

            SimulationMode2D previousSimulationMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;

            // =========================================================================
            // 1. Actual DRAW Screen (Genuine game flow via stageManager.EnterDrawingMode)
            // =========================================================================
            Debug.Log("[RealGameplayCapture] Capturing authentic DRAW screen...");
            stageManager.SelectStage("1-1");
            stageManager.EnterDrawingMode();

            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            screenShot.Apply();
            File.WriteAllBytes(Path.Combine(OutputDir, "actual_draw_screen_human.png"), screenShot.EncodeToPNG());

            stageManager.ExitDrawingMode();

            // =========================================================================
            // 2. Play with Weird Character A: Ultra Long-Leg Human (超ロング脚人間)
            // =========================================================================
            Debug.Log("[RealGameplayCapture] Applying Long-Leg character to player...");
            if (drawManager != null)
            {
                drawManager.LoadState(BuildLongLegHumanState(), true);
            }

            CaptureStageAction("1-1", stageManager, player, camera, rt, screenShot, "longlegs");
            CaptureStageAction("1-2", stageManager, player, camera, rt, screenShot, "longlegs");

            // =========================================================================
            // 3. Play with Weird Character B: Bowl-Head Cat (お椀頭猫)
            // =========================================================================
            Debug.Log("[RealGameplayCapture] Applying Bowl-Head Cat to player...");
            if (drawManager != null)
            {
                drawManager.SetSpecies(DrawManager.Species.Cat);
                drawManager.LoadState(BuildBowlHeadCatState(), true);
            }

            CaptureStageAction("1-1", stageManager, player, camera, rt, screenShot, "cat");
            CaptureStageAction("2-1", stageManager, player, camera, rt, screenShot, "cat");

            // Restore camera and clean up
            camera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(screenShot);

                Debug.Log($"[RealGameplayCapture] All genuine gameplay captures saved to: {OutputDir}");
            }
            finally
            {
                // This setting survives after this edit-mode capture finishes.
                // Leaving it in Script mode makes normal gameplay assign body
                // velocity without advancing positions: characters walk in place.
                Physics2D.simulationMode = previousSimulationMode;
            }
        }

        private static void CaptureStageAction(
            string stageId,
            StageManager stageManager,
            PlayerController2D player,
            Camera camera,
            RenderTexture rt,
            Texture2D screenShot,
            string characterTag)
        {
            stageManager.SelectStage(stageId);

            // Settle on ground
            for (int step = 0; step < 20; step++)
            {
                Physics2D.Simulate(0.02f);
            }

            // Sprint forward
            player.SetScriptedInput(1.0f, false);
            for (int step = 0; step < 28; step++)
            {
                Physics2D.Simulate(0.02f);
            }

            camera.transform.position = new Vector3(player.transform.position.x + 1.8f, player.transform.position.y + 1.2f, -10f);
            camera.orthographicSize = 6.2f;
            Physics2D.SyncTransforms();

            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            screenShot.Apply();
            File.WriteAllBytes(Path.Combine(OutputDir, $"gameplay_stage_{stageId}_{characterTag}_sprint.png"), screenShot.EncodeToPNG());

            // Jump
            player.SetScriptedInput(1.0f, true, true);
            for (int step = 0; step < 16; step++)
            {
                Physics2D.Simulate(0.02f);
            }

            camera.transform.position = new Vector3(player.transform.position.x + 1.8f, player.transform.position.y + 1.2f, -10f);
            Physics2D.SyncTransforms();

            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            screenShot.Apply();
            File.WriteAllBytes(Path.Combine(OutputDir, $"gameplay_stage_{stageId}_{characterTag}_jump.png"), screenShot.EncodeToPNG());

            player.ClearScriptedInput();
        }

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
                new Vector2(-12f, 85f), new Vector2(0f, 80f), new Vector2(12f, 85f)
            };
            w[DrawManager.BodyPart.Head] = new List<float>();

            p[DrawManager.BodyPart.Torso] = new List<Vector2>
            {
                new Vector2(-18f, -20f), new Vector2(18f, -20f), new Vector2(18f, 70f), new Vector2(-18f, 70f), new Vector2(-18f, -20f)
            };
            w[DrawManager.BodyPart.Torso] = new List<float>();

            p[DrawManager.BodyPart.LeftArm] = new List<Vector2>
            {
                new Vector2(-18f, 50f), new Vector2(-55f, 30f), new Vector2(-80f, 55f)
            };
            w[DrawManager.BodyPart.LeftArm] = new List<float>();

            p[DrawManager.BodyPart.RightArm] = new List<Vector2>
            {
                new Vector2(18f, 50f), new Vector2(55f, 30f), new Vector2(80f, 55f)
            };
            w[DrawManager.BodyPart.RightArm] = new List<float>();

            // Super Tall 3-stage Truss Legs
            p[DrawManager.BodyPart.LeftLeg] = new List<Vector2>
            {
                new Vector2(-10f, -20f), new Vector2(-35f, -80f), new Vector2(-10f, -140f), new Vector2(-35f, -200f), new Vector2(-10f, -260f), new Vector2(-45f, -320f)
            };
            w[DrawManager.BodyPart.LeftLeg] = new List<float>();

            p[DrawManager.BodyPart.RightLeg] = new List<Vector2>
            {
                new Vector2(10f, -20f), new Vector2(35f, -80f), new Vector2(10f, -140f), new Vector2(35f, -200f), new Vector2(10f, -260f), new Vector2(45f, -320f)
            };
            w[DrawManager.BodyPart.RightLeg] = new List<float>();

            s.Points[DrawManager.Species.Human] = p;
            s.StrokeWidths[DrawManager.Species.Human] = w;
            return s;
        }

        private static DrawManager.DrawingState BuildBowlHeadCatState()
        {
            DrawManager.DrawingState s = new DrawManager.DrawingState { Species = DrawManager.Species.Cat, Part = DrawManager.BodyPart.Head };
            Dictionary<DrawManager.BodyPart, List<Vector2>> p = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            Dictionary<DrawManager.BodyPart, List<float>> w = new Dictionary<DrawManager.BodyPart, List<float>>();

            // Huge U-shaped Bowl Head
            p[DrawManager.BodyPart.Head] = new List<Vector2>
            {
                new Vector2(-110f, 95f), new Vector2(-95f, 25f), new Vector2(-60f, -10f), new Vector2(0f, -15f), new Vector2(60f, -10f), new Vector2(95f, 25f), new Vector2(110f, 95f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-105f, 95f), new Vector2(-125f, 135f), new Vector2(-80f, 95f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(80f, 95f), new Vector2(125f, 135f), new Vector2(105f, 95f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-35f, 30f), new Vector2(-25f, 30f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(25f, 30f), new Vector2(35f, 30f),
                new Vector2(float.NaN, float.NaN),
                new Vector2(-20f, 10f), new Vector2(0f, 0f), new Vector2(20f, 10f)
            };
            w[DrawManager.BodyPart.Head] = new List<float>();

            p[DrawManager.BodyPart.Torso] = new List<Vector2>
            {
                new Vector2(0f, -15f), new Vector2(-55f, -30f), new Vector2(-110f, -15f)
            };
            w[DrawManager.BodyPart.Torso] = new List<float>();

            p[DrawManager.BodyPart.LeftFrontLeg] = new List<Vector2> { new Vector2(-15f, -20f), new Vector2(-20f, -105f) };
            w[DrawManager.BodyPart.LeftFrontLeg] = new List<float>();

            p[DrawManager.BodyPart.RightFrontLeg] = new List<Vector2> { new Vector2(5f, -20f), new Vector2(10f, -105f) };
            w[DrawManager.BodyPart.RightFrontLeg] = new List<float>();

            p[DrawManager.BodyPart.LeftBackLeg] = new List<Vector2> { new Vector2(-95f, -20f), new Vector2(-105f, -105f) };
            w[DrawManager.BodyPart.LeftBackLeg] = new List<float>();

            p[DrawManager.BodyPart.RightBackLeg] = new List<Vector2> { new Vector2(-75f, -20f), new Vector2(-80f, -105f) };
            w[DrawManager.BodyPart.RightBackLeg] = new List<float>();

            p[DrawManager.BodyPart.Tail] = new List<Vector2> { new Vector2(-110f, -15f), new Vector2(-150f, 20f), new Vector2(-170f, 10f) };
            w[DrawManager.BodyPart.Tail] = new List<float>();

            s.Points[DrawManager.Species.Cat] = p;
            s.StrokeWidths[DrawManager.Species.Cat] = w;
            return s;
        }
    }
}
