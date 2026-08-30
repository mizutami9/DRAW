using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageJumpRopeController : StageEliminationChallengeController
    {
        private const string StageId = "6-2";
        private const string KindState = "jump_rope_state";
        private const string KindAttack = "jump_rope_attack";
        private const string KindEliminateRequest = "jump_rope_eliminate_request";
        private const string KindEliminated = "jump_rope_eliminated";
        private const float FloorY = -2f;
        private const float ArenaHalfWidth = 13f;
        private const float FloorWidth = 26f;
        private const float RopeHalfWidth = 12.2f;
        private const float IntroSeconds = 2.8f;
        private const float CountdownSeconds = 4f;
        private const float DangerWarningSeconds = 1.15f;

        private enum Phase { Intro, Countdown, Playing, Finished, Failed }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Phase;
            public float Remaining;
            public float Elapsed;
            public float PhaseRemaining;
            public float RopePhase;
            public float RopePeriod;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class AttackState
        {
            public int Sequence;
            public int Type;
            public int Variant;
            public Vector2 Position;
            public Vector2 Direction;
            public float Speed;
            public float Size;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<int> appliedAttackSequences = new HashSet<int>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private CameraFollow2D cameraFollow;
        private StageJumpRopeHazard ropeHazard;
        private LineRenderer ropeLine;
        private SpriteRenderer leftHandle;
        private SpriteRenderer rightHandle;
        private TextMesh monitorMain;
        private TextMesh jumpPrompt;
        private readonly List<SpriteRenderer> landingGuideDashes = new List<SpriteRenderer>();
        private Phase phase = Phase.Intro;
        private float durationSeconds = 60f;
        private float remainingSeconds = 60f;
        private float phaseRemaining = IntroSeconds;
        private float elapsedSeconds;
        private float ropePhase = 0.32f;
        private float ropePeriod = 3.2f;
        private float nextBombAt = 12f;
        private float nextMissileAt = 20f;
        private float nextEnemyAt = 16f;
        private float nextHurdleAt = 1.8f;
        private float nextStateAt;
        private float restartRemaining;
        private float previousCameraMinimum = 8f;
        private int bombSequence;
        private int attackSequence;
        private int stateSequence;
        private int lastStateSequence;
        private bool restoredPlayers;
        private bool slowCalloutPlayed;
        private bool accelerationCalloutPlayed;
        private bool ropeWarningPlayed;

        public void Configure(float seconds)
        {
            durationSeconds = Mathf.Clamp(seconds > 0f ? seconds : 60f, 30f, 180f);
            remainingSeconds = durationSeconds;
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            if (cameraFollow != null) cameraFollow.SetMinimumOrthographicSize(previousCameraMinimum);
            RestoreHiddenPlayers();
        }

        private void Start()
        {
            BuildArena();
            if (cameraFollow != null)
            {
                previousCameraMinimum = cameraFollow.MinimumOrthographicSize;
                cameraFollow.SetMinimumOrthographicSize(8.2f);
            }
            CaptureParticipants();
            SetLocalControls(false);
            RefreshMonitor();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (IsOnlineActive() && !HasAuthority())
            {
                UpdateReplica();
                return;
            }

            BroadcastState();
            ApplyPendingEliminations();
            CheckFalls();

            if (phase == Phase.Finished) return;
            if (phase == Phase.Failed)
            {
                restartRemaining -= Time.deltaTime;
                RefreshMonitor();
                if (restartRemaining <= 0f) stageManager.Retry();
                return;
            }
            if (phase == Phase.Intro)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    phase = Phase.Countdown;
                    phaseRemaining = CountdownSeconds;
                    GameSfx.Play(SfxId.StageCountdownTick);
                }
                RefreshMonitor();
                return;
            }
            if (phase == Phase.Countdown)
            {
                phaseRemaining -= Time.deltaTime;
                if (phaseRemaining <= 0f)
                {
                    phase = Phase.Playing;
                    phaseRemaining = 0f;
                    SetLocalControls(true);
                    GameSfx.Play(SfxId.StageCountdownGo);
                    BroadcastState(true);
                }
                RefreshMonitor();
                return;
            }

            elapsedSeconds += Time.deltaTime;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            UpdateHurdleDirector();
            UpdateAttackDirector();

            if (!slowCalloutPlayed && elapsedSeconds >= 40f)
            {
                slowCalloutPlayed = true;
                GameSfx.Play(SfxId.UiButtonPress);
            }
            if (!accelerationCalloutPlayed && elapsedSeconds >= 45f)
            {
                accelerationCalloutPlayed = true;
                GameSfx.Play(SfxId.CannonFire);
            }

            if (AreAllPlayersEliminated())
            {
                BeginFailure();
                return;
            }
            if (remainingSeconds <= 0f)
            {
                // A fall and the final timer tick can occur in the same frame.
                // Never award survival clear unless a participant still exists
                // above the fall boundary after processing that frame's falls.
                if (!HasActiveSurvivor())
                {
                    BeginFailure();
                    return;
                }
                phase = Phase.Finished;
                ropeHazard?.SetDangerous(false);
                SetLocalControls(false);
                SetMonitorMain(LocalizationManager.T("clear"), 0.17f);
                SetMonitorSub(LocalizationManager.T("jump_rope_clear_sub"), 0.1f);
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            RefreshMonitor();
        }

        private void UpdateReplica()
        {
            ApplyPendingEliminations();
            CheckFalls();
            if (phase == Phase.Intro || phase == Phase.Countdown)
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
            if (phase == Phase.Playing)
            {
                elapsedSeconds += Time.deltaTime;
                remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            }
            RefreshMonitor();
        }

        private float GetRopePeriod(float elapsed)
        {
            if (elapsed < 40f) return Mathf.Lerp(3.2f, 1.55f, elapsed / 40f);
            if (elapsed < 45f) return Mathf.Lerp(3.65f, 3.05f, (elapsed - 40f) / 5f);
            return Mathf.Lerp(1.45f, 0.85f, Mathf.Clamp01((elapsed - 45f) / Mathf.Max(1f, durationSeconds - 45f)));
        }

        private void UpdateHurdleDirector()
        {
            if (elapsedSeconds < nextHurdleAt) return;

            float interval;
            float speed;
            if (elapsedSeconds < 40f)
            {
                float progress = elapsedSeconds / 40f;
                interval = Mathf.Lerp(3.6f, 1.9f, progress);
                speed = Mathf.Lerp(4.2f, 6.8f, progress);
            }
            else if (elapsedSeconds < 45f)
            {
                float progress = (elapsedSeconds - 40f) / 5f;
                interval = Mathf.Lerp(4.2f, 3.5f, progress);
                speed = Mathf.Lerp(3.7f, 4.5f, progress);
            }
            else
            {
                float progress = Mathf.Clamp01((elapsedSeconds - 45f) / Mathf.Max(1f, durationSeconds - 45f));
                interval = Mathf.Lerp(1.75f, 1.15f, progress);
                speed = Mathf.Lerp(7.1f, 9.2f, progress);
            }

            float hurdleScale = Random.Range(0.8f, 1.18f);
            float hurdleSpeed = speed * Random.Range(0.94f, 1.07f);
            float floorTop = FloorY + 0.36f;
            float alignedCenterY = floorTop + 0.62f * hurdleScale;
            BroadcastAttack(new AttackState
            {
                Sequence = ++attackSequence,
                Type = 2,
                Variant = Random.Range(0, 5),
                Position = new Vector2(ArenaHalfWidth - 0.55f, alignedCenterY),
                Direction = Vector2.left,
                Speed = hurdleSpeed,
                Size = hurdleScale
            });
            nextHurdleAt = elapsedSeconds + Mathf.Max(0.9f, interval * Random.Range(0.78f, 1.22f));
        }

        private void AdvanceRope(float deltaTime)
        {
            ropePhase = Mathf.Repeat(ropePhase + deltaTime / Mathf.Max(0.4f, ropePeriod), 1f);
            UpdateRopeVisual();
        }

        private void UpdateRopeVisual()
        {
            if (ropeLine == null || ropeHazard == null) return;
            const int points = 31;
            const float endpointY = 1.45f;
            float middleY = 1.7f - Mathf.Cos(ropePhase * Mathf.PI * 2f) * 2.95f;
            for (int i = 0; i < points; i++)
            {
                float normalized = i / (float)(points - 1);
                float x = Mathf.Lerp(-RopeHalfWidth, RopeHalfWidth, normalized);
                float curve = 1f - Mathf.Pow(Mathf.Abs(x) / RopeHalfWidth, 2f);
                float y = Mathf.Lerp(endpointY, middleY, curve);
                ropeLine.SetPosition(i, new Vector3(x, y, 0f));
            }
            bool nearGround = ropePhase < 0.065f || ropePhase > 0.935f;
            float timeToGround = (1f - ropePhase) * Mathf.Max(0.4f, ropePeriod);
            bool warnToJump = phase == Phase.Playing && ropePhase > 0.55f && timeToGround <= 0.62f;
            ropeHazard.SetDangerous(phase == Phase.Playing && nearGround);
            Color ropeColor = nearGround
                ? new Color(0.92f, 0.13f, 0.1f, 1f)
                : new Color(0.16f, 0.38f, 0.88f, 0.94f);
            ropeLine.startColor = ropeColor;
            ropeLine.endColor = ropeColor;
            ropeLine.sortingOrder = ropePhase < 0.25f || ropePhase > 0.75f ? 42 : 8;
            float handleAngle = -ropePhase * 360f;
            if (leftHandle != null) leftHandle.transform.localRotation = Quaternion.Euler(0f, 0f, handleAngle);
            if (rightHandle != null) rightHandle.transform.localRotation = Quaternion.Euler(0f, 0f, -handleAngle);
            UpdateLandingWarning(warnToJump, nearGround, timeToGround);
        }

        private void UpdateLandingWarning(bool warnToJump, bool nearGround, float timeToGround)
        {
            float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 18f) * 0.25f;
            Color guideColor = nearGround
                ? new Color(1f, 0.08f, 0.05f, 0.95f)
                : warnToJump
                    ? new Color(1f, 0.55f, 0.05f, Mathf.Clamp01(pulse + 0.2f))
                    : new Color(0.15f, 0.48f, 0.9f, 0.18f);
            for (int i = 0; i < landingGuideDashes.Count; i++)
            {
                if (landingGuideDashes[i] != null) landingGuideDashes[i].color = guideColor;
            }

            if (jumpPrompt != null)
            {
                jumpPrompt.gameObject.SetActive(warnToJump);
                if (warnToJump)
                {
                    jumpPrompt.text = LocalizationManager.T("jump_rope_jump_now");
                    jumpPrompt.color = timeToGround < 0.28f
                        ? new Color(1f, 0.12f, 0.08f, 1f)
                        : new Color(1f, 0.62f, 0.05f, 1f);
                    jumpPrompt.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 17f) * 0.08f);
                }
            }

            if (warnToJump && !ropeWarningPlayed)
            {
                ropeWarningPlayed = true;
                GameSfx.Play(SfxId.CrumblingFloorWarning);
            }
            else if (ropePhase > 0.12f && ropePhase < 0.55f)
            {
                ropeWarningPlayed = false;
            }
        }

        private void UpdateAttackDirector()
        {
            float progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            if (elapsedSeconds >= nextBombAt)
            {
                int count = elapsedSeconds >= 45f ? 3 : elapsedSeconds >= 28f ? 2 : 1;
                for (int i = 0; i < count; i++) SpawnBomb(progress);
                nextBombAt = elapsedSeconds + Mathf.Lerp(5.2f, 1.65f, progress);
            }
            if (elapsedSeconds >= nextMissileAt)
            {
                SpawnMissile(progress);
                if (elapsedSeconds >= 48f) SpawnMissile(progress);
                nextMissileAt = elapsedSeconds + Mathf.Lerp(7.2f, 2.3f, progress);
            }
            if (elapsedSeconds >= nextEnemyAt)
            {
                SpawnEnemy(progress);
                nextEnemyAt = elapsedSeconds + Mathf.Lerp(8.5f, 3.4f, progress);
            }
        }

        private void SpawnBomb(float progress)
        {
            bool fromSide = elapsedSeconds > 25f && Random.value < 0.42f;
            Vector2 position;
            Vector2 velocity;
            if (fromSide)
            {
                bool left = Random.value < 0.5f;
                position = new Vector2(left ? -12.1f : 12.1f, Random.Range(0.2f, 4.8f));
                velocity = new Vector2(left ? Random.Range(6.5f, 10f) : Random.Range(-10f, -6.5f), Random.Range(0.5f, 3.2f));
            }
            else
            {
                position = new Vector2(Random.Range(-10.8f, 10.8f), 6.5f);
                velocity = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-1.5f, -0.3f));
            }
            float size = Random.Range(0.65f, Mathf.Lerp(0.95f, 1.45f, progress));
            float fuse = Random.Range(3.2f, 6.2f);
            Vector2 warningPosition = fromSide
                ? position
                : new Vector2(position.x, FloorY + 0.16f);
            BroadcastAttack(new AttackState
            {
                Sequence = ++attackSequence,
                Type = 3,
                Variant = 0,
                Position = warningPosition,
                Direction = velocity.normalized,
                Speed = DangerWarningSeconds,
                Size = Mathf.Max(0.9f, size)
            });
            StartCoroutine(SpawnBombAfterWarning(position, velocity, size, fuse));
        }

        private System.Collections.IEnumerator SpawnBombAfterWarning(
            Vector2 position,
            Vector2 velocity,
            float size,
            float fuse)
        {
            yield return new WaitForSeconds(DangerWarningSeconds);
            if (phase != Phase.Playing || stageManager == null || stageManager.CurrentStageId != StageId) yield break;
            string id = "jump_rope_bomb_" + (++bombSequence);
            GameObject bomb = IsOnlineActive() && syncManager != null
                ? syncManager.SpawnDropperBox(id, StageObjectType.Bomb, position, size, 0f, fuse, velocity)
                : objectFactory != null ? objectFactory.CreateDroppedBox(StageObjectType.Bomb, id, position, size, transform, fuse) : null;
            Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
            if (body != null) body.linearVelocity = velocity;
        }

        private void SpawnMissile(float progress)
        {
            bool top = elapsedSeconds > 34f && Random.value < 0.35f;
            Vector2 position;
            Vector2 direction;
            if (top)
            {
                position = new Vector2(Random.Range(-9.5f, 9.5f), 6.7f);
                direction = new Vector2(Random.Range(-0.22f, 0.22f), -1f).normalized;
            }
            else
            {
                bool left = Random.value < 0.5f;
                position = new Vector2(left ? -12.25f : 12.25f, Random.Range(-0.3f, 4.5f));
                direction = new Vector2(left ? 1f : -1f, Random.Range(-0.12f, 0.12f)).normalized;
            }
            AttackState missile = new AttackState
            {
                Sequence = ++attackSequence, Type = 0, Position = position, Direction = direction,
                Speed = Mathf.Lerp(6.5f, 11.5f, progress), Size = 1f
            };
            BroadcastAttack(new AttackState
            {
                Sequence = ++attackSequence,
                Type = 3,
                Variant = 1,
                Position = position,
                Direction = direction,
                Speed = DangerWarningSeconds,
                Size = 1f
            });
            StartCoroutine(SpawnMissileAfterWarning(missile));
        }

        private System.Collections.IEnumerator SpawnMissileAfterWarning(AttackState missile)
        {
            yield return new WaitForSeconds(DangerWarningSeconds);
            if (phase == Phase.Playing && stageManager != null && stageManager.CurrentStageId == StageId)
                BroadcastAttack(missile);
        }

        private void SpawnEnemy(float progress)
        {
            bool fromTop = Random.value < 0.55f;
            bool left = Random.value < 0.5f;
            Vector2 position = fromTop
                ? new Vector2(left ? Random.Range(-10.5f, -6f) : Random.Range(6f, 10.5f), 6.4f)
                : new Vector2(left ? -11.8f : 11.8f, -0.9f);
            StageObjectType type = progress > 0.72f && Random.value < 0.38f
                ? StageObjectType.EnemyJumper
                : progress > 0.45f && Random.value < 0.32f
                    ? StageObjectType.EnemyCharger
                    : StageObjectType.EnemyWalker;
            AttackState attack = new AttackState
            {
                Sequence = ++attackSequence, Type = 1, Variant = (int)type, Position = position,
                Direction = new Vector2(left ? 1f : -1f, 0f), Speed = Mathf.Lerp(1.7f, 3.5f, progress),
                Size = Random.Range(0.75f, 1.08f)
            };
            BroadcastAttack(new AttackState
            {
                Sequence = ++attackSequence,
                Type = 3,
                Variant = 2,
                Position = position,
                Direction = attack.Direction,
                Speed = DangerWarningSeconds,
                Size = attack.Size
            });
            StartCoroutine(SpawnEnemyAfterWarning(attack));
        }

        private System.Collections.IEnumerator SpawnEnemyAfterWarning(AttackState attack)
        {
            yield return new WaitForSeconds(DangerWarningSeconds);
            if (phase != Phase.Playing || stageManager == null || stageManager.CurrentStageId != StageId) yield break;
            if (syncManager != null)
            {
                appliedAttackSequences.Add(attack.Sequence);
                Vector2 launchVelocity = attack.Position.y > 5f
                    ? new Vector2(attack.Direction.x * 1.4f, -3.5f)
                    : Vector2.zero;
                syncManager.SpawnDropperEnemy(
                    "jump_rope_enemy_" + attack.Sequence,
                    (StageObjectType)attack.Variant,
                    attack.Position,
                    attack.Size,
                    attack.Speed,
                    attack.Direction.x,
                    launchVelocity);
            }
            else BroadcastAttack(attack);
        }

        private void BroadcastAttack(AttackState attack)
        {
            ApplyAttack(attack);
            if (IsOnlineActive() && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId, Kind = KindAttack, Json = JsonUtility.ToJson(attack)
                });
            }
        }

        private void ApplyAttack(AttackState attack)
        {
            if (attack == null || !appliedAttackSequences.Add(attack.Sequence)) return;
            if (attack.Type == 3)
            {
                StageJumpRopeDangerWarning.Create(
                    transform,
                    attack.Position,
                    attack.Direction,
                    attack.Variant,
                    Mathf.Max(0.35f, attack.Speed),
                    attack.Size);
                GameSfx.PlayAt(SfxId.CrumblingFloorWarning, attack.Position, 0.58f);
                return;
            }
            if (attack.Type == 0)
            {
                StageMissileProjectile.Create(transform, transform, attack.Position, attack.Direction, attack.Speed);
                GameSfx.PlayAt(SfxId.CannonFire, attack.Position);
                return;
            }
            if (attack.Type == 2)
            {
                StageSurvivalHurdle.Create(transform, attack.Position, attack.Speed, attack.Size, attack.Variant);
                GameSfx.PlayAt(SfxId.EditorObjectDrop, attack.Position, 0.58f);
                return;
            }
            if (objectFactory == null) return;
            GameObject enemy = objectFactory.CreateSpawnedEnemy(
                (StageObjectType)attack.Variant, "jump_rope_enemy_" + attack.Sequence,
                attack.Position, attack.Size, transform, attack.Speed, attack.Direction.x);
            Rigidbody2D body = enemy != null ? enemy.GetComponent<Rigidbody2D>() : null;
            if (body != null && attack.Position.y > 5f) body.linearVelocity = new Vector2(attack.Direction.x * 1.4f, -3.5f);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != Phase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (!IsOnlineActive()) participantIds.Add(id);
            if (IsOnlineActive())
            {
                string localId = onlineManager != null ? onlineManager.LocalPlayerId : null;
                if (id != localId) return;
                if (!HasAuthority())
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId, Kind = KindEliminateRequest,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                    });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id, IsOnlineActive());
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId, Kind = KindEliminated,
                    Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                });
            }
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminatedIds.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null) HidePlayer(player);
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || string.IsNullOrEmpty(data.Kind)) return;
            if (data.Kind == KindState && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyState(JsonUtility.FromJson<NetworkState>(data.Json));
            else if (data.Kind == KindAttack && IsHostPlayer(data.PlayerId) && !HasAuthority())
                ApplyAttack(JsonUtility.FromJson<AttackState>(data.Json));
            else if (data.Kind == KindEliminateRequest && HasAuthority())
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(data.Json);
                string id = request != null && !string.IsNullOrEmpty(request.PlayerId) ? request.PlayerId : data.PlayerId;
                if (id == data.PlayerId) ConfirmElimination(id, true);
            }
            else if (data.Kind == KindEliminated && IsHostPlayer(data.PlayerId))
            {
                EliminationState eliminated = JsonUtility.FromJson<EliminationState>(data.Json);
                if (eliminated != null) ApplyElimination(eliminated.PlayerId);
            }
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnlineActive() || !HasAuthority() || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.15f;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence, Phase = (int)phase, Remaining = remainingSeconds,
                Elapsed = elapsedSeconds, PhaseRemaining = phaseRemaining, RopePhase = ropePhase,
                RopePeriod = ropePeriod, EliminatedIds = new List<string>(eliminatedIds).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId, Kind = KindState, Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyState(NetworkState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            phase = (Phase)Mathf.Clamp(state.Phase, 0, (int)Phase.Failed);
            remainingSeconds = state.Remaining;
            elapsedSeconds = state.Elapsed;
            phaseRemaining = state.PhaseRemaining;
            ropePhase = state.RopePhase;
            ropePeriod = state.RopePeriod;
            if (state.EliminatedIds != null)
                for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
            SetLocalControls(phase == Phase.Playing);
            UpdateRopeVisual();
            RefreshMonitor();
        }

        private void CheckFalls()
        {
            if (phase != Phase.Playing) return;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D fallen = players[i];
                if (fallen == null || fallen.transform.position.y >= FloorY - 4f) continue;
                string id = ResolvePlayerId(fallen);
                if (IsOnlineActive() && HasAuthority()) ConfirmElimination(id, true);
                else RequestElimination(fallen);
            }
        }

        private bool HasActiveSurvivor()
        {
            foreach (string id in participantIds)
            {
                if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) continue;
                PlayerController2D candidate = ResolvePlayer(id);
                if (candidate != null && candidate.gameObject.activeInHierarchy
                    && candidate.transform.position.y >= FloorY - 4f) return true;
            }
            return false;
        }

        private void CaptureParticipants()
        {
            if (IsOnlineActive())
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players != null)
                    for (int i = 0; i < players.Length; i++)
                        if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) participantIds.Add(players[i].PlayerId);
                return;
            }
            PlayerController2D[] locals = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < locals.Length; i++) participantIds.Add(ResolvePlayerId(locals[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds) if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private void BeginFailure()
        {
            phase = Phase.Failed;
            restartRemaining = 2.5f;
            ropeHazard?.SetDangerous(false);
            SetLocalControls(false);
            SetMonitorMain(LocalizationManager.T("game_over"), 0.14f);
            SetMonitorSub(LocalizationManager.T("survival_retrying"), 0.09f);
            BroadcastState(true);
        }

        private void HidePlayer(PlayerController2D player)
        {
            if (player == null || hiddenPlayers.Contains(player)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.ResetMotion();
            player.SetControlsEnabled(false);
            hiddenPlayers.Add(player);
            player.gameObject.SetActive(false);
        }

        private void RestoreHiddenPlayers()
        {
            if (restoredPlayers) return;
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++) if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private void ApplyPendingEliminations()
        {
            foreach (string id in eliminatedIds)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player != null && player.gameObject.activeSelf) HidePlayer(player);
            }
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode && !IsEliminated(active));
            if (!IsOnlineActive())
                stageManager.RemotePlayerController?.SetControlsEnabled(enabled && !IsEliminated(stageManager.RemotePlayerController));
        }

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminatedIds.Contains(id);
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnlineActive() ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnlineActive()) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private bool IsOnlineActive() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnlineActive() || stageManager.IsOnlineStageHost;

        private bool IsHostPlayer(string id)
        {
            if (onlineManager != null && onlineManager.IsHostPlayer(id)) return true;
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("6-2 Jump Rope Arena");
            arena.transform.SetParent(transform, false);
            CreateFloor(arena.transform);
            CreateBoundary(arena.transform);
            CreateHurdleEntrance(arena.transform);
            CreateMonitor(arena.transform);
        }

        private void CreateHurdleEntrance(Transform parent)
        {
            GameObject entrance = new GameObject("Hurdle Entrance");
            entrance.transform.SetParent(parent, false);
            entrance.transform.localPosition = new Vector2(ArenaHalfWidth - 0.48f, FloorY + 1.5f);
            AddPolyline(entrance.transform, new[]
            {
                new Vector2(-0.15f, 0.55f), new Vector2(0.35f, 0f), new Vector2(-0.15f, -0.55f)
            }, 0.12f, new Color(0.92f, 0.2f, 0.12f, 1f), 39);
            TextMesh direction = CreateText(
                entrance.transform,
                "Hurdle Direction",
                new Vector3(-0.8f, 0f, -0.02f),
                42,
                0.09f,
                new Color(0.92f, 0.2f, 0.12f, 1f),
                39);
            direction.text = "←";
        }

        private void CreateFloor(Transform parent)
        {
            GameObject floor = new GameObject("Jump Rope Floor");
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector2(0f, FloorY);
            floor.layer = 6;
            floor.tag = "Ground";
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(FloorWidth, 0.72f);
            GameObject fill = new GameObject("Paper Fill");
            fill.transform.SetParent(floor.transform, false);
            fill.transform.localScale = new Vector3(FloorWidth, 0.72f, 1f);
            SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = new Color(0.91f, 0.87f, 0.73f, 1f);
            renderer.sortingOrder = 12;
            AddBoxOutline(floor.transform, new Vector2(FloorWidth, 0.72f), new Color(0.18f, 0.12f, 0.08f, 1f), 13);
        }

        private void CreateBoundary(Transform parent)
        {
            if (objectFactory == null) return;
            StageObjectData boundary = StageObjectFactory.CreateDefaultData(StageObjectType.StageBoundary, new Vector2(0f, 2.25f));
            boundary.objectId = "jump_rope_boundary";
            boundary.size = new Vector2(ArenaHalfWidth * 2f + 0.8f, 11.2f);
            boundary.pathThickness = 0.6f;
            objectFactory.Create(boundary, parent);
        }

        private void CreatePost(Transform parent, float x, bool left)
        {
            GameObject post = new GameObject(left ? "Left Rope Post" : "Right Rope Post");
            post.transform.SetParent(parent, false);
            post.transform.localPosition = new Vector2(x, -0.05f);
            AddPolyline(post.transform, new[] { new Vector2(0f, -1.55f), new Vector2(0f, 1.5f) }, 0.16f, new Color(0.36f, 0.2f, 0.08f, 1f), 35);
            GameObject handle = new GameObject("Turning Handle");
            handle.transform.SetParent(post.transform, false);
            handle.transform.localPosition = new Vector2(0f, 1.5f);
            handle.transform.localScale = new Vector3(0.72f, 0.18f, 1f);
            SpriteRenderer renderer = handle.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = new Color(1f, 0.63f, 0.12f, 1f);
            renderer.sortingOrder = 38;
            if (left) leftHandle = renderer; else rightHandle = renderer;
        }

        private void CreateRope(Transform parent)
        {
            GameObject rope = new GameObject("Turning Jump Rope");
            rope.transform.SetParent(parent, false);
            ropeLine = rope.AddComponent<LineRenderer>();
            ropeLine.useWorldSpace = false;
            ropeLine.positionCount = 31;
            ropeLine.startWidth = 0.11f;
            ropeLine.endWidth = 0.11f;
            ropeLine.numCapVertices = 5;
            ropeLine.numCornerVertices = 3;
            ropeLine.sharedMaterial = DoodleRuntimeAssets.LineMaterial;

            GameObject hazard = new GameObject("Rope Ground Hitbox");
            hazard.transform.SetParent(rope.transform, false);
            hazard.transform.localPosition = new Vector2(0f, -1.28f);
            BoxCollider2D trigger = hazard.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(RopeHalfWidth * 2f, 0.3f);
            trigger.isTrigger = true;
            ropeHazard = hazard.AddComponent<StageJumpRopeHazard>();
            ropeHazard.SetDangerous(false);
        }

        private void CreateLandingGuide(Transform parent)
        {
            GameObject guide = new GameObject("Rope Landing Warning");
            guide.transform.SetParent(parent, false);
            guide.transform.localPosition = new Vector3(0f, -1.28f, 0f);
            const int dashCount = 17;
            for (int i = 0; i < dashCount; i++)
            {
                float x = Mathf.Lerp(-RopeHalfWidth + 0.45f, RopeHalfWidth - 0.45f, i / (float)(dashCount - 1));
                GameObject dash = new GameObject("Landing Dash " + (i + 1));
                dash.transform.SetParent(guide.transform, false);
                dash.transform.localPosition = new Vector3(x, 0f, 0f);
                dash.transform.localScale = new Vector3(0.82f, 0.075f, 1f);
                SpriteRenderer renderer = dash.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.SquareSprite;
                renderer.color = new Color(0.15f, 0.48f, 0.9f, 0.18f);
                renderer.sortingOrder = 41;
                landingGuideDashes.Add(renderer);
            }

            jumpPrompt = CreateText(
                parent,
                "Jump Timing Prompt",
                new Vector3(0f, -0.25f, -0.08f),
                72,
                0.17f,
                new Color(1f, 0.62f, 0.05f, 1f),
                45);
            jumpPrompt.gameObject.SetActive(false);
        }

        private void CreateMonitor(Transform parent)
        {
            GameObject monitor = new GameObject("Jump Rope Monitor");
            monitor.transform.SetParent(parent, false);
            monitor.transform.localPosition = new Vector3(0f, 5.1f, 0.4f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(9.8f, 2.65f), -32);
            monitorMain = CreateText(monitor.transform, "Main", new Vector3(0f, -0.05f, -0.03f), 68, 0.18f, new Color(0.04f, 0.43f, 0.58f, 1f), -25);
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null) return;
            if (phase == Phase.Intro)
            {
                SetMonitorMain(string.Empty, 0.18f);
                return;
            }
            if (phase == Phase.Countdown)
            {
                if (phaseRemaining > 3f) SetMonitorMain("3", 0.19f);
                else if (phaseRemaining > 2f) SetMonitorMain("2", 0.19f);
                else if (phaseRemaining > 1f) SetMonitorMain("1", 0.19f);
                else SetMonitorMain(LocalizationManager.T("survival_start"), 0.14f);
                return;
            }
            if (phase == Phase.Failed)
            {
                SetMonitorMain(LocalizationManager.T("game_over"), 0.14f);
                return;
            }
            if (phase == Phase.Finished)
            {
                SetMonitorMain(LocalizationManager.T("clear"), 0.17f);
                return;
            }
            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            float seconds = remainingSeconds - minutes * 60f;
            SetMonitorMain(string.Format("{0:00}:{1:00.0}", minutes, seconds), 0.16f);
        }

        private void SetMonitorMain(string value, float size)
        {
            monitorMain.text = value;
            monitorMain.characterSize = Mathf.Min(0.16f, size, 1.45f / Mathf.Max(1, value != null ? value.Length : 0));
        }

        private void SetMonitorSub(string value, float size)
        {
            // Supplemental instructions are intentionally omitted from the in-world monitor.
        }

        private static void CreateRect(Transform parent, Vector2 size, Color color, int order)
        {
            GameObject obj = new GameObject("Monitor Rect");
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private static TextMesh CreateText(Transform parent, string name, Vector3 position, int fontSize, float size, Color color, int order)
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

        private static void AddBoxOutline(Transform parent, Vector2 size, Color color, int order)
        {
            Vector2 half = size * 0.5f;
            AddPolyline(parent, new[]
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y), new Vector2(half.x, half.y),
                new Vector2(half.x, -half.y), new Vector2(-half.x, -half.y)
            }, 0.055f, color, order);
        }

        private static void AddPolyline(Transform parent, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject("Pencil Line");
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageJumpRopeHazard : MonoBehaviour
    {
        private Collider2D hitbox;

        private void Awake() => hitbox = GetComponent<Collider2D>();

        public void SetDangerous(bool dangerous)
        {
            if (hitbox == null) hitbox = GetComponent<Collider2D>();
            if (hitbox != null) hitbox.enabled = dangerous;
        }

        private void OnTriggerEnter2D(Collider2D other) => Hit(other);
        private void OnTriggerStay2D(Collider2D other) => Hit(other);

        private static void Hit(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null || player.IsInvulnerable) return;
            Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageSurvivalHurdle : MonoBehaviour
    {
        private float speed;
        private float lifeRemaining = 9f;
        private Rigidbody2D body;
        private static Material lineMaterial;

        public static StageSurvivalHurdle Create(Transform parent, Vector2 position, float moveSpeed, float scale, int variant)
        {
            int style = Mathf.Abs(variant) % 5;
            GameObject root = new GameObject("Incoming Hurdle Style " + (style + 1));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.85f, 1.2f);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(0.48f, 1.28f);
            trigger.offset = new Vector2(0f, -0.02f);
            trigger.isTrigger = true;

            BuildHurdleVisual(root.transform, style);

            StageSurvivalHurdle hurdle = root.AddComponent<StageSurvivalHurdle>();
            hurdle.speed = Mathf.Clamp(moveSpeed, 2f, 12f);
            hurdle.body = body;
            body.linearVelocity = Vector2.left * hurdle.speed;
            return hurdle;
        }

        private static void BuildHurdleVisual(Transform parent, int style)
        {
            switch (style)
            {
                case 1:
                    BuildWoodHurdle(parent);
                    break;
                case 2:
                    BuildConstructionHurdle(parent);
                    break;
                case 3:
                    BuildSchoolHurdle(parent);
                    break;
                case 4:
                    BuildStarHurdle(parent);
                    break;
                default:
                    BuildSportHurdle(parent);
                    break;
            }
        }

        private static void BuildSportHurdle(Transform parent)
        {
            Color blue = new Color(0.08f, 0.42f, 0.88f, 1f);
            Color red = new Color(0.92f, 0.12f, 0.1f, 1f);
            Color paper = new Color(0.98f, 0.96f, 0.86f, 1f);
            AddFilledBar(parent, "Sport Crossbar", new Vector2(0f, 0.52f), new Vector2(1.12f, 0.24f), paper, blue, 0f);
            AddFilledBar(parent, "Left Stripe", new Vector2(-0.34f, 0.52f), new Vector2(0.15f, 0.24f), red, red, 0f);
            AddFilledBar(parent, "Right Stripe", new Vector2(0.34f, 0.52f), new Vector2(0.15f, 0.24f), red, red, 0f);
            AddHurdleLine(parent, new Vector2(-0.32f, 0.4f), new Vector2(-0.32f, -0.58f), 0.13f, blue);
            AddHurdleLine(parent, new Vector2(0.32f, 0.4f), new Vector2(0.32f, -0.58f), 0.13f, blue);
            AddFeet(parent, blue);
            AddBolt(parent, new Vector2(-0.32f, 0.29f), red);
            AddBolt(parent, new Vector2(0.32f, 0.29f), red);
        }

        private static void BuildWoodHurdle(Transform parent)
        {
            Color wood = new Color(0.66f, 0.33f, 0.12f, 1f);
            Color lightWood = new Color(0.9f, 0.65f, 0.28f, 1f);
            Color ink = new Color(0.24f, 0.1f, 0.035f, 1f);
            AddFilledBar(parent, "Wood Plank", new Vector2(0f, 0.48f), new Vector2(1.18f, 0.32f), lightWood, ink, -2f);
            AddHurdleLine(parent, new Vector2(-0.38f, 0.34f), new Vector2(-0.5f, -0.62f), 0.15f, wood);
            AddHurdleLine(parent, new Vector2(0.38f, 0.34f), new Vector2(0.5f, -0.62f), 0.15f, wood);
            AddHurdleLine(parent, new Vector2(-0.46f, -0.42f), new Vector2(0.4f, 0.25f), 0.09f, wood);
            AddHurdleLine(parent, new Vector2(0.46f, -0.42f), new Vector2(-0.4f, 0.25f), 0.09f, wood);
            AddFeet(parent, ink);
            AddBolt(parent, new Vector2(-0.37f, 0.48f), ink);
            AddBolt(parent, new Vector2(0.37f, 0.48f), ink);
        }

        private static void BuildConstructionHurdle(Transform parent)
        {
            Color yellow = new Color(1f, 0.68f, 0.04f, 1f);
            Color charcoal = new Color(0.12f, 0.14f, 0.16f, 1f);
            AddFilledBar(parent, "Warning Board", new Vector2(0f, 0.43f), new Vector2(1.22f, 0.38f), yellow, charcoal, 0f);
            for (int i = -2; i <= 2; i++)
            {
                AddFilledBar(parent, "Warning Stripe", new Vector2(i * 0.23f, 0.43f), new Vector2(0.1f, 0.38f), charcoal, charcoal, -24f);
            }
            AddHurdleLine(parent, new Vector2(-0.4f, 0.23f), new Vector2(-0.52f, -0.62f), 0.14f, charcoal);
            AddHurdleLine(parent, new Vector2(0.4f, 0.23f), new Vector2(0.52f, -0.62f), 0.14f, charcoal);
            AddFeet(parent, yellow);
        }

        private static void BuildSchoolHurdle(Transform parent)
        {
            Color cyan = new Color(0.05f, 0.68f, 0.86f, 1f);
            Color navy = new Color(0.04f, 0.22f, 0.43f, 1f);
            Color white = new Color(0.98f, 0.98f, 0.9f, 1f);
            AddFilledBar(parent, "School Top", new Vector2(0f, 0.55f), new Vector2(1.08f, 0.2f), white, navy, 0f);
            AddFilledBar(parent, "School Center", new Vector2(0f, 0.25f), new Vector2(0.32f, 0.62f), cyan, navy, 0f);
            AddHurdleLine(parent, new Vector2(-0.16f, -0.02f), new Vector2(-0.52f, -0.62f), 0.13f, navy);
            AddHurdleLine(parent, new Vector2(0.16f, -0.02f), new Vector2(0.52f, -0.62f), 0.13f, navy);
            AddFeet(parent, cyan);
            AddBolt(parent, new Vector2(0f, 0.42f), new Color(1f, 0.3f, 0.22f, 1f));
        }

        private static void BuildStarHurdle(Transform parent)
        {
            Color purple = new Color(0.58f, 0.2f, 0.88f, 1f);
            Color pink = new Color(1f, 0.3f, 0.58f, 1f);
            Color yellow = new Color(1f, 0.78f, 0.08f, 1f);
            AddFilledBar(parent, "Game Crossbar", new Vector2(0f, 0.5f), new Vector2(1.16f, 0.3f), purple, new Color(0.22f, 0.05f, 0.35f, 1f), 1.5f);
            AddFilledBar(parent, "Center Badge", new Vector2(0f, 0.5f), new Vector2(0.3f, 0.3f), yellow, pink, 45f);
            AddHurdleLine(parent, new Vector2(-0.38f, 0.34f), new Vector2(-0.48f, -0.62f), 0.16f, pink);
            AddHurdleLine(parent, new Vector2(0.38f, 0.34f), new Vector2(0.48f, -0.62f), 0.16f, pink);
            AddFeet(parent, purple);
            AddBolt(parent, new Vector2(-0.38f, 0.25f), yellow);
            AddBolt(parent, new Vector2(0.38f, 0.25f), yellow);
        }

        private static void AddFeet(Transform parent, Color color)
        {
            AddFilledBar(parent, "Left Foot", new Vector2(-0.4f, -0.62f), new Vector2(0.48f, 0.15f), color, color, 0f);
            AddFilledBar(parent, "Right Foot", new Vector2(0.4f, -0.62f), new Vector2(0.48f, 0.15f), color, color, 0f);
        }

        private static void AddBolt(Transform parent, Vector2 position, Color color)
        {
            GameObject bolt = new GameObject("Hurdle Bolt");
            bolt.transform.SetParent(parent, false);
            bolt.transform.localPosition = position;
            bolt.transform.localScale = Vector3.one * 0.13f;
            SpriteRenderer renderer = bolt.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = 47;
        }

        private static void AddFilledBar(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color outline, float rotation)
        {
            GameObject bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            bar.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = fill;
            renderer.sortingOrder = 45;

            Vector2 half = size * 0.5f;
            Vector2 bottomLeft = position + RotatePoint(new Vector2(-half.x, -half.y), rotation);
            Vector2 topLeft = position + RotatePoint(new Vector2(-half.x, half.y), rotation);
            Vector2 topRight = position + RotatePoint(new Vector2(half.x, half.y), rotation);
            Vector2 bottomRight = position + RotatePoint(new Vector2(half.x, -half.y), rotation);
            AddHurdleLine(parent, bottomLeft, topLeft, 0.055f, outline);
            AddHurdleLine(parent, topLeft, topRight, 0.055f, outline);
            AddHurdleLine(parent, topRight, bottomRight, 0.055f, outline);
            AddHurdleLine(parent, bottomRight, bottomLeft, 0.055f, outline);
        }

        private static Vector2 RotatePoint(Vector2 point, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);
        }

        private void FixedUpdate()
        {
            if (body != null) body.linearVelocity = Vector2.left * speed;
            lifeRemaining -= Time.fixedDeltaTime;
            if (lifeRemaining <= 0f || transform.position.x < -15f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null || player.IsInvulnerable) return;
            Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
        }

        private static void AddHurdleLine(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject lineObject = new GameObject("Hurdle Crayon Line");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
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
            line.sortingOrder = 44;
        }
    }

    internal sealed class StageJumpRopeDangerWarning : MonoBehaviour
    {
        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        private LineRenderer directionLine;
        private float duration;
        private float elapsed;

        public static void Create(
            Transform parent,
            Vector2 position,
            Vector2 direction,
            int hazardType,
            float seconds,
            float size)
        {
            GameObject root = new GameObject("6-2 Danger Preview");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, -0.35f);
            StageJumpRopeDangerWarning warning = root.AddComponent<StageJumpRopeDangerWarning>();
            warning.duration = seconds;
            warning.Build(direction, hazardType, Mathf.Max(0.75f, size));
        }

        private void Build(Vector2 direction, int hazardType, float size)
        {
            Color red = new Color(1f, 0.12f, 0.05f, 0.78f);
            for (int i = 0; i < 3; i++)
            {
                GameObject mark = new GameObject("Warning Ring " + (i + 1));
                mark.transform.SetParent(transform, false);
                mark.transform.localScale = hazardType == 0
                    ? new Vector3((1.4f + i * 0.28f) * size, 0.22f + i * 0.06f, 1f)
                    : Vector3.one * ((0.72f + i * 0.22f) * size);
                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(red.r, red.g, red.b, red.a * (1f - i * 0.2f));
                renderer.sortingOrder = 68 + i;
                marks.Add(renderer);
            }

            if (hazardType != 1 || direction.sqrMagnitude < 0.01f) return;
            directionLine = new GameObject("Missile Warning Direction").AddComponent<LineRenderer>();
            directionLine.transform.SetParent(transform, false);
            directionLine.useWorldSpace = false;
            directionLine.positionCount = 2;
            directionLine.SetPosition(0, Vector3.zero);
            directionLine.SetPosition(1, direction.normalized * 5.2f);
            directionLine.startWidth = 0.16f;
            directionLine.endWidth = 0.045f;
            directionLine.numCapVertices = 5;
            directionLine.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            directionLine.startColor = red;
            directionLine.endColor = new Color(red.r, red.g, red.b, 0.12f);
            directionLine.sortingOrder = 71;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float pulse = 0.82f + Mathf.Sin(elapsed * 22f) * 0.16f;
            for (int i = 0; i < marks.Count; i++)
            {
                SpriteRenderer mark = marks[i];
                if (mark == null) continue;
                Color color = mark.color;
                color.a = (0.78f - i * 0.14f) * pulse * (1f - normalized * 0.55f);
                mark.color = color;
            }
            if (directionLine != null)
            {
                Color start = directionLine.startColor;
                start.a = 0.78f * pulse;
                directionLine.startColor = start;
            }
            if (elapsed >= duration) Destroy(gameObject);
        }
    }
}
