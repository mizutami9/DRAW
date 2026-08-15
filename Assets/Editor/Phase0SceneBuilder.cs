using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string GeneratedFolder = "Assets/Generated";
        private const string SquareTexturePath = GeneratedFolder + "/SquareTexture.asset";
        private const string TitleLogoPath = "Assets/Art/UI/NICO_DROW.png";
        private const string ProjectFontPath = "Assets/Art/Fonts/Yomogi-Regular.ttf";
        private const int GroundLayer = 6;
        private const int PlayerLayer = 7;
        private const int GoalLayer = 8;
        private const int PushableLayer = 9;
        private static Material doodleLineMaterial;

        [MenuItem("PICO/Build Phase 0 Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Phase 0 scene generation is unavailable during Play Mode. Stop Play Mode and run it again.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Physics2D.gravity = new Vector2(0f, -28f);

            GameObject root = new GameObject("Phase 0 Prototype");
            root.AddComponent<LocalizationManager>();
            GameObject spawnPoint = CreateMarker("SpawnPoint", new Vector3(-6f, 1.8f, 0f), root.transform);
            Sprite squareSprite = CreateSquareSprite();
            Font font = CreateDefaultFont();

            GameObject player = CreatePlayer(spawnPoint.transform.position, root.transform, squareSprite);
            GameObject stageManager = new GameObject("StageManager");
            stageManager.transform.SetParent(root.transform);
            StageManager manager = stageManager.AddComponent<StageManager>();
            OnlineManager onlineManager = stageManager.AddComponent<OnlineManager>();
            OnlinePlayerSync onlinePlayerSync = stageManager.AddComponent<OnlinePlayerSync>();
            StageObjectFactory objectFactory = stageManager.AddComponent<StageObjectFactory>();
            StageLoader stageLoader = stageManager.AddComponent<StageLoader>();
            GameObject debugStageRoot = new GameObject("DebugStageRoot");
            debugStageRoot.transform.SetParent(root.transform);
            GameObject runtimeStageRoot = new GameObject("RuntimeStageRoot");
            runtimeStageRoot.transform.SetParent(root.transform);
            GameObject runtimeStageEditorRoot = new GameObject("RuntimeStageEditorRoot");
            runtimeStageEditorRoot.transform.SetParent(root.transform);

            GameObject cameraObject = CreateCamera(player.transform, root.transform);
            CreateNotebookBackdrop(root.transform, squareSprite, font);
            CreateLevel(debugStageRoot.transform, squareSprite, font);
            GameObject goal = CreateGoal(new Vector3(38.8f, 0.58f, 0f), debugStageRoot.transform, squareSprite);
            CreateMapDoodles(debugStageRoot.transform, font);
            UIManager ui = CreateUi(root.transform, font, manager, onlineManager, out DrawManager drawManager, out RuntimeStageEditor runtimeStageEditor);

            goal.GetComponent<Goal>();
            AssignObject(manager, "player", player.GetComponent<PlayerController2D>());
            AssignObject(manager, "uiManager", ui);
            AssignObject(manager, "drawManager", drawManager);
            AssignObject(manager, "stageLoader", stageLoader);
            AssignObject(manager, "stageEditor", runtimeStageEditor);
            AssignObject(manager, "cameraFollow", cameraObject.GetComponent<CameraFollow2D>());
            AssignObject(manager, "spawnPoint", spawnPoint.transform);
            AssignObject(onlinePlayerSync, "onlineManager", onlineManager);
            AssignObject(onlinePlayerSync, "stageManager", manager);
            AssignLayerMask(manager, "groundLayer", 1 << GroundLayer);
            AssignObject(stageLoader, "stageRoot", runtimeStageRoot.transform);
            AssignObject(stageLoader, "fallbackStageRoot", debugStageRoot);
            AssignObject(stageLoader, "spawnPoint", spawnPoint.transform);
            AssignObject(stageLoader, "objectFactory", objectFactory);
            AssignObject(runtimeStageEditor, "stageLoader", stageLoader);
            AssignObject(runtimeStageEditor, "objectFactory", objectFactory);
            AssignObject(runtimeStageEditor, "editorRoot", runtimeStageEditorRoot.transform);
            AssignObject(runtimeStageEditor, "worldCamera", Camera.main);
            AssignObject(drawManager, "stageManager", manager);
            AssignObject(drawManager, "onlineManager", onlineManager);
            AssignObject(drawManager, "bodyBuilder", player.GetComponent<BodyBuilder>());
            AssignObject(drawManager, "abilityController", player.GetComponent<PlayerAbilityController>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = player;

            Debug.Log($"Phase 0 scene generated: {ScenePath}");
        }

        [MenuItem("PICO/Build Phase 0 Scene", true)]
        private static bool ValidateBuildScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling;
        }

    }
}
