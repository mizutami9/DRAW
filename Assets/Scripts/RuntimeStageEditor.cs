using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
        [SerializeField] private Dropdown objectTypeDropdown;
        [SerializeField] private string stageId = "1-1";
        [SerializeField] private string displayName = "New Stage";
        [SerializeField] private float gridSize = 0.5f;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float cameraMoveSpeed = 9f;
        [SerializeField] private float minCameraSize = 2.5f;
        [SerializeField] private float maxCameraSize = 14f;

        private readonly List<StageObjectData> objects = new List<StageObjectData>();
        private StageObjectType addType = StageObjectType.Platform;
        private StageObjectData selectedData;
        private GameObject selectedObject;
        private GameObject selectionBox;
        private GameObject dragPreviewObject;
        private bool active;
        private bool dragging;
        private bool drawingRect;
        private bool updatingDropdown;
        private Vector2 rectStart;
        private Vector2 dragOffset;

        private static readonly StageObjectType[] PaletteTypes =
        {
            StageObjectType.Platform,
            StageObjectType.Wall,
            StageObjectType.Spawn,
            StageObjectType.Goal,
            StageObjectType.BalanceScale,
            StageObjectType.Weight
        };

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
                SetStatus($"Grid: {(snapToGrid ? "ON" : "OFF")}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetAddType(StageObjectType.Platform);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetAddType(StageObjectType.Wall);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetAddType(StageObjectType.Spawn);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                SetAddType(StageObjectType.Goal);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                SetAddType(StageObjectType.BalanceScale);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                SetAddType(StageObjectType.Weight);
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
            LoadWorkingData();
            BuildEditorObjects();
            SetPanel(true);
            RefreshObjectTypeDropdown();
            RefreshText();
            SetStatus("Editor opened. 1/2 drag a block, 3/4 click a marker. A/D scroll.");
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
            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            SetStatus(IsBlockType(type)
                ? $"Add mode: {GetObjectLabel(type)}. Drag on the map to create it."
                : $"Add mode: {GetObjectLabel(type)}. Click the map to place it.");
            RefreshObjectTypeDropdown();
            RefreshText();
        }

        public void SetAddTypeFromDropdown(int index)
        {
            if (updatingDropdown || index < 0 || index >= PaletteTypes.Length)
            {
                return;
            }

            SetAddType(PaletteTypes[index]);
        }

        public void ToggleSnap()
        {
            snapToGrid = !snapToGrid;
            RefreshText();
        }

        public void ResizeSelected(Vector2 delta)
        {
            if (selectedData == null)
            {
                SetStatus("Select an object first.");
                return;
            }

            selectedData.size = new Vector2(
                Mathf.Max(0.2f, selectedData.size.x + delta.x),
                Mathf.Max(0.2f, selectedData.size.y + delta.y));
            RebuildSelectedObject();
            RefreshText();
        }

        public void DeleteSelected()
        {
            if (selectedData == null)
            {
                return;
            }

            objects.Remove(selectedData);
            if (selectedObject != null)
            {
                Destroy(selectedObject);
            }

            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            RefreshText();
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
                Destroy(editorRoot.GetChild(i).gameObject);
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

                selectedData.position = next;
                selectedObject.transform.position = next;
                RefreshText();
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
            if (Mathf.Abs(wheel) > 0.01f)
            {
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - wheel * 0.65f, minCameraSize, maxCameraSize);
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
            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, center);
            data.size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            SetStatus($"Placed {GetObjectLabel(addType)} {data.size.x:0.0} x {data.size.y:0.0}");
            RefreshText();
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

            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, position);
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            SetStatus($"Placed {GetObjectLabel(addType)} at {position.x:0.0}, {position.y:0.0}");
            RefreshText();
        }

        private static bool IsBlockType(StageObjectType type)
        {
            return type == StageObjectType.Platform || type == StageObjectType.Wall;
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

            if (selectedObject != null)
            {
                Destroy(selectedObject);
            }

            CreateEditorObject(selectedData);
            SelectData(selectedData);
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
                rotation = data.rotation
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

        private bool IsPointerOverEditorUi()
        {
            return uiBlocker != null && RectTransformUtility.RectangleContainsScreenPoint(uiBlocker, Input.mousePosition);
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
                    selectedText.text = $"Add: {GetObjectLabel(addType)} / Snap: {(snapToGrid ? "ON" : "OFF")}";
                }
                else
                {
                    selectedText.text = $"{GetObjectLabel(selectedData.type)}  Pos {selectedData.position.x:0.0},{selectedData.position.y:0.0}  Size {selectedData.size.x:0.0},{selectedData.size.y:0.0}";
                }
            }
        }

        private void SetupObjectTypeDropdown()
        {
            if (objectTypeDropdown == null)
            {
                return;
            }

            objectTypeDropdown.onValueChanged.RemoveListener(SetAddTypeFromDropdown);
            objectTypeDropdown.ClearOptions();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            for (int i = 0; i < PaletteTypes.Length; i++)
            {
                options.Add(new Dropdown.OptionData(GetObjectLabel(PaletteTypes[i])));
            }

            objectTypeDropdown.AddOptions(options);
            objectTypeDropdown.onValueChanged.AddListener(SetAddTypeFromDropdown);
            RefreshObjectTypeDropdown();
        }

        private void RefreshObjectTypeDropdown()
        {
            if (objectTypeDropdown == null)
            {
                return;
            }

            int index = 0;
            for (int i = 0; i < PaletteTypes.Length; i++)
            {
                if (PaletteTypes[i] == addType)
                {
                    index = i;
                    break;
                }
            }

            updatingDropdown = true;
            objectTypeDropdown.value = index;
            objectTypeDropdown.RefreshShownValue();
            updatingDropdown = false;
        }

        private static string GetObjectLabel(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Wall:
                    return LocalizationManager.T("stage_object_wall");
                case StageObjectType.Spawn:
                    return LocalizationManager.T("stage_object_spawn");
                case StageObjectType.Goal:
                    return LocalizationManager.T("stage_object_goal");
                case StageObjectType.BalanceScale:
                    return LocalizationManager.T("stage_object_balance");
                case StageObjectType.Weight:
                    return LocalizationManager.T("stage_object_weight");
                default:
                    return LocalizationManager.T("stage_object_platform");
            }
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
