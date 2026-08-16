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
        public StageData CurrentStageData { get; private set; }

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
            CurrentStageData = null;
            ResetStageFallBoundary();
            ClearStageRoot();
            StageBackgroundAppearance.Reset();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(true);
            }
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
            if (data == null)
            {
                return;
            }

            EnsureReferences();
            CurrentStageData = data;
            RefreshStageFallBoundary(data.objects);
            ClearStageRoot();
            StageBackgroundAppearance.Apply(StageBackgroundAppearance.Parse(data.backgroundColorHex));
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }

            objectFactory.FitSeparateBridges(data.objects);

            for (int i = 0; i < data.objects.Length; i++)
            {
                StageObjectData obj = data.objects[i];
                if (obj == null)
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

            stageRoot.gameObject.AddComponent<StageGimmickSyncManager>();
            stageRoot.gameObject.AddComponent<StageGimmickLinkController>();
            if (data != null && data.ruleMode == StageRuleMode.Survival)
            {
                if (data.id == "6-2")
                {
                    StageJumpRopeController jumpRope = stageRoot.gameObject.AddComponent<StageJumpRopeController>();
                    jumpRope.Configure(data.timeLimitSeconds);
                }
                else
                {
                    StageSurvivalController survival = stageRoot.gameObject.AddComponent<StageSurvivalController>();
                    survival.Configure(data.timeLimitSeconds);
                }
            }
            else if (data != null && data.ruleMode == StageRuleMode.BlockBreaker)
            {
                StageBlockBreakerController blockBreaker = stageRoot.gameObject.AddComponent<StageBlockBreakerController>();
                blockBreaker.Configure(data.timeLimitSeconds);
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
