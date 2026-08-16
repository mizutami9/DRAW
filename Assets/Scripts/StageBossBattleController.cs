using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBossBattleController : StageEliminationChallengeController
    {
        private const string StageId = "4-3";
        private const string NetworkId = "4-3_boss_system";
        private const string StateKind = "boss_state";
        private const string AttackKind = "boss_attack";
        private const string EliminateRequestKind = "boss_eliminate_request";
        private const int MaximumHealth = 100;
        private const float EntryX = -1.4f;
        private const float LeftArenaX = -1.4f;
        private const float RightArenaX = 24.4f;
        private const float FloorY = -2.45f;
        private const float CeilingY = 11.2f;

        private enum Phase { Waiting, Intro, Fighting, Special, Defeated, Failed }
        private enum AttackType { Bombs, Beam, Enemies, SpecialOrbs, Bomber, RemoveBomber }

        [System.Serializable]
        private sealed class BossState
        {
            public int Sequence;
            public int Health;
            public int Phase;
            public Vector2 Position;
            public float Facing;
            public bool Invulnerable;
            public bool Charging;
            public float ChargeRemaining;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class PlayerState { public string PlayerId; }

        [System.Serializable]
        private sealed class AttackState
        {
            public int Sequence;
            public int Type;
            public Vector2 Origin;
            public Vector2 Direction;
            public int Variant;
            public string TargetId;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private Transform bossRoot;
        private Rigidbody2D bossBody;
        private Collider2D bossCollider;
        private Transform bossVisual;
        private SpriteRenderer bossCore;
        private SpriteRenderer bossHead;
        private SpriteRenderer leftEye;
        private SpriteRenderer rightEye;
        private SpriteRenderer chargeAura;
        private LineRenderer mouthLine;
        private LineRenderer leftBrow;
        private LineRenderer rightBrow;
        private LineRenderer leftEyeX;
        private LineRenderer rightEyeX;
        private readonly List<LineRenderer> bossFillStrokes = new List<LineRenderer>();
        private TextMesh monitorTitle;
        private TextMesh monitorStatus;
        private TextMesh monitorHealth;
        private Transform healthFill;
        private SpriteRenderer healthFillRenderer;
        private Phase phase = Phase.Waiting;
        private int health = MaximumHealth;
        private int attackIndex;
        private int spawnSequence;
        private int stateSequence;
        private int lastStateSequence;
        private int lastAttackSequence;
        private float nextAttackAt;
        private float nextStateAt;
        private float facing = -1f;
        private bool invulnerable;
        private bool usedHalfSpecial;
        private bool usedLastSpecial;
        private bool spawnedMidBomber;
        private bool spawnedLowBomber;
        private bool charging;
        private float chargeWarningRemaining;
        private bool failing;
        private readonly List<GameObject> specialOrbs = new List<GameObject>();
        private readonly Dictionary<string, StageBossBomber> bombers = new Dictionary<string, StageBossBomber>();
        private readonly List<GameObject> chargePlatforms = new List<GameObject>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
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
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            BuildBoss();
            BuildMonitor();
            CaptureParticipants();
            RefreshMonitor();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId || bossRoot == null) return;
            if (!HasAuthority)
            {
                RefreshMonitor();
                return;
            }

            BroadcastState();
            if (!failing && phase != Phase.Defeated && AreAllPlayersEliminated())
            {
                StartCoroutine(FailAndRetry());
                return;
            }
            if (phase == Phase.Waiting)
            {
                if (HasPlayerEnteredArena()) StartCoroutine(BeginBattle());
                return;
            }
            if (phase != Phase.Fighting || charging) return;

            if (!usedHalfSpecial && health <= 50)
            {
                usedHalfSpecial = true;
                StartCoroutine(RunSpecial(1));
                return;
            }
            if (!usedLastSpecial && health <= 10)
            {
                usedLastSpecial = true;
                StartCoroutine(RunSpecial(2));
                return;
            }
            if (!spawnedMidBomber && health <= 50)
            {
                spawnedMidBomber = true;
                SpawnBomber(1);
            }
            if (!spawnedLowBomber && health <= 20)
            {
                spawnedLowBomber = true;
                SpawnBomber(2);
            }
            if (Time.time >= nextAttackAt)
            {
                nextAttackAt = Time.time + GetAttackInterval();
                RunNextAttack();
            }
        }

        public bool TryHitByBullet(Vector2 point)
        {
            if (!HasAuthority || phase != Phase.Fighting || health <= 0) return true;
            if (invulnerable)
            {
                StageBossImpactFlash.Create(transform, point, new Color(0.35f, 0.85f, 1f, 1f));
                GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.82f);
                return true;
            }

            health = Mathf.Max(0, health - 1);
            StageBossImpactFlash.Create(transform, point, new Color(1f, 0.72f, 0.12f, 1f));
            if (bossVisual != null) StartCoroutine(HitFlash());
            RefreshMonitor();
            BroadcastState(true);
            if (health <= 0) StartCoroutine(DefeatBoss());
            return true;
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase == Phase.Defeated || failing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (IsOnline)
            {
                if (id != onlineManager.LocalPlayerId && !HasAuthority) return;
                if (!HasAuthority)
                {
                    ApplyElimination(id);
                    Send(EliminateRequestKind, new PlayerState { PlayerId = id });
                    return;
                }
            }
            ApplyElimination(id);
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

        private IEnumerator FailAndRetry()
        {
            if (failing || phase == Phase.Defeated) yield break;
            failing = true;
            phase = Phase.Failed;
            invulnerable = true;
            if (monitorStatus != null) monitorStatus.text = LocalizationManager.T("boss_all_out");
            BroadcastState(true);
            yield return new WaitForSeconds(3f);
            stageManager?.Retry();
        }

        private void CaptureParticipants()
        {
            participantIds.Clear();
            if (IsOnline)
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                if (players == null) return;
                for (int i = 0; i < players.Length; i++)
                    if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId))
                        participantIds.Add(players[i].PlayerId);
                return;
            }
            PlayerController2D[] local = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < local.Length; i++) participantIds.Add(ResolvePlayerId(local[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds)
                if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            if (IsOnline) return stageManager != null ? stageManager.GetOnlinePlayerId(player) : null;
            return "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private void RestorePlayers()
        {
            for (int i = 0; i < hiddenPlayers.Count; i++)
                if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
        }

        private IEnumerator BeginBattle()
        {
            phase = Phase.Intro;
            invulnerable = true;
            SetBossExpression(1);
            RefreshMonitor();
            BroadcastState(true);
            GameSfx.PlayAt(SfxId.EnemyCharge, bossRoot.position, 1.15f);
            float end = Time.time + 2.4f;
            while (Time.time < end)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 13f) * 0.045f;
                bossVisual.localScale = Vector3.one * pulse;
                yield return null;
            }
            bossVisual.localScale = Vector3.one;
            SetBossFacing();
            SetBossExpression(0);
            invulnerable = false;
            phase = Phase.Fighting;
            nextAttackAt = Time.time + 1.6f;
            RefreshMonitor();
            BroadcastState(true);
        }

        private void RunNextAttack()
        {
            int pattern = attackIndex++ % 4;
            if (pattern == 0) SpawnBombVolley();
            else if (pattern == 1) SpawnBeamVolley();
            else if (pattern == 2) SpawnEnemies();
            else StartCoroutine(ChargeAcrossArena());

            // Low HP adds overlapping attacks instead of only shortening the
            // interval. This makes each phase visibly more frantic.
            if (health <= 65 && pattern != 3 && (attackIndex & 1) == 0)
                StartCoroutine(DelayedPressureAttack(0.7f, health <= 25));
            if (health <= 25 && pattern != 3)
                StartCoroutine(DelayedPressureAttack(1.25f, true));
        }

        private float GetAttackInterval()
        {
            if (health <= 10) return 1.35f;
            if (health <= 25) return 1.55f;
            if (health <= 50) return 2.05f;
            if (health <= 75) return 2.65f;
            return 3.35f;
        }

        private IEnumerator DelayedPressureAttack(float delay, bool severe)
        {
            yield return new WaitForSeconds(delay);
            if (phase != Phase.Fighting || charging || failing || health <= 0) yield break;
            if (severe && (attackIndex & 1) == 0) SpawnBeamVolley();
            else SpawnBombVolley();
        }

        private void SpawnBomber(int level)
        {
            string id = "4-3_bomber_" + level;
            SpawnAttack(new AttackState
            {
                Type = (int)AttackType.Bomber,
                TargetId = id,
                Origin = new Vector2(level == 1 ? 17.5f : 7.5f, level == 1 ? 8.4f : 7.2f),
                Direction = level == 1 ? Vector2.left : Vector2.right,
                Variant = level
            });
        }

        internal bool CanDriveBomberAttacks => HasAuthority && phase == Phase.Fighting && !charging && !failing;

        internal void DropBombFromBomber(StageBossBomber bomber, int level)
        {
            if (!CanDriveBomberAttacks || bomber == null) return;
            Vector2 origin = (Vector2)bomber.transform.position + Vector2.down * 0.75f;
            SpawnAttack(new AttackState
            {
                Type = (int)AttackType.Bombs,
                Origin = origin,
                Direction = new Vector2(level % 2 == 0 ? 0.25f : -0.25f, -1f).normalized,
                Variant = Mathf.Clamp(level, 0, 3)
            });
        }

        internal void HitBomber(StageBossBomber bomber, Vector2 point)
        {
            if (!HasAuthority || bomber == null || !bombers.ContainsKey(bomber.BomberId)) return;
            if (!bomber.ApplyBulletHit(point)) return;
            SpawnAttack(new AttackState
            {
                Type = (int)AttackType.RemoveBomber,
                TargetId = bomber.BomberId,
                Origin = bomber.transform.position
            });
        }

        private void SpawnBombVolley()
        {
            int count = health <= 35 ? 4 : health <= 70 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                Vector2 origin = (Vector2)bossRoot.position + new Vector2(-0.8f + i * 0.55f, 2.3f + i * 0.25f);
                Vector2 direction = new Vector2(-1f, 0.55f + i * 0.2f).normalized;
                SpawnAttack(new AttackState { Type = (int)AttackType.Bombs, Origin = origin, Direction = direction, Variant = i });
            }
        }

        private void SpawnBeamVolley()
        {
            PlayerController2D target = FindNearestPlayer();
            float targetY = target != null ? Mathf.Clamp(target.transform.position.y, -1.5f, 8.8f) : 0f;
            SpawnAttack(new AttackState
            {
                Type = (int)AttackType.Beam,
                Origin = new Vector2(RightArenaX - 0.5f, targetY),
                Direction = Vector2.left,
                Variant = 0
            });
            if (health <= 45)
            {
                float secondY = targetY < 3f ? targetY + 3.1f : targetY - 3.1f;
                SpawnAttack(new AttackState
                {
                    Type = (int)AttackType.Beam,
                    Origin = new Vector2(RightArenaX - 0.5f, secondY),
                    Direction = Vector2.left,
                    Variant = 1
                });
            }
        }

        private void SpawnEnemies()
        {
            int count = health <= 40 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                SpawnAttack(new AttackState
                {
                    Type = (int)AttackType.Enemies,
                    Origin = new Vector2(RightArenaX - 1.6f - i * 0.8f, FloorY + 1.25f),
                    Direction = Vector2.left,
                    Variant = (attackIndex + i) % 3
                });
            }
        }

        private IEnumerator ChargeAcrossArena()
        {
            charging = true;
            invulnerable = true;
            float destinationX = facing < 0f ? LeftArenaX + 1.8f : RightArenaX - 1.8f;
            if (monitorStatus != null) monitorStatus.text = LocalizationManager.T("boss_charge");
            SetBossExpression(1);
            CreateChargePlatforms();
            BroadcastState(true);

            // Five full seconds of unmistakable warning: the boss turns red,
            // crouches and shakes harder while temporary escape floors appear.
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                float urgency = Mathf.Clamp01(t / 5f);
                chargeWarningRemaining = Mathf.Max(0f, 5f - t);
                RefreshMonitor();
                float shake = Mathf.Lerp(0.035f, 0.22f, urgency);
                bossVisual.localPosition = new Vector3(Mathf.Sin(Time.time * Mathf.Lerp(13f, 40f, urgency)) * shake, -urgency * 0.18f, 0f);
                float squash = 1f + Mathf.Sin(Time.time * 11f) * 0.035f;
                bossVisual.localScale = new Vector3((facing < 0f ? 1f : -1f) * (1f + urgency * 0.12f), 1f / squash, 1f);
                if (chargeAura != null)
                {
                    chargeAura.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.35f, urgency)
                        * (1f + Mathf.Sin(Time.time * 18f) * 0.08f);
                }
                yield return null;
            }
            bossVisual.localPosition = Vector3.zero;
            bossVisual.localScale = Vector3.one;
            chargeWarningRemaining = 0f;
            SetBossFacing();
            GameSfx.PlayAt(SfxId.EnemyCharge, bossRoot.position, 1.3f);
            while (Mathf.Abs(bossRoot.position.x - destinationX) > 0.12f)
            {
                float x = Mathf.MoveTowards(bossRoot.position.x, destinationX, 15.5f * Time.deltaTime);
                bossRoot.position = new Vector3(x, bossRoot.position.y, bossRoot.position.z);
                if (bossBody != null) bossBody.position = bossRoot.position;
                yield return null;
            }
            facing = -facing;
            SetBossFacing();
            invulnerable = false;
            SetBossExpression(0);
            RefreshMonitor();
            BroadcastState(true);

            // Keep the escape route for five seconds after the charge so a
            // player is never dropped immediately when the boss passes.
            yield return new WaitForSeconds(5f);
            yield return StartCoroutine(RemoveChargePlatforms());
            charging = false;
            nextAttackAt = Time.time + 1.1f;
            RefreshMonitor();
            BroadcastState(true);
        }

        private void CreateChargePlatforms()
        {
            for (int i = chargePlatforms.Count - 1; i >= 0; i--)
                if (chargePlatforms[i] != null) Destroy(chargePlatforms[i]);
            chargePlatforms.Clear();
            if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            Vector2[] positions =
            {
                new Vector2(3.2f, 1.6f), new Vector2(10.7f, 4.15f), new Vector2(18.2f, 6.7f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.OneWayPlatform, positions[i]);
                data.objectId = "4-3_charge_escape_" + i;
                data.size = new Vector2(5.2f, 0.42f);
                data.keepSeparate = true;
                GameObject platform = objectFactory?.Create(data, transform);
                if (platform == null) continue;
                platform.name = "Charge Escape Floor " + (i + 1);
                platform.transform.localScale = new Vector3(1f, 0.04f, 1f);
                foreach (SpriteRenderer renderer in platform.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = Color.Lerp(renderer.color, new Color(1f, 0.82f, 0.18f, renderer.color.a), 0.58f);
                chargePlatforms.Add(platform);
                StartCoroutine(RevealChargePlatform(platform));
                StageBossImpactFlash.Create(transform, positions[i], new Color(1f, 0.82f, 0.18f, 1f));
            }
        }

        private static IEnumerator RevealChargePlatform(GameObject platform)
        {
            float elapsed = 0f;
            while (platform != null && elapsed < 0.38f)
            {
                elapsed += Time.deltaTime;
                float y = Mathf.SmoothStep(0.04f, 1f, elapsed / 0.38f);
                platform.transform.localScale = new Vector3(1f, y, 1f);
                yield return null;
            }
            if (platform != null) platform.transform.localScale = Vector3.one;
        }

        private IEnumerator RemoveChargePlatforms()
        {
            float elapsed = 0f;
            while (elapsed < 0.42f)
            {
                elapsed += Time.deltaTime;
                float y = Mathf.SmoothStep(1f, 0.02f, elapsed / 0.42f);
                for (int i = 0; i < chargePlatforms.Count; i++)
                    if (chargePlatforms[i] != null) chargePlatforms[i].transform.localScale = new Vector3(1f, y, 1f);
                yield return null;
            }
            for (int i = chargePlatforms.Count - 1; i >= 0; i--)
                if (chargePlatforms[i] != null) Destroy(chargePlatforms[i]);
            chargePlatforms.Clear();
        }

        private IEnumerator RunSpecial(int level)
        {
            phase = Phase.Special;
            invulnerable = true;
            SetBossExpression(2);
            if (monitorStatus != null) monitorStatus.text = LocalizationManager.T("boss_special_warning");
            BroadcastState(true);
            GameSfx.PlayAt(SfxId.BombFuseStart, bossRoot.position, 1.35f);
            for (float t = 0f; t < 1.35f; t += Time.deltaTime)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.08f;
                bossVisual.localScale = Vector3.one * pulse;
                yield return null;
            }
            bossVisual.localScale = Vector3.one;
            SetBossFacing();

            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(155f, 245f, i / 4f) + (level - 1) * 11f;
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnAttack(new AttackState
                {
                    Type = (int)AttackType.SpecialOrbs,
                    Origin = (Vector2)bossRoot.position + new Vector2(facing * 2.45f, 1.1f),
                    Direction = direction,
                    Variant = level
                });
            }

            yield return new WaitForSeconds(level == 1 ? 7f : 8.5f);
            for (int i = specialOrbs.Count - 1; i >= 0; i--)
                if (specialOrbs[i] != null) Destroy(specialOrbs[i]);
            specialOrbs.Clear();
            invulnerable = false;
            phase = Phase.Fighting;
            SetBossFacing();
            SetBossExpression(0);
            nextAttackAt = Time.time + 1.4f;
            RefreshMonitor();
            BroadcastState(true);
        }

        private void SpawnAttack(AttackState attack, bool broadcast = true)
        {
            if (broadcast)
            {
                attack.Sequence = ++spawnSequence;
                Send(AttackKind, attack);
            }
            AttackType type = (AttackType)attack.Type;
            if (type == AttackType.Bombs)
            {
                if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
                string id = "4-3_boss_bomb_" + attack.Sequence.ToString("D5");
                GameObject bomb = objectFactory?.CreateDroppedBox(StageObjectType.Bomb, id, attack.Origin,
                    0.82f + attack.Variant * 0.08f, transform, 3.2f + attack.Variant * 0.25f);
                Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
                if (body != null) body.linearVelocity = attack.Direction * (8.5f + attack.Variant * 0.7f);
            }
            else if (type == AttackType.Beam)
            {
                StageBossBeam.Create(transform, attack.Origin, attack.Direction, 0.85f);
            }
            else if (type == AttackType.Enemies)
            {
                if (objectFactory == null) objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
                StageObjectType enemyType = attack.Variant == 1 ? StageObjectType.EnemyJumper
                    : attack.Variant == 2 ? StageObjectType.EnemyCharger : StageObjectType.EnemyWalker;
                objectFactory?.CreateSpawnedEnemy(enemyType, "4-3_boss_enemy_" + attack.Sequence.ToString("D5"),
                    attack.Origin, 1.05f, transform, 2.6f + attack.Variant * 0.25f, -1f);
            }
            else if (type == AttackType.SpecialOrbs)
            {
                StageBossRicochetOrb orb = StageBossRicochetOrb.Create(transform, attack.Origin, attack.Direction,
                    attack.Variant >= 2 ? 9.2f : 8.1f, attack.Variant >= 2 ? 8.5f : 7f);
                if (orb != null) specialOrbs.Add(orb.gameObject);
            }
            else if (type == AttackType.Bomber)
            {
                if (string.IsNullOrEmpty(attack.TargetId) || bombers.ContainsKey(attack.TargetId)) return;
                StageBossBomber bomber = StageBossBomber.Create(transform, this, attack.TargetId,
                    attack.Origin, attack.Direction.x, attack.Variant);
                if (bomber != null) bombers[attack.TargetId] = bomber;
            }
            else if (type == AttackType.RemoveBomber)
            {
                if (string.IsNullOrEmpty(attack.TargetId) || !bombers.TryGetValue(attack.TargetId, out StageBossBomber bomber)) return;
                bombers.Remove(attack.TargetId);
                if (bomber != null) bomber.ApplyDefeat();
            }
        }

        private IEnumerator DefeatBoss()
        {
            if (phase == Phase.Defeated) yield break;
            phase = Phase.Defeated;
            invulnerable = true;
            charging = false;
            SetBossExpression(3);
            if (bossCollider != null) bossCollider.enabled = false;
            BroadcastState(true);
            RefreshMonitor();
            GameSfx.PlayAt(SfxId.BombWallBreak, bossRoot.position, 1.4f);
            for (int i = 0; i < 7; i++)
            {
                Vector2 point = (Vector2)bossRoot.position + Random.insideUnitCircle * 2.1f;
                StageBossImpactFlash.Create(transform, point, i % 2 == 0
                    ? new Color(1f, 0.35f, 0.18f, 1f) : new Color(0.25f, 0.8f, 1f, 1f));
                bossVisual.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
                yield return new WaitForSeconds(0.22f);
            }
            float elapsed = 0f;
            while (elapsed < 1.45f)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.Lerp(1f, 0f, elapsed / 1.45f);
                bossVisual.localScale = Vector3.one * scale;
                yield return null;
            }
            yield return new WaitForSeconds(1.4f);
            stageManager.ClearStage();
        }

        private IEnumerator HitFlash()
        {
            Color original = bossCore != null ? bossCore.color : Color.white;
            if (bossCore != null) bossCore.color = Color.white;
            yield return new WaitForSeconds(0.055f);
            if (bossCore != null) bossCore.color = original;
        }

        private bool HasPlayerEnteredArena()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].gameObject.activeInHierarchy && players[i].transform.position.x >= EntryX)
                    return true;
            return false;
        }

        private PlayerController2D FindNearestPlayer()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            PlayerController2D nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].gameObject.activeInHierarchy) continue;
                float sqr = ((Vector2)players[i].transform.position - (Vector2)bossRoot.position).sqrMagnitude;
                if (sqr < best) { best = sqr; nearest = players[i]; }
            }
            return nearest;
        }

        internal void HandleBossContact(PlayerController2D player)
        {
            if (player == null || phase == Phase.Waiting || phase == Phase.Defeated) return;
            if (player.IsTurtleShelled)
            {
                GameSfx.PlayAt(SfxId.EnemyShellBounce, player.transform.position, 0.9f);
                return;
            }
            stageManager?.RespawnFromHazard(player);
        }

        private void BuildBoss()
        {
            GameObject root = new GameObject("4-3 Doodle Boss");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(RightArenaX - 2.1f, FloorY + 2.25f, 0f);
            bossRoot = root.transform;
            bossBody = root.AddComponent<Rigidbody2D>();
            bossBody.bodyType = RigidbodyType2D.Kinematic;
            bossBody.gravityScale = 0f;
            bossCollider = root.AddComponent<CapsuleCollider2D>();
            ((CapsuleCollider2D)bossCollider).size = new Vector2(3.3f, 4.4f);
            StageBossHitbox hitbox = root.AddComponent<StageBossHitbox>();
            hitbox.Configure(this);

            GameObject visual = new GameObject("Boss Drawing");
            visual.transform.SetParent(root.transform, false);
            bossVisual = visual.transform;
            GameObject core = StageGun.CreateSprite(visual.transform, "Boss Body", new Vector2(0f, 0.1f),
                new Vector2(3.15f, 3.75f), new Color(0.46f, 0.25f, 0.56f, 0.96f), 34);
            bossCore = core.GetComponent<SpriteRenderer>();
            bossCore.sprite = StageSurvivalController.GetCircleSprite();
            bossHead = StageGun.CreateSprite(visual.transform, "Boss Head", new Vector2(0f, 1.95f),
                new Vector2(2.5f, 1.75f), new Color(0.58f, 0.32f, 0.68f, 0.96f), 35).GetComponent<SpriteRenderer>();
            bossHead.sprite = StageSurvivalController.GetCircleSprite();
            leftEye = StageGun.CreateSprite(visual.transform, "Eye Left", new Vector2(-0.48f, 2.12f),
                new Vector2(0.32f, 0.42f), new Color(1f, 0.87f, 0.18f, 1f), 39).GetComponent<SpriteRenderer>();
            rightEye = StageGun.CreateSprite(visual.transform, "Eye Right", new Vector2(0.48f, 2.12f),
                new Vector2(0.32f, 0.42f), new Color(1f, 0.87f, 0.18f, 1f), 39).GetComponent<SpriteRenderer>();
            Color outline = new Color(0.15f, 0.025f, 0.22f, 1f);
            CreateBossCrayonFill(visual.transform);
            CreateBossLine(visual.transform, "Boss Body Outline", new[]
            {
                new Vector2(-1.5f, 1.55f), new Vector2(-1.68f, 0.75f), new Vector2(-1.55f, -0.8f),
                new Vector2(-1.18f, -1.75f), new Vector2(0f, -1.9f), new Vector2(1.18f, -1.75f),
                new Vector2(1.55f, -0.8f), new Vector2(1.68f, 0.75f), new Vector2(1.5f, 1.55f)
            }, 0.16f, outline, 40);
            CreateBossLine(visual.transform, "Boss Head Outline", new[]
            {
                new Vector2(-1.22f, 2.5f), new Vector2(-0.85f, 2.86f), new Vector2(0f, 2.98f),
                new Vector2(0.85f, 2.86f), new Vector2(1.22f, 2.5f), new Vector2(1.28f, 1.78f),
                new Vector2(0.82f, 1.22f), new Vector2(0f, 1.08f), new Vector2(-0.82f, 1.22f),
                new Vector2(-1.28f, 1.78f), new Vector2(-1.22f, 2.5f)
            }, 0.15f, outline, 41);
            StageGun.AddLine(visual.transform, "Boss Horn Left", new[] { new Vector2(-0.75f, 2.7f), new Vector2(-1.25f, 3.5f), new Vector2(-0.25f, 3f) }, 0.14f, outline, 38);
            StageGun.AddLine(visual.transform, "Boss Horn Right", new[] { new Vector2(0.75f, 2.7f), new Vector2(1.25f, 3.5f), new Vector2(0.25f, 3f) }, 0.14f, outline, 38);
            mouthLine = CreateBossLine(visual.transform, "Boss Mouth", new[] { new Vector2(-0.72f, 1.55f), new Vector2(0f, 1.2f), new Vector2(0.72f, 1.55f) }, 0.13f, outline, 42);
            leftBrow = CreateBossLine(visual.transform, "Boss Brow Left", new[] { new Vector2(-0.82f, 2.55f), new Vector2(-0.25f, 2.42f) }, 0.12f, outline, 42);
            rightBrow = CreateBossLine(visual.transform, "Boss Brow Right", new[] { new Vector2(0.25f, 2.42f), new Vector2(0.82f, 2.55f) }, 0.12f, outline, 42);
            leftEyeX = CreateBossLine(visual.transform, "Boss Defeat Eye Left", new[] { new Vector2(-0.7f, 2.35f), new Vector2(-0.25f, 1.9f), new Vector2(-0.48f, 2.12f), new Vector2(-0.7f, 1.9f), new Vector2(-0.25f, 2.35f) }, 0.11f, outline, 44);
            rightEyeX = CreateBossLine(visual.transform, "Boss Defeat Eye Right", new[] { new Vector2(0.25f, 2.35f), new Vector2(0.7f, 1.9f), new Vector2(0.48f, 2.12f), new Vector2(0.25f, 1.9f), new Vector2(0.7f, 2.35f) }, 0.11f, outline, 44);
            leftEyeX.enabled = rightEyeX.enabled = false;
            StageGun.AddLine(visual.transform, "Boss Arm Left", new[] { new Vector2(-1.35f, 0.9f), new Vector2(-2.25f, 0.25f), new Vector2(-2.55f, 0.7f) }, 0.2f, outline, 36);
            StageGun.AddLine(visual.transform, "Boss Arm Right", new[] { new Vector2(1.35f, 0.9f), new Vector2(2.25f, 0.25f), new Vector2(2.55f, 0.7f) }, 0.2f, outline, 36);
            StageGun.AddLine(visual.transform, "Boss Legs", new[] { new Vector2(-0.8f, -1.45f), new Vector2(-1.05f, -2.15f), new Vector2(-0.35f, -2.15f), new Vector2(0.35f, -2.15f), new Vector2(1.05f, -2.15f), new Vector2(0.8f, -1.45f) }, 0.22f, outline, 36);
            chargeAura = StageGun.CreateSprite(visual.transform, "Charge Warning Aura", new Vector2(0f, 0.45f),
                new Vector2(5.4f, 6.2f), new Color(1f, 0.08f, 0.06f, 0.18f), 32).GetComponent<SpriteRenderer>();
            chargeAura.enabled = false;
            SetBossFacing();
            SetBossExpression(0);
        }

        private void SetBossFacing()
        {
            if (bossVisual == null) return;
            Vector3 scale = bossVisual.localScale;
            scale.x = Mathf.Abs(scale.x) * (facing < 0f ? 1f : -1f);
            bossVisual.localScale = scale;
        }

        private static LineRenderer CreateBossLine(Transform parent, string name, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 5;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
            return line;
        }

        private void CreateBossCrayonFill(Transform parent)
        {
            bossFillStrokes.Clear();
            Color dark = new Color(0.24f, 0.06f, 0.34f, 0.64f);
            Color light = new Color(0.72f, 0.46f, 0.78f, 0.48f);

            // Uneven, overlapping pencil strokes stay inside an elliptical body.
            // The alternating lean and color avoids the flat digital-fill look.
            const int bodyStrokeCount = 13;
            for (int i = 0; i < bodyStrokeCount; i++)
            {
                float x = Mathf.Lerp(-1.35f, 1.35f, i / (bodyStrokeCount - 1f));
                float normalized = x / 1.48f;
                float halfHeight = 1.72f * Mathf.Sqrt(Mathf.Max(0.08f, 1f - normalized * normalized));
                float lean = i % 2 == 0 ? 0.17f : -0.13f;
                LineRenderer stroke = CreateBossLine(parent, "Body Crayon Stroke " + i, new[]
                {
                    new Vector2(x - lean * 0.45f, 0.1f - halfHeight + 0.12f),
                    new Vector2(x + lean, -0.55f),
                    new Vector2(x - lean * 0.6f, 0.25f),
                    new Vector2(x + lean * 0.7f, 0.88f),
                    new Vector2(x - lean * 0.35f, 0.1f + halfHeight - 0.12f)
                }, i % 3 == 0 ? 0.16f : 0.12f, i % 2 == 0 ? dark : light, 36);
                bossFillStrokes.Add(stroke);
            }

            const int headStrokeCount = 10;
            for (int i = 0; i < headStrokeCount; i++)
            {
                float x = Mathf.Lerp(-1.05f, 1.05f, i / (headStrokeCount - 1f));
                float normalized = x / 1.18f;
                float halfHeight = 0.73f * Mathf.Sqrt(Mathf.Max(0.08f, 1f - normalized * normalized));
                float lean = i % 2 == 0 ? -0.12f : 0.15f;
                LineRenderer stroke = CreateBossLine(parent, "Head Crayon Stroke " + i, new[]
                {
                    new Vector2(x, 1.95f - halfHeight + 0.08f),
                    new Vector2(x + lean, 1.72f),
                    new Vector2(x - lean * 0.7f, 2.17f),
                    new Vector2(x + lean * 0.45f, 1.95f + halfHeight - 0.08f)
                }, i % 3 == 1 ? 0.14f : 0.105f, i % 2 == 0 ? light : dark, 37);
                bossFillStrokes.Add(stroke);
            }

            // A few horizontal passes make the fill read as hand-layered crayon,
            // not evenly spaced hatching.
            bossFillStrokes.Add(CreateBossLine(parent, "Body Crayon Cross 1", new[]
            {
                new Vector2(-1.38f, -0.92f), new Vector2(-0.55f, -0.78f), new Vector2(0.38f, -0.96f), new Vector2(1.32f, -0.74f)
            }, 0.11f, light, 36));
            bossFillStrokes.Add(CreateBossLine(parent, "Body Crayon Cross 2", new[]
            {
                new Vector2(-1.48f, 0.52f), new Vector2(-0.38f, 0.7f), new Vector2(0.52f, 0.48f), new Vector2(1.43f, 0.64f)
            }, 0.13f, dark, 36));
        }

        // 0: normal, 1: charge/angry, 2: special/grin, 3: defeated.
        private void SetBossExpression(int expression)
        {
            if (leftEye == null || rightEye == null) return;
            bool defeated = expression == 3;
            leftEye.enabled = rightEye.enabled = !defeated;
            if (leftEyeX != null) leftEyeX.enabled = defeated;
            if (rightEyeX != null) rightEyeX.enabled = defeated;
            if (chargeAura != null) chargeAura.enabled = expression == 1;

            Color normalBody = new Color(0.46f, 0.25f, 0.56f, 0.96f);
            Color normalHead = new Color(0.58f, 0.32f, 0.68f, 0.96f);
            if (bossCore != null) bossCore.color = expression == 1 ? new Color(0.72f, 0.1f, 0.2f, 1f)
                : expression == 2 ? new Color(0.52f, 0.1f, 0.66f, 1f) : normalBody;
            if (bossHead != null) bossHead.color = expression == 1 ? new Color(0.88f, 0.16f, 0.18f, 1f)
                : expression == 2 ? new Color(0.68f, 0.18f, 0.8f, 1f) : normalHead;
            for (int i = 0; i < bossFillStrokes.Count; i++)
            {
                LineRenderer stroke = bossFillStrokes[i];
                if (stroke == null) continue;
                Color strokeColor = expression == 1
                    ? (i % 2 == 0 ? new Color(0.42f, 0.025f, 0.07f, 0.72f) : new Color(1f, 0.36f, 0.16f, 0.54f))
                    : expression == 2
                        ? (i % 2 == 0 ? new Color(0.28f, 0.04f, 0.5f, 0.68f) : new Color(0.82f, 0.38f, 0.92f, 0.52f))
                        : (i % 2 == 0 ? new Color(0.24f, 0.06f, 0.34f, 0.64f) : new Color(0.72f, 0.46f, 0.78f, 0.48f));
                stroke.startColor = stroke.endColor = strokeColor;
            }

            Color eyeColor = expression == 1 ? new Color(1f, 0.12f, 0.08f, 1f)
                : expression == 2 ? new Color(0.35f, 1f, 0.9f, 1f) : new Color(1f, 0.87f, 0.18f, 1f);
            leftEye.color = rightEye.color = eyeColor;
            leftEye.transform.localScale = rightEye.transform.localScale = expression == 1
                ? new Vector3(0.48f, 0.2f, 1f) : expression == 2 ? new Vector3(0.42f, 0.55f, 1f) : new Vector3(0.32f, 0.42f, 1f);
            leftEye.transform.localRotation = Quaternion.Euler(0f, 0f, expression == 1 ? -22f : 0f);
            rightEye.transform.localRotation = Quaternion.Euler(0f, 0f, expression == 1 ? 22f : 0f);

            if (leftBrow != null && rightBrow != null)
            {
                SetLinePoints(leftBrow, expression == 1
                    ? new[] { new Vector2(-0.88f, 2.64f), new Vector2(-0.22f, 2.28f) }
                    : new[] { new Vector2(-0.82f, 2.55f), new Vector2(-0.25f, 2.42f) });
                SetLinePoints(rightBrow, expression == 1
                    ? new[] { new Vector2(0.22f, 2.28f), new Vector2(0.88f, 2.64f) }
                    : new[] { new Vector2(0.25f, 2.42f), new Vector2(0.82f, 2.55f) });
            }
            if (mouthLine != null)
            {
                Vector2[] mouth = expression == 1
                    ? new[] { new Vector2(-0.78f, 1.42f), new Vector2(-0.38f, 1.7f), new Vector2(0f, 1.36f), new Vector2(0.38f, 1.7f), new Vector2(0.78f, 1.42f) }
                    : expression == 2
                        ? new[] { new Vector2(-0.82f, 1.5f), new Vector2(-0.42f, 1.18f), new Vector2(0f, 1.08f), new Vector2(0.42f, 1.18f), new Vector2(0.82f, 1.5f) }
                        : defeated
                            ? new[] { new Vector2(-0.7f, 1.3f), new Vector2(0f, 1.65f), new Vector2(0.7f, 1.3f) }
                            : new[] { new Vector2(-0.72f, 1.55f), new Vector2(0f, 1.2f), new Vector2(0.72f, 1.55f) };
                SetLinePoints(mouthLine, mouth);
            }
        }

        private static void SetLinePoints(LineRenderer line, Vector2[] points)
        {
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private void BuildMonitor()
        {
            GameObject board = new GameObject("4-3 Boss HP Monitor");
            board.transform.SetParent(transform, false);
            board.transform.position = new Vector3(10.8f, -5.15f, 0f);
            StageGun.CreateSprite(board.transform, "Monitor Frame", Vector2.zero, new Vector2(13.2f, 3.1f), new Color(0.18f, 0.22f, 0.26f, 0.96f), 6);
            StageGun.CreateSprite(board.transform, "Monitor Screen", Vector2.zero, new Vector2(12.5f, 2.5f), new Color(0.018f, 0.045f, 0.055f, 0.98f), 7);
            monitorTitle = CreateText(board.transform, new Vector3(-5.65f, 0.72f, -0.04f), 0.085f, new Color(0.45f, 0.9f, 1f, 1f), 9, TextAnchor.MiddleLeft);
            monitorHealth = CreateText(board.transform, new Vector3(5.65f, 0.72f, -0.04f), 0.085f, new Color(1f, 0.82f, 0.2f, 1f), 9, TextAnchor.MiddleRight);
            StageGun.CreateSprite(board.transform, "Health Track", new Vector2(0f, 0f), new Vector2(11.2f, 0.52f), new Color(0.16f, 0.18f, 0.2f, 1f), 8);
            GameObject fill = StageGun.CreateSprite(board.transform, "Health Fill", new Vector2(-5.6f, 0f), new Vector2(11.2f, 0.38f), new Color(0.25f, 0.9f, 0.38f, 1f), 9);
            healthFill = fill.transform;
            healthFillRenderer = fill.GetComponent<SpriteRenderer>();
            monitorStatus = CreateText(board.transform, new Vector3(0f, -0.72f, -0.04f), 0.09f, new Color(0.3f, 1f, 0.76f, 1f), 9, TextAnchor.MiddleCenter);
        }

        private void RefreshMonitor()
        {
            if (monitorTitle == null) return;
            monitorTitle.text = LocalizationManager.T("boss_name");
            monitorHealth.text = LocalizationManager.Format("boss_health", health, MaximumHealth);
            float ratio = Mathf.Clamp01(health / (float)MaximumHealth);
            if (healthFill != null)
            {
                Vector3 scale = healthFill.localScale;
                scale.x = 11.2f * ratio;
                healthFill.localScale = scale;
                Vector3 position = healthFill.localPosition;
                position.x = -5.6f + scale.x * 0.5f;
                healthFill.localPosition = position;
            }
            if (healthFillRenderer != null)
                healthFillRenderer.color = ratio > 0.5f ? new Color(0.25f, 0.9f, 0.38f, 1f)
                    : ratio > 0.1f ? new Color(1f, 0.72f, 0.12f, 1f) : new Color(1f, 0.18f, 0.12f, 1f);
            monitorStatus.text = phase == Phase.Waiting ? LocalizationManager.T("boss_enter_room")
                : phase == Phase.Intro ? LocalizationManager.T("boss_appears")
                : phase == Phase.Special ? LocalizationManager.T("boss_special_warning")
                : phase == Phase.Defeated ? LocalizationManager.T("boss_defeated")
                : phase == Phase.Failed ? LocalizationManager.T("boss_all_out")
                : charging && chargeWarningRemaining > 0f ? LocalizationManager.Format("boss_charge_countdown", Mathf.CeilToInt(chargeWarningRemaining))
                : invulnerable ? LocalizationManager.T("boss_invulnerable") : LocalizationManager.T("boss_fight");
        }

        private static TextMesh CreateText(Transform parent, Vector3 position, float size, Color color, int order, TextAnchor anchor)
        {
            GameObject obj = new GameObject("Monitor Text");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = size;
            text.color = color;
            Font font = StageSurvivalController.FindHandwrittenFont();
            if (font != null) { text.font = font; obj.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || !force && Time.time < nextStateAt) return;
            nextStateAt = Time.time + 0.1f;
            Send(StateKind, new BossState
            {
                Sequence = ++stateSequence,
                Health = health,
                Phase = (int)phase,
                Position = bossRoot != null ? (Vector2)bossRoot.position : Vector2.zero,
                Facing = facing,
                Invulnerable = invulnerable,
                Charging = charging,
                ChargeRemaining = chargeWarningRemaining,
                EliminatedIds = new List<string>(eliminatedIds).ToArray()
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != NetworkId) return;
            if (message.Kind == EliminateRequestKind && HasAuthority)
            {
                PlayerState request = JsonUtility.FromJson<PlayerState>(message.Json);
                if (request != null && !string.IsNullOrEmpty(request.PlayerId))
                {
                    ApplyElimination(request.PlayerId);
                    BroadcastState(true);
                }
                return;
            }
            if (HasAuthority || !IsHost(message.PlayerId)) return;
            if (message.Kind == StateKind)
            {
                BossState state = JsonUtility.FromJson<BossState>(message.Json);
                if (state == null || state.Sequence <= lastStateSequence) return;
                lastStateSequence = state.Sequence;
                health = state.Health;
                phase = (Phase)state.Phase;
                facing = state.Facing;
                invulnerable = state.Invulnerable;
                bool wasCharging = charging;
                charging = state.Charging;
                chargeWarningRemaining = state.ChargeRemaining;
                if (!wasCharging && charging) CreateChargePlatforms();
                else if (wasCharging && !charging) StartCoroutine(RemoveChargePlatforms());
                if (state.EliminatedIds != null)
                    for (int i = 0; i < state.EliminatedIds.Length; i++) ApplyElimination(state.EliminatedIds[i]);
                if (bossRoot != null)
                {
                    bossRoot.position = state.Position;
                    if (bossBody != null) bossBody.position = state.Position;
                    SetBossFacing();
                }
                SetBossExpression(phase == Phase.Defeated ? 3 : phase == Phase.Special ? 2 : charging ? 1 : 0);
                RefreshMonitor();
            }
            else if (message.Kind == AttackKind)
            {
                AttackState attack = JsonUtility.FromJson<AttackState>(message.Json);
                if (attack == null || attack.Sequence <= lastAttackSequence) return;
                lastAttackSequence = attack.Sequence;
                SpawnAttack(attack, false);
            }
        }

        private void Send<T>(string kind, T value)
        {
            if (!IsOnline || onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = NetworkId, Kind = kind, Json = JsonUtility.ToJson(value) });
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            return false;
        }
    }

    public sealed class StageBossHitbox : MonoBehaviour
    {
        private StageBossBattleController owner;
        public void Configure(StageBossBattleController value) => owner = value;
        public bool HitByBullet(Vector2 point) => owner != null && owner.TryHitByBullet(point);
        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController2D player = collision.collider != null ? collision.collider.GetComponentInParent<PlayerController2D>() : null;
            if (player == null && collision.otherCollider != null) player = collision.otherCollider.GetComponentInParent<PlayerController2D>();
            owner?.HandleBossContact(player);
        }
        private void OnCollisionStay2D(Collision2D collision)
        {
            PlayerController2D player = collision.collider != null ? collision.collider.GetComponentInParent<PlayerController2D>() : null;
            if (player == null && collision.otherCollider != null) player = collision.otherCollider.GetComponentInParent<PlayerController2D>();
            owner?.HandleBossContact(player);
        }
    }

    public sealed class StageBossBomber : MonoBehaviour
    {
        private StageBossBattleController owner;
        private Rigidbody2D body;
        private SpriteRenderer shell;
        private Transform propeller;
        private float direction;
        private float baseY;
        private float phase;
        private float speed;
        private float dropInterval;
        private float nextDropAt;
        private int level;
        private int health = 3;
        private bool defeated;

        public string BomberId { get; private set; }

        public static StageBossBomber Create(
            Transform parent,
            StageBossBattleController owner,
            string id,
            Vector2 position,
            float initialDirection,
            int level)
        {
            GameObject root = new GameObject(id);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1.65f, 1.25f);
            collider.direction = CapsuleDirection2D.Horizontal;

            GameObject balloon = StageGun.CreateSprite(root.transform, "Bomber Balloon", new Vector2(0f, 0.18f),
                new Vector2(1.65f, 1.05f), level >= 2 ? new Color(0.86f, 0.2f, 0.24f, 1f) : new Color(0.93f, 0.52f, 0.12f, 1f), 43);
            StageGun.AddLine(root.transform, "Bomber Outline", new[]
            {
                new Vector2(-0.82f, 0.18f), new Vector2(-0.62f, 0.65f), new Vector2(0f, 0.78f),
                new Vector2(0.62f, 0.65f), new Vector2(0.82f, 0.18f), new Vector2(0.6f, -0.35f),
                new Vector2(0f, -0.48f), new Vector2(-0.6f, -0.35f), new Vector2(-0.82f, 0.18f)
            }, 0.1f, new Color(0.2f, 0.04f, 0.05f, 1f), 46);
            StageGun.CreateSprite(root.transform, "Bomber Eye Left", new Vector2(-0.32f, 0.28f),
                new Vector2(0.18f, 0.22f), Color.white, 47);
            StageGun.CreateSprite(root.transform, "Bomber Eye Right", new Vector2(0.32f, 0.28f),
                new Vector2(0.18f, 0.22f), Color.white, 47);
            StageGun.AddLine(root.transform, "Bomber Angry Face", new[]
            {
                new Vector2(-0.48f, 0.52f), new Vector2(-0.18f, 0.38f),
                new Vector2(0.18f, 0.38f), new Vector2(0.48f, 0.52f),
                new Vector2(-0.35f, -0.05f), new Vector2(0f, -0.22f), new Vector2(0.35f, -0.05f)
            }, 0.075f, new Color(0.15f, 0.02f, 0.03f, 1f), 48);
            StageGun.CreateSprite(root.transform, "Bomb Mark", new Vector2(0f, -0.48f),
                new Vector2(0.42f, 0.42f), new Color(0.08f, 0.1f, 0.14f, 1f), 47);
            StageGun.AddLine(root.transform, "Bomb Fuse Mark", new[] { new Vector2(0.1f, -0.29f), new Vector2(0.26f, -0.08f) },
                0.055f, new Color(1f, 0.75f, 0.08f, 1f), 48);
            GameObject rotor = new GameObject("Bomber Propeller");
            rotor.transform.SetParent(root.transform, false);
            rotor.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            StageGun.AddLine(rotor.transform, "Rotor", new[] { new Vector2(-0.8f, 0f), new Vector2(0.8f, 0f) },
                0.09f, new Color(0.12f, 0.2f, 0.28f, 1f), 48);

            StageBossBomber bomber = root.AddComponent<StageBossBomber>();
            bomber.owner = owner;
            bomber.BomberId = id;
            bomber.body = body;
            bomber.shell = balloon.GetComponent<SpriteRenderer>();
            bomber.propeller = rotor.transform;
            bomber.direction = Mathf.Abs(initialDirection) > 0.1f ? Mathf.Sign(initialDirection) : -1f;
            bomber.baseY = position.y;
            bomber.phase = Mathf.Abs(id.GetHashCode() % 1000) * 0.01f;
            bomber.level = Mathf.Max(1, level);
            bomber.speed = level >= 2 ? 3.8f : 2.8f;
            bomber.dropInterval = level >= 2 ? 2.05f : 3.15f;
            bomber.nextDropAt = Time.time + (level >= 2 ? 1.1f : 1.7f);
            return bomber;
        }

        private void Update()
        {
            if (defeated || body == null) return;
            float x = body.position.x + direction * speed * Time.deltaTime;
            if (x <= 1.2f) { x = 1.2f; direction = 1f; }
            else if (x >= 22.3f) { x = 22.3f; direction = -1f; }
            float y = baseY + Mathf.Sin(Time.time * 1.65f + phase) * 0.7f;
            body.position = new Vector2(x, y);
            transform.position = body.position;
            if (propeller != null)
                propeller.localRotation = Quaternion.Euler(0f, 0f, Time.time * (level >= 2 ? 900f : 680f));
            if (owner != null && owner.CanDriveBomberAttacks && Time.time >= nextDropAt)
            {
                nextDropAt = Time.time + dropInterval;
                owner.DropBombFromBomber(this, level);
                GameSfx.PlayAt(SfxId.BombFuseStart, transform.position, 0.62f);
            }
        }

        public void HitByBullet(Vector2 point) => owner?.HitBomber(this, point);

        public bool ApplyBulletHit(Vector2 point)
        {
            if (defeated) return false;
            health--;
            StageBossImpactFlash.Create(transform.parent, point, new Color(1f, 0.86f, 0.2f, 1f));
            GameSfx.PlayAt(SfxId.EnemyShellBounce, point, 0.7f);
            if (shell != null) StartCoroutine(Flash());
            return health <= 0;
        }

        public void ApplyDefeat()
        {
            if (defeated) return;
            defeated = true;
            GameSfx.PlayAt(SfxId.EnemyDefeat, transform.position, 1f);
            StageBossImpactFlash.Create(transform.parent, transform.position, new Color(1f, 0.35f, 0.12f, 1f));
            Destroy(gameObject);
        }

        private IEnumerator Flash()
        {
            if (shell == null) yield break;
            Color original = shell.color;
            shell.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (shell != null) shell.color = original;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController2D player = collision.collider != null ? collision.collider.GetComponentInParent<PlayerController2D>() : null;
            if (player == null && collision.otherCollider != null)
                player = collision.otherCollider.GetComponentInParent<PlayerController2D>();
            if (player == null) return;
            if (player.IsTurtleShelled)
            {
                direction = Mathf.Sign(transform.position.x - player.transform.position.x);
                if (Mathf.Abs(direction) < 0.1f) direction = -1f;
                GameSfx.PlayAt(SfxId.EnemyShellBounce, transform.position, 0.75f);
            }
            else Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
        }
    }

    public sealed class StageBossBeam : MonoBehaviour
    {
        private LineRenderer line;
        private Vector2 origin;
        private Vector2 direction;
        private float fireAt;
        private float endAt;
        private bool fired;
        private readonly HashSet<PlayerController2D> hit = new HashSet<PlayerController2D>();

        public static StageBossBeam Create(Transform parent, Vector2 origin, Vector2 direction, float warning)
        {
            GameObject root = new GameObject("Boss Beam");
            root.transform.SetParent(parent, false);
            StageBossBeam beam = root.AddComponent<StageBossBeam>();
            beam.origin = origin;
            beam.direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.left;
            beam.fireAt = Time.time + warning;
            beam.endAt = beam.fireAt + 0.42f;
            beam.line = root.AddComponent<LineRenderer>();
            beam.line.useWorldSpace = true;
            beam.line.positionCount = 2;
            beam.line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            beam.line.sortingOrder = 48;
            return beam;
        }

        private void Update()
        {
            if (Time.time >= endAt) { Destroy(gameObject); return; }
            bool active = Time.time >= fireAt;
            if (active && !fired) { fired = true; GameSfx.PlayAt(SfxId.BeamFire, origin, 1.1f); }
            float distance = ResolveDistance(active);
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + direction * distance);
            line.startWidth = line.endWidth = active ? 0.42f : 0.075f;
            Color color = active ? new Color(1f, 0.12f, 0.15f, 0.95f)
                : new Color(1f, 0.7f, 0.12f, 0.45f + Mathf.Sin(Time.time * 24f) * 0.2f);
            line.startColor = line.endColor = color;
        }

        private float ResolveDistance(bool damage)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, 40f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float stop = 40f;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<StageBossHitbox>() != null) continue;
                PlayerController2D player = collider.GetComponentInParent<PlayerController2D>();
                if (player != null)
                {
                    if (player.IsTurtleShelled) { stop = hits[i].distance; break; }
                    if (damage && hit.Add(player)) Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
                    continue;
                }
                stop = hits[i].distance;
                break;
            }
            return stop;
        }
    }

    public sealed class StageBossRicochetOrb : MonoBehaviour
    {
        private Rigidbody2D body;
        private float speed;
        private float expireAt;

        public static StageBossRicochetOrb Create(Transform parent, Vector2 position, Vector2 direction, float speed, float lifetime)
        {
            GameObject root = new GameObject("Boss Ricochet Orb");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 20f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.38f;
            PhysicsMaterial2D material = new PhysicsMaterial2D("Boss Orb Bounce") { bounciness = 1f, friction = 0f };
            collider.sharedMaterial = material;
            StageGun.CreateSprite(root.transform, "Orb Ink", Vector2.zero, Vector2.one * 0.82f, new Color(0.9f, 0.12f, 0.72f, 1f), 49);
            StageGun.AddLine(root.transform, "Orb Ring", CirclePoints(18, 0.46f), 0.08f, new Color(0.25f, 0.02f, 0.32f, 1f), 50);
            StageBossRicochetOrb orb = root.AddComponent<StageBossRicochetOrb>();
            orb.body = body;
            orb.speed = speed;
            orb.expireAt = Time.time + lifetime;
            body.linearVelocity = (direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.left) * speed;
            return orb;
        }

        private void FixedUpdate()
        {
            if (Time.time >= expireAt) { Destroy(gameObject); return; }
            if (body.linearVelocity.sqrMagnitude < 0.1f) body.linearVelocity = Vector2.left * speed;
            else body.linearVelocity = body.linearVelocity.normalized * speed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController2D player = collision.collider != null ? collision.collider.GetComponentInParent<PlayerController2D>() : null;
            if (player == null && collision.otherCollider != null) player = collision.otherCollider.GetComponentInParent<PlayerController2D>();
            if (player == null) return;
            if (player.IsTurtleShelled)
            {
                Vector2 away = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
                if (away.sqrMagnitude > 0.01f) body.linearVelocity = away * speed;
                GameSfx.PlayAt(SfxId.EnemyShellBounce, transform.position, 0.85f);
            }
            else Object.FindFirstObjectByType<StageManager>()?.RespawnFromHazard(player);
        }

        private static Vector2[] CirclePoints(int count, float radius)
        {
            Vector2[] points = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }
    }

    public sealed class StageBossImpactFlash : MonoBehaviour
    {
        private float born;
        public static void Create(Transform parent, Vector2 point, Color color)
        {
            GameObject root = new GameObject("Boss Impact");
            root.transform.SetParent(parent, false);
            root.transform.position = point;
            StageGun.AddLine(root.transform, "Impact Star", new[]
            {
                new Vector2(-0.55f, 0f), new Vector2(0.55f, 0f), Vector2.zero,
                new Vector2(0f, -0.55f), new Vector2(0f, 0.55f), Vector2.zero,
                new Vector2(-0.4f, -0.4f), new Vector2(0.4f, 0.4f), Vector2.zero,
                new Vector2(-0.4f, 0.4f), new Vector2(0.4f, -0.4f)
            }, 0.09f, color, 55);
            root.AddComponent<StageBossImpactFlash>().born = Time.time;
        }
        private void Update()
        {
            float t = (Time.time - born) / 0.28f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.8f, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
