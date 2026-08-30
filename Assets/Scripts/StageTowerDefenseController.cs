using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageTowerDefenseController : MonoBehaviour
    {
        private const string StateKind = "tower_defense_state";
        private const string EnemyStateKind = "tower_defense_enemy_state";
        private const string ButtonRequestKind = "tower_defense_button_request";
        private const float IntroSeconds = 3f;
        private const float PhaseSeconds = 30f;
        private const float ButtonCooldown = 8f;
        private const float AirstrikeWarning = 3f;
        private const float ArenaHalfWidth = 22f;
        private const float GroundY = -3.65f;

        private enum MatchState { Intro, Playing, Failed, Clear }

        [System.Serializable]
        private sealed class EnemySnapshot
        {
            public string Id;
            public int Type;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Speed;
            public int Hp;
            public int MaxHp;
        }

        [System.Serializable]
        private sealed class DefenseState
        {
            public int Sequence;
            public int State;
            public float Remaining;
            public float IntroRemaining;
            public float LeftCooldown;
            public float RightCooldown;
            public float LeftWarning;
            public float RightWarning;
            public float OutcomeRemaining;
            public EnemySnapshot[] Enemies;
        }

        [System.Serializable]
        private sealed class EnemyBatch
        {
            public int Sequence;
            public int BatchIndex;
            public int BatchCount;
            public EnemySnapshot[] Enemies;
        }

        [System.Serializable]
        private sealed class ButtonRequest { public bool Left; }

        private sealed class TrajectoryVisual
        {
            public Transform Root;
            public readonly List<SpriteRenderer> Dots = new List<SpriteRenderer>();
            public SpriteRenderer Impact;
            public Color Color;
        }

        private readonly Dictionary<string, StageTowerDefenseEnemyHealth> enemies =
            new Dictionary<string, StageTowerDefenseEnemyHealth>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private StageGimmickSyncManager syncManager;
        private StageTowerDefenseAlly ally;
        private StageTowerDefenseButton leftButton;
        private StageTowerDefenseButton rightButton;
        private TextMesh titleText;
        private TextMesh phaseText;
        private TextMesh timerText;
        private TextMesh messageText;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private bool cameraWasEnabled;
        private bool cameraLocked;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private MatchState matchState = MatchState.Intro;
        private float totalSeconds = 90f;
        private float remainingSeconds = 90f;
        private float introRemaining = IntroSeconds;
        private float nextSpawnAt;
        private float leftCooldown;
        private float rightCooldown;
        private float leftWarning;
        private float rightWarning;
        private float failedRemaining;
        private float clearRemaining;
        private float nextStateAt;
        private int enemySequence;
        private int stateSequence;
        private int lastStateSequence;
        private int pendingEnemySequence = -1;
        private int pendingEnemyBatchCount;
        private readonly Dictionary<int, EnemySnapshot[]> pendingEnemyBatches =
            new Dictionary<int, EnemySnapshot[]>();
        private string stageId = "8-3";
        private bool hardMode;
        private StageBombDropper hardBombLauncher;
        private StageMissileLauncher hardMissileLauncher;
        private StageOscillatingAim hardBombAim;
        private StageOscillatingAim hardMissileAim;
        private TrajectoryVisual bombTrajectory;
        private TrajectoryVisual missileTrajectory;

        public bool HasAuthority => stageManager == null || !stageManager.IsOnlineStageActive || stageManager.IsOnlineStageHost;
        internal bool ShouldAllySpeak => hardMode;

        public void Configure(float seconds)
        {
            totalSeconds = Mathf.Max(90f, seconds);
            remainingSeconds = totalSeconds;
        }

        public void ConfigureHardMode(float seconds)
        {
            stageId = "13-1";
            hardMode = true;
            totalSeconds = Mathf.Max(60f, seconds);
            remainingSeconds = totalSeconds;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
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
            if (cameraLocked && cameraFollow != null) cameraFollow.enabled = cameraWasEnabled;
            if (cameraLocked && gameCamera != null)
            {
                gameCamera.transform.position = previousCameraPosition;
                gameCamera.orthographicSize = previousCameraSize;
            }
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            if (!hardMode) BuildAirstrikeFloorZones();
            BuildMonitor();
            LockCameraToArena();
            ally = StageTowerDefenseAlly.Create(transform, hardMode ? new Vector2(-19.5f, -2.98f) : new Vector2(0f, -2.98f), this);
            leftButton = AttachButton(hardMode ? "13-1_bomb_button" : "8-3_left_button", true);
            rightButton = AttachButton(hardMode ? "13-1_missile_button" : "8-3_right_button", false);
            if (hardMode) ConfigureHardModeLaunchers();
            nextSpawnAt = Time.time + IntroSeconds + (hardMode ? 0.35f : 1.1f);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != stageId) return;

            if (hardMode) RefreshLaunchTrajectories();

            if (!HasAuthority)
            {
                // Continue the shared clock locally between host snapshots. The
                // next snapshot still corrects drift, but launcher aiming and
                // its trajectory no longer advance in visible 150 ms steps.
                float replicaDt = Time.deltaTime;
                leftCooldown = Mathf.Max(0f, leftCooldown - replicaDt);
                rightCooldown = Mathf.Max(0f, rightCooldown - replicaDt);
                if (matchState == MatchState.Intro)
                    introRemaining = Mathf.Max(0f, introRemaining - replicaDt);
                else if (matchState == MatchState.Playing)
                    remainingSeconds = Mathf.Max(0f, remainingSeconds - replicaDt);
                else if (matchState == MatchState.Failed)
                    failedRemaining = Mathf.Max(0f, failedRemaining - replicaDt);
                else if (matchState == MatchState.Clear)
                    clearRemaining = Mathf.Max(0f, clearRemaining - replicaDt);
                RefreshButtons();
                RefreshDisplay();
                return;
            }

            float dt = Time.deltaTime;
            leftCooldown = Mathf.Max(0f, leftCooldown - dt);
            rightCooldown = Mathf.Max(0f, rightCooldown - dt);
            UpdateAirstrike(ref leftWarning, true, dt);
            UpdateAirstrike(ref rightWarning, false, dt);

            if (matchState == MatchState.Intro)
            {
                introRemaining -= dt;
                if (introRemaining <= 0f) matchState = MatchState.Playing;
            }
            else if (matchState == MatchState.Playing)
            {
                remainingSeconds = Mathf.Max(0f, remainingSeconds - dt);
                RemoveDefeatedEnemies();
                if (Time.time >= nextSpawnAt) SpawnWavePair();
                if (remainingSeconds <= 0f) BeginClear();
            }
            else if (matchState == MatchState.Failed)
            {
                failedRemaining -= dt;
                if (failedRemaining <= 0f) stageManager.Retry();
            }
            else if (matchState == MatchState.Clear)
            {
                clearRemaining -= dt;
                if (clearRemaining <= 0f) stageManager.ClearStage();
            }

            BroadcastState();
            RefreshButtons();
            RefreshDisplay();
        }

        public void RequestButton(bool left)
        {
            if (matchState != MatchState.Playing) return;
            if (!HasAuthority)
            {
                onlineManager?.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = stageId,
                    Kind = ButtonRequestKind,
                    Json = JsonUtility.ToJson(new ButtonRequest { Left = left })
                });
                return;
            }
            ActivateButton(left);
        }

        private void ActivateButton(bool left)
        {
            if (left ? leftCooldown > 0f || leftWarning > 0f : rightCooldown > 0f || rightWarning > 0f) return;
            if (hardMode)
            {
                if (left)
                {
                    leftCooldown = 1f;
                    hardBombLauncher?.ActivateFromLink();
                }
                else
                {
                    rightCooldown = 1f;
                    hardMissileLauncher?.ActivateFromLink();
                }
                BroadcastState(true);
                return;
            }
            if (left)
            {
                leftCooldown = ButtonCooldown;
                leftWarning = AirstrikeWarning;
            }
            else
            {
                rightCooldown = ButtonCooldown;
                rightWarning = AirstrikeWarning;
            }
            GameSfx.PlayAt(SfxId.BombTick, new Vector2(left ? -10f : 10f, 0f), 1.05f);
            BroadcastState(true);
        }

        private void UpdateAirstrike(ref float warning, bool left, float dt)
        {
            if (warning <= 0f) return;
            float before = warning;
            warning = Mathf.Max(0f, warning - dt);
            if (Mathf.CeilToInt(before) != Mathf.CeilToInt(warning))
                GameSfx.PlayAt(SfxId.BombTick, new Vector2(left ? -10f : 10f, 1f), 0.9f);
            if (warning <= 0f) LaunchAirstrike(left);
        }

        private void LaunchAirstrike(bool left)
        {
            int phase = CurrentPhase();
            int count = 8 + phase * 3;
            float minX = left ? -19f : 6f;
            float maxX = left ? -6f : 19f;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = new Vector2(Random.Range(minX, maxX), Random.Range(6.4f, 9f));
                float size = Random.Range(0.72f, 1.2f);
                string id = "8-3_airstrike_" + (left ? "l_" : "r_") + (++enemySequence);
                GameObject bomb = syncManager != null
                    ? syncManager.SpawnDropperBox(id, StageObjectType.Bomb, position, size, Random.Range(-18f, 18f), Random.Range(2.0f, 3.1f), new Vector2(Random.Range(-0.5f, 0.5f), -1.5f))
                    : factory?.CreateDroppedBox(StageObjectType.Bomb, id, position, size, transform, Random.Range(2.0f, 3.1f));
                Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
                if (body != null && syncManager == null) body.linearVelocity = new Vector2(Random.Range(-0.5f, 0.5f), -1.5f);
            }
            GameSfx.PlayAt(SfxId.CannonFire, new Vector2(left ? -11f : 11f, 5f), 0.85f);
        }

        private void SpawnWavePair()
        {
            int phase = CurrentPhase();
            if (hardMode)
            {
                int baseCount = phase == 1 ? Random.Range(3, 5) : phase == 2 ? Random.Range(4, 6) : Random.Range(5, 7);
                int participantCount = Mathf.Clamp(
                    stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1,
                    1,
                    4);
                float participantMultiplier = Mathf.Pow(1.3f, participantCount - 1);
                int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * participantMultiplier));
                for (int i = 0; i < count; i++) SpawnEnemy(false, phase, i);
                nextSpawnAt = Time.time + (phase == 1 ? 3.4f : phase == 2 ? 2.8f : 2.2f);
                return;
            }
            int eachSide = phase == 1 ? 1 : phase == 2 ? Random.Range(1, 3) : Random.Range(2, 4);
            int normalParticipantCount = Mathf.Clamp(
                stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1,
                1,
                4);
            int waveCount = Mathf.Max(1, Mathf.CeilToInt(eachSide * 2f * normalParticipantCount / 4f));
            for (int i = 0; i < waveCount; i++)
                SpawnEnemy(i % 2 == 0, phase, i);
            nextSpawnAt = Time.time + (phase == 1 ? 4.1f : phase == 2 ? 3.15f : 2.3f);
        }

        private void SpawnEnemy(bool fromLeft, int phase, int lane)
        {
            StageObjectType type;
            int pattern = (enemySequence + lane + (fromLeft ? 1 : 0))
                % (hardMode ? phase == 1 ? 5 : 6 : phase == 1 ? 2 : phase == 2 ? 5 : 6);
            if (hardMode)
            {
                if (pattern == 1) type = StageObjectType.EnemyJumper;
                else if (pattern == 2) type = StageObjectType.EnemyFlyer;
                else if (pattern == 3) type = StageObjectType.EnemyShooter;
                else if (pattern == 4) type = StageObjectType.EnemyCharger;
                else if (pattern == 5) type = StageObjectType.EnemyShooter;
                else type = StageObjectType.EnemyWalker;
            }
            else if (pattern == 1) type = StageObjectType.EnemyJumper;
            else if (pattern == 2) type = StageObjectType.EnemyShooter;
            else if (pattern == 3) type = StageObjectType.EnemyCharger;
            else if (pattern == 4) type = phase >= 3 ? StageObjectType.EnemyFlyer : StageObjectType.EnemyBomber;
            else if (pattern == 5) type = StageObjectType.EnemyBomber;
            else type = StageObjectType.EnemyWalker;
            float size = Random.Range(0.82f, 1.05f) + (phase - 1) * 0.08f;
            float speed = type == StageObjectType.EnemyBomber
                ? Random.Range(1.05f, 1.35f)
                : Random.Range(1.7f, 2.15f) + phase * 0.42f;
            if (hardMode) speed *= 1.85f;
            string id = stageId + "_enemy_" + (++enemySequence);
            float spawnY = type == StageObjectType.EnemyBomber ? Random.Range(2.4f, hardMode ? 5.2f : 3.8f)
                : type == StageObjectType.EnemyFlyer ? hardMode ? Random.Range(1.3f, 5.4f) : -2.55f
                : GroundY + size * 0.58f + lane * 0.04f;
            float edge = hardMode ? 21.3f : 20.2f;
            Vector2 position = new Vector2(fromLeft ? -edge : edge, spawnY);
            GameObject obj = factory?.CreateSpawnedEnemy(type, id, position, size, transform, speed, fromLeft ? 1f : -1f);
            StageEnemyCharacter enemy = obj != null ? obj.GetComponent<StageEnemyCharacter>() : null;
            if (enemy == null) return;
            ConfigureBomberIfNeeded(obj, type, phase);
            StageTowerDefenseEnemyHealth health = obj.AddComponent<StageTowerDefenseEnemyHealth>();
            int enemyHp = hardMode
                ? phase == 1 ? (enemySequence % 5 == 0 ? 2 : 1)
                    : phase == 2 ? (enemySequence % 3 == 0 ? 2 : 1)
                    : enemySequence % 4 == 0 ? 3 : 2
                : phase <= 1 || enemySequence % 2 == 0 ? 1 : 2;
            health.Configure(this, enemyHp, enemyHp);
            enemies[id] = health;
        }

        private void ConfigureBomberIfNeeded(GameObject obj, StageObjectType type, int phase)
        {
            if (obj == null || type != StageObjectType.EnemyBomber) return;
            StageBombingEnemy bomber = obj.GetComponent<StageBombingEnemy>();
            if (bomber == null) bomber = obj.AddComponent<StageBombingEnemy>();
            bomber.Configure(factory, transform, phase >= 3 ? 3.6f : 4.6f, phase >= 3 ? 0.8f : 0.68f);
        }

        public void NotifyEnemyDefeated(StageTowerDefenseEnemyHealth enemy)
        {
            if (enemy == null) return;
            enemies.Remove(enemy.ObjectId);
        }

        public void NotifyAllyHit()
        {
            if (!HasAuthority || matchState != MatchState.Playing) return;
            matchState = MatchState.Failed;
            failedRemaining = 3f;
            ally?.PlayDefeat();
            GameSfx.PlayAt(SfxId.PlayerDeath, ally != null ? ally.transform.position : Vector3.zero, 1.1f);
            BroadcastState(true);
        }

        private void BeginClear()
        {
            if (matchState != MatchState.Playing) return;
            matchState = MatchState.Clear;
            clearRemaining = 3.2f;
            StageTowerDefenseEnemyHealth[] remainingEnemies = new StageTowerDefenseEnemyHealth[enemies.Count];
            enemies.Values.CopyTo(remainingEnemies, 0);
            for (int i = 0; i < remainingEnemies.Length; i++) remainingEnemies[i]?.ForceDefeat();
            enemies.Clear();
            ally?.PlayClearFlight();
            GameSfx.PlayAt(SfxId.EmotePop, ally != null ? ally.transform.position : Vector3.zero, 1.15f);
            BroadcastState(true);
        }

        private int CurrentPhase()
        {
            float elapsed = totalSeconds - remainingSeconds;
            float phaseLength = hardMode ? 20f : PhaseSeconds;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed / phaseLength) + 1, 1, 3);
        }

        private void ConfigureHardModeLaunchers()
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker == null) continue;
                if (marker.objectId == "13-1_bomb_launcher")
                {
                    hardBombLauncher = marker.GetComponent<StageBombDropper>();
                    hardBombLauncher?.PrepareForLink();
                    hardBombLauncher?.SetLinkedLaunchTuning(1f, 14f);
                    StageOscillatingAim aim = marker.GetComponent<StageOscillatingAim>();
                    if (aim == null) aim = marker.gameObject.AddComponent<StageOscillatingAim>();
                    aim.Configure(45f, 45f, 1.05f);
                    hardBombAim = aim;
                }
                else if (marker.objectId == "13-1_missile_launcher")
                {
                    hardMissileLauncher = marker.GetComponent<StageMissileLauncher>();
                    hardMissileLauncher?.PrepareForLink();
                    hardMissileLauncher?.SetLinkCooldown(1f);
                    StageOscillatingAim aim = marker.GetComponent<StageOscillatingAim>();
                    if (aim == null) aim = marker.gameObject.AddComponent<StageOscillatingAim>();
                    aim.Configure(-45f, 45f, 0.92f);
                    hardMissileAim = aim;
                }
                else if (marker.objectId == "13-1_upper_button_platform")
                {
                    StageRicochetBulletPassage passage = marker.GetComponent<StageRicochetBulletPassage>();
                    if (passage == null) passage = marker.gameObject.AddComponent<StageRicochetBulletPassage>();
                    passage.SetAllowsBullet(true);
                }
            }

            bombTrajectory = CreateTrajectoryVisual("Bomb Landing Preview", new Color(1f, 0.38f, 0.08f, 0.86f));
            missileTrajectory = CreateTrajectoryVisual("Missile Landing Preview", new Color(0.12f, 0.72f, 1f, 0.86f));
        }

        private TrajectoryVisual CreateTrajectoryVisual(string objectName, Color color)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            TrajectoryVisual visual = new TrajectoryVisual { Root = root.transform, Color = color };
            for (int i = 0; i < 56; i++)
            {
                GameObject dot = new GameObject("Prediction Dot " + (i + 1));
                dot.transform.SetParent(root.transform, false);
                dot.transform.localScale = Vector3.one * (i % 4 == 0 ? 0.145f : 0.105f);
                SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = color;
                renderer.sortingOrder = 54;
                visual.Dots.Add(renderer);
            }

            GameObject impact = new GameObject("Predicted Impact");
            impact.transform.SetParent(root.transform, false);
            visual.Impact = impact.AddComponent<SpriteRenderer>();
            visual.Impact.sprite = DoodleRuntimeAssets.CircleSprite;
            visual.Impact.color = new Color(color.r, color.g, color.b, 0.34f);
            visual.Impact.sortingOrder = 53;
            impact.SetActive(false);
            return visual;
        }

        private void RefreshLaunchTrajectories()
        {
            float aimClock = matchState == MatchState.Intro
                ? Mathf.Max(0f, IntroSeconds - introRemaining)
                : IntroSeconds + Mathf.Max(0f, totalSeconds - remainingSeconds);
            hardBombAim?.ApplyExternalClock(aimClock);
            hardMissileAim?.ApplyExternalClock(aimClock);

            if (bombTrajectory == null && hardBombLauncher != null)
                bombTrajectory = CreateTrajectoryVisual("Bomb Landing Preview", new Color(1f, 0.38f, 0.08f, 0.86f));
            if (missileTrajectory == null && hardMissileLauncher != null)
                missileTrajectory = CreateTrajectoryVisual("Missile Landing Preview", new Color(0.12f, 0.72f, 1f, 0.86f));

            if (hardBombLauncher != null
                && hardBombLauncher.TryGetLaunchPrediction(out Vector2 bombOrigin, out Vector2 bombVelocity))
                RefreshTrajectory(bombTrajectory, hardBombLauncher.transform, bombOrigin, bombVelocity, true);
            else SetTrajectoryVisible(bombTrajectory, false);

            if (hardMissileLauncher != null
                && hardMissileLauncher.TryGetLaunchPrediction(out Vector2 missileOrigin, out Vector2 missileVelocity))
                RefreshTrajectory(missileTrajectory, hardMissileLauncher.transform, missileOrigin, missileVelocity, false);
            else SetTrajectoryVisible(missileTrajectory, false);
        }

        private static void RefreshTrajectory(
            TrajectoryVisual visual,
            Transform launcher,
            Vector2 origin,
            Vector2 initialVelocity,
            bool ballistic)
        {
            if (visual == null) return;
            SetTrajectoryVisible(visual, true);
            Vector2 position = origin;
            Vector2 velocity = initialVelocity;
            const float stepSeconds = 0.04f;
            int dotIndex = 0;
            bool impacted = false;
            Vector2 impactPoint = position;

            for (int step = 0; step < 90 && dotIndex < visual.Dots.Count; step++)
            {
                Vector2 nextVelocity = ballistic
                    ? velocity + Physics2D.gravity * stepSeconds
                    : velocity;
                if (ballistic) nextVelocity /= 1f + 0.12f * stepSeconds;
                Vector2 nextPosition = position + (velocity + nextVelocity) * (0.5f * stepSeconds);
                if (TryFindTerrainHit(position, nextPosition, launcher, out Vector2 hitPoint))
                {
                    impactPoint = hitPoint;
                    impacted = true;
                    break;
                }

                SpriteRenderer dot = visual.Dots[dotIndex++];
                dot.transform.position = new Vector3(nextPosition.x, nextPosition.y, -0.46f);
                dot.enabled = true;
                position = nextPosition;
                velocity = nextVelocity;
                if (Mathf.Abs(position.x) > ArenaHalfWidth + 2f || position.y < GroundY - 2f || position.y > 11f)
                    break;
            }

            for (int i = dotIndex; i < visual.Dots.Count; i++) visual.Dots[i].enabled = false;
            visual.Impact.gameObject.SetActive(impacted);
            if (impacted)
            {
                visual.Impact.transform.position = new Vector3(impactPoint.x, impactPoint.y, -0.45f);
                float pulse = 0.62f + Mathf.Sin(Time.unscaledTime * 7f) * 0.1f;
                visual.Impact.transform.localScale = new Vector3(pulse * 1.55f, pulse * 0.42f, 1f);
            }
        }

        private static bool TryFindTerrainHit(Vector2 from, Vector2 to, Transform launcher, out Vector2 point)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || collider.gameObject.layer != 6) continue;
                if (launcher != null && (collider.transform == launcher || collider.transform.IsChildOf(launcher))) continue;
                point = hits[i].point;
                return true;
            }
            point = to;
            return false;
        }

        private static void SetTrajectoryVisible(TrajectoryVisual visual, bool visible)
        {
            if (visual?.Root != null) visual.Root.gameObject.SetActive(visible);
        }

        private void RemoveDefeatedEnemies()
        {
            List<string> remove = null;
            foreach (KeyValuePair<string, StageTowerDefenseEnemyHealth> pair in enemies)
            {
                if (pair.Value == null || pair.Value.IsDefeated)
                {
                    if (remove == null) remove = new List<string>();
                    remove.Add(pair.Key);
                }
            }
            if (remove != null) for (int i = 0; i < remove.Count; i++) enemies.Remove(remove[i]);
        }

        private StageTowerDefenseButton AttachButton(string id, bool left)
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].objectId != id) continue;
                StageTowerDefenseButton button = markers[i].gameObject.AddComponent<StageTowerDefenseButton>();
                button.Configure(this, left, hardMode);
                return button;
            }
            return null;
        }

        private void RefreshButtons()
        {
            leftButton?.Refresh(leftCooldown, leftWarning);
            rightButton?.Refresh(rightCooldown, rightWarning);
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject(stageId + " Defense Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(0f, 5.65f, 0f);
            Vector2 frameSize = new Vector2(14.5f, 2.25f);
            DoodleMonitorVisuals.Build(monitor.transform, frameSize, 20);
            if (hardMode)
            {
                phaseText = CreateText(monitor.transform, new Vector2(-3.25f, 0f), 0.14f, 26, new Color(0.78f, 0.39f, 0.06f, 1f));
                timerText = CreateText(monitor.transform, new Vector2(3.25f, 0f), 0.17f, 26, new Color(0.04f, 0.43f, 0.58f, 1f));
            }
            else
            {
                phaseText = CreateText(monitor.transform, new Vector2(-3.25f, 0f), 0.14f, 26, new Color(0.78f, 0.39f, 0.06f, 1f));
                timerText = CreateText(monitor.transform, new Vector2(3.25f, 0f), 0.17f, 26, new Color(0.04f, 0.43f, 0.58f, 1f));
            }
        }

        private void LockCameraToArena()
        {
            if (gameCamera == null) return;
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            cameraWasEnabled = cameraFollow != null && cameraFollow.enabled;
            if (cameraFollow != null) cameraFollow.enabled = false;
            float requiredForWidth = 23.5f / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 1.3f, previousCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(9.5f, requiredForWidth);
            cameraLocked = true;
        }

        private void BuildAirstrikeFloorZones()
        {
            CreateAirstrikeFloorZone(true);
            CreateAirstrikeFloorZone(false);
        }

        private void CreateAirstrikeFloorZone(bool left)
        {
            float centerX = left ? -12.5f : 12.5f;
            Color baseColor = left
                ? new Color(1f, 0.48f, 0.16f, 0.62f)
                : new Color(0.95f, 0.2f, 0.3f, 0.62f);
            GameObject zone = new GameObject(left ? "Left Airstrike Floor" : "Right Airstrike Floor");
            zone.transform.SetParent(transform, false);
            zone.transform.position = new Vector3(centerX, -3.76f, 0f);
            zone.layer = 2;
            StageGun.CreateSprite(zone.transform, "Airstrike Color Band", Vector2.zero,
                new Vector2(13f, 0.24f), baseColor, 19);
            for (int i = -6; i <= 6; i++)
            {
                float x = i;
                StageGun.AddLine(zone.transform, "Airstrike Hatch", new[]
                {
                    new Vector2(x - 0.35f, -0.45f), new Vector2(x + 0.35f, 0.45f)
                }, 0.055f, new Color(1f, 0.9f, 0.45f, 0.78f), 20);
            }
            StageGun.AddLine(zone.transform, "Airstrike Edge", new[]
            {
                new Vector2(-6.5f, 0.48f), new Vector2(6.5f, 0.48f)
            }, 0.07f, new Color(baseColor.r * 0.65f, baseColor.g * 0.65f, baseColor.b * 0.65f, 0.95f), 21);
            foreach (Transform child in zone.transform) child.gameObject.layer = 2;
            Collider2D[] accidentalColliders = zone.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < accidentalColliders.Length; i++)
                if (accidentalColliders[i] != null) Destroy(accidentalColliders[i]);
        }

        private static TextMesh CreateText(Transform parent, Vector2 position, float size, int order, Color color)
        {
            GameObject obj = new GameObject("Monitor Text");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(position.x, position.y, -0.08f);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = size;
            text.fontSize = 64;
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

        private void RefreshDisplay()
        {
            if (phaseText == null || timerText == null) return;
            phaseText.text = LocalizationManager.Format("tower_defense_phase", CurrentPhase(), 3);
            timerText.text = Mathf.CeilToInt(remainingSeconds).ToString("00") + ".0";
        }

        private void BroadcastState(bool force = false)
        {
            if (onlineManager == null || stageManager == null || !stageManager.IsOnlineStageActive || !HasAuthority
                || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            List<EnemySnapshot> snapshots = new List<EnemySnapshot>();
            foreach (StageTowerDefenseEnemyHealth health in enemies.Values)
            {
                if (health == null || health.IsDefeated) continue;
                Rigidbody2D body = health.GetComponent<Rigidbody2D>();
                snapshots.Add(new EnemySnapshot
                {
                    Id = health.ObjectId, Type = (int)health.EnemyType, Position = health.transform.position,
                    Velocity = body != null ? body.linearVelocity : Vector2.zero, Size = health.EnemySize,
                    Speed = health.EnemySpeed, Hp = health.Hp, MaxHp = health.MaxHp
                });
            }
            DefenseState state = new DefenseState
            {
                Sequence = ++stateSequence, State = (int)matchState, Remaining = remainingSeconds,
                IntroRemaining = introRemaining, LeftCooldown = leftCooldown, RightCooldown = rightCooldown,
                LeftWarning = leftWarning, RightWarning = rightWarning,
                OutcomeRemaining = matchState == MatchState.Failed ? failedRemaining : clearRemaining,
                Enemies = null
            };
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = stageId, Kind = StateKind, Json = JsonUtility.ToJson(state) });
            const int enemiesPerBatch = 3;
            int batchCount = Mathf.Max(1, Mathf.CeilToInt(snapshots.Count / (float)enemiesPerBatch));
            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                int start = batchIndex * enemiesPerBatch;
                int count = Mathf.Min(enemiesPerBatch, snapshots.Count - start);
                EnemySnapshot[] batchSnapshots = new EnemySnapshot[Mathf.Max(0, count)];
                for (int i = 0; i < count; i++) batchSnapshots[i] = snapshots[start + i];
                EnemyBatch batch = new EnemyBatch
                {
                    Sequence = state.Sequence,
                    BatchIndex = batchIndex,
                    BatchCount = batchCount,
                    Enemies = batchSnapshots
                };
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = stageId,
                    Kind = EnemyStateKind,
                    Json = JsonUtility.ToJson(batch)
                });
            }
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != stageId) return;
            if (data.Kind == ButtonRequestKind && HasAuthority)
            {
                ButtonRequest request = JsonUtility.FromJson<ButtonRequest>(data.Json);
                if (request != null) ActivateButton(request.Left);
                return;
            }
            if (data.Kind == EnemyStateKind && !HasAuthority)
            {
                ReceiveEnemyBatch(JsonUtility.FromJson<EnemyBatch>(data.Json));
                return;
            }
            if (data.Kind != StateKind || HasAuthority) return;
            DefenseState state = JsonUtility.FromJson<DefenseState>(data.Json);
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            MatchState previous = matchState;
            matchState = (MatchState)state.State;
            remainingSeconds = state.Remaining;
            introRemaining = state.IntroRemaining;
            leftCooldown = state.LeftCooldown;
            rightCooldown = state.RightCooldown;
            leftWarning = state.LeftWarning;
            rightWarning = state.RightWarning;
            if (matchState == MatchState.Failed) failedRemaining = state.OutcomeRemaining;
            else if (matchState == MatchState.Clear) clearRemaining = state.OutcomeRemaining;
            if (previous != matchState)
            {
                if (matchState == MatchState.Failed) ally?.PlayDefeat();
                else if (matchState == MatchState.Clear) ally?.PlayClearFlight();
            }
        }

        private void ReceiveEnemyBatch(EnemyBatch batch)
        {
            if (batch == null || batch.Sequence < pendingEnemySequence || batch.BatchCount <= 0
                || batch.BatchIndex < 0 || batch.BatchIndex >= batch.BatchCount) return;
            if (batch.Sequence > pendingEnemySequence)
            {
                pendingEnemySequence = batch.Sequence;
                pendingEnemyBatchCount = batch.BatchCount;
                pendingEnemyBatches.Clear();
            }
            if (batch.BatchCount != pendingEnemyBatchCount) return;
            pendingEnemyBatches[batch.BatchIndex] = batch.Enemies ?? new EnemySnapshot[0];
            if (pendingEnemyBatches.Count < pendingEnemyBatchCount) return;
            List<EnemySnapshot> combined = new List<EnemySnapshot>();
            for (int i = 0; i < pendingEnemyBatchCount; i++)
            {
                if (!pendingEnemyBatches.TryGetValue(i, out EnemySnapshot[] part)) return;
                combined.AddRange(part);
            }
            ApplyEnemySnapshots(combined.ToArray());
            pendingEnemyBatches.Clear();
        }

        private void ApplyEnemySnapshots(EnemySnapshot[] snapshots)
        {
            HashSet<string> seen = new HashSet<string>();
            if (snapshots != null)
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    EnemySnapshot snapshot = snapshots[i];
                    if (snapshot == null || string.IsNullOrEmpty(snapshot.Id)) continue;
                    seen.Add(snapshot.Id);
                    if (!enemies.TryGetValue(snapshot.Id, out StageTowerDefenseEnemyHealth health) || health == null)
                    {
                        float facing = snapshot.Velocity.x < 0f ? -1f : 1f;
                        GameObject obj = factory?.CreateSpawnedEnemy((StageObjectType)snapshot.Type, snapshot.Id, snapshot.Position,
                            snapshot.Size, transform, snapshot.Speed, facing);
                        ConfigureBomberIfNeeded(obj, (StageObjectType)snapshot.Type, CurrentPhase());
                        health = obj != null ? obj.AddComponent<StageTowerDefenseEnemyHealth>() : null;
                        if (health == null) continue;
                        health.Configure(this, snapshot.MaxHp, snapshot.MaxHp);
                        enemies[snapshot.Id] = health;
                    }
                    health.ApplyReplica(snapshot.Position, snapshot.Velocity, snapshot.Hp);
                }
            }
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, StageTowerDefenseEnemyHealth> pair in enemies)
                if (!seen.Contains(pair.Key)) { pair.Value?.ForceDefeat(); remove.Add(pair.Key); }
            for (int i = 0; i < remove.Count; i++) enemies.Remove(remove[i]);
        }
    }

    public sealed class StageTowerDefenseEnemyHealth : MonoBehaviour
    {
        private StageTowerDefenseController owner;
        private StageEnemyCharacter enemy;
        private int hp;
        private int maxHp;
        private float size;
        private float speed;
        private StageObjectType enemyType;
        private TextMesh badge;
        private Vector2 replicaTarget;
        private Vector2 replicaVelocity;
        private bool replicaMode;

        public string ObjectId => enemy != null ? enemy.ObjectId : gameObject.name;
        public bool IsDefeated => enemy == null || enemy.IsDefeated || hp <= 0;
        public int Hp => hp;
        public int MaxHp => maxHp;
        public float EnemySize => size;
        public float EnemySpeed => speed;
        public StageObjectType EnemyType => enemyType;

        public void Configure(StageTowerDefenseController controller, int health, int maximum)
        {
            owner = controller;
            enemy = GetComponent<StageEnemyCharacter>();
            StageEditorObject marker = GetComponent<StageEditorObject>();
            enemyType = marker != null ? marker.type : StageObjectType.EnemyWalker;
            size = marker != null ? Mathf.Max(marker.size.x, marker.size.y) : 1f;
            speed = marker != null ? marker.movementSpeed : 2f;
            maxHp = Mathf.Max(1, maximum);
            hp = Mathf.Clamp(health, 1, maxHp);
            if (maxHp > 1) CreateBadge();
        }

        public void HitByBullet(Vector2 point)
        {
            if (owner == null || !owner.HasAuthority || IsDefeated) return;
            hp--;
            RefreshBadge();
            if (hp <= 0) ForceDefeat();
            else GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.8f);
        }

        public void HitByBomb()
        {
            if (owner == null || !owner.HasAuthority || IsDefeated) return;
            hp = 0;
            ForceDefeat();
        }

        public void ApplyReplica(Vector2 position, Vector2 velocity, int health)
        {
            hp = Mathf.Clamp(health, 0, maxHp);
            replicaMode = true;
            replicaTarget = position;
            replicaVelocity = velocity;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            if ((current - position).sqrMagnitude > 16f)
            {
                if (body != null) body.position = position;
                else transform.position = position;
            }
            RefreshBadge();
        }

        private void LateUpdate()
        {
            if (!replicaMode || IsDefeated) return;
            Vector2 predicted = replicaTarget + replicaVelocity * 0.075f;
            float blend = 1f - Mathf.Exp(-15f * Time.deltaTime);
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            Vector2 next = Vector2.Lerp(current, predicted, blend);
            if (body != null)
            {
                body.position = next;
                body.linearVelocity = replicaVelocity;
            }
            else transform.position = next;
        }

        public void ForceDefeat()
        {
            if (enemy == null || enemy.IsDefeated) return;
            hp = 0;
            enemy.ApplyDefeated();
            owner?.NotifyEnemyDefeated(this);
        }

        private void CreateBadge()
        {
            if (badge != null) return;
            GameObject obj = new GameObject("Enemy Toughness");
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(0f, size * 0.75f, -0.08f);
            badge = obj.AddComponent<TextMesh>();
            badge.anchor = TextAnchor.MiddleCenter;
            badge.alignment = TextAlignment.Center;
            badge.fontSize = 42;
            badge.characterSize = 0.065f;
            badge.color = new Color(0.55f, 0.08f, 0.12f, 1f);
            obj.GetComponent<MeshRenderer>().sortingOrder = 42;
            RefreshBadge();
        }

        private void RefreshBadge()
        {
            if (badge != null) badge.text = hp > 1 ? "\u2665" + hp : string.Empty;
        }
    }

    public sealed class StageTowerDefenseButton : MonoBehaviour
    {
        private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
        private StageTowerDefenseController owner;
        private bool left;
        private TextMesh label;
        private SpriteRenderer glow;
        private float nextLocalRequestAt;
        private bool showStatusLabel;

        public void Configure(StageTowerDefenseController controller, bool isLeft, bool showStatus)
        {
            owner = controller;
            left = isLeft;
            showStatusLabel = showStatus;
            BoxCollider2D trigger = GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Mathf.Max(1.35f, trigger.size.x), Mathf.Max(1.8f, trigger.size.y));
            trigger.offset = new Vector2(trigger.offset.x, 0.45f);
            GameObject glowObject = StageGun.CreateSprite(transform, "Airstrike Button Glow", new Vector2(0f, 0.18f),
                new Vector2(1.15f, 0.34f), new Color(0.2f, 0.9f, 0.75f, 0.75f), 41);
            glow = glowObject.GetComponent<SpriteRenderer>();
            label = CreateLabel();
            label.gameObject.SetActive(showStatusLabel);
        }

        private TextMesh CreateLabel()
        {
            GameObject obj = new GameObject("Cooldown");
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(0f, 0.75f, -0.08f);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.07f;
            text.color = new Color(0.08f, 0.2f, 0.24f, 1f);
            obj.GetComponent<MeshRenderer>().sortingOrder = 43;
            return text;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryPress(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryPress(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null) contacts.Remove(other);
        }

        private void TryPress(Collider2D other)
        {
            if (other == null || other.GetComponentInParent<PlayerController2D>() == null || contacts.Contains(other)) return;
            bool wasEmpty = contacts.Count == 0;
            contacts.Add(other);
            if (!wasEmpty || Time.time < nextLocalRequestAt) return;
            nextLocalRequestAt = Time.time + 0.5f;
            owner?.RequestButton(left);
        }

        public void Refresh(float cooldown, float warning)
        {
            if (warning > 0f)
            {
                if (label != null && showStatusLabel)
                {
                    label.text = Mathf.CeilToInt(warning).ToString();
                    label.color = new Color(1f, 0.22f, 0.1f, 1f);
                }
                if (glow != null) glow.color = new Color(1f, 0.2f, 0.08f, 0.7f + Mathf.Sin(Time.time * 12f) * 0.2f);
            }
            else if (cooldown > 0f)
            {
                if (label != null && showStatusLabel)
                {
                    label.text = cooldown.ToString("0.0");
                    label.color = new Color(0.3f, 0.34f, 0.38f, 1f);
                }
                if (glow != null) glow.color = new Color(0.3f, 0.34f, 0.38f, 0.45f);
            }
            else
            {
                if (label != null && showStatusLabel)
                {
                    label.text = LocalizationManager.T("stage_ready_label");
                    label.color = new Color(0.05f, 0.48f, 0.3f, 1f);
                }
                if (glow != null) glow.color = new Color(0.15f, 0.95f, 0.68f, 0.72f);
            }
        }
    }

    public sealed class StageTowerDefenseAlly : MonoBehaviour
    {
        private StageTowerDefenseController owner;
        private Transform visual;
        private bool finished;
        private TextMesh speech;
        private float nextSpeechAt;
        private float hideSpeechAt;
        private static readonly string[] FunnyWords = { "HELP!", "NOPE!", "DUDE!", "YIKES!", "BRUH!" };

        public static StageTowerDefenseAlly Create(Transform parent, Vector2 position, StageTowerDefenseController owner)
        {
            GameObject root = new GameObject("8-3 Ally Character");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.78f;
            trigger.isTrigger = true;
            StageTowerDefenseAlly ally = root.AddComponent<StageTowerDefenseAlly>();
            root.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 8;
            ally.owner = owner;
            ally.BuildVisual();
            ally.nextSpeechAt = Time.time + Random.Range(2.5f, 5f);
            return ally;
        }

        private void BuildVisual()
        {
            visual = new GameObject("Ally Visual").transform;
            visual.SetParent(transform, false);
            Color outline = new Color(0.05f, 0.2f, 0.38f, 1f);
            Color blue = new Color(0.18f, 0.63f, 0.92f, 1f);
            Color face = new Color(0.76f, 0.94f, 1f, 1f);
            Color scarf = new Color(1f, 0.46f, 0.25f, 1f);

            GameObject body = StageGun.CreateSprite(visual, "Round Body", new Vector2(0f, -0.12f),
                new Vector2(0.72f, 0.86f), blue, 38);
            body.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
            StageGun.AddLine(visual, "Body Outline", EllipsePoints(new Vector2(0f, -0.12f), 0.37f, 0.44f), 0.065f, outline, 40);

            GameObject head = StageGun.CreateSprite(visual, "Round Head", new Vector2(0f, 0.54f),
                new Vector2(0.9f, 0.82f), face, 39);
            head.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
            StageGun.AddLine(visual, "Head Outline", EllipsePoints(new Vector2(0f, 0.54f), 0.46f, 0.42f), 0.065f, outline, 41);
            StageGun.AddLine(visual, "Hair Tuft", new[]
            {
                new Vector2(-0.25f, 0.9f), new Vector2(-0.08f, 1.08f), new Vector2(0.02f, 0.91f),
                new Vector2(0.19f, 1.05f), new Vector2(0.27f, 0.86f)
            }, 0.075f, blue, 42);

            AddDot(new Vector2(-0.17f, 0.62f), 0.075f, outline);
            AddDot(new Vector2(0.17f, 0.62f), 0.075f, outline);
            StageGun.AddLine(visual, "Friendly Smile", new[]
            {
                new Vector2(-0.17f, 0.43f), new Vector2(0f, 0.34f), new Vector2(0.18f, 0.44f)
            }, 0.05f, outline, 43);
            AddDot(new Vector2(-0.34f, 0.45f), 0.09f, new Color(1f, 0.55f, 0.58f, 0.55f));
            AddDot(new Vector2(0.34f, 0.45f), 0.09f, new Color(1f, 0.55f, 0.58f, 0.55f));

            StageGun.AddLine(visual, "Scarf", new[]
            {
                new Vector2(-0.34f, 0.17f), new Vector2(0.33f, 0.17f), new Vector2(0.55f, -0.04f)
            }, 0.105f, scarf, 43);
            StageGun.AddLine(visual, "Left Arm", new[]
            {
                new Vector2(-0.31f, 0.02f), new Vector2(-0.67f, -0.12f), new Vector2(-0.78f, 0.08f)
            }, 0.075f, outline, 41);
            StageGun.AddLine(visual, "Right Arm", new[]
            {
                new Vector2(0.31f, 0.02f), new Vector2(0.67f, -0.12f), new Vector2(0.78f, 0.08f)
            }, 0.075f, outline, 41);
            StageGun.AddLine(visual, "Left Leg", new[]
            {
                new Vector2(-0.18f, -0.45f), new Vector2(-0.22f, -0.78f), new Vector2(-0.42f, -0.8f)
            }, 0.085f, outline, 41);
            StageGun.AddLine(visual, "Right Leg", new[]
            {
                new Vector2(0.18f, -0.45f), new Vector2(0.22f, -0.78f), new Vector2(0.42f, -0.8f)
            }, 0.085f, outline, 41);

            GameObject heart = StageGun.CreateSprite(visual, "Friend Heart", new Vector2(0f, -0.12f),
                new Vector2(0.18f, 0.18f), new Color(1f, 0.25f, 0.32f, 1f), 43);
            heart.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
            NicoDrawBossArt.Apply(visual, "ally-defense", new Vector2(1.55f, 1.9f), 43);

            GameObject speechObject = new GameObject("Ally Speech");
            speechObject.transform.SetParent(transform, false);
            speechObject.transform.localPosition = new Vector3(0f, 1.65f, -0.1f);
            speech = speechObject.AddComponent<TextMesh>();
            speech.anchor = TextAnchor.MiddleCenter;
            speech.alignment = TextAlignment.Center;
            speech.fontSize = 54;
            speech.characterSize = 0.075f;
            speech.fontStyle = FontStyle.Bold;
            speech.color = new Color(0.08f, 0.2f, 0.38f, 1f);
            speechObject.GetComponent<MeshRenderer>().sortingOrder = 48;
        }

        private void Update()
        {
            if (speech == null) return;
            if (hideSpeechAt > 0f && Time.time >= hideSpeechAt)
            {
                speech.text = string.Empty;
                hideSpeechAt = 0f;
            }
            if (finished || owner == null || !owner.ShouldAllySpeak || Time.time < nextSpeechAt) return;
            speech.text = FunnyWords[Random.Range(0, FunnyWords.Length)];
            hideSpeechAt = Time.time + 1.25f;
            nextSpeechAt = Time.time + Random.Range(4.5f, 8f);
        }

        private void AddDot(Vector2 position, float size, Color color)
        {
            GameObject dot = StageGun.CreateSprite(visual, "Face Dot", position, Vector2.one * size, color, 43);
            dot.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
        }

        private static Vector2[] EllipsePoints(Vector2 center, float radiusX, float radiusY)
        {
            const int count = 32;
            Vector2[] points = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }
            return points;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (finished || other == null || other.GetComponentInParent<StageTowerDefenseEnemyHealth>() == null) return;
            owner?.NotifyAllyHit();
        }

        public void TryHitByEnemyBomb(Vector2 blastCenter, float blastRadius)
        {
            if (finished) return;
            float hitRadius = Mathf.Max(0.2f, blastRadius) + 0.72f;
            if (((Vector2)transform.position - blastCenter).sqrMagnitude <= hitRadius * hitRadius)
                owner?.NotifyAllyHit();
        }

        public void PlayDefeat()
        {
            if (finished) return;
            finished = true;
            StartCoroutine(DefeatRoutine());
        }

        public void PlayClearFlight()
        {
            if (finished) return;
            finished = true;
            CreateWings();
            StartCoroutine(FlyRoutine());
        }

        private void CreateWings()
        {
            Color wing = new Color(1f, 0.92f, 0.48f, 0.95f);
            StageGun.AddLine(visual, "Left Clear Wing", new[] { new Vector2(-0.38f, 0.2f), new Vector2(-1.25f, 0.9f), new Vector2(-1.5f, 0.15f), new Vector2(-0.62f, -0.15f) }, 0.12f, wing, 42);
            StageGun.AddLine(visual, "Right Clear Wing", new[] { new Vector2(0.38f, 0.2f), new Vector2(1.25f, 0.9f), new Vector2(1.5f, 0.15f), new Vector2(0.62f, -0.15f) }, 0.12f, wing, 42);
        }

        private IEnumerator FlyRoutine()
        {
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                transform.position += new Vector3(Mathf.Sin(elapsed * 4f) * 0.012f, Time.deltaTime * Mathf.Lerp(1f, 5f, elapsed / 3f), 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 7f) * 7f);
                yield return null;
            }
        }

        private IEnumerator DefeatRoutine()
        {
            float elapsed = 0f;
            while (elapsed < 0.55f)
            {
                elapsed += Time.deltaTime;
                visual.localRotation = Quaternion.Euler(0f, 0f, elapsed * 240f);
                visual.localScale = Vector3.one * Mathf.Max(0.15f, 1f - elapsed);
                yield return null;
            }
        }
    }
}
