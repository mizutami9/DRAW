using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DrawBody.Prototype
{
    /// <summary>
    /// A device-scoped input source for one local avatar. The primary player may
    /// use AutoPrimary while the lobby is not locked; additional local players
    /// are always bound to one concrete gamepad so one pad can never drive two
    /// characters. Legacy keyboard/mouse input remains available through the
    /// existing code path while Active Input Handling is set to Both.
    /// </summary>
    public sealed class LocalPlayerInputBinding : MonoBehaviour
    {
        public enum DeviceKind
        {
            AutoPrimary,
            KeyboardMouse,
            Gamepad,
            LegacyJoystick
        }

        [SerializeField] private int localPlayerIndex;
        [SerializeField] private DeviceKind deviceKind = DeviceKind.AutoPrimary;
        [SerializeField] private int gamepadDeviceId = -1;
        [SerializeField] private string gamepadIdentity = string.Empty;
        [SerializeField] private int legacyJoystickNumber = -1;
        [SerializeField] private float stickDeadzone = 0.18f;
        private bool legacyAxesUnavailable;
        private bool legacyDpadAxesUnavailable;
        private bool gameplayInputSuppressed;
        private StageManager cachedStageManager;

        public int LocalPlayerIndex => localPlayerIndex;
        public DeviceKind Kind => deviceKind;
        public int GamepadDeviceId => gamepadDeviceId;
        public int LegacyJoystickNumber => legacyJoystickNumber;
        public string DeviceDisplayName
        {
            get
            {
                if (deviceKind == DeviceKind.KeyboardMouse)
                    return LocalizationManager.T("input_device_keyboard_mouse");
                if (deviceKind == DeviceKind.LegacyJoystick)
                {
                    string[] names = Input.GetJoystickNames();
                    int index = legacyJoystickNumber - 1;
                    if (index >= 0 && index < names.Length && !string.IsNullOrWhiteSpace(names[index]))
                        return names[index];
                    return LocalizationManager.T("input_device_gamepad_disconnected");
                }
#if ENABLE_INPUT_SYSTEM
                Gamepad pad = ResolveGamepad();
                if (pad != null && !string.IsNullOrWhiteSpace(pad.displayName)) return pad.displayName;
#endif
                return deviceKind == DeviceKind.Gamepad
                    ? LocalizationManager.T("input_device_gamepad_disconnected")
                    : LocalizationManager.T("input_device_automatic");
            }
        }

        // AutoPrimary is the shared P1 slot used by characters created from the
        // single-player drawer. It deliberately falls back to the same keyboard
        // and mouse input as P1; StageManager enables only the selected avatar.
        public bool UsesLegacyKeyboardMouse => deviceKind == DeviceKind.KeyboardMouse
            || deviceKind == DeviceKind.AutoPrimary;
        public bool UsesPlayStationGlyphs
        {
            get
            {
                string identity = DeviceDisplayName ?? string.Empty;
#if ENABLE_INPUT_SYSTEM
                Gamepad pad = ResolveGamepadForInput();
                if (pad != null)
                {
                    identity += " " + (pad.description.product ?? string.Empty);
                    identity += " " + (pad.description.manufacturer ?? string.Empty);
                    identity += " " + (pad.layout ?? string.Empty);
                }
#endif
                return identity.IndexOf("DualSense", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || identity.IndexOf("DualShock", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || identity.IndexOf("PlayStation", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || identity.IndexOf("Wireless Controller", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || identity.IndexOf("Sony", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        public void SetGameplayInputSuppressed(bool suppressed) => gameplayInputSuppressed = suppressed;
        public bool HasUsableGamepad
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return ResolveGamepadForInput() != null || IsLegacyJoystickConnected();
#else
                return false;
#endif
            }
        }

        public void BindKeyboardMouse(int playerIndex)
        {
            localPlayerIndex = Mathf.Clamp(playerIndex, 0, StageManager.MaxLocalPlayersPerConnection - 1);
            deviceKind = DeviceKind.KeyboardMouse;
            gamepadDeviceId = -1;
            gamepadIdentity = string.Empty;
            legacyJoystickNumber = -1;
        }

#if ENABLE_INPUT_SYSTEM
        public void BindGamepad(int playerIndex, Gamepad gamepad)
        {
            localPlayerIndex = Mathf.Clamp(playerIndex, 0, StageManager.MaxLocalPlayersPerConnection - 1);
            deviceKind = DeviceKind.Gamepad;
            gamepadDeviceId = gamepad != null ? gamepad.deviceId : -1;
            gamepadIdentity = BuildIdentity(gamepad);
            legacyJoystickNumber = -1;
        }

        public bool MatchesGamepad(Gamepad gamepad)
        {
            if (gamepad == null || deviceKind != DeviceKind.Gamepad) return false;
            if (gamepad.deviceId == gamepadDeviceId) return true;
            return !string.IsNullOrEmpty(gamepad.description.serial)
                && BuildIdentity(gamepad) == gamepadIdentity;
        }
#endif

        public void BindLegacyJoystick(int playerIndex, int joystickNumber)
        {
            localPlayerIndex = Mathf.Clamp(playerIndex, 0, StageManager.MaxLocalPlayersPerConnection - 1);
            deviceKind = DeviceKind.LegacyJoystick;
            gamepadDeviceId = -1;
            gamepadIdentity = string.Empty;
            legacyJoystickNumber = Mathf.Clamp(joystickNumber, 1, 4);
            legacyAxesUnavailable = false;
            legacyDpadAxesUnavailable = false;
        }

        public bool MatchesLegacyJoystick(int joystickNumber)
        {
            return deviceKind == DeviceKind.LegacyJoystick
                && legacyJoystickNumber == joystickNumber;
        }

        public void BindAutoPrimary()
        {
            localPlayerIndex = 0;
            deviceKind = DeviceKind.AutoPrimary;
            gamepadDeviceId = -1;
            gamepadIdentity = string.Empty;
            legacyJoystickNumber = -1;
        }

        public void SetLocalPlayerIndex(int playerIndex)
        {
            localPlayerIndex = Mathf.Clamp(playerIndex, 0, StageManager.MaxLocalPlayersPerConnection - 1);
        }

        public float ReadMoveX()
        {
            return ReadMove().x;
        }

        public Vector2 ReadMove()
        {
            if (gameplayInputSuppressed) return Vector2.zero;
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadMove();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude < stickDeadzone * stickDeadzone) stick = Vector2.zero;
                Vector2 dpad = pad.dpad.ReadValue();
                return dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
            }
#endif
            if (deviceKind == DeviceKind.LegacyJoystick)
            {
                float horizontal = ReadLegacyAxis("Horizontal", "Horizontal");
                float legacyVertical = ReadLegacyAxis("Vertical", "Vertical");
                return new Vector2(ApplyDeadzone(horizontal), ApplyDeadzone(legacyVertical));
            }
            if (!UsesLegacyKeyboardMouse) return Vector2.zero;
            if (deviceKind == DeviceKind.KeyboardMouse || deviceKind == DeviceKind.AutoPrimary)
            {
                float horizontal = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                float verticalKeys = 0f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) verticalKeys += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) verticalKeys -= 1f;
                return new Vector2(horizontal, verticalKeys);
            }
            float vertical = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            return new Vector2(Input.GetAxisRaw("Horizontal"), vertical);
        }

        public Vector2 ReadAim()
        {
            if (gameplayInputSuppressed) return Vector2.zero;
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadAim();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
            {
                Vector2 aim = pad.rightStick.ReadValue();
                if (aim.sqrMagnitude < stickDeadzone * stickDeadzone) aim = pad.leftStick.ReadValue();
                return aim.sqrMagnitude >= stickDeadzone * stickDeadzone ? aim.normalized : Vector2.zero;
            }
#endif
            if (deviceKind == DeviceKind.LegacyJoystick)
            {
                Vector2 aim = new Vector2(
                    ReadLegacyAxis("AimX", "Horizontal"),
                    ReadLegacyAxis("AimY", "Vertical"));
                return aim.sqrMagnitude >= stickDeadzone * stickDeadzone ? aim.normalized : Vector2.zero;
            }
            return Vector2.zero;
        }

        public Vector2 ReadDrawCursor()
        {
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadDrawCursor();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
            {
                Vector2 value = pad.leftStick.ReadValue();
                return value.sqrMagnitude >= stickDeadzone * stickDeadzone ? value : Vector2.zero;
            }
#endif
            if (deviceKind == DeviceKind.LegacyJoystick)
            {
                float horizontal = ReadLegacyAxis("Horizontal", "Horizontal");
                float vertical = ReadLegacyAxis("Vertical", "Vertical");
                return new Vector2(ApplyDeadzone(horizontal), ApplyDeadzone(vertical));
            }
            return Vector2.zero;
        }

        // DRAW uses a pointer-like interaction rather than gameplay actions.
        // Accept both face-button conventions as well as the right trigger so
        // PlayStation users are not forced to guess whether Circle or Cross is
        // the confirm button for their region. Holding any of them draws.
        public bool DrawPrimaryPressed()
        {
            if (gameplayInputSuppressed) return false;
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.DrawPrimaryPressed();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
                return pad.rightTrigger.wasPressedThisFrame
                    || pad.buttonSouth.wasPressedThisFrame
                    || pad.buttonEast.wasPressedThisFrame;
#endif
            return deviceKind == DeviceKind.LegacyJoystick
                && (ReadLegacyButton(7, ButtonPhase.Pressed)
                    || ReadLegacyButton(1, ButtonPhase.Pressed)
                    || ReadLegacyButton(2, ButtonPhase.Pressed));
        }

        public bool DrawPrimaryHeld()
        {
            if (gameplayInputSuppressed) return false;
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.DrawPrimaryHeld();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
                return pad.rightTrigger.isPressed
                    || pad.buttonSouth.isPressed
                    || pad.buttonEast.isPressed;
#endif
            return deviceKind == DeviceKind.LegacyJoystick
                && (ReadLegacyButton(7, ButtonPhase.Held)
                    || ReadLegacyButton(1, ButtonPhase.Held)
                    || ReadLegacyButton(2, ButtonPhase.Held));
        }

        public bool DrawPrimaryReleased()
        {
            if (gameplayInputSuppressed) return false;
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.DrawPrimaryReleased();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
                return pad.rightTrigger.wasReleasedThisFrame
                    || pad.buttonSouth.wasReleasedThisFrame
                    || pad.buttonEast.wasReleasedThisFrame;
#endif
            return deviceKind == DeviceKind.LegacyJoystick
                && (ReadLegacyButton(7, ButtonPhase.Released)
                    || ReadLegacyButton(1, ButtonPhase.Released)
                    || ReadLegacyButton(2, ButtonPhase.Released));
        }

        public bool DrawEraseHeld()
        {
            if (gameplayInputSuppressed) return false;
            return ReadButton(ButtonRole.Secondary, ButtonPhase.Held);
        }

        public bool JumpPressed() => !gameplayInputSuppressed && ReadButton(ButtonRole.Jump, ButtonPhase.Pressed);
        public bool JumpHeld() => !gameplayInputSuppressed && ReadButton(ButtonRole.Jump, ButtonPhase.Held);
        public bool AbilityPressed() => !gameplayInputSuppressed && ReadButton(ButtonRole.Ability, ButtonPhase.Pressed);
        public bool AbilityHeld() => !gameplayInputSuppressed && ReadButton(ButtonRole.Ability, ButtonPhase.Held);
        public bool PrimaryPressed() => !gameplayInputSuppressed && ReadButton(ButtonRole.Primary, ButtonPhase.Pressed);
        public bool PrimaryHeld() => !gameplayInputSuppressed && ReadButton(ButtonRole.Primary, ButtonPhase.Held);
        public bool PrimaryReleased() => !gameplayInputSuppressed && ReadButton(ButtonRole.Primary, ButtonPhase.Released);
        public bool SecondaryHeld() => !gameplayInputSuppressed && ReadButton(ButtonRole.Secondary, ButtonPhase.Held);
        public bool DrawTogglePressed() => !gameplayInputSuppressed && ReadButton(ButtonRole.DrawToggle, ButtonPhase.Pressed);
        public bool MenuPressed() => ReadButton(ButtonRole.Menu, ButtonPhase.Pressed);
        public bool ConfirmPressed() => ReadButton(ButtonRole.Confirm, ButtonPhase.Pressed);
        public bool CancelPressed() => ReadButton(ButtonRole.Cancel, ButtonPhase.Pressed);
        public bool CancelHeld() => ReadButton(ButtonRole.Cancel, ButtonPhase.Held);
        public bool LeaveChordPressed() => ReadButton(ButtonRole.DrawToggle, ButtonPhase.Held)
            && ReadButton(ButtonRole.Cancel, ButtonPhase.Pressed);

        public Vector2 ReadMenuDirection()
        {
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadMenuDirection();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null) return pad.dpad.ReadValue();
#endif
            if (deviceKind != DeviceKind.LegacyJoystick || legacyDpadAxesUnavailable)
                return Vector2.zero;
            try
            {
                return new Vector2(
                    Input.GetAxisRaw($"LocalPad{legacyJoystickNumber}DpadX"),
                    Input.GetAxisRaw($"LocalPad{legacyJoystickNumber}DpadY"));
            }
            catch (System.ArgumentException)
            {
                legacyDpadAxesUnavailable = true;
                return Vector2.zero;
            }
        }

        public Vector2 ReadUiCursor(bool useRightStick)
        {
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadUiCursor(useRightStick);
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
            {
                Vector2 value = useRightStick
                    ? pad.rightStick.ReadValue()
                    : pad.leftStick.ReadValue();
                return value.sqrMagnitude >= stickDeadzone * stickDeadzone
                    ? value
                    : Vector2.zero;
            }
#endif
            if (deviceKind != DeviceKind.LegacyJoystick) return Vector2.zero;
            string xAxis = useRightStick ? "AimX" : "Horizontal";
            string yAxis = useRightStick ? "AimY" : "Vertical";
            Vector2 legacy = new Vector2(
                ReadLegacyAxis(xAxis, "Horizontal"),
                ReadLegacyAxis(yAxis, "Vertical"));
            return legacy.sqrMagnitude >= stickDeadzone * stickDeadzone
                ? Vector2.ClampMagnitude(legacy, 1f)
                : Vector2.zero;
        }
        public bool ClearPressed() => ReadButton(ButtonRole.Clear, ButtonPhase.Pressed);
        public bool PreviousPartPressed() => ReadButton(ButtonRole.PreviousPart, ButtonPhase.Pressed);
        public bool NextPartPressed() => ReadButton(ButtonRole.NextPart, ButtonPhase.Pressed);
        public bool PreviousSpeciesPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            return pad != null && pad.dpad.down.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public bool NextSpeciesPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            return pad != null && pad.dpad.up.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public bool IsAssignedGamepadConnected()
        {
#if ENABLE_INPUT_SYSTEM
            if (deviceKind == DeviceKind.Gamepad) return ResolveGamepad() != null;
#else
            if (deviceKind == DeviceKind.Gamepad) return false;
#endif
            return deviceKind != DeviceKind.LegacyJoystick || IsLegacyJoystickConnected();
        }

        private enum ButtonRole
        {
            Jump,
            Ability,
            Primary,
            Secondary,
            DrawToggle,
            Menu,
            Confirm,
            Cancel,
            Clear,
            PreviousPart,
            NextPart
        }

        private enum ButtonPhase { Pressed, Held, Released }

        private bool ReadButton(ButtonRole role, ButtonPhase phase)
        {
            LocalPlayerInputBinding sharedPrimary = ResolveSharedPrimaryBinding();
            if (sharedPrimary != null
                && sharedPrimary.deviceKind == DeviceKind.LegacyJoystick)
                return sharedPrimary.ReadButton(role, phase);
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = ResolveGamepadForInput();
            if (pad != null)
            {
                UnityEngine.InputSystem.Controls.ButtonControl button;
                switch (role)
                {
                    case ButtonRole.Jump:
                    case ButtonRole.Confirm: button = pad.buttonSouth; break;
                    case ButtonRole.Ability: button = pad.buttonWest; break;
                    case ButtonRole.Primary: button = pad.rightTrigger; break;
                    case ButtonRole.Secondary: button = pad.leftTrigger; break;
                    case ButtonRole.DrawToggle: button = pad.selectButton; break;
                    case ButtonRole.Menu: button = pad.startButton; break;
                    case ButtonRole.Cancel: button = pad.buttonEast; break;
                    case ButtonRole.Clear: button = pad.buttonNorth; break;
                    case ButtonRole.PreviousPart: button = pad.leftShoulder; break;
                    case ButtonRole.NextPart: button = pad.rightShoulder; break;
                    default: return false;
                }
                if (phase == ButtonPhase.Pressed) return button.wasPressedThisFrame;
                if (phase == ButtonPhase.Released) return button.wasReleasedThisFrame;
                return button.isPressed;
            }
#endif
            if (deviceKind == DeviceKind.LegacyJoystick)
            {
                int buttonIndex;
                switch (role)
                {
                    case ButtonRole.Jump:
                    case ButtonRole.Confirm: buttonIndex = 1; break;
                    case ButtonRole.Ability: buttonIndex = 0; break;
                    case ButtonRole.Primary: buttonIndex = 7; break;
                    case ButtonRole.Secondary: buttonIndex = 6; break;
                    case ButtonRole.DrawToggle: buttonIndex = 8; break;
                    case ButtonRole.Menu: buttonIndex = 9; break;
                    case ButtonRole.Cancel: buttonIndex = 2; break;
                    case ButtonRole.Clear: buttonIndex = 3; break;
                    case ButtonRole.PreviousPart: buttonIndex = 4; break;
                    case ButtonRole.NextPart: buttonIndex = 5; break;
                    default: return false;
                }
                return ReadLegacyButton(buttonIndex, phase);
            }
            return false;
        }

        private bool ReadLegacyButton(int buttonIndex, ButtonPhase phase)
        {
            if (legacyJoystickNumber < 1 || legacyJoystickNumber > 4) return false;
            KeyCode key = (KeyCode)((int)KeyCode.Joystick1Button0
                + (legacyJoystickNumber - 1) * 20 + Mathf.Clamp(buttonIndex, 0, 19));
            if (phase == ButtonPhase.Pressed) return Input.GetKeyDown(key);
            if (phase == ButtonPhase.Released) return Input.GetKeyUp(key);
            return Input.GetKey(key);
        }

        private bool IsLegacyJoystickConnected()
        {
            string[] names = Input.GetJoystickNames();
            int index = legacyJoystickNumber - 1;
            return index >= 0 && index < names.Length && !string.IsNullOrWhiteSpace(names[index]);
        }

        private float ReadLegacyAxis(string suffix, string fallbackAxis)
        {
            if (!legacyAxesUnavailable)
            {
                try
                {
                    return Input.GetAxisRaw($"LocalPad{legacyJoystickNumber}{suffix}");
                }
                catch (System.ArgumentException)
                {
                    legacyAxesUnavailable = true;
                }
            }
            float fallback = Input.GetAxisRaw(fallbackAxis);
            // The project-wide legacy axes also include keyboard bindings. They
            // are only a temporary compatibility path until Unity reloads the
            // per-pad axes, so suppress keyboard input here to prevent P1 from
            // driving a legacy-controller player at the same time.
            if (fallbackAxis == "Horizontal"
                && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D)
                    || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
                return 0f;
            if (fallbackAxis == "Vertical"
                && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S)
                    || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)))
                return 0f;
            return fallback;
        }

        private LocalPlayerInputBinding ResolveSharedPrimaryBinding()
        {
            if (deviceKind != DeviceKind.AutoPrimary) return null;
            if (cachedStageManager == null)
                cachedStageManager = FindFirstObjectByType<StageManager>();
            if (cachedStageManager == null || cachedStageManager.LocalPlayers.Count == 0)
                return null;
            LocalPlayerInputBinding primary = cachedStageManager.LocalPlayers[0] != null
                ? cachedStageManager.LocalPlayers[0].InputBinding
                : null;
            return primary != this ? primary : null;
        }

#if ENABLE_INPUT_SYSTEM
        private Gamepad ResolveGamepadForInput()
        {
            Gamepad assigned = ResolveGamepad();
            if (assigned != null || deviceKind != DeviceKind.AutoPrimary) return assigned;

            // AutoPrimary characters created from the single-player drawer
            // share P1's controls. Mirror only P1's explicitly assigned pad;
            // never fall back to Gamepad.current because an unjoined device
            // must not start controlling a character by accident.
            LocalPlayerInputBinding primary = ResolveSharedPrimaryBinding();
            if (primary != null && primary.deviceKind == DeviceKind.Gamepad)
            {
                return primary.ResolveGamepad();
            }
            return null;
        }

        private Gamepad ResolveGamepad()
        {
            if (deviceKind != DeviceKind.Gamepad || gamepadDeviceId < 0) return null;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (pad.deviceId == gamepadDeviceId) return pad;
            }
            if (string.IsNullOrEmpty(gamepadIdentity)) return null;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (BuildIdentity(pad) != gamepadIdentity
                    || IsDeviceClaimedByAnotherBinding(pad.deviceId)) continue;
                gamepadDeviceId = pad.deviceId;
                return pad;
            }
            return null;
        }

        private bool IsDeviceClaimedByAnotherBinding(int deviceId)
        {
            StageManager manager = FindFirstObjectByType<StageManager>();
            if (manager == null) return false;
            System.Collections.Generic.IReadOnlyList<PlayerController2D> players = manager.LocalPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                LocalPlayerInputBinding binding = players[i] != null ? players[i].InputBinding : null;
                if (binding != null && binding != this
                    && binding.deviceKind == DeviceKind.Gamepad
                    && binding.gamepadDeviceId == deviceId) return true;
            }
            return false;
        }

        private static string BuildIdentity(Gamepad gamepad)
        {
            if (gamepad == null) return string.Empty;
            return string.Join("|", gamepad.description.interfaceName,
                gamepad.description.manufacturer, gamepad.description.product,
                gamepad.description.serial);
        }
#endif

        private float ApplyDeadzone(float value)
        {
            float absolute = Mathf.Abs(value);
            if (absolute <= stickDeadzone) return 0f;
            return Mathf.Sign(value) * Mathf.InverseLerp(stickDeadzone, 1f, absolute);
        }
    }
}
