using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject drawingHintPanel;
        [SerializeField] private GameObject clearPanel;
        [SerializeField] private GameObject gameplayHudPanel;
        [SerializeField] private GameplayHudDrawer gameplayHudDrawer;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private SlidingMenuPanel menuDrawer;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject multiPanel;
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private GameObject stageEditorPanel;
        [SerializeField] private Text statusText;

        private StageManager stageManager;
        private Text clearTitleText;
        private Text clearStageText;
        private Text clearNextLabel;
        private Text clearBackLabel;
        private GameObject stageSelectLockedPanel;
        private GameObject leaveSessionConfirmPanel;
        private Button clearNextButton;
        private Button clearBackButton;
        private Button editorTestReturnButton;
        private Button gameplayLeaveSessionButton;
        private Text editorTestReturnLabel;
        private RectTransform clearStamp;
        private readonly Image[] clearBurstLines = new Image[16];
        private bool drawing;
        private bool cleared;
        private bool titleShowing;
        private bool multiShowing;
        private bool optionShowing;
        private bool stageSelecting;
        private bool stageEditing;
        private bool stageSelectLocked;
        private bool titleVisibleBeforeDrawing;
        private bool multiVisibleBeforeDrawing;
        private bool optionVisibleBeforeDrawing;
        private bool optionReturnToGameplayMenu;
        private float clearAnimTime;
        private string clearStageId;
        private string clearNextStageId;
        private GameObject challengeHud;
        private Text challengeTimerText;
        private Text challengeProgressText;
        private GameObject challengeCountdownOverlay;
        private Text challengeCountdownText;

        private void Awake()
        {
            stageManager = FindObjectOfType<StageManager>();
            ResolveGameplayDrawer();
            ResolveMenuDrawer();
            EnsureClearPanel();
            EnsureEditorTestReturnButton();
            RefreshGameplayMenu();
            DoodleUiDirector uiDirector = GetComponent<DoodleUiDirector>();
            if (uiDirector == null)
            {
                uiDirector = gameObject.AddComponent<DoodleUiDirector>();
            }

            uiDirector.ApplyTheme();
        }

        public void SetChallengeHud(
            bool visible,
            float remainingSeconds,
            StageObjectType targetType,
            int collected,
            int required,
            bool failed)
        {
            EnsureChallengeHud();
            challengeHud.SetActive(false);
        }

        public void SetChallengeCountdown(bool visible, string value)
        {
            EnsureChallengeCountdownOverlay();
            challengeCountdownOverlay.SetActive(visible);
            if (!visible)
            {
                return;
            }

            challengeCountdownText.text = value ?? string.Empty;
            bool timeUp = string.Equals(value, "TIME UP", System.StringComparison.Ordinal);
            bool start = string.Equals(value, "START!", System.StringComparison.Ordinal);
            challengeCountdownText.color = timeUp
                ? new Color(1f, 0.2f, 0.12f, 1f)
                : start
                    ? new Color(0.16f, 0.86f, 1f, 1f)
                    : new Color(1f, 0.76f, 0.08f, 1f);
            challengeCountdownOverlay.transform.SetAsLastSibling();
        }

        private void EnsureChallengeCountdownOverlay()
        {
            if (challengeCountdownOverlay != null)
            {
                return;
            }

            challengeCountdownOverlay = new GameObject(
                "ChallengeCountdownOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            challengeCountdownOverlay.transform.SetParent(transform, false);
            RectTransform root = challengeCountdownOverlay.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 36f);
            root.sizeDelta = new Vector2(720f, 240f);

            GameObject textObject = new GameObject(
                "CountdownText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(root, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            challengeCountdownText = textObject.GetComponent<Text>();
            challengeCountdownText.font = statusText != null && statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            challengeCountdownText.fontSize = 132;
            challengeCountdownText.fontStyle = FontStyle.Bold;
            challengeCountdownText.alignment = TextAnchor.MiddleCenter;
            challengeCountdownText.resizeTextForBestFit = true;
            challengeCountdownText.resizeTextMinSize = 72;
            challengeCountdownText.resizeTextMaxSize = 132;
            challengeCountdownText.raycastTarget = false;
            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.035f, 0.08f, 0.13f, 0.92f);
            outline.effectDistance = new Vector2(5f, -5f);
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
            shadow.effectDistance = new Vector2(8f, -9f);
            challengeCountdownOverlay.SetActive(false);
        }

        private void EnsureChallengeHud()
        {
            if (challengeHud != null)
            {
                return;
            }

            challengeHud = new GameObject("ChallengeHud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            challengeHud.transform.SetParent(transform, false);
            RectTransform rect = challengeHud.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(300f, 44f);
            Image background = challengeHud.GetComponent<Image>();
            background.color = new Color(1f, 0.97f, 0.78f, 0.96f);
            Outline outline = challengeHud.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.3f, 0.48f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            Font font = statusText != null && statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            challengeTimerText = CreateChallengeText("Timer", rect, font, 27, new Vector2(0f, -6f), new Vector2(370f, 38f));
            challengeTimerText.fontStyle = FontStyle.Bold;
            challengeProgressText = CreateChallengeText("Progress", rect, font, 19, new Vector2(0f, -7f), new Vector2(286f, 30f));
            challengeHud.SetActive(false);
        }

        private static Text CreateChallengeText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            Vector2 position,
            Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.05f, 0.16f, 0.25f, 1f);
            return text;
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshText;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshText;
        }

        private void Update()
        {
            if (!cleared || clearStamp == null)
            {
                return;
            }

            clearAnimTime += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(clearAnimTime * 5.5f) * 0.035f;
            clearStamp.localScale = Vector3.one * pulse;

            for (int i = 0; i < clearBurstLines.Length; i++)
            {
                Image line = clearBurstLines[i];
                if (line == null)
                {
                    continue;
                }

                float angle = (360f / clearBurstLines.Length) * i + Mathf.Sin(clearAnimTime * 2f + i) * 4f;
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public void SetDrawing(bool drawing)
        {
            if (this.drawing == drawing)
            {
                return;
            }

            if (drawing)
            {
                titleVisibleBeforeDrawing = titleShowing;
                multiVisibleBeforeDrawing = multiShowing;
                optionVisibleBeforeDrawing = optionShowing;
                SetPanelActive(titlePanel, false);
                SetPanelActive(multiPanel, false);
                SetPanelActive(optionPanel, false);
            }

            this.drawing = drawing;
            if (drawingHintPanel != null)
            {
                drawingHintPanel.SetActive(drawing);
            }

            // The full theme pass walks every UI element and is already applied
            // during Awake/Start and language changes. Re-running it on every
            // redraw click made opening the DRAW screen noticeably stall.

            if (!drawing)
            {
                SetPanelActive(titlePanel, titleVisibleBeforeDrawing && !multiVisibleBeforeDrawing && !optionVisibleBeforeDrawing);
                SetPanelActive(multiPanel, multiVisibleBeforeDrawing);
                SetPanelActive(optionPanel, optionVisibleBeforeDrawing);
            }

            RefreshHudVisibility();
            RefreshText();
        }

        public void ToggleMenu()
        {
            if (stageSelecting || titleShowing)
            {
                return;
            }

            if (menuPanel == null)
            {
                return;
            }

            ResolveMenuDrawer();
            bool menuOpen = menuDrawer != null ? menuDrawer.IsOpen : menuPanel.activeSelf;
            if (menuOpen)
            {
                if (menuDrawer != null)
                {
                    menuDrawer.Close();
                }
                else
                {
                    menuPanel.SetActive(false);
                }
            }
            else
            {
                RefreshGameplayMenu();
                gameplayHudDrawer?.Close();
                if (menuDrawer != null)
                {
                    menuDrawer.Open();
                }
                else
                {
                    menuPanel.SetActive(true);
                }
            }

            RefreshHudVisibility();
        }

        /// <summary>
        /// Keyboard escape must feel immediate.  In particular, do not let a
        /// second press reverse an opening slide before the panel is visible.
        /// Mouse/TAB driven drawers keep their normal animated behaviour.
        /// </summary>
        public void ToggleMenuFromEscape()
        {
            if (stageSelecting || titleShowing || menuPanel == null)
            {
                return;
            }

            ResolveMenuDrawer();
            bool menuOpen = menuDrawer != null
                ? menuDrawer.IsOpen
                : menuPanel.activeSelf;
            if (menuOpen)
            {
                HideMenu();
                return;
            }

            RefreshGameplayMenu();
            gameplayHudDrawer?.Close();
            if (menuDrawer != null)
            {
                menuDrawer.OpenImmediate();
            }
            else
            {
                menuPanel.SetActive(true);
                menuPanel.transform.SetAsLastSibling();
            }

            RefreshHudVisibility();
        }

        private void RefreshGameplayMenu()
        {
            if (menuPanel == null)
            {
                return;
            }

            bool online = ResolveStageManager() != null && ResolveStageManager().IsOnlineStageActive;
            bool host = online && ResolveStageManager().IsOnlineStageHost;
            Transform exit = menuPanel.transform.Find("MenuExitButton");
            if (exit != null)
            {
                exit.gameObject.SetActive(false);
            }

            Transform destination = menuPanel.transform.Find("MenuTitleButton");
            if (destination != null)
            {
                destination.gameObject.SetActive(!online || host);
                GameplayButtonCommand command = destination.GetComponent<GameplayButtonCommand>();
                command?.Configure(GameplayButtonCommand.Command.StageSelect);
                Text label = destination.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = LocalizationManager.T("menu_stage_select");
                }
            }

            EnsureGameplayLeaveSessionButton();
            if (gameplayLeaveSessionButton != null)
            {
                gameplayLeaveSessionButton.gameObject.SetActive(online);
                Text label = gameplayLeaveSessionButton.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = LocalizationManager.T("menu_leave_session");
                }
            }

            Transform retry = menuPanel.transform.Find("MenuRetryButton");
            if (retry != null)
            {
                retry.gameObject.SetActive(!online || host);
                Text label = retry.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = LocalizationManager.T(online ? "menu_restart_stage" : "retry");
                }
            }
        }

        private void EnsureGameplayLeaveSessionButton()
        {
            if (gameplayLeaveSessionButton != null || menuPanel == null)
            {
                return;
            }

            Transform existing = menuPanel.transform.Find("MenuLeaveSessionButton");
            if (existing != null)
            {
                gameplayLeaveSessionButton = existing.GetComponent<Button>();
            }

            if (gameplayLeaveSessionButton == null)
            {
                Font font = statusText != null && statusText.font != null
                    ? statusText.font
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                gameplayLeaveSessionButton = CreateClearButton(
                    "MenuLeaveSessionButton",
                    menuPanel.transform,
                    font,
                    new Vector2(0f, 58f),
                    new Vector2(250f, 48f),
                    new Color(1f, 0.72f, 0.66f, 0.94f));
                RectTransform rect = gameplayLeaveSessionButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                Text label = gameplayLeaveSessionButton.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 22;
                }
            }

            GameplayButtonCommand command = gameplayLeaveSessionButton.GetComponent<GameplayButtonCommand>();
            if (command == null)
            {
                command = gameplayLeaveSessionButton.gameObject.AddComponent<GameplayButtonCommand>();
            }
            command.Configure(GameplayButtonCommand.Command.LeaveSession);
        }

        public void ShowLeaveSessionConfirm(bool host)
        {
            EnsureLeaveSessionConfirmPanel();
            if (leaveSessionConfirmPanel == null)
            {
                return;
            }

            Text message = leaveSessionConfirmPanel.transform.Find("LeaveSessionMessage")?.GetComponent<Text>();
            if (message != null)
            {
                message.text = LocalizationManager.T(host
                    ? "menu_leave_session_host_confirm"
                    : "menu_leave_session_confirm");
            }
            leaveSessionConfirmPanel.SetActive(true);
            leaveSessionConfirmPanel.transform.SetAsLastSibling();
        }

        public void HideLeaveSessionConfirm()
        {
            if (leaveSessionConfirmPanel != null)
            {
                leaveSessionConfirmPanel.SetActive(false);
            }
        }

        private void EnsureLeaveSessionConfirmPanel()
        {
            if (leaveSessionConfirmPanel != null)
            {
                return;
            }

            Font font = statusText != null && statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            leaveSessionConfirmPanel = new GameObject("LeaveSessionConfirmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            leaveSessionConfirmPanel.transform.SetParent(transform, false);
            RectTransform rect = leaveSessionConfirmPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 250f);
            Image paper = leaveSessionConfirmPanel.GetComponent<Image>();
            paper.color = new Color(0.985f, 0.975f, 0.925f, 1f);
            Outline outline = leaveSessionConfirmPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.07f, 0.11f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            Text message = CreateClearText(
                "LeaveSessionMessage",
                leaveSessionConfirmPanel.transform,
                font,
                24,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 48f),
                new Vector2(450f, 90f));
            message.color = new Color(0.04f, 0.07f, 0.11f, 1f);
            message.fontStyle = FontStyle.Bold;

            Button leave = CreateClearButton(
                "LeaveSessionConfirmButton",
                leaveSessionConfirmPanel.transform,
                font,
                new Vector2(-120f, -66f),
                new Vector2(210f, 64f),
                new Color(1f, 0.42f, 0.32f, 1f));
            leave.GetComponentInChildren<Text>().text = LocalizationManager.T("menu_leave_session_yes");
            leave.onClick.AddListener(() => ResolveStageManager()?.ConfirmLeaveSession());

            Button cancel = CreateClearButton(
                "LeaveSessionCancelButton",
                leaveSessionConfirmPanel.transform,
                font,
                new Vector2(120f, -66f),
                new Vector2(180f, 64f),
                new Color(0.72f, 0.88f, 0.96f, 1f));
            cancel.GetComponentInChildren<Text>().text = LocalizationManager.T("cancel");
            cancel.onClick.AddListener(HideLeaveSessionConfirm);
            leaveSessionConfirmPanel.SetActive(false);
        }

        public void HideMenu()
        {
            if (menuPanel != null)
            {
                ResolveMenuDrawer();
                if (menuDrawer != null)
                {
                    menuDrawer.CloseImmediate();
                }
                else
                {
                    menuPanel.SetActive(false);
                }
            }

            RefreshHudVisibility();
        }

        public void ToggleGameplayHudDrawer()
        {
            if (gameplayHudPanel == null || !gameplayHudPanel.activeSelf)
            {
                return;
            }

            ResolveGameplayDrawer();
            gameplayHudDrawer?.Toggle();
        }

        public void SetStageSelect(bool selecting)
        {
            stageSelecting = selecting;
            if (stageSelectPanel != null)
            {
                stageSelectPanel.SetActive(selecting);
                if (selecting)
                {
                    StageSelectVisualPolisher polisher = stageSelectPanel.GetComponent<StageSelectVisualPolisher>();
                    if (polisher == null)
                    {
                        polisher = stageSelectPanel.AddComponent<StageSelectVisualPolisher>();
                    }

                    polisher.Polish();
                    ApplyModernTheme();
                    ApplyStageSelectLockedState();
                    // ApplyModernTheme also touches panel Images. Restore the
                    // per-world paper colors after it, including the first frame
                    // entered from the title screen.
                    polisher.RefreshWorldCardColors();
                }
            }

            if (selecting)
            {
                SetTitle(false);
            }

            if (menuPanel != null && selecting)
            {
                menuPanel.SetActive(false);
            }

            RefreshHudVisibility();
        }

        public void SetStageSelectLocked(bool locked)
        {
            stageSelectLocked = locked;
            ApplyStageSelectLockedState();
        }

        private void ApplyStageSelectLockedState()
        {
            if (stageSelectPanel == null)
            {
                return;
            }

            Button[] buttons = stageSelectPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].interactable = !stageSelectLocked;
                }
            }

            EnsureStageSelectLockedPanel();
            if (stageSelectLockedPanel != null)
            {
                stageSelectLockedPanel.SetActive(stageSelectLocked && stageSelectPanel.activeInHierarchy);
            }
        }

        private void EnsureStageSelectLockedPanel()
        {
            if (stageSelectLockedPanel != null || stageSelectPanel == null)
            {
                return;
            }

            Font font = statusText != null && statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stageSelectLockedPanel = new GameObject("StageSelectLockedPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stageSelectLockedPanel.transform.SetParent(stageSelectPanel.transform, false);
            RectTransform rect = stageSelectLockedPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 84f);
            rect.sizeDelta = new Vector2(500f, 72f);
            stageSelectLockedPanel.GetComponent<Image>().color = new Color(0.96f, 0.93f, 0.82f, 0.92f);

            GameObject textObject = new GameObject("StageSelectLockedText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(stageSelectLockedPanel.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = LocalizationManager.T("multi_host_selecting_stage");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            stageSelectLockedPanel.SetActive(false);
        }

        public void SetTitle(bool showing)
        {
            titleShowing = showing;
            if (titlePanel != null)
            {
                titlePanel.SetActive(showing);
            }

            if (showing)
            {
                ApplyModernTheme();
            }

            if (!showing)
            {
                SetMulti(false);
                SetOption(false);
            }

            RefreshHudVisibility();
        }

        public void SetMulti(bool showing)
        {
            multiShowing = showing;
            if (multiPanel != null)
            {
                multiPanel.SetActive(showing);
            }

            if (titlePanel != null && titleShowing)
            {
                titlePanel.SetActive(!multiShowing && !optionShowing);
            }

            if (showing)
            {
                SetOption(false);
                ApplyModernTheme();
            }

            RefreshHudVisibility();
        }

        public void SetOption(bool showing)
        {
            optionShowing = showing;
            if (optionPanel != null)
            {
                optionPanel.SetActive(showing);
            }

            if (titlePanel != null && titleShowing)
            {
                titlePanel.SetActive(!multiShowing && !optionShowing);
            }

            if (showing)
            {
                SetMulti(false);
                ApplyModernTheme();
            }

            RefreshHudVisibility();
        }

        public void OpenOption(bool returnToGameplayMenu)
        {
            optionReturnToGameplayMenu = returnToGameplayMenu;
            SetOption(true);
        }

        public void CloseOption()
        {
            bool reopenGameplayMenu = optionShowing && optionReturnToGameplayMenu;
            optionReturnToGameplayMenu = false;
            SetOption(false);
            if (reopenGameplayMenu)
            {
                ToggleMenu();
            }
        }

        public bool IsTitleSubmenuShowing =>
            multiShowing && multiPanel != null && multiPanel.activeInHierarchy
            || optionShowing && optionPanel != null && optionPanel.activeInHierarchy;
        public bool IsGameplayOverlayShowing => drawing
            || optionShowing
            || menuPanel != null && menuPanel.activeInHierarchy
            || gameplayHudDrawer != null && gameplayHudDrawer.IsOpenOrTransitioning;

        public void SetStageEditor(bool editing)
        {
            stageEditing = editing;
            if (stageEditorPanel != null)
            {
                stageEditorPanel.SetActive(editing);
            }

            if (menuPanel != null && editing)
            {
                menuPanel.SetActive(false);
            }

            RefreshHudVisibility();
        }

        public void SetEditorTestMode(bool testing)
        {
            EnsureEditorTestReturnButton();
            if (editorTestReturnButton != null)
            {
                editorTestReturnButton.gameObject.SetActive(testing);
                if (testing)
                {
                    editorTestReturnButton.transform.SetAsLastSibling();
                }
            }

            if (clearNextButton != null)
            {
                clearNextButton.gameObject.SetActive(!testing);
            }

            if (clearBackButton != null)
            {
                RectTransform backRect = clearBackButton.GetComponent<RectTransform>();
                if (backRect != null)
                {
                    backRect.anchoredPosition = testing ? new Vector2(0f, -130f) : new Vector2(135f, -130f);
                }
            }

            RefreshClearPanelText();
            RefreshText();
        }

        public void SetCleared(bool cleared, string stageId = null, string nextStageId = null)
        {
            this.cleared = cleared;
            clearStageId = stageId;
            clearNextStageId = nextStageId;
            EnsureClearPanel();
            if (clearPanel != null)
            {
                clearPanel.SetActive(cleared);
                if (cleared)
                {
                    clearPanel.transform.SetAsLastSibling();
                    if (editorTestReturnButton != null && editorTestReturnButton.gameObject.activeSelf)
                    {
                        editorTestReturnButton.transform.SetAsLastSibling();
                    }
                    clearAnimTime = 0f;
                    RefreshClearPanelText();
                    ApplyModernTheme();
                }
            }

            RefreshHudVisibility();
            RefreshText();
        }

        private void RefreshHudVisibility()
        {
            ResolveGameplayDrawer();
            ResolveMenuDrawer();
            if (gameplayHudPanel != null)
            {
                bool menuShowing = menuDrawer != null ? menuDrawer.IsOpen : menuPanel != null && menuPanel.activeSelf;
                bool visible = !titleShowing && !multiShowing && !optionShowing && !stageSelecting && !stageEditing && !drawing && !cleared;
                gameplayHudPanel.SetActive(visible);
                if (!visible || menuShowing)
                {
                    gameplayHudDrawer?.Close();
                }
            }
        }

        private void ResolveGameplayDrawer()
        {
            if (gameplayHudDrawer == null && gameplayHudPanel != null)
            {
                gameplayHudDrawer = gameplayHudPanel.GetComponentInChildren<GameplayHudDrawer>(true);
            }
        }

        private void ResolveMenuDrawer()
        {
            if (menuDrawer == null && menuPanel != null)
            {
                menuDrawer = menuPanel.GetComponent<SlidingMenuPanel>();
                if (menuDrawer == null)
                {
                    menuDrawer = menuPanel.AddComponent<SlidingMenuPanel>();
                }
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private void ApplyModernTheme()
        {
            DoodleUiDirector director = GetComponent<DoodleUiDirector>();
            director?.RefreshDynamicTheme();
        }

        private void EnsureClearPanel()
        {
            if (clearPanel == null)
            {
                clearPanel = new GameObject("ClearPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                clearPanel.transform.SetParent(transform, false);
                clearPanel.SetActive(false);
            }

            Image dim = clearPanel.GetComponent<Image>();
            if (dim != null)
            {
                dim.color = new Color(0.035f, 0.055f, 0.09f, 0.78f);
            }

            Stretch(clearPanel.GetComponent<RectTransform>());

            Transform existing = clearPanel.transform.Find("StageClearResult");
            if (existing != null)
            {
                return;
            }

            for (int i = 0; i < clearPanel.transform.childCount; i++)
            {
                clearPanel.transform.GetChild(i).gameObject.SetActive(false);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject result = new GameObject("StageClearResult", typeof(RectTransform));
            result.transform.SetParent(clearPanel.transform, false);
            RectTransform resultRect = result.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0.5f, 0.5f);
            resultRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.anchoredPosition = Vector2.zero;
            resultRect.sizeDelta = new Vector2(760f, 430f);

            Image paper = result.AddComponent<Image>();
            paper.color = new Color(0.985f, 0.975f, 0.925f, 1f);
            Outline paperOutline = result.AddComponent<Outline>();
            paperOutline.effectColor = new Color(0.045f, 0.075f, 0.12f, 1f);
            paperOutline.effectDistance = new Vector2(4f, -4f);
            Shadow paperShadow = result.AddComponent<Shadow>();
            paperShadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
            paperShadow.effectDistance = new Vector2(12f, -14f);

            CreateClearBlock("TopAccent", result.transform, new Vector2(0f, 206f), new Vector2(760f, 18f), new Color(1f, 0.78f, 0.12f, 1f));
            CreateClearBlock("AccentCyan", result.transform, new Vector2(-300f, 165f), new Vector2(72f, 12f), new Color(0.12f, 0.72f, 0.88f, 1f));
            CreateClearBlock("AccentCoral", result.transform, new Vector2(300f, 165f), new Vector2(72f, 12f), new Color(1f, 0.34f, 0.26f, 1f));
            CreateClearBlock("Divider", result.transform, new Vector2(0f, -28f), new Vector2(600f, 3f), new Color(0.045f, 0.075f, 0.12f, 0.22f));

            clearStamp = CreateClearStamp(result.transform);

            clearTitleText = CreateClearText("ClearTitle", result.transform, font, 54, TextAnchor.MiddleCenter, new Vector2(0f, 62f), new Vector2(650f, 72f));
            clearTitleText.fontStyle = FontStyle.Bold;
            clearTitleText.color = new Color(0.035f, 0.065f, 0.11f, 1f);

            clearStageText = CreateClearText("ClearStage", result.transform, font, 25, TextAnchor.MiddleCenter, new Vector2(0f, 10f), new Vector2(620f, 38f));
            clearStageText.color = new Color(0.18f, 0.22f, 0.27f, 1f);

            Button next = CreateClearButton("NextStageButton", result.transform, font, new Vector2(-145f, -122f), new Vector2(240f, 76f), new Color(0.18f, 0.78f, 0.88f, 1f));
            clearNextButton = next;
            clearNextLabel = next.GetComponentInChildren<Text>();
            next.onClick.AddListener(() => ResolveStageManager()?.GoToNextStage());

            Button back = CreateClearButton("BackToStageSelectButton", result.transform, font, new Vector2(145f, -122f), new Vector2(240f, 76f), new Color(1f, 0.73f, 0.22f, 1f));
            clearBackButton = back;
            clearBackLabel = back.GetComponentInChildren<Text>();
            back.onClick.AddListener(HandleClearBack);

            RefreshClearPanelText();
        }

        private StageManager ResolveStageManager()
        {
            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            return stageManager;
        }

        private void HandleClearBack()
        {
            if (editorTestReturnButton != null && editorTestReturnButton.gameObject.activeSelf)
            {
                ResolveStageManager()?.ReturnToStageEditor();
                return;
            }

            ResolveStageManager()?.OpenStageSelect();
        }

        private void EnsureEditorTestReturnButton()
        {
            if (editorTestReturnButton != null)
            {
                return;
            }

            Transform existing = transform.Find("EditorTestReturnButton");
            if (existing != null)
            {
                editorTestReturnButton = existing.GetComponent<Button>();
                editorTestReturnLabel = existing.GetComponentInChildren<Text>(true);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            editorTestReturnButton = CreateClearButton(
                "EditorTestReturnButton",
                transform,
                font,
                Vector2.zero,
                new Vector2(260f, 62f),
                new Color(1f, 0.82f, 0.24f, 0.98f));
            RectTransform rect = editorTestReturnButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -24f);
            editorTestReturnLabel = editorTestReturnButton.GetComponentInChildren<Text>();
            editorTestReturnButton.onClick.AddListener(() => ResolveStageManager()?.ReturnToStageEditor());
            editorTestReturnButton.gameObject.SetActive(false);
        }

        private RectTransform CreateClearStamp(Transform parent)
        {
            GameObject stamp = new GameObject("ClearStamp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stamp.transform.SetParent(parent, false);
            RectTransform rect = stamp.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 142f);
            rect.sizeDelta = new Vector2(68f, 68f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image image = stamp.GetComponent<Image>();
            image.color = new Color(1f, 0.78f, 0.12f, 1f);
            Outline outline = stamp.AddComponent<Outline>();
            outline.effectColor = new Color(0.035f, 0.065f, 0.11f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            GameObject checkRoot = new GameObject("ClearCheck", typeof(RectTransform));
            checkRoot.transform.SetParent(stamp.transform, false);
            RectTransform checkRect = checkRoot.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(68f, 68f);
            checkRect.localRotation = Quaternion.Euler(0f, 0f, -45f);

            Color checkColor = new Color(0.035f, 0.065f, 0.11f, 1f);
            Image shortStroke = CreateClearBlock("CheckShort", checkRoot.transform, new Vector2(-10f, -3f), new Vector2(9f, 28f), checkColor);
            shortStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Image longStroke = CreateClearBlock("CheckLong", checkRoot.transform, new Vector2(10f, 3f), new Vector2(9f, 48f), checkColor);
            longStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return rect;
        }

        private static Image CreateClearBlock(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject block = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            block.transform.SetParent(parent, false);
            RectTransform rect = block.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = block.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private void CreateBurstLines(RectTransform root)
        {
            for (int i = 0; i < clearBurstLines.Length; i++)
            {
                GameObject line = new GameObject($"ClearBurst{i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                line.transform.SetParent(root, false);
                RectTransform rect = line.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(i % 2 == 0 ? 54f : 36f, 5f);
                rect.localRotation = Quaternion.Euler(0f, 0f, (360f / clearBurstLines.Length) * i);
                Image image = line.GetComponent<Image>();
                image.color = i % 3 == 0 ? new Color(0.96f, 0.22f, 0.18f, 0.8f) : new Color(0.12f, 0.47f, 1f, 0.72f);
                clearBurstLines[i] = image;
            }
        }

        private Text CreateClearText(string name, Transform parent, Font font, int size, TextAnchor anchor, Vector2 position, Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
            return text;
        }

        private Button CreateClearButton(string name, Transform parent, Font font, Vector2 position, Vector2 size, Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
            colors.disabledColor = new Color(0.65f, 0.63f, 0.56f, 0.55f);
            button.colors = colors;

            Text label = CreateClearText($"{name}Label", buttonObject.transform, font, 24, TextAnchor.MiddleCenter, Vector2.zero, size);
            label.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            return button;
        }

        private void CreateDoodleStar(Transform parent, Vector2 position, Color color)
        {
            CreateDoodleLine(parent, position, new Vector2(44f, 5f), color);
            CreateDoodleLine(parent, position, new Vector2(44f, 5f), color).transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
            CreateDoodleLine(parent, position, new Vector2(44f, 5f), color).transform.localRotation = Quaternion.Euler(0f, 0f, 120f);
        }

        private static GameObject CreateDoodleLine(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject line = new GameObject("ClearDoodleLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
            line.GetComponent<Image>().color = color;
            return line;
        }

        private void RefreshClearPanelText()
        {
            if (clearTitleText != null)
            {
                clearTitleText.text = LocalizationManager.T("stage_clear_title");
            }

            if (clearStageText != null)
            {
                clearStageText.text = string.IsNullOrEmpty(clearStageId)
                    ? LocalizationManager.T("stage_clear_body_generic")
                    : LocalizationManager.Format("stage_clear_body", clearStageId);
            }

            if (clearNextLabel != null)
            {
                clearNextLabel.text = string.IsNullOrEmpty(clearNextStageId)
                    ? LocalizationManager.T("stage_clear_all_done")
                    : LocalizationManager.Format("stage_clear_next", clearNextStageId);
            }

            if (clearBackLabel != null)
            {
                clearBackLabel.text = editorTestReturnButton != null && editorTestReturnButton.gameObject.activeSelf
                    ? LocalizationManager.T("stage_editor_return")
                    : LocalizationManager.T("stage_clear_back");
            }

            if (clearNextButton != null)
            {
                clearNextButton.interactable = !string.IsNullOrEmpty(clearNextStageId);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void RefreshText()
        {
            RefreshGameplayMenu();
            if (editorTestReturnLabel != null)
            {
                editorTestReturnLabel.text = LocalizationManager.T("stage_editor_return_esc");
            }

            RefreshClearPanelText();
            if (statusText == null)
            {
                return;
            }

            if (cleared)
            {
                statusText.text = string.Empty;
            }
            else if (drawing)
            {
                statusText.text = LocalizationManager.T("status_draw");
            }
            else
            {
                statusText.text = LocalizationManager.T("status_play");
            }
        }
    }
}
