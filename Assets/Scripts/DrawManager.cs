using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class DrawManager : MonoBehaviour
    {
        public enum BodyPart
        {
            Head,
            Torso,
            LeftArm,
            RightArm,
            LeftLeg,
            RightLeg,
            LeftFrontLeg,
            RightFrontLeg,
            LeftBackLeg,
            RightBackLeg,
            Tail,
            LeftWing,
            RightWing,
            TailFeather,
            SlimeBody
        }

        public enum Species
        {
            Human,
            Cat,
            Bird,
            Snake,
            Slime
        }

        private enum ToolMode
        {
            Pen,
            Eraser
        }

        private sealed class PartDrawing
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public readonly List<GameObject> LineSegments = new List<GameObject>();
            public readonly List<GameObject> PreviewSegments = new List<GameObject>();
            public float UsedInk;
        }

        public sealed class DrawingState
        {
            public Species Species;
            public BodyPart Part;
            public readonly Dictionary<Species, Dictionary<BodyPart, List<Vector2>>> Points =
                new Dictionary<Species, Dictionary<BodyPart, List<Vector2>>>();
        }

        [SerializeField] private StageManager stageManager;
        [SerializeField] private BodyBuilder bodyBuilder;
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private GameObject drawPanel;
        [SerializeField] private RectTransform drawArea;
        [SerializeField] private RectTransform lineRoot;
        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private Text inkText;
        [SerializeField] private Image inkGaugeFill;
        [SerializeField] private Text partText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text abilityText;
        [SerializeField] private float maxInk = 350f;
        [SerializeField] private float pixelsPerInk = 5f;
        [SerializeField] private float minPointDistance = 8f;
        [SerializeField] private float lineWidth = 6f;
        [SerializeField] private float eraserRadius = 18f;
        [SerializeField] private float previewScale = 0.7f;
        [SerializeField] private float previewLineWidth = 5f;
        [SerializeField] private float startPointSnapRadius = 42f;
        [SerializeField] private Vector2 assembledMaxSize = new Vector2(190f, 300f);

        private readonly Dictionary<BodyPart, PartDrawing> drawings = new Dictionary<BodyPart, PartDrawing>();
        private readonly Dictionary<Species, Dictionary<BodyPart, PartDrawing>> speciesDrawings = new Dictionary<Species, Dictionary<BodyPart, PartDrawing>>();
        private static readonly Vector2 StrokeBreak = new Vector2(float.NaN, float.NaN);
        private GameObject connectionMarker;
        private bool active;
        private bool drawing;
        private bool initialized;
        private Species currentSpecies = Species.Human;
        private BodyPart currentPart = BodyPart.Torso;
        private bool previewDirty;
        private bool hasEditSnapshot;
        private Species snapshotSpecies;
        private BodyPart snapshotPart;
        private Dictionary<Species, Dictionary<BodyPart, List<Vector2>>> editSnapshot;
        private ToolMode toolMode = ToolMode.Pen;
        private readonly float[] brushSizes = { 3f, 5f, 6f, 8f, 10f };
        private int brushSizeIndex = 2;

        public event System.Action<BodyPart> CurrentPartChanged;
        public event System.Action<Species> CurrentSpeciesChanged;
        public float UsedInk => GetTotalInk();
        public BodyPart CurrentPart => currentPart;
        public Species CurrentSpecies => currentSpecies;

        private void Awake()
        {
            maxInk = 350f;
            EnsureInitialized();

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (bodyBuilder == null)
            {
                bodyBuilder = FindObjectOfType<BodyBuilder>();
            }

            if (abilityController == null)
            {
                abilityController = FindObjectOfType<PlayerAbilityController>();
            }

            SetActive(false);
            RefreshInkText();
            SetPartSegmentVisibility();
        }

        private void Start()
        {
            EnsureInitialized();
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            ApplyDrawing();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                ClearDrawing();
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmDrawing();
            }

            HandleMouseInput();
        }

        public void SetActive(bool value)
        {
            active = value;
            drawing = false;

            if (drawPanel != null)
            {
                drawPanel.SetActive(value);
            }

            RefreshInkText();
            if (value)
            {
                CaptureEditSnapshot();
                RefreshConnectionMessage();
                SetPartSegmentVisibility();
                UpdateConnectionMarker();
                CurrentPartChanged?.Invoke(currentPart);
            }
            else
            {
                UpdateConnectionMarker();
                SetMessage(string.Empty);
            }
        }

        public void CancelEditing()
        {
            FinishStroke();
            RestoreEditSnapshot();
            ApplyDrawing();
        }

        public void ClearDrawing()
        {
            FinishStroke();
            PartDrawing current = drawings[currentPart];
            current.Points.Clear();
            current.UsedInk = 0f;
            drawing = false;

            for (int i = 0; i < current.LineSegments.Count; i++)
            {
                DestroyObject(current.LineSegments[i]);
            }

            for (int i = 0; i < current.PreviewSegments.Count; i++)
            {
                DestroyObject(current.PreviewSegments[i]);
            }

            current.LineSegments.Clear();
            current.PreviewSegments.Clear();
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
        }

        private void CaptureEditSnapshot()
        {
            EnsureInitialized();
            FinishStroke();

            snapshotSpecies = currentSpecies;
            snapshotPart = currentPart;
            editSnapshot = new Dictionary<Species, Dictionary<BodyPart, List<Vector2>>>();

            foreach (KeyValuePair<Species, Dictionary<BodyPart, PartDrawing>> speciesPair in speciesDrawings)
            {
                Dictionary<BodyPart, List<Vector2>> partSnapshot = new Dictionary<BodyPart, List<Vector2>>();
                foreach (KeyValuePair<BodyPart, PartDrawing> partPair in speciesPair.Value)
                {
                    partSnapshot[partPair.Key] = new List<Vector2>(partPair.Value.Points);
                }

                editSnapshot[speciesPair.Key] = partSnapshot;
            }

            hasEditSnapshot = true;
        }

        private void RestoreEditSnapshot()
        {
            if (!hasEditSnapshot || editSnapshot == null)
            {
                return;
            }

            foreach (KeyValuePair<Species, Dictionary<BodyPart, List<Vector2>>> speciesPair in editSnapshot)
            {
                if (!speciesDrawings.TryGetValue(speciesPair.Key, out Dictionary<BodyPart, PartDrawing> targetSpecies))
                {
                    continue;
                }

                foreach (KeyValuePair<BodyPart, List<Vector2>> partPair in speciesPair.Value)
                {
                    if (!targetSpecies.TryGetValue(partPair.Key, out PartDrawing targetPart))
                    {
                        continue;
                    }

                    targetPart.Points.Clear();
                    targetPart.Points.AddRange(partPair.Value);
                    targetPart.UsedInk = CalculateInk(targetPart.Points);
                }
            }

            currentSpecies = snapshotSpecies;
            UseSpeciesDrawings(currentSpecies);
            currentPart = IsPartActive(snapshotPart) ? snapshotPart : GetCurrentParts()[0];
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            CurrentSpeciesChanged?.Invoke(currentSpecies);
            CurrentPartChanged?.Invoke(currentPart);
            hasEditSnapshot = false;
        }

        public void UndoLastStroke()
        {
            FinishStroke();
            PartDrawing current = drawings[currentPart];
            if (current.Points.Count == 0)
            {
                return;
            }

            int removeStart = current.Points.Count - 1;
            while (removeStart > 0 && !IsBreakPoint(current.Points[removeStart - 1]))
            {
                removeStart--;
            }

            current.Points.RemoveRange(removeStart, current.Points.Count - removeStart);
            if (current.Points.Count > 0 && IsBreakPoint(current.Points[current.Points.Count - 1]))
            {
                current.Points.RemoveAt(current.Points.Count - 1);
            }

            current.UsedInk = CalculateInk(current.Points);
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
        }

        public void SetBrushSize(int index)
        {
            brushSizeIndex = Mathf.Clamp(index, 0, brushSizes.Length - 1);
            lineWidth = brushSizes[brushSizeIndex];
        }

        public void SetBrushSizePixels(float pixels)
        {
            lineWidth = Mathf.Clamp(pixels, 1f, 30f);
        }

        public void SetToolMode(int mode)
        {
            FinishStroke();
            toolMode = mode == 1 ? ToolMode.Eraser : ToolMode.Pen;
        }

        public void SetCurrentPart(BodyPart part)
        {
            if (!IsPartActive(part))
            {
                return;
            }

            if (currentPart == part)
            {
                return;
            }

            FinishStroke();
            currentPart = part;
            RefreshInkText();
            RefreshConnectionMessage();
            SetPartSegmentVisibility();
            UpdateConnectionMarker();
            CurrentPartChanged?.Invoke(currentPart);
        }

        public void SetSpecies(Species species)
        {
            if (currentSpecies == species)
            {
                return;
            }

            EnsureInitialized();
            FinishStroke();
            currentSpecies = species;
            UseSpeciesDrawings(species);
            SnapSpeciesConnectionStarts();
            currentPart = GetCurrentParts()[0];
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            ApplyDrawing();
            CurrentSpeciesChanged?.Invoke(currentSpecies);
            CurrentPartChanged?.Invoke(currentPart);
        }

        private void SnapSpeciesConnectionStarts()
        {
            if (currentSpecies == Species.Slime)
            {
                return;
            }

            foreach (BodyPart part in GetCurrentParts())
            {
                if (part == BodyPart.Torso)
                {
                    continue;
                }

                PartDrawing drawing = drawings[part];
                for (int i = 0; i < drawing.Points.Count; i++)
                {
                    if (IsBreakPoint(drawing.Points[i]))
                    {
                        continue;
                    }

                    drawing.Points[i] = GetRequiredLocalStartPoint(part);
                    NormalizeDefaultCatLeg(part, drawing.Points);
                    drawing.UsedInk = CalculateInk(drawing.Points);
                    break;
                }
            }
        }

        private void NormalizeDefaultCatLeg(BodyPart part, List<Vector2> points)
        {
            if (currentSpecies != Species.Cat || !IsCatLeg(part))
            {
                return;
            }

            if (CountDrawablePoints(points) != 2)
            {
                return;
            }

            int first = -1;
            int second = -1;
            for (int i = 0; i < points.Count; i++)
            {
                if (IsBreakPoint(points[i]))
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }
                else
                {
                    second = i;
                    break;
                }
            }

            if (first < 0 || second < 0)
            {
                return;
            }

            float length = Mathf.Max(80f, Mathf.Abs(points[first].y - points[second].y));
            points[second] = new Vector2(points[first].x, points[first].y - length);
        }

        private static bool IsCatLeg(BodyPart part)
        {
            return part == BodyPart.LeftFrontLeg
                || part == BodyPart.RightFrontLeg
                || part == BodyPart.LeftBackLeg
                || part == BodyPart.RightBackLeg;
        }

        public bool IsPartActive(BodyPart part)
        {
            BodyPart[] parts = GetCurrentParts();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == part)
                {
                    return true;
                }
            }

            return false;
        }

        public BodyPart[] GetCurrentParts()
        {
            return GetPartsForSpecies(currentSpecies);
        }

        public void ConfirmDrawing()
        {
            if (TryApplyDrawing())
            {
                hasEditSnapshot = false;
                editSnapshot = null;
                stageManager?.ExitDrawingMode();
            }
        }

        public bool TryApplyDrawing()
        {
            if (!ValidateConnections(out string errorMessage))
            {
                SetMessage(errorMessage);
                return false;
            }

            SetMessage(string.Empty);
            ApplyDrawing();
            return true;
        }

        public void ApplyDrawingWithoutValidation()
        {
            SetMessage(string.Empty);
            ApplyDrawing();
        }

        public void InitializeForScene(BodyBuilder builder, PlayerAbilityController abilities, StageManager manager)
        {
            EnsureInitialized();
            bodyBuilder = builder;
            abilityController = abilities;
            stageManager = manager;
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            ApplyDrawing();
        }

        public void SetBuildTarget(BodyBuilder builder, PlayerAbilityController abilities)
        {
            bodyBuilder = builder;
            abilityController = abilities;
        }

        public DrawingState CreateState()
        {
            EnsureInitialized();
            FinishStroke();

            DrawingState state = new DrawingState
            {
                Species = currentSpecies,
                Part = currentPart
            };

            foreach (KeyValuePair<Species, Dictionary<BodyPart, PartDrawing>> speciesPair in speciesDrawings)
            {
                Dictionary<BodyPart, List<Vector2>> partPoints = new Dictionary<BodyPart, List<Vector2>>();
                foreach (KeyValuePair<BodyPart, PartDrawing> partPair in speciesPair.Value)
                {
                    partPoints[partPair.Key] = new List<Vector2>(partPair.Value.Points);
                }

                state.Points[speciesPair.Key] = partPoints;
            }

            return state;
        }

        public void LoadState(DrawingState state, bool applyDrawing)
        {
            if (state == null)
            {
                return;
            }

            EnsureInitialized();
            FinishStroke();

            foreach (KeyValuePair<Species, Dictionary<BodyPart, List<Vector2>>> speciesPair in state.Points)
            {
                if (!speciesDrawings.TryGetValue(speciesPair.Key, out Dictionary<BodyPart, PartDrawing> targetSpecies))
                {
                    continue;
                }

                foreach (KeyValuePair<BodyPart, List<Vector2>> partPair in speciesPair.Value)
                {
                    if (!targetSpecies.TryGetValue(partPair.Key, out PartDrawing targetPart))
                    {
                        continue;
                    }

                    targetPart.Points.Clear();
                    targetPart.Points.AddRange(partPair.Value);
                    targetPart.UsedInk = CalculateInk(targetPart.Points);
                }
            }

            currentSpecies = state.Species;
            UseSpeciesDrawings(currentSpecies);
            currentPart = IsPartActive(state.Part) ? state.Part : GetCurrentParts()[0];
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            CurrentSpeciesChanged?.Invoke(currentSpecies);
            CurrentPartChanged?.Invoke(currentPart);

            if (applyDrawing)
            {
                ApplyDrawing();
            }
        }

        private void ApplyDrawing()
        {
            bodyBuilder?.BuildFromDrawing(this);
            abilityController?.ApplyFromDrawing(this);
        }

        public IReadOnlyList<Vector2> GetPoints(BodyPart part)
        {
            EnsureInitialized();
            return drawings[part].Points;
        }

        public IReadOnlyList<Vector2> GetBodyPoints(BodyPart part)
        {
            EnsureInitialized();
            return GetFittedAssembledPoints(part);
        }

        private List<Vector2> GetFittedAssembledPoints(BodyPart part)
        {
            List<Vector2> result = new List<Vector2>();
            IReadOnlyList<Vector2> source = drawings[part].Points;
            GetAssemblyFit(out float scale, out Vector2 offset);

            for (int i = 0; i < source.Count; i++)
            {
                result.Add(IsBreakPoint(source[i]) ? StrokeBreak : GetRawAssembledPoint(part, source[i]) * scale + offset);
            }

            return result;
        }

        public float GetInk(BodyPart part)
        {
            EnsureInitialized();
            return drawings[part].UsedInk;
        }

        public static BodyPart[] GetAllParts()
        {
            return new[]
            {
                BodyPart.Head,
                BodyPart.Torso,
                BodyPart.LeftArm,
                BodyPart.RightArm,
                BodyPart.LeftLeg,
                BodyPart.RightLeg,
                BodyPart.LeftFrontLeg,
                BodyPart.RightFrontLeg,
                BodyPart.LeftBackLeg,
                BodyPart.RightBackLeg,
                BodyPart.Tail,
                BodyPart.LeftWing,
                BodyPart.RightWing,
                BodyPart.TailFeather,
                BodyPart.SlimeBody
            };
        }

        public static BodyPart[] GetPartsForSpecies(Species species)
        {
            switch (species)
            {
                case Species.Cat:
                    return new[]
                    {
                        BodyPart.Head,
                        BodyPart.Torso,
                        BodyPart.LeftFrontLeg,
                        BodyPart.RightFrontLeg,
                        BodyPart.LeftBackLeg,
                        BodyPart.RightBackLeg,
                        BodyPart.Tail
                    };
                case Species.Bird:
                    return new[]
                    {
                        BodyPart.Head,
                        BodyPart.Torso,
                        BodyPart.LeftWing,
                        BodyPart.RightWing
                    };
                case Species.Snake:
                    return new[]
                    {
                        BodyPart.Head,
                        BodyPart.Torso
                    };
                case Species.Slime:
                    return new[]
                    {
                        BodyPart.SlimeBody
                    };
                default:
                    return new[]
                    {
                        BodyPart.Head,
                        BodyPart.Torso,
                        BodyPart.LeftArm,
                        BodyPart.RightArm,
                        BodyPart.LeftLeg,
                        BodyPart.RightLeg
                    };
            }
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (toolMode == ToolMode.Eraser)
                {
                    if (TryGetDrawPoint(out Vector2 erasePoint))
                    {
                        EraseAt(erasePoint);
                    }

                    drawing = false;
                    return;
                }

                drawing = TryGetDrawPoint(out Vector2 point);
                PartDrawing current = drawings[currentPart];
                if (drawing)
                {
                    if (!CanStartStroke(point, out Vector2 startPoint))
                    {
                        drawing = false;
                        RefreshConnectionMessage();
                        return;
                    }

                    if (current.Points.Count > 0)
                    {
                        current.Points.Add(StrokeBreak);
                    }

                    current.Points.Add(startPoint);
                    RefreshInkText();
                    RefreshConnectionMessage();
                }
            }

            if (Input.GetMouseButton(0) && toolMode == ToolMode.Eraser)
            {
                if (TryGetDrawPoint(out Vector2 erasePoint))
                {
                    EraseAt(erasePoint);
                }

                return;
            }

            if (Input.GetMouseButton(0) && drawing && TryGetDrawPoint(out Vector2 currentPoint))
            {
                TryAddPoint(currentPoint);
            }
            else if (Input.GetMouseButton(0) && drawing)
            {
                FinishStroke();
            }

            if (Input.GetMouseButtonUp(0))
            {
                FinishStroke();
            }
        }

        private void EraseAt(Vector2 point)
        {
            PartDrawing current = drawings[currentPart];
            if (!TryFindStrokeAtPoint(current.Points, point, eraserRadius, out int start, out int end))
            {
                return;
            }

            current.Points.RemoveRange(start, end - start + 1);

            if (start < current.Points.Count && IsBreakPoint(current.Points[start]))
            {
                current.Points.RemoveAt(start);
            }

            if (start > 0 && start - 1 < current.Points.Count && IsBreakPoint(current.Points[start - 1]))
            {
                current.Points.RemoveAt(start - 1);
            }

            current.UsedInk = CalculateInk(current.Points);
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
        }

        private static bool TryFindStrokeAtPoint(IReadOnlyList<Vector2> points, Vector2 point, float radius, out int start, out int end)
        {
            start = -1;
            end = -1;
            int strokeStart = -1;
            Vector2 previous = Vector2.zero;
            bool hasPrevious = false;

            for (int i = 0; i <= points.Count; i++)
            {
                bool atEnd = i == points.Count;
                bool breakPoint = !atEnd && IsBreakPoint(points[i]);
                if (atEnd || breakPoint)
                {
                    if (start >= 0)
                    {
                        end = i - 1;
                        return true;
                    }

                    strokeStart = -1;
                    hasPrevious = false;
                    continue;
                }

                if (strokeStart < 0)
                {
                    strokeStart = i;
                }

                if (hasPrevious && DistancePointToSegment(point, previous, points[i]) <= radius)
                {
                    start = strokeStart;
                }

                previous = points[i];
                hasPrevious = true;
            }

            return false;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 nearest = start + segment * t;
            return Vector2.Distance(point, nearest);
        }

        private void FinishStroke()
        {
            if (previewDirty)
            {
                RebuildPreviewVisuals();
                previewDirty = false;
            }

            drawing = false;
        }

        private bool TryGetDrawPoint(out Vector2 point)
        {
            point = Vector2.zero;

            if (drawArea == null)
            {
                return false;
            }

            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                drawArea,
                Input.mousePosition,
                null,
                out point);

            if (!inside || !drawArea.rect.Contains(point))
            {
                return false;
            }

            return true;
        }

        private void TryAddPoint(Vector2 point)
        {
            PartDrawing current = drawings[currentPart];

            if (current.Points.Count == 0)
            {
                current.Points.Add(point);
                return;
            }

            Vector2 previous = current.Points[current.Points.Count - 1];
            if (IsBreakPoint(previous))
            {
                current.Points.Add(point);
                return;
            }

            float pixelLength = Vector2.Distance(previous, point);
            float effectiveMinPointDistance = Mathf.Max(8f, minPointDistance);
            if (pixelLength < effectiveMinPointDistance)
            {
                return;
            }

            float inkCost = pixelLength / pixelsPerInk;
            float remainingInk = maxInk - GetTotalInk();
            if (remainingInk <= 0f)
            {
                FinishStroke();
                RefreshInkText();
                return;
            }

            if (inkCost > remainingInk)
            {
                float allowedPixels = remainingInk * pixelsPerInk;
                point = previous + (point - previous).normalized * allowedPixels;
                inkCost = remainingInk;
            }

            current.Points.Add(point);
            current.UsedInk += inkCost;
            CreateSegment(current, currentPart, previous, point);
            previewDirty = true;
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();

            if (inkCost >= remainingInk)
            {
                FinishStroke();
            }
        }

        private void ClearCurrentPart()
        {
            FinishStroke();
            PartDrawing current = drawings[currentPart];
            current.Points.Clear();
            current.UsedInk = 0f;
            ClearVisuals(current);
        }

        private void CreateSegment(PartDrawing partDrawing, BodyPart part, Vector2 start, Vector2 end)
        {
            if (lineRoot == null)
            {
                return;
            }

            GameObject segment = CreateUiSegment("InkSegment", lineRoot, start, end, lineWidth, GetPartColor(part), 1f);
            segment.SetActive(part == currentPart);
            partDrawing.LineSegments.Add(segment);

            if (previewRoot != null)
            {
                GameObject previewSegment = CreateUiSegment("PreviewSegment", previewRoot, ToPreviewPoint(part, start), ToPreviewPoint(part, end), previewLineWidth, GetPartColor(part), 1f);
                partDrawing.PreviewSegments.Add(previewSegment);
            }
        }

        private void RefreshInkText()
        {
            float totalInk = GetTotalInk();

            if (inkText != null)
            {
                inkText.text = $"{totalInk:0} / {maxInk:0}";
            }

            if (inkGaugeFill != null)
            {
                inkGaugeFill.fillAmount = maxInk <= 0f ? 0f : Mathf.Clamp01(totalInk / maxInk);
            }

            if (partText != null)
            {
                partText.text = $"{LocalizationManager.T("part")}: {GetPartLabel(currentPart)}";
            }

            if (abilityText != null)
            {
                PlayerAbilityController.AbilityProfile profile = PlayerAbilityController.CalculateProfile(this);
                abilityText.text = PlayerAbilityController.GetProfileSummary(profile);
            }
        }

        private bool ValidateConnections(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (currentSpecies != Species.Slime
                && (!TryGetBounds(BodyPart.Torso, out _) || CountDrawablePoints(drawings[BodyPart.Torso].Points) < 2))
            {
                errorMessage = LocalizationManager.T("msg_torso_needed");
                return false;
            }

            foreach (BodyPart part in GetCurrentParts())
            {
                IReadOnlyList<Vector2> rawPartPoints = drawings[part].Points;
                if (CountDrawablePoints(rawPartPoints) < 2)
                {
                    errorMessage = LocalizationManager.Format("msg_part_required", GetPartLabel(part));
                    return false;
                }

                if (part == BodyPart.Torso || currentSpecies == Species.Slime)
                {
                    continue;
                }

                if (!TryGetFirstDrawablePoint(rawPartPoints, out Vector2 startPoint))
                {
                    continue;
                }

                if (!IsCloseToRequiredStart(part, startPoint))
                {
                    errorMessage = LocalizationManager.Format("msg_part_must_start", GetPartLabel(part));
                    return false;
                }

                if (!TryGetPartConnectionPoint(part, rawPartPoints, out _))
                {
                    errorMessage = LocalizationManager.Format("msg_part_must_start", GetPartLabel(part));
                    return false;
                }
            }

            return true;
        }

        private void RefreshConnectionMessage()
        {
            if (!active)
            {
                return;
            }

            if (currentPart == BodyPart.Torso || currentSpecies == Species.Slime)
            {
                SetMessage(LocalizationManager.T("msg_torso_base"));
                return;
            }

            IReadOnlyList<Vector2> current = drawings[currentPart].Points;
            if (!TryGetFirstDrawablePoint(current, out Vector2 currentStart))
            {
                SetMessage(LocalizationManager.Format("msg_start_near", GetPartLabel(currentPart)));
                return;
            }

            if (currentSpecies != Species.Slime
                && (!TryGetBounds(BodyPart.Torso, out _) || CountDrawablePoints(drawings[BodyPart.Torso].Points) < 2))
            {
                SetMessage(LocalizationManager.T("msg_draw_torso_first"));
                return;
            }

            bool connected = IsCloseToRequiredStart(currentPart, currentStart);
            SetMessage(connected
                ? LocalizationManager.Format("msg_connected", GetPartLabel(currentPart))
                : LocalizationManager.Format("msg_not_connected", GetPartLabel(currentPart)));
        }

        private void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        private void SetPartSegmentVisibility()
        {
            foreach (KeyValuePair<BodyPart, PartDrawing> pair in drawings)
            {
                bool visible = pair.Key == currentPart;
                for (int i = 0; i < pair.Value.LineSegments.Count; i++)
                {
                    if (pair.Value.LineSegments[i] != null)
                    {
                        pair.Value.LineSegments[i].SetActive(visible);
                    }
                }
            }
        }

        private void UpdateConnectionMarker()
        {
            if (drawArea == null)
            {
                return;
            }

            EnsureConnectionMarker();
            bool show = active && currentSpecies != Species.Slime && currentPart != BodyPart.Torso;
            connectionMarker.SetActive(show);

            if (!show)
            {
                return;
            }

            RectTransform rect = connectionMarker.GetComponent<RectTransform>();
            rect.anchoredPosition = GetRequiredLocalStartPoint(currentPart);

            Image image = connectionMarker.GetComponent<Image>();
            image.color = GetPartColor(currentPart);
        }

        private void EnsureConnectionMarker()
        {
            if (connectionMarker != null)
            {
                return;
            }

            connectionMarker = new GameObject("ConnectionStartMarker");
            connectionMarker.transform.SetParent(drawArea, false);

            Image image = connectionMarker.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static Vector2[] Segment(Vector2 start, Vector2 end)
        {
            return new[] { start, end };
        }

        public static bool IsBreakPoint(Vector2 point)
        {
            return float.IsNaN(point.x) || float.IsNaN(point.y);
        }

        private static bool TryGetFirstDrawablePoint(IReadOnlyList<Vector2> points, out Vector2 point)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsBreakPoint(points[i]))
                {
                    point = points[i];
                    return true;
                }
            }

            point = Vector2.zero;
            return false;
        }

        private static int CountDrawablePoints(IReadOnlyList<Vector2> points)
        {
            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsBreakPoint(points[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetBounds(BodyPart part, out Rect bounds)
        {
            return TryGetBounds(drawings[part].Points, out bounds);
        }

        private static bool TryGetBounds(IReadOnlyList<Vector2> points, out Rect bounds)
        {
            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                if (IsBreakPoint(point))
                {
                    continue;
                }

                if (!found)
                {
                    minX = maxX = point.x;
                    minY = maxY = point.y;
                    found = true;
                }
                else
                {
                    minX = Mathf.Min(minX, point.x);
                    maxX = Mathf.Max(maxX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }
            }

            bounds = found ? Rect.MinMaxRect(minX, minY, maxX, maxY) : new Rect();
            return found;
        }

        private bool TryGetTorsoConnectionPoint(BodyPart part, out Vector2 point)
        {
            point = Vector2.zero;
            if (!TryGetBounds(BodyPart.Torso, out Rect torso))
            {
                return false;
            }

            if (currentSpecies == Species.Cat || currentSpecies == Species.Snake)
            {
                torso = ExpandRect(torso, 180f, 70f);
            }
            else
            {
                torso = ExpandRect(torso, 80f, 130f);
            }

            float centerX = (torso.xMin + torso.xMax) * 0.5f;
            float centerY = (torso.yMin + torso.yMax) * 0.5f;
            float lowerLeftX = Mathf.Lerp(torso.xMin, torso.xMax, 0.25f);
            float lowerRightX = Mathf.Lerp(torso.xMin, torso.xMax, 0.75f);

            if (currentSpecies == Species.Cat)
            {
                float frontX = Mathf.Lerp(torso.xMin, torso.xMax, 0.72f);
                float backX = Mathf.Lerp(torso.xMin, torso.xMax, 0.28f);
                switch (part)
                {
                    case BodyPart.Head:
                        point = new Vector2(torso.xMax, centerY);
                        return true;
                    case BodyPart.Tail:
                        point = new Vector2(torso.xMin, centerY);
                        return true;
                    case BodyPart.LeftFrontLeg:
                        point = new Vector2(frontX - 14f, torso.yMin);
                        return true;
                    case BodyPart.RightFrontLeg:
                        point = new Vector2(frontX + 14f, torso.yMin);
                        return true;
                    case BodyPart.LeftBackLeg:
                        point = new Vector2(backX - 14f, torso.yMin);
                        return true;
                    case BodyPart.RightBackLeg:
                        point = new Vector2(backX + 14f, torso.yMin);
                        return true;
                }
            }

            if (currentSpecies == Species.Snake && part == BodyPart.Head)
            {
                point = new Vector2(torso.xMax, centerY);
                return true;
            }

            switch (part)
            {
                case BodyPart.Head:
                    point = new Vector2(centerX, torso.yMax);
                    return true;
                case BodyPart.LeftArm:
                case BodyPart.LeftFrontLeg:
                case BodyPart.LeftWing:
                    point = new Vector2(torso.xMin, centerY);
                    return true;
                case BodyPart.RightArm:
                case BodyPart.RightFrontLeg:
                case BodyPart.RightWing:
                    point = new Vector2(torso.xMax, centerY);
                    return true;
                case BodyPart.LeftLeg:
                case BodyPart.LeftBackLeg:
                    point = new Vector2(lowerLeftX, torso.yMin);
                    return true;
                case BodyPart.RightLeg:
                case BodyPart.RightBackLeg:
                    point = new Vector2(lowerRightX, torso.yMin);
                    return true;
                case BodyPart.Tail:
                case BodyPart.TailFeather:
                    point = new Vector2(centerX, torso.yMin);
                    return true;
                default:
                    return false;
            }
        }

        private static Rect ExpandRect(Rect rect, float minWidth, float minHeight)
        {
            float width = Mathf.Max(rect.width, minWidth);
            float height = Mathf.Max(rect.height, minHeight);
            Vector2 center = rect.center;
            return Rect.MinMaxRect(
                center.x - width * 0.5f,
                center.y - height * 0.5f,
                center.x + width * 0.5f,
                center.y + height * 0.5f);
        }

        private bool TryGetPartConnectionPoint(BodyPart part, IReadOnlyList<Vector2> points, out Vector2 point)
        {
            return TryGetFirstDrawablePoint(points, out point);
        }

        private Vector2 ToPreviewPoint(BodyPart part, Vector2 drawPoint)
        {
            if (IsBreakPoint(drawPoint))
            {
                return drawPoint;
            }

            GetAssemblyFit(out float scale, out Vector2 offset);
            return (GetRawAssembledPoint(part, drawPoint) * scale + offset) * previewScale;
        }

        private Vector2 GetRawAssembledPoint(BodyPart part, Vector2 drawPoint)
        {
            if (IsBreakPoint(drawPoint))
            {
                return drawPoint;
            }

            if (currentSpecies != Species.Slime
                && part != BodyPart.Torso
                && TryGetPartConnectionPoint(part, drawings[part].Points, out Vector2 sourceConnection)
                && TryGetTorsoConnectionPoint(part, out Vector2 targetConnection))
            {
                return drawPoint + targetConnection - sourceConnection;
            }

            return drawPoint;
        }

        private void GetAssemblyFit(out float scale, out Vector2 offset)
        {
            scale = 1f;
            offset = Vector2.zero;

            if (!TryGetRawAssemblyBounds(out Rect bounds))
            {
                return;
            }

            float width = Mathf.Max(bounds.width, 1f);
            float height = Mathf.Max(bounds.height, 1f);
            float fitX = assembledMaxSize.x / width;
            float fitY = assembledMaxSize.y / height;
            scale = Mathf.Min(1f, fitX, fitY);
            offset = -bounds.center * scale;
        }

        private bool TryGetRawAssemblyBounds(out Rect bounds)
        {
            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;

            foreach (BodyPart part in GetCurrentParts())
            {
                IReadOnlyList<Vector2> points = drawings[part].Points;
                for (int i = 0; i < points.Count; i++)
                {
                    if (IsBreakPoint(points[i]))
                    {
                        continue;
                    }

                    Vector2 point = GetRawAssembledPoint(part, points[i]);
                    if (!found)
                    {
                        minX = maxX = point.x;
                        minY = maxY = point.y;
                        found = true;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, point.x);
                        maxX = Mathf.Max(maxX, point.x);
                        minY = Mathf.Min(minY, point.y);
                        maxY = Mathf.Max(maxY, point.y);
                    }
                }
            }

            bounds = found ? Rect.MinMaxRect(minX, minY, maxX, maxY) : new Rect();
            return found;
        }

        private bool CanStartStroke(Vector2 point, out Vector2 startPoint)
        {
            startPoint = point;
            if (currentSpecies == Species.Slime || currentPart == BodyPart.Torso || CountDrawablePoints(drawings[currentPart].Points) > 0)
            {
                return true;
            }

            Vector2 required = GetRequiredLocalStartPoint(currentPart);
            if (Vector2.Distance(point, required) > startPointSnapRadius)
            {
                SetMessage(LocalizationManager.Format("msg_start_at_marker", GetPartLabel(currentPart)));
                return false;
            }

            startPoint = required;
            return true;
        }

        private bool IsCloseToRequiredStart(BodyPart part, Vector2 point)
        {
            return Vector2.Distance(point, GetRequiredLocalStartPoint(part)) <= startPointSnapRadius;
        }

        private Vector2 GetRequiredLocalStartPoint(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return currentSpecies == Species.Cat || currentSpecies == Species.Snake
                        ? new Vector2(-115f, 0f)
                        : new Vector2(0f, -70f);
                case BodyPart.LeftArm:
                case BodyPart.LeftWing:
                    return new Vector2(115f, 0f);
                case BodyPart.RightArm:
                case BodyPart.RightWing:
                    return new Vector2(-115f, 0f);
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                case BodyPart.LeftFrontLeg:
                case BodyPart.RightFrontLeg:
                case BodyPart.LeftBackLeg:
                case BodyPart.RightBackLeg:
                    return new Vector2(0f, 70f);
                case BodyPart.Tail:
                    return currentSpecies == Species.Cat ? new Vector2(115f, 0f) : new Vector2(0f, 70f);
                case BodyPart.TailFeather:
                    return new Vector2(0f, 70f);
                default:
                    return Vector2.zero;
            }
        }

        private static GameObject CreateUiSegment(string name, RectTransform parent, Vector2 start, Vector2 end, float width, Color color, float scale)
        {
            GameObject segment = new GameObject(name);
            segment.transform.SetParent(parent, false);

            Image image = segment.AddComponent<Image>();
            image.color = color;

            RectTransform rect = image.rectTransform;
            Vector2 scaledStart = start * scale;
            Vector2 scaledEnd = end * scale;
            Vector2 delta = scaledEnd - scaledStart;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.anchoredPosition = (scaledStart + scaledEnd) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return segment;
        }

        private static Color GetPartColor(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return new Color(1f, 0.72f, 0.2f);
                case BodyPart.Torso:
                    return new Color(0.1f, 0.35f, 1f);
                case BodyPart.LeftArm:
                case BodyPart.RightArm:
                case BodyPart.LeftFrontLeg:
                case BodyPart.RightFrontLeg:
                case BodyPart.LeftBackLeg:
                case BodyPart.RightBackLeg:
                    return new Color(0.98f, 0.28f, 0.25f);
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    return new Color(0.1f, 0.72f, 0.32f);
                case BodyPart.Tail:
                case BodyPart.TailFeather:
                    return new Color(0.95f, 0.55f, 0.18f);
                case BodyPart.LeftWing:
                case BodyPart.RightWing:
                    return new Color(0.45f, 0.35f, 0.95f);
                case BodyPart.SlimeBody:
                    return new Color(0.3f, 0.85f, 0.75f);
                default:
                    return Color.black;
            }
        }

        private void InitializeDrawings()
        {
            drawings.Clear();
            speciesDrawings.Clear();

            foreach (Species species in System.Enum.GetValues(typeof(Species)))
            {
                speciesDrawings.Add(species, CreateDrawingSet(species));
            }

            UseSpeciesDrawings(currentSpecies);
        }

        private Dictionary<BodyPart, PartDrawing> CreateDrawingSet(Species species)
        {
            Dictionary<BodyPart, PartDrawing> set = new Dictionary<BodyPart, PartDrawing>();
            foreach (BodyPart part in GetAllParts())
            {
                set.Add(part, new PartDrawing());
            }

            Dictionary<BodyPart, PartDrawing> previous = new Dictionary<BodyPart, PartDrawing>(drawings);
            drawings.Clear();
            foreach (KeyValuePair<BodyPart, PartDrawing> pair in set)
            {
                drawings.Add(pair.Key, pair.Value);
            }

            BuildDefaultBody(species);

            drawings.Clear();
            foreach (KeyValuePair<BodyPart, PartDrawing> pair in previous)
            {
                drawings.Add(pair.Key, pair.Value);
            }

            return set;
        }

        private void UseSpeciesDrawings(Species species)
        {
            drawings.Clear();
            Dictionary<BodyPart, PartDrawing> set = speciesDrawings[species];
            foreach (KeyValuePair<BodyPart, PartDrawing> pair in set)
            {
                drawings.Add(pair.Key, pair.Value);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            InitializeDrawings();
            SnapSpeciesConnectionStarts();
            RebuildAllVisuals();
            initialized = true;
        }

        private void BuildDefaultBody(Species species)
        {
            if (species == Species.Slime)
            {
                SetDefaultPart(BodyPart.SlimeBody, new[]
                {
                    new Vector2(-88f, 0f),
                    new Vector2(-72f, 34f),
                    new Vector2(-35f, 56f),
                    new Vector2(22f, 58f),
                    new Vector2(68f, 35f),
                    new Vector2(92f, 0f),
                    new Vector2(72f, -35f),
                    new Vector2(25f, -56f),
                    new Vector2(-35f, -54f),
                    new Vector2(-76f, -32f),
                    new Vector2(-88f, 0f)
                });
                return;
            }

            if (species == Species.Cat)
            {
                SetDefaultPart(BodyPart.Torso, new[]
                {
                    new Vector2(-85f, 40f),
                    new Vector2(85f, 40f),
                    new Vector2(85f, -35f),
                    new Vector2(-85f, -35f),
                    new Vector2(-85f, 40f)
                });
                SetDefaultPart(BodyPart.Head, new[]
                {
                    new Vector2(-115f, 0f),
                    new Vector2(-78f, 36f),
                    new Vector2(-70f, 74f),
                    new Vector2(-38f, 50f),
                    new Vector2(2f, 52f),
                    new Vector2(32f, 76f),
                    new Vector2(38f, 40f),
                    new Vector2(56f, 8f),
                    new Vector2(38f, -34f),
                    new Vector2(-34f, -46f),
                    new Vector2(-80f, -30f),
                    new Vector2(-115f, 0f)
                });
                SetDefaultPart(BodyPart.LeftFrontLeg, new[] { new Vector2(0f, 70f), new Vector2(0f, -58f) });
                SetDefaultPart(BodyPart.RightFrontLeg, new[] { new Vector2(0f, 70f), new Vector2(0f, -58f) });
                SetDefaultPart(BodyPart.LeftBackLeg, new[] { new Vector2(0f, 70f), new Vector2(0f, -62f) });
                SetDefaultPart(BodyPart.RightBackLeg, new[] { new Vector2(0f, 70f), new Vector2(0f, -62f) });
                SetDefaultPart(BodyPart.Tail, new[] { new Vector2(115f, 0f), new Vector2(35f, 55f), new Vector2(-50f, 25f) });
                return;
            }

            if (species == Species.Snake)
            {
                SetDefaultPart(BodyPart.Torso, new[]
                {
                    new Vector2(-120f, 20f),
                    new Vector2(95f, 18f),
                    new Vector2(120f, -18f),
                    new Vector2(-105f, -22f),
                    new Vector2(-120f, 20f)
                });
                SetDefaultPart(BodyPart.Head, new[] { new Vector2(-115f, 0f), new Vector2(-35f, 42f), new Vector2(45f, 0f), new Vector2(-35f, -42f), new Vector2(-115f, 0f) });
                return;
            }

            SetDefaultPart(BodyPart.Torso, new[]
            {
                new Vector2(0f, 70f),
                new Vector2(-35f, 70f),
                new Vector2(-35f, -70f),
                new Vector2(35f, -70f),
                new Vector2(35f, 70f),
                new Vector2(0f, 70f)
            });

            SetDefaultPart(BodyPart.Head, new[]
            {
                new Vector2(0f, -70f),
                new Vector2(-55f, -70f),
                new Vector2(-55f, 25f),
                new Vector2(55f, 25f),
                new Vector2(55f, -70f),
                new Vector2(0f, -70f)
            });

            SetDefaultPart(BodyPart.LeftArm, new[]
            {
                new Vector2(115f, 0f),
                new Vector2(20f, -24f)
            });

            SetDefaultPart(BodyPart.RightArm, new[]
            {
                new Vector2(-115f, 0f),
                new Vector2(-20f, -24f)
            });

            SetDefaultPart(BodyPart.LeftLeg, new[]
            {
                new Vector2(0f, 70f),
                new Vector2(-45f, -55f)
            });

            SetDefaultPart(BodyPart.RightLeg, new[]
            {
                new Vector2(0f, 70f),
                new Vector2(45f, -55f)
            });

            if (species == Species.Bird)
            {
                SetDefaultPart(BodyPart.LeftWing, new[] { new Vector2(115f, 0f), new Vector2(-95f, 20f), new Vector2(-45f, -35f) });
                SetDefaultPart(BodyPart.RightWing, new[] { new Vector2(-115f, 0f), new Vector2(95f, 20f), new Vector2(45f, -35f) });
            }
        }

        private void SetDefaultPart(BodyPart part, Vector2[] points)
        {
            PartDrawing drawing = drawings[part];
            drawing.Points.Clear();
            drawing.Points.AddRange(points);
            drawing.UsedInk = CalculateInk(points);
        }

        private float CalculateInk(IReadOnlyList<Vector2> points)
        {
            float ink = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                if (IsBreakPoint(points[i - 1]) || IsBreakPoint(points[i]))
                {
                    continue;
                }

                ink += Vector2.Distance(points[i - 1], points[i]) / pixelsPerInk;
            }

            return ink;
        }

        private void RebuildAllVisuals()
        {
            ClearRootChildren(lineRoot);
            ClearRootChildren(previewRoot);

            foreach (KeyValuePair<BodyPart, PartDrawing> pair in drawings)
            {
                pair.Value.LineSegments.Clear();
                pair.Value.PreviewSegments.Clear();
            }

            foreach (BodyPart part in GetCurrentParts())
            {
                PartDrawing drawing = drawings[part];
                IReadOnlyList<Vector2> points = drawing.Points;
                for (int i = 1; i < points.Count; i++)
                {
                    if (IsBreakPoint(points[i - 1]) || IsBreakPoint(points[i]))
                    {
                        continue;
                    }

                    CreateSegment(drawing, part, points[i - 1], points[i]);
                }
            }
        }

        private void RebuildPreviewVisuals()
        {
            ClearRootChildren(previewRoot);

            foreach (KeyValuePair<BodyPart, PartDrawing> pair in drawings)
            {
                pair.Value.PreviewSegments.Clear();
            }

            foreach (BodyPart part in GetCurrentParts())
            {
                PartDrawing drawing = drawings[part];
                IReadOnlyList<Vector2> points = drawing.Points;
                for (int i = 1; i < points.Count; i++)
                {
                    if (IsBreakPoint(points[i - 1]) || IsBreakPoint(points[i]))
                    {
                        continue;
                    }

                    GameObject previewSegment = CreateUiSegment(
                        "PreviewSegment",
                        previewRoot,
                        ToPreviewPoint(part, points[i - 1]),
                        ToPreviewPoint(part, points[i]),
                        previewLineWidth,
                        GetPartColor(part),
                        1f);
                    drawing.PreviewSegments.Add(previewSegment);
                }
            }
        }

        private void ClearVisuals(PartDrawing drawing)
        {
            for (int i = 0; i < drawing.LineSegments.Count; i++)
            {
                DestroyObject(drawing.LineSegments[i]);
            }

            for (int i = 0; i < drawing.PreviewSegments.Count; i++)
            {
                DestroyObject(drawing.PreviewSegments[i]);
            }

            drawing.LineSegments.Clear();
            drawing.PreviewSegments.Clear();
        }

        private static void ClearRootChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyObject(root.GetChild(i).gameObject);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (target is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private float GetTotalInk()
        {
            float total = 0f;
            foreach (BodyPart part in GetCurrentParts())
            {
                total += drawings[part].UsedInk;
            }

            return total;
        }

        public static string GetPartLabel(BodyPart part)
        {
            return LocalizationManager.GetPartLabel(part);
        }

        private void RefreshLocalizedText()
        {
            RefreshInkText();
            RefreshConnectionMessage();
        }
    }
}
