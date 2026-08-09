using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor : MonoBehaviour
    {
        public enum CopyDirection
        {
            Right,
            Down,
            Left,
            Up
        }

        [SerializeField] private GameObject editorPanel;
        [SerializeField] private StageLoader stageLoader;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private StageObjectFactory objectFactory;
        [SerializeField] private Transform editorRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform uiBlocker;
        [SerializeField] private Text stageText;
        [SerializeField] private Text selectedText;
        [SerializeField] private Text statusText;
        [SerializeField] private Dropdown categoryDropdown;
        [SerializeField] private Dropdown objectTypeDropdown;
        [SerializeField] private InputField searchInput;
        [SerializeField] private string stageId = "1-1";
        [SerializeField] private string displayName = "New Stage";
        [SerializeField] private float gridSize = 0.5f;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float cameraMoveSpeed = 9f;
        [SerializeField] private float minCameraSize = 2.5f;
        [SerializeField] private float maxCameraSize = 24f;
        [SerializeField] private float testPlayCameraSize = 8f;

        private readonly List<StageObjectData> objects = new List<StageObjectData>();
        private readonly Stack<EditorSnapshot> undoStack = new Stack<EditorSnapshot>();
        private readonly Stack<EditorSnapshot> redoStack = new Stack<EditorSnapshot>();
        private sealed class EditorSnapshot
        {
            public List<StageObjectData> Objects;
            public StageRuleMode RuleMode;
            public float TimeLimitSeconds;
            public StageObjectType CollectionTarget;
            public int RequiredCollectionCount;
        }
        private StageObjectType addType = StageObjectType.Platform;
        private StageObjectData selectedData;
        private StageObjectData linkSourceData;
        private GameObject selectedObject;
        private GameObject selectionBox;
        private GameObject dragPreviewObject;
        private bool active;
        private bool dragging;
        private bool drawingRect;
        private bool drawingTerrainStroke;
        private bool terrainFreehand = true;
        private bool terrainStraightLine = true;
        private float terrainPathThickness = 2f;
        private bool terrainKeepSeparate = true;
        private Color stageBackgroundColor = new Color(0.985f, 0.975f, 0.93f, 1f);
        private StageRuleMode stageRuleMode;
        private float stageTimeLimitSeconds = 60f;
        private StageObjectType stageCollectionTarget = StageObjectType.CollectibleFish;
        private int stageRequiredCollectionCount = 1;
        private Vector2 terrainStrokeLastPoint;
        private StageObjectData terrainStrokeLastData;
        private StageObjectData terrainStrokeExtendData;
        private int terrainStrokeSegmentCount;
        private int terrainStrokeBasePointCount;
        private bool terrainStrokeForcePath;
        private readonly List<Vector2> terrainStrokePoints = new List<Vector2>();
        private bool updatingDropdown;
        private bool updatingCategoryDropdown;
        private Vector3 editorCameraPositionBeforeTest;
        private float editorCameraSizeBeforeTest;
        private bool hasEditorCameraStateBeforeTest;
        private StageObjectCategory currentCategory = StageObjectCategory.Terrain;
        private readonly List<StageObjectType> filteredPaletteTypes = new List<StageObjectType>();
        private readonly List<StageObjectData> visibleListItems = new List<StageObjectData>();
        private Text listTitleText;
        private Text listPageText;
        private readonly Text[] listItemTexts = new Text[5];
        private bool listShowsLinks;
        private int listPage;
        private Vector2 rectStart;
        private Vector2 dragOffset;
        private CopyDirection copyDirection = CopyDirection.Right;

        public bool IsEditing => active;
        public bool HasMultipleSelection => rangeSelectedObjects.Count > 1;
        public CopyDirection CurrentCopyDirection => copyDirection;
        public string CopyDirectionLabel => LocalizationManager.T(
            copyDirection == CopyDirection.Left
                ? "stage_editor_copy_left"
                : copyDirection == CopyDirection.Up
                    ? "stage_editor_copy_up"
                    : copyDirection == CopyDirection.Down
                        ? "stage_editor_copy_down"
                        : "stage_editor_copy_right");
        public bool SnapEnabled => snapToGrid;
        public StageObjectType CurrentAddType => addType;
        public bool TerrainFreehandEnabled => terrainFreehand;
        public bool TerrainStraightLineEnabled => terrainStraightLine;
        public float TerrainPathThickness => terrainPathThickness;
        public bool TerrainKeepSeparate => terrainKeepSeparate;
        public Color StageBackgroundColor => stageBackgroundColor;
        public string StageRuleModeLabel => LocalizationManager.T(
            stageRuleMode == StageRuleMode.TimedCollection
                ? "stage_rule_timed"
                : stageRuleMode == StageRuleMode.Survival
                    ? "stage_rule_survival"
                    : "stage_rule_normal");
        public string StageCollectionTargetLabel => LocalizationManager.T(StageObjectCatalog.GetObjectKey(stageCollectionTarget));
        public string StageTimeLimitLabel => LocalizationManager.Format("stage_rule_seconds", stageTimeLimitSeconds);
        public string StageRequiredCountLabel => stageRequiredCollectionCount <= 0
            ? LocalizationManager.T("stage_rule_all")
            : LocalizationManager.Format("stage_rule_count", stageRequiredCollectionCount);
        public bool IsTimedCollectionRule => stageRuleMode == StageRuleMode.TimedCollection;
        public bool IsSurvivalRule => stageRuleMode == StageRuleMode.Survival;
        public float StageTimeLimitSeconds => stageTimeLimitSeconds;
        public StageObjectType StageCollectionTarget => stageCollectionTarget;
        public int StagePlacedCollectionTargetCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] != null && objects[i].type == stageCollectionTarget)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
        public int StageEffectiveRequiredCollectionCount => stageRequiredCollectionCount > 0
            ? stageRequiredCollectionCount
            : Mathf.Max(1, StagePlacedCollectionTargetCount);
        public bool SelectedSupportsActionStrength => selectedData != null && SupportsActionStrength(selectedData.type);
        public bool SelectedIsMovingPlatform => selectedData != null && selectedData.type == StageObjectType.MovingPlatform;
        public bool SelectedSupportsBombFuse => selectedData != null &&
            (selectedData.type == StageObjectType.Bomb ||
             selectedData.type == StageObjectType.PickupFuseBomb ||
             selectedData.type == StageObjectType.BombDropper);
        public bool SelectedSupportsSecondarySlider => SelectedIsMovingPlatform || SelectedSupportsBombFuse;
        public float SelectedMovementSpeed => SelectedSupportsBombFuse
            ? Mathf.Clamp(selectedData.bombFuseSeconds > 0f ? selectedData.bombFuseSeconds : 5f, 1f, 15f)
            : selectedData != null && selectedData.movementSpeed > 0f
                ? Mathf.Clamp(selectedData.movementSpeed, 0.5f, 10f)
                : 3.2f;
        public float SelectedSecondarySliderMinimum => SelectedSupportsBombFuse ? 1f : 0.5f;
        public float SelectedSecondarySliderMaximum => SelectedSupportsBombFuse ? 15f : 10f;
        public string SelectedSecondarySliderLabel => LocalizationManager.T(
            SelectedSupportsBombFuse ? "stage_editor_bomb_fuse_seconds" : "stage_editor_move_speed");
        public bool SelectedIsElevator => selectedData != null && selectedData.type == StageObjectType.Elevator;
        public bool SelectedIsCrumblingFloor => selectedData != null && selectedData.type == StageObjectType.FallingFloor;
        public bool SelectedIsBombWall => selectedData != null && selectedData.type == StageObjectType.BreakableWall;
        public bool SelectedIsConveyor => selectedData != null && IsConveyorType(selectedData.type);
        public bool SelectedIsBoxDropper => selectedData != null && selectedData.type == StageObjectType.BoxDropper;
        public bool SelectedIsSpikeDropper => selectedData != null && selectedData.type == StageObjectType.SpikeDropper;
        public bool SelectedIsBombDropper => selectedData != null && selectedData.type == StageObjectType.BombDropper;
        public bool SelectedUsesDropperPattern => SelectedIsBoxDropper || SelectedIsBombDropper;
        public bool SelectedIsDropper => SelectedIsBoxDropper || SelectedIsSpikeDropper || SelectedIsBombDropper;
        public bool SelectedIsBeamEmitter => selectedData != null && selectedData.type == StageObjectType.BeamEmitter;
        public string SelectedActionStrengthLabel => LocalizationManager.T(
            SelectedIsMovingPlatform || SelectedIsElevator
                ? "stage_editor_move_distance"
                : SelectedIsCrumblingFloor
                    ? "stage_editor_crumble_delay"
                    : SelectedIsBombWall
                        ? "stage_editor_bomb_wall_hits"
                    : SelectedIsConveyor
                        ? "stage_editor_conveyor_speed"
                        : SelectedIsDropper
                            ? "stage_editor_drop_interval"
                            : SelectedIsBeamEmitter
                                ? "stage_editor_beam_interval"
                            : "stage_editor_action_strength");
        public float SelectedActionStrengthMinimum => SelectedIsCrumblingFloor
            ? 0.1f
            : SelectedIsBombWall
                ? 1f
            : SelectedIsConveyor || SelectedIsDropper || SelectedIsBeamEmitter
                ? 0.5f
                : SelectedIsMovingPlatform || SelectedIsElevator ? 1f : 5f;
        public float SelectedActionStrengthMaximum => SelectedIsCrumblingFloor
            ? 5f
            : SelectedIsBombWall
                ? 5f
            : SelectedIsConveyor || SelectedIsDropper || SelectedIsBeamEmitter
                ? 10f
                : SelectedIsMovingPlatform ? 100f : SelectedIsElevator ? 30f : 60f;
        public bool SelectedSupportsWeightThreshold => selectedData != null && selectedData.type == StageObjectType.InkScale;
        public float SelectedActionStrength => selectedData != null && selectedData.actionStrength > 0f
            ? selectedData.actionStrength
            : SelectedIsMovingPlatform || SelectedIsElevator
                ? SelectedIsElevator ? 8f : 6f
                : SelectedIsCrumblingFloor
                    ? 0.4f
                    : SelectedIsBombWall
                        ? 1f
                    : SelectedIsConveyor
                        ? 3f
                        : SelectedIsDropper || SelectedIsBeamEmitter ? 2f : 27f;
        public string SelectedConveyorDirectionLabel => LocalizationManager.T(
            selectedData != null && Mathf.Cos(selectedData.movementAngle * Mathf.Deg2Rad) < 0f
                ? "stage_editor_conveyor_left"
                : "stage_editor_conveyor_right");
        public string SelectedDropperPatternLabel => LocalizationManager.T(
            SelectedIsBombDropper
                ? selectedData != null && selectedData.spawnPattern == 1
                    ? "stage_editor_bomb_pattern_spawn"
                    : selectedData != null && selectedData.spawnPattern == 2
                        ? "stage_editor_bomb_pattern_pickup"
                        : "stage_editor_bomb_pattern_both"
                : selectedData != null && selectedData.spawnPattern == 1
                ? "stage_editor_box_pattern_square"
                : selectedData != null && selectedData.spawnPattern == 2
                    ? "stage_editor_box_pattern_round"
                    : selectedData != null && selectedData.spawnPattern == 3
                        ? "stage_editor_box_pattern_triangle"
                        : "stage_editor_box_pattern_all");
        public float SelectedDropperBoxSize => selectedData != null && selectedData.spawnBoxSize > 0f
            ? selectedData.spawnBoxSize
            : 0.9f;
        public string SelectedDropperSizeLabel => LocalizationManager.T(
            SelectedIsSpikeDropper
                ? "stage_editor_spike_size"
                : SelectedIsBombDropper ? "stage_editor_bomb_size" : "stage_editor_box_size");
        public float SelectedWeightThreshold => selectedData != null && selectedData.actionStrength > 0f
            ? selectedData.actionStrength
            : 300f;

        private static bool SupportsActionStrength(StageObjectType type)
        {
            return type == StageObjectType.JumpPad
                || type == StageObjectType.Spring
                || type == StageObjectType.MovingPlatform
                || type == StageObjectType.Elevator
                || type == StageObjectType.FallingFloor
                || type == StageObjectType.BreakableWall
                || IsConveyorType(type)
                || type == StageObjectType.BoxDropper
                || type == StageObjectType.SpikeDropper
                || type == StageObjectType.BombDropper
                || type == StageObjectType.BeamEmitter;
        }

        private static bool IsConveyorType(StageObjectType type)
        {
            return type == StageObjectType.Belt
                || type == StageObjectType.ConveyorLeft
                || type == StageObjectType.ConveyorRight;
        }

        public void SetStageBackgroundColor(Color color)
        {
            stageBackgroundColor = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
            StageBackgroundAppearance.Apply(stageBackgroundColor);
            SetStatus(LocalizationManager.T("stage_editor_status_background_color"));
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
        }

        private void Awake()
        {
            EnsureReferences();
            if (editorRoot == null)
            {
                GameObject root = GameObject.Find("RuntimeStageEditorRoot");
                if (root == null)
                {
                    root = new GameObject("RuntimeStageEditorRoot");
                }

                editorRoot = root.transform;
            }

            SetPanel(false);
            SetupCategoryDropdown();
            SetupObjectTypeDropdown();
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            HandleCameraInput();

            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            HandleSelectedObjectNudge();

            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                DeleteSelected();
            }

            if (Input.GetKeyDown(KeyCode.U) || (control && !shift && Input.GetKeyDown(KeyCode.Z)))
            {
                Undo();
            }

            if ((control && Input.GetKeyDown(KeyCode.Y)) || (control && shift && Input.GetKeyDown(KeyCode.Z)))
            {
                Redo();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                stageManager?.CloseStageEditor();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5) || (control && Input.GetKeyDown(KeyCode.S)))
            {
                Save();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                stageManager?.TestEditedStage();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                ResizeSelected(new Vector2(-0.5f, 0f));
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                ResizeSelected(new Vector2(0.5f, 0f));
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                ResizeSelected(new Vector2(0f, -0.5f));
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                ResizeSelected(new Vector2(0f, 0.5f));
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                ToggleSnap();
                SetStatus(LocalizationManager.Format("stage_editor_status_snap", snapToGrid ? "ON" : "OFF", gridSize));
            }

            HandleMouse();
            UpdateSelectionBox();
        }

        public void Open(string id)
        {
            stageId = string.IsNullOrEmpty(id) ? "1-1" : id;
            displayName = $"Stage {stageId}";
            terrainKeepSeparate = true;
            terrainStraightLine = true;
            terrainPathThickness = 2f;
            active = true;
            dragging = false;
            ClearRangeSelection();
            undoStack.Clear();
            redoStack.Clear();
            stageLoader?.HideStages();
            LoadWorkingData();
            BuildEditorObjects();
            SetPanel(true);
            RefreshObjectTypeDropdown();
            EnsureListReferences();
            RefreshText();
            RefreshListPanel();
            SetStatus(LocalizationManager.T("stage_editor_status_open"));
        }

        public void Close()
        {
            active = false;
            dragging = false;
            drawingRect = false;
            drawingTerrainStroke = false;
            ClearRangeSelection();
            ClearEditorObjects();
            ClearDragPreview();
            SetPanel(false);
        }

        private void EnsureReferences()
        {
            if (stageLoader == null)
            {
                stageLoader = FindObjectOfType<StageLoader>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (objectFactory == null)
            {
                objectFactory = FindObjectOfType<StageObjectFactory>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }
    }
}
