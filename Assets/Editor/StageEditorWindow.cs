using System.Collections.Generic;
using System.IO;
using DrawBody.Prototype;
using UnityEditor;
using UnityEngine;

namespace DrawBody.EditorTools
{
    public sealed class StageEditorWindow : EditorWindow
    {
        private const string StageFolder = "Assets/Resources/Stages";

        private string stageId = "1-1";
        private string displayName = "New Stage";
        private StageObjectType selectedType = StageObjectType.Platform;
        private Vector2 defaultSize = new Vector2(3f, 0.4f);
        private Vector2 addPosition;
        private bool sceneEditMode = true;
        private bool placeOnClick;
        private bool snapToGrid = true;
        private float gridSize = 0.5f;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        [MenuItem("PICO/Stage Editor")]
        public static void Open()
        {
            GetWindow<StageEditorWindow>("Stage Editor");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Draw Body Stage Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Editor fallback: place stage objects, move them in the Scene view, then save as JSON. The in-game editor is the main workflow.", MessageType.Info);

            stageId = EditorGUILayout.TextField("Stage ID", stageId);
            displayName = EditorGUILayout.TextField("Display Name", displayName);

            EditorGUILayout.Space(8f);
            sceneEditMode = EditorGUILayout.Toggle("Scene Edit Mode", sceneEditMode);
            placeOnClick = EditorGUILayout.Toggle("Click To Place", placeOnClick);
            snapToGrid = EditorGUILayout.Toggle("Snap To Grid", snapToGrid);
            gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Grid Size", gridSize));

            EditorGUILayout.Space(8f);
            EditorGUI.BeginChangeCheck();
            selectedType = (StageObjectType)EditorGUILayout.EnumPopup("Add Type", selectedType);
            if (EditorGUI.EndChangeCheck())
            {
                defaultSize = GetDefaultSize(selectedType);
            }

            defaultSize = EditorGUILayout.Vector2Field("Size", defaultSize);
            addPosition = EditorGUILayout.Vector2Field("Position", addPosition);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Object", GUILayout.Height(32f)))
                {
                    AddObject();
                }

                if (GUILayout.Button("Use Scene Center", GUILayout.Height(32f)))
                {
                    addPosition = GetSceneCenter();
                }
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load JSON", GUILayout.Height(32f)))
                {
                    LoadJson();
                }

                if (GUILayout.Button("Save JSON", GUILayout.Height(32f)))
                {
                    SaveJson();
                }
            }

            if (GUILayout.Button("Clear Editor Objects"))
            {
                ClearRoot();
            }

            EditorGUILayout.Space(10f);
            DrawSelectionTools();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!sceneEditMode)
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            DrawGrid(sceneView);
            DrawSceneOverlay();
            DrawObjectHandles();
            HandleClickPlacement(sceneView);
        }

        private void DrawSceneOverlay()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 280f, 118f), "Draw Body Stage Editor", GUI.skin.window);
            GUILayout.Label($"Mode: {(placeOnClick ? "Click places objects" : "Select / edit objects")}");
            GUILayout.Label($"Type: {selectedType}  Size: {defaultSize.x:0.##} x {defaultSize.y:0.##}");
            GUILayout.Label($"Snap: {(snapToGrid ? gridSize.ToString("0.##") : "off")}");
            GUILayout.Label("Shortcuts: P toggle place, G snap, Delete selected");
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawGrid(SceneView sceneView)
        {
            float size = Mathf.Max(0.1f, gridSize);
            Vector3 center = sceneView.pivot;
            float extent = Mathf.Max(sceneView.size * 1.6f, 12f);
            float minX = Mathf.Floor((center.x - extent) / size) * size;
            float maxX = Mathf.Ceil((center.x + extent) / size) * size;
            float minY = Mathf.Floor((center.y - extent) / size) * size;
            float maxY = Mathf.Ceil((center.y + extent) / size) * size;

            Handles.color = new Color(0.25f, 0.45f, 0.8f, 0.14f);
            for (float x = minX; x <= maxX; x += size)
            {
                Handles.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));
            }

            for (float y = minY; y <= maxY; y += size)
            {
                Handles.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));
            }

            Handles.color = new Color(0.95f, 0.2f, 0.25f, 0.3f);
            Handles.DrawLine(new Vector3(0f, minY, 0f), new Vector3(0f, maxY, 0f));
            Handles.color = new Color(0.1f, 0.6f, 0.2f, 0.3f);
            Handles.DrawLine(new Vector3(minX, 0f, 0f), new Vector3(maxX, 0f, 0f));
        }

        private void DrawObjectHandles()
        {
            Transform root = GetOrCreateRoot();
            for (int i = 0; i < root.childCount; i++)
            {
                StageEditorObject stageObject = root.GetChild(i).GetComponent<StageEditorObject>();
                if (stageObject == null)
                {
                    continue;
                }

                DrawObjectOutline(stageObject);
            }

            StageEditorObject selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<StageEditorObject>()
                : null;

            if (selected == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(selected.transform.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selected.transform, "Move Stage Object");
                selected.transform.position = Snap(moved);
                EditorUtility.SetDirty(selected.transform);
            }

            if (selected.type == StageObjectType.Platform || selected.type == StageObjectType.Wall || selected.type == StageObjectType.Goal || selected.type == StageObjectType.BalanceScale || selected.type == StageObjectType.Weight)
            {
                DrawSizeHandles(selected);
            }
        }

        private void DrawObjectOutline(StageEditorObject stageObject)
        {
            Transform transform = stageObject.transform;
            Vector2 size = GetObjectWorldSize(stageObject);
            Vector3 center = transform.position;
            Quaternion rotation = transform.rotation;

            Vector3[] corners =
            {
                center + rotation * new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f),
                center + rotation * new Vector3(size.x * 0.5f, -size.y * 0.5f, 0f),
                center + rotation * new Vector3(size.x * 0.5f, size.y * 0.5f, 0f),
                center + rotation * new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f)
            };

            Handles.color = Selection.activeGameObject == stageObject.gameObject
                ? new Color(0.1f, 0.45f, 1f, 0.95f)
                : new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(3f, corners[0], corners[1], corners[2], corners[3], corners[0]);

            Handles.color = Color.black;
            Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.18f), $"{stageObject.type}");
        }

        private void DrawSizeHandles(StageEditorObject selected)
        {
            Vector2 size = GetObjectWorldSize(selected);
            Vector3 center = selected.transform.position;
            Quaternion rotation = selected.transform.rotation;

            float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;
            Vector3 right = center + rotation * new Vector3(size.x * 0.5f, 0f, 0f);
            Vector3 top = center + rotation * new Vector3(0f, size.y * 0.5f, 0f);

            EditorGUI.BeginChangeCheck();
            Vector3 movedRight = Handles.Slider(right, rotation * Vector3.right, handleSize, Handles.CubeHandleCap, gridSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selected.transform, "Resize Stage Object");
                Undo.RecordObject(selected, "Resize Stage Object");
                float newWidth = Mathf.Max(0.1f, Vector3.Dot(movedRight - center, rotation * Vector3.right) * 2f);
                if (snapToGrid)
                {
                    newWidth = Mathf.Max(gridSize, Mathf.Round(newWidth / gridSize) * gridSize);
                }
                SetObjectWorldSize(selected, new Vector2(newWidth, size.y));
            }

            EditorGUI.BeginChangeCheck();
            Vector3 movedTop = Handles.Slider(top, rotation * Vector3.up, handleSize, Handles.CubeHandleCap, gridSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selected.transform, "Resize Stage Object");
                Undo.RecordObject(selected, "Resize Stage Object");
                float newHeight = Mathf.Max(0.1f, Vector3.Dot(movedTop - center, rotation * Vector3.up) * 2f);
                if (snapToGrid)
                {
                    newHeight = Mathf.Max(gridSize, Mathf.Round(newHeight / gridSize) * gridSize);
                }
                SetObjectWorldSize(selected, new Vector2(size.x, newHeight));
            }
        }

        private void HandleClickPlacement(SceneView sceneView)
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.P)
                {
                    placeOnClick = !placeOnClick;
                    Repaint();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.G)
                {
                    snapToGrid = !snapToGrid;
                    Repaint();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
                {
                    StageEditorObject selected = Selection.activeGameObject != null
                        ? Selection.activeGameObject.GetComponent<StageEditorObject>()
                        : null;
                    if (selected != null)
                    {
                        DestroyImmediate(selected.gameObject);
                        current.Use();
                    }
                }
            }

            if (!placeOnClick || current.alt || current.button != 0)
            {
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (current.type == EventType.MouseDown)
            {
                addPosition = GetMouseWorldPosition(current.mousePosition);
                AddObject();
                current.Use();
                sceneView.Repaint();
            }
        }

        private void DrawSelectionTools()
        {
            StageEditorObject selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<StageEditorObject>()
                : null;

            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a stage object to edit its type and size.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            selected.type = (StageObjectType)EditorGUILayout.EnumPopup("Type", selected.type);
            selected.size = EditorGUILayout.Vector2Field("Size", selected.size);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildSelected(selected);
            }

            if (GUILayout.Button("Delete Selected"))
            {
                DestroyImmediate(selected.gameObject);
            }
        }

        private void AddObject()
        {
            Transform root = GetOrCreateRoot();
            StageObjectFactory factory = GetOrCreateFactory(root);
            StageObjectData data = StageObjectFactory.CreateDefaultData(selectedType, addPosition);
            data.size = defaultSize;
            GameObject obj = factory.Create(data, root);
            Selection.activeGameObject = obj;
            EditorUtility.SetDirty(root.gameObject);
        }

        private void RebuildSelected(StageEditorObject selected)
        {
            if (selected == null)
            {
                return;
            }

            Transform root = GetOrCreateRoot();
            StageObjectFactory factory = GetOrCreateFactory(root);
            StageObjectData data = ToData(selected);
            int sibling = selected.transform.GetSiblingIndex();
            DestroyImmediate(selected.gameObject);
            GameObject obj = factory.Create(data, root);
            obj.transform.SetSiblingIndex(Mathf.Min(sibling, root.childCount - 1));
            Selection.activeGameObject = obj;
        }

        private void SaveJson()
        {
            Transform root = GetOrCreateRoot();
            List<StageObjectData> objects = new List<StageObjectData>();

            for (int i = 0; i < root.childCount; i++)
            {
                StageEditorObject stageObject = root.GetChild(i).GetComponent<StageEditorObject>();
                if (stageObject == null)
                {
                    continue;
                }

                objects.Add(ToData(stageObject));
            }

            StageData data = new StageData
            {
                id = stageId,
                displayName = displayName,
                objects = objects.ToArray()
            };

            Directory.CreateDirectory(StageFolder);
            string path = Path.Combine(StageFolder, $"{stageId}.json").Replace("\\", "/");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
            Debug.Log($"Stage saved: {path}");
        }

        private void LoadJson()
        {
            string path = Path.Combine(StageFolder, $"{stageId}.json").Replace("\\", "/");
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Stage Editor", $"JSON not found: {path}", "OK");
                return;
            }

            StageData data = JsonUtility.FromJson<StageData>(File.ReadAllText(path));
            if (data == null)
            {
                EditorUtility.DisplayDialog("Stage Editor", "Failed to read JSON.", "OK");
                return;
            }

            stageId = data.id;
            displayName = data.displayName;
            ClearRoot();

            Transform root = GetOrCreateRoot();
            StageObjectFactory factory = GetOrCreateFactory(root);
            for (int i = 0; i < data.objects.Length; i++)
            {
                factory.Create(data.objects[i], root);
            }

            Debug.Log($"Stage loaded: {path}");
        }

        private static StageObjectData ToData(StageEditorObject stageObject)
        {
            Vector3 position = stageObject.transform.position;
            Vector3 rotation = stageObject.transform.eulerAngles;
            Vector2 size = stageObject.size;
            if (stageObject.type == StageObjectType.Platform || stageObject.type == StageObjectType.Wall || stageObject.type == StageObjectType.Goal || stageObject.type == StageObjectType.BalanceScale || stageObject.type == StageObjectType.Weight)
            {
                size = new Vector2(Mathf.Abs(stageObject.transform.localScale.x), Mathf.Abs(stageObject.transform.localScale.y));
            }

            return new StageObjectData
            {
                objectId = string.IsNullOrEmpty(stageObject.objectId)
                    ? $"{stageObject.type}_{System.Guid.NewGuid():N}".Substring(0, 14)
                    : stageObject.objectId,
                type = stageObject.type,
                position = new Vector2(position.x, position.y),
                size = size,
                rotation = rotation.z
            };
        }

        private static Transform GetOrCreateRoot()
        {
            GameObject root = GameObject.Find("StageEditorRoot");
            if (root == null)
            {
                root = new GameObject("StageEditorRoot");
            }

            return root.transform;
        }

        private static StageObjectFactory GetOrCreateFactory(Transform root)
        {
            StageObjectFactory factory = root.GetComponent<StageObjectFactory>();
            if (factory == null)
            {
                factory = root.gameObject.AddComponent<StageObjectFactory>();
            }

            return factory;
        }

        private static void ClearRoot()
        {
            Transform root = GetOrCreateRoot();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static Vector2 GetSceneCenter()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return Vector2.zero;
            }

            Vector3 pivot = view.pivot;
            return new Vector2(Mathf.Round(pivot.x * 2f) * 0.5f, Mathf.Round(pivot.y * 2f) * 0.5f);
        }

        private Vector2 GetMouseWorldPosition(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                return Vector2.zero;
            }

            Vector3 point = ray.GetPoint(distance);
            return Snap(point);
        }

        private Vector3 Snap(Vector3 point)
        {
            if (!snapToGrid)
            {
                return new Vector3(point.x, point.y, 0f);
            }

            float size = Mathf.Max(0.1f, gridSize);
            return new Vector3(
                Mathf.Round(point.x / size) * size,
                Mathf.Round(point.y / size) * size,
                0f);
        }

        private static Vector2 GetObjectWorldSize(StageEditorObject stageObject)
        {
            if (stageObject.type == StageObjectType.Spawn)
            {
                return stageObject.size;
            }

            Vector3 scale = stageObject.transform.localScale;
            return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private static void SetObjectWorldSize(StageEditorObject stageObject, Vector2 size)
        {
            stageObject.size = size;
            if (stageObject.type != StageObjectType.Spawn)
            {
                stageObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            }

            EditorUtility.SetDirty(stageObject);
            EditorUtility.SetDirty(stageObject.transform);
        }

        private static Vector2 GetDefaultSize(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Wall:
                    return new Vector2(0.55f, 2.2f);
                case StageObjectType.Spawn:
                    return new Vector2(0.7f, 0.7f);
                case StageObjectType.Goal:
                    return new Vector2(1.15f, 2.05f);
                case StageObjectType.BalanceScale:
                    return new Vector2(4.5f, 0.6f);
                case StageObjectType.Weight:
                    return new Vector2(0.9f, 0.9f);
                default:
                    return new Vector2(3f, 0.4f);
            }
        }
    }
}
