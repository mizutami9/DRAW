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
            TrySave(CreateStageData());
        }

        private bool TrySave(StageData data)
        {
            List<StageValidationIssue> issues = StageDataValidator.Validate(data, stageId);
            int errors = 0;
            int warnings = 0;
            StageValidationIssue firstError = default;
            for (int i = 0; i < issues.Count; i++)
            {
                StageValidationIssue issue = issues[i];
                if (issue.Severity == StageValidationSeverity.Error)
                {
                    if (errors == 0) firstError = issue;
                    errors++;
                    Debug.LogError($"Stage '{stageId}': {issue}");
                }
                else
                {
                    warnings++;
                    Debug.LogWarning($"Stage '{stageId}': {issue}");
                }
            }
            if (errors > 0)
            {
                SetStatus(LocalizationManager.Format(
                    "stage_editor_status_validation_failed", errors, firstError.Message));
                return false;
            }
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "Resources", "Stages");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{stageId}.json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            string assetPath = $"Assets/Resources/Stages/{stageId}.json";
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            SetStatus(warnings > 0
                ? LocalizationManager.Format("stage_editor_status_saved_with_warnings", assetPath, warnings)
                : LocalizationManager.Format("stage_editor_status_saved", assetPath));
            return true;
#else
            SetStatus(LocalizationManager.T("stage_editor_status_save_editor_only"));
            return false;
#endif
        }

        public void TestPlay()
        {
            StageData data = CreateStageData();
            if (!TrySave(data)) return;
            StoreEditorCameraStateForTest();
            stageLoader?.LoadStage(data);
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
            EnsureLaserRelayEditorLayouts();
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
            else if (stageId == "13-3")
            {
                StageHumanCircuitController.CreateEditorPreview(editorRoot);
            }
            ApplyLaserRelayLayoutVisibility();
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
            RefreshLaserRelayEditorMarkerVisual(obj, data);
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

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor
    {
        private const string LaserRelayLayoutRootPrefix = "14-3-layout-";
        private int laserRelayPreviewPlayers = 1;
        private int laserRelayPreviewRound = 1;

        public bool IsLaserRelayLayoutEditor => active && stageId == "14-3";
        public int LaserRelayPreviewPlayers => laserRelayPreviewPlayers;
        public int LaserRelayPreviewRound => laserRelayPreviewRound;

        public void CycleLaserRelayPreviewPlayers()
        {
            if (!IsLaserRelayLayoutEditor) return;
            laserRelayPreviewPlayers = laserRelayPreviewPlayers % 4 + 1;
            ApplyLaserRelayLayoutVisibility();
            RefreshListPanel();
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
            SetStatus(LocalizationManager.Format("laser_relay_editor_preview", laserRelayPreviewPlayers, laserRelayPreviewRound));
        }

        public void CycleLaserRelayPreviewRound()
        {
            if (!IsLaserRelayLayoutEditor) return;
            laserRelayPreviewRound = laserRelayPreviewRound % 3 + 1;
            ApplyLaserRelayLayoutVisibility();
            RefreshListPanel();
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
            SetStatus(LocalizationManager.Format("laser_relay_editor_preview", laserRelayPreviewPlayers, laserRelayPreviewRound));
        }

        private void EnsureLaserRelayEditorLayouts()
        {
            if (stageId != "14-3") return;
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData item = objects[i];
                if (item != null && item.type == StageObjectType.EscortPlayerOneWayFloor
                    && !IsLaserRelayLayoutObject(item))
                {
                    AssignLaserRelayLayoutObjectId(item);
                }
            }
            bool hasLayout = objects.Exists(item => item != null && !string.IsNullOrEmpty(item.objectId)
                && item.objectId.StartsWith(LaserRelayLayoutRootPrefix, System.StringComparison.Ordinal));
            if (hasLayout) return;
            for (int players = 1; players <= 4; players++)
            for (int targetRound = 1; targetRound <= 3; targetRound++)
                objects.AddRange(StageLaserRelayController.CreateEditorLayoutDefaults(players, targetRound));
        }

        private bool IsLaserRelayLayoutObject(StageObjectData data)
        {
            return data != null && !string.IsNullOrEmpty(data.objectId)
                && data.objectId.StartsWith(LaserRelayLayoutRootPrefix, System.StringComparison.Ordinal);
        }

        private bool IsLaserRelayLayoutObjectVisible(StageObjectData data)
        {
            if (!IsLaserRelayLayoutObject(data)) return true;
            return data.objectId.StartsWith(StageLaserRelayController.GetEditorLayoutPrefix(
                laserRelayPreviewPlayers, laserRelayPreviewRound), System.StringComparison.Ordinal);
        }

        private void ApplyLaserRelayLayoutVisibility()
        {
            if (stageId != "14-3" || editorRoot == null) return;
            string visiblePrefix = StageLaserRelayController.GetEditorLayoutPrefix(
                laserRelayPreviewPlayers, laserRelayPreviewRound);
            StageEditorObject[] markers = editorRoot.GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker == null || string.IsNullOrEmpty(marker.objectId)
                    || !marker.objectId.StartsWith(LaserRelayLayoutRootPrefix, System.StringComparison.Ordinal)) continue;
                marker.gameObject.SetActive(marker.objectId.StartsWith(visiblePrefix, System.StringComparison.Ordinal));
            }
            if (selectedData != null && !IsLaserRelayLayoutObjectVisible(selectedData))
            {
                selectedData = null;
                selectedObject = null;
                SetSelectionBox(false);
                RefreshText();
            }
        }

        private string CreateDuplicateObjectId(StageObjectData source)
        {
            if (stageId == "14-3" && IsLaserRelayLayoutObject(source))
            {
                string prefix = StageLaserRelayController.GetEditorLayoutPrefix(
                    laserRelayPreviewPlayers, laserRelayPreviewRound);
                string kind = GetLaserRelayLayoutKind(source);
                return $"{prefix}{kind}-copy-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            }
            return StageObjectId.New();
        }

        private void AssignLaserRelayLayoutObjectId(StageObjectData data)
        {
            if (!IsLaserRelayLayoutEditor || data == null) return;
            string prefix = StageLaserRelayController.GetEditorLayoutPrefix(
                laserRelayPreviewPlayers, laserRelayPreviewRound);
            data.objectId = $"{prefix}{GetLaserRelayLayoutKind(data)}-custom-" +
                System.Guid.NewGuid().ToString("N").Substring(0, 8);
            data.keepSeparate = true;
        }

        private static string GetLaserRelayLayoutKind(StageObjectData data)
        {
            if (data != null && !string.IsNullOrEmpty(data.objectId))
            {
                if (data.objectId.Contains("-source-")) return "source";
                if (data.objectId.Contains("-goal-")) return "goal";
                if (data.objectId.Contains("-player-")) return "player";
                if (data.objectId.Contains("-passfloor-")) return "passfloor";
            }
            if (data != null && (data.type == StageObjectType.OneWayPlatform
                || data.type == StageObjectType.MovingOneWayPlatform
                || data.type == StageObjectType.EscortPlayerOneWayFloor)) return "passfloor";
            if (data != null && data.type == StageObjectType.JumpPad) return "jumppad";
            if (data != null && data.type == StageObjectType.BackgroundArrow) return "source";
            if (data != null && data.type == StageObjectType.BackgroundLightBulb) return "goal";
            if (data != null && data.type == StageObjectType.BackgroundStickFigure) return "player";
            return "wall";
        }

        private void RefreshLaserRelayEditorMarkerVisual(GameObject obj, StageObjectData data)
        {
            if (obj == null || !IsLaserRelayLayoutObject(data)) return;
            string resourcePath = null;
            Vector2 artSize = data.size;
            if (data.objectId.Contains("-source-"))
                resourcePath = "StageObjects/NicoDraw/laser-relay-emitter";
            else if (data.objectId.Contains("-goal-"))
                resourcePath = "StageObjects/NicoDraw/laser-relay-bulb";
            if (resourcePath == null) return;

            SpriteRenderer[] oldRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < oldRenderers.Length; i++) oldRenderers[i].enabled = false;
            StageGun.TryCreateResourceSprite(obj.transform, resourcePath,
                data.objectId.Contains("-source-") ? "Laser Emitter Preview" : "Bulb Preview",
                artSize, 40);
        }
    }
}
