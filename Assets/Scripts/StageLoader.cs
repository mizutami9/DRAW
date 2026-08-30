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
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(0f, -0.2f), new Vector2(28f, 0.55f));
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(0f, 6.75f), new Vector2(28f, 0.55f));
            AddTitleObject(objects, StageObjectType.Wall, new Vector2(-13.75f, 3.275f), new Vector2(0.55f, 6.95f));
            AddTitleObject(objects, StageObjectType.Wall, new Vector2(13.75f, 3.275f), new Vector2(0.55f, 6.95f));

            // Keep the middle open for running around, while placing a few simple
            // shelves and toys around the edges of the room.
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(-8.7f, 1.5f), new Vector2(5.2f, 0.38f));
            AddTitleObject(objects, StageObjectType.Platform, new Vector2(8.6f, 2.2f), new Vector2(5.4f, 0.38f));
            AddTitleObject(objects, StageObjectType.OneWayPlatform, new Vector2(0.2f, 4.65f), new Vector2(5.3f, 0.28f));

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

            EnsureReferences();
            StagePoseTowerRandomizer.Prepare(data);
            CurrentStageData = data;
            RefreshStageFallBoundary(data.objects);
            ClearStageRoot();
            StageBackgroundAppearance.Apply(StageBackgroundAppearance.Parse(data.backgroundColorHex));
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }

            objectFactory.FitSeparateBridges(data.objects);

            int playerCount = GetCurrentStagePlayerCount();
            HashSet<string> enabledBombDroppers = BuildPlayerScaledBombDropperIds(data, playerCount);

            for (int i = 0; i < data.objects.Length; i++)
            {
                StageObjectData obj = data.objects[i];
                if (obj == null)
                {
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

            objectFactory.RefreshBridgeConnectionVisuals(data.objects, stageRoot);

            ConfigureStageGimmicks(data);
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

            stageRoot.gameObject.AddComponent<StageGimmickSyncManager>();
            stageRoot.gameObject.AddComponent<StageGimmickLinkController>();
            if (data != null && (data.id == "5-3" || data.id == "10-2"))
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
                stageRoot.gameObject.AddComponent<StageSpikeChaseController>();
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
                StageTowerDefenseController towerDefense = stageRoot.gameObject.AddComponent<StageTowerDefenseController>();
                towerDefense.Configure(data.timeLimitSeconds);
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
                stageRoot.gameObject.AddComponent<StageLinkedShieldSurvivalController>();
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
                else if (data.id == "6-3")
                {
                    // StageSpikeChaseController owns elimination and retries.
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
