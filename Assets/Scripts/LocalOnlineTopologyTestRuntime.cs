using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace DrawBody.Prototype
{
    /// <summary>
    /// Development-build regression harness for mixed local/online topologies.
    /// It joins local seats through the same Enter/START path used by players,
    /// then validates ownership, movement, shared-menu routing and network roster.
    /// </summary>
    public sealed class LocalOnlineTopologyTestRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string runId;
            public string caseName;
            public int clientInstance;
            public string layout;
            public string result;
            public int localPlayers;
            public int rosterPlayers;
            public int observedPlayers;
            public string[] passedChecks;
            public string[] failedChecks;
        }

        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();
        private readonly List<Vector2> movementStarts = new List<Vector2>();
#if ENABLE_INPUT_SYSTEM
        private readonly List<Gamepad> virtualPads = new List<Gamepad>();
        private Keyboard virtualKeyboard;
#endif
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private string reportDirectory;
        private string runId;
        private string caseName;
        private string layout;
        private string[] layoutTokens;
        private int clientInstance;
        private int expectedPlayers = 4;
        private int joinStep;
        private float nextJoinAt;
        private float gameplayStartedAt = -1f;
        private bool setupChecked;
        private bool movementCaptured;
        private bool p2MenuChecked;
        private bool p1MenuRequested;
        private bool p1MenuOpened;
        private float p1MenuVisibleAt = -1f;
        private bool menuCursorSeen;
        private bool menuControlsSuppressed;
        private bool menuButtonsChecked;
        private bool optionButtonWorked;
        private bool menuCloseRequested;
        private bool menuClosed;
        private bool actionInputsSeen;
        private bool jumpSeen;
        private bool abilitySeen;
        private bool primarySeen;
        private bool finished;
        private int exceptions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if NICO_DRAW_AI_TEST
            string[] args = Environment.GetCommandLineArgs();
            if (!HasFlag(args, "-pico-local-online-test")) return;
            GameObject root = new GameObject("NICO DRAW Local Online Topology Test");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalOnlineTopologyTestRuntime>();
#endif
        }

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            reportDirectory = GetArg(args, "-pico-ai-report-dir")
                ?? Path.Combine(Application.persistentDataPath, "LocalOnlineTopologyTest");
            runId = GetArg(args, "-pico-ai-run-id") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            caseName = GetArg(args, "-pico-topology-case") ?? "mixed-local-online";
            layout = (GetArg(args, "-pico-local-layout") ?? "keyboard").ToLowerInvariant();
            layoutTokens = layout.Split(new[] { '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (layoutTokens.Length == 0) layoutTokens = new[] { "keyboard" };
            if (int.TryParse(GetArg(args, "-pico-ai-client-id"), out int parsedClient))
                clientInstance = Mathf.Max(0, parsedClient);
            if (int.TryParse(GetArg(args, "-pico-regression-players"), out int parsedPlayers))
                expectedPlayers = Mathf.Clamp(parsedPlayers, 2, 4);
            Directory.CreateDirectory(reportDirectory);
            Application.logMessageReceived += HandleLog;
            CreateVirtualDevices();
            nextJoinAt = Time.unscaledTime + 0.35f;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            ClearLocalInput();
#if ENABLE_INPUT_SYSTEM
            for (int i = 0; i < virtualPads.Count; i++)
                if (virtualPads[i] != null && virtualPads[i].added) InputSystem.RemoveDevice(virtualPads[i]);
            if (virtualKeyboard != null && virtualKeyboard.added) InputSystem.RemoveDevice(virtualKeyboard);
#endif
        }

        private void Update()
        {
            ResolveReferences();
            DriveNormalJoinCommands();
            if (finished || stageManager == null || onlineManager == null) return;
            if (!stageManager.IsGameplayActive) return;

            if (gameplayStartedAt < 0f)
            {
                gameplayStartedAt = Time.unscaledTime;
                CaptureMovementStarts();
            }
            float elapsed = Time.unscaledTime - gameplayStartedAt;
            ObserveMappedButtons();
            DriveGameplayInputs(elapsed);
            RunChecks(elapsed);
        }

        private void ResolveReferences()
        {
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        }

        private void DriveNormalJoinCommands()
        {
#if ENABLE_INPUT_SYSTEM
            if (joinStep >= layoutTokens.Length || Time.unscaledTime < nextJoinAt) return;
            LocalCoopSessionManager localCoop = stageManager != null
                ? stageManager.GetComponent<LocalCoopSessionManager>()
                : null;
            if (localCoop == null) return;
            string token = layoutTokens[joinStep];
            bool joined = token == "keyboard"
                ? localCoop.TryJoinKeyboardMouse()
                : token == "gamepad" && joinStep < virtualPads.Count
                    && localCoop.TryJoinGamepad(virtualPads[joinStep]);
            if (!joined) return;
            Debug.Log($"[PICO LOCAL ONLINE TEST] joined P{joinStep + 1} as {token}");
            joinStep++;
            nextJoinAt = Time.unscaledTime + 0.38f;
#endif
        }

        private void CaptureMovementStarts()
        {
            movementStarts.Clear();
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++)
                movementStarts.Add(locals[i] != null ? (Vector2)locals[i].transform.position : Vector2.zero);
        }

        private void DriveGameplayInputs(float elapsed)
        {
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++)
            {
                PlayerController2D local = locals[i];
                if (local == null) continue;
                LocalPlayerInputBinding binding = local.InputBinding;
                float direction = i == 0 ? 1f : -1f;
                if (binding != null && binding.Kind == LocalPlayerInputBinding.DeviceKind.KeyboardMouse)
                {
                    bool jump = elapsed >= 1.0f && elapsed < 1.5f;
                    local.SetScriptedInput(elapsed < 3.4f ? direction : 0f, jump, jump);
                }
#if ENABLE_INPUT_SYSTEM
                else if (binding != null && binding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad)
                {
                    Gamepad pad = FindPad(binding.GamepadDeviceId);
                    if (pad == null) continue;
                    GamepadState state = new GamepadState
                    {
                        leftStick = elapsed < 3.4f || elapsed >= 6f && elapsed < 6.5f
                            ? new Vector2(direction, 0f)
                            : Vector2.zero
                    };
                    if (elapsed >= 1.0f && elapsed < 1.5f)
                        state = state.WithButton(GamepadButton.South);
                    if (elapsed >= 1.7f && elapsed < 2.2f)
                        state = state.WithButton(GamepadButton.West);
                    if (elapsed >= 2.3f && elapsed < 3.3f)
                        state.rightTrigger = 1f;
                    if (i == 1 && elapsed >= 4.0f && !p2MenuChecked)
                        state = state.WithButton(GamepadButton.Start);
                    if (i == 0 && p2MenuChecked && !p1MenuOpened && elapsed >= 5.0f)
                        state = state.WithButton(GamepadButton.Start);
                    if (i == 0 && menuButtonsChecked && !menuClosed
                        && uiManager != null && uiManager.IsGameplayMenuShowing)
                    {
                        state = state.WithButton(GamepadButton.East);
                        menuCloseRequested = true;
                    }
                    InputSystem.QueueStateEvent(pad, state);
                }
#endif
            }

            if (layoutTokens[0] == "keyboard" && p2MenuChecked
                && !p1MenuRequested && elapsed >= 5f)
            {
                p1MenuRequested = true;
                uiManager?.ToggleMenuFromEscape();
            }
        }

        private void ObserveMappedButtons()
        {
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++)
            {
                LocalPlayerInputBinding binding = locals[i] != null ? locals[i].InputBinding : null;
                if (binding == null || binding.Kind != LocalPlayerInputBinding.DeviceKind.Gamepad) continue;
                // The virtual-device state is queued from Update and may be
                // consumed by PlayerController2D before this harness observes
                // the one-frame edge. Held verifies the same binding while
                // remaining independent of MonoBehaviour execution order.
                jumpSeen |= binding.JumpPressed() || binding.JumpHeld();
                abilitySeen |= binding.AbilityPressed() || binding.AbilityHeld();
                primarySeen |= binding.PrimaryPressed() || binding.PrimaryHeld();
            }
            actionInputsSeen = jumpSeen && abilitySeen && primarySeen;
        }

        private void RunChecks(float elapsed)
        {
            if (!setupChecked && elapsed >= 0.5f)
            {
                setupChecked = true;
                CheckLocalBindings();
                CheckRoster();
            }
            if (!movementCaptured && elapsed >= 3.8f)
            {
                movementCaptured = true;
                CheckMovement();
                Add(actionInputsSeen || !HasControllerSeat(),
                    "controller jump/ability/action mappings produced input",
                    $"controller mappings missing: jump={jumpSeen}, ability={abilitySeen}, primary={primarySeen}");
            }
            if (!p2MenuChecked && elapsed >= 4.65f)
            {
                p2MenuChecked = true;
                if (layoutTokens.Length > 1 && layoutTokens[1] == "gamepad")
                    Add(uiManager != null && !uiManager.IsGameplayMenuShowing,
                        "P2 START did not take over the shared menu",
                        "P2 START incorrectly opened the shared menu");
            }
            if (!p1MenuOpened && elapsed >= 5.2f)
            {
                bool open = uiManager != null && uiManager.IsGameplayMenuShowing;
                if (open && p1MenuVisibleAt < 0f) p1MenuVisibleAt = elapsed;
                if (open && elapsed - p1MenuVisibleAt < 0.25f) return;
                if (!open && elapsed < 7f) return;
                p1MenuOpened = true;
                Add(open, "P1 opened the gameplay menu", "P1 could not open the gameplay menu");
                IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
                menuControlsSuppressed = true;
                for (int i = 0; i < locals.Count; i++)
                    menuControlsSuppressed &= locals[i] != null && !locals[i].ControlsEnabled;
                Add(menuControlsSuppressed, "gameplay controls stopped while the menu was open",
                    "a local character kept gameplay controls while the menu was open");
                GameObject cursor = GameObject.Find("P1 Controller UI Cursor");
                menuCursorSeen = layoutTokens[0] != "gamepad"
                    || cursor != null && cursor.activeInHierarchy;
                Add(menuCursorSeen, "P1 controller cursor was available in the menu",
                    "P1 controller cursor was missing from the menu");
                CheckMenuButtons();
            }
            if (!menuCloseRequested && menuButtonsChecked
                && layoutTokens[0] == "keyboard")
            {
                menuCloseRequested = true;
                uiManager?.ToggleMenuFromEscape();
            }
            if (!menuClosed && menuCloseRequested
                && (uiManager == null || !uiManager.IsGameplayMenuShowing || elapsed >= 11.5f))
            {
                menuClosed = true;
                Add(uiManager != null && !uiManager.IsGameplayMenuShowing,
                    "P1 closed the gameplay menu", "P1 could not close the gameplay menu");
                if (uiManager != null && !uiManager.IsGameplayMenuShowing)
                    CheckOptionButtonWorks();
            }
            // 15-3 creates its first independent mimic clone after eight
            // seconds of active combat. Keep that stage alive long enough for
            // boss/clone attack staggering to be observable in test builds.
            bool observeMimicClone = stageManager != null && stageManager.CurrentStageId == "15-3";
            if (menuClosed && (!observeMimicClone || elapsed >= 16f)
                || elapsed >= (observeMimicClone ? 20f : 12f)) Finish();
        }

        private void CheckLocalBindings()
        {
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            Add(locals.Count == layoutTokens.Length,
                $"local seats joined ({locals.Count}/{layoutTokens.Length})",
                $"local seat count was {locals.Count}/{layoutTokens.Length}");
            HashSet<int> padIds = new HashSet<int>();
            for (int i = 0; i < Mathf.Min(locals.Count, layoutTokens.Length); i++)
            {
                LocalPlayerInputBinding binding = locals[i] != null ? locals[i].InputBinding : null;
                bool expectedPad = layoutTokens[i] == "gamepad";
                bool kindMatches = binding != null && (expectedPad
                    ? binding.Kind == LocalPlayerInputBinding.DeviceKind.Gamepad
                    : binding.Kind == LocalPlayerInputBinding.DeviceKind.KeyboardMouse);
                Add(kindMatches, $"P{i + 1} owns {layoutTokens[i]}",
                    $"P{i + 1} binding did not match {layoutTokens[i]}");
                if (expectedPad && binding != null)
                    Add(padIds.Add(binding.GamepadDeviceId), $"P{i + 1} controller is unique",
                        $"P{i + 1} shares another player's controller");
            }
        }

        private void CheckRoster()
        {
            OnlinePlayerInfo[] roster = onlineManager.CurrentLobby?.Players;
            int rosterCount = 0;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (roster != null)
            {
                for (int i = 0; i < roster.Length; i++)
                    if (roster[i] != null && !string.IsNullOrEmpty(roster[i].PlayerId))
                    {
                        rosterCount++;
                        ids.Add(roster[i].PlayerId);
                    }
            }
            Add(rosterCount == expectedPlayers, $"online roster has {expectedPlayers} game players",
                $"online roster was {rosterCount}/{expectedPlayers}");
            Add(ids.Count == rosterCount, "all online game-player IDs are unique",
                "online game-player IDs were duplicated");
            int observed = CountObservedPlayers();
            Add(observed == expectedPlayers, $"all {expectedPlayers} avatars were created",
                $"only {observed}/{expectedPlayers} avatars were created");
            if (stageManager != null && stageManager.CurrentStageId == "15-3")
            {
                HashSet<DrawManager.Species> species = new HashSet<DrawManager.Species>();
                bool turtleFound = false;
                for (int i = 0; roster != null && i < roster.Length; i++)
                {
                    OnlinePlayerInfo member = roster[i];
                    PlayerController2D observedPlayer = member != null
                        ? stageManager.GetOnlinePlayerController(member.PlayerId)
                        : null;
                    if (observedPlayer == null) continue;
                    DrawManager.Species current = observedPlayer.CurrentSpecies;
                    turtleFound |= current == DrawManager.Species.Turtle;
                    species.Add(current);
                }
                Add(!turtleFound, "15-3 replaced every turtle before combat",
                    "15-3 still contained a turtle avatar");
                Add(species.Count == Mathf.Min(expectedPlayers, 4),
                    "15-3 assigned a different allowed species to every player",
                    $"15-3 species were not unique ({species.Count}/{Mathf.Min(expectedPlayers, 4)})");
            }
        }

        private void CheckMovement()
        {
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < Mathf.Min(locals.Count, movementStarts.Count); i++)
            {
                float distance = locals[i] != null
                    ? Vector2.Distance(movementStarts[i], locals[i].transform.position)
                    : 0f;
                Add(distance >= 0.08f, $"P{i + 1} moved independently ({distance:0.00})",
                    $"P{i + 1} did not move ({distance:0.00})");
            }
        }

        private void CheckMenuButtons()
        {
            if (menuButtonsChecked) return;
            menuButtonsChecked = true;
            string[] expected = stageManager != null && stageManager.IsOnlineStageHost
                ? new[] { "MenuContinueButton", "MenuRetryButton", "MenuOptionButton", "MenuLeaveSessionButton" }
                : new[] { "MenuContinueButton", "MenuOptionButton", "MenuLeaveSessionButton" };
            for (int i = 0; i < expected.Length; i++)
            {
                Button button = FindSceneButton(expected[i]);
                GameObject obj = button != null ? button.gameObject : null;
                Add(button != null && obj.activeInHierarchy && button.IsInteractable(),
                    expected[i] + " is pressable",
                    button == null
                        ? expected[i] + " is missing"
                        : $"{expected[i]} state: self={obj.activeSelf}, hierarchy={obj.activeInHierarchy}, interactable={button.IsInteractable()}, inactive={DescribeInactiveAncestor(obj.transform)}");
            }
        }

        private void CheckOptionButtonWorks()
        {
            if (optionButtonWorked || uiManager == null) return;
            uiManager.ToggleMenuFromEscape();
            Button optionButton = FindSceneButton("MenuOptionButton");
            if (optionButton != null && optionButton.IsInteractable())
                optionButton.onClick.Invoke();
            optionButtonWorked = uiManager.IsOptionShowing;
            Add(optionButtonWorked, "OPTION button opened its panel",
                "OPTION button did not open its panel");
        }

        private static Button FindSceneButton(string objectName)
        {
            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Button inactiveMatch = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && button.name == objectName
                    && button.gameObject.scene.IsValid())
                {
                    if (button.gameObject.activeInHierarchy) return button;
                    if (inactiveMatch == null) inactiveMatch = button;
                }
            }
            return inactiveMatch;
        }

        private static string DescribeInactiveAncestor(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (!current.gameObject.activeSelf) return current.name;
            return "none";
        }

        private int CountObservedPlayers()
        {
            OnlinePlayerInfo[] roster = onlineManager.CurrentLobby?.Players;
            if (roster == null) return 0;
            int count = 0;
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != null && !string.IsNullOrEmpty(roster[i].PlayerId)
                    && stageManager.GetOnlinePlayerController(roster[i].PlayerId) != null) count++;
            return count;
        }

        private bool HasControllerSeat()
        {
            for (int i = 0; i < layoutTokens.Length; i++)
                if (layoutTokens[i] == "gamepad") return true;
            return false;
        }

        private void Finish()
        {
            finished = true;
            ClearLocalInput();
            Add(exceptions == 0, "no Unity exceptions", $"Unity exceptions: {exceptions}");
            Result report = new Result
            {
                runId = runId,
                caseName = caseName,
                clientInstance = clientInstance,
                layout = layout,
                result = failed.Count == 0 ? "PASS" : "FAIL",
                localPlayers = stageManager.LocalPlayers.Count,
                rosterPlayers = onlineManager.CurrentLobby?.Players?.Length ?? 0,
                observedPlayers = CountObservedPlayers(),
                passedChecks = passed.ToArray(),
                failedChecks = failed.ToArray()
            };
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(reportDirectory, $"topology-client-{clientInstance}.json"), json);
            File.WriteAllText(Path.Combine(reportDirectory, $"topology-client-{clientInstance}.done"), report.result);
            Debug.Log($"[PICO LOCAL ONLINE TEST] {report.result} case={caseName} client={clientInstance} layout={layout}");
            StartCoroutine(QuitSoon());
        }

        private System.Collections.IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(failed.Count == 0 ? 0 : 1);
        }

        private void ClearLocalInput()
        {
            if (stageManager == null) return;
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++) locals[i]?.ClearScriptedInput();
        }

        private void Add(bool condition, string pass, string fail)
        {
            List<string> target = condition ? passed : failed;
            string value = condition ? pass : fail;
            if (!target.Contains(value)) target.Add(value);
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions++;
        }

        private void CreateVirtualDevices()
        {
#if ENABLE_INPUT_SYSTEM
            int pads = 0;
            bool keyboard = false;
            for (int i = 0; i < layoutTokens.Length; i++)
            {
                if (layoutTokens[i] == "gamepad") pads++;
                if (layoutTokens[i] == "keyboard") keyboard = true;
            }
            if (keyboard) virtualKeyboard = InputSystem.AddDevice<Keyboard>();
            // Keep token indexes aligned so P1/P2 device ownership is explicit.
            for (int i = 0; i < layoutTokens.Length; i++)
                virtualPads.Add(layoutTokens[i] == "gamepad" ? InputSystem.AddDevice<Gamepad>() : null);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Gamepad FindPad(int deviceId)
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i].deviceId == deviceId) return Gamepad.all[i];
            return null;
        }
#endif

        private static bool HasFlag(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string[] args, string key)
        {
            string prefix = key + "=";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(prefix.Length).Trim('"');
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1].Trim('"');
            }
            return null;
        }
    }
}
