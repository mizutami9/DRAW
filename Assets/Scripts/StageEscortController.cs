using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageEscortController : MonoBehaviour
    {
        private const string KindState = "escort_state";
        private const float LowerFloorY = -4.2f;
        private const float RespawnDelay = 3f;

        [System.Serializable]
        private sealed class EscortState
        {
            public int Sequence;
            public bool Active;
            public bool Cleared;
            public Vector2 Position;
            public Vector2 Velocity;
            public float RespawnRemaining;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private StageEscortFriend friend;
        private readonly List<TextMesh> statusTitles = new List<TextMesh>();
        private readonly List<TextMesh> statusMains = new List<TextMesh>();
        private readonly List<TextMesh> statusSubs = new List<TextMesh>();
        private float respawnRemaining;
        private float nextBroadcastAt;
        private int stateSequence;
        private int lastReceivedSequence;
        private bool cleared;
        private bool built;
        private string stageId = "5-3";
        private float failureBottomY = -6.1f;
        private float failureLeftX = -16.5f;
        private Vector2 replicaTarget;
        private Vector2 friendSpawnPosition = new Vector2(-13.15f, 1.35f);
        private Vector2 escortGoalPosition = new Vector2(14.2f, 1.55f);
        private StageEscortGoalMarker escortGoalMarker;
        private static Material lineMaterial;

        public void Configure(string configuredStageId)
        {
            if (!string.IsNullOrEmpty(configuredStageId)) stageId = configuredStageId;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
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
            BuildStage();
            built = true;
            if (HasAuthority()) SpawnFriend();
            RefreshStatus();
        }

        private void Update()
        {
            if (!built || stageManager == null || stageManager.CurrentStageId != stageId || cleared) return;

            if (!HasAuthority())
            {
                UpdateReplica();
                RefreshStatus();
                return;
            }

            if (friend == null)
            {
                respawnRemaining = Mathf.Max(0f, respawnRemaining - Time.deltaTime);
                if (respawnRemaining <= 0f) SpawnFriend();
            }
            else
            {
                Vector2 position = friend.transform.position;
                if (friend.IsDefeated || position.y < failureBottomY || position.x < failureLeftX)
                {
                    RemoveFriendAndSchedule();
                }
                else if (escortGoalMarker != null
                    ? escortGoalMarker.HasReached(position)
                    : position.x >= escortGoalPosition.x - 0.15f
                        && Mathf.Abs(position.y - escortGoalPosition.y) < 2.7f)
                {
                    CompleteStage();
                }
            }

            BroadcastState();
            RefreshStatus();
        }

        private void UpdateReplica()
        {
            if (friend != null)
            {
                friend.transform.position = Vector2.Lerp(friend.transform.position, replicaTarget, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            else
            {
                respawnRemaining = Mathf.Max(0f, respawnRemaining - Time.deltaTime);
            }
        }

        private void SpawnFriend()
        {
            if (friend != null || cleared) return;
            StageEscortSpawnerMarker spawner = Object.FindFirstObjectByType<StageEscortSpawnerMarker>();
            spawner?.GetComponent<StageDropperVisualAnimator>()?.PlayDispense();
            friend = StageEscortFriend.Create(transform, friendSpawnPosition, HasAuthority(), stageId == "10-2");
            replicaTarget = friend.transform.position;
            IgnorePlayerOnlyFloors(friend);
            respawnRemaining = 0f;
            GameSfx.PlayAt(SfxId.EmotePop, friend.transform.position, 0.9f);
            BroadcastState(true);
        }

        private void RemoveFriendAndSchedule()
        {
            if (friend != null)
            {
                Vector2 defeatPosition = friend.transform.position;
                GameSfx.PlayAt(SfxId.PlayerDeath, defeatPosition, 0.72f);
                if (stageId == "10-2") StageEscortFriendDefeatEffect.Create(transform, defeatPosition);
                Destroy(friend.gameObject);
                friend = null;
            }
            respawnRemaining = RespawnDelay;
            BroadcastState(true);
        }

        private void CompleteStage()
        {
            if (cleared) return;
            cleared = true;
            if (friend != null) friend.StopWalking();
            RefreshStatus();
            BroadcastState(true);
            stageManager.ClearStage();
        }

        private void IgnorePlayerOnlyFloors(StageEscortFriend target)
        {
            if (target == null || target.Hitbox == null) return;
            StageEscortPlayerOnlyFloor[] floors = Object.FindObjectsByType<StageEscortPlayerOnlyFloor>(FindObjectsSortMode.None);
            for (int i = 0; i < floors.Length; i++)
            {
                Collider2D[] colliders = floors[i].GetComponentsInChildren<Collider2D>(true);
                for (int j = 0; j < colliders.Length; j++)
                    if (colliders[j] != null) Physics2D.IgnoreCollision(target.Hitbox, colliders[j], true);
            }
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnlineActive() || !HasAuthority() || onlineManager == null || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            EscortState state = new EscortState
            {
                Sequence = ++stateSequence,
                Active = friend != null,
                Cleared = cleared,
                Position = friend != null ? (Vector2)friend.transform.position : Vector2.zero,
                Velocity = friend != null && friend.Body != null ? friend.Body.linearVelocity : Vector2.zero,
                RespawnRemaining = respawnRemaining
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = stageId,
                Kind = KindState,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != stageId || data.Kind != KindState || HasAuthority() || !IsHost(data.PlayerId)) return;
            EscortState state = JsonUtility.FromJson<EscortState>(data.Json);
            if (state == null || state.Sequence <= lastReceivedSequence) return;
            lastReceivedSequence = state.Sequence;
            respawnRemaining = state.RespawnRemaining;
            replicaTarget = state.Position;
            if (state.Active && friend == null)
            {
                StageEscortSpawnerMarker spawner = Object.FindFirstObjectByType<StageEscortSpawnerMarker>();
                spawner?.GetComponent<StageDropperVisualAnimator>()?.PlayDispense();
                friend = StageEscortFriend.Create(transform, state.Position, false, stageId == "10-2");
                IgnorePlayerOnlyFloors(friend);
            }
            else if (!state.Active && friend != null)
            {
                if (stageId == "10-2") StageEscortFriendDefeatEffect.Create(transform, friend.transform.position);
                GameSfx.PlayAt(SfxId.PlayerDeath, friend.transform.position, 0.72f);
                Destroy(friend.gameObject);
                friend = null;
            }
            if (state.Cleared && !cleared)
            {
                cleared = true;
                if (friend != null) friend.StopWalking();
                RefreshStatus();
            }
        }

        private bool IsOnlineActive() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnlineActive() || stageManager.IsOnlineStageHost;

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        private void BuildStage()
        {
            StageEscortSpawnerMarker spawner = Object.FindFirstObjectByType<StageEscortSpawnerMarker>();
            if (spawner != null) friendSpawnPosition = spawner.SpawnPosition;
            StageEscortGoalMarker goal = Object.FindFirstObjectByType<StageEscortGoalMarker>();
            if (goal != null)
            {
                escortGoalMarker = goal;
                escortGoalPosition = goal.transform.position;
            }

            failureBottomY = friendSpawnPosition.y - 7.5f;
            failureLeftX = friendSpawnPosition.x - 4f;

            GameObject runtime = new GameObject(stageId + " Escort Runtime UI");
            runtime.transform.SetParent(transform, false);
            // Stages 5-3 and 10-2 communicate through the friend, goal and hazards;
            // extra status monitors only obscure their routes.
        }

        private void CreatePlayerOnlyFloor(Transform parent)
        {
            GameObject root = new GameObject("Player And Box Floor");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector2(0f, LowerFloorY);
            root.layer = 6;
            root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(30.4f, 0.72f);
            root.AddComponent<StageEscortPlayerOnlyFloor>();
            AddFilledRect(root.transform, "Blue Floor", Vector2.zero, new Vector2(30.4f, 0.72f), new Color(0.74f, 0.9f, 1f, 1f), 12);
            AddBoxOutline(root.transform, Vector2.zero, new Vector2(30.4f, 0.72f), new Color(0.08f, 0.3f, 0.58f, 1f), 13);
            for (int i = -14; i <= 14; i += 2)
                AddLine(root.transform, new Vector2(i - 0.28f, 0.12f), new Vector2(i + 0.28f, -0.12f), 0.05f, new Color(0.12f, 0.48f, 0.78f, 0.62f), 14);
        }

        private void CreateSideWalls(Transform parent)
        {
            CreateSolidRect(parent, "Left Wall", new Vector2(-15.55f, 0.4f), new Vector2(0.65f, 9.9f));
            CreateSolidRect(parent, "Right Wall", new Vector2(15.55f, 0.4f), new Vector2(0.65f, 9.9f));
        }

        private void CreateRoute(Transform parent)
        {
            CreatePlatform(parent, "Route A", new Vector2(-12.8f, 0.45f), new Vector2(3.8f, 0.62f));
            CreatePlatform(parent, "Route B", new Vector2(-7.25f, 0.45f), new Vector2(3.5f, 0.62f));
            CreatePlatform(parent, "Route C", new Vector2(-1.7f, 0.85f), new Vector2(3.4f, 0.62f));
            CreatePlatform(parent, "Route D", new Vector2(4.35f, 0.15f), new Vector2(3.5f, 0.62f));
            CreatePlatform(parent, "Route E", new Vector2(9.35f, 0.7f), new Vector2(2.7f, 0.62f));
            CreatePlatform(parent, "Goal Route", new Vector2(13.7f, 0.4f), new Vector2(3.1f, 0.62f));
        }

        private void CreateTutorialHeadBridge(Transform parent)
        {
            GameObject dummy = new GameObject("Head Is A Bridge Tutorial");
            dummy.transform.SetParent(parent, false);
            dummy.transform.localPosition = new Vector2(-10.3f, -0.1f);
            Color red = new Color(0.92f, 0.12f, 0.1f, 1f);

            GameObject head = new GameObject("Human Head Platform");
            head.transform.SetParent(dummy.transform, false);
            head.transform.localPosition = new Vector2(0f, 0.48f);
            head.layer = 6;
            head.tag = "Ground";
            BoxCollider2D collider = head.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.25f, 0.72f);
            AddFilledRect(head.transform, "Head Paper", Vector2.zero, new Vector2(1.25f, 0.72f), new Color(1f, 0.88f, 0.7f, 1f), 28);
            AddBoxOutline(head.transform, Vector2.zero, new Vector2(1.25f, 0.72f), red, 29);
            AddDot(head.transform, new Vector2(-0.25f, 0.08f), red, 30);
            AddDot(head.transform, new Vector2(0.25f, 0.08f), red, 30);
            AddLine(head.transform, new Vector2(-0.2f, -0.15f), new Vector2(0.2f, -0.15f), 0.05f, red, 30);
            AddLine(dummy.transform, new Vector2(0f, 0.12f), new Vector2(0f, -1.7f), 0.1f, red, 27);
            AddLine(dummy.transform, new Vector2(0f, -0.55f), new Vector2(-0.75f, -0.2f), 0.08f, red, 27);
            AddLine(dummy.transform, new Vector2(0f, -0.55f), new Vector2(0.75f, -0.2f), 0.08f, red, 27);
            AddLine(dummy.transform, new Vector2(0f, -1.7f), new Vector2(-0.45f, -2.45f), 0.08f, red, 27);
            AddLine(dummy.transform, new Vector2(0f, -1.7f), new Vector2(0.45f, -2.45f), 0.08f, red, 27);

        }

        private void CreateSupplySteps(Transform parent)
        {
            CreatePlatform(parent, "Supply Step 1", new Vector2(-12.8f, -2.8f), new Vector2(2.2f, 0.48f));
            CreatePlatform(parent, "Supply Step 2", new Vector2(-10.8f, -1.75f), new Vector2(1.7f, 0.48f));
            CreatePlatform(parent, "Supply Step 3", new Vector2(-8.9f, -0.75f), new Vector2(1.5f, 0.48f));
            CreatePlatform(parent, "Middle Step", new Vector2(1.1f, -1.5f), new Vector2(2.2f, 0.48f));
            CreatePlatform(parent, "Right Step", new Vector2(7.1f, -1.25f), new Vector2(1.8f, 0.48f));
        }

        private void CreateBoxes(Transform parent)
        {
            StageObjectType[] types =
            {
                StageObjectType.WoodBox, StageObjectType.Ball, StageObjectType.TriangleBox,
                StageObjectType.Barrel, StageObjectType.IronBox, StageObjectType.RubberBox,
                StageObjectType.IceBlock, StageObjectType.Rock
            };
            float[] positions = { -7.8f, -5.9f, -4f, -2.1f, 0f, 2.2f, 4.5f, 6.7f };
            for (int i = 0; i < types.Length; i++)
            {
                StageObjectData data = StageObjectFactory.CreateDefaultData(types[i], new Vector2(positions[i], -3.05f));
                data.objectId = "escort_supply_" + i;
                float size = i % 3 == 0 ? 1.15f : i % 3 == 1 ? 0.9f : 1f;
                data.size = Vector2.one * size;
                factory?.Create(data, parent);
            }
        }

        private void CreateSpawner(Transform parent)
        {
            GameObject spawner = new GameObject("Friendly Character Spawner");
            spawner.transform.SetParent(parent, false);
            spawner.transform.localPosition = new Vector2(-13.15f, 2.5f);
            if (StageGun.TryCreateResourceSprite(
                spawner.transform,
                "StageObjects/NicoDraw/escort-spawner",
                "Colored Pencil Friend Spawner",
                new Vector2(2.2f, 1.65f),
                36))
            {
                return;
            }
            AddFilledRect(spawner.transform, "Spawner Body", Vector2.zero, new Vector2(2.2f, 1.35f), new Color(0.25f, 0.7f, 0.95f, 1f), 32);
            AddBoxOutline(spawner.transform, Vector2.zero, new Vector2(2.2f, 1.35f), new Color(0.04f, 0.22f, 0.45f, 1f), 33);
            AddFilledRect(spawner.transform, "Spawner Opening", new Vector2(0f, -0.48f), new Vector2(1.15f, 0.35f), new Color(0.025f, 0.055f, 0.08f, 1f), 34);
            AddLine(spawner.transform, new Vector2(0f, 0.42f), new Vector2(0f, -0.05f), 0.1f, Color.white, 35);
            AddLine(spawner.transform, new Vector2(-0.22f, 0.15f), new Vector2(0f, -0.08f), 0.1f, Color.white, 35);
            AddLine(spawner.transform, new Vector2(0.22f, 0.15f), new Vector2(0f, -0.08f), 0.1f, Color.white, 35);
        }

        private void CreateGoal(Transform parent)
        {
            GameObject goal = new GameObject("Friend Goal House");
            goal.transform.SetParent(parent, false);
            goal.transform.localPosition = new Vector2(14.2f, 1.55f);
            AddFilledRect(goal.transform, "Goal House", Vector2.zero, new Vector2(1.7f, 2.1f), new Color(0.45f, 0.9f, 0.55f, 1f), 30);
            AddBoxOutline(goal.transform, Vector2.zero, new Vector2(1.7f, 2.1f), new Color(0.05f, 0.38f, 0.16f, 1f), 31);
            AddFilledRect(goal.transform, "Goal Door", new Vector2(0f, -0.48f), new Vector2(0.75f, 1.1f), new Color(0.03f, 0.12f, 0.08f, 1f), 32);
            AddLine(goal.transform, new Vector2(-1.05f, 1.05f), new Vector2(0f, 1.72f), 0.13f, new Color(0.05f, 0.38f, 0.16f, 1f), 33);
            AddLine(goal.transform, new Vector2(0f, 1.72f), new Vector2(1.05f, 1.05f), 0.13f, new Color(0.05f, 0.38f, 0.16f, 1f), 33);
            if (stageId != "10-2")
            {
                TextMesh label = CreateText(goal.transform, "Goal Label", new Vector3(0f, 2.15f, -0.03f),
                    42, 0.1f, new Color(0.05f, 0.38f, 0.16f, 1f), 34);
                label.text = LocalizationManager.T("stage_goal_label");
            }
        }

        private void CreateMonitor(Transform parent, Vector2 position)
        {
            GameObject monitor = new GameObject("Escort Status Monitor");
            monitor.transform.SetParent(parent, false);
            monitor.transform.localPosition = new Vector3(position.x, position.y, 0.5f);
            DoodleMonitorVisuals.KeepBehindPlayers(monitor.transform);
            AddFilledRect(monitor.transform, "Frame", Vector2.zero, new Vector2(10.2f, 2.55f), new Color(0.18f, 0.22f, 0.27f, 0.88f), -32);
            AddFilledRect(monitor.transform, "Screen", Vector2.zero, new Vector2(9.55f, 1.95f), new Color(0.01f, 0.035f, 0.045f, 0.9f), -31);
            statusTitles.Add(CreateText(monitor.transform, "Title", new Vector3(0f, 0.68f, -0.02f), 46, 0.12f, new Color(0.55f, 0.9f, 1f, 1f), -28));
            statusMains.Add(CreateText(monitor.transform, "Main", new Vector3(0f, 0f, -0.03f), 60, 0.145f, new Color(0.2f, 1f, 0.72f, 1f), -27));
            statusSubs.Add(CreateText(monitor.transform, "Sub", new Vector3(0f, -0.68f, -0.04f), 40, 0.09f, new Color(1f, 0.84f, 0.3f, 1f), -26));
        }

        private void RefreshStatus()
        {
            if (statusTitles.Count == 0) return;
            for (int i = 0; i < statusTitles.Count; i++)
            {
                TextMesh title = statusTitles[i];
                TextMesh main = i < statusMains.Count ? statusMains[i] : null;
                TextMesh sub = i < statusSubs.Count ? statusSubs[i] : null;
                if (title == null) continue;
                title.text = cleared
                    ? LocalizationManager.T("escort_clear_title")
                    : LocalizationManager.T(stageId == "10-2" ? "escort_defense_title" : "escort_title");
                if (cleared)
                {
                    SetText(main, LocalizationManager.T("clear"), 0.17f, 1.55f);
                    SetText(sub, LocalizationManager.T("escort_clear_sub"), 0.09f, 2f);
                }
                else if (friend == null)
                {
                    SetText(main, LocalizationManager.Format("escort_respawning", respawnRemaining), 0.13f, 1.7f);
                    SetText(sub, LocalizationManager.T("escort_respawn_sub"), 0.085f, 2f);
                }
                else
                {
                    SetText(main, LocalizationManager.T("escort_friend_active"), 0.13f, 1.7f);
                    SetText(sub, LocalizationManager.T(stageId == "10-2" ? "escort_defense_instruction" : "escort_instruction"), 0.085f, 2f);
                }
            }
        }

        private void CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.layer = 6;
            root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            AddFilledRect(root.transform, "Paper Fill", Vector2.zero, size, new Color(0.93f, 0.89f, 0.77f, 1f), 12);
            AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.18f, 0.12f, 0.08f, 1f), 13);
            for (float x = -size.x * 0.42f; x < size.x * 0.42f; x += 0.45f)
                AddLine(root.transform, new Vector2(x - 0.2f, -size.y * 0.25f), new Vector2(x + 0.2f, size.y * 0.25f), 0.025f, new Color(0.3f, 0.24f, 0.16f, 0.28f), 14);
        }

        private void CreateSolidRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.layer = 6;
            root.tag = "Ground";
            root.AddComponent<BoxCollider2D>().size = size;
            AddFilledRect(root.transform, "Wall Fill", Vector2.zero, size, new Color(0.88f, 0.84f, 0.72f, 1f), 12);
            AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.18f, 0.12f, 0.08f, 1f), 13);
        }

        private static void SetText(TextMesh target, string value, float size, float fitWidth)
        {
            if (target == null) return;
            target.text = value;
            target.characterSize = Mathf.Min(size, fitWidth / Mathf.Max(1, value != null ? value.Length : 0));
        }

        internal static void AddFilledRect(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        internal static void AddBoxOutline(Transform parent, Vector2 center, Vector2 size, Color color, int order)
        {
            Vector2 half = size * 0.5f;
            Vector2 a = center + new Vector2(-half.x, -half.y);
            Vector2 b = center + new Vector2(-half.x, half.y);
            Vector2 c = center + new Vector2(half.x, half.y);
            Vector2 d = center + new Vector2(half.x, -half.y);
            AddLine(parent, a, b, 0.055f, color, order);
            AddLine(parent, b, c, 0.055f, color, order);
            AddLine(parent, c, d, 0.055f, color, order);
            AddLine(parent, d, a, 0.055f, color, order);
        }

        private static void AddDot(Transform parent, Vector2 position, Color color, int order)
        {
            GameObject obj = new GameObject("Crayon Dot");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = Vector3.one * 0.1f;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        internal static void AddLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, int order)
        {
            GameObject obj = new GameObject("Crayon Line");
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            if (lineMaterial == null) lineMaterial = DoodleRuntimeAssets.LineMaterial;
            line.sharedMaterial = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
        }

        internal static TextMesh CreateText(Transform parent, string name, Vector3 position, int fontSize, float size, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            Font font = DoodleRuntimeAssets.HandwrittenFont;
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }
    }

    public sealed class StageEscortPlayerOnlyFloor : MonoBehaviour { }

    /// <summary>
    /// A route platform reserved for the rolling escort friend. Player drawing
    /// colliders can be rebuilt at runtime, so their collision exclusions are
    /// refreshed instead of being captured only once during arena creation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageEscortFriendOnlyFloor : MonoBehaviour
    {
        private Collider2D[] floorColliders;
        private float nextRefreshAt;

        private void Awake()
        {
            floorColliders = GetComponentsInChildren<Collider2D>(true);
            IgnorePlayerColliders();
        }

        private void FixedUpdate()
        {
            if (Time.unscaledTime < nextRefreshAt) return;
            nextRefreshAt = Time.unscaledTime + 0.25f;
            IgnorePlayerColliders();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Collider2D other = collision != null ? collision.collider : null;
            if (other == null || other.GetComponentInParent<StageDrawnEscortFriend>() != null) return;
            IgnoreCollider(other);
        }

        private void IgnorePlayerColliders()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Collider2D[] playerColliders = players[i].GetComponentsInChildren<Collider2D>(false);
                for (int j = 0; j < playerColliders.Length; j++) IgnoreCollider(playerColliders[j]);
            }
        }

        private void IgnoreCollider(Collider2D other)
        {
            if (other == null || floorColliders == null) return;
            for (int i = 0; i < floorColliders.Length; i++)
            {
                Collider2D floor = floorColliders[i];
                if (floor != null && floor != other) Physics2D.IgnoreCollision(floor, other, true);
            }
        }
    }

    public sealed class StageEscortSpawnerMarker : MonoBehaviour
    {
        [SerializeField] private Vector2 localSpawnOffset = new Vector2(0f, -1.15f);
        public Vector2 SpawnPosition => transform.TransformPoint(localSpawnOffset);
        public void Configure(Vector2 offset) => localSpawnOffset = offset;
    }

    public sealed class StageEscortGoalMarker : MonoBehaviour
    {
        private Collider2D goalArea;

        private void Awake() => goalArea = GetComponent<Collider2D>();

        public bool HasReached(Vector2 position)
        {
            if (goalArea == null) goalArea = GetComponent<Collider2D>();
            return goalArea != null && goalArea.OverlapPoint(position);
        }
    }

    public static class StageEscortEditableObjectFactory
    {
        public static GameObject Create(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.type.ToString());
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            Vector2 size = new Vector2(Mathf.Max(0.5f, data.size.x), Mathf.Max(0.4f, data.size.y));

            switch (data.type)
            {
                case StageObjectType.EscortSpawner:
                    BuildSpawner(root, size);
                    break;
                case StageObjectType.EscortGoal:
                    BuildGoal(root, size, data.objectId == null || !data.objectId.StartsWith("10-2_"));
                    break;
                case StageObjectType.EscortPlayerOnlyFloor:
                    BuildPlayerOnlyFloor(root, size);
                    break;
                case StageObjectType.EscortHeadBridge:
                    BuildHeadBridge(root, size);
                    break;
            }

            AddMetadata(root, data);
            return root;
        }

        private static void BuildSpawner(GameObject root, Vector2 size)
        {
            BoxCollider2D selection = root.AddComponent<BoxCollider2D>();
            selection.size = size;
            selection.isTrigger = true;
            StageEscortSpawnerMarker marker = root.AddComponent<StageEscortSpawnerMarker>();
            marker.Configure(new Vector2(0f, -size.y * 0.85f));
            const string artworkName = "Colored Pencil Friend Spawner";
            if (StageGun.TryCreateResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/escort-spawner",
                artworkName,
                new Vector2(size.x, size.y * 1.12f),
                36))
            {
                Transform artwork = root.transform.Find(artworkName);
                StageDropperVisualAnimator animator = root.AddComponent<StageDropperVisualAnimator>();
                animator.Configure(artwork, null, Mathf.Max(0.12f, size.y * 0.16f));
                return;
            }
            Color fill = new Color(0.25f, 0.7f, 0.95f, 1f);
            Color ink = new Color(0.04f, 0.22f, 0.45f, 1f);
            StageEscortController.AddFilledRect(root.transform, "Spawner Body", Vector2.zero, size, fill, 32);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, ink, 33);
            Vector2 openingSize = new Vector2(size.x * 0.52f, size.y * 0.25f);
            StageEscortController.AddFilledRect(root.transform, "Spawner Opening", new Vector2(0f, -size.y * 0.36f), openingSize, new Color(0.025f, 0.055f, 0.08f, 1f), 34);
            StageEscortController.AddLine(root.transform, new Vector2(0f, size.y * 0.3f), new Vector2(0f, -size.y * 0.05f), 0.1f, Color.white, 35);
            StageEscortController.AddLine(root.transform, new Vector2(-size.x * 0.1f, size.y * 0.1f), new Vector2(0f, -size.y * 0.08f), 0.1f, Color.white, 35);
            StageEscortController.AddLine(root.transform, new Vector2(size.x * 0.1f, size.y * 0.1f), new Vector2(0f, -size.y * 0.08f), 0.1f, Color.white, 35);
        }

        private static void BuildGoal(GameObject root, Vector2 size, bool showLabel)
        {
            BoxCollider2D selection = root.AddComponent<BoxCollider2D>();
            selection.size = size;
            selection.isTrigger = true;
            root.AddComponent<StageEscortGoalMarker>();
            Color fill = new Color(0.45f, 0.9f, 0.55f, 1f);
            Color ink = new Color(0.05f, 0.38f, 0.16f, 1f);
            StageEscortController.AddFilledRect(root.transform, "Goal House", Vector2.zero, size, fill, 30);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, ink, 31);
            StageEscortController.AddFilledRect(root.transform, "Goal Door", new Vector2(0f, -size.y * 0.23f), new Vector2(size.x * 0.44f, size.y * 0.52f), new Color(0.03f, 0.12f, 0.08f, 1f), 32);
            StageEscortController.AddLine(root.transform, new Vector2(-size.x * 0.62f, size.y * 0.5f), new Vector2(0f, size.y * 0.82f), 0.13f, ink, 33);
            StageEscortController.AddLine(root.transform, new Vector2(0f, size.y * 0.82f), new Vector2(size.x * 0.62f, size.y * 0.5f), 0.13f, ink, 33);
            if (showLabel)
            {
                TextMesh label = StageEscortController.CreateText(root.transform, "Goal Label",
                    new Vector3(0f, size.y * 1.02f, -0.03f), 42, 0.1f, ink, 34);
                label.text = LocalizationManager.T("stage_goal_label");
            }
        }

        private static void BuildPlayerOnlyFloor(GameObject root, Vector2 size)
        {
            root.layer = 6;
            root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.usedByEffector = true;
            PlatformEffector2D effector = root.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 165f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            root.AddComponent<StageEscortPlayerOnlyFloor>();
            Color fill = new Color(0.74f, 0.9f, 1f, 1f);
            Color ink = new Color(0.08f, 0.3f, 0.58f, 1f);
            StageEscortController.AddFilledRect(root.transform, "Blue Floor", Vector2.zero, size, fill, 12);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, ink, 13);
            float spacing = Mathf.Max(0.8f, size.x / 16f);
            for (float x = -size.x * 0.46f; x <= size.x * 0.46f; x += spacing)
                StageEscortController.AddLine(root.transform, new Vector2(x - 0.24f, size.y * 0.2f), new Vector2(x + 0.24f, -size.y * 0.2f), 0.045f, new Color(0.12f, 0.48f, 0.78f, 0.62f), 14);
        }

        private static void BuildHeadBridge(GameObject root, Vector2 size)
        {
            Color red = new Color(0.92f, 0.12f, 0.1f, 1f);
            float headHeight = Mathf.Clamp(size.y * 0.24f, 0.55f, 1f);
            float headY = size.y * 0.16f;
            GameObject head = new GameObject("Human Head Platform");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector2(0f, headY);
            head.layer = 6;
            head.tag = "Ground";
            head.AddComponent<BoxCollider2D>().size = new Vector2(size.x, headHeight);
            StageEscortController.AddFilledRect(head.transform, "Head Paper", Vector2.zero, new Vector2(size.x, headHeight), new Color(1f, 0.88f, 0.7f, 1f), 28);
            StageEscortController.AddBoxOutline(head.transform, Vector2.zero, new Vector2(size.x, headHeight), red, 29);
            StageEscortController.AddLine(head.transform, new Vector2(-size.x * 0.18f, -headHeight * 0.18f), new Vector2(size.x * 0.18f, -headHeight * 0.18f), 0.05f, red, 30);
            AddDot(head.transform, new Vector2(-size.x * 0.2f, headHeight * 0.12f), red);
            AddDot(head.transform, new Vector2(size.x * 0.2f, headHeight * 0.12f), red);
            float shoulderY = headY - headHeight * 0.55f;
            float hipY = -size.y * 0.42f;
            StageEscortController.AddLine(root.transform, new Vector2(0f, shoulderY), new Vector2(0f, hipY), 0.1f, red, 27);
            StageEscortController.AddLine(root.transform, new Vector2(0f, shoulderY - 0.25f), new Vector2(-size.x * 0.62f, shoulderY - 0.05f), 0.08f, red, 27);
            StageEscortController.AddLine(root.transform, new Vector2(0f, shoulderY - 0.25f), new Vector2(size.x * 0.62f, shoulderY - 0.05f), 0.08f, red, 27);
            StageEscortController.AddLine(root.transform, new Vector2(0f, hipY), new Vector2(-size.x * 0.38f, -size.y * 0.5f), 0.08f, red, 27);
            StageEscortController.AddLine(root.transform, new Vector2(0f, hipY), new Vector2(size.x * 0.38f, -size.y * 0.5f), 0.08f, red, 27);
        }

        private static void AddDot(Transform parent, Vector2 position, Color color)
        {
            GameObject dot = new GameObject("Face Dot");
            dot.transform.SetParent(parent, false);
            dot.transform.localPosition = position;
            dot.transform.localScale = Vector3.one * 0.1f;
            SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = 30;
        }

        private static void AddMetadata(GameObject root, StageObjectData data)
        {
            StageEditorObject marker = root.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
            marker.actionStrength = data.actionStrength;
            marker.movementAngle = data.movementAngle;
            marker.movementSpeed = data.movementSpeed;
            marker.spawnPattern = data.spawnPattern;
            marker.spawnBoxSize = data.spawnBoxSize;
            marker.bombFuseSeconds = data.bombFuseSeconds;
            marker.linkTargetId = data.linkTargetId;
            marker.linkAction = data.linkAction;
        }
    }

    /// <summary>
    /// Marks a surface that deliberately slows only the escorted friend. The
    /// platform remains ordinary ground for player characters and physics props.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageEscortStickySurface : MonoBehaviour
    {
    }

    [DisallowMultipleComponent]
    public sealed class StageEscortFriend : MonoBehaviour
    {
        private const float WalkSpeed = 1.15f;
        private static readonly string[] SpeechWords = { "GO!", "HELP!", "WAIT!", "YIKES!", "RUN!" };
        private bool authoritative;
        private bool stopped;
        private bool defeated;
        private bool ignorePlayerCollisions;
        private bool defeatOnObstacleCollision;
        private bool randomSpeechEnabled;
        private float nextPlayerCollisionRefresh;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private Transform visual;
        private Transform speechBubble;
        private TextMesh speechText;
        private float walkPhase;
        private float nextSpeechAt;
        private float speechRemaining;

        public Rigidbody2D Body => body;
        public Collider2D Hitbox => hitbox;
        public bool IsDefeated => defeated;

        public static StageEscortFriend Create(
            Transform parent,
            Vector2 position,
            bool hasAuthority,
            bool useObstacleCourseRules = false)
        {
            GameObject root = new GameObject("Friendly Doodle Walker");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            // The escort must be able to stand on players, but must not be picked
            // up directly through the pushable-object carry mask (layer 9).
            root.layer = 0;

            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = hasAuthority ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = hasAuthority ? 1.45f : 0f;
            rigidbody.mass = 0.42f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            collider.enabled = hasAuthority;

            StageEscortFriend friend = root.AddComponent<StageEscortFriend>();
            friend.authoritative = hasAuthority;
            friend.ignorePlayerCollisions = useObstacleCourseRules;
            friend.defeatOnObstacleCollision = useObstacleCourseRules;
            friend.randomSpeechEnabled = useObstacleCourseRules;
            friend.body = rigidbody;
            friend.hitbox = collider;
            friend.BuildVisual();
            friend.nextSpeechAt = Time.time + Random.Range(1.8f, 3.8f);
            friend.RefreshIgnoredPlayerCollisions();
            return friend;
        }

        public void StopWalking()
        {
            stopped = true;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void Defeat()
        {
            if (!authoritative || defeated) return;
            defeated = true;
            stopped = true;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (!authoritative || stopped || body == null) return;
            if (ignorePlayerCollisions && Time.time >= nextPlayerCollisionRefresh)
            {
                nextPlayerCollisionRefresh = Time.time + 0.5f;
                RefreshIgnoredPlayerCollisions();
            }
            if (TryGetGroundSupport(out Vector2 normal, out bool sticky, out Rigidbody2D supportBody))
            {
                float speedMultiplier = sticky ? 0.3f : 1f;
                Vector2 carrierVelocity = ResolveSupportVelocity(supportBody);
                if (!defeatOnObstacleCollision && normal.y > 0.92f && HasWallAhead())
                {
                    body.linearVelocity = new Vector2(
                        carrierVelocity.x + WalkSpeed * speedMultiplier,
                        4.8f);
                    return;
                }

                // Walk along the detected surface instead of continually pushing
                // horizontally into a slope. Keep the same horizontal progress so
                // ascending routes do not make the escort visibly crawl.
                Vector2 rightTangent = new Vector2(normal.y, -normal.x).normalized;
                if (rightTangent.x < 0f) rightTangent = -rightTangent;
                float tangentSpeed = WalkSpeed * speedMultiplier / Mathf.Max(0.45f, rightTangent.x);
                body.linearVelocity = carrierVelocity + rightTangent * tangentSpeed;
            }
            else
            {
                body.linearVelocity = new Vector2(WalkSpeed, body.linearVelocity.y);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!authoritative || defeated || !defeatOnObstacleCollision || collision == null) return;

            Collider2D other = collision.collider == hitbox ? collision.otherCollider : collision.collider;
            if (other == null || other.GetComponentInParent<PlayerController2D>() != null) return;

            // Ordinary support from below is safe. A side impact, something landing
            // on the friend, or a wall/box blocking its route defeats it.
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < 0.55f)
                {
                    Defeat();
                    return;
                }
            }
        }

        private void RefreshIgnoredPlayerCollisions()
        {
            if (!ignorePlayerCollisions || hitbox == null) return;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                Collider2D[] playerColliders = players[i].GetComponentsInChildren<Collider2D>(true);
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    if (playerColliders[j] != null) Physics2D.IgnoreCollision(hitbox, playerColliders[j], true);
                }
            }
        }

        private static Vector2 ResolveSupportVelocity(Rigidbody2D supportBody)
        {
            if (supportBody == null) return Vector2.zero;

            // Kinematic platforms are driven with MovePosition. Depending on the
            // physics step order their Rigidbody velocity can read as zero even
            // while the platform is moving, which made the escort stop relative
            // to the platform. Use the platform driver's exact planned velocity.
            DirectionalMovingPlatform directional = supportBody.GetComponent<DirectionalMovingPlatform>();
            if (directional != null) return directional.SurfaceVelocity;
            AutomaticMovingPlatform automatic = supportBody.GetComponent<AutomaticMovingPlatform>();
            if (automatic != null) return automatic.SurfaceVelocity;
            return supportBody.linearVelocity;
        }

        private void Update()
        {
            if (visual != null && !stopped)
            {
                walkPhase += Time.deltaTime * 7f;
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 2.2f);
            }

            if (speechBubble == null) return;
            if (speechRemaining > 0f)
            {
                speechRemaining -= Time.deltaTime;
                if (speechRemaining <= 0f) speechBubble.gameObject.SetActive(false);
            }
            else if (randomSpeechEnabled && !stopped && Time.time >= nextSpeechAt)
            {
                speechText.text = SpeechWords[Random.Range(0, SpeechWords.Length)];
                speechBubble.gameObject.SetActive(true);
                speechRemaining = Random.Range(0.85f, 1.25f);
                nextSpeechAt = Time.time + Random.Range(3.2f, 6.2f);
                GameSfx.PlayAt(SfxId.EmotePop, transform.position, 0.42f);
            }
        }

        private bool TryGetGroundSupport(out Vector2 normal, out bool sticky, out Rigidbody2D supportBody)
        {
            normal = Vector2.up;
            sticky = false;
            supportBody = null;
            float bestUpNormal = 0.15f;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 0.32f, Vector2.down, 0.18f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D candidate = hits[i].collider;
                if (!IsSolidOther(candidate) || hits[i].normal.y <= bestUpNormal) continue;
                bestUpNormal = hits[i].normal.y;
                normal = hits[i].normal.normalized;
                sticky = candidate.GetComponentInParent<StageEscortStickySurface>() != null;
                supportBody = candidate.attachedRigidbody;
            }
            return bestUpNormal > 0.15f;
        }

        private bool HasWallAhead()
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll((Vector2)transform.position + Vector2.up * 0.05f, 0.3f, Vector2.right, 0.24f);
            for (int i = 0; i < hits.Length; i++)
                if (IsSolidOther(hits[i].collider)) return true;
            return false;
        }

        private bool IsSolidOther(Collider2D other)
        {
            return other != null && other != hitbox && !other.isTrigger
                && other.GetComponentInParent<StageEscortPlayerOnlyFloor>() == null;
        }

        private void BuildVisual()
        {
            GameObject visualRoot = new GameObject("Friendly Doodle Visual");
            visualRoot.transform.SetParent(transform, false);
            visual = visualRoot.transform;
            GameObject bodyObject = new GameObject("Round Friend Body");
            bodyObject.transform.SetParent(visual, false);
            bodyObject.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer renderer = bodyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.25f, 0.82f, 0.95f, 1f);
            renderer.sortingOrder = 40;
            StageEscortController.AddLine(visual, new Vector2(-0.35f, 0.28f), new Vector2(0f, 0.44f), 0.07f, new Color(0.04f, 0.3f, 0.55f, 1f), 41);
            StageEscortController.AddLine(visual, new Vector2(0f, 0.44f), new Vector2(0.35f, 0.28f), 0.07f, new Color(0.04f, 0.3f, 0.55f, 1f), 41);
            AddFaceDot(visual, new Vector2(-0.16f, 0.08f));
            AddFaceDot(visual, new Vector2(0.16f, 0.08f));
            StageEscortController.AddLine(visual, new Vector2(-0.13f, -0.12f), new Vector2(0f, -0.19f), 0.045f, new Color(0.04f, 0.3f, 0.55f, 1f), 42);
            StageEscortController.AddLine(visual, new Vector2(0f, -0.19f), new Vector2(0.15f, -0.1f), 0.045f, new Color(0.04f, 0.3f, 0.55f, 1f), 42);
            StageEscortController.AddLine(visual, new Vector2(-0.2f, -0.34f), new Vector2(-0.31f, -0.46f), 0.07f, new Color(0.04f, 0.3f, 0.55f, 1f), 41);
            StageEscortController.AddLine(visual, new Vector2(0.2f, -0.34f), new Vector2(0.34f, -0.46f), 0.07f, new Color(0.04f, 0.3f, 0.55f, 1f), 41);
            NicoDrawBossArt.Apply(visual, "ally-escort", new Vector2(0.92f, 0.92f), 42);

            GameObject bubble = new GameObject("Friend Speech Bubble");
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = new Vector3(0f, 1.35f, -0.05f);
            StageEscortController.AddFilledRect(bubble.transform, "Paper", Vector2.zero,
                new Vector2(1.65f, 0.72f), new Color(1f, 0.97f, 0.79f, 0.96f), 48);
            StageEscortController.AddBoxOutline(bubble.transform, Vector2.zero,
                new Vector2(1.65f, 0.72f), new Color(0.06f, 0.3f, 0.55f), 49);
            StageEscortController.AddLine(bubble.transform, new Vector2(-0.25f, -0.36f),
                new Vector2(-0.05f, -0.62f), 0.055f, new Color(0.06f, 0.3f, 0.55f), 49);
            speechText = StageEscortController.CreateText(bubble.transform, "Word", new Vector3(0f, -0.02f, -0.03f),
                46, 0.105f, new Color(0.04f, 0.25f, 0.48f), 50);
            speechBubble = bubble.transform;
            bubble.SetActive(false);
        }

        private static void AddFaceDot(Transform parent, Vector2 position)
        {
            GameObject eye = new GameObject("Friend Eye");
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = position;
            eye.transform.localScale = Vector3.one * 0.095f;
            SpriteRenderer renderer = eye.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.04f, 0.3f, 0.55f, 1f);
            renderer.sortingOrder = 42;
        }
    }

    public sealed class StageEscortFriendDefeatEffect : MonoBehaviour
    {
        private readonly List<LineRenderer> strokes = new List<LineRenderer>();
        private readonly List<Vector2> directions = new List<Vector2>();
        private SpriteRenderer blot;
        private TextMesh cry;
        private float age;

        public static void Create(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("Friend Crayon Defeat Burst");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            StageEscortFriendDefeatEffect effect = root.AddComponent<StageEscortFriendDefeatEffect>();

            GameObject blotObject = new GameObject("Blue Crayon Blot");
            blotObject.transform.SetParent(root.transform, false);
            blotObject.transform.localScale = Vector3.one * 0.65f;
            effect.blot = blotObject.AddComponent<SpriteRenderer>();
            effect.blot.sprite = DoodleRuntimeAssets.CircleSprite;
            effect.blot.color = new Color(0.18f, 0.72f, 0.96f, 0.85f);
            effect.blot.sortingOrder = 58;

            effect.cry = StageEscortController.CreateText(
                root.transform,
                "Friend Defeat Cry",
                new Vector3(0f, 1.05f, -0.08f),
                58,
                0.13f,
                new Color(0.04f, 0.25f, 0.48f, 1f),
                61);
            effect.cry.text = LocalizationManager.T("escort_friend_defeat_cry");
            GameSfx.PlayAt(SfxId.EmotePop, position, 0.9f);

            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f + Random.Range(-0.14f, 0.14f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageEscortController.AddLine(root.transform, direction * 0.22f, direction * 0.58f,
                    Random.Range(0.055f, 0.105f), new Color(0.05f, 0.38f, 0.72f, 0.95f), 59);
                LineRenderer line = root.transform.GetChild(root.transform.childCount - 1).GetComponent<LineRenderer>();
                effect.strokes.Add(line);
                effect.directions.Add(direction);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / 0.72f);
            float alpha = 1f - progress;
            if (blot != null)
            {
                blot.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.5f, progress);
                Color color = blot.color;
                color.a = 0.85f * alpha;
                blot.color = color;
            }
            if (cry != null)
            {
                cry.transform.localPosition = new Vector3(0f, Mathf.Lerp(1.05f, 1.65f, progress), -0.08f);
                Color cryColor = cry.color;
                cryColor.a = alpha;
                cry.color = cryColor;
            }
            for (int i = 0; i < strokes.Count; i++)
            {
                LineRenderer line = strokes[i];
                if (line == null) continue;
                Vector2 direction = directions[i];
                line.SetPosition(0, direction * Mathf.Lerp(0.22f, 0.7f, progress));
                line.SetPosition(1, direction * Mathf.Lerp(0.58f, 1.8f, progress));
                Color color = new Color(0.05f, 0.38f, 0.72f, 0.95f * alpha);
                line.startColor = line.endColor = color;
            }
            if (age >= 0.72f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Stage 5-3's three-round body-bridge escort. Players get a short planning
    /// window to redraw and place their bodies, then become stationary terrain
    /// while the round friend rolls over their actual drawing colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageDrawnEscortChallengeController : MonoBehaviour
    {
        private const string StageId = "5-3";
        private const string StateKind = "drawn_escort_state";
        private const float PlanningSeconds = 20f;

        private enum EscortPhase
        {
            Planning,
            Running,
            RoundClear,
            Failed,
            Complete
        }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Round;
            public int Phase;
            public float Remaining;
            public bool FriendActive;
            public Vector2 FriendPosition;
            public Vector2 FriendVelocity;
            public bool SnackCollected;
        }

        private StageManager stageManager;
        private StageLoader stageLoader;
        private OnlineManager onlineManager;
        private CameraFollow2D cameraFollow;
        private Transform arenaRoot;
        private readonly List<GameObject> planningPads = new List<GameObject>();
        private StageDrawnEscortFriend friend;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private GameObject snack;
        private Vector2 friendSpawn;
        private Vector2 goalPosition;
        private Vector2 replicaTarget;
        private float gapLeft;
        private float gapRight;
        private float playerFloorY;
        private float remaining;
        private float runningElapsed;
        private float nextBroadcastAt;
        private int sequence;
        private int lastSequence;
        private int round = 1;
        private EscortPhase phase;
        private bool snackCollected;
        private bool initialized;

        public bool IsPlanningPhase => initialized && phase == EscortPhase.Planning;
        public bool ShouldKeepRedrawInPlace => IsPlanningPhase
            || initialized && stageManager != null && stageManager.IsDrawingMode;
        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkState;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkState;
        }

        private void Start()
        {
            EnsureInitializedForPlay();
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

            // The old JSON course remains editable, but normal play is owned by
            // this rebuilt arena. Remove the loaded objects completely so an old
            // map decoration cannot be re-enabled and remain at the far right.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject obsolete = transform.GetChild(i).gameObject;
                obsolete.SetActive(false);
                Destroy(obsolete);
            }

            initialized = true;
            BeginRound(1);
        }

        private void Update()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (!HasAuthority)
            {
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                UpdateReplica();
                ApplyPlayerPhase();
                RefreshMonitor();
                return;
            }

            switch (phase)
            {
                case EscortPhase.Planning:
                    remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    ApplyPlayerPhase();
                    if (remaining <= 0f)
                    {
                        // Twenty seconds is a real deadline. Commit an open DRAW
                        // screen, then launch as soon as its collider rebuild ends.
                        if (stageManager.IsDrawingMode) stageManager.ConfirmDrawingMode();
                        else LaunchFriend();
                    }
                    break;

                case EscortPhase.Running:
                    runningElapsed += Time.deltaTime;
                    ApplyPlayerPhase();
                    EvaluateRun();
                    break;

                case EscortPhase.RoundClear:
                    remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    if (remaining <= 0f) BeginRound(round + 1);
                    break;

                case EscortPhase.Failed:
                    remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    // Keep progress through the three-round challenge. A failed
                    // friend retries only the current round instead of loading
                    // 5-3 again and sending everyone back to round one.
                    if (remaining <= 0f) BeginRound(round);
                    break;
            }

            BroadcastState();
            RefreshMonitor();
        }

        private void BeginRound(int nextRound)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            phase = EscortPhase.Planning;
            remaining = PlanningSeconds;
            runningElapsed = 0f;
            snackCollected = false;
            if (friend != null) Destroy(friend.gameObject);
            friend = null;
            BuildArena(round);
            PositionPlayersForPlanning();
            ApplyPlayerPhase();
            Transform local = stageManager != null ? stageManager.ActivePlayerTransform : null;
            if (cameraFollow != null && local != null) cameraFollow.SetTarget(local);
            BroadcastState(true);
            RefreshMonitor();
        }

        private void BuildArena(int roundIndex)
        {
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
            }
            planningPads.Clear();
            GameObject arena = new GameObject("5-3 Three Round Drawn Escort Arena");
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;

            int playerCount = Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
            float gapLength = 5.6f * playerCount;
            gapLeft = -gapLength * 0.5f;
            gapRight = gapLength * 0.5f;
            float startY = GetRouteY(roundIndex, 0f);
            float goalY = GetRouteY(roundIndex, 1f);
            friendSpawn = new Vector2(gapLeft - 0.55f, startY + 0.92f);
            goalPosition = new Vector2(gapRight + 0.7f, goalY + 0.65f);

            CreatePlatform("Start Paper", new Vector2(gapLeft - 2.35f, startY), new Vector2(4.7f, 0.62f),
                new Color(0.78f, 0.91f, 1f), new Color(0.08f, 0.34f, 0.62f), true, false, true);
            CreatePlatform("Goal Paper", new Vector2(gapRight + 2.35f, goalY), new Vector2(4.7f, 0.62f),
                new Color(0.76f, 0.96f, 0.72f), new Color(0.08f, 0.48f, 0.18f), true, false, true);
            CreateSpawnerArt(new Vector2(gapLeft - 2.55f, startY + 1.35f));
            CreateGoalArt(new Vector2(gapRight + 2.35f, goalY + 1.38f));

            playerFloorY = Mathf.Min(startY, goalY) - 2.65f;
            // Replace the obsolete JSON spawn before any fall/respawn routine
            // can send a player back outside this runtime-built arena.
            stageLoader?.SetRuntimeSpawnPosition(new Vector2(0f, playerFloorY + 1.1f));
            CreateMonitor(new Vector2(0f, playerFloorY - 2.1f));
            float playerAreaWidth = gapLength + 18f;
            const float preparationFloorHeight = 0.58f;
            const float preparationWallWidth = 0.62f;
            const float preparationWallHeight = 6.55f;
            CreatePlatform("Player Preparation Floor", new Vector2(0f, playerFloorY),
                new Vector2(playerAreaWidth - preparationWallWidth * 2f, preparationFloorHeight), new Color(1f, 0.88f, 0.52f),
                new Color(0.88f, 0.42f, 0.04f), false, true);
            float wallX = playerAreaWidth * 0.5f - preparationWallWidth * 0.5f;
            float wallY = playerFloorY + preparationFloorHeight * 0.5f + preparationWallHeight * 0.5f;
            CreatePlayerAreaWall("Player Area Left Wall", -wallX, wallY);
            CreatePlayerAreaWall("Player Area Right Wall", wallX, wallY);

            if (roundIndex == 1) CreateHeadWalkingExample(playerAreaWidth);
            if (roundIndex == 3) CreateSnack(new Vector2(0f, GetRouteY(3, 0.5f) + 0.55f));
        }

        private float GetRouteY(int roundIndex, float t)
        {
            if (roundIndex == 1) return 0f;
            if (roundIndex == 2) return Mathf.Lerp(-1.8f, 2.8f, t);
            return Mathf.Lerp(-1.8f, 2.6f, Mathf.Clamp01(1f - Mathf.Abs(t * 2f - 1f)));
        }

        private void PositionPlayersForPlanning()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            int total = Mathf.Max(1, stageManager != null ? stageManager.GetInkBudgetPlayerCount() : players.Length);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy || IsOnline && !IsLocalPlayer(player)) continue;
                int slot = ResolvePlayerSlot(player, i, total);
                float t = (slot + 1f) / (total + 1f);
                Vector2 position = new Vector2(Mathf.Lerp(gapLeft, gapRight, t), playerFloorY + 1.45f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.simulated = true;
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                else player.transform.position = position;
                player.ResetMotion();
                AlignPlayerBottomToPreparationFloor(player);
                stageManager?.RecordAssignedPlayerStart(player, player.transform.position);
            }
        }

        private void AlignPlayerBottomToPreparationFloor(PlayerController2D player)
        {
            if (player == null) return;
            Physics2D.SyncTransforms();
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) bounds = collider.bounds;
                else bounds.Encapsulate(collider.bounds);
                found = true;
            }
            if (!found) return;
            float desiredBottom = playerFloorY + 0.58f * 0.5f + 0.07f;
            float correction = desiredBottom - bounds.min.y;
            Vector3 corrected = player.transform.position + Vector3.up * correction;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = corrected;
            else player.transform.position = corrected;
            Physics2D.SyncTransforms();
        }

        private int ResolvePlayerSlot(PlayerController2D player, int fallback, int total)
        {
            if (!IsOnline || onlineManager?.CurrentLobby?.Players == null) return Mathf.Clamp(fallback, 0, total - 1);
            string id = stageManager.GetOnlinePlayerId(player);
            OnlinePlayerInfo[] lobbyPlayers = onlineManager.CurrentLobby.Players;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].PlayerId == id) return Mathf.Clamp(i, 0, total - 1);
            return Mathf.Clamp(fallback, 0, total - 1);
        }

        private void LaunchFriend()
        {
            phase = EscortPhase.Running;
            remaining = 0f;
            runningElapsed = 0f;
            for (int i = 0; i < planningPads.Count; i++)
                if (planningPads[i] != null) planningPads[i].SetActive(false);
            ApplyPlayerPhase();
            friend = StageDrawnEscortFriend.Create(arenaRoot, friendSpawn, true);
            replicaTarget = friendSpawn;
            if (cameraFollow != null) cameraFollow.SetTarget(friend.transform);
            GameSfx.PlayAt(SfxId.EmotePop, friendSpawn, 0.9f);
            BroadcastState(true);
        }

        private void EvaluateRun()
        {
            if (friend == null) return;
            Vector2 position = friend.transform.position;
            if (round == 3 && !snackCollected
                && Vector2.Distance(position, snack.transform.position) < 1.25f)
            {
                snackCollected = true;
                snack.SetActive(false);
                GameSfx.PlayAt(SfxId.CoinCollect, position, 0.82f);
                BroadcastState(true);
            }

            bool exhausted = round == 3 && !snackCollected && position.x > 2.1f;
            bool fell = position.y < -7.2f || position.x < gapLeft - 4.8f;
            int playerCount = Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
            bool timedOut = runningElapsed >= 5f * playerCount;
            if (exhausted || fell || timedOut)
            {
                FailRound();
                return;
            }

            if (position.x >= goalPosition.x && Mathf.Abs(position.y - goalPosition.y) < 3.2f)
                ClearRound();
        }

        private void ClearRound()
        {
            if (friend != null) friend.StopRolling();
            GameSfx.PlayAt(SfxId.GoalReached, goalPosition, 0.92f);
            if (round >= 3)
            {
                phase = EscortPhase.Complete;
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            phase = EscortPhase.RoundClear;
            remaining = 1.8f;
            BroadcastState(true);
        }

        private void FailRound()
        {
            if (phase == EscortPhase.Failed) return;
            phase = EscortPhase.Failed;
            remaining = 2.2f;
            if (friend != null)
            {
                friend.Collapse();
                GameSfx.PlayAt(SfxId.PlayerDeath, friend.transform.position, 0.72f);
            }
            BroadcastState(true);
        }

        private void ApplyPlayerPhase()
        {
            bool planning = phase == EscortPhase.Planning;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy || IsOnline && !IsLocalPlayer(player)) continue;
                player.SetControlsEnabled(planning && !stageManager.IsDrawingMode);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body == null) continue;
                if (planning)
                {
                    if (body.bodyType != RigidbodyType2D.Dynamic) body.bodyType = RigidbodyType2D.Dynamic;
                }
                else
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    if (body.bodyType != RigidbodyType2D.Kinematic) body.bodyType = RigidbodyType2D.Kinematic;
                }
            }
        }

        private bool IsLocalPlayer(PlayerController2D player)
        {
            return stageManager != null && player.transform == stageManager.ActivePlayerTransform;
        }

        private void UpdateReplica()
        {
            if (friend != null)
                friend.transform.position = Vector2.Lerp(friend.transform.position, replicaTarget,
                    1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            NetworkState state = new NetworkState
            {
                Sequence = ++sequence,
                Round = round,
                Phase = (int)phase,
                Remaining = remaining,
                FriendActive = friend != null,
                FriendPosition = friend != null ? (Vector2)friend.transform.position : Vector2.zero,
                FriendVelocity = friend != null && friend.Body != null ? friend.Body.linearVelocity : Vector2.zero,
                SnackCollected = snackCollected
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkState(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastSequence) return;
            lastSequence = state.Sequence;
            EscortPhase previousPhase = phase;
            EscortPhase incomingPhase = (EscortPhase)state.Phase;
            // A same-round retry does not change Round, so clients also need
            // the Failed -> Planning transition to rebuild the arena and place
            // their locally owned player back on this round's floor.
            if (state.Round != round || previousPhase == EscortPhase.Failed && incomingPhase == EscortPhase.Planning)
                BeginRound(state.Round);
            phase = incomingPhase;
            remaining = state.Remaining;
            snackCollected = state.SnackCollected;
            if (phase != EscortPhase.Planning && stageManager.IsDrawingMode)
                stageManager.ConfirmDrawingMode();
            if (snack != null) snack.SetActive(round == 3 && !snackCollected);
            replicaTarget = state.FriendPosition;

            if (state.FriendActive && friend == null)
            {
                for (int i = 0; i < planningPads.Count; i++)
                    if (planningPads[i] != null) planningPads[i].SetActive(false);
                friend = StageDrawnEscortFriend.Create(arenaRoot, state.FriendPosition, false);
            }
            else if (!state.FriendActive && friend != null)
            {
                Destroy(friend.gameObject);
                friend = null;
            }
            if (friend != null && cameraFollow != null && phase == EscortPhase.Running && previousPhase != phase)
                cameraFollow.SetTarget(friend.transform);
            if (friend != null && phase == EscortPhase.Failed) friend.Collapse();
            ApplyPlayerPhase();
            RefreshMonitor();
        }

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        private void CreateMonitor(Vector2 position)
        {
            GameObject monitor = new GameObject("5-3 Planning Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.position = position;
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(16.2f, 3.15f), -32);
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main",
                new Vector3(0f, 0.58f, -0.03f), 54, 0.13f, new Color(0.02f, 0.34f, 0.52f), -28);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub",
                new Vector3(0f, -0.52f, -0.04f), 40, 0.085f, new Color(0.62f, 0.22f, 0.08f), -27);
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null || monitorSub == null) return;
            switch (phase)
            {
                case EscortPhase.Planning:
                    monitorMain.text = LocalizationManager.Format("drawn_escort_monitor", round, remaining);
                    monitorSub.text = round == 3
                        ? LocalizationManager.T("drawn_escort_final_plan")
                        : LocalizationManager.T("drawn_escort_plan");
                    break;
                case EscortPhase.Running:
                    monitorMain.text = LocalizationManager.Format("drawn_escort_round_running", round);
                    monitorSub.text = LocalizationManager.T("drawn_escort_running");
                    break;
                case EscortPhase.RoundClear:
                    monitorMain.text = LocalizationManager.Format("drawn_escort_round_clear", round);
                    monitorSub.text = string.Empty;
                    break;
                case EscortPhase.Failed:
                    monitorMain.text = LocalizationManager.T("drawn_escort_game_over");
                    monitorSub.text = LocalizationManager.T("drawn_escort_failed");
                    break;
                case EscortPhase.Complete:
                    monitorMain.text = LocalizationManager.T("escort_clear_title");
                    monitorSub.text = LocalizationManager.T("escort_clear_sub");
                    break;
            }
        }

        private GameObject CreatePlatform(
            string name, Vector2 position, Vector2 size, Color fill, Color outline,
            bool oneWay = false, bool playerOnly = false, bool friendOnly = false)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(arenaRoot, false);
            root.transform.position = position;
            root.layer = 6;
            root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            if (oneWay)
            {
                collider.usedByEffector = true;
                PlatformEffector2D effector = root.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 165f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }
            if (playerOnly) root.AddComponent<StageEscortPlayerOnlyFloor>();
            if (friendOnly) root.AddComponent<StageEscortFriendOnlyFloor>();
            StageEscortController.AddFilledRect(root.transform, "Paper", Vector2.zero, size, fill, 18);
            if (size.x > size.y * 1.5f) AddPlatformPencilHatching(root.transform, size, outline);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, outline, 20);
            if (friendOnly) AddFriendOnlyPlatformMarks(root.transform, size, outline);
            return root;
        }

        private static void AddPlatformPencilHatching(Transform parent, Vector2 size, Color ink)
        {
            float halfWidth = size.x * 0.5f;
            float bottom = -size.y * 0.5f + 0.055f;
            float top = size.y * 0.5f - 0.055f;
            float rise = Mathf.Max(0.01f, top - bottom);
            const float run = 0.72f;
            const float spacing = 0.28f;
            Color hatch = new Color(ink.r, ink.g, ink.b, 0.32f);
            int index = 0;
            for (float startX = -halfWidth - run; startX < halfWidth; startX += spacing)
            {
                float t0 = Mathf.Clamp01((-halfWidth - startX) / run);
                float t1 = Mathf.Clamp01((halfWidth - startX) / run);
                Vector2 from = new Vector2(startX + run * t0, Mathf.Lerp(bottom, top, t0));
                Vector2 to = new Vector2(startX + run * t1, Mathf.Lerp(bottom, top, t1));
                if ((to - from).sqrMagnitude < 0.002f) continue;
                StageEscortController.AddLine(parent, from, to,
                    index++ % 3 == 0 ? 0.026f : 0.018f, hatch, 19);
            }
        }

        private static void AddFriendOnlyPlatformMarks(Transform parent, Vector2 size, Color ink)
        {
            // Repeated round-friend stamps communicate the collision rule
            // without adding language-dependent text to the course.
            for (int i = -1; i <= 1; i++)
            {
                GameObject mark = new GameObject("Friend Only Mark");
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(i * size.x * 0.24f, 0f, -0.04f);
                mark.transform.localScale = Vector3.one * 0.24f;
                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(ink.r, ink.g, ink.b, 0.72f);
                renderer.sortingOrder = 21;
            }
        }

        private void CreatePlayerAreaWall(string name, float x, float y)
        {
            CreatePlatform(name, new Vector2(x, y), new Vector2(0.62f, 6.55f),
                new Color(1f, 0.9f, 0.62f), new Color(0.88f, 0.42f, 0.04f));
        }

        private void CreateSpawnerArt(Vector2 position)
        {
            GameObject spawner = new GameObject("Clearly Marked Friend Spawner");
            spawner.transform.SetParent(arenaRoot, false);
            spawner.transform.position = position;
            if (!StageGun.TryCreateResourceSprite(spawner.transform,
                "StageObjects/NicoDraw/escort-spawner", "Friend Spawner Art", new Vector2(2.25f, 1.7f), 29))
            {
                StageEscortController.AddFilledRect(spawner.transform, "Blue Spawner", Vector2.zero,
                    new Vector2(2.1f, 1.45f), new Color(0.28f, 0.72f, 0.94f), 29);
                StageEscortController.AddBoxOutline(spawner.transform, Vector2.zero,
                    new Vector2(2.1f, 1.45f), new Color(0.04f, 0.3f, 0.55f), 30);
            }
            else
            {
                // The source artwork faces the opposite way. The machine sits
                // on the left and must visibly feed the friend toward the goal.
                Transform art = spawner.transform.Find("Friend Spawner Art");
                if (art != null)
                {
                    Vector3 scale = art.localScale;
                    scale.x = -Mathf.Abs(scale.x);
                    art.localScale = scale;
                }
            }
            StageEscortController.AddLine(spawner.transform, new Vector2(0.2f, -0.95f),
                new Vector2(1.55f, -0.95f), 0.1f, new Color(0.04f, 0.45f, 0.72f), 31);
        }

        private void CreateGoalArt(Vector2 position)
        {
            GameObject goal = new GameObject("Round Goal");
            goal.transform.SetParent(arenaRoot, false);
            goal.transform.position = position;
            StageGun.TryCreateResourceSprite(goal.transform, "StageObjects/NicoDraw/goal-door",
                "Colored Pencil Goal Door", new Vector2(3.15f, 2.65f), 27);
            TextMesh label = StageEscortController.CreateText(goal.transform, "Goal Label",
                new Vector3(0f, 1.85f, -0.04f), 40, 0.095f, new Color(0.04f, 0.42f, 0.16f), 28);
            label.text = LocalizationManager.T("stage_goal_label");
        }

        private void CreateSnack(Vector2 position)
        {
            snack = new GameObject("Round 3 Energy Snack");
            snack.transform.SetParent(arenaRoot, false);
            snack.transform.position = position;
            if (!StageGun.TryCreateResourceSprite(snack.transform,
                "StageDecorations/CrayonSet/donut", "Crayon Donut Snack", new Vector2(1.2f, 1.2f), 35))
            {
                SpriteRenderer fallback = snack.AddComponent<SpriteRenderer>();
                fallback.sprite = DoodleRuntimeAssets.CircleSprite;
                fallback.color = new Color(1f, 0.67f, 0.18f);
                fallback.sortingOrder = 35;
                snack.transform.localScale = Vector3.one * 0.72f;
            }
        }

        private void CreateHeadWalkingExample(float playerAreaWidth)
        {
            GameObject example = new GameObject("Friend Walks On Your Drawing Example");
            example.transform.SetParent(arenaRoot, false);
            // Keep the sample out of the course itself. It belongs beside the
            // lower instruction monitor, where it can be read while planning.
            example.transform.position = new Vector2(-playerAreaWidth * 0.5f + 1.65f, playerFloorY - 2.05f);
            Color red = new Color(0.9f, 0.13f, 0.1f);
            StageEscortController.AddBoxOutline(example.transform, Vector2.up * 0.25f,
                new Vector2(1.35f, 0.78f), red, 28);
            StageEscortController.AddLine(example.transform, new Vector2(0f, -0.15f), new Vector2(0f, -1.45f), 0.09f, red, 27);
            StageEscortController.AddLine(example.transform, new Vector2(0f, -0.55f), new Vector2(-0.72f, -0.22f), 0.07f, red, 27);
            StageEscortController.AddLine(example.transform, new Vector2(0f, -0.55f), new Vector2(0.72f, -0.22f), 0.07f, red, 27);
            GameObject ball = new GameObject("Example Round Friend");
            ball.transform.SetParent(example.transform, false);
            ball.transform.localPosition = new Vector2(0f, 1.05f);
            ball.transform.localScale = Vector3.one * 0.58f;
            SpriteRenderer renderer = ball.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.25f, 0.82f, 0.95f);
            renderer.sortingOrder = 31;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageDrawnEscortFriend : MonoBehaviour
    {
        private const float RollSpeed = 2.4f;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private bool authoritative;
        private bool stopped;
        private GameObject normalFace;
        private GameObject defeatedFace;

        public Rigidbody2D Body => body;

        public static StageDrawnEscortFriend Create(Transform parent, Vector2 position, bool authority)
        {
            GameObject root = new GameObject("Rolling Round Friend");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = 0;
            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = authority ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = authority ? 1.4f : 0f;
            rigidbody.mass = 0.38f;
            rigidbody.freezeRotation = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.enabled = authority;

            StageDrawnEscortFriend friend = root.AddComponent<StageDrawnEscortFriend>();
            friend.authoritative = authority;
            friend.body = rigidbody;
            friend.hitbox = collider;
            friend.BuildVisual();
            friend.IgnorePlayerOnlyFloors();
            return friend;
        }

        public void StopRolling()
        {
            stopped = true;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void Collapse()
        {
            if (stopped && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, -90f)) < 1f) return;
            stopped = true;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
            }
            transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            transform.localScale = new Vector3(1.18f, 0.72f, 1f);
            if (normalFace != null) normalFace.SetActive(false);
            if (defeatedFace != null)
            {
                defeatedFace.SetActive(true);
                // The body falls sideways, but keep the defeated expression
                // upright so its X eyes and frown remain immediately readable.
                defeatedFace.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private void FixedUpdate()
        {
            if (!authoritative || stopped || body == null) return;
            Vector2 velocity = body.linearVelocity;
            if (TryGetSupport(out Vector2 normal))
            {
                Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;
                if (tangent.x < 0f) tangent = -tangent;
                float tangentSpeed = RollSpeed / Mathf.Max(0.38f, tangent.x);
                velocity = tangent * tangentSpeed;
            }
            else velocity.x = RollSpeed;
            body.linearVelocity = velocity;
            body.angularVelocity = -210f;
        }

        private bool TryGetSupport(out Vector2 normal)
        {
            normal = Vector2.up;
            float best = 0.12f;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 0.36f, Vector2.down, 0.2f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D candidate = hits[i].collider;
                if (candidate == null || candidate == hitbox || candidate.isTrigger || hits[i].normal.y <= best) continue;
                best = hits[i].normal.y;
                normal = hits[i].normal.normalized;
            }
            return best > 0.12f;
        }

        private void IgnorePlayerOnlyFloors()
        {
            if (hitbox == null) return;
            StageEscortPlayerOnlyFloor[] floors = Object.FindObjectsByType<StageEscortPlayerOnlyFloor>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < floors.Length; i++)
            {
                Collider2D[] colliders = floors[i].GetComponentsInChildren<Collider2D>(false);
                for (int j = 0; j < colliders.Length; j++)
                    if (colliders[j] != null) Physics2D.IgnoreCollision(hitbox, colliders[j], true);
            }
        }

        private void BuildVisual()
        {
            GameObject outline = new GameObject("Round Crayon Outline");
            outline.transform.SetParent(transform, false);
            outline.transform.localScale = Vector3.one * 0.98f;
            SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            outlineRenderer.color = new Color(0.04f, 0.3f, 0.55f);
            outlineRenderer.sortingOrder = 41;

            GameObject ball = new GameObject("Round Crayon Friend");
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * 0.88f;
            SpriteRenderer renderer = ball.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.25f, 0.82f, 0.95f);
            renderer.sortingOrder = 42;
            normalFace = new GameObject("Happy Face");
            normalFace.transform.SetParent(ball.transform, false);
            StageEscortController.AddLine(normalFace.transform, new Vector2(-0.22f, 0.12f), new Vector2(-0.13f, 0.12f),
                0.06f, new Color(0.04f, 0.3f, 0.55f), 44);
            StageEscortController.AddLine(normalFace.transform, new Vector2(0.13f, 0.12f), new Vector2(0.22f, 0.12f),
                0.06f, new Color(0.04f, 0.3f, 0.55f), 44);
            StageEscortController.AddLine(normalFace.transform, new Vector2(-0.16f, -0.12f), new Vector2(0f, -0.2f),
                0.045f, new Color(0.04f, 0.3f, 0.55f), 44);
            StageEscortController.AddLine(normalFace.transform, new Vector2(0f, -0.2f), new Vector2(0.17f, -0.1f),
                0.045f, new Color(0.04f, 0.3f, 0.55f), 44);

            defeatedFace = new GameObject("Defeated Face");
            defeatedFace.transform.SetParent(ball.transform, false);
            Color faceColor = new Color(0.04f, 0.3f, 0.55f);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(-0.24f, 0.2f), new Vector2(-0.1f, 0.06f),
                0.055f, faceColor, 44);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(-0.24f, 0.06f), new Vector2(-0.1f, 0.2f),
                0.055f, faceColor, 44);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(0.1f, 0.2f), new Vector2(0.24f, 0.06f),
                0.055f, faceColor, 44);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(0.1f, 0.06f), new Vector2(0.24f, 0.2f),
                0.055f, faceColor, 44);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(-0.18f, -0.2f), new Vector2(0f, -0.1f),
                0.05f, faceColor, 44);
            StageEscortController.AddLine(defeatedFace.transform, new Vector2(0f, -0.1f), new Vector2(0.18f, -0.2f),
                0.05f, faceColor, 44);
            defeatedFace.SetActive(false);
        }
    }
}
