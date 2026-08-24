using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSideScrollBossChaseController : StageEliminationChallengeController
    {
        private const string StageId = "15-2";
        private const string StateKind = "side_boss_state";
        private const string AttackKind = "side_boss_attack";
        private const string WeaponRequestKind = "side_boss_weapon_request";
        private const string EliminateRequestKind = "side_boss_eliminate_request";
        private const string EliminateKind = "side_boss_eliminate";
        private const float FloorY = -3.8f;
        private const float StartDelay = 3f;
        private const int RequiredHits = 5;

        private enum Phase { Ready, Running, Defeated, Failed }
        private enum AttackType { Barrage, AimedShot, Laser, BreakFloor, GiantHand }

        [System.Serializable]
        private sealed class ChaseState
        {
            public int Sequence;
            public int Phase;
            public float Elapsed;
            public float ScrollX;
            public float BossX;
            public int BossHealth;
            public bool[] ActivatedWeapons;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class AttackState
        {
            public int Sequence;
            public int Type;
            public Vector2 Origin;
            public Vector2 Target;
            public float Lane;
            public int FloorIndex;
        }

        [System.Serializable] private sealed class EliminationState { public string PlayerId; }
        [System.Serializable] private sealed class WeaponRequest { public int Index; }

        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly HashSet<int> receivedAttacks = new HashSet<int>();
        private readonly HashSet<string> redrawingPlayers = new HashSet<string>();
        private readonly HashSet<int> landedCounterBombs = new HashSet<int>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly List<StageSideBossFloor> floors = new List<StageSideBossFloor>();
        private readonly List<StageSideBossWeapon> weapons = new List<StageSideBossWeapon>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private StageObjectFactory objectFactory;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousFollowEnabled;
        private Transform boss;
        private Transform bossFace;
        private SpriteRenderer bossCore;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private Transform hpFill;
        private PlayerController2D[] players = System.Array.Empty<PlayerController2D>();
        private bool[] activatedWeapons;
        private Phase phase = Phase.Ready;
        private float elapsed;
        private float scrollX;
        private float bossX = -15f;
        private float nextAttackAt = 6f;
        private float nextBroadcastAt;
        private float nextPlayerRefreshAt;
        private int bossHealth = RequiredHits;
        private int sequence;
        private int receivedSequence;
        private int attackSequence;
        private int attackCursor;
        private bool controlsReleased;
        private bool retryStarted;
        private bool restored;

        public override bool UsesGlobalFallBoundary => false;
        private bool HasAuthority => stageManager == null || !stageManager.IsOnlineStageActive || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
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
            RestoreStageState();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildStage();
            BuildBoss();
            BuildMonitor();
            RefreshPlayers();
            CaptureParticipants();
            activatedWeapons = new bool[weapons.Count];
            PositionPlayersAtStart();
            LockCamera();
            SetLocalControls(false);
            BroadcastState(true);
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            HandleRedrawProtection();
            RefreshCountdown();
            if (phase == Phase.Defeated || phase == Phase.Failed) { UpdateCameraAndBoss(); return; }

            if (HasAuthority)
            {
                elapsed += Time.deltaTime;
                if (phase == Phase.Ready && elapsed >= StartDelay)
                {
                    phase = Phase.Running;
                    controlsReleased = true;
                    SetLocalControls(true);
                    nextAttackAt = elapsed + 2.5f;
                    GameSfx.Play(SfxId.UiToggleOn, 1.15f);
                }
                if (phase == Phase.Running)
                {
                    float progress = Mathf.Clamp01(scrollX / 180f);
                    scrollX += Mathf.Lerp(2.25f, 3.15f, Mathf.InverseLerp(0.62f, 1f, progress)) * Time.deltaTime;
                    bossX = scrollX - Mathf.Lerp(15.2f, 12.8f, progress);
                    CheckPlayers();
                    CheckCounterWeapons();
                    if (elapsed >= nextAttackAt) BeginAttack(progress);
                    if (AreAllEliminated()) BeginFailure();
                }
                BroadcastState(false);
            }
            else
            {
                CheckLocalFallOrCatch();
            }
            UpdateCameraAndBoss();
            RefreshMonitor();
        }

        private void BuildStage()
        {
            CreateFloor(-14f, 16f, FloorY, false);
            CreateFloor(19f, 34f, FloorY, false);
            CreateFloor(34f, 47f, FloorY + 1f, false);
            CreateMovingFloor(new Vector2(49.5f, FloorY - 0.1f), new Vector2(5f, 0.55f), Vector2.up * 1.6f);
            CreateFloor(52f, 67f, FloorY, false);
            CreateFloor(67f, 75f, FloorY, true);
            CreateFloor(78f, 94f, FloorY - 0.6f, false);
            CreateMovingFloor(new Vector2(97f, FloorY + 0.4f), new Vector2(5.5f, 0.55f), Vector2.right * 1.6f);
            CreateFloor(100f, 119f, FloorY + 0.2f, false);
            CreateFloor(122f, 137f, FloorY - 0.7f, true);
            CreateFloor(140f, 155f, FloorY + 0.8f, false);
            CreateFloor(158f, 176f, FloorY - 0.2f, false);
            CreateFloor(179f, 194f, FloorY, false);

            CreateStep(27f, FloorY + 1.15f, 3.2f);
            CreateStep(31f, FloorY + 2.05f, 3.2f);
            CreateStep(84f, FloorY + 1.1f, 4f);
            CreateStep(88.5f, FloorY + 2f, 3.4f);
            CreateStep(145f, FloorY + 2.1f, 4.5f);
            CreateStep(151f, FloorY + 3f, 4.5f);

            CreateSpikes(11f, FloorY + 0.48f, 3);
            CreateSpikes(39f, FloorY + 1.48f, 4);
            CreateSpikes(59f, FloorY + 0.48f, 3);
            CreateSpikes(106f, FloorY + 0.68f, 4);
            CreateSpikes(146f, FloorY + 1.28f, 3);
            CreateSpikes(169f, FloorY + 0.28f, 5);

            CreateJumpPad(14f, FloorY + 0.55f);
            CreateJumpPad(44f, FloorY + 1.55f);
            CreateJumpPad(73f, FloorY + 0.55f);
            CreateJumpPad(115f, FloorY + 0.75f);
            CreateJumpPad(153f, FloorY + 1.35f);
            CreateJumpPad(174f, FloorY + 0.35f);

            CreateCeilingBombButton(27.5f, 0.7f);
            CreateCeilingBombButton(56f, 0.15f);
            CreateCeilingBombButton(87f, 1.15f);
            CreateCeilingBombButton(112f, 0.45f);
            CreateCeilingBombButton(147f, 1.55f);
            CreateCeilingBombButton(184f, 0.35f);
        }

        private StageSideBossFloor CreateFloor(float left, float right, float y, bool crumble)
        {
            float totalWidth = Mathf.Max(0.5f, right - left);
            int pieceCount = Mathf.Max(1, Mathf.CeilToInt(totalWidth / 5f));
            float pieceWidth = totalWidth / pieceCount;
            StageSideBossFloor first = null;
            for (int i = 0; i < pieceCount; i++)
            {
                float pieceLeft = left + pieceWidth * i;
                StageSideBossFloor piece = CreateFloorPiece(pieceLeft, pieceLeft + pieceWidth, y, crumble);
                if (first == null) first = piece;
            }
            return first;
        }

        private StageSideBossFloor CreateFloorPiece(float left, float right, float y, bool crumble)
        {
            GameObject root = new GameObject(crumble ? "Crumbling Escape Floor" : "Escape Floor");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3((left + right) * 0.5f, y, 0f);
            root.layer = 6;
            Vector2 size = new Vector2(right - left, 0.65f);
            StageEscortController.AddFilledRect(root.transform, "Paper Fill", Vector2.zero, size, crumble ? new Color(0.9f, 0.7f, 0.42f) : new Color(0.94f, 0.94f, 0.88f), -4);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.12f, 0.12f, 0.12f), -2);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>(); collider.size = size;
            StageSideBossFloor floor = root.AddComponent<StageSideBossFloor>();
            floor.Configure(collider, crumble);
            floors.Add(floor);
            return floor;
        }

        private void CreateStep(float x, float y, float width) => CreateFloor(x - width * 0.5f, x + width * 0.5f, y, false);

        private void CreateMovingFloor(Vector2 position, Vector2 size, Vector2 travel)
        {
            GameObject root = new GameObject("Moving Escape Floor");
            root.transform.SetParent(transform, false); root.transform.position = position;
            root.layer = 6;
            StageEscortController.AddFilledRect(root.transform, "Moving Fill", Vector2.zero, size, new Color(0.52f, 0.82f, 1f), -4);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.08f, 0.3f, 0.6f), -2);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>(); collider.size = size;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            root.AddComponent<StageSideBossMovingFloor>().Configure(position, travel);
        }

        private void CreateSpikes(float startX, float y, int count)
        {
            // Every spike row is introduced by an existing project jump pad so
            // players always receive the intended visual and gameplay warning.
            CreateJumpPad(startX - 2f, y + 0.05f);
            if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            if (objectFactory == null) objectFactory = gameObject.AddComponent<StageObjectFactory>();
            float width = count * 0.72f;
            StageObjectData data = StageObjectFactory.CreateDefaultData(
                StageObjectType.Spike,
                new Vector2(startX + width * 0.5f - 0.36f, y));
            data.objectId = "15-2_spikes_" + Mathf.RoundToInt(startX * 10f);
            data.size = new Vector2(width, 0.75f);
            data.keepSeparate = true;
            objectFactory.Create(data, transform);
        }

        private void CreateJumpPad(float x, float y)
        {
            if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            if (objectFactory == null) objectFactory = gameObject.AddComponent<StageObjectFactory>();
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.JumpPad, new Vector2(x, y));
            data.objectId = "15-2_jump_" + Mathf.RoundToInt(x * 10f) + "_" + Mathf.RoundToInt(y * 10f);
            data.size = Vector2.one;
            // The old stage-only pad launched at 13. This existing pad launches at
            // 39, preserving the requested roughly three-times higher jump.
            data.actionStrength = 39f;
            data.keepSeparate = true;
            GameObject jumpObject = objectFactory.Create(data, transform);
            JumpPad jumpPad = jumpObject != null ? jumpObject.GetComponentInChildren<JumpPad>(true) : null;
            // The common jump pad normally triples bird launch power. This stage
            // already uses a three-times stronger pad, so stacking both bonuses
            // would launch birds at 117 instead of the intended 39.
            jumpPad?.ConfigureBirdMultiplier(1f);
        }

        private void CreateCeilingBombButton(float x, float ceilingY)
        {
            CreateButtonCeiling(x, ceilingY);
            CreateJumpPad(x - 2.2f, FloorY + 0.55f);
            if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            if (objectFactory == null) objectFactory = gameObject.AddComponent<StageObjectFactory>();
            StageSideBossWeapon weapon = StageSideBossWeapon.Create(
                transform,
                weapons.Count,
                new Vector2(x, ceilingY - 0.72f),
                objectFactory,
                this,
                HasAuthority);
            weapons.Add(weapon);
        }

        private void CreateButtonCeiling(float x, float y)
        {
            GameObject root = new GameObject("Bomb Button Ceiling");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x, y, 0f);
            root.layer = 6;
            Vector2 size = new Vector2(4.2f, 0.55f);
            StageEscortController.AddFilledRect(root.transform, "Ceiling Fill", Vector2.zero, size, new Color(0.75f, 0.82f, 0.9f), -4);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.08f, 0.22f, 0.4f), -2);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>(); collider.size = size;
        }

        private void BuildBoss()
        {
            boss = new GameObject("15-2 Giant Pursuer").transform;
            boss.SetParent(transform, false);
            bossFace = new GameObject("Giant Boss Doodle").transform;
            bossFace.SetParent(boss, false);
            bossCore = AddDisc(bossFace, "Boss Ink", Vector2.zero, new Vector2(7.4f, 8.3f), new Color(0.25f, 0.04f, 0.38f), 70);
            AddDisc(bossFace, "Left Eye", new Vector2(-1.45f, 1.1f), new Vector2(1.2f, 1.5f), Color.white, 73);
            AddDisc(bossFace, "Right Eye", new Vector2(1.35f, 1.1f), new Vector2(1.2f, 1.5f), Color.white, 73);
            AddDisc(bossFace, "Left Pupil", new Vector2(-1.15f, 0.95f), new Vector2(0.42f, 0.62f), Color.black, 74);
            AddDisc(bossFace, "Right Pupil", new Vector2(1.65f, 0.95f), new Vector2(0.42f, 0.62f), Color.black, 74);
            Color ink = new Color(0.08f, 0.01f, 0.12f);
            StageEscortController.AddLine(bossFace, new Vector2(-2.2f, 2.2f), new Vector2(-0.55f, 1.65f), 0.3f, ink, 76);
            StageEscortController.AddLine(bossFace, new Vector2(2.15f, 2.25f), new Vector2(0.55f, 1.65f), 0.3f, ink, 76);
            StageEscortController.AddLine(bossFace, new Vector2(-2.25f, -1.25f), new Vector2(0f, -2.25f), 0.28f, Color.white, 76);
            StageEscortController.AddLine(bossFace, new Vector2(0f, -2.25f), new Vector2(2.25f, -1.25f), 0.28f, Color.white, 76);
            for (int i = 0; i < 7; i++)
            {
                float y = -1.25f - i % 2 * 0.65f;
                float x = -1.8f + i * 0.6f;
                StageEscortController.AddLine(bossFace, new Vector2(x, y), new Vector2(x + 0.25f, y - 0.75f), 0.16f, Color.white, 77);
            }
            StageEscortController.AddLine(bossFace, new Vector2(-3.1f, 1.5f), new Vector2(-4.3f, 3.4f), 0.34f, ink, 69);
            StageEscortController.AddLine(bossFace, new Vector2(3.1f, 1.5f), new Vector2(4.1f, 3.55f), 0.34f, ink, 69);
            StageEscortController.AddLine(bossFace, new Vector2(-3.4f, -0.4f), new Vector2(-5f, -1.6f), 0.38f, new Color(0.62f, 0.12f, 0.78f), 69);
            StageEscortController.AddLine(bossFace, new Vector2(3.4f, -0.4f), new Vector2(4.9f, -1.5f), 0.38f, new Color(0.62f, 0.12f, 0.78f), 69);
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("15-2 Chase Monitor");
            monitor.transform.SetParent(transform, false);
            StageEscortController.AddFilledRect(monitor.transform, "Frame", Vector2.zero, new Vector2(12.8f, 1.75f), new Color(0.12f, 0.16f, 0.2f, 0.94f), 180);
            StageEscortController.AddFilledRect(monitor.transform, "Screen", Vector2.zero, new Vector2(12.2f, 1.2f), new Color(0.01f, 0.04f, 0.055f), 181);
            StageEscortController.AddFilledRect(monitor.transform, "HP Track", new Vector2(0f, -0.28f), new Vector2(8.8f, 0.32f), new Color(0.18f, 0.2f, 0.22f), 182);
            GameObject fill = new GameObject("Boss HP Fill"); fill.transform.SetParent(monitor.transform, false);
            fill.transform.localPosition = new Vector3(-4.4f, -0.28f, -0.03f); fill.transform.localScale = new Vector3(8.8f, 0.22f, 1f);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>(); renderer.sprite = StageLinkedShieldSurvivalController.GetSquareSprite(); renderer.color = new Color(0.95f, 0.2f, 0.35f); renderer.sortingOrder = 184;
            hpFill = fill.transform;
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main", new Vector3(0f, 0.38f, -0.04f), 42, 0.1f, Color.white, 185);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub", new Vector3(0f, -0.66f, -0.04f), 28, 0.065f, new Color(0.45f, 0.9f, 1f), 185);
        }

        private void BeginAttack(float progress)
        {
            List<AttackType> choices = new List<AttackType> { AttackType.Barrage, AttackType.AimedShot };
            if (progress >= 0.32f) { choices.Add(AttackType.Laser); choices.Add(AttackType.BreakFloor); choices.Add(AttackType.GiantHand); }
            AttackType type = choices[attackCursor++ % choices.Count];
            if (progress >= 0.72f && Random.value < 0.55f) type = choices[Random.Range(0, choices.Count)];
            float interval = progress >= 0.72f ? 1.85f : progress >= 0.32f ? 2.75f : 3.8f;
            nextAttackAt = elapsed + interval;
            AttackState state = BuildAttack(type);
            ApplyAttack(state);
            if (stageManager.IsOnlineStageActive) Send(AttackKind, state);
        }

        private AttackState BuildAttack(AttackType type)
        {
            PlayerController2D targetPlayer = RandomLivingPlayer();
            Vector2 target = targetPlayer != null ? targetPlayer.transform.position : new Vector2(scrollX + 2f, FloorY + 1f);
            float lane = type == AttackType.Laser
                ? (Random.value < 0.5f ? FloorY + 1.05f : FloorY + 4.1f)
                : Random.Range(FloorY + 0.8f, FloorY + 5.5f);
            int floorIndex = FindBreakableFloorAhead();
            return new AttackState
            {
                Sequence = ++attackSequence,
                Type = (int)type,
                Origin = new Vector2(bossX + 2.8f, lane),
                Target = target,
                Lane = lane,
                FloorIndex = floorIndex
            };
        }

        private void ApplyAttack(AttackState state)
        {
            if (state == null || !receivedAttacks.Add(state.Sequence)) return;
            StartCoroutine(RunAttack(state));
        }

        private IEnumerator RunAttack(AttackState state)
        {
            AttackType type = (AttackType)state.Type;
            if (type == AttackType.Barrage) yield return RunBarrage(state);
            else if (type == AttackType.AimedShot) yield return RunAimedShot(state);
            else if (type == AttackType.Laser) yield return RunLaser(state);
            else if (type == AttackType.BreakFloor) yield return RunBreakFloor(state);
            else yield return RunGiantHand(state);
        }

        private IEnumerator RunBarrage(AttackState state)
        {
            TextMesh warning = CreateAttackText(new Vector2(scrollX - 6f, 5.8f), LocalizationManager.T("side_boss_barrage_warning"), new Color(1f, 0.55f, 0.15f));
            yield return new WaitForSeconds(0.9f);
            if (warning != null) Destroy(warning.gameObject);
            float attackScrollX = state.Origin.x + 15f;
            int count = attackScrollX >= 130f ? 9 : attackScrollX >= 60f ? 7 : 5;
            System.Random random = new System.Random(state.Sequence * 7919 + 152);
            for (int i = 0; i < count; i++)
            {
                float y = FloorY + 0.8f + (i % 4) * 1.55f + Mathf.Lerp(-0.25f, 0.25f, (float)random.NextDouble());
                float speed = Mathf.Lerp(8f, 11f, (float)random.NextDouble());
                Vector2 origin = new Vector2(state.Origin.x, y);
                StageSideBossProjectile.Create(transform, this, origin, Vector2.right * speed, HasAuthority, false);
                GameSfx.PlayAt(SfxId.CannonFire, origin, 0.35f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        private IEnumerator RunAimedShot(AttackState state)
        {
            GameObject marker = CreateTargetMarker(state.Target, 1.15f);
            yield return new WaitForSeconds(1.25f);
            if (marker != null) Destroy(marker);
            Vector2 direction = (state.Target - state.Origin).normalized;
            StageSideBossProjectile.Create(transform, this, state.Origin, direction * 17f, HasAuthority, true);
            GameSfx.PlayAt(SfxId.CannonFire, state.Origin, 0.8f);
        }

        private IEnumerator RunLaser(AttackState state)
        {
            Vector2 center = new Vector2(scrollX + 1f, state.Lane);
            GameObject warning = CreateWarningRect(center, new Vector2(34f, 2.2f), new Color(1f, 0.2f, 0.08f, 0.2f));
            TextMesh text = CreateAttackText(new Vector2(scrollX, state.Lane + 1.6f), LocalizationManager.T("side_boss_laser_warning"), new Color(1f, 0.45f, 0.12f));
            yield return new WaitForSeconds(1.65f);
            SetWarningColor(warning, new Color(1f, 0.08f, 0.03f, 0.78f));
            if (text != null) Destroy(text.gameObject);
            GameSfx.PlayAt(SfxId.BeamFire, new Vector2(bossX, state.Lane), 1f);
            if (HasAuthority) HitPlayersInHorizontalBand(state.Lane, 1.1f);
            yield return new WaitForSeconds(0.14f);
            if (warning != null) Destroy(warning);
        }

        private IEnumerator RunBreakFloor(AttackState state)
        {
            if (state.FloorIndex < 0 || state.FloorIndex >= floors.Count) yield break;
            StageSideBossFloor floor = floors[state.FloorIndex];
            if (floor == null) yield break;
            floor.SetWarning(true);
            TextMesh text = CreateAttackText(floor.transform.position + Vector3.up * 1.25f, LocalizationManager.T("side_boss_floor_warning"), Color.red);
            yield return new WaitForSeconds(1.45f);
            if (text != null) Destroy(text.gameObject);
            yield return floor.BreakTemporarily(3.8f);
        }

        private IEnumerator RunGiantHand(AttackState state)
        {
            Vector2 center = new Vector2(scrollX - 3f, state.Lane);
            GameObject warning = CreateWarningRect(center, new Vector2(25f, 1.8f), new Color(0.95f, 0.18f, 0.35f, 0.2f));
            TextMesh text = CreateAttackText(new Vector2(scrollX, state.Lane + 1.3f), LocalizationManager.T("side_boss_hand_warning"), new Color(1f, 0.35f, 0.55f));
            yield return new WaitForSeconds(1.35f);
            if (warning != null) Destroy(warning);
            if (text != null) Destroy(text.gameObject);
            GameObject hand = new GameObject("Giant Doodle Hand"); hand.transform.SetParent(transform, false); hand.transform.position = new Vector3(bossX + 1f, state.Lane, -0.3f);
            StageEscortController.AddFilledRect(hand.transform, "Arm", new Vector2(7f, 0f), new Vector2(14f, 1.25f), new Color(0.58f, 0.16f, 0.72f), 96);
            AddDisc(hand.transform, "Palm", new Vector2(14f, 0f), new Vector2(2.7f, 2.2f), new Color(0.7f, 0.22f, 0.88f), 97);
            for (int i = 0; i < 4; i++) StageEscortController.AddLine(hand.transform, new Vector2(13.5f + i * 0.35f, 0.5f), new Vector2(14.2f + i * 0.45f, 1.55f), 0.24f, new Color(0.12f, 0.02f, 0.18f), 98);
            float end = Time.time + 0.75f;
            while (Time.time < end)
            {
                if (HasAuthority) HitPlayersInRect(new Vector2(bossX + 8f, state.Lane), new Vector2(15.5f, 0.9f));
                yield return null;
            }
            Destroy(hand);
        }

        private void CheckCounterWeapons()
        {
            RefreshPlayersIfNeeded();
            for (int w = 0; w < weapons.Count; w++)
            {
                if (activatedWeapons[w] || weapons[w] == null) continue;
                for (int p = 0; p < players.Length; p++)
                {
                    PlayerController2D player = players[p];
                    if (!IsLiving(player) || Vector2.Distance(player.transform.position, weapons[w].transform.position) > 1.45f) continue;
                    ActivateCounterWeapon(w);
                    break;
                }
            }
        }

        private void CheckPlayers()
        {
            RefreshPlayersIfNeeded();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (!IsLiving(player)) continue;
                Vector3 p = player.transform.position;
                if (p.y < -8.4f || p.x <= bossX + 3.2f) RequestForcedElimination(player);
            }
        }

        private void CheckLocalFallOrCatch()
        {
            PlayerController2D local = stageManager.ActivePlayerTransform != null ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            if (IsLiving(local) && (local.transform.position.y < -8.4f || local.transform.position.x <= bossX + 2.8f)) RequestForcedElimination(local);
        }

        internal void ProjectileHit(PlayerController2D player) => RequestElimination(player);

        internal void NotifyCounterButtonTouched(int weaponIndex, PlayerController2D player)
        {
            if (player == null || weaponIndex < 0 || weaponIndex >= weapons.Count || activatedWeapons[weaponIndex]) return;
            string id = ResolvePlayerId(player);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (stageManager.IsOnlineStageActive && !HasAuthority)
            {
                if (id == onlineManager?.LocalPlayerId) Send(WeaponRequestKind, new WeaponRequest { Index = weaponIndex });
                return;
            }
            ActivateCounterWeapon(weaponIndex);
        }

        private void ActivateCounterWeapon(int weaponIndex)
        {
            if (!HasAuthority || weaponIndex < 0 || weaponIndex >= weapons.Count || activatedWeapons[weaponIndex]) return;
            activatedWeapons[weaponIndex] = true;
            weapons[weaponIndex]?.Activate(boss);
            BroadcastState(true);
        }

        internal void CounterBombLanded(int weaponIndex, Vector2 position)
        {
            if (!HasAuthority || weaponIndex < 0 || weaponIndex >= weapons.Count || !landedCounterBombs.Add(weaponIndex)) return;
            bossHealth = Mathf.Max(0, bossHealth - 1);
            if (bossCore != null) StartCoroutine(BossHitFlash());
            GameSfx.PlayAt(SfxId.BombExplosion, position, 1f);
            BroadcastState(true);
            if (bossHealth <= 0) StartCoroutine(DefeatBoss());
        }

        public override void RequestElimination(PlayerController2D player)
        {
            RequestEliminationCore(player, true);
        }

        private void RequestForcedElimination(PlayerController2D player)
        {
            RequestEliminationCore(player, false);
        }

        private void RequestEliminationCore(PlayerController2D player, bool canBlockWithShell)
        {
            if (player == null || phase == Phase.Defeated || phase == Phase.Failed) return;
            if (IsPlayerRedrawing(player)) return;
            if (canBlockWithShell && (player.IsInvulnerable || player.IsTurtleShelled)) return;
            string id = ResolvePlayerId(player);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (stageManager.IsOnlineStageActive && !HasAuthority)
            {
                if (id != onlineManager?.LocalPlayerId) return;
                Send(EliminateRequestKind, new EliminationState { PlayerId = id });
                return;
            }
            ConfirmElimination(id, stageManager.IsOnlineStageActive);
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast) Send(EliminateKind, new EliminationState { PlayerId = id });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null && !hiddenPlayers.Contains(player))
            {
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                player.ResetMotion(); player.SetControlsEnabled(false);
                hiddenPlayers.Add(player); player.gameObject.SetActive(false);
            }
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private IEnumerator DefeatBoss()
        {
            if (phase == Phase.Defeated) yield break;
            phase = Phase.Defeated; SetLocalControls(false); BroadcastState(true);
            for (int i = 0; i < 8; i++)
            {
                Vector2 point = (Vector2)boss.position + Random.insideUnitCircle * 3f;
                GameObject burst = new GameObject("15-2 Boss Defeat Burst"); burst.transform.position = point;
                burst.AddComponent<BombExplosionVisual>().Configure(Random.Range(0.8f, 1.7f), false);
                GameSfx.PlayAt(SfxId.BombExplosion, point, 0.6f);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(1.4f);
            if (HasAuthority) stageManager.ClearStage();
        }

        private void BeginFailure()
        {
            if (phase == Phase.Failed) return;
            phase = Phase.Failed; SetLocalControls(false); BroadcastState(true);
            if (!retryStarted) { retryStarted = true; StartCoroutine(RetryAfterDelay()); }
        }

        private IEnumerator RetryAfterDelay()
        {
            yield return new WaitForSeconds(2.5f);
            if (HasAuthority && stageManager != null && stageManager.CurrentStageId == StageId) stageManager.Retry();
        }

        private void RefreshCountdown()
        {
            if (phase != Phase.Ready) { uiManager?.SetChallengeCountdown(false, string.Empty); return; }
            float remaining = Mathf.Max(0f, StartDelay - elapsed);
            string value = Mathf.CeilToInt(remaining).ToString();
            uiManager?.SetChallengeCountdown(true, value);
        }

        private void UpdateCameraAndBoss()
        {
            if (gameCamera != null)
            {
                Vector3 target = new Vector3(scrollX, 0.7f, previousCameraPosition.z);
                gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            if (boss != null)
            {
                boss.position = new Vector3(bossX, 0.1f + Mathf.Sin(Time.time * 2f) * 0.3f, -0.25f);
                bossFace.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.4f) * 2.5f);
            }
            Transform monitor = monitorMain != null ? monitorMain.transform.parent : null;
            if (monitor != null) monitor.position = new Vector3(scrollX, 7.25f, -0.45f);
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null) return;
            monitorMain.text = LocalizationManager.Format("side_boss_hp", bossHealth, RequiredHits);
            monitorSub.text = LocalizationManager.T(phase == Phase.Ready ? "side_boss_ready" : phase == Phase.Defeated ? "side_boss_clear" : phase == Phase.Failed ? "side_boss_failed" : "side_boss_run");
            float ratio = Mathf.Clamp01(bossHealth / (float)RequiredHits);
            hpFill.localScale = new Vector3(8.8f * ratio, 0.22f, 1f);
            hpFill.localPosition = new Vector3(-4.4f + 4.4f * ratio, -0.28f, -0.03f);
        }

        private int FindBreakableFloorAhead()
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < floors.Count; i++)
                if (floors[i] != null && floors[i].transform.position.x > scrollX - 1f && floors[i].transform.position.x < scrollX + 13f) candidates.Add(i);
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
        }

        private void HitPlayersInHorizontalBand(float lane, float halfHeight)
        {
            RefreshPlayersIfNeeded();
            for (int i = 0; i < players.Length; i++) if (IsLiving(players[i]) && Mathf.Abs(players[i].transform.position.y - lane) <= halfHeight) RequestElimination(players[i]);
        }

        private void HitPlayersInRect(Vector2 center, Vector2 halfSize)
        {
            RefreshPlayersIfNeeded();
            for (int i = 0; i < players.Length; i++)
            {
                if (!IsLiving(players[i])) continue;
                Vector2 point = players[i].transform.position;
                if (Mathf.Abs(point.x - center.x) <= halfSize.x && Mathf.Abs(point.y - center.y) <= halfSize.y)
                    RequestElimination(players[i]);
            }
        }

        private PlayerController2D RandomLivingPlayer()
        {
            RefreshPlayersIfNeeded();
            List<PlayerController2D> living = new List<PlayerController2D>();
            for (int i = 0; i < players.Length; i++) if (IsLiving(players[i])) living.Add(players[i]);
            return living.Count > 0 ? living[Random.Range(0, living.Count)] : null;
        }

        private bool IsLiving(PlayerController2D player) => player != null
            && player.gameObject.activeInHierarchy
            && !IsPlayerRedrawing(player)
            && !eliminated.Contains(ResolvePlayerId(player));

        private static bool IsPlayerRedrawing(PlayerController2D player)
        {
            PlayerRedrawStateController state = player != null ? player.GetComponent<PlayerRedrawStateController>() : null;
            return state != null && state.IsRedrawing;
        }

        private void HandleRedrawProtection()
        {
            RefreshPlayersIfNeeded();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null) continue;
                string id = ResolvePlayerId(player);
                if (string.IsNullOrEmpty(id)) continue;
                if (IsPlayerRedrawing(player))
                {
                    redrawingPlayers.Add(id);
                    continue;
                }
                if (!redrawingPlayers.Remove(id) || eliminated.Contains(id) || !CanRelocateAfterRedraw(id)) continue;
                Vector2 safe = FindSafeReturnPosition();
                player.transform.position = new Vector3(safe.x, safe.y, player.transform.position.z);
                player.ResetMotion();
                Physics2D.SyncTransforms();
            }
        }

        private bool CanRelocateAfterRedraw(string playerId)
        {
            return !stageManager.IsOnlineStageActive || playerId == onlineManager?.LocalPlayerId;
        }

        private Vector2 FindSafeReturnPosition()
        {
            float desiredX = scrollX - 4.5f;
            float bestDistance = float.PositiveInfinity;
            Vector2 best = new Vector2(desiredX, FloorY + 1.35f);
            for (int i = 0; i < floors.Count; i++)
            {
                StageSideBossFloor floor = floors[i];
                BoxCollider2D collider = floor != null ? floor.GetComponent<BoxCollider2D>() : null;
                if (collider == null || !collider.enabled) continue;
                Bounds bounds = collider.bounds;
                if (bounds.max.x < scrollX - 7f || bounds.min.x > scrollX + 8f) continue;
                float x = Mathf.Clamp(desiredX, bounds.min.x + 0.8f, bounds.max.x - 0.8f);
                float distance = Mathf.Abs(x - desiredX);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = new Vector2(x, bounds.max.y + 1.1f);
            }
            return best;
        }

        private void RefreshPlayersIfNeeded() { if (Time.unscaledTime >= nextPlayerRefreshAt) RefreshPlayers(); }

        private void RefreshPlayers()
        {
            players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            nextPlayerRefreshAt = Time.unscaledTime + 0.45f;
        }

        private void CaptureParticipants()
        {
            if (stageManager.IsOnlineStageActive)
            {
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                if (roster != null) for (int i = 0; i < roster.Length; i++) if (roster[i] != null && !string.IsNullOrEmpty(roster[i].PlayerId)) participants.Add(roster[i].PlayerId);
            }
            else for (int i = 0; i < players.Length; i++) participants.Add(ResolvePlayerId(players[i]));
        }

        private bool AreAllEliminated()
        {
            if (participants.Count == 0) return false;
            foreach (string id in participants) if (!eliminated.Contains(id)) return false;
            return true;
        }

        private void PositionPlayersAtStart()
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].transform.position = new Vector3(-7f + i * 1.3f, FloorY + 1.3f, -0.2f);
                players[i].ResetMotion();
            }
        }

        private void SetLocalControls(bool value)
        {
            PlayerController2D active = stageManager?.ActivePlayerTransform != null ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(value && !stageManager.IsDrawingMode);
            if (stageManager != null && !stageManager.IsOnlineStageActive) stageManager.RemotePlayerController?.SetControlsEnabled(value);
        }

        private void LockCamera()
        {
            if (gameCamera == null) return;
            previousCameraPosition = gameCamera.transform.position; previousCameraSize = gameCamera.orthographicSize;
            cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            if (cameraFollow != null) { previousFollowEnabled = cameraFollow.enabled; cameraFollow.enabled = false; }
            gameCamera.orthographicSize = Mathf.Max(9f, 16.5f / Mathf.Max(0.1f, gameCamera.aspect));
        }

        private void RestoreStageState()
        {
            if (restored) return; restored = true;
            for (int i = 0; i < hiddenPlayers.Count; i++) if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear(); uiManager?.SetChallengeCountdown(false, string.Empty);
            if (gameCamera != null) { gameCamera.transform.position = previousCameraPosition; gameCamera.orthographicSize = previousCameraSize; }
            if (cameraFollow != null) cameraFollow.enabled = previousFollowEnabled;
        }

        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : stageManager.IsOnlineStageActive ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();

        private PlayerController2D ResolvePlayer(string id)
        {
            if (stageManager.IsOnlineStageActive) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] all = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) if (ResolvePlayerId(all[i]) == id) return all[i];
            return null;
        }

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
            if (roster == null) return false;
            for (int i = 0; i < roster.Length; i++) if (roster[i] != null && roster[i].IsHost && roster[i].PlayerId == id) return true;
            return false;
        }

        private void BroadcastState(bool force)
        {
            if (!stageManager.IsOnlineStageActive || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            Send(StateKind, new ChaseState
            {
                Sequence = ++sequence, Phase = (int)phase, Elapsed = elapsed, ScrollX = scrollX, BossX = bossX,
                BossHealth = bossHealth, ActivatedWeapons = (bool[])activatedWeapons.Clone(), EliminatedIds = new List<string>(eliminated).ToArray()
            });
        }

        private void ApplyState(ChaseState state)
        {
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence; phase = (Phase)Mathf.Clamp(state.Phase, 0, (int)Phase.Failed);
            elapsed = state.Elapsed; scrollX = Mathf.Lerp(scrollX, state.ScrollX, 0.65f); bossX = state.BossX; bossHealth = state.BossHealth;
            if (activatedWeapons == null || activatedWeapons.Length != weapons.Count) activatedWeapons = new bool[weapons.Count];
            if (state.ActivatedWeapons != null)
                for (int i = 0; i < Mathf.Min(weapons.Count, state.ActivatedWeapons.Length); i++)
                    if (state.ActivatedWeapons[i] && !activatedWeapons[i])
                    {
                        activatedWeapons[i] = true;
                        weapons[i]?.Activate(boss);
                    }
            if (state.EliminatedIds != null) for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            if (phase == Phase.Running && !controlsReleased) { controlsReleased = true; SetLocalControls(true); }
            else if (phase == Phase.Defeated || phase == Phase.Failed) SetLocalControls(false);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && !HasAuthority && IsHost(data.PlayerId)) ApplyState(JsonUtility.FromJson<ChaseState>(data.Json));
            else if (data.Kind == AttackKind && !HasAuthority && IsHost(data.PlayerId)) ApplyAttack(JsonUtility.FromJson<AttackState>(data.Json));
            else if (data.Kind == WeaponRequestKind && HasAuthority)
            {
                WeaponRequest request = JsonUtility.FromJson<WeaponRequest>(data.Json);
                if (request != null && !string.IsNullOrEmpty(data.PlayerId) && !eliminated.Contains(data.PlayerId)) ActivateCounterWeapon(request.Index);
            }
            else if (data.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(data.Json);
                if (request != null && request.PlayerId == data.PlayerId) ConfirmElimination(request.PlayerId, true);
            }
            else if (data.Kind == EliminateKind && !HasAuthority && IsHost(data.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(data.Json); if (state != null) ApplyElimination(state.PlayerId);
            }
        }

        private void Send(string kind, object state)
        {
            if (onlineManager != null) onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = kind, Json = JsonUtility.ToJson(state) });
        }

        private IEnumerator BossHitFlash()
        {
            if (bossCore == null) yield break; Color old = bossCore.color; bossCore.color = Color.white;
            yield return new WaitForSeconds(0.12f); if (bossCore != null) bossCore.color = old;
        }

        private GameObject CreateTargetMarker(Vector2 position, float radius)
        {
            GameObject root = new GameObject("Aimed Shot Target"); root.transform.SetParent(transform, false); root.transform.position = position;
            AddDisc(root.transform, "Target Ring", Vector2.zero, Vector2.one * radius * 2f, new Color(1f, 0.08f, 0.12f, 0.22f), 120);
            StageEscortController.AddLine(root.transform, Vector2.left * radius, Vector2.right * radius, 0.1f, Color.red, 122);
            StageEscortController.AddLine(root.transform, Vector2.down * radius, Vector2.up * radius, 0.1f, Color.red, 122);
            return root;
        }

        private GameObject CreateWarningRect(Vector2 position, Vector2 size, Color color)
        {
            GameObject root = new GameObject("15-2 Attack Warning"); root.transform.SetParent(transform, false); root.transform.position = position;
            StageEscortController.AddFilledRect(root.transform, "Warning Fill", Vector2.zero, size, color, 110);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(1f, 0.12f, 0.08f), 112); return root;
        }

        private static void SetWarningColor(GameObject warning, Color color)
        {
            if (warning == null) return; SpriteRenderer[] renderers = warning.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].color = color;
        }

        private TextMesh CreateAttackText(Vector2 position, string value, Color color)
        {
            TextMesh text = StageEscortController.CreateText(transform, "15-2 Attack Text", new Vector3(position.x, position.y, -0.4f), 42, 0.1f, color, 160); text.text = value; return text;
        }

        private static SpriteRenderer AddDisc(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = StageSurvivalController.GetCircleSprite(); renderer.color = color; renderer.sortingOrder = order; return renderer;
        }
    }

    public sealed class StageSideBossProjectile : MonoBehaviour
    {
        private StageSideScrollBossChaseController owner;
        private Vector2 velocity;
        private bool authoritative;
        private float expiresAt;

        public static void Create(Transform parent, StageSideScrollBossChaseController owner, Vector2 position, Vector2 velocity, bool authoritative, bool fast)
        {
            GameObject root = new GameObject(fast ? "Aimed Boss Bullet" : "Boss Barrage Bullet"); root.transform.SetParent(parent, false); root.transform.position = position;
            StageSideBossProjectile projectile = root.AddComponent<StageSideBossProjectile>(); projectile.owner = owner; projectile.velocity = velocity; projectile.authoritative = authoritative; projectile.expiresAt = Time.time + 6f;
            Vector2 size = fast ? new Vector2(1.25f, 0.42f) : new Vector2(0.72f, 0.72f);
            StageEscortController.AddFilledRect(root.transform, "Ink Shot", Vector2.zero, size, fast ? new Color(1f, 0.18f, 0.12f) : new Color(0.75f, 0.2f, 0.95f), 125);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.15f, 0.02f, 0.2f), 127);
        }

        private void Update()
        {
            Vector2 previous = transform.position; transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            if (authoritative && owner != null)
            {
                PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < players.Length; i++)
                {
                    if (DistanceToSegment(players[i].transform.position, previous, transform.position) > 0.55f) continue;
                    owner.ProjectileHit(players[i]); Destroy(gameObject); return;
                }
            }
            if (Time.time >= expiresAt) Destroy(gameObject);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }
    }

    public sealed class StageSideBossFloor : MonoBehaviour
    {
        private BoxCollider2D floorCollider;
        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private bool crumble;
        private bool busy;

        public void Configure(BoxCollider2D collider, bool isCrumbling)
        {
            floorCollider = collider; crumble = isCrumbling; renderers = GetComponentsInChildren<SpriteRenderer>(); originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!crumble || busy || collision.collider.GetComponentInParent<PlayerController2D>() == null) return;
            StartCoroutine(CrumbleAfterDelay());
        }

        private IEnumerator CrumbleAfterDelay()
        {
            busy = true; SetWarning(true); yield return new WaitForSeconds(0.8f); yield return BreakTemporarily(2.4f); busy = false;
        }

        public void SetWarning(bool value)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) renderers[i].color = value ? new Color(1f, 0.18f, 0.12f, 0.85f) : originalColors[i];
        }

        public IEnumerator BreakTemporarily(float duration)
        {
            busy = true; if (floorCollider != null) floorCollider.enabled = false;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
            yield return new WaitForSeconds(duration);
            if (floorCollider != null) floorCollider.enabled = true;
            for (int i = 0; i < renderers.Length; i++) { renderers[i].enabled = true; renderers[i].color = originalColors[i]; }
            busy = false;
        }
    }

    public sealed class StageSideBossMovingFloor : MonoBehaviour
    {
        private Vector2 origin;
        private Vector2 travel;
        private Rigidbody2D body;
        public void Configure(Vector2 start, Vector2 offset) { origin = start; travel = offset; body = GetComponent<Rigidbody2D>(); }
        private void FixedUpdate() { Vector2 target = origin + travel * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.25f)); if (body != null) body.MovePosition(target); else transform.position = target; }
    }

    public sealed class StageSideBossWeapon : MonoBehaviour
    {
        private StageSideScrollBossChaseController owner;
        private int weaponIndex;
        private bool authoritative;
        private SpriteRenderer[] buttonRenderers;
        private Color[] originalColors;
        private bool activated;

        public static StageSideBossWeapon Create(
            Transform parent,
            int index,
            Vector2 position,
            StageObjectFactory factory,
            StageSideScrollBossChaseController owner,
            bool authoritative)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.Button, position);
            data.objectId = "15-2_boss_bomb_button_" + index;
            data.size = new Vector2(1.55f, 0.72f);
            data.rotation = 180f;
            data.keepSeparate = true;
            GameObject root = factory.Create(data, parent);
            if (root == null) return null;
            root.name = "Ceiling Boss Bomb Button " + (index + 1);
            StageSideBossWeapon weapon = root.AddComponent<StageSideBossWeapon>();
            weapon.owner = owner; weapon.weaponIndex = index; weapon.authoritative = authoritative;
            weapon.buttonRenderers = root.GetComponentsInChildren<SpriteRenderer>();
            weapon.originalColors = new Color[weapon.buttonRenderers.Length];
            for (int i = 0; i < weapon.buttonRenderers.Length; i++) weapon.originalColors[i] = weapon.buttonRenderers[i].color;
            GameObject mark = new GameObject("Giant Bomb Mark"); mark.transform.SetParent(root.transform, false); mark.transform.localPosition = new Vector3(0f, -0.95f, -0.05f); mark.transform.localScale = Vector3.one * 0.72f;
            SpriteRenderer icon = mark.AddComponent<SpriteRenderer>(); icon.sprite = StageSurvivalController.GetCircleSprite(); icon.color = new Color(0.12f, 0.12f, 0.15f); icon.sortingOrder = 28;
            StageEscortController.AddLine(mark.transform, new Vector2(0.18f, 0.32f), new Vector2(0.42f, 0.62f), 0.11f, new Color(1f, 0.5f, 0.12f), 29);
            return weapon;
        }

        public void Activate(Transform boss)
        {
            if (activated) return; SetActivated(true);
            if (boss == null) return;
            GameObject projectile = new GameObject("Falling Giant Boss Bomb");
            projectile.transform.position = boss.position + Vector3.up * 10f;
            projectile.AddComponent<StageSideBossCounterImpact>().Configure(boss, authoritative ? owner : null, weaponIndex);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null) owner?.NotifyCounterButtonTouched(weaponIndex, player);
        }

        public void SetActivated(bool value)
        {
            activated = value;
            if (buttonRenderers == null) return;
            for (int i = 0; i < buttonRenderers.Length; i++)
                if (buttonRenderers[i] != null) buttonRenderers[i].color = value ? new Color(0.3f, 0.3f, 0.32f, originalColors[i].a) : originalColors[i];
        }
    }

    public sealed class StageSideBossCounterImpact : MonoBehaviour
    {
        private Transform boss;
        private StageSideScrollBossChaseController owner;
        private int weaponIndex;
        private float fallSpeed;

        public void Configure(Transform targetBoss, StageSideScrollBossChaseController authoritativeOwner, int index)
        {
            boss = targetBoss; owner = authoritativeOwner; weaponIndex = index;
            GameObject outline = new GameObject("Bomb Outline"); outline.transform.SetParent(transform, false); outline.transform.localScale = Vector3.one * 4.2f;
            SpriteRenderer rim = outline.AddComponent<SpriteRenderer>(); rim.sprite = StageSurvivalController.GetCircleSprite(); rim.color = new Color(0.08f, 0.05f, 0.06f); rim.sortingOrder = 143;
            GameObject body = new GameObject("Bomb Body"); body.transform.SetParent(transform, false); body.transform.localScale = Vector3.one * 3.75f;
            SpriteRenderer fill = body.AddComponent<SpriteRenderer>(); fill.sprite = StageSurvivalController.GetCircleSprite(); fill.color = new Color(0.82f, 0.12f, 0.16f); fill.sortingOrder = 144;
            StageEscortController.AddLine(transform, new Vector2(0.75f, 1.55f), new Vector2(1.45f, 2.5f), 0.28f, new Color(0.12f, 0.08f, 0.08f), 145);
            StageEscortController.AddLine(transform, new Vector2(1.45f, 2.5f), new Vector2(1.8f, 2.9f), 0.18f, new Color(1f, 0.65f, 0.12f), 146);
        }

        private void Update()
        {
            if (boss == null) { Destroy(gameObject); return; }
            fallSpeed = Mathf.MoveTowards(fallSpeed, 18f, 22f * Time.deltaTime);
            Vector3 position = transform.position;
            position.x = Mathf.MoveTowards(position.x, boss.position.x, 8f * Time.deltaTime);
            position.y -= fallSpeed * Time.deltaTime;
            transform.position = position;
            transform.Rotate(0f, 0f, 85f * Time.deltaTime);
            if (position.y > boss.position.y + 2.2f) return;
            Vector2 impact = boss.position + Vector3.up * 1.7f;
            GameObject burst = new GameObject("Giant Bomb Boss Impact"); burst.transform.position = impact; burst.AddComponent<BombExplosionVisual>().Configure(3.6f, false);
            owner?.CounterBombLanded(weaponIndex, impact);
            Destroy(gameObject);
        }
    }
}
