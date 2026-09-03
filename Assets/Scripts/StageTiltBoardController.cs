using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class StageTiltBoardController : MonoBehaviour
    {
        private const string StageId = "8-3";
        private const string StateKind = "tilt_board_state";
        private const float RoundSeconds = 60f;
        private const float BallRadius = 0.42f;
        private const float PlayerLaneWidth = 4.2f;

        private enum BoardPhase { Active, RoundClear, Failed, Complete }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Round;
            public int Phase;
            public int Variant;
            public float Remaining;
            public int FilledMask;
            public int SunkMask;
            public Vector2 MagneticField;
            public Vector2[] Balls;
            public Vector2[] Velocities;
        }

        private sealed class BallState
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public bool Sunk;
        }

        private sealed class HoleState
        {
            public Vector2 Position;
            public SpriteRenderer Core;
            public SpriteRenderer Ring;
            public bool Filled;
        }

        private sealed class MazeWall
        {
            public Vector2 Center;
            public Vector2 HalfSize;
            public float Rotation;
        }

        private readonly List<Vector2> polygon = new List<Vector2>(4);
        private readonly List<Vector2> outerPolygon = new List<Vector2>(4);
        private readonly List<BallState> balls = new List<BallState>(3);
        private readonly List<HoleState> holes = new List<HoleState>(3);
        private readonly List<MazeWall> mazeWalls = new List<MazeWall>();
        private readonly Dictionary<Rigidbody2D, float> originalGravity = new Dictionary<Rigidbody2D, float>();
        private readonly Dictionary<PlayerController2D, int> assignedEdges = new Dictionary<PlayerController2D, int>();
        private readonly Dictionary<PlayerController2D, TiltBoardMagnetVisual> magnetVisuals =
            new Dictionary<PlayerController2D, TiltBoardMagnetVisual>();
        private readonly List<TiltBoardMagneticLinkVisual> magneticLinks =
            new List<TiltBoardMagneticLinkVisual>(12);

        private StageManager stageManager;
        private StageLoader stageLoader;
        private OnlineManager onlineManager;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private Transform arenaRoot;
        private Transform boardFace;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private Vector2 boardCenter = new Vector2(0f, -0.2f);
        private Vector2 magneticField;
        private float remaining;
        private float transitionRemaining;
        private float nextBroadcastAt;
        private int round = 1;
        private int layoutVariant;
        private int filledMask;
        private int sequence;
        private int receivedSequence;
        private int playerCount;
        private bool initialized;
        private bool cameraCaptured;
        private bool previousCameraFollowEnabled;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private BoardPhase phase;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkState;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkState;
            RestorePlayers();
            RestoreCamera();
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

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject obsolete = transform.GetChild(i).gameObject;
                obsolete.SetActive(false);
                Destroy(obsolete);
            }

            initialized = true;
            CaptureCamera();
            BeginRound(1, HasAuthority ? Random.Range(0, 6) : 0);
        }

        private void Update()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;
            ConfigureCamera();
            PreparePlayers();
            AnimateBoard();

            if (!HasAuthority)
            {
                if (phase == BoardPhase.Active) remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                RefreshMonitor();
                return;
            }

            switch (phase)
            {
                case BoardPhase.Active:
                    remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    CalculateMagneticField();
                    SimulateBalls(Time.deltaTime);
                    if (AllBallsSunk()) ClearRound();
                    else if (remaining <= 0f) FailRound();
                    break;
                case BoardPhase.RoundClear:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(round + 1, Random.Range(0, 6));
                    break;
                case BoardPhase.Failed:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(round, Random.Range(0, 6));
                    break;
            }

            BroadcastState();
            RefreshMonitor();
        }

        private void FixedUpdate()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;
            MoveActivePlayer();
        }

        private void BeginRound(int nextRound, int variant)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            layoutVariant = Mathf.Abs(variant) % 6;
            playerCount = Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
            phase = BoardPhase.Active;
            remaining = RoundSeconds;
            transitionRemaining = 0f;
            filledMask = 0;
            magneticField = Vector2.zero;
            BuildArena();
            StartCoroutine(PositionPlayersAfterBuild());
            BroadcastState(true);
            RefreshMonitor();
        }

        private void BuildArena()
        {
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
            }
            balls.Clear();
            holes.Clear();
            mazeWalls.Clear();
            polygon.Clear();
            outerPolygon.Clear();
            assignedEdges.Clear();
            magnetVisuals.Clear();
            magneticLinks.Clear();

            GameObject arena = new GameObject("8-3 Tilt Board Round " + round);
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;

            BuildPolygon();
            BuildOuterPolygon();
            CreateRoom();
            CreateBoard();
            CreatePuzzle();
            CreateMonitor();
            ConfigureCamera();
            stageLoader?.SetRuntimeSpawnPosition(GetAssignedLaneCenter(0));
        }

        private void BuildPolygon()
        {
            if (playerCount >= 4)
            {
                polygon.Add(boardCenter + new Vector2(-7f, -6f));
                polygon.Add(boardCenter + new Vector2(7f, -6f));
                polygon.Add(boardCenter + new Vector2(7f, 6f));
                polygon.Add(boardCenter + new Vector2(-7f, 6f));
            }
            else if (playerCount == 3)
            {
                polygon.Add(boardCenter + new Vector2(0f, 6.5f));
                polygon.Add(boardCenter + new Vector2(-9f, -5.5f));
                polygon.Add(boardCenter + new Vector2(9f, -5.5f));
            }
            else
            {
                polygon.Add(boardCenter + new Vector2(-10.5f, -4.8f));
                polygon.Add(boardCenter + new Vector2(10.5f, -4.8f));
                polygon.Add(boardCenter + new Vector2(10.5f, 4.8f));
                polygon.Add(boardCenter + new Vector2(-10.5f, 4.8f));
            }
        }

        private void BuildOuterPolygon()
        {
            outerPolygon.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                int previous = (i - 1 + polygon.Count) % polygon.Count;
                Vector2 previousDirection = (polygon[i] - polygon[previous]).normalized;
                Vector2 currentDirection = (polygon[(i + 1) % polygon.Count] - polygon[i]).normalized;
                Vector2 previousOutward = new Vector2(previousDirection.y, -previousDirection.x);
                Vector2 currentOutward = new Vector2(currentDirection.y, -currentDirection.x);
                Vector2 previousPoint = polygon[i] + previousOutward * PlayerLaneWidth;
                Vector2 currentPoint = polygon[i] + currentOutward * PlayerLaneWidth;
                outerPolygon.Add(TryLineIntersection(
                    previousPoint, previousDirection,
                    currentPoint, currentDirection,
                    out Vector2 intersection)
                    ? intersection
                    : polygon[i] + (previousOutward + currentOutward).normalized * PlayerLaneWidth);
            }
        }

        private static bool TryLineIntersection(
            Vector2 firstPoint, Vector2 firstDirection,
            Vector2 secondPoint, Vector2 secondDirection,
            out Vector2 intersection)
        {
            float cross = firstDirection.x * secondDirection.y - firstDirection.y * secondDirection.x;
            if (Mathf.Abs(cross) < 0.0001f)
            {
                intersection = Vector2.zero;
                return false;
            }
            Vector2 delta = secondPoint - firstPoint;
            float t = (delta.x * secondDirection.y - delta.y * secondDirection.x) / cross;
            intersection = firstPoint + firstDirection * t;
            return true;
        }

        private void CreateRoom()
        {
            Bounds bounds = GetOuterPolygonBounds();
            StageEscortController.AddFilledRect(arenaRoot, "Wide Room", bounds.center,
                (Vector2)bounds.size + new Vector2(4f, 4f),
                new Color(0.96f, 0.91f, 0.72f, 0.34f), -60);
        }

        private void CreateBoard()
        {
            GameObject face = new GameObject("Tilting Board Face");
            face.transform.SetParent(arenaRoot, false);
            boardFace = face.transform;
            if (polygon.Count == 4)
            {
                Bounds bounds = GetPolygonBounds();
                StageEscortController.AddFilledRect(boardFace, "Board Fill", boardCenter,
                    bounds.size, new Color(0.38f, 0.68f, 0.78f, 0.34f), -25);
            }
            else
            {
                CreateTriangleFill(boardFace);
            }

            CreateEdgeLanes();
            CreatePolygonLine(arenaRoot, "Board Inner Frame", polygon, 0.16f,
                new Color(0.04f, 0.25f, 0.34f, 0.9f), 9, true);
            CreatePolygonLine(arenaRoot, "Player Lane Outer Frame", outerPolygon, 0.2f,
                new Color(0.04f, 0.25f, 0.34f, 0.82f), 8, true);
        }

        private void CreateEdgeLanes()
        {
            Color[] colors =
            {
                new Color(0.94f, 0.36f, 0.22f, 0.5f),
                new Color(0.22f, 0.62f, 0.94f, 0.5f),
                new Color(0.96f, 0.72f, 0.18f, 0.5f),
                new Color(0.48f, 0.72f, 0.3f, 0.5f)
            };
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 from = polygon[i];
                Vector2 to = polygon[(i + 1) % polygon.Count];
                Vector2 outerTo = outerPolygon[(i + 1) % outerPolygon.Count];
                Vector2 outerFrom = outerPolygon[i];
                CreateFilledQuad(arenaRoot, "Player Lane " + (i + 1),
                    from, to, outerTo, outerFrom, colors[i % colors.Length], 5);
            }
        }

        private static void CreateFilledQuad(
            Transform parent, string name,
            Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color color, int order)
        {
            GameObject fill = new GameObject(name);
            fill.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = new[] { (Vector3)a, (Vector3)b, (Vector3)c, (Vector3)d };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.colors = new[] { color, color, color, color };
            MeshFilter filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            renderer.sortingOrder = order;
        }

        private void CreateTriangleFill(Transform parent)
        {
            GameObject fill = new GameObject("Triangle Board Fill");
            fill.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = "8-3 Triangle Board" };
            mesh.vertices = new[]
            {
                (Vector3)polygon[0], (Vector3)polygon[1], (Vector3)polygon[2]
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.colors = new[]
            {
                new Color(0.42f, 0.74f, 0.82f, 0.34f),
                new Color(0.28f, 0.58f, 0.7f, 0.34f),
                new Color(0.34f, 0.66f, 0.76f, 0.34f)
            };
            MeshFilter filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            renderer.sortingOrder = -25;
        }

        private void CreatePuzzle()
        {
            if (round == 1)
            {
                Vector2[] candidates =
                {
                    BoardPoint(-0.58f, 0.42f), BoardPoint(0.58f, 0.42f),
                    BoardPoint(-0.62f, -0.42f), BoardPoint(0.62f, -0.42f),
                    BoardPoint(0f, 0.58f), BoardPoint(0f, -0.56f)
                };
                CreateHole(candidates[layoutVariant], 0);
                CreateBall(BoardPoint(0f, 0f), 0);
            }
            else if (round == 2)
            {
                Vector2 start = playerCount == 3 ? BoardPoint(-0.52f, -0.5f) : BoardPoint(-0.72f, -0.58f);
                Vector2 goal = playerCount == 3 ? BoardPoint(0f, 0.68f) : BoardPoint(0.72f, 0.58f);
                CreateHole(goal, 0);
                CreateBall(start, 0);
                CreateMaze();
            }
            else
            {
                CreateHole(BoardPoint(-0.58f, 0.45f), 0);
                CreateHole(BoardPoint(0.58f, 0.45f), 1);
                CreateHole(BoardPoint(0f, -0.55f), 2);
                CreateBall(BoardPoint(-0.38f, -0.1f), 0);
                CreateBall(BoardPoint(0f, 0.2f), 1);
                CreateBall(BoardPoint(0.38f, -0.1f), 2);
            }
        }

        private Vector2 BoardPoint(float normalizedX, float normalizedY)
        {
            float halfWidth = playerCount == 4 ? 6.1f : playerCount == 3 ? 6.8f : 9.1f;
            float halfHeight = playerCount == 4 ? 5.1f : playerCount == 3 ? 4.3f : 3.8f;
            Vector2 point = boardCenter + new Vector2(normalizedX * halfWidth, normalizedY * halfHeight);
            return KeepInsidePolygon(point, 0.85f);
        }

        private void CreateMaze()
        {
            if (playerCount <= 2)
            {
                AddMazeWall(BoardPoint(-0.58f, -0.18f), new Vector2(0.62f, 5.35f));
                AddMazeWall(BoardPoint(-0.28f, 0.2f), new Vector2(0.62f, 5.1f));
                AddMazeWall(BoardPoint(0.02f, -0.2f), new Vector2(0.62f, 5.25f));
                AddMazeWall(BoardPoint(0.32f, 0.2f), new Vector2(0.62f, 5.1f));
                AddMazeWall(BoardPoint(0.6f, -0.18f), new Vector2(0.62f, 5.35f));
            }
            else if (playerCount == 3)
            {
                // A triangular zig-zag uses all three sides of the board.
                AddMazeWall(BoardPoint(-0.34f, -0.18f), new Vector2(5.7f, 0.58f), 52f);
                AddMazeWall(BoardPoint(0.34f, -0.18f), new Vector2(5.7f, 0.58f), -52f);
                AddMazeWall(BoardPoint(0f, 0.18f), new Vector2(5.2f, 0.58f), 0f);
                AddMazeWall(BoardPoint(-0.12f, 0.43f), new Vector2(3.3f, 0.52f), 52f);
                AddMazeWall(BoardPoint(0.12f, 0.43f), new Vector2(3.3f, 0.52f), -52f);
            }
            else
            {
                // The four-player square gets a broad spiral instead of the
                // long two-player slalom.
                AddMazeWall(BoardPoint(-0.46f, 0.02f), new Vector2(0.62f, 7.8f));
                AddMazeWall(BoardPoint(0f, 0.48f), new Vector2(6.4f, 0.62f));
                AddMazeWall(BoardPoint(0.46f, 0.02f), new Vector2(0.62f, 7.8f));
                AddMazeWall(BoardPoint(0.08f, -0.48f), new Vector2(5.3f, 0.62f));
                AddMazeWall(BoardPoint(-0.02f, 0f), new Vector2(0.58f, 3.8f));
            }
        }

        private void AddMazeWall(Vector2 position, Vector2 size, float rotation = 0f)
        {
            mazeWalls.Add(new MazeWall { Center = position, HalfSize = size * 0.5f, Rotation = rotation });
            GameObject wall = new GameObject("Maze Wall");
            wall.transform.SetParent(arenaRoot, false);
            wall.transform.position = position;
            wall.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = new Color(0.86f, 0.48f, 0.18f, 0.88f);
            renderer.sortingOrder = 12;
        }

        private void CreateBall(Vector2 position, int index)
        {
            GameObject obj = new GameObject("Board Ball " + (index + 1));
            obj.transform.SetParent(arenaRoot, false);
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * (BallRadius * 2f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            Color[] colors =
            {
                new Color(0.95f, 0.25f, 0.14f), new Color(0.95f, 0.7f, 0.08f), new Color(0.48f, 0.3f, 0.9f)
            };
            renderer.color = colors[index % colors.Length];
            renderer.sortingOrder = 22;
            obj.AddComponent<TiltBoardMagneticBallVisual>().Configure(renderer.color);
            balls.Add(new BallState { Transform = obj.transform, Renderer = renderer, Position = position });
        }

        private void CreateHole(Vector2 position, int index)
        {
            GameObject root = new GameObject("Board Hole " + (index + 1));
            root.transform.SetParent(arenaRoot, false);
            root.transform.position = position;
            GameObject ringObj = new GameObject("Hole Ring");
            ringObj.transform.SetParent(root.transform, false);
            ringObj.transform.localScale = Vector3.one * 1.25f;
            SpriteRenderer ring = ringObj.AddComponent<SpriteRenderer>();
            ring.sprite = DoodleRuntimeAssets.CircleSprite;
            ring.color = new Color(0.05f, 0.24f, 0.3f, 0.92f);
            ring.sortingOrder = 17;
            GameObject coreObj = new GameObject("Hole Core");
            coreObj.transform.SetParent(root.transform, false);
            coreObj.transform.localScale = Vector3.one * 0.88f;
            SpriteRenderer core = coreObj.AddComponent<SpriteRenderer>();
            core.sprite = DoodleRuntimeAssets.CircleSprite;
            core.color = new Color(0.015f, 0.025f, 0.03f, 0.98f);
            core.sortingOrder = 18;
            holes.Add(new HoleState { Position = position, Core = core, Ring = ring });
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("8-3 Tilt Board Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.position = new Vector3(0f, GetOuterPolygonBounds().max.y + 1.15f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(12.5f, 1.8f), 40);
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main",
                new Vector3(0f, 0.28f, -0.03f), 54, 0.115f, new Color(0.04f, 0.32f, 0.46f), 44);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub",
                new Vector3(0f, -0.35f, -0.04f), 42, 0.082f, new Color(0.65f, 0.2f, 0.06f), 45);
        }

        private void PreparePlayers()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform active = stageManager != null ? stageManager.ActivePlayerTransform : null;
            for (int i = 0; i < players.Length; i++)
            {
                if (!assignedEdges.ContainsKey(players[i]))
                {
                    int slot = ResolvePlayerSlot(players[i], i, players.Length);
                    assignedEdges.Add(players[i], GetAssignedEdge(slot));
                }
                Rigidbody2D body = players[i].GetComponent<Rigidbody2D>();
                if (body == null) continue;
                if (!originalGravity.ContainsKey(body)) originalGravity.Add(body, body.gravityScale);
                body.gravityScale = 0f;
                players[i].SetControlsEnabled(false);
                if (!IsOnline && assignedEdges.TryGetValue(players[i], out int assignedEdge))
                {
                    body.position = ClampToAssignedLane(body.position, assignedEdge, players[i]);
                    if (players[i].transform != active) body.linearVelocity = Vector2.zero;
                }

                if (!magnetVisuals.ContainsKey(players[i]) && arenaRoot != null)
                {
                    GameObject effect = new GameObject("Player Magnet Field " + (assignedEdges[players[i]] + 1));
                    effect.transform.SetParent(arenaRoot, false);
                    TiltBoardMagnetVisual visual = effect.AddComponent<TiltBoardMagnetVisual>();
                    visual.Configure(players[i].transform, assignedEdges[players[i]]);
                    magnetVisuals.Add(players[i], visual);
                }
                PlayerAbilityController ability = players[i].GetComponent<PlayerAbilityController>();
                float ink = ability != null ? Mathf.Max(0f, ability.CurrentProfile.TotalInk) : 0f;
                if (magnetVisuals.TryGetValue(players[i], out TiltBoardMagnetVisual magnet))
                    magnet.SetStrength(Mathf.Clamp01(ink / 500f));
                EnsureMagneticLinks(players[i], assignedEdges[players[i]], ink);
            }
        }

        private void EnsureMagneticLinks(PlayerController2D player, int edge, float ink)
        {
            for (int ballIndex = 0; ballIndex < balls.Count; ballIndex++)
            {
                Transform ball = balls[ballIndex].Transform;
                TiltBoardMagneticLinkVisual link = null;
                for (int i = 0; i < magneticLinks.Count; i++)
                {
                    if (magneticLinks[i] != null && magneticLinks[i].Matches(player.transform, ball))
                    {
                        link = magneticLinks[i];
                        break;
                    }
                }
                if (link == null && arenaRoot != null)
                {
                    GameObject linkObject = new GameObject("Magnetic Attraction Link");
                    linkObject.transform.SetParent(arenaRoot, false);
                    link = linkObject.AddComponent<TiltBoardMagneticLinkVisual>();
                    link.Configure(player.transform, ball, edge);
                    magneticLinks.Add(link);
                }
                if (link != null) link.SetStrength(Mathf.Clamp01(ink / 500f));
            }
        }

        private void MoveActivePlayer()
        {
            Transform active = stageManager != null ? stageManager.ActivePlayerTransform : null;
            if (active == null || stageManager.IsDrawingMode) return;
            Rigidbody2D body = active.GetComponent<Rigidbody2D>();
            if (body == null) return;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            Vector2 input = new Vector2(horizontal, vertical);
            Vector2 candidate = body.position + input.normalized * (5.2f * Time.fixedDeltaTime);
            PlayerController2D controller = active.GetComponent<PlayerController2D>();
            if (controller != null && assignedEdges.TryGetValue(controller, out int edge))
                body.position = ClampToAssignedLane(candidate, edge, controller);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private IEnumerator PositionPlayersAfterBuild()
        {
            yield return null;
            yield return new WaitForFixedUpdate();
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                int slot = ResolvePlayerSlot(players[i], i, players.Length);
                int edge = GetAssignedEdge(slot);
                assignedEdges[players[i]] = edge;
                Vector2 position = GetAssignedLaneCenter(edge);
                Rigidbody2D body = players[i].GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                    body.gravityScale = 0f;
                }
                else players[i].transform.position = position;
            }
            Physics2D.SyncTransforms();
        }

        private void CalculateMagneticField()
        {
            Vector2 target = Vector2.ClampMagnitude(CalculateMagneticForce(boardCenter), 1f);
            magneticField = Vector2.MoveTowards(magneticField, target, Time.deltaTime * 2.4f);
        }

        private static Vector2 CalculateMagneticForce(Vector2 point)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector2 force = Vector2.zero;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerAbilityController ability = players[i].GetComponent<PlayerAbilityController>();
                float ink = ability != null ? Mathf.Max(0f, ability.CurrentProfile.TotalInk) : 0f;
                Vector2 delta = (Vector2)players[i].transform.position - point;
                float distance = Mathf.Max(0.8f, delta.magnitude);
                // INK is deliberately dominant. Direction depends on where the
                // character stands, but proximity never lets a light character
                // overpower a distant high-INK character.
                float inkStrength = Mathf.Pow(ink / 350f, 2.35f);
                force += delta / distance * inkStrength;
            }
            return Vector2.ClampMagnitude(force, 1.6f);
        }

        private void SimulateBalls(float deltaTime)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                BallState ball = balls[i];
                if (ball.Sunk) continue;
                Vector2 attraction = CalculateMagneticForce(ball.Position);
                ball.Velocity += attraction * (7.4f * deltaTime);
                ball.Velocity *= Mathf.Exp(-0.52f * deltaTime);
                ball.Velocity = Vector2.ClampMagnitude(ball.Velocity, 7.2f);
                Vector2 previous = ball.Position;
                Vector2 next = KeepInsidePolygon(previous + ball.Velocity * deltaTime, BallRadius);
                if ((next - (previous + ball.Velocity * deltaTime)).sqrMagnitude > 0.0001f)
                    ball.Velocity *= -0.32f;
                ResolveMaze(ref next, ref ball.Velocity);
                ball.Position = next;
                ball.Transform.position = next;
                ball.Transform.Rotate(0f, 0f, -ball.Velocity.x * 18f * deltaTime);
                TrySinkBall(ball);
            }
        }

        private void ResolveMaze(ref Vector2 position, ref Vector2 velocity)
        {
            for (int i = 0; i < mazeWalls.Count; i++)
            {
                MazeWall wall = mazeWalls[i];
                Quaternion inverse = Quaternion.Euler(0f, 0f, -wall.Rotation);
                Vector2 local = inverse * (position - wall.Center);
                Vector2 localVelocity = inverse * velocity;
                Vector2 half = wall.HalfSize + Vector2.one * BallRadius;
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y) continue;
                float left = local.x + half.x;
                float right = half.x - local.x;
                float bottom = local.y + half.y;
                float top = half.y - local.y;
                float minimum = Mathf.Min(left, right, bottom, top);
                if (minimum == left) { local.x = -half.x; localVelocity.x = -Mathf.Abs(localVelocity.x) * 0.4f; }
                else if (minimum == right) { local.x = half.x; localVelocity.x = Mathf.Abs(localVelocity.x) * 0.4f; }
                else if (minimum == bottom) { local.y = -half.y; localVelocity.y = -Mathf.Abs(localVelocity.y) * 0.4f; }
                else { local.y = half.y; localVelocity.y = Mathf.Abs(localVelocity.y) * 0.4f; }
                Quaternion rotation = Quaternion.Euler(0f, 0f, wall.Rotation);
                position = wall.Center + (Vector2)(rotation * local);
                velocity = rotation * localVelocity;
            }
        }

        private void TrySinkBall(BallState ball)
        {
            for (int i = 0; i < holes.Count; i++)
            {
                if (holes[i].Filled || Vector2.Distance(ball.Position, holes[i].Position) > 0.68f) continue;
                ball.Sunk = true;
                ball.Transform.gameObject.SetActive(false);
                holes[i].Filled = true;
                filledMask |= 1 << i;
                holes[i].Core.color = new Color(0.18f, 0.76f, 0.3f, 0.92f);
                holes[i].Ring.color = new Color(0.08f, 0.5f, 0.2f, 0.88f);
                GameSfx.PlayAt(SfxId.CoinCollect, holes[i].Position);
                return;
            }
        }

        private bool AllBallsSunk()
        {
            if (balls.Count == 0) return false;
            for (int i = 0; i < balls.Count; i++) if (!balls[i].Sunk) return false;
            return true;
        }

        private void ClearRound()
        {
            if (phase != BoardPhase.Active) return;
            GameSfx.Play(SfxId.GoalReached);
            if (round >= 3)
            {
                phase = BoardPhase.Complete;
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            phase = BoardPhase.RoundClear;
            transitionRemaining = 1.8f;
            BroadcastState(true);
        }

        private void FailRound()
        {
            if (phase != BoardPhase.Active) return;
            phase = BoardPhase.Failed;
            transitionRemaining = 2.2f;
            GameSfx.Play(SfxId.PlayerDeath, 0.7f);
            BroadcastState(true);
        }

        private void AnimateBoard()
        {
            if (boardFace != null) boardFace.localRotation = Quaternion.identity;
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null || monitorSub == null) return;
            switch (phase)
            {
                case BoardPhase.Active:
                    monitorMain.text = LocalizationManager.Format("tilt_board_monitor", round, remaining);
                    monitorSub.text = round == 2
                        ? LocalizationManager.T("tilt_board_maze")
                        : LocalizationManager.Format("tilt_board_progress", CountBits(filledMask), holes.Count);
                    break;
                case BoardPhase.RoundClear:
                    monitorMain.text = LocalizationManager.Format("tilt_board_round_clear", round);
                    monitorSub.text = LocalizationManager.T("tilt_board_hint");
                    break;
                case BoardPhase.Failed:
                    monitorMain.text = LocalizationManager.T("tilt_board_timeout");
                    monitorSub.text = LocalizationManager.T("tilt_board_hint");
                    break;
            }
        }

        private int GetAssignedEdge(int slot)
        {
            if (polygon.Count == 4 && playerCount <= 2) return slot % 2 == 0 ? 0 : 2;
            return Mathf.Clamp(slot, 0, polygon.Count - 1);
        }

        private Vector2 GetAssignedLaneCenter(int edge)
        {
            int index = Mathf.Clamp(edge, 0, polygon.Count - 1);
            Vector2 innerCenter = (polygon[index] + polygon[(index + 1) % polygon.Count]) * 0.5f;
            Vector2 outerCenter = (outerPolygon[index] + outerPolygon[(index + 1) % outerPolygon.Count]) * 0.5f;
            return (innerCenter + outerCenter) * 0.5f;
        }

        private Vector2 ClampToAssignedLane(Vector2 point, int edge, PlayerController2D player)
        {
            int index = Mathf.Clamp(edge, 0, polygon.Count - 1);
            Vector2 from = polygon[index];
            Vector2 to = polygon[(index + 1) % polygon.Count];
            Vector2 tangent = (to - from).normalized;
            Vector2 outward = new Vector2(tangent.y, -tangent.x);
            Vector2 center = (from + to) * 0.5f;
            Vector2 relative = point - center;
            GetPlayerProjection(player, tangent, outward,
                out float minimumTangent, out float maximumTangent,
                out float minimumOutward, out float maximumOutward);
            const float padding = 0.14f;
            float minimumAcross = -minimumOutward + padding;
            float maximumAcross = PlayerLaneWidth - maximumOutward - padding;
            float across = minimumAcross <= maximumAcross
                ? Mathf.Clamp(Vector2.Dot(relative, outward), minimumAcross, maximumAcross)
                : PlayerLaneWidth * 0.5f;

            // The outer frame is wider than the inner frame at the corners.
            // Clamp against the actual trapezoid cross-section at the body's
            // innermost point, not against the shorter inner edge everywhere.
            float inwardBodyDepth = Mathf.Clamp(across + minimumOutward, 0f, PlayerLaneWidth);
            float crossSection = inwardBodyDepth / PlayerLaneWidth;
            Vector2 crossFrom = Vector2.Lerp(from, outerPolygon[index], crossSection);
            Vector2 crossTo = Vector2.Lerp(to, outerPolygon[(index + 1) % outerPolygon.Count], crossSection);
            float firstAlong = Vector2.Dot(crossFrom - center, tangent);
            float secondAlong = Vector2.Dot(crossTo - center, tangent);
            float minimumAlong = Mathf.Min(firstAlong, secondAlong) - minimumTangent + padding;
            float maximumAlong = Mathf.Max(firstAlong, secondAlong) - maximumTangent - padding;
            float along = minimumAlong <= maximumAlong
                ? Mathf.Clamp(Vector2.Dot(relative, tangent), minimumAlong, maximumAlong)
                : (minimumAlong + maximumAlong) * 0.5f;
            return center + tangent * along + outward * across;
        }

        private static void GetPlayerProjection(
            PlayerController2D player, Vector2 tangent, Vector2 outward,
            out float minimumTangent, out float maximumTangent,
            out float minimumOutward, out float maximumOutward)
        {
            minimumTangent = minimumOutward = -0.45f;
            maximumTangent = maximumOutward = 0.45f;
            if (player == null) return;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
            bool found = false;
            Vector2 root = player.transform.position;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                Bounds bounds = collider.bounds;
                Vector2 offset = (Vector2)bounds.center - root;
                float tangentExtent = Mathf.Abs(tangent.x) * bounds.extents.x
                    + Mathf.Abs(tangent.y) * bounds.extents.y;
                float outwardExtent = Mathf.Abs(outward.x) * bounds.extents.x
                    + Mathf.Abs(outward.y) * bounds.extents.y;
                float minT = Vector2.Dot(offset, tangent) - tangentExtent;
                float maxT = Vector2.Dot(offset, tangent) + tangentExtent;
                float minO = Vector2.Dot(offset, outward) - outwardExtent;
                float maxO = Vector2.Dot(offset, outward) + outwardExtent;
                if (!found)
                {
                    minimumTangent = minT;
                    maximumTangent = maxT;
                    minimumOutward = minO;
                    maximumOutward = maxO;
                    found = true;
                }
                else
                {
                    minimumTangent = Mathf.Min(minimumTangent, minT);
                    maximumTangent = Mathf.Max(maximumTangent, maxT);
                    minimumOutward = Mathf.Min(minimumOutward, minO);
                    maximumOutward = Mathf.Max(maximumOutward, maxO);
                }
            }
        }

        private int ResolvePlayerSlot(PlayerController2D player, int fallback, int total)
        {
            if (!IsOnline || onlineManager?.CurrentLobby?.Players == null)
                return Mathf.Clamp(fallback, 0, total - 1);
            string id = stageManager.GetOnlinePlayerId(player);
            OnlinePlayerInfo[] lobbyPlayers = onlineManager.CurrentLobby.Players;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].PlayerId == id)
                    return Mathf.Clamp(i, 0, total - 1);
            return Mathf.Clamp(fallback, 0, total - 1);
        }

        private Vector2 ProjectToTrack(Vector2 point)
        {
            Vector2 best = polygon[0];
            float bestDistance = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 candidate = ClosestPointOnSegment(point, polygon[i], polygon[(i + 1) % polygon.Count]);
                float distance = (candidate - point).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        private Vector2 KeepInsidePolygon(Vector2 point, float inset)
        {
            Vector2 edge = ProjectToTrack(point);
            Vector2 inward = (boardCenter - edge).normalized;
            if (!IsInsidePolygon(point)) return edge + inward * inset;
            float edgeDistance = Vector2.Distance(point, edge);
            return edgeDistance < inset
                ? point + inward * (inset - edgeDistance)
                : point;
        }

        private bool IsInsidePolygon(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float t = Vector2.Dot(point - a, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude);
            return a + segment * Mathf.Clamp01(t);
        }

        private Bounds GetPolygonBounds()
        {
            Bounds bounds = new Bounds(polygon[0], Vector3.zero);
            for (int i = 1; i < polygon.Count; i++) bounds.Encapsulate(polygon[i]);
            return bounds;
        }

        private Bounds GetOuterPolygonBounds()
        {
            Bounds bounds = new Bounds(outerPolygon[0], Vector3.zero);
            for (int i = 1; i < outerPolygon.Count; i++) bounds.Encapsulate(outerPolygon[i]);
            return bounds;
        }

        private static void CreatePolygonLine(Transform parent, string name, List<Vector2> points,
            float width, Color color, int order, bool loop)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Count;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        }

        private void CaptureCamera()
        {
            if (cameraCaptured) return;
            if (gameCamera == null) gameCamera = Camera.main;
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
            Bounds bounds = GetOuterPolygonBounds();
            float minimumY = bounds.min.y - 0.8f;
            float maximumY = bounds.max.y + 2.4f;
            float centerY = (minimumY + maximumY) * 0.5f;
            float heightSize = (maximumY - minimumY) * 0.5f + 0.35f;
            float widthSize = (bounds.size.x * 0.5f + 0.8f) / Mathf.Max(0.1f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, centerY, -10f);
            gameCamera.orthographicSize = Mathf.Max(heightSize, widthSize);
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousCameraFollowEnabled;
            cameraCaptured = false;
        }

        private void RestorePlayers()
        {
            foreach (KeyValuePair<Rigidbody2D, float> entry in originalGravity)
            {
                if (entry.Key != null) entry.Key.gravityScale = entry.Value;
            }
            originalGravity.Clear();
            Transform active = stageManager != null ? stageManager.ActivePlayerTransform : null;
            if (active != null) active.GetComponent<PlayerController2D>()?.SetControlsEnabled(true);
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            Vector2[] positions = new Vector2[balls.Count];
            Vector2[] velocities = new Vector2[balls.Count];
            int sunkMask = 0;
            for (int i = 0; i < balls.Count; i++)
            {
                positions[i] = balls[i].Position;
                velocities[i] = balls[i].Velocity;
                if (balls[i].Sunk) sunkMask |= 1 << i;
            }
            NetworkState state = new NetworkState
            {
                Sequence = ++sequence,
                Round = round,
                Phase = (int)phase,
                Variant = layoutVariant,
                Remaining = remaining,
                FilledMask = filledMask,
                SunkMask = sunkMask,
                MagneticField = magneticField,
                Balls = positions,
                Velocities = velocities
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
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind
                || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;
            if (state.Round != round || state.Variant != layoutVariant) BeginRound(state.Round, state.Variant);
            phase = (BoardPhase)state.Phase;
            remaining = state.Remaining;
            filledMask = state.FilledMask;
            magneticField = state.MagneticField;
            for (int i = 0; i < balls.Count && state.Balls != null && i < state.Balls.Length; i++)
            {
                balls[i].Position = state.Balls[i];
                balls[i].Velocity = state.Velocities != null && i < state.Velocities.Length ? state.Velocities[i] : Vector2.zero;
                balls[i].Sunk = (state.SunkMask & (1 << i)) != 0;
                balls[i].Transform.gameObject.SetActive(!balls[i].Sunk);
                balls[i].Transform.position = balls[i].Position;
            }
            for (int i = 0; i < holes.Count; i++)
            {
                holes[i].Filled = (filledMask & (1 << i)) != 0;
                holes[i].Core.color = holes[i].Filled
                    ? new Color(0.18f, 0.76f, 0.3f, 0.92f)
                    : new Color(0.015f, 0.025f, 0.03f, 0.98f);
                holes[i].Ring.color = holes[i].Filled
                    ? new Color(0.08f, 0.5f, 0.2f, 0.88f)
                    : new Color(0.05f, 0.24f, 0.3f, 0.92f);
            }
            RefreshMonitor();
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null) return false;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].IsHost && lobbyPlayers[i].PlayerId == playerId) return true;
            return false;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }
    }

    public sealed class TiltBoardMagnetVisual : MonoBehaviour
    {
        private readonly List<LineRenderer> arcs = new List<LineRenderer>(3);
        private Transform target;
        private float strength;
        private float phase;

        public void Configure(Transform followTarget, int colorIndex)
        {
            target = followTarget;
            phase = colorIndex * 1.37f;
            Color[] colors =
            {
                new Color(1f, 0.28f, 0.16f, 0.72f), new Color(0.18f, 0.58f, 1f, 0.72f),
                new Color(1f, 0.72f, 0.12f, 0.72f), new Color(0.36f, 0.82f, 0.26f, 0.72f)
            };
            Color color = colors[Mathf.Abs(colorIndex) % colors.Length];
            for (int ring = 0; ring < 3; ring++)
            {
                GameObject arcObject = new GameObject("Magnetic Field Arc " + ring);
                arcObject.transform.SetParent(transform, false);
                LineRenderer line = arcObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 18;
                line.startWidth = 0.055f;
                line.endWidth = 0.025f;
                line.numCapVertices = 4;
                line.numCornerVertices = 3;
                line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                line.startColor = color;
                line.endColor = color;
                line.sortingOrder = 28;
                float radius = 0.7f + ring * 0.28f;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = Mathf.Lerp(-145f, 145f, i / (float)(line.positionCount - 1)) * Mathf.Deg2Rad;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }
                arcs.Add(line);
            }
        }

        public void SetStrength(float value)
        {
            strength = Mathf.Clamp01(value);
        }

        private void LateUpdate()
        {
            if (target == null) { gameObject.SetActive(false); return; }
            transform.position = target.position;
            float pulse = 0.88f + Mathf.Sin(Time.unscaledTime * 4.2f + phase) * 0.08f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.2f, strength) * pulse;
            transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.unscaledTime * 1.8f + phase) * 9f);
            for (int i = 0; i < arcs.Count; i++)
            {
                if (arcs[i] == null) continue;
                arcs[i].widthMultiplier = Mathf.Lerp(0.55f, 2.5f, strength);
                Color arcColor = arcs[i].startColor;
                arcColor.a = Mathf.Lerp(0.16f, 0.82f, strength);
                arcs[i].startColor = arcColor;
                arcs[i].endColor = arcColor;
            }
        }
    }

    public sealed class TiltBoardMagneticBallVisual : MonoBehaviour
    {
        private Transform effectRoot;
        private float phase;

        public void Configure(Color color)
        {
            phase = Random.Range(0f, 6f);
            GameObject root = new GameObject("Magnetic Pull Ripples");
            root.transform.SetParent(transform, false);
            effectRoot = root.transform;
            for (int ring = 0; ring < 2; ring++)
            {
                GameObject ringObject = new GameObject("Pull Ripple " + ring);
                ringObject.transform.SetParent(effectRoot, false);
                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 22;
                line.startWidth = 0.035f;
                line.endWidth = 0.035f;
                line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                line.startColor = new Color(color.r, color.g, color.b, 0.48f - ring * 0.14f);
                line.endColor = line.startColor;
                line.sortingOrder = 21;
                float radius = 0.62f + ring * 0.2f;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i / (float)line.positionCount * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }
            }
        }

        private void Update()
        {
            if (effectRoot == null) return;
            float pulse = 0.92f + Mathf.Sin(Time.unscaledTime * 5f + phase) * 0.1f;
            effectRoot.localScale = Vector3.one * pulse;
            effectRoot.Rotate(0f, 0f, 22f * Time.unscaledDeltaTime);
        }
    }

    public sealed class TiltBoardMagneticLinkVisual : MonoBehaviour
    {
        private readonly List<Transform> sparks = new List<Transform>(5);
        private Transform player;
        private Transform ball;
        private LineRenderer line;
        private Color color;
        private float strength;
        private float phase;

        public bool Matches(Transform playerTarget, Transform ballTarget)
        {
            return player == playerTarget && ball == ballTarget;
        }

        public void Configure(Transform playerTarget, Transform ballTarget, int colorIndex)
        {
            player = playerTarget;
            ball = ballTarget;
            phase = colorIndex * 0.83f;
            Color[] colors =
            {
                new Color(1f, 0.3f, 0.18f), new Color(0.2f, 0.64f, 1f),
                new Color(1f, 0.76f, 0.14f), new Color(0.4f, 0.84f, 0.28f)
            };
            color = colors[Mathf.Abs(colorIndex) % colors.Length];
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 16;
            // The start is the ball side: make that end visibly denser and
            // taper toward the character so the pull direction reads at once.
            line.startWidth = 0.11f;
            line.endWidth = 0.022f;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.sortingOrder = 19;

            for (int i = 0; i < 5; i++)
            {
                GameObject spark = new GameObject("Pulling Spark " + i);
                spark.transform.SetParent(transform, false);
                spark.transform.localScale = Vector3.one * (0.09f + i % 2 * 0.035f);
                SpriteRenderer renderer = spark.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(color.r, color.g, color.b, 0.82f);
                renderer.sortingOrder = 20;
                sparks.Add(spark.transform);
            }
        }

        public void SetStrength(float value)
        {
            strength = Mathf.Clamp01(value);
        }

        private void LateUpdate()
        {
            bool visible = player != null && ball != null && ball.gameObject.activeInHierarchy && strength > 0.02f;
            if (line != null) line.enabled = visible;
            for (int i = 0; i < sparks.Count; i++)
                if (sparks[i] != null) sparks[i].gameObject.SetActive(visible);
            if (!visible) return;

            Vector2 start = ball.position;
            Vector2 end = player.position;
            Vector2 direction = end - start;
            Vector2 perpendicular = direction.sqrMagnitude > 0.01f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            Vector2 control = (start + end) * 0.5f
                + perpendicular * Mathf.Sin(Time.unscaledTime * 2.6f + phase) * 0.3f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                line.SetPosition(i, Quadratic(start, control, end, t));
            }
            float alpha = Mathf.Lerp(0.08f, 0.9f, strength);
            line.startColor = new Color(color.r, color.g, color.b, alpha);
            line.endColor = new Color(color.r, color.g, color.b, alpha * 0.18f);
            line.widthMultiplier = Mathf.Lerp(0.4f, 3.1f, strength);

            for (int i = 0; i < sparks.Count; i++)
            {
                bool sparkVisible = i < Mathf.CeilToInt(Mathf.Lerp(2f, sparks.Count, strength));
                sparks[i].gameObject.SetActive(sparkVisible);
                if (!sparkVisible) continue;
                float t = Mathf.Repeat(
                    Time.unscaledTime * Mathf.Lerp(0.45f, 1.15f, strength) + i / (float)sparks.Count + phase,
                    1f);
                sparks[i].position = Quadratic(start, control, end, t);
                float sparkSize = Mathf.Lerp(0.065f, 0.18f, strength) * Mathf.Lerp(1.25f, 0.65f, t);
                sparks[i].localScale = Vector3.one * sparkSize;
                SpriteRenderer renderer = sparks[i].GetComponent<SpriteRenderer>();
                if (renderer != null)
                    renderer.color = new Color(color.r, color.g, color.b,
                        Mathf.Lerp(alpha, alpha * 0.3f, t));
            }
        }

        private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }
    }
}
