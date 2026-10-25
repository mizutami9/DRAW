using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Owns the two co-op wind puzzles appended to the bottom of stage 3-1.
    /// The host is authoritative online; clients only apply the replicated
    /// selected wind column and door state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage31UpdraftRelayController : MonoBehaviour
    {
        private const string StageId = "3-1";
        private const string StateKind = "stage31_updraft_state";
        private const string ButtonRequestKind = "stage31_updraft_button_request";
        private const float StateInterval = 0.2f;

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public bool ValleyWindOn;
            public bool SectionOneOpen;
            public int ActiveRelayWind;
            public bool HumanDoorOpen;
        }

        [System.Serializable]
        private sealed class ButtonRequest
        {
            public int Button;
            public Vector2 PlayerPosition;
        }

        private sealed class ButtonVisual
        {
            public Transform Cap;
            public Vector3 ReleasedPosition;
            public Vector3 ReleasedScale;
            public SpriteRenderer[] Sprites;
            public Color[] SpriteColors;
            public LineRenderer[] Lines;
            public Color[] LineStartColors;
            public Color[] LineEndColors;
        }

        private readonly Dictionary<Transform, ButtonVisual> buttonVisuals =
            new Dictionary<Transform, ButtonVisual>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Transform valleyButton;
        private Transform blueButton;
        private Transform redButton;
        private Transform greenButton;
        private Transform birdGoalButton;
        private Transform valleyWind;
        private readonly Transform[] relayWinds = new Transform[3];
        private readonly List<Transform> lowerSpikes = new List<Transform>();
        private Transform sectionOneGate;
        private Transform humanDoor;
        private bool valleyButtonWasPressed;
        private bool blueButtonWasPressed;
        private bool redButtonWasPressed;
        private bool greenButtonWasPressed;
        private bool birdGoalButtonWasPressed;
        private bool valleyWindOn;
        private bool sectionOneOpen;
        private int activeRelayWind;
        private bool humanDoorOpen;
        private int stateSequence;
        private int lastReceivedSequence;
        private float nextStateAt;
        private bool ready;
        private readonly Dictionary<string, float> nextLocalButtonRequestAt =
            new Dictionary<string, float>();
        private readonly Dictionary<string, float> lastButtonRequestAt = new Dictionary<string, float>();

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            // ConfigureStageGimmicks adds this component after every stage
            // object exists. Hide the authored wind immediately in Awake so it
            // cannot lift a player for one frame before Start runs.
            CacheCourseObjects();
            ApplyStateVisuals();
            RefreshReadyState();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            CacheCourseObjects();
            ApplyStateVisuals();
            RefreshReadyState();
        }

        private void Update()
        {
            if (!ready || stageManager == null || stageManager.CurrentStageId != StageId
                || !stageManager.IsGameplayActive)
            {
                return;
            }

            bool valleyPressed = IsPressedBySpecies(valleyButton, DrawManager.Species.Bird);
            bool bluePressed = IsPressedBySpecies(blueButton, DrawManager.Species.Human);
            bool redPressed = IsPressedBySpecies(redButton, DrawManager.Species.Human);
            bool greenPressed = IsPressedBySpecies(greenButton, DrawManager.Species.Human);
            bool birdGoalPressed = IsPressedBySpecies(birdGoalButton, DrawManager.Species.Bird);
            SetButtonVisual(valleyButton, valleyPressed || valleyWindOn);
            SetButtonVisual(blueButton, bluePressed || activeRelayWind == 1);
            SetButtonVisual(redButton, redPressed || activeRelayWind == 2);
            SetButtonVisual(greenButton, greenPressed || activeRelayWind == 3);
            SetButtonVisual(birdGoalButton, birdGoalPressed || humanDoorOpen);

            if (!HasAuthority())
            {
                IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerController2D local = localPlayers[i];
                    string gamePlayerId = stageManager.GetOnlinePlayerId(local);
                    float nextRequest = nextLocalButtonRequestAt.TryGetValue(gamePlayerId, out float scheduled)
                        ? scheduled
                        : 0f;
                    if (Time.unscaledTime < nextRequest) continue;
                    int requested = GetPressedButton(local);
                    if (requested < 0) continue;
                    nextLocalButtonRequestAt[gamePlayerId] = Time.unscaledTime + 0.18f;
                    onlineManager?.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = ButtonRequestKind,
                        PlayerId = gamePlayerId,
                        Json = JsonUtility.ToJson(new ButtonRequest
                        {
                            Button = requested,
                            PlayerPosition = local.transform.position
                        })
                    });
                }
            }

            if (HasAuthority())
            {
                bool changed = false;
                if (valleyPressed && !valleyButtonWasPressed && !valleyWindOn)
                {
                    valleyWindOn = true;
                    changed = true;
                    PlaySwitch(valleyButton);
                }

                if (valleyWindOn && !sectionOneOpen && AreHumanAndBirdOnFirstRightBank())
                {
                    sectionOneOpen = true;
                    changed = true;
                    GameSfx.PlayAt(SfxId.DoorOpen, sectionOneGate != null
                        ? sectionOneGate.position : transform.position);
                }

                // Each colour directly selects its matching wind column.  Only
                // one column runs at a time, so the human can react to the
                // bird's position without having to remember a hidden sequence.
                if (bluePressed && !blueButtonWasPressed && activeRelayWind != 1)
                {
                    activeRelayWind = 1;
                    changed = true;
                    PlaySwitch(blueButton);
                }
                if (redPressed && !redButtonWasPressed && activeRelayWind != 2)
                {
                    activeRelayWind = 2;
                    changed = true;
                    PlaySwitch(redButton);
                }
                if (greenPressed && !greenButtonWasPressed && activeRelayWind != 3)
                {
                    activeRelayWind = 3;
                    changed = true;
                    PlaySwitch(greenButton);
                }
                if (birdGoalPressed && !birdGoalButtonWasPressed
                    && activeRelayWind == 3 && !humanDoorOpen)
                {
                    humanDoorOpen = true;
                    changed = true;
                    PlaySwitch(birdGoalButton);
                    GameSfx.PlayAt(SfxId.DoorOpen, humanDoor.position);
                }

                if (changed)
                {
                    ApplyStateVisuals();
                    BroadcastState(true);
                }
                else
                {
                    BroadcastState();
                }
            }

            valleyButtonWasPressed = valleyPressed;
            blueButtonWasPressed = bluePressed;
            redButtonWasPressed = redPressed;
            greenButtonWasPressed = greenPressed;
            birdGoalButtonWasPressed = birdGoalPressed;
        }

        private void CacheCourseObjects()
        {
            lowerSpikes.Clear();
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker == null) continue;
                switch (marker.objectId)
                {
                    case "3-1-coop-bird-button": valleyButton = marker.transform; break;
                    case "3-1-coop-blue-button": blueButton = marker.transform; break;
                    case "3-1-coop-red-button": redButton = marker.transform; break;
                    case "3-1-coop-green-button": greenButton = marker.transform; break;
                    case "3-1-coop-bird-goal-button": birdGoalButton = marker.transform; break;
                    case "3-1-coop-valley-wind": valleyWind = marker.transform; break;
                    case "3-1-coop-relay-wind-1": relayWinds[0] = marker.transform; break;
                    case "3-1-coop-relay-wind-2": relayWinds[1] = marker.transform; break;
                    case "3-1-coop-relay-wind-3": relayWinds[2] = marker.transform; break;
                    case "3-1-coop-section-one-gate": sectionOneGate = marker.transform; break;
                    case "3-1-coop-human-door": humanDoor = marker.transform; break;
                }
                if (marker.type == StageObjectType.Spike
                    && marker.transform.position.x >= 54f
                    && marker.transform.position.y <= -34f)
                {
                    lowerSpikes.Add(marker.transform);
                }
            }
            CacheButtonVisual(valleyButton);
            CacheButtonVisual(blueButton);
            CacheButtonVisual(redButton);
            CacheButtonVisual(greenButton);
            CacheButtonVisual(birdGoalButton);
        }

        private void CacheButtonVisual(Transform button)
        {
            if (button == null || buttonVisuals.ContainsKey(button)) return;
            Transform cap = button.Find("Button Cap");
            SpriteRenderer[] sprites = cap != null
                ? cap.GetComponentsInChildren<SpriteRenderer>(true)
                : System.Array.Empty<SpriteRenderer>();
            LineRenderer[] lines = cap != null
                ? cap.GetComponentsInChildren<LineRenderer>(true)
                : System.Array.Empty<LineRenderer>();
            Color[] spriteColors = new Color[sprites.Length];
            Color[] lineStartColors = new Color[lines.Length];
            Color[] lineEndColors = new Color[lines.Length];
            for (int i = 0; i < sprites.Length; i++)
                spriteColors[i] = sprites[i] != null ? sprites[i].color : Color.white;
            for (int i = 0; i < lines.Length; i++)
            {
                lineStartColors[i] = lines[i] != null ? lines[i].startColor : Color.white;
                lineEndColors[i] = lines[i] != null ? lines[i].endColor : Color.white;
            }
            buttonVisuals[button] = new ButtonVisual
            {
                Cap = cap,
                ReleasedPosition = cap != null ? cap.localPosition : Vector3.zero,
                ReleasedScale = cap != null ? cap.localScale : Vector3.one,
                Sprites = sprites,
                SpriteColors = spriteColors,
                Lines = lines,
                LineStartColors = lineStartColors,
                LineEndColors = lineEndColors
            };
        }

        private void SetButtonVisual(Transform button, bool pressed)
        {
            if (button == null || !buttonVisuals.TryGetValue(button, out ButtonVisual visual)
                || visual.Cap == null)
            {
                return;
            }
            visual.Cap.localPosition = visual.ReleasedPosition
                + (pressed ? Vector3.down * 0.2f : Vector3.zero);
            visual.Cap.localScale = pressed
                ? new Vector3(visual.ReleasedScale.x, visual.ReleasedScale.y * 0.62f,
                    visual.ReleasedScale.z)
                : visual.ReleasedScale;
            Color pressedColor = button == blueButton
                ? new Color(0.08f, 0.42f, 0.98f, 0.96f)
                : button == redButton
                    ? new Color(0.98f, 0.12f, 0.1f, 0.96f)
                    : button == greenButton
                        ? new Color(0.08f, 0.78f, 0.24f, 0.96f)
                        : new Color(0.2f, 0.9f, 0.32f, 0.96f);
            for (int i = 0; i < visual.Sprites.Length; i++)
            {
                SpriteRenderer sprite = visual.Sprites[i];
                if (sprite == null) continue;
                sprite.color = pressed
                    ? Color.Lerp(visual.SpriteColors[i], pressedColor, 0.42f)
                    : visual.SpriteColors[i];
            }
            for (int i = 0; i < visual.Lines.Length; i++)
            {
                LineRenderer line = visual.Lines[i];
                if (line == null) continue;
                line.startColor = pressed ? pressedColor : visual.LineStartColors[i];
                line.endColor = pressed ? pressedColor : visual.LineEndColors[i];
            }
        }

        private void RefreshReadyState()
        {
            ready = valleyButton != null && valleyWind != null
                && blueButton != null && redButton != null
                && greenButton != null
                && birdGoalButton != null && humanDoor != null;
        }

        private bool IsPressedBySpecies(Transform button, DrawManager.Species species)
        {
            if (button == null || !button.gameObject.activeInHierarchy) return false;
            Collider2D buttonCollider = button.GetComponent<Collider2D>();
            if (buttonCollider == null) return false;
            Bounds buttonBounds = buttonCollider.bounds;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                PlayerAbilityController ability = player != null
                    ? player.GetComponent<PlayerAbilityController>() : null;
                if (ability == null || ability.CurrentProfile.Species != species) continue;
                Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
                bool hasBodyBounds = false;
                Bounds bodyBounds = default;
                for (int j = 0; j < colliders.Length; j++)
                {
                    Collider2D collider = colliders[j];
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    if (buttonBounds.Intersects(collider.bounds)) return true;
                    if (!hasBodyBounds)
                    {
                        bodyBounds = collider.bounds;
                        hasBodyBounds = true;
                    }
                    else
                    {
                        bodyBounds.Encapsulate(collider.bounds);
                    }
                }

                // Very small or flat drawings can rest a few pixels above the
                // trigger because the visible cap is higher than its root box.
                // Treat a body whose underside is directly over the plate as a
                // press, independent of the character's total height.
                if (hasBodyBounds
                    && bodyBounds.max.x >= buttonBounds.min.x + 0.06f
                    && bodyBounds.min.x <= buttonBounds.max.x - 0.06f
                    && bodyBounds.min.y >= buttonBounds.min.y - 0.08f
                    && bodyBounds.min.y <= buttonBounds.max.y + 0.42f)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetPressedButton(PlayerController2D player)
        {
            if (IsPlayerPressingButton(player, valleyButton, DrawManager.Species.Bird)) return 0;
            if (IsPlayerPressingButton(player, blueButton, DrawManager.Species.Human)) return 1;
            if (IsPlayerPressingButton(player, redButton, DrawManager.Species.Human)) return 2;
            if (IsPlayerPressingButton(player, greenButton, DrawManager.Species.Human)) return 3;
            if (IsPlayerPressingButton(player, birdGoalButton, DrawManager.Species.Bird)) return 4;
            return -1;
        }

        private static bool IsPlayerPressingButton(
            PlayerController2D player, Transform button, DrawManager.Species species)
        {
            if (player == null || button == null || !button.gameObject.activeInHierarchy) return false;
            PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
            Collider2D buttonCollider = button.GetComponent<Collider2D>();
            if (ability == null || ability.CurrentProfile.Species != species || buttonCollider == null) return false;
            Bounds bounds = buttonCollider.bounds;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                Bounds body = collider.bounds;
                if (bounds.Intersects(body)
                    || body.max.x >= bounds.min.x + 0.06f
                        && body.min.x <= bounds.max.x - 0.06f
                        && body.min.y >= bounds.min.y - 0.18f
                        && body.min.y <= bounds.max.y + 0.65f) return true;
            }
            return false;
        }

        private bool AreHumanAndBirdOnFirstRightBank()
        {
            bool human = false;
            bool bird = false;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null) continue;
                Vector2 position = player.transform.position;
                if (position.x < 38f || position.x > 55.5f || position.y < -43f || position.y > -27f)
                    continue;
                PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
                if (ability == null) continue;
                if (ability.CurrentProfile.Species == DrawManager.Species.Human) human = true;
                if (ability.CurrentProfile.Species == DrawManager.Species.Bird) bird = true;
            }
            return human && bird;
        }

        private void ApplyStateVisuals()
        {
            SetObjectActive(valleyWind, valleyWindOn);
            SetObjectActive(sectionOneGate, !sectionOneOpen);
            for (int i = 0; i < relayWinds.Length; i++)
            {
                bool active = activeRelayWind > 0
                    && i == Mathf.Clamp(activeRelayWind - 1, 0, relayWinds.Length - 1);
                SetObjectActive(relayWinds[i], active);
            }
            SetObjectActive(humanDoor, !humanDoorOpen);
            for (int i = 0; i < lowerSpikes.Count; i++)
                SetObjectActive(lowerSpikes[i], !humanDoorOpen);
        }

        private static void SetObjectActive(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }

        private void PlaySwitch(Transform source)
        {
            GameSfx.PlayAt(SfxId.SwitchPress, source != null ? source.position : transform.position);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == ButtonRequestKind && HasAuthority())
            {
                ButtonRequest request = JsonUtility.FromJson<ButtonRequest>(data.Json);
                PlayerController2D player = stageManager.GetOnlinePlayerController(data.PlayerId);
                if (request == null || player == null
                    || !ValidateButtonRequest(player, request)) return;
                string key = data.PlayerId + ":" + request.Button;
                if (lastButtonRequestAt.TryGetValue(key, out float last)
                    && Time.unscaledTime - last < 0.15f) return;
                lastButtonRequestAt[key] = Time.unscaledTime;
                ApplyRequestedButton(request.Button);
                return;
            }
            if (data.Kind != StateKind || HasAuthority() || !IsHostPlayer(data.PlayerId))
            {
                return;
            }
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastReceivedSequence) return;
            lastReceivedSequence = state.Sequence;
            valleyWindOn = state.ValleyWindOn;
            sectionOneOpen = state.SectionOneOpen;
            activeRelayWind = Mathf.Clamp(state.ActiveRelayWind, 0, 3);
            humanDoorOpen = state.HumanDoorOpen;
            ApplyStateVisuals();
        }

        private bool ValidateButtonRequest(PlayerController2D player, ButtonRequest request)
        {
            Transform button = request.Button == 0 ? valleyButton
                : request.Button == 1 ? blueButton
                : request.Button == 2 ? redButton
                : request.Button == 3 ? greenButton
                : request.Button == 4 ? birdGoalButton
                : null;
            DrawManager.Species expected = request.Button == 0 || request.Button == 4
                ? DrawManager.Species.Bird
                : DrawManager.Species.Human;
            PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
            Collider2D collider = button != null ? button.GetComponent<Collider2D>() : null;
            if (button == null || collider == null || ability == null
                || ability.CurrentProfile.Species != expected
                || Vector2.Distance(player.transform.position, request.PlayerPosition) > 3.5f) return false;
            return Vector2.Distance(collider.ClosestPoint(request.PlayerPosition), request.PlayerPosition) <= 0.85f;
        }

        private void ApplyRequestedButton(int button)
        {
            bool changed = false;
            Transform source = null;
            if (button == 0 && !valleyWindOn)
            {
                valleyWindOn = true;
                source = valleyButton;
                changed = true;
            }
            else if (button >= 1 && button <= 3 && activeRelayWind != button)
            {
                activeRelayWind = button;
                source = button == 1 ? blueButton : button == 2 ? redButton : greenButton;
                changed = true;
            }
            else if (button == 4 && activeRelayWind == 3 && !humanDoorOpen)
            {
                humanDoorOpen = true;
                source = birdGoalButton;
                changed = true;
                GameSfx.PlayAt(SfxId.DoorOpen, humanDoor.position);
            }
            if (!changed) return;
            PlaySwitch(source);
            ApplyStateVisuals();
            BroadcastState(true);
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline() || !HasAuthority() || onlineManager == null
                || !force && Time.unscaledTime < nextStateAt)
            {
                return;
            }
            nextStateAt = Time.unscaledTime + StateInterval;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence,
                ValleyWindOn = valleyWindOn,
                SectionOneOpen = sectionOneOpen,
                ActiveRelayWind = activeRelayWind,
                HumanDoorOpen = humanDoorOpen
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnline() || stageManager.IsOnlineStageHost;

        private bool IsHostPlayer(string playerId)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(playerId)) return true;
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId)
                    return true;
            }
            return false;
        }
    }
}
