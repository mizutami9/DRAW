using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageLinkedShieldSurvivalController : StageEliminationChallengeController
    {
        private const string StageId = "14-3";
        private const string StateKind = "linked_shield_state";
        private const string MissileKind = "linked_shield_missile";
        private const string ButtonRequestKind = "linked_shield_button_request";
        private const string EliminateRequestKind = "linked_shield_eliminate_request";
        private const string EliminatedKind = "linked_shield_eliminated";
        private const float PreparationSeconds = 3f;
        private const float DurationSeconds = 60f;
        private const float ShieldSeconds = 1f;

        private enum Phase { Preparation, Playing, Finished, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Phase;
            public float Preparation;
            public float Remaining;
            public float Restart;
            public int[] MapTargets;
            public int[] MapSides;
            public float[] ShieldRemaining;
            public string[] RoomPlayerIds;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class MissileState
        {
            public int Sequence;
            public int TargetRoom;
            public int TargetSide;
            public Vector2 Position;
            public Vector2 Impact;
            public float Speed;
            public float Size;
        }

        [System.Serializable] private sealed class ButtonRequest { public int ButtonIndex; }
        [System.Serializable] private sealed class EliminationState { public string PlayerId; }

        private static readonly Vector2[] RoomCenters =
        {
            new Vector2(-4.5f, 2.8f), new Vector2(4.5f, 2.8f),
            new Vector2(-4.5f, -2.8f), new Vector2(4.5f, -2.8f)
        };

        private static readonly Color[] RoomColors =
        {
            new Color(1f, 0.38f, 0.25f), new Color(0.2f, 0.72f, 1f),
            new Color(1f, 0.72f, 0.15f), new Color(0.35f, 0.85f, 0.45f)
        };

        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly HashSet<int> receivedMissiles = new HashSet<int>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly List<StageLinkedShieldButton> buttons = new List<StageLinkedShieldButton>();
        private readonly List<StageLinkedShieldWall> shieldWalls = new List<StageLinkedShieldWall>();
        private readonly List<StageLinkedShieldMissile> missiles = new List<StageLinkedShieldMissile>();
        private readonly int[] mapTargets = new int[16];
        private readonly int[] mapSides = new int[16];
        private readonly float[] shieldEndsAt = new float[16];
        private readonly string[] roomPlayerIds = new string[4];

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousCameraFollowEnabled;
        private bool cameraLocked;
        private Transform hud;
        private TextMesh titleText;
        private TextMesh timerText;
        private TextMesh helpText;
        private Phase phase = Phase.Preparation;
        private float preparation = PreparationSeconds;
        private float remaining = DurationSeconds;
        private float restartRemaining;
        private float nextVolleyAt;
        private float nextStateAt;
        private int stateSequence;
        private int lastStateSequence;
        private int missileSequence;
        private int nextTargetCursor;
        private int occupiedRooms = 1;
        private bool restored;
        private static Sprite squareSprite;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            RestorePlayers();
            RestoreCamera();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }

            BuildRoster();
            BuildArena();
            if (HasAuthority) RandomizeLinks();
            else RefreshButtonLabels();
            PositionPlayers();
            BuildMonitor();
            LockCamera();
            SetLocalControls(false);
            nextVolleyAt = Time.time + PreparationSeconds + 1.2f;
            BroadcastState(true);
            RefreshPresentation();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            ApplyPendingEliminations();
            RefreshShields();
            missiles.RemoveAll(item => item == null);

            if (HasAuthority)
            {
                if (phase == Phase.Preparation)
                {
                    preparation = Mathf.Max(0f, preparation - Time.deltaTime);
                    if (preparation <= 0f)
                    {
                        phase = Phase.Playing;
                        SetLocalControls(true);
                        BroadcastState(true);
                        GameSfx.Play(SfxId.EmotePop);
                    }
                }
                else if (phase == Phase.Playing)
                {
                    remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    if (Time.time >= nextVolleyAt) FireVolley();
                    if (AreAllEliminated()) BeginFailure();
                    else if (remaining <= 0f)
                    {
                        phase = Phase.Finished;
                        SetLocalControls(false);
                        BroadcastState(true);
                        stageManager.ClearStage();
                    }
                }
                else if (phase == Phase.Failed)
                {
                    restartRemaining -= Time.deltaTime;
                    if (restartRemaining <= 0f) stageManager.Retry();
                }
                BroadcastState(false);
            }
            else
            {
                if (phase == Phase.Preparation) preparation = Mathf.Max(0f, preparation - Time.deltaTime);
                else if (phase == Phase.Playing) remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                else if (phase == Phase.Failed) restartRemaining = Mathf.Max(0f, restartRemaining - Time.deltaTime);
            }

            RefreshPresentation();
        }

        private void BuildRoster()
        {
            participants.Clear();
            System.Array.Clear(roomPlayerIds, 0, roomPlayerIds.Length);
            if (IsOnline)
            {
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                int room = 0;
                if (roster != null)
                    for (int i = 0; i < roster.Length && room < 4; i++)
                    {
                        if (roster[i] == null || string.IsNullOrEmpty(roster[i].PlayerId)) continue;
                        roomPlayerIds[room++] = roster[i].PlayerId;
                        participants.Add(roster[i].PlayerId);
                    }
                occupiedRooms = Mathf.Max(1, room);
                return;
            }

            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            occupiedRooms = Mathf.Clamp(players.Length, 1, 4);
            for (int i = 0; i < players.Length && i < 4; i++)
            {
                string id = ResolvePlayerId(players[i]);
                roomPlayerIds[i] = id;
                participants.Add(id);
            }
        }

        private void BuildArena()
        {
            for (int room = 0; room < 4; room++)
            {
                Vector2 center = RoomCenters[room];
                Color color = RoomColors[room];
                CreateTerrain("Room " + (room + 1) + " Floor", center + Vector2.down * 1.9f, new Vector2(5.2f, 0.42f));
                CreateTerrain("Room " + (room + 1) + " Ceiling", center + Vector2.up * 1.9f, new Vector2(5.2f, 0.42f));
                CreateTerrain("Room " + (room + 1) + " Left", center + Vector2.left * 2.6f, new Vector2(0.42f, 4.18f));
                CreateTerrain("Room " + (room + 1) + " Right", center + Vector2.right * 2.6f, new Vector2(0.42f, 4.18f));
                CreateRoomBadge(room, center + new Vector2(0f, 2.48f), color);

                for (int side = 0; side < 4; side++)
                    shieldWalls.Add(StageLinkedShieldWall.Create(transform, GetShieldPosition(room, side), GetShieldSize(side), color));

                for (int button = 0; button < 4; button++)
                {
                    int index = room * 4 + button;
                    Vector2 position = button == 0 ? center + Vector2.up * 1.52f
                        : button == 1 ? center + Vector2.right * 2.22f
                        : button == 2 ? center + Vector2.down * 1.52f
                        : center + Vector2.left * 2.22f;
                    buttons.Add(StageLinkedShieldButton.Create(transform, this, index, button, position));
                }
            }
        }

        private void CreateTerrain(string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name) { layer = 6, tag = "Ground" };
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.AddComponent<BoxCollider2D>().size = size;
            StageEscortController.AddFilledRect(root.transform, "Paper Steel Fill", Vector2.zero, size, new Color(0.89f, 0.92f, 0.92f), 8);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.18f, 0.23f, 0.27f), 10);
            float length = size.x > size.y ? size.x : size.y;
            bool horizontal = size.x > size.y;
            for (float value = -length * 0.42f; value < length * 0.42f; value += 0.75f)
            {
                Vector2 a = horizontal ? new Vector2(value, -size.y * 0.34f) : new Vector2(-size.x * 0.34f, value);
                Vector2 b = horizontal ? new Vector2(value + 0.38f, size.y * 0.34f) : new Vector2(size.x * 0.34f, value + 0.38f);
                StageEscortController.AddLine(root.transform, a, b, 0.035f, new Color(0.48f, 0.55f, 0.58f, 0.72f), 11);
            }
        }

        private void CreateRoomBadge(int room, Vector2 position, Color color)
        {
            GameObject badge = new GameObject("Room Badge P" + (room + 1));
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = position;
            StageEscortController.AddFilledRect(badge.transform, "Badge", Vector2.zero, new Vector2(2.05f, 0.72f), new Color(color.r, color.g, color.b, 0.84f), 18);
            StageEscortController.AddBoxOutline(badge.transform, Vector2.zero, new Vector2(2.05f, 0.72f), new Color(0.12f, 0.14f, 0.16f), 19);
            TextMesh label = StageEscortController.CreateText(badge.transform, "Label", new Vector3(0f, 0f, -0.02f), 42, 0.1f, Color.white, 20);
            label.text = "P" + (room + 1);
        }

        private void RandomizeLinks()
        {
            int offset = occupiedRooms > 1 ? Random.Range(1, occupiedRooms) : 0;
            for (int source = 0; source < 4; source++)
            {
                int target = source < occupiedRooms ? (source + offset) % occupiedRooms : source;
                int[] sides = { 0, 1, 2, 3 };
                for (int i = sides.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int temp = sides[i]; sides[i] = sides[j]; sides[j] = temp;
                }
                for (int button = 0; button < 4; button++)
                {
                    int index = source * 4 + button;
                    mapTargets[index] = target;
                    mapSides[index] = sides[button];
                }
            }
            RefreshButtonLabels();
        }

        private void RefreshButtonLabels()
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i] != null) buttons[i].SetMapping(mapTargets[i], mapSides[i], RoomColors[mapTargets[i]]);
        }

        internal void RequestButton(PlayerController2D player, int buttonIndex)
        {
            if (phase != Phase.Playing || player == null || buttonIndex < 0 || buttonIndex >= 16) return;
            string playerId = ResolvePlayerId(player);
            int room = FindRoom(playerId);
            if (room < 0 || buttonIndex / 4 != room || eliminated.Contains(playerId)) return;
            if (IsOnline && !HasAuthority)
            {
                if (playerId != onlineManager?.LocalPlayerId) return;
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId, Kind = ButtonRequestKind,
                    Json = JsonUtility.ToJson(new ButtonRequest { ButtonIndex = buttonIndex })
                });
                return;
            }
            ActivateMappedShield(buttonIndex);
        }

        private void ActivateMappedShield(int buttonIndex)
        {
            int target = Mathf.Clamp(mapTargets[buttonIndex], 0, 3);
            int side = Mathf.Clamp(mapSides[buttonIndex], 0, 3);
            int shield = target * 4 + side;
            shieldEndsAt[shield] = Mathf.Max(shieldEndsAt[shield], Time.time + ShieldSeconds);
            shieldWalls[shield]?.SetActive(true);
            buttons[buttonIndex]?.Pulse();
            GameSfx.Play(SfxId.SwitchPress, 0.82f);
            BroadcastState(true);
        }

        private void RefreshShields()
        {
            for (int i = 0; i < shieldWalls.Count; i++)
                shieldWalls[i]?.SetActive(Time.time < shieldEndsAt[i]);
        }

        private void FireVolley()
        {
            List<int> livingRooms = GetLivingRooms();
            if (livingRooms.Count == 0) return;
            float progress = 1f - remaining / DurationSeconds;
            int maxCount = Mathf.Clamp(1 + Mathf.CeilToInt(progress * occupiedRooms), 1, Mathf.Min(4, livingRooms.Count + 1));
            int count = Mathf.Clamp(1 + Mathf.FloorToInt(progress * maxCount), 1, maxCount);
            for (int i = 0; i < count; i++)
            {
                int room = livingRooms[(nextTargetCursor + i) % livingRooms.Count];
                int side = Random.Range(0, 4);
                Vector2 impact = GetRandomImpact(room, side);
                Vector2 outward = SideVector(side);
                float distance = Mathf.Lerp(11.5f, 8.5f, progress) + Random.Range(-0.3f, 1.3f);
                MissileState state = new MissileState
                {
                    Sequence = ++missileSequence,
                    TargetRoom = room,
                    TargetSide = side,
                    Impact = impact,
                    Position = impact + outward * distance,
                    Speed = Mathf.Lerp(3.2f, 9.2f, progress) * Random.Range(0.92f, 1.08f),
                    Size = Random.Range(0.8f, Mathf.Lerp(1f, 1.4f, progress))
                };
                ApplyMissile(state);
                if (IsOnline && onlineManager != null)
                    onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = MissileKind, Json = JsonUtility.ToJson(state) });
            }
            nextTargetCursor = (nextTargetCursor + count) % livingRooms.Count;
            nextVolleyAt = Time.time + Mathf.Lerp(3.6f, 0.8f, progress) / Mathf.Lerp(1f, 1.18f, (occupiedRooms - 1) / 3f);
        }

        private void ApplyMissile(MissileState state)
        {
            if (state == null || !receivedMissiles.Add(state.Sequence)) return;
            StageLinkedShieldMissile missile = StageLinkedShieldMissile.Create(transform, this, state.Sequence,
                state.TargetRoom, state.TargetSide, state.Position, state.Impact, state.Speed, state.Size, HasAuthority);
            missiles.Add(missile);
            StageIncomingShieldMarker.Create(transform, state.Impact, GetShieldSize(state.TargetSide), RoomColors[state.TargetRoom],
                Vector2.Distance(state.Position, state.Impact) / Mathf.Max(0.1f, state.Speed));
            GameSfx.PlayAt(SfxId.CannonFire, state.Position, 0.65f);
        }

        internal void ResolveMissile(int sequence, int room, int side, Vector2 point)
        {
            if (!HasAuthority || phase != Phase.Playing) return;
            int shieldIndex = Mathf.Clamp(room * 4 + side, 0, 15);
            bool blocked = Time.time < shieldEndsAt[shieldIndex];
            if (blocked) return;

            string playerId = room >= 0 && room < roomPlayerIds.Length ? roomPlayerIds[room] : null;
            PlayerController2D player = ResolvePlayer(playerId);
            if (player != null && (player.IsInvulnerable || player.IsTurtleShelled))
            {
                GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.85f);
                return;
            }
            ConfirmElimination(playerId, IsOnline);
        }

        internal void ShowMissileImpact(int room, int side, Vector2 point)
        {
            int shieldIndex = Mathf.Clamp(room * 4 + side, 0, 15);
            if (Time.time < shieldEndsAt[shieldIndex])
            {
                GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.95f);
                StageRicochetImpactPulse.Create(transform, point);
                return;
            }
            GameObject explosion = new GameObject("14-3 Missile Impact");
            explosion.transform.position = point;
            explosion.AddComponent<BombExplosionVisual>().Configure(1.45f, false);
            GameSfx.PlayAt(SfxId.BombExplosion, point, 0.9f);
        }

        private List<int> GetLivingRooms()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < occupiedRooms; i++)
                if (!string.IsNullOrEmpty(roomPlayerIds[i]) && !eliminated.Contains(roomPlayerIds[i])) result.Add(i);
            return result;
        }

        private static Vector2 GetRandomImpact(int room, int side)
        {
            Vector2 center = RoomCenters[Mathf.Clamp(room, 0, 3)];
            if (side == 0) return center + new Vector2(Random.Range(-1.85f, 1.85f), 2.16f);
            if (side == 1) return center + new Vector2(2.86f, Random.Range(-1.28f, 1.28f));
            if (side == 2) return center + new Vector2(Random.Range(-1.85f, 1.85f), -2.16f);
            return center + new Vector2(-2.86f, Random.Range(-1.28f, 1.28f));
        }

        private static Vector2 GetShieldPosition(int room, int side)
        {
            Vector2 center = RoomCenters[room];
            if (side == 0) return center + Vector2.up * 2.16f;
            if (side == 1) return center + Vector2.right * 2.86f;
            if (side == 2) return center + Vector2.down * 2.16f;
            return center + Vector2.left * 2.86f;
        }

        private static Vector2 GetShieldSize(int side) => side == 0 || side == 2 ? new Vector2(4.35f, 0.34f) : new Vector2(0.34f, 3f);
        private static Vector2 SideVector(int side) => side == 0 ? Vector2.up : side == 1 ? Vector2.right : side == 2 ? Vector2.down : Vector2.left;
        internal static string SideArrow(int side) => side == 0 ? "↑" : side == 1 ? "→" : side == 2 ? "↓" : "←";

        private void PositionPlayers()
        {
            if (IsOnline)
            {
                int room = FindRoom(onlineManager?.LocalPlayerId);
                PlayerController2D local = stageManager.ActivePlayerTransform != null
                    ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
                PlacePlayer(local, Mathf.Max(0, room));
                return;
            }
            for (int room = 0; room < occupiedRooms; room++) PlacePlayer(ResolvePlayer(roomPlayerIds[room]), room);
        }

        private static void PlacePlayer(PlayerController2D player, int room)
        {
            if (player == null) return;
            float floorY = RoomCenters[room].y - 1.68f;
            Vector2 destination = RoomCenters[room] + new Vector2(0f, -0.72f);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = destination; body.linearVelocity = Vector2.zero; }
            player.transform.position = destination;
            Physics2D.SyncTransforms();
            float lowest = float.PositiveInfinity;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger) lowest = Mathf.Min(lowest, colliders[i].bounds.min.y);
            if (!float.IsPositiveInfinity(lowest)) destination.y += floorY + 0.04f - lowest;
            if (body != null) body.position = destination;
            player.transform.position = destination;
            player.ResetMotion();
            Physics2D.SyncTransforms();
        }

        private void BuildMonitor()
        {
            GameObject root = new GameObject("14-3 Defense Monitor");
            root.transform.SetParent(transform, false);
            hud = root.transform;
            hud.localPosition = new Vector3(0f, 7.2f, 0.45f);
            StageEscortController.AddFilledRect(hud, "Frame", Vector2.zero, new Vector2(10f, 3.2f), new Color(0.16f, 0.2f, 0.24f, 0.95f), 210);
            StageEscortController.AddFilledRect(hud, "Screen", Vector2.zero, new Vector2(9.35f, 2.58f), new Color(0.01f, 0.04f, 0.055f, 0.96f), 211);
            titleText = StageEscortController.CreateText(hud, "Title", new Vector3(0f, 0.86f, -0.02f), 52, 0.12f, new Color(0.4f, 0.95f, 1f), 213);
            timerText = StageEscortController.CreateText(hud, "Timer", new Vector3(0f, 0.05f, -0.03f), 72, 0.2f, new Color(0.25f, 1f, 0.68f), 213);
            helpText = StageEscortController.CreateText(hud, "Help", new Vector3(0f, -0.92f, -0.04f), 40, 0.08f, new Color(1f, 0.75f, 0.2f), 213);
        }

        private void RefreshPresentation()
        {
            if (titleText == null) return;
            titleText.text = LocalizationManager.T("linked_shield_title");
            if (phase == Phase.Preparation)
            {
                timerText.text = Mathf.Max(1, Mathf.CeilToInt(preparation)).ToString();
                helpText.text = LocalizationManager.T("linked_shield_ready");
            }
            else if (phase == Phase.Playing)
            {
                timerText.text = remaining.ToString("00.0");
                helpText.text = LocalizationManager.T("linked_shield_hint");
            }
            else if (phase == Phase.Failed)
            {
                timerText.text = "NG";
                helpText.text = LocalizationManager.T("linked_shield_failed");
            }
            else
            {
                timerText.text = "CLEAR";
                helpText.text = LocalizationManager.T("linked_shield_clear");
            }
        }

        private void LockCamera()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            if (cameraFollow != null) { previousCameraFollowEnabled = cameraFollow.enabled; cameraFollow.enabled = false; }
            gameCamera.transform.position = new Vector3(0f, 0.8f, previousCameraPosition.z);
            // The four rooms and monitor stay visible, while incoming missiles
            // enter from off-screen instead of making the whole arena look tiny.
            gameCamera.orthographicSize = Mathf.Max(12.5f, 18.5f / Mathf.Max(0.1f, gameCamera.aspect));
            cameraLocked = true;
        }

        private void RestoreCamera()
        {
            if (!cameraLocked || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousCameraFollowEnabled;
            cameraLocked = false;
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != Phase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (IsOnline && !HasAuthority)
            {
                if (id != onlineManager?.LocalPlayerId) return;
                onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = EliminateRequestKind, Json = JsonUtility.ToJson(new EliminationState { PlayerId = id }) });
                return;
            }
            ConfirmElimination(id, IsOnline);
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
                onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = EliminatedKind, Json = JsonUtility.ToJson(new EliminationState { PlayerId = id }) });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null && !hiddenPlayers.Contains(player))
            {
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                player.ResetMotion();
                player.SetControlsEnabled(false);
                hiddenPlayers.Add(player);
                player.gameObject.SetActive(false);
            }
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void ApplyPendingEliminations()
        {
            foreach (string id in eliminated)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player != null && player.gameObject.activeSelf) ApplyEliminationVisual(player);
            }
        }

        private void ApplyEliminationVisual(PlayerController2D player)
        {
            if (hiddenPlayers.Contains(player)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.ResetMotion();
            player.SetControlsEnabled(false);
            hiddenPlayers.Add(player);
            player.gameObject.SetActive(false);
        }

        private void RestorePlayers()
        {
            if (restored) return;
            restored = true;
            for (int i = 0; i < hiddenPlayers.Count; i++) if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private bool AreAllEliminated()
        {
            if (participants.Count == 0) return false;
            foreach (string id in participants) if (!eliminated.Contains(id)) return false;
            return true;
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed) return;
            phase = Phase.Failed;
            restartRemaining = 3f;
            SetLocalControls(false);
            BroadcastState(true);
        }

        private void SetLocalControls(bool value)
        {
            PlayerController2D active = stageManager?.ActivePlayerTransform != null ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(value && !stageManager.IsDrawingMode && !IsEliminated(active));
            if (!IsOnline) stageManager?.RemotePlayerController?.SetControlsEnabled(value && !IsEliminated(stageManager.RemotePlayerController));
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminated.Contains(id);
        }

        private int FindRoom(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return -1;
            for (int i = 0; i < occupiedRooms; i++) if (roomPlayerIds[i] == playerId) return i;
            return -1;
        }

        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
            if (roster == null) return false;
            for (int i = 0; i < roster.Length; i++) if (roster[i] != null && roster[i].IsHost && roster[i].PlayerId == id) return true;
            return false;
        }

        private void BroadcastState(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            float[] shieldRemaining = new float[16];
            for (int i = 0; i < shieldRemaining.Length; i++) shieldRemaining[i] = Mathf.Max(0f, shieldEndsAt[i] - Time.time);
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence, Phase = (int)phase, Preparation = preparation, Remaining = remaining,
                Restart = restartRemaining, MapTargets = (int[])mapTargets.Clone(), MapSides = (int[])mapSides.Clone(),
                ShieldRemaining = shieldRemaining, RoomPlayerIds = (string[])roomPlayerIds.Clone(),
                EliminatedIds = new List<string>(eliminated).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = StateKind, Json = JsonUtility.ToJson(state) });
        }

        private void ApplyState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            Phase oldPhase = phase;
            phase = (Phase)Mathf.Clamp(state.Phase, 0, (int)Phase.Failed);
            preparation = state.Preparation;
            remaining = state.Remaining;
            restartRemaining = state.Restart;
            if (state.MapTargets != null && state.MapTargets.Length == 16) System.Array.Copy(state.MapTargets, mapTargets, 16);
            if (state.MapSides != null && state.MapSides.Length == 16) System.Array.Copy(state.MapSides, mapSides, 16);
            if (state.RoomPlayerIds != null)
            {
                System.Array.Clear(roomPlayerIds, 0, roomPlayerIds.Length);
                System.Array.Copy(state.RoomPlayerIds, roomPlayerIds, Mathf.Min(4, state.RoomPlayerIds.Length));
                occupiedRooms = 0;
                for (int i = 0; i < roomPlayerIds.Length; i++) if (!string.IsNullOrEmpty(roomPlayerIds[i])) occupiedRooms++;
                occupiedRooms = Mathf.Max(1, occupiedRooms);
            }
            if (state.ShieldRemaining != null)
                for (int i = 0; i < Mathf.Min(16, state.ShieldRemaining.Length); i++) shieldEndsAt[i] = Time.time + Mathf.Max(0f, state.ShieldRemaining[i]);
            if (state.EliminatedIds != null) for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            RefreshButtonLabels();
            if (oldPhase != phase) SetLocalControls(phase == Phase.Playing);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyState(JsonUtility.FromJson<NetworkState>(data.Json));
            else if (data.Kind == MissileKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplyMissile(JsonUtility.FromJson<MissileState>(data.Json));
            else if (data.Kind == ButtonRequestKind && HasAuthority)
            {
                ButtonRequest request = JsonUtility.FromJson<ButtonRequest>(data.Json);
                int room = FindRoom(data.PlayerId);
                if (request != null && room >= 0 && request.ButtonIndex / 4 == room) ActivateMappedShield(request.ButtonIndex);
            }
            else if (data.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(data.Json);
                if (request != null && request.PlayerId == data.PlayerId) ConfirmElimination(request.PlayerId, true);
            }
            else if (data.Kind == EliminatedKind && IsHostPlayer(data.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(data.Json);
                if (state != null) ApplyElimination(state.PlayerId);
            }
        }

        internal static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = "14-3 Shield Square";
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            return squareSprite;
        }
    }

    public sealed class StageLinkedShieldButton : MonoBehaviour
    {
        private StageLinkedShieldSurvivalController owner;
        private int index;
        private SpriteRenderer pad;
        private Transform padTransform;
        private Vector3 restScale;
        private TextMesh label;
        private float nextActivationAt;
        private Color baseColor;

        public static StageLinkedShieldButton Create(Transform parent, StageLinkedShieldSurvivalController owner, int index, int sourceSide, Vector2 position)
        {
            GameObject root = new GameObject("Linked Shield Button " + index);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            bool vertical = sourceSide == 1 || sourceSide == 3;
            Vector2 padSize = vertical ? new Vector2(0.48f, 1.25f) : new Vector2(1.25f, 0.48f);
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = padSize;
            trigger.isTrigger = true;
            StageLinkedShieldButton button = root.AddComponent<StageLinkedShieldButton>();
            button.owner = owner;
            button.index = index;
            GameObject visual = new GameObject("Pad");
            visual.transform.SetParent(root.transform, false);
            button.padTransform = visual.transform;
            button.pad = visual.AddComponent<SpriteRenderer>();
            Sprite buttonSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/shield-button");
            if (buttonSprite != null && buttonSprite.bounds.size.x > 0f && buttonSprite.bounds.size.y > 0f)
            {
                button.pad.sprite = buttonSprite;
                Vector2 artSize = new Vector2(1.42f, 0.72f);
                visual.transform.localScale = new Vector3(
                    artSize.x / buttonSprite.bounds.size.x,
                    artSize.y / buttonSprite.bounds.size.y,
                    1f);
                if (vertical) visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                visual.transform.localScale = new Vector3(padSize.x, padSize.y * 0.62f, 1f);
                button.pad.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
                StageEscortController.AddBoxOutline(root.transform, Vector2.zero, padSize, new Color(0.1f, 0.14f, 0.16f), 36);
            }
            button.restScale = visual.transform.localScale;
            button.pad.sortingOrder = 35;
            Vector3 labelPosition = sourceSide == 0 ? new Vector3(0f, -0.62f, -0.03f)
                : sourceSide == 1 ? new Vector3(-0.76f, 0f, -0.03f)
                : sourceSide == 2 ? new Vector3(0f, 0.62f, -0.03f)
                : new Vector3(0.76f, 0f, -0.03f);
            button.label = StageEscortController.CreateText(root.transform, "Mapping", labelPosition, 31, 0.075f, Color.white, 38);
            return button;
        }

        public void SetMapping(int targetRoom, int targetSide, Color color)
        {
            baseColor = Color.Lerp(Color.white, color, 0.58f);
            if (pad != null) pad.color = baseColor;
            if (label != null) label.text = "P" + (targetRoom + 1) + " " + StageLinkedShieldSurvivalController.SideArrow(targetSide);
        }

        public void Pulse()
        {
            if (pad != null) pad.color = new Color(0.38f, 1f, 0.5f, 1f);
            if (padTransform != null) padTransform.localScale = restScale * 0.84f;
            CancelInvoke(nameof(RestoreButton));
            Invoke(nameof(RestoreButton), 0.2f);
        }

        private void RestoreButton()
        {
            if (pad != null) pad.color = baseColor;
            if (padTransform != null) padTransform.localScale = restScale;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Time.time < nextActivationAt || other == null) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null || !player.ControlsEnabled) return;
            nextActivationAt = Time.time + 0.3f;
            owner?.RequestButton(player, index);
        }
    }

    public sealed class StageLinkedShieldWall : MonoBehaviour
    {
        private SpriteRenderer fill;

        public static StageLinkedShieldWall Create(Transform parent, Vector2 position, Vector2 size, Color accent)
        {
            GameObject root = new GameObject("One Second Steel Shield");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            StageLinkedShieldWall wall = root.AddComponent<StageLinkedShieldWall>();
            GameObject visual = new GameObject("Steel Fill");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            wall.fill = visual.AddComponent<SpriteRenderer>();
            wall.fill.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
            wall.fill.color = new Color(0.68f, 0.78f, 0.84f, 0.96f);
            wall.fill.sortingOrder = 70;
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f), 72);
            bool horizontal = size.x >= size.y;
            float length = horizontal ? size.x : size.y;
            for (float offset = -length * 0.42f; offset <= length * 0.42f; offset += 0.55f)
            {
                Vector2 a = horizontal
                    ? new Vector2(offset - 0.14f, -size.y * 0.34f)
                    : new Vector2(-size.x * 0.34f, offset - 0.14f);
                Vector2 b = horizontal
                    ? new Vector2(offset + 0.14f, size.y * 0.34f)
                    : new Vector2(size.x * 0.34f, offset + 0.14f);
                StageEscortController.AddLine(root.transform, a, b, 0.025f,
                    new Color(accent.r, accent.g, accent.b, 0.62f), 71);
            }
            wall.SetActive(false);
            return wall;
        }

        public void SetActive(bool value)
        {
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
        }
    }

    public sealed class StageLinkedShieldMissile : MonoBehaviour
    {
        private StageLinkedShieldSurvivalController owner;
        private int sequence;
        private int room;
        private int side;
        private Vector2 impact;
        private float speed;
        private bool authoritative;

        public static StageLinkedShieldMissile Create(Transform parent, StageLinkedShieldSurvivalController owner,
            int sequence, int room, int side, Vector2 position, Vector2 impact, float speed, float size, bool authoritative)
        {
            GameObject root = new GameObject("14-3 Incoming Missile " + sequence);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            Vector2 direction = (impact - position).normalized;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            root.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.7f, 1.5f);
            StageLinkedShieldMissile missile = root.AddComponent<StageLinkedShieldMissile>();
            missile.owner = owner; missile.sequence = sequence; missile.room = room; missile.side = side;
            missile.impact = impact; missile.speed = speed; missile.authoritative = authoritative;
            StageEscortController.AddFilledRect(root.transform, "Rocket", Vector2.zero, new Vector2(0.9f, 0.3f), new Color(0.9f, 0.16f, 0.1f), 120);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, new Vector2(0.9f, 0.3f), new Color(0.3f, 0.03f, 0.02f), 122);
            StageEscortController.AddLine(root.transform, new Vector2(-0.45f, 0f), new Vector2(-0.82f, 0f), 0.16f, new Color(1f, 0.72f, 0.12f), 121);
            return missile;
        }

        private void Update()
        {
            Vector2 current = transform.position;
            Vector2 next = Vector2.MoveTowards(current, impact, speed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, -0.15f);
            if ((next - impact).sqrMagnitude > 0.0025f) return;
            owner?.ShowMissileImpact(room, side, impact);
            if (authoritative) owner?.ResolveMissile(sequence, room, side, impact);
            Destroy(gameObject);
        }
    }

    public sealed class StageIncomingShieldMarker : MonoBehaviour
    {
        private float expiresAt;
        private SpriteRenderer fill;

        public static void Create(Transform parent, Vector2 position, Vector2 size, Color color, float seconds)
        {
            GameObject root = new GameObject("Incoming Side Warning");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            StageIncomingShieldMarker marker = root.AddComponent<StageIncomingShieldMarker>();
            marker.expiresAt = Time.time + Mathf.Max(0.2f, seconds);
            GameObject visual = new GameObject("Warning Glow");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            marker.fill = visual.AddComponent<SpriteRenderer>();
            marker.fill.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
            marker.fill.color = new Color(color.r, color.g, color.b, 0.28f);
            marker.fill.sortingOrder = 64;
        }

        private void Update()
        {
            float remaining = expiresAt - Time.time;
            if (remaining <= 0f) { Destroy(gameObject); return; }
            if (fill != null)
            {
                Color color = fill.color;
                color.a = 0.16f + Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.42f;
                fill.color = color;
            }
        }
    }
}
