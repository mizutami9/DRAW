using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor : MonoBehaviour
    {
        public void CreateOrFitStageBoundary()
        {
            if (!active)
            {
                return;
            }

            bool hasContent = false;
            Rect contentBounds = default;
            StageObjectData boundary = null;
            List<StageObjectData> duplicateBoundaries = new List<StageObjectData>();

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data == null)
                {
                    continue;
                }

                if (data.type == StageObjectType.StageBoundary)
                {
                    if (boundary == null)
                    {
                        boundary = data;
                    }
                    else
                    {
                        duplicateBoundaries.Add(data);
                    }
                    continue;
                }

                if (StageObjectCatalog.Get(data.type).Category == StageObjectCategory.Decoration)
                {
                    continue;
                }

                Rect bounds = GetBoundaryFitBounds(data);
                if (!hasContent)
                {
                    contentBounds = bounds;
                    hasContent = true;
                }
                else
                {
                    contentBounds = Rect.MinMaxRect(
                        Mathf.Min(contentBounds.xMin, bounds.xMin),
                        Mathf.Min(contentBounds.yMin, bounds.yMin),
                        Mathf.Max(contentBounds.xMax, bounds.xMax),
                        Mathf.Max(contentBounds.yMax, bounds.yMax));
                }
            }

            PushUndo();

            for (int i = 0; i < duplicateBoundaries.Count; i++)
            {
                StageObjectData duplicate = duplicateBoundaries[i];
                RemoveEditorObjectById(duplicate.objectId);
                objects.Remove(duplicate);
            }

            if (boundary == null)
            {
                boundary = StageObjectFactory.CreateDefaultData(
                    StageObjectType.StageBoundary,
                    hasContent ? contentBounds.center : Vector2.zero);
                objects.Add(boundary);
            }
            else
            {
                RemoveEditorObjectById(boundary.objectId);
            }

            float step = Mathf.Max(0.1f, gridSize);
            if (hasContent)
            {
                const float wallThickness = 0.5f;
                const float topHeadroom = 2.5f;
                float left = Mathf.Floor((contentBounds.xMin - wallThickness) / step) * step;
                float right = Mathf.Ceil((contentBounds.xMax + wallThickness) / step) * step;
                float bottom = Mathf.Floor(contentBounds.yMin / step) * step;
                float top = Mathf.Ceil((contentBounds.yMax + topHeadroom) / step) * step;
                boundary.position = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
                boundary.size = new Vector2(Mathf.Max(4f, right - left), Mathf.Max(4f, top - bottom));
            }
            else
            {
                boundary.position = Vector2.zero;
                boundary.size = new Vector2(30f, 18f);
            }

            boundary.rotation = 0f;
            boundary.pathThickness = 0.5f;
            CreateEditorObject(boundary);
            SelectData(boundary);
            RefreshText();
            RefreshListPanel();
            SetStatus(LocalizationManager.T("stage_editor_status_boundary_fitted"));
        }

        private static Rect GetBoundaryFitBounds(StageObjectData data)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                bool hasPart = false;
                Rect result = default;
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part == null)
                    {
                        continue;
                    }

                    Vector2 worldCenter = PathPointToWorld(data, part.position);
                    StageObjectData partData = new StageObjectData
                    {
                        position = worldCenter,
                        size = part.size,
                        rotation = data.rotation
                    };
                    Rect partBounds = RectFromData(partData, worldCenter);
                    result = hasPart
                        ? Rect.MinMaxRect(
                            Mathf.Min(result.xMin, partBounds.xMin),
                            Mathf.Min(result.yMin, partBounds.yMin),
                            Mathf.Max(result.xMax, partBounds.xMax),
                            Mathf.Max(result.yMax, partBounds.yMax))
                        : partBounds;
                    hasPart = true;
                }

                if (hasPart)
                {
                    return result;
                }
            }

            if (data.pathPoints != null && data.pathPoints.Length > 0)
            {
                Vector2 first = PathPointToWorld(data, data.pathPoints[0]);
                float minX = first.x;
                float minY = first.y;
                float maxX = first.x;
                float maxY = first.y;
                for (int i = 1; i < data.pathPoints.Length; i++)
                {
                    Vector2 point = PathPointToWorld(data, data.pathPoints[i]);
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }

                float halfThickness = Mathf.Max(0.1f, data.pathThickness * 0.5f);
                return Rect.MinMaxRect(
                    minX - halfThickness,
                    minY - halfThickness,
                    maxX + halfThickness,
                    maxY + halfThickness);
            }

            return RectFromData(data, data.position);
        }

        public void ResizeSelected(Vector2 delta)
        {
            ResizeSelected(delta, true);
        }

        private void ResizeSelected(Vector2 delta, bool recordUndo)
        {
            if (selectedData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_first"));
                return;
            }

            if (recordUndo)
            {
                PushUndo();
            }

            if (selectedData.type == StageObjectType.StageBoundary)
            {
                Vector2 previousSize = selectedData.size;
                float sizeStep = Mathf.Max(0.1f, gridSize);
                Vector2 nextSize = new Vector2(
                    delta.x == 0f
                        ? previousSize.x
                        : Mathf.Max(4f, Mathf.Round((previousSize.x + delta.x) / sizeStep) * sizeStep),
                    delta.y == 0f
                        ? previousSize.y
                        : Mathf.Max(4f, Mathf.Round((previousSize.y + delta.y) / sizeStep) * sizeStep));
                Vector2 appliedDelta = nextSize - previousSize;

                // The left wall and invisible lower edge remain fixed. Width moves
                // only the right wall, and height moves only the ceiling.
                selectedData.position += appliedDelta * 0.5f;
                selectedData.size = nextSize;
            }
            else
            {
                Vector2 localDelta = delta;
                if (IsSurfaceMountedType(selectedData.type))
                {
                    // Switches often need to fit inside narrow terrain recesses.
                    // Keep their resize buttons/wheel precise without changing
                    // the terrain editor's regular grid increments.
                    localDelta *= 0.2f;
                }
                if (StageObjectCatalog.IsRectPlacement(selectedData.type))
                {
                    float angleToVertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, selectedData.rotation)) - 90f);
                    if (angleToVertical < 2f)
                    {
                        localDelta = new Vector2(delta.y, delta.x);
                    }
                }

                float sizeStep = IsSurfaceMountedType(selectedData.type)
                    ? 0.1f
                    : Mathf.Max(0.1f, gridSize);
                Vector2 nextSize = new Vector2(
                    localDelta.x == 0f
                        ? selectedData.size.x
                        : Mathf.Max(0.2f, Mathf.Round((selectedData.size.x + localDelta.x) / sizeStep) * sizeStep),
                    localDelta.y == 0f
                        ? selectedData.size.y
                        : Mathf.Max(0.2f, Mathf.Round((selectedData.size.y + localDelta.y) / sizeStep) * sizeStep));
                ResizeDetailedGeometry(selectedData, nextSize);
            }
            RebuildSelectedObject();
            RefreshText();
            GameSfx.Play(SfxId.EditorObjectResize);
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

        private void ScaleSelectedProportionally(float direction)
        {
            if (selectedData == null)
            {
                return;
            }

            PushUndo();
            float factor = direction > 0f ? 1.08f : 1f / 1.08f;
            Vector2 nextSize = new Vector2(
                Mathf.Max(0.2f, selectedData.size.x * factor),
                Mathf.Max(0.2f, selectedData.size.y * factor));
            ResizeDetailedGeometry(selectedData, nextSize);
            RebuildSelectedObject();
            RefreshText();
            GameSfx.Play(SfxId.EditorObjectResize);
        }

        private static void ResizeDetailedGeometry(StageObjectData data, Vector2 nextSize)
        {
            if (data == null)
            {
                return;
            }

            Vector2 previousSize = new Vector2(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y));
            Vector2 scale = new Vector2(nextSize.x / previousSize.x, nextSize.y / previousSize.y);

            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                bool hasPart = false;
                Vector2 min = Vector2.zero;
                Vector2 max = Vector2.zero;
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part == null) continue;
                    Vector2 half = part.size * 0.5f;
                    if (!hasPart)
                    {
                        min = part.position - half;
                        max = part.position + half;
                        hasPart = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, part.position - half);
                        max = Vector2.Max(max, part.position + half);
                    }
                }

                Vector2 center = hasPart ? (min + max) * 0.5f : Vector2.zero;
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part == null) continue;
                    Vector2 offset = part.position - center;
                    part.position = center + Vector2.Scale(offset, scale);
                    part.size = new Vector2(
                        Mathf.Max(0.2f, part.size.x * scale.x),
                        Mathf.Max(0.2f, part.size.y * scale.y));
                }
            }

            if (data.pathPoints != null && data.pathPoints.Length >= 2)
            {
                Vector2 min = data.pathPoints[0];
                Vector2 max = data.pathPoints[0];
                for (int i = 1; i < data.pathPoints.Length; i++)
                {
                    min = Vector2.Min(min, data.pathPoints[i]);
                    max = Vector2.Max(max, data.pathPoints[i]);
                }

                Vector2 center = (min + max) * 0.5f;
                for (int i = 0; i < data.pathPoints.Length; i++)
                {
                    data.pathPoints[i] = center + Vector2.Scale(data.pathPoints[i] - center, scale);
                }

                Vector2 pathSpan = max - min;
                float thicknessScale = pathSpan.x >= pathSpan.y ? scale.y : scale.x;
                data.pathThickness = Mathf.Max(0.2f, data.pathThickness * thicknessScale);
            }

            data.size = nextSize;
        }

        private void RotateSelected(float degrees)
        {
            if (selectedData == null)
            {
                return;
            }

            PushUndo();
            float next = Mathf.Repeat(selectedData.rotation + degrees + 180f, 360f) - 180f;
            selectedData.rotation = Mathf.Round(next * 2f) * 0.5f;
            RebuildSelectedObject();
            RefreshText();
            GameSfx.Play(SfxId.EditorObjectRotate);
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
            if (selectedObject != null)
            {
                DestroyEditorObject(selectedObject);
                selectedObject = null;
            }
            else
            {
                RemoveEditorObjectById(objectId);
            }
            RefreshBridgeConnectionVisuals();

            selectedData = null;
            selectedObject = null;
            SetSelectionBox(false);
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectDelete);
        }

        public void DuplicateSelected()
        {
            if (selectedData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_first"));
                return;
            }

            PushUndo();
            StageObjectData duplicate = CloneData(selectedData);
            duplicate.objectId = StageObjectId.New();
            Rect sourceBounds = GetBoundaryFitBounds(selectedData);
            float horizontalOffset = Mathf.Max(0.2f, sourceBounds.width);
            float verticalOffset = Mathf.Max(0.2f, sourceBounds.height);
            Vector2 offset;
            switch (copyDirection)
            {
                case CopyDirection.Left:
                    offset = Vector2.left * horizontalOffset;
                    break;
                case CopyDirection.Up:
                    offset = Vector2.up * verticalOffset;
                    break;
                case CopyDirection.Down:
                    offset = Vector2.down * verticalOffset;
                    break;
                default:
                    offset = Vector2.right * horizontalOffset;
                    break;
            }
            duplicate.position = selectedData.position + offset;
            objects.Add(duplicate);
            CreateEditorObject(duplicate);
            SelectData(duplicate);
            RefreshBridgeConnectionVisuals();
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectCopy);
            SetStatus(LocalizationManager.Format("stage_editor_status_copied_direction", CopyDirectionLabel));
        }

        public void CycleCopyDirection()
        {
            copyDirection = (CopyDirection)(((int)copyDirection + 1) % 4);
            editorPanel?.GetComponent<StageEditorVisualPolisher>()?.RefreshState(this);
            SetStatus(LocalizationManager.Format("stage_editor_status_copy_direction", CopyDirectionLabel));
        }

        private void HandleSelectedObjectNudge()
        {
            if (selectedData == null || selectedObject == null || IsPointerOverEditorUi())
            {
                return;
            }

            Vector2 direction = Vector2.zero;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                direction.x -= 1f;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                direction.x += 1f;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                direction.y -= 1f;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                direction.y += 1f;
            }

            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            bool fine = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            float step = fine ? 0.02f : 0.1f;
            if (NudgeRangeSelection(direction, step))
            {
                return;
            }
            PushUndo();
            selectedData.position += direction * step;
            selectedData.position = new Vector2(
                Mathf.Round(selectedData.position.x * 100f) / 100f,
                Mathf.Round(selectedData.position.y * 100f) / 100f);
            selectedObject.transform.position = selectedData.position;
            RefreshBridgeConnectionVisuals();
            RefreshText();
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_nudge",
                selectedData.position.x,
                selectedData.position.y));
        }

        private void HandleMouse()
        {
            if (IsPointerOverEditorUi())
            {
                if (drawingTerrainStroke && Input.GetMouseButtonUp(0))
                {
                    CommitTerrainStroke(terrainStrokeLastPoint);
                }

                if (dragging && snapToGrid)
                {
                    FinalizeDraggedTerrainConnection();
                }
                if (dragging)
                {
                    GameSfx.Play(SfxId.EditorObjectDrop);
                }
                if (rangeSelecting)
                {
                    ClearRangeSelection();
                }
                if (groupDragging)
                {
                    EndGroupDrag();
                }
                dragging = false;
                return;
            }

            Vector2 world = ScreenToWorld(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                if (snapToGrid
                    && !terrainKeepSeparate
                    && terrainFreehand
                    && IsFreehandTerrainType(addType)
                    && TryFindTerrainPathEndpoint(world, null, out StageObjectData extendData, out Vector2 extendPoint))
                {
                    BeginTerrainStroke(extendPoint, extendData);
                    return;
                }

                GameObject hit = FindObjectAt(world);
                if (hit != null)
                {
                    if (TryBeginGroupDrag(hit, world))
                    {
                        return;
                    }
                    SelectObject(hit);
                    dragOffset = (Vector2)hit.transform.position - world;
                    dragging = true;
                    PushUndo();
                    GameSfx.Play(SfxId.EditorObjectMove);
                }
                else
                {
                    // Drawing mode OFF is a dedicated selection/edit mode.
                    // Empty-space clicks must not create an object in this mode.
                    if (!terrainFreehand)
                    {
                        BeginRangeSelection(world);
                        return;
                    }

                    if (IsFreehandTerrainType(addType))
                    {
                        BeginTerrainStroke(snapToGrid ? SnapTerrainConnectionPoint(world, null) : world);
                    }
                    else if (IsBlockType(addType))
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
                if (rangeSelecting)
                {
                    CompleteRangeSelection(world);
                }
                else if (groupDragging)
                {
                    EndGroupDrag();
                }
                else if (drawingRect)
                {
                    CommitRect(world);
                }

                else if (drawingTerrainStroke)
                {
                    CommitTerrainStroke(world);
                }

                if (dragging && snapToGrid)
                {
                    FinalizeDraggedTerrainConnection();
                }
                if (dragging)
                {
                    GameSfx.Play(SfxId.EditorObjectDrop);
                }
                dragging = false;
            }

            if (drawingRect && Input.GetMouseButton(0))
            {
                UpdateDragPreview(world);
            }

            if (rangeSelecting && Input.GetMouseButton(0))
            {
                UpdateRangeSelection(world);
            }

            if (groupDragging && Input.GetMouseButton(0))
            {
                UpdateGroupDrag(world);
            }


            if (drawingTerrainStroke && Input.GetMouseButton(0))
            {
                UpdateTerrainStroke(world);
            }

            if (dragging && selectedData != null && selectedObject != null && Input.GetMouseButton(0))
            {
                Vector2 next = world + dragOffset;
                if (snapToGrid)
                {
                    next = SnapObjectPosition(selectedData, next);
                }

                next = SnapJoinPosition(next);
                next = SnapMountedObjectToSurface(selectedData, next);
                selectedData.position = next;
                selectedObject.transform.position = next;
                RefreshText();
            }

            if (selectedData != null && rangeSelectedObjects.Count < 2 && !Input.GetMouseButton(0))
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
            if (Input.GetKey(KeyCode.A) || (selectedData == null && Input.GetKey(KeyCode.LeftArrow)))
            {
                move.x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || (selectedData == null && Input.GetKey(KeyCode.RightArrow)))
            {
                move.x += 1f;
            }

            if (Input.GetKey(KeyCode.W) || (selectedData == null && Input.GetKey(KeyCode.UpArrow)))
            {
                move.y += 1f;
            }

            if (Input.GetKey(KeyCode.S) || (selectedData == null && Input.GetKey(KeyCode.DownArrow)))
            {
                move.y -= 1f;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                worldCamera.transform.position += move.normalized * cameraMoveSpeed * Time.unscaledDeltaTime;
            }

            float wheel = Input.mouseScrollDelta.y;
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Mathf.Abs(wheel) > 0.01f && (control || selectedData == null))
            {
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - wheel * 0.65f, minCameraSize, maxCameraSize);
            }
        }

        private void HandleSelectedObjectWheel()
        {
            float wheel = Input.mouseScrollDelta.y;
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (control || Mathf.Abs(wheel) < 0.01f || IsPointerOverEditorUi())
            {
                return;
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool widthOnly = Input.GetKey(KeyCode.X);
            bool movementDirection = Input.GetKey(KeyCode.M);
            bool isDecoration = StageObjectCatalog.Get(selectedData.type).Category == StageObjectCategory.Decoration;
            if (movementDirection && selectedData.type == StageObjectType.MovingPlatform)
            {
                RotateSelectedMovementDirection(wheel * (alt ? 1f : 15f));
                SetStatus(LocalizationManager.Format(
                    "stage_editor_status_move_direction",
                    selectedData.movementAngle));
                return;
            }

            // The stage boundary is an axis-aligned editing frame rather than a
            // placeable object. Every other object uses the same rotation input.
            if (shift && selectedData.type != StageObjectType.StageBoundary)
            {
                RotateSelected(wheel * (alt ? 1f : 15f));
                SetStatus(LocalizationManager.T("stage_editor_status_rotate_object"));
                return;
            }

            if (isDecoration)
            {
                ScaleSelectedProportionally(Mathf.Sign(wheel));
                SetStatus(LocalizationManager.T("stage_editor_status_scale_decoration"));
                return;
            }

            bool horizontalOnly = widthOnly;
            bool verticalOnly = alt;
            bool horizontal = !verticalOnly;
            bool vertical = !horizontalOnly;
            ScaleSelected(Mathf.Sign(wheel), horizontal, vertical);

            if (horizontalOnly)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_resize_width"));
            }
            else if (verticalOnly)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_resize_height"));
            }
            else
            {
                SetStatus(LocalizationManager.T("stage_editor_status_resize_all"));
            }
        }

        private void RotateSelectedMovementDirection(float degrees)
        {
            if (selectedData == null || selectedData.type != StageObjectType.MovingPlatform)
            {
                return;
            }

            PushUndo();
            float next = Mathf.Repeat(selectedData.movementAngle + degrees + 180f, 360f) - 180f;
            selectedData.movementAngle = Mathf.Round(next);
            RebuildSelectedObject();
            RefreshText();
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
                SetStatus(LocalizationManager.T("stage_editor_status_drag_larger"));
                return;
            }

            AddBlock(rect.center, rect.size);
        }

        private void AddBlock(Vector2 center, Vector2 size)
        {
            PushUndo();
            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, center);
            data.size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
            data.keepSeparate = terrainKeepSeparate;
            data.position = SnapJoinPosition(data, data.position);
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            RefreshBridgeConnectionVisuals();
            SetStatus(LocalizationManager.Format("stage_editor_status_placed_rect", GetObjectLabel(addType), data.size.x, data.size.y));
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectPlace);
        }

        private void BeginTerrainStroke(Vector2 world, StageObjectData extendData = null)
        {
            drawingTerrainStroke = true;
            terrainStrokeLastPoint = world;
            terrainStrokeLastData = null;
            terrainStrokeExtendData = extendData;
            terrainStrokeSegmentCount = 0;
            terrainStrokeForcePath = extendData != null;
            terrainStrokePoints.Clear();
            PushUndo();

            if (extendData != null && extendData.pathPoints != null && extendData.pathPoints.Length >= 2)
            {
                Vector2 first = PathPointToWorld(extendData, extendData.pathPoints[0]);
                Vector2 last = PathPointToWorld(extendData, extendData.pathPoints[extendData.pathPoints.Length - 1]);
                bool extendAtFirst = Vector2.Distance(world, first) <= Vector2.Distance(world, last);
                if (extendAtFirst)
                {
                    for (int i = extendData.pathPoints.Length - 1; i >= 0; i--)
                    {
                        terrainStrokePoints.Add(PathPointToWorld(extendData, extendData.pathPoints[i]));
                    }
                }
                else
                {
                    for (int i = 0; i < extendData.pathPoints.Length; i++)
                    {
                        terrainStrokePoints.Add(PathPointToWorld(extendData, extendData.pathPoints[i]));
                    }
                }

                terrainStrokeLastPoint = terrainStrokePoints[terrainStrokePoints.Count - 1];
                terrainPathThickness = Mathf.Max(0.25f, extendData.pathThickness);
                objects.Remove(extendData);
                RemoveEditorObjectById(extendData.objectId);
            }
            else
            {
                terrainStrokePoints.Add(world);
            }
            terrainStrokeBasePointCount = terrainStrokePoints.Count;

            ClearDragPreview();
            dragPreviewObject = new GameObject("TerrainPathPreview");
            dragPreviewObject.transform.SetParent(editorRoot, false);
            LineRenderer line = dragPreviewObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = terrainStrokePoints.Count;
            for (int i = 0; i < terrainStrokePoints.Count; i++)
            {
                line.SetPosition(i, terrainStrokePoints[i]);
            }
            line.startWidth = terrainPathThickness;
            line.endWidth = terrainPathThickness;
            line.numCapVertices = 6;
            line.numCornerVertices = 6;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.12f, 0.42f, 1f, 0.42f);
            line.endColor = line.startColor;
            line.sortingOrder = 90;
        }

        private void UpdateTerrainStroke(Vector2 world)
        {
            if (terrainStraightLine)
            {
                if (terrainStrokePoints.Count == terrainStrokeBasePointCount)
                {
                    terrainStrokePoints.Add(world);
                }
                else
                {
                    terrainStrokePoints[terrainStrokeBasePointCount] = world;
                }

                terrainStrokeLastPoint = world;
                RefreshTerrainStrokePreview();
                return;
            }

            const float sampleDistance = 0.24f;
            Vector2 delta = world - terrainStrokeLastPoint;
            float distance = delta.magnitude;
            if (distance < sampleDistance)
            {
                return;
            }

            Vector2 direction = delta / distance;
            while (distance >= sampleDistance)
            {
                Vector2 next = terrainStrokeLastPoint + direction * sampleDistance;
                terrainStrokePoints.Add(next);
                terrainStrokeLastPoint = next;
                delta = world - terrainStrokeLastPoint;
                distance = delta.magnitude;
                if (distance > 0.001f)
                {
                    direction = delta / distance;
                }
            }

            RefreshTerrainStrokePreview();
        }

        private void CommitTerrainStroke(Vector2 world)
        {
            if (!drawingTerrainStroke)
            {
                return;
            }

            StageObjectData endJoinData = null;
            if (snapToGrid && !terrainKeepSeparate && TryFindTerrainPathEndpoint(world, terrainStrokeExtendData, out endJoinData, out Vector2 joinedEndpoint))
            {
                world = joinedEndpoint;
            }
            else if (snapToGrid)
            {
                world = SnapTerrainConnectionPoint(world, terrainStrokeExtendData);
            }

            if (terrainStraightLine)
            {
                if (terrainStrokePoints.Count == terrainStrokeBasePointCount)
                {
                    terrainStrokePoints.Add(world);
                }
                else
                {
                    terrainStrokePoints[terrainStrokeBasePointCount] = world;
                }
            }
            else if (Vector2.Distance(terrainStrokePoints[terrainStrokePoints.Count - 1], world) > 0.05f)
            {
                terrainStrokePoints.Add(world);
            }

            if (endJoinData != null)
            {
                AppendJoinedTerrainPath(endJoinData, world);
                objects.Remove(endJoinData);
                RemoveEditorObjectById(endJoinData.objectId);
                terrainStrokeForcePath = true;
            }

            drawingTerrainStroke = false;
            ClearDragPreview();
            if (terrainStrokePoints.Count < 2 || Vector2.Distance(terrainStrokePoints[0], terrainStrokePoints[terrainStrokePoints.Count - 1]) < 0.08f)
            {
                terrainStrokePoints.Clear();
                SetStatus(LocalizationManager.T("stage_editor_status_drag_larger"));
                return;
            }

            terrainStrokeLastData = CreateTerrainPathData();
            if (terrainStrokeExtendData != null)
            {
                terrainStrokeLastData.type = terrainStrokeExtendData.type;
            }
            objects.Add(terrainStrokeLastData);
            CreateEditorObject(terrainStrokeLastData);
            SelectData(terrainStrokeLastData);
            terrainStrokeSegmentCount = terrainStrokePoints.Count;
            SetStatus(LocalizationManager.Format("stage_editor_status_freehand_done", GetObjectLabel(addType), terrainStrokeSegmentCount));
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectPlace);
            terrainStrokeExtendData = null;
        }

        private bool TryFindTerrainPathEndpoint(Vector2 world, StageObjectData ignored, out StageObjectData data, out Vector2 point)
        {
            float bestDistance = Mathf.Max(0.35f, gridSize * 0.9f);
            data = null;
            point = world;
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData candidate = objects[i];
                if (candidate == null || candidate == ignored || candidate.pathPoints == null || candidate.pathPoints.Length < 2)
                {
                    continue;
                }

                if (candidate.keepSeparate)
                {
                    continue;
                }

                Vector2 first = PathPointToWorld(candidate, candidate.pathPoints[0]);
                Vector2 last = PathPointToWorld(candidate, candidate.pathPoints[candidate.pathPoints.Length - 1]);
                float firstDistance = Vector2.Distance(world, first);
                float lastDistance = Vector2.Distance(world, last);
                if (firstDistance < bestDistance)
                {
                    bestDistance = firstDistance;
                    data = candidate;
                    point = first;
                }

                if (lastDistance < bestDistance)
                {
                    bestDistance = lastDistance;
                    data = candidate;
                    point = last;
                }
            }

            return data != null;
        }

        private Vector2 SnapTerrainConnectionPoint(Vector2 world, StageObjectData ignored)
        {
            if (TryFindTerrainPathEndpoint(world, ignored, out _, out Vector2 endpoint))
            {
                return endpoint;
            }

            float bestDistance = Mathf.Max(0.35f, gridSize * 0.9f);
            Vector2 best = Snap(world);
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData candidate = objects[i];
                if (candidate == null || candidate == ignored || candidate.pathPoints != null && candidate.pathPoints.Length >= 2 || !IsFreehandTerrainType(candidate.type))
                {
                    continue;
                }

                float radians = -candidate.rotation * Mathf.Deg2Rad;
                Vector2 offset = world - candidate.position;
                Vector2 local = new Vector2(
                    offset.x * Mathf.Cos(radians) - offset.y * Mathf.Sin(radians),
                    offset.x * Mathf.Sin(radians) + offset.y * Mathf.Cos(radians));
                Vector2 half = candidate.size * 0.5f;
                Vector2 clamped = new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
                float left = Mathf.Abs(local.x + half.x);
                float right = Mathf.Abs(local.x - half.x);
                float bottom = Mathf.Abs(local.y + half.y);
                float top = Mathf.Abs(local.y - half.y);
                float edge = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
                if (edge == left) clamped.x = -half.x;
                else if (edge == right) clamped.x = half.x;
                else if (edge == bottom) clamped.y = -half.y;
                else clamped.y = half.y;

                float forward = candidate.rotation * Mathf.Deg2Rad;
                Vector2 snapped = candidate.position + new Vector2(
                    clamped.x * Mathf.Cos(forward) - clamped.y * Mathf.Sin(forward),
                    clamped.x * Mathf.Sin(forward) + clamped.y * Mathf.Cos(forward));
                float distance = Vector2.Distance(world, snapped);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = snapped;
                }
            }

            return best;
        }

        private static Vector2 PathPointToWorld(StageObjectData data, Vector2 localPoint)
        {
            float radians = data.rotation * Mathf.Deg2Rad;
            return data.position + new Vector2(
                localPoint.x * Mathf.Cos(radians) - localPoint.y * Mathf.Sin(radians),
                localPoint.x * Mathf.Sin(radians) + localPoint.y * Mathf.Cos(radians));
        }

        private void FinalizeDraggedTerrainConnection()
        {
            if (selectedData != null)
            {
                Vector2 fittedPosition = selectedData.position;
                if (FitVerticalWallBetweenSurfaces(selectedData, ref fittedPosition))
                {
                    selectedData.position = fittedPosition;
                    RebuildSelectedObject();
                    RefreshBridgeConnectionVisuals();
                    SetStatus(LocalizationManager.T("stage_editor_status_wall_fitted"));
                    RefreshText();
                    RefreshListPanel();
                    return;
                }
            }

            if (IsSeparateHorizontalBridge(selectedData))
            {
                RebuildSelectedObject();
                RefreshBridgeConnectionVisuals();
                return;
            }

            RefreshBridgeConnectionVisuals();
            if (selectedData == null
                || !IsFreehandTerrainType(selectedData.type)
                || selectedData.keepSeparate
                || selectedData.pathPoints != null && selectedData.pathPoints.Length >= 2
                || Mathf.Abs(Mathf.DeltaAngle(selectedData.rotation, 0f)) > 0.1f)
            {
                return;
            }

            bool merged = false;
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                StageObjectData other = objects[i];
                if (!CanMergeTerrainRects(selectedData, other))
                {
                    continue;
                }

                MergeTerrainRects(selectedData, other);
                objects.Remove(other);
                RemoveEditorObjectById(other.objectId);
                merged = true;
            }

            if (!merged)
            {
                return;
            }

            RebuildSelectedObject();
            SetStatus(LocalizationManager.T("stage_editor_status_terrain_connected"));
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectPlace);
        }

        private bool SplitSelectedConnectedTerrain()
        {
            if (selectedData == null || selectedData.connectedRects == null || selectedData.connectedRects.Length == 0)
            {
                return false;
            }

            PushUndo();
            StageObjectData connected = selectedData;
            objects.Remove(connected);
            RemoveEditorObjectById(connected.objectId);
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].linkTargetId == connected.objectId)
                {
                    objects[i].linkTargetId = string.Empty;
                    objects[i].linkAction = string.Empty;
                }
            }
            StageObjectData lastPart = null;
            for (int i = 0; i < connected.connectedRects.Length; i++)
            {
                StageRectPartData part = connected.connectedRects[i];
                if (part == null) continue;
                StageObjectData data = StageObjectFactory.CreateDefaultData(connected.type, connected.position + part.position);
                data.size = part.size;
                data.keepSeparate = true;
                objects.Add(data);
                CreateEditorObject(data);
                lastPart = data;
            }

            if (lastPart != null)
            {
                SelectData(lastPart);
            }
            else
            {
                selectedData = null;
                selectedObject = null;
            }

            RefreshText();
            RefreshListPanel();
            RefreshBridgeConnectionVisuals();
            return true;
        }

        private static bool CanMergeTerrainRects(StageObjectData moving, StageObjectData other)
        {
            if (other == null
                || other == moving
                || other.keepSeparate
                || !IsFreehandTerrainType(other.type)
                || other.pathPoints != null && other.pathPoints.Length >= 2
                || Mathf.Abs(Mathf.DeltaAngle(other.rotation, 0f)) > 0.1f)
            {
                return false;
            }

            Rect a = RectFromData(moving, moving.position);
            Rect b = RectFromData(other, other.position);
            const float tolerance = 0.035f;
            float xGap = Mathf.Max(0f, Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax));
            float yGap = Mathf.Max(0f, Mathf.Max(a.yMin, b.yMin) - Mathf.Min(a.yMax, b.yMax));
            float xOverlap = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float yOverlap = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return (xGap <= tolerance && yOverlap > tolerance)
                || (yGap <= tolerance && xOverlap > tolerance);
        }

        private static void MergeTerrainRects(StageObjectData target, StageObjectData other)
        {
            List<StageRectPartData> worldParts = new List<StageRectPartData>();
            AppendWorldRectParts(worldParts, target);
            AppendWorldRectParts(worldParts, other);

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < worldParts.Count; i++)
            {
                Vector2 half = worldParts[i].size * 0.5f;
                min = Vector2.Min(min, worldParts[i].position - half);
                max = Vector2.Max(max, worldParts[i].position + half);
            }

            Vector2 center = (min + max) * 0.5f;
            StageRectPartData[] localParts = new StageRectPartData[worldParts.Count];
            for (int i = 0; i < worldParts.Count; i++)
            {
                localParts[i] = new StageRectPartData
                {
                    position = worldParts[i].position - center,
                    size = worldParts[i].size
                };
            }

            target.position = center;
            target.size = max - min;
            target.rotation = 0f;
            target.type = target.type == StageObjectType.Platform || other.type == StageObjectType.Platform
                ? StageObjectType.Platform
                : StageObjectType.Wall;
            target.connectedRects = localParts;
        }

        private static void AppendWorldRectParts(List<StageRectPartData> destination, StageObjectData data)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part == null) continue;
                    destination.Add(new StageRectPartData
                    {
                        position = data.position + part.position,
                        size = part.size
                    });
                }
                return;
            }

            destination.Add(new StageRectPartData
            {
                position = data.position,
                size = data.size
            });
        }

        private StageObjectData CreateTerrainPathData()
        {
            Vector2 min = terrainStrokePoints[0];
            Vector2 max = terrainStrokePoints[0];
            for (int i = 0; i < terrainStrokePoints.Count; i++)
            {
                Vector2 point = terrainStrokePoints[i];
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            float thickness = terrainPathThickness;
            Vector2 center = (min + max) * 0.5f;
            Vector2[] localPoints = new Vector2[terrainStrokePoints.Count];
            for (int i = 0; i < terrainStrokePoints.Count; i++)
            {
                localPoints[i] = terrainStrokePoints[i] - center;
            }

            StageObjectData data = StageObjectFactory.CreateDefaultData(addType, center);
            data.keepSeparate = terrainKeepSeparate;
            if (terrainStraightLine && !terrainStrokeForcePath && terrainStrokePoints.Count >= 2)
            {
                Vector2 delta = terrainStrokePoints[terrainStrokePoints.Count - 1] - terrainStrokePoints[0];
                data.position = (terrainStrokePoints[0] + terrainStrokePoints[terrainStrokePoints.Count - 1]) * 0.5f;
                data.size = new Vector2(Mathf.Max(0.2f, delta.magnitude), thickness);
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                if (angle > 90f) angle -= 180f;
                if (angle < -90f) angle += 180f;
                if (Mathf.Abs(angle) < 5f) angle = 0f;
                else if (Mathf.Abs(Mathf.Abs(angle) - 90f) < 5f) angle = angle < 0f ? -90f : 90f;
                data.rotation = angle;
                data.pathPoints = System.Array.Empty<Vector2>();
                data.pathThickness = 0f;
                return data;
            }

            data.size = max - min + Vector2.one * thickness;
            data.rotation = 0f;
            data.pathPoints = localPoints;
            data.pathThickness = thickness;
            return data;
        }

        private void AppendJoinedTerrainPath(StageObjectData joined, Vector2 endpoint)
        {
            if (joined == null || joined.pathPoints == null || joined.pathPoints.Length < 2)
            {
                return;
            }

            Vector2 first = PathPointToWorld(joined, joined.pathPoints[0]);
            Vector2 last = PathPointToWorld(joined, joined.pathPoints[joined.pathPoints.Length - 1]);
            if (Vector2.Distance(endpoint, first) <= Vector2.Distance(endpoint, last))
            {
                for (int i = 1; i < joined.pathPoints.Length; i++)
                {
                    terrainStrokePoints.Add(PathPointToWorld(joined, joined.pathPoints[i]));
                }
            }
            else
            {
                for (int i = joined.pathPoints.Length - 2; i >= 0; i--)
                {
                    terrainStrokePoints.Add(PathPointToWorld(joined, joined.pathPoints[i]));
                }
            }
        }

        private void RefreshTerrainStrokePreview()
        {
            if (dragPreviewObject == null)
            {
                return;
            }

            LineRenderer line = dragPreviewObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                return;
            }

            line.positionCount = terrainStrokePoints.Count;
            for (int i = 0; i < terrainStrokePoints.Count; i++)
            {
                line.SetPosition(i, terrainStrokePoints[i]);
            }
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
            data.position = SnapMountedObjectToSurface(data, data.position);
            objects.Add(data);
            CreateEditorObject(data);
            SelectData(data);
            SetStatus(LocalizationManager.Format("stage_editor_status_placed_point", GetObjectLabel(addType), position.x, position.y));
            RefreshText();
            RefreshListPanel();
            GameSfx.Play(SfxId.EditorObjectPlace);
        }

        private static bool IsBlockType(StageObjectType type)
        {
            return StageObjectCatalog.IsRectPlacement(type);
        }

        private Vector2 SnapMountedObjectToSurface(StageObjectData data, Vector2 position)
        {
            if (!snapToGrid || data == null || !IsSurfaceMountedType(data.type))
            {
                return position;
            }

            const float maxRotationDifference = 8f;
            float normalizedRotation = Mathf.Repeat(data.rotation, 360f);
            int mountDirection = Mathf.RoundToInt(normalizedRotation / 90f) % 4;
            float cardinalRotation = mountDirection * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(normalizedRotation, cardinalRotation)) > maxRotationDifference)
            {
                return position;
            }

            // Keep the magnet local. Previously this was at least 3 units, so a
            // button could not be dragged through a recess without jumping back
            // to a distant floor.
            float bestDistance = Mathf.Max(0.22f, gridSize * 0.5f);
            Vector2 bestPosition = position;
            float baseContactOffset = data.size.y
                * (data.type == StageObjectType.Spike ? 0.5f : 0.33f);
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData terrain = objects[i];
                if (terrain == null
                    || terrain == data
                    || !IsFreehandTerrainType(terrain.type)
                    || terrain.pathPoints != null && terrain.pathPoints.Length >= 2
                    || !IsAxisAlignedTerrain(terrain.rotation))
                {
                    continue;
                }

                List<Rect> surfaces = new List<Rect>();
                if (terrain.connectedRects != null && terrain.connectedRects.Length > 0)
                {
                    for (int partIndex = 0; partIndex < terrain.connectedRects.Length; partIndex++)
                    {
                        StageRectPartData part = terrain.connectedRects[partIndex];
                        if (part == null) continue;
                        Vector2 partCenter = terrain.position + part.position;
                        Vector2 half = part.size * 0.5f;
                        surfaces.Add(Rect.MinMaxRect(
                            partCenter.x - half.x,
                            partCenter.y - half.y,
                            partCenter.x + half.x,
                            partCenter.y + half.y));
                    }
                }
                else
                {
                    surfaces.Add(RectFromData(terrain, terrain.position));
                }

                for (int surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex++)
                {
                    Rect surface = surfaces[surfaceIndex];
                    float supportTolerance = Mathf.Min(0.1f, gridSize * 0.2f);
                    float halfSupportedLength = Mathf.Max(0.05f, data.size.x * 0.42f);
                    Vector2 candidate = position;
                    float distance;
                    if (mountDirection == 0 || mountDirection == 2)
                    {
                        if (position.x - halfSupportedLength < surface.xMin - supportTolerance
                            || position.x + halfSupportedLength > surface.xMax + supportTolerance)
                        {
                            continue;
                        }

                        candidate.y = mountDirection == 0
                            ? surface.yMax + baseContactOffset
                            : surface.yMin - baseContactOffset;
                        distance = Mathf.Abs(position.y - candidate.y);
                    }
                    else
                    {
                        if (position.y - halfSupportedLength < surface.yMin - supportTolerance
                            || position.y + halfSupportedLength > surface.yMax + supportTolerance)
                        {
                            continue;
                        }

                        candidate.x = mountDirection == 1
                            ? surface.xMin - baseContactOffset
                            : surface.xMax + baseContactOffset;
                        distance = Mathf.Abs(position.x - candidate.x);
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = candidate;
                    }
                }
            }

            return bestPosition;
        }

        private static bool IsAxisAlignedTerrain(float rotation)
        {
            float horizontal = Mathf.Abs(Mathf.DeltaAngle(0f, rotation));
            float vertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, rotation)) - 90f);
            return horizontal < 2f || vertical < 2f;
        }

        private static bool IsSurfaceMountedType(StageObjectType type)
        {
            return type == StageObjectType.Spike
                || type == StageObjectType.Button
                || type == StageObjectType.WeightButton
                || type == StageObjectType.SimultaneousButton
                || type == StageObjectType.HoldButton
                || type == StageObjectType.PressurePlate
                || type == StageObjectType.Lever
                || type == StageObjectType.ToggleSwitch
                || type == StageObjectType.TimerSwitch
                || type == StageObjectType.RedSwitch
                || type == StageObjectType.BlueSwitch
                || type == StageObjectType.GreenSwitch
                || type == StageObjectType.YellowSwitch;
        }

        private static bool IsFreehandTerrainType(StageObjectType type)
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
            float bestDistance = float.PositiveInfinity;
            int bestSiblingIndex = int.MinValue;
            HashSet<int> visitedMarkers = new HashSet<int>();
            for (int i = 0; i < hits.Length; i++)
            {
                StageEditorObject marker = hits[i].GetComponentInParent<StageEditorObject>();
                if (marker == null
                    || !marker.transform.IsChildOf(editorRoot)
                    || !visitedMarkers.Add(marker.GetInstanceID()))
                {
                    continue;
                }

                float distance = ((Vector2)marker.transform.position - position).sqrMagnitude;
                int siblingIndex = marker.transform.GetSiblingIndex();
                if (distance < bestDistance - 0.0001f
                    || Mathf.Abs(distance - bestDistance) <= 0.0001f && siblingIndex > bestSiblingIndex)
                {
                    bestDistance = distance;
                    bestSiblingIndex = siblingIndex;
                    best = marker.gameObject;
                }
            }

            if (best != null)
            {
                return best;
            }

            bestDistance = 0.4f * 0.4f;
            for (int i = 0; i < editorRoot.childCount; i++)
            {
                Transform child = editorRoot.GetChild(i);
                StageEditorObject marker = child.GetComponent<StageEditorObject>();
                if (marker == null)
                {
                    continue;
                }

                float distance = ((Vector2)child.position - position).sqrMagnitude;
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
            ClearRangeSelection();
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
                    GameSfx.Play(SfxId.EditorObjectSelect);
                    SetStatus(LocalizationManager.T("stage_editor_status_selected"));
                    RefreshText();
                    FocusListPageOn(selectedData);
                    RefreshListPanel();
                    return;
                }
            }
        }

        private void SelectData(StageObjectData data)
        {
            ClearRangeSelection();
            selectedData = data;
            FocusListPageOn(data);
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

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData other = objects[i];
                if (other != selectedData && other != null && other.objectId == selectedData.objectId)
                {
                    selectedData.objectId = StageObjectId.New();
                    break;
                }
            }

            GameObject objectToReplace = selectedObject;
            if (objectToReplace != null)
            {
                DestroyEditorObject(objectToReplace);
                selectedObject = null;
            }
            else
            {
                RemoveEditorObjectById(selectedData.objectId);
            }

            CreateEditorObject(selectedData);
            SelectData(selectedData);
            RefreshBridgeConnectionVisuals();
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

        private Vector2 SnapObjectPosition(StageObjectData data, Vector2 position)
        {
            if (!IsSimpleRectObject(data))
            {
                if (data != null && IsSurfaceMountedType(data.type))
                {
                    float mountedStep = Mathf.Min(Mathf.Max(0.05f, gridSize), 0.1f);
                    return new Vector2(
                        Mathf.Round(position.x / mountedStep) * mountedStep,
                        Mathf.Round(position.y / mountedStep) * mountedStep);
                }
                return Snap(position);
            }

            float step = Mathf.Max(0.05f, gridSize);
            Rect bounds = RectFromData(data, position);
            position.x += Mathf.Round(bounds.xMin / step) * step - bounds.xMin;
            position.y += Mathf.Round(bounds.yMin / step) * step - bounds.yMin;
            return position;
        }

        private static bool IsSimpleRectObject(StageObjectData data)
        {
            return data != null
                && StageObjectCatalog.IsRectPlacement(data.type)
                && (data.pathPoints == null || data.pathPoints.Length < 2)
                && (data.connectedRects == null || data.connectedRects.Length == 0);
        }

        private Vector2 SnapJoinPosition(Vector2 position)
        {
            return SnapJoinPosition(selectedData, position);
        }

        private Vector2 SnapJoinPosition(StageObjectData movingData, Vector2 position)
        {
            if (movingData == null)
            {
                return position;
            }

            if (movingData.type == StageObjectType.StageBoundary)
            {
                return SnapBoundaryPositionToTerrain(movingData, position);
            }

            if (!IsBlockType(movingData.type))
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

                if (other.type == StageObjectType.StageBoundary)
                {
                    SnapBlockToBoundaryInterior(movingData, other, ref position, ref moving);
                    continue;
                }

                Rect target = RectFromData(other, other.position);
                // Joining must only happen when the collider edges are effectively
                // touching. A grid-sized threshold made long horizontal floors jump
                // sideways as soon as their Y ranges lined up, even with a visible gap.
                float joinDistance = Mathf.Clamp(gridSize * 0.15f, 0.035f, 0.08f);
                float overlapJoinDistance = joinDistance;
                if (RangesOverlap(moving.yMin, moving.yMax, target.yMin, target.yMax))
                {
                    if (Mathf.Abs(moving.xMax - target.xMin) <= joinDistance)
                    {
                        position.x += target.xMin - moving.xMax;
                        moving = RectFromData(movingData, position);
                    }
                    else if (Mathf.Abs(moving.xMin - target.xMax) <= joinDistance)
                    {
                        position.x += target.xMax - moving.xMin;
                        moving = RectFromData(movingData, position);
                    }
                    else if (moving.center.x > target.center.x && moving.xMin < target.xMax && target.xMax - moving.xMin <= overlapJoinDistance)
                    {
                        position.x += target.xMax - moving.xMin;
                        moving = RectFromData(movingData, position);
                    }
                    else if (moving.center.x < target.center.x && moving.xMax > target.xMin && moving.xMax - target.xMin <= overlapJoinDistance)
                    {
                        position.x += target.xMin - moving.xMax;
                        moving = RectFromData(movingData, position);
                    }
                }

                if (RangesOverlap(moving.xMin, moving.xMax, target.xMin, target.xMax))
                {
                    if (Mathf.Abs(moving.yMax - target.yMin) <= joinDistance)
                    {
                        position.y += target.yMin - moving.yMax;
                        moving = RectFromData(movingData, position);
                    }
                    else if (Mathf.Abs(moving.yMin - target.yMax) <= joinDistance)
                    {
                        position.y += target.yMax - moving.yMin;
                        moving = RectFromData(movingData, position);
                    }
                    else if (moving.center.y > target.center.y && moving.yMin < target.yMax && target.yMax - moving.yMin <= overlapJoinDistance)
                    {
                        position.y += target.yMax - moving.yMin;
                        moving = RectFromData(movingData, position);
                    }
                    else if (moving.center.y < target.center.y && moving.yMax > target.yMin && moving.yMax - target.yMin <= overlapJoinDistance)
                    {
                        position.y += target.yMin - moving.yMax;
                        moving = RectFromData(movingData, position);
                    }
                }
            }

            if (IsSeparateHorizontalBridge(movingData))
            {
                FitBridgeBetweenBanks(movingData, ref position);
            }

            FitVerticalWallBetweenSurfaces(movingData, ref position);

            return position;
        }

        private Vector2 SnapBoundaryPositionToTerrain(StageObjectData boundary, Vector2 position)
        {
            float thickness = Mathf.Clamp(
                boundary.pathThickness > 0f ? boundary.pathThickness : 0.5f,
                0.35f,
                1.5f);
            float halfWidth = Mathf.Max(4f, boundary.size.x) * 0.5f;
            float halfHeight = Mathf.Max(4f, boundary.size.y) * 0.5f;
            float snapDistance = Mathf.Max(thickness * 1.5f, gridSize * 2f, 0.75f);
            float bestHorizontalDelta = float.PositiveInfinity;
            float bestVerticalDelta = float.PositiveInfinity;

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData terrain = objects[i];
                if (terrain == null
                    || terrain == boundary
                    || !IsFreehandTerrainType(terrain.type))
                {
                    continue;
                }

                Rect target = RectFromData(terrain, terrain.position);
                float innerLeft = position.x - halfWidth + thickness;
                float innerRight = position.x + halfWidth - thickness;
                float lowerEdge = position.y - halfHeight;
                float ceilingBottom = position.y + halfHeight - thickness;

                if (RangesOverlap(target.yMin, target.yMax, lowerEdge, ceilingBottom))
                {
                    float leftDelta = target.xMin - innerLeft;
                    if (Mathf.Abs(leftDelta) <= snapDistance
                        && Mathf.Abs(leftDelta) < Mathf.Abs(bestHorizontalDelta))
                    {
                        bestHorizontalDelta = leftDelta;
                    }

                    float rightDelta = target.xMax - innerRight;
                    if (Mathf.Abs(rightDelta) <= snapDistance
                        && Mathf.Abs(rightDelta) < Mathf.Abs(bestHorizontalDelta))
                    {
                        bestHorizontalDelta = rightDelta;
                    }
                }

                if (RangesOverlap(target.xMin, target.xMax, innerLeft, innerRight))
                {
                    float ceilingDelta = target.yMax - ceilingBottom;
                    if (Mathf.Abs(ceilingDelta) <= snapDistance
                        && Mathf.Abs(ceilingDelta) < Mathf.Abs(bestVerticalDelta))
                    {
                        bestVerticalDelta = ceilingDelta;
                    }
                }
            }

            if (!float.IsPositiveInfinity(bestHorizontalDelta))
            {
                position.x += bestHorizontalDelta;
            }

            if (!float.IsPositiveInfinity(bestVerticalDelta))
            {
                position.y += bestVerticalDelta;
            }

            return position;
        }

        private void SnapBlockToBoundaryInterior(
            StageObjectData movingData,
            StageObjectData boundary,
            ref Vector2 position,
            ref Rect moving)
        {
            float thickness = Mathf.Clamp(
                boundary.pathThickness > 0f ? boundary.pathThickness : 0.5f,
                0.35f,
                1.5f);
            float halfWidth = Mathf.Max(4f, boundary.size.x) * 0.5f;
            float halfHeight = Mathf.Max(4f, boundary.size.y) * 0.5f;
            float innerLeft = boundary.position.x - halfWidth + thickness;
            float innerRight = boundary.position.x + halfWidth - thickness;
            float lowerEdge = boundary.position.y - halfHeight;
            float ceilingBottom = boundary.position.y + halfHeight - thickness;
            float snapDistance = Mathf.Max(thickness * 1.5f, gridSize * 2f, 0.75f);

            bool overlapsWallHeight = RangesOverlap(
                moving.yMin,
                moving.yMax,
                lowerEdge,
                ceilingBottom);
            if (overlapsWallHeight)
            {
                float leftDelta = innerLeft - moving.xMin;
                float rightDelta = innerRight - moving.xMax;
                bool nearLeft = Mathf.Abs(leftDelta) <= snapDistance;
                bool nearRight = Mathf.Abs(rightDelta) <= snapDistance;

                if (nearLeft && (!nearRight || Mathf.Abs(leftDelta) <= Mathf.Abs(rightDelta)))
                {
                    position.x += leftDelta;
                    moving = RectFromData(movingData, position);
                }
                else if (nearRight)
                {
                    position.x += rightDelta;
                    moving = RectFromData(movingData, position);
                }
            }

            bool overlapsInteriorWidth = RangesOverlap(
                moving.xMin,
                moving.xMax,
                innerLeft,
                innerRight);
            float ceilingDelta = ceilingBottom - moving.yMax;
            if (overlapsInteriorWidth && Mathf.Abs(ceilingDelta) <= snapDistance)
            {
                position.y += ceilingDelta;
                moving = RectFromData(movingData, position);
            }
        }

        private bool IsSeparateHorizontalBridge(StageObjectData data)
        {
            return data != null
                && data.keepSeparate
                && data.type == StageObjectType.Platform
                && data.size.x > data.size.y * 1.5f
                && (data.pathPoints == null || data.pathPoints.Length < 2)
                && Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation)) < 2f;
        }

        private bool FitVerticalWallBetweenSurfaces(StageObjectData wall, ref Vector2 position)
        {
            if (!snapToGrid
                || wall == null
                || !IsFreehandTerrainType(wall.type)
                || wall.pathPoints != null && wall.pathPoints.Length >= 2
                || wall.connectedRects != null && wall.connectedRects.Length > 0)
            {
                return false;
            }

            Rect wallRect = RectFromData(wall, position);
            if (wallRect.height <= wallRect.width * 1.35f)
            {
                return false;
            }

            // Auto-fit is only a final, close-range assist. One attached end must
            // never pull the opposite end across a large empty space.
            float fitDistance = Mathf.Max(0.35f, gridSize * 1.5f);
            float bottomSurface = float.NegativeInfinity;
            float topSurface = float.PositiveInfinity;
            float bestBottomGap = float.PositiveInfinity;
            float bestTopGap = float.PositiveInfinity;

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData candidate = objects[i];
                if (candidate == null || candidate == wall)
                {
                    continue;
                }

                if (candidate.type == StageObjectType.StageBoundary)
                {
                    float thickness = Mathf.Clamp(
                        candidate.pathThickness > 0f ? candidate.pathThickness : 0.5f,
                        0.35f,
                        1.5f);
                    float innerLeft = candidate.position.x - candidate.size.x * 0.5f + thickness;
                    float innerRight = candidate.position.x + candidate.size.x * 0.5f - thickness;
                    if (RangesOverlap(wallRect.xMin, wallRect.xMax, innerLeft, innerRight))
                    {
                        float surface = candidate.position.y + candidate.size.y * 0.5f - thickness;
                        float gap = Mathf.Abs(surface - wallRect.yMax);
                        if (surface >= wallRect.center.y && gap < bestTopGap)
                        {
                            bestTopGap = gap;
                            topSurface = surface;
                        }
                    }
                    continue;
                }

                if (!IsFreehandTerrainType(candidate.type)
                    || candidate.pathPoints != null && candidate.pathPoints.Length >= 2)
                {
                    continue;
                }

                List<StageRectPartData> parts = new List<StageRectPartData>();
                AppendWorldRectParts(parts, candidate);
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    StageRectPartData part = parts[partIndex];
                    if (part == null)
                    {
                        continue;
                    }

                    Vector2 half = part.size * 0.5f;
                    Rect surfaceRect = Rect.MinMaxRect(
                        part.position.x - half.x,
                        part.position.y - half.y,
                        part.position.x + half.x,
                        part.position.y + half.y);
                    if (surfaceRect.width <= surfaceRect.height * 1.2f
                        || !RangesOverlap(wallRect.xMin, wallRect.xMax, surfaceRect.xMin, surfaceRect.xMax))
                    {
                        continue;
                    }

                    if (surfaceRect.yMax <= wallRect.center.y)
                    {
                        float gap = Mathf.Abs(surfaceRect.yMax - wallRect.yMin);
                        if (gap < bestBottomGap)
                        {
                            bestBottomGap = gap;
                            bottomSurface = surfaceRect.yMax;
                        }
                    }

                    if (surfaceRect.yMin >= wallRect.center.y)
                    {
                        float gap = Mathf.Abs(surfaceRect.yMin - wallRect.yMax);
                        if (gap < bestTopGap)
                        {
                            bestTopGap = gap;
                            topSurface = surfaceRect.yMin;
                        }
                    }
                }
            }

            if (float.IsNegativeInfinity(bottomSurface)
                || float.IsPositiveInfinity(topSurface)
                || topSurface - bottomSurface < 0.4f
                || bestBottomGap > fitDistance
                || bestTopGap > fitDistance)
            {
                return false;
            }

            float fittedHeight = topSurface - bottomSurface;
            float angleToVertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, wall.rotation)) - 90f);
            if (angleToVertical < 2f)
            {
                wall.size.x = fittedHeight;
            }
            else
            {
                wall.size.y = fittedHeight;
            }

            position.y = (bottomSurface + topSurface) * 0.5f;
            return true;
        }

        private bool FitBridgeBetweenBanks(StageObjectData bridge, ref Vector2 position)
        {
            Rect bridgeRect = RectFromData(bridge, position);
            float fitDistance = Mathf.Max(0.5f, gridSize * 2f);
            float leftEdge = 0f;
            float rightEdge = 0f;
            float bestLeftGap = float.MaxValue;
            float bestRightGap = float.MaxValue;
            bool hasLeft = false;
            bool hasRight = false;

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData candidate = objects[i];
                if (candidate == null
                    || candidate == bridge
                    || !IsFreehandTerrainType(candidate.type)
                    || candidate.pathPoints != null && candidate.pathPoints.Length >= 2
                    || Mathf.Abs(Mathf.DeltaAngle(0f, candidate.rotation)) > 2f)
                {
                    continue;
                }

                List<StageRectPartData> parts = new List<StageRectPartData>();
                AppendWorldRectParts(parts, candidate);
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    StageRectPartData part = parts[partIndex];
                    Vector2 half = part.size * 0.5f;
                    Rect partRect = Rect.MinMaxRect(
                        part.position.x - half.x,
                        part.position.y - half.y,
                        part.position.x + half.x,
                        part.position.y + half.y);
                    if (!RangesOverlap(bridgeRect.yMin, bridgeRect.yMax, partRect.yMin, partRect.yMax))
                    {
                        continue;
                    }

                    float leftGap = Mathf.Abs(partRect.xMax - bridgeRect.xMin);
                    if (partRect.center.x < bridgeRect.center.x && leftGap <= fitDistance && leftGap < bestLeftGap)
                    {
                        bestLeftGap = leftGap;
                        leftEdge = partRect.xMax;
                        hasLeft = true;
                    }

                    float rightGap = Mathf.Abs(partRect.xMin - bridgeRect.xMax);
                    if (partRect.center.x > bridgeRect.center.x && rightGap <= fitDistance && rightGap < bestRightGap)
                    {
                        bestRightGap = rightGap;
                        rightEdge = partRect.xMin;
                        hasRight = true;
                    }
                }
            }

            if (!hasLeft || !hasRight || rightEdge - leftEdge < 0.2f)
            {
                return false;
            }

            bridge.size = new Vector2(rightEdge - leftEdge, bridge.size.y);
            position.x = (leftEdge + rightEdge) * 0.5f;
            return true;
        }

        private static Rect RectFromData(StageObjectData data, Vector2 position)
        {
            Vector2 sourceHalf = data.size * 0.5f;
            float radians = data.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(radians));
            float sin = Mathf.Abs(Mathf.Sin(radians));
            Vector2 half = new Vector2(
                sourceHalf.x * cos + sourceHalf.y * sin,
                sourceHalf.x * sin + sourceHalf.y * cos);
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static bool RangesOverlap(float aMin, float aMax, float bMin, float bMax)
        {
            return aMax >= bMin && bMax >= aMin;
        }

        private bool IsPointerOverEditorUi()
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                || (uiBlocker != null && RectTransformUtility.RectangleContainsScreenPoint(uiBlocker, Input.mousePosition));
        }

        private void UpdateSelectionBox()
        {
            if (rangeSelectedObjects.Count > 1)
            {
                SetSelectionBox(false);
                UpdateRangeSelectionBoxes();
                return;
            }

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

            float localX = size.x * 0.5f + 0.08f;
            float localY = size.y * 0.5f + 0.08f;
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
    }
}
