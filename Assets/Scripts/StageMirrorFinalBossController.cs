using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageMirrorFinalBossController : StageEliminationChallengeController
    {
        private const string StageId = "15-3";
        private const string StateKind = "mirror_brawl_state";
        private const string FakeStateKind = "mirror_brawl_fakes";
        private const string MachineRequestKind = "mirror_brawl_machine_request";
        private const string MissileRequestKind = "mirror_brawl_missile_request";
        private const string ScratchRequestKind = "mirror_brawl_scratch_request";
        private const string RealHitKind = "mirror_brawl_real_hit";
        private const string WeaponGrantKind = "mirror_brawl_weapon_grant";
        private const float ArenaHalfWidth = 20f;
        private const float LowerFloorY = -4.6f;
        private const float UpperFloorY = 1.15f;
        private const int FakeHealth = 1;

        private enum BattleState { Intro, Fighting, Intermission, Cleared, Failed }
        internal enum WeaponType { Bomb, Missile }

        [System.Serializable] private sealed class MachineRequest { public int Machine; }
        [System.Serializable] private sealed class MissileRequest { public Vector2 Direction; }
        [System.Serializable] private sealed class ScratchRequest { public int FakeId; }
        [System.Serializable] private sealed class RealHitState { public string PlayerId; public int FakeId; }
        [System.Serializable] private sealed class WeaponGrantState { public string PlayerId; public int Ammo; }
        [System.Serializable] private sealed class FakeStateBatch
        {
            public int Sequence;
            public FakeState[] Fakes;
        }
        [System.Serializable] internal sealed class FakeState
        {
            public int Id;
            public int SourceRoom;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Facing;
            public int Health;
            public int MaximumHealth;
            public bool Alive;
            public bool HasMissile;
            public bool HasBomb;
            public bool Shelled;
            public bool Berserk;
        }
        [System.Serializable] private sealed class BattleSnapshot
        {
            public int Sequence;
            public int State;
            public int Phase;
            public int PlayerCount;
            public float Remaining;
            public string[] RoomPlayerIds;
            public FakeState[] Fakes;
            public int[] RealAmmo;
            public float[] MachineCooldowns;
            public float[] MachineAngles;
            public string[] EliminatedPlayerIds;
            public int DisabledMachine;
            public float DisabledRemaining;
            public bool Concealed;
            public int FailureReason;
            public Vector2[] RealPositions;
        }

        private readonly string[] roomPlayerIds = new string[4];
        private readonly List<StageMirrorCombatant> fakes = new List<StageMirrorCombatant>();
        private readonly List<StageMirrorWeaponMachine> machines = new List<StageMirrorWeaponMachine>();
        private readonly Dictionary<string, int> realMissileAmmo = new Dictionary<string, int>();
        private readonly HashSet<string> eliminatedPlayers = new HashSet<string>();
        private readonly Dictionary<int, int> lastFakeSequenceById = new Dictionary<int, int>();
        private readonly Dictionary<PlayerController2D, Dictionary<Renderer, bool>> hiddenPlayerRenderers =
            new Dictionary<PlayerController2D, Dictionary<Renderer, bool>>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousFollowEnabled;
        private TextMesh phaseText;
        private TextMesh timerText;
        private TextMesh hintText;
        private GameObject concealRoot;
        private SpriteRenderer concealInk;
        private readonly List<LineRenderer> concealStrokes = new List<LineRenderer>();
        private readonly List<Transform> concealSplatPieces = new List<Transform>();
        private readonly List<Vector3> concealSplatTargetScales = new List<Vector3>();
        private GameObject eraserVisual;
        private TextMesh resultText;
        private BattleState battleState = BattleState.Intro;
        private int playerCount = 1;
        private int phase;
        private int nextFakeId = 1;
        private int spawnSequence;
        private int missileSequence;
        private int stateSequence;
        private int lastStateSequence;
        private int disabledMachine = -1;
        private float disabledUntil;
        private float remaining;
        private float nextStateAt;
        private float nextPhaseThreeDisableAt;
        private bool transitionRunning;
        private bool concealed;
        private bool concealAnimationRunning;
        private Coroutine networkConcealRoutine;
        private int networkConcealSeenPhase = -1;
        private int networkConcealCompletedPhase = -1;
        private float networkConcealDeadline = -1f;
        private int failureReason;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;
        internal bool IsCurrentStageActive => isActiveAndEnabled
            && stageManager != null
            && stageManager.IsGameplayActive
            && stageManager.CurrentStageId == StageId;
        public override bool UsesGlobalFallBoundary => false;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
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
            if (networkConcealRoutine != null) StopCoroutine(networkConcealRoutine);
            networkConcealRoutine = null;
            SetConcealmentImmediate(false);
            if (eraserVisual != null) eraserVisual.SetActive(false);
            BombExplosionVisual[] looseEffects = Object.FindObjectsByType<BombExplosionVisual>(FindObjectsSortMode.None);
            for (int i = 0; i < looseEffects.Length; i++)
                if (looseEffects[i] != null && looseEffects[i].gameObject.name == "INK Knockout")
                {
                    looseEffects[i].gameObject.SetActive(false);
                    Destroy(looseEffects[i].gameObject);
                }
            RestorePlayersForStageExit();
            RestoreCamera();
        }

        private void RestorePlayersForStageExit()
        {
            for (int i = 0; i < playerCount; i++)
            {
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); if (player == null) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>(); if (body != null) { body.simulated = true; body.linearVelocity = Vector2.zero; }
                SetPlayerVisible(player, true);
                if (!IsOnline || stageManager != null && stageManager.ActivePlayerTransform == player.transform) player.SetControlsEnabled(true);
            }
            eliminatedPlayers.Clear();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildRoster();
            BuildArena();
            BuildMachines();
            BuildMonitor();
            BuildConcealment();
            LockCamera();
            PlacePlayers();
            if (HasAuthority) StartCoroutine(StartPhase(1));
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            UpdateClientConcealmentFailsafe();
            fakes.RemoveAll(item => item == null);
            if (HasAuthority && battleState == BattleState.Fighting)
            {
                remaining -= Time.deltaTime;
                if ((remaining <= 0f || AreAllRealPlayersEliminated()) && !transitionRunning)
                    StartCoroutine(FailBattle(remaining <= 0f));
                else
                {
                    if (phase == 3) UpdatePhaseThreeMachineLock();
                    if (CountLivingFakes() == 0 && !transitionRunning)
                    {
                        if (phase < 3) StartCoroutine(StartPhase(phase + 1));
                        else StartCoroutine(ClearBattle());
                    }
                }
            }
            if (HasAuthority) BroadcastState(false);
            RefreshMonitor();
        }

        private void UpdateClientConcealmentFailsafe()
        {
            if (HasAuthority || !concealed || networkConcealDeadline < 0f
                || Time.unscaledTime < networkConcealDeadline) return;

            networkConcealCompletedPhase = Mathf.Max(networkConcealCompletedPhase, phase);
            networkConcealDeadline = -1f;
            if (networkConcealRoutine != null) StopCoroutine(networkConcealRoutine);
            networkConcealRoutine = null;
            concealAnimationRunning = false;
            SetConcealmentImmediate(false);
            if (eraserVisual != null) eraserVisual.SetActive(false);
        }

        private IEnumerator StartPhase(int nextPhase)
        {
            transitionRunning = true;
            battleState = nextPhase == 1 ? BattleState.Intro : BattleState.Intermission;
            phase = nextPhase;
            remaining = 3f;
            BroadcastState(true);
            for (int number = 3; number > 0; number--)
            {
                remaining = number;
                RefreshMonitor();
                GameSfx.Play(SfxId.StageCountdownTick);
                yield return new WaitForSeconds(1f);
            }
            yield return StartCoroutine(SetConcealment(true));
            DestroyFakes();
            ResetMachinesForPhase();
            ReviveAllPlayersForPhase();
            SpawnPhaseFakes();
            RandomizeLivingCharacters();
            BroadcastState(true);
            yield return new WaitForSeconds(0.55f);
            yield return StartCoroutine(SetConcealment(false));
            remaining = GetPhaseTimeLimit(phase, playerCount);
            battleState = BattleState.Fighting;
            transitionRunning = false;
            nextPhaseThreeDisableAt = Time.time + 10f;
            BroadcastState(true);
            GameSfx.Play(SfxId.StageCountdownGo);
        }

        private IEnumerator ClearBattle()
        {
            transitionRunning = true;
            battleState = BattleState.Cleared;
            BroadcastState(true);
            GameSfx.Play(SfxId.EmotePop);
            yield return new WaitForSeconds(2.4f);
            stageManager.ClearStage();
        }

        private IEnumerator FailBattle(bool timeUp)
        {
            transitionRunning = true;
            battleState = BattleState.Failed;
            failureReason = timeUp ? 1 : 2;
            ShowFailure(failureReason);
            BroadcastState(true);
            GameSfx.Play(SfxId.PlayerDeath);
            yield return new WaitForSeconds(3f);
            stageManager.Retry();
        }

        private void SpawnPhaseFakes()
        {
            int count = GetFakeCount(phase, playerCount);
            for (int i = 0; i < count; i++)
            {
                int sourceRoom = i < playerCount ? i : i % playerCount;
                PlayerController2D source = ResolvePlayer(roomPlayerIds[sourceRoom]);
                if (source == null) continue;
                Vector2 position = GetSpawnPosition(i, count);
                StageMirrorCombatant fake = StageMirrorCombatant.Create(
                    transform, this, source, nextFakeId++, sourceRoom, phase, GetFakeHealth(), position, HasAuthority);
                bool addedCopy = i >= playerCount;
                bool berserk = addedCopy && (phase >= 3 || (i - playerCount) % 2 == 0);
                fake.ConfigureVariant(berserk);
                fakes.Add(fake);
                if (HasAuthority && (addedCopy || phase == 3 && i == playerCount - 1))
                {
                    GiveInitialLoadout(fake, i - playerCount);
                }
            }
        }

        private void GiveInitialLoadout(StageMirrorCombatant fake, int variantIndex)
        {
            if (fake == null) return;
            if (fake.Species == DrawManager.Species.Cat
                || fake.Species == DrawManager.Species.Turtle
                || fake.Species == DrawManager.Species.Slime) return;
            bool giveBomb;
            switch (fake.Species)
            {
                case DrawManager.Species.Bird:
                    giveBomb = true;
                    break;
                case DrawManager.Species.Turtle:
                    giveBomb = false;
                    break;
                case DrawManager.Species.Slime:
                    giveBomb = variantIndex % 2 == 0;
                    break;
                default:
                    giveBomb = (variantIndex + phase) % 2 == 0;
                    break;
            }
            if (giveBomb)
            {
                GameObject bomb = SpawnBombAt(fake.transform.position + Vector3.up * 0.8f);
                if (bomb != null) fake.TakeBomb(bomb);
            }
            else fake.GiveMissile();

            if (phase == 3 && fake.IsBerserk && Mathf.Abs(variantIndex) % 3 == 0)
            {
                if (giveBomb) fake.GiveMissile();
                else
                {
                    GameObject bomb = SpawnBombAt(fake.transform.position + Vector3.up * 0.8f);
                    if (bomb != null) fake.TakeBomb(bomb);
                }
            }
        }

        private static int GetFakeCount(int targetPhase, int humans)
        {
            if (targetPhase <= 1) return humans;
            if (targetPhase == 2) return 3;
            return humans >= 4 ? 20 : 5;
        }

        private static int GetFakeHealth() => FakeHealth;
        private static float GetPhaseTimeLimit(int targetPhase, int humans)
        {
            float baseTime = targetPhase == 1 ? 55f : targetPhase == 2 ? 48f : 42f;
            return baseTime + Mathf.Max(0, humans - 1) * 10f;
        }

        private Vector2 GetSpawnPosition(int index, int count)
        {
            bool upper = index % 2 == 1;
            int rowIndex = index / 2;
            float width = Mathf.Min(30f, Mathf.Max(8f, count * 4.1f));
            float x = count <= 2 ? (index == 0 ? -7f : 7f) : -width * 0.5f + Mathf.Repeat(rowIndex * 5.7f + (upper ? 2f : 0f), width);
            return new Vector2(x, upper ? UpperFloorY + 1.25f : LowerFloorY + 1.25f);
        }

        private void DestroyFakes()
        {
            for (int i = 0; i < fakes.Count; i++) if (fakes[i] != null) Destroy(fakes[i].gameObject);
            fakes.Clear();
        }

        internal void RequestMachineUse(int machineIndex, PlayerController2D player)
        {
            if (player == null || battleState != BattleState.Fighting) return;
            string playerId = ResolvePlayerId(player);
            if (IsOnline && !HasAuthority)
            {
                Send(MachineRequestKind, new MachineRequest { Machine = machineIndex });
                return;
            }
            UseMachine(machineIndex, playerId, null);
        }

        internal void RequestMachineUse(int machineIndex, StageMirrorCombatant fake)
        {
            if (!HasAuthority || fake == null || !fake.CanUseWeaponMachines
                || battleState != BattleState.Fighting) return;
            UseMachine(machineIndex, null, fake);
        }

        private void UseMachine(int machineIndex, string playerId, StageMirrorCombatant fake)
        {
            if (machineIndex < 0 || machineIndex >= machines.Count) return;
            StageMirrorWeaponMachine machine = machines[machineIndex];
            if (machine == null || !machine.TryConsume(Time.time, GetMachineCooldown())) return;
            if (machine.Type == WeaponType.Bomb)
            {
                GameObject bomb = SpawnBomb(machine, fake);
                if (fake != null && bomb != null) fake.TakeBomb(bomb);
            }
            else machine.FireMissile();
            GameSfx.PlayAt(SfxId.SwitchPress, machine.transform.position, 1f);
            BroadcastState(true);
        }

        private float GetMachineCooldown() => 5f;

        private GameObject SpawnBomb(StageMirrorWeaponMachine machine, StageMirrorCombatant fake)
        {
            return SpawnBombAt((Vector2)machine.transform.position + Vector2.up * 1.35f);
        }

        private GameObject SpawnBombAt(Vector2 position)
        {
            string objectId = "15-3_bomb_" + (++spawnSequence).ToString("D5");
            if (syncManager == null) syncManager = GetComponent<StageGimmickSyncManager>();
            GameObject bomb = syncManager != null
                ? syncManager.SpawnDropperBox(objectId, StageObjectType.Bomb, position, 0.85f, fuseSeconds: phase == 1 ? 6f : 5f)
                : objectFactory != null ? objectFactory.CreateDroppedBox(StageObjectType.Bomb, objectId, position, 0.85f, transform, phase == 1 ? 6f : 5f) : null;
            return bomb;
        }

        private void UpdateLocalMissileInput()
        {
            if (battleState != BattleState.Fighting || !Input.GetMouseButtonDown(0)) return;
            PlayerController2D player = stageManager != null && stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            string id = ResolvePlayerId(player);
            if (player == null || string.IsNullOrEmpty(id) || !realMissileAmmo.TryGetValue(id, out int ammo) || ammo <= 0) return;
            Vector3 mouse = gameCamera != null ? gameCamera.ScreenToWorldPoint(Input.mousePosition) : player.transform.position + Vector3.right;
            Vector2 direction = ((Vector2)mouse - (Vector2)player.transform.position).normalized;
            if (direction.sqrMagnitude < 0.1f) direction = Vector2.right;
            if (IsOnline && !HasAuthority) Send(MissileRequestKind, new MissileRequest { Direction = direction });
            else FireRealMissile(id, direction);
            realMissileAmmo[id] = ammo - 1;
        }

        private void FireRealMissile(string playerId, Vector2 direction)
        {
            PlayerController2D player = ResolvePlayer(playerId);
            if (player == null) return;
            SpawnMissile(player.transform, player.transform.position, direction, 11f);
        }

        internal void FireFakeMissile(StageMirrorCombatant fake, Vector2 direction)
        {
            if (!HasAuthority || fake == null) return;
            SpawnMissile(fake.transform, fake.transform.position, direction, phase == 3 ? 12f : 10f);
        }

        private void SpawnMissile(Transform launcher, Vector2 position, Vector2 direction, float speed)
        {
            direction = KeepMissileAimedInsideArena(position, direction);
            if (syncManager == null) syncManager = GetComponent<StageGimmickSyncManager>();
            string launchId = "15-3_missile_" + (++missileSequence).ToString("D5");
            if (syncManager != null) syncManager.SpawnMissile(launchId, StageId, launcher, position + direction * 1.35f, direction, speed);
            else
            {
                StageMissileProjectile.Create(transform, launcher, position + direction * 1.35f, direction, speed);
                GameSfx.PlayAt(SfxId.MissileLaunch, position, 0.9f);
            }
        }

        private static Vector2 KeepMissileAimedInsideArena(Vector2 position, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.01f) direction = position.x >= 0f ? Vector2.left : Vector2.right;
            direction.Normalize();
            const float wallSafetyMargin = 3.2f;
            if (position.x >= ArenaHalfWidth - wallSafetyMargin && direction.x > -0.08f)
                direction.x = -Mathf.Max(0.18f, Mathf.Abs(direction.x));
            else if (position.x <= -ArenaHalfWidth + wallSafetyMargin && direction.x < 0.08f)
                direction.x = Mathf.Max(0.18f, Mathf.Abs(direction.x));
            return direction.normalized;
        }

        public void ApplyAreaDamage(Vector2 center, float radius, int damage)
        {
            if (!HasAuthority || battleState != BattleState.Fighting) return;
            for (int i = 0; i < fakes.Count; i++)
            {
                StageMirrorCombatant fake = fakes[i];
                if (fake == null || !fake.IsAlive) continue;
                Collider2D[] colliders = fake.GetComponentsInChildren<Collider2D>(false);
                float closestSqr = ((Vector2)fake.transform.position - center).sqrMagnitude;
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider2D collider = colliders[c]; if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    float sqr = (collider.ClosestPoint(center) - center).sqrMagnitude; if (sqr < closestSqr) closestSqr = sqr;
                }
                if (closestSqr <= radius * radius) fake.TakeDamage(damage, center);
            }
        }

        public bool TryPlayerCatScratch(PlayerController2D player, float rangeMultiplier)
        {
            if (player == null || battleState != BattleState.Fighting) return false;
            int facing = GetFacing(player);
            StageMirrorCombatant best = null;
            float bestDistance = float.MaxValue;
            Collider2D sourceCollider = player.GetComponentInChildren<Collider2D>();
            Vector2 origin = sourceCollider != null ? sourceCollider.bounds.center : player.transform.position;
            float reach = 1.35f * Mathf.Clamp(rangeMultiplier, 1f, 3f);
            for (int i = 0; i < fakes.Count; i++)
            {
                StageMirrorCombatant fake = fakes[i];
                if (fake == null || !fake.IsAlive) continue;
                Collider2D targetCollider = fake.GetComponentInChildren<Collider2D>();
                Vector2 point = targetCollider != null ? targetCollider.ClosestPoint(origin) : (Vector2)fake.transform.position;
                Vector2 delta = point - origin;
                if (delta.x * facing < -0.15f || delta.magnitude > reach || delta.magnitude >= bestDistance) continue;
                bestDistance = delta.magnitude; best = fake;
            }
            if (best == null) return false;
            if (IsOnline && !HasAuthority) Send(ScratchRequestKind, new ScratchRequest { FakeId = best.FakeId });
            else best.TakeDamage(1, player.transform.position);
            return true;
        }

        internal PlayerController2D FindRealTarget(Vector2 from, bool preferFar = false)
        {
            List<PlayerController2D> candidates = new List<PlayerController2D>();
            for (int i = 0; i < playerCount; i++)
            {
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]);
                if (player != null && player.gameObject.activeInHierarchy) candidates.Add(player);
            }
            if (candidates.Count == 0) return null;
            if (Random.value < 0.3f) return candidates[Random.Range(0, candidates.Count)];
            PlayerController2D best = candidates[0]; float score = preferFar ? float.MinValue : float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float distance = Vector2.Distance(from, candidates[i].transform.position);
                if ((!preferFar && distance < score) || (preferFar && distance > score)) { score = distance; best = candidates[i]; }
            }
            return best;
        }

        internal StageMirrorWeaponMachine FindMachine(Vector2 from, bool requireReady)
        {
            StageMirrorWeaponMachine best = null; float bestDistance = float.MaxValue;
            for (int i = 0; i < machines.Count; i++)
            {
                StageMirrorWeaponMachine machine = machines[i];
                if (machine == null || requireReady && !machine.IsReady) continue;
                float distance = Vector2.Distance(from, machine.transform.position);
                if (distance < bestDistance) { bestDistance = distance; best = machine; }
            }
            return best;
        }

        internal bool IsBombDangerNear(Vector2 point, out Vector2 danger)
        {
            StageBomb[] bombs = Object.FindObjectsByType<StageBomb>(FindObjectsSortMode.None);
            float best = 16f; danger = point;
            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] == null || bombs[i].HasExploded) continue;
                float sqr = ((Vector2)bombs[i].transform.position - point).sqrMagnitude;
                if (sqr < best) { best = sqr; danger = bombs[i].transform.position; }
            }
            return best < 16f;
        }

        internal bool IsFakeCurrentlyHeld(Transform target)
        {
            if (target == null) return false;
            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
                if (carriers[i] != null && carriers[i].IsHoldingTarget(target)) return true;
            return false;
        }

        internal void FakeCatScratch(StageMirrorCombatant fake)
        {
            if (!HasAuthority || fake == null) return;
            PlayerController2D target = FindRealTarget(fake.transform.position);
            if (target == null || target.IsInvulnerable || target.IsTurtleShelled || Vector2.Distance(target.transform.position, fake.transform.position) > fake.ScratchReach) return;
            PlayScratchVisual(fake.transform, fake.Facing);
            string targetId = ResolvePlayerId(target);
            if (IsOnline) Send(RealHitKind, new RealHitState { PlayerId = targetId, FakeId = fake.FakeId });
            ApplyRealHit(targetId);
        }

        private static void PlayScratchVisual(Transform source, int facing)
        {
            GameObject root = new GameObject("Cat Scratch Burst"); root.transform.position = source.position + Vector3.right * facing * 0.8f;
            for (int i = 0; i < 3; i++)
            {
                float y = (i - 1) * 0.18f;
                StageEscortController.AddLine(root.transform, new Vector2(-0.35f * facing, y - 0.18f), new Vector2(0.45f * facing, y + 0.18f), 0.07f, new Color(1f, 0.42f, 0.08f), 180);
            }
            Destroy(root, 0.3f); GameSfx.PlayAt(SfxId.CatClawAttach, source.position, 0.9f);
        }

        private void ApplyRealHit(string playerId)
        {
            PlayerController2D target = ResolvePlayer(playerId);
            if (target == null || target.IsInvulnerable || target.IsTurtleShelled) return;
            EliminateReal(playerId, target);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || battleState == BattleState.Cleared || battleState == BattleState.Failed) return;
            string playerId = ResolvePlayerId(target);
            if (IsOnline && !HasAuthority)
            {
                Send(RealHitKind, new RealHitState { PlayerId = playerId });
                return;
            }
            EliminateReal(playerId, target);
        }

        private void EliminateReal(string playerId, PlayerController2D player)
        {
            if (player == null || string.IsNullOrEmpty(playerId) || !eliminatedPlayers.Add(playerId)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            player.SetControlsEnabled(false);
            SetPlayerVisible(player, false);
            GameSfx.Play(SfxId.PlayerDeath);
            BroadcastState(true);
        }

        private bool AreAllRealPlayersEliminated()
        {
            int present = 0;
            for (int i = 0; i < playerCount; i++)
                if (!string.IsNullOrEmpty(roomPlayerIds[i])) { present++; if (!eliminatedPlayers.Contains(roomPlayerIds[i])) return false; }
            return present > 0;
        }

        private void SetPlayerVisible(PlayerController2D player, bool visible)
        {
            if (player == null) return;
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            if (!visible)
            {
                if (!hiddenPlayerRenderers.ContainsKey(player))
                {
                    Dictionary<Renderer, bool> states = new Dictionary<Renderer, bool>();
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) states[renderers[i]] = renderers[i].enabled;
                    hiddenPlayerRenderers[player] = states;
                }
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = false;
                return;
            }

            // BodyBuilder intentionally keeps helper/fill renderers disabled.
            // Enabling every child here exposed those shapes as solid blocks and
            // a large white circle on the authority instance after a revive.
            if (!hiddenPlayerRenderers.TryGetValue(player, out Dictionary<Renderer, bool> savedStates)) return;
            foreach (KeyValuePair<Renderer, bool> state in savedStates)
                if (state.Key != null) state.Key.enabled = state.Value;
            hiddenPlayerRenderers.Remove(player);
        }

        private void UpdatePhaseThreeMachineLock()
        {
            if (disabledMachine >= 0 && Time.time >= disabledUntil)
            {
                machines[disabledMachine].SetDisabled(false); disabledMachine = -1; BroadcastState(true);
            }
            if (disabledMachine < 0 && Time.time >= nextPhaseThreeDisableAt)
            {
                disabledMachine = Random.Range(0, machines.Count); disabledUntil = Time.time + Random.Range(7f, 10f);
                machines[disabledMachine].SetDisabled(true); nextPhaseThreeDisableAt = disabledUntil + Random.Range(7f, 11f); BroadcastState(true);
            }
        }

        private void ResetMachinesForPhase()
        {
            disabledMachine = -1; disabledUntil = 0f;
            for (int i = 0; i < machines.Count; i++) machines[i]?.ResetMachine();
        }

        private void ReviveAllPlayersForPhase()
        {
            for (int i = 0; i < playerCount; i++)
            {
                string playerId = roomPlayerIds[i];
                if (string.IsNullOrEmpty(playerId)) continue;
                PlayerController2D player = ResolvePlayer(playerId);
                if (player == null) continue;

                eliminatedPlayers.Remove(playerId);
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.simulated = true;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                SetPlayerVisible(player, true);
                if (!IsOnline || stageManager.ActivePlayerTransform == player.transform)
                    player.SetControlsEnabled(true);
            }
            eliminatedPlayers.Clear();
        }

        private void BuildArena()
        {
            CreateSolid("Lower Ground", new Vector2(0f, LowerFloorY - 0.45f), new Vector2(40f, 0.9f));
            CreateSolid("Left Wall", new Vector2(-20.45f, -0.3f), new Vector2(0.9f, 10.4f));
            CreateSolid("Right Wall", new Vector2(20.45f, -0.3f), new Vector2(0.9f, 10.4f));
            // One continuous collider avoids side seams catching only one of a
            // hand-drawn character's many body colliders while rising through it.
            // Leave broad drop openings at both ends. The upper weapon buttons
            // sit around x=+/-8.6, so a 28-unit span keeps them supported while
            // leaving roughly six world units to descend on either side.
            CreateUpperSegment(0f, 28f);
            CreateJumpAccess(-16f); CreateJumpAccess(16f);
        }

        private void CreateUpperSegment(float x, float width)
        {
            GameObject root = new GameObject("Upper One Way Floor"); root.transform.SetParent(transform, false); root.transform.localPosition = new Vector2(x, UpperFloorY); root.layer = 6; root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>(); collider.size = new Vector2(width, 0.62f); collider.usedByEffector = true;
            PlatformEffector2D effector = root.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 180f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            StageEscortController.AddFilledRect(root.transform, "One Way Paper", Vector2.zero, new Vector2(width, 0.62f), new Color(0.76f, 0.91f, 1f, 0.9f), 8);
            StageEscortController.AddLine(root.transform, new Vector2(-width * 0.5f, 0.3f), new Vector2(width * 0.5f, 0.3f), 0.08f, new Color(0.1f, 0.46f, 0.85f), 10);
        }
        private void CreateStairs(float startX, bool risesRight)
        {
            for (int i = 0; i < 5; i++)
            {
                float direction = risesRight ? 1f : -1f;
                CreateSolid("Layer Stair", new Vector2(startX + direction * i * 1.25f, LowerFloorY + 0.55f + i * 1.12f), new Vector2(2.2f, 0.42f));
            }
        }

        private void CreateJumpAccess(float x)
        {
            if (objectFactory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.JumpPad, new Vector2(x, LowerFloorY + 0.38f));
            data.objectId = "15-3_layer_jump_" + x.ToString("0"); data.actionStrength = 66f;
            objectFactory.Create(data, transform);
        }

        private void CreateSolid(string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name); root.transform.SetParent(transform, false); root.transform.localPosition = position; root.layer = 6; root.tag = "Ground";
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>(); collider.size = size;
            StageEscortController.AddFilledRect(root.transform, "Paper", Vector2.zero, size, new Color(0.9f, 0.88f, 0.8f), 8);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, new Color(0.14f, 0.11f, 0.09f), 9);
        }

        private void BuildMachines()
        {
            const float lowerButtonY = LowerFloorY + 0.17f;
            const float upperButtonY = UpperFloorY + 0.48f;
            machines.Add(CreateExistingMachine(0, WeaponType.Bomb, new Vector2(-11f, 5.25f), new Vector2(-8.6f, upperButtonY)));
            machines.Add(CreateExistingMachine(1, WeaponType.Missile, new Vector2(18.55f, 1.25f), new Vector2(13.2f, lowerButtonY)));
            machines.Add(CreateExistingMachine(2, WeaponType.Missile, new Vector2(-18.55f, 0f), new Vector2(-13.2f, lowerButtonY)));
            machines.Add(CreateExistingMachine(3, WeaponType.Bomb, new Vector2(10.7f, -0.2f), new Vector2(8.3f, lowerButtonY)));
            machines.Add(CreateExistingMachine(4, WeaponType.Bomb, new Vector2(11f, 5.25f), new Vector2(8.6f, upperButtonY)));
            machines.Add(CreateExistingMachine(5, WeaponType.Bomb, new Vector2(-10.7f, -0.2f), new Vector2(-8.3f, lowerButtonY)));
        }

        private StageMirrorWeaponMachine CreateExistingMachine(int index, WeaponType type, Vector2 position, Vector2 buttonPosition)
        {
            if (objectFactory == null) return StageMirrorWeaponMachine.CreateFallback(transform, this, index, type, position);
            StageObjectType objectType = type == WeaponType.Bomb ? StageObjectType.BombDropper : StageObjectType.MissileLauncher;
            StageObjectData deviceData = StageObjectFactory.CreateDefaultData(objectType, position);
            deviceData.objectId = "15-3_weapon_" + index;
            deviceData.size = type == WeaponType.Bomb ? new Vector2(2.2f, 1.65f) : new Vector2(2.5f, 1.35f);
            GameObject device = objectFactory.Create(deviceData, transform);
            if (device == null) return StageMirrorWeaponMachine.CreateFallback(transform, this, index, type, position);
            StageBombDropper bomb = device != null ? device.GetComponent<StageBombDropper>() : null;
            StageMissileLauncher missile = device != null ? device.GetComponent<StageMissileLauncher>() : null;
            bomb?.PrepareForLink(); missile?.PrepareForLink();
            StageObjectData buttonData = StageObjectFactory.CreateDefaultData(StageObjectType.Button, buttonPosition);
            buttonData.objectId = "15-3_weapon_button_" + index;
            GameObject button = objectFactory.Create(buttonData, transform);
            if (button == null) return StageMirrorWeaponMachine.CreateFallback(transform, this, index, type, position);
            StageMirrorWeaponMachine machine = device.AddComponent<StageMirrorWeaponMachine>();
            machine.Configure(this, index, type, missile);
            StageMirrorWeaponButton receiver = button.AddComponent<StageMirrorWeaponButton>(); receiver.Configure(machine);
            return machine;
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("15-3 Battle Monitor"); monitor.transform.SetParent(transform, false); monitor.transform.localPosition = new Vector3(0f, 6.7f, 0.6f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(15f, 2.25f), -24);
            phaseText = StageEscortController.CreateText(monitor.transform, "Phase", new Vector3(-4.1f, 0f, -0.03f), 48, 0.11f, new Color(0.04f, 0.43f, 0.58f), -18);
            timerText = StageEscortController.CreateText(monitor.transform, "Time", new Vector3(4.1f, 0f, -0.03f), 54, 0.13f, new Color(0.78f, 0.39f, 0.06f), -18);
        }

        private void BuildConcealment()
        {
            concealRoot = new GameObject("15-3 Ink Mix Overlay"); concealRoot.transform.SetParent(transform, false); concealRoot.transform.localPosition = new Vector3(0f, 0.7f, -5f);
            StageEscortController.AddFilledRect(concealRoot.transform, "Ink", Vector2.zero, new Vector2(45f, 23f), new Color(0.035f, 0.02f, 0.055f, 0f), 980);
            Transform ink = concealRoot.transform.Find("Ink"); concealInk = ink != null ? ink.GetComponent<SpriteRenderer>() : null;
            BuildPaintSplat();
            for (int i = 0; i < 27; i++)
            {
                string strokeName = "INK Stroke " + i.ToString("D2");
                StageEscortController.AddLine(concealRoot.transform, Vector2.zero, Vector2.right, 1.25f, Color.black, 981);
                Transform strokeTransform = concealRoot.transform.Find("Crayon Line"); if (strokeTransform != null) strokeTransform.name = strokeName;
                LineRenderer stroke = strokeTransform != null ? strokeTransform.GetComponent<LineRenderer>() : null; if (stroke == null) continue;
                stroke.useWorldSpace = false; stroke.positionCount = 6; stroke.startWidth = stroke.endWidth = 1.25f;
                stroke.numCapVertices = 8; stroke.numCornerVertices = 8; stroke.sortingOrder = 981;
                stroke.startColor = stroke.endColor = i % 3 == 0 ? new Color(0.12f, 0.025f, 0.18f) : i % 3 == 1 ? new Color(0.025f, 0.04f, 0.09f) : new Color(0.04f, 0.015f, 0.055f);
                float y = -10.4f + i * 0.8f; float wobble = i % 2 == 0 ? 0.34f : -0.34f;
                for (int p = 0; p < 6; p++) stroke.SetPosition(p, new Vector3(-23f + p * 9.2f, y + (p % 2 == 0 ? wobble : -wobble), 0f));
                stroke.enabled = false; concealStrokes.Add(stroke);
            }
            eraserVisual = new GameObject("Eraser Sweep"); eraserVisual.transform.SetParent(concealRoot.transform, false);
            StageEscortController.AddFilledRect(eraserVisual.transform, "Eraser", Vector2.zero, new Vector2(7f, 1.15f), new Color(0.96f, 0.82f, 0.68f), 985);
            StageEscortController.AddBoxOutline(eraserVisual.transform, Vector2.zero, new Vector2(7f, 1.15f), new Color(0.18f, 0.12f, 0.1f), 986); eraserVisual.SetActive(false);
            GameObject result = new GameObject("Result Banner"); result.transform.SetParent(transform, false); result.transform.localPosition = new Vector3(0f, 0.7f, -6f);
            StageEscortController.AddFilledRect(result.transform, "Failure Paper", Vector2.zero, new Vector2(16f, 4.2f), new Color(0.2f, 0.025f, 0.04f, 0.96f), 990);
            resultText = StageEscortController.CreateText(result.transform, "Failure", Vector3.zero, 62, 0.13f, new Color(1f, 0.83f, 0.7f), 991);
            result.SetActive(false);
        }

        private void BuildPaintSplat()
        {
            Color[] colors =
            {
                new Color(0.12f, 0.025f, 0.18f, 0.98f),
                new Color(0.025f, 0.04f, 0.09f, 0.98f),
                new Color(0.19f, 0.035f, 0.2f, 0.96f),
                new Color(0.04f, 0.015f, 0.055f, 0.99f)
            };

            AddPaintSplatPiece("Paint Splat Core", Vector2.zero, new Vector2(18f, 12.5f), 0f, colors[0], 982);
            for (int i = 0; i < 12; i++)
            {
                float angle = (i * 137.5f + 18f) * Mathf.Deg2Rad;
                float radius = 2.2f + i % 4 * 1.05f;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 size = new Vector2(7.2f + i % 3 * 1.5f, 5.1f + (i + 1) % 4 * 0.9f);
                AddPaintSplatPiece("Paint Splat Blob " + i.ToString("D2"), position, size,
                    i * 19f - 7f, colors[i % colors.Length], 982 + i % 2);
            }

            for (int i = 0; i < 20; i++)
            {
                float degrees = i * 137.5f + 9f;
                float angle = degrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = 5.4f + i % 6 * 1.15f;
                float length = 4.8f + i % 5 * 1.2f;
                float thickness = 0.34f + i % 4 * 0.13f;
                AddPaintSplatPiece("Paint Splat Streak " + i.ToString("D2"), direction * distance,
                    new Vector2(length, thickness), degrees, colors[(i + 1) % colors.Length], 983);
            }

            for (int i = 0; i < 30; i++)
            {
                float angle = (i * 97f + 31f) * Mathf.Deg2Rad;
                float radius = 7.5f + i % 8 * 1.72f;
                Vector2 position = new Vector2(
                    Mathf.Cos(angle) * radius * 1.35f,
                    Mathf.Sin(angle) * Mathf.Min(radius, 9.8f));
                float diameter = 0.48f + i % 5 * 0.29f;
                AddPaintSplatPiece("Paint Splat Drop " + i.ToString("D2"), position,
                    new Vector2(diameter * (1f + i % 3 * 0.18f), diameter),
                    i * 23f, colors[(i + 2) % colors.Length], 984);
            }
            SetPaintSplatImmediate(false);
        }

        private void AddPaintSplatPiece(string name, Vector2 position, Vector2 size,
            float rotation, Color color, int sortingOrder)
        {
            GameObject piece = new GameObject(name);
            piece.transform.SetParent(concealRoot.transform, false);
            piece.transform.localPosition = new Vector3(position.x, position.y, -0.08f);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            concealSplatPieces.Add(piece.transform);
            concealSplatTargetScales.Add(new Vector3(size.x, size.y, 1f));
        }

        private void SetPaintSplatImmediate(bool visible)
        {
            for (int i = 0; i < concealSplatPieces.Count; i++)
            {
                Transform piece = concealSplatPieces[i];
                if (piece == null) continue;
                piece.gameObject.SetActive(visible);
                piece.localScale = visible ? concealSplatTargetScales[i] : Vector3.zero;
            }
        }

        private IEnumerator SplashPaintOntoScreen()
        {
            for (int i = 0; i < concealSplatPieces.Count; i++)
            {
                Transform piece = concealSplatPieces[i];
                if (piece == null) continue;
                piece.gameObject.SetActive(true);
                piece.localScale = concealSplatTargetScales[i] * 0.025f;
            }
            GameSfx.Play(SfxId.DrawInkOver);

            const float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float squash = Mathf.Sin(t * Mathf.PI) * 0.13f;
                for (int i = 0; i < concealSplatPieces.Count; i++)
                {
                    Transform piece = concealSplatPieces[i];
                    if (piece == null) continue;
                    float delay = i < 13 ? 0f : i < 33 ? (i % 4) * 0.025f : (i % 6) * 0.018f;
                    float localT = Mathf.Clamp01((t - delay) / Mathf.Max(0.01f, 1f - delay));
                    float localBurst = 1f - Mathf.Pow(1f - localT, 3f);
                    float amount = Mathf.Max(0.025f, localBurst + squash * localT);
                    piece.localScale = concealSplatTargetScales[i] * amount;
                }
                yield return null;
            }
            SetPaintSplatImmediate(true);
        }

        private IEnumerator PullPaintSplatAway()
        {
            const float duration = 0.14f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float amount = 1f - Mathf.Clamp01(elapsed / duration);
                amount *= amount;
                for (int i = 0; i < concealSplatPieces.Count; i++)
                {
                    if (concealSplatPieces[i] != null)
                        concealSplatPieces[i].localScale = concealSplatTargetScales[i] * amount;
                }
                yield return null;
            }
            SetPaintSplatImmediate(false);
        }

        private IEnumerator SetConcealment(bool visible)
        {
            concealAnimationRunning = true;
            concealed = visible;
            if (concealRoot != null) concealRoot.SetActive(true);
            if (concealInk != null) { Color baseColor = concealInk.color; baseColor.a = 0f; concealInk.color = baseColor; }
            if (visible)
            {
                if (eraserVisual != null) eraserVisual.SetActive(false);
                yield return StartCoroutine(SplashPaintOntoScreen());
                for (int i = 0; i < concealStrokes.Count; i++)
                {
                    if (concealStrokes[i] != null) concealStrokes[i].enabled = true;
                    if (i % 3 == 2) yield return new WaitForSecondsRealtime(0.012f);
                }
                for (int i = 0; i < concealStrokes.Count; i++) if (concealStrokes[i] != null) concealStrokes[i].enabled = true;
                concealAnimationRunning = false; yield break;
            }
            yield return StartCoroutine(PullPaintSplatAway());
            if (eraserVisual != null) eraserVisual.SetActive(true);
            for (int i = concealStrokes.Count - 1; i >= 0; i--)
            {
                if (eraserVisual != null) eraserVisual.transform.localPosition = new Vector3(0f, -10.4f + i * 0.8f, -0.2f);
                if (concealStrokes[i] != null) concealStrokes[i].enabled = false;
                yield return new WaitForSecondsRealtime(0.022f);
            }
            if (eraserVisual != null) eraserVisual.SetActive(false);
            for (int i = 0; i < concealStrokes.Count; i++) if (concealStrokes[i] != null) concealStrokes[i].enabled = false;
            if (concealRoot != null) concealRoot.SetActive(false);
            concealAnimationRunning = false;
        }

        private void ApplyNetworkConcealment(bool visible)
        {
            if (networkConcealRoutine != null) StopCoroutine(networkConcealRoutine);
            networkConcealRoutine = StartCoroutine(ApplyNetworkConcealmentRoutine(visible));
        }

        private IEnumerator ApplyNetworkConcealmentRoutine(bool visible)
        {
            yield return SetConcealment(visible);
            networkConcealRoutine = null;
        }

        private void SetConcealmentImmediate(bool visible)
        {
            concealed = visible;
            if (concealRoot != null) concealRoot.SetActive(visible);
            if (concealInk != null)
            {
                Color color = concealInk.color;
                color.a = 0f;
                concealInk.color = color;
            }
            for (int i = 0; i < concealStrokes.Count; i++) if (concealStrokes[i] != null) concealStrokes[i].enabled = visible;
            SetPaintSplatImmediate(visible);
        }

        private void ShowFailure(int reason)
        {
            if (resultText == null) return;
            resultText.text = LocalizationManager.T(reason == 2 ? "mirror_brawl_all_down" : "mirror_brawl_time_up")
                + "\n" + LocalizationManager.T("mirror_brawl_retry");
            resultText.transform.parent.gameObject.SetActive(true);
        }

        private void RandomizeLivingCharacters()
        {
            List<Transform> bodies = new List<Transform>();
            for (int i = 0; i < playerCount; i++)
            {
                if (eliminatedPlayers.Contains(roomPlayerIds[i])) continue;
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); if (player != null) bodies.Add(player.transform);
            }
            for (int i = 0; i < fakes.Count; i++) if (fakes[i] != null && fakes[i].IsAlive) bodies.Add(fakes[i].transform);
            for (int i = bodies.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (bodies[i], bodies[j]) = (bodies[j], bodies[i]); }
            for (int i = 0; i < bodies.Count; i++)
            {
                bool upper = i % 2 == 1; float x = Mathf.Lerp(-16.5f, 16.5f, (i + 0.5f) / Mathf.Max(1f, bodies.Count));
                bodies[i].position = new Vector2(x, (upper ? UpperFloorY : LowerFloorY) + 3f);
                Rigidbody2D body = bodies[i].GetComponent<Rigidbody2D>(); if (body != null) body.linearVelocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
            for (int i = 0; i < bodies.Count; i++)
            {
                bool upper = i % 2 == 1;
                AlignCharacterBottomToSurface(bodies[i], upper ? UpperFloorY + 0.31f : LowerFloorY);
            }
            Physics2D.SyncTransforms();
        }

        internal static void AlignCharacterBottomToSurface(Transform target, float surfaceY)
        {
            if (target == null) return;
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(false);
            float minimumY = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i]; if (collider == null || !collider.enabled || collider.isTrigger) continue;
                minimumY = Mathf.Min(minimumY, collider.bounds.min.y);
            }
            if (float.IsPositiveInfinity(minimumY)) return;
            Vector3 position = target.position; position.y += surfaceY + 0.035f - minimumY; target.position = position;
            Rigidbody2D body = target.GetComponent<Rigidbody2D>(); if (body != null) body.position = position;
        }

        private void RefreshMonitor()
        {
            if (phaseText == null) return;
            if (battleState == BattleState.Cleared)
            {
                phaseText.text = LocalizationManager.T("mirror_brawl_clear"); timerText.text = ""; if (hintText != null) hintText.text = "";
            }
            else if (battleState == BattleState.Failed)
            {
                phaseText.text = LocalizationManager.T("mirror_brawl_failed"); timerText.text = "0.0"; if (hintText != null) hintText.text = "";
            }
            else if (battleState == BattleState.Intro || battleState == BattleState.Intermission)
            {
                phaseText.text = LocalizationManager.Format("mirror_brawl_phase_ready", Mathf.Max(1, phase)); timerText.text = Mathf.CeilToInt(remaining).ToString(); if (hintText != null) hintText.text = "";
            }
            else
            {
                phaseText.text = LocalizationManager.Format("mirror_brawl_phase", phase, CountLivingFakes()); timerText.text = Mathf.Max(0f, remaining).ToString("00.0");
                if (hintText != null) hintText.text = "";
            }
        }

        private void BuildRoster()
        {
            if (IsOnline)
            {
                OnlinePlayerInfo[] roster = onlineManager?.CurrentLobby?.Players;
                if (roster != null) for (int i = 0; i < roster.Length; i++)
                {
                    if (roster[i] == null || string.IsNullOrEmpty(roster[i].PlayerId)) continue;
                    int room = PlayerColorPalette.GetLobbyPlayerSlot(onlineManager.CurrentLobby, roster[i].PlayerId);
                    if (room >= 0 && room < roomPlayerIds.Length) roomPlayerIds[room] = roster[i].PlayerId;
                }
                playerCount = 0;
                for (int i = 0; i < roomPlayerIds.Length; i++)
                    if (!string.IsNullOrEmpty(roomPlayerIds[i])) playerCount = i + 1;
                playerCount = Mathf.Max(1, playerCount); return;
            }
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            playerCount = Mathf.Clamp(players.Length, 1, 4);
            for (int i = 0; i < playerCount; i++) roomPlayerIds[i] = ResolvePlayerId(players[i]);
        }

        private void PlacePlayers()
        {
            for (int i = 0; i < playerCount; i++)
            {
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); if (player == null) continue;
                player.transform.position = new Vector2(-4.5f + i * 3f, LowerFloorY + 1.3f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>(); if (body != null) body.linearVelocity = Vector2.zero;
                if (!IsOnline || stageManager.ActivePlayerTransform == player.transform) player.SetControlsEnabled(true);
            }
            Physics2D.SyncTransforms();
            for (int i = 0; i < playerCount; i++)
            {
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); if (player != null) AlignCharacterBottomToSurface(player.transform, LowerFloorY);
            }
            Physics2D.SyncTransforms();
        }

        private void LockCamera()
        {
            if (gameCamera == null) gameCamera = Camera.main; if (gameCamera == null) return;
            cameraFollow = gameCamera.GetComponent<CameraFollow2D>(); previousCameraPosition = gameCamera.transform.position; previousCameraSize = gameCamera.orthographicSize;
            if (cameraFollow != null) { previousFollowEnabled = cameraFollow.enabled; cameraFollow.enabled = false; }
            gameCamera.transform.position = new Vector3(0f, 0.7f, previousCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(8.8f, 21.4f / Mathf.Max(0.1f, gameCamera.aspect));
        }

        private void RestoreCamera()
        {
            if (gameCamera == null) return; gameCamera.transform.position = previousCameraPosition; gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousFollowEnabled;
        }

        private void BroadcastState(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.1f;
            float[] cooldowns = new float[machines.Count]; float[] angles = new float[machines.Count];
            for (int i = 0; i < cooldowns.Length; i++) { cooldowns[i] = machines[i] != null ? machines[i].CooldownRemaining : 0f; angles[i] = machines[i] != null ? machines[i].AimAngle : 0f; }
            FakeState[] fakeStates = GetFakeStates();
            Send(StateKind, new BattleSnapshot
            {
                Sequence = ++stateSequence, State = (int)battleState, Phase = phase, PlayerCount = playerCount, Remaining = remaining,
                RoomPlayerIds = (string[])roomPlayerIds.Clone(), Fakes = null, RealAmmo = GetRealAmmo(), MachineCooldowns = cooldowns,
                MachineAngles = angles, EliminatedPlayerIds = new List<string>(eliminatedPlayers).ToArray(),
                DisabledMachine = disabledMachine, DisabledRemaining = Mathf.Max(0f, disabledUntil - Time.time), Concealed = concealed,
                FailureReason = failureReason, RealPositions = GetRealPositions()
            });
            const int fakesPerPacket = 3;
            for (int start = 0; start < fakeStates.Length; start += fakesPerPacket)
            {
                int count = Mathf.Min(fakesPerPacket, fakeStates.Length - start);
                FakeState[] batch = new FakeState[count];
                System.Array.Copy(fakeStates, start, batch, 0, count);
                Send(FakeStateKind, new FakeStateBatch
                {
                    Sequence = stateSequence,
                    Fakes = batch
                });
            }
        }

        private FakeState[] GetFakeStates()
        {
            FakeState[] result = new FakeState[fakes.Count]; for (int i = 0; i < fakes.Count; i++) result[i] = fakes[i] != null ? fakes[i].ToState() : new FakeState(); return result;
        }

        private void ApplySnapshot(BattleSnapshot state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence; battleState = (BattleState)Mathf.Clamp(state.State, 0, (int)BattleState.Failed); phase = state.Phase; remaining = state.Remaining;
            if (state.RoomPlayerIds != null) System.Array.Copy(state.RoomPlayerIds, roomPlayerIds, Mathf.Min(4, state.RoomPlayerIds.Length));
            playerCount = Mathf.Clamp(state.PlayerCount, 1, 4);
            if (state.Fakes != null) ApplyFakeStates(state.Fakes, state.Sequence);
            if (!state.Concealed)
            {
                // The host's Concealed=false is authoritative. A client may have
                // missed or interrupted an earlier paint coroutine, so never let
                // a cosmetic erase animation keep the play field covered.
                if (networkConcealRoutine != null) StopCoroutine(networkConcealRoutine);
                networkConcealRoutine = null;
                concealAnimationRunning = false;
                SetConcealmentImmediate(false);
                if (eraserVisual != null) eraserVisual.SetActive(false);
                if (networkConcealSeenPhase == state.Phase)
                    networkConcealCompletedPhase = Mathf.Max(
                        networkConcealCompletedPhase, state.Phase);
                networkConcealDeadline = -1f;
            }
            else if (networkConcealCompletedPhase == state.Phase)
            {
                // Reliable packets can arrive late. Once this phase has already
                // been revealed, an older Concealed=true snapshot must not paint
                // the participant's screen again.
                SetConcealmentImmediate(false);
            }
            else
            {
                if (networkConcealSeenPhase != state.Phase)
                {
                    networkConcealSeenPhase = state.Phase;
                    networkConcealDeadline = Time.unscaledTime + 1.8f;
                }
                if (!concealed) ApplyNetworkConcealment(true);
                else if (!concealAnimationRunning) SetConcealmentImmediate(true);
            }
            ApplyRealPositions(state.RealPositions);
            ApplyEliminatedPlayers(state.EliminatedPlayerIds);
            if (battleState == BattleState.Failed) ShowFailure(state.FailureReason);
            if (state.RealAmmo != null) for (int i = 0; i < Mathf.Min(playerCount, state.RealAmmo.Length); i++)
                if (!string.IsNullOrEmpty(roomPlayerIds[i])) realMissileAmmo[roomPlayerIds[i]] = Mathf.Max(0, state.RealAmmo[i]);
            if (state.MachineCooldowns != null) for (int i = 0; i < Mathf.Min(machines.Count, state.MachineCooldowns.Length); i++) machines[i].ApplyRemoteCooldown(state.MachineCooldowns[i]);
            if (state.MachineAngles != null) for (int i = 0; i < Mathf.Min(machines.Count, state.MachineAngles.Length); i++) machines[i].ApplyRemoteAngle(state.MachineAngles[i]);
            for (int i = 0; i < machines.Count; i++) machines[i].SetDisabled(i == state.DisabledMachine && state.DisabledRemaining > 0f);
        }

        private Vector2[] GetRealPositions()
        {
            Vector2[] positions = new Vector2[playerCount];
            for (int i = 0; i < playerCount; i++) { PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); positions[i] = player != null ? (Vector2)player.transform.position : Vector2.zero; }
            return positions;
        }

        private void ApplyRealPositions(Vector2[] positions)
        {
            if (!concealed || positions == null) return;
            for (int i = 0; i < Mathf.Min(playerCount, positions.Length); i++)
            {
                PlayerController2D player = ResolvePlayer(roomPlayerIds[i]); if (player == null) continue;
                player.transform.position = positions[i]; Rigidbody2D body = player.GetComponent<Rigidbody2D>(); if (body != null) { body.position = positions[i]; body.linearVelocity = Vector2.zero; }
            }
        }

        private void ApplyEliminatedPlayers(string[] ids)
        {
            HashSet<string> incoming = ids != null ? new HashSet<string>(ids) : new HashSet<string>();
            for (int i = 0; i < playerCount; i++)
            {
                string id = roomPlayerIds[i]; PlayerController2D player = ResolvePlayer(id); if (player == null) continue;
                bool dead = incoming.Contains(id);
                bool wasDead = eliminatedPlayers.Contains(id);
                if (!dead)
                {
                    if (!wasDead) continue;
                    eliminatedPlayers.Remove(id); Rigidbody2D restoredBody = player.GetComponent<Rigidbody2D>();
                    if (restoredBody != null) { restoredBody.simulated = true; restoredBody.linearVelocity = Vector2.zero; restoredBody.angularVelocity = 0f; }
                    SetPlayerVisible(player, true);
                    if (!IsOnline || stageManager.ActivePlayerTransform == player.transform) player.SetControlsEnabled(true);
                    continue;
                }
                eliminatedPlayers.Add(id); player.SetControlsEnabled(false);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>(); if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
                SetPlayerVisible(player, false);
            }
        }

        private void ApplyFakeStates(FakeState[] states, int snapshotSequence)
        {
            if (states == null) return;
            for (int i = 0; i < states.Length; i++)
            {
                FakeState state = states[i];
                if (state == null || state.Id <= 0) continue;
                if (lastFakeSequenceById.TryGetValue(state.Id, out int appliedSequence)
                    && snapshotSequence < appliedSequence) continue;
                lastFakeSequenceById[state.Id] = snapshotSequence;
                StageMirrorCombatant fake = FindFake(state.Id);
                if (!state.Alive) { fake?.ApplyDefeat(); continue; }
                if (fake == null)
                {
                    PlayerController2D source = state.SourceRoom >= 0 && state.SourceRoom < playerCount ? ResolvePlayer(roomPlayerIds[state.SourceRoom]) : null;
                    if (source == null) continue;
                    fake = StageMirrorCombatant.Create(transform, this, source, state.Id, state.SourceRoom, phase, state.MaximumHealth, state.Position, false); fakes.Add(fake);
                }
                fake.ApplyNetworkState(state);
            }
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            // A packet queued just before a stage transition can be delivered
            // before this old controller reaches OnDisable. Never let it create
            // combatants or defeat flashes outside 15-3.
            if (!IsCurrentStageActive || data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && IsHostPlayer(data.PlayerId) && !HasAuthority) ApplySnapshot(JsonUtility.FromJson<BattleSnapshot>(data.Json));
            else if (data.Kind == FakeStateKind && IsHostPlayer(data.PlayerId) && !HasAuthority)
            {
                FakeStateBatch batch = JsonUtility.FromJson<FakeStateBatch>(data.Json);
                if (batch != null) ApplyFakeStates(batch.Fakes, batch.Sequence);
            }
            else if (data.Kind == MachineRequestKind && HasAuthority)
            {
                MachineRequest request = JsonUtility.FromJson<MachineRequest>(data.Json); if (request != null && FindRoom(data.PlayerId) >= 0) UseMachine(request.Machine, data.PlayerId, null);
            }
            else if (data.Kind == MissileRequestKind && HasAuthority)
            {
                MissileRequest request = JsonUtility.FromJson<MissileRequest>(data.Json);
                if (request != null && realMissileAmmo.TryGetValue(data.PlayerId, out int ammo) && ammo > 0) { realMissileAmmo[data.PlayerId] = ammo - 1; FireRealMissile(data.PlayerId, request.Direction.normalized); }
            }
            else if (data.Kind == ScratchRequestKind && HasAuthority)
            {
                ScratchRequest request = JsonUtility.FromJson<ScratchRequest>(data.Json); StageMirrorCombatant fake = request != null ? FindFake(request.FakeId) : null;
                PlayerController2D player = ResolvePlayer(data.PlayerId); if (fake != null && player != null && Vector2.Distance(fake.transform.position, player.transform.position) <= 4.2f) fake.TakeDamage(1, player.transform.position);
            }
            else if (data.Kind == RealHitKind && IsHostPlayer(data.PlayerId) && !HasAuthority)
            {
                RealHitState hit = JsonUtility.FromJson<RealHitState>(data.Json);
                if (hit != null)
                {
                    StageMirrorCombatant source = hit.FakeId > 0 ? FindFake(hit.FakeId) : null;
                    if (source != null) PlayScratchVisual(source.transform, source.Facing);
                    ApplyRealHit(hit.PlayerId);
                }
            }
            else if (data.Kind == RealHitKind && HasAuthority)
            {
                RealHitState hit = JsonUtility.FromJson<RealHitState>(data.Json);
                if (hit != null && hit.PlayerId == data.PlayerId && FindRoom(data.PlayerId) >= 0) ApplyRealHit(hit.PlayerId);
            }
            else if (data.Kind == WeaponGrantKind && IsHostPlayer(data.PlayerId))
            {
                WeaponGrantState grant = JsonUtility.FromJson<WeaponGrantState>(data.Json);
                if (grant != null && !string.IsNullOrEmpty(grant.PlayerId)) realMissileAmmo[grant.PlayerId] = Mathf.Max(0, grant.Ammo);
            }
        }

        private void Send(string kind, object value)
        {
            if (onlineManager != null) onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = kind, Json = JsonUtility.ToJson(value) });
        }

        private int[] GetRealAmmo()
        {
            int[] result = new int[playerCount];
            for (int i = 0; i < playerCount; i++) if (!string.IsNullOrEmpty(roomPlayerIds[i]) && realMissileAmmo.TryGetValue(roomPlayerIds[i], out int ammo)) result[i] = ammo;
            return result;
        }

        private int CountLivingFakes() { int count = 0; for (int i = 0; i < fakes.Count; i++) if (fakes[i] != null && fakes[i].IsAlive) count++; return count; }
        private StageMirrorCombatant FindFake(int id) { for (int i = 0; i < fakes.Count; i++) if (fakes[i] != null && fakes[i].FakeId == id) return fakes[i]; return null; }
        private int FindRoom(string id) { for (int i = 0; i < playerCount; i++) if (roomPlayerIds[i] == id) return i; return -1; }
        private string ResolvePlayerId(PlayerController2D player) => player == null ? null : IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null; if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i]; return null;
        }
        private bool IsHostPlayer(string id) => onlineManager != null && onlineManager.IsHostPlayer(id);
        private static int GetFacing(PlayerController2D player) { Rigidbody2D body = player != null ? player.GetComponent<Rigidbody2D>() : null; return body != null && body.linearVelocity.x < -0.05f ? -1 : 1; }
    }

    [DisallowMultipleComponent]
    public sealed class StageMirrorCombatant : MonoBehaviour
    {
        private enum Mood { Wander, Observe, SeekWeapon, Attack, Flee, ChangeFloor }
        private StageMirrorFinalBossController owner;
        private Rigidbody2D body;
        private Transform visualRoot;
        private DrawManager.Species species;
        private Mood mood;
        private bool authoritative;
        private bool alive = true;
        private bool hasMissile;
        private bool turtleShelled;
        private bool berserk;
        private Transform missileProp;
        private GameObject heldBomb;
        private Rigidbody2D heldBombBody;
        private Collider2D[] heldBombColliders;
        private int health;
        private int maximumHealth;
        private int phase;
        private int facing = 1;
        private float personalityAggression;
        private float personalityCuriosity;
        private float nextDecisionAt;
        private float nextJumpAt;
        private float nextAttackAt;
        private float desiredX;
        private bool wantsUpper;
        private Vector2 networkTarget;
        private Vector2 networkVelocity;
        private Vector2 lastProgressPosition;
        private float lastProgressAt;
        private int committedMoveDirection;
        private float moveDirectionCommittedUntil;
        public int FakeId { get; private set; }
        public int SourceRoom { get; private set; }
        public bool IsAlive => alive;
        public int Facing => facing;
        public float ScratchReach { get; private set; } = 2f;
        public bool HasWeapon => heldBomb != null || hasMissile;
        public bool IsBerserk => berserk;
        public DrawManager.Species Species => species;

        internal static StageMirrorCombatant Create(Transform parent, StageMirrorFinalBossController owner, PlayerController2D source,
            int id, int sourceRoom, int phase, int hp, Vector2 position, bool authoritative)
        {
            GameObject root = new GameObject("Player"); root.transform.SetParent(parent, false); root.transform.position = position; root.layer = source.gameObject.layer;
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3.3f; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            root.AddComponent<CarryableObject>();
            Transform sourceVisual = source.transform.Find("GeneratedBody"); Transform clone;
            if (sourceVisual != null && sourceVisual.childCount > 0)
            {
                clone = Object.Instantiate(sourceVisual.gameObject, root.transform).transform; clone.name = "GeneratedBody"; clone.localPosition = sourceVisual.localPosition;
            }
            else
            {
                GameObject fallback = new GameObject("GeneratedBody"); fallback.transform.SetParent(root.transform, false); clone = fallback.transform;
                SpriteRenderer original = source.GetComponent<SpriteRenderer>(); SpriteRenderer renderer = fallback.AddComponent<SpriteRenderer>();
                if (original != null) { renderer.sprite = original.sprite; renderer.color = original.color; renderer.sortingLayerID = original.sortingLayerID; renderer.sortingOrder = original.sortingOrder; }
                BoxCollider2D sourceCollider = source.GetComponent<BoxCollider2D>(); BoxCollider2D collider = fallback.AddComponent<BoxCollider2D>(); collider.size = sourceCollider != null ? sourceCollider.size : new Vector2(0.9f, 1.1f);
            }
            BodyBuilder builder = source.GetComponent<BodyBuilder>(); PlayerAbilityController ability = source.GetComponent<PlayerAbilityController>();
            StageMirrorCombatant fake = root.AddComponent<StageMirrorCombatant>(); fake.owner = owner; fake.body = rb; fake.visualRoot = clone;
            fake.species = builder != null ? builder.BuiltSpecies : ability != null ? ability.CurrentProfile.Species : DrawManager.Species.Human;
            fake.FakeId = id; fake.SourceRoom = sourceRoom; fake.phase = phase; fake.health = fake.maximumHealth = Mathf.Max(1, hp); fake.authoritative = authoritative;
            fake.personalityAggression = Random.Range(0.48f, 1f); fake.personalityCuriosity = Random.Range(0.38f, 1f);
            fake.lastProgressPosition = position; fake.lastProgressAt = Time.time;
            if (ability != null) fake.ScratchReach = 1.35f * PlayerController2D.CalculateCatScratchRangeMultiplier(ability.CurrentProfile.CatFrontLegInk);
            fake.DecideMood(true); return fake;
        }

        internal void ConfigureVariant(bool isBerserk)
        {
            berserk = isBerserk;
            if (!berserk) return;
            personalityAggression = Random.Range(0.92f, 1.18f);
            personalityCuriosity = Random.Range(0.65f, 1f);
            mood = Mood.Attack;
            nextDecisionAt = Time.time + Random.Range(0.15f, 0.55f);
        }

        private void Update()
        {
            if (!alive) return;
            if (!authoritative)
            {
                Vector2 predicted = networkTarget + networkVelocity * 0.075f;
                float blend = 1f - Mathf.Exp(-15f * Time.deltaTime);
                transform.position = Vector2.Lerp(transform.position, predicted, blend);
                return;
            }
            bool heldByPlayer = owner != null && owner.IsFakeCurrentlyHeld(transform);
            if (heldByPlayer) { lastProgressPosition = transform.position; lastProgressAt = Time.time; return; }
            if (body != null && (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic))
            {
                body.bodyType = RigidbodyType2D.Dynamic; body.simulated = true; body.gravityScale = 3.3f; body.freezeRotation = true;
            }
            if (((Vector2)transform.position - lastProgressPosition).sqrMagnitude > 0.04f)
            {
                lastProgressPosition = transform.position; lastProgressAt = Time.time;
            }
            else if (Time.time - lastProgressAt >= 2.5f)
            {
                StageMirrorFinalBossController.AlignCharacterBottomToSurface(transform, transform.position.y > -1f ? 1.46f : -4.6f);
                mood = Mood.Wander; desiredX = Mathf.Clamp(transform.position.x + (Random.value < 0.5f ? -1f : 1f) * Random.Range(4f, 8f), -17f, 17f);
                lastProgressPosition = transform.position; lastProgressAt = Time.time; nextDecisionAt = Time.time + 1.2f; Jump();
            }
            if (heldBomb != null) UpdateHeldBomb();
            if (Time.time >= nextDecisionAt) DecideMood(false);
            Act();
            if (transform.position.y < -8f) transform.position = new Vector2(Random.Range(-8f, 8f), -3.2f);
        }

        private void DecideMood(bool immediate)
        {
            float aggression = personalityAggression + (phase - 1) * 0.17f + (berserk ? 0.32f : 0f);
            if (species == DrawManager.Species.Cat || species == DrawManager.Species.Slime)
            {
                mood = Random.value < 0.82f ? Mood.Attack : Mood.ChangeFloor;
                PlayerController2D prey = owner.FindRealTarget(transform.position);
                    desiredX = prey != null
                        ? GetApproachPosition(prey.transform.position.x)
                        : Random.Range(-17f, 17f);
                wantsUpper = prey != null ? prey.transform.position.y > -1f : Random.value < 0.5f;
                nextDecisionAt = Time.time + (immediate ? 0.1f : Random.Range(0.28f, 0.78f));
                return;
            }
            if (owner.IsBombDangerNear(transform.position, out Vector2 danger)
                && heldBomb == null
                && (!berserk || Random.value < 0.48f))
            {
                mood = Mood.Flee; desiredX = transform.position.x + Mathf.Sign(transform.position.x - danger.x) * Random.Range(5f, 10f); nextDecisionAt = Time.time + Random.Range(1.1f, 2.4f); return;
            }
            float roll = Random.value;
            if (HasWeapon && roll < 0.3f + aggression * 0.45f) mood = Mood.Attack;
            else if (!HasWeapon && CanUseWeaponMachines
                && roll < 0.22f + phase * 0.12f + (berserk ? 0.22f : 0f)) mood = Mood.SeekWeapon;
            else if (roll < 0.43f) mood = Mood.Wander;
            else if (roll < 0.58f) mood = Mood.Observe;
            else if (roll < 0.76f + personalityCuriosity * 0.1f) mood = Mood.ChangeFloor;
            else mood = Random.value < 0.5f ? Mood.Flee : Mood.Attack;
            desiredX = Random.Range(-17f, 17f); wantsUpper = Random.value < (phase == 1 ? 0.38f : 0.52f);
            float minimum = mood == Mood.Observe ? 0.65f : berserk ? 0.5f : 0.9f;
            float maximum = berserk ? 1.8f : mood == Mood.Attack ? 2.4f : 3.2f;
            nextDecisionAt = Time.time + (immediate ? 0.12f : Random.Range(minimum, maximum));
        }

        private void Act()
        {
            SetTurtleShell(species == DrawManager.Species.Turtle && mood == Mood.Flee);
            if (turtleShelled) { MoveHorizontal(0f); return; }
            if (mood == Mood.Observe) { MoveHorizontal(0f); return; }
            if (species == DrawManager.Species.Cat || species == DrawManager.Species.Slime)
            {
                PlayerController2D prey = owner.FindRealTarget(transform.position);
                if (prey != null)
                {
                    Vector2 delta = prey.transform.position - transform.position;
                    desiredX = GetApproachPosition(prey.transform.position.x);
                    wantsUpper = prey.transform.position.y > -1f;
                    mood = Mood.Attack;
                    if (species == DrawManager.Species.Cat && delta.magnitude <= ScratchReach * 1.12f && Time.time >= nextAttackAt)
                    {
                        nextAttackAt = Time.time + Random.Range(0.8f, 1.35f);
                        owner.FakeCatScratch(this);
                    }
                }
            }
            if (mood == Mood.SeekWeapon)
            {
                StageMirrorWeaponMachine machine = owner.FindMachine(transform.position, true);
                if (machine != null)
                {
                    desiredX = machine.transform.position.x; wantsUpper = machine.transform.position.y > -1f;
                    if (Vector2.Distance(transform.position, machine.transform.position) < 1.6f) { owner.RequestMachineUse(machine.Index, this); nextDecisionAt = Time.time; }
                }
            }
            else if (mood == Mood.Attack)
            {
                PlayerController2D target = owner.FindRealTarget(transform.position);
                if (target != null)
                {
                    Vector2 delta = target.transform.position - transform.position;
                    desiredX = GetApproachPosition(target.transform.position.x);
                    if (heldBomb != null && Time.time >= nextAttackAt && delta.magnitude < 9f)
                    {
                        Vector2 velocity = species == DrawManager.Species.Bird
                            ? new Vector2(Mathf.Clamp(delta.x, -3.5f, 3.5f), -2.5f)
                            : (delta.normalized + Vector2.up * 0.28f).normalized * 12f;
                        ThrowBomb(velocity);
                    }
                    else if (hasMissile && Time.time >= nextAttackAt) { SetMissile(false); nextAttackAt = Time.time + Random.Range(1.5f, 2.8f); owner.FireFakeMissile(this, (delta + Random.insideUnitCircle * (berserk ? 1.15f : phase == 1 ? 1.2f : 0.45f)).normalized); nextDecisionAt = Time.time + 0.4f; }
                    else if (species == DrawManager.Species.Cat && delta.magnitude <= ScratchReach && Time.time >= nextAttackAt)
                    { nextAttackAt = Time.time + (phase == 1 ? 2.4f : phase == 2 ? 1.6f : 1.05f) * (berserk ? 0.68f : 1f); owner.FakeCatScratch(this); nextDecisionAt = Time.time + Random.Range(0.45f, 1.2f); }
                    if (phase >= 2 && HasWeapon && delta.magnitude < 2.5f) desiredX = transform.position.x - Mathf.Sign(delta.x) * Random.Range(3f, 6f);
                }
            }
            else if (mood == Mood.Flee)
            {
                PlayerController2D target = owner.FindRealTarget(transform.position); if (target != null && heldBomb == null) desiredX = transform.position.x + Mathf.Sign(transform.position.x - target.transform.position.x) * 7f;
            }
            NavigateLayers();
            float speed = species == DrawManager.Species.Cat ? 10.2f
                : species == DrawManager.Species.Slime ? 9.1f
                : species == DrawManager.Species.Turtle ? 5.3f : 7.1f;
            speed *= phase == 1 ? 1f : phase == 2 ? 1.08f : 1.18f;
            if (berserk) speed *= 1.22f;
            float requestedDirection = Mathf.Abs(desiredX - transform.position.x) < 0.7f
                ? 0f
                : Mathf.Sign(desiredX - transform.position.x);
            float direction = StabilizeMoveDirection(requestedDirection);
            MoveHorizontal(direction * speed);
            if (direction != 0f) SetFacing(direction < 0f ? -1 : 1);
            if (Random.value < Time.deltaTime * (species == DrawManager.Species.Slime ? 0.68f : 0.34f) * (berserk ? 2.2f : 1f) && Time.time >= nextJumpAt) Jump();
        }

        private float GetApproachPosition(float targetX)
        {
            float delta = targetX - transform.position.x;
            float comfortableDistance = species == DrawManager.Species.Cat
                ? Mathf.Max(0.9f, ScratchReach * 0.68f)
                : species == DrawManager.Species.Slime ? 1.15f : 1.4f;
            if (Mathf.Abs(delta) <= comfortableDistance)
            {
                return transform.position.x;
            }
            return targetX - Mathf.Sign(delta) * comfortableDistance;
        }

        private float StabilizeMoveDirection(float requestedDirection)
        {
            int requested = requestedDirection < -0.01f ? -1 : requestedDirection > 0.01f ? 1 : 0;
            // Reaching a goal should look like a brief human pause, not an
            // immediate left-right twitch around the exact target coordinate.
            if (requested == 0)
            {
                return 0f;
            }

            // Arena edges always override the commitment so an enemy cannot
            // intentionally keep running into the outside wall.
            if (transform.position.x >= 17.2f && requested > 0) requested = -1;
            else if (transform.position.x <= -17.2f && requested < 0) requested = 1;

            if (committedMoveDirection == 0)
            {
                committedMoveDirection = requested;
                moveDirectionCommittedUntil = Time.time + Random.Range(0.65f, 1.15f);
            }
            else if (requested != committedMoveDirection
                && Time.time >= moveDirectionCommittedUntil)
            {
                committedMoveDirection = requested;
                moveDirectionCommittedUntil = Time.time + Random.Range(0.8f, 1.45f);
            }
            return committedMoveDirection;
        }

        private void NavigateLayers()
        {
            bool upper = transform.position.y > -1f;
            if (upper == wantsUpper) return;
            float[] access = { -17f, -7f, 1f, 17f }; float nearest = access[0];
            for (int i = 1; i < access.Length; i++) if (Mathf.Abs(transform.position.x - access[i]) < Mathf.Abs(transform.position.x - nearest)) nearest = access[i];
            desiredX = nearest;
            if (!upper && Mathf.Abs(transform.position.x - nearest) < 1f && Time.time >= nextJumpAt) Jump();
        }

        private void MoveHorizontal(float x)
        {
            if (body == null) return; float acceleration = 1f - Mathf.Exp(-14f * Time.deltaTime);
            body.linearVelocity = new Vector2(Mathf.Lerp(body.linearVelocity.x, x, acceleration), body.linearVelocity.y);
            if (species == DrawManager.Species.Bird && body.linearVelocity.y < -2.2f) body.linearVelocity = new Vector2(body.linearVelocity.x, -2.2f);
        }

        private void Jump()
        {
            if (!IsGrounded()) return; nextJumpAt = Time.time + Random.Range(0.48f, 1.2f);
            float force = species == DrawManager.Species.Bird ? 12.5f : species == DrawManager.Species.Slime ? 11.8f : 10.5f;
            body.linearVelocity = new Vector2(body.linearVelocity.x, force);
        }

        private bool IsGrounded()
        {
            Collider2D collider = GetComponentInChildren<Collider2D>(); if (collider == null) return false;
            RaycastHit2D[] hits = Physics2D.BoxCastAll(collider.bounds.center, new Vector2(Mathf.Max(0.25f, collider.bounds.size.x * 0.65f), 0.12f), 0f, Vector2.down, collider.bounds.extents.y + 0.18f);
            for (int i = 0; i < hits.Length; i++) if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(transform)) return true; return false;
        }

        internal void TakeBomb(GameObject bomb)
        {
            if (bomb == null || heldBomb != null || !CanUseWeaponMachines) return; heldBomb = bomb; heldBombBody = bomb.GetComponent<Rigidbody2D>(); heldBombColliders = bomb.GetComponentsInChildren<Collider2D>();
            if (heldBombBody != null) heldBombBody.simulated = false;
            for (int i = 0; i < heldBombColliders.Length; i++) heldBombColliders[i].enabled = false;
            bomb.transform.SetParent(transform, true); bomb.transform.position = transform.position + new Vector3(facing * 0.75f, 0.8f, -0.1f);
            bomb.GetComponent<StageBomb>()?.NotifyPickedUp(); nextAttackAt = Time.time + Random.Range(0.8f, 1.8f); mood = Mood.Attack;
        }

        private void UpdateHeldBomb()
        {
            if (heldBomb == null) return;
            StageBomb fuse = heldBomb.GetComponent<StageBomb>();
            if (fuse != null && fuse.RemainingFuseSeconds <= 1f)
            {
                Vector2 emergencyThrow = new Vector2(facing * Random.Range(9f, 13f), Random.Range(6f, 9f));
                ThrowBomb(emergencyThrow);
                return;
            }
            heldBomb.transform.position = transform.position + new Vector3(facing * 0.72f, 0.85f, -0.1f);
        }

        private void ThrowBomb(Vector2 velocity)
        {
            if (heldBomb == null) return; GameObject bomb = heldBomb; heldBomb = null; bomb.transform.SetParent(owner.transform, true);
            if (heldBombBody != null) { heldBombBody.simulated = true; heldBombBody.linearVelocity = velocity; }
            if (heldBombColliders != null) for (int i = 0; i < heldBombColliders.Length; i++) if (heldBombColliders[i] != null) heldBombColliders[i].enabled = true;
            heldBombBody = null; heldBombColliders = null; nextAttackAt = Time.time + Random.Range(1.4f, 2.5f); mood = phase >= 2 ? Mood.Flee : Mood.Wander; nextDecisionAt = Time.time + Random.Range(0.8f, 1.7f);
            GameSfx.PlayAt(SfxId.HumanThrow, transform.position, 0.9f);
        }

        internal void GiveMissile()
        {
            if (!CanUseWeaponMachines) return;
            SetMissile(true); mood = Mood.Attack; nextDecisionAt = Time.time + 0.2f;
        }

        internal bool CanUseWeaponMachines => species == DrawManager.Species.Human
            || species == DrawManager.Species.Bird;

        private void SetMissile(bool value)
        {
            hasMissile = value;
            if (!value)
            {
                if (missileProp != null) Destroy(missileProp.gameObject);
                missileProp = null;
                return;
            }
            if (missileProp != null) return;
            GameObject prop = new GameObject("Held Missile"); prop.transform.SetParent(transform, false); prop.transform.localPosition = new Vector3(facing * 0.85f, 0.65f, -0.12f);
            prop.transform.localScale = new Vector3(facing, 1f, 1f);
            BossAttackVisuals.AddMissile(prop.transform, 1.05f, 0.36f,
                new Color(0.92f, 0.22f, 0.12f), new Color(1f, 0.65f, 0.08f), 45, false);
            missileProp = prop.transform;
        }

        internal void TakeDamage(int amount, Vector2 source)
        {
            if (!authoritative || !alive || amount <= 0) return;
            if (turtleShelled) return;
            health = Mathf.Max(0, health - amount); GameSfx.PlayAt(SfxId.PlayerHit, transform.position, 0.9f);
            if (health <= 0) ApplyDefeat(); else { mood = Mood.Flee; desiredX = transform.position.x + Mathf.Sign(transform.position.x - source.x) * 6f; nextDecisionAt = Time.time + Random.Range(1.2f, 2.7f); }
        }

        internal void ApplyDefeat()
        {
            if (!alive) return; alive = false;
            if (heldBomb != null) ThrowBomb(new Vector2(Random.Range(-2f, 2f), 2f));
            if (owner != null && owner.IsCurrentStageActive)
            {
                GameObject burst = new GameObject("INK Knockout"); burst.transform.SetParent(transform.parent, true); burst.transform.position = transform.position; burst.AddComponent<BombExplosionVisual>().Configure(0.75f, false, "15-3");
            }
            GameSfx.PlayAt(SfxId.EnemyDefeat, transform.position, 0.85f); gameObject.SetActive(false);
        }

        private void SetFacing(int value)
        {
            facing = value < 0 ? -1 : 1;
            if (visualRoot != null) { Vector3 scale = visualRoot.localScale; scale.x = Mathf.Abs(scale.x) * facing; visualRoot.localScale = scale; }
            if (missileProp != null)
            {
                Vector3 position = missileProp.localPosition; position.x = Mathf.Abs(position.x) * facing; missileProp.localPosition = position;
                Vector3 scale = missileProp.localScale; scale.x = Mathf.Abs(scale.x) * facing; missileProp.localScale = scale;
            }
        }

        private void SetTurtleShell(bool value)
        {
            if (turtleShelled == value || species != DrawManager.Species.Turtle || visualRoot == null) return;
            turtleShelled = value;
            Transform[] parts = visualRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (part == null || !part.name.StartsWith("HeadSegment", System.StringComparison.Ordinal)) continue;
                part.localScale = value ? Vector3.one * 0.08f : Vector3.one;
                Collider2D collider = part.GetComponent<Collider2D>(); if (collider != null) collider.enabled = !value;
            }
        }

        internal void ApplyNetworkState(StageMirrorFinalBossController.FakeState state)
        {
            if (state == null) return; health = state.Health; maximumHealth = state.MaximumHealth; SetMissile(state.HasMissile);
            if (!state.Alive) { ApplyDefeat(); return; } berserk = state.Berserk; networkTarget = state.Position; SetFacing(state.Facing); SetTurtleShell(state.Shelled); body.simulated = false;
            networkVelocity = state.Velocity;
        }

        internal StageMirrorFinalBossController.FakeState ToState() => new StageMirrorFinalBossController.FakeState
        {
            Id = FakeId, SourceRoom = SourceRoom, Position = transform.position, Velocity = body != null ? body.linearVelocity : Vector2.zero,
            Facing = facing, Health = health, MaximumHealth = maximumHealth, Alive = alive, HasMissile = hasMissile, HasBomb = heldBomb != null, Shelled = turtleShelled, Berserk = berserk
        };
    }

    public sealed class StageMirrorWeaponMachine : MonoBehaviour
    {
        private StageMirrorFinalBossController owner;
        private StageMissileLauncher missileLauncher;
        private SpriteRenderer status;
        private TextMesh label;
        private float readyAt;
        private float cooldownDuration = 1f;
        private bool disabled;
        public int Index { get; private set; }
        internal StageMirrorFinalBossController.WeaponType Type { get; private set; }
        public bool IsReady => !disabled && Time.time >= readyAt;
        public bool IsDisabled => disabled;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public float CooldownProgress => IsReady ? 1f : 1f - Mathf.Clamp01(CooldownRemaining / Mathf.Max(0.01f, cooldownDuration));
        public float AimAngle => transform.eulerAngles.z;

        internal void Configure(StageMirrorFinalBossController battle, int index, StageMirrorFinalBossController.WeaponType type, StageMissileLauncher launcher)
        {
            owner = battle; Index = index; Type = type; missileLauncher = launcher;
        }

        internal static StageMirrorWeaponMachine CreateFallback(Transform parent, StageMirrorFinalBossController owner, int index, StageMirrorFinalBossController.WeaponType type, Vector2 position)
        {
            GameObject root = new GameObject(type == StageMirrorFinalBossController.WeaponType.Bomb ? "Bomb Machine" : "Missile Machine"); root.transform.SetParent(parent, false); root.transform.localPosition = position;
            StageEscortController.AddFilledRect(root.transform, "Machine", new Vector2(0f, 0.45f), new Vector2(2.6f, 1.55f), type == StageMirrorFinalBossController.WeaponType.Bomb ? new Color(0.96f, 0.53f, 0.2f) : new Color(0.2f, 0.68f, 0.96f), 30);
            StageEscortController.AddBoxOutline(root.transform, new Vector2(0f, 0.45f), new Vector2(2.6f, 1.55f), new Color(0.08f, 0.12f, 0.18f), 31);
            StageEscortController.AddFilledRect(root.transform, "Opening", new Vector2(0f, 0.05f), new Vector2(1.15f, 0.42f), new Color(0.03f, 0.05f, 0.07f), 32);
            GameObject statusObject = new GameObject("Status"); statusObject.transform.SetParent(root.transform, false); statusObject.transform.localPosition = new Vector3(0f, 1.38f, -0.04f); statusObject.transform.localScale = new Vector3(1.8f, 0.2f, 1f);
            SpriteRenderer status = statusObject.AddComponent<SpriteRenderer>(); status.sprite = GetSquareSprite(); status.color = new Color(0.18f, 1f, 0.4f); status.sortingOrder = 33;
            TextMesh label = StageEscortController.CreateText(root.transform, "Type", new Vector3(0f, 0.75f, -0.05f), 27, 0.075f, Color.white, 34); label.text = type == StageMirrorFinalBossController.WeaponType.Bomb ? LocalizationManager.T("stage_weapon_bomb") : LocalizationManager.T("stage_weapon_missile");
            GameObject button = new GameObject("Machine Button"); button.transform.SetParent(root.transform, false); button.transform.localPosition = new Vector3(type == StageMirrorFinalBossController.WeaponType.Bomb ? 1.8f : -1.8f, -0.42f, 0f);
            BoxCollider2D trigger = button.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(1.1f, 0.75f); trigger.isTrigger = true;
            StageEscortController.AddFilledRect(button.transform, "Button", Vector2.zero, new Vector2(1.05f, 0.38f), new Color(1f, 0.23f, 0.16f), 35);
            StageMirrorWeaponButton receiver = button.AddComponent<StageMirrorWeaponButton>();
            StageMirrorWeaponMachine machine = root.AddComponent<StageMirrorWeaponMachine>(); machine.owner = owner; machine.Index = index; machine.Type = type; machine.status = status; machine.label = label;
            receiver.Configure(machine); return machine;
        }

        private void Update()
        {
            if (Type == StageMirrorFinalBossController.WeaponType.Missile && owner != null && owner.enabled)
            {
                float inwardAngle = transform.position.x >= 0f ? 180f : 0f;
                float sweep = Mathf.Sin(Time.time * 1.55f + Index * 0.73f) * 65f;
                transform.rotation = Quaternion.Euler(0f, 0f, inwardAngle + sweep);
            }
            if (status == null) return; status.color = disabled ? new Color(0.32f, 0.32f, 0.36f) : IsReady ? new Color(0.18f, 1f, 0.4f) : new Color(1f, 0.2f, 0.12f);
            if (label != null && disabled) label.text = "X"; else if (label != null) label.text = Type == StageMirrorFinalBossController.WeaponType.Bomb ? LocalizationManager.T("stage_weapon_bomb") : LocalizationManager.T("stage_weapon_missile");
        }
        internal bool TryConsume(float now, float cooldown) { if (!IsReady) return false; cooldownDuration = Mathf.Max(0.01f, cooldown); readyAt = now + cooldownDuration; return true; }
        internal void ApplyRemoteCooldown(float seconds) { cooldownDuration = Mathf.Max(cooldownDuration, seconds); readyAt = Time.time + Mathf.Max(0f, seconds); }
        internal void ApplyRemoteAngle(float angle) { if (Type == StageMirrorFinalBossController.WeaponType.Missile) transform.rotation = Quaternion.Euler(0f, 0f, angle); }
        internal void FireMissile() { if (missileLauncher != null) missileLauncher.ActivateFromLink(); }
        internal void SetDisabled(bool value) => disabled = value;
        internal void ResetMachine() { disabled = false; readyAt = 0f; }
        private static Sprite GetSquareSprite()
        {
            return DoodleRuntimeAssets.SquareSprite;
        }
    }

    public sealed class StageMirrorWeaponButton : MonoBehaviour
    {
        private StageMirrorWeaponMachine machine;
        private float nextUseAt;
        private StageGimmickTrigger pressedVisual;
        private bool showingPressed;
        private float pressedUntil;

        internal void Configure(StageMirrorWeaponMachine target)
        {
            machine = target;
            pressedVisual = GetComponent<StageGimmickTrigger>();
            if (pressedVisual == null) pressedVisual = gameObject.AddComponent<StageGimmickTrigger>();
            pressedVisual.enabled = false;
            pressedVisual.Configure(() => { });
        }

        private void Update()
        {
            if (machine == null) return;
            if (showingPressed && (Time.time >= pressedUntil || machine.IsDisabled))
            {
                showingPressed = false;
                pressedVisual?.ApplyPressedState(false);
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (machine == null || !machine.IsReady || Time.time < nextUseAt) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>(); StageMirrorCombatant fake = other.GetComponentInParent<StageMirrorCombatant>();
            if (player == null && fake == null) return; nextUseAt = Time.time + 0.6f;
            showingPressed = true; pressedUntil = Time.time + 5f; pressedVisual?.ApplyPressedState(true);
            StageMirrorFinalBossController owner = machine.GetComponentInParent<StageMirrorFinalBossController>();
            if (player != null) owner?.RequestMachineUse(machine.Index, player); else owner?.RequestMachineUse(machine.Index, fake);
        }
    }
}
