using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Layouts;
#endif

namespace DrawBody.Prototype
{
    /// <summary>
    /// Owns local device pairing independently from the online connection.
    /// Each connection is capped at two local game players. Online rooms may
    /// still contain four game players across multiple connections. Camera layout depends
    /// only on this roster, so opening DRAW never changes any viewport.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class LocalCoopSessionManager : MonoBehaviour
    {
        private StageManager stageManager;
        private readonly List<Camera> splitCameras = new List<Camera>(2);
        private readonly Dictionary<PlayerController2D, DrawManager> drawSessions =
            new Dictionary<PlayerController2D, DrawManager>();
        private readonly HashSet<int> disconnectedPlayerIndices = new HashSet<int>();
        private Camera sourceCamera;
        private CameraFollow2D sourceCameraFollow;
        private Transform sourceFollowTargetBeforeSplit;
        private bool sourceFrameGroupBeforeSplit;
        private bool sourceFollowStateCaptured;
        private DrawManager sourceDrawManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private float sourceViewportScale = 1f;
        private float nextDeviceJoinAt;
        private Vector2 legacyMenuDirectionHeld;
        private float nextLegacyMenuMoveAt;
        private readonly Dictionary<LocalPlayerInputBinding, float> leaveHoldStartedAt =
            new Dictionary<LocalPlayerInputBinding, float>();
        private bool waitForLeaveButtonRelease;
        private Canvas controllerCursorCanvas;
        private RectTransform controllerCursorRect;
        private Vector2 controllerCursorPosition;
        private GameObject controllerCursorTarget;
        private readonly List<RaycastResult> controllerUiRaycasts = new List<RaycastResult>(16);
        private PointerEventData controllerPointerData;
        private const float ControllerCursorSpeed = 920f;
        private Canvas localHudCanvas;
        private readonly List<GameObject> localHudRoots = new List<GameObject>(2);
        private readonly List<Text> localHudControlTexts = new List<Text>(2);
        private readonly List<Text> localHudActionTexts = new List<Text>(2);
        private readonly List<GameObject> localHudNoticeCards = new List<GameObject>(2);
        private readonly List<Text> localHudNoticeTexts = new List<Text>(2);
        private readonly List<Coroutine> localHudNoticeRoutines = new List<Coroutine>(2);
        private readonly List<GameObject> splitDividerObjects = new List<GameObject>(2);
        private GameObject originalGameplayControlHint;
        private GameObject originalGameplayKeyHints;
        private GameObject localRosterRoot;
        private Text localRosterCountText;
        private Text localRosterHintText;
        private int lastRosterPlayerCount = -1;
        private string lastRosterLanguage = string.Empty;
#if ENABLE_INPUT_SYSTEM
        private static bool controllerLayoutsRegistered;
#endif

        public int LocalPlayerCount => stageManager != null ? stageManager.LocalPlayers.Count : 0;

        public bool HasDedicatedSecondaryPlayers
        {
            get
            {
                if (stageManager == null) return false;
                IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
                for (int i = 1; i < players.Count; i++)
                {
                    LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                    if (binding != null && binding.Kind != LocalPlayerInputBinding.DeviceKind.AutoPrimary)
                        return true;
                }
                return false;
            }
        }

        public bool AnyDrawingSessionActive
        {
            get
            {
                foreach (KeyValuePair<PlayerController2D, DrawManager> pair in drawSessions)
                    if (pair.Value != null && pair.Value.IsActive) return true;
                return false;
            }
        }

        public bool AnyLocalMenuPressed()
        {
            if (stageManager == null) return false;
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            // Only P1 owns shared menus. P2's START/OPTIONS input must never
            // interrupt both players' gameplay.
            return players.Count > 0 && players[0]?.InputBinding != null
                && players[0].InputBinding.MenuPressed();
        }

        public bool AnyLocalCancelPressed()
        {
            if (stageManager == null) return false;
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            return players.Count > 0 && players[0]?.InputBinding != null
                && players[0].InputBinding.CancelPressed();
        }

        public bool IsJoinCommandThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i].startButton.wasPressedThisFrame
                    && !IsGamepadAssigned(Gamepad.all[i])) return true;
#endif
            // Input System pads also appear in the legacy joystick array on
            // Windows. Do not interpret one physical OPTIONS/START press twice.
            if (Gamepad.all.Count == 0)
            {
                string[] legacyJoysticks = Input.GetJoystickNames();
                for (int i = 0; i < Mathf.Min(4, legacyJoysticks.Length); i++)
                    if (!string.IsNullOrWhiteSpace(legacyJoysticks[i])
                        && WasLegacyJoystickButtonPressed(i + 1, 9)
                        && !IsLegacyJoystickAssigned(i + 1)) return true;
            }
            return false;
        }

        public bool IsLeaveCommandThisFrame()
        {
            if (stageManager == null) return false;
            for (int i = 1; i < stageManager.LocalPlayers.Count; i++)
            {
                LocalPlayerInputBinding binding = stageManager.LocalPlayers[i] != null
                    ? stageManager.LocalPlayers[i].InputBinding
                    : null;
                if (binding != null && binding.LeaveChordPressed()) return true;
            }
            return false;
        }

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureControllerLayouts();
#endif
            stageManager = GetComponent<StageManager>();
            sourceCamera = Camera.main;
            sourceDrawManager = FindFirstObjectByType<DrawManager>();
            onlineManager = FindFirstObjectByType<OnlineManager>();
            uiManager = FindFirstObjectByType<UIManager>();
        }

        private void Start()
        {
            EnsurePrimaryBinding();
            RefreshSplitScreen();
            EnsureLocalRosterDisplay();
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            UpdateGameplayInputSuppression();
            DetectPrimaryGamepadIntent();
            MonitorAssignedDevices();
            HandleDeviceJoining();
            HandleDrawingSessions();
            HandleMenuNavigation();
#endif
            UpdateLocalRosterDisplay();
            UpdateLocalPlayerHud();
            if (splitCameras.Count != RequiredCameraCount()) RefreshSplitScreen();
        }

        private void UpdateGameplayInputSuppression()
        {
            if (stageManager == null) return;
            bool outsideStage = !stageManager.IsGameplayActive && !stageManager.IsDrawingMode;
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (IsPlayerDrawing(players[i])) continue;
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding == null) continue;
                bool isController = binding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad
                    || binding.Kind == LocalPlayerInputBinding.DeviceKind.LegacyJoystick;
                // Outside a stage, P1's pad is dedicated to UI navigation.
                // P2 cannot operate UI, so it remains free to move its
                // title-background characters.
                binding.SetGameplayInputSuppressed(outsideStage && isController && i == 0);
            }
        }

        private void EnsureLocalRosterDisplay()
        {
            if (localRosterRoot != null) return;
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
            RectTransform uiRoot = uiManager != null ? uiManager.transform as RectTransform : null;
            if (uiRoot == null) return;

            localRosterRoot = new GameObject("Local Join Status", typeof(RectTransform),
                typeof(Image), typeof(Outline));
            localRosterRoot.transform.SetParent(uiRoot, false);
            RectTransform root = localRosterRoot.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            // The upper-left paper margin is common to title, lobby and stage
            // select, and does not cover their primary controls.
            root.anchoredPosition = new Vector2(24f, -24f);
            root.sizeDelta = new Vector2(292f, 54f);
            root.localRotation = Quaternion.Euler(0f, 0f, -0.35f);
            localRosterRoot.GetComponent<Image>().color = new Color(1f, 0.975f, 0.84f, 0.96f);
            Outline rootOutline = localRosterRoot.GetComponent<Outline>();
            rootOutline.effectColor = new Color(0.08f, 0.26f, 0.34f, 0.82f);
            rootOutline.effectDistance = new Vector2(2f, -2f);

            Text existingText = Object.FindFirstObjectByType<Text>();
            Font font = existingText != null
                ? existingText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject iconObject = new GameObject("Local Group Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = Vector2.zero;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(25f, 27f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(1);
            icon.preserveAspect = true;
            icon.color = new Color(0.04f, 0.18f, 0.24f, 0.96f);
            icon.raycastTarget = false;

            localRosterCountText = CreateRosterText(root, font, "Player Count",
                new Vector2(69f, 27f), new Vector2(66f, 36f), 21, TextAnchor.MiddleCenter);
            localRosterCountText.fontStyle = FontStyle.Bold;
            localRosterHintText = CreateRosterText(root, font, "Join Hint",
                new Vector2(190f, 27f), new Vector2(174f, 38f), 13, TextAnchor.MiddleLeft);
            localRosterHintText.color = new Color(0.035f, 0.12f, 0.16f, 0.92f);
            localRosterRoot.transform.SetAsLastSibling();
        }

        private static Text CreateRosterText(RectTransform parent, Font font, string name,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.08f, 0.22f, 0.31f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private void UpdateLocalRosterDisplay()
        {
            EnsureLocalRosterDisplay();
            if (localRosterRoot == null || stageManager == null) return;
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
            bool visible = uiManager != null
                ? uiManager.ShouldShowLocalRoster
                : stageManager.IsTitleMode;
            if (localRosterRoot.activeSelf != visible) localRosterRoot.SetActive(visible);
            if (!visible || localRosterHintText == null) return;
            localRosterRoot.transform.SetAsLastSibling();

            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            int joinedCount = 0;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && binding.Kind != LocalPlayerInputBinding.DeviceKind.AutoPrimary)
                    joinedCount++;
            }
            string language = LocalizationManager.CurrentLanguageCode;
            if (joinedCount == lastRosterPlayerCount && language == lastRosterLanguage) return;
            lastRosterPlayerCount = joinedCount;
            lastRosterLanguage = language;
            if (localRosterCountText != null) localRosterCountText.text = $"{joinedCount}/2";
            localRosterHintText.text = LocalizationManager.T(joinedCount < 2
                ? "local_roster_compact_join"
                : "local_roster_compact_full");
            Font localizedFont = LocalizationManager.LoadCurrentFont(localRosterHintText.font);
            localRosterHintText.font = localizedFont;
            if (localRosterCountText != null) localRosterCountText.font = localizedFont;
        }

        private void LateUpdate()
        {
            if (sourceCamera == null) sourceCamera = Camera.main;
            if (sourceCamera == null) return;
            float baseOrthographicSize = sourceCamera.orthographicSize / Mathf.Max(0.01f, sourceViewportScale);
            float fullAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            for (int i = 0; i < splitCameras.Count; i++)
            {
                Camera camera = splitCameras[i];
                if (camera == null) continue;
                CameraFollow2D localFollow = camera.GetComponent<CameraFollow2D>();
                if (camera != sourceCamera && (localFollow == null || !localFollow.enabled))
                    camera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                float viewportAspect = fullAspect * camera.rect.width / Mathf.Max(0.01f, camera.rect.height);
                float preserveWidthScale = Mathf.Max(1f, fullAspect / Mathf.Max(0.01f, viewportAspect));
                camera.orthographicSize = baseOrthographicSize * preserveWidthScale;
                camera.backgroundColor = sourceCamera.backgroundColor;
                if (camera == sourceCamera) sourceViewportScale = preserveWidthScale;
            }
        }

        private void EnsurePrimaryBinding()
        {
            if (stageManager == null || stageManager.LocalPlayers.Count == 0) return;
            PlayerController2D primary = stageManager.LocalPlayers[0];
            LocalPlayerInputBinding binding = primary.InputBinding;
            if (binding == null) binding = primary.gameObject.AddComponent<LocalPlayerInputBinding>();
            binding.BindKeyboardMouse(0);
        }

#if ENABLE_INPUT_SYSTEM
        private void DetectPrimaryGamepadIntent()
        {
            if (stageManager == null || stageManager.LocalPlayers.Count != 1)
                return;
            PlayerController2D primaryPlayer = stageManager.LocalPlayers[0];
            LocalPlayerInputBinding primary = primaryPlayer != null ? primaryPlayer.InputBinding : null;
            if (primary == null || primary.Kind != LocalPlayerInputBinding.DeviceKind.AutoPrimary) return;

            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (pad == null || IsGamepadAssigned(pad) || !pad.startButton.wasPressedThisFrame) continue;
                primary.BindGamepad(0, pad);
                stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                    "local_player_joined", 1, primary.DeviceDisplayName));
                nextDeviceJoinAt = Time.unscaledTime + 0.2f;
                return;
            }

            string[] legacyJoysticks = Gamepad.all.Count == 0
                ? Input.GetJoystickNames()
                : System.Array.Empty<string>();
            for (int i = 0; i < Mathf.Min(4, legacyJoysticks.Length); i++)
            {
                int joystickNumber = i + 1;
                if (string.IsNullOrWhiteSpace(legacyJoysticks[i])
                    || IsLegacyJoystickAssigned(joystickNumber)
                    || !WasLegacyJoystickButtonPressed(joystickNumber, 9)) continue;
                primary.BindLegacyJoystick(0, joystickNumber);
                stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                    "local_primary_device_claimed", primary.DeviceDisplayName));
                nextDeviceJoinAt = Time.unscaledTime + 0.2f;
                return;
            }
        }

        private static bool HasOrdinaryGamepadIntent(Gamepad pad)
        {
            if (pad == null) return false;
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.16f
                || pad.rightStick.ReadValue().sqrMagnitude > 0.16f
                || pad.dpad.ReadValue().sqrMagnitude > 0f) return true;
            return pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
                || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
        }

        private void HandleDeviceJoining()
        {
            if (stageManager == null) return;
            if (Time.unscaledTime < nextDeviceJoinAt) return;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (!pad.startButton.wasPressedThisFrame || IsGamepadAssigned(pad)) continue;

                if (TryReconnectDisconnectedSlot(pad))
                {
                    nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                    return;
                }

                if (stageManager.IsGameplayActive)
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T(
                        "local_roster_locked_during_stage"));
                    nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                    continue;
                }
                if (!CanAddAnotherLocalPlayer())
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T("local_player_limit_reached"));
                    nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                    continue;
                }
                PlayerController2D joined = stageManager.AddLocalCharacter();
                if (joined == null) continue;
                LocalPlayerInputBinding binding = joined.InputBinding;
                if (binding == null) binding = joined.gameObject.AddComponent<LocalPlayerInputBinding>();
                binding.BindGamepad(stageManager.LocalPlayers.Count - 1, pad);
                joined.SetControlsEnabled(stageManager.ShouldEnableLocalCharacterControls);
                stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                    "local_player_joined", stageManager.LocalPlayers.Count, binding.DeviceDisplayName));
                nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                RefreshSplitScreen();
                return;
            }

            string[] legacyJoysticks = Gamepad.all.Count == 0
                ? Input.GetJoystickNames()
                : System.Array.Empty<string>();
            for (int i = 0; i < Mathf.Min(4, legacyJoysticks.Length); i++)
            {
                int joystickNumber = i + 1;
                if (string.IsNullOrWhiteSpace(legacyJoysticks[i])
                    || IsLegacyJoystickAssigned(joystickNumber)
                    || !WasLegacyJoystickButtonPressed(joystickNumber, 9)) continue;
                if (stageManager.IsGameplayActive)
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T(
                        "local_roster_locked_during_stage"));
                    nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                    continue;
                }
                if (!CanAddAnotherLocalPlayer())
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T("local_player_limit_reached"));
                    nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                    continue;
                }
                PlayerController2D joined = stageManager.AddLocalCharacter();
                if (joined == null) continue;
                LocalPlayerInputBinding binding = joined.InputBinding;
                if (binding == null) binding = joined.gameObject.AddComponent<LocalPlayerInputBinding>();
                binding.BindLegacyJoystick(stageManager.LocalPlayers.Count - 1, joystickNumber);
                joined.SetControlsEnabled(stageManager.ShouldEnableLocalCharacterControls);
                stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                    "local_player_joined", stageManager.LocalPlayers.Count, binding.DeviceDisplayName));
                Debug.Log($"[LOCAL INPUT] Legacy joystick {joystickNumber} joined as P{stageManager.LocalPlayers.Count}: {legacyJoysticks[i]}");
                nextDeviceJoinAt = Time.unscaledTime + 0.35f;
                RefreshSplitScreen();
                return;
            }

            // A keyboard can join after P1 was claimed by a gamepad.
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame
                && !IsKeyboardMouseAssigned() && !IsTypingInInputField())
            {
                PlayerController2D primaryPlayer = stageManager.LocalPlayers.Count > 0
                    ? stageManager.LocalPlayers[0]
                    : null;
                LocalPlayerInputBinding primaryBinding = primaryPlayer != null
                    ? primaryPlayer.InputBinding
                    : null;
                if (primaryBinding != null
                    && primaryBinding.Kind == LocalPlayerInputBinding.DeviceKind.AutoPrimary)
                {
                    primaryBinding.BindKeyboardMouse(0);
                    stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                        "local_player_joined", 1, primaryBinding.DeviceDisplayName));
                    nextDeviceJoinAt = Time.unscaledTime + 0.2f;
                    return;
                }
                if (stageManager.IsGameplayActive)
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T(
                        "local_roster_locked_during_stage"));
                    return;
                }
                if (!CanAddAnotherLocalPlayer())
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T("local_player_limit_reached"));
                    return;
                }
                PlayerController2D joined = stageManager.AddLocalCharacter();
                if (joined != null)
                {
                    LocalPlayerInputBinding binding = joined.InputBinding;
                    if (binding == null) binding = joined.gameObject.AddComponent<LocalPlayerInputBinding>();
                    binding.BindKeyboardMouse(stageManager.LocalPlayers.Count - 1);
                    joined.SetControlsEnabled(stageManager.ShouldEnableLocalCharacterControls);
                    stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                        "local_player_joined", stageManager.LocalPlayers.Count, binding.DeviceDisplayName));
                    RefreshSplitScreen();
                }
            }

            // Keyboard and mouse use the same explicit join/leave rules as a pad.
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame
                && !stageManager.IsGameplayActive && !IsTypingInInputField())
            {
                for (int i = stageManager.LocalPlayers.Count - 1; i >= 0; i--)
                {
                    PlayerController2D local = stageManager.LocalPlayers[i];
                    LocalPlayerInputBinding binding = local != null
                        ? local.InputBinding
                        : null;
                    if (binding == null || binding.Kind != LocalPlayerInputBinding.DeviceKind.KeyboardMouse)
                        continue;
                    RemoveJoinedDevice(i);
                    return;
                }
            }

            HandleControllerLeaving();
        }

        private void HandleControllerLeaving()
        {
            if (stageManager == null) return;
            bool anyCancelHeld = false;
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && (binding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad
                    || binding.Kind == LocalPlayerInputBinding.DeviceKind.LegacyJoystick)
                    && binding.CancelHeld())
                {
                    anyCancelHeld = true;
                    break;
                }
            }
            if (waitForLeaveButtonRelease)
            {
                if (!anyCancelHeld) waitForLeaveButtonRelease = false;
                return;
            }

            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (IsPlayerDrawing(players[i]))
                {
                    LocalPlayerInputBinding drawingBinding = players[i] != null
                        ? players[i].InputBinding
                        : null;
                    if (drawingBinding != null) leaveHoldStartedAt.Remove(drawingBinding);
                    continue;
                }
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding == null || binding.Kind != LocalPlayerInputBinding.DeviceKind.Gamepad
                    && binding.Kind != LocalPlayerInputBinding.DeviceKind.LegacyJoystick) continue;
                if (!binding.CancelHeld())
                {
                    leaveHoldStartedAt.Remove(binding);
                    continue;
                }
                if (!leaveHoldStartedAt.TryGetValue(binding, out float startedAt))
                {
                    leaveHoldStartedAt[binding] = Time.unscaledTime;
                    continue;
                }
                if (Time.unscaledTime - startedAt < 1f) continue;
                if (stageManager.IsGameplayActive)
                {
                    stageManager.ShowLocalCoopNotice(LocalizationManager.T(
                        "local_roster_locked_during_stage"));
                    leaveHoldStartedAt.Remove(binding);
                    waitForLeaveButtonRelease = true;
                    return;
                }
                RemoveJoinedDevice(i);
                leaveHoldStartedAt.Clear();
                waitForLeaveButtonRelease = true;
                return;
            }
        }

        private void RemoveJoinedDevice(int playerIndex)
        {
            if (stageManager == null || playerIndex < 0
                || playerIndex >= stageManager.LocalPlayers.Count) return;
            int oldPlayerNumber = playerIndex + 1;
            if (playerIndex > 0)
            {
                stageManager.RemoveLocalCharacter(stageManager.LocalPlayers[playerIndex]);
            }
            else if (stageManager.LocalPlayers.Count > 1)
            {
                PlayerController2D donorPlayer = stageManager.LocalPlayers[stageManager.LocalPlayers.Count - 1];
                LocalPlayerInputBinding donor = donorPlayer != null ? donorPlayer.InputBinding : null;
                LocalPlayerInputBinding primary = stageManager.LocalPlayers[0].InputBinding;
                if (donor != null && primary != null)
                {
                    if (donor.Kind == LocalPlayerInputBinding.DeviceKind.KeyboardMouse)
                        primary.BindKeyboardMouse(0);
                    else if (donor.Kind == LocalPlayerInputBinding.DeviceKind.LegacyJoystick)
                        primary.BindLegacyJoystick(0, donor.LegacyJoystickNumber);
                    else if (donor.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad)
                    {
                        Gamepad pad = FindGamepad(donor.GamepadDeviceId);
                        if (pad != null) primary.BindGamepad(0, pad);
                        else primary.BindAutoPrimary();
                    }
                    stageManager.RemoveLocalCharacter(donorPlayer);
                }
            }
            else
            {
                stageManager.LocalPlayers[0].InputBinding?.BindAutoPrimary();
            }
            disconnectedPlayerIndices.Clear();
            stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                "local_player_left", oldPlayerNumber));
            lastRosterPlayerCount = -1;
            RefreshSplitScreen();
        }

        private bool TryReconnectDisconnectedSlot(Gamepad pad)
        {
            if (pad == null || stageManager == null || disconnectedPlayerIndices.Count == 0)
                return false;

            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (!disconnectedPlayerIndices.Contains(i)) continue;
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding == null || binding.Kind != LocalPlayerInputBinding.DeviceKind.Gamepad
                    || binding.IsAssignedGamepadConnected()) continue;
                binding.BindGamepad(i, pad);
                disconnectedPlayerIndices.Remove(i);
                onlineManager?.SetLocalReady(false);
                stageManager.ShowLocalCoopNotice(LocalizationManager.Format(
                    "local_gamepad_reconnected", i + 1));
                return true;
            }
            return false;
        }

        private bool CanAddAnotherLocalPlayer()
        {
            if (stageManager == null
                || stageManager.LocalPlayers.Count >= StageManager.MaxLocalPlayersPerConnection)
                return false;
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            int totalPlayers = stageManager.LocalPlayers.Count;
            if (lobby?.Players != null)
            {
                string localConnectionId = onlineManager.LocalPlayerId;
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    OnlinePlayerInfo candidate = lobby.Players[i];
                    if (candidate == null || string.IsNullOrEmpty(candidate.PlayerId)
                        || OnlinePlayerIdentity.IsOwnedByConnection(
                            candidate.PlayerId, localConnectionId)) continue;
                    totalPlayers++;
                }
            }
            int limit = lobby != null ? Mathf.Clamp(lobby.MaxPlayers, 1, 4) : 4;
            return totalPlayers < limit;
        }

        private void MonitorAssignedDevices()
        {
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding == null || binding.Kind != LocalPlayerInputBinding.DeviceKind.Gamepad
                    && binding.Kind != LocalPlayerInputBinding.DeviceKind.LegacyJoystick) continue;
                bool connected = binding.IsAssignedGamepadConnected();
                if (!connected && disconnectedPlayerIndices.Add(i))
                {
                    onlineManager?.SetLocalReady(false);
                    stageManager.ShowLocalCoopNotice(LocalizationManager.Format("local_gamepad_disconnected", i + 1));
                }
                else if (connected && disconnectedPlayerIndices.Remove(i))
                    stageManager.ShowLocalCoopNotice(LocalizationManager.Format("local_gamepad_reconnected", i + 1));
            }
        }

        private bool IsGamepadAssigned(Gamepad gamepad)
        {
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && binding.MatchesGamepad(gamepad)) return true;
            }
            return false;
        }

        private bool IsLegacyJoystickAssigned(int joystickNumber)
        {
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && binding.MatchesLegacyJoystick(joystickNumber)) return true;
            }
            return false;
        }

        private bool IsKeyboardMouseAssigned()
        {
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && binding.Kind == LocalPlayerInputBinding.DeviceKind.KeyboardMouse) return true;
            }
            return false;
        }

        private static bool IsTypingInInputField()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }

        private static Gamepad FindGamepad(int deviceId)
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i].deviceId == deviceId) return Gamepad.all[i];
            return null;
        }

        private static bool WasAnyGamepadButtonPressed(Gamepad gamepad)
        {
            if (gamepad == null) return false;
            for (int i = 0; i < gamepad.allControls.Count; i++)
            {
                UnityEngine.InputSystem.Controls.ButtonControl button =
                    gamepad.allControls[i] as UnityEngine.InputSystem.Controls.ButtonControl;
                if (button != null && !button.synthetic && button.wasPressedThisFrame) return true;
            }
            return false;
        }

        private static bool WasAnyLegacyJoystickButtonPressed(int joystickNumber)
        {
            if (joystickNumber < 1 || joystickNumber > 4) return false;
            int first = (int)KeyCode.Joystick1Button0 + (joystickNumber - 1) * 20;
            for (int button = 0; button < 20; button++)
                if (Input.GetKeyDown((KeyCode)(first + button))) return true;
            return false;
        }

        private static bool WasLegacyJoystickButtonPressed(int joystickNumber, int button)
        {
            if (joystickNumber < 1 || joystickNumber > 4 || button < 0 || button >= 20)
                return false;
            int key = (int)KeyCode.Joystick1Button0
                + (joystickNumber - 1) * 20 + button;
            return Input.GetKeyDown((KeyCode)key);
        }

        private static void EnsureControllerLayouts()
        {
            if (controllerLayoutsRegistered) return;
            controllerLayoutsRegistered = true;

            // Some Windows/Steam/driver combinations omit Sony's VID/PID and
            // present DualSense only by product name. Unity then falls back to a
            // generic Joystick, which is absent from Gamepad.all.
            InputSystem.RegisterLayoutMatcher<DualSenseGamepadHID>(
                new InputDeviceMatcher().WithInterface("HID").WithProduct(".*DualSense.*"));

            List<InputDeviceDescription> rematch = new List<InputDeviceDescription>();
            for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
            {
                InputDevice device = InputSystem.devices[i];
                if (device is Gamepad) continue;
                string product = device.description.product ?? string.Empty;
                string display = device.displayName ?? string.Empty;
                if (product.IndexOf("DualSense", System.StringComparison.OrdinalIgnoreCase) < 0
                    && display.IndexOf("DualSense", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                rematch.Add(device.description);
                InputSystem.RemoveDevice(device);
            }
            for (int i = 0; i < rematch.Count; i++) InputSystem.AddDevice(rematch[i]);

            string detected = string.Empty;
            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                InputDevice device = InputSystem.devices[i];
                string product = device.description.product ?? string.Empty;
                if (!(device is Gamepad) && !(device is Joystick)
                    && product.IndexOf("DualSense", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (detected.Length > 0) detected += ", ";
                detected += $"{device.displayName} [{device.layout}] id={device.deviceId}";
            }
            Debug.Log("[LOCAL INPUT] Controllers: " + (detected.Length > 0 ? detected : "none"));
        }

        private void HandleMenuNavigation()
        {
            if (EventSystem.current == null || stageManager == null) return;
            EventSystem.current.sendNavigationEvents = false;
            PlayerController2D primaryPlayer = stageManager.LocalPlayers.Count > 0
                ? stageManager.LocalPlayers[0]
                : null;
            bool drawingUi = primaryPlayer != null && IsPlayerDrawing(primaryPlayer)
                || stageManager.IsDrawingMode && stageManager.ActivePlayerController == primaryPlayer;
            // Every DRAW session owns its own free cursor. Keeping the global
            // P1 cursor active here would also make it appear when keyboard P2
            // opens DRAW.
            bool uiAvailable = (!stageManager.ShouldEnableLocalGameplayControls
                || stageManager.IsTitleMode) && !drawingUi;
            if (!uiAvailable || stageManager.LocalPlayers.Count == 0)
            {
                SetControllerCursorVisible(false);
                return;
            }

            LocalPlayerInputBinding primaryBinding = stageManager.LocalPlayers[0] != null
                ? stageManager.LocalPlayers[0].InputBinding
                : null;
            bool controllerPrimary = primaryBinding != null
                && (primaryBinding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad
                    || primaryBinding.Kind == LocalPlayerInputBinding.DeviceKind.LegacyJoystick);
            if (!controllerPrimary)
            {
                SetControllerCursorVisible(false);
                return;
            }

            EnsureControllerCursor();
            SetControllerCursorVisible(true);
            Vector2 movement = primaryBinding.ReadUiCursor(drawingUi);
            controllerCursorPosition += movement * (ControllerCursorSpeed * Time.unscaledDeltaTime);
            controllerCursorPosition.x = Mathf.Clamp(controllerCursorPosition.x, 8f, Screen.width - 8f);
            controllerCursorPosition.y = Mathf.Clamp(controllerCursorPosition.y, 8f, Screen.height - 8f);
            controllerCursorRect.position = controllerCursorPosition;
            RefreshControllerCursorTarget();

            if (primaryBinding.ConfirmPressed() && controllerCursorTarget != null)
            {
                controllerPointerData.button = PointerEventData.InputButton.Left;
                ExecuteEvents.Execute(controllerCursorTarget, controllerPointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(controllerCursorTarget, controllerPointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(controllerCursorTarget, controllerPointerData, ExecuteEvents.pointerClickHandler);
            }

            if (primaryBinding.CancelPressed() && !stageManager.IsGameplayActive)
            {
                UIManager ui = Object.FindFirstObjectByType<UIManager>();
                bool handled = ui != null && ui.TryHandleTitleGamepadBack();
                if (!handled && ui != null && ui.IsTitleSubmenuShowing)
                {
                    if (!TryInvokeVisibleBackButton()) stageManager.CloseTitleSubmenu();
                    handled = true;
                }
                if (handled) EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void EnsureControllerCursor()
        {
            if (controllerCursorCanvas != null) return;
            GameObject canvasObject = new GameObject("P1 Controller UI Cursor", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            controllerCursorCanvas = canvasObject.GetComponent<Canvas>();
            controllerCursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            controllerCursorCanvas.sortingOrder = short.MaxValue;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(Image), typeof(Outline));
            cursorObject.transform.SetParent(canvasObject.transform, false);
            controllerCursorRect = cursorObject.GetComponent<RectTransform>();
            controllerCursorRect.anchorMin = controllerCursorRect.anchorMax = Vector2.zero;
            controllerCursorRect.pivot = new Vector2(0.15f, 0.85f);
            controllerCursorRect.sizeDelta = new Vector2(24f, 30f);
            controllerCursorRect.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Image image = cursorObject.GetComponent<Image>();
            image.sprite = CreateControllerCursorSprite();
            image.color = new Color(0.18f, 0.7f, 0.95f, 1f);
            image.raycastTarget = false;
            Outline outline = cursorObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.12f, 0.2f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            controllerCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            controllerCursorRect.position = controllerCursorPosition;
        }

        private static Sprite CreateControllerCursorSprite()
        {
            const int width = 24;
            const int height = 30;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Controller Cursor Crayon",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color white = Color.white;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool arrowHead = y >= 8 && x <= Mathf.RoundToInt((height - y) * 0.72f);
                    bool stem = y < 15 && x >= 5 && x <= 10;
                    texture.SetPixel(x, y, arrowHead || stem ? white : clear);
                }
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.12f, 0.88f), 100f);
        }

        private void SetControllerCursorVisible(bool visible)
        {
            if (controllerCursorCanvas == null) return;
            if (controllerCursorCanvas.gameObject.activeSelf != visible)
                controllerCursorCanvas.gameObject.SetActive(visible);
            if (!visible) SetControllerCursorTarget(null);
        }

        private void RefreshControllerCursorTarget()
        {
            if (EventSystem.current == null) return;
            if (controllerPointerData == null) controllerPointerData = new PointerEventData(EventSystem.current);
            controllerPointerData.position = controllerCursorPosition;
            controllerUiRaycasts.Clear();
            EventSystem.current.RaycastAll(controllerPointerData, controllerUiRaycasts);
            GameObject target = null;
            if (controllerUiRaycasts.Count > 0)
            {
                GameObject hit = controllerUiRaycasts[0].gameObject;
                Selectable selectable = hit != null ? hit.GetComponentInParent<Selectable>() : null;
                if (IsUsableSelectable(selectable)) target = selectable.gameObject;
            }
            SetControllerCursorTarget(target);
        }

        private void SetControllerCursorTarget(GameObject target)
        {
            if (controllerCursorTarget == target) return;
            if (controllerCursorTarget != null && controllerPointerData != null)
                ExecuteEvents.Execute(controllerCursorTarget, controllerPointerData, ExecuteEvents.pointerExitHandler);
            controllerCursorTarget = target;
            if (controllerCursorTarget != null && controllerPointerData != null)
                ExecuteEvents.Execute(controllerCursorTarget, controllerPointerData, ExecuteEvents.pointerEnterHandler);
        }

        private static bool IsUsableSelectable(Selectable selectable)
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy
                || !selectable.IsActive() || !selectable.IsInteractable()) return false;
            CanvasGroup[] groups = selectable.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
                if (!groups[i].interactable || groups[i].alpha <= 0.01f) return false;
            return true;
        }

        private static Selectable FindInitialSelectable()
        {
            GameObject localButton = GameObject.Find("TitleSingleButton");
            Selectable localSelectable = localButton != null
                ? localButton.GetComponent<Selectable>()
                : null;
            if (IsUsableSelectable(localSelectable)) return localSelectable;

            Selectable[] all = Object.FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Selectable best = null;
            Vector2 bestPosition = new Vector2(float.PositiveInfinity, float.NegativeInfinity);
            for (int i = 0; i < all.Length; i++)
            {
                Selectable candidate = all[i];
                if (!IsUsableSelectable(candidate)) continue;
                Vector2 position = candidate.transform.position;
                if (best == null || position.y > bestPosition.y + 1f
                    || Mathf.Abs(position.y - bestPosition.y) <= 1f && position.x < bestPosition.x)
                {
                    best = candidate;
                    bestPosition = position;
                }
            }
            return best;
        }

        private static Selectable FindSpatialSelectable(Selectable current, Vector2 direction)
        {
            Selectable[] all = Object.FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector2 origin = current.transform.position;
            Selectable best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < all.Length; i++)
            {
                Selectable candidate = all[i];
                if (candidate == current || !IsUsableSelectable(candidate)) continue;
                Vector2 delta = (Vector2)candidate.transform.position - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 1f) continue;
                float sideways = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
                float score = forward + sideways * 2.5f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private static bool TryInvokeVisibleBackButton()
        {
            Button[] buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Button best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (!IsUsableSelectable(candidate)) continue;
                string name = candidate.name;
                bool explicitNo = name.EndsWith("No", System.StringComparison.OrdinalIgnoreCase);
                bool cancel = name.IndexOf("Cancel", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool back = name.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!explicitNo && !cancel && !back) continue;
                int priority = explicitNo ? 300 : cancel ? 200 : 100;
                priority += GetTopLevelSiblingIndex(candidate.transform);
                if (priority <= bestPriority) continue;
                bestPriority = priority;
                best = candidate;
            }
            if (best == null) return false;
            best.onClick.Invoke();
            return true;
        }

        private static int GetTopLevelSiblingIndex(Transform target)
        {
            if (target == null) return 0;
            Transform current = target;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;
            while (current.parent != null && current.parent != canvasTransform)
                current = current.parent;
            return current.GetSiblingIndex();
        }
#endif

        private void HandleDrawingSessions()
        {
            if (stageManager == null) return;
            if (!HasDedicatedSecondaryPlayers)
            {
                RestoreSinglePlayerDrawing();
                return;
            }
            EnsureDrawSessions();
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController2D local = players[i];
                if (local == null || !drawSessions.TryGetValue(local, out DrawManager session) || session == null) continue;
                LocalPlayerInputBinding binding = local.InputBinding;
                bool keyboardDrawToggle = binding != null && binding.UsesLegacyKeyboardMouse
                    && Input.GetKeyDown(KeyCode.Tab);
                if (keyboardDrawToggle || binding != null && binding.DrawTogglePressed()) ToggleDrawing(local);
                if (session.IsActive)
                {
                    local.SetControlsEnabled(false);
                    bool keyboardCancel = binding != null && binding.UsesLegacyKeyboardMouse
                        && Input.GetKeyDown(KeyCode.Escape);
                    if (keyboardCancel)
                    {
                        session.CancelEditing();
                        stageManager.CancelIndependentLocalRedraw(local);
                        session.SetActive(false);
                    }
                }
                else if (binding != null
                    && binding.Kind != LocalPlayerInputBinding.DeviceKind.AutoPrimary)
                {
                    // Dedicated local players are not the StageManager's active
                    // player, so every non-gameplay screen must explicitly keep
                    // them disabled as well as re-enable them for gameplay.
                    local.SetControlsEnabled(stageManager.ShouldEnableLocalCharacterControls);
                }
            }
        }

        public void ToggleDrawing(PlayerController2D localPlayer)
        {
            if (localPlayer == null || stageManager == null || !HasDedicatedSecondaryPlayers) return;
            EnsureDrawSessions();
            if (!drawSessions.TryGetValue(localPlayer, out DrawManager session) || session == null) return;
            if (session.IsActive)
            {
                session.CancelEditing();
                stageManager.CancelIndependentLocalRedraw(localPlayer);
                session.SetActive(false);
            }
            else
            {
                if (!stageManager.CanPlayerEnterDrawingMode(localPlayer)) return;
                localPlayer.ResetMotion();
                localPlayer.SetControlsEnabled(false);
                stageManager.BeginIndependentLocalRedraw(localPlayer);
                session.SetAllowedSpecies(StageSpeciesRules.GetAllowedForStage(stageManager.CurrentStageId));
                session.SetActive(true);
            }
        }

        public bool IsPlayerDrawing(PlayerController2D localPlayer)
        {
            return localPlayer != null && drawSessions.TryGetValue(localPlayer, out DrawManager session)
                && session != null && session.IsActive;
        }

        public bool TryGetLocalDrawSessionInk(PlayerController2D localPlayer, out float ink)
        {
            ink = 0f;
            if (localPlayer == null
                || !drawSessions.TryGetValue(localPlayer, out DrawManager session)
                || session == null) return false;
            ink = Mathf.Max(0f, session.UsedInk);
            return true;
        }

        public bool TryGetDrawingState(
            PlayerController2D localPlayer,
            out DrawManager.DrawingState state)
        {
            state = null;
            if (localPlayer == null
                || !drawSessions.TryGetValue(localPlayer, out DrawManager session)
                || session == null) return false;
            state = session.CreateState();
            return state != null;
        }

        public bool CompleteSpeciesSwapForPlayer(PlayerController2D localPlayer)
        {
            if (localPlayer == null
                || !drawSessions.TryGetValue(localPlayer, out DrawManager session)
                || session == null) return false;
            session.ShowSpeciesSwapResult(true);
            if (session.IsActive) session.ConfirmDrawing();
            return true;
        }

        public bool ShowSpeciesSwapFailure(PlayerController2D localPlayer)
        {
            if (localPlayer == null
                || !drawSessions.TryGetValue(localPlayer, out DrawManager session)
                || session == null) return false;
            session.ShowSpeciesSwapResult(false);
            return true;
        }

        public void CancelAllDrawingSessions()
        {
            if (drawSessions.Count == 0) return;
            foreach (KeyValuePair<PlayerController2D, DrawManager> pair in drawSessions)
            {
                DrawManager session = pair.Value;
                if (session == null || !session.IsActive) continue;
                session.CancelEditing();
                stageManager?.CancelIndependentLocalRedraw(pair.Key);
                session.SetActive(false);
            }
        }

        public void SendAllLocalBodies()
        {
            if (stageManager == null) return;
            EnsureDrawSessions();
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController2D local = players[i];
                if (local != null && drawSessions.TryGetValue(local, out DrawManager session) && session != null)
                {
                    // An active session contains a temporary, not-yet-fit-checked
                    // body. It must never replace the last confirmed network body.
                    if (!session.IsActive) session.SendCurrentBodyData();
                }
            }
        }

        public bool SendLocalBody(string gamePlayerId, string connectionId)
        {
            if (stageManager == null || string.IsNullOrEmpty(gamePlayerId)
                || !OnlinePlayerIdentity.IsOwnedByConnection(gamePlayerId, connectionId)) return false;
            EnsureDrawSessions();
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (OnlinePlayerIdentity.Compose(connectionId, i) != gamePlayerId) continue;
                PlayerController2D local = players[i];
                if (local == null || !drawSessions.TryGetValue(local, out DrawManager session) || session == null)
                    return false;
                if (session.IsActive) return true;
                session.SendCurrentBodyData();
                return true;
            }
            return false;
        }

        private void EnsureDrawSessions()
        {
            if (sourceDrawManager == null) sourceDrawManager = FindFirstObjectByType<DrawManager>();
            if (sourceDrawManager == null || stageManager == null) return;
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController2D local = players[i];
                if (local == null || drawSessions.ContainsKey(local)) continue;
                DrawManager.DrawingState initial = stageManager.GetPlayerDrawingStateSnapshot(local)
                    ?? sourceDrawManager.CreateState();
                DrawManager session;
                if (i == 0)
                {
                    session = sourceDrawManager;
                    session.ConfigureIndependentLocalSession(local, true);
                    session.LoadState(initial, true);
                }
                else
                {
                    session = sourceDrawManager.CreateIndependentLocalClone(local, initial);
                }
                if (session != null) drawSessions.Add(local, session);
            }

            List<PlayerController2D> removed = new List<PlayerController2D>();
            foreach (KeyValuePair<PlayerController2D, DrawManager> pair in drawSessions)
                if (pair.Key == null || !ContainsPlayer(players, pair.Key)) removed.Add(pair.Key);
            for (int i = 0; i < removed.Count; i++)
            {
                PlayerController2D key = removed[i];
                if (drawSessions.TryGetValue(key, out DrawManager session) && session != null && session != sourceDrawManager)
                    Destroy(session.gameObject);
                drawSessions.Remove(key);
            }

            for (int i = 0; i < players.Count; i++)
                if (drawSessions.TryGetValue(players[i], out DrawManager session) && session != null)
                {
                    session.SetLocalViewport(GetViewport(players.Count, i));
                    LocalPlayerInputBinding binding = players[i] != null
                        ? players[i].InputBinding
                        : null;
                    session.SetPointerInputEnabled(binding == null || binding.UsesLegacyKeyboardMouse);
                }
        }

        private void RestoreSinglePlayerDrawing()
        {
            if (sourceDrawManager == null) return;
            bool restoredFromSplit = drawSessions.Count > 0;
            if (restoredFromSplit)
            {
                CancelAllDrawingSessions();
                foreach (KeyValuePair<PlayerController2D, DrawManager> pair in drawSessions)
                {
                    DrawManager session = pair.Value;
                    if (session == null) continue;
                    session.SetActive(false);
                    if (session != sourceDrawManager) Destroy(session.gameObject);
                }
                drawSessions.Clear();
            }
            PlayerController2D primary = stageManager.LocalPlayers.Count > 0 ? stageManager.LocalPlayers[0] : null;
            if (restoredFromSplit)
            {
                sourceDrawManager.ConfigureIndependentLocalSession(primary, false);
                sourceDrawManager.SetLocalViewport(new Rect(0f, 0f, 1f, 1f));
            }
            LocalPlayerInputBinding primaryBinding = primary != null ? primary.InputBinding : null;
            sourceDrawManager.SetPointerInputEnabled(
                primaryBinding == null || primaryBinding.UsesLegacyKeyboardMouse);
        }

        private static bool ContainsPlayer(IReadOnlyList<PlayerController2D> players, PlayerController2D candidate)
        {
            for (int i = 0; i < players.Count; i++) if (players[i] == candidate) return true;
            return false;
        }

        private void UpdateLocalPlayerHud()
        {
            if (stageManager == null) return;
            ResolveOriginalGameplayHints();
            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            int count = stageManager.ShouldUseLocalSplitScreen
                ? Mathf.Clamp(players.Count, 1, StageManager.MaxLocalPlayersPerConnection)
                : 1;
            LocalPlayerInputBinding primaryBinding = players.Count > 0 && players[0] != null
                ? players[0].InputBinding
                : null;
            bool primaryUsesController = IsControllerBinding(primaryBinding);
            bool showCustom = stageManager.IsGameplayActive && (count > 1 || primaryUsesController);
            bool sharedOverlayShowing = uiManager != null
                && (uiManager.IsGameplayMenuShowing || uiManager.IsOptionShowing);
            if (originalGameplayControlHint != null) originalGameplayControlHint.SetActive(!showCustom);
            if (originalGameplayKeyHints != null) originalGameplayKeyHints.SetActive(!showCustom);
            if (!showCustom)
            {
                if (localHudCanvas != null) localHudCanvas.gameObject.SetActive(false);
                return;
            }

            EnsureLocalHudCanvas();
            localHudCanvas.gameObject.SetActive(true);
            EnsureLocalHudPlayerCount(count);
            if (sharedOverlayShowing)
            {
                // MENU and OPTION are shared P1 overlays, but the horizontal
                // split itself remains in effect. Hide only per-player hint
                // cards and keep the divider visible on both overlays.
                for (int i = 0; i < localHudRoots.Count; i++)
                    localHudRoots[i].SetActive(false);
                RefreshSplitDividers(count);
                return;
            }
            for (int i = 0; i < localHudRoots.Count; i++)
            {
                bool active = i < count && i < players.Count && !IsPlayerDrawing(players[i]);
                localHudRoots[i].SetActive(active);
                if (!active) continue;
                Rect viewport = GetViewport(count, i);
                RectTransform root = localHudRoots[i].GetComponent<RectTransform>();
                root.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
                root.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
                root.offsetMin = root.offsetMax = Vector2.zero;

                PlayerController2D local = players[i];
                LocalPlayerInputBinding binding = local != null ? local.InputBinding : null;
                DrawManager.Species species = DrawManager.Species.Human;
                PlayerAbilityController ability = local != null
                    ? local.GetComponent<PlayerAbilityController>()
                    : null;
                if (ability != null) species = ability.CurrentProfile.Species;
                bool controller = IsControllerBinding(binding);
                bool playStation = controller && binding != null && binding.UsesPlayStationGlyphs;
                string prefix = !controller
                    ? "ability_control_"
                    : playStation ? "ability_control_ps_" : "ability_control_xbox_";
                string control = $"P{i + 1}  " + LocalizationManager.T(
                    prefix + species.ToString().ToLowerInvariant());
                if (count == 1 && !stageManager.IsOnlineStageActive)
                {
                    control += "\n" + LocalizationManager.T(controller
                        ? playStation ? "gameplay_control_ps_switch" : "gameplay_control_xbox_switch"
                        : "gameplay_control_keyboard_switch");
                }
                localHudControlTexts[i].text = control;
                string actionsKey;
                if (i == 0)
                {
                    actionsKey = controller
                        ? playStation ? "gameplay_actions_ps" : "gameplay_actions_xbox"
                        : "gameplay_actions_keyboard";
                }
                else
                {
                    actionsKey = controller
                        ? playStation ? "gameplay_actions_ps_secondary" : "gameplay_actions_xbox_secondary"
                        : "gameplay_actions_keyboard_secondary";
                }
                localHudActionTexts[i].text = LocalizationManager.T(actionsKey);
                localHudControlTexts[i].font = LocalizationManager.LoadCurrentFont(localHudControlTexts[i].font);
                localHudActionTexts[i].font = LocalizationManager.LoadCurrentFont(localHudActionTexts[i].font);
            }
            RefreshSplitDividers(count);
        }

        private static bool IsControllerBinding(LocalPlayerInputBinding binding)
        {
            return binding != null && (binding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad
                || binding.Kind == LocalPlayerInputBinding.DeviceKind.LegacyJoystick);
        }

        private void ResolveOriginalGameplayHints()
        {
            if (originalGameplayControlHint != null && originalGameplayKeyHints != null) return;
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate.name == "GameplayControlHint")
                    originalGameplayControlHint = candidate.gameObject;
                else if (candidate.name == "GameplayKeyHints")
                    originalGameplayKeyHints = candidate.gameObject;
            }
        }

        private void EnsureLocalHudCanvas()
        {
            if (localHudCanvas != null) return;
            GameObject canvasObject = new GameObject("Local Player HUD", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            localHudCanvas = canvasObject.GetComponent<Canvas>();
            localHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            localHudCanvas.sortingOrder = 40;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;
        }

        private void EnsureLocalHudPlayerCount(int count)
        {
            Text sample = Object.FindFirstObjectByType<Text>();
            Font font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (localHudRoots.Count < count)
            {
                int index = localHudRoots.Count;
                GameObject root = new GameObject($"P{index + 1} HUD", typeof(RectTransform));
                root.transform.SetParent(localHudCanvas.transform, false);
                Text control = CreateLocalHudCard(root.transform, font, "Controls",
                    new Vector2(14f, -14f), new Vector2(330f, 66f), true);
                Text actions = CreateLocalHudCard(root.transform, font, "Actions",
                    new Vector2(14f, 14f), new Vector2(330f, 54f), false);
                localHudRoots.Add(root);
                localHudControlTexts.Add(control);
                localHudActionTexts.Add(actions);
                Text notice = CreateLocalHudNotice(root.transform, font);
                localHudNoticeCards.Add(notice.transform.parent.gameObject);
                localHudNoticeTexts.Add(notice);
                localHudNoticeRoutines.Add(null);
            }
        }

        public bool ShowLocalPlayerNotice(
            PlayerController2D targetPlayer,
            string message,
            float seconds = 2.4f)
        {
            if (stageManager == null || targetPlayer == null
                || stageManager.LocalPlayers.Count < 2) return false;
            int index = stageManager.GetLocalPlayerIndex(targetPlayer);
            if (index < 0 || index >= StageManager.MaxLocalPlayersPerConnection) return false;
            EnsureLocalHudCanvas();
            EnsureLocalHudPlayerCount(stageManager.LocalPlayers.Count);
            if (index >= localHudNoticeTexts.Count) return false;

            if (localHudNoticeRoutines[index] != null)
                StopCoroutine(localHudNoticeRoutines[index]);
            Text label = localHudNoticeTexts[index];
            label.font = LocalizationManager.LoadCurrentFont(label.font);
            label.text = message ?? string.Empty;
            localHudNoticeCards[index].SetActive(true);
            localHudNoticeCards[index].transform.SetAsLastSibling();
            localHudNoticeRoutines[index] = StartCoroutine(
                HideLocalPlayerNoticeAfter(index, Mathf.Max(0.5f, seconds)));
            return true;
        }

        private IEnumerator HideLocalPlayerNoticeAfter(int index, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (index >= 0 && index < localHudNoticeCards.Count
                && localHudNoticeCards[index] != null)
                localHudNoticeCards[index].SetActive(false);
            if (index >= 0 && index < localHudNoticeRoutines.Count)
                localHudNoticeRoutines[index] = null;
        }

        private static Text CreateLocalHudNotice(Transform parent, Font font)
        {
            GameObject card = new GameObject("Viewport Notice", typeof(RectTransform),
                typeof(Image), typeof(Shadow));
            card.transform.SetParent(parent, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -14f);
            rect.sizeDelta = new Vector2(720f, 58f);
            Image paper = card.GetComponent<Image>();
            DoodlePaperUi.Apply(paper, new Color(1f, 0.91f, 0.72f, 0.97f));
            paper.raycastTarget = false;
            Shadow shadow = card.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.1f, 0.08f, 0.05f, 0.22f);
            shadow.effectDistance = new Vector2(4f, -4f);

            GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(card.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 23;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 23;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.25f, 0.12f, 0.08f, 1f);
            text.raycastTarget = false;
            card.SetActive(false);
            return text;
        }

        private static Text CreateLocalHudCard(Transform parent, Font font, string name,
            Vector2 position, Vector2 size, bool top)
        {
            GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(parent, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = top ? new Vector2(0f, 1f) : Vector2.zero;
            rect.pivot = top ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = card.GetComponent<Image>();
            DoodlePaperUi.Apply(image, new Color(1f, 0.97f, 0.84f, 0.86f));
            image.raycastTarget = false;
            Outline outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.23f, 0.28f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(card.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.08f, 0.12f, 0.15f, 0.96f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private void RefreshSplitDividers(int count)
        {
            int needed = count == 2 ? 1 : 0;
            while (splitDividerObjects.Count < needed)
            {
                GameObject divider = new GameObject("Crayon Split Divider", typeof(RectTransform),
                    typeof(Image), typeof(Outline));
                divider.transform.SetParent(localHudCanvas.transform, false);
                Image image = divider.GetComponent<Image>();
                image.color = new Color(0.14f, 0.55f, 0.78f, 0.98f);
                image.raycastTarget = false;
                Outline outline = divider.GetComponent<Outline>();
                outline.effectColor = new Color(0.04f, 0.13f, 0.2f, 0.92f);
                outline.effectDistance = new Vector2(2f, -2f);
                splitDividerObjects.Add(divider);
            }
            for (int i = 0; i < splitDividerObjects.Count; i++)
                splitDividerObjects[i].SetActive(i < needed);
            if (needed == 0) return;
            ConfigureDivider(splitDividerObjects[0].GetComponent<RectTransform>(), true);
            for (int i = 0; i < needed; i++) splitDividerObjects[i].transform.SetAsLastSibling();
        }

        private static void ConfigureDivider(RectTransform rect, bool horizontal)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = horizontal ? new Vector2(Screen.width, 8f) : new Vector2(8f, Screen.height);
        }

        public void RefreshSplitScreen()
        {
            if (sourceCamera == null) sourceCamera = Camera.main;
            if (sourceCamera == null || stageManager == null) return;
            int count = stageManager.ShouldUseLocalSplitScreen
                ? Mathf.Clamp(stageManager.LocalPlayers.Count, 1, StageManager.MaxLocalPlayersPerConnection)
                : 1;
            int cameraCount = count;

            while (splitCameras.Count > cameraCount)
            {
                Camera last = splitCameras[splitCameras.Count - 1];
                splitCameras.RemoveAt(splitCameras.Count - 1);
                if (last != null && last != sourceCamera) Destroy(last.gameObject);
            }
            if (splitCameras.Count == 0) splitCameras.Add(sourceCamera);
            while (splitCameras.Count < cameraCount) splitCameras.Add(CreateSplitCamera(splitCameras.Count));

            for (int i = 0; i < splitCameras.Count; i++)
            {
                Camera camera = splitCameras[i];
                if (camera == null) continue;
                if (camera != sourceCamera)
                {
                    camera.CopyFrom(sourceCamera);
                }
                camera.rect = GetViewport(count, i);
                camera.enabled = true;
            }
            ConfigureSplitCameraFollowers(count);
            if (count > 1) EnsureDrawSessions();
            else RestoreSinglePlayerDrawing();
        }

        private void ConfigureSplitCameraFollowers(int count)
        {
            if (sourceCamera == null || stageManager == null) return;
            if (sourceCameraFollow == null)
                sourceCameraFollow = sourceCamera.GetComponent<CameraFollow2D>();

            if (count <= 1)
            {
                if (sourceCameraFollow != null && sourceFollowStateCaptured)
                {
                    sourceCameraFollow.SetTarget(sourceFollowTargetBeforeSplit);
                    sourceCameraFollow.SetFrameAllActivePlayers(sourceFrameGroupBeforeSplit);
                }
                sourceFollowStateCaptured = false;
                return;
            }

            IReadOnlyList<PlayerController2D> players = stageManager.LocalPlayers;
            if (players.Count < count || sourceCameraFollow == null) return;
            if (!sourceFollowStateCaptured)
            {
                sourceFollowTargetBeforeSplit = sourceCameraFollow.Target;
                sourceFrameGroupBeforeSplit = sourceCameraFollow.FramesAllActivePlayers;
                sourceFollowStateCaptured = true;
            }

            // Each half-screen follows its own local player. Group framing is
            // disabled only while split, otherwise a distant partner makes both
            // cameras zoom out and appear to show the same view.
            sourceCameraFollow.SetTarget(players[0] != null ? players[0].transform : null);
            sourceCameraFollow.SetFrameAllActivePlayers(false);
            for (int i = 1; i < splitCameras.Count && i < players.Count; i++)
            {
                Camera camera = splitCameras[i];
                if (camera == null) continue;
                CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
                if (follow == null) follow = camera.gameObject.AddComponent<CameraFollow2D>();
                follow.CopySettingsFrom(
                    sourceCameraFollow,
                    players[i] != null ? players[i].transform : null,
                    false);
                follow.enabled = sourceCameraFollow.enabled;
            }
        }

        private int RequiredCameraCount()
        {
            int count = stageManager != null && stageManager.ShouldUseLocalSplitScreen
                ? Mathf.Clamp(LocalPlayerCount, 1, StageManager.MaxLocalPlayersPerConnection)
                : 1;
            return count;
        }

        private Camera CreateSplitCamera(int index)
        {
            GameObject cameraObject = new GameObject($"Local Split Camera P{index + 1}");
            cameraObject.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(sourceCamera);
            camera.depth = sourceCamera.depth;
            return camera;
        }

        private static Rect GetViewport(int playerCount, int index)
        {
            if (playerCount <= 1) return new Rect(0f, 0f, 1f, 1f);
            if (playerCount == 2) return index == 0
                ? new Rect(0f, 0.5f, 1f, 0.5f)
                : new Rect(0f, 0f, 1f, 0.5f);
            return new Rect(0f, 0f, 1f, 1f);
        }
    }
}
