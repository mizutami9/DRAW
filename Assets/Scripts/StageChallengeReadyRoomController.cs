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
        private const float RoomBodyHorizontalPadding = 5.2f;
        private const float RoomBodyVerticalPadding = 2.4f;
        private const float RoomFrameThickness = 0.72f;
        private const float ReadyStateResendInterval = 0.5f;

        [System.Serializable]
        private sealed class ReadyMessage
        {
            public bool Ready;
            public bool Launch;
            public string[] ReadyIds;
            public string FitRejectedId;
            public float RoomWidth;
            public float RoomHeight;
        }

        private sealed class RoomVisual
        {
            public Vector2 Center;
            public Vector2 ButtonCenter;
            public Transform ButtonCap;
            public Collider2D ButtonCollider;
        }

        private readonly struct RecommendationEntry
        {
            public readonly DrawManager.Species Species;
            public readonly int Count;

            public RecommendationEntry(DrawManager.Species species, int count)
            {
                Species = species;
                Count = count;
            }
        }

        private readonly List<PlayerController2D> offlinePlayers = new List<PlayerController2D>();
        private readonly List<string> expectedIds = new List<string>();
        private readonly List<RoomVisual> rooms = new List<RoomVisual>();
        private readonly HashSet<string> readyIds = new HashSet<string>();
        private readonly HashSet<string> buttonArmedIds = new HashSet<string>();
        private readonly HashSet<string> fitRejectedIds = new HashSet<string>();
        private readonly Dictionary<PlayerController2D, Vector3> returnPositions =
            new Dictionary<PlayerController2D, Vector3>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Transform suspendedStageRoot;
        private TextMesh descriptionText;
        private TextMesh statusText;
        private TextMesh recommendationTitleText;
        private TextMesh recommendationNoneText;
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
        private float nextLocalReadySendTime;
        private float nextSessionStateRequestTime;
        private float nextRegressionAutoReadyTime;
        private bool hostSessionStateKnown;
        private int decorationSeed;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        public void Configure(StageManager manager, Transform stageRoot)
        {
            stageManager = manager;
            suspendedStageRoot = stageRoot;
            stageId = manager != null ? manager.CurrentStageId : string.Empty;
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            localId = onlineManager != null ? onlineManager.LocalPlayerId : string.Empty;
            decorationSeed = CreateDecorationSeed();

            CaptureRosterAndReturnPositions();
            BuildRooms();
            PositionPlayersInRooms();
            if (suspendedStageRoot != null) suspendedStageRoot.gameObject.SetActive(false);
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
            configured = true;
            RefreshPresentation();
            if (IsOnline && HasAuthority) BroadcastSnapshot(false);
            else if (IsOnline) RequestHostSessionState();
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

            if (!IsOnline) RefreshOfflineRoster();

            if (Time.unscaledTime >= nextBodyFitScanTime)
            {
                nextBodyFitScanTime = Time.unscaledTime + 0.25f;
                ExpandRoomsForCurrentBodies();
            }

            // Each local regression executable owns exactly one controllable
            // player. Unfocused Unity windows cannot receive keyboard input, so
            // move that process' player onto its own physical ready button. This
            // still exercises the regular overlap and host-authoritative ready
            // flow, while leaving normal online sessions completely manual.
            if (IsOnline
                && onlineManager != null
                && onlineManager.IsLocalRegressionActive
                && Time.unscaledTime >= nextRegressionAutoReadyTime)
            {
                nextRegressionAutoReadyTime = Time.unscaledTime + 0.25f;
                AutoPlaceLocalPlayerOnReadyButton();
            }

            if (IsOnline)
            {
                if (HasAuthority)
                {
                    RefreshAuthoritativeOnlineReadyState();
                }
                else
                {
                    if (!hostSessionStateKnown && Time.unscaledTime >= nextSessionStateRequestTime)
                        RequestHostSessionState();
                    bool localOnButton = IsLocalPlayerOnAssignedButton();
                    if (!localOnButton) fitRejectedIds.Remove(localId);
                    bool localReady = localOnButton && !fitRejectedIds.Contains(localId);
                    bool readyChanged = localReady != lastLocalReady;
                    if (readyChanged)
                    {
                        lastLocalReady = localReady;
                    }
                    if (readyChanged || Time.unscaledTime >= nextLocalReadySendTime)
                    {
                        nextLocalReadySendTime = Time.unscaledTime + ReadyStateResendInterval;
                        SendReadyRequest(localReady);
                    }
                }
            }
            else
            {
                RefreshOfflineReadyState();
            }

            RefreshPresentation();
            if (HasAuthority && AreAllPlayersReady()) LaunchForEveryone();
        }

        private void RequestHostSessionState()
        {
            nextSessionStateRequestTime = Time.unscaledTime + 0.75f;
            stageManager?.RequestChallengeSessionState();
        }

        internal void ApplyHostRunState(bool runStarted)
        {
            hostSessionStateKnown = true;
            if (runStarted) Launch();
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
            roomWidth = Mathf.Max(MinimumRoomWidth, maximumBodyWidth + RoomBodyHorizontalPadding);
            roomHeight = Mathf.Max(MinimumRoomHeight, maximumBodyHeight + RoomBodyVerticalPadding);

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

        private void RefreshOfflineRoster()
        {
            PlayerController2D[] current = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            bool changed = current.Length != offlinePlayers.Count;
            if (!changed)
            {
                for (int i = 0; i < current.Length; i++)
                    if (!offlinePlayers.Contains(current[i])) { changed = true; break; }
            }
            if (!changed) return;

            List<PlayerController2D> previous = new List<PlayerController2D>(offlinePlayers);
            offlinePlayers.Clear();
            for (int i = 0; i < current.Length && offlinePlayers.Count < 4; i++)
            {
                PlayerController2D player = current[i];
                if (player == null) continue;
                offlinePlayers.Add(player);
                if (returnPositions.ContainsKey(player)) continue;
                Vector3 stagePosition = player.transform.position;
                if (previous.Count > 0 && previous[0] != null
                    && returnPositions.TryGetValue(previous[0], out Vector3 firstStart))
                    stagePosition = firstStart + Vector3.right * (offlinePlayers.Count - 1) * 1.55f;
                returnPositions[player] = stagePosition;
            }

            List<PlayerController2D> known = new List<PlayerController2D>(returnPositions.Keys);
            for (int i = 0; i < known.Count; i++)
                if (known[i] == null || !offlinePlayers.Contains(known[i])) returnPositions.Remove(known[i]);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            rooms.Clear();
            descriptionText = null;
            statusText = null;
            recommendationTitleText = null;
            recommendationNoneText = null;
            readyIds.Clear();
            buttonArmedIds.Clear();
            lastLocalReady = false;
            BuildRooms();
            PositionPlayersInRooms();
            RefreshPresentation();
        }

        private void BuildRooms()
        {
            int count = Mathf.Clamp(IsOnline ? expectedIds.Count : offlinePlayers.Count, 1, 4);
            int columns = count == 1 ? 1 : 2;
            int rows = Mathf.CeilToInt(count / (float)columns);
            CreateGlobalReadyRoomBackdrop(GetGlobalBackgroundTheme());
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Vector2 center = new Vector2(
                    (column - (columns - 1) * 0.5f) * roomWidth,
                    ((rows - 1) * 0.5f - row) * roomHeight);
                CreateRedrawSpot(center, i);
                CreateTerrain("Floor", center + Vector2.down * roomHeight * 0.5f,
                    new Vector2(roomWidth + RoomFrameThickness, RoomFrameThickness));
                CreateTerrain("Ceiling", center + Vector2.up * roomHeight * 0.5f,
                    new Vector2(roomWidth + RoomFrameThickness, RoomFrameThickness));
                CreateTerrain("Left Wall", center + Vector2.left * roomWidth * 0.5f,
                    new Vector2(RoomFrameThickness, roomHeight + RoomFrameThickness));
                CreateTerrain("Right Wall", center + Vector2.right * roomWidth * 0.5f,
                    new Vector2(RoomFrameThickness, roomHeight + RoomFrameThickness));
                CreateReadyRoomPresentation(center, i);

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
                    ButtonCap = cap,
                    ButtonCollider = capCollider
                });
            }

            bool showRecommendations = ShouldShowRecommendationMonitor();
            float monitorY = rows * roomHeight * 0.5f + 2.35f;
            bool spaciousDescription = stageId == "14-3";
            const float originalDescriptionHeight = 2.7f;
            const float descriptionHeight = 3.6f;
            // Keep the top edge at the established camera-safe height and use
            // the free gap below it for a true two-line description area.
            monitorY -= (descriptionHeight - originalDescriptionHeight) * 0.5f;
            float descriptionWidth = showRecommendations ? 12f : spaciousDescription ? 18.5f : 16.5f;
            float descriptionX = showRecommendations ? -2.8f : 0f;
            GameObject monitor = new GameObject("Ready Room Game Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(descriptionX, monitorY, 0.25f);
            DoodleMonitorVisuals.Build(monitor.transform,
                new Vector2(descriptionWidth, descriptionHeight), 55);
            descriptionText = StageEscortController.CreateText(monitor.transform, "Game Description",
                new Vector3(0f, 0.5f, -0.03f), 58, spaciousDescription ? 0.09f : showRecommendations ? 0.105f : 0.12f,
                new Color(0.04f, 0.34f, 0.5f), 61);
            statusText = StageEscortController.CreateText(monitor.transform, "Status",
                new Vector3(0f, -0.82f, -0.03f), 64, 0.145f,
                new Color(0.04f, 0.43f, 0.58f), 61);

            if (showRecommendations)
            {
                BuildRecommendationMonitor(new Vector3(6.2f, monitorY, 0.25f), count);
            }
        }

        private void CreateRedrawSpot(Vector2 center, int index)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(
                StageObjectType.RedrawZone, center);
            data.objectId = "ready_room_redraw_spot_" + index;
            data.size = new Vector2(roomWidth, roomHeight);
            GameObject backdrop = StageRedrawZoneFactory.CreateRedrawZone(data, transform);
            if (backdrop != null)
            {
                backdrop.name = "Ready Room Redraw Spot " + (index + 1);
                backdrop.transform.localPosition = center;
            }
        }

        private bool ShouldShowRecommendationMonitor()
        {
            if (string.IsNullOrEmpty(stageId)) return false;
            int separator = stageId.IndexOf('-');
            return separator > 0
                && int.TryParse(stageId.Substring(0, separator), out int world)
                && world < 11;
        }

        private void BuildRecommendationMonitor(Vector3 position, int playerCount)
        {
            GameObject monitor = new GameObject("Ready Room Recommendation Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = position;
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(5.2f, 3.6f), 55);
            recommendationTitleText = StageEscortController.CreateText(monitor.transform, "Recommendation Title",
                new Vector3(0f, 0.78f, -0.03f), 48, 0.085f,
                new Color(0.04f, 0.34f, 0.5f), 61);

            RecommendationEntry[] entries = GetRecommendations(playerCount);
            if (entries.Length == 0)
            {
                recommendationNoneText = StageEscortController.CreateText(monitor.transform, "No Recommendation",
                    new Vector3(0f, -0.43f, -0.03f), 52, 0.105f,
                    new Color(0.18f, 0.2f, 0.2f), 61);
                return;
            }

            float spacing = entries.Length >= 3 ? 1.4f : entries.Length == 2 ? 1.85f : 0f;
            float startX = -(entries.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < entries.Length; i++)
            {
                CreateRecommendationIcon(monitor.transform, entries[i],
                    new Vector2(startX + i * spacing, -0.43f));
            }
        }

        private RecommendationEntry[] GetRecommendations(int playerCount)
        {
            int count = Mathf.Clamp(playerCount, 1, 4);
            switch (stageId)
            {
                case "2-3":
                    return Recommendations((DrawManager.Species.Cat, count));
                case "2-2":
                case "8-1":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count == 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 1));
                    if (count == 3) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Cat, 1));
                    if (count == 4) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Cat, 2));
                    break;
                case "4-3":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count == 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Turtle, 1));
                    if (count == 3) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Turtle, 1));
                    if (count == 4) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Turtle, 2));
                    break;
                case "6-2":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count == 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 1));
                    if (count == 3) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 2));
                    if (count == 4) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Cat, 2));
                    break;
                case "6-3":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count == 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 1));
                    if (count == 3) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 1), (DrawManager.Species.Bird, 1));
                    if (count == 4) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, 1), (DrawManager.Species.Bird, 2));
                    break;
                case "8-2":
                    return System.Array.Empty<RecommendationEntry>();
                case "8-3":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count >= 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Cat, count - 1));
                    break;
                case "9-1":
                case "9-3":
                case "10-1":
                    return Recommendations((DrawManager.Species.Slime, count));
                case "9-2":
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count == 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Bird, 1));
                    if (count == 3) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Bird, 2));
                    if (count == 4) return Recommendations((DrawManager.Species.Human, 2), (DrawManager.Species.Bird, 2));
                    break;
                case "10-3":
                    return Recommendations((DrawManager.Species.Human, count));
            }
            return System.Array.Empty<RecommendationEntry>();
        }

        private static RecommendationEntry[] Recommendations(
            params (DrawManager.Species species, int count)[] entries)
        {
            RecommendationEntry[] result = new RecommendationEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                result[i] = new RecommendationEntry(entries[i].species, entries[i].count);
            return result;
        }

        private static void CreateRecommendationIcon(Transform parent, RecommendationEntry entry, Vector2 position)
        {
            Transform root = new GameObject("Recommended " + entry.Species).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(position.x - 0.22f, position.y, -0.03f);
            Color ink = GetRecommendationColor(entry.Species);

            switch (entry.Species)
            {
                case DrawManager.Species.Cat:
                    AddIconLine(root, new Vector2(-0.38f, -0.28f), new Vector2(-0.34f, 0.31f), ink);
                    AddIconLine(root, new Vector2(-0.34f, 0.31f), new Vector2(-0.12f, 0.12f), ink);
                    AddIconLine(root, new Vector2(-0.12f, 0.12f), new Vector2(0.12f, 0.12f), ink);
                    AddIconLine(root, new Vector2(0.12f, 0.12f), new Vector2(0.34f, 0.31f), ink);
                    AddIconLine(root, new Vector2(0.34f, 0.31f), new Vector2(0.38f, -0.28f), ink);
                    AddIconLine(root, new Vector2(0.38f, -0.28f), new Vector2(-0.38f, -0.28f), ink);
                    break;
                case DrawManager.Species.Bird:
                    AddIconLine(root, new Vector2(-0.43f, -0.2f), new Vector2(-0.12f, 0.2f), ink);
                    AddIconLine(root, new Vector2(-0.12f, 0.2f), new Vector2(0.08f, -0.08f), ink);
                    AddIconLine(root, new Vector2(0.08f, -0.08f), new Vector2(0.42f, 0.2f), ink);
                    AddIconLine(root, new Vector2(0.08f, -0.08f), new Vector2(0.34f, -0.25f), ink);
                    break;
                case DrawManager.Species.Turtle:
                    AddIconLine(root, new Vector2(-0.4f, -0.25f), new Vector2(-0.28f, 0.23f), ink);
                    AddIconLine(root, new Vector2(-0.28f, 0.23f), new Vector2(0.23f, 0.23f), ink);
                    AddIconLine(root, new Vector2(0.23f, 0.23f), new Vector2(0.38f, -0.25f), ink);
                    AddIconLine(root, new Vector2(0.38f, -0.25f), new Vector2(-0.4f, -0.25f), ink);
                    AddIconLine(root, new Vector2(0.38f, -0.12f), new Vector2(0.56f, 0.02f), ink);
                    break;
                case DrawManager.Species.Slime:
                    AddIconLine(root, new Vector2(-0.43f, -0.27f), new Vector2(-0.29f, 0.16f), ink);
                    AddIconLine(root, new Vector2(-0.29f, 0.16f), new Vector2(0.02f, 0.3f), ink);
                    AddIconLine(root, new Vector2(0.02f, 0.3f), new Vector2(0.37f, 0.12f), ink);
                    AddIconLine(root, new Vector2(0.37f, 0.12f), new Vector2(0.43f, -0.27f), ink);
                    AddIconLine(root, new Vector2(0.43f, -0.27f), new Vector2(-0.43f, -0.27f), ink);
                    break;
                default:
                    AddIconLine(root, new Vector2(-0.17f, 0.34f), new Vector2(0.17f, 0.34f), ink);
                    AddIconLine(root, new Vector2(0.17f, 0.34f), new Vector2(0.17f, 0.06f), ink);
                    AddIconLine(root, new Vector2(0.17f, 0.06f), new Vector2(-0.17f, 0.06f), ink);
                    AddIconLine(root, new Vector2(-0.17f, 0.06f), new Vector2(-0.17f, 0.34f), ink);
                    AddIconLine(root, new Vector2(0f, 0.06f), new Vector2(0f, -0.34f), ink);
                    AddIconLine(root, new Vector2(-0.34f, -0.05f), new Vector2(0.34f, -0.05f), ink);
                    AddIconLine(root, new Vector2(0f, -0.34f), new Vector2(-0.25f, -0.55f), ink);
                    AddIconLine(root, new Vector2(0f, -0.34f), new Vector2(0.25f, -0.55f), ink);
                    break;
            }

            StageEscortController.CreateText(root, "Count", new Vector3(0.72f, -0.08f, 0f),
                48, 0.08f, ink, 62).text = "×" + entry.Count;
        }

        private static void AddIconLine(Transform parent, Vector2 from, Vector2 to, Color color)
        {
            StageEscortController.AddLine(parent, from, to, 0.055f, color, 62);
        }

        private static Color GetRecommendationColor(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat: return new Color(0.92f, 0.48f, 0.08f);
                case DrawManager.Species.Bird: return new Color(0.08f, 0.35f, 0.92f);
                case DrawManager.Species.Turtle: return new Color(0.08f, 0.55f, 0.25f);
                case DrawManager.Species.Slime: return new Color(0.55f, 0.2f, 0.82f);
                default: return new Color(0.88f, 0.12f, 0.12f);
            }
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
        }

        private int CreateDecorationSeed()
        {
            if (!IsOnline) return Random.Range(0, int.MaxValue);
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            string sharedSessionId = lobby == null
                ? "online"
                : !string.IsNullOrEmpty(lobby.LobbyId)
                    ? "lobby:" + lobby.LobbyId
                    : !string.IsNullOrEmpty(lobby.RoomCode)
                        ? "room:" + lobby.RoomCode
                        : "online";
            string source = sharedSessionId
                + "|" + stageId
                + "|" + (lobby != null ? lobby.StageRevision : 0)
                + "|" + (lobby != null ? lobby.RetryRevision : 0);
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffffu);
            }
        }

        private int GetDecorationTheme(int roomIndex)
        {
            int[] coprimeSteps = { 1, 3, 7, 9 };
            int first = decorationSeed % 10;
            int step = coprimeSteps[(decorationSeed / 10) % coprimeSteps.Length];
            return (first + Mathf.Max(0, roomIndex) * step) % 10;
        }

        private int GetGlobalBackgroundTheme()
        {
            // A session has one coherent background. Individual booths may use
            // other decorations, but every client derives this shared theme from
            // the same online seed.
            return GetDecorationTheme(0);
        }

        private void CreateReadyRoomPresentation(Vector2 center, int roomIndex)
        {
            int decorationTheme = GetDecorationTheme(roomIndex);
            int backgroundTheme = GetGlobalBackgroundTheme();
            GameObject frameRoot = new GameObject(
                "Ready Room Crayon Booth " + (roomIndex + 1)
                + " Decoration " + (decorationTheme + 1)
                + " Background " + (backgroundTheme + 1));
            frameRoot.transform.SetParent(transform, false);
            frameRoot.transform.localPosition = center;

            float halfWidth = roomWidth * 0.5f;
            float halfHeight = roomHeight * 0.5f;
            Color graphite = new Color(0.2f, 0.24f, 0.27f, 0.96f);

            // One shared outer and inner outline keeps all four corners exact.
            StageEscortController.AddBoxOutline(frameRoot.transform, Vector2.zero,
                new Vector2(roomWidth + RoomFrameThickness, roomHeight + RoomFrameThickness), graphite, 38);
            StageEscortController.AddBoxOutline(frameRoot.transform, Vector2.zero,
                new Vector2(roomWidth - RoomFrameThickness, roomHeight - RoomFrameThickness), graphite, 38);

            Color[] crayons =
            {
                new Color(0.12f, 0.68f, 0.92f, 0.48f),
                new Color(1f, 0.32f, 0.28f, 0.46f),
                new Color(1f, 0.76f, 0.12f, 0.46f),
                new Color(0.18f, 0.72f, 0.4f, 0.46f),
                new Color(0.78f, 0.3f, 0.86f, 0.44f)
            };

            // The large, pale doodles are scenery only. They deliberately sit
            // below players, the redraw floor and the physical room frame, and
            // never receive a collider or a gameplay component.
            AddReadyRoomBackdrop(frameRoot.transform, halfWidth, halfHeight,
                roomIndex, backgroundTheme);

            // Uneven colored strokes make the frame read as a handmade booth,
            // while staying entirely inside the non-playable wall surfaces.
            int verticalStrokeCount = Mathf.Max(4, Mathf.FloorToInt(roomHeight / 0.8f));
            for (int stroke = 0; stroke < verticalStrokeCount; stroke++)
            {
                float t = (stroke + 0.5f) / verticalStrokeCount;
                float y = Mathf.Lerp(-halfHeight + 0.45f, halfHeight - 0.45f, t);
                Color color = crayons[(stroke + roomIndex + decorationTheme) % crayons.Length];
                StageEscortController.AddLine(frameRoot.transform,
                    new Vector2(-halfWidth - 0.22f, y - 0.16f),
                    new Vector2(-halfWidth + 0.22f, y + 0.16f), 0.075f, color, 36);
                StageEscortController.AddLine(frameRoot.transform,
                    new Vector2(halfWidth - 0.22f, y - 0.16f),
                    new Vector2(halfWidth + 0.22f, y + 0.16f), 0.075f,
                    crayons[(stroke + roomIndex + decorationTheme + 2) % crayons.Length], 36);
            }

            int topStrokeCount = Mathf.Max(5, Mathf.FloorToInt(roomWidth / 0.75f));
            for (int stroke = 0; stroke < topStrokeCount; stroke++)
            {
                float t = (stroke + 0.5f) / topStrokeCount;
                float x = Mathf.Lerp(-halfWidth + 0.42f, halfWidth - 0.42f, t);
                StageEscortController.AddLine(frameRoot.transform,
                    new Vector2(x - 0.18f, halfHeight - 0.2f),
                    new Vector2(x + 0.18f, halfHeight + 0.2f), 0.075f,
                    crayons[(stroke + roomIndex + decorationTheme + 1) % crayons.Length], 36);
            }

            AddReadyRoomTheme(
                frameRoot.transform,
                halfWidth,
                halfHeight,
                crayons,
                roomIndex,
                decorationTheme);
        }

        private void CreateGlobalReadyRoomBackdrop(int theme)
        {
            Transform backdrop = new GameObject(
                "Ready Room Global Background Theme " + (theme + 1)).transform;
            backdrop.SetParent(transform, false);

            UnityEngine.Rendering.SortingGroup group =
                backdrop.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = -70;

            StageEscortController.AddFilledRect(
                backdrop,
                "Full Screen Theme Color",
                Vector2.zero,
                new Vector2(52f, 30f),
                GetGlobalThemeColor(theme),
                0);

            bool darkTheme = theme == 2 || theme == 7 || theme == 8;
            Color paperLine = darkTheme
                ? new Color(0.62f, 0.74f, 1f, 0.1f)
                : new Color(0.12f, 0.34f, 0.48f, 0.075f);
            for (int lineIndex = 0; lineIndex < 11; lineIndex++)
            {
                float y = -12.5f + lineIndex * 2.5f;
                float wobble = Mathf.Sin(lineIndex * 1.73f + theme * 0.61f) * 0.34f;
                StageEscortController.AddLine(
                    backdrop,
                    new Vector2(-26f, y - wobble),
                    new Vector2(26f, y + wobble),
                    0.035f,
                    paperLine,
                    1);
            }

            switch (theme)
            {
                case 0: // Party
                    AddGlobalThemeSprite(backdrop, "confetti", new Vector2(0f, 1.2f), 8.5f, 0f, 0.33f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(-9.5f, 2.6f), 5.8f, -8f, 0.42f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(10.2f, -2.8f), 4.6f, 12f, 0.4f);
                    break;
                case 1: // Balloons
                    AddGlobalThemeSprite(backdrop, "balloon", new Vector2(-10.5f, -1.7f), 7.2f, -8f, 0.48f);
                    AddGlobalThemeSprite(backdrop, "balloon", new Vector2(10.2f, 1.8f), 6.5f, 9f, 0.43f, true);
                    AddGlobalThemeSprite(backdrop, "confetti", new Vector2(0f, 4.8f), 5.2f, 0f, 0.28f);
                    break;
                case 2: // Starry night
                    AddGlobalThemeSprite(backdrop, "moon", new Vector2(-9.6f, 2.2f), 7.4f, -8f, 0.64f);
                    AddGlobalThemeSprite(backdrop, "cloud", new Vector2(8.8f, -3.8f), 4.2f, 0f, 0.28f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(8.8f, 3.8f), 3.4f, 10f, 0.58f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(1.5f, -4.8f), 1.9f, -8f, 0.42f);
                    break;
                case 3: // Rainbow sky
                    AddGlobalThemeSprite(backdrop, "rainbow", new Vector2(0f, 3.1f), 10.5f, 0f, 0.38f);
                    AddGlobalThemeSprite(backdrop, "cloud", new Vector2(-10.8f, -2.2f), 5.2f, 0f, 0.4f);
                    AddGlobalThemeSprite(backdrop, "cloud", new Vector2(10.5f, -1.2f), 4.6f, 0f, 0.36f, true);
                    break;
                case 4: // Aquarium
                    AddGlobalThemeSprite(backdrop, "fish", new Vector2(-9.5f, -1.2f), 6.8f, -6f, 0.52f);
                    AddGlobalThemeSprite(backdrop, "fish", new Vector2(9.7f, 2.3f), 5.5f, 7f, 0.46f, true);
                    AddGlobalThemeSprite(backdrop, "bubbles", new Vector2(1.2f, 1.4f), 7.8f, 0f, 0.34f);
                    break;
                case 5: // Flower garden
                    AddGlobalThemeSprite(backdrop, "sunflower", new Vector2(-10.2f, -2.4f), 7.2f, -4f, 0.5f);
                    AddGlobalThemeSprite(backdrop, "flower", new Vector2(9.7f, -2.7f), 6.4f, 5f, 0.43f);
                    AddGlobalThemeSprite(backdrop, "butterfly", new Vector2(7.8f, 4.5f), 3.2f, -12f, 0.44f);
                    break;
                case 6: // Music
                    AddGlobalThemeSprite(backdrop, "microphone", new Vector2(-10.4f, -1.3f), 6.6f, -16f, 0.46f);
                    AddGlobalThemeSprite(backdrop, "microphone", new Vector2(10.2f, 2.1f), 5.6f, 15f, 0.34f, true);
                    AddThemeMusicNote(backdrop, new Vector2(-3.8f, 4.2f), 2.1f,
                        new Color(0.94f, 0.38f, 0.68f, 0.48f), true);
                    AddThemeMusicNote(backdrop, new Vector2(5f, -4.2f), 1.8f,
                        new Color(0.24f, 0.58f, 0.94f, 0.46f), false);
                    break;
                case 7: // Space
                    AddGlobalSaturn(backdrop, new Vector2(-8.4f, -0.8f), 3.45f, -7f);
                    AddGlobalThemeSprite(backdrop, "rocket", new Vector2(9.3f, -3.1f), 5.2f, 18f, 0.52f);
                    AddGlobalComet(backdrop, new Vector2(8.8f, 4.2f), 3.8f, -12f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(1.2f, 4.8f), 1.8f, 8f, 0.6f);
                    AddGlobalThemeSprite(backdrop, "star", new Vector2(4.5f, -4.9f), 1.25f, -12f, 0.5f);
                    break;
                case 8: // Lightning arcade
                    AddGlobalThemeSprite(backdrop, "lightning", new Vector2(-10.3f, 0.8f), 7.2f, -8f, 0.58f);
                    AddGlobalThemeSprite(backdrop, "crown", new Vector2(0f, 4.6f), 4.6f, 0f, 0.52f);
                    AddGlobalThemeSprite(backdrop, "lightning", new Vector2(10.2f, -1.1f), 6.4f, 10f, 0.52f, true);
                    AddGlobalThemeSprite(backdrop, "gear", new Vector2(6.2f, 4f), 2.8f, 0f, 0.4f);
                    break;
                default: // Art desk
                    AddGlobalThemeSprite(backdrop, "palette", new Vector2(-9.5f, 0.2f), 7.4f, -8f, 0.48f);
                    AddGlobalThemeSprite(backdrop, "pencil", new Vector2(9.8f, 1.7f), 7.5f, 18f, 0.5f);
                    AddGlobalThemeSprite(backdrop, "paintbrush", new Vector2(6.8f, -4.4f), 5f, -28f, 0.42f);
                    AddGlobalThemeSprite(backdrop, "crayon", new Vector2(-3.8f, 4.7f), 3.3f, -20f, 0.38f);
                    break;
            }

            ReadyRoomGlobalBackdropFollower follower =
                backdrop.gameObject.AddComponent<ReadyRoomGlobalBackdropFollower>();
            follower.Configure(Camera.main, 8f);
        }

        private static Color GetGlobalThemeColor(int theme)
        {
            switch (theme)
            {
                case 0: return new Color(0.97f, 0.86f, 0.62f, 0.92f);
                case 1: return new Color(0.96f, 0.78f, 0.86f, 0.92f);
                case 2: return new Color(0.16f, 0.2f, 0.39f, 0.94f);
                case 3: return new Color(0.66f, 0.84f, 0.92f, 0.92f);
                case 4: return new Color(0.38f, 0.7f, 0.75f, 0.93f);
                case 5: return new Color(0.68f, 0.84f, 0.58f, 0.92f);
                case 6: return new Color(0.76f, 0.65f, 0.86f, 0.92f);
                case 7: return new Color(0.045f, 0.06f, 0.14f, 0.96f);
                case 8: return new Color(0.19f, 0.12f, 0.3f, 0.94f);
                default: return new Color(0.91f, 0.72f, 0.53f, 0.92f);
            }
        }

        private static void AddGlobalThemeSprite(
            Transform parent,
            string resourceName,
            Vector2 position,
            float targetHeight,
            float rotation,
            float alpha,
            bool flipX = false)
        {
            Sprite sprite = Resources.Load<Sprite>(
                "StageDecorations/CrayonSet/" + resourceName);
            if (sprite == null || sprite.bounds.size.y <= 0f) return;

            GameObject visual = new GameObject("Global Theme " + resourceName);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(position.x, position.y, -0.03f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            float scale = Mathf.Max(0.01f, targetHeight) / sprite.bounds.size.y;
            visual.transform.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            renderer.sortingOrder = 3;
        }

        private static void AddGlobalSaturn(
            Transform parent,
            Vector2 position,
            float radius,
            float rotation)
        {
            Transform saturn = new GameObject("Global Theme Hand Drawn Saturn").transform;
            saturn.SetParent(parent, false);
            saturn.localPosition = position;
            saturn.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Color body = new Color(0.69f, 0.48f, 0.9f, 0.7f);
            Color ring = new Color(1f, 0.7f, 0.24f, 0.78f);
            Color pencil = new Color(0.88f, 0.72f, 1f, 0.48f);
            AddThemePlanet(saturn, Vector2.zero, radius, body, ring);
            for (int stroke = -3; stroke <= 3; stroke++)
            {
                float y = stroke * radius * 0.18f;
                float normalized = Mathf.Clamp01(1f - y * y / (radius * radius));
                float halfWidth = radius * Mathf.Sqrt(normalized) * 0.72f;
                StageEscortController.AddLine(
                    saturn,
                    new Vector2(-halfWidth, y - 0.05f * (stroke & 1)),
                    new Vector2(halfWidth, y + 0.04f * ((stroke + 1) & 1)),
                    0.075f,
                    pencil,
                    5);
            }
        }

        private static void AddGlobalComet(
            Transform parent,
            Vector2 position,
            float scale,
            float rotation)
        {
            Transform comet = new GameObject("Global Theme Hand Drawn Comet").transform;
            comet.SetParent(parent, false);
            comet.localPosition = position;
            comet.localRotation = Quaternion.Euler(0f, 0f, rotation);
            comet.localScale = Vector3.one * scale;
            AddThemeComet(comet, Vector2.zero, new Color(0.52f, 0.78f, 1f, 0.62f));
        }

        private static void AddReadyRoomBackdrop(
            Transform parent,
            float halfWidth,
            float halfHeight,
            int roomIndex,
            int theme)
        {
            Transform backdrop = new GameObject("Ready Room Theme Backdrop " + (theme + 1)).transform;
            backdrop.SetParent(parent, false);

            Color[][] palettes =
            {
                new[] { new Color(1f, 0.72f, 0.12f, 0.13f), new Color(0.96f, 0.28f, 0.3f, 0.12f), new Color(0.18f, 0.68f, 0.9f, 0.12f) },
                new[] { new Color(0.96f, 0.35f, 0.58f, 0.13f), new Color(0.28f, 0.72f, 0.92f, 0.12f), new Color(0.92f, 0.66f, 0.16f, 0.12f) },
                new[] { new Color(0.2f, 0.38f, 0.78f, 0.13f), new Color(0.62f, 0.42f, 0.86f, 0.12f), new Color(0.98f, 0.78f, 0.22f, 0.13f) },
                new[] { new Color(0.18f, 0.7f, 0.9f, 0.12f), new Color(0.94f, 0.34f, 0.45f, 0.11f), new Color(0.35f, 0.72f, 0.38f, 0.11f) },
                new[] { new Color(0.1f, 0.62f, 0.82f, 0.13f), new Color(0.2f, 0.76f, 0.66f, 0.11f), new Color(0.92f, 0.48f, 0.24f, 0.12f) },
                new[] { new Color(0.16f, 0.64f, 0.32f, 0.13f), new Color(0.92f, 0.38f, 0.58f, 0.12f), new Color(0.96f, 0.7f, 0.18f, 0.12f) },
                new[] { new Color(0.58f, 0.3f, 0.82f, 0.13f), new Color(0.18f, 0.58f, 0.9f, 0.12f), new Color(0.94f, 0.38f, 0.54f, 0.12f) },
                new[] { new Color(0.3f, 0.3f, 0.76f, 0.13f), new Color(0.72f, 0.38f, 0.88f, 0.12f), new Color(0.98f, 0.68f, 0.14f, 0.13f) },
                new[] { new Color(0.98f, 0.66f, 0.1f, 0.13f), new Color(0.22f, 0.64f, 0.9f, 0.12f), new Color(0.92f, 0.28f, 0.34f, 0.12f) },
                new[] { new Color(0.94f, 0.46f, 0.18f, 0.12f), new Color(0.2f, 0.68f, 0.88f, 0.12f), new Color(0.66f, 0.36f, 0.84f, 0.12f) }
            };
            Color[] colors = palettes[Mathf.Clamp(theme, 0, palettes.Length - 1)];
            float motifAlpha = theme == 7 ? 0.38f : theme == 2 || theme == 8 ? 0.32f : 0.25f;
            for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
            {
                Color color = colors[colorIndex];
                color.a = motifAlpha;
                colors[colorIndex] = color;
            }

            Color wash;
            if (theme == 7)
                wash = new Color(0.72f, 0.78f, 0.94f, 0.34f);
            else if (theme == 2)
                wash = new Color(0.76f, 0.8f, 0.96f, 0.29f);
            else if (theme == 8)
                wash = new Color(0.84f, 0.76f, 0.94f, 0.28f);
            else
            {
                wash = Color.Lerp(colors[0], Color.white, 0.62f);
                wash.a = 0.19f;
            }
            Vector2 innerSize = new Vector2(
                Mathf.Max(0.8f, halfWidth * 2f - RoomFrameThickness * 1.45f),
                Mathf.Max(0.8f, halfHeight * 2f - RoomFrameThickness * 1.45f));
            StageEscortController.AddFilledRect(backdrop, "Pale Theme Paper Wash",
                Vector2.zero, innerSize, wash, 2);

            float left = -halfWidth + 1.12f;
            float right = halfWidth - 1.12f;
            float upper = halfHeight - 1.2f;
            float middle = Mathf.Clamp(0.2f + (roomIndex % 2) * 0.12f,
                -halfHeight + 1.1f, halfHeight - 1.1f);
            float lower = -halfHeight + 0.88f;

            switch (theme)
            {
                case 0: // Party wall paper
                    AddThemeStar(backdrop, new Vector2(left, middle + 0.2f), 0.58f, colors[0]);
                    AddThemeStar(backdrop, new Vector2(right, middle - 0.12f), 0.46f, colors[1]);
                    AddThemeConfetti(backdrop, halfWidth - 0.2f, upper - 0.25f, colors, roomIndex);
                    break;
                case 1: // Balloon room
                    AddThemeBalloon(backdrop, new Vector2(left, middle + 0.35f), colors[0], 0.18f);
                    AddThemeBalloon(backdrop, new Vector2(0f, upper - 0.32f), colors[1], -0.12f);
                    AddThemeBalloon(backdrop, new Vector2(right, middle + 0.12f), colors[2], -0.2f);
                    break;
                case 2: // Night sky
                    AddThemeCrescent(backdrop, new Vector2(left, upper - 0.12f), colors[2]);
                    AddThemeStar(backdrop, new Vector2(0f, middle + 0.35f), 0.7f, colors[0]);
                    AddThemeStar(backdrop, new Vector2(right, upper - 0.32f), 0.38f, colors[1]);
                    break;
                case 3: // Cloud and rainbow sky
                    AddThemeRainbow(backdrop, new Vector2(0f, middle + 0.1f), colors);
                    AddThemeCloud(backdrop, new Vector2(left, upper - 0.3f), colors[0]);
                    AddThemeCloud(backdrop, new Vector2(right, middle - 0.15f), colors[1]);
                    break;
                case 4: // Aquarium
                    AddThemeFish(backdrop, new Vector2(left + 0.25f, middle + 0.2f), 0.62f, colors[2], false);
                    AddThemeFish(backdrop, new Vector2(right - 0.18f, middle - 0.35f), 0.52f, colors[0], true);
                    for (int bubble = 0; bubble < 5; bubble++)
                    {
                        float t = bubble / 4f;
                        AddThemeCircle(backdrop,
                            new Vector2(Mathf.Lerp(left, right, t), Mathf.Lerp(lower + 0.2f, upper, t)),
                            0.08f + (bubble % 3) * 0.045f, colors[1], false);
                    }
                    break;
                case 5: // Flower garden
                    AddThemeFlower(backdrop, new Vector2(left, lower + 0.35f), 0.48f, colors[1], colors[2]);
                    AddThemeFlower(backdrop, new Vector2(0f, lower + 0.22f), 0.56f, colors[2], colors[0]);
                    AddThemeFlower(backdrop, new Vector2(right, lower + 0.4f), 0.44f, colors[0], colors[1]);
                    StageEscortController.AddLine(backdrop, new Vector2(left - 0.5f, lower),
                        new Vector2(right + 0.5f, lower + 0.06f), 0.06f, colors[0], 4);
                    break;
                case 6: // Music room
                    AddThemeMusicNote(backdrop, new Vector2(left, middle), 0.92f, colors[0], true);
                    AddThemeMusicNote(backdrop, new Vector2(0.15f, upper - 0.35f), 0.72f, colors[1], false);
                    AddThemeMusicNote(backdrop, new Vector2(right, middle - 0.25f), 0.82f, colors[2], true);
                    break;
                case 7: // Space room
                    AddThemePlanet(backdrop, new Vector2(left, middle + 0.12f), 0.52f, colors[1], colors[2]);
                    AddThemeComet(backdrop, new Vector2(right, upper - 0.28f), colors[0]);
                    AddThemeStar(backdrop, new Vector2(0.25f, middle - 0.3f), 0.44f, colors[2]);
                    break;
                case 8: // Lightning arcade
                    AddThemeLightning(backdrop, new Vector2(left, middle + 0.15f), 1.05f, colors[0]);
                    AddThemeCrown(backdrop, new Vector2(0f, middle + 0.15f), 1.05f, colors[2]);
                    AddThemeLightning(backdrop, new Vector2(right, middle - 0.1f), 0.95f, colors[1]);
                    break;
                default: // Art room
                    AddThemeCrayon(backdrop, new Vector2(left, middle), 1.15f, colors[0], 18f);
                    AddThemeCrayon(backdrop, new Vector2(right, middle - 0.1f), 1.05f, colors[1], -17f);
                    AddThemeStar(backdrop, new Vector2(-0.62f, upper - 0.25f), 0.4f, colors[2]);
                    AddThemeCircle(backdrop, new Vector2(0f, middle - 0.25f), 0.34f, colors[0], false);
                    AddThemeTriangle(backdrop, new Vector2(0.72f, upper - 0.35f), 0.36f, colors[1]);
                    break;
            }

            Renderer[] renderers = backdrop.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = renderers[i].gameObject.name == "Pale Theme Paper Wash" ? 2 : 4;
        }

        private static void AddReadyRoomPennants(Transform parent, float halfWidth, float halfHeight,
            Color[] crayons, int roomIndex)
        {
            float left = -halfWidth + 1.25f;
            float right = halfWidth - 1.25f;
            float top = halfHeight - 0.52f;
            StageEscortController.AddLine(parent, new Vector2(left, top), new Vector2(0f, top - 0.12f),
                0.032f, new Color(0.22f, 0.2f, 0.17f, 0.62f), 8);
            StageEscortController.AddLine(parent, new Vector2(0f, top - 0.12f), new Vector2(right, top),
                0.032f, new Color(0.22f, 0.2f, 0.17f, 0.62f), 8);

            const int flagCount = 5;
            for (int flag = 0; flag < flagCount; flag++)
            {
                float t = (flag + 0.5f) / flagCount;
                float x = Mathf.Lerp(left, right, t);
                float lineY = top - Mathf.Sin(t * Mathf.PI) * 0.12f;
                Vector2 a = new Vector2(x - 0.18f, lineY);
                Vector2 b = new Vector2(x + 0.18f, lineY);
                Vector2 tip = new Vector2(x + Mathf.Sin(flag * 1.7f) * 0.025f, lineY - 0.42f);
                Color color = crayons[(flag + roomIndex) % crayons.Length];
                StageEscortController.AddLine(parent, a, b, 0.055f, color, 8);
                StageEscortController.AddLine(parent, b, tip, 0.055f, color, 8);
                StageEscortController.AddLine(parent, tip, a, 0.055f, color, 8);
            }
        }

        private static void AddReadyRoomSpark(Transform parent, Vector2 center, Color color)
        {
            for (int ray = 0; ray < 4; ray++)
            {
                float angle = ray * Mathf.PI * 0.5f + 0.18f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageEscortController.AddLine(parent, center + direction * 0.09f,
                    center + direction * 0.25f, 0.045f, color, 8);
            }
        }

        private static void AddReadyRoomTheme(
            Transform parent,
            float halfWidth,
            float halfHeight,
            Color[] crayons,
            int roomIndex,
            int theme)
        {
            Color a = crayons[(roomIndex + theme) % crayons.Length];
            Color b = crayons[(roomIndex + theme + 2) % crayons.Length];
            Color c = crayons[(roomIndex + theme + 4) % crayons.Length];
            float left = -halfWidth + 0.85f;
            float right = halfWidth - 0.85f;
            float top = halfHeight - 0.78f;

            switch (theme)
            {
                case 0: // Party pennants
                    AddReadyRoomPennants(parent, halfWidth, halfHeight, crayons, roomIndex);
                    AddReadyRoomSpark(parent, new Vector2(left, top), a);
                    AddReadyRoomSpark(parent, new Vector2(right, top), c);
                    break;
                case 1: // Balloons and confetti
                    AddThemeBalloon(parent, new Vector2(left, top - 0.05f), a, -0.22f);
                    AddThemeBalloon(parent, new Vector2(right, top + 0.08f), b, 0.2f);
                    AddReadyRoomSpark(parent, new Vector2(0f, top + 0.08f), c);
                    AddThemeConfetti(parent, halfWidth, top - 0.15f, crayons, roomIndex);
                    break;
                case 2: // Starry night
                    AddThemeStar(parent, new Vector2(left, top), 0.3f, a);
                    AddThemeStar(parent, new Vector2(0f, top + 0.16f), 0.38f, b);
                    AddThemeStar(parent, new Vector2(right, top - 0.06f), 0.26f, c);
                    AddThemeCrescent(parent, new Vector2(-halfWidth + 0.82f, 0.25f), a);
                    break;
                case 3: // Rainbow and clouds
                    AddThemeRainbow(parent, new Vector2(0f, top - 0.22f), crayons);
                    AddThemeCloud(parent, new Vector2(left, top - 0.05f), a);
                    AddThemeCloud(parent, new Vector2(right, top - 0.1f), b);
                    break;
                case 4: // Aquarium
                    AddThemeFish(parent, new Vector2(left + 0.18f, top), 0.38f, a, false);
                    AddThemeFish(parent, new Vector2(right - 0.2f, top - 0.16f), 0.32f, b, true);
                    for (int i = 0; i < 5; i++)
                        AddThemeCircle(parent,
                            new Vector2(Mathf.Lerp(-halfWidth + 0.55f, halfWidth - 0.55f, i / 4f), top + (i % 2) * 0.18f),
                            0.08f + (i % 3) * 0.025f, crayons[(i + roomIndex) % crayons.Length], false);
                    break;
                case 5: // Flower garden
                    AddThemeFlower(parent, new Vector2(left, top - 0.02f), 0.28f, a, b);
                    AddThemeFlower(parent, new Vector2(0f, top + 0.08f), 0.32f, b, c);
                    AddThemeFlower(parent, new Vector2(right, top - 0.04f), 0.27f, c, a);
                    StageEscortController.AddLine(parent, new Vector2(-halfWidth + 0.45f, top - 0.5f),
                        new Vector2(halfWidth - 0.45f, top - 0.42f), 0.04f,
                        new Color(0.15f, 0.58f, 0.25f, 0.5f), 8);
                    break;
                case 6: // Music
                    AddThemeMusicNote(parent, new Vector2(left, top), 0.42f, a, false);
                    AddThemeMusicNote(parent, new Vector2(0f, top + 0.1f), 0.48f, b, true);
                    AddThemeMusicNote(parent, new Vector2(right, top - 0.08f), 0.38f, c, false);
                    break;
                case 7: // Space
                    AddThemePlanet(parent, new Vector2(left, top), 0.28f, a, b);
                    AddThemeComet(parent, new Vector2(right, top + 0.05f), c);
                    AddThemeStar(parent, new Vector2(0f, top + 0.14f), 0.22f, b);
                    AddReadyRoomSpark(parent, new Vector2(halfWidth - 0.62f, 0.2f), a);
                    break;
                case 8: // Lightning arcade
                    AddThemeLightning(parent, new Vector2(left, top), 0.55f, a);
                    AddThemeCrown(parent, new Vector2(0f, top + 0.02f), 0.62f, b);
                    AddThemeLightning(parent, new Vector2(right, top), 0.55f, c);
                    break;
                default: // Art desk doodles
                    AddThemeCrayon(parent, new Vector2(left, top - 0.02f), 0.58f, a, 12f);
                    AddThemeCrayon(parent, new Vector2(right, top - 0.02f), 0.58f, b, -12f);
                    AddThemeStar(parent, new Vector2(-0.52f, top + 0.08f), 0.22f, c);
                    AddThemeCircle(parent, new Vector2(0.05f, top + 0.08f), 0.18f, a, false);
                    AddThemeTriangle(parent, new Vector2(0.62f, top + 0.08f), 0.23f, b);
                    break;
            }
        }

        private static void AddThemeBalloon(Transform parent, Vector2 center, Color color, float stringLean)
        {
            AddThemeCircle(parent, center, 0.3f, color, true);
            StageEscortController.AddLine(parent, center + Vector2.down * 0.3f,
                center + new Vector2(stringLean, -0.82f), 0.035f, color, 8);
        }

        private static void AddThemeConfetti(Transform parent, float halfWidth, float y, Color[] colors, int offset)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-halfWidth + 1.35f, halfWidth - 1.35f, i / 6f);
                Vector2 from = new Vector2(x, y + Mathf.Sin(i * 1.7f) * 0.13f);
                StageEscortController.AddLine(parent, from, from + new Vector2(0.12f, -0.18f),
                    0.055f, colors[(i + offset) % colors.Length], 8);
            }
        }

        private static void AddThemeStar(Transform parent, Vector2 center, float radius, Color color)
        {
            Vector2[] points = new Vector2[11];
            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 0.8f;
                float r = i % 2 == 0 ? radius : radius * 0.42f;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            AddThemePolyline(parent, points, 0.045f, color);
        }

        private static void AddThemeCrescent(Transform parent, Vector2 center, Color color)
        {
            AddThemeArc(parent, center, 0.34f, -75f, 235f, color, 0.055f);
            AddThemeArc(parent, center + new Vector2(0.13f, 0.03f), 0.27f, -92f, 198f, color, 0.04f);
        }

        private static void AddThemeRainbow(Transform parent, Vector2 center, Color[] colors)
        {
            for (int i = 0; i < 4; i++)
                AddThemeArc(parent, center + Vector2.down * i * 0.04f, 0.92f - i * 0.13f,
                    15f, 165f, colors[i % colors.Length], 0.075f);
        }

        private static void AddThemeCloud(Transform parent, Vector2 center, Color color)
        {
            AddThemeCircle(parent, center + new Vector2(-0.2f, 0f), 0.19f, color, true);
            AddThemeCircle(parent, center + new Vector2(0.02f, 0.08f), 0.24f, color, true);
            AddThemeCircle(parent, center + new Vector2(0.25f, -0.01f), 0.17f, color, true);
        }

        private static void AddThemeFish(Transform parent, Vector2 center, float size, Color color, bool faceLeft)
        {
            AddThemeCircle(parent, center, size, color, false, new Vector2(1.25f, 0.7f));
            float direction = faceLeft ? -1f : 1f;
            Vector2 tailBase = center - Vector2.right * direction * size * 0.58f;
            AddThemePolyline(parent, new[]
            {
                tailBase,
                tailBase - Vector2.right * direction * size * 0.55f + Vector2.up * size * 0.42f,
                tailBase - Vector2.right * direction * size * 0.55f - Vector2.up * size * 0.42f,
                tailBase
            }, 0.045f, color);
            AddThemeCircle(parent, center + Vector2.right * direction * size * 0.32f + Vector2.up * size * 0.08f,
                size * 0.075f, color, true);
        }

        private static void AddThemeFlower(Transform parent, Vector2 center, float radius, Color petal, Color middle)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 0.4f;
                AddThemeCircle(parent, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.58f,
                    radius * 0.38f, petal, true);
            }
            AddThemeCircle(parent, center, radius * 0.3f, middle, true);
        }

        private static void AddThemeMusicNote(Transform parent, Vector2 center, float size, Color color, bool doubleNote)
        {
            AddThemeCircle(parent, center + new Vector2(-size * 0.2f, -size * 0.25f), size * 0.17f, color, true);
            StageEscortController.AddLine(parent, center + new Vector2(-size * 0.05f, -size * 0.22f),
                center + new Vector2(-size * 0.05f, size * 0.48f), 0.055f, color, 8);
            if (!doubleNote) return;
            AddThemeCircle(parent, center + new Vector2(size * 0.42f, -size * 0.18f), size * 0.17f, color, true);
            StageEscortController.AddLine(parent, center + new Vector2(size * 0.56f, -size * 0.15f),
                center + new Vector2(size * 0.56f, size * 0.4f), 0.055f, color, 8);
            StageEscortController.AddLine(parent, center + new Vector2(-size * 0.05f, size * 0.48f),
                center + new Vector2(size * 0.56f, size * 0.4f), 0.055f, color, 8);
        }

        private static void AddThemePlanet(Transform parent, Vector2 center, float radius, Color planet, Color ring)
        {
            AddThemeCircle(parent, center, radius, planet, true);
            AddThemeArc(parent, center, radius * 1.55f, 195f, 345f, ring, 0.055f, 0.38f);
            AddThemeArc(parent, center, radius * 1.55f, 15f, 165f, ring, 0.055f, 0.38f);
        }

        private static void AddThemeComet(Transform parent, Vector2 center, Color color)
        {
            AddThemeCircle(parent, center, 0.2f, color, true);
            StageEscortController.AddLine(parent, center + new Vector2(-0.18f, -0.02f),
                center + new Vector2(-0.72f, 0.22f), 0.07f, color, 8);
            StageEscortController.AddLine(parent, center + new Vector2(-0.18f, -0.08f),
                center + new Vector2(-0.62f, -0.22f), 0.04f, color, 8);
        }

        private static void AddThemeLightning(Transform parent, Vector2 center, float size, Color color)
        {
            AddThemePolyline(parent, new[]
            {
                center + new Vector2(-0.12f, size * 0.5f),
                center + new Vector2(0.14f, size * 0.12f),
                center + new Vector2(-0.02f, size * 0.08f),
                center + new Vector2(0.12f, -size * 0.5f),
                center + new Vector2(-0.2f, -size * 0.02f),
                center + new Vector2(-0.04f, size * 0.02f)
            }, 0.075f, color);
        }

        private static void AddThemeCrown(Transform parent, Vector2 center, float size, Color color)
        {
            AddThemePolyline(parent, new[]
            {
                center + new Vector2(-size * 0.5f, -size * 0.25f),
                center + new Vector2(-size * 0.42f, size * 0.35f),
                center + new Vector2(-size * 0.12f, size * 0.02f),
                center + new Vector2(0f, size * 0.42f),
                center + new Vector2(size * 0.14f, size * 0.02f),
                center + new Vector2(size * 0.44f, size * 0.35f),
                center + new Vector2(size * 0.5f, -size * 0.25f),
                center + new Vector2(-size * 0.5f, -size * 0.25f)
            }, 0.06f, color);
        }

        private static void AddThemeCrayon(Transform parent, Vector2 center, float height, Color color, float angle)
        {
            Transform root = new GameObject("Ready Room Crayon Decoration").transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            root.localRotation = Quaternion.Euler(0f, 0f, angle);
            AddThemePolyline(root, new[]
            {
                new Vector2(-0.12f, -height * 0.5f), new Vector2(-0.12f, height * 0.25f),
                new Vector2(0f, height * 0.5f), new Vector2(0.12f, height * 0.25f),
                new Vector2(0.12f, -height * 0.5f), new Vector2(-0.12f, -height * 0.5f)
            }, 0.045f, color);
        }

        private static void AddThemeTriangle(Transform parent, Vector2 center, float size, Color color)
        {
            AddThemePolyline(parent, new[]
            {
                center + Vector2.up * size,
                center + new Vector2(size, -size * 0.72f),
                center + new Vector2(-size, -size * 0.72f),
                center + Vector2.up * size
            }, 0.045f, color);
        }

        private static void AddThemeCircle(
            Transform parent,
            Vector2 center,
            float radius,
            Color color,
            bool filled,
            Vector2 aspect = default)
        {
            Transform root = new GameObject("Ready Room Doodle Circle").transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            Vector2 shape = aspect == default ? Vector2.one : aspect;
            if (filled)
                AddButtonOval(root, "Crayon Fill", Vector2.zero,
                    new Vector2(radius * 2f * shape.x, radius * 2f * shape.y), color, 8);
            const int segments = 18;
            Vector2 previous = new Vector2(radius * shape.x, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2 next = new Vector2(Mathf.Cos(angle) * radius * shape.x,
                    Mathf.Sin(angle) * radius * shape.y);
                StageEscortController.AddLine(root, previous, next, 0.035f, color, 9);
                previous = next;
            }
        }

        private static void AddThemeArc(
            Transform parent,
            Vector2 center,
            float radius,
            float fromDegrees,
            float toDegrees,
            Color color,
            float width,
            float aspectY = 1f)
        {
            const int segments = 12;
            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * aspectY);
            }
            AddThemePolyline(parent, points, width, color);
        }

        private static void AddThemePolyline(Transform parent, Vector2[] points, float width, Color color)
        {
            for (int i = 1; i < points.Length; i++)
                StageEscortController.AddLine(parent, points[i - 1], points[i], width, color, 8);
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

            const float wallInset = 0.48f;
            const float bodyMargin = 0.12f;
            float interiorWidth = roomWidth - (wallInset + bodyMargin) * 2f;
            float interiorHeight = roomHeight - (wallInset + bodyMargin) * 2f;
            if (bounds.size.x > interiorWidth || bounds.size.y > interiorHeight) return false;

            Vector2 center = rooms[room].Center;
            float leftInside = center.x - roomWidth * 0.5f + wallInset + bodyMargin;
            float floorInside = center.y - roomHeight * 0.5f + 0.36f + bodyMargin;
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
            float nextRoomWidth = Mathf.Max(MinimumRoomWidth, nextMaximumWidth + RoomBodyHorizontalPadding);
            float nextRoomHeight = Mathf.Max(MinimumRoomHeight, nextMaximumHeight + RoomBodyVerticalPadding);
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
            if (IsOnline && HasAuthority) BroadcastSnapshot(false);
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
                float floorTop = rooms[room].Center.y - roomHeight * 0.5f + 0.36f;
                float desiredBodyCenterX = rooms[room].Center.x - roomWidth * 0.5f
                    + bounds.size.x * 0.5f + 0.55f;
                destination.x += desiredBodyCenterX - bounds.center.x;
                destination.y += floorTop + 0.06f - bounds.min.y;
                if (body != null) body.position = destination;
                player.transform.position = destination;
            }
            player.ResetMotion();
            bool isControlledPlayer = stageManager != null
                && player.transform == stageManager.ActivePlayerTransform;
            player.SetControlsEnabled(isControlledPlayer);
            Physics2D.SyncTransforms();
        }

        private bool IsLocalPlayerOnAssignedButton()
        {
            PlayerController2D local = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            int room = expectedIds.IndexOf(localId);
            return local != null && room >= 0 && room < rooms.Count && IsPlayerPressingRoomButton(local, room);
        }

        private void AutoPlaceLocalPlayerOnReadyButton()
        {
            PlayerController2D local = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            int room = expectedIds.IndexOf(localId);
            if (local == null || room < 0 || room >= rooms.Count
                || IsPlayerPressingRoomButton(local, room)) return;

            Vector3 destination = rooms[room].ButtonCenter;
            destination.z = local.transform.position.z;
            float capTop = rooms[room].ButtonCollider != null
                ? rooms[room].ButtonCollider.bounds.max.y
                : rooms[room].ButtonCenter.y + 0.72f;
            if (TryGetSolidBounds(local, out Bounds bounds))
            {
                destination.x += local.transform.position.x - bounds.center.x;
                destination.y = capTop + 0.04f + local.transform.position.y - bounds.min.y;
            }
            else
            {
                destination.y = capTop + 0.6f;
            }

            Rigidbody2D body = local.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;
                body.position = destination;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            local.transform.position = destination;
            local.ResetMotion();
            Physics2D.SyncTransforms();
        }

        private void RefreshOfflineReadyState()
        {
            readyIds.Clear();
            for (int i = 0; i < offlinePlayers.Count && i < rooms.Count; i++)
            {
                PlayerController2D player = offlinePlayers[i];
                string id = "offline-" + i;
                bool overlappingButton = player != null && IsPlayerPressingRoomButton(player, i);
                if (!overlappingButton)
                {
                    buttonArmedIds.Add(id);
                    fitRejectedIds.Remove(id);
                }
                if (buttonArmedIds.Contains(id) && overlappingButton && !fitRejectedIds.Contains(id)) readyIds.Add(id);
            }
        }

        private void RefreshAuthoritativeOnlineReadyState()
        {
            bool changed = false;
            for (int i = 0; i < expectedIds.Count; i++)
            {
                string playerId = expectedIds[i];
                PlayerController2D onlinePlayer = stageManager.GetOnlinePlayerController(playerId);
                bool ready = onlinePlayer != null
                    && i < rooms.Count
                    && IsPlayerPressingRoomButton(onlinePlayer, i);
                if (!ready) fitRejectedIds.Remove(playerId);
                if (ready)
                {
                    if (!fitRejectedIds.Contains(playerId)) changed |= readyIds.Add(playerId);
                    else changed |= readyIds.Remove(playerId);
                }
                else
                {
                    changed |= readyIds.Remove(playerId);
                }
            }

            if (changed)
            {
                BroadcastSnapshot(false);
            }
        }

        private bool IsPlayerPressingRoomButton(PlayerController2D player, int room)
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            Collider2D buttonCollider = rooms[room].ButtonCollider;
            if (buttonCollider != null && buttonCollider.enabled)
            {
                Bounds contactBounds = buttonCollider.bounds;
                contactBounds.Expand(new Vector3(0.12f, 0.16f, 0f));
                Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < playerColliders.Length; i++)
                {
                    Collider2D playerCollider = playerColliders[i];
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;
                    if (buttonCollider.IsTouching(playerCollider)
                        || contactBounds.Intersects(playerCollider.bounds)) return true;
                }
            }

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
            if (!ready) fitRejectedIds.Remove(playerId);
            if (ready && fitRejectedIds.Contains(playerId)) return;
            if (ready) readyIds.Add(playerId);
            else readyIds.Remove(playerId);
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
                bool ready = readyIds.Contains(id)
                    || IsOnline && !HasAuthority && id == localId && lastLocalReady;
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
                string description = LocalizationManager.T(GetGameDescriptionKey());
                string clearConditionKey = GetClearConditionKey();
                if (!string.IsNullOrEmpty(clearConditionKey))
                {
                    description += "\n" + LocalizationManager.T(clearConditionKey);
                    descriptionText.transform.localPosition = new Vector3(0f, 0.54f, -0.03f);
                    descriptionText.characterSize = stageId == "14-3" ? 0.088f : ShouldShowRecommendationMonitor() ? 0.09f : 0.105f;
                }
                else
                {
                    descriptionText.transform.localPosition = new Vector3(0f, 0.42f, -0.03f);
                    descriptionText.characterSize = ShouldShowRecommendationMonitor() ? 0.105f : 0.12f;
                }
                descriptionText.text = description;
            }
            if (recommendationTitleText != null)
            {
                recommendationTitleText.text = LocalizationManager.T("ready_room_recommended");
            }
            if (recommendationNoneText != null)
            {
                recommendationNoneText.text = LocalizationManager.T("ready_room_recommended_none");
            }
        }

        private string GetGameDescriptionKey()
        {
            return "ready_room_game_" + (string.IsNullOrEmpty(stageId)
                ? "default"
                : stageId.Replace('-', '_'));
        }

        private string GetClearConditionKey()
        {
            switch (stageId)
            {
                case "6-2":
                case "8-1":
                case "9-1":
                case "11-2":
                case "14-3":
                    return "ready_room_clear_one_survivor";
                case "10-1":
                case "11-1":
                    return "ready_room_clear_one_goal";
                default:
                    return string.Empty;
            }
        }

        private void SendReadyRequest(bool ready)
        {
            if (onlineManager == null || string.IsNullOrEmpty(localId)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = NetworkKind,
                Json = JsonUtility.ToJson(new ReadyMessage
                {
                    Ready = ready,
                    RoomWidth = roomWidth,
                    RoomHeight = roomHeight
                })
            });
        }

        private void BroadcastSnapshot(bool launch, string fitRejectedId = null)
        {
            if (!IsOnline || onlineManager == null) return;
            string[] ids = new string[readyIds.Count];
            readyIds.CopyTo(ids);
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = NetworkKind,
                Json = JsonUtility.ToJson(new ReadyMessage
                {
                    ReadyIds = ids,
                    Launch = launch,
                    FitRejectedId = fitRejectedId,
                    RoomWidth = roomWidth,
                    RoomHeight = roomHeight
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != stageId || data.Kind != NetworkKind) return;
            ReadyMessage message = JsonUtility.FromJson<ReadyMessage>(data.Json);
            if (message == null) return;

            if (HasAuthority && message.ReadyIds == null && !string.IsNullOrEmpty(data.PlayerId))
            {
                if (message.RoomWidth > roomWidth + 0.05f
                    || message.RoomHeight > roomHeight + 0.05f)
                {
                    ExpandRoomsToFit(
                        Mathf.Max(0f, message.RoomWidth - RoomBodyHorizontalPadding),
                        Mathf.Max(0f, message.RoomHeight - RoomBodyVerticalPadding),
                        null);
                }
                SetReady(data.PlayerId, message.Ready);
                // Reply even when the ready bit did not change. This doubles as
                // a late-join/resync response for authoritative room dimensions.
                BroadcastSnapshot(false);
                return;
            }

            if (!HasAuthority && message.ReadyIds != null)
            {
                // The host owns the room dimensions. Body drawings arrive on a
                // separate channel, so clients also need the resolved size in
                // the ready-room snapshot to keep walls and buttons identical.
                if (message.RoomWidth > roomWidth + 0.05f
                    || message.RoomHeight > roomHeight + 0.05f)
                {
                    ExpandRoomsToFit(
                        Mathf.Max(0f, message.RoomWidth - RoomBodyHorizontalPadding),
                        Mathf.Max(0f, message.RoomHeight - RoomBodyVerticalPadding),
                        null);
                }
                readyIds.Clear();
                for (int i = 0; i < message.ReadyIds.Length; i++)
                    if (!string.IsNullOrEmpty(message.ReadyIds[i])) readyIds.Add(message.ReadyIds[i]);
            }
            if (!string.IsNullOrEmpty(message.FitRejectedId)
                && message.FitRejectedId == localId)
            {
                fitRejectedIds.Add(localId);
                lastLocalReady = false;
                PlayerController2D localPlayer = stageManager.ActivePlayerTransform != null
                    ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                    : null;
                stageManager.HandleChallengeStartFitRejected(localPlayer);
            }
            if (message.Launch) Launch();
        }

        private void LaunchForEveryone()
        {
            if (!TryValidateAllStageStarts(out string rejectedId, out PlayerController2D rejectedPlayer))
            {
                readyIds.Remove(rejectedId);
                buttonArmedIds.Remove(rejectedId);
                fitRejectedIds.Add(rejectedId);
                BroadcastSnapshot(false, rejectedId);
                stageManager.HandleChallengeStartFitRejected(rejectedPlayer);
                RefreshPresentation();
                return;
            }
            BroadcastSnapshot(true);
            Launch();
        }

        internal bool TryValidatePlayerStageFit(PlayerController2D targetPlayer, out Vector3 safePosition)
        {
            safePosition = targetPlayer != null ? targetPlayer.transform.position : Vector3.zero;
            if (targetPlayer == null || !returnPositions.TryGetValue(targetPlayer, out Vector3 preferred)) return true;
            bool stageWasActive = suspendedStageRoot != null && suspendedStageRoot.gameObject.activeSelf;
            if (suspendedStageRoot != null && !stageWasActive) suspendedStageRoot.gameObject.SetActive(true);
            bool fits = stageManager.TryResolveChallengeStartPosition(targetPlayer, preferred, out safePosition);
            if (suspendedStageRoot != null && !stageWasActive) suspendedStageRoot.gameObject.SetActive(false);
            if (fits) returnPositions[targetPlayer] = safePosition;
            return fits;
        }

        private bool TryValidateAllStageStarts(out string rejectedId, out PlayerController2D rejectedPlayer)
        {
            rejectedId = string.Empty;
            rejectedPlayer = null;
            List<KeyValuePair<PlayerController2D, Vector3>> entries =
                new List<KeyValuePair<PlayerController2D, Vector3>>(returnPositions);
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerController2D candidate = entries[i].Key;
                if (candidate == null) continue;
                if (TryValidatePlayerStageFit(candidate, out _)) continue;
                rejectedPlayer = candidate;
                rejectedId = ResolveReadyId(candidate);
                return false;
            }
            return true;
        }

        private string ResolveReadyId(PlayerController2D targetPlayer)
        {
            if (IsOnline) return stageManager.GetOnlinePlayerId(targetPlayer);
            int index = offlinePlayers.IndexOf(targetPlayer);
            return index >= 0 ? "offline-" + index : string.Empty;
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
                Vector3 destination = entry.Value;
                stageManager.TryResolveChallengeStartPosition(player, entry.Value, out destination);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.simulated = true;
                    body.position = destination;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                player.transform.position = destination;
                player.ResetMotion();
                bool isControlledPlayer = stageManager != null
                    && player.transform == stageManager.ActivePlayerTransform;
                player.SetControlsEnabled(isControlledPlayer);
                stageManager.RecordAssignedPlayerStart(player, destination);
            }
            Physics2D.SyncTransforms();
            GameSfx.Play(SfxId.StageCountdownGo);
            stageManager.CompleteChallengeReadyRoom();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Keeps the decorative ready-room backdrop larger than the active camera.
    /// It is presentation-only and deliberately has no collider or network state.
    /// </summary>
    [DefaultExecutionOrder(200)]
    internal sealed class ReadyRoomGlobalBackdropFollower : MonoBehaviour
    {
        private Camera targetCamera;
        private float referenceOrthographicSize = 8f;

        public void Configure(Camera camera, float referenceSize)
        {
            targetCamera = camera;
            referenceOrthographicSize = Mathf.Max(0.1f, referenceSize);
            SyncToCamera();
        }

        private void LateUpdate()
        {
            SyncToCamera();
        }

        private void SyncToCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0.5f);
            float scale = targetCamera.orthographic
                ? Mathf.Max(0.5f, targetCamera.orthographicSize / referenceOrthographicSize)
                : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
