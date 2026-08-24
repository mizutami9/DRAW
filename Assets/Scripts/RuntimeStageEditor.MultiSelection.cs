using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor
    {
        private readonly List<StageObjectData> rangeSelectedObjects = new List<StageObjectData>();
        private readonly List<GameObject> rangeSelectionBoxes = new List<GameObject>();
        private readonly List<Vector2> groupDragInitialPositions = new List<Vector2>();
        private readonly List<GameObject> groupDragObjects = new List<GameObject>();
        private GameObject rangeSelectionMarquee;
        private bool rangeSelecting;
        private bool groupDragging;
        private Vector2 rangeSelectionStart;
        private Vector2 groupDragStartWorld;

        private void BeginRangeSelection(Vector2 world)
        {
            ClearRangeSelection();
            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            rangeSelectionStart = world;
            rangeSelecting = true;
            EnsureRangeSelectionMarquee();
            UpdateWorldRectLine(rangeSelectionMarquee, MakeRect(world, world));
            rangeSelectionMarquee.SetActive(true);
            SetStatus(LocalizationManager.T("stage_editor_status_range_selecting"));
        }

        private void UpdateRangeSelection(Vector2 world)
        {
            if (!rangeSelecting)
            {
                return;
            }

            EnsureRangeSelectionMarquee();
            UpdateWorldRectLine(rangeSelectionMarquee, MakeRect(rangeSelectionStart, world));
        }

        private void CompleteRangeSelection(Vector2 world)
        {
            if (!rangeSelecting)
            {
                return;
            }

            rangeSelecting = false;
            if (rangeSelectionMarquee != null)
            {
                rangeSelectionMarquee.SetActive(false);
            }

            Rect selectionRect = MakeRect(rangeSelectionStart, world);
            if (selectionRect.width < 0.05f && selectionRect.height < 0.05f)
            {
                ClearRangeSelection();
                selectedData = null;
                selectedObject = null;
                RefreshText();
                SetStatus(LocalizationManager.T("stage_editor_status_range_empty"));
                return;
            }

            rangeSelectedObjects.Clear();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData candidate = objects[i];
                if (candidate == null)
                {
                    continue;
                }

                Rect bounds = GetBoundaryFitBounds(candidate);
                const float tolerance = 0.001f;
                bool fullyContained = bounds.xMin >= selectionRect.xMin - tolerance
                    && bounds.xMax <= selectionRect.xMax + tolerance
                    && bounds.yMin >= selectionRect.yMin - tolerance
                    && bounds.yMax <= selectionRect.yMax + tolerance;
                if (fullyContained)
                {
                    rangeSelectedObjects.Add(candidate);
                }
            }

            if (rangeSelectedObjects.Count == 0)
            {
                selectedData = null;
                selectedObject = null;
                ClearRangeSelectionBoxes();
                SetStatus(LocalizationManager.T("stage_editor_status_range_empty"));
            }
            else if (rangeSelectedObjects.Count == 1)
            {
                StageObjectData only = rangeSelectedObjects[0];
                rangeSelectedObjects.Clear();
                selectedData = only;
                selectedObject = FindEditorObject(only.objectId);
                FocusListPageOn(only);
                SetStatus(LocalizationManager.T("stage_editor_status_selected"));
            }
            else
            {
                selectedData = rangeSelectedObjects[0];
                selectedObject = FindEditorObject(selectedData.objectId);
                FocusListPageOn(selectedData);
                UpdateRangeSelectionBoxes();
                SetStatus(LocalizationManager.Format(
                    "stage_editor_status_range_selected",
                    rangeSelectedObjects.Count));
            }

            RefreshText();
            RefreshListPanel();
        }

        private bool TryBeginGroupDrag(GameObject hit, Vector2 world)
        {
            if (hit == null || rangeSelectedObjects.Count < 2)
            {
                return false;
            }

            StageEditorObject marker = hit.GetComponent<StageEditorObject>();
            if (marker == null)
            {
                return false;
            }

            bool selected = false;
            for (int i = 0; i < rangeSelectedObjects.Count; i++)
            {
                if (rangeSelectedObjects[i] != null && rangeSelectedObjects[i].objectId == marker.objectId)
                {
                    selected = true;
                    break;
                }
            }

            if (!selected)
            {
                return false;
            }

            groupDragInitialPositions.Clear();
            groupDragObjects.Clear();
            for (int i = 0; i < rangeSelectedObjects.Count; i++)
            {
                StageObjectData data = rangeSelectedObjects[i];
                groupDragInitialPositions.Add(data.position);
                groupDragObjects.Add(FindEditorObject(data.objectId));
            }

            groupDragStartWorld = world;
            groupDragging = true;
            PushUndo();
            return true;
        }

        private void UpdateGroupDrag(Vector2 world)
        {
            if (!groupDragging || rangeSelectedObjects.Count < 2)
            {
                return;
            }

            Vector2 delta = world - groupDragStartWorld;
            if (snapToGrid && rangeSelectedObjects[0] != null)
            {
                Vector2 anchorStart = groupDragInitialPositions[0];
                Vector2 anchorTarget = SnapObjectPosition(rangeSelectedObjects[0], anchorStart + delta);
                delta = anchorTarget - anchorStart;
            }

            int count = Mathf.Min(
                rangeSelectedObjects.Count,
                Mathf.Min(groupDragInitialPositions.Count, groupDragObjects.Count));
            for (int i = 0; i < count; i++)
            {
                StageObjectData data = rangeSelectedObjects[i];
                if (data == null)
                {
                    continue;
                }

                Vector2 position = groupDragInitialPositions[i] + delta;
                data.position = position;
                if (groupDragObjects[i] != null)
                {
                    groupDragObjects[i].transform.position = position;
                }
            }
        }

        private void EndGroupDrag()
        {
            if (!groupDragging)
            {
                return;
            }

            groupDragging = false;
            groupDragInitialPositions.Clear();
            groupDragObjects.Clear();
            RefreshBridgeConnectionVisuals();
            RefreshText();
            RefreshListPanel();
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_range_moved",
                rangeSelectedObjects.Count));
        }

        private bool NudgeRangeSelection(Vector2 direction, float step)
        {
            if (rangeSelectedObjects.Count < 2)
            {
                return false;
            }

            PushUndo();
            Vector2 offset = direction * step;
            for (int i = 0; i < rangeSelectedObjects.Count; i++)
            {
                StageObjectData data = rangeSelectedObjects[i];
                if (data == null)
                {
                    continue;
                }

                data.position += offset;
                data.position = new Vector2(
                    Mathf.Round(data.position.x * 100f) / 100f,
                    Mathf.Round(data.position.y * 100f) / 100f);
                GameObject obj = FindEditorObject(data.objectId);
                if (obj != null)
                {
                    obj.transform.position = data.position;
                }
            }

            RefreshBridgeConnectionVisuals();
            RefreshText();
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_range_moved",
                rangeSelectedObjects.Count));
            return true;
        }

        private void ClearRangeSelection()
        {
            rangeSelecting = false;
            groupDragging = false;
            rangeSelectedObjects.Clear();
            groupDragInitialPositions.Clear();
            groupDragObjects.Clear();
            ClearRangeSelectionBoxes();
            if (rangeSelectionMarquee != null)
            {
                rangeSelectionMarquee.SetActive(false);
            }
        }

        private void UpdateRangeSelectionBoxes()
        {
            if (rangeSelectedObjects.Count < 2)
            {
                ClearRangeSelectionBoxes();
                return;
            }

            while (rangeSelectionBoxes.Count < rangeSelectedObjects.Count)
            {
                GameObject box = CreateWorldRectLine(
                    "RuntimeStageGroupSelectionBox",
                    new Color(0.08f, 0.62f, 0.92f, 1f),
                    0.045f,
                    82);
                rangeSelectionBoxes.Add(box);
            }

            for (int i = 0; i < rangeSelectionBoxes.Count; i++)
            {
                bool visible = i < rangeSelectedObjects.Count && rangeSelectedObjects[i] != null;
                rangeSelectionBoxes[i].SetActive(visible);
                if (visible)
                {
                    UpdateWorldRectLine(rangeSelectionBoxes[i], GetBoundaryFitBounds(rangeSelectedObjects[i]), 0.06f);
                }
            }
        }

        private void ClearRangeSelectionBoxes()
        {
            for (int i = 0; i < rangeSelectionBoxes.Count; i++)
            {
                if (rangeSelectionBoxes[i] != null)
                {
                    Destroy(rangeSelectionBoxes[i]);
                }
            }
            rangeSelectionBoxes.Clear();
        }

        private void EnsureRangeSelectionMarquee()
        {
            if (rangeSelectionMarquee == null)
            {
                rangeSelectionMarquee = CreateWorldRectLine(
                    "RuntimeStageRangeSelection",
                    new Color(1f, 0.48f, 0.08f, 1f),
                    0.055f,
                    90);
            }
        }

        private static GameObject CreateWorldRectLine(string name, Color color, float width, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.loop = false;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.material = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            return obj;
        }

        private static void UpdateWorldRectLine(GameObject obj, Rect rect, float padding = 0f)
        {
            if (obj == null)
            {
                return;
            }

            LineRenderer line = obj.GetComponent<LineRenderer>();
            if (line == null)
            {
                return;
            }

            float xMin = rect.xMin - padding;
            float xMax = rect.xMax + padding;
            float yMin = rect.yMin - padding;
            float yMax = rect.yMax + padding;
            line.SetPositions(new[]
            {
                new Vector3(xMin, yMin, -0.04f),
                new Vector3(xMin, yMax, -0.04f),
                new Vector3(xMax, yMax, -0.04f),
                new Vector3(xMax, yMin, -0.04f),
                new Vector3(xMin, yMin, -0.04f)
            });
        }

        private GameObject FindEditorObject(string objectId)
        {
            if (editorRoot == null || string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            for (int i = 0; i < editorRoot.childCount; i++)
            {
                StageEditorObject marker = editorRoot.GetChild(i).GetComponent<StageEditorObject>();
                if (marker != null && marker.objectId == objectId)
                {
                    return marker.gameObject;
                }
            }

            return null;
        }
    }
}
