using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageLoader : MonoBehaviour
    {
        [SerializeField] private Transform stageRoot;
        [SerializeField] private GameObject fallbackStageRoot;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private StageObjectFactory objectFactory;

        private bool hasStageFallBoundary;
        private float stageFallBoundaryY;
        private Vector3 fallbackSpawnPosition;
        private bool fallbackSpawnCaptured;
        public StageData CurrentStageData { get; private set; }
        public Transform LoadedStageRoot => stageRoot;

        private void Awake()
        {
            if (spawnPoint == null) return;
            fallbackSpawnPosition = spawnPoint.position;
            fallbackSpawnCaptured = true;
        }

        public int CountLoadedCollectibles(StageObjectType type)
        {
            if (stageRoot == null)
            {
                return 0;
            }

            StageCollectible[] collectibles = stageRoot.GetComponentsInChildren<StageCollectible>(false);
            int count = 0;
            for (int i = 0; i < collectibles.Length; i++)
            {
                if (collectibles[i] != null && collectibles[i].CollectibleType == type)
                {
                    count++;
                }
            }
            return count;
        }

        public StageCollectible FindLoadedCollectible(string objectId)
        {
            if (stageRoot == null || string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            StageCollectible[] collectibles = stageRoot.GetComponentsInChildren<StageCollectible>(false);
            for (int i = 0; i < collectibles.Length; i++)
            {
                if (collectibles[i] != null && collectibles[i].ObjectId == objectId)
                {
                    return collectibles[i];
                }
            }
            return null;
        }

        public bool TryGetStageFallBoundaryY(out float boundaryY)
        {
            boundaryY = stageFallBoundaryY;
            return hasStageFallBoundary;
        }

        public void SetRuntimeSpawnPosition(Vector2 position)
        {
            if (spawnPoint == null) return;
            spawnPoint.position = new Vector3(position.x, position.y, spawnPoint.position.z);
        }

        public void ShowFallbackStage()
        {
            if (!fallbackSpawnCaptured && spawnPoint != null)
            {
                fallbackSpawnPosition = spawnPoint.position;
                fallbackSpawnCaptured = true;
            }
            CurrentStageData = null;
            ResetStageFallBoundary();
            ClearStageRoot();
            StageBackgroundAppearance.Reset();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(true);
            }
            if (spawnPoint != null && fallbackSpawnCaptured)
            {
                spawnPoint.position = fallbackSpawnPosition;
            }
        }

        public void ShowTitlePlayground()
        {
            EnsureReferences();

            List<StageObjectData> objects = new List<StageObjectData>();
            // The title and multiplayer controls occupy the bottom of the
            // screen. Keep the whole playable floor above that UI band.
            AddTitleRoomFrame(objects);

            // Keep the middle open for running around, while placing a few simple
            // shelves and toys around the edges of the room.
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(-8.7f, 1.5f), new Vector2(5.2f, 0.38f));
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(8.6f, 2.2f), new Vector2(5.4f, 0.38f));
            AddTitleObject(objects, StageObjectType.OneWayPlatform, new Vector2(0.2f, 4.65f), new Vector2(5.3f, 0.28f));
            AddTitlePlaygroundDecorations(objects);

            AddTitleObject(objects, StageObjectType.WoodBox, new Vector2(-5.3f, 0.6f), new Vector2(1.05f, 1.05f));
            AddTitleObject(objects, StageObjectType.WoodBox, new Vector2(-4.15f, 0.6f), new Vector2(1.05f, 1.05f));
            AddTitleObject(objects, StageObjectType.IronBox, new Vector2(-3f, 0.575f), new Vector2(1f, 1f));
            AddTitleObject(objects, StageObjectType.RubberBox, new Vector2(5.2f, 0.6f), new Vector2(1.05f, 1.05f));
            AddTitleObject(objects, StageObjectType.Ball, new Vector2(6.55f, 0.6f), new Vector2(1.05f, 1.05f));
            AddTitleObject(objects, StageObjectType.TriangleBox, new Vector2(8.95f, 2.965f), new Vector2(1.15f, 1.15f));

            AddTitleObject(objects, StageObjectType.Handgun, new Vector2(-0.95f, 0.375f), new Vector2(1.05f, 0.6f));
            AddTitleObject(objects, StageObjectType.Bazooka, new Vector2(1.45f, 0.485f), new Vector2(1.65f, 0.82f));
            AddTitleObject(objects, StageObjectType.JumpPad, new Vector2(10.8f, 0.385f), new Vector2(1.45f, 0.62f), 31f);
            AddTitleObject(objects, StageObjectType.BulletBreakableWall, new Vector2(11.2f, 4f), new Vector2(0.65f, 3.2f));
            // Feed occasional moving targets onto the right shelf without
            // filling the open play space in the middle of the title room.
            AddTitleObject(objects, StageObjectType.EnemyDropper, new Vector2(8.6f, 5.85f), new Vector2(1.8f, 1.35f), 5.5f);

            StageData titlePlayground = new StageData
            {
                id = "title-playground",
                displayName = "Title Playground",
                backgroundColorHex = "#EAF8F2FF",
                objects = objects.ToArray()
            };

            LoadStage(titlePlayground);
            if (spawnPoint != null)
            {
                // Start on the left shelf. The multiplayer drawer occupies the
                // lower edge of the screen, so the player remains visible while
                // waiting for friends.
                spawnPoint.position = new Vector3(-8.7f, 2.65f, spawnPoint.position.z);
            }
        }

        private static void AddTitleRoomFrame(List<StageObjectData> objects)
        {
            const float roomWidth = 28f;
            const float terrainThickness = 0.55f;
            const float floorY = -0.2f;
            const float ceilingY = 6.75f;

            float outerBottom = floorY - terrainThickness * 0.5f;
            float outerTop = ceilingY + terrainThickness * 0.5f;
            float wallX = roomWidth * 0.5f - terrainThickness * 0.5f;
            float wallHeight = outerTop - outerBottom;
            float wallY = (outerTop + outerBottom) * 0.5f;

            StageObjectData frame = StageObjectFactory.CreateDefaultData(StageObjectType.Platform, Vector2.zero);
            frame.objectId = $"title-playground-{objects.Count:D2}-RoomFrame";
            frame.size = new Vector2(roomWidth, wallHeight);
            frame.connectedRects = new[]
            {
                new StageRectPartData { position = new Vector2(0f, floorY), size = new Vector2(roomWidth, terrainThickness) },
                new StageRectPartData { position = new Vector2(0f, ceilingY), size = new Vector2(roomWidth, terrainThickness) },
                new StageRectPartData { position = new Vector2(-wallX, wallY), size = new Vector2(terrainThickness, wallHeight) },
                new StageRectPartData { position = new Vector2(wallX, wallY), size = new Vector2(terrainThickness, wallHeight) }
            };
            objects.Add(frame);
        }

        private static void AddTitlePlaygroundDecorations(List<StageObjectData> objects)
        {
            const float floorPlantY = 0.06f;
            const float leftShelfPlantY = 1.675f;
            const float rightShelfPlantY = 2.375f;
            const float oneWayPlantY = 4.775f;

            // Soft scenery stays well behind the playable objects so the title
            // room feels illustrated without making its toys hard to read.
            AddTitleDecoration(objects, StageObjectType.BackgroundTree, new Vector2(-11.6f, 1.75f), new Vector2(3.1f, 3.5f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundMountain, new Vector2(-5.8f, 1.45f), new Vector2(6f, 2.6f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundMountain, new Vector2(-0.2f, 1.2f), new Vector2(5.1f, 2.2f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundMountain, new Vector2(5.1f, 1.4f), new Vector2(5.8f, 2.5f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundCloud, new Vector2(-6.4f, 4.9f), new Vector2(2.7f, 1.25f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundCloud, new Vector2(0.8f, 5.25f), new Vector2(1.8f, 0.85f), true);
            AddTitleDecoration(objects, StageObjectType.BackgroundCloud, new Vector2(5.9f, 4.75f), new Vector2(2.35f, 1.1f), true);

            // Plants sit just above the floor and shelves. Deliberately vary
            // their species and size instead of repeating a uniform border.
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(-12.65f, floorPlantY), new Vector2(1.15f, 0.58f));
            AddTitleDecoration(objects, StageObjectType.BackgroundBush, new Vector2(-11.25f, floorPlantY), new Vector2(1.35f, 0.9f));
            AddTitleDecoration(objects, StageObjectType.BackgroundFlower, new Vector2(-7.1f, floorPlantY), new Vector2(0.55f, 0.72f));
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(-6.35f, floorPlantY), new Vector2(1.05f, 0.52f));
            AddTitleDecoration(objects, StageObjectType.BackgroundTulip, new Vector2(-1.95f, floorPlantY), new Vector2(0.55f, 0.72f));
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(3.15f, floorPlantY), new Vector2(1.15f, 0.54f));
            AddTitleDecoration(objects, StageObjectType.BackgroundSunflower, new Vector2(3.9f, floorPlantY), new Vector2(0.68f, 0.86f));
            AddTitleDecoration(objects, StageObjectType.BackgroundMushroom, new Vector2(7.7f, floorPlantY), new Vector2(0.65f, 0.58f));
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(9.45f, floorPlantY), new Vector2(1.05f, 0.52f));
            AddTitleDecoration(objects, StageObjectType.BackgroundFlower, new Vector2(12.55f, floorPlantY), new Vector2(0.55f, 0.72f));

            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(-10.6f, leftShelfPlantY), new Vector2(0.9f, 0.46f));
            AddTitleDecoration(objects, StageObjectType.BackgroundFlower, new Vector2(-6.7f, leftShelfPlantY), new Vector2(0.48f, 0.65f));
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(2.2f, oneWayPlantY), new Vector2(0.8f, 0.42f));
            AddTitleDecoration(objects, StageObjectType.BackgroundGrass, new Vector2(6.35f, rightShelfPlantY), new Vector2(0.85f, 0.44f));
            AddTitleDecoration(objects, StageObjectType.BackgroundFlower, new Vector2(10.35f, rightShelfPlantY), new Vector2(0.5f, 0.66f));
        }

        private static void AddTitleDecoration(
            List<StageObjectData> objects,
            StageObjectType type,
            Vector2 position,
            Vector2 size,
            bool distant = false)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            string layer = distant ? "distant" : "flora";
            data.objectId = $"title-playground-{layer}-{objects.Count:D2}-{type}";
            data.size = size;
            objects.Add(data);
        }

        private static void AddTitleObject(
            List<StageObjectData> objects,
            StageObjectType type,
            Vector2 position,
            Vector2 size,
            float actionStrength = -1f)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = $"title-playground-{objects.Count:D2}-{type}";
            data.size = size;
            if (actionStrength >= 0f)
            {
                data.actionStrength = actionStrength;
            }
            objects.Add(data);
        }

        public void HideStages()
        {
            CurrentStageData = null;
            ResetStageFallBoundary();
            ClearStageRoot();
            StageBackgroundAppearance.Reset();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }
        }

        public bool LoadStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                return false;
            }

            if (!DemoAccessPolicy.IsStageAllowed(stageId))
            {
                Debug.LogWarning($"Demo build rejected unavailable stage: {stageId}");
                return false;
            }

            TextAsset asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset == null)
            {
                Debug.LogWarning($"Stage JSON not found: Resources/Stages/{stageId}. Falling back to debug stage.");
                ShowFallbackStage();
                return false;
            }

            StageData data = JsonUtility.FromJson<StageData>(asset.text);
            if (data == null)
            {
                ShowFallbackStage();
                return false;
            }

            LoadStage(data);
            return true;
        }

        public void LoadStage(StageData data)
        {
            if (stageRoot != null && !stageRoot.gameObject.activeSelf)
            {
                stageRoot.gameObject.SetActive(true);
            }
            if (data == null)
            {
                return;
            }

            if (DemoAccessPolicy.IsDemoBuild
                && data.id != "title-playground"
                && !DemoAccessPolicy.IsStageAllowed(data.id))
            {
                Debug.LogWarning($"Demo build rejected unavailable stage data: {data.id}");
                return;
            }

            EnsureReferences();
            StagePoseTowerRandomizer.Prepare(data);
            CurrentStageData = data;
            StageObjectData[] runtimeObjects = BuildRuntimeStageObjects(data);
            RefreshStageFallBoundary(runtimeObjects);
            ClearStageRoot();
            objectFactory.ConfigureStageVisualTheme(data.id, stageRoot);
            StageBackgroundAppearance.Apply(StageBackgroundAppearance.Parse(data.backgroundColorHex));
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }

            objectFactory.FitSeparateBridges(runtimeObjects);

            int playerCount = GetCurrentStagePlayerCount();
            HashSet<string> enabledBombDroppers = BuildPlayerScaledBombDropperIds(data, playerCount);

            for (int i = 0; i < runtimeObjects.Length; i++)
            {
                StageObjectData obj = runtimeObjects[i];
                if (obj == null)
                {
                    continue;
                }

                if (data.id == "14-3" && !string.IsNullOrEmpty(obj.objectId)
                    && obj.objectId.StartsWith("14-3-layout-", System.StringComparison.Ordinal))
                {
                    // These records are editable layout definitions consumed by
                    // StageLaserRelayController, not ordinary runtime objects.
                    continue;
                }

                if (data.id == "9-2"
                    && playerCount < 3
                    && !string.IsNullOrEmpty(obj.objectId)
                    && obj.objectId.StartsWith("9-2_coin_extra_"))
                {
                    continue;
                }

                if (enabledBombDroppers != null
                    && obj.type == StageObjectType.BombDropper
                    && !enabledBombDroppers.Contains(obj.objectId))
                {
                    continue;
                }

                if (obj.type == StageObjectType.Spawn)
                {
                    if (spawnPoint != null)
                    {
                        spawnPoint.position = obj.position;
                    }
                    objectFactory.Create(obj, stageRoot);
                    continue;
                }

                objectFactory.Create(obj, stageRoot);
            }

            objectFactory.RefreshBridgeConnectionVisuals(runtimeObjects, stageRoot);
            AddNatureStageVines(data.id, runtimeObjects);

            ConfigureStageGimmicks(data);
        }

        private static StageObjectData[] BuildRuntimeStageObjects(StageData data)
        {
            if (data == null || (data.id != "1-1" && data.id != "2-1" && data.id != "3-1"))
            {
                return data != null && data.objects != null ? data.objects : new StageObjectData[0];
            }

            List<StageObjectData> objects = data.objects != null
                ? new List<StageObjectData>(data.objects)
                : new List<StageObjectData>();
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null
                    && !string.IsNullOrEmpty(objects[i].objectId)
                    && objects[i].objectId.StartsWith("nature-", System.StringComparison.Ordinal))
                {
                    return objects.ToArray();
                }
            }

            if (data.id == "1-1")
            {
                AppendStage11Nature(objects);
            }
            else if (data.id == "2-1")
            {
                AppendStage21Nature(objects);
            }
            else
            {
                AppendStage31Nature(objects);
            }
            return objects.ToArray();
        }

        private static void AppendStage11Nature(List<StageObjectData> objects)
        {
            AddNatureDecoration(objects, "1-1", "mountain-a", StageObjectType.BackgroundMountain, new Vector2(-2f, 1.1f), new Vector2(7.5f, 3.2f), true);
            AddNatureDecoration(objects, "1-1", "mountain-b", StageObjectType.BackgroundMountain, new Vector2(10.5f, 1.35f), new Vector2(7f, 3f), true);
            AddNatureDecoration(objects, "1-1", "mountain-c", StageObjectType.BackgroundMountain, new Vector2(31f, 8.1f), new Vector2(8f, 3.5f), true);
            AddNatureDecoration(objects, "1-1", "mountain-d", StageObjectType.BackgroundMountain, new Vector2(49.5f, 14.3f), new Vector2(8.5f, 3.7f), true);
            AddNatureDecoration(objects, "1-1", "tree-a", StageObjectType.BackgroundTree, new Vector2(31.5f, 7.8f), new Vector2(4.1f, 4.8f), true);
            AddNatureDecoration(objects, "1-1", "tree-b", StageObjectType.BackgroundTree, new Vector2(57f, 14.4f), new Vector2(3.8f, 4.5f), true);

            AddNatureDecoration(objects, "1-1", "grass-01", StageObjectType.BackgroundGrass, new Vector2(-8.35f, -0.615f), new Vector2(1.15f, 0.58f));
            AddNatureDecoration(objects, "1-1", "bush-01", StageObjectType.BackgroundBush, new Vector2(-4.2f, -0.615f), new Vector2(1.35f, 0.9f));
            AddNatureDecoration(objects, "1-1", "flower-01", StageObjectType.BackgroundFlower, new Vector2(2.75f, -0.615f), new Vector2(0.55f, 0.72f));
            AddNatureDecoration(objects, "1-1", "grass-03", StageObjectType.BackgroundGrass, new Vector2(11.4f, -0.315f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "1-1", "mushroom-01", StageObjectType.BackgroundMushroom, new Vector2(15.05f, -0.315f), new Vector2(0.62f, 0.56f));
            AddNatureDecoration(objects, "1-1", "grass-04", StageObjectType.BackgroundGrass, new Vector2(22f, 12.485f), new Vector2(0.95f, 0.48f));
            AddNatureDecoration(objects, "1-1", "flower-02", StageObjectType.BackgroundSunflower, new Vector2(25.6f, 12.485f), new Vector2(0.62f, 0.82f));
            AddNatureDecoration(objects, "1-1", "grass-05", StageObjectType.BackgroundGrass, new Vector2(7.8f, 18.985f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "1-1", "bush-02", StageObjectType.BackgroundBush, new Vector2(27f, 5.985f), new Vector2(1.2f, 0.78f));
            AddNatureDecoration(objects, "1-1", "flower-03", StageObjectType.BackgroundFlower, new Vector2(35.2f, 5.985f), new Vector2(0.52f, 0.7f));
            AddNatureDecoration(objects, "1-1", "grass-06", StageObjectType.BackgroundGrass, new Vector2(47.2f, 12.285f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "1-1", "flower-04", StageObjectType.BackgroundTulip, new Vector2(58f, 12.285f), new Vector2(0.52f, 0.72f));
            AddNatureDecoration(objects, "1-1", "grass-07", StageObjectType.BackgroundGrass, new Vector2(9.6f, 28.485f), new Vector2(1.1f, 0.55f));
        }

        private static void AppendStage21Nature(List<StageObjectData> objects)
        {
            float[] mountainX = { 6f, 30f, 55f, 82f, 108f };
            for (int i = 0; i < mountainX.Length; i++)
            {
                AddNatureDecoration(objects, "2-1", "mountain-" + i, StageObjectType.BackgroundMountain, new Vector2(mountainX[i], 2.2f + (i % 2) * 0.6f), new Vector2(9f, 3.8f), true);
            }
            AddNatureDecoration(objects, "2-1", "tree-a", StageObjectType.BackgroundTree, new Vector2(50f, 1.35f), new Vector2(3.8f, 4.4f), true);
            AddNatureDecoration(objects, "2-1", "tree-b", StageObjectType.BackgroundTree, new Vector2(76f, 1.15f), new Vector2(4f, 4.6f), true);
            AddNatureDecoration(objects, "2-1", "tree-c", StageObjectType.BackgroundTree, new Vector2(101f, 1f), new Vector2(3.8f, 4.4f), true);

            AddNatureDecoration(objects, "2-1", "grass-01", StageObjectType.BackgroundGrass, new Vector2(-8.5f, -0.56f), new Vector2(1.1f, 0.55f));
            AddNatureDecoration(objects, "2-1", "flower-01", StageObjectType.BackgroundFlower, new Vector2(-0.5f, -0.56f), new Vector2(0.52f, 0.7f));
            AddNatureDecoration(objects, "2-1", "bush-01", StageObjectType.BackgroundBush, new Vector2(14.5f, -0.57f), new Vector2(1.2f, 0.78f));
            AddNatureDecoration(objects, "2-1", "grass-02", StageObjectType.BackgroundGrass, new Vector2(20.5f, -0.57f), new Vector2(1f, 0.5f));
            AddNatureDecoration(objects, "2-1", "grass-03", StageObjectType.BackgroundGrass, new Vector2(24.7f, 0.285f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "2-1", "mushroom-01", StageObjectType.BackgroundMushroom, new Vector2(28.7f, 0.285f), new Vector2(0.62f, 0.56f));
            AddNatureDecoration(objects, "2-1", "flower-02", StageObjectType.BackgroundSunflower, new Vector2(35.2f, 0.285f), new Vector2(0.64f, 0.84f));
            AddNatureDecoration(objects, "2-1", "grass-04", StageObjectType.BackgroundGrass, new Vector2(39.8f, 0.285f), new Vector2(0.95f, 0.48f));
            AddNatureDecoration(objects, "2-1", "grass-05", StageObjectType.BackgroundGrass, new Vector2(115.5f, -2.515f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "2-1", "flower-03", StageObjectType.BackgroundTulip, new Vector2(122.5f, -2.515f), new Vector2(0.54f, 0.72f));
        }

        private static void AppendStage31Nature(List<StageObjectData> objects)
        {
            Vector2[] mountainPositions =
            {
                new Vector2(4f, 20.5f), new Vector2(29f, 14.5f), new Vector2(6f, -5f),
                new Vector2(29f, -18.5f), new Vector2(5f, -31.5f), new Vector2(29f, -44f),
                new Vector2(-4f, -56.5f)
            };
            for (int i = 0; i < mountainPositions.Length; i++)
            {
                AddNatureDecoration(objects, "3-1", "mountain-" + i, StageObjectType.BackgroundMountain, mountainPositions[i], new Vector2(8f, 3.5f), true);
            }
            AddNatureDecoration(objects, "3-1", "tree-a", StageObjectType.BackgroundTree, new Vector2(-9f, 20.5f), new Vector2(3.8f, 4.5f), true);
            AddNatureDecoration(objects, "3-1", "tree-b", StageObjectType.BackgroundTree, new Vector2(31f, 10f), new Vector2(3.8f, 4.5f), true);
            AddNatureDecoration(objects, "3-1", "tree-c", StageObjectType.BackgroundTree, new Vector2(-7f, -10f), new Vector2(3.6f, 4.2f), true);
            AddNatureDecoration(objects, "3-1", "tree-d", StageObjectType.BackgroundTree, new Vector2(32f, -32f), new Vector2(3.8f, 4.5f), true);
            AddNatureDecoration(objects, "3-1", "tree-e", StageObjectType.BackgroundTree, new Vector2(-12f, -53f), new Vector2(3.8f, 4.5f), true);

            AddNatureDecoration(objects, "3-1", "grass-01", StageObjectType.BackgroundGrass, new Vector2(-10.2f, 18.94f), new Vector2(1.1f, 0.55f));
            AddNatureDecoration(objects, "3-1", "flower-01", StageObjectType.BackgroundFlower, new Vector2(-2.2f, 18.94f), new Vector2(0.52f, 0.7f));
            AddNatureDecoration(objects, "3-1", "bush-01", StageObjectType.BackgroundBush, new Vector2(9.5f, 14.43f), new Vector2(1.15f, 0.76f));
            AddNatureDecoration(objects, "3-1", "grass-02", StageObjectType.BackgroundGrass, new Vector2(13f, 14.43f), new Vector2(0.95f, 0.48f));
            AddNatureDecoration(objects, "3-1", "grass-03", StageObjectType.BackgroundGrass, new Vector2(31.5f, -2.015f), new Vector2(1.05f, 0.52f));
            AddNatureDecoration(objects, "3-1", "flower-02", StageObjectType.BackgroundTulip, new Vector2(9.8f, -34.015f), new Vector2(0.52f, 0.7f));
            AddNatureDecoration(objects, "3-1", "grass-04", StageObjectType.BackgroundGrass, new Vector2(0.3f, -38.015f), new Vector2(0.95f, 0.48f));
            AddNatureDecoration(objects, "3-1", "mushroom-01", StageObjectType.BackgroundMushroom, new Vector2(26f, -40.29f), new Vector2(0.62f, 0.56f));
            AddNatureDecoration(objects, "3-1", "grass-05", StageObjectType.BackgroundGrass, new Vector2(-16.7f, -63.015f), new Vector2(1.05f, 0.52f));
        }

        private static void AddNatureDecoration(
            List<StageObjectData> objects,
            string stageId,
            string suffix,
            StageObjectType type,
            Vector2 position,
            Vector2 size,
            bool distant = false)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = $"nature-{(distant ? "distant" : "flora")}-{stageId}-{suffix}";
            data.size = size;
            objects.Add(data);
        }

        private void AddNatureStageVines(string stageId, StageObjectData[] objects)
        {
            if (objectFactory == null || stageRoot == null)
            {
                return;
            }

            int seed = 20;
            if (stageId == "1-1")
            {
                AddNatureVine(new Vector2(9.2f, 23.48f), 1.65f, ref seed);
                AddNatureVine(new Vector2(7.4f, 17.98f), 1.35f, ref seed);
                AddNatureVine(new Vector2(39f, 10.28f), 1.4f, ref seed);
                AddNatureVine(new Vector2(49f, 10.28f), 1.75f, ref seed);
                AddNatureWallVine(new Vector2(15.92f, -0.22f), 6.3f, ref seed);
                AddNatureWallVine(new Vector2(24.16f, 4.65f), 4.4f, ref seed);
                AddNatureWallVine(new Vector2(37.82f, 4.3f), 4.2f, ref seed);
                AddNatureWallVine(new Vector2(41.82f, 12.5f), 5.4f, ref seed);
            }
            else if (stageId == "2-1")
            {
                float[] xs = { -7f, 18f, 43f, 67f, 93f, 117f };
                for (int i = 0; i < xs.Length; i++)
                {
                    AddNatureVine(new Vector2(xs[i], 15.7f), 1.15f + (i % 3) * 0.4f, ref seed);
                }
                AddNatureWallVine(new Vector2(38.66f, 0.45f), 5.6f, ref seed);
                AddNatureWallVine(new Vector2(40.62f, -4.25f), 2.6f, ref seed);
            }
            else if (stageId == "3-1")
            {
                AddNatureVine(new Vector2(-8f, 30.2f), 1.4f, ref seed);
                AddNatureVine(new Vector2(8f, 30.2f), 1.8f, ref seed);
                AddNatureVine(new Vector2(31f, 30.2f), 1.25f, ref seed);
                AddNatureVine(new Vector2(-9f, 18.48f), 1.2f, ref seed);
                AddNatureVine(new Vector2(30f, -2.47f), 1.55f, ref seed);
                AddNatureVine(new Vector2(27f, -40.74f), 1.45f, ref seed);
                AddNatureWallVine(new Vector2(-2.84f, 13.45f), 3.8f, ref seed);
                AddNatureWallVine(new Vector2(14.16f, -1.8f), 5.7f, ref seed);
                AddNatureWallVine(new Vector2(23.34f, 4.2f), 5.8f, ref seed);
                AddNatureWallVine(new Vector2(13.12f, -47f), 3.1f, ref seed);
            }
            else if (IsAdditionalNatureStage(stageId))
            {
                AddAutomaticNatureWallVines(stageId, objects, ref seed);
            }
        }

        private void AddAutomaticNatureWallVines(
            string stageId,
            StageObjectData[] objects,
            ref int seed)
        {
            if (objects == null || objects.Length == 0)
            {
                return;
            }

            HashSet<string> dynamicTargets = new HashSet<string>();
            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData source = objects[i];
                if (source != null && !string.IsNullOrEmpty(source.linkTargetId))
                {
                    dynamicTargets.Add(source.linkTargetId);
                }
            }

            List<StageObjectData> candidates = new List<StageObjectData>();
            StageObjectData boundary = null;
            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData data = objects[i];
                if (data == null)
                {
                    continue;
                }

                if (data.type == StageObjectType.StageBoundary)
                {
                    boundary = data;
                    continue;
                }

                if ((data.type != StageObjectType.Platform
                        && data.type != StageObjectType.Wall
                        && data.type != StageObjectType.BreakableWall
                        && data.type != StageObjectType.BulletBreakableWall)
                    || (data.pathPoints != null && data.pathPoints.Length >= 2)
                    || (data.connectedRects != null && data.connectedRects.Length > 0)
                    || (!string.IsNullOrEmpty(data.objectId) && dynamicTargets.Contains(data.objectId)))
                {
                    continue;
                }

                GetRotatedSize(data, out float worldWidth, out float worldHeight);
                if (worldHeight >= 3f && worldHeight > worldWidth * 1.45f)
                {
                    candidates.Add(data);
                }
            }

            candidates.Sort((left, right) =>
            {
                int xOrder = left.position.x.CompareTo(right.position.x);
                return xOrder != 0 ? xOrder : left.position.y.CompareTo(right.position.y);
            });

            int vineCount = Mathf.Clamp((candidates.Count + 2) / 3, 0, 4);
            for (int vineIndex = 0; vineIndex < vineCount; vineIndex++)
            {
                int candidateIndex = Mathf.Clamp(
                    Mathf.RoundToInt((vineIndex + 0.5f) * candidates.Count / vineCount - 0.5f),
                    0,
                    candidates.Count - 1);
                StageObjectData wall = candidates[candidateIndex];
                GetRotatedSize(wall, out float width, out float height);
                int wallSeed = GetStableNatureSeed(stageId + ":" + wall.objectId);
                bool useRightEdge = (wallSeed & 1) != 0;
                float x = wall.position.x + (useRightEdge ? width * 0.5f - 0.14f : -width * 0.5f + 0.14f);
                float y = wall.position.y - height * 0.5f + 0.16f;
                AddNatureWallVine(
                    new Vector2(x, y),
                    Mathf.Clamp(height * 0.42f, 2.4f, 5.8f),
                    ref seed);
            }

            if (vineCount == 0 && boundary != null)
            {
                int boundarySeed = GetStableNatureSeed(stageId + ":boundary");
                bool useRightEdge = (boundarySeed & 1) != 0;
                float x = boundary.position.x
                    + (useRightEdge ? boundary.size.x * 0.5f - 0.18f : -boundary.size.x * 0.5f + 0.18f);
                float y = boundary.position.y - boundary.size.y * 0.5f + 0.75f;
                AddNatureWallVine(
                    new Vector2(x, y),
                    Mathf.Clamp(boundary.size.y * 0.3f, 2.8f, 5.2f),
                    ref seed);
            }
        }

        private static void GetRotatedSize(StageObjectData data, out float width, out float height)
        {
            float radians = data.rotation * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(radians));
            float sine = Mathf.Abs(Mathf.Sin(radians));
            width = data.size.x * cosine + data.size.y * sine;
            height = data.size.x * sine + data.size.y * cosine;
        }

        private static bool IsAdditionalNatureStage(string stageId)
        {
            return stageId == "5-2"
                || stageId == "7-2"
                || stageId == "9-3"
                || stageId == "10-2"
                || stageId == "14-2";
        }

        private static int GetStableNatureSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }
                return hash & 0x7fffffff;
            }
        }

        private void AddNatureVine(Vector2 anchor, float length, ref int seed)
        {
            objectFactory.AddNatureHangingVine(stageRoot, anchor, length, seed++);
        }

        private void AddNatureWallVine(Vector2 bottom, float height, ref int seed)
        {
            objectFactory.AddNatureWallVine(stageRoot, bottom, height, seed++);
        }

        private static int GetCurrentStagePlayerCount()
        {
            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            return Mathf.Clamp(
                stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1,
                1,
                4);
        }

        private static HashSet<string> BuildPlayerScaledBombDropperIds(StageData data, int playerCount)
        {
            if (data == null || data.id != "11-1" || data.objects == null)
            {
                return null;
            }

            List<StageObjectData> droppers = new List<StageObjectData>();
            for (int i = 0; i < data.objects.Length; i++)
            {
                StageObjectData obj = data.objects[i];
                if (obj != null && obj.type == StageObjectType.BombDropper)
                {
                    droppers.Add(obj);
                }
            }
            if (droppers.Count <= 1)
            {
                return null;
            }

            int enabledCount = Mathf.Clamp(
                Mathf.CeilToInt(droppers.Count * playerCount / 4f),
                1,
                droppers.Count);

            HashSet<string> enabledIds = new HashSet<string>();
            if (enabledCount == 1)
            {
                enabledIds.Add(droppers[droppers.Count / 2].objectId);
                return enabledIds;
            }

            for (int i = 0; i < enabledCount; i++)
            {
                int sourceIndex = Mathf.RoundToInt(i * (droppers.Count - 1f) / (enabledCount - 1f));
                enabledIds.Add(droppers[sourceIndex].objectId);
            }
            return enabledIds;
        }

        private void RefreshStageFallBoundary(StageObjectData[] objects)
        {
            ResetStageFallBoundary();
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData obj = objects[i];
                if (obj == null)
                {
                    continue;
                }

                StageObjectCatalogEntry entry = StageObjectCatalog.Get(obj.type);
                if (obj.type == StageObjectType.StageBoundary
                    || entry.Kind == StageObjectKind.Decoration
                    || entry.Kind == StageObjectKind.Marker)
                {
                    continue;
                }

                float bottomY = CalculateObjectBottomY(obj);
                if (!hasStageFallBoundary || bottomY < stageFallBoundaryY)
                {
                    hasStageFallBoundary = true;
                    stageFallBoundaryY = bottomY;
                }
            }
        }

        private static float CalculateObjectBottomY(StageObjectData obj)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, obj.rotation);
            float bottom = float.PositiveInfinity;
            if (obj.connectedRects != null && obj.connectedRects.Length > 0)
            {
                for (int i = 0; i < obj.connectedRects.Length; i++)
                {
                    StageRectPartData part = obj.connectedRects[i];
                    if (part == null)
                    {
                        continue;
                    }

                    Vector2 center = obj.position + (Vector2)(rotation * part.position);
                    bottom = Mathf.Min(bottom, center.y - CalculateVerticalExtent(part.size, obj.rotation));
                }
            }
            else if (obj.pathPoints != null && obj.pathPoints.Length >= 2)
            {
                float halfThickness = Mathf.Max(0.2f, obj.pathThickness > 0f ? obj.pathThickness : 0.5f) * 0.5f;
                for (int i = 0; i < obj.pathPoints.Length; i++)
                {
                    Vector2 point = obj.position + (Vector2)(rotation * obj.pathPoints[i]);
                    bottom = Mathf.Min(bottom, point.y - halfThickness);
                }
            }
            else
            {
                bottom = obj.position.y - CalculateVerticalExtent(obj.size, obj.rotation);
            }

            return float.IsPositiveInfinity(bottom) ? obj.position.y : bottom;
        }

        private static float CalculateVerticalExtent(Vector2 size, float rotation)
        {
            float radians = rotation * Mathf.Deg2Rad;
            return Mathf.Abs(Mathf.Sin(radians)) * Mathf.Abs(size.x) * 0.5f
                + Mathf.Abs(Mathf.Cos(radians)) * Mathf.Abs(size.y) * 0.5f;
        }

        private void ResetStageFallBoundary()
        {
            hasStageFallBoundary = false;
            stageFallBoundaryY = 0f;
        }

        private void ConfigureStageGimmicks(StageData data)
        {
            if (stageRoot == null)
            {
                return;
            }

            StageGimmickLinkController existingLinkController = stageRoot.GetComponent<StageGimmickLinkController>();
            if (existingLinkController != null)
            {
                DestroyComponentNow(existingLinkController);
            }

            StageGimmickSyncManager existingSyncManager = stageRoot.GetComponent<StageGimmickSyncManager>();
            if (existingSyncManager != null)
            {
                DestroyComponentNow(existingSyncManager);
            }

            StageEliminationChallengeController existingSurvival = stageRoot.GetComponent<StageEliminationChallengeController>();
            if (existingSurvival != null)
            {
                DestroyComponentNow(existingSurvival);
            }

            StageBlockBreakerController existingBlockBreaker = stageRoot.GetComponent<StageBlockBreakerController>();
            if (existingBlockBreaker != null)
            {
                DestroyComponentNow(existingBlockBreaker);
            }

            StageRicochetBreakerController existingRicochet = stageRoot.GetComponent<StageRicochetBreakerController>();
            if (existingRicochet != null)
            {
                DestroyComponentNow(existingRicochet);
            }

            StageEscortController existingEscort = stageRoot.GetComponent<StageEscortController>();
            if (existingEscort != null)
            {
                DestroyComponentNow(existingEscort);
            }

            StageBossBattleController existingBossBattle = stageRoot.GetComponent<StageBossBattleController>();
            if (existingBossBattle != null)
            {
                DestroyComponentNow(existingBossBattle);
            }

            StageSpikeChaseController existingSpikeChase = stageRoot.GetComponent<StageSpikeChaseController>();
            if (existingSpikeChase != null)
            {
                DestroyComponentNow(existingSpikeChase);
            }

            StageAquariumSealController existingAquariumSeal = stageRoot.GetComponent<StageAquariumSealController>();
            if (existingAquariumSeal != null)
            {
                DestroyComponentNow(existingAquariumSeal);
            }

            StageTiltBoardController existingTiltBoard = stageRoot.GetComponent<StageTiltBoardController>();
            if (existingTiltBoard != null)
            {
                DestroyComponentNow(existingTiltBoard);
            }

            StageGrainCarryController existingGrainCarry = stageRoot.GetComponent<StageGrainCarryController>();
            if (existingGrainCarry != null)
            {
                DestroyComponentNow(existingGrainCarry);
            }

            // 7-3 owns this timer. The loaded stage root is reused, so leaving
            // the component attached made its 03:00 display continue in 7-2.
            StageTimedGoalController existingTimedGoal = stageRoot.GetComponent<StageTimedGoalController>();
            if (existingTimedGoal != null)
            {
                DestroyComponentNow(existingTimedGoal);
            }

            StageTowerDefenseController existingTowerDefense = stageRoot.GetComponent<StageTowerDefenseController>();
            if (existingTowerDefense != null)
            {
                DestroyComponentNow(existingTowerDefense);
            }

            StageSlimeMissileSurvivalController existingSlimeMissile = stageRoot.GetComponent<StageSlimeMissileSurvivalController>();
            if (existingSlimeMissile != null)
            {
                DestroyComponentNow(existingSlimeMissile);
            }

            StageGrainRainController existingGrainRain = stageRoot.GetComponent<StageGrainRainController>();
            if (existingGrainRain != null)
            {
                DestroyComponentNow(existingGrainRain);
            }

            StageIceSpeedrunController existingIceSpeedrun = stageRoot.GetComponent<StageIceSpeedrunController>();
            if (existingIceSpeedrun != null)
            {
                DestroyComponentNow(existingIceSpeedrun);
            }

            StageRicochetChallengeController existingRicochetChallenge = stageRoot.GetComponent<StageRicochetChallengeController>();
            if (existingRicochetChallenge != null)
            {
                DestroyComponentNow(existingRicochetChallenge);
            }

            StageRicochetEnemyChallengeController existingRicochetEnemy =
                stageRoot.GetComponent<StageRicochetEnemyChallengeController>();
            if (existingRicochetEnemy != null)
            {
                DestroyComponentNow(existingRicochetEnemy);
            }

            StageWindSpeedrunController existingWindSpeedrun =
                stageRoot.GetComponent<StageWindSpeedrunController>();
            if (existingWindSpeedrun != null)
            {
                DestroyComponentNow(existingWindSpeedrun);
            }

            StageUmbrellaRainController existingUmbrellaRain =
                stageRoot.GetComponent<StageUmbrellaRainController>();
            if (existingUmbrellaRain != null)
            {
                DestroyComponentNow(existingUmbrellaRain);
            }

            StageLinkedShieldSurvivalController existingLinkedShield =
                stageRoot.GetComponent<StageLinkedShieldSurvivalController>();
            if (existingLinkedShield != null)
            {
                DestroyComponentNow(existingLinkedShield);
            }

            StageLaserRelayController existingLaserRelay = stageRoot.GetComponent<StageLaserRelayController>();
            if (existingLaserRelay != null)
            {
                DestroyComponentNow(existingLaserRelay);
            }

            StageFlyingPlatformBossController existingFlyingBoss =
                stageRoot.GetComponent<StageFlyingPlatformBossController>();
            if (existingFlyingBoss != null)
            {
                DestroyComponentNow(existingFlyingBoss);
            }

            StageSideScrollBossChaseController existingSideScrollBoss =
                stageRoot.GetComponent<StageSideScrollBossChaseController>();
            if (existingSideScrollBoss != null)
            {
                DestroyComponentNow(existingSideScrollBoss);
            }

            StageMirrorFinalBossController existingMirrorBoss =
                stageRoot.GetComponent<StageMirrorFinalBossController>();
            if (existingMirrorBoss != null)
            {
                DestroyComponentNow(existingMirrorBoss);
            }

            StageValueCoinChallengeController existingValueCoinChallenge = stageRoot.GetComponent<StageValueCoinChallengeController>();
            if (existingValueCoinChallenge != null)
            {
                DestroyComponentNow(existingValueCoinChallenge);
            }

            StageCoinRushController existingCoinRush = stageRoot.GetComponent<StageCoinRushController>();
            if (existingCoinRush != null)
            {
                DestroyComponentNow(existingCoinRush);
            }

            StageMovingCoinChallengeController existingMovingCoinChallenge =
                stageRoot.GetComponent<StageMovingCoinChallengeController>();
            if (existingMovingCoinChallenge != null)
            {
                DestroyComponentNow(existingMovingCoinChallenge);
            }

            StageHumanCircuitController existingHumanCircuit = stageRoot.GetComponent<StageHumanCircuitController>();
            if (existingHumanCircuit != null)
            {
                DestroyComponentNow(existingHumanCircuit);
            }

            StageMovingGauntletController existingMovingGauntlet =
                stageRoot.GetComponent<StageMovingGauntletController>();
            if (existingMovingGauntlet != null)
            {
                DestroyComponentNow(existingMovingGauntlet);
            }

            StageDrawnEscortChallengeController existingDrawnEscort =
                stageRoot.GetComponent<StageDrawnEscortChallengeController>();
            if (existingDrawnEscort != null)
            {
                DestroyComponentNow(existingDrawnEscort);
            }

            StageBalloonGalleryController existingBalloonGallery =
                stageRoot.GetComponent<StageBalloonGalleryController>();
            if (existingBalloonGallery != null)
            {
                DestroyComponentNow(existingBalloonGallery);
            }

            StageBalloonCourseController existingBalloonCourse =
                stageRoot.GetComponent<StageBalloonCourseController>();
            if (existingBalloonCourse != null)
            {
                DestroyComponentNow(existingBalloonCourse);
            }

            StageCatEscapeController existingCatEscape =
                stageRoot.GetComponent<StageCatEscapeController>();
            if (existingCatEscape != null)
            {
                DestroyComponentNow(existingCatEscape);
            }

            stageRoot.gameObject.AddComponent<StageGimmickSyncManager>();
            stageRoot.gameObject.AddComponent<StageGimmickLinkController>();
            if (data != null && data.id == "1-2")
            {
                stageRoot.gameObject.AddComponent<StageBalloonGalleryController>();
            }
            if (data != null && data.id == "3-2")
            {
                stageRoot.gameObject.AddComponent<StageBalloonCourseController>();
            }
            if (data != null && data.id == "2-3")
            {
                stageRoot.gameObject.AddComponent<StageCatEscapeController>();
            }
            if (data != null && data.id == "5-3")
            {
                stageRoot.gameObject.AddComponent<StageDrawnEscortChallengeController>();
            }
            if (data != null && data.id == "10-2")
            {
                StageEscortController escort = stageRoot.gameObject.AddComponent<StageEscortController>();
                escort.Configure(data.id);
            }
            if (data != null && data.id == "4-3")
            {
                stageRoot.gameObject.AddComponent<StageBossBattleController>();
            }
            if (data != null && data.id == "6-3")
            {
                stageRoot.gameObject.AddComponent<StageAquariumSealController>();
            }
            if (data != null && data.id == "7-2")
            {
                stageRoot.gameObject.AddComponent<StageGrainCarryController>();
            }
            if (data != null && data.id == "7-3")
            {
                int playerCount = GetCurrentStagePlayerCount();
                float seconds = playerCount >= 4 ? 120f : playerCount == 3 ? 150f : 180f;
                StageTimedGoalController timer = stageRoot.gameObject.AddComponent<StageTimedGoalController>();
                timer.Configure(data.id, seconds);
            }
            if (data != null && data.id == "8-3")
            {
                stageRoot.gameObject.AddComponent<StageTiltBoardController>();
            }
            if (data != null && data.id == "13-1")
            {
                StageTowerDefenseController towerDefense = stageRoot.gameObject.AddComponent<StageTowerDefenseController>();
                towerDefense.ConfigureHardMode(data.timeLimitSeconds);
            }
            if (data != null && data.id == "9-1")
            {
                StageSlimeMissileSurvivalController slimeMissile = stageRoot.gameObject.AddComponent<StageSlimeMissileSurvivalController>();
                slimeMissile.Configure(data.timeLimitSeconds);
            }
            if (data != null && data.id == "9-2")
            {
                stageRoot.gameObject.AddComponent<StageCoinDescentController>();
            }
            if (data != null && data.id == "9-3")
            {
                stageRoot.gameObject.AddComponent<StageGrainRainController>();
            }
            if (data != null && data.id == "10-1")
            {
                StageIceSpeedrunController speedrun = stageRoot.gameObject.AddComponent<StageIceSpeedrunController>();
                speedrun.Configure(data.timeLimitSeconds);
            }
            if (data != null && data.id == "10-3")
            {
                stageRoot.gameObject.AddComponent<StageRicochetChallengeController>();
            }
            if (data != null && data.id == "11-1")
            {
                stageRoot.gameObject.AddComponent<StageMovingGauntletController>();
            }
            if (data != null && data.id == "12-1")
            {
                stageRoot.gameObject.AddComponent<StageValueCoinChallengeController>();
            }
            if (data != null && data.id == "12-2")
            {
                stageRoot.gameObject.AddComponent<StageCoinRushController>();
            }
            if (data != null && data.id == "12-3")
            {
                stageRoot.gameObject.AddComponent<StageMovingCoinChallengeController>();
            }
            if (data != null && data.id == "13-2")
            {
                stageRoot.gameObject.AddComponent<StageRicochetEnemyChallengeController>();
            }
            if (data != null && data.id == "13-3")
            {
                stageRoot.gameObject.AddComponent<StageHumanCircuitController>();
            }
            if (data != null && data.id == "14-1")
            {
                stageRoot.gameObject.AddComponent<StageWindSpeedrunController>();
            }
            if (data != null && data.id == "14-2")
            {
                stageRoot.gameObject.AddComponent<StageUmbrellaRainController>();
            }
            if (data != null && data.id == "14-3")
            {
                stageRoot.gameObject.AddComponent<StageLaserRelayController>();
            }
            if (data != null && data.id == "15-1")
            {
                stageRoot.gameObject.AddComponent<StageFlyingPlatformBossController>();
            }
            if (data != null && data.id == "15-2")
            {
                stageRoot.gameObject.AddComponent<StageSideScrollBossChaseController>();
            }
            if (data != null && data.id == "15-3")
            {
                stageRoot.gameObject.AddComponent<StageMirrorFinalBossController>();
            }
            if (data != null && data.ruleMode == StageRuleMode.Survival)
            {
                if (data.id == "8-3" || data.id == "9-1" || data.id == "13-1" || data.id == "14-3" || data.id == "15-1" || data.id == "15-2" || data.id == "15-3")
                {
                    // The stage-specific controller owns its timer, elimination and retry flow.
                }
                else if (data.id == "4-3")
                {
                    // StageBossBattleController is the elimination controller
                    // for this stage. Do not build the 11-2 survival arena.
                }
                else if (data.id == "6-2")
                {
                    StageJumpRopeController jumpRope = stageRoot.gameObject.AddComponent<StageJumpRopeController>();
                    jumpRope.Configure(data.timeLimitSeconds);
                }
                else if (data.id == "8-1")
                {
                    StagePillarSurvivalController pillarSurvival =
                        stageRoot.gameObject.AddComponent<StagePillarSurvivalController>();
                    pillarSurvival.Configure(data.timeLimitSeconds);
                }
                else if (data.id == "14-2")
                {
                    // StageUmbrellaRainController owns elimination and retries.
                }
                else
                {
                    StageSurvivalController survival = stageRoot.gameObject.AddComponent<StageSurvivalController>();
                    survival.Configure(data.timeLimitSeconds);
                }
            }
            else if (data != null && data.ruleMode == StageRuleMode.BlockBreaker)
            {
                if (data.id == "8-2")
                {
                    StageRicochetBreakerController ricochet = stageRoot.gameObject.AddComponent<StageRicochetBreakerController>();
                    ricochet.Configure(data.timeLimitSeconds);
                }
                else
                {
                    StageBlockBreakerController blockBreaker = stageRoot.gameObject.AddComponent<StageBlockBreakerController>();
                    blockBreaker.Configure(data.timeLimitSeconds);
                }
            }
        }

        private static void DestroyComponentNow(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                DestroyImmediate(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private void EnsureReferences()
        {
            if (stageRoot == null)
            {
                GameObject root = GameObject.Find("RuntimeStageRoot");
                if (root == null)
                {
                    root = new GameObject("RuntimeStageRoot");
                }

                stageRoot = root.transform;
            }

            if (objectFactory == null)
            {
                objectFactory = GetComponent<StageObjectFactory>();
                if (objectFactory == null)
                {
                    objectFactory = gameObject.AddComponent<StageObjectFactory>();
                }
            }
        }

        private void ClearStageRoot()
        {
            if (stageRoot == null)
            {
                return;
            }

            for (int i = stageRoot.childCount - 1; i >= 0; i--)
            {
                GameObject oldStageObject = stageRoot.GetChild(i).gameObject;
                oldStageObject.SetActive(false);
                Destroy(oldStageObject);
            }
        }
    }
}
