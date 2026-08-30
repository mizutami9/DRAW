using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageUmbrellaRainController : StageEliminationChallengeController
    {
        private const string StageId = "14-2";
        private const string StateKind = "umbrella_rain_state";
        private const string EliminateRequestKind = "umbrella_rain_eliminate_request";
        private const string EliminatedKind = "umbrella_rain_eliminated";
        private const float StartDelay = 3f;
        private const float StartX = -34f;
        private const float FriendY = 5.28f;
        private const float FinalX = 80f;
        private const float FinalShelterX = 75f;
        private const float UmbrellaHalfWidth = 5.1f;
        private const float CanopyOffsetY = 2.42f;
        private const float RainExposureSeconds = 0.32f;

        private enum MotionMode { Normal, Stop, Backstep, Slow, Fast }

        [System.Serializable]
        private sealed class RainState
        {
            public int Sequence;
            public float Elapsed;
            public float FriendX;
            public float FriendSpeed;
            public int Motion;
            public bool Failed;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly Dictionary<PlayerController2D, float> exposure = new Dictionary<PlayerController2D, float>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private StageUmbrellaFriend friend;
        private StageDeadlyRainVisual rainVisual;
        private TextMesh instructionTitle;
        private TextMesh instructionHint;
        private PlayerController2D[] players = System.Array.Empty<PlayerController2D>();
        private float elapsed;
        private float friendX = StartX;
        private float friendSpeed;
        private float replicaFriendX = StartX;
        private float nextTrickAt = 7f;
        private float motionRemaining;
        private float nextBroadcastAt;
        private float nextPlayerRefreshAt;
        private int sequence;
        private int receivedSequence;
        private int lastTrick = -1;
        private MotionMode motion = MotionMode.Normal;
        private bool controlsReleased;
        private bool failed;
        private bool retryStarted;
        private bool restoredPlayers;
        private CameraFollow2D stageCamera;
        private float previousCameraMinimum;

        private bool HasAuthority => stageManager == null
            || !stageManager.IsOnlineStageActive
            || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (stageCamera != null)
                stageCamera.SetMinimumOrthographicSize(previousCameraMinimum);
            RestoreHiddenPlayers();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            stageCamera = Object.FindFirstObjectByType<CameraFollow2D>();
            if (stageCamera != null)
            {
                previousCameraMinimum = stageCamera.MinimumOrthographicSize;
                stageCamera.SetMinimumOrthographicSize(10f);
            }
            friend = StageUmbrellaFriend.Create(transform, new Vector2(friendX, FriendY));
            rainVisual = StageDeadlyRainVisual.Create(transform);
            rainVisual.SetShelter(friendX, FriendY + CanopyOffsetY, UmbrellaHalfWidth, FinalShelterX);
            RefreshPlayers();
            CaptureParticipants();
            SetLocalControls(false);
            RefreshPresentation();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId || failed) return;
            elapsed += Time.deltaTime;
            RefreshPresentation();
            if (instructionTitle != null) instructionTitle.text = LocalizationManager.T("umbrella_rain_title");
            if (instructionHint != null) instructionHint.text = LocalizationManager.T("umbrella_rain_hint");
            if (elapsed < StartDelay) return;

            if (HasAuthority)
            {
                UpdateFriendMotion();
                BroadcastState();
            }
            else
            {
                friendX = Mathf.Lerp(friendX, replicaFriendX, 1f - Mathf.Exp(-16f * Time.deltaTime));
            }

            if (friend != null)
            {
                friend.SetPosition(friendX, FriendY);
                friend.SetMotion(friendSpeed, motion);
            }
            rainVisual?.SetShelter(friendX, FriendY + CanopyOffsetY, UmbrellaHalfWidth, FinalShelterX);
            CheckLocalRainExposure();
        }

        private void UpdateFriendMotion()
        {
            if (friendX >= FinalX)
            {
                friendX = FinalX;
                friendSpeed = 0f;
                motion = MotionMode.Stop;
                return;
            }

            if (motion != MotionMode.Normal)
            {
                motionRemaining = Mathf.Max(0f, motionRemaining - Time.deltaTime);
                if (motionRemaining <= 0f)
                {
                    motion = MotionMode.Normal;
                    nextTrickAt = elapsed + Random.Range(4.2f, 7.2f);
                }
            }
            else if (elapsed >= nextTrickAt)
            {
                BeginRandomTrick();
            }

            switch (motion)
            {
                case MotionMode.Stop: friendSpeed = 0f; break;
                case MotionMode.Backstep: friendSpeed = -3f; break;
                case MotionMode.Slow: friendSpeed = 0.9f; break;
                case MotionMode.Fast: friendSpeed = 3.65f; break;
                default: friendSpeed = 2.15f; break;
            }
            friendX = Mathf.Clamp(friendX + friendSpeed * Time.deltaTime, StartX - 1.2f, FinalX);
        }

        private void BeginRandomTrick()
        {
            int choice = Random.Range(0, 4);
            if (choice == lastTrick) choice = (choice + Random.Range(1, 4)) % 4;
            lastTrick = choice;
            motion = choice == 0 ? MotionMode.Stop
                : choice == 1 ? MotionMode.Backstep
                : choice == 2 ? MotionMode.Slow
                : MotionMode.Fast;
            motionRemaining = motion == MotionMode.Backstep
                ? Random.Range(1.15f, 1.65f)
                : motion == MotionMode.Stop ? Random.Range(0.65f, 1.05f)
                : Random.Range(0.9f, 1.35f);
            GameSfx.PlayAt(SfxId.EmotePop, new Vector2(friendX, FriendY), 0.72f);
        }

        private void CheckLocalRainExposure()
        {
            if (Time.unscaledTime >= nextPlayerRefreshAt) RefreshPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy || !IsLocallyOwned(player)) continue;
                string id = ResolvePlayerId(player);
                if (eliminatedIds.Contains(id)) continue;

                bool sheltered = player.transform.position.x >= FinalShelterX && player.transform.position.y <= 0.55f
                    || player.transform.position.y <= FriendY + CanopyOffsetY
                        && Mathf.Abs(player.transform.position.x - friendX) <= UmbrellaHalfWidth - 0.5f;
                if (sheltered)
                {
                    exposure[player] = 0f;
                    continue;
                }

                float value = exposure.TryGetValue(player, out float current) ? current + Time.deltaTime : Time.deltaTime;
                exposure[player] = value;
                if (value >= RainExposureSeconds) RequestElimination(player);
            }
        }

        private bool IsLocallyOwned(PlayerController2D player)
        {
            return !stageManager.IsOnlineStageActive || player.ControlsEnabled;
        }

        public override void RequestElimination(PlayerController2D player)
        {
            if (player == null || failed) return;
            string id = ResolvePlayerId(player);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (!stageManager.IsOnlineStageActive) participantIds.Add(id);
            if (stageManager.IsOnlineStageActive)
            {
                if (id != onlineManager?.LocalPlayerId) return;
                if (!HasAuthority)
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = EliminateRequestKind,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                    });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id, stageManager.IsOnlineStageActive);
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = EliminatedKind,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
            if (AreAllPlayersEliminated()) BeginFailure();
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminatedIds.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void HidePlayer(PlayerController2D player)
        {
            if (player == null || hiddenPlayers.Contains(player)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.ResetMotion();
            player.SetControlsEnabled(false);
            hiddenPlayers.Add(player);
            player.gameObject.SetActive(false);
        }

        private void BeginFailure()
        {
            if (failed) return;
            failed = true;
            BroadcastState(true);
            if (!retryStarted) StartCoroutine(RetryAfterDelay());
        }

        private IEnumerator RetryAfterDelay()
        {
            retryStarted = true;
            uiManager?.SetChallengeCountdown(true, LocalizationManager.T("game_over"));
            yield return new WaitForSeconds(2.5f);
            if (stageManager != null && stageManager.CurrentStageId == StageId && HasAuthority) stageManager.Retry();
        }

        private void RefreshPresentation()
        {
            float remaining = StartDelay - elapsed;
            if (!controlsReleased && remaining <= 0f)
            {
                controlsReleased = true;
                SetLocalControls(true);
            }
            string main = remaining > 0f
                ? Mathf.CeilToInt(remaining).ToString()
                : elapsed < StartDelay + 0.65f ? LocalizationManager.T("survival_start") : string.Empty;
            uiManager?.SetChallengeCountdown(!string.IsNullOrEmpty(main), main);
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            if (!stageManager.IsOnlineStageActive)
                stageManager.RemotePlayerController?.SetControlsEnabled(enabled);
        }

        private void BuildInstructionSign()
        {
            GameObject board = new GameObject("14-2 Rain Rule Sign");
            board.transform.SetParent(transform, false);
            board.transform.position = new Vector3(-25.5f, 6f, 0.22f);
            StageEscortController.AddFilledRect(board.transform, "Board", Vector2.zero,
                new Vector2(13f, 2.05f), new Color(0.94f, 0.96f, 0.88f, 0.94f), 24);
            StageEscortController.AddBoxOutline(board.transform, Vector2.zero,
                new Vector2(13f, 2.05f), new Color(0.06f, 0.3f, 0.58f), 25);
            instructionTitle = StageEscortController.CreateText(board.transform, "Title",
                new Vector3(0f, 0.38f, -0.04f), 46, 0.075f, new Color(0.02f, 0.38f, 0.75f), 27);
            instructionHint = StageEscortController.CreateText(board.transform, "Hint",
                new Vector3(0f, -0.48f, -0.04f), 36, 0.055f, new Color(0.16f, 0.14f, 0.12f), 27);
        }

        private void RefreshPlayers()
        {
            players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            nextPlayerRefreshAt = Time.unscaledTime + 0.4f;
        }

        private void CaptureParticipants()
        {
            if (stageManager.IsOnlineStageActive)
            {
                OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
                if (lobbyPlayers != null)
                    for (int i = 0; i < lobbyPlayers.Length; i++)
                        if (lobbyPlayers[i] != null && !string.IsNullOrEmpty(lobbyPlayers[i].PlayerId))
                            participantIds.Add(lobbyPlayers[i].PlayerId);
                return;
            }
            for (int i = 0; i < players.Length; i++) participantIds.Add(ResolvePlayerId(players[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds) if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private void BroadcastState(bool force = false)
        {
            if (onlineManager == null || stageManager == null || !stageManager.IsOnlineStageActive || !HasAuthority
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new RainState
                {
                    Sequence = ++sequence,
                    Elapsed = elapsed,
                    FriendX = friendX,
                    FriendSpeed = friendSpeed,
                    Motion = (int)motion,
                    Failed = failed,
                    EliminatedIds = new List<string>(eliminatedIds).ToArray()
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId) return;
            if (message.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(message.Json);
                if (request != null && request.PlayerId == message.PlayerId) ConfirmElimination(request.PlayerId, true);
                return;
            }
            if (message.Kind == EliminatedKind && !HasAuthority && IsHost(message.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(message.Json);
                if (state != null) ApplyElimination(state.PlayerId);
                return;
            }
            if (message.Kind != StateKind || HasAuthority || !IsHost(message.PlayerId)) return;
            RainState rain = JsonUtility.FromJson<RainState>(message.Json);
            if (rain == null || rain.Sequence <= receivedSequence) return;
            receivedSequence = rain.Sequence;
            elapsed = Mathf.Lerp(elapsed, rain.Elapsed, 0.42f);
            replicaFriendX = rain.FriendX;
            friendSpeed = rain.FriendSpeed;
            motion = (MotionMode)Mathf.Clamp(rain.Motion, 0, (int)MotionMode.Fast);
            if (rain.EliminatedIds != null)
                for (int i = 0; i < rain.EliminatedIds.Length; i++) ApplyElimination(rain.EliminatedIds[i]);
            failed = rain.Failed;
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return stageManager.IsOnlineStageActive
                ? stageManager.GetOnlinePlayerId(player)
                : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (stageManager.IsOnlineStageActive) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] all = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) if (ResolvePlayerId(all[i]) == id) return all[i];
            return null;
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null) return false;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].IsHost && lobbyPlayers[i].PlayerId == playerId) return true;
            return false;
        }

        private void RestoreHiddenPlayers()
        {
            if (restoredPlayers) return;
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++)
                if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }
    }

    public sealed class StageUmbrellaFriend : MonoBehaviour
    {
        private Transform characterVisual;
        private Transform canopy;
        private TextMesh cue;
        private float walkPhase;

        public static StageUmbrellaFriend Create(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("Umbrella Guide Friend");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            StageUmbrellaFriend result = root.AddComponent<StageUmbrellaFriend>();
            result.Build();
            return result;
        }

        public void SetPosition(float x, float y) => transform.position = new Vector3(x, y, -0.2f);

        public void SetMotion(float speed, System.Enum mode)
        {
            walkPhase += Time.deltaTime * (3.5f + Mathf.Abs(speed) * 1.8f);
            if (characterVisual != null)
                characterVisual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * (Mathf.Abs(speed) > 0.1f ? 2.8f : 0.5f));
            if (canopy != null) canopy.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-speed * 1.2f, -4.5f, 4.5f));
            if (cue != null)
            {
                string name = mode != null ? mode.ToString() : string.Empty;
                cue.text = name == "Stop" ? "…" : name == "Backstep" ? "←" : name == "Fast" ? "»" : string.Empty;
            }
        }

        private void Build()
        {
            characterVisual = new GameObject("Frog Rain Guide").transform;
            characterVisual.SetParent(transform, false);
            Color coat = new Color(1f, 0.72f, 0.08f, 1f);
            Color ink = new Color(0.12f, 0.18f, 0.1f, 1f);
            Color frog = new Color(0.42f, 0.8f, 0.38f, 1f);
            Color frogLight = new Color(0.66f, 0.94f, 0.55f, 1f);

            // A small frog in a raincoat: the oversized eyes and smile keep it
            // readable even when the camera frames the full two-level course.
            AddCircle(characterVisual, "Head Outline", new Vector2(0f, 1.02f), new Vector2(1.12f, 0.92f), ink, 43);
            AddCircle(characterVisual, "Head", new Vector2(0f, 1.02f), new Vector2(1.02f, 0.82f), frog, 44);
            AddCircle(characterVisual, "Left Eye Rim", new Vector2(-0.32f, 1.4f), new Vector2(0.42f, 0.42f), ink, 45);
            AddCircle(characterVisual, "Right Eye Rim", new Vector2(0.32f, 1.4f), new Vector2(0.42f, 0.42f), ink, 45);
            AddCircle(characterVisual, "Left Eye", new Vector2(-0.32f, 1.4f), new Vector2(0.31f, 0.31f), Color.white, 46);
            AddCircle(characterVisual, "Right Eye", new Vector2(0.32f, 1.4f), new Vector2(0.31f, 0.31f), Color.white, 46);
            AddCircle(characterVisual, "Left Pupil", new Vector2(-0.29f, 1.38f), new Vector2(0.13f, 0.16f), ink, 47);
            AddCircle(characterVisual, "Right Pupil", new Vector2(0.35f, 1.38f), new Vector2(0.13f, 0.16f), ink, 47);
            AddCircle(characterVisual, "Muzzle", new Vector2(0f, 0.9f), new Vector2(0.58f, 0.35f), frogLight, 45);
            StageEscortController.AddLine(characterVisual, new Vector2(-0.19f, 0.91f), new Vector2(0f, 0.82f), 0.045f, ink, 47);
            StageEscortController.AddLine(characterVisual, new Vector2(0f, 0.82f), new Vector2(0.19f, 0.91f), 0.045f, ink, 47);

            AddCircle(characterVisual, "Coat Outline", new Vector2(0f, 0.05f), new Vector2(1.02f, 1.25f), ink, 42);
            AddCircle(characterVisual, "Raincoat", new Vector2(0f, 0.08f), new Vector2(0.9f, 1.14f), coat, 43);
            StageEscortController.AddLine(characterVisual, new Vector2(0f, 0.62f), new Vector2(0f, -0.48f), 0.045f, ink, 45);
            AddCircle(characterVisual, "Left Button", new Vector2(-0.12f, 0.3f), new Vector2(0.08f, 0.08f), ink, 46);
            AddCircle(characterVisual, "Right Button", new Vector2(0.12f, 0.02f), new Vector2(0.08f, 0.08f), ink, 46);
            StageEscortController.AddLine(characterVisual, new Vector2(-0.25f, -0.45f), new Vector2(-0.36f, -0.92f), 0.11f, ink, 44);
            StageEscortController.AddLine(characterVisual, new Vector2(0.25f, -0.45f), new Vector2(0.36f, -0.92f), 0.11f, ink, 44);
            StageEscortController.AddLine(characterVisual, new Vector2(-0.5f, 0.38f), new Vector2(-0.82f, 0.67f), 0.1f, frog, 44);
            StageEscortController.AddLine(characterVisual, new Vector2(0.48f, 0.38f), new Vector2(0.72f, 0.74f), 0.1f, frog, 44);
            NicoDrawBossArt.Apply(characterVisual, "ally-umbrella", new Vector2(1.5f, 2.2f), 47);

            canopy = new GameObject("Wide Umbrella").transform;
            canopy.SetParent(characterVisual, false);
            bool hasCanopyArtwork = BuildCanopy(canopy, 3.4f, 0.84f);
            // Widen only the canopy axis. Its height and the handle position stay
            // in the guide character's hand while the shelter span grows by 1.5x.
            canopy.localScale = new Vector3(1.5f, 1f, 1f);
            // Anchor the curved handle in the guide's raised right hand. The
            // PNG includes the full long handle, while the fallback uses the
            // shorter line-art handle, so their canopy offsets differ.
            canopy.localPosition = hasCanopyArtwork
                ? new Vector3(-0.12f, 2.42f, -0.08f)
                : new Vector3(0.42f, 2.42f, -0.08f);
            if (!hasCanopyArtwork)
            {
                StageEscortController.AddLine(canopy, new Vector2(0f, 0f), new Vector2(0f, -1.48f), 0.11f, ink, 48);
                StageEscortController.AddLine(canopy, new Vector2(0f, -1.48f), new Vector2(0.3f, -1.68f), 0.11f, ink, 48);
            }

            cue = StageEscortController.CreateText(transform, "Motion Cue", new Vector3(0f, 3.05f, -0.1f),
                56, 0.1f, new Color(1f, 0.35f, 0.12f), 54);
        }

        private static bool BuildCanopy(Transform parent, float halfWidth, float height)
        {
            Sprite umbrellaSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/umbrella");
            if (umbrellaSprite != null && umbrellaSprite.bounds.size.x > 0f && umbrellaSprite.bounds.size.y > 0f)
            {
                GameObject art = new GameObject("Colored Pencil Umbrella");
                art.transform.SetParent(parent, false);
                art.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                float width = halfWidth * 2f;
                float scale = width / umbrellaSprite.bounds.size.x;
                // The source PNG includes a very long handle. Compress only
                // its vertical axis so the canopy stays wide enough to shelter
                // players while the hook ends naturally in the guide's hand.
                art.transform.localScale = new Vector3(scale, scale * 0.58f, 1f);
                SpriteRenderer artRenderer = art.AddComponent<SpriteRenderer>();
                artRenderer.sprite = umbrellaSprite;
                artRenderer.color = Color.white;
                artRenderer.sortingOrder = 49;
                return true;
            }

            GameObject fill = new GameObject("Umbrella Canopy Fill");
            fill.transform.SetParent(parent, false);
            Mesh mesh = new Mesh();
            const int steps = 18;
            Vector3[] vertices = new Vector3[steps + 2];
            int[] triangles = new int[steps * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(Mathf.PI, 0f, t);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * halfWidth, Mathf.Sin(angle) * height, 0f);
                if (i < steps)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            fill.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial.color = new Color(0.16f, 0.7f, 0.96f, 0.94f);
            renderer.sortingOrder = 47;

            List<Vector2> arc = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI, 0f, i / (float)steps);
                arc.Add(new Vector2(Mathf.Cos(angle) * halfWidth, Mathf.Sin(angle) * height));
            }
            for (int i = 0; i < arc.Count - 1; i++)
                StageEscortController.AddLine(parent, arc[i], arc[i + 1], 0.1f, new Color(0.02f, 0.28f, 0.58f), 49);
            for (int i = -3; i <= 3; i++)
                StageEscortController.AddLine(parent, Vector2.zero, new Vector2(i * halfWidth / 3f, 0f), 0.055f,
                    new Color(0.06f, 0.38f, 0.7f, 0.72f), 48);
            return false;
        }

        private static void AddCircle(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }
    }

    public sealed class StageDeadlyRainVisual : MonoBehaviour
    {
        private sealed class Drop
        {
            public Transform Transform;
            public float Speed;
            public float Drift;
        }

        private readonly List<Drop> drops = new List<Drop>();
        private Camera targetCamera;
        private float umbrellaX;
        private float canopyY;
        private float halfWidth;
        private float finalShelterX;

        public static StageDeadlyRainVisual Create(Transform parent)
        {
            GameObject root = new GameObject("14-2 Deadly Rain");
            root.transform.SetParent(parent, false);
            StageDeadlyRainVisual rain = root.AddComponent<StageDeadlyRainVisual>();
            rain.Build();
            return rain;
        }

        public void SetShelter(float x, float y, float width, float finalX)
        {
            umbrellaX = x;
            canopyY = y;
            halfWidth = width;
            finalShelterX = finalX;
        }

        private void Build()
        {
            targetCamera = Camera.main;
            Material material = new Material(Shader.Find("Sprites/Default"));
            float horizontalExtent = GetHorizontalExtent();
            float verticalExtent = GetVerticalExtent();
            for (int i = 0; i < 104; i++)
            {
                GameObject obj = new GameObject("Rain Streak " + i);
                obj.transform.SetParent(transform, false);
                LineRenderer line = obj.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(-0.15f, 0.42f, 0f));
                line.SetPosition(1, new Vector3(0.15f, -0.42f, 0f));
                line.startWidth = 0.045f;
                line.endWidth = 0.025f;
                line.sharedMaterial = material;
                line.startColor = new Color(0.08f, 0.42f, 0.95f, 0.88f);
                line.endColor = new Color(0.18f, 0.72f, 1f, 0.45f);
                line.sortingOrder = 38;
                obj.transform.localPosition = new Vector3(
                    Random.Range(-horizontalExtent, horizontalExtent),
                    Random.Range(-verticalExtent, verticalExtent),
                    -0.05f);
                drops.Add(new Drop { Transform = obj.transform, Speed = Random.Range(9.5f, 15.5f), Drift = Random.Range(-0.85f, -0.25f) });
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
                transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 0f);
            float horizontalExtent = GetHorizontalExtent();
            float verticalExtent = GetVerticalExtent();
            for (int i = 0; i < drops.Count; i++)
            {
                Drop drop = drops[i];
                if (drop?.Transform == null) continue;
                Vector3 local = drop.Transform.localPosition;
                local.x += drop.Drift * Time.deltaTime;
                local.y -= drop.Speed * Time.deltaTime;
                Vector2 world = (Vector2)transform.position + (Vector2)local;
                bool struckUmbrella = Mathf.Abs(world.x - umbrellaX) <= halfWidth && world.y <= canopyY;
                bool struckFinalRoof = world.x >= finalShelterX && world.y <= 0.5f;
                if (local.y < -verticalExtent - 1f || struckUmbrella || struckFinalRoof)
                {
                    local.y = Random.Range(verticalExtent * 0.72f, verticalExtent + 1.5f);
                    local.x = Random.Range(-horizontalExtent, horizontalExtent);
                }
                drop.Transform.localPosition = local;
            }
        }

        private float GetHorizontalExtent()
        {
            return targetCamera != null && targetCamera.orthographic
                ? targetCamera.orthographicSize * Mathf.Max(0.1f, targetCamera.aspect) + 2f
                : 20f;
        }

        private float GetVerticalExtent()
        {
            return targetCamera != null && targetCamera.orthographic
                ? targetCamera.orthographicSize + 2f
                : 12f;
        }
    }
}
