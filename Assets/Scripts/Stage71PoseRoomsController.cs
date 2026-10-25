using System.Collections;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class Stage71PoseRoomsController : MonoBehaviour
    {
        private const string StageId = "7-1";
        private const string StateKind = "pose_rooms_state";
        private const string UnlockRequestKind = "pose_rooms_unlock_request";
        private const float FloorY = -4.4f;
        private const float CeilingY = 5.8f;
        private const float RoomHalfWidth = 14.25f;
        private const float TurnSeconds = 1.45f;
        private const float RetrySeconds = 2.2f;

        private enum RoomPhase
        {
            Active,
            KeyTurning,
            Failed,
            Complete
        }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Round;
            public int Attempt;
            public int Phase;
            public int AnswerPose;
            public int LayoutSeed;
            public float TransitionRemaining;
            public float Remaining;
            public float ReadyRemaining;
        }

        [System.Serializable]
        private sealed class UnlockRequest
        {
            public int Round;
            public int Attempt;
            public float X;
            public float Y;
        }

        private StageManager stageManager;
        private StageLoader stageLoader;
        private StageObjectFactory factory;
        private StageGimmickSyncManager syncManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private StageCountdownPresenter countdownPresenter;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Transform arenaRoot;
        private KeyLockReceiver keyReceiver;
        private Transform correctKey;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousCameraFollowEnabled;
        private bool cameraCaptured;
        private bool initialized;
        private int round = 1;
        private int attempt;
        private int answerPose;
        private int layoutSeed;
        private int sequence;
        private int receivedSequence;
        private float transitionRemaining;
        private float remaining;
        private float roundReadyAt;
        private float nextBroadcastAt;
        private RoomPhase phase;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;
        internal bool KeysMayRoam => initialized
            && phase == RoomPhase.Active
            && Time.unscaledTime >= roundReadyAt;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
            countdownPresenter = new StageCountdownPresenter(uiManager);
            gameCamera = Camera.main;
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            EnsureInitializedForPlay();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            countdownPresenter?.Hide();
            RestoreCamera();
        }

        public void EnsureInitializedForPlay()
        {
            if (initialized) return;
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject previewObject = transform.GetChild(i).gameObject;
                previewObject.SetActive(false);
                Destroy(previewObject);
            }

            initialized = true;
            CaptureCamera();
            BeginRound(1);
        }

        private void Update()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;

            ConfigureCamera();
            float readyRemaining = Mathf.Max(0f, roundReadyAt - Time.unscaledTime);
            bool controlsEnabled = phase == RoomPhase.Active
                && readyRemaining <= 0f
                && !stageManager.IsDrawingMode;
            SetPlayerControls(controlsEnabled);

            if (phase == RoomPhase.Failed) countdownPresenter?.ShowGameOver();
            else if (phase == RoomPhase.Active && readyRemaining > 0f)
                countdownPresenter?.Show(readyRemaining);
            else countdownPresenter?.Hide();

            if (phase == RoomPhase.Active && readyRemaining <= 0f)
            {
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                if (HasAuthority && remaining <= 0f) FailRound();
            }

            if (HasAuthority && phase == RoomPhase.KeyTurning)
            {
                transitionRemaining = Mathf.Max(0f, transitionRemaining - Time.unscaledDeltaTime);
                if (transitionRemaining <= 0f)
                {
                    if (round >= 3)
                    {
                        phase = RoomPhase.Complete;
                        BroadcastState(true);
                        stageManager.ClearStage();
                    }
                    else
                    {
                        BeginRound(round + 1);
                    }
                }
            }
            else if (HasAuthority && phase == RoomPhase.Failed)
            {
                transitionRemaining = Mathf.Max(0f, transitionRemaining - Time.unscaledDeltaTime);
                if (transitionRemaining <= 0f) BeginRound(round);
            }

            BroadcastState();
            RefreshMonitor();
        }

        private void BeginRound(
            int nextRound,
            int synchronizedAttempt = -1,
            int synchronizedAnswer = -1,
            int synchronizedLayoutSeed = int.MinValue)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            attempt = synchronizedAttempt >= 0 ? synchronizedAttempt : attempt + 1;
            answerPose = synchronizedAnswer >= 0 ? Mathf.Clamp(synchronizedAnswer, 0, 29) : ChooseAnswerPose();
            layoutSeed = synchronizedLayoutSeed != int.MinValue
                ? synchronizedLayoutSeed
                : ChooseLayoutSeed();
            phase = RoomPhase.Active;
            transitionRemaining = 0f;
            remaining = GetRoundSeconds();
            roundReadyAt = Time.unscaledTime + 4f;
            BuildRoom();
            StartCoroutine(PositionPlayersAfterSetup());
            BroadcastState(true);
            RefreshMonitor();
        }

        private float GetRoundSeconds()
        {
            return 60f;
        }

        private int ChooseAnswerPose()
        {
            if (!IsOnline) return new System.Random(System.Guid.NewGuid().GetHashCode()).Next(30);

            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            string context = lobby != null
                ? (lobby.LobbyId ?? string.Empty) + "|" + (lobby.RoomCode ?? string.Empty)
                    + "|" + lobby.StageRevision + "|" + lobby.RetryRevision
                : "online";
            return new System.Random(StableHash(context + "|7-1|" + round + "|" + attempt)).Next(30);
        }

        private int ChooseLayoutSeed()
        {
            if (!IsOnline) return System.Guid.NewGuid().GetHashCode();

            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            string context = lobby != null
                ? (lobby.LobbyId ?? string.Empty) + "|" + (lobby.RoomCode ?? string.Empty)
                    + "|" + lobby.StageRevision + "|" + lobby.RetryRevision
                : "online";
            int seed = StableHash(context + "|7-1-layout|" + round + "|" + attempt);
            return seed == int.MinValue ? 1 : seed;
        }

        private void BuildRoom()
        {
            if (arenaRoot != null)
            {
                if (syncManager != null)
                {
                    StageEditorObject[] oldObjects = arenaRoot.GetComponentsInChildren<StageEditorObject>(true);
                    for (int i = 0; i < oldObjects.Length; i++)
                        if (oldObjects[i] != null) syncManager.UnregisterRuntimeObject(oldObjects[i].transform);
                }
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
            }

            keyReceiver = null;
            correctKey = null;
            monitorMain = null;
            monitorSub = null;

            GameObject arena = new GameObject("7-1 Pose Room " + round);
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;

            CreateBackground();
            // The controller removes the editable preview objects when play
            // begins, which also removes StageLoader's shared backdrop. Rebuild
            // it inside every randomized room so the carnival scenery survives
            // round changes as well as retries.
            factory?.CreateAmusementParkStageBackdrop(StageId, null, arenaRoot);
            CreateRoomBoundary();
            CreateMonitor();
            CreatePoseKeys();
            if (round == 3) CreateSpringFloor();
            CreateKeyhole();

            Vector2 spawn = new Vector2(-12.35f, FloorY + 1.3f);
            stageLoader?.SetRuntimeSpawnPosition(spawn);
            ConfigureCamera();
        }

        private void CreateBackground()
        {
            Color paper = new Color(0.8f, 0.94f, 0.98f, 0.24f);
            StageEscortController.AddFilledRect(arenaRoot, "Room Paper",
                new Vector2(0f, (FloorY + CeilingY) * 0.5f),
                // Keep the room tint behind the shared amusement-park scenery.
                // At -55 it covered the Ferris wheel, carousel and coaster, so
                // only the themed frame remained visible in 7-1.
                new Vector2(RoomHalfWidth * 2f, CeilingY - FloorY), paper, -94);

            Color pencil = new Color(0.24f, 0.66f, 0.84f, 0.15f);
            for (int i = 0; i < 9; i++)
            {
                float y = FloorY + 1.1f + i * 1.05f;
                StageGun.AddLine(arenaRoot, "Notebook Line " + i,
                    new[] { new Vector2(-13.8f, y), new Vector2(13.8f, y) }, 0.025f, pencil, -50);
            }
        }

        private void CreateRoomBoundary()
        {
            const float wallThickness = 0.62f;
            CreateSolid("Pose Room Floor", new Vector2(0f, FloorY),
                new Vector2(RoomHalfWidth * 2f - wallThickness, 0.72f));
            CreateSolid("Pose Room Ceiling", new Vector2(0f, CeilingY),
                new Vector2(RoomHalfWidth * 2f + wallThickness, wallThickness));
            CreateSolid("Pose Room Left Wall", new Vector2(-RoomHalfWidth, 0.7f), new Vector2(0.62f, 10.8f));
            CreateSolid("Pose Room Right Wall", new Vector2(RoomHalfWidth, 0.7f), new Vector2(0.62f, 10.8f));
        }

        private void CreateSolid(string name, Vector2 position, Vector2 size)
        {
            GameObject solid = new GameObject(name);
            solid.transform.SetParent(arenaRoot, false);
            solid.transform.position = position;
            solid.layer = 6;
            solid.tag = "Ground";
            BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
            collider.size = size;

            bool floor = name == "Pose Room Floor";
            bool ceiling = name == "Pose Room Ceiling";
            bool wall = name == "Pose Room Left Wall" || name == "Pose Room Right Wall";
            if (floor)
            {
                AddAmusementFloorVisual(solid.transform, size);
            }
            else if (ceiling)
            {
                AddAmusementCeilingVisual(solid.transform, size);
            }
            else if (wall)
            {
                AddAmusementWallVisual(solid.transform, size);
            }
            else
            {
                StageEscortController.AddFilledRect(solid.transform, "Crayon Fill", Vector2.zero, size,
                    new Color(0.74f, 0.65f, 0.52f, 0.94f), 2);
            }
            Vector2 half = size * 0.5f;
            StageGun.AddLine(solid.transform, "Pencil Outline", new[]
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y),
                new Vector2(-half.x, -half.y)
            }, 0.06f, new Color(0.2f, 0.15f, 0.1f, 0.9f), 4);
        }

        internal static void AddAmusementFloorVisual(Transform parent, Vector2 size)
        {
            StageEscortController.AddFilledRect(
                parent, "Pastel Blue Floor Side", Vector2.zero, size,
                new Color(0.48f, 0.79f, 0.91f, 0.96f), 2);
            const float tileHeight = 0.24f;
            StageEscortController.AddFilledRect(
                parent, "Cream Tile Surface",
                new Vector2(0f, size.y * 0.5f - tileHeight * 0.5f),
                new Vector2(size.x, tileHeight),
                new Color(1f, 0.91f, 0.68f, 0.98f), 5);

            int tileCount = Mathf.Max(1, Mathf.RoundToInt(size.x / 1.8f));
            for (int tile = 1; tile < tileCount; tile++)
            {
                float x = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, tile / (float)tileCount);
                StageGun.AddLine(parent, "Tile Joint", new[]
                {
                    new Vector2(x, size.y * 0.5f - tileHeight),
                    new Vector2(x, size.y * 0.5f)
                }, 0.022f, new Color(0.6f, 0.46f, 0.3f, 0.42f), 6);
            }

            for (int star = 0; star < 7; star++)
            {
                float x = Mathf.Lerp(-size.x * 0.42f, size.x * 0.42f, star / 6f);
                AddAmusementStar(parent, new Vector2(x, -0.08f), 0.16f, star);
            }
        }

        internal static void AddAmusementCeilingVisual(Transform parent, Vector2 size)
        {
            StageEscortController.AddFilledRect(
                parent, "Cream Wooden Ceiling", Vector2.zero, size,
                new Color(1f, 0.9f, 0.66f, 0.97f), 2);
            for (int grain = 0; grain < 12; grain++)
            {
                float x = Mathf.Lerp(-size.x * 0.45f, size.x * 0.45f, (grain + 0.5f) / 12f);
                StageGun.AddLine(parent, "Wood Grain", new[]
                {
                    new Vector2(x - 0.5f, Mathf.Sin(grain * 1.7f) * 0.08f),
                    new Vector2(x + 0.5f, Mathf.Sin(grain * 1.7f) * 0.08f + 0.02f)
                }, 0.022f, new Color(0.58f, 0.4f, 0.24f, 0.3f), 5);
            }

            for (int bulb = 0; bulb < 18; bulb++)
            {
                float x = Mathf.Lerp(-size.x * 0.46f, size.x * 0.46f, bulb / 17f);
                GameObject light = new GameObject("Small Carnival Bulb");
                light.transform.SetParent(parent, false);
                light.transform.localPosition = new Vector3(x, -size.y * 0.36f, 0f);
                light.transform.localScale = Vector3.one * 0.16f;
                SpriteRenderer renderer = light.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = bulb % 3 == 0
                    ? new Color(1f, 0.5f, 0.42f, 1f)
                    : bulb % 3 == 1
                        ? new Color(1f, 0.82f, 0.22f, 1f)
                        : new Color(0.4f, 0.81f, 0.96f, 1f);
                renderer.sortingOrder = 7;
            }
        }

        internal static void AddAmusementWallVisual(Transform parent, Vector2 size)
        {
            int stripeCount = 3;
            float stripeWidth = size.x / stripeCount;
            for (int stripe = 0; stripe < stripeCount; stripe++)
            {
                StageEscortController.AddFilledRect(
                    parent,
                    "Pastel Vertical Stripe",
                    new Vector2(-size.x * 0.5f + stripeWidth * (stripe + 0.5f), 0f),
                    new Vector2(stripeWidth + 0.01f, size.y),
                    stripe % 2 == 0
                        ? new Color(0.61f, 0.87f, 0.96f, 0.96f)
                        : new Color(1f, 0.86f, 0.45f, 0.96f),
                    3);
            }
        }

        private static void AddAmusementStar(
            Transform parent,
            Vector2 center,
            float radius,
            int seed)
        {
            Vector2[] points = new Vector2[11];
            float rotation = -90f + Mathf.Sin(seed * 1.7f) * 4f;
            for (int point = 0; point < 10; point++)
            {
                float r = (point & 1) == 0 ? radius : radius * 0.43f;
                float angle = (rotation + point * 36f) * Mathf.Deg2Rad;
                points[point] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            points[10] = points[0];
            StageGun.AddLine(
                parent,
                "Yellow Carnival Star",
                points,
                0.04f,
                new Color(1f, 0.74f, 0.18f, 0.94f),
                6);
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("Pose Room Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.position = new Vector3(0f, 4.12f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(13.8f, 2.65f), 23);
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main",
                new Vector3(0f, 0.35f, -0.04f), 70, 0.17f, new Color(0.05f, 0.12f, 0.18f, 1f), 26);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub",
                new Vector3(0f, -0.48f, -0.05f), 46, 0.11f, new Color(0.04f, 0.28f, 0.42f, 1f), 27);
        }

        private void CreatePoseKeys()
        {
            int species = round - 1;
            int[] shuffledSlots = new int[30];
            for (int i = 0; i < shuffledSlots.Length; i++) shuffledSlots[i] = i;
            System.Random layoutRandom = new System.Random(layoutSeed);
            for (int i = shuffledSlots.Length - 1; i > 0; i--)
            {
                int swapIndex = layoutRandom.Next(i + 1);
                int swap = shuffledSlots[i];
                shuffledSlots[i] = shuffledSlots[swapIndex];
                shuffledSlots[swapIndex] = swap;
            }

            for (int pose = 0; pose < 30; pose++)
            {
                int slot = shuffledSlots[pose];
                int column = slot % 10;
                int row = slot / 10;
                float x = -10.8f + column * 1.8f;
                float y;
                if (round == 1) y = -3.22f + row * 1.68f;
                else if (round == 2) y = -2.5f + row * 2.25f + (column % 2 == 0 ? 0.18f : -0.18f);
                else y = -1.65f + row * 1.65f;

                StageObjectData data = StageObjectFactory.CreateDefaultData(
                    StageObjectType.PoseCharacterKey, new Vector2(x, y));
                data.objectId = "7-1-r" + round + "-pose-" + pose.ToString("00");
                data.size = new Vector2(0.9f, 1.1f);
                data.spawnPattern = species * 100 + pose;
                data.rotation = round == 1 ? layoutRandom.Next(-3, 4) * 8f : 0f;
                GameObject key = factory.Create(data, arenaRoot);
                if (key == null) continue;
                syncManager?.RegisterRuntimeObject(key.transform);
                if (pose == answerPose) correctKey = key.transform;

                Rigidbody2D body = key.GetComponent<Rigidbody2D>();
                if (round == 1 && body != null)
                {
                    body.freezeRotation = true;
                    Stage71RoamingPoseKey roaming = key.AddComponent<Stage71RoamingPoseKey>();
                    roaming.Configure(pose * 0.91f);
                }
                else if (round == 2 && body != null)
                {
                    body.gravityScale = 0f;
                    body.freezeRotation = true;
                    Stage71FloatingPoseKey floating = key.AddComponent<Stage71FloatingPoseKey>();
                    floating.Configure(pose * 0.73f);
                }
            }
        }

        private void CreateSpringFloor()
        {
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Lerp(-12.3f, 8.5f, i / 11f);
                StageObjectData spring = StageObjectFactory.CreateDefaultData(
                    StageObjectType.Spring, new Vector2(x, FloorY + 0.72f));
                spring.objectId = "7-1-r3-spring-" + i.ToString("00");
                spring.size = new Vector2(1.55f, 0.72f);
                spring.actionStrength = 23f;
                factory.Create(spring, arenaRoot);
            }
        }

        private void CreateKeyhole()
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(
                StageObjectType.PoseCharacterKeyhole, new Vector2(11.15f, FloorY + 1.22f));
            data.objectId = "7-1-r" + round + "-keyhole";
            data.size = new Vector2(1.25f, 1.35f);
            data.spawnPattern = (round - 1) * 100 + answerPose;
            GameObject keyhole = factory.Create(data, arenaRoot);
            if (keyhole == null || correctKey == null) return;
            keyReceiver = keyhole.GetComponent<KeyLockReceiver>();
            if (keyReceiver == null) keyReceiver = keyhole.AddComponent<KeyLockReceiver>();
            keyReceiver.Configure(correctKey, HandleLocalUnlock,
                () => IsOnline && !HasAuthority);
        }

        private void HandleLocalUnlock()
        {
            if (!HasAuthority)
            {
                if (onlineManager == null || correctKey == null) return;
                UnlockRequest request = new UnlockRequest
                {
                    Round = round,
                    Attempt = attempt,
                    X = correctKey.position.x,
                    Y = correctKey.position.y
                };
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = UnlockRequestKind,
                    Json = JsonUtility.ToJson(request)
                });
                return;
            }

            if (phase != RoomPhase.Active) return;
            phase = RoomPhase.KeyTurning;
            transitionRemaining = TurnSeconds;
            GameSfx.Play(SfxId.GoalReached);
            BroadcastState(true);
            RefreshMonitor();
        }

        private void FailRound()
        {
            if (phase != RoomPhase.Active) return;
            phase = RoomPhase.Failed;
            transitionRemaining = RetrySeconds;
            BroadcastState(true);
            RefreshMonitor();
        }

        private IEnumerator PositionPlayersAfterSetup()
        {
            yield return null;
            if (arenaRoot == null || stageManager == null || stageManager.CurrentStageId != StageId) yield break;

            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            int placed = 0;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (IsOnline && !stageManager.IsLocallyControlledPlayer(player)) continue;
                Vector2 position = new Vector2(-12.35f + placed * 1.15f, FloorY + 1.35f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.simulated = true;
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                else player.transform.position = position;
                player.ResetMotion();
                stageManager.RecordAssignedPlayerStart(player, position);
                placed++;
            }
            Physics2D.SyncTransforms();
        }

        private void SetPlayerControls(bool controlsEnabled)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (!IsOnline || stageManager.IsLocallyControlledPlayer(players[i]))
                    players[i].SetControlsEnabled(controlsEnabled);
            }
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null || monitorSub == null) return;
            monitorMain.text = phase == RoomPhase.KeyTurning
                ? LocalizationManager.Format("pose_rooms_round_clear", round)
                : phase == RoomPhase.Failed
                    ? LocalizationManager.T("pose_rooms_timeout")
                    : LocalizationManager.Format("pose_rooms_monitor", round, remaining);
            string hintKey = round == 1
                ? "pose_rooms_human_hint"
                : round == 2 ? "pose_rooms_bird_hint" : "pose_rooms_turtle_hint";
            monitorSub.text = phase == RoomPhase.Failed
                ? LocalizationManager.T("pose_rooms_retry")
                : LocalizationManager.T(hintKey);
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.15f;
            NetworkState state = new NetworkState
            {
                Sequence = ++sequence,
                Round = round,
                Attempt = attempt,
                Phase = (int)phase,
                AnswerPose = answerPose,
                LayoutSeed = layoutSeed,
                TransitionRemaining = transitionRemaining,
                Remaining = remaining,
                ReadyRemaining = Mathf.Max(0f, roundReadyAt - Time.unscaledTime)
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;

            if (data.Kind == UnlockRequestKind && HasAuthority)
            {
                UnlockRequest request = JsonUtility.FromJson<UnlockRequest>(data.Json);
                if (request == null || request.Round != round || request.Attempt != attempt
                    || phase != RoomPhase.Active || keyReceiver == null) return;
                keyReceiver.TryUnlockAuthoritatively(new Vector2(request.X, request.Y));
                return;
            }

            if (data.Kind != StateKind || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;

            bool sameRoom = state.Round == round
                && state.Attempt == attempt
                && state.AnswerPose == answerPose
                && state.LayoutSeed == layoutSeed;
            if (!sameRoom)
            {
                BeginRound(state.Round, state.Attempt, state.AnswerPose, state.LayoutSeed);
            }

            RoomPhase incoming = (RoomPhase)state.Phase;
            if (phase == RoomPhase.Active && incoming == RoomPhase.KeyTurning)
            {
                keyReceiver?.ApplyUnlockedState();
                GameSfx.Play(SfxId.GoalReached);
            }
            phase = incoming;
            transitionRemaining = state.TransitionRemaining;
            remaining = state.Remaining;
            roundReadyAt = Time.unscaledTime + Mathf.Max(0f, state.ReadyRemaining);
            RefreshMonitor();
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            return false;
        }

        private void CaptureCamera()
        {
            if (cameraCaptured) return;
            if (gameCamera == null) gameCamera = Camera.main;
            if (cameraFollow == null && gameCamera != null) cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            if (gameCamera == null) return;
            cameraCaptured = true;
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            previousCameraFollowEnabled = cameraFollow != null && cameraFollow.enabled;
        }

        private void ConfigureCamera()
        {
            if (!cameraCaptured) CaptureCamera();
            if (gameCamera == null) return;
            if (cameraFollow != null) cameraFollow.enabled = false;
            float sizeForWidth = (RoomHalfWidth + 0.5f) / Mathf.Max(0.1f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 0.65f, -10f);
            gameCamera.orthographicSize = Mathf.Max(5.85f, sizeForWidth);
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousCameraFollowEnabled;
            cameraCaptured = false;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class Stage71RoamingPoseKey : MonoBehaviour
    {
        private const float LeftBound = -12.1f;
        private const float RightBound = 8.55f;
        private float phase;
        private Rigidbody2D body;
        private StageManager stageManager;
        private Stage71PoseRoomsController roomController;
        private PlayerCarryController[] carriers = System.Array.Empty<PlayerCarryController>();
        private float nextCarrierRefresh;

        public void Configure(float configuredPhase) => phase = configuredPhase;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            roomController = Object.FindFirstObjectByType<Stage71PoseRoomsController>();
        }

        private void FixedUpdate()
        {
            if (body == null || !body.simulated) return;
            if (stageManager != null && stageManager.IsOnlineStageActive && !stageManager.IsOnlineStageHost) return;
            if (roomController != null && !roomController.KeysMayRoam)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }
            if (IsCarried()) return;

            float directionWave = Mathf.Sin(Time.time * 0.43f + phase);
            float walkingWindow = Mathf.Sin(Time.time * 0.21f + phase * 1.37f);
            float targetX = walkingWindow < -0.28f
                ? 0f
                : (directionWave >= 0f ? 1f : -1f) * (0.72f + 0.22f * Mathf.Sin(phase));
            if (body.position.x <= LeftBound) targetX = Mathf.Abs(targetX) + 0.55f;
            else if (body.position.x >= RightBound) targetX = -Mathf.Abs(targetX) - 0.55f;

            body.linearVelocity = new Vector2(
                Mathf.MoveTowards(body.linearVelocity.x, targetX, 2.4f * Time.fixedDeltaTime),
                body.linearVelocity.y);
            body.angularVelocity = 0f;
        }

        private bool IsCarried()
        {
            if (Time.unscaledTime >= nextCarrierRefresh)
            {
                nextCarrierRefresh = Time.unscaledTime + 0.25f;
                carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            }
            for (int i = 0; i < carriers.Length; i++)
                if (carriers[i] != null && carriers[i].IsHoldingTarget(transform)) return true;
            return false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Stage71FloatingPoseKey : MonoBehaviour
    {
        private float phase;
        private Rigidbody2D body;
        private PlayerCarryController[] carriers = System.Array.Empty<PlayerCarryController>();
        private StageManager stageManager;
        private Stage71PoseRoomsController roomController;
        private float nextCarrierRefresh;

        public void Configure(float configuredPhase)
        {
            phase = configuredPhase;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            roomController = Object.FindFirstObjectByType<Stage71PoseRoomsController>();
        }

        private void FixedUpdate()
        {
            if (body == null || !body.simulated) return;
            if (stageManager != null && stageManager.IsOnlineStageActive && !stageManager.IsOnlineStageHost) return;
            if (roomController != null && !roomController.KeysMayRoam)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }
            if (Time.unscaledTime >= nextCarrierRefresh)
            {
                nextCarrierRefresh = Time.unscaledTime + 0.25f;
                carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            }
            for (int i = 0; i < carriers.Length; i++)
                if (carriers[i] != null && carriers[i].IsHoldingTarget(transform)) return;

            float flightPulse = Mathf.Clamp01(Mathf.Sin(Time.time * 0.31f + phase) * 1.8f);
            float desiredX = Mathf.Sin(Time.time * 0.39f + phase * 0.73f)
                * Mathf.Lerp(0.18f, 1.15f, flightPulse);
            float desiredY = Mathf.Sin(Time.time * 1.15f + phase)
                * Mathf.Lerp(0.32f, 0.72f, flightPulse);
            if (body.position.x <= -12.1f) desiredX = Mathf.Abs(desiredX) + 0.5f;
            else if (body.position.x >= 8.55f) desiredX = -Mathf.Abs(desiredX) - 0.5f;
            if (body.position.y <= -2.85f) desiredY = Mathf.Abs(desiredY) + 0.35f;
            else if (body.position.y >= 2.45f) desiredY = -Mathf.Abs(desiredY) - 0.35f;

            body.linearVelocity = new Vector2(
                Mathf.MoveTowards(body.linearVelocity.x, desiredX, 0.9f * Time.fixedDeltaTime),
                Mathf.MoveTowards(body.linearVelocity.y, desiredY, 1.2f * Time.fixedDeltaTime));
            body.angularVelocity = 0f;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageInsertedKeyTurnAnimation : MonoBehaviour
    {
        private const float Duration = 1.05f;
        private Quaternion startRotation;
        private Quaternion endRotation;
        private Vector3 startScale;
        private float elapsed;

        public static void Play(Transform key, Transform keyhole)
        {
            if (key == null) return;
            StageInsertedKeyTurnAnimation animation = key.GetComponent<StageInsertedKeyTurnAnimation>();
            if (animation == null) animation = key.gameObject.AddComponent<StageInsertedKeyTurnAnimation>();
            animation.startRotation = keyhole != null ? keyhole.rotation : key.rotation;
            animation.endRotation = animation.startRotation * Quaternion.Euler(0f, 0f, -110f);
            animation.startScale = key.localScale;
            animation.elapsed = 0f;
            animation.enabled = true;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.14f;
            transform.localScale = startScale * pulse;
            if (t < 1f) return;
            transform.localScale = startScale;
            enabled = false;
        }
    }
}
