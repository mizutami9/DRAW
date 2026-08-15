using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBlockBreakerController : MonoBehaviour
    {
        private const string StageId = "11-3";
        private const string KindState = "block_breaker_state";
        private const string KindEnemySpawn = "block_breaker_enemy_spawn";
        private const string KindEnemyDefeat = "block_breaker_enemy_defeat";
        private const string KindEnemyDefeatRequest = "block_breaker_enemy_defeat_request";
        private const string KindEliminateRequest = "block_breaker_eliminate_request";
        private const string KindEliminated = "block_breaker_eliminated";
        private const float ArenaHalfWidth = 22.5f;
        private const float FloorY = -6f;
        private const float IntroSeconds = 2.4f;
        private const float CountdownSeconds = 4f;

        private enum ChallengePhase { Intro, Countdown, Playing, Clear, Failed }

        [System.Serializable]
        private sealed class ChallengeState
        {
            public int Sequence;
            public int Phase;
            public float Remaining;
            public float PhaseRemaining;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class PlayerState { public string PlayerId; }

        [System.Serializable]
        private sealed class EnemyState
        {
            public int Sequence;
            public string EnemyId;
            public Vector2 Position;
            public float Direction;
            public float Speed;
        }

        private readonly struct LetterSegment
        {
            public readonly Vector2 From;
            public readonly Vector2 To;

            public LetterSegment(float fromX, float fromY, float toX, float toY)
            {
                From = new Vector2(fromX, fromY);
                To = new Vector2(toX, toY);
            }
        }

        private readonly List<StageBombBreakableWall> letterBlocks = new List<StageBombBreakableWall>();
        private readonly Dictionary<string, StageBlockBreakerEnemy> enemies = new Dictionary<string, StageBlockBreakerEnemy>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private TextMesh titleText;
        private TextMesh statusText;
        private TextMesh countText;
        private ChallengePhase phase = ChallengePhase.Intro;
        private float durationSeconds = 75f;
        private float remainingSeconds;
        private float phaseRemaining = IntroSeconds;
        private float nextBombAt;
        private float nextEnemyAt;
        private float failedRestartRemaining;
        private float nextStateAt;
        private int bombSequence;
        private int enemySequence;
        private int stateSequence;
        private int lastStateSequence;
        private int lastEnemySequence;
        private bool configured;
        private bool cameraWasEnabled;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private static Sprite squareSprite;
        private static Sprite circleSprite;
        private static Material logoLineMaterial;

        public void Configure(float seconds)
        {
            durationSeconds = Mathf.Clamp(seconds > 0f ? seconds : 75f, 30f, 240f);
            remainingSeconds = durationSeconds;
            configured = true;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
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
            if (cameraFollow != null) cameraFollow.enabled = cameraWasEnabled;
            if (gameCamera != null)
            {
                gameCamera.transform.position = previousCameraPosition;
                gameCamera.orthographicSize = previousCameraSize;
            }
        }

        private void Start()
        {
            if (!configured) Configure(75f);
            BuildArena();
            CaptureParticipants();
            SetLocalControls(false);
            LockCameraToArena();
            nextBombAt = 4f;
            nextEnemyAt = 13f;
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (IsOnlineActive() && !HasAuthority())
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phase == ChallengePhase.Playing) remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
                CheckFalls();
                RefreshDisplay();
                return;
            }

            BroadcastState();
            if (phase == ChallengePhase.Failed)
            {
                failedRestartRemaining -= Time.deltaTime;
                RefreshDisplay();
                if (failedRestartRemaining <= 0f) stageManager.Retry();
                return;
            }
            if (phase == ChallengePhase.Clear) return;

            CheckFalls();
            if (phase == ChallengePhase.Intro || phase == ChallengePhase.Countdown)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    if (phase == ChallengePhase.Intro)
                    {
                        phase = ChallengePhase.Countdown;
                        phaseRemaining = CountdownSeconds;
                    }
                    else
                    {
                        phase = ChallengePhase.Playing;
                        SetLocalControls(true);
                    }
                    BroadcastState(true);
                }
                RefreshDisplay();
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            UpdateAttackDirector();
            if (AreAllPlayersEliminated() || remainingSeconds <= 0f)
            {
                BeginFailure();
                return;
            }
            if (CountRemainingBlocks() == 0)
            {
                phase = ChallengePhase.Clear;
                SetLocalControls(false);
                BroadcastState(true);
                RefreshDisplay();
                stageManager.ClearStage();
                return;
            }
            RefreshDisplay();
        }

        public void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != ChallengePhase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (!IsOnlineActive()) participantIds.Add(id);
            if (IsOnlineActive())
            {
                if (id != onlineManager.LocalPlayerId) return;
                if (!HasAuthority())
                {
                    Send(KindEliminateRequest, new PlayerState { PlayerId = id });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id);
        }

        internal void NotifyEnemyDefeated(string enemyId)
        {
            if (!HasAuthority() || string.IsNullOrEmpty(enemyId)) return;
            RemoveEnemy(enemyId);
            Send(KindEnemyDefeat, new EnemyState { EnemyId = enemyId });
        }

        internal void RequestEnemyDefeat(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            if (HasAuthority())
            {
                NotifyEnemyDefeated(enemyId);
                return;
            }
            Send(KindEnemyDefeatRequest, new EnemyState { EnemyId = enemyId });
        }

        private void UpdateAttackDirector()
        {
            float elapsed = durationSeconds - remainingSeconds;
            float progress = Mathf.Clamp01(elapsed / durationSeconds);
            if (elapsed >= nextBombAt)
            {
                int portsPerSide = 1 + Mathf.FloorToInt(progress * 2.1f);
                for (int i = 0; i < portsPerSide; i++)
                {
                    SpawnBomb(true);
                    SpawnBomb(false);
                }
                nextBombAt = elapsed + Mathf.Lerp(3.1f, 1.4f, progress);
            }
            if (elapsed >= nextEnemyAt)
            {
                SpawnEnemy(enemySequence % 2 == 0);
                nextEnemyAt = elapsed + Random.Range(Mathf.Lerp(10f, 5.5f, progress), Mathf.Lerp(14f, 8f, progress));
            }
        }

        private void SpawnBomb(bool fromLeft)
        {
            int slot = bombSequence % 3;
            string id = "block_breaker_bomb_" + (++bombSequence);
            float fuseSeconds = Random.Range(3f, 7.01f);
            float bombSize = Random.Range(0.92f, 2.76f);
            Vector2 position = GetBombPortPosition(fromLeft, slot);
            GameObject bomb = IsOnlineActive() && syncManager != null
                ? syncManager.SpawnDropperBox(id, StageObjectType.Bomb, position, bombSize, 0f, fuseSeconds)
                : objectFactory != null
                    ? objectFactory.CreateDroppedBox(StageObjectType.Bomb, id, position, bombSize, transform, fuseSeconds)
                    : null;
            Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
            if (body != null)
            {
                body.linearVelocity = new Vector2(fromLeft ? Random.Range(6f, 8.5f) : Random.Range(-8.5f, -6f), Random.Range(2.1f, 4.2f));
                body.AddTorque(Random.Range(-1.8f, 1.8f), ForceMode2D.Impulse);
            }
        }

        private static Vector2 GetBombPortPosition(bool fromLeft, int slot)
        {
            float[] heights = { FloorY + 1.1f, FloorY + 2.75f, FloorY + 4.4f };
            return new Vector2(fromLeft ? -ArenaHalfWidth + 1.8f : ArenaHalfWidth - 1.8f, heights[Mathf.Clamp(slot, 0, 2)]);
        }

        private void SpawnEnemy(bool fromLeft)
        {
            EnemyState state = new EnemyState
            {
                Sequence = ++enemySequence,
                EnemyId = "block_breaker_enemy_" + enemySequence,
                Position = new Vector2(fromLeft ? -ArenaHalfWidth + 2.7f : ArenaHalfWidth - 2.7f, 9.25f),
                Direction = fromLeft ? 1f : -1f,
                Speed = Random.Range(2.3f, 3.4f)
            };
            ApplyEnemySpawn(state);
            Send(KindEnemySpawn, state);
        }

        private void ApplyEnemySpawn(EnemyState state)
        {
            if (state == null || string.IsNullOrEmpty(state.EnemyId) || enemies.ContainsKey(state.EnemyId)) return;
            lastEnemySequence = Mathf.Max(lastEnemySequence, state.Sequence);
            StageBlockBreakerEnemy enemy = StageBlockBreakerEnemy.Create(
                transform, this, state.EnemyId, state.Position, state.Direction, state.Speed);
            enemies[state.EnemyId] = enemy;
        }

        private void RemoveEnemy(string id)
        {
            if (!enemies.TryGetValue(id, out StageBlockBreakerEnemy enemy)) return;
            enemies.Remove(id);
            if (enemy != null) enemy.DefeatVisual();
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("11-3 Block Breaker Arena");
            arena.transform.SetParent(transform, false);
            CreateBoundary(arena.transform);
            CreateFloor(arena.transform);
            CreateBombPorts(arena.transform);
            CreateLetterBlocks(arena.transform);
            CreateStatusBoard(arena.transform);
        }

        private void CreateBoundary(Transform parent)
        {
            if (objectFactory == null) return;
            StageObjectData boundary = StageObjectFactory.CreateDefaultData(StageObjectType.StageBoundary, new Vector2(0f, 2f));
            boundary.objectId = "block_breaker_boundary";
            boundary.size = new Vector2(ArenaHalfWidth * 2f + 1.2f, 20f);
            boundary.pathThickness = 0.7f;
            objectFactory.Create(boundary, parent);
        }

        private void CreateFloor(Transform parent)
        {
            if (objectFactory == null) return;
            StageObjectData floor = StageObjectFactory.CreateDefaultData(StageObjectType.Platform, new Vector2(0f, FloorY));
            floor.objectId = "block_breaker_floor";
            // Extend slightly into both boundary colliders so neither the drawing
            // nor the physics surface leaves a visible corner seam.
            floor.size = new Vector2(ArenaHalfWidth * 2f + 0.1f, 0.75f);
            objectFactory.Create(floor, parent);
        }

        private void CreateBombPorts(Transform parent)
        {
            for (int side = 0; side < 2; side++)
            {
                bool fromLeft = side == 0;
                for (int slot = 0; slot < 3; slot++)
                {
                    Vector2 position = GetBombPortPosition(fromLeft, slot);
                    GameObject port = new GameObject(fromLeft ? "Left Bomb Port" : "Right Bomb Port");
                    port.transform.SetParent(parent, false);
                    port.transform.position = position;

                    GameObject rim = new GameObject("Crayon Launcher Rim");
                    rim.transform.SetParent(port.transform, false);
                    rim.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                    SpriteRenderer rimRenderer = rim.AddComponent<SpriteRenderer>();
                    rimRenderer.sprite = GetCircleSprite();
                    rimRenderer.color = new Color(0.15f, 0.12f, 0.2f, 1f);
                    rimRenderer.sortingOrder = 28;

                    GameObject opening = new GameObject("Launcher Opening");
                    opening.transform.SetParent(port.transform, false);
                    opening.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
                    SpriteRenderer openingRenderer = opening.AddComponent<SpriteRenderer>();
                    openingRenderer.sprite = GetCircleSprite();
                    openingRenderer.color = new Color(0.015f, 0.02f, 0.025f, 1f);
                    openingRenderer.sortingOrder = 29;

                    GameObject arrow = new GameObject("Launch Arrow");
                    arrow.transform.SetParent(port.transform, false);
                    arrow.transform.localPosition = new Vector3(fromLeft ? 0.8f : -0.8f, 0f, -0.02f);
                    arrow.transform.localRotation = Quaternion.Euler(0f, 0f, fromLeft ? 0f : 180f);
                    arrow.transform.localScale = new Vector3(0.78f, 0.18f, 1f);
                    SpriteRenderer arrowRenderer = arrow.AddComponent<SpriteRenderer>();
                    arrowRenderer.sprite = GetSquareSprite();
                    arrowRenderer.color = new Color(1f, 0.42f, 0.16f, 0.85f);
                    arrowRenderer.sortingOrder = 30;
                }
            }
        }

        private void CreateLetterBlocks(Transform parent)
        {
            const string text = "NICO DRAW";
            const float letterWidth = 3.1f;
            const float letterGap = 0.62f;
            const float wordGap = 1.2f;
            const float thickness = 0.58f;
            float totalWidth = 0f;
            for (int i = 0; i < text.Length; i++) totalWidth += text[i] == ' ' ? wordGap : letterWidth + letterGap;
            float cursor = -totalWidth * 0.5f;
            int index = 0;
            for (int c = 0; c < text.Length; c++)
            {
                char letter = text[c];
                if (letter == ' ')
                {
                    cursor += wordGap;
                    continue;
                }
                LetterSegment[] segments = GetLetterSegments(letter);
                Color logoColor = GetLogoLetterColor(letter);
                List<StageBombBreakableWall> currentLetterWalls = new List<StageBombBreakableWall>();
                for (int i = 0; i < segments.Length; i++)
                {
                    Vector2 from = segments[i].From + new Vector2(cursor, 4.25f);
                    Vector2 to = segments[i].To + new Vector2(cursor, 4.25f);
                    Vector2 delta = to - from;
                    StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.BreakableWall, (from + to) * 0.5f);
                    data.objectId = "nico_draw_block_" + index++;
                    data.size = new Vector2(delta.magnitude + thickness * 0.35f, thickness);
                    data.rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    data.actionStrength = 1f;
                    GameObject block = objectFactory.Create(data, parent);
                    AddLogoColorFill(block != null ? block.transform : null, data.size, logoColor);
                    StageBombBreakableWall wall = block != null ? block.GetComponent<StageBombBreakableWall>() : null;
                    if (wall != null)
                    {
                        wall.SetRequirementBadgeVisible(false);
                        letterBlocks.Add(wall);
                        currentLetterWalls.Add(wall);
                    }
                }
                if (letter == 'O')
                {
                    CreateLogoFace(parent, new Vector2(cursor + 1.4f, 6.6f), currentLetterWalls);
                }
                cursor += letterWidth + letterGap;
            }
        }

        private static Color GetLogoLetterColor(char letter)
        {
            switch (letter)
            {
                case 'N':
                case 'A':
                    return new Color(1f, 0.06f, 0.08f, 1f);
                case 'I':
                    return new Color(1f, 0.68f, 0.02f, 1f);
                case 'C':
                case 'W':
                    return new Color(0.02f, 0.68f, 0.2f, 1f);
                case 'R':
                    return new Color(0.42f, 0.1f, 0.76f, 1f);
                case 'D':
                case 'O':
                default:
                    return new Color(0.03f, 0.32f, 0.92f, 1f);
            }
        }

        private static void AddLogoColorFill(Transform block, Vector2 size, Color color)
        {
            if (block == null) return;
            GameObject fill = new GameObject("NICO DRAW Logo Color");
            fill.transform.SetParent(block, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(color.r, color.g, color.b, 0.58f);
            renderer.sortingOrder = 6;
        }

        private static LetterSegment[] GetLetterSegments(char letter)
        {
            LetterSegment top = new LetterSegment(0f, 4.7f, 2.8f, 4.7f);
            LetterSegment middle = new LetterSegment(0f, 2.35f, 2.8f, 2.35f);
            LetterSegment bottom = new LetterSegment(0f, 0f, 2.8f, 0f);
            LetterSegment left = new LetterSegment(0f, 0f, 0f, 4.7f);
            LetterSegment right = new LetterSegment(2.8f, 0f, 2.8f, 4.7f);
            switch (letter)
            {
                case 'N': return new[] { left, new LetterSegment(0f, 4.7f, 2.8f, 0f), right };
                case 'I': return new[] { top, new LetterSegment(1.4f, 0f, 1.4f, 4.7f), bottom };
                case 'C': return new[] { top, left, bottom };
                case 'O': return new[]
                {
                    new LetterSegment(0.65f, 4.7f, 2.15f, 4.7f),
                    new LetterSegment(2.15f, 4.7f, 2.8f, 3.85f),
                    new LetterSegment(2.8f, 3.85f, 2.8f, 0.85f),
                    new LetterSegment(2.8f, 0.85f, 2.15f, 0f),
                    new LetterSegment(2.15f, 0f, 0.65f, 0f),
                    new LetterSegment(0.65f, 0f, 0f, 0.85f),
                    new LetterSegment(0f, 0.85f, 0f, 3.85f),
                    new LetterSegment(0f, 3.85f, 0.65f, 4.7f)
                };
                case 'D': return new[] { left, top, right, bottom };
                case 'R': return new[] { left, top, middle, new LetterSegment(2.8f, 2.35f, 2.8f, 4.7f), new LetterSegment(1.4f, 2.35f, 2.8f, 0f) };
                case 'A': return new[]
                {
                    new LetterSegment(0f, 0f, 1.4f, 4.7f),
                    new LetterSegment(1.4f, 4.7f, 2.8f, 0f),
                    new LetterSegment(0.62f, 2.05f, 2.18f, 2.05f)
                };
                case 'W': return new[]
                {
                    new LetterSegment(0f, 4.7f, 0.45f, 0f),
                    new LetterSegment(0.45f, 0f, 1.4f, 2.15f),
                    new LetterSegment(1.4f, 2.15f, 2.35f, 0f),
                    new LetterSegment(2.35f, 0f, 2.8f, 4.7f)
                };
                default: return new LetterSegment[0];
            }
        }

        private static void CreateLogoFace(
            Transform parent,
            Vector2 center,
            List<StageBombBreakableWall> faceWalls)
        {
            GameObject face = new GameObject("Breakable Logo O Face");
            face.transform.SetParent(parent, false);
            face.transform.position = new Vector3(center.x, center.y, -0.08f);
            Color blue = GetLogoLetterColor('O');

            AddFaceDot(face.transform, "Left Eye", new Vector2(-0.48f, 0.42f), new Vector2(0.3f, 0.25f), blue);
            AddFaceDot(face.transform, "Right Eye", new Vector2(0.48f, 0.42f), new Vector2(0.3f, 0.25f), blue);
            AddFaceLine(face.transform, "Smile", new[]
            {
                new Vector2(-0.56f, -0.25f), new Vector2(-0.4f, -0.52f),
                new Vector2(0f, -0.66f), new Vector2(0.4f, -0.52f), new Vector2(0.56f, -0.25f)
            }, 0.12f, blue);
            AddFaceLine(face.transform, "Hair One", new[]
            {
                new Vector2(0.18f, 2.1f), new Vector2(0.68f, 2.48f)
            }, 0.13f, blue);
            AddFaceLine(face.transform, "Hair Two", new[]
            {
                new Vector2(0.46f, 2.05f), new Vector2(1.08f, 2.34f)
            }, 0.13f, blue);
            AddFaceLine(face.transform, "Hair Three", new[]
            {
                new Vector2(0.7f, 1.98f), new Vector2(1.25f, 2.12f)
            }, 0.13f, blue);

            StageLogoFaceDecoration decoration = face.AddComponent<StageLogoFaceDecoration>();
            decoration.Configure(faceWalls);
        }

        private static void AddFaceDot(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject dot = new GameObject(name);
            dot.transform.SetParent(parent, false);
            dot.transform.localPosition = new Vector3(position.x, position.y, -0.02f);
            dot.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = 20;
        }

        private static void AddFaceLine(Transform parent, string name, Vector2[] points, float width, Color color)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.sharedMaterial = GetLogoLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 20;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private static Material GetLogoLineMaterial()
        {
            if (logoLineMaterial == null) logoLineMaterial = new Material(Shader.Find("Sprites/Default"));
            return logoLineMaterial;
        }

        private void CreateStatusBoard(Transform parent)
        {
            GameObject board = new GameObject("11-3 Status Board");
            board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(0f, 10.55f, 0.3f);
            CreateRect(board.transform, new Vector2(25.5f, 2.3f), new Color(0.04f, 0.07f, 0.08f, 0.9f), -20);
            titleText = CreateText(board.transform, new Vector3(-8.2f, 0.52f, -0.02f), 0.1f, new Color(0.42f, 0.9f, 1f, 1f), -18);
            countText = CreateText(board.transform, new Vector3(8.2f, 0.52f, -0.03f), 0.1f, new Color(1f, 0.83f, 0.28f, 1f), -17);
            statusText = CreateText(board.transform, new Vector3(0f, -0.5f, -0.04f), 0.12f, new Color(0.2f, 1f, 0.7f, 1f), -16);
        }

        private void LockCameraToArena()
        {
            if (gameCamera == null) return;
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            cameraWasEnabled = cameraFollow != null && cameraFollow.enabled;
            if (cameraFollow != null) cameraFollow.enabled = false;
            float halfHeight = 12f;
            float requiredForWidth = (ArenaHalfWidth + 1f) / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 2f, previousCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(halfHeight, requiredForWidth);
        }

        private int CountRemainingBlocks()
        {
            int count = 0;
            for (int i = 0; i < letterBlocks.Count; i++)
                if (letterBlocks[i] != null && !letterBlocks[i].IsBroken) count++;
            return count;
        }

        private void RefreshDisplay()
        {
            if (titleText == null) return;
            SetFittedBoardText(titleText, LocalizationManager.T("block_breaker_title"), 7.5f, 0.1f);
            SetFittedBoardText(countText, LocalizationManager.Format("block_breaker_blocks", CountRemainingBlocks()), 7.5f, 0.1f);
            string status;
            if (phase == ChallengePhase.Intro) status = LocalizationManager.T("block_breaker_goal");
            else if (phase == ChallengePhase.Countdown)
            {
                if (phaseRemaining > 3f) status = "3";
                else if (phaseRemaining > 2f) status = "2";
                else if (phaseRemaining > 1f) status = "1";
                else status = LocalizationManager.T("survival_start");
            }
            else if (phase == ChallengePhase.Failed)
                status = LocalizationManager.Format("block_breaker_retry", Mathf.CeilToInt(failedRestartRemaining));
            else if (phase == ChallengePhase.Clear) status = "CLEAR!";
            else status = FormatTime(remainingSeconds);
            SetFittedBoardText(statusText, status, 22.5f, 0.12f);
        }

        private static void SetFittedBoardText(TextMesh text, string value, float maximumWidth, float preferredSize)
        {
            if (text == null) return;
            text.text = value ?? string.Empty;
            float estimatedUnits = Mathf.Max(1f, text.text.Length * 2.7f);
            text.characterSize = Mathf.Min(preferredSize, maximumWidth / estimatedUnits);
        }

        private void BeginFailure()
        {
            phase = ChallengePhase.Failed;
            failedRestartRemaining = 3f;
            SetLocalControls(false);
            BroadcastState(true);
            GameSfx.Play(SfxId.PlayerHit);
            RefreshDisplay();
        }

        private void CheckFalls()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].transform.position.y < FloorY - 3.2f) RequestElimination(players[i]);
        }

        private void ConfirmElimination(string id)
        {
            ApplyElimination(id);
            Send(KindEliminated, new PlayerState { PlayerId = id });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminatedIds.Add(id)) return;
            PlayerController2D target = ResolvePlayer(id);
            if (target != null && !hiddenPlayers.Contains(target))
            {
                target.GetComponent<PlayerCarryController>()?.ForceDrop();
                target.ResetMotion();
                target.SetControlsEnabled(false);
                hiddenPlayers.Add(target);
                target.gameObject.SetActive(false);
            }
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void RestorePlayers()
        {
            for (int i = 0; i < hiddenPlayers.Count; i++)
                if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private void CaptureParticipants()
        {
            if (IsOnlineActive())
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players == null) return;
                for (int i = 0; i < players.Length; i++)
                    if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) participantIds.Add(players[i].PlayerId);
                return;
            }
            PlayerController2D[] local = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < local.Length; i++) participantIds.Add(ResolvePlayerId(local[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds) if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || string.IsNullOrEmpty(data.Kind)) return;
            if (data.Kind == KindState && IsHostPlayer(data.PlayerId) && !HasAuthority())
            {
                ChallengeState state = JsonUtility.FromJson<ChallengeState>(data.Json);
                if (state == null || state.Sequence <= lastStateSequence) return;
                lastStateSequence = state.Sequence;
                phase = (ChallengePhase)Mathf.Clamp(state.Phase, 0, (int)ChallengePhase.Failed);
                remainingSeconds = state.Remaining;
                phaseRemaining = state.PhaseRemaining;
                if (state.EliminatedIds != null)
                    for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
                SetLocalControls(phase == ChallengePhase.Playing);
            }
            else if (data.Kind == KindEnemySpawn && IsHostPlayer(data.PlayerId) && !HasAuthority())
            {
                EnemyState state = JsonUtility.FromJson<EnemyState>(data.Json);
                if (state != null && state.Sequence > lastEnemySequence) ApplyEnemySpawn(state);
            }
            else if (data.Kind == KindEnemyDefeat && IsHostPlayer(data.PlayerId))
            {
                EnemyState state = JsonUtility.FromJson<EnemyState>(data.Json);
                if (state != null) RemoveEnemy(state.EnemyId);
            }
            else if (data.Kind == KindEnemyDefeatRequest && HasAuthority())
            {
                EnemyState state = JsonUtility.FromJson<EnemyState>(data.Json);
                if (state != null && enemies.ContainsKey(state.EnemyId)) NotifyEnemyDefeated(state.EnemyId);
            }
            else if (data.Kind == KindEliminateRequest && HasAuthority())
            {
                PlayerState state = JsonUtility.FromJson<PlayerState>(data.Json);
                if (state != null && state.PlayerId == data.PlayerId) ConfirmElimination(state.PlayerId);
            }
            else if (data.Kind == KindEliminated && IsHostPlayer(data.PlayerId))
            {
                PlayerState state = JsonUtility.FromJson<PlayerState>(data.Json);
                if (state != null) ApplyElimination(state.PlayerId);
            }
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnlineActive() || !HasAuthority() || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.2f;
            Send(KindState, new ChallengeState
            {
                Sequence = ++stateSequence,
                Phase = (int)phase,
                Remaining = remainingSeconds,
                PhaseRemaining = phaseRemaining,
                EliminatedIds = new List<string>(eliminatedIds).ToArray()
            });
        }

        private void Send<T>(string kind, T value)
        {
            if (!IsOnlineActive() || onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = kind,
                Json = JsonUtility.ToJson(value)
            });
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            if (active != null && active.gameObject.activeSelf) active.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            if (!IsOnlineActive())
            {
                PlayerController2D secondary = stageManager.RemotePlayerController;
                if (secondary != null && secondary.gameObject.activeSelf) secondary.SetControlsEnabled(enabled);
            }
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnlineActive() ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (IsOnlineActive()) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsOnlineActive() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnlineActive() || stageManager.IsOnlineStageHost;

        private bool IsHostPlayer(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        private static string FormatTime(float seconds)
        {
            int value = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
        }

        private static void CreateRect(Transform parent, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject("Board Back");
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private static TextMesh CreateText(Transform parent, Vector3 position, float size, Color color, int order)
        {
            GameObject obj = new GameObject("Board Text");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 58;
            text.characterSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            Font font = FindHandwrittenFont();
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }

        private static Font FindHandwrittenFont()
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                if (fonts[i] != null && fonts[i].name.Contains("Yomogi"))
                {
                    return fonts[i];
                }
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                squareSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }
            return squareSprite;
        }

        internal static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        Vector2.Distance(new Vector2(x, y), center) <= size * 0.46f ? (byte)255 : (byte)0);
            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageLogoFaceDecoration : MonoBehaviour
    {
        private readonly List<StageBombBreakableWall> walls = new List<StageBombBreakableWall>();

        public void Configure(List<StageBombBreakableWall> source)
        {
            walls.Clear();
            if (source != null) walls.AddRange(source);
        }

        private void Update()
        {
            if (walls.Count == 0) return;
            for (int i = 0; i < walls.Count; i++)
            {
                if (walls[i] != null && !walls[i].IsBroken) return;
            }
            gameObject.SetActive(false);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageBlockBreakerEnemy : MonoBehaviour
    {
        private StageBlockBreakerController owner;
        private string enemyId;
        private float direction;
        private float speed;
        private bool defeated;
        private Rigidbody2D enemyBody;
        private static Material doodleMaterial;

        public static StageBlockBreakerEnemy Create(Transform parent, StageBlockBreakerController owner,
            string id, Vector2 position, float direction, float speed)
        {
            GameObject root = new GameObject(id);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = 9;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1.65f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(1.15f, 1.3f);

            GameObject visual = new GameObject("Crayon Doodle Monster");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(Mathf.Sign(direction), 1f, 1f);

            GameObject bodyObject = new GameObject("Ink Blot Body");
            bodyObject.transform.SetParent(visual.transform, false);
            bodyObject.transform.localScale = new Vector3(1.2f, 1.05f, 1f);
            SpriteRenderer renderer = bodyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = StageBlockBreakerController.GetCircleSprite();
            renderer.color = new Color(0.58f, 0.34f, 0.78f, 0.88f);
            renderer.sortingOrder = 32;
            AddBodyOutline(visual.transform);
            AddSpikes(visual.transform);
            AddEye(visual.transform, -0.25f);
            AddEye(visual.transform, 0.25f);
            AddDoodleLine(visual.transform, "Angry Brow Left",
                new[] { new Vector2(-0.45f, 0.42f), new Vector2(-0.12f, 0.29f) }, 0.075f,
                new Color(0.13f, 0.06f, 0.2f, 1f), 36);
            AddDoodleLine(visual.transform, "Angry Brow Right",
                new[] { new Vector2(0.12f, 0.29f), new Vector2(0.45f, 0.42f) }, 0.075f,
                new Color(0.13f, 0.06f, 0.2f, 1f), 36);
            AddDoodleLine(visual.transform, "Zigzag Mouth",
                new[] { new Vector2(-0.32f, -0.15f), new Vector2(-0.14f, -0.28f), new Vector2(0.02f, -0.14f), new Vector2(0.19f, -0.29f), new Vector2(0.36f, -0.14f) },
                0.07f, new Color(0.13f, 0.06f, 0.2f, 1f), 36);
            AddDoodleLine(visual.transform, "Left Arm",
                new[] { new Vector2(-0.5f, 0f), new Vector2(-0.86f, 0.18f), new Vector2(-1.02f, 0.02f) },
                0.08f, new Color(0.36f, 0.13f, 0.58f, 1f), 34);
            AddDoodleLine(visual.transform, "Right Arm",
                new[] { new Vector2(0.5f, 0f), new Vector2(0.86f, 0.18f), new Vector2(1.02f, 0.02f) },
                0.08f, new Color(0.36f, 0.13f, 0.58f, 1f), 34);
            AddDoodleLine(visual.transform, "Running Feet",
                new[] { new Vector2(-0.34f, -0.45f), new Vector2(-0.48f, -0.7f), new Vector2(-0.7f, -0.7f), new Vector2(-0.48f, -0.7f), new Vector2(-0.2f, -0.5f), new Vector2(0.2f, -0.5f), new Vector2(0.45f, -0.7f), new Vector2(0.72f, -0.7f) },
                0.09f, new Color(0.36f, 0.13f, 0.58f, 1f), 34);

            StageBlockBreakerEnemy enemy = root.AddComponent<StageBlockBreakerEnemy>();
            enemy.owner = owner;
            enemy.enemyId = id;
            enemy.direction = Mathf.Sign(direction);
            enemy.speed = Mathf.Max(1f, speed);
            enemy.enemyBody = body;
            return enemy;
        }

        private static void AddEye(Transform parent, float x)
        {
            GameObject eye = new GameObject("Enemy Eye");
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = new Vector3(x, 0.17f, -0.05f);
            eye.transform.localScale = new Vector3(0.23f, 0.29f, 1f);
            SpriteRenderer renderer = eye.AddComponent<SpriteRenderer>();
            renderer.sprite = StageBlockBreakerController.GetCircleSprite();
            renderer.color = new Color(1f, 0.96f, 0.78f, 1f);
            renderer.sortingOrder = 35;

            GameObject pupil = new GameObject("Scribble Pupil");
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0.16f, -0.06f, -0.02f);
            pupil.transform.localScale = Vector3.one * 0.42f;
            SpriteRenderer pupilRenderer = pupil.AddComponent<SpriteRenderer>();
            pupilRenderer.sprite = StageBlockBreakerController.GetCircleSprite();
            pupilRenderer.color = new Color(0.08f, 0.03f, 0.12f, 1f);
            pupilRenderer.sortingOrder = 36;
        }

        private static void AddBodyOutline(Transform parent)
        {
            Vector2[] points = new Vector2[15];
            for (int i = 0; i < 14; i++)
            {
                float angle = i / 14f * Mathf.PI * 2f;
                float wobble = i % 2 == 0 ? 1f : 0.91f;
                points[i] = new Vector2(Mathf.Cos(angle) * 0.62f * wobble, Mathf.Sin(angle) * 0.55f * wobble);
            }
            points[14] = points[0];
            AddDoodleLine(parent, "Wobbly Crayon Outline", points, 0.09f, new Color(0.25f, 0.08f, 0.42f, 1f), 34);
        }

        private static void AddSpikes(Transform parent)
        {
            AddDoodleLine(parent, "Top Crayon Spikes", new[]
            {
                new Vector2(-0.52f, 0.36f), new Vector2(-0.42f, 0.82f), new Vector2(-0.16f, 0.51f),
                new Vector2(0.02f, 0.9f), new Vector2(0.22f, 0.5f), new Vector2(0.48f, 0.76f), new Vector2(0.54f, 0.34f)
            }, 0.095f, new Color(0.42f, 0.14f, 0.65f, 1f), 34);
        }

        private static void AddDoodleLine(Transform parent, string name, Vector2[] points, float width, Color color, int order)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            lineObject.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width * 0.92f;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.sharedMaterial = GetDoodleMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private static Material GetDoodleMaterial()
        {
            if (doodleMaterial == null) doodleMaterial = new Material(Shader.Find("Sprites/Default"));
            return doodleMaterial;
        }

        private void FixedUpdate()
        {
            if (defeated) return;
            bool grounded = Physics2D.Raycast(transform.position, Vector2.down, 0.82f, 1 << 6).collider != null;
            if (enemyBody != null)
            {
                enemyBody.linearVelocity = new Vector2(grounded ? direction * speed : 0f, enemyBody.linearVelocity.y);
            }
            if (Mathf.Abs(transform.position.x) > 23.5f) Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (defeated) return;
            Collider2D other = collision != null ? collision.collider : null;
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null && collision != null && collision.otherCollider != null)
            {
                player = collision.otherCollider.GetComponentInParent<PlayerController2D>();
            }
            if (player != null && player.IsTurtleShelled)
            {
                GameSfx.PlayAt(SfxId.EnemyShellBounce, collision.GetContact(0).point);
                direction = Mathf.Sign(transform.position.x - player.transform.position.x);
                if (Mathf.Abs(transform.position.x - player.transform.position.x) < 0.04f) direction = -direction;
                if (enemyBody != null)
                    enemyBody.linearVelocity = new Vector2(direction * speed, Mathf.Max(0.7f, enemyBody.linearVelocity.y));
                Transform visual = transform.Find("Crayon Doodle Monster");
                if (visual != null)
                    visual.localScale = new Vector3(direction >= 0f ? 1f : -1f, 1f, 1f);
                return;
            }
            if (player != null) owner?.RequestElimination(player);
        }

        public void HitByBomb()
        {
            if (defeated) return;
            owner?.NotifyEnemyDefeated(enemyId);
        }

        public void HitByCatScratch()
        {
            if (defeated) return;
            owner?.RequestEnemyDefeat(enemyId);
        }

        public void DefeatVisual()
        {
            if (defeated) return;
            defeated = true;
            GameSfx.PlayAt(SfxId.EnemyDefeat, transform.position);
            Destroy(gameObject);
        }
    }
}
