using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageRicochetBreakerController : MonoBehaviour
    {
        private const string StageId = "8-2";
        private const string StateKind = "ricochet_breaker_state";
        private const float OuterHalfWidth = 15.5f;
        private const float OuterHalfHeight = 9f;
        private const float InnerHalfWidth = 12.35f;
        private const float InnerHalfHeight = 5.7f;
        private const float BallSpeed = 6.4f;
        private const float IntroSeconds = 12f;
        private const float CountdownSeconds = 3f;

        private enum Phase { Intro, Countdown, Playing, Clear, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int PhaseValue;
            public float Remaining;
            public float PhaseRemaining;
            public Vector2 BallPosition;
            public Vector2 BallVelocity;
            public Vector2 BallDirection;
            public bool BallActive;
            public int BallsLaunched;
            public float RetryRemaining;
            public string[] BrokenIds;
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
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private StageRicochetBall ball;
        private TextMesh titleText;
        private TextMesh countText;
        private TextMesh statusText;
        private Phase phase = Phase.Intro;
        private float duration = 60f;
        private float remaining;
        private float phaseRemaining = IntroSeconds;
        private float retryRemaining;
        private float nextStateAt;
        private int stateSequence;
        private int lastStateSequence;
        private int ballsLaunched;
        private int replicaBallGeneration;
        private int lastSpawnCorner = -1;
        private float nextBallSpawnAt;
        private bool configured;
        private bool cameraWasEnabled;
        private Vector3 oldCameraPosition;
        private float oldCameraSize;
        private Vector2 replicaBallPosition;
        private Vector2 replicaBallVelocity;
        private Vector2 preparedBallDirection = Vector2.up;

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
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (cameraFollow != null) cameraFollow.enabled = cameraWasEnabled;
            if (gameCamera != null)
            {
                gameCamera.transform.position = oldCameraPosition;
                gameCamera.orthographicSize = oldCameraSize;
            }
            SetLocalControls(true);
        }

        private void Start()
        {
            if (!configured) Configure(60f);
            BuildArena();
            LockCamera();
            // The 15-second preparation period is part of the play: players can
            // spread out around the ring before the first ball is served.
            SetLocalControls(true);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (IsOnline() && !HasAuthority())
            {
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
                if (phase == Phase.Playing) remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                if (ball != null) ball.SetReplicaTarget(replicaBallPosition, replicaBallVelocity);
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
                    ball.UpdateLaunchCountdown(Mathf.CeilToInt(phaseRemaining));
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

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (ball != null) ball.SetCruiseSpeed(GetCurrentBallSpeed());
            if (ball != null && (Mathf.Abs(ball.transform.position.y) > OuterHalfHeight + 3f
                || Mathf.Abs(ball.transform.position.x) > OuterHalfWidth + 3f))
            {
                LoseBall();
            }
            else if (ball == null && ballsLaunched < 3 && Time.time >= nextBallSpawnAt)
            {
                phase = Phase.Countdown;
                phaseRemaining = CountdownSeconds;
                PrepareNextBall();
                BroadcastState(true);
            }
            if (remaining <= 0f)
            {
                BeginFailure();
            }
            else if (RemainingBlocks() == 0)
            {
                phase = Phase.Clear;
                ball?.Stop();
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
            GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.92f);
            StageRicochetImpactPulse.Create(transform, point);
        }

        private void LoseBall()
        {
            if (ball != null) Destroy(ball.gameObject);
            ball = null;
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
            if (!HasAuthority() || ball != null || ballsLaunched >= 3) return;
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
            ball = StageRicochetBall.Create(transform, this, position, true);
            float currentSpeed = GetCurrentBallSpeed();
            ball.PrepareLaunch(preparedBallDirection, currentSpeed, Mathf.CeilToInt(phaseRemaining));
            replicaBallPosition = position;
            replicaBallVelocity = Vector2.zero;
            BroadcastState(true);
        }

        private void LaunchPreparedBall()
        {
            if (!HasAuthority() || ball == null) return;
            float currentSpeed = GetCurrentBallSpeed();
            ball.PrepareLaunch(preparedBallDirection, currentSpeed, 1);
            ball.LaunchPrepared();
            replicaBallVelocity = preparedBallDirection * currentSpeed;
        }

        private float GetCurrentBallSpeed()
        {
            float progress = Mathf.Clamp01((duration - remaining) / Mathf.Max(1f, duration));
            return BallSpeed * Mathf.Lerp(0.35f, 1.5f, progress);
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed || phase == Phase.Clear) return;
            phase = Phase.Failed;
            retryRemaining = 3f;
            if (ball != null) ball.Stop();
            SetLocalControls(false);
            GameSfx.Play(SfxId.PlayerHit);
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
            renderer.sprite = StageSurvivalController.GetSquareSpriteForChallenges();
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
            int availableBalls = Mathf.Max(0, 3 - ballsLaunched + (ball != null ? 1 : 0));
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
            else if (phase == Phase.Clear) statusText.text = "CLEAR!";
            else statusText.text = FormatTime(remaining);
            FitText(statusText, 0.1f, 18f);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind || HasAuthority() || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            phase = (Phase)Mathf.Clamp(state.PhaseValue, 0, (int)Phase.Failed);
            remaining = state.Remaining;
            phaseRemaining = state.PhaseRemaining;
            retryRemaining = state.RetryRemaining;
            replicaBallPosition = state.BallPosition;
            replicaBallVelocity = state.BallVelocity;
            preparedBallDirection = state.BallDirection.sqrMagnitude > 0.01f
                ? state.BallDirection.normalized
                : Vector2.up;
            if (state.BallActive)
            {
                if (ball == null || replicaBallGeneration != state.BallsLaunched)
                {
                    if (ball != null) Destroy(ball.gameObject);
                    ball = StageRicochetBall.Create(transform, this, state.BallPosition, false);
                    replicaBallGeneration = state.BallsLaunched;
                }
                if (phase == Phase.Countdown)
                    ball.PrepareLaunch(preparedBallDirection, GetCurrentBallSpeed(), Mathf.CeilToInt(phaseRemaining));
                else ball.HideLaunchPreview();
            }
            else if (ball != null)
            {
                Destroy(ball.gameObject);
                ball = null;
            }
            ballsLaunched = state.BallsLaunched;
            if (state.BrokenIds != null)
            {
                for (int i = 0; i < state.BrokenIds.Length; i++) ApplyBrokenId(state.BrokenIds[i]);
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
            nextStateAt = Time.unscaledTime + 0.1f;
            Rigidbody2D ballBody = ball != null ? ball.Body : null;
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
                    BallPosition = ball != null ? (Vector2)ball.transform.position : Vector2.zero,
                    BallVelocity = ballBody != null ? ballBody.linearVelocity : Vector2.zero,
                    BallDirection = preparedBallDirection,
                    BallActive = ball != null,
                    BallsLaunched = ballsLaunched,
                    RetryRemaining = retryRemaining,
                    BrokenIds = new List<string>(brokenIds).ToArray()
                })
            });
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            if (active != null && active.gameObject.activeSelf) active.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            if (!IsOnline())
            {
                PlayerController2D secondary = stageManager.RemotePlayerController;
                if (secondary != null && secondary.gameObject.activeSelf) secondary.SetControlsEnabled(enabled);
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
            renderer.sprite = StageSurvivalController.GetSquareSpriteForChallenges();
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
            Font font = StageSurvivalController.FindHandwrittenFont();
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
            renderer.sprite = StageSurvivalController.GetCircleSprite();
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
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
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
        private StageRicochetBreakerController owner;
        private StageRicochetEnemyChallengeController enemyOwner;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private bool authoritative;
        private Vector2 replicaTarget;
        private Vector2 previousVelocity;
        private Vector2 preparedDirection = Vector2.up;
        private float cruiseSpeed = 3.2f;
        private GameObject serveGuide;
        private TextMesh serveCountdown;

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
            renderer.sprite = StageSurvivalController.GetCircleSprite();
            renderer.color = new Color(1f, 0.5f, 0.08f, 1f);
            renderer.sortingOrder = 42;
            GameObject core = new GameObject("Ball White Core");
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.22f;
            SpriteRenderer coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = StageSurvivalController.GetCircleSprite();
            coreRenderer.color = new Color(1f, 0.95f, 0.55f, 0.9f);
            coreRenderer.sortingOrder = 43;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.32f;
            trail.startWidth = 0.2f;
            trail.endWidth = 0.03f;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.55f, 0.08f, 0.7f);
            trail.endColor = new Color(1f, 0.8f, 0.1f, 0f);
            trail.sortingOrder = 40;

            StageRicochetBall ball = root.AddComponent<StageRicochetBall>();
            ball.owner = owner;
            ball.body = rigidbody;
            ball.hitbox = collider;
            ball.authoritative = authoritative;
            ball.replicaTarget = position;
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
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void SetReplicaTarget(Vector2 position, Vector2 velocity)
        {
            replicaTarget = position;
            if (body != null) body.linearVelocity = velocity;
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            if (!authoritative)
            {
                body.position = Vector2.Lerp(body.position, replicaTarget, 0.48f);
                return;
            }
            float speed = body.linearVelocity.magnitude;
            if (speed > 0.2f)
                body.linearVelocity = body.linearVelocity.normalized * cruiseSpeed;
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
            Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal :
                ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
            // Rigidbody2D may already have applied its material response when the
            // callback runs. Reflect the velocity captured before the physics step
            // so a second reflection cannot accidentally send the ball back into
            // the surface.
            Vector2 incoming = previousVelocity.sqrMagnitude > 0.1f ? previousVelocity : Vector2.up * cruiseSpeed;
            body.linearVelocity = Vector2.Reflect(incoming, normal).normalized * cruiseSpeed;
            previousVelocity = body.linearVelocity;

            StageBombBreakableWall wall = collision.collider.GetComponentInParent<StageBombBreakableWall>();
            if (wall != null) owner?.HitBlock(wall, transform.position);
            StageRicochetEnemyTarget enemy = collision.collider.GetComponentInParent<StageRicochetEnemyTarget>();
            if (enemy != null) enemyOwner?.HitEnemy(enemy, transform.position);
            PlayerController2D player = collision.collider.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
                owner?.NotifyPlayerReflection(point);
                enemyOwner?.NotifyPlayerReflection(point);
            }
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

            // These two stages intentionally let the ball leave through the
            // horizontal player floors. Keep a geometry fallback because a
            // rebuilt/connected floor can put its Collider on a sibling object,
            // outside the marker's transform hierarchy.
            if (collision.contactCount <= 0) return false;
            ContactPoint2D contact = collision.GetContact(0);
            return Mathf.Abs(contact.point.y) >= 5.25f && Mathf.Abs(contact.normal.y) >= 0.55f;
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
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
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
        private SpriteRenderer renderer;
        private float elapsed;

        public static void Create(Transform parent, Vector2 position)
        {
            GameObject obj = new GameObject("Ricochet Impact Pulse");
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
            sprite.sprite = StageSurvivalController.GetCircleSprite();
            sprite.color = new Color(1f, 0.78f, 0.15f, 0.75f);
            sprite.sortingOrder = 44;
            StageRicochetImpactPulse pulse = obj.AddComponent<StageRicochetImpactPulse>();
            pulse.renderer = sprite;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.28f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.65f, t);
            if (renderer != null) renderer.color = new Color(1f, 0.78f, 0.15f, (1f - t) * 0.75f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
