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
        private const float ReadyStateResendInterval = 0.5f;

        [System.Serializable]
        private sealed class ReadyMessage
        {
            public bool Ready;
            public bool Launch;
            public string[] ReadyIds;
            public string FitRejectedId;
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
        private TextMesh restrictionNoteText;
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
            if (IsOnline && !HasAuthority) RequestHostSessionState();
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
                CreateTerrain("Floor", center + Vector2.down * roomHeight * 0.5f, new Vector2(roomWidth, 0.72f));
                CreateTerrain("Ceiling", center + Vector2.up * roomHeight * 0.5f, new Vector2(roomWidth, 0.72f));
                CreateTerrain("Left Wall", center + Vector2.left * roomWidth * 0.5f, new Vector2(0.72f, roomHeight));
                CreateTerrain("Right Wall", center + Vector2.right * roomWidth * 0.5f, new Vector2(0.72f, roomHeight));

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
            float descriptionWidth = showRecommendations ? 12f : spaciousDescription ? 18.5f : 16.5f;
            float descriptionX = showRecommendations ? -2.8f : 0f;
            GameObject monitor = new GameObject("Ready Room Game Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(descriptionX, monitorY, 0.25f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(descriptionWidth, spaciousDescription ? 3.8f : 3.3f), 55);
            descriptionText = StageEscortController.CreateText(monitor.transform, "Game Description",
                new Vector3(0f, 0.43f, -0.03f), 58, spaciousDescription ? 0.09f : showRecommendations ? 0.105f : 0.12f,
                new Color(0.04f, 0.34f, 0.5f), 61);
            statusText = StageEscortController.CreateText(monitor.transform, "Status",
                new Vector3(0f, -0.72f, -0.03f), 64, 0.145f,
                new Color(0.04f, 0.43f, 0.58f), 61);
            restrictionNoteText = StageEscortController.CreateText(monitor.transform, "After Start Restriction Note",
                new Vector3(descriptionWidth * 0.5f - 0.42f, spaciousDescription ? -1.48f : -1.25f, -0.03f),
                38, 0.052f, new Color(0.32f, 0.36f, 0.39f), 61);
            restrictionNoteText.anchor = TextAnchor.LowerRight;
            restrictionNoteText.alignment = TextAlignment.Right;

            if (showRecommendations)
            {
                BuildRecommendationMonitor(new Vector3(6.2f, monitorY, 0.25f), count);
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
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(5.2f, 3.3f), 55);
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
                    if (count == 1) return Recommendations((DrawManager.Species.Human, 1));
                    if (count >= 2) return Recommendations((DrawManager.Species.Human, 1), (DrawManager.Species.Slime, count - 1));
                    break;
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
                float floorTop = rooms[room].Center.y - roomHeight * 0.5f + 0.36f;
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
            bool changed = ready ? readyIds.Add(playerId) : readyIds.Remove(playerId);
            if (changed && HasAuthority) BroadcastSnapshot(false);
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
                    descriptionText.transform.localPosition = new Vector3(0f, 0.52f, -0.03f);
                    descriptionText.characterSize = stageId == "14-3" ? 0.088f : ShouldShowRecommendationMonitor() ? 0.09f : 0.105f;
                }
                else
                {
                    descriptionText.transform.localPosition = new Vector3(0f, 0.43f, -0.03f);
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
            if (restrictionNoteText != null)
            {
                restrictionNoteText.text = LocalizationManager.T(
                    stageId == "10-3"
                        ? "ready_room_restriction_redraw_allowed"
                        : "ready_room_restriction");
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
                Json = JsonUtility.ToJson(new ReadyMessage { Ready = ready })
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
                    FitRejectedId = fitRejectedId
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
                SetReady(data.PlayerId, message.Ready);
                return;
            }

            if (!HasAuthority && message.ReadyIds != null)
            {
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
                player.SetControlsEnabled(true);
                stageManager.RecordAssignedPlayerStart(player, destination);
            }
            Physics2D.SyncTransforms();
            GameSfx.Play(SfxId.StageCountdownGo);
            stageManager.CompleteChallengeReadyRoom();
            Destroy(gameObject);
        }
    }
}
