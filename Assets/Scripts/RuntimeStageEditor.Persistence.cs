using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor : MonoBehaviour
    {
        public void Save()
        {
            StageData data = CreateStageData();
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "Resources", "Stages");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{stageId}.json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            string assetPath = $"Assets/Resources/Stages/{stageId}.json";
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            SetStatus(LocalizationManager.Format("stage_editor_status_saved", assetPath));
#else
            SetStatus(LocalizationManager.T("stage_editor_status_save_editor_only"));
#endif
        }

        public void TestPlay()
        {
            StoreEditorCameraStateForTest();
            Save();
            stageLoader?.LoadStage(CreateStageData());
            Close();
        }

        public void ResumeAfterTestPlay()
        {
            active = true;
            dragging = false;
            drawingRect = false;
            selectedData = null;
            selectedObject = null;
            linkSourceData = null;
            stageLoader?.HideStages();
            StageBackgroundAppearance.Apply(stageBackgroundColor);
            BuildEditorObjects();
            RestoreDebugPlayerPosition();
            RestoreEditorCameraStateAfterTest();
            SetPanel(true);
            RefreshObjectTypeDropdown();
            EnsureListReferences();
            RefreshText();
            RefreshListPanel();
            SetStatus(LocalizationManager.T("stage_editor_status_debug_start_help"));
        }

        private void StoreEditorCameraStateForTest()
        {
            EnsureReferences();
            if (worldCamera == null)
            {
                return;
            }

            editorCameraPositionBeforeTest = worldCamera.transform.position;
            editorCameraSizeBeforeTest = worldCamera.orthographicSize;
            hasEditorCameraStateBeforeTest = true;
            worldCamera.orthographicSize = Mathf.Max(0.1f, testPlayCameraSize);
        }

        private void RestoreEditorCameraStateAfterTest()
        {
            if (!hasEditorCameraStateBeforeTest)
            {
                return;
            }

            EnsureReferences();
            if (worldCamera == null)
            {
                return;
            }

            worldCamera.transform.position = editorCameraPositionBeforeTest;
            worldCamera.orthographicSize = editorCameraSizeBeforeTest;
            hasEditorCameraStateBeforeTest = false;
        }

        private void LoadWorkingData()
        {
            objects.Clear();
            selectedData = null;
            selectedObject = null;
            stageBackgroundColor = StageBackgroundAppearance.DefaultColor;
            stageRuleMode = StageRuleMode.Normal;
            stageTimeLimitSeconds = 60f;
            stageCollectionTarget = StageObjectType.CollectibleFish;
            stageRequiredCollectionCount = 1;

            TextAsset asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset != null)
            {
                StageData loaded = JsonUtility.FromJson<StageData>(asset.text);
                if (loaded != null && loaded.objects != null)
                {
                    displayName = string.IsNullOrEmpty(loaded.displayName) ? displayName : loaded.displayName;
                    stageBackgroundColor = StageBackgroundAppearance.Parse(loaded.backgroundColorHex);
                    stageRuleMode = loaded.ruleMode;
                    stageTimeLimitSeconds = Mathf.Clamp(loaded.timeLimitSeconds > 0f ? loaded.timeLimitSeconds : 60f, 5f, 1800f);
                    stageCollectionTarget = loaded.collectionTarget == StageObjectType.CollectibleCoin
                        || loaded.collectionTarget == StageObjectType.CollectibleStar
                            ? loaded.collectionTarget
                            : StageObjectType.CollectibleFish;
                    stageRequiredCollectionCount = Mathf.Clamp(loaded.requiredCollectionCount, 0, 2000);
                    for (int i = 0; i < loaded.objects.Length; i++)
                    {
                        if (loaded.objects[i] != null)
                        {
                            objects.Add(CloneData(loaded.objects[i]));
                        }
                    }
                }
            }
            StageBackgroundAppearance.Apply(stageBackgroundColor);

            if (objects.Count == 0)
            {
                objects.Add(StageObjectFactory.CreateDefaultData(StageObjectType.Spawn, new Vector2(-5f, 1.4f)));
                objects.Add(new StageObjectData
                {
                    objectId = "Platform_Start",
                    type = StageObjectType.Platform,
                    position = new Vector2(-2f, -1f),
                    size = new Vector2(7f, 0.45f),
                    rotation = 0f
                });
                objects.Add(StageObjectFactory.CreateDefaultData(StageObjectType.Goal, new Vector2(5f, 0.1f)));
            }

            if (stageRuleMode == StageRuleMode.TimedCollection
                && !objects.Exists(item => item != null && item.type == StageObjectType.ChallengeClock))
            {
                objects.Add(StageObjectFactory.CreateDefaultData(StageObjectType.ChallengeClock, new Vector2(0f, 4f)));
            }
        }

        private void BuildEditorObjects()
        {
            EnsureUniqueObjectIds();
            ClearEditorObjects();
            EnsureReferences();
            objectFactory?.FitSeparateBridges(objects);
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (snapToGrid
                    && data != null
                    && data.keepSeparate
                    && data.type == StageObjectType.Platform
                    && data.size.x > data.size.y * 1.5f)
                {
                    // Re-apply docking to bridges loaded from older editor saves so
                    // their endpoints and walking surface meet the banks exactly.
                    data.position = SnapJoinPosition(data, data.position);
                }

                CreateEditorObject(data);
            }

            RefreshBridgeConnectionVisuals();
            if (stageId == "12-2")
            {
                StageCoinRushController.CreateEditorPreview(editorRoot);
            }
            else if (stageId == "12-3")
            {
                StageMovingCoinChallengeController.CreateEditorPreview(editorRoot);
            }
        }

        private void CreateEditorObject(StageObjectData data)
        {
            EnsureReferences();
            if (objectFactory == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_factory_missing"));
                return;
            }

            GameObject obj = objectFactory.Create(data, editorRoot);
            StageEditorObject marker = obj != null ? obj.GetComponent<StageEditorObject>() : null;
            if (marker != null)
            {
                marker.objectId = data.objectId;
                marker.type = data.type;
                marker.size = data.size;
                marker.actionStrength = data.actionStrength;
                marker.movementAngle = data.movementAngle;
                marker.movementSpeed = data.movementSpeed;
                marker.spawnPattern = data.spawnPattern;
                marker.spawnBoxSize = data.spawnBoxSize;
                marker.bombFuseSeconds = data.bombFuseSeconds;
                marker.linkTargetId = data.linkTargetId;
                marker.linkAction = data.linkAction;
            }
        }

        private void RefreshBridgeConnectionVisuals()
        {
            objectFactory?.RefreshBridgeConnectionVisuals(objects, editorRoot);
        }

        private void ClearEditorObjects()
        {
            if (editorRoot == null)
            {
                return;
            }

            for (int i = editorRoot.childCount - 1; i >= 0; i--)
            {
                DestroyEditorObject(editorRoot.GetChild(i).gameObject);
            }

            SetSelectionBox(false);
            ClearDragPreview();
        }

        private StageData CreateStageData()
        {
            return new StageData
            {
                id = stageId,
                displayName = displayName,
                backgroundColorHex = StageBackgroundAppearance.ToHex(stageBackgroundColor),
                ruleMode = stageRuleMode,
                timeLimitSeconds = stageTimeLimitSeconds,
                collectionTarget = stageCollectionTarget,
                requiredCollectionCount = stageRequiredCollectionCount,
                objects = objects.ToArray()
            };
        }

        private StageObjectData CloneData(StageObjectData data)
        {
            return new StageObjectData
            {
                objectId = string.IsNullOrEmpty(data.objectId) ? StageObjectId.New() : data.objectId,
                type = data.type,
                position = data.position,
                size = data.size,
                rotation = data.rotation,
                pathPoints = data.pathPoints != null ? (Vector2[])data.pathPoints.Clone() : System.Array.Empty<Vector2>(),
                pathThickness = data.pathThickness,
                connectedRects = CloneConnectedRects(data.connectedRects),
                keepSeparate = data.keepSeparate,
                actionStrength = data.actionStrength,
                movementAngle = data.movementAngle,
                movementSpeed = data.movementSpeed,
                spawnPattern = data.spawnPattern,
                spawnBoxSize = data.spawnBoxSize,
                bombFuseSeconds = data.bombFuseSeconds,
                linkTargetId = data.linkTargetId,
                linkAction = data.linkAction
            };
        }

        private void EnsureUniqueObjectIds()
        {
            HashSet<string> usedIds = new HashSet<string>();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(data.objectId) || !usedIds.Add(data.objectId))
                {
                    do
                    {
                        data.objectId = StageObjectId.New();
                    }
                    while (!usedIds.Add(data.objectId));
                }
            }
        }

        private static StageRectPartData[] CloneConnectedRects(StageRectPartData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<StageRectPartData>();
            }

            StageRectPartData[] clone = new StageRectPartData[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = new StageRectPartData
                {
                    position = source[i].position,
                    size = source[i].size
                };
            }

            return clone;
        }
    }
}
