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
    public sealed class RuntimeStageEditor : MonoBehaviour
    {
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

        private readonly List<StageObjectData> objects = new List<StageObjectData>();
        private readonly Stack<List<StageObjectData>> undoStack = new Stack<List<StageObjectData>>();
        private readonly Stack<List<StageObjectData>> redoStack = new Stack<List<StageObjectData>>();
        private StageObjectType addType = StageObjectType.Platform;
        private StageObjectData selectedData;
        private StageObjectData linkSourceData;
        private GameObject selectedObject;
        private GameObject selectionBox;
        private GameObject dragPreviewObject;
        private bool active;
        private bool dragging;
        private bool drawingRect;
        private bool updatingDropdown;
        private bool updatingCategoryDropdown;
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

        public bool IsEditing => active;

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

            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                DeleteSelected();
            }

            if (Input.GetKeyDown(KeyCode.U) || (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z)))
            {
                Undo();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                stageManager?.CloseStageEditor();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
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
                SetStatus($"Snap: {(snapToGrid ? "ON" : "OFF")}. When ON, objects stick to {gridSize:0.##} unit steps.");
            }

            HandleMouse();
            UpdateSelectionBox();
        }

        public void Open(string id)
        {
            stageId = string.IsNullOrEmpty(id) ? "1-1" : id;
            displayName = $"Stage {stageId}";
            active = true;
            dragging = false;
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
            SetStatus("Drag to place blocks. Click to select. Wheel resizes selected / zooms when nothing is selected. G toggles snap.");
        }

        public void Close()
        {
            active = false;
            dragging = false;
            drawingRect = false;
            ClearEditorObjects();
            ClearDragPreview();
            SetPanel(false);
        }

        public void SetAddType(StageObjectType type)
        {
            addType = type;
            currentCategory = StageObjectCatalog.Get(type).Category;
            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            SetStatus(IsBlockType(type)
                ? $"Add mode: {GetObjectLabel(type)}. Drag on the map to create it."
                : $"Add mode: {GetObjectLabel(type)}. Click the map to place it.");
            RefreshObjectTypeDropdown();
            RefreshText();
            RefreshListPanel();
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
        }

        public void ResizeSelected(Vector2 delta)
        {
            ResizeSelected(delta, true);
        }

        private void ResizeSelected(Vector2 delta, bool recordUndo)
        {
            if (selectedData == null)
            {
                SetStatus("Select an object first.");
                return;
            }

            if (recordUndo)
            {
                PushUndo();
            }

            selectedData.size = new Vector2(
                Mathf.Max(0.2f, selectedData.size.x + delta.x),
                Mathf.Max(0.2f, selectedData.size.y + delta.y));
            selectedData.position = SnapJoinPosition(selectedData.position);
            RebuildSelectedObject();
            RefreshText();
        }

        public void ScaleSelected(float amount, bool horizontal, bool vertical)
        {
            Vector2 delta = Vector2.zero;
            float step = Mathf.Max(0.1f, gridSize);
            if (horizontal)
            {
                delta.x = amount * step;
            }

            if (vertical)
            {
                delta.y = amount * step;
            }

            ResizeSelected(delta, true);
        }

        public void DeleteSelected()
        {
            if (selectedData == null)
            {
                return;
            }

            PushUndo();
            string objectId = selectedData.objectId;
            objects.Remove(selectedData);
            RemoveEditorObjectById(objectId);

            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            RefreshText();
            RefreshListPanel();
        }

        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                SetStatus("Nothing to undo.");
                return;
            }

            redoStack.Push(CreateSnapshot());
            RestoreSnapshot(undoStack.Pop());
            SetStatus("Undid the last edit.");
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                SetStatus("Nothing to redo.");
                return;
            }

            undoStack.Push(CreateSnapshot());
            RestoreSnapshot(redoStack.Pop());
            SetStatus("Redid the edit.");
        }

        public void MarkSelectedAsLinkSource()
        {
            if (selectedData == null)
            {
                SetStatus("Select a button or switch first.");
                return;
            }

            if (!CanBeLinkSource(selectedData.type))
            {
                SetStatus("Only buttons and switches can be link sources for now.");
                return;
            }

            linkSourceData = selectedData;
            SetStatus($"Link source set: {GetObjectLabel(selectedData.type)}. Select a target, then press Link Target.");
            RefreshText();
            RefreshListPanel();
        }

        public void LinkSelectedAsTarget()
        {
            if (linkSourceData == null)
            {
                SetStatus("Set a link source first.");
                return;
            }

            if (selectedData == null)
            {
                SetStatus("Select a target object.");
                return;
            }

            if (selectedData == linkSourceData)
            {
                SetStatus("Source and target must be different objects.");
                return;
            }

            PushUndo();
            linkSourceData.linkTargetId = selectedData.objectId;
            linkSourceData.linkAction = GetDefaultLinkAction(selectedData.type);
            SetStatus($"Linked {GetObjectLabel(linkSourceData.type)} -> {GetObjectLabel(selectedData.type)}.");
            RefreshText();
            RefreshListPanel();
        }

        public void ClearSelectedLink()
        {
            if (selectedData == null)
            {
                SetStatus("Select a linked button or switch first.");
                return;
            }

            PushUndo();
            selectedData.linkTargetId = string.Empty;
            selectedData.linkAction = string.Empty;
            if (linkSourceData == selectedData)
            {
                linkSourceData = null;
            }

            SetStatus("Cleared link.");
            RefreshText();
            RefreshListPanel();
        }

        public void SetListModeObjects()
        {
            listShowsLinks = false;
            listPage = 0;
            RefreshListPanel();
        }

        public void SetListModeLinks()
        {
            listShowsLinks = true;
            listPage = 0;
            RefreshListPanel();
        }

        public void ChangeListPage(int delta)
        {
            BuildVisibleListItems();
            int maxPage = Mathf.Max(0, (visibleListItems.Count - 1) / listItemTexts.Length);
            listPage = Mathf.Clamp(listPage + delta, 0, maxPage);
            RefreshListPanel();
        }

        public void SelectListItem(int localIndex)
        {
            BuildVisibleListItems();
            int index = listPage * listItemTexts.Length + localIndex;
            if (index < 0 || index >= visibleListItems.Count)
            {
                return;
            }

            SelectData(visibleListItems[index]);
            UpdateSelectionBox();
            RefreshText();
            RefreshListPanel();
            SetStatus("Selected from list.");
        }

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
            SetStatus($"Saved {assetPath}");
#else
            SetStatus("Saving is available in the Unity Editor only.");
#endif
        }

        public void TestPlay()
        {
            Save();
            stageLoader?.LoadStage(CreateStageData());
            Close();
        }

        private void LoadWorkingData()
        {
            objects.Clear();
            selectedData = null;
            selectedObject = null;

            TextAsset asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset != null)
            {
                StageData loaded = JsonUtility.FromJson<StageData>(asset.text);
                if (loaded != null && loaded.objects != null)
                {
                    displayName = string.IsNullOrEmpty(loaded.displayName) ? displayName : loaded.displayName;
                    for (int i = 0; i < loaded.objects.Length; i++)
                    {
                        if (loaded.objects[i] != null)
                        {
                            objects.Add(CloneData(loaded.objects[i]));
                        }
                    }
                }
            }

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
        }

        private void BuildEditorObjects()
        {
            ClearEditorObjects();
            EnsureReferences();
            for (int i = 0; i < objects.Count; i++)
            {
                CreateEditorObject(objects[i]);
            }
        }

        private void CreateEditorObject(StageObjectData data)
        {
            EnsureReferences();
            if (objectFactory == null)
            {
                SetStatus("StageObjectFactory is missing.");
                return;
            }

            GameObject obj = objectFactory.Create(data, editorRoot);
            StageEditorObject marker = obj != null ? obj.GetComponent<StageEditorObject>() : null;
            if (marker != null)
            {
                marker.objectId = data.objectId;
                marker.type = data.type;
                marker.size = data.size;
                marker.linkTargetId = data.linkTargetId;
                marker.linkAction = data.linkAction;
            }
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

        private void HandleMouse()
        {
            if (IsPointerOverEditorUi())
            {
                dragging = false;
                return;
            }

            Vector2 world = ScreenToWorld(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                GameObject hit = FindObjectAt(world);
                if (hit != null)
                {
                    SelectObject(hit);
                    dragOffset = (Vector2)hit.transform.position - world;
                    dragging = true;
                    PushUndo();
                }
                else
                {
                    if (IsBlockType(addType))
                    {
                        BeginRect(world);
                    }
                    else
                    {
                        AddObject(world);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (drawingRect)
                {
                    CommitRect(world);
                }

                dragging = false;
            }

            if (drawingRect && Input.GetMouseButton(0))
            {
                UpdateDragPreview(world);
            }

            if (dragging && selectedData != null && selectedObject != null && Input.GetMouseButton(0))
            {
                Vector2 next = world + dragOffset;
                if (snapToGrid)
                {
                    next = Snap(next);
                }

                next = SnapJoinPosition(next);
                selectedData.position = next;
                selectedObject.transform.position = next;
                RefreshText();
            }

            if (selectedData != null && !Input.GetMouseButton(0))
            {
                HandleSelectedObjectWheel();
            }
        }

        private void HandleCameraInput()
        {
            if (worldCamera == null)
            {
                return;
            }

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                move.x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                move.x += 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                move.y += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                move.y -= 1f;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                worldCamera.transform.position += move.normalized * cameraMoveSpeed * Time.unscaledDeltaTime;
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && selectedData == null)
            {
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - wheel * 0.65f, minCameraSize, maxCameraSize);
            }
        }

        private void HandleSelectedObjectWheel()
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f || IsPointerOverEditorUi())
            {
                return;
            }

            bool horizontalOnly = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool verticalOnly = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool horizontal = !verticalOnly;
            bool vertical = !horizontalOnly;
            ScaleSelected(Mathf.Sign(wheel), horizontal, vertical);

            if (horizontalOnly)
            {
                SetStatus("Resized width. Wheel = both, Shift+Wheel = width, Alt+Wheel = height.");
            }
            else if (verticalOnly)
            {
                SetStatus("Resized height. Wheel = both, Shift+Wheel = width, Alt+Wheel = height.");
            }
            else
            {
                SetStatus("Resized object. Wheel = both, Shift+Wheel = width, Alt+Wheel = height.");
            }
        }

        private void BeginRect(Vector2 world)
        {
            rectStart = snapToGrid ? Snap(world) : world;
            drawingRect = true;
            ClearDragPreview();
            dragPreviewObject = new GameObject("DragBlockPreview");
            dragPreviewObject.transform.SetParent(editorRoot, false);
            LineRenderer line = dragPreviewObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.loop = false;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.1f, 0.32f, 1f, 0.95f);
            line.endColor = new Color(0.1f, 0.32f, 1f, 0.95f);
            line.sortingOrder = 90;
            UpdateDragPreview(world);
        }

        private void UpdateDragPreview(Vector2 world)
        {
            if (dragPreviewObject == null)
            {
                return;
            }

            Vector2 end = snapToGrid ? Snap(world) : world;
            Rect rect = MakeRect(rectStart, end);
            dragPreviewObject.transform.position = rect.center;
            LineRenderer line = dragPreviewObject.GetComponent<LineRenderer>();
            float x = Mathf.Max(rect.width * 0.5f, 0.1f);
            float y = Mathf.Max(rect.height * 0.5f, 0.1f);
            line.SetPositions(new[]
            {
                new Vector3(-x, -y, 0f),
                new Vector3(-x, y, 0f),
                new Vector3(x, y, 0f),
                new Vector3(x, -y, 0f),
                new Vector3(-x, -y, 0f)
            });
        }

        private void CommitRect(Vector2 world)
        {
            Vector2 end = snapToGrid ? Snap(world) : world;
            Rect rect = MakeRect(rectStart, end);
            drawingRect = false;
            ClearDragPreview();

            if (rect.width < 0.2f || rect.height < 0.2f)
            {
                SetStatus("Drag a larger area to create a block.");
                return;
            }

            AddBlock(rect.center, rect.size);
        }

        private void AddBlock(Vector2 center, Vector2 size)
        {
            PushUndo();
            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, center);
            data.size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
            data.position = SnapJoinPosition(data, data.position);
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            SetStatus($"Placed {GetObjectLabel(addType)} {data.size.x:0.0} x {data.size.y:0.0}");
            RefreshText();
            RefreshListPanel();
        }

        private void ClearDragPreview()
        {
            if (dragPreviewObject != null)
            {
                Destroy(dragPreviewObject);
                dragPreviewObject = null;
            }
        }

        private void AddObject(Vector2 position)
        {
            if (snapToGrid)
            {
                position = Snap(position);
            }

            PushUndo();
            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, position);
            data.position = SnapJoinPosition(data, data.position);
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            SetStatus($"Placed {GetObjectLabel(addType)} at {position.x:0.0}, {position.y:0.0}");
            RefreshText();
            RefreshListPanel();
        }

        private static bool IsBlockType(StageObjectType type)
        {
            return StageObjectCatalog.IsRectPlacement(type);
        }

        private static Rect MakeRect(Vector2 a, Vector2 b)
        {
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private GameObject FindObjectAt(Vector2 position)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(position);
            GameObject best = null;
            for (int i = 0; i < hits.Length; i++)
            {
                StageEditorObject marker = hits[i].GetComponentInParent<StageEditorObject>();
                if (marker != null && marker.transform.IsChildOf(editorRoot))
                {
                    best = marker.gameObject;
                }
            }

            if (best != null)
            {
                return best;
            }

            float bestDistance = 0.4f;
            for (int i = 0; i < editorRoot.childCount; i++)
            {
                Transform child = editorRoot.GetChild(i);
                StageEditorObject marker = child.GetComponent<StageEditorObject>();
                if (marker == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, child.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = child.gameObject;
                }
            }

            return best;
        }

        private void SelectObject(GameObject obj)
        {
            StageEditorObject marker = obj.GetComponent<StageEditorObject>();
            if (marker == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].objectId == marker.objectId)
                {
                    selectedData = objects[i];
                    selectedObject = obj;
                    SetStatus("Selected. Wheel: scale both. Shift+Wheel: width. Alt+Wheel: height. Drag to move.");
                    RefreshText();
                    return;
                }
            }
        }

        private void SelectData(StageObjectData data)
        {
            selectedData = data;
            selectedObject = null;
            for (int i = 0; i < editorRoot.childCount; i++)
            {
                StageEditorObject marker = editorRoot.GetChild(i).GetComponent<StageEditorObject>();
                if (marker != null && marker.objectId == data.objectId)
                {
                    selectedObject = marker.gameObject;
                    break;
                }
            }
        }

        private void RebuildSelectedObject()
        {
            if (selectedData == null)
            {
                return;
            }

            RemoveEditorObjectById(selectedData.objectId);
            CreateEditorObject(selectedData);
            SelectData(selectedData);
        }

        private void RemoveEditorObjectById(string objectId)
        {
            if (editorRoot == null || string.IsNullOrEmpty(objectId))
            {
                return;
            }

            for (int i = editorRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = editorRoot.GetChild(i);
                StageEditorObject marker = child.GetComponent<StageEditorObject>();
                if (marker == null || marker.objectId != objectId)
                {
                    continue;
                }

                DestroyEditorObject(child.gameObject);
            }

            selectedObject = null;
        }

        private static void DestroyEditorObject(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            obj.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private StageData CreateStageData()
        {
            return new StageData
            {
                id = stageId,
                displayName = displayName,
                objects = objects.ToArray()
            };
        }

        private StageObjectData CloneData(StageObjectData data)
        {
            return new StageObjectData
            {
                objectId = string.IsNullOrEmpty(data.objectId) ? $"{data.type}_{System.Guid.NewGuid():N}".Substring(0, 14) : data.objectId,
                type = data.type,
                position = data.position,
                size = data.size,
                rotation = data.rotation,
                linkTargetId = data.linkTargetId,
                linkAction = data.linkAction
            };
        }

        private Vector2 ScreenToWorld(Vector3 screen)
        {
            EnsureReferences();
            Vector3 world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2(world.x, world.y);
        }

        private Vector2 Snap(Vector2 value)
        {
            float size = Mathf.Max(0.05f, gridSize);
            return new Vector2(Mathf.Round(value.x / size) * size, Mathf.Round(value.y / size) * size);
        }

        private Vector2 SnapJoinPosition(Vector2 position)
        {
            return SnapJoinPosition(selectedData, position);
        }

        private Vector2 SnapJoinPosition(StageObjectData movingData, Vector2 position)
        {
            if (movingData == null || !IsBlockType(movingData.type))
            {
                return position;
            }

            Rect moving = RectFromData(movingData, position);
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData other = objects[i];
                if (other == movingData || other == null || !IsBlockType(other.type))
                {
                    continue;
                }

                Rect target = RectFromData(other, other.position);
                const float joinDistance = 0.18f;
                if (RangesOverlap(moving.yMin, moving.yMax, target.yMin, target.yMax))
                {
                    if (Mathf.Abs(moving.xMax - target.xMin) < joinDistance)
                    {
                        position.x += target.xMin - moving.xMax;
                        moving = RectFromData(movingData, position);
                    }
                    else if (Mathf.Abs(moving.xMin - target.xMax) < joinDistance)
                    {
                        position.x += target.xMax - moving.xMin;
                        moving = RectFromData(movingData, position);
                    }
                }

                if (RangesOverlap(moving.xMin, moving.xMax, target.xMin, target.xMax))
                {
                    if (Mathf.Abs(moving.yMax - target.yMin) < joinDistance)
                    {
                        position.y += target.yMin - moving.yMax;
                        moving = RectFromData(movingData, position);
                    }
                    else if (Mathf.Abs(moving.yMin - target.yMax) < joinDistance)
                    {
                        position.y += target.yMax - moving.yMin;
                        moving = RectFromData(movingData, position);
                    }
                }
            }

            return position;
        }

        private static Rect RectFromData(StageObjectData data, Vector2 position)
        {
            Vector2 half = data.size * 0.5f;
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static bool RangesOverlap(float aMin, float aMax, float bMin, float bMax)
        {
            return aMax >= bMin && bMax >= aMin;
        }

        private void PushUndo()
        {
            undoStack.Push(CreateSnapshot());
            redoStack.Clear();
        }

        private List<StageObjectData> CreateSnapshot()
        {
            List<StageObjectData> snapshot = new List<StageObjectData>(objects.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                snapshot.Add(CloneData(objects[i]));
            }

            return snapshot;
        }

        private void RestoreSnapshot(List<StageObjectData> snapshot)
        {
            objects.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                objects.Add(CloneData(snapshot[i]));
            }

            selectedData = null;
            selectedObject = null;
            linkSourceData = null;
            BuildEditorObjects();
            RefreshText();
            RefreshListPanel();
        }

        private bool IsPointerOverEditorUi()
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                || (uiBlocker != null && RectTransformUtility.RectangleContainsScreenPoint(uiBlocker, Input.mousePosition));
        }

        private void UpdateSelectionBox()
        {
            if (selectedData == null || selectedObject == null)
            {
                SetSelectionBox(false);
                return;
            }

            if (selectionBox == null)
            {
                selectionBox = new GameObject("RuntimeStageSelectionBox");
                LineRenderer line = selectionBox.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 5;
                line.loop = false;
                line.startWidth = 0.035f;
                line.endWidth = 0.035f;
                line.numCapVertices = 3;
                line.numCornerVertices = 3;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = new Color(0.1f, 0.32f, 1f);
                line.endColor = new Color(0.1f, 0.32f, 1f);
                line.sortingOrder = 80;
            }

            selectionBox.SetActive(true);
            selectionBox.transform.SetParent(selectedObject.transform, false);
            selectionBox.transform.localPosition = Vector3.zero;
            selectionBox.transform.localRotation = Quaternion.identity;
            selectionBox.transform.localScale = Vector3.one;

            LineRenderer selection = selectionBox.GetComponent<LineRenderer>();
            Vector2 size = selectedData.size;
            if (selectedData.type == StageObjectType.Spawn)
            {
                size = new Vector2(0.8f, 0.8f);
            }

            float localX = 0.5f + 0.08f / Mathf.Max(size.x, 0.1f);
            float localY = 0.5f + 0.08f / Mathf.Max(size.y, 0.1f);
            selection.SetPositions(new[]
            {
                new Vector3(-localX, -localY, -0.04f),
                new Vector3(-localX, localY, -0.04f),
                new Vector3(localX, localY, -0.04f),
                new Vector3(localX, -localY, -0.04f),
                new Vector3(-localX, -localY, -0.04f)
            });
        }

        private void SetSelectionBox(bool visible)
        {
            if (selectionBox != null)
            {
                selectionBox.SetActive(visible);
            }
        }

        private void RefreshText()
        {
            if (stageText != null)
            {
                stageText.text = $"Stage {stageId}";
            }

            if (selectedText != null)
            {
                if (selectedData == null)
                {
                    selectedText.text = $"追加: {GetObjectLabel(addType)} / 吸着: {(snapToGrid ? "ON" : "OFF")}";
                }
                else
                {
                    selectedText.text = $"{GetObjectLabel(selectedData.type)}  位置 {selectedData.position.x:0.0},{selectedData.position.y:0.0}  サイズ {selectedData.size.x:0.0},{selectedData.size.y:0.0}";
                }
            }
        }

        private void EnsureListReferences()
        {
            if (editorPanel == null || listTitleText != null)
            {
                return;
            }

            listTitleText = FindText("RuntimeStageEditorListTitle");
            listPageText = FindText("RuntimeStageEditorListPage");
            for (int i = 0; i < listItemTexts.Length; i++)
            {
                listItemTexts[i] = FindText($"RuntimeStageEditorListItem{i}Label");
            }
        }

        private Text FindText(string objectName)
        {
            Transform target = FindChildRecursive(editorPanel.transform, objectName);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == objectName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void RefreshListPanel()
        {
            EnsureListReferences();
            BuildVisibleListItems();

            if (listTitleText != null)
            {
                listTitleText.text = listShowsLinks ? "リンク一覧" : "オブジェクト一覧";
            }

            int pageSize = listItemTexts.Length;
            int maxPage = Mathf.Max(0, (visibleListItems.Count - 1) / pageSize);
            listPage = Mathf.Clamp(listPage, 0, maxPage);
            if (listPageText != null)
            {
                listPageText.text = $"{listPage + 1} / {maxPage + 1}";
            }

            for (int i = 0; i < listItemTexts.Length; i++)
            {
                Text itemText = listItemTexts[i];
                if (itemText == null)
                {
                    continue;
                }

                int index = listPage * pageSize + i;
                if (index >= visibleListItems.Count)
                {
                    itemText.text = "";
                    continue;
                }

                StageObjectData data = visibleListItems[index];
                string selectedMark = data == selectedData ? ">" : " ";
                if (listShowsLinks)
                {
                    string targetLabel = GetObjectLabelById(data.linkTargetId);
                    itemText.text = $"{selectedMark} {GetObjectLabel(data.type)} -> {targetLabel}";
                }
                else
                {
                    itemText.text = $"{selectedMark} {index + 1}. {GetObjectLabel(data.type)}  {data.position.x:0.0},{data.position.y:0.0}";
                }
            }
        }

        private void BuildVisibleListItems()
        {
            visibleListItems.Clear();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data == null)
                {
                    continue;
                }

                if (listShowsLinks && string.IsNullOrEmpty(data.linkTargetId))
                {
                    continue;
                }

                visibleListItems.Add(data);
            }
        }

        private string GetObjectLabelById(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return "(none)";
            }

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].objectId == objectId)
                {
                    return GetObjectLabel(objects[i].type);
                }
            }

            return objectId;
        }

        private static bool CanBeLinkSource(StageObjectType type)
        {
            return type == StageObjectType.Button
                || type == StageObjectType.WeightButton
                || type == StageObjectType.PressurePlate
                || type == StageObjectType.Lever
                || type == StageObjectType.ToggleSwitch
                || type == StageObjectType.TimerSwitch
                || type == StageObjectType.RedSwitch
                || type == StageObjectType.BlueSwitch
                || type == StageObjectType.GreenSwitch
                || type == StageObjectType.YellowSwitch;
        }

        private static string GetDefaultLinkAction(StageObjectType targetType)
        {
            if (targetType == StageObjectType.Platform
                || targetType == StageObjectType.HalfPlatform
                || targetType == StageObjectType.MovingPlatform
                || targetType == StageObjectType.Wall
                || targetType == StageObjectType.Door
                || targetType == StageObjectType.Shutter)
            {
                return "RevealGrow";
            }

            return "Reveal";
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
                if (string.IsNullOrEmpty(search) && entry.Category != currentCategory)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search)
                    && entry.Label.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0
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
                options.Add(new Dropdown.OptionData("No match"));
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
