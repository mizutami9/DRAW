using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [DefaultExecutionOrder(-500)]
    public sealed class TrailerCoopDemoController : MonoBehaviour
    {
        private enum RecorderMode { Idle, Recording, Playback }

        [Serializable]
        private struct InputFrame
        {
            public float horizontal;
            public bool jumpHeld;
            public bool jumpPressed;
            public bool actionHeld;
            public bool actionPressed;
            public Vector2 throwDirection;
        }

        [Serializable]
        private sealed class InputTrack
        {
            public List<InputFrame> frames = new List<InputFrame>();
        }

        [Serializable]
        private sealed class TakeData
        {
            public InputTrack human = new InputTrack();
            public InputTrack bird = new InputTrack();
            public InputTrack cat = new InputTrack();
            public InputTrack slime = new InputTrack();
            public bool hasStartPositions;
            public Vector3 humanStart;
            public Vector3 birdStart;
            public Vector3 catStart;
            public Vector3 slimeStart;
        }

        private sealed class CpuActor
        {
            public GameObject Object;
            public PlayerController2D Controller;
            public PlayerCarryController Carry;
            public Rigidbody2D Body;
        }

        private readonly float[] speeds = { 0.25f, 0.5f, 1f };
        private StageManager stageManager;
        private GameObject sourcePlayer;
        private CameraFollow2D cameraFollow;
        private DrawManager drawManager;
        private Camera captureCamera;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private DrawManager.DrawingState originalDrawingState;
        private Transform stageRoot;
        private Transform actorRoot;
        private CpuActor[] actors;
        private TakeData take = new TakeData();
        private RecorderMode mode;
        private int selectedActor;
        private int frameIndex;
        private int speedIndex;
        private bool paused;
        private bool stepRequested;
        private float liveHorizontal;
        private bool liveJumpHeld;
        private bool liveJumpPressed;
        private bool liveActionHeld;
        private bool liveActionPressed;
        private bool sceneRestored;
        private Text statusLabel;
        private Text[] actorLabels;
        private Text speedLabel;
        private Text presetLabel;
        private DrawManager.DrawingState selectedPresetState;
        private int selectedPresetSlot;
        private int draggedActor = -1;
        private Vector3 dragOffset;
        private float nextUiRefresh;

        public void Configure(StageManager manager, GameObject playerObject, CameraFollow2D follow, DrawManager drawingManager)
        {
            stageManager = manager;
            sourcePlayer = playerObject;
            cameraFollow = follow;
            drawManager = drawingManager;
            captureCamera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
            if (captureCamera != null)
            {
                previousCameraPosition = captureCamera.transform.position;
                previousCameraSize = captureCamera.orthographicSize;
                captureCamera.transform.position = new Vector3(0f, 1f, -10f);
                captureCamera.orthographicSize = 7f;
            }
            if (drawManager != null) originalDrawingState = drawManager.CreateState();
            selectedPresetSlot = Mathf.Clamp(PlayerPrefs.GetInt("trailer_character_preset", 0), 0, CharacterDrawingPresetStore.SlotCount - 1);
            if (!CharacterDrawingPresetStore.Exists(0) && originalDrawingState != null)
            {
                CharacterDrawingPresetStore.Save(0, originalDrawingState);
            }
            selectedPresetState = CharacterDrawingPresetStore.Load(selectedPresetSlot) ?? originalDrawingState;
            LoadTake();
            BuildGameplayStage();
            BuildActualPlayers();
            if (sourcePlayer != null) sourcePlayer.SetActive(false);
            CreateRecorderUi();
            ResetWorld();
            ApplySpeed();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { stageManager?.ExitTrailerCoopDemo(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectActor(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectActor(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectActor(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectActor(3);
            if (Input.GetKeyDown(KeyCode.F9)) ToggleRecording();
            if (Input.GetKeyDown(KeyCode.F10)) PlayAll();
            if (Input.GetKeyDown(KeyCode.F8)) StopAndReset();
            if (Input.GetKeyDown(KeyCode.P)) TogglePause();
            if (Input.GetKeyDown(KeyCode.O)) StepOneFrame();
            if (Input.GetKeyDown(KeyCode.Minus)) ChangeSpeed(-1);
            if (Input.GetKeyDown(KeyCode.Equals)) ChangeSpeed(1);

            if (mode == RecorderMode.Recording)
            {
                liveHorizontal = Input.GetAxisRaw("Horizontal");
                liveJumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                liveJumpPressed |= Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
                liveActionHeld = Input.GetKey(KeyCode.F);
                liveActionPressed |= Input.GetKeyDown(KeyCode.F);
            }
            HandleStartPositionDragging();
            if (Time.unscaledTime >= nextUiRefresh)
            {
                nextUiRefresh = Time.unscaledTime + 0.08f;
                RefreshUi();
            }
        }

        private void FixedUpdate()
        {
            if (mode == RecorderMode.Idle || actors == null) return;

            for (int i = 0; i < actors.Length; i++)
            {
                InputFrame frame;
                bool live = mode == RecorderMode.Recording && i == selectedActor;
                if (live)
                {
                    frame = CaptureLiveFrame(actors[i]);
                    GetTrack(i).frames.Add(frame);
                }
                else
                {
                    InputTrack track = GetTrack(i);
                    frame = frameIndex < track.frames.Count ? track.frames[frameIndex] : default;
                }
                ApplyFrame(actors[i], frame, !live);
            }

            liveJumpPressed = false;
            liveActionPressed = false;
            frameIndex++;

            if (mode == RecorderMode.Playback && frameIndex >= GetLongestTrackLength())
            {
                mode = RecorderMode.Idle;
                NeutralizeActors();
            }
            if (stepRequested)
            {
                stepRequested = false;
                paused = true;
                Time.timeScale = 0f;
            }
        }

        public void RestoreScene()
        {
            if (sceneRestored) return;
            sceneRestored = true;
            SaveTake();
            Time.timeScale = 1f;
            RestoreDrawTarget();
            if (sourcePlayer != null) sourcePlayer.SetActive(true);
            if (captureCamera != null)
            {
                captureCamera.transform.position = previousCameraPosition;
                captureCamera.orthographicSize = previousCameraSize;
            }
            if (cameraFollow != null) cameraFollow.enabled = true;
        }

        private InputFrame CaptureLiveFrame(CpuActor actor)
        {
            return new InputFrame
            {
                horizontal = liveHorizontal,
                jumpHeld = liveJumpHeld,
                jumpPressed = liveJumpPressed,
                actionHeld = liveActionHeld,
                actionPressed = liveActionPressed,
                throwDirection = actor.Carry != null ? actor.Carry.GetThrowDirectionForScript() : Vector2.right
            };
        }

        private static void ApplyFrame(CpuActor actor, InputFrame frame, bool replay)
        {
            if (actor?.Controller == null) return;
            actor.Controller.SetScriptedInput(frame.horizontal, frame.jumpHeld, frame.jumpPressed);
            actor.Carry?.ApplyActionForScript(
                frame.actionHeld,
                frame.actionPressed,
                frame.throwDirection,
                replay);
        }

        private void ToggleRecording()
        {
            if (mode == RecorderMode.Recording)
            {
                mode = RecorderMode.Idle;
                SaveTake();
                NeutralizeActors();
                return;
            }
            RebuildPlayers();
            GetTrack(selectedActor).frames.Clear();
            frameIndex = 0;
            mode = RecorderMode.Recording;
            SetActorsDynamic(true);
            paused = false;
            ClearLiveInput();
            ApplySpeed();
        }

        private void PlayAll()
        {
            if (GetLongestTrackLength() == 0) return;
            RebuildPlayers();
            frameIndex = 0;
            mode = RecorderMode.Playback;
            SetActorsDynamic(true);
            paused = false;
            ApplySpeed();
        }

        private void StopAndReset()
        {
            if (mode == RecorderMode.Recording) SaveTake();
            mode = RecorderMode.Idle;
            paused = false;
            frameIndex = 0;
            RebuildPlayers();
            ApplySpeed();
        }

        private void TogglePause()
        {
            if (mode == RecorderMode.Idle) return;
            paused = !paused;
            ApplySpeed();
        }

        private void StepOneFrame()
        {
            if (mode == RecorderMode.Idle) return;
            stepRequested = true;
            paused = false;
            Time.timeScale = speeds[speedIndex];
        }

        private void ChangeSpeed(int direction)
        {
            speedIndex = Mathf.Clamp(speedIndex + direction, 0, speeds.Length - 1);
            ApplySpeed();
        }

        private void ApplySpeed()
        {
            Time.timeScale = paused ? 0f : speeds[speedIndex];
        }

        private void SelectActor(int index)
        {
            selectedActor = Mathf.Clamp(index, 0, 3);
            RefreshUi();
        }

        private void ClearSelectedTrack()
        {
            GetTrack(selectedActor).frames.Clear();
            SaveTake();
            StopAndReset();
        }

        private void CyclePreset()
        {
            if (mode != RecorderMode.Idle) return;
            SaveTake();
            selectedPresetSlot = (selectedPresetSlot + 1) % CharacterDrawingPresetStore.SlotCount;
            PlayerPrefs.SetInt("trailer_character_preset", selectedPresetSlot);
            selectedPresetState = CharacterDrawingPresetStore.Load(selectedPresetSlot) ?? originalDrawingState;
            take = new TakeData();
            LoadTake();
            RebuildPlayers();
            RefreshUi();
        }

        private void SaveCurrentPreset()
        {
            if (originalDrawingState == null) return;
            CharacterDrawingPresetStore.Save(selectedPresetSlot, originalDrawingState);
            selectedPresetState = CharacterDrawingPresetStore.Load(selectedPresetSlot) ?? originalDrawingState;
            take = new TakeData();
            SaveTake();
            RebuildPlayers();
            RefreshUi();
        }

        private void ClearLiveInput()
        {
            liveHorizontal = 0f;
            liveJumpHeld = false;
            liveJumpPressed = false;
            liveActionHeld = false;
            liveActionPressed = false;
        }

        private void NeutralizeActors()
        {
            if (actors == null) return;
            InputFrame empty = default;
            for (int i = 0; i < actors.Length; i++) ApplyFrame(actors[i], empty, true);
        }

        private int GetLongestTrackLength()
        {
            int result = 0;
            for (int i = 0; i < 4; i++) result = Mathf.Max(result, GetTrack(i).frames.Count);
            return result;
        }

        private InputTrack GetTrack(int index)
        {
            return index switch { 0 => take.human, 1 => take.bird, 2 => take.cat, _ => take.slime };
        }

        private void BuildGameplayStage()
        {
            GameObject root = new GameObject("Trailer Gameplay Stage");
            root.transform.SetParent(transform, false);
            stageRoot = root.transform;
            StageObjectFactory factory = FindFirstObjectByType<StageObjectFactory>();
            if (factory == null) factory = root.AddComponent<StageObjectFactory>();
            CreateStageObject(factory, StageObjectType.Platform, new Vector2(-5.2f, -2.6f), new Vector2(13.2f, 0.55f));
            CreateStageObject(factory, StageObjectType.Wall, new Vector2(11.25f, 1.45f), new Vector2(0.65f, 8.55f));
        }

        private void CreateStageObject(StageObjectFactory factory, StageObjectType type, Vector2 position, Vector2 size)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.size = size;
            factory.Create(data, stageRoot);
        }

        private void BuildActualPlayers()
        {
            if (sourcePlayer == null) return;
            GameObject root = new GameObject("TAS Players");
            root.transform.SetParent(transform, false);
            actorRoot = root.transform;
            actors = new[]
            {
                CreateActualPlayer(DrawManager.Species.Human, 0),
                CreateActualPlayer(DrawManager.Species.Bird, 1),
                CreateActualPlayer(DrawManager.Species.Cat, 2),
                CreateActualPlayer(DrawManager.Species.Slime, 3)
            };
            RestoreDrawTarget();
        }

        private CpuActor CreateActualPlayer(DrawManager.Species species, int colorIndex)
        {
            GameObject instance = Instantiate(sourcePlayer, actorRoot);
            instance.name = "TAS " + species;
            instance.SetActive(true);
            PlayerController2D controller = instance.GetComponent<PlayerController2D>();
            PlayerAbilityController abilities = instance.GetComponent<PlayerAbilityController>();
            BodyBuilder builder = instance.GetComponent<BodyBuilder>();
            DrawManager.DrawingState buildState = selectedPresetState ?? originalDrawingState;
            if (drawManager != null && buildState != null && builder != null && abilities != null)
            {
                drawManager.SetBuildTarget(builder, abilities);
                drawManager.LoadState(CloneStateForSpecies(buildState, species), true);
            }
            builder?.SetPlayerColor(PlayerColorPalette.GetColor(colorIndex));
            Transform marker = instance.transform.Find("Controlled Player Marker");
            if (marker != null) marker.gameObject.SetActive(false);
            Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;
                body.bodyType = RigidbodyType2D.Dynamic;
                body.freezeRotation = true;
            }
            controller?.SetControlsEnabled(true);
            PlayerCarryController carry = instance.GetComponent<PlayerCarryController>();
            carry?.ApplyActionForScript(false, false, Vector2.right, true);
            return new CpuActor { Object = instance, Controller = controller, Carry = carry, Body = body };
        }

        private void ResetWorld()
        {
            if (actors == null || actors.Length != 4) return;
            if (!take.hasStartPositions)
            {
                SetActorOnSurface(actors[0], -8.4f, -2.28f);
                SetActorOnSurface(actors[1], -6.9f, -2.28f);
                Physics2D.SyncTransforms();
                SetActorOnActor(actors[2], actors[1]);
                Physics2D.SyncTransforms();
                SetActorOnActor(actors[3], actors[2]);
                Physics2D.SyncTransforms();
                StoreStartPositions();
            }
            else
            {
                SetActorPosition(actors[0], take.humanStart);
                SetActorPosition(actors[1], take.birdStart);
                SetActorPosition(actors[2], take.catStart);
                SetActorPosition(actors[3], take.slimeStart);
                Physics2D.SyncTransforms();
            }
            NeutralizeActors();
            SetActorsDynamic(mode != RecorderMode.Idle);
        }

        private void RebuildPlayers()
        {
            if (actorRoot != null)
            {
                actorRoot.gameObject.SetActive(false);
                Destroy(actorRoot.gameObject);
            }
            actors = null;
            BuildActualPlayers();
            ResetWorld();
            ClearLiveInput();
        }

        private void HandleStartPositionDragging()
        {
            if (mode != RecorderMode.Idle || actors == null || captureCamera == null) return;
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null
                    && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
                Vector3 world = captureCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector2 point = new Vector2(world.x, world.y);
                Collider2D[] hits = Physics2D.OverlapPointAll(point);
                for (int h = 0; h < hits.Length && draggedActor < 0; h++)
                {
                    for (int i = 0; i < actors.Length; i++)
                    {
                        if (hits[h] != null && hits[h].transform.IsChildOf(actors[i].Object.transform))
                        {
                            draggedActor = i;
                            selectedActor = i;
                            dragOffset = actors[i].Object.transform.position - new Vector3(world.x, world.y, 0f);
                            break;
                        }
                    }
                }
            }
            if (draggedActor >= 0 && Input.GetMouseButton(0))
            {
                Vector3 world = captureCamera.ScreenToWorldPoint(Input.mousePosition);
                SetActorPosition(actors[draggedActor], new Vector3(world.x, world.y, 0f) + dragOffset);
                Physics2D.SyncTransforms();
            }
            if (draggedActor >= 0 && Input.GetMouseButtonUp(0))
            {
                draggedActor = -1;
                StoreStartPositions();
                SaveTake();
                RefreshUi();
            }
        }

        private void StoreStartPositions()
        {
            take.hasStartPositions = true;
            take.humanStart = actors[0].Object.transform.position;
            take.birdStart = actors[1].Object.transform.position;
            take.catStart = actors[2].Object.transform.position;
            take.slimeStart = actors[3].Object.transform.position;
        }

        private void SetActorsDynamic(bool dynamicBodies)
        {
            if (actors == null) return;
            for (int i = 0; i < actors.Length; i++)
            {
                Rigidbody2D body = actors[i].Body;
                if (body == null) continue;
                body.bodyType = dynamicBodies ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
                if (!dynamicBodies)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
        }

        private static void SetActorPosition(CpuActor actor, Vector3 position)
        {
            position.z = 0f;
            actor.Object.transform.position = position;
            actor.Body.position = position;
            actor.Body.linearVelocity = Vector2.zero;
            actor.Body.angularVelocity = 0f;
        }

        private static void SetActorOnSurface(CpuActor actor, float x, float surfaceY)
        {
            Bounds bounds = GetActorBounds(actor.Object);
            Vector3 position = actor.Object.transform.position;
            position.x = x;
            position.y += surfaceY - bounds.min.y + 0.035f;
            position.z = 0f;
            actor.Object.transform.position = position;
            actor.Body.position = position;
            actor.Body.linearVelocity = Vector2.zero;
            actor.Body.angularVelocity = 0f;
        }

        private static void SetActorOnActor(CpuActor upper, CpuActor lower)
        {
            Bounds lowerBounds = GetActorBounds(lower.Object);
            Bounds upperBounds = GetActorBounds(upper.Object);
            Vector3 position = upper.Object.transform.position;
            position.x += lowerBounds.center.x - upperBounds.center.x;
            position.y += lowerBounds.max.y + 0.045f - upperBounds.min.y;
            position.z = 0f;
            upper.Object.transform.position = position;
            upper.Body.position = position;
            upper.Body.linearVelocity = Vector2.zero;
            upper.Body.angularVelocity = 0f;
        }

        private static Bounds GetActorBounds(GameObject actor)
        {
            Collider2D[] colliders = actor.GetComponentsInChildren<Collider2D>(false);
            Bounds bounds = new Bounds(actor.transform.position, Vector3.one);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return bounds;
        }

        private DrawManager.DrawingState CloneStateForSpecies(DrawManager.DrawingState source, DrawManager.Species species)
        {
            DrawManager.DrawingState clone = new DrawManager.DrawingState { Species = species, Part = source.Part };
            foreach (KeyValuePair<DrawManager.Species, Dictionary<DrawManager.BodyPart, List<Vector2>>> speciesPair in source.Points)
            {
                Dictionary<DrawManager.BodyPart, List<Vector2>> parts = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
                foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> partPair in speciesPair.Value)
                    parts[partPair.Key] = new List<Vector2>(partPair.Value);
                clone.Points[speciesPair.Key] = parts;
            }
            return clone;
        }

        private void RestoreDrawTarget()
        {
            if (drawManager == null || sourcePlayer == null) return;
            drawManager.SetBuildTarget(sourcePlayer.GetComponent<BodyBuilder>(), sourcePlayer.GetComponent<PlayerAbilityController>());
            if (originalDrawingState != null) drawManager.LoadState(originalDrawingState, true);
        }

        private string TakePath => Path.Combine(
            Application.persistentDataPath,
            "trailer_take_01_preset_" + (selectedPresetSlot + 1) + ".json");

        private void SaveTake()
        {
            try { File.WriteAllText(TakePath, JsonUtility.ToJson(take)); }
            catch (Exception exception) { Debug.LogWarning("Could not save trailer TAS: " + exception.Message); }
        }

        private void LoadTake()
        {
            try
            {
                if (File.Exists(TakePath)) take = JsonUtility.FromJson<TakeData>(File.ReadAllText(TakePath)) ?? new TakeData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not load trailer TAS: " + exception.Message);
                take = new TakeData();
            }
            take.human ??= new InputTrack();
            take.bird ??= new InputTrack();
            take.cat ??= new InputTrack();
            take.slime ??= new InputTrack();
        }

        private void CreateRecorderUi()
        {
            GameObject canvasObject = new GameObject("Trailer TAS UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Font font = FindFirstObjectByType<Text>()?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform panel = CreateRect("Panel", canvasObject.transform as RectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1500f, 190f));
            panel.pivot = new Vector2(0.5f, 1f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.97f, 0.94f, 0.82f, 0.94f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.07f, 0.05f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            Text title = CreateText("Title", panel, LocalizationManager.T("trailer_tas_title"), font, 30, new Vector2(-530f, 66f), new Vector2(390f, 44f));
            title.fontStyle = FontStyle.Bold;
            Text help = CreateText("Help", panel, LocalizationManager.T("trailer_tas_help"), font, 19, new Vector2(220f, 67f), new Vector2(980f, 40f));
            help.alignment = TextAnchor.MiddleLeft;

            actorLabels = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                int captured = i;
                Button button = CreateButton("Actor" + i, panel, font, new Vector2(-595f + i * 150f, 10f), new Vector2(138f, 52f), () => SelectActor(captured));
                actorLabels[i] = button.GetComponentInChildren<Text>();
            }
            CreateButtonWithText("Record", panel, font, LocalizationManager.T("trailer_tas_record"), new Vector2(55f, 10f), new Vector2(175f, 52f), ToggleRecording);
            CreateButtonWithText("Play", panel, font, LocalizationManager.T("trailer_tas_play"), new Vector2(245f, 10f), new Vector2(190f, 52f), PlayAll);
            CreateButtonWithText("Reset", panel, font, LocalizationManager.T("trailer_tas_reset"), new Vector2(435f, 10f), new Vector2(170f, 52f), StopAndReset);
            CreateButtonWithText("Clear", panel, font, LocalizationManager.T("trailer_tas_clear"), new Vector2(615f, 10f), new Vector2(180f, 52f), ClearSelectedTrack);
            CreateButtonWithText("Slow", panel, font, "-", new Vector2(-65f, -53f), new Vector2(54f, 42f), () => ChangeSpeed(-1));
            speedLabel = CreateText("Speed", panel, string.Empty, font, 21, new Vector2(0f, -53f), new Vector2(80f, 42f));
            CreateButtonWithText("Fast", panel, font, "+", new Vector2(65f, -53f), new Vector2(54f, 42f), () => ChangeSpeed(1));
            CreateButtonWithText("Pause", panel, font, LocalizationManager.T("trailer_tas_pause"), new Vector2(230f, -53f), new Vector2(180f, 42f), TogglePause);
            CreateButtonWithText("Step", panel, font, LocalizationManager.T("trailer_tas_step"), new Vector2(420f, -53f), new Vector2(170f, 42f), StepOneFrame);
            Button presetButton = CreateButton("Preset", panel, font, new Vector2(555f, -53f), new Vector2(130f, 42f), CyclePreset);
            presetLabel = presetButton.GetComponentInChildren<Text>();
            CreateButtonWithText("SavePreset", panel, font, LocalizationManager.T("trailer_tas_save_preset"), new Vector2(680f, -53f), new Vector2(110f, 42f), SaveCurrentPreset);
            statusLabel = CreateText("Status", panel, string.Empty, font, 21, new Vector2(-430f, -53f), new Vector2(600f, 42f));
            statusLabel.alignment = TextAnchor.MiddleLeft;
            RefreshUi();
        }

        private void RefreshUi()
        {
            if (actorLabels == null) return;
            string[] keys = { "species_human", "species_bird", "species_cat", "species_slime" };
            for (int i = 0; i < 4; i++)
            {
                actorLabels[i].text = (i + 1) + " " + LocalizationManager.T(keys[i]) + "\n" + GetTrack(i).frames.Count + "F";
                actorLabels[i].color = i == selectedActor ? new Color(0.02f, 0.42f, 0.82f, 1f) : new Color(0.08f, 0.07f, 0.05f, 1f);
            }
            string state = LocalizationManager.T(mode == RecorderMode.Recording
                ? "trailer_tas_status_recording"
                : mode == RecorderMode.Playback ? "trailer_tas_status_playback" : "trailer_tas_status_idle");
            if (paused) state += " / " + LocalizationManager.T("trailer_tas_paused");
            statusLabel.text = LocalizationManager.Format(
                "trailer_tas_status_format",
                state,
                LocalizationManager.T(keys[selectedActor]),
                frameIndex,
                speeds[speedIndex]);
            speedLabel.text = speeds[speedIndex].ToString("0.##") + "x";
            presetLabel.text = LocalizationManager.Format("trailer_tas_preset_format", selectedPresetSlot + 1);
        }

        private static Button CreateButtonWithText(string name, RectTransform parent, Font font, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, font, position, size, action);
            button.GetComponentInChildren<Text>().text = label;
            return button;
        }

        private static Button CreateButton(string name, RectTransform parent, Font font, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 0.82f, 0.25f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
                action?.Invoke();
            });
            CreateText("Label", rect, string.Empty, font, 19, Vector2.zero, size);
            return button;
        }

        private static Text CreateText(string name, RectTransform parent, string value, Font font, int size, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.07f, 0.05f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = target.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
