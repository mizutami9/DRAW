using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class DrawManager : MonoBehaviour
    {
        public const float InkAllowancePerPlayer = 350f;
        public const float IndividualInkLimit = 500f;
        private const float TurtleGameplayCoordinateScale = 3f;
        private const float SlimeGameplayCoordinateScale = 5f;
        public const int BodyCoordinateVersion = 3;

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
            Turtle,
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
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private BodyBuilder bodyBuilder;
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private GameObject drawPanel;
        [SerializeField] private RectTransform drawArea;
        [SerializeField] private RectTransform lineRoot;
        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private Text inkText;
        [SerializeField] private Image inkGaugeFill;
        private Text personalInkValueText;
        private Text teamInkValueText;
        private Text personalInkLabelText;
        private Text teamInkLabelText;
        private Image teamInkGaugeFill;
        private Text abilityTitleText;
        private Text abilityEffectText;
        private Text abilityInkText;
        private Text abilityLowText;
        private Text abilityHighText;
        private Text abilityHintText;
        private Image abilityGaugeFill;
        private Image humanArmGaugeFill;
        private Text humanJumpGaugeLabel;
        private Text humanArmGaugeLabel;
        private Image abilityHeaderImage;
        [SerializeField] private Text partText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text abilityText;
        [SerializeField] private DrawFeedbackController feedback;
        [SerializeField] private float maxInk = IndividualInkLimit;
        [SerializeField] private float pixelsPerInk = 5f;
        [SerializeField] private float minPointDistance = 8f;
        [SerializeField] private float lineWidth = 6f;
        [SerializeField] private float eraserRadius = 18f;
        [SerializeField] private float previewScale = 0.7f;
        [SerializeField] private float previewLineWidth = 5f;
        [SerializeField] private float drawAreaSquareSize = 300f;
        [SerializeField] private float previewSquareSize = 290f;
        [SerializeField] private float startPointSnapRadius = 42f;

        private readonly Dictionary<BodyPart, PartDrawing> drawings = new Dictionary<BodyPart, PartDrawing>();
        private readonly Dictionary<Species, Dictionary<BodyPart, PartDrawing>> speciesDrawings = new Dictionary<Species, Dictionary<BodyPart, PartDrawing>>();
        private static readonly Vector2 StrokeBreak = new Vector2(float.NaN, float.NaN);
        private GameObject connectionMarker;
        private bool active;
        private bool drawing;
        private bool initialized;
        private Species currentSpecies = Species.Human;
        private StageSpeciesMask allowedSpecies = StageSpeciesMask.All;
        private BodyPart currentPart = BodyPart.Torso;
        private bool previewDirty;
        private bool hasEditSnapshot;
        private Species snapshotSpecies;
        private BodyPart snapshotPart;
        private Dictionary<Species, Dictionary<BodyPart, List<Vector2>>> editSnapshot;
        private ToolMode toolMode = ToolMode.Pen;
        private readonly float[] brushSizes = { 3f, 5f, 6f, 8f, 10f };
        private int brushSizeIndex = 2;
        private GameObject previewHighlight;
        private float previewContentScale = 1f;
        private Vector2 previewContentOffset;
        [SerializeField] private Button penToolButton;
        [SerializeField] private Button eraserToolButton;
        private GameObject eraserCursor;
        private float eraserCursorRadius = -1f;
        private readonly List<Vector2> clearedPartUndoPoints = new List<Vector2>();
        private Species clearedPartUndoSpecies;
        private BodyPart clearedPartUndoPart;
        private bool hasClearedPartUndo;

        public event System.Action<BodyPart> CurrentPartChanged;
        public event System.Action<Species> CurrentSpeciesChanged;
        public event System.Action SpeciesAvailabilityChanged;
        public float UsedInk => GetTotalInk();
        public BodyPart CurrentPart => currentPart;
        public Species CurrentSpecies => currentSpecies;
        public StageSpeciesMask AllowedSpecies => allowedSpecies;

        public bool IsSpeciesAllowed(Species species)
        {
            return StageSpeciesRules.IsAllowed(allowedSpecies, species);
        }

        public void SetAllowedSpecies(StageSpeciesMask availability)
        {
            StageSpeciesMask next = availability == StageSpeciesMask.None ? StageSpeciesMask.All : availability;
            if (allowedSpecies == next)
            {
                return;
            }

            allowedSpecies = next;
            SpeciesAvailabilityChanged?.Invoke();
            if (!IsSpeciesAllowed(currentSpecies))
            {
                SetSpecies(StageSpeciesRules.GetFirstAllowed(allowedSpecies));
            }
        }

        private void Awake()
        {
            maxInk = IndividualInkLimit;
            drawAreaSquareSize = 300f;
            previewSquareSize = 290f;
            EnsureInitialized();
            NormalizeDrawLayout();

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
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

        private void NormalizeDrawLayout()
        {
            DrawScreenVisualPolisher polisher = drawPanel != null ? drawPanel.GetComponent<DrawScreenVisualPolisher>() : null;
            if (polisher == null && drawPanel != null)
            {
                polisher = drawPanel.AddComponent<DrawScreenVisualPolisher>();
            }

            if (drawArea != null)
            {
                drawArea.anchoredPosition = new Vector2(-225f, 0f);
                drawArea.sizeDelta = Vector2.one * drawAreaSquareSize;
                EnsureRectMask(drawArea.gameObject);
            }

            if (lineRoot != null)
            {
                lineRoot.anchorMin = Vector2.zero;
                lineRoot.anchorMax = Vector2.one;
                lineRoot.offsetMin = Vector2.zero;
                lineRoot.offsetMax = Vector2.zero;
            }

            if (previewRoot != null)
            {
                RectTransform previewArea = previewRoot.parent as RectTransform;
                if (previewArea != null)
                {
                    previewArea.anchoredPosition = new Vector2(390f, 0f);
                    previewArea.sizeDelta = Vector2.one * previewSquareSize;
                    EnsureRectMask(previewArea.gameObject);
                    MovePreviewTitleOutside(previewArea);
                }

                previewRoot.anchoredPosition = new Vector2(0f, -8f);
                previewRoot.localScale = Vector3.one * 0.6f;
                previewRoot.sizeDelta = Vector2.one * (previewSquareSize - 34f);
            }

            RectTransform toolPanel = FindRect("DrawToolPanel");
            if (toolPanel != null)
            {
                toolPanel.anchorMin = new Vector2(0.5f, 0f);
                toolPanel.anchorMax = new Vector2(0.5f, 0f);
                toolPanel.pivot = new Vector2(0.5f, 0f);
                toolPanel.anchoredPosition = new Vector2(-145f, 14f);
                toolPanel.sizeDelta = new Vector2(930f, 118f);
            }

            if (inkText != null)
            {
                inkText.gameObject.SetActive(false);
            }

            if (messageText != null)
            {
                messageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                messageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                messageText.rectTransform.pivot = new Vector2(0.5f, 0f);
                messageText.rectTransform.anchoredPosition = new Vector2(90f, 536f);
                messageText.rectTransform.sizeDelta = new Vector2(780f, 34f);
            }

            RectTransform speciesPanel = FindRect("DrawSpeciesPanel");
            if (speciesPanel != null)
            {
                speciesPanel.anchorMin = new Vector2(0f, 0f);
                speciesPanel.anchorMax = new Vector2(0f, 0f);
                speciesPanel.pivot = new Vector2(0f, 0f);
                speciesPanel.anchoredPosition = new Vector2(16f, 142f);
                speciesPanel.sizeDelta = new Vector2(68f, 300f);
                HideChild("DrawSpeciesTitle");
                LayoutDrawSpeciesButtons(speciesPanel);
            }

            polisher?.Polish();
            ResolveInkUi();
        }

        private static void EnsureRectMask(GameObject target)
        {
            if (target != null && target.GetComponent<RectMask2D>() == null)
            {
                target.AddComponent<RectMask2D>();
            }
        }

        private void ResolveInkUi()
        {
            RectTransform personalValue = FindRect("PersonalInkValue");
            RectTransform teamValue = FindRect("TeamInkValue");
            RectTransform personalLabel = FindRect("PersonalInkLabel");
            RectTransform teamLabel = FindRect("TeamInkLabel");
            RectTransform teamFill = FindRect("TeamInkGaugeFill");
            personalInkValueText = personalValue != null ? personalValue.GetComponent<Text>() : null;
            teamInkValueText = teamValue != null ? teamValue.GetComponent<Text>() : null;
            personalInkLabelText = personalLabel != null ? personalLabel.GetComponent<Text>() : null;
            teamInkLabelText = teamLabel != null ? teamLabel.GetComponent<Text>() : null;
            teamInkGaugeFill = teamFill != null ? teamFill.GetComponent<Image>() : null;
            abilityTitleText = FindRect("AbilityTitleText")?.GetComponent<Text>();
            abilityEffectText = FindRect("AbilityEffectText")?.GetComponent<Text>();
            abilityInkText = FindRect("AbilityInkText")?.GetComponent<Text>();
            abilityLowText = FindRect("AbilityLowText")?.GetComponent<Text>();
            abilityHighText = FindRect("AbilityHighText")?.GetComponent<Text>();
            abilityHintText = FindRect("AbilityHintText")?.GetComponent<Text>();
            abilityGaugeFill = FindRect("AbilityGaugeFill")?.GetComponent<Image>();
            humanArmGaugeFill = FindRect("HumanArmGaugeFill")?.GetComponent<Image>();
            humanJumpGaugeLabel = FindRect("HumanJumpGaugeLabel")?.GetComponent<Text>();
            humanArmGaugeLabel = FindRect("HumanArmGaugeLabel")?.GetComponent<Text>();
            abilityHeaderImage = FindRect("AbilityHeaderBand")?.GetComponent<Image>();
        }

        private void MovePreviewTitleOutside(RectTransform previewArea)
        {
            RectTransform title = FindRect("PreviewTitle");
            if (title == null || drawPanel == null)
            {
                return;
            }

            title.SetParent(drawPanel.transform, false);
            title.anchorMin = new Vector2(0.5f, 0.5f);
            title.anchorMax = new Vector2(0.5f, 0.5f);
            title.pivot = new Vector2(0.5f, 0.5f);
            title.anchoredPosition = previewArea.anchoredPosition + new Vector2(0f, previewSquareSize * 0.5f + 11f);
            title.sizeDelta = new Vector2(previewSquareSize, 18f);
        }

        private void LayoutDrawSpeciesButtons(RectTransform speciesPanel)
        {
            Species[] species =
            {
                Species.Human,
                Species.Cat,
                Species.Bird,
                Species.Turtle,
                Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                string prefix = species[i].ToString();
                RectTransform button = FindRect(prefix + "DrawSpeciesButton");
                if (button != null)
                {
                    button.SetParent(speciesPanel, false);
                    button.anchorMin = new Vector2(0.5f, 1f);
                    button.anchorMax = new Vector2(0.5f, 1f);
                    button.pivot = new Vector2(0.5f, 1f);
                    button.anchoredPosition = new Vector2(0f, -10f - i * 58f);
                    button.sizeDelta = new Vector2(52f, 52f);
                }

                HideChild(prefix + "DrawSpeciesLabel");
            }
        }

        private void HideChild(string name)
        {
            Transform child = FindDeep(drawPanel != null ? drawPanel.transform : transform, name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private RectTransform FindRect(string name)
        {
            Transform child = FindDeep(drawPanel != null ? drawPanel.transform : transform, name);
            return child != null ? child as RectTransform : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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
            if (onlineManager != null)
            {
                onlineManager.BodyDataReceived += HandleInkBudgetChanged;
                onlineManager.StateChanged += HandleOnlineStateChanged;
            }
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
            if (onlineManager != null)
            {
                onlineManager.BodyDataReceived -= HandleInkBudgetChanged;
                onlineManager.StateChanged -= HandleOnlineStateChanged;
            }
        }

        private void HandleInkBudgetChanged(OnlineBodyData bodyData)
        {
            RefreshInkText();
        }

        private void HandleOnlineStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            RefreshInkText();
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

            UpdateEraserCursor();
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

            feedback?.SetActive(value);
            UpdateToolButtons();
            UpdateEraserCursor();
            RefreshInkText();
            if (value)
            {
                CaptureEditSnapshot();
                RefreshConnectionMessage();
                SetPartSegmentVisibility();
                UpdateConnectionMarker();
                UpdatePreviewHighlight();
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
            if (current.Points.Count == 0)
            {
                return;
            }

            clearedPartUndoPoints.Clear();
            clearedPartUndoPoints.AddRange(current.Points);
            clearedPartUndoSpecies = currentSpecies;
            clearedPartUndoPart = currentPart;
            hasClearedPartUndo = true;
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
            feedback?.ButtonPress();
        }

        public void ResetAllToDefault()
        {
            EnsureInitialized();
            FinishStroke();

            currentSpecies = StageSpeciesRules.GetFirstAllowed(allowedSpecies);
            currentPart = BodyPart.Torso;
            InitializeDrawings();
            UseSpeciesDrawings(currentSpecies);
            toolMode = ToolMode.Pen;
            hasClearedPartUndo = false;
            clearedPartUndoPoints.Clear();
            drawing = false;

            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            UpdatePreviewHighlight();
            UpdateToolButtons();
            CurrentSpeciesChanged?.Invoke(currentSpecies);
            CurrentPartChanged?.Invoke(currentPart);
            feedback?.ButtonPress();
        }

        public void RefreshInkBudgetDisplay()
        {
            RefreshInkText();
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

            currentSpecies = IsSpeciesAllowed(snapshotSpecies)
                ? snapshotSpecies
                : StageSpeciesRules.GetFirstAllowed(allowedSpecies);
            UseSpeciesDrawings(currentSpecies);
            currentPart = IsPartActive(snapshotPart) ? snapshotPart : GetCurrentParts()[0];
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            feedback?.ButtonPress();
            CurrentSpeciesChanged?.Invoke(currentSpecies);
            CurrentPartChanged?.Invoke(currentPart);
            hasEditSnapshot = false;
        }

        public void UndoLastStroke()
        {
            FinishStroke();
            PartDrawing current = drawings[currentPart];
            if (hasClearedPartUndo
                && clearedPartUndoSpecies == currentSpecies
                && clearedPartUndoPart == currentPart)
            {
                current.Points.Clear();
                current.Points.AddRange(clearedPartUndoPoints);
                current.UsedInk = CalculateInk(current.Points);
                clearedPartUndoPoints.Clear();
                hasClearedPartUndo = false;
                RebuildAllVisuals();
                SetPartSegmentVisibility();
                RefreshInkText();
                RefreshConnectionMessage();
                UpdateConnectionMarker();
                feedback?.ButtonPress();
                return;
            }

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
            eraserRadius = Mathf.Max(8f, lineWidth * 3.2f);
            UpdateEraserCursor();
            feedback?.ButtonPress();
        }

        public void SetBrushSizePixels(float pixels)
        {
            lineWidth = Mathf.Clamp(pixels, 1f, 30f);
            eraserRadius = Mathf.Max(8f, lineWidth * 3.2f);
            UpdateEraserCursor();
            feedback?.ButtonPress();
        }

        public void SetToolMode(int mode)
        {
            FinishStroke();
            toolMode = mode == 1 ? ToolMode.Eraser : ToolMode.Pen;
            UpdateToolButtons();
            UpdateEraserCursor();
            feedback?.ButtonPress();
        }

        public void SetToolButtons(Button penButton, Button eraserButton)
        {
            penToolButton = penButton;
            eraserToolButton = eraserButton;
            UpdateToolButtons();
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
            UpdatePreviewHighlight();
            GameSfx.Play(SfxId.DrawPartChange);
            CurrentPartChanged?.Invoke(currentPart);
        }

        public void SetSpecies(Species species)
        {
            if (!IsSpeciesAllowed(species))
            {
                SetMessage(LocalizationManager.Format(
                    "draw_species_locked",
                    LocalizationManager.T(StageSpeciesRules.GetSpeciesLocalizationKey(species))),
                    true);
                GameSfx.Play(SfxId.UiToggleOff);
                return;
            }

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
            if (!active)
            {
                SendCurrentBodyData();
            }
            UpdatePreviewHighlight();
            GameSfx.Play(SfxId.DrawSpeciesChange);
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
                GameSfx.Play(SfxId.DrawConfirm);
                hasEditSnapshot = false;
                editSnapshot = null;
                stageManager?.ConfirmDrawingMode();
            }
        }

        public bool TryApplyDrawing()
        {
            if (!ValidateConnections(out string errorMessage))
            {
                SetMessage(errorMessage, true);
                return false;
            }

            if (!ValidateInkBudget(out errorMessage))
            {
                SetMessage(errorMessage, true);
                GameSfx.Play(SfxId.DrawInkOver);
                return false;
            }

            SetMessage(string.Empty);
            ApplyDrawing();
            SendCurrentBodyData();
            return true;
        }

        public void SendCurrentBodyData()
        {
            onlineManager?.SendBodyData(new OnlineBodyData
            {
                PlayerId = "local",
                Json = ExportCurrentBodyJson()
            });
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

            currentSpecies = IsSpeciesAllowed(state.Species)
                ? state.Species
                : StageSpeciesRules.GetFirstAllowed(allowedSpecies);
            UseSpeciesDrawings(currentSpecies);
            currentPart = IsPartActive(state.Part) ? state.Part : GetCurrentParts()[0];
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
            UpdatePreviewHighlight();
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
            return GetUnscaledBodyPoints(part);
        }

        private List<Vector2> GetUnscaledBodyPoints(BodyPart part)
        {
            List<Vector2> result = new List<Vector2>();
            IReadOnlyList<Vector2> source = drawings[part].Points;
            Vector2 offset = GetBodyAnchorOffset();
            float coordinateScale = GetGameplayCoordinateScale(currentSpecies);

            for (int i = 0; i < source.Count; i++)
            {
                result.Add(IsBreakPoint(source[i])
                    ? StrokeBreak
                    : (GetRawAssembledPoint(part, source[i]) + offset) * coordinateScale);
            }

            return result;
        }

        private Vector2 GetBodyAnchorOffset()
        {
            if (currentSpecies != Species.Slime && TryGetRawPartBounds(BodyPart.Torso, out Rect torsoBounds))
            {
                return -torsoBounds.center;
            }

            return TryGetRawAssemblyBounds(out Rect bounds) ? -bounds.center : Vector2.zero;
        }

        public float GetInk(BodyPart part)
        {
            EnsureInitialized();
            return drawings[part].UsedInk;
        }

        public string ExportCurrentBodyJson()
        {
            EnsureInitialized();
            BodyPart[] activeParts = GetCurrentParts();
            SerializableBodyDrawing body = new SerializableBodyDrawing
            {
                Species = currentSpecies.ToString(),
                CoordinateVersion = BodyCoordinateVersion,
                Parts = new SerializableBodyPartDrawing[activeParts.Length]
            };

            for (int i = 0; i < activeParts.Length; i++)
            {
                BodyPart part = activeParts[i];
                PartDrawing drawing = drawings[part];
                body.Parts[i] = new SerializableBodyPartDrawing
                {
                    Part = part.ToString(),
                    Points = drawing.Points.ToArray(),
                    Ink = drawing.UsedInk
                };
            }

            return JsonUtility.ToJson(body);
        }

        public DrawingState CreateStateFromBodyJson(string json)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            SerializableBodyDrawing body = JsonUtility.FromJson<SerializableBodyDrawing>(json);
            if (body == null)
            {
                return null;
            }

            Species species = Species.Human;
            if (!string.IsNullOrEmpty(body.Species))
            {
                if (string.Equals(body.Species, "Snake", System.StringComparison.OrdinalIgnoreCase))
                {
                    species = Species.Turtle;
                }
                else
                {
                    System.Enum.TryParse(body.Species, true, out species);
                }
            }
            if (!IsSpeciesAllowed(species))
            {
                species = StageSpeciesRules.GetFirstAllowed(allowedSpecies);
            }

            DrawingState state = new DrawingState
            {
                Species = species,
                Part = GetPartsForSpecies(species)[0]
            };

            foreach (Species entrySpecies in System.Enum.GetValues(typeof(Species)))
            {
                Dictionary<BodyPart, List<Vector2>> partPoints = new Dictionary<BodyPart, List<Vector2>>();
                foreach (BodyPart part in GetAllParts())
                {
                    partPoints[part] = new List<Vector2>();
                }

                state.Points[entrySpecies] = partPoints;
            }

            if (body.Parts != null)
            {
                for (int i = 0; i < body.Parts.Length; i++)
                {
                    SerializableBodyPartDrawing partDrawing = body.Parts[i];
                    if (partDrawing == null || string.IsNullOrEmpty(partDrawing.Part))
                    {
                        continue;
                    }

                    if (!System.Enum.TryParse(partDrawing.Part, out BodyPart part))
                    {
                        continue;
                    }

                    List<Vector2> loadedPoints = partDrawing.Points != null
                        ? new List<Vector2>(partDrawing.Points)
                        : new List<Vector2>();
                    if (species == Species.Turtle && body.CoordinateVersion < 2)
                    {
                        ScaleDrawablePoints(loadedPoints, 1f / TurtleGameplayCoordinateScale);
                    }
                    if (species == Species.Slime && body.CoordinateVersion < 3)
                    {
                        ScaleDrawablePoints(loadedPoints, 1f / SlimeGameplayCoordinateScale);
                    }
                    state.Points[species][part] = loadedPoints;
                }
            }

            return state;
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
                case Species.Turtle:
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

                    hasClearedPartUndo = false;
                    clearedPartUndoPoints.Clear();
                    current.Points.Add(startPoint);
                    feedback?.BeginStroke(startPoint, GetPartColor(currentPart));
                    RefreshInkText();
                    RefreshConnectionMessage();
                }
            }

            if (Input.GetMouseButton(0) && toolMode == ToolMode.Eraser)
            {
                if (TryGetDrawPoint(out Vector2 erasePoint))
                {
                    EraseAt(erasePoint);
                    feedback?.Erase(erasePoint);
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
            if (!EraseCircle(current.Points, point, eraserRadius))
            {
                return;
            }

            current.UsedInk = CalculateInk(current.Points);
            RebuildAllVisuals();
            SetPartSegmentVisibility();
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();
        }

        private static bool EraseCircle(List<Vector2> points, Vector2 center, float radius)
        {
            if (points == null || points.Count == 0)
            {
                return false;
            }

            List<Vector2> rebuilt = new List<Vector2>();
            bool changed = false;
            bool hasPrevious = false;
            Vector2 previous = Vector2.zero;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                if (IsBreakPoint(current))
                {
                    AppendBreak(rebuilt);
                    hasPrevious = false;
                    continue;
                }

                if (!hasPrevious)
                {
                    previous = current;
                    hasPrevious = true;
                    if (i == points.Count - 1 || IsBreakPoint(points[i + 1]))
                    {
                        if (Vector2.Distance(current, center) > radius)
                        {
                            AppendPoint(rebuilt, current);
                        }
                        else
                        {
                            changed = true;
                        }
                    }

                    continue;
                }

                int pieceCount = GetOutsidePieces(previous, current, center, radius, out Vector2 a0, out Vector2 a1, out Vector2 b0, out Vector2 b1);
                if (pieceCount == 0)
                {
                    changed = true;
                }
                else
                {
                    if (pieceCount >= 1)
                    {
                        AppendPiece(rebuilt, a0, a1);
                    }

                    if (pieceCount >= 2)
                    {
                        AppendPiece(rebuilt, b0, b1);
                    }

                    if (pieceCount != 1 || a0 != previous || a1 != current)
                    {
                        changed = true;
                    }
                }

                previous = current;
            }

            CleanupBreaks(rebuilt);
            if (!changed)
            {
                return false;
            }

            points.Clear();
            points.AddRange(rebuilt);
            return true;
        }

        private static int GetOutsidePieces(
            Vector2 start,
            Vector2 end,
            Vector2 center,
            float radius,
            out Vector2 a0,
            out Vector2 a1,
            out Vector2 b0,
            out Vector2 b1)
        {
            a0 = a1 = b0 = b1 = Vector2.zero;
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                if (Vector2.Distance(start, center) <= radius)
                {
                    return 0;
                }

                a0 = start;
                a1 = end;
                return 1;
            }

            float radiusSquared = radius * radius;
            Vector2 fromCenter = start - center;
            float qa = lengthSquared;
            float qb = 2f * Vector2.Dot(fromCenter, delta);
            float qc = fromCenter.sqrMagnitude - radiusSquared;
            float discriminant = qb * qb - 4f * qa * qc;

            if (discriminant <= 0f)
            {
                if (DistancePointToSegment(center, start, end) <= radius)
                {
                    return 0;
                }

                a0 = start;
                a1 = end;
                return 1;
            }

            float sqrt = Mathf.Sqrt(discriminant);
            float rawT0 = (-qb - sqrt) / (2f * qa);
            float rawT1 = (-qb + sqrt) / (2f * qa);
            if (rawT1 < rawT0)
            {
                float temp = rawT0;
                rawT0 = rawT1;
                rawT1 = temp;
            }

            if (rawT1 <= 0f || rawT0 >= 1f)
            {
                if (Vector2.Distance((start + end) * 0.5f, center) <= radius)
                {
                    return 0;
                }

                a0 = start;
                a1 = end;
                return 1;
            }

            float t0 = Mathf.Clamp01(rawT0);
            float t1 = Mathf.Clamp01(rawT1);
            int count = 0;
            AddOutsideInterval(start, delta, 0f, t0, center, radius, ref count, ref a0, ref a1, ref b0, ref b1);
            AddOutsideInterval(start, delta, t1, 1f, center, radius, ref count, ref a0, ref a1, ref b0, ref b1);
            return count;
        }

        private static void AddOutsideInterval(
            Vector2 start,
            Vector2 delta,
            float from,
            float to,
            Vector2 center,
            float radius,
            ref int count,
            ref Vector2 a0,
            ref Vector2 a1,
            ref Vector2 b0,
            ref Vector2 b1)
        {
            const float MinInterval = 0.002f;
            if (to - from <= MinInterval)
            {
                return;
            }

            float mid = (from + to) * 0.5f;
            if (Vector2.Distance(start + delta * mid, center) <= radius)
            {
                return;
            }

            Vector2 p0 = start + delta * from;
            Vector2 p1 = start + delta * to;
            if (count == 0)
            {
                a0 = p0;
                a1 = p1;
            }
            else
            {
                b0 = p0;
                b1 = p1;
            }

            count++;
        }

        private static void AppendPiece(List<Vector2> points, Vector2 start, Vector2 end)
        {
            if (Vector2.Distance(start, end) <= Mathf.Epsilon)
            {
                AppendPoint(points, start);
                return;
            }

            if (points.Count > 0 && !IsBreakPoint(points[points.Count - 1]) && Vector2.Distance(points[points.Count - 1], start) > 0.01f)
            {
                AppendBreak(points);
            }

            AppendPoint(points, start);
            AppendPoint(points, end);
        }

        private static void AppendPoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count > 0 && !IsBreakPoint(points[points.Count - 1]) && Vector2.Distance(points[points.Count - 1], point) <= 0.01f)
            {
                return;
            }

            points.Add(point);
        }

        private static void AppendBreak(List<Vector2> points)
        {
            if (points.Count == 0 || IsBreakPoint(points[points.Count - 1]))
            {
                return;
            }

            points.Add(StrokeBreak);
        }

        private static void CleanupBreaks(List<Vector2> points)
        {
            while (points.Count > 0 && IsBreakPoint(points[0]))
            {
                points.RemoveAt(0);
            }

            while (points.Count > 0 && IsBreakPoint(points[points.Count - 1]))
            {
                points.RemoveAt(points.Count - 1);
            }

            for (int i = points.Count - 2; i >= 0; i--)
            {
                if (IsBreakPoint(points[i]) && IsBreakPoint(points[i + 1]))
                {
                    points.RemoveAt(i + 1);
                }
            }
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
            feedback?.EndStroke();
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
                GameSfx.Play(SfxId.DrawInkOver);
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
            feedback?.DrawSegment(previous, point, GetPartColor(currentPart));
            previewDirty = true;
            RefreshInkText();
            RefreshConnectionMessage();
            UpdateConnectionMarker();

            if (inkCost >= remainingInk)
            {
                FinishStroke();
                GameSfx.Play(SfxId.DrawInkWarning);
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
            int playerCount = ResolveInkBudgetPlayerCount();
            float otherConfirmedInk = ResolveOtherConfirmedInk();
            float teamInk = otherConfirmedInk + totalInk;
            float teamLimit = playerCount * InkAllowancePerPlayer;
            bool personalOver = totalInk > maxInk;
            bool teamOver = teamInk > teamLimit;

            if (personalInkValueText == null || teamInkValueText == null || teamInkGaugeFill == null)
            {
                ResolveInkUi();
            }

            Color normalText = new Color(0.12f, 0.1f, 0.08f, 1f);
            Color warningText = new Color(0.82f, 0.16f, 0.12f, 1f);
            if (personalInkLabelText != null)
            {
                personalInkLabelText.text = LocalizationManager.T("ink_personal_cap");
            }
            if (teamInkLabelText != null)
            {
                teamInkLabelText.text = LocalizationManager.Format("ink_team_formula", playerCount, InkAllowancePerPlayer);
            }
            if (personalInkValueText != null)
            {
                personalInkValueText.text = $"{totalInk:0.#} / {maxInk:0}";
                personalInkValueText.color = personalOver ? warningText : normalText;
            }

            if (teamInkValueText != null)
            {
                teamInkValueText.text = $"{teamInk:0.#} / {teamLimit:0}";
                teamInkValueText.color = teamOver ? warningText : normalText;
            }

            if (inkText != null)
            {
                inkText.gameObject.SetActive(false);
            }

            SetInkGauge(inkGaugeFill, maxInk <= 0f ? 0f : totalInk / maxInk, personalOver);
            SetInkGauge(teamInkGaugeFill, teamLimit <= 0f ? 0f : teamInk / teamLimit, teamOver);

            if (partText != null)
            {
                partText.text = $"{LocalizationManager.T("part")}: {GetPartLabel(currentPart)}";
            }

            if (abilityText != null)
            {
                PlayerAbilityController.AbilityProfile profile = PlayerAbilityController.CalculateProfile(this);
                RefreshAbilityCard(profile);
            }
        }

        private void RefreshAbilityCard(PlayerAbilityController.AbilityProfile profile)
        {
            float progress;
            float rankProgress;
            float secondaryProgress = 0f;
            string title;
            string effect;
            string ink;
            Color accent;

            switch (profile.Species)
            {
                case Species.Cat:
                    progress = Mathf.Clamp01(profile.CatLegInk / 120f);
                    title = LocalizationManager.T("ability_card_cat");
                    effect = LocalizationManager.Format(
                        "ability_effect_cat",
                        PlayerController2D.CalculateCatMoveSpeedMultiplier(profile.CatLegInk));
                    ink = LocalizationManager.Format("ability_ink_cat", profile.CatLegInk);
                    accent = new Color(0.94f, 0.54f, 0.18f, 1f);
                    break;
                case Species.Bird:
                    progress = Mathf.Clamp01(profile.WingInk / 350f);
                    title = LocalizationManager.T("ability_card_bird");
                    effect = LocalizationManager.Format("ability_effect_bird", progress * 100f);
                    ink = LocalizationManager.Format("ability_ink_bird", profile.WingInk);
                    accent = new Color(0.16f, 0.64f, 0.9f, 1f);
                    break;
                case Species.Turtle:
                    progress = 1f;
                    title = LocalizationManager.T("ability_card_turtle");
                    effect = LocalizationManager.T("ability_effect_turtle");
                    ink = LocalizationManager.T("ability_ink_turtle");
                    accent = new Color(0.24f, 0.62f, 0.34f, 1f);
                    break;
                case Species.Slime:
                    progress = Mathf.Clamp01(profile.SlimeInk / PlayerController2D.MaximumSlimeAbilityInk);
                    title = LocalizationManager.T("ability_card_slime");
                    effect = LocalizationManager.Format(
                        "ability_effect_slime",
                        PlayerController2D.CalculateSlimeMoveSpeedMultiplier(profile.SlimeInk),
                        PlayerController2D.CalculateSlimeJumpMultiplier(profile.SlimeInk),
                        PlayerController2D.CalculateSlimeStickStrength(profile.SlimeInk) * 100f);
                    ink = LocalizationManager.Format("ability_ink_slime", profile.SlimeInk);
                    accent = new Color(0.68f, 0.38f, 0.86f, 1f);
                    break;
                default:
                    progress = Mathf.Clamp01(profile.LegInk / 80f);
                    secondaryProgress = Mathf.Clamp01(profile.ArmInk / 280f);
                    title = LocalizationManager.T("ability_card_human");
                    effect = LocalizationManager.Format(
                        "ability_effect_human_combined",
                        ArmSwingController.CalculateArmStrengthMultiplier(profile.ArmInk),
                        PlayerAbilityController.CalculateHumanJumpMultiplier(profile.LegInk));
                    ink = LocalizationManager.Format("ability_ink_human_combined", profile.ArmInk, profile.LegInk);
                    accent = new Color(0.2f, 0.48f, 0.86f, 1f);
                    break;
            }

            bool human = profile.Species == Species.Human;
            rankProgress = human ? Mathf.Max(progress, secondaryProgress) : progress;

            string rank = rankProgress >= 0.9f ? "S"
                : rankProgress >= 0.7f ? "A"
                : rankProgress >= 0.45f ? "B"
                : rankProgress >= 0.2f ? "C"
                : "D";

            abilityText.text = profile.Species == Species.Turtle
                ? LocalizationManager.T("ability_turtle_badge")
                : profile.Species == Species.Slime
                    ? LocalizationManager.Format("ability_slime_badge", progress * 100f)
                    : LocalizationManager.Format("ability_rank", rank);
            abilityText.gameObject.SetActive(true);
            if (abilityTitleText != null) abilityTitleText.text = title;
            if (abilityEffectText != null) abilityEffectText.text = effect;
            if (abilityInkText != null) abilityInkText.text = ink;
            if (abilityLowText != null)
            {
                abilityLowText.gameObject.SetActive(!human);
                abilityLowText.text = LocalizationManager.T(
                    profile.Species == Species.Slime ? "ability_slime_gauge_low" : "ability_gauge_low");
            }
            if (abilityHighText != null)
            {
                abilityHighText.gameObject.SetActive(!human);
                abilityHighText.text = LocalizationManager.T(
                    profile.Species == Species.Slime ? "ability_slime_gauge_high" : "ability_gauge_high");
            }
            if (abilityHintText != null)
            {
                abilityHintText.text = LocalizationManager.T(
                    profile.Species == Species.Turtle
                        ? "ability_turtle_hint"
                        : profile.Species == Species.Slime
                            ? "ability_slime_hint"
                            : "ability_growth_hint");
            }
            if (abilityHeaderImage != null) abilityHeaderImage.color = accent;
            SetHumanAbilityGaugeLayout(human);
            SetInkGauge(abilityGaugeFill, progress, false);
            if (abilityGaugeFill != null) abilityGaugeFill.color = accent;
            SetInkGauge(humanArmGaugeFill, secondaryProgress, false);
            if (humanArmGaugeFill != null) humanArmGaugeFill.color = new Color(0.94f, 0.42f, 0.2f, 1f);
        }

        private void SetHumanAbilityGaugeLayout(bool human)
        {
            if (humanJumpGaugeLabel != null) humanJumpGaugeLabel.gameObject.SetActive(human);
            if (humanArmGaugeLabel != null) humanArmGaugeLabel.gameObject.SetActive(human);
            if (humanArmGaugeFill != null) humanArmGaugeFill.transform.parent.gameObject.SetActive(human);

            RectTransform gauge = abilityGaugeFill != null ? abilityGaugeFill.transform.parent as RectTransform : null;
            if (gauge == null)
            {
                return;
            }

            gauge.anchorMin = new Vector2(0f, 1f);
            gauge.anchorMax = new Vector2(0f, 1f);
            gauge.pivot = new Vector2(0f, 1f);
            gauge.anchoredPosition = human ? new Vector2(68f, -133f) : new Vector2(18f, -136f);
            gauge.sizeDelta = human ? new Vector2(194f, 14f) : new Vector2(244f, 18f);
        }

        private static void SetInkGauge(Image fill, float amount, bool over)
        {
            if (fill == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(amount);
            fill.color = over
                ? new Color(0.94f, 0.25f, 0.18f, 1f)
                : new Color(0.12f, 0.72f, 0.48f, 1f);
            fill.type = Image.Type.Simple;
            fill.fillAmount = 1f;
            RectTransform gaugeRect = fill.rectTransform;
            gaugeRect.anchorMin = Vector2.zero;
            gaugeRect.anchorMax = new Vector2(normalized, 1f);
            gaugeRect.pivot = new Vector2(0f, 0.5f);
            gaugeRect.offsetMin = Vector2.zero;
            gaugeRect.offsetMax = Vector2.zero;
        }

        private void UpdateToolButtons()
        {
            ResolveToolButtons();
            SetToolButtonVisual(penToolButton, toolMode == ToolMode.Pen, new Color(0.73f, 0.94f, 0.67f, 0.96f));
            SetToolButtonVisual(eraserToolButton, toolMode == ToolMode.Eraser, new Color(0.98f, 0.82f, 0.68f, 0.96f));
        }

        private void ResolveToolButtons()
        {
            if (drawPanel == null || (penToolButton != null && eraserToolButton != null))
            {
                return;
            }

            Button[] buttons = drawPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (penToolButton == null && buttons[i].name == "PenToolButton")
                {
                    penToolButton = buttons[i];
                }
                else if (eraserToolButton == null && buttons[i].name == "EraserToolButton")
                {
                    eraserToolButton = buttons[i];
                }
            }
        }

        private static void SetToolButtonVisual(Button button, bool selected, Color baseColor)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            Color shownColor = selected ? baseColor : new Color(0.92f, 0.91f, 0.87f, 1f);
            if (image != null)
            {
                image.color = shownColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }

            outline.enabled = true;
            outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(1f, -1f);
            outline.effectColor = selected
                ? new Color(0.04f, 0.12f, 0.16f, 1f)
                : new Color(0.2f, 0.18f, 0.14f, 0.42f);

            Transform selectionBadge = button.transform.Find("SelectionBadge");
            if (selectionBadge != null)
            {
                selectionBadge.gameObject.SetActive(selected);
                selectionBadge.SetAsLastSibling();
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontStyle = FontStyle.Bold;
                label.color = new Color(0.12f, 0.1f, 0.08f, 1f);
            }
        }

        private void UpdateEraserCursor()
        {
            if (!active || toolMode != ToolMode.Eraser || drawArea == null || !TryGetDrawPoint(out Vector2 point))
            {
                if (eraserCursor != null)
                {
                    eraserCursor.SetActive(false);
                }

                return;
            }

            EnsureEraserCursor();
            RectTransform rect = eraserCursor.GetComponent<RectTransform>();
            rect.anchoredPosition = point;
            eraserCursor.SetActive(true);
            eraserCursor.transform.SetAsLastSibling();

            if (!Mathf.Approximately(eraserCursorRadius, eraserRadius))
            {
                eraserCursorRadius = eraserRadius;
                RebuildEraserCursorGeometry();
            }
        }

        private void EnsureEraserCursor()
        {
            if (eraserCursor != null)
            {
                return;
            }

            eraserCursor = new GameObject("EraserCursor");
            eraserCursor.transform.SetParent(drawArea, false);
            RectTransform rect = eraserCursor.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            eraserCursorRadius = -1f;
            RebuildEraserCursorGeometry();
        }

        private void RebuildEraserCursorGeometry()
        {
            if (eraserCursor == null)
            {
                return;
            }

            ClearRootChildren(eraserCursor.transform);
            const int segments = 36;
            Color color = new Color(0.08f, 0.08f, 0.08f, 0.78f);
            RectTransform cursorRoot = eraserCursor.GetComponent<RectTransform>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * eraserRadius;
                Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * eraserRadius;
                CreateUiSegment("EraserCursorRing", cursorRoot, p0, p1, 2.2f, color, 1f);
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

        private bool ValidateInkBudget(out string errorMessage)
        {
            float localInk = GetTotalInk();
            if (localInk > maxInk + 0.01f)
            {
                errorMessage = LocalizationManager.Format("msg_personal_ink_over", localInk, maxInk, Mathf.Ceil(localInk - maxInk));
                return false;
            }

            int playerCount = ResolveInkBudgetPlayerCount();
            float otherConfirmedInk = ResolveOtherConfirmedInk();
            float teamLimit = playerCount * InkAllowancePerPlayer;
            float projectedTeamInk = otherConfirmedInk + localInk;
            if (projectedTeamInk > teamLimit + 0.01f)
            {
                errorMessage = LocalizationManager.Format("msg_team_ink_over", projectedTeamInk, teamLimit, Mathf.Ceil(projectedTeamInk - teamLimit));
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private int ResolveInkBudgetPlayerCount()
        {
            if (stageManager != null)
            {
                return stageManager.GetInkBudgetPlayerCount();
            }
            return onlineManager != null ? onlineManager.GetInkBudgetPlayerCount() : 1;
        }

        private float ResolveOtherConfirmedInk()
        {
            if (stageManager != null)
            {
                return stageManager.GetConfirmedInkExcludingActivePlayer();
            }
            return onlineManager != null ? onlineManager.GetConfirmedInkExcludingLocal() : 0f;
        }

        private void RefreshConnectionMessage()
        {
            if (!active)
            {
                return;
            }

            if (currentPart == BodyPart.Torso || currentSpecies == Species.Slime)
            {
                SetMessage(string.Empty);
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

        private void SetMessage(string message, bool alarm = false)
        {
            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = alarm ? new Color(0.82f, 0.12f, 0.08f) : Color.black;
                messageText.fontStyle = alarm ? FontStyle.Bold : FontStyle.Normal;
                messageText.gameObject.SetActive(active && alarm && !string.IsNullOrEmpty(message));
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

            if (currentSpecies == Species.Turtle && part == BodyPart.Head)
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

        private bool TryGetPartConnectionPoint(BodyPart part, IReadOnlyList<Vector2> points, out Vector2 point)
        {
            point = Vector2.zero;
            if (!TryGetBounds(points, out Rect bounds))
            {
                return false;
            }

            if (currentSpecies == Species.Cat)
            {
                switch (part)
                {
                    case BodyPart.Head:
                        return TryGetEdgeCenter(points, PartEdge.Left, out point);
                    case BodyPart.Tail:
                        return TryGetEdgeCenter(points, PartEdge.Right, out point);
                    case BodyPart.LeftFrontLeg:
                    case BodyPart.RightFrontLeg:
                    case BodyPart.LeftBackLeg:
                    case BodyPart.RightBackLeg:
                        return TryGetEdgeCenter(points, PartEdge.Top, out point);
                }
            }

            if (currentSpecies == Species.Turtle && part == BodyPart.Head)
            {
                return TryGetEdgeCenter(points, PartEdge.Left, out point);
            }

            switch (part)
            {
                case BodyPart.Head:
                    return TryGetEdgeCenter(points, PartEdge.Bottom, out point);
                case BodyPart.LeftArm:
                case BodyPart.LeftWing:
                    return TryGetEdgeCenter(points, PartEdge.Right, out point);
                case BodyPart.RightArm:
                case BodyPart.RightWing:
                    return TryGetEdgeCenter(points, PartEdge.Left, out point);
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                case BodyPart.LeftFrontLeg:
                case BodyPart.RightFrontLeg:
                case BodyPart.LeftBackLeg:
                case BodyPart.RightBackLeg:
                case BodyPart.Tail:
                case BodyPart.TailFeather:
                    return TryGetEdgeCenter(points, PartEdge.Top, out point);
                default:
                    return TryGetFirstDrawablePoint(points, out point);
            }
        }

        private enum PartEdge
        {
            Left,
            Right,
            Top,
            Bottom
        }

        private static bool TryGetEdgeCenter(IReadOnlyList<Vector2> points, PartEdge edge, out Vector2 center)
        {
            center = Vector2.zero;
            if (!TryGetBounds(points, out Rect bounds))
            {
                return false;
            }

            float target = edge switch
            {
                PartEdge.Left => bounds.xMin,
                PartEdge.Right => bounds.xMax,
                PartEdge.Top => bounds.yMax,
                PartEdge.Bottom => bounds.yMin,
                _ => 0f
            };
            float tolerance = Mathf.Max(2f, Mathf.Max(bounds.width, bounds.height) * 0.08f);
            Vector2 sum = Vector2.zero;
            int count = 0;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 candidate = points[i];
                if (IsBreakPoint(candidate))
                {
                    continue;
                }

                float value = edge == PartEdge.Left || edge == PartEdge.Right ? candidate.x : candidate.y;
                if (Mathf.Abs(value - target) <= tolerance)
                {
                    sum += candidate;
                    count++;
                }
            }

            if (count > 0)
            {
                center = sum / count;
                return true;
            }

            switch (edge)
            {
                case PartEdge.Left:
                    center = new Vector2(bounds.xMin, bounds.center.y);
                    return true;
                case PartEdge.Right:
                    center = new Vector2(bounds.xMax, bounds.center.y);
                    return true;
                case PartEdge.Top:
                    center = new Vector2(bounds.center.x, bounds.yMax);
                    return true;
                case PartEdge.Bottom:
                    center = new Vector2(bounds.center.x, bounds.yMin);
                    return true;
                default:
                    return false;
            }
        }

        private Vector2 ToPreviewPoint(BodyPart part, Vector2 drawPoint)
        {
            if (IsBreakPoint(drawPoint))
            {
                return drawPoint;
            }

            Vector2 point = (GetRawAssembledPoint(part, drawPoint) + GetBodyAnchorOffset())
                * GetGameplayCoordinateScale(currentSpecies)
                * previewScale;
            return point * previewContentScale + previewContentOffset;
        }

        private void UpdatePreviewFit()
        {
            previewContentScale = 1f;
            previewContentOffset = Vector2.zero;
            if (previewRoot == null)
            {
                return;
            }

            bool found = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            Vector2 bodyOffset = GetBodyAnchorOffset();
            float speciesScale = GetGameplayCoordinateScale(currentSpecies) * previewScale;
            foreach (BodyPart part in GetCurrentParts())
            {
                IReadOnlyList<Vector2> points = drawings[part].Points;
                for (int i = 0; i < points.Count; i++)
                {
                    if (IsBreakPoint(points[i]))
                    {
                        continue;
                    }

                    Vector2 point = (GetRawAssembledPoint(part, points[i]) + bodyOffset) * speciesScale;
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                    found = true;
                }
            }

            if (!found)
            {
                return;
            }

            Vector2 contentSize = max - min;
            const float safePadding = 24f;
            float availableWidth = Mathf.Max(1f, previewRoot.rect.width - safePadding * 2f);
            float availableHeight = Mathf.Max(1f, previewRoot.rect.height - safePadding * 2f);
            float fitX = contentSize.x > 0.01f ? availableWidth / contentSize.x : 1f;
            float fitY = contentSize.y > 0.01f ? availableHeight / contentSize.y : 1f;
            previewContentScale = Mathf.Min(1f, fitX, fitY);
            previewContentOffset = -(min + max) * 0.5f * previewContentScale;
        }

        private static float GetGameplayCoordinateScale(Species species)
        {
            return species == Species.Turtle
                ? TurtleGameplayCoordinateScale
                : species == Species.Slime
                    ? SlimeGameplayCoordinateScale
                    : 1f;
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

        private bool TryGetRawPartBounds(BodyPart part, out Rect bounds)
        {
            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;

            if (!drawings.TryGetValue(part, out PartDrawing drawing))
            {
                bounds = new Rect();
                return false;
            }

            for (int i = 0; i < drawing.Points.Count; i++)
            {
                if (IsBreakPoint(drawing.Points[i]))
                {
                    continue;
                }

                Vector2 point = GetRawAssembledPoint(part, drawing.Points[i]);
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
                    return currentSpecies == Species.Cat || currentSpecies == Species.Turtle
                        ? new Vector2(-115f / GetGameplayCoordinateScale(currentSpecies), 0f)
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
            image.raycastTarget = false;
            image.color = color;

            RectTransform rect = image.rectTransform;
            Vector2 scaledStart = start * scale;
            Vector2 scaledEnd = end * scale;
            Vector2 delta = scaledEnd - scaledStart;
            float noise = Hash01(scaledStart.x * 12.1f + scaledStart.y * 7.7f + scaledEnd.x * 3.9f + scaledEnd.y * 5.1f);
            bool pencilStroke = name == "InkSegment" || name == "PreviewSegment";
            float widthJitter = Mathf.Lerp(0.72f, 1.08f, noise);
            Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
            Color jitteredColor = color;
            jitteredColor.a *= pencilStroke ? Mathf.Lerp(0.42f, 0.68f, noise) : Mathf.Lerp(0.82f, 1f, noise);
            image.color = jitteredColor;
            float baseWidth = pencilStroke ? width * 0.5f : width;
            rect.sizeDelta = new Vector2(delta.magnitude * Mathf.Lerp(0.98f, 1.01f, noise), baseWidth * widthJitter);
            rect.anchoredPosition = (scaledStart + scaledEnd) * 0.5f + normal * Mathf.Lerp(-0.8f, 0.8f, noise);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            if (pencilStroke && delta.magnitude > 8f)
            {
                int fibers = name == "PreviewSegment" ? 3 : 5;
                for (int i = 0; i < fibers; i++)
                {
                    float fiberNoise = Hash01(noise * 97f + i * 13.37f);
                    GameObject fiber = new GameObject("PencilFiber");
                    fiber.transform.SetParent(segment.transform, false);
                    Image fiberImage = fiber.AddComponent<Image>();
                    fiberImage.raycastTarget = false;
                    Color fiberColor = color;
                    fiberColor.a *= Mathf.Lerp(0.16f, 0.34f, fiberNoise);
                    fiberImage.color = fiberColor;
                    RectTransform fiberRect = fiberImage.rectTransform;
                    fiberRect.anchorMin = new Vector2(0.5f, 0.5f);
                    fiberRect.anchorMax = new Vector2(0.5f, 0.5f);
                    fiberRect.pivot = new Vector2(0.5f, 0.5f);
                    fiberRect.anchoredPosition = new Vector2(
                        Mathf.Lerp(-1.2f, 1.2f, Hash01(fiberNoise * 23f)),
                        Mathf.Lerp(-width * 0.38f, width * 0.38f, fiberNoise));
                    fiberRect.sizeDelta = new Vector2(delta.magnitude * Mathf.Lerp(0.72f, 1.02f, fiberNoise), Mathf.Max(0.7f, width * Mathf.Lerp(0.1f, 0.2f, fiberNoise)));
                    fiberRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-1.8f, 1.8f, Hash01(fiberNoise * 47f)));
                }
            }

            if (!pencilStroke && delta.magnitude > 12f)
            {
                GameObject ghost = new GameObject("HandDrawnGhost");
                ghost.transform.SetParent(segment.transform, false);
                Image ghostImage = ghost.AddComponent<Image>();
                ghostImage.raycastTarget = false;
                Color ghostColor = color;
                ghostColor.a *= 0.24f;
                ghostImage.color = ghostColor;
                RectTransform ghostRect = ghostImage.rectTransform;
                ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
                ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
                ghostRect.pivot = new Vector2(0.5f, 0.5f);
                ghostRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-1.4f, 1.4f, Hash01(noise * 41f)));
                ghostRect.sizeDelta = new Vector2(delta.magnitude * 0.98f, Mathf.Max(1f, width * 0.38f));
            }

            return segment;
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value) * 43758.5453f, 1f);
        }

        private Color GetPartColor(BodyPart part)
        {
            if (bodyBuilder != null)
            {
                return bodyBuilder.PlayerColor;
            }

            return PlayerColorPalette.GetColor(0);
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
                }, 1f / SlimeGameplayCoordinateScale);
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

            if (species == Species.Turtle)
            {
                SetDefaultPart(BodyPart.Torso, new[]
                {
                    new Vector2(-105f, 0f),
                    new Vector2(-78f, 52f),
                    new Vector2(35f, 68f),
                    new Vector2(100f, 28f),
                    new Vector2(105f, -18f),
                    new Vector2(35f, -62f),
                    new Vector2(-78f, -48f),
                    new Vector2(-105f, 0f),
                    new Vector2(100f, 28f),
                    new Vector2(-78f, -48f),
                    new Vector2(35f, 68f),
                    new Vector2(35f, -62f)
                }, 1f / TurtleGameplayCoordinateScale);
                SetDefaultPart(BodyPart.Head, new[]
                {
                    new Vector2(-115f, 0f), new Vector2(-82f, 30f),
                    new Vector2(-36f, 22f), new Vector2(-22f, 0f),
                    new Vector2(-36f, -22f), new Vector2(-82f, -30f),
                    new Vector2(-115f, 0f)
                }, 1f / TurtleGameplayCoordinateScale);
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

        private void SetDefaultPart(BodyPart part, Vector2[] points, float coordinateScale = 1f)
        {
            PartDrawing drawing = drawings[part];
            drawing.Points.Clear();
            for (int i = 0; i < points.Length; i++)
            {
                drawing.Points.Add(IsBreakPoint(points[i]) ? StrokeBreak : points[i] * coordinateScale);
            }
            drawing.UsedInk = CalculateInk(drawing.Points);
        }

        private static void ScaleDrawablePoints(List<Vector2> points, float scale)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsBreakPoint(points[i]))
                {
                    points[i] *= scale;
                }
            }
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
            previewHighlight = null;
            UpdatePreviewFit();

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

            UpdatePreviewHighlight();
        }

        private void RebuildPreviewVisuals()
        {
            ClearRootChildren(previewRoot);
            previewHighlight = null;
            UpdatePreviewFit();

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

            UpdatePreviewHighlight();
        }

        private void UpdatePreviewHighlight()
        {
            if (previewRoot == null || !active)
            {
                if (previewHighlight != null)
                {
                    previewHighlight.SetActive(false);
                }

                return;
            }

            EnsurePreviewHighlight();
            if (!TryGetCurrentPreviewBounds(out Vector2 center, out Vector2 size))
            {
                previewHighlight.SetActive(false);
                return;
            }

            RectTransform rect = previewHighlight.GetComponent<RectTransform>();
            rect.anchoredPosition = center;
            rect.sizeDelta = size + new Vector2(22f, 22f);
            previewHighlight.SetActive(true);
            previewHighlight.transform.SetAsLastSibling();
        }

        private void EnsurePreviewHighlight()
        {
            if (previewHighlight != null)
            {
                return;
            }

            previewHighlight = new GameObject("CurrentPartPreviewHighlight");
            previewHighlight.transform.SetParent(previewRoot, false);
            Image image = previewHighlight.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 0.84f, 0.18f, 0.08f);
            Outline outline = previewHighlight.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.72f, 0.05f, 0.88f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private bool TryGetCurrentPreviewBounds(out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero;
            size = Vector2.zero;

            if (!drawings.TryGetValue(currentPart, out PartDrawing drawing) || drawing.Points.Count == 0)
            {
                Vector2 anchor = ToPreviewPoint(currentPart, Vector2.zero);
                center = anchor;
                size = new Vector2(42f, 42f);
                return true;
            }

            bool found = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            IReadOnlyList<Vector2> points = drawing.Points;
            for (int i = 0; i < points.Count; i++)
            {
                if (IsBreakPoint(points[i]))
                {
                    continue;
                }

                Vector2 previewPoint = ToPreviewPoint(currentPart, points[i]);
                min = Vector2.Min(min, previewPoint);
                max = Vector2.Max(max, previewPoint);
                found = true;
            }

            if (!found)
            {
                return false;
            }

            center = (min + max) * 0.5f;
            size = new Vector2(Mathf.Max(34f, max.x - min.x), Mathf.Max(34f, max.y - min.y));
            return true;
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
