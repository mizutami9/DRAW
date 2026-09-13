using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Development-build-only black-box player. It observes scene state and feeds
    /// the same scripted input seam used by recorded gameplay. It never writes a
    /// Transform, Rigidbody, gimmick state, or network model directly.
    /// </summary>
    public sealed class AiOnlineTestRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class PlayerObservation
        {
            public string playerId;
            public int slot;
            public Vector2 position;
            public Vector2 velocity;
            public string species;
            public float legInk;
            public float armInk;
            public bool grounded;
            public bool controlsEnabled;
            public bool respawning;
            public bool eliminated;
        }

        [Serializable]
        private sealed class ObjectObservation
        {
            public string objectId;
            public string type;
            public Vector2 position;
            public bool active;
        }

        [Serializable]
        private sealed class ClientReport
        {
            public string runId;
            public string stage;
            public string transport;
            public string playerId;
            public int slot;
            public string result;
            public float elapsedSeconds;
            public int deaths;
            public int retries;
            public int stalls;
            public int exceptions;
            public int suspiciousPhysicsEvents;
            public string currentObjective;
            public string lastAction;
            public string[] passedChecks;
            public string[] failedChecks;
            public PlayerObservation[] players;
            public ObjectObservation[] importantObjects;
        }

        private enum ObjectiveKind
        {
            Touch,
            CarryTo,
            Wait,
            Goal
        }

        [Serializable]
        private sealed class StageObjective
        {
            public string name;
            public ObjectiveKind kind;
            public string objectId;
            public string completionObjectId;
            public string targetObjectId;
            public Vector2 fallbackPosition;
            public int roleSlot = -1;
        }

        private sealed class StageTestDefinition
        {
            public string stageId;
            public StageObjective firstButton;
            public StageObjective upperButton;
            public StageObjective firstKey;
            public StageObjective secondKey;
            public StageObjective waitAtStart;
            public StageObjective waitAtBridge;
            public StageObjective waitForKeys;
            public StageObjective goal;

            public static StageTestDefinition For(string stageId)
            {
                if (stageId != "1-1") return null;
                return new StageTestDefinition
                {
                    stageId = "1-1",
                    firstButton = Objective("reveal-first-bridge", ObjectiveKind.Touch,
                        "Button_f9e3a5e", "Platform_0ac2f", null, new Vector2(10f, -0.2f), 3),
                    upperButton = Objective("reveal-jump-pad", ObjectiveKind.Touch,
                        "Button_db2acc2", "JumpPad_6fab90", null, new Vector2(17f, 12.6f), 3),
                    firstKey = Objective("deliver-lower-key", ObjectiveKind.CarryTo,
                        "obj_51a1d398d022421e", "obj_7b4b8157480f41fc",
                        "obj_97cacc6c611a4413", new Vector2(1.5f, 20f), 0),
                    secondKey = Objective("deliver-upper-key", ObjectiveKind.CarryTo,
                        "obj_737a1c53c1984216", "obj_189b60d7a10442c9",
                        "obj_07fe2ebe0ed84257", new Vector2(3.5f, 35.5f), 1),
                    waitAtStart = Objective("wait-for-first-bridge", ObjectiveKind.Wait,
                        null, null, null, new Vector2(-4f, 0.5f)),
                    waitAtBridge = Objective("wait-for-jump-pad", ObjectiveKind.Wait,
                        null, null, null, new Vector2(11f, 0.5f)),
                    waitForKeys = Objective("support-at-key-gates", ObjectiveKind.Wait,
                        null, null, null, new Vector2(34.5f, 13f)),
                    goal = Objective("reach-goal", ObjectiveKind.Goal,
                        "obj_b44c3402ed6d4547", null, null, new Vector2(53f, 14f))
                };
            }

            private static StageObjective Objective(string name, ObjectiveKind kind,
                string objectId, string completionObjectId, string targetObjectId,
                Vector2 fallback, int role = -1)
            {
                return new StageObjective
                {
                    name = name,
                    kind = kind,
                    objectId = objectId,
                    completionObjectId = completionObjectId,
                    targetObjectId = targetObjectId,
                    fallbackPosition = fallback,
                    roleSlot = role
                };
            }
        }

        private StageManager stageManager;
        private StageLoader stageLoader;
        private OnlineManager onlineManager;
        private DrawManager drawManager;
        private PlayerController2D player;
        private PlayerCarryController carry;
        private Rigidbody2D body;
        private StageTestDefinition definition;
        private StageObjective objective;
        private readonly Dictionary<string, StageEditorObject> objects =
            new Dictionary<string, StageEditorObject>(StringComparer.Ordinal);
        private string reportDirectory;
        private string runId;
        private string transport;
        private int slot = -1;
        private float startedAt;
        private float nextResolveAt;
        private float nextReportAt;
        private float nextActionAt;
        private float nextJumpAt;
        private float nextScreenshotAt;
        private float firstBridgeSeenAt = -1f;
        private float bestDistance = float.PositiveInfinity;
        private float lastProgressAt;
        private float recoveryUntil;
        private int recoveryDirection = 1;
        private int deaths;
        private int retries;
        private int stalls;
        private int exceptions;
        private int suspiciousPhysicsEvents;
        private bool wasRespawning;
        private bool finished;
        private bool recordingIncident;
        private bool smokeMode;
        private bool smokeConcurrentRedraw;
        private bool smokeStarted;
        private bool smokeMoveRecorded;
        private bool smokeBodyWindowOpened;
        private bool smokeRedrawAttempted;
        private bool smokeRedrawEntered;
        private bool smokeControlsDisabledDuringDraw;
        private bool smokeRedrawClosed;
        private bool smokeControlsRestored;
        private bool smokeScreenshotTaken;
        private float smokeStartedAt;
        private Vector2 smokeStartPosition;
        private float smokeMovementDistance;
        private int smokeExpectedPlayers = 4;
        private OnlineManager smokeSubscribedManager;
        private readonly HashSet<string> smokeBodyPlayers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> smokePassedChecks = new List<string>();
        private readonly List<string> smokeFailedChecks = new List<string>();
        private bool lobbySpeciesAttempted;
        private int lobbyPreparedSlot = -1;
        private bool roleSpeciesAttempted;
        private bool precisionRedrawStarted;
        private bool precisionRedrawCompleted;
        private bool basinEscapeStarted;
        private bool basinJumpAttempted;
        private bool keyPlatformWasCarried;
        private bool keyPlatformThrowReceived;
        private string progressObjectiveName;
        private string lastAction = "boot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if NICO_DRAW_AI_TEST
            string[] args = Environment.GetCommandLineArgs();
            if (!HasFlag(args, "-pico-ai-test")) return;
            GameObject root = new GameObject("NICO DRAW AI Online Test");
            DontDestroyOnLoad(root);
            root.AddComponent<AiOnlineTestRuntime>();
#endif
        }

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            reportDirectory = GetArg(args, "-pico-ai-report-dir") ??
                Path.Combine(Application.persistentDataPath, "AiOnlineTest");
            runId = GetArg(args, "-pico-ai-run-id") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            transport = GetArg(args, "-pico-regression-backend") ?? "direct";
            smokeMode = HasFlag(args, "-pico-multiplayer-smoke");
            smokeConcurrentRedraw = HasFlag(args, "-pico-smoke-concurrent-redraw");
            if (int.TryParse(GetArg(args, "-pico-regression-players"), out int expectedPlayers))
                smokeExpectedPlayers = Mathf.Clamp(expectedPlayers, 2, 4);
            Directory.CreateDirectory(reportDirectory);
            if (int.TryParse(GetArg(args, "-pico-ai-slot"), out int configuredSlot))
                slot = Mathf.Clamp(configuredSlot, 0, 3);
            Application.logMessageReceived += HandleLog;
            startedAt = Time.unscaledTime;
            lastProgressAt = startedAt;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            if (smokeSubscribedManager != null)
                smokeSubscribedManager.BodyDataReceived -= HandleSmokeBodyData;
            player?.ClearScriptedInput();
        }

        private void Update()
        {
            ResolveRuntimeReferences();
            if (stageManager == null || onlineManager == null || player == null || body == null) return;

            TryPrepareLobbySpecies();

            if (smokeMode)
            {
                UpdateMultiplayerSmoke();
                return;
            }

            if (stageManager.IsStageCleared)
            {
                if (!finished)
                {
                    finished = true;
                    player.ClearScriptedInput();
                    WriteReport("PASS", true);
                    Debug.Log($"[PICO AI TEST] Player {slot + 1} observed stage clear.");
                }
                return;
            }

            if (!stageManager.IsGameplayActive)
            {
                player.SetScriptedInput(0f, false);
                WritePeriodicReport("RUNNING");
                return;
            }

            bool respawning = stageManager.IsPlayerRespawning(player);
            if (respawning && !wasRespawning)
            {
                deaths++;
                keyPlatformWasCarried = false;
                keyPlatformThrowReceived = false;
                recoveryUntil = 0f;
                recoveryDirection = 1;
                lastProgressAt = Time.unscaledTime;
                RecordIncident("death", "Player entered respawn state.", false);
            }
            wasRespawning = respawning;
            if (respawning || stageManager.IsPlayerEliminated(player) || !player.ControlsEnabled)
            {
                player.SetScriptedInput(0f, false);
                WritePeriodicReport("RUNNING");
                return;
            }

            DetectPhysicsAnomalies();
            if (TryPrepareRoleSpecies())
            {
                WritePeriodicReport("RUNNING");
                return;
            }
            objective = SelectObjective();
            if (objective == null)
            {
                player.SetScriptedInput(0f, false);
                WritePeriodicReport("UNSUPPORTED_STAGE");
                return;
            }

            if (TryDriveCooperativeTraversal())
            {
                WritePeriodicReport("RUNNING");
                return;
            }
            if (TryDriveKeyPlatformThrows())
            {
                WritePeriodicReport("RUNNING");
                return;
            }
            if (TryDriveBasinEscape())
            {
                WritePeriodicReport("RUNNING");
                return;
            }
            if (TryDiscardKeyObstacle())
            {
                WritePeriodicReport("RUNNING");
                return;
            }

            Vector2 target = ResolveObjectivePosition(objective);
            DriveToward(target);
            UpdateProgress(target);
            WritePeriodicReport("RUNNING");
        }

        private void ResolveRuntimeReferences()
        {
            if (Time.unscaledTime < nextResolveAt) return;
            nextResolveAt = Time.unscaledTime + 0.5f;
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            if (stageLoader == null) stageLoader = FindFirstObjectByType<StageLoader>();
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
            if (drawManager == null) drawManager = FindFirstObjectByType<DrawManager>();
            if (stageManager == null || onlineManager == null) return;

            Transform active = stageManager.ActivePlayerTransform;
            if (active != null && (player == null || player.transform != active))
            {
                player?.ClearScriptedInput();
                player = active.GetComponent<PlayerController2D>();
                carry = active.GetComponent<PlayerCarryController>();
                body = active.GetComponent<Rigidbody2D>();
            }

            OnlineLobbyInfo lobby = onlineManager.CurrentLobby;
            if (lobby?.Players != null)
            {
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    OnlinePlayerInfo member = lobby.Players[i];
                    if (member != null && member.PlayerId == onlineManager.LocalPlayerId)
                    {
                        slot = PlayerColorPalette.GetLobbyPlayerSlot(lobby, onlineManager.LocalPlayerId);
                        break;
                    }
                }
            }
            if (slot < 0) slot = ParseSlot(GetArg(Environment.GetCommandLineArgs(), "-pico-regression-name"));

            if (definition == null || definition.stageId != stageManager.CurrentStageId)
            {
                definition = StageTestDefinition.For(stageManager.CurrentStageId);
                bestDistance = float.PositiveInfinity;
                lastProgressAt = Time.unscaledTime;
            }
            RebuildObjectIndex();

            if (smokeSubscribedManager != onlineManager)
            {
                if (smokeSubscribedManager != null)
                    smokeSubscribedManager.BodyDataReceived -= HandleSmokeBodyData;
                smokeSubscribedManager = onlineManager;
                smokeSubscribedManager.BodyDataReceived += HandleSmokeBodyData;
            }
        }

        private void HandleSmokeBodyData(OnlineBodyData data)
        {
            if (!smokeMode || !smokeBodyWindowOpened || data == null
                || string.IsNullOrEmpty(data.PlayerId) || string.IsNullOrEmpty(data.Json)) return;
            smokeBodyPlayers.Add(data.PlayerId);
        }

        private void UpdateMultiplayerSmoke()
        {
            if (finished) return;
            OnlinePlayerInfo[] roster = onlineManager.CurrentLobby?.Players;
            int rosterCount = CountValidPlayers(roster);
            if (!smokeStarted)
            {
                if (stageManager.CurrentStageId == "title" || !stageManager.IsGameplayActive)
                {
                    player.SetScriptedInput(0f, false);
                    WritePeriodicReport("WAITING_FOR_STAGE");
                    return;
                }

                smokeStarted = true;
                smokeStartedAt = Time.unscaledTime;
                smokeStartPosition = player.transform.position;
                AddSmokeCheck(rosterCount == smokeExpectedPlayers,
                    $"lobby roster is {smokeExpectedPlayers}", $"lobby roster was {rosterCount}/{smokeExpectedPlayers}");
                AddSmokeCheck(AllPlayerIdsUnique(roster), "all player IDs are unique", "duplicate or missing player ID");
                lastAction = "multiplayer smoke started";
                Debug.Log($"[PICO MULTI SMOKE] START stage={stageManager.CurrentStageId} slot={slot} roster={rosterCount}");
            }

            float elapsed = Time.unscaledTime - smokeStartedAt;
            if (elapsed < 2f)
            {
                player.SetScriptedInput(0f, false);
            }
            else if (elapsed < 5f)
            {
                float horizontal = slot % 2 == 0 ? 1f : -1f;
                bool jump = elapsed >= 2.5f + slot * 0.15f && elapsed < 2.65f + slot * 0.15f;
                player.SetScriptedInput(horizontal, jump, jump);
                lastAction = jump ? "independent jump input" : $"independent move input {horizontal:+0;-0}";
            }
            else
            {
                player.SetScriptedInput(0f, false);
                if (!smokeMoveRecorded)
                {
                    smokeMoveRecorded = true;
                    smokeMovementDistance = Vector2.Distance(smokeStartPosition, player.transform.position);
                    AddSmokeCheck(smokeMovementDistance >= 0.2f,
                        $"local input moved only owned player ({smokeMovementDistance:0.00})",
                        $"owned player did not respond to scripted input ({smokeMovementDistance:0.00})");
                }
            }

            if (!smokeBodyWindowOpened && elapsed >= 6.5f)
            {
                smokeBodyWindowOpened = true;
                smokeBodyPlayers.Clear();
                lastAction = "opened redraw synchronization observation window";
            }

            float redrawAt = 7.5f + (smokeConcurrentRedraw ? 0f : slot * 1.5f);
            if (!smokeRedrawAttempted && elapsed >= redrawAt)
            {
                if (stageManager.CanEnterDrawingMode())
                {
                    smokeRedrawAttempted = true;
                    stageManager.EnterDrawingMode();
                    smokeRedrawEntered = stageManager.IsDrawingMode;
                    smokeControlsDisabledDuringDraw = !player.ControlsEnabled;
                    if (smokeRedrawEntered)
                    {
                        // Change only the already test-owned head/torso artwork. This
                        // exercises the normal DRAW validation, body rebuild and send path.
                        drawManager.LoadState(CreateSmokeRedrawPreset(), false);
                        drawManager.ConfirmDrawing();
                        lastAction = "confirmed a unique redraw through normal DRAW commands";
                    }
                }
                else if (elapsed >= redrawAt + 5f)
                {
                    smokeRedrawAttempted = true;
                    lastAction = "redraw remained unavailable";
                }
            }

            if (smokeRedrawEntered && !stageManager.IsDrawingMode)
            {
                smokeRedrawClosed = true;
                smokeControlsRestored |= player.ControlsEnabled;
            }

            if (!smokeScreenshotTaken && elapsed >= 15f)
            {
                smokeScreenshotTaken = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(reportDirectory, $"smoke-client-{Mathf.Max(0, slot)}.png"));
            }

            if (elapsed < 20f)
            {
                WritePeriodicReport("RUNNING_SMOKE");
                return;
            }

            AddSmokeCheck(smokeRedrawEntered, "DRAW opened inside stage", "DRAW could not open inside stage");
            AddSmokeCheck(smokeControlsDisabledDuringDraw,
                "owned player controls disabled during DRAW", "owned player controls remained active during DRAW");
            AddSmokeCheck(smokeRedrawClosed, "DRAW confirmation closed normally", "DRAW confirmation did not close");
            AddSmokeCheck(smokeControlsRestored,
                "owned player controls restored after DRAW", "owned player controls were not restored after DRAW");
            // The Direct TCP host applies its own body locally and does not echo
            // that packet back through BodyDataReceived. Every peer body must be
            // observed; a client may additionally receive its own server echo.
            int expectedRemoteBodies = Mathf.Max(1, smokeExpectedPlayers - 1);
            AddSmokeCheck(smokeBodyPlayers.Count >= expectedRemoteBodies,
                $"redraw bodies received for every remote player ({smokeBodyPlayers.Count}/{expectedRemoteBodies})",
                $"redraw body synchronization reached only {smokeBodyPlayers.Count}/{expectedRemoteBodies} remote players");
            AddSmokeCheck(CountObservedPlayers() == smokeExpectedPlayers,
                $"all {smokeExpectedPlayers} player avatars are observable",
                $"only {CountObservedPlayers()}/{smokeExpectedPlayers} player avatars are observable");
            AddSmokeCheck(exceptions == 0, "no Unity exceptions", $"Unity exceptions: {exceptions}");
            AddSmokeCheck(suspiciousPhysicsEvents == 0,
                "no NaN/Infinity or abnormal velocity", $"suspicious physics events: {suspiciousPhysicsEvents}");

            finished = true;
            bool passed = smokeFailedChecks.Count == 0;
            string result = passed ? "PASS" : "FAIL";
            WriteReport(result, true);
            Debug.Log($"[PICO MULTI SMOKE] {result} slot={slot} passed={smokePassedChecks.Count} failed={smokeFailedChecks.Count}");
            StartCoroutine(QuitAfterSmoke());
        }

        private IEnumerator QuitAfterSmoke()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(smokeFailedChecks.Count == 0 ? 0 : 2);
        }

        private DrawManager.DrawingState CreateSmokeRedrawPreset()
        {
            DrawManager.DrawingState state = drawManager.CreateState();
            DrawManager.BodyPart[] parts = DrawManager.GetPartsForSpecies(state.Species);
            DrawManager.BodyPart part = state.Species == DrawManager.Species.Slime
                ? DrawManager.BodyPart.SlimeBody
                : DrawManager.BodyPart.Head;
            bool found = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != part) continue;
                found = true;
                break;
            }
            if (!found && parts.Length > 0) part = parts[0];
            state.Part = part;

            if (!state.Points.TryGetValue(state.Species, out Dictionary<DrawManager.BodyPart, List<Vector2>> species)
                || !species.TryGetValue(part, out List<Vector2> points) || points == null)
                return state;

            float wobble = 3f + Mathf.Max(0, slot) * 1.25f;
            if (points.Count <= 2 && points.Count > 0)
            {
                Vector2 first = points[0];
                Vector2 last = points[points.Count - 1];
                points.Insert(1, (first + last) * 0.5f + new Vector2(wobble, -wobble));
            }
            else
            {
                // Preserve both connection endpoints and add a small, unmistakable
                // hand-drawn wobble to the existing species-specific part.
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector2 point = points[i];
                    point.x += (i % 2 == 0 ? wobble : -wobble);
                    point.y += (i % 3 == 0 ? wobble * 0.6f : -wobble * 0.35f);
                    points[i] = point;
                }
            }
            return state;
        }

        private void AddSmokeCheck(bool passed, string passText, string failText)
        {
            List<string> target = passed ? smokePassedChecks : smokeFailedChecks;
            string value = passed ? passText : failText;
            if (!target.Contains(value)) target.Add(value);
        }

        private int CountObservedPlayers()
        {
            PlayerObservation[] observations = ObservePlayers();
            int count = 0;
            for (int i = 0; i < observations.Length; i++)
                if (observations[i].species != "Missing") count++;
            return count;
        }

        private static int CountValidPlayers(OnlinePlayerInfo[] players)
        {
            if (players == null) return 0;
            int count = 0;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) count++;
            return count;
        }

        private static bool AllPlayerIdsUnique(OnlinePlayerInfo[] players)
        {
            if (players == null || players.Length == 0) return false;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < players.Length; i++)
            {
                string id = players[i]?.PlayerId;
                if (string.IsNullOrEmpty(id) || !ids.Add(id)) return false;
            }
            return true;
        }

        private bool TryPrepareRoleSpecies()
        {
            if (definition == null || drawManager == null || roleSpeciesAttempted
                || slot != 2
                || IsObjectActive(definition.firstButton.completionObjectId)
                || HasStrongThrowProfile())
                return false;

            if (!stageManager.CanEnterDrawingMode()) return false;
            roleSpeciesAttempted = true;
            stageManager.EnterDrawingMode();
            drawManager.LoadState(CreateStrongThrowHumanPreset(), false);
            drawManager.ConfirmDrawing();
            lastAction = "load and confirm strong-arm human preset through DRAW commands";
            return true;
        }

        private void TryPrepareLobbySpecies()
        {
            if ((lobbySpeciesAttempted && lobbyPreparedSlot == slot) || drawManager == null
                || stageManager.CurrentStageId != "title")
                return;

            lobbySpeciesAttempted = true;
            lobbyPreparedSlot = slot;
            drawManager.LoadState(slot == 2
                ? CreateStrongThrowHumanPreset()
                : CreateFunnyHumanPreset(slot), false);
            bool applied = drawManager.TryApplyDrawing();
            lastAction = applied
                ? $"load and confirm funny human preset {slot + 1} before stage start"
                : $"funny human preset {slot + 1} was rejected";
        }

        private bool HasStrongThrowProfile()
        {
            PlayerAbilityController ability = player != null
                ? player.GetComponent<PlayerAbilityController>()
                : null;
            return ability != null && ability.CurrentProfile.Species == DrawManager.Species.Human
                && ability.CurrentProfile.ArmInk >= 80f;
        }

        private DrawManager.DrawingState CreateStrongThrowHumanPreset()
        {
            DrawManager.DrawingState state = drawManager.CreateState();
            state.Species = DrawManager.Species.Human;
            state.Part = DrawManager.BodyPart.LeftArm;
            ApplyFunnyTorsoAndHead(state, slot);
            Dictionary<DrawManager.BodyPart, List<Vector2>> human =
                state.Points[DrawManager.Species.Human];
            human[DrawManager.BodyPart.LeftArm] = new List<Vector2>
            {
                new Vector2(0f, 15f), new Vector2(-25f, 35f),
                new Vector2(-5f, -30f), new Vector2(-30f, 30f),
                new Vector2(-5f, -35f)
            };
            human[DrawManager.BodyPart.RightArm] = new List<Vector2>
            {
                new Vector2(0f, 15f), new Vector2(25f, 35f),
                new Vector2(5f, -30f), new Vector2(30f, 30f),
                new Vector2(5f, -35f)
            };
            return state;
        }

        private DrawManager.DrawingState CreateFunnyHumanPreset(int appearanceSlot)
        {
            DrawManager.DrawingState state = drawManager.CreateState();
            state.Species = DrawManager.Species.Human;
            state.Part = DrawManager.BodyPart.Head;
            ApplyFunnyTorsoAndHead(state, appearanceSlot);
            return state;
        }

        private static void ApplyFunnyTorsoAndHead(
            DrawManager.DrawingState state, int appearanceSlot)
        {
            Dictionary<DrawManager.BodyPart, List<Vector2>> human =
                state.Points[DrawManager.Species.Human];
            switch (Mathf.Clamp(appearanceSlot, 0, 3))
            {
                case 0:
                    // Pear body with a tiny three-point crown.
                    human[DrawManager.BodyPart.Torso] = new List<Vector2>
                    {
                        new Vector2(0f, 70f), new Vector2(-22f, 66f),
                        new Vector2(-42f, -62f), new Vector2(38f, -68f),
                        new Vector2(24f, 64f), new Vector2(0f, 70f)
                    };
                    human[DrawManager.BodyPart.Head] = new List<Vector2>
                    {
                        new Vector2(0f, -70f), new Vector2(-48f, -62f),
                        new Vector2(-42f, 14f), new Vector2(-8f, -2f),
                        new Vector2(4f, 26f), new Vector2(18f, 0f),
                        new Vector2(48f, 18f), new Vector2(48f, -60f),
                        new Vector2(0f, -70f)
                    };
                    break;
                case 1:
                    // Crooked hourglass with an oversized sideways nose.
                    human[DrawManager.BodyPart.Torso] = new List<Vector2>
                    {
                        new Vector2(0f, 70f), new Vector2(-38f, 62f),
                        new Vector2(-19f, 0f), new Vector2(-38f, -66f),
                        new Vector2(35f, -69f), new Vector2(18f, 0f),
                        new Vector2(40f, 60f), new Vector2(0f, 70f)
                    };
                    human[DrawManager.BodyPart.Head] = new List<Vector2>
                    {
                        new Vector2(0f, -70f), new Vector2(-42f, -62f),
                        new Vector2(-48f, 16f), new Vector2(8f, 22f),
                        new Vector2(56f, 4f), new Vector2(30f, -12f),
                        new Vector2(44f, -60f),
                        new Vector2(0f, -70f)
                    };
                    break;
                case 2:
                    // Tiny dented trophy body and a cheap cat-ear head leave
                    // enough ink for this role's deliberately oversized arms.
                    human[DrawManager.BodyPart.Torso] = new List<Vector2>
                    {
                        new Vector2(0f, 70f), new Vector2(-20f, 58f),
                        new Vector2(-18f, -60f), new Vector2(18f, -60f),
                        new Vector2(20f, 58f),
                        new Vector2(0f, 70f)
                    };
                    human[DrawManager.BodyPart.Head] = new List<Vector2>
                    {
                        new Vector2(0f, -70f), new Vector2(-30f, -58f),
                        new Vector2(-24f, 4f), new Vector2(0f, 24f),
                        new Vector2(24f, 4f), new Vector2(30f, -58f),
                        new Vector2(0f, -70f)
                    };
                    break;
                default:
                    // Squashed body and lopsided lightning-bolt hair.
                    human[DrawManager.BodyPart.Torso] = new List<Vector2>
                    {
                        new Vector2(0f, 70f), new Vector2(-36f, 65f),
                        new Vector2(-44f, -58f), new Vector2(30f, -70f),
                        new Vector2(42f, -8f), new Vector2(24f, 62f),
                        new Vector2(0f, 70f)
                    };
                    human[DrawManager.BodyPart.Head] = new List<Vector2>
                    {
                        new Vector2(0f, -70f), new Vector2(-50f, -55f),
                        new Vector2(-42f, 16f), new Vector2(-8f, 2f),
                        new Vector2(8f, 28f), new Vector2(20f, 0f),
                        new Vector2(52f, 16f), new Vector2(44f, -60f),
                        new Vector2(0f, -70f)
                    };
                    break;
            }
        }

        private void RebuildObjectIndex()
        {
            objects.Clear();
            Transform root = stageLoader != null ? stageLoader.LoadedStageRoot : null;
            if (root == null) return;
            StageEditorObject[] markers = root.GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker != null && !string.IsNullOrEmpty(marker.objectId))
                    objects[marker.objectId] = marker;
            }
        }

        private StageObjective SelectObjective()
        {
            if (definition == null) return null;
            if (!IsObjectActive(definition.firstButton.completionObjectId))
                return slot == definition.firstButton.roleSlot ? definition.firstButton : definition.waitAtStart;
            if (!IsObjectActive(definition.upperButton.completionObjectId))
                return slot == definition.upperButton.roleSlot ? definition.upperButton : definition.waitAtBridge;

            bool firstWall = IsObjectActive(definition.firstKey.completionObjectId);
            bool secondWall = IsObjectActive(definition.secondKey.completionObjectId);
            if (firstWall && slot == definition.firstKey.roleSlot) return definition.firstKey;
            if (secondWall && slot == definition.secondKey.roleSlot) return definition.secondKey;
            if (firstWall || secondWall) return definition.waitForKeys;
            return definition.goal;
        }

        private bool TryDriveCooperativeTraversal()
        {
            if (definition == null) return false;
            if (!IsObjectActive(definition.firstButton.completionObjectId))
            {
                firstBridgeSeenAt = -1f;
                return DriveFirstFriendThrow();
            }
            if (firstBridgeSeenAt < 0f)
                firstBridgeSeenAt = Time.unscaledTime;
            if (Time.unscaledTime - firstBridgeSeenAt < 3f)
            {
                player.SetScriptedInput(0f, false);
                lastAction = "wait for revealed bridge to finish extending";
                return true;
            }
            if (!IsObjectActive(definition.upperButton.completionObjectId))
                return DriveUpperFriendThrow();
            return false;
        }

        private bool TryPreparePrecisionThrowProfile()
        {
            if (slot != 2 || precisionRedrawCompleted) return false;
            if (precisionRedrawStarted) return true;

            PlayerAbilityController ability = player != null
                ? player.GetComponent<PlayerAbilityController>()
                : null;
            if (ability == null || ability.CurrentProfile.ArmInk <= 45f)
            {
                precisionRedrawCompleted = true;
                return false;
            }
            if (drawManager == null) return true;
            if (!stageManager.CanEnterDrawingMode())
            {
                DriveToward(new Vector2(18.5f, 13.5f));
                lastAction = "leave jump pad and land before precision redraw";
                return true;
            }

            precisionRedrawStarted = true;
            StartCoroutine(ApplyPrecisionThrowProfile());
            lastAction = "open DRAW to replace strong arms after first bridge";
            return true;
        }

        private IEnumerator ApplyPrecisionThrowProfile()
        {
            stageManager.EnterDrawingMode();
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
            drawManager.LoadState(CreatePrecisionThrowHumanPreset(), false);
            yield return null;
            drawManager.ConfirmDrawing();
            precisionRedrawCompleted = true;
            lastAction = "confirmed normal arms for controlled cooperative throws";
        }

        private DrawManager.DrawingState CreatePrecisionThrowHumanPreset()
        {
            DrawManager.DrawingState state = drawManager.CreateState();
            state.Species = DrawManager.Species.Human;
            state.Part = DrawManager.BodyPart.LeftArm;
            Dictionary<DrawManager.BodyPart, List<Vector2>> human =
                state.Points[DrawManager.Species.Human];
            human[DrawManager.BodyPart.LeftArm] = new List<Vector2>
            {
                new Vector2(115f, 0f), new Vector2(20f, -24f)
            };
            human[DrawManager.BodyPart.RightArm] = new List<Vector2>
            {
                new Vector2(-115f, 0f), new Vector2(-20f, -24f)
            };
            return state;
        }

        private bool DriveFirstFriendThrow()
        {
            PlayerController2D receiver = GetPlayerForSlot(3);
            Rigidbody2D receiverBody = receiver != null ? receiver.GetComponent<Rigidbody2D>() : null;

            if (slot == 3)
            {
                if (body.position.x >= 8.8f)
                    DriveToward(definition.firstButton.fallbackPosition);
                else if (body.position.x <= 3.45f)
                    DriveToward(new Vector2(2.55f, 0.5f));
                else
                    DriveToward(definition.firstButton.fallbackPosition);
                lastAction = body.position.x >= 8.8f
                    ? "receiver presses first button"
                    : body.position.x > 3.45f
                        ? "receiver steers across first gap after throw"
                        : "receiver waits at first throw mark";
                return true;
            }

            if (slot != 2)
            {
                player.SetScriptedInput(0f, false);
                lastAction = "wait while throw team reveals first bridge";
                return true;
            }

            if (receiver == null || receiverBody == null)
            {
                player.SetScriptedInput(0f, false);
                lastAction = "wait for first throw receiver observation";
                return true;
            }

            if (receiverBody.position.x >= 8.8f)
            {
                DriveToward(new Vector2(2.25f, 0.5f));
                lastAction = "thrower waits for first button";
                return true;
            }

            if (carry != null && carry.IsHoldingTarget(receiver.transform))
            {
                player.SetScriptedInput(0f, false);
                if (Time.unscaledTime >= nextActionAt)
                {
                    nextActionAt = Time.unscaledTime + 1f;
                    carry.ThrowHeldForScript(new Vector2(1f, 0.38f));
                    player.SetScriptedInput(0f, false);
                    lastAction = "throw receiver across first gap";
                }
                return true;
            }

            // Once released over the gap, do not chase the airborne receiver.
            if (receiverBody.position.x > 3.45f)
            {
                DriveToward(new Vector2(2.25f, 0.5f));
                lastAction = "observe receiver crossing first gap";
                return true;
            }

            Vector2 pickupPoint = receiverBody.position + Vector2.left * 0.65f;
            DriveToward(pickupPoint);
            if (carry != null && Vector2.Distance(body.position, receiverBody.position) <= 1.55f
                && Time.unscaledTime >= nextActionAt)
            {
                nextActionAt = Time.unscaledTime + 0.75f;
                carry.TryPickupForScript();
                lastAction = carry.IsHoldingTarget(receiver.transform)
                    ? "pick up receiver for first gap"
                    : "attempt receiver pickup for first gap";
            }
            return true;
        }

        private bool DriveUpperFriendThrow()
        {
            PlayerController2D receiver = GetPlayerForSlot(3);
            Rigidbody2D receiverBody = receiver != null ? receiver.GetComponent<Rigidbody2D>() : null;

            if (slot == 3)
            {
                if (body.position.y >= 12.8f)
                {
                    float upperButtonDelta = definition.upperButton.fallbackPosition.x
                        - body.position.x;
                    player.SetScriptedInput(Mathf.Abs(upperButtonDelta) > 0.25f
                        ? Mathf.Sign(upperButtonDelta) * 0.2f
                        : 0f, false);
                }
                else
                    DriveToward(new Vector2(14.15f, 0.5f));
                lastAction = body.position.y >= 12.8f
                    ? "receiver moves onto upper button"
                    : "receiver waits at upper throw mark";
                return true;
            }

            if (slot != 2)
            {
                DriveToward(new Vector2(slot == 0 ? 8.5f : 9.8f, 0.5f));
                lastAction = "cross revealed bridge and wait for jump pad";
                return true;
            }

            if (receiver == null || receiverBody == null)
            {
                player.SetScriptedInput(0f, false);
                lastAction = "wait for upper throw receiver observation";
                return true;
            }

            if (receiverBody.position.y >= 8f)
            {
                DriveToward(new Vector2(13f, 0.5f));
                lastAction = "thrower waits for upper button";
                return true;
            }

            if (body.position.x < 10.2f)
            {
                DriveToward(new Vector2(11.2f, 3f));
                lastAction = "thrower jumps bridge-to-platform seam";
                return true;
            }

            if (carry != null && carry.IsHoldingTarget(receiver.transform))
            {
                if (body.position.x < 12.5f)
                {
                    DriveToward(new Vector2(13.2f, 0.5f));
                    lastAction = "carry upper receiver to throw mark";
                    return true;
                }
                player.SetScriptedInput(0f, false);
                if (Time.unscaledTime >= nextActionAt)
                {
                    nextActionAt = Time.unscaledTime + 1f;
                    carry.ThrowHeldForScript(new Vector2(0.08f, 1f));
                    player.SetScriptedInput(0f, false);
                    lastAction = "throw receiver onto upper platform";
                }
                return true;
            }

            if (receiverBody.position.x < 13.2f)
            {
                Vector2 earlyPickupPoint = receiverBody.position + Vector2.left * 0.65f;
                DriveToward(earlyPickupPoint);
                if (carry != null
                    && Vector2.Distance(body.position, receiverBody.position) <= 1.55f
                    && Time.unscaledTime >= nextActionAt)
                {
                    nextActionAt = Time.unscaledTime + 0.75f;
                    carry.TryPickupForScript();
                    lastAction = "pick up upper receiver on right bank";
                }
                return true;
            }

            Vector2 pickupPoint = receiverBody.position + Vector2.left * 0.65f;
            DriveToward(pickupPoint);
            if (carry != null && Vector2.Distance(body.position, receiverBody.position) <= 1.55f
                && Time.unscaledTime >= nextActionAt)
            {
                nextActionAt = Time.unscaledTime + 0.75f;
                carry.TryPickupForScript();
                lastAction = carry.IsHoldingTarget(receiver.transform)
                    ? "pick up receiver for upper platform"
                    : "attempt receiver pickup for upper platform";
            }
            return true;
        }

        private PlayerController2D GetPlayerForSlot(int requestedSlot)
        {
            OnlineLobbyInfo lobby = onlineManager?.CurrentLobby;
            if (lobby?.Players == null || stageManager == null) return null;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo member = lobby.Players[i];
                if (member != null
                    && PlayerColorPalette.GetLobbyPlayerSlot(lobby, member.PlayerId) == requestedSlot)
                    return stageManager.GetOnlinePlayerController(member.PlayerId);
            }
            return null;
        }

        private bool TryDriveBasinEscape()
        {
            if (!basinEscapeStarted)
            {
                basinEscapeStarted = AreAllPlayersInBasin();
                if (!basinEscapeStarted) return false;
                lastProgressAt = Time.unscaledTime;
                lastAction = "all players assembled in right basin";
            }

            PlayerController2D receiver = GetNextBasinReceiver();
            if (receiver != null)
            {
                Rigidbody2D receiverBody = receiver.GetComponent<Rigidbody2D>();
                int receiverSlot = GetSlotForPlayer(receiver);
                if (slot == 2)
                {
                    if (carry != null && carry.IsHoldingTarget(receiver.transform))
                    {
                        player.SetScriptedInput(0f, false);
                        if (Time.unscaledTime >= nextActionAt)
                        {
                            nextActionAt = Time.unscaledTime + 1f;
                            carry.ThrowHeldForScript(new Vector2(1f, 0.3f));
                            lastAction = $"throw player {receiverSlot + 1} out of basin";
                        }
                        return true;
                    }

                    Vector2 pickupPoint = receiverBody.position + Vector2.left * 0.65f;
                    DriveToward(pickupPoint);
                    if (carry != null
                        && Vector2.Distance(body.position, receiverBody.position) <= 1.55f
                        && Time.unscaledTime >= nextActionAt)
                    {
                        nextActionAt = Time.unscaledTime + 0.75f;
                        carry.TryPickupForScript();
                        lastAction = $"pick up player {receiverSlot + 1} in basin";
                    }
                    return true;
                }

                if (slot == receiverSlot)
                {
                    if (body.position.y >= 9f || body.position.x >= 35.5f)
                    {
                        DriveToward(new Vector2(40f, 13f));
                        lastAction = "steer onto right ledge after basin throw";
                    }
                    else
                    {
                        player.SetScriptedInput(0f, false);
                        lastAction = "wait to be thrown out of basin";
                    }
                    return true;
                }

                player.SetScriptedInput(0f, false);
                lastAction = "wait for assigned basin throw";
                return true;
            }

            if (slot == 2 && !IsOutsideBasin(body.position))
            {
                if (!HasBasinJumpProfile())
                {
                    if (!basinJumpAttempted && drawManager != null
                        && stageManager.CanEnterDrawingMode())
                    {
                        basinJumpAttempted = true;
                        stageManager.EnterDrawingMode();
                        drawManager.LoadState(CreateBasinJumpHumanPreset(), false);
                        drawManager.ConfirmDrawing();
                        lastAction = "redraw long legs through DRAW commands for basin escape";
                    }
                    else
                    {
                        player.SetScriptedInput(0f, false);
                    }
                    return true;
                }

                DriveToward(new Vector2(40f, 13f));
                lastAction = "long-leg thrower jumps out of basin";
                return true;
            }

            return !AreAllPlayersOutsideBasin();
        }

        private bool TryDriveKeyPlatformThrows()
        {
            if (!IsObjectActive(definition.upperButton.completionObjectId)) return false;
            PlayerController2D receiver = !HasReachedKeyPlatform(0)
                ? GetPlayerForSlot(0)
                : !HasReachedKeyPlatform(1) ? GetPlayerForSlot(1) : null;
            if (receiver == null) return false;

            int receiverSlot = GetSlotForPlayer(receiver);
            Rigidbody2D receiverBody = receiver.GetComponent<Rigidbody2D>();
            Vector2 stagingPoint = new Vector2(receiverSlot == 0 ? 16.2f : 19.2f, 13.5f);

            // Once the lower-key player has landed, let that player clear boxes
            // and acquire the key while the second throw is coordinated.
            if (slot == 0 && receiverSlot == 1 && HasReachedKeyPlatform(0))
                return false;

            if (slot == receiverSlot)
            {
                bool isCarriedOnline = !string.IsNullOrEmpty(
                    stageManager.GetOnlineCarrierPlayerId(player));
                if (isCarriedOnline)
                {
                    keyPlatformWasCarried = true;
                    player.SetScriptedInput(0f, false);
                    lastAction = $"player {receiverSlot + 1} is carried for key throw";
                    return true;
                }
                if (keyPlatformWasCarried)
                {
                    keyPlatformWasCarried = false;
                    keyPlatformThrowReceived = true;
                }
                if (keyPlatformThrowReceived)
                {
                    StageObjective keyObjective = receiverSlot == 0
                        ? definition.firstKey
                        : definition.secondKey;
                    DriveToward(TryGetObject(keyObjective.objectId, out StageEditorObject key)
                        ? key.transform.position
                        : keyObjective.fallbackPosition);
                    lastAction = $"steer toward key platform {receiverSlot + 1} after throw";
                }
                else if (body.position.y < 13f)
                {
                    DriveToward(TryGetObject(definition.upperButton.completionObjectId,
                            out StageEditorObject jumpPad)
                        ? jumpPad.transform.position + Vector3.up * 0.35f
                        : new Vector2(14f, 0.5f));
                    lastAction = $"player {receiverSlot + 1} rides jump pad to throw staging";
                }
                else if (!player.IsGrounded)
                {
                    float landingX = receiverSlot == 0 ? 16.2f : 18.8f;
                    float delta = landingX - body.position.x;
                    player.SetScriptedInput(Mathf.Abs(delta) > 0.25f
                        ? Mathf.Sign(delta) * 0.4f
                        : 0f, false);
                    lastAction = $"player {receiverSlot + 1} gently lands on upper wall";
                }
                else
                {
                    DriveToward(stagingPoint);
                    lastAction = $"player {receiverSlot + 1} waits at key throw staging";
                }
                return true;
            }

            if (slot == 3)
            {
                if (carry != null && carry.IsHoldingTarget(receiver.transform))
                {
                    player.SetScriptedInput(0f, false);
                    if (Time.unscaledTime >= nextActionAt)
                    {
                        nextActionAt = Time.unscaledTime + 1f;
                        Vector2 direction = receiverSlot == 0
                            ? new Vector2(-1f, 0.55f)
                            : new Vector2(-0.8f, 1f);
                        carry.ThrowHeldForScript(direction);
                        lastAction = $"throw player {receiverSlot + 1} to key platform";
                    }
                    return true;
                }

                if (receiverBody.position.y >= 9.5f
                    && receiver.IsGrounded && player.IsGrounded)
                {
                    Vector2 pickupPoint = receiverBody.position + Vector2.right * 0.65f;
                    DriveToward(pickupPoint);
                    if (carry != null
                        && Vector2.Distance(body.position, receiverBody.position) <= 1.55f
                        && AreOtherPlayersClearForPickup(receiver)
                        && Time.unscaledTime >= nextActionAt)
                    {
                        nextActionAt = Time.unscaledTime + 0.75f;
                        carry.TryPickupForScript();
                        lastAction = $"pick up player {receiverSlot + 1} for key platform";
                    }
                }
                else
                {
                    if (body.position.y < 13f)
                    {
                        DriveToward(new Vector2(14f, 0.5f));
                        lastAction = "thrower rises vertically on jump pad";
                    }
                    else if (!player.IsGrounded)
                    {
                        DriveToward(new Vector2(16.5f, 13.5f));
                        lastAction = "thrower lands just beyond upper wall";
                    }
                    else if (body.position.x < 17f)
                    {
                        DriveToward(new Vector2(18.5f, 13.5f));
                        lastAction = "thrower walks to upper staging after landing";
                    }
                    else
                    {
                        player.SetScriptedInput(0f, false);
                        lastAction = $"thrower waits for player {receiverSlot + 1} at upper staging";
                    }
                }
                return true;
            }

            if (slot == 1 && receiverSlot == 0)
            {
                if (body.position.y < 13f)
                {
                    Vector2 parallelJumpPoint = TryGetObject(
                            definition.upperButton.completionObjectId,
                            out StageEditorObject parallelJumpPad)
                        ? (Vector2)parallelJumpPad.transform.position + new Vector2(-0.55f, 0.35f)
                        : new Vector2(13.45f, 0.5f);
                    DriveToward(parallelJumpPoint);
                    lastAction = "upper-key player rides jump pad in parallel";
                }
                else
                {
                    DriveToward(new Vector2(19.2f, 13.5f));
                    lastAction = "upper-key player waits at second throw staging";
                }
                return true;
            }

            if (slot == 2)
            {
                if (body.position.y < 13f)
                {
                    DriveToward(new Vector2(14f, 0.5f));
                    lastAction = "support player rises vertically on jump pad";
                    return true;
                }
                DriveToward(new Vector2(20.3f, 13.5f));
                lastAction = "support player clears key-throw staging lane";
                return true;
            }

            player.SetScriptedInput(0f, false);
            lastAction = "wait during assigned key-platform throws";
            return true;
        }

        private bool AreOtherPlayersClearForPickup(PlayerController2D receiver)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == 3) continue;
                PlayerController2D candidate = GetPlayerForSlot(i);
                if (candidate == null || candidate == receiver) continue;
                if (Vector2.Distance(body.position, candidate.transform.position) < 2f)
                    return false;
            }
            return true;
        }

        private bool HasReachedKeyPlatform(int requestedSlot)
        {
            PlayerController2D candidate = GetPlayerForSlot(requestedSlot);
            if (candidate == null) return false;
            Vector2 position = candidate.transform.position;
            return requestedSlot == 0
                ? position.x < 9f && position.y >= 15f
                : position.x < 9f && position.y >= 28f;
        }

        private bool TryDiscardKeyObstacle()
        {
            if (objective == null || objective.kind != ObjectiveKind.CarryTo
                || carry == null || !carry.IsHolding || Time.unscaledTime < nextActionAt)
                return false;
            if (TryGetObject(objective.objectId, out StageEditorObject key)
                && carry.IsHoldingTarget(key.transform))
                return false;

            nextActionAt = Time.unscaledTime + 0.75f;
            carry.ThrowHeldForScript(slot == 0 ? new Vector2(-1f, 0.35f) : new Vector2(1f, 0.35f));
            player.SetScriptedInput(0f, false);
            lastAction = "throw obstructing box away from key";
            return true;
        }

        private bool AreAllPlayersInBasin()
        {
            for (int i = 0; i < 4; i++)
            {
                PlayerController2D candidate = GetPlayerForSlot(i);
                if (candidate == null || !IsInsideBasin(candidate.transform.position)) return false;
            }
            return true;
        }

        private bool AreAllPlayersOutsideBasin()
        {
            for (int i = 0; i < 4; i++)
            {
                PlayerController2D candidate = GetPlayerForSlot(i);
                if (candidate == null || !IsOutsideBasin(candidate.transform.position)) return false;
            }
            return true;
        }

        private PlayerController2D GetNextBasinReceiver()
        {
            int[] order = { 0, 1, 3 };
            for (int i = 0; i < order.Length; i++)
            {
                PlayerController2D candidate = GetPlayerForSlot(order[i]);
                if (candidate != null && !IsOutsideBasin(candidate.transform.position)) return candidate;
            }
            return null;
        }

        private int GetSlotForPlayer(PlayerController2D candidate)
        {
            OnlineLobbyInfo lobby = onlineManager?.CurrentLobby;
            if (candidate == null || lobby?.Players == null) return -1;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo member = lobby.Players[i];
                if (member != null && stageManager.GetOnlinePlayerController(member.PlayerId) == candidate)
                    return PlayerColorPalette.GetLobbyPlayerSlot(lobby, member.PlayerId);
            }
            return -1;
        }

        private static bool IsInsideBasin(Vector2 position) =>
            position.x >= 25.5f && position.x <= 36.4f && position.y < 10f;

        private static bool IsOutsideBasin(Vector2 position) =>
            position.x >= 37.5f && position.y >= 9.5f;

        private bool HasBasinJumpProfile()
        {
            PlayerAbilityController ability = player != null
                ? player.GetComponent<PlayerAbilityController>()
                : null;
            return ability != null && ability.CurrentProfile.Species == DrawManager.Species.Human
                && ability.CurrentProfile.LegInk >= 160f;
        }

        private DrawManager.DrawingState CreateBasinJumpHumanPreset()
        {
            DrawManager.DrawingState state = drawManager.CreateState();
            state.Species = DrawManager.Species.Human;
            state.Part = DrawManager.BodyPart.LeftLeg;
            Dictionary<DrawManager.BodyPart, List<Vector2>> human =
                state.Points[DrawManager.Species.Human];
            human[DrawManager.BodyPart.LeftLeg] = new List<Vector2>
            {
                new Vector2(0f, 40f), new Vector2(-20f, -40f),
                new Vector2(-35f, 40f), new Vector2(-45f, -40f),
                new Vector2(-35f, 40f), new Vector2(-20f, -40f)
            };
            human[DrawManager.BodyPart.RightLeg] = new List<Vector2>
            {
                new Vector2(0f, 40f), new Vector2(20f, -40f),
                new Vector2(35f, 40f), new Vector2(45f, -40f),
                new Vector2(35f, 40f), new Vector2(20f, -40f)
            };
            return state;
        }

        private Vector2 ResolveObjectivePosition(StageObjective selected)
        {
            if (selected == definition.waitAtStart && body != null)
                return body.position;
            if (selected == definition.waitAtBridge)
                return new Vector2(9.7f + slot * 1.15f, 0.5f);
            if (selected == definition.waitForKeys)
                return new Vector2(27f + slot, 7f);
            // 1-1's apparent large floor is a -90 degree platform: in world
            // space it is a tall pillar from x=15.75 to 21.25. Back away from
            // its left face, jump above the top, then steer onto the button.
            if (selected == definition.upperButton && body != null && body.position.y < 16.5f)
                return new Vector2(11.5f, 18f);
            if (selected.kind == ObjectiveKind.CarryTo && carry != null && !carry.IsHolding
                && body != null)
            {
                float ascentThreshold = selected == definition.secondKey ? 27f : 15.5f;
                if (body.position.y < ascentThreshold
                    && TryGetObject(definition.upperButton.completionObjectId, out StageEditorObject jumpPad))
                    return jumpPad.transform.position + Vector3.up * 0.35f;
            }
            bool holdsSelectedObject = selected.kind == ObjectiveKind.CarryTo
                && carry != null
                && TryGetObject(selected.objectId, out StageEditorObject selectedObject)
                && carry.IsHoldingTarget(selectedObject.transform);
            if (selected.kind == ObjectiveKind.CarryTo && holdsSelectedObject
                && body != null && body.position.x < 25f && body.position.y < 11f
                && TryGetObject(definition.upperButton.completionObjectId, out StageEditorObject carryJumpPad))
            {
                // A key collected on the tower still has to be carried back
                // through the jump pad before heading toward the keyholes.
                return carryJumpPad.transform.position + Vector3.up * 0.35f;
            }
            string id = selected.kind == ObjectiveKind.CarryTo && holdsSelectedObject
                ? selected.targetObjectId
                : selected.objectId;
            return TryGetObject(id, out StageEditorObject marker)
                ? marker.transform.position
                : selected.fallbackPosition;
        }

        private void DriveToward(Vector2 target)
        {
            Vector2 position = body.position;
            Vector2 delta = target - position;
            float horizontal = Mathf.Abs(delta.x) > 0.3f ? Mathf.Sign(delta.x) : 0f;
            bool recovery = Time.unscaledTime < recoveryUntil;
            if (recovery) horizontal = recoveryDirection;

            bool obstacleAhead = HasSolidAhead(horizontal);
            bool gapAhead = !HasGroundAhead(horizontal);
            bool needsHeight = delta.y > 0.8f;
            bool jump = Time.unscaledTime >= nextJumpAt
                && (player.IsGrounded || gapAhead)
                && (needsHeight || obstacleAhead || gapAhead
                    || Mathf.Abs(body.linearVelocity.x) < 0.18f && Mathf.Abs(horizontal) > 0.1f);
            if (jump)
            {
                nextJumpAt = Time.unscaledTime + (recovery ? 0.28f : 0.48f);
                lastAction = $"jump toward {objective.name}";
            }
            else
            {
                lastAction = $"move {horizontal:0.0} toward {objective.name}";
            }
            player.SetScriptedInput(horizontal, needsHeight || gapAhead, jump);

            if (carry != null && objective.kind == ObjectiveKind.CarryTo
                && Vector2.Distance(position, target) < (carry.IsHolding ? 2.2f : 1.5f)
                && Time.unscaledTime >= nextActionAt)
            {
                nextActionAt = Time.unscaledTime + 0.9f;
                Vector2 actionDirection = carry.IsHolding
                    ? (target - position).normalized
                    : Vector2.right;
                carry.ApplyActionForScript(false, true, actionDirection, true);
                lastAction = carry.IsHolding ? $"carry toward {objective.targetObjectId}" : "press action";
            }
        }

        private bool HasSolidAhead(float direction)
        {
            if (Mathf.Abs(direction) < 0.1f) return false;
            RaycastHit2D[] hits = Physics2D.RaycastAll(body.position + Vector2.up * 0.15f,
                Vector2.right * direction, 1.15f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i].collider;
                if (hit != null && !hit.isTrigger && !hit.transform.IsChildOf(player.transform)) return true;
            }
            return false;
        }

        private bool HasGroundAhead(float direction)
        {
            if (Mathf.Abs(direction) < 0.1f) return true;
            Vector2 origin = body.position + new Vector2(direction * 0.9f, 0.2f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 1.8f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i].collider;
                if (hit != null && !hit.isTrigger && !hit.transform.IsChildOf(player.transform)) return true;
            }
            return false;
        }

        private void UpdateProgress(Vector2 target)
        {
            string objectiveName = objective != null ? objective.name : string.Empty;
            if (!string.Equals(progressObjectiveName, objectiveName, StringComparison.Ordinal))
            {
                progressObjectiveName = objectiveName;
                bestDistance = float.PositiveInfinity;
                lastProgressAt = Time.unscaledTime;
            }
            float distance = Vector2.Distance(body.position, target);
            if (distance + 0.5f < bestDistance)
            {
                bestDistance = distance;
                lastProgressAt = Time.unscaledTime;
            }
            if (objective != null && objective.kind == ObjectiveKind.Wait)
            {
                if (IsLocalHost() && Time.unscaledTime - lastProgressAt >= 45f)
                {
                    retries++;
                    stageManager.Retry();
                    lastProgressAt = Time.unscaledTime;
                    lastAction = "host requested normal stage retry after coordinator timeout";
                }
                return;
            }
            if (Time.unscaledTime - lastProgressAt < 12f) return;

            stalls++;
            recoveryDirection = stalls % 2 == 0 ? 1 : -1;
            recoveryUntil = Time.unscaledTime + 1.1f;
            nextJumpAt = 0f;
            bestDistance = float.PositiveInfinity;
            lastProgressAt = Time.unscaledTime;
            lastAction = $"stall recovery {stalls}: reverse and jump";
            RecordIncident("stall", $"No objective progress for 12 seconds at {objective?.name}.", stalls >= 3);

            if (stalls % 3 == 0 && IsLocalHost())
            {
                retries++;
                stageManager.Retry();
                lastAction = "host requested normal stage retry";
            }
        }

        private void DetectPhysicsAnomalies()
        {
            Vector2 position = body.position;
            Vector2 velocity = body.linearVelocity;
            bool nonFinite = !IsFinite(position.x) || !IsFinite(position.y)
                || !IsFinite(velocity.x) || !IsFinite(velocity.y);
            // The strong-arm preset deliberately throws a friend at about 96/s.
            // Keep that valid action below the anomaly threshold while still
            // catching runaway impulses well above normal traversal velocity.
            bool excessive = velocity.sqrMagnitude > 120f * 120f;
            if (!nonFinite && !excessive) return;
            if (Time.unscaledTime < nextActionAt) return;
            nextActionAt = Time.unscaledTime + 2f;
            suspiciousPhysicsEvents++;
            RecordIncident("physics", nonFinite ? "Non-finite player state." :
                $"Excessive player speed: {velocity.magnitude:0.0}.", true);
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            exceptions++;
            if (recordingIncident) return;
            RecordIncident("unity-log", condition + "\n" + stackTrace, true);
        }

        private void RecordIncident(string kind, string detail, bool screenshot)
        {
            if (recordingIncident) return;
            recordingIncident = true;
            try
            {
            string safeSlot = Mathf.Max(0, slot).ToString();
            string line = JsonUtility.ToJson(new ClientReport
            {
                runId = runId,
                stage = stageManager != null ? stageManager.CurrentStageId : string.Empty,
                transport = transport,
                playerId = onlineManager != null ? onlineManager.LocalPlayerId : string.Empty,
                slot = slot,
                result = kind + ": " + detail,
                elapsedSeconds = Time.unscaledTime - startedAt,
                deaths = deaths,
                retries = retries,
                stalls = stalls,
                exceptions = exceptions,
                suspiciousPhysicsEvents = suspiciousPhysicsEvents,
                currentObjective = objective?.name,
                lastAction = lastAction,
                passedChecks = smokePassedChecks.ToArray(),
                failedChecks = smokeFailedChecks.ToArray(),
                players = ObservePlayers(),
                importantObjects = ObserveObjects()
            });
            File.AppendAllText(Path.Combine(reportDirectory, $"incidents-client-{safeSlot}.jsonl"), line + Environment.NewLine);
            if (screenshot && Time.unscaledTime >= nextScreenshotAt)
            {
                nextScreenshotAt = Time.unscaledTime + 3f;
                string path = Path.Combine(reportDirectory,
                    $"incident-{safeSlot}-{DateTime.UtcNow:HHmmssfff}-{kind}.png");
                ScreenCapture.CaptureScreenshot(path);
            }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PICO AI TEST] Could not write incident artifact: {exception.Message}");
            }
            finally
            {
                recordingIncident = false;
            }
        }

        private void WritePeriodicReport(string result)
        {
            if (Time.unscaledTime < nextReportAt) return;
            nextReportAt = Time.unscaledTime + 1f;
            WriteReport(result, false);
        }

        private void WriteReport(string result, bool final)
        {
            int safeSlot = Mathf.Max(0, slot);
            ClientReport report = new ClientReport
            {
                runId = runId,
                stage = stageManager != null ? stageManager.CurrentStageId : string.Empty,
                transport = transport,
                playerId = onlineManager != null ? onlineManager.LocalPlayerId : string.Empty,
                slot = slot,
                result = result,
                elapsedSeconds = Time.unscaledTime - startedAt,
                deaths = deaths,
                retries = retries,
                stalls = stalls,
                exceptions = exceptions,
                suspiciousPhysicsEvents = suspiciousPhysicsEvents,
                currentObjective = objective?.name,
                lastAction = lastAction,
                passedChecks = smokePassedChecks.ToArray(),
                failedChecks = smokeFailedChecks.ToArray(),
                players = ObservePlayers(),
                importantObjects = ObserveObjects()
            };
            string json = JsonUtility.ToJson(report, true);
            TryWriteShared(Path.Combine(reportDirectory, $"client-{safeSlot}.json"), json);
            TryAppendShared(Path.Combine(reportDirectory, $"telemetry-client-{safeSlot}.jsonl"),
                JsonUtility.ToJson(report) + Environment.NewLine);
            if (final) TryWriteShared(Path.Combine(reportDirectory, $"client-{safeSlot}.done"), result);
        }

        private static void TryWriteShared(string path, string contents)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter writer = new StreamWriter(stream))
                    writer.Write(contents);
            }
            catch (IOException)
            {
                // The runner samples this file while clients are live. A missed
                // one-second sample is harmless and the next update retries.
            }
        }

        private static void TryAppendShared(string path, string contents)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter writer = new StreamWriter(stream))
                    writer.Write(contents);
            }
            catch (IOException)
            {
                // Live report readers may briefly overlap this append. The next
                // telemetry sample is sufficient for diagnostics.
            }
        }

        private PlayerObservation[] ObservePlayers()
        {
            OnlineLobbyInfo lobby = onlineManager?.CurrentLobby;
            if (lobby?.Players == null) return Array.Empty<PlayerObservation>();
            List<PlayerObservation> result = new List<PlayerObservation>();
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo info = lobby.Players[i];
                if (info == null || string.IsNullOrEmpty(info.PlayerId)) continue;
                PlayerController2D observed = stageManager.GetOnlinePlayerController(info.PlayerId);
                Rigidbody2D observedBody = observed != null ? observed.GetComponent<Rigidbody2D>() : null;
                PlayerAbilityController observedAbility = observed != null
                    ? observed.GetComponent<PlayerAbilityController>()
                    : null;
                result.Add(new PlayerObservation
                {
                    playerId = info.PlayerId,
                    slot = PlayerColorPalette.GetLobbyPlayerSlot(lobby, info.PlayerId),
                    position = observed != null ? (Vector2)observed.transform.position : Vector2.zero,
                    velocity = observedBody != null ? observedBody.linearVelocity : Vector2.zero,
                    species = observed != null ? observed.CurrentSpecies.ToString() : "Missing",
                    legInk = observedAbility != null ? observedAbility.CurrentProfile.LegInk : 0f,
                    armInk = observedAbility != null ? observedAbility.CurrentProfile.ArmInk : 0f,
                    grounded = observed != null && observed.IsGrounded,
                    controlsEnabled = observed != null && observed.ControlsEnabled,
                    respawning = observed != null && stageManager.IsPlayerRespawning(observed),
                    eliminated = observed != null && stageManager.IsPlayerEliminated(observed)
                });
            }
            return result.ToArray();
        }

        private ObjectObservation[] ObserveObjects()
        {
            List<ObjectObservation> result = new List<ObjectObservation>();
            foreach (KeyValuePair<string, StageEditorObject> pair in objects)
            {
                StageEditorObject marker = pair.Value;
                if (marker == null || !IsImportant(marker.type)) continue;
                result.Add(new ObjectObservation
                {
                    objectId = pair.Key,
                    type = marker.type.ToString(),
                    position = marker.transform.position,
                    active = marker.gameObject.activeInHierarchy
                });
            }
            return result.ToArray();
        }

        private bool IsLocalHost()
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].PlayerId == onlineManager.LocalPlayerId)
                    return players[i].IsHost;
            return false;
        }

        private bool IsObjectActive(string id)
        {
            return TryGetObject(id, out StageEditorObject marker) && marker.gameObject.activeInHierarchy;
        }

        private bool TryGetObject(string id, out StageEditorObject marker)
        {
            if (string.IsNullOrEmpty(id))
            {
                marker = null;
                return false;
            }
            return objects.TryGetValue(id, out marker) && marker != null;
        }

        private static bool IsImportant(StageObjectType type)
        {
            return type == StageObjectType.Goal || type == StageObjectType.Button
                || type == StageObjectType.Key || type == StageObjectType.Keyhole
                || type == StageObjectType.WoodBox || type == StageObjectType.Weight
                || type == StageObjectType.JumpPad || type == StageObjectType.Door
                || type == StageObjectType.LockedDoor || type == StageObjectType.HiddenWall;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static int ParseSlot(string name)
        {
            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase)) return 0;
            if (!string.IsNullOrEmpty(name) && name.Length > 1
                && int.TryParse(name.Substring(1), out int number)) return Mathf.Clamp(number - 1, 0, 3);
            return 0;
        }

        private static bool HasFlag(string[] args, string key)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string[] args, string key)
        {
            if (args == null) return null;
            string prefix = key + "=";
            for (int i = 0; i < args.Length; i++)
                if (args[i] != null && args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(prefix.Length).Trim('"');
            return null;
        }
    }
}
