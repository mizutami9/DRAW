using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor : MonoBehaviour
    {
        public void CycleStageRuleMode()
        {
            PushUndo();
            if (stageId == "11-2" || stageId == "11-3")
            {
                stageRuleMode = stageRuleMode == StageRuleMode.Normal
                    ? StageRuleMode.TimedCollection
                    : stageRuleMode == StageRuleMode.TimedCollection
                        ? stageId == "11-3" ? StageRuleMode.BlockBreaker : StageRuleMode.Survival
                        : StageRuleMode.Normal;
            }
            else
            {
                stageRuleMode = stageRuleMode == StageRuleMode.Normal
                    ? StageRuleMode.TimedCollection
                    : StageRuleMode.Normal;
            }
            if (stageRuleMode == StageRuleMode.TimedCollection)
            {
                if (stageRequiredCollectionCount == 1)
                {
                    stageRequiredCollectionCount = 0;
                }
                EnsureChallengeClockObject();
            }
            SetStatus(LocalizationManager.Format("stage_editor_status_rule", StageRuleModeLabel));
            RefreshText();
        }

        private void EnsureChallengeClockObject()
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].type == StageObjectType.ChallengeClock)
                {
                    return;
                }
            }

            Vector2 position = worldCamera != null
                ? new Vector2(worldCamera.transform.position.x, worldCamera.transform.position.y + 3f)
                : new Vector2(0f, 4f);
            if (snapToGrid)
            {
                position = Snap(position);
            }
            StageObjectData clock = StageObjectFactory.CreateDefaultData(StageObjectType.ChallengeClock, position);
            objects.Add(clock);
            CreateEditorObject(clock);
            RefreshListPanel();
        }

        public void CycleStageCollectionTarget()
        {
            PushUndo();
            stageCollectionTarget = stageCollectionTarget == StageObjectType.CollectibleFish
                ? StageObjectType.CollectibleCoin
                : stageCollectionTarget == StageObjectType.CollectibleCoin
                    ? StageObjectType.CollectibleStar
                    : StageObjectType.CollectibleFish;
            RefreshText();
        }

        public void AdjustStageTimeLimit()
        {
            PushUndo();
            float[] values = { 30f, 60f, 90f, 120f, 180f, 300f };
            int current = 0;
            float closest = float.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                float distance = Mathf.Abs(values[i] - stageTimeLimitSeconds);
                if (distance < closest)
                {
                    closest = distance;
                    current = i;
                }
            }
            int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            stageTimeLimitSeconds = values[(current + direction + values.Length) % values.Length];
            RefreshText();
        }

        public void AdjustStageRequiredCount()
        {
            PushUndo();
            int[] values = { 0, 1, 3, 5, 10, 20, 30 };
            int current = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == stageRequiredCollectionCount)
                {
                    current = i;
                    break;
                }
            }
            int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            stageRequiredCollectionCount = values[(current + direction + values.Length) % values.Length];
            RefreshText();
        }

        public void SetAddType(StageObjectType type)
        {
            addType = type;
            currentCategory = StageObjectCatalog.Get(type).Category;
            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            SetStatus(!terrainFreehand
                ? LocalizationManager.T("stage_editor_status_select_mode")
                : IsFreehandTerrainType(type) && terrainStraightLine
                ? LocalizationManager.Format("stage_editor_status_add_straight", GetObjectLabel(type))
                : IsFreehandTerrainType(type)
                    ? LocalizationManager.Format("stage_editor_status_add_freehand", GetObjectLabel(type))
                : IsBlockType(type)
                    ? LocalizationManager.Format("stage_editor_status_add_rect", GetObjectLabel(type))
                : LocalizationManager.Format("stage_editor_status_add_point", GetObjectLabel(type)));
            RefreshObjectTypeDropdown();
            RefreshText();
            RefreshListPanel();
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        public void SetAddTypeFromDropdown(int index)
        {
            if (updatingDropdown || index < 0 || index >= filteredPaletteTypes.Count)
            {
                return;
            }

            SetAddType(filteredPaletteTypes[index]);
        }

        public void SetCategoryFromDropdown(int index)
        {
            if (updatingCategoryDropdown || index < 0 || index >= StageObjectCatalog.Categories.Length)
            {
                return;
            }

            currentCategory = StageObjectCatalog.Categories[index];
            RefreshObjectTypeDropdown();
            if (filteredPaletteTypes.Count > 0)
            {
                SetAddType(filteredPaletteTypes[0]);
            }
        }

        public void SetSearchText(string text)
        {
            RefreshObjectTypeDropdown();
            if (filteredPaletteTypes.Count > 0 && !filteredPaletteTypes.Contains(addType))
            {
                SetAddType(filteredPaletteTypes[0]);
            }
        }

        public void ToggleSnap()
        {
            snapToGrid = !snapToGrid;
            RefreshText();
            RefreshListPanel();
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        public void ToggleTerrainFreehand()
        {
            terrainFreehand = !terrainFreehand;
            SetStatus(terrainFreehand
                ? LocalizationManager.T("stage_editor_status_draw_mode")
                : LocalizationManager.T("stage_editor_status_select_mode"));
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        public void ToggleTerrainStraightLine()
        {
            terrainStraightLine = !terrainStraightLine;
            SetStatus(terrainStraightLine
                ? LocalizationManager.T("stage_editor_status_straight_on")
                : LocalizationManager.T("stage_editor_status_straight_off"));
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        public void AdjustTerrainThickness(float delta)
        {
            SetTerrainThickness(terrainPathThickness + delta);
        }

        public void SetTerrainThickness(float value)
        {
            terrainPathThickness = Mathf.Clamp(Mathf.Round(value * 20f) / 20f, 0.25f, 4f);
            SetStatus(LocalizationManager.Format("stage_editor_status_thickness", terrainPathThickness));
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        public void BeginActionStrengthEdit()
        {
            if (SelectedSupportsActionStrength)
            {
                PushUndo();
            }
        }

        public void BeginMovementSpeedEdit()
        {
            if (SelectedSupportsSecondarySlider)
            {
                PushUndo();
            }
        }

        public void SetSelectedMovementSpeed(float value)
        {
            if (!SelectedSupportsSecondarySlider)
            {
                return;
            }

            if (SelectedSupportsBombFuse)
            {
                selectedData.bombFuseSeconds = Mathf.Clamp(
                    Mathf.Round(value * 10f) / 10f,
                    1f,
                    15f);
                RebuildSelectedObject();
                SetStatus(LocalizationManager.Format(
                    "stage_editor_status_bomb_fuse_seconds",
                    selectedData.bombFuseSeconds));
                RefreshText();
                return;
            }

            selectedData.movementSpeed = Mathf.Clamp(
                Mathf.Round(value * 10f) / 10f,
                SelectedSecondarySliderMinimum,
                SelectedSecondarySliderMaximum);
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_move_speed",
                selectedData.movementSpeed));
            RefreshText();
        }

        public void SetSelectedActionStrength(float value)
        {
            if (!SelectedSupportsActionStrength)
            {
                return;
            }

            float minimum = SelectedActionStrengthMinimum;
            float maximum = SelectedActionStrengthMaximum;
            float rounded = SelectedIsBombWall
                ? Mathf.Round(value)
                : SelectedIsCrumblingFloor || SelectedIsConveyor || SelectedIsDropper
                    ? Mathf.Round(value * 10f) / 10f
                    : Mathf.Round(value * 2f) / 2f;
            selectedData.actionStrength = Mathf.Clamp(rounded, minimum, maximum);
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format(
                selectedData.type == StageObjectType.MovingPlatform
                    || selectedData.type == StageObjectType.MovingOneWayPlatform
                    || selectedData.type == StageObjectType.Elevator
                    ? "stage_editor_status_move_distance"
                    : selectedData.type == StageObjectType.FallingFloor
                        ? "stage_editor_status_crumble_delay"
                        : selectedData.type == StageObjectType.BreakableWall || selectedData.type == StageObjectType.BulletBreakableWall
                            ? selectedData.type == StageObjectType.BulletBreakableWall
                                ? "stage_editor_status_bullet_wall_hits"
                                : "stage_editor_status_bomb_wall_hits"
                        : IsConveyorType(selectedData.type)
                            ? "stage_editor_status_conveyor_speed"
                            : selectedData.type == StageObjectType.BoxDropper || selectedData.type == StageObjectType.SpikeDropper || selectedData.type == StageObjectType.BombDropper
                                ? "stage_editor_status_drop_interval"
                                : "stage_editor_status_action_strength",
                selectedData.actionStrength));
            RefreshText();
        }

        public void ToggleSelectedConveyorDirection()
        {
            if (!SelectedIsConveyor)
            {
                return;
            }

            PushUndo();
            bool currentlyLeft = Mathf.Cos(selectedData.movementAngle * Mathf.Deg2Rad) < 0f;
            selectedData.movementAngle = currentlyLeft ? 0f : 180f;
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_conveyor_direction",
                SelectedConveyorDirectionLabel));
            RefreshText();
        }

        public void CycleSelectedDropperPattern()
        {
            if (!SelectedUsesDropperPattern)
            {
                return;
            }

            PushUndo();
            int patternCount = SelectedIsBombDropper ? 3 : SelectedIsEnemyDropper ? 5 : 4;
            selectedData.spawnPattern = (Mathf.Clamp(selectedData.spawnPattern, 0, patternCount - 1) + 1) % patternCount;
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format(
                SelectedIsEnemyDropper
                    ? "stage_editor_status_enemy_pattern"
                    : SelectedIsBombDropper ? "stage_editor_status_bomb_pattern" : "stage_editor_status_box_pattern",
                SelectedDropperPatternLabel));
            RefreshText();
        }

        public void BeginDropperBoxSizeEdit()
        {
            if (SelectedIsDropper)
            {
                PushUndo();
            }
        }

        public void SetSelectedDropperBoxSize(float value)
        {
            if (!SelectedIsDropper)
            {
                return;
            }

            selectedData.spawnBoxSize = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.5f, 2f);
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format(
                SelectedIsSpikeDropper
                    ? "stage_editor_status_spike_size"
                    : SelectedIsBombDropper
                        ? "stage_editor_status_bomb_size"
                        : SelectedIsEnemyDropper ? "stage_editor_status_enemy_size" : "stage_editor_status_box_size",
                selectedData.spawnBoxSize));
            RefreshText();
        }

        public void SetSelectedWeightThreshold(string value)
        {
            if (!SelectedSupportsWeightThreshold)
            {
                return;
            }

            if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                && !float.TryParse(value, out parsed))
            {
                SetStatus(LocalizationManager.T("stage_editor_status_weight_threshold_invalid"));
                editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
                return;
            }

            PushUndo();
            selectedData.actionStrength = Mathf.Clamp(Mathf.Round(parsed), 1f, 2000f);
            RebuildSelectedObject();
            SetStatus(LocalizationManager.Format("stage_editor_status_weight_threshold", selectedData.actionStrength));
            RefreshText();
        }

        public void ToggleTerrainKeepSeparate()
        {
            terrainKeepSeparate = !terrainKeepSeparate;
            if (terrainKeepSeparate && SplitSelectedConnectedTerrain())
            {
                SetStatus(LocalizationManager.T("stage_editor_status_separate_split"));
                editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
                return;
            }

            if (selectedData != null
                && IsFreehandTerrainType(selectedData.type)
                && (selectedData.connectedRects == null || selectedData.connectedRects.Length == 0))
            {
                selectedData.keepSeparate = terrainKeepSeparate;
            }

            SetStatus(terrainKeepSeparate
                ? LocalizationManager.T("stage_editor_status_separate_on")
                : LocalizationManager.T("stage_editor_status_separate_off"));
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        private void RefreshText()
        {
            if (stageText != null)
            {
                stageText.text = LocalizationManager.Format("stage_label", stageId);
            }

            if (selectedText != null)
            {
                if (rangeSelectedObjects.Count > 1)
                {
                    selectedText.text = LocalizationManager.Format(
                        "stage_editor_selected_multiple",
                        rangeSelectedObjects.Count);
                }
                else if (selectedData == null)
                {
                    selectedText.text = LocalizationManager.Format("stage_editor_selected_add", GetObjectLabel(addType), snapToGrid ? "ON" : "OFF");
                }
                else
                {
                    selectedText.text = LocalizationManager.Format(
                        "stage_editor_selected_object",
                        GetObjectLabel(selectedData.type),
                        selectedData.position.x,
                        selectedData.position.y,
                        selectedData.size.x,
                        selectedData.size.y);
                    if (selectedData.type == StageObjectType.StageBoundary)
                    {
                        selectedText.text += "\n" + LocalizationManager.T("stage_editor_boundary_resize_hint");
                    }
                }
            }

            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        private void SetupObjectTypeDropdown()
        {
            if (objectTypeDropdown == null)
            {
                return;
            }

            objectTypeDropdown.onValueChanged.RemoveListener(SetAddTypeFromDropdown);
            if (searchInput != null)
            {
                searchInput.onValueChanged.RemoveListener(SetSearchText);
                searchInput.onValueChanged.AddListener(SetSearchText);
            }

            RefreshObjectTypeDropdown();
            objectTypeDropdown.onValueChanged.AddListener(SetAddTypeFromDropdown);
        }

        private void SetupCategoryDropdown()
        {
            if (categoryDropdown == null)
            {
                return;
            }

            categoryDropdown.onValueChanged.RemoveListener(SetCategoryFromDropdown);
            categoryDropdown.ClearOptions();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            StageObjectCategory[] categories = StageObjectCatalog.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                options.Add(new Dropdown.OptionData(StageObjectCatalog.GetCategoryLabel(categories[i])));
            }

            categoryDropdown.AddOptions(options);
            categoryDropdown.onValueChanged.AddListener(SetCategoryFromDropdown);
            RefreshCategoryDropdown();
        }

        private void RefreshCategoryDropdown()
        {
            if (categoryDropdown == null)
            {
                return;
            }

            int index = 0;
            StageObjectCategory[] categories = StageObjectCatalog.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i] == currentCategory)
                {
                    index = i;
                    break;
                }
            }

            updatingCategoryDropdown = true;
            categoryDropdown.value = index;
            categoryDropdown.RefreshShownValue();
            updatingCategoryDropdown = false;
        }

        private void BuildFilteredPalette()
        {
            filteredPaletteTypes.Clear();
            string search = searchInput != null ? searchInput.text : string.Empty;
            search = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
            foreach (StageObjectCatalogEntry entry in StageObjectCatalog.All)
            {
                if (!StageObjectCatalog.IsPaletteVisible(entry.Type))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(search) && entry.Category != currentCategory)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search)
                    && entry.Label.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && entry.LabelKey.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Type.ToString().IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filteredPaletteTypes.Add(entry.Type);
            }
        }

        private void RefreshObjectTypeDropdown()
        {
            if (objectTypeDropdown == null)
            {
                return;
            }

            BuildFilteredPalette();
            objectTypeDropdown.ClearOptions();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            for (int i = 0; i < filteredPaletteTypes.Count; i++)
            {
                options.Add(new Dropdown.OptionData(GetObjectLabel(filteredPaletteTypes[i])));
            }

            if (options.Count == 0)
            {
                options.Add(new Dropdown.OptionData(LocalizationManager.T("stage_editor_no_match")));
            }

            updatingDropdown = true;
            objectTypeDropdown.AddOptions(options);

            int index = 0;
            for (int i = 0; i < filteredPaletteTypes.Count; i++)
            {
                if (filteredPaletteTypes[i] == addType)
                {
                    index = i;
                    break;
                }
            }

            objectTypeDropdown.value = index;
            objectTypeDropdown.RefreshShownValue();
            updatingDropdown = false;
            RefreshCategoryDropdown();
        }

        private static string GetObjectLabel(StageObjectType type)
        {
            return StageObjectCatalog.Get(type).Label;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void SetPanel(bool visible)
        {
            if (editorPanel != null)
            {
                editorPanel.SetActive(visible);
            }
        }
    }
}
