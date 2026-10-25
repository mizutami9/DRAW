using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageRicochetBreakerController : MonoBehaviour
    {
        private const string StageId = "8-2";
        private const string StateKind = "ricochet_breaker_state";
        private const string ReflectionRequestKind = "ricochet_breaker_reflection_request";
        private const float OuterHalfWidth = 15.5f;
        private const float OuterHalfHeight = 9f;
        private const float InnerHalfWidth = 12.35f;
        private const float InnerHalfHeight = 5.7f;
        private const float BallSpeed = 6.4f;
        private const float InitialBallSpeedMultiplier = 0.7f;
        private const float BallAccelerationPerSecond = 0.018f;
        private const float MaximumBallSpeedMultiplier = 1.22f;
        private const float IntroSeconds = 2f;
        private const float CountdownSeconds = 3f;
        private const float AdditionalBallDelaySeconds = 10f;
        private const int MaximumConcurrentBalls = 2;
        private const bool HasTimeLimit = false;
        private const float ReflectionRequestCooldown = 0.28f;
        private const float ReflectionRequestPlayerTolerance = 1.1f;
        private const float ReflectionRequestTravelAllowance = 0.35f;

        private enum Phase { Intro, Countdown, Playing, Clear, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int PhaseValue;
            public float Remaining;
            public float PhaseRemaining;
            public float PlayingElapsed;
            public Vector2 BallPosition;
            public Vector2 BallVelocity;
            public Vector2 BallDirection;
            public bool BallActive;
            public int BallGeneration;
            public Vector2 AdditionalBallPosition;
            public Vector2 AdditionalBallVelocity;
            public Vector2 AdditionalBallDirection;
            public bool AdditionalBallActive;
            public int AdditionalBallGeneration;
            public int BallsLaunched;
            public float RetryRemaining;
            public long[] BrokenWords;
        }

        [System.Serializable]
        private sealed class ReflectionRequest
        {
            public int Sequence;
            public bool AdditionalBall;
            public int BallGeneration;
            public Vector2 Point;
            public Vector2 Normal;
            public Vector2 IncomingVelocity;
        }

        private readonly struct LetterSegment
        {
            public readonly Vector2 From;
            public readonly Vector2 To;
            public LetterSegment(float x1, float y1, float x2, float y2)
            {
                From = new Vector2(x1, y1);
                To = new Vector2(x2, y2);
            }
        }

        private readonly List<StageBombBreakableWall> blocks = new List<StageBombBreakableWall>();
        private readonly HashSet<string> brokenIds = new HashSet<string>();
        private readonly Dictionary<string, float> lastAcceptedReflectionAt = new Dictionary<string, float>();
        private readonly Dictionary<string, int> lastReflectionRequestSequence = new Dictionary<string, int>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private UIManager uiManager;
        private StageCountdownPresenter countdownPresenter;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private StageRicochetBall ball;
        private StageRicochetBall additionalBall;
        private TextMesh titleText;
        private TextMesh countText;
        private TextMesh statusText;
        private Phase phase = Phase.Intro;
        private float duration = 60f;
        private float remaining;
        private float phaseRemaining = IntroSeconds;
        private float playingElapsed;
        private float retryRemaining;
        private float nextStateAt;
        private int stateSequence;
        private int lastStateSequence;
        private int ballsLaunched;
        private int primaryBallGeneration;
        private int additionalBallGeneration;
        private int replicaBallGeneration;
        private int replicaAdditionalBallGeneration;
        private int lastSpawnCorner = -1;
        private float nextBallSpawnAt;
        private bool configured;
        private bool cameraWasEnabled;
        private bool cameraStateCaptured;
        private Vector3 oldCameraPosition;
        private float oldCameraSize;
        private Vector2 preparedBallDirection = Vector2.up;
        private Vector2 additionalBallDirection = Vector2.down;
        private bool additionalBallSpawned;
        private int reflectionRequestSequence;

        public void Configure(float seconds)
        {
            duration = Mathf.Clamp(seconds > 0f ? seconds : 60f, 30f, 240f);
            remaining = duration;
            configured = true;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
            uiManager = Object.FindFirstObjectByType<UIManager>();
            countdownPresenter = new StageCountdownPresenter(uiManager);
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
            if (cameraStateCaptured)
            {
                if (cameraFollow != null) cameraFollow.enabled = cameraWasEnabled;
                if (gameCamera != null)
                {
                    gameCamera.transform.position = oldCameraPosition;
                    gameCamera.orthographicSize = oldCameraSize;
                }
                cameraStateCaptured = false;
            }
            SetLocalControls(true);
        }

        private void Start()
        {
            if (!configured) Configure(60f);
            BuildArena();
            LockCamera();
            // Keep a short preparation window so players can spread around the
            // ring without making every retry wait through the old 15 seconds.
            SetLocalControls(true);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (phase == Phase.Countdown) countdownPresenter?.Show(phaseRemaining);
            else if (phase == Phase.Failed) countdownPresenter?.ShowGameOver();
            else countdownPresenter?.Hide();

            if (IsOnline() && !HasAuthority())
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phase == Phase.Playing)
                {
                    playingElapsed += Time.deltaTime;
                    if (HasTimeLimit && !LocalMultiplayerDebugMode.NoTimeLimit)
                        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                }
                RefreshDisplay();
                return;
            }

            BroadcastState();
            if (phase == Phase.Failed)
            {
                retryRemaining -= Time.deltaTime;
                RefreshDisplay();
                if (retryRemaining <= 0f) stageManager.Retry();
                return;
            }
            if (phase == Phase.Clear) return;

            if (phase == Phase.Intro || phase == Phase.Countdown)
            {
                phaseRemaining -= Time.deltaTime;
                if (phase == Phase.Countdown && ball != null)
                    ball.UpdateLaunchCountdown(StageCountdownPresenter.GetNumber(phaseRemaining));
                if (phaseRemaining <= 0f)
                {
                    if (phase == Phase.Intro)
                    {
                        phase = Phase.Countdown;
                        phaseRemaining = CountdownSeconds;
                        PrepareNextBall();
                    }
                    else
                    {
                        phase = Phase.Playing;
                        LaunchPreparedBall();
                        SetLocalControls(true);
                    }
                    BroadcastState(true);
                }
                RefreshDisplay();
                return;
            }

            playingElapsed += Time.deltaTime;
            if (HasTimeLimit && !LocalMultiplayerDebugMode.NoTimeLimit)
                remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (ball != null) ball.SetCruiseSpeed(GetCurrentBallSpeed());
            if (additionalBall != null) additionalBall.SetCruiseSpeed(GetCurrentBallSpeed());
            if (ball != null) ball.RecoverMissedSideWall(InnerHalfWidth, InnerHalfHeight, 0.62f);
            if (additionalBall != null) additionalBall.RecoverMissedSideWall(InnerHalfWidth, InnerHalfHeight, 0.62f);
            if (IsOutsideArena(ball)) LoseBall(ball);
            if (IsOutsideArena(additionalBall)) LoseBall(additionalBall);

            if (!additionalBallSpawned
                && GetPlayerCount() >= 3
                && playingElapsed >= AdditionalBallDelaySeconds
                && GetActiveBallCount() < MaximumConcurrentBalls)
            {
                SpawnAdditionalBall();
            }
            else if (GetActiveBallCount() == 0 && ballsLaunched < 3 && Time.time >= nextBallSpawnAt)
            {
                phase = Phase.Countdown;
                phaseRemaining = CountdownSeconds;
                PrepareNextBall();
                BroadcastState(true);
            }
            if (HasTimeLimit && !LocalMultiplayerDebugMode.NoTimeLimit && remaining <= 0f)
            {
                BeginFailure();
            }
            else if (RemainingBlocks() == 0)
            {
                phase = Phase.Clear;
                ball?.Stop();
                additionalBall?.Stop();
                SetLocalControls(false);
                BroadcastState(true);
                RefreshDisplay();
                stageManager.ClearStage();
                return;
            }
            RefreshDisplay();
        }

        internal void HitBlock(StageBombBreakableWall wall, Vector2 impact)
        {
            if (!HasAuthority() || phase != Phase.Playing || wall == null || wall.IsBroken) return;
            // The logo is intentionally made from roughly one hundred small
            // pieces. Chip a tiny cluster per impact so the one-minute target is
            // achievable without losing the detailed block-by-block appearance.
            const float chipRadius = 1.05f;
            for (int i = 0; i < blocks.Count; i++)
            {
                StageBombBreakableWall candidate = blocks[i];
                if (candidate == null || candidate.IsBroken
                    || Vector2.Distance(candidate.transform.position, impact) > chipRadius) continue;
                candidate.Break(impact);
                brokenIds.Add(candidate.ObjectId);
            }
            GameSfx.PlayAt(SfxId.BombWallBreak, impact, 0.78f);
            BroadcastState(true);
        }

        internal void NotifyPlayerReflection(Vector2 point)
        {
            GameSfx.PlayAt(SfxId.Ricochet, point, 0.92f);
            StageRicochetImpactPulse.Create(transform, point);
        }

        internal void RequestLocalPlayerReflection(
            StageRicochetBall target,
            Vector2 point,
            Vector2 normal,
            Vector2 incomingVelocity)
        {
            if (!IsOnline() || HasAuthority() || phase != Phase.Playing
                || onlineManager == null || target == null) return;

            bool isAdditional = target == additionalBall;
            int generation = isAdditional ? replicaAdditionalBallGeneration : replicaBallGeneration;
            if (generation <= 0 || target != ball && target != additionalBall) return;

            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = ReflectionRequestKind,
                Json = JsonUtility.ToJson(new ReflectionRequest
                {
                    Sequence = ++reflectionRequestSequence,
                    AdditionalBall = isAdditional,
                    BallGeneration = generation,
                    Point = point,
                    Normal = normal,
                    IncomingVelocity = incomingVelocity
                })
            });
        }

        private void HandleReflectionRequest(string playerId, ReflectionRequest request)
        {
            if (!HasAuthority() || phase != Phase.Playing || request == null
                || string.IsNullOrEmpty(playerId) || !IsLobbyPlayer(playerId)) return;

            StageRicochetBall target = request.AdditionalBall ? additionalBall : ball;
            int generation = request.AdditionalBall ? additionalBallGeneration : primaryBallGeneration;
            if (target == null || request.BallGeneration != generation
                || request.Normal.sqrMagnitude < 0.25f
                || request.IncomingVelocity.sqrMagnitude < 0.25f) return;

            string cooldownKey = playerId + ":" + generation + ":" + (request.AdditionalBall ? "1" : "0");
            if (request.Sequence <= 0
                || lastReflectionRequestSequence.TryGetValue(cooldownKey, out int lastSequence)
                    && request.Sequence <= lastSequence) return;
            lastReflectionRequestSequence[cooldownKey] = request.Sequence;
            if (lastAcceptedReflectionAt.TryGetValue(cooldownKey, out float lastAccepted)
                && Time.unscaledTime - lastAccepted < ReflectionRequestCooldown) return;

            PlayerController2D player = stageManager != null
                ? stageManager.GetOnlinePlayerController(playerId)
                : null;
            if (player == null || !IsPointNearPlayer(player, request.Point, ReflectionRequestPlayerTolerance)) return;

            Rigidbody2D targetBody = target.Body;
            Vector2 currentVelocity = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
            float speed = Mathf.Max(0.5f, currentVelocity.magnitude);
            float allowedDistance = ReflectionRequestPlayerTolerance
                + speed * ReflectionRequestTravelAllowance;
            if (Vector2.Distance(target.transform.position, request.Point) > allowedDistance) return;

            Vector2 currentDirection = currentVelocity.sqrMagnitude > 0.1f
                ? currentVelocity.normalized
                : request.IncomingVelocity.normalized;
            if (Vector2.Dot(currentDirection, request.IncomingVelocity.normalized) < 0.15f) return;

            if (!target.ApplyReportedPlayerReflection(
                    player,
                    request.Point,
                    request.Normal,
                    currentDirection * speed)) return;

            lastAcceptedReflectionAt[cooldownKey] = Time.unscaledTime;
            NotifyPlayerReflection(request.Point);
            BroadcastState(true);
        }

        private static bool IsPointNearPlayer(PlayerController2D player, Vector2 point, float tolerance)
        {
            if (player == null) return false;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled) continue;
                // Human arms are trigger colliders so they cannot wedge the
                // locomotion body into terrain. They are still visible drawn
                // segments and must be valid ricochet surfaces. Do not accept
                // unrelated interaction/proxy triggers as ball contacts.
                if (collider.isTrigger && collider.GetComponent<LineRenderer>() == null) continue;
                if (Vector2.Distance(collider.ClosestPoint(point), point) <= tolerance) return true;
            }
            return false;
        }

        private bool IsOutsideArena(StageRicochetBall target)
        {
            return target != null
                && (Mathf.Abs(target.transform.position.y) > OuterHalfHeight + 3f
                    || Mathf.Abs(target.transform.position.x) > OuterHalfWidth + 3f);
        }

        private void LoseBall(StageRicochetBall target)
        {
            if (target == null) return;
            if (target == ball) ball = null;
            if (target == additionalBall) additionalBall = null;
            Destroy(target.gameObject);
            if (GetActiveBallCount() > 0)
            {
                BroadcastState(true);
                return;
            }
            if (ballsLaunched >= 3)
            {
                if (RemainingBlocks() > 0) BeginFailure();
                return;
            }
            nextBallSpawnAt = Time.time + 1.15f;
            BroadcastState(true);
        }

        private void PrepareNextBall()
        {
            if (!HasAuthority() || GetActiveBallCount() > 0 || ballsLaunched >= 3) return;
            playingElapsed = 0f;
            int corner = Random.Range(0, 4);
            if (corner == lastSpawnCorner) corner = (corner + Random.Range(1, 4)) % 4;
            lastSpawnCorner = corner;
            bool fromTop = corner >= 2;
            bool fromRight = corner == 1 || corner == 3;
            Vector2 position = new Vector2(
                (fromRight ? 1f : -1f) * (InnerHalfWidth - 0.72f),
                (fromTop ? 1f : -1f) * (InnerHalfHeight - 0.72f));
            preparedBallDirection = fromTop ? Vector2.down : Vector2.up;
            ballsLaunched++;
            primaryBallGeneration = ballsLaunched;
            ball = StageRicochetBall.Create(transform, this, position, true);
            float currentSpeed = GetCurrentBallSpeed();
            ball.PrepareLaunch(preparedBallDirection, currentSpeed, Mathf.CeilToInt(phaseRemaining));
            BroadcastState(true);
        }

        private void SpawnAdditionalBall()
        {
            if (!HasAuthority() || phase != Phase.Playing || additionalBallSpawned
                || additionalBall != null || GetActiveBallCount() >= MaximumConcurrentBalls
                || ballsLaunched >= 3) return;

            int corner = Random.Range(0, 4);
            if (corner == lastSpawnCorner) corner = (corner + Random.Range(1, 4)) % 4;
            lastSpawnCorner = corner;
            bool fromTop = corner >= 2;
            bool fromRight = corner == 1 || corner == 3;
            Vector2 position = new Vector2(
                (fromRight ? 1f : -1f) * (InnerHalfWidth - 0.72f),
                (fromTop ? 1f : -1f) * (InnerHalfHeight - 0.72f));
            additionalBallDirection = fromTop ? Vector2.down : Vector2.up;
            additionalBallSpawned = true;
            ballsLaunched++;
            additionalBallGeneration = ballsLaunched;
            additionalBall = StageRicochetBall.Create(transform, this, position, true);
            additionalBall.Launch(additionalBallDirection, GetCurrentBallSpeed());
            BroadcastState(true);
        }

        private int GetActiveBallCount()
        {
            int count = 0;
            if (ball != null) count++;
            if (additionalBall != null) count++;
            return count;
        }

        internal int ActiveBallCountForTest => GetActiveBallCount();
        internal int AvailableBallCountForTest => Mathf.Max(0, 3 - ballsLaunched + GetActiveBallCount());
        internal bool IsPlayingForTest => phase == Phase.Playing;
        internal bool IsCountdownForTest => phase == Phase.Countdown;
        internal bool IsFailedForTest => phase == Phase.Failed;

        internal void EnsureAdditionalBallForTest()
        {
            if (HasAuthority() && phase == Phase.Playing && ball != null && additionalBall == null
                && !additionalBallSpawned && ballsLaunched < 3)
                SpawnAdditionalBall();
        }

        internal void DropOneBallForTest()
        {
            if (!HasAuthority()) return;
            StageRicochetBall target = additionalBall != null ? additionalBall : ball;
            LoseBall(target);
        }

        private int GetPlayerCount()
        {
            return stageManager != null ? Mathf.Clamp(stageManager.GetInkBudgetPlayerCount(), 1, 4) : 1;
        }

        private void LaunchPreparedBall()
        {
            if (!HasAuthority() || ball == null) return;
            float currentSpeed = GetCurrentBallSpeed();
            ball.PrepareLaunch(preparedBallDirection, currentSpeed, 1);
            ball.LaunchPrepared();
        }

        private float GetCurrentBallSpeed()
        {
            return Mathf.Min(
                BallSpeed * MaximumBallSpeedMultiplier,
                BallSpeed * InitialBallSpeedMultiplier
                    + playingElapsed * BallAccelerationPerSecond);
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed || phase == Phase.Clear) return;
            phase = Phase.Failed;
            retryRemaining = 3f;
            if (ball != null) ball.Stop();
            if (additionalBall != null) additionalBall.Stop();
            SetLocalControls(false);
            BroadcastState(true);
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("8-2 Ricochet Arena");
            arena.transform.SetParent(transform, false);

            CreateSolid(arena.transform, "Outer Left Frame", StageObjectType.Wall,
                new Vector2(-OuterHalfWidth, 0f), new Vector2(0.7f, OuterHalfHeight * 2f + 0.7f), true);
            CreateSolid(arena.transform, "Outer Right Frame", StageObjectType.Wall,
                new Vector2(OuterHalfWidth, 0f), new Vector2(0.7f, OuterHalfHeight * 2f + 0.7f), true);
            RegisterExistingBallPassSurface("ricochet_outer_bottom");
            CreateSolid(arena.transform, "Outer Top Frame", StageObjectType.Platform,
                new Vector2(0f, OuterHalfHeight), new Vector2(OuterHalfWidth * 2f + 0.7f, 0.7f), true);

            CreateSolid(arena.transform, "Inner Left Bounce Wall", StageObjectType.Wall,
                new Vector2(-InnerHalfWidth, 0f), new Vector2(0.62f, InnerHalfHeight * 2f), false);
            CreateSolid(arena.transform, "Inner Right Bounce Wall", StageObjectType.Wall,
                new Vector2(InnerHalfWidth, 0f), new Vector2(0.62f, InnerHalfHeight * 2f), false);
            CreateSolid(arena.transform, "Inner Bottom Player Floor", StageObjectType.OneWayPlatform,
                new Vector2(0f, -InnerHalfHeight), new Vector2(InnerHalfWidth * 2f, 0.45f), true);
            CreateSolid(arena.transform, "Inner Top Player Floor", StageObjectType.OneWayPlatform,
                new Vector2(0f, InnerHalfHeight), new Vector2(InnerHalfWidth * 2f, 0.45f), true);

            CreateJumpPad(arena.transform, "Left High Jump", new Vector2(-14.15f, -8.25f));
            CreateJumpPad(arena.transform, "Right High Jump", new Vector2(14.15f, -8.25f));
            CreateSolid(arena.transform, "Left Upper Landing", StageObjectType.OneWayPlatform,
                new Vector2(-13.8f, 5.7f), new Vector2(3.1f, 0.45f), true);
            CreateSolid(arena.transform, "Right Upper Landing", StageObjectType.OneWayPlatform,
                new Vector2(13.8f, 5.7f), new Vector2(3.1f, 0.45f), true);

            CreateLetterBlocks(arena.transform);
            CreateStatusBoard(arena.transform);
        }

        private GameObject CreateSolid(Transform parent, string id, StageObjectType type,
            Vector2 position, Vector2 size, bool ballPasses)
        {
            if (factory == null) return null;
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = "ricochet_" + id.Replace(' ', '_').ToLowerInvariant();
            data.size = size;
            data.keepSeparate = true;
            GameObject created = factory.Create(data, parent);
            if (ballPasses && created != null) StageRicochetBallPassSurface.Mark(created);
            return created;
        }

        private void CreateJumpPad(Transform parent, string id, Vector2 position)
        {
            if (factory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.JumpPad, position);
            data.objectId = "ricochet_" + id.Replace(' ', '_').ToLowerInvariant();
            data.size = new Vector2(1.55f, 0.68f);
            data.actionStrength = 82f;
            GameObject created = factory.Create(data, parent);
            if (created != null) StageRicochetBallPassSurface.Mark(created);
        }

        private void RegisterExistingBallPassSurface(string objectId)
        {
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null || objects[i].objectId != objectId) continue;
                StageRicochetBallPassSurface.Mark(objects[i].gameObject);
                return;
            }
        }

        private void CreateLetterBlocks(Transform parent)
        {
            const string text = "NICO DRAW";
            const float letterWidth = 2.25f;
            const float gap = 0.34f;
            const float wordGap = 0.9f;
            const float thickness = 0.34f;
            const float maximumBlockLength = 1f;
            float total = 0f;
            for (int i = 0; i < text.Length; i++) total += text[i] == ' ' ? wordGap : letterWidth + gap;
            float cursor = -total * 0.5f;
            int index = 0;
            for (int c = 0; c < text.Length; c++)
            {
                char letter = text[c];
                if (letter == ' ')
                {
                    cursor += wordGap;
                    continue;
                }
                LetterSegment[] segments = GetSegments(letter);
                List<StageBombBreakableWall> faceWalls = new List<StageBombBreakableWall>();
                Color color = GetLogoColor(letter);
                for (int i = 0; i < segments.Length; i++)
                {
                    Vector2 segmentFrom = segments[i].From + new Vector2(cursor, -2f);
                    Vector2 segmentTo = segments[i].To + new Vector2(cursor, -2f);
                    Vector2 wholeDelta = segmentTo - segmentFrom;
                    int pieces = Mathf.Max(1, Mathf.CeilToInt(wholeDelta.magnitude / maximumBlockLength));
                    for (int piece = 0; piece < pieces; piece++)
                    {
                        Vector2 from = Vector2.Lerp(segmentFrom, segmentTo, piece / (float)pieces);
                        Vector2 to = Vector2.Lerp(segmentFrom, segmentTo, (piece + 1f) / pieces);
                        Vector2 delta = to - from;
                        StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.BreakableWall, (from + to) * 0.5f);
                        data.objectId = "ricochet_logo_" + index++;
                        data.size = new Vector2(delta.magnitude + thickness * 0.34f, thickness);
                        data.rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                        data.actionStrength = 1f;
                        GameObject block = factory.Create(data, parent);
                        AddColorFill(block != null ? block.transform : null, data.size, color);
                        StageBombBreakableWall wall = block != null ? block.GetComponent<StageBombBreakableWall>() : null;
                        if (wall != null)
                        {
                            wall.SetRequirementBadgeVisible(false);
                            blocks.Add(wall);
                            faceWalls.Add(wall);
                        }
                    }
                }
                if (letter == 'O') CreateFace(parent, new Vector2(cursor + 1.02f, 0.02f), faceWalls);
                cursor += letterWidth + gap;
            }
        }

        private static LetterSegment[] GetSegments(char letter)
        {
            LetterSegment top = new LetterSegment(0f, 4f, 2.05f, 4f);
            LetterSegment middle = new LetterSegment(0f, 2f, 2.05f, 2f);
            LetterSegment bottom = new LetterSegment(0f, 0f, 2.05f, 0f);
            LetterSegment left = new LetterSegment(0f, 0f, 0f, 4f);
            LetterSegment right = new LetterSegment(2.05f, 0f, 2.05f, 4f);
            switch (letter)
            {
                case 'N': return new[] { left, new LetterSegment(0f, 4f, 2.05f, 0f), right };
                case 'I': return new[] { top, new LetterSegment(1.03f, 0f, 1.03f, 4f), bottom };
                case 'C': return new[] { top, left, bottom };
                case 'O': return new[] { top, left, right, bottom };
                case 'D': return new[] { left, top, right, bottom };
                case 'R': return new[] { left, top, middle, new LetterSegment(2.05f, 2f, 2.05f, 4f), new LetterSegment(1.03f, 2f, 2.05f, 0f) };
                case 'A': return new[] { new LetterSegment(0f, 0f, 1.03f, 4f), new LetterSegment(1.03f, 4f, 2.05f, 0f), new LetterSegment(0.45f, 1.72f, 1.6f, 1.72f) };
                case 'W': return new[] { new LetterSegment(0f, 4f, 0.34f, 0f), new LetterSegment(0.34f, 0f, 1.03f, 1.72f), new LetterSegment(1.03f, 1.72f, 1.72f, 0f), new LetterSegment(1.72f, 0f, 2.05f, 4f) };
                default: return new LetterSegment[0];
            }
        }

        private static Color GetLogoColor(char letter)
        {
            if (letter == 'N' || letter == 'A') return new Color(1f, 0.06f, 0.08f, 1f);
            if (letter == 'I') return new Color(1f, 0.68f, 0.02f, 1f);
            if (letter == 'C' || letter == 'W') return new Color(0.02f, 0.68f, 0.2f, 1f);
            if (letter == 'R') return new Color(0.42f, 0.1f, 0.76f, 1f);
            return new Color(0.03f, 0.32f, 0.92f, 1f);
        }

        private static void AddColorFill(Transform block, Vector2 size, Color color)
        {
            if (block == null) return;
            GameObject fill = new GameObject("NICO DRAW Logo Color");
            fill.transform.SetParent(block, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = new Color(color.r, color.g, color.b, 0.62f);
            renderer.sortingOrder = 7;
        }

        private static void CreateFace(Transform parent, Vector2 center, List<StageBombBreakableWall> faceWalls)
        {
            GameObject face = new GameObject("Ricochet Logo O Face");
            face.transform.SetParent(parent, false);
            face.transform.position = center;
            Color blue = GetLogoColor('O');
            CreateDot(face.transform, new Vector2(-0.28f, 0.25f), 0.16f, blue);
            CreateDot(face.transform, new Vector2(0.28f, 0.25f), 0.16f, blue);
            CreateLine(face.transform, new[] { new Vector2(-0.32f, -0.2f), new Vector2(0f, -0.4f), new Vector2(0.34f, -0.18f) }, 0.09f, blue);
            CreateLine(face.transform, new[] { new Vector2(0.12f, 1.55f), new Vector2(0.58f, 1.88f) }, 0.1f, blue);
            CreateLine(face.transform, new[] { new Vector2(0.38f, 1.52f), new Vector2(0.84f, 1.74f) }, 0.1f, blue);
            StageLogoFaceDecoration decoration = face.AddComponent<StageLogoFaceDecoration>();
            decoration.Configure(faceWalls);
        }

        private void CreateStatusBoard(Transform parent)
        {
            GameObject board = new GameObject("8-2 Status Board");
            board.transform.SetParent(parent, false);
            board.transform.position = new Vector3(0f, 10.15f, 0.2f);
            DoodleMonitorVisuals.KeepBehindPlayers(board.transform);
            CreateRect(board.transform, new Vector2(19f, 1.85f), new Color(0.04f, 0.07f, 0.08f, 0.9f), 24);
            titleText = CreateText(board.transform, new Vector3(-5.8f, 0.35f, -0.02f), 0.08f, new Color(0.4f, 0.9f, 1f, 1f), 26);
            countText = CreateText(board.transform, new Vector3(5.8f, 0.35f, -0.03f), 0.08f, new Color(1f, 0.82f, 0.25f, 1f), 27);
            statusText = CreateText(board.transform, new Vector3(0f, -0.48f, -0.04f), 0.09f, new Color(0.2f, 1f, 0.7f, 1f), 27);
        }

        private void LockCamera()
        {
            if (gameCamera == null) return;
            oldCameraPosition = gameCamera.transform.position;
            oldCameraSize = gameCamera.orthographicSize;
            cameraWasEnabled = cameraFollow != null && cameraFollow.enabled;
            cameraStateCaptured = true;
            if (cameraFollow != null) cameraFollow.enabled = false;
            float requiredWidth = (OuterHalfWidth + 1.2f) / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 0.75f, oldCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(11.5f, requiredWidth);
        }

        private int RemainingBlocks()
        {
            int count = 0;
            for (int i = 0; i < blocks.Count; i++) if (blocks[i] != null && !blocks[i].IsBroken) count++;
            return count;
        }

        private void RefreshDisplay()
        {
            if (titleText == null) return;
            titleText.text = LocalizationManager.T("ricochet_breaker_title");
            int availableBalls = Mathf.Max(0, 3 - ballsLaunched + GetActiveBallCount());
            countText.text = LocalizationManager.Format("ricochet_breaker_blocks", RemainingBlocks(), availableBalls);
            FitText(countText, 0.08f, 7.2f);
            if (phase == Phase.Intro || phase == Phase.Countdown)
            {
                float totalStartRemaining = phase == Phase.Intro
                    ? phaseRemaining + CountdownSeconds
                    : phaseRemaining;
                statusText.text = totalStartRemaining > 0.15f
                    ? LocalizationManager.Format("ricochet_breaker_start_in", Mathf.CeilToInt(totalStartRemaining))
                    : LocalizationManager.T("survival_start");
            }
            else if (phase == Phase.Failed) statusText.text = LocalizationManager.Format("ricochet_breaker_retry", Mathf.CeilToInt(retryRemaining));
            else if (phase == Phase.Clear) statusText.text = LocalizationManager.T("clear");
            else statusText.text = LocalizationManager.T("ricochet_breaker_goal");
            FitText(statusText, 0.1f, 18f);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == ReflectionRequestKind)
            {
                if (HasAuthority())
                    HandleReflectionRequest(data.PlayerId, JsonUtility.FromJson<ReflectionRequest>(data.Json));
                return;
            }
            if (data.Kind != StateKind || HasAuthority() || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            phase = (Phase)Mathf.Clamp(state.PhaseValue, 0, (int)Phase.Failed);
            remaining = state.Remaining;
            phaseRemaining = state.PhaseRemaining;
            playingElapsed = Mathf.Max(0f, state.PlayingElapsed);
            retryRemaining = state.RetryRemaining;
            preparedBallDirection = state.BallDirection.sqrMagnitude > 0.01f
                ? state.BallDirection.normalized
                : Vector2.up;
            additionalBallDirection = state.AdditionalBallDirection.sqrMagnitude > 0.01f
                ? state.AdditionalBallDirection.normalized
                : Vector2.down;
            if (state.BallActive)
            {
                if (ball == null || replicaBallGeneration != state.BallGeneration)
                {
                    if (ball != null) Destroy(ball.gameObject);
                    ball = StageRicochetBall.Create(transform, this, state.BallPosition, false);
                    replicaBallGeneration = state.BallGeneration;
                }
                if (phase == Phase.Countdown)
                    ball.PrepareLaunch(preparedBallDirection, GetCurrentBallSpeed(), Mathf.CeilToInt(phaseRemaining));
                else ball.HideLaunchPreview();
                ball.SetReplicaTarget(state.BallPosition, state.BallVelocity);
            }
            else if (ball != null)
            {
                Destroy(ball.gameObject);
                ball = null;
            }
            if (state.AdditionalBallActive)
            {
                if (additionalBall == null || replicaAdditionalBallGeneration != state.AdditionalBallGeneration)
                {
                    if (additionalBall != null) Destroy(additionalBall.gameObject);
                    additionalBall = StageRicochetBall.Create(transform, this, state.AdditionalBallPosition, false);
                    replicaAdditionalBallGeneration = state.AdditionalBallGeneration;
                }
                additionalBall.HideLaunchPreview();
                additionalBall.SetReplicaTarget(state.AdditionalBallPosition, state.AdditionalBallVelocity);
                additionalBallSpawned = true;
            }
            else if (additionalBall != null)
            {
                Destroy(additionalBall.gameObject);
                additionalBall = null;
            }
            ballsLaunched = state.BallsLaunched;
            if (state.BrokenWords != null)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    int wordIndex = i / 64;
                    int bitIndex = i % 64;
                    if (wordIndex < state.BrokenWords.Length
                        && (state.BrokenWords[wordIndex] & (1L << bitIndex)) != 0)
                    {
                        ApplyBrokenId(blocks[i] != null ? blocks[i].ObjectId : null);
                    }
                }
            }
            SetLocalControls(phase == Phase.Intro || phase == Phase.Countdown || phase == Phase.Playing);
        }

        private void ApplyBrokenId(string id)
        {
            if (string.IsNullOrEmpty(id) || !brokenIds.Add(id)) return;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null && blocks[i].ObjectId == id)
                {
                    blocks[i].Break(blocks[i].transform.position);
                    return;
                }
            }
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline() || !HasAuthority() || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.05f;
            Rigidbody2D ballBody = ball != null ? ball.Body : null;
            Rigidbody2D additionalBallBody = additionalBall != null ? additionalBall.Body : null;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new NetworkState
                {
                    Sequence = ++stateSequence,
                    PhaseValue = (int)phase,
                    Remaining = remaining,
                    PhaseRemaining = phaseRemaining,
                    PlayingElapsed = playingElapsed,
                    BallPosition = ball != null ? (Vector2)ball.transform.position : Vector2.zero,
                    BallVelocity = ballBody != null ? ballBody.linearVelocity : Vector2.zero,
                    BallDirection = preparedBallDirection,
                    BallActive = ball != null,
                    BallGeneration = primaryBallGeneration,
                    AdditionalBallPosition = additionalBall != null ? (Vector2)additionalBall.transform.position : Vector2.zero,
                    AdditionalBallVelocity = additionalBallBody != null ? additionalBallBody.linearVelocity : Vector2.zero,
                    AdditionalBallDirection = additionalBallDirection,
                    AdditionalBallActive = additionalBall != null,
                    AdditionalBallGeneration = additionalBallGeneration,
                    BallsLaunched = ballsLaunched,
                    RetryRemaining = retryRemaining,
                    BrokenWords = BuildBrokenWords()
                })
            });
        }

        private long[] BuildBrokenWords()
        {
            long[] words = new long[Mathf.Max(1, (blocks.Count + 63) / 64)];
            for (int i = 0; i < blocks.Count; i++)
            {
                StageBombBreakableWall block = blocks[i];
                if (block == null || !block.IsBroken) continue;
                words[i / 64] |= 1L << (i % 64);
            }
            return words;
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (local != null && local.gameObject.activeSelf)
                    local.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            }
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnline() || stageManager.IsOnlineStageHost;

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            return false;
        }

        private bool IsLobbyPlayer(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null || string.IsNullOrEmpty(playerId)) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].PlayerId == playerId) return true;
            return false;
        }

        private static string FormatTime(float seconds)
        {
            int value = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
        }

        private static void FitText(TextMesh text, float preferredSize, float width)
        {
            if (text == null) return;
            text.characterSize = Mathf.Min(preferredSize, width / Mathf.Max(1f, text.text.Length * 2.6f));
        }

        private static void CreateRect(Transform parent, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject("Board Back");
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
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
            Font font = DoodleRuntimeAssets.HandwrittenFont;
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }

        private static void CreateDot(Transform parent, Vector2 position, float size, Color color)
        {
            GameObject dot = new GameObject("Logo Face Dot");
            dot.transform.SetParent(parent, false);
            dot.transform.localPosition = position;
            dot.transform.localScale = Vector3.one * size;
            SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
        }

        private static void CreateLine(Transform parent, Vector2[] points, float width, Color color)
        {
            GameObject obj = new GameObject("Logo Face Line");
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 20;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageRicochetBallPassSurface : MonoBehaviour
    {
        public static void Mark(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponent<StageRicochetBallPassSurface>() == null)
                root.AddComponent<StageRicochetBallPassSurface>();
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject colliderObject = colliders[i].gameObject;
                if (colliderObject.GetComponent<StageRicochetBallPassSurface>() == null)
                    colliderObject.AddComponent<StageRicochetBallPassSurface>();
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageRicochetBall : MonoBehaviour
    {
        private const float PlayerReflectionCooldown = 0.1f;
        private const float LocalReflectionRequestCooldown = 0.24f;
        private const float PlayerSweepPadding = 0.08f;
        private const float PlayerSeparationPadding = 0.055f;

        private StageRicochetBreakerController owner;
        private StageRicochetEnemyChallengeController enemyOwner;
        private StageManager stageManager;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private bool authoritative;
        private Vector2 replicaTarget;
        private Vector2 replicaVelocity;
        private float replicaReceivedAt;
        private Vector2 previousVelocity;
        private Vector2 preparedDirection = Vector2.up;
        private float cruiseSpeed = 3.2f;
        private GameObject serveGuide;
        private TextMesh serveCountdown;
        private float lastPlayerReflectionAt = -10f;
        private int lastReflectedPlayerId;
        private float lastLocalReflectionRequestAt = -10f;
        private bool launched;
        private float lowSpeedSince = -1f;

        public Rigidbody2D Body => body;

        public static StageRicochetBall Create(Transform parent, StageRicochetBreakerController owner,
            Vector2 position, bool authoritative)
        {
            GameObject root = new GameObject("Ricochet Doodle Ball");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = authoritative ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            rigidbody.mass = 0.55f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;
            collider.enabled = authoritative;
            PhysicsMaterial2D material = new PhysicsMaterial2D("Ricochet Ball Material")
            {
                bounciness = 1f,
                friction = 0f
            };
            collider.sharedMaterial = material;

            GameObject fill = new GameObject("Ball Crayon Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = Vector3.one * 0.62f;
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(1f, 0.5f, 0.08f, 1f);
            renderer.sortingOrder = 42;
            GameObject core = new GameObject("Ball White Core");
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.22f;
            SpriteRenderer coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            coreRenderer.color = new Color(1f, 0.95f, 0.55f, 0.9f);
            coreRenderer.sortingOrder = 43;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.32f;
            trail.startWidth = 0.2f;
            trail.endWidth = 0.03f;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.startColor = new Color(1f, 0.55f, 0.08f, 0.7f);
            trail.endColor = new Color(1f, 0.8f, 0.1f, 0f);
            trail.sortingOrder = 40;

            StageRicochetBall ball = root.AddComponent<StageRicochetBall>();
            ball.owner = owner;
            ball.body = rigidbody;
            ball.hitbox = collider;
            ball.authoritative = authoritative;
            ball.replicaTarget = position;
            ball.stageManager = Object.FindFirstObjectByType<StageManager>();
            ball.IgnorePassThroughSurfaces();
            return ball;
        }

        public static StageRicochetBall Create(Transform parent, StageRicochetEnemyChallengeController owner,
            Vector2 position, bool authoritative)
        {
            StageRicochetBall ball = Create(parent, (StageRicochetBreakerController)null, position, authoritative);
            ball.enemyOwner = owner;
            return ball;
        }

        public void Launch(Vector2 direction, float speed)
        {
            if (!authoritative || body == null) return;
            cruiseSpeed = Mathf.Max(0.5f, speed);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearVelocity = direction.normalized * cruiseSpeed;
            previousVelocity = body.linearVelocity;
            launched = true;
            lowSpeedSince = -1f;
            SetServeGuideVisible(false);
        }

        public void PrepareLaunch(Vector2 direction, float speed, int countdown)
        {
            if (body == null) return;
            preparedDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.up;
            cruiseSpeed = Mathf.Max(0.5f, speed);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            previousVelocity = Vector2.zero;
            launched = false;
            lowSpeedSince = -1f;
            EnsureServeGuide();
            UpdateServeGuide(countdown);
            SetServeGuideVisible(true);
        }

        public void UpdateLaunchCountdown(int countdown)
        {
            if (serveGuide == null || !serveGuide.activeSelf) return;
            UpdateServeGuide(countdown);
        }

        public void HideLaunchPreview()
        {
            SetServeGuideVisible(false);
        }

        public void LaunchPrepared()
        {
            Launch(preparedDirection, cruiseSpeed);
        }

        public void SetCruiseSpeed(float speed)
        {
            cruiseSpeed = Mathf.Max(0.5f, speed);
        }

        public void Stop()
        {
            launched = false;
            lowSpeedSince = -1f;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void SetReplicaTarget(Vector2 position, Vector2 velocity)
        {
            replicaTarget = position;
            replicaVelocity = velocity;
            replicaReceivedAt = Time.unscaledTime;
            if (velocity.sqrMagnitude > 0.25f) cruiseSpeed = velocity.magnitude;
            if (body != null) body.linearVelocity = velocity;
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            if (!authoritative)
            {
                Vector2 start = body.position;
                float age = Mathf.Clamp(Time.unscaledTime - replicaReceivedAt, 0f, 0.35f);
                Vector2 predicted = replicaTarget + replicaVelocity * age;
                float blend = 1f - Mathf.Exp(-24f * Time.fixedUnscaledDeltaTime);
                body.position = Vector2.Lerp(body.position, predicted, blend);
                if (owner != null || enemyOwner != null)
                    TrySweepPlayer(start, body.position - start, true);
                return;
            }
            float speed = body.linearVelocity.magnitude;
            if (speed > 0.2f)
            {
                body.linearVelocity = body.linearVelocity.normalized * cruiseSpeed;
                lowSpeedSince = -1f;
            }
            else if (launched && body.bodyType == RigidbodyType2D.Dynamic)
            {
                if (lowSpeedSince < 0f)
                {
                    lowSpeedSince = Time.unscaledTime;
                }
                else if (Time.unscaledTime - lowSpeedSince >= 0.2f)
                {
                    // The challenge has no resting-ball state. A solver contact
                    // can nevertheless zero the velocity and leave the ball
                    // suspended forever. Resume the last valid direction without
                    // changing the configured ball speed or scoring rules.
                    Vector2 direction = previousVelocity.sqrMagnitude > 0.1f
                        ? previousVelocity.normalized
                        : preparedDirection.sqrMagnitude > 0.01f
                            ? preparedDirection.normalized
                            : Vector2.up;
                    body.linearVelocity = direction * cruiseSpeed;
                    lowSpeedSince = -1f;
                }
            }
            TrySweepPlayer(
                body.position,
                body.linearVelocity * Mathf.Max(Time.fixedDeltaTime, 0.001f),
                false);
            previousVelocity = body.linearVelocity;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!authoritative || body == null || collision.collider == null) return;
            if (IsPassThroughSurface(collision))
            {
                // Pass-through floors must never contribute a bounce even if the
                // physics callback was queued before IgnoreCollision took effect.
                Physics2D.IgnoreCollision(hitbox, collision.collider, true);
                Vector2 passVelocity = previousVelocity.sqrMagnitude > 0.1f
                    ? previousVelocity.normalized * cruiseSpeed
                    : body.linearVelocity;
                body.linearVelocity = passVelocity;
                previousVelocity = passVelocity;
                return;
            }
            PlayerController2D player = collision.collider.GetComponentInParent<PlayerController2D>();
            Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal :
                ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
            // Rigidbody2D may already have applied its material response when the
            // callback runs. Reflect the velocity captured before the physics step
            // so a second reflection cannot accidentally send the ball back into
            // the surface.
            Vector2 incoming = previousVelocity.sqrMagnitude > 0.1f ? previousVelocity : Vector2.up * cruiseSpeed;
            if (player != null)
            {
                Vector2 point = collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : collision.collider.ClosestPoint(transform.position);
                if (ApplyPlayerReflection(player, point, normal, incoming, true))
                {
                    owner?.NotifyPlayerReflection(point);
                    enemyOwner?.NotifyPlayerReflection(point);
                }
                return;
            }
            body.linearVelocity = Vector2.Reflect(incoming, normal).normalized * cruiseSpeed;
            previousVelocity = body.linearVelocity;

            StageBombBreakableWall wall = collision.collider.GetComponentInParent<StageBombBreakableWall>();
            if (wall != null) owner?.HitBlock(wall, transform.position);
            StageRicochetEnemyTarget enemy = collision.collider.GetComponentInParent<StageRicochetEnemyTarget>();
            if (enemy != null) enemyOwner?.HitEnemy(enemy, transform.position);
        }

        internal bool ApplyReportedPlayerReflection(
            PlayerController2D player,
            Vector2 point,
            Vector2 normal,
            Vector2 incomingVelocity)
        {
            return authoritative
                && ApplyPlayerReflection(player, point, normal, incomingVelocity, true);
        }

        private bool TrySweepPlayer(Vector2 origin, Vector2 movement, bool localPlayerOnly)
        {
            float distance = movement.magnitude;
            if (distance < 0.001f || hitbox == null) return false;

            Vector2 direction = movement / distance;
            float radius = GetWorldRadius() + PlayerSweepPadding;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                origin,
                radius,
                direction,
                distance + PlayerSweepPadding);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider == hitbox) continue;
                PlayerController2D player = collider.GetComponentInParent<PlayerController2D>();
                if (collider.isTrigger
                    && (player == null || collider.GetComponent<LineRenderer>() == null)) continue;
                if (player == null)
                {
                    if (collider.GetComponentInParent<StageRicochetBallPassSurface>() != null) continue;
                    // Do not report a player through a nearer solid/logo block.
                    return false;
                }
                if (localPlayerOnly && !IsLocalPlayer(player)) continue;

                Vector2 incoming = localPlayerOnly
                    ? (replicaVelocity.sqrMagnitude > 0.1f ? replicaVelocity : direction * cruiseSpeed)
                    : (body.linearVelocity.sqrMagnitude > 0.1f ? body.linearVelocity : direction * cruiseSpeed);
                Vector2 normal = hits[i].normal.sqrMagnitude > 0.1f
                    ? hits[i].normal
                    : (origin - (Vector2)player.transform.position).normalized;
                Vector2 point = hits[i].point;

                if (localPlayerOnly)
                {
                    if (Time.unscaledTime - lastLocalReflectionRequestAt < LocalReflectionRequestCooldown) return false;
                    if (!ApplyPlayerReflection(player, point, normal, incoming, false)) return false;
                    lastLocalReflectionRequestAt = Time.unscaledTime;
                    owner?.RequestLocalPlayerReflection(this, point, normal, incoming);
                    enemyOwner?.RequestLocalPlayerReflection(this, point, normal, incoming);
                    owner?.NotifyPlayerReflection(point);
                    enemyOwner?.NotifyPlayerReflection(point);
                    return true;
                }

                if (ApplyPlayerReflection(player, point, normal, incoming, true))
                {
                    owner?.NotifyPlayerReflection(point);
                    enemyOwner?.NotifyPlayerReflection(point);
                    return true;
                }
            }
            return false;
        }

        private bool ApplyPlayerReflection(
            PlayerController2D player,
            Vector2 point,
            Vector2 normal,
            Vector2 incomingVelocity,
            bool moveOutOfContact)
        {
            if (body == null || player == null || incomingVelocity.sqrMagnitude < 0.01f) return false;
            int playerId = player.GetInstanceID();
            if (playerId == lastReflectedPlayerId
                && Time.unscaledTime - lastPlayerReflectionAt < PlayerReflectionCooldown)
            {
                // A hand-drawn body can report several segment contacts in the
                // same physics step. Preserve the first bounce instead of
                // reflecting it again and cancelling the direction change.
                body.linearVelocity = previousVelocity.sqrMagnitude > 0.1f
                    ? previousVelocity.normalized * cruiseSpeed
                    : body.linearVelocity;
                return false;
            }

            Vector2 incoming = incomingVelocity.normalized;
            Vector2 playerToBall = (Vector2)body.position - (Vector2)player.transform.position;
            Vector2 bounceNormal = normal.sqrMagnitude > 0.01f ? normal.normalized : -incoming;
            if (Vector2.Dot(incoming, bounceNormal) > 0f) bounceNormal = -bounceNormal;

            // Thin/angled line segments can yield an almost tangential normal.
            // Blend in the direction away from the player so a visible body hit
            // always produces a meaningful bounce rather than a tiny deflection.
            Vector2 away = playerToBall.sqrMagnitude > 0.01f ? playerToBall.normalized : -incoming;
            if (Vector2.Dot(incoming, away) > 0f) away = -away;
            float approach = -Vector2.Dot(incoming, bounceNormal);
            if (approach < 0.38f)
                bounceNormal = (bounceNormal * 0.35f + away * 0.65f - incoming * 0.3f).normalized;
            if (Vector2.Dot(incoming, bounceNormal) > -0.16f)
                bounceNormal = (bounceNormal - incoming).normalized;

            Vector2 outgoing = Vector2.Reflect(incoming, bounceNormal).normalized;
            if (Vector2.Dot(outgoing, incoming) > 0.68f)
                outgoing = Vector2.Lerp(outgoing, -incoming, 0.72f).normalized;
            if (Vector2.Dot(outgoing, bounceNormal) < 0.28f)
                outgoing = (outgoing + bounceNormal * 0.72f).normalized;

            // A hit on the upper/lower half of a character must not continue
            // through that character in the same vertical direction.
            if (Mathf.Abs(playerToBall.y) > Mathf.Abs(playerToBall.x) * 0.45f)
            {
                float awayY = Mathf.Sign(playerToBall.y);
                if (outgoing.y * awayY < 0.3f)
                {
                    outgoing.y = awayY * Mathf.Max(0.55f, Mathf.Abs(incoming.y));
                    outgoing.Normalize();
                }
            }

            cruiseSpeed = Mathf.Max(0.5f, cruiseSpeed);
            body.linearVelocity = outgoing * cruiseSpeed;
            previousVelocity = body.linearVelocity;
            replicaVelocity = body.linearVelocity;
            if (moveOutOfContact)
                body.position = point + bounceNormal * (GetWorldRadius() + PlayerSeparationPadding);
            else
                replicaTarget = body.position;
            replicaReceivedAt = Time.unscaledTime;
            lastReflectedPlayerId = playerId;
            lastPlayerReflectionAt = Time.unscaledTime;
            return true;
        }

        private float GetWorldRadius()
        {
            if (hitbox == null) return 0.32f;
            Vector3 scale = hitbox.transform.lossyScale;
            return hitbox.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private bool IsLocalPlayer(PlayerController2D player)
        {
            if (player == null) return false;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            return stageManager != null && stageManager.IsLocallyControlledPlayer(player);
        }

        private void IgnorePassThroughSurfaces()
        {
            if (hitbox == null) return;
            StageRicochetBallPassSurface[] surfaces = Object.FindObjectsByType<StageRicochetBallPassSurface>(FindObjectsSortMode.None);
            for (int i = 0; i < surfaces.Length; i++)
            {
                Collider2D[] colliders = surfaces[i].GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < colliders.Length; c++) Physics2D.IgnoreCollision(hitbox, colliders[c], true);
            }
        }

        private bool IsPassThroughSurface(Collision2D collision)
        {
            if (collision == null || collision.collider == null) return false;
            if (collision.collider.GetComponentInParent<StageRicochetBallPassSurface>() != null) return true;
            if (owner == null && enemyOwner == null) return false;
            StageEditorObject stageObject = collision.collider.GetComponentInParent<StageEditorObject>();
            if (stageObject == null) return false;

            // The geometry fallback below is only for horizontal player floors.
            // At the upper/lower corner of a vertical wall, the contact normal can
            // contain a large Y component. Treating that corner as a floor calls
            // IgnoreCollision for the whole wall collider, after which the ball
            // can pass through that side wall for the rest of its lifetime.
            if (stageObject.type != StageObjectType.Platform
                && stageObject.type != StageObjectType.OneWayPlatform
                && stageObject.type != StageObjectType.MovingOneWayPlatform
                && stageObject.type != StageObjectType.EscortPlayerOneWayFloor)
            {
                return false;
            }

            // These two stages intentionally let the ball leave through the
            // horizontal player floors. Keep a geometry fallback because a
            // rebuilt/connected floor can put its Collider on a sibling object,
            // outside the marker's transform hierarchy.
            if (collision.contactCount <= 0) return false;
            ContactPoint2D contact = collision.GetContact(0);
            return Mathf.Abs(contact.point.y) >= 5.25f && Mathf.Abs(contact.normal.y) >= 0.8f;
        }

        internal void RecoverMissedSideWall(float halfWidth, float halfHeight, float wallThickness)
        {
            if (!authoritative || body == null || !launched) return;
            float radius = GetWorldRadius();
            if (Mathf.Abs(body.position.y) > halfHeight + radius) return;

            float centerLimit = Mathf.Max(radius, halfWidth - wallThickness * 0.5f - radius);
            float side = Mathf.Sign(body.position.x);
            if (Mathf.Abs(body.position.x) <= centerLimit + 0.04f || side == 0f) return;

            body.position = new Vector2(side * centerLimit, body.position.y);
            Vector2 incoming = body.linearVelocity.sqrMagnitude > 0.1f
                ? body.linearVelocity
                : previousVelocity;
            if (incoming.sqrMagnitude < 0.1f) incoming = -side * Vector2.right * cruiseSpeed;
            incoming.x = -side * Mathf.Max(Mathf.Abs(incoming.x), cruiseSpeed * 0.35f);
            body.linearVelocity = incoming.normalized * cruiseSpeed;
            previousVelocity = body.linearVelocity;
        }

        private void EnsureServeGuide()
        {
            if (serveGuide != null) return;
            serveGuide = new GameObject("Ball Launch Preview");
            serveGuide.transform.SetParent(transform, false);

            GameObject arrow = new GameObject("Direction Arrow");
            arrow.transform.SetParent(serveGuide.transform, false);
            LineRenderer line = arrow.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.startWidth = 0.1f;
            line.endWidth = 0.1f;
            line.numCapVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = new Color(0.1f, 0.75f, 1f, 0.92f);
            line.endColor = new Color(0.1f, 0.75f, 1f, 0.92f);
            line.sortingOrder = 50;

            GameObject label = new GameObject("Launch Countdown");
            label.transform.SetParent(serveGuide.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.8f, -0.08f);
            serveCountdown = label.AddComponent<TextMesh>();
            serveCountdown.anchor = TextAnchor.MiddleCenter;
            serveCountdown.alignment = TextAlignment.Center;
            serveCountdown.fontSize = 54;
            serveCountdown.characterSize = 0.075f;
            serveCountdown.color = new Color(1f, 0.72f, 0.08f, 1f);
            label.GetComponent<MeshRenderer>().sortingOrder = 51;
        }

        private void UpdateServeGuide(int countdown)
        {
            if (serveGuide == null) return;
            LineRenderer line = serveGuide.GetComponentInChildren<LineRenderer>(true);
            if (line != null)
            {
                Vector2 side = new Vector2(-preparedDirection.y, preparedDirection.x);
                Vector2 tip = preparedDirection * 1.45f;
                Vector2 neck = preparedDirection * 0.98f;
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, tip);
                line.SetPosition(2, neck + side * 0.28f);
                line.SetPosition(3, tip);
                line.SetPosition(4, neck - side * 0.28f);
            }
            if (serveCountdown != null) serveCountdown.text = Mathf.Max(1, countdown).ToString();
        }

        private void SetServeGuideVisible(bool visible)
        {
            if (serveGuide != null) serveGuide.SetActive(visible);
        }
    }

    public sealed class StageRicochetImpactPulse : MonoBehaviour
    {
        private SpriteRenderer pulseRenderer;
        private float elapsed;

        public static void Create(Transform parent, Vector2 position)
        {
            GameObject obj = new GameObject("Ricochet Impact Pulse");
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
            sprite.sprite = DoodleRuntimeAssets.CircleSprite;
            sprite.color = new Color(1f, 0.78f, 0.15f, 0.75f);
            sprite.sortingOrder = 44;
            StageRicochetImpactPulse pulse = obj.AddComponent<StageRicochetImpactPulse>();
            pulse.pulseRenderer = sprite;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.28f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.65f, t);
            if (pulseRenderer != null) pulseRenderer.color = new Color(1f, 0.78f, 0.15f, (1f - t) * 0.75f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
