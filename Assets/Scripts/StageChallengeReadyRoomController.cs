using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageChallengeReadyRoomController : MonoBehaviour
    {
        private const string NetworkKind = "challenge_ready_room";
        private const float MinimumRoomWidth = 5.4f;
        private const float MinimumRoomHeight = 4.6f;

        [System.Serializable]
        private sealed class ReadyMessage
        {
            public bool Ready;
            public bool Launch;
            public string[] ReadyIds;
        }

        private sealed class RoomVisual
        {
            public Vector2 Center;
            public Vector2 ButtonCenter;
            public Transform ButtonCap;
        }

        private readonly List<PlayerController2D> offlinePlayers = new List<PlayerController2D>();
        private readonly List<string> expectedIds = new List<string>();
        private readonly List<RoomVisual> rooms = new List<RoomVisual>();
        private readonly HashSet<string> readyIds = new HashSet<string>();
        private readonly HashSet<string> buttonArmedIds = new HashSet<string>();
        private readonly Dictionary<PlayerController2D, Vector3> returnPositions =
            new Dictionary<PlayerController2D, Vector3>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Transform suspendedStageRoot;
        private TextMesh descriptionText;
        private TextMesh statusText;
        private string stageId;
        private string localId;
        private bool configured;
        private bool launched;
        private bool lastLocalReady;
        private float roomWidth = MinimumRoomWidth;
        private float roomHeight = MinimumRoomHeight;
        private float maximumBodyWidth = 1.5f;
        private float maximumBodyHeight = 2.5f;
        private float nextBodyFitScanTime;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        public void Configure(StageManager manager, Transform stageRoot)
        {
            stageManager = manager;
            suspendedStageRoot = stageRoot;
            stageId = manager != null ? manager.CurrentStageId : string.Empty;
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            localId = onlineManager != null ? onlineManager.LocalPlayerId : string.Empty;

            CaptureRosterAndReturnPositions();
            BuildRooms();
            PositionPlayersInRooms();
            if (suspendedStageRoot != null) suspendedStageRoot.gameObject.SetActive(false);
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
            configured = true;
            RefreshPresentation();
        }

        public void Abort()
        {
            if (!launched && suspendedStageRoot != null)
            {
                suspendedStageRoot.gameObject.SetActive(true);
            }
            launched = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            stageManager?.CancelChallengeReadyRoomReference(this);
        }

        private void Update()
        {
            if (!configured || launched || stageManager == null) return;
            if (stageManager.CurrentStageId != stageId)
            {
                Abort();
                return;
            }

            if (Time.unscaledTime >= nextBodyFitScanTime)
            {
                nextBodyFitScanTime = Time.unscaledTime + 0.25f;
                ExpandRoomsForCurrentBodies();
            }

            if (IsOnline)
            {
                bool overlappingButton = IsLocalPlayerOnAssignedButton();
                if (!overlappingButton && !string.IsNullOrEmpty(localId)) buttonArmedIds.Add(localId);
                bool localReady = buttonArmedIds.Contains(localId) && overlappingButton;
                if (localReady != lastLocalReady)
                {
                    lastLocalReady = localReady;
                    SendReadyRequest(localReady);
                    if (HasAuthority) SetReady(localId, localReady);
                }
            }
            else
            {
                RefreshOfflineReadyState();
            }

            RefreshPresentation();
            if (HasAuthority && AreAllPlayersReady()) LaunchForEveryone();
        }

        private void CaptureRosterAndReturnPositions()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            for (int i = 0; i < players.Length && i < 4; i++)
            {
                if (players[i] == null) continue;
                returnPositions[players[i]] = players[i].transform.position;
                if (TryGetSolidBounds(players[i], out Bounds bounds))
                {
                    maximumBodyWidth = Mathf.Max(maximumBodyWidth, bounds.size.x);
                    maximumBodyHeight = Mathf.Max(maximumBodyHeight, bounds.size.y);
                }
            }
            roomWidth = Mathf.Max(MinimumRoomWidth, maximumBodyWidth + 5.2f);
            roomHeight = Mathf.Max(MinimumRoomHeight, maximumBodyHeight + 2.4f);

            if (IsOnline)
            {
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                if (roster != null)
                {
                    List<OnlinePlayerInfo> orderedRoster = new List<OnlinePlayerInfo>();
                    for (int i = 0; i < roster.Length; i++)
                    {
                        OnlinePlayerInfo playerInfo = roster[i];
                        if (playerInfo == null || string.IsNullOrEmpty(playerInfo.PlayerId)) continue;
                        bool duplicate = false;
                        for (int known = 0; known < orderedRoster.Count; known++)
                        {
                            if (orderedRoster[known].PlayerId == playerInfo.PlayerId)
                            {
                                duplicate = true;
                                break;
                            }
                        }
                        if (!duplicate) orderedRoster.Add(playerInfo);
                    }

                    // Lobby array order can briefly differ between peers while a
                    // stage is opening. Host-first plus stable player id ordering
                    // makes P1/P2 rooms identical on every machine.
                    orderedRoster.Sort((left, right) =>
                    {
                        if (left.IsHost != right.IsHost) return left.IsHost ? -1 : 1;
                        return string.CompareOrdinal(left.PlayerId, right.PlayerId);
                    });
                    for (int i = 0; i < orderedRoster.Count && expectedIds.Count < 4; i++)
                        expectedIds.Add(orderedRoster[i].PlayerId);
                }
                if (!string.IsNullOrEmpty(localId) && !expectedIds.Contains(localId))
                {
                    // A newly joined client's local id can arrive one roster event
                    // before its own entry. Keep it out of P1's fallback room.
                    if (expectedIds.Count < 4) expectedIds.Add(localId);
                    else expectedIds[expectedIds.Count - 1] = localId;
                }
                return;
            }

            for (int i = 0; i < players.Length && offlinePlayers.Count < 4; i++)
            {
                if (players[i] != null) offlinePlayers.Add(players[i]);
            }
        }

        private void BuildRooms()
        {
            int count = Mathf.Clamp(IsOnline ? expectedIds.Count : offlinePlayers.Count, 1, 4);
            int columns = count == 1 ? 1 : 2;
            int rows = Mathf.CeilToInt(count / (float)columns);
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Vector2 center = new Vector2(
                    (column - (columns - 1) * 0.5f) * roomWidth,
                    ((rows - 1) * 0.5f - row) * roomHeight);
                CreateTerrain("Floor", center + Vector2.down * roomHeight * 0.5f, new Vector2(roomWidth, 0.38f));
                CreateTerrain("Ceiling", center + Vector2.up * roomHeight * 0.5f, new Vector2(roomWidth, 0.38f));
                CreateTerrain("Left Wall", center + Vector2.left * roomWidth * 0.5f, new Vector2(0.38f, roomHeight));
                CreateTerrain("Right Wall", center + Vector2.right * roomWidth * 0.5f, new Vector2(0.38f, roomHeight));

                GameObject button = new GameObject("Ready Button P" + (i + 1));
                button.transform.SetParent(transform, false);
                Vector2 buttonCenter = center + new Vector2(roomWidth * 0.5f - 1.35f, -roomHeight * 0.5f + 0.38f);
                button.transform.localPosition = buttonCenter;
                StageEscortController.AddFilledRect(button.transform, "Heavy Base",
                    new Vector2(0.03f, 0f), new Vector2(2.15f, 0.3f), new Color(0.42f, 0.42f, 0.37f), 42);
                button.transform.Find("Heavy Base").localRotation = Quaternion.Euler(0f, 0f, 1.7f);
                AddWonkyBox(button.transform, new Vector2(0.03f, 0f), new Vector2(2.15f, 0.3f), 45);
                StageEscortController.AddFilledRect(button.transform, "Button Neck",
                    new Vector2(0f, 0.22f), new Vector2(1.25f, 0.28f), new Color(0.42f, 0.12f, 0.1f), 43);
                Transform cap = new GameObject("Wonky Crayon Cap").transform;
                cap.SetParent(button.transform, false);
                cap.localPosition = new Vector2(0f, 0.5f);
                cap.gameObject.layer = 6;
                cap.gameObject.tag = "Ground";
                BoxCollider2D capCollider = cap.gameObject.AddComponent<BoxCollider2D>();
                capCollider.size = new Vector2(1.68f, 0.43f);
                AddButtonOval(cap, "Crayon Fill 1", new Vector2(-0.035f, 0.01f),
                    new Vector2(1.62f, 0.39f), new Color(0.94f, 0.2f, 0.15f, 0.74f), 45).transform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
                AddButtonOval(cap, "Crayon Fill 2", new Vector2(0.04f, -0.015f),
                    new Vector2(1.52f, 0.36f), new Color(1f, 0.25f, 0.18f, 0.68f), 46).transform.localRotation = Quaternion.Euler(0f, 0f, -3.2f);
                AddButtonOval(cap, "Crayon Fill 3", new Vector2(-0.01f, 0.025f),
                    new Vector2(1.42f, 0.31f), new Color(0.9f, 0.12f, 0.1f, 0.55f), 47);
                AddWonkyOval(cap, new Vector2(1.68f, 0.43f), 48);
                rooms.Add(new RoomVisual
                {
                    Center = center,
                    ButtonCenter = buttonCenter,
                    ButtonCap = cap
                });
            }

            GameObject monitor = new GameObject("Ready Room Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(0f, rows * roomHeight * 0.5f + 2.05f, 0.25f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(15f, 2.75f), 55);
            descriptionText = StageEscortController.CreateText(monitor.transform, "Game Description",
                new Vector3(0f, 0.36f, -0.03f), 58, 0.115f,
                new Color(0.04f, 0.34f, 0.5f), 61);
            statusText = StageEscortController.CreateText(monitor.transform, "Status",
                new Vector3(0f, -0.58f, -0.03f), 64, 0.145f,
                new Color(0.04f, 0.43f, 0.58f), 61);
        }

        private static SpriteRenderer AddButtonOval(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int order)
        {
            GameObject oval = new GameObject(name);
            oval.transform.SetParent(parent, false);
            oval.transform.localPosition = position;
            oval.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = oval.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void AddWonkyBox(Transform parent, Vector2 center, Vector2 size, int order)
        {
            Vector2 half = size * 0.5f;
            Vector2[] points =
            {
                center + new Vector2(-half.x - 0.03f, -half.y + 0.01f),
                center + new Vector2(-half.x + 0.02f, half.y + 0.025f),
                center + new Vector2(half.x + 0.035f, half.y - 0.015f),
                center + new Vector2(half.x - 0.02f, -half.y - 0.025f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                StageEscortController.AddLine(parent, points[i], points[(i + 1) % points.Length],
                    0.055f, new Color(0.18f, 0.16f, 0.13f, 0.82f), order);
            }
        }

        private static void AddWonkyOval(Transform parent, Vector2 size, int order)
        {
            const int segments = 14;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2 previous = default;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    float wobble = 1f + Mathf.Sin(i * 2.73f + pass * 1.9f) * 0.055f;
                    Vector2 point = new Vector2(Mathf.Cos(angle) * size.x * 0.5f,
                        Mathf.Sin(angle) * size.y * 0.5f) * wobble;
                    point += new Vector2(pass * 0.018f, -pass * 0.012f);
                    if (i > 0) StageEscortController.AddLine(parent, previous, point, 0.035f,
                        new Color(0.28f, 0.08f, 0.07f, 0.72f), order + pass);
                    previous = point;
                }
            }
        }

        private void CreateTerrain(string name, Vector2 position, Vector2 size)
        {
            GameObject terrain = new GameObject("Ready Room " + name) { layer = 6, tag = "Ground" };
            terrain.transform.SetParent(transform, false);
            terrain.transform.localPosition = position;
            terrain.AddComponent<BoxCollider2D>().size = size;
            StageEscortController.AddFilledRect(terrain.transform, "Paper", Vector2.zero, size,
                new Color(0.96f, 0.95f, 0.87f), 34);
            StageEscortController.AddBoxOutline(terrain.transform, Vector2.zero, size,
                new Color(0.2f, 0.24f, 0.27f), 36);
        }

        private void PositionPlayersInRooms()
        {
            if (IsOnline)
            {
                PlayerController2D local = stageManager.ActivePlayerTransform != null
                    ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
                int room = expectedIds.IndexOf(localId);
                if (room >= 0) PlacePlayer(local, room);
                return;
            }

            for (int i = 0; i < offlinePlayers.Count && i < rooms.Count; i++) PlacePlayer(offlinePlayers[i], i);
        }

        internal bool TryGetFittedReturnPosition(PlayerController2D player, out Vector3 fittedPosition)
        {
            fittedPosition = player != null ? player.transform.position : Vector3.zero;
            if (player == null || rooms.Count == 0 || !TryGetSolidBounds(player, out Bounds bounds)) return false;

            // Ready rooms are generated from body size, so selecting a larger species
            // should enlarge the rooms rather than reject an otherwise valid drawing.
            ExpandRoomsToFit(bounds.size.x, bounds.size.y, player);
            fittedPosition = player.transform.position;

            int room = IsOnline
                ? expectedIds.IndexOf(stageManager != null ? stageManager.GetOnlinePlayerId(player) : null)
                : offlinePlayers.IndexOf(player);
            if (room < 0 || room >= rooms.Count) return false;

            const float wallInset = 0.31f;
            const float bodyMargin = 0.12f;
            float interiorWidth = roomWidth - (wallInset + bodyMargin) * 2f;
            float interiorHeight = roomHeight - (wallInset + bodyMargin) * 2f;
            if (bounds.size.x > interiorWidth || bounds.size.y > interiorHeight) return false;

            Vector2 center = rooms[room].Center;
            float leftInside = center.x - roomWidth * 0.5f + wallInset + bodyMargin;
            float floorInside = center.y - roomHeight * 0.5f + 0.19f + bodyMargin;
            float desiredBodyCenterX = leftInside + bounds.extents.x;
            fittedPosition.x += desiredBodyCenterX - bounds.center.x;
            fittedPosition.y += floorInside - bounds.min.y;

            // The assigned start side must also remain clear of the ready button.
            float fittedRight = bounds.max.x + (fittedPosition.x - player.transform.position.x);
            float buttonLeft = rooms[room].ButtonCenter.x - 1.25f;
            return fittedRight + 0.28f < buttonLeft;
        }

        private void ExpandRoomsForCurrentBodies()
        {
            float requiredBodyWidth = maximumBodyWidth;
            float requiredBodyHeight = maximumBodyHeight;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            for (int i = 0; i < players.Length; i++)
            {
                if (!TryGetSolidBounds(players[i], out Bounds bounds)) continue;
                requiredBodyWidth = Mathf.Max(requiredBodyWidth, bounds.size.x);
                requiredBodyHeight = Mathf.Max(requiredBodyHeight, bounds.size.y);
            }
            ExpandRoomsToFit(requiredBodyWidth, requiredBodyHeight, null);
        }

        private void ExpandRoomsToFit(
            float requiredBodyWidth,
            float requiredBodyHeight,
            PlayerController2D playerBeingRedrawn)
        {
            float nextMaximumWidth = Mathf.Max(maximumBodyWidth, requiredBodyWidth);
            float nextMaximumHeight = Mathf.Max(maximumBodyHeight, requiredBodyHeight);
            float nextRoomWidth = Mathf.Max(MinimumRoomWidth, nextMaximumWidth + 5.2f);
            float nextRoomHeight = Mathf.Max(MinimumRoomHeight, nextMaximumHeight + 2.4f);
            if (nextRoomWidth <= roomWidth + 0.05f && nextRoomHeight <= roomHeight + 0.05f)
            {
                return;
            }

            maximumBodyWidth = nextMaximumWidth;
            maximumBodyHeight = nextMaximumHeight;
            roomWidth = nextRoomWidth;
            roomHeight = nextRoomHeight;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            rooms.Clear();
            descriptionText = null;
            statusText = null;
            readyIds.Clear();
            buttonArmedIds.Clear();
            lastLocalReady = false;
            BuildRooms();

            if (IsOnline)
            {
                PlayerController2D local = stageManager.ActivePlayerTransform != null
                    ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                    : null;
                if (local != null && local != playerBeingRedrawn)
                {
                    int room = expectedIds.IndexOf(localId);
                    if (room >= 0) PlacePlayer(local, room);
                }
            }
            else
            {
                for (int i = 0; i < offlinePlayers.Count && i < rooms.Count; i++)
                {
                    if (offlinePlayers[i] != null && offlinePlayers[i] != playerBeingRedrawn)
                        PlacePlayer(offlinePlayers[i], i);
                }
            }
            RefreshPresentation();
        }

        private void PlacePlayer(PlayerController2D player, int room)
        {
            if (player == null || rooms.Count == 0) return;
            room = Mathf.Clamp(room, 0, rooms.Count - 1);
            Vector3 destination = rooms[room].Center;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;
                body.position = destination;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            player.transform.position = destination;
            Physics2D.SyncTransforms();
            if (TryGetSolidBounds(player, out Bounds bounds))
            {
                float floorTop = rooms[room].Center.y - roomHeight * 0.5f + 0.19f;
                float desiredBodyCenterX = rooms[room].Center.x - roomWidth * 0.5f
                    + bounds.size.x * 0.5f + 0.55f;
                destination.x += desiredBodyCenterX - bounds.center.x;
                destination.y += floorTop + 0.06f - bounds.min.y;
                if (body != null) body.position = destination;
                player.transform.position = destination;
            }
            player.ResetMotion();
            player.SetControlsEnabled(true);
            Physics2D.SyncTransforms();
        }

        private bool IsLocalPlayerOnAssignedButton()
        {
            PlayerController2D local = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            int room = expectedIds.IndexOf(localId);
            return local != null && room >= 0 && room < rooms.Count && IsPlayerPressingRoomButton(local, room);
        }

        private void RefreshOfflineReadyState()
        {
            readyIds.Clear();
            for (int i = 0; i < offlinePlayers.Count && i < rooms.Count; i++)
            {
                PlayerController2D player = offlinePlayers[i];
                string id = "offline-" + i;
                bool overlappingButton = player != null && IsPlayerPressingRoomButton(player, i);
                if (!overlappingButton) buttonArmedIds.Add(id);
                if (buttonArmedIds.Contains(id) && overlappingButton) readyIds.Add(id);
            }
        }

        private bool IsPlayerPressingRoomButton(PlayerController2D player, int room)
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            // The character stands on top of the cap, so scan above its physical
            // surface. The old box ended below short cat/slime bodies and could
            // leave a non-host client unable to report its ready state.
            Vector2 buttonCenter = rooms[room].ButtonCenter + Vector2.up * 0.78f;
            Collider2D[] hits = Physics2D.OverlapBoxAll(buttonCenter, new Vector2(2.45f, 0.78f), 0f);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] != null && hits[i].GetComponentInParent<PlayerController2D>() == player) return true;
            return false;
        }

        private static bool TryGetSolidBounds(PlayerController2D player, out Bounds bounds)
        {
            bounds = default;
            if (player == null) return false;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }

        private void SetReady(string playerId, bool ready)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (ready) readyIds.Add(playerId);
            else readyIds.Remove(playerId);
            if (HasAuthority) BroadcastSnapshot(false);
        }

        private bool AreAllPlayersReady()
        {
            if (IsOnline)
            {
                if (expectedIds.Count == 0) return false;
                for (int i = 0; i < expectedIds.Count; i++)
                    if (!readyIds.Contains(expectedIds[i])) return false;
                return true;
            }
            return offlinePlayers.Count > 0 && readyIds.Count >= offlinePlayers.Count;
        }

        private void RefreshPresentation()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                string id = IsOnline && i < expectedIds.Count ? expectedIds[i] : "offline-" + i;
                bool ready = readyIds.Contains(id);
                if (rooms[i].ButtonCap != null)
                {
                    SpriteRenderer[] fills = rooms[i].ButtonCap.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int fill = 0; fill < fills.Length; fill++)
                    {
                        if (fills[fill] == null) continue;
                        Color color = ready
                            ? new Color(0.22f, 0.76f - fill * 0.035f, 0.35f, fills[fill].color.a)
                            : new Color(0.94f, 0.18f + fill * 0.035f, 0.14f, fills[fill].color.a);
                        fills[fill].color = color;
                    }
                    Vector3 capPosition = rooms[i].ButtonCap.localPosition;
                    capPosition.y = ready ? 0.31f : 0.5f;
                    rooms[i].ButtonCap.localPosition = capPosition;
                }
            }
            if (statusText != null)
            {
                int total = IsOnline ? expectedIds.Count : offlinePlayers.Count;
                statusText.text = LocalizationManager.Format("ready_room_status", readyIds.Count, total);
            }
            if (descriptionText != null)
            {
                descriptionText.text = LocalizationManager.T(GetGameDescriptionKey());
            }
        }

        private string GetGameDescriptionKey()
        {
            return "ready_room_game_" + (string.IsNullOrEmpty(stageId)
                ? "default"
                : stageId.Replace('-', '_'));
        }

        private void SendReadyRequest(bool ready)
        {
            if (onlineManager == null || string.IsNullOrEmpty(localId)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = NetworkKind,
                Json = JsonUtility.ToJson(new ReadyMessage { Ready = ready })
            });
        }

        private void BroadcastSnapshot(bool launch)
        {
            if (!IsOnline || onlineManager == null) return;
            string[] ids = new string[readyIds.Count];
            readyIds.CopyTo(ids);
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = NetworkKind,
                Json = JsonUtility.ToJson(new ReadyMessage { ReadyIds = ids, Launch = launch })
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != stageId || data.Kind != NetworkKind) return;
            ReadyMessage message = JsonUtility.FromJson<ReadyMessage>(data.Json);
            if (message == null) return;

            if (HasAuthority && message.ReadyIds == null && !string.IsNullOrEmpty(data.PlayerId))
            {
                SetReady(data.PlayerId, message.Ready);
                return;
            }

            if (!HasAuthority && message.ReadyIds != null)
            {
                readyIds.Clear();
                for (int i = 0; i < message.ReadyIds.Length; i++)
                    if (!string.IsNullOrEmpty(message.ReadyIds[i])) readyIds.Add(message.ReadyIds[i]);
            }
            if (message.Launch) Launch();
        }

        private void LaunchForEveryone()
        {
            BroadcastSnapshot(true);
            Launch();
        }

        private void Launch()
        {
            if (launched) return;
            launched = true;
            if (suspendedStageRoot != null) suspendedStageRoot.gameObject.SetActive(true);

            foreach (KeyValuePair<PlayerController2D, Vector3> entry in returnPositions)
            {
                PlayerController2D player = entry.Key;
                if (player == null || IsOnline && player.transform != stageManager.ActivePlayerTransform) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.simulated = true;
                    body.position = entry.Value;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                player.transform.position = entry.Value;
                player.ResetMotion();
                player.SetControlsEnabled(true);
                stageManager.RecordAssignedPlayerStart(player, entry.Value);
            }
            Physics2D.SyncTransforms();
            GameSfx.Play(SfxId.StageCountdownGo);
            stageManager.CompleteChallengeReadyRoom();
            Destroy(gameObject);
        }
    }
}
