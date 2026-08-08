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

            bool hasSolidFloor = false;
            float lowestSolidBottom = 0f;
            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData obj = objects[i];
                if (obj == null)
                {
                    continue;
                }

                float radians = obj.rotation * Mathf.Deg2Rad;
                float verticalExtent = Mathf.Abs(Mathf.Sin(radians)) * Mathf.Abs(obj.size.x) * 0.5f
                    + Mathf.Abs(Mathf.Cos(radians)) * Mathf.Abs(obj.size.y) * 0.5f;
                float bottomY = obj.position.y - verticalExtent;
                if (obj.type == StageObjectType.StageBoundary)
                {
                    if (!hasStageFallBoundary || bottomY < stageFallBoundaryY)
                    {
                        hasStageFallBoundary = true;
                        stageFallBoundaryY = bottomY;
                    }
                    continue;
                }

                if (StageObjectCatalog.Get(obj.type).Kind == StageObjectKind.Solid
                    && (!hasSolidFloor || bottomY < lowestSolidBottom))
                {
                    hasSolidFloor = true;
                    lowestSolidBottom = bottomY;
                }
            }

            // A stage may be expanded downward after its boundary was placed.
            // Never let the fall-reset line sit above real solid terrain, or a
            // player falling toward that terrain will be respawned before landing.
            if (hasStageFallBoundary && hasSolidFloor)
            {
                stageFallBoundaryY = Mathf.Min(stageFallBoundaryY, lowestSolidBottom);
            }
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

            stageRoot.gameObject.AddComponent<StageGimmickSyncManager>();
            stageRoot.gameObject.AddComponent<StageGimmickLinkController>();
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
