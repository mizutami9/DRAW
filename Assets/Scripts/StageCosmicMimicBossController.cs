using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Stage 15-3. A host-authoritative, no-attack survival boss which copies
    /// every participating player's exact GeneratedBody in species order.
    /// Each copied form lasts 40 seconds and is worth 50 HP.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9000)]
    public sealed class StageCosmicMimicBossController : StageEliminationChallengeController
    {
        private const string StageId = "15-3";
        private const string StateKind = "cosmic_mimic_state";
        private const string HitKind = "cosmic_mimic_hit";
        private const float FormDuration = 40f;
        private const float CloneInterval = 8f;
        private const float ArenaHalfWidth = 18.5f;
        private const float ArenaBottom = -8.7f;
        private const float ArenaTop = 8.2f;

        private enum BattleState { Intro, Active, Intermission, Cleared, Failed }
        private enum AttackType
        {
            HumanRocket, HumanBomb, CatScratch, CatSpikeDash, BirdBomb,
            BirdSpikeDash, TurtleLaser, TurtleBomb, SlimeSpikes, SlimeMultiply
        }

        [Serializable]
        private sealed class BattleSnapshot
        {
            public int Sequence;
            public int State;
            public int FormIndex;
            public int FormCount;
            public float Remaining;
            public int CurrentHp;
            public int MaximumHp;
            public string[] OrderedPlayerIds;
            public string[] EliminatedPlayerIds;
            public Vector2 BossPosition;
            public Vector2 BossVelocity;
            public int BossFacing;
            public bool BossShelled;
            public CloneSnapshot[] Clones;
            public AttackPacket Attack;
        }

        [Serializable]
        private sealed class CloneSnapshot
        {
            public int Id;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Facing;
        }

        private sealed class MimicClone
        {
            public int Id;
            public Transform Root;
            public Transform Visual;
            public Vector2 Velocity;
            public float MoveDirection;
            public float MoveSpeed;
            public float NextDecisionAt;
            public float NextJumpAt;
            public float NextAttackAt;
            public int AttackAlternator;
            public float LockedUntil;
            public int Facing = 1;
        }

        [Serializable]
        private sealed class AttackPacket
        {
            public int Serial;
            public int Type;
            public Vector2 Origin;
            public Vector2 Target;
            public int Variant;
            public int Seed;
            public int[] AttackerIds;
            public Vector2[] Origins;
            public Vector2[] Targets;
            public int[] Variants;
        }

        [Serializable]
        private sealed class HitRequest { public string PlayerId; }

        private sealed class SavedPhysics
        {
            public float GravityScale;
            public float LinearDamping;
            public float AngularDamping;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private Camera gameCamera;
        private CameraFollow2D cameraFollow;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool previousFollowEnabled;
        private bool cameraCaptured;
        private readonly List<string> orderedPlayerIds = new List<string>();
        private readonly HashSet<string> eliminatedPlayers = new HashSet<string>();
        private readonly Dictionary<PlayerController2D, SavedPhysics> savedPhysics = new Dictionary<PlayerController2D, SavedPhysics>();
        private readonly Dictionary<PlayerController2D, Dictionary<Renderer, bool>> hiddenRenderers = new Dictionary<PlayerController2D, Dictionary<Renderer, bool>>();
        private readonly Dictionary<PlayerController2D, Dictionary<Collider2D, bool>> hiddenColliders = new Dictionary<PlayerController2D, Dictionary<Collider2D, bool>>();
        private readonly List<MimicClone> mimicClones = new List<MimicClone>();

        private BattleState state;
        private int formIndex;
        private float remaining;
        private int currentHp;
        private int maximumHp;
        private bool initialized;
        private bool transitionRunning;
        private Coroutine resumeRoutine;
        private float nextAttackAt;
        private float nextCloneAt;
        private int nextCloneId;
        private int attackAlternator;
        private int stateSequence;
        private int lastStateSequence;
        private int attackSerial;
        private int lastPlayedAttackSerial;
        private float nextBroadcastAt;
        private float bossLockedUntil;
        private bool bossShelled;
        private Vector2 bossVelocity;
        private float bossMoveDirection;
        private float bossMoveSpeed;
        private float nextBossMoveDecisionAt;
        private float nextBossJumpAt;
        private float bossContactRadius = 1.1f;
        private int bossFacing = 1;
        private AttackPacket currentAttack;
        private Transform bossRoot;
        private Transform bossVisual;
        private DrawManager.Species bossSpecies;
        private Transform bazookaProp;
        private Transform carriedSpike;
        private Transform shellOverlay;
        private Transform monitorRoot;
        private Transform timeSegmentRoot;
        private readonly List<Transform> timeSegmentFills = new List<Transform>();
        private readonly List<float> timeSegmentWidths = new List<float>();

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;
        // A final network packet can already be queued when the stage root is
        // destroyed. Unity may still invoke the copied event delegate once in
        // that frame, and reading Behaviour.isActiveAndEnabled on a destroyed
        // component throws instead of simply returning false.
        private bool IsStageActive => this != null && isActiveAndEnabled && stageManager != null
            && stageManager.IsGameplayActive && stageManager.CurrentStageId == StageId;
        public override bool UsesGlobalFallBoundary => false;

        private void Awake()
        {
            stageManager = FindFirstObjectByType<StageManager>();
            onlineManager = FindFirstObjectByType<OnlineManager>();
            objectFactory = FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
            if (initialized && state != BattleState.Cleared && state != BattleState.Failed)
                resumeRoutine = StartCoroutine(ResumeAfterReadyRoom());
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            resumeRoutine = null;
            transitionRunning = false;
            RestoreAllPlayers();
            RestoreCamera();
        }

        private void OnDestroy()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            RestoreAllPlayers();
            RestoreCamera();
        }

        private void Start() => EnsureInitializedForPlay();

        public void EnsureInitializedForPlay()
        {
            if (initialized) return;
            RuntimeStageEditor editor = FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            initialized = true;
            BuildRoster();
            BuildSpaceArena();
            BuildGravityFieldEffect();
            BuildMonitor();
            LockCamera();
            PlacePlayers();
            ApplyCosmicPhysicsToAll();
            if (HasAuthority) StartCoroutine(StartForm(0, true));
        }

        private IEnumerator ResumeAfterReadyRoom()
        {
            // The common challenge ready room temporarily disables the loaded
            // stage root. Coroutines do not resume after that suspension, so a
            // fresh countdown must be started when the real arena is enabled.
            yield return null;
            if (!isActiveAndEnabled || stageManager == null
                || stageManager.CurrentStageId != StageId) yield break;
            transitionRunning = false;
            DestroyBoss();
            BuildRoster();
            RebuildTimeSegments();
            PlacePlayers();
            ApplyCosmicPhysicsToAll();
            LockCamera();
            currentAttack = null;
            lastPlayedAttackSerial = 0;
            eliminatedPlayers.Clear();
            resumeRoutine = null;
            if (HasAuthority)
            {
                attackSerial = 0;
                yield return StartCoroutine(StartForm(0, true));
            }
        }

        private void Update()
        {
            if (!IsStageActive) return;
            MaintainCamera();
            ApplyCosmicPhysicsToAll();
            ApplyLocalCosmicInput();
            if (HasAuthority && state == BattleState.Active)
            {
                remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                UpdateBossMotion();
                UpdateMimicClones(true);
                while (Time.time >= nextCloneAt && remaining > 0f)
                {
                    SpawnTimedClone();
                    nextCloneAt += CloneInterval;
                }
                ScheduleDueAttacks();
                if (AreAllPlayersEliminated() && !transitionRunning)
                    StartCoroutine(FailBattle());
                else if (remaining <= 0f && !transitionRunning)
                    StartCoroutine(CompleteForm());
            }
            else if (state == BattleState.Active)
            {
                UpdateMimicClones(false);
            }
            if (state == BattleState.Active && bossRoot != null)
                HitPlayersNear(bossRoot.position, bossContactRadius);
            if (HasAuthority) BroadcastState(false);
            RefreshMonitor();
        }

        private void LateUpdate()
        {
            if (!IsStageActive) return;
            MaintainCamera();
            MaintainEliminatedPlayers();
        }

        private void MaintainEliminatedPlayers()
        {
            foreach (string id in eliminatedPlayers)
            {
                PlayerController2D player = ResolvePlayer(id);
                if (player == null) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                    body.simulated = false;
                }
                player.SetControlsEnabled(false);
                SetPlayerVisible(player, false);
            }
        }

        private IEnumerator StartForm(int nextForm, bool first)
        {
            transitionRunning = true;
            state = first ? BattleState.Intro : BattleState.Intermission;
            formIndex = Mathf.Clamp(nextForm, 0, Mathf.Max(0, orderedPlayerIds.Count - 1));
            DestroyBoss();
            remaining = 3f;
            BroadcastState(true);
            for (int count = 3; count > 0; count--)
            {
                remaining = count;
                RefreshMonitor();
                GameSfx.Play(SfxId.StageCountdownTick);
                yield return new WaitForSecondsRealtime(1f);
            }
            SpawnBossForCurrentForm();
            remaining = FormDuration;
            state = BattleState.Active;
            // The boss and every later clone own separate clocks. Starting the
            // boss with jitter also prevents each form from opening on the
            // exact same beat.
            nextAttackAt = Time.time + UnityEngine.Random.Range(1.45f, 2.15f);
            nextCloneAt = Time.time + CloneInterval;
            nextCloneId = 0;
            attackAlternator = UnityEngine.Random.Range(0, 2);
            transitionRunning = false;
            BroadcastState(true);
            GameSfx.Play(SfxId.StageCountdownGo);
        }

        private IEnumerator CompleteForm()
        {
            transitionRunning = true;
            state = BattleState.Intermission;
            currentHp = Mathf.Max(0, currentHp - 50);
            BroadcastState(true);
            GameSfx.Play(SfxId.EmotePop);
            yield return new WaitForSecondsRealtime(1.7f);
            if (formIndex + 1 >= orderedPlayerIds.Count)
            {
                state = BattleState.Cleared;
                BroadcastState(true);
                yield return new WaitForSecondsRealtime(2.1f);
                stageManager.ClearStage();
                yield break;
            }
            yield return StartCoroutine(StartForm(formIndex + 1, false));
        }

        private IEnumerator FailBattle()
        {
            transitionRunning = true;
            state = BattleState.Failed;
            BroadcastState(true);
            GameSfx.Play(SfxId.PlayerDeath);
            yield return new WaitForSecondsRealtime(3f);
            stageManager.Retry();
        }

        private void BuildRoster()
        {
            orderedPlayerIds.Clear();
            if (IsOnline)
            {
                OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
                List<KeyValuePair<int, string>> byRoom = new List<KeyValuePair<int, string>>();
                if (players != null)
                    for (int i = 0; i < players.Length; i++)
                        if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId))
                            byRoom.Add(new KeyValuePair<int, string>(
                                PlayerColorPalette.GetLobbyPlayerSlot(onlineManager.CurrentLobby, players[i].PlayerId),
                                players[i].PlayerId));
                byRoom.Sort((a, b) => a.Key.CompareTo(b.Key));
                for (int i = 0; i < byRoom.Count; i++) orderedPlayerIds.Add(byRoom[i].Value);
            }
            else
            {
                PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
                for (int i = 0; i < Mathf.Min(4, players.Length); i++) orderedPlayerIds.Add(ResolvePlayerId(players[i]));
            }
            orderedPlayerIds.Sort((a, b) =>
            {
                int species = GetSpecies(ResolvePlayer(a)).CompareTo(GetSpecies(ResolvePlayer(b)));
                return species != 0 ? species : string.CompareOrdinal(a, b);
            });
            if (orderedPlayerIds.Count == 0 && stageManager.ActivePlayerTransform != null)
                orderedPlayerIds.Add(ResolvePlayerId(stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()));
            maximumHp = Mathf.Max(1, orderedPlayerIds.Count) * 50;
            currentHp = maximumHp;
        }

        private void PlacePlayers()
        {
            int count = Mathf.Max(1, orderedPlayerIds.Count);
            for (int i = 0; i < orderedPlayerIds.Count; i++)
            {
                PlayerController2D player = ResolvePlayer(orderedPlayerIds[i]);
                if (player == null) continue;
                float x = Mathf.Lerp(-10f, 10f, (i + 0.5f) / count);
                player.transform.position = new Vector2(x, -4.8f + (i % 2) * 2.2f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null) { body.position = player.transform.position; body.linearVelocity = Vector2.zero; }
            }
            Physics2D.SyncTransforms();
        }

        private void ApplyCosmicPhysicsToAll()
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Rigidbody2D body = players[i].GetComponent<Rigidbody2D>();
                if (body == null) continue;
                if (!savedPhysics.ContainsKey(players[i]))
                    savedPhysics[players[i]] = new SavedPhysics
                    {
                        GravityScale = body.gravityScale,
                        LinearDamping = body.linearDamping,
                        AngularDamping = body.angularDamping
                    };
                body.gravityScale = 0.18f;
                body.linearDamping = 0.48f;
                body.angularDamping = 1.2f;
                players[i].SetStageAirJumpStrength(2.7f);
            }
        }

        private void ApplyLocalCosmicInput()
        {
            IReadOnlyList<PlayerController2D> locals = stageManager.LocalPlayers;
            for (int i = 0; i < locals.Count; i++)
            {
                PlayerController2D player = locals[i];
                if (player == null || !player.ControlsEnabled || eliminatedPlayers.Contains(ResolvePlayerId(player))) continue;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic) continue;
                LocalPlayerInputBinding binding = player.InputBinding;
                bool descend = binding != null && binding.ReadMove().y < -0.35f;
                if (descend) body.linearVelocity = new Vector2(body.linearVelocity.x,
                    Mathf.MoveTowards(body.linearVelocity.y, -8.5f, 18f * Time.deltaTime));
                else if (body.linearVelocity.y < -3.2f)
                    body.linearVelocity = new Vector2(body.linearVelocity.x, -3.2f);
            }
        }

        private void RestoreAllPlayers()
        {
            foreach (KeyValuePair<PlayerController2D, SavedPhysics> pair in savedPhysics)
            {
                if (pair.Key == null) continue;
                Rigidbody2D body = pair.Key.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.gravityScale = pair.Value.GravityScale;
                    body.linearDamping = pair.Value.LinearDamping;
                    body.angularDamping = pair.Value.AngularDamping;
                    body.simulated = true;
                }
                pair.Key.SetStageAirJumpStrength(-1f);
                SetPlayerVisible(pair.Key, true);
                if (stageManager != null && stageManager.IsLocallyControlledPlayer(pair.Key))
                    pair.Key.SetControlsEnabled(stageManager.ShouldEnableLocalGameplayControls);
            }
            savedPhysics.Clear();
            eliminatedPlayers.Clear();
        }

        private void SpawnBossForCurrentForm()
        {
            if (orderedPlayerIds.Count == 0) return;
            PlayerController2D source = ResolvePlayer(orderedPlayerIds[formIndex]);
            if (source == null) return;
            GameObject root = new GameObject("Cosmic Mimic Boss");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector2(0f, 3.4f);
            root.layer = source.gameObject.layer;
            bossRoot = root.transform;
            Transform generated = source.transform.Find("GeneratedBody");
            if (generated != null && generated.childCount > 0)
            {
                bossVisual = Instantiate(generated.gameObject, root.transform).transform;
                bossVisual.name = "GeneratedBody";
                bossVisual.localPosition = generated.localPosition;
            }
            else
            {
                GameObject fallback = new GameObject("GeneratedBody");
                fallback.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = fallback.AddComponent<SpriteRenderer>();
                SpriteRenderer original = source.GetComponent<SpriteRenderer>();
                if (original != null) { renderer.sprite = original.sprite; renderer.color = original.color; }
                bossVisual = fallback.transform;
            }
            Collider2D[] copiedColliders = root.GetComponentsInChildren<Collider2D>(true);
            Bounds copiedBounds = new Bounds(root.transform.position, Vector3.one);
            bool hasBounds = false;
            for (int i = 0; i < copiedColliders.Length; i++)
            {
                if (!copiedColliders[i].enabled || copiedColliders[i].isTrigger) continue;
                if (!hasBounds) { copiedBounds = copiedColliders[i].bounds; hasBounds = true; }
                else copiedBounds.Encapsulate(copiedColliders[i].bounds);
            }
            bossContactRadius = hasBounds
                ? Mathf.Clamp(Mathf.Max(copiedBounds.extents.x, copiedBounds.extents.y) * 0.72f, 0.75f, 2.2f)
                : 1.1f;
            for (int i = 0; i < copiedColliders.Length; i++) copiedColliders[i].enabled = false;
            bossSpecies = GetSpecies(source);
            ApplyCosmicCharacterLook(bossRoot, bossVisual, formIndex * 41 + 7, false);
            CreateBossProps();
            bossVelocity = Vector2.zero;
            bossMoveDirection = 0f;
            bossMoveSpeed = 0f;
            nextBossMoveDecisionAt = Time.time + UnityEngine.Random.Range(0.35f, 0.9f);
            nextBossJumpAt = Time.time + UnityEngine.Random.Range(0.9f, 2.1f);
            bossFacing = 1;
            bossLockedUntil = 0f;
            bossShelled = false;
        }

        private void DestroyBoss()
        {
            for (int i = 0; i < mimicClones.Count; i++)
                if (mimicClones[i].Root != null) Destroy(mimicClones[i].Root.gameObject);
            mimicClones.Clear();
            if (bossRoot != null) Destroy(bossRoot.gameObject);
            bossRoot = bossVisual = bazookaProp = carriedSpike = shellOverlay = null;
        }

        private void CreateBossProps()
        {
            if (bossRoot == null) return;
            GameObject bazooka = new GameObject("Crayon Bazooka");
            bazooka.transform.SetParent(bossRoot, false);
            bazooka.transform.localPosition = new Vector3(0.8f, 0.15f, -0.1f);
            if (!StageGun.TryCreateResourceSprite(
                bazooka.transform,
                "StageObjects/NicoDraw/bazooka",
                "Colored Pencil Bazooka",
                new Vector2(1.35f, 1.35f),
                170))
            {
                Color green = new Color(0.18f, 0.48f, 0.25f, 1f);
                Color dark = new Color(0.04f, 0.15f, 0.08f, 1f);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Tube", new Vector2(0.02f, 0.08f),
                    new Vector2(1.18f, 0.38f), green, 168);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Muzzle", new Vector2(0.63f, 0.08f),
                    new Vector2(0.25f, 0.58f), dark, 169);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Grip", new Vector2(-0.08f, -0.26f),
                    new Vector2(0.22f, 0.44f), dark, 167, -10f);
            }
            bazooka.SetActive(false);
            bazookaProp = bazooka.transform;

            GameObject spike = bossSpecies == DrawManager.Species.Cat
                ? CreateCatClawWeapon(bossRoot, Vector2.zero, 1.5f, 165)
                : CreateBirdSpikeBallVisual(bossRoot, Vector2.zero, 1.65f, 165);
            spike.name = bossSpecies == DrawManager.Species.Cat
                ? "Carried Cat Claw Fan"
                : "Carried Bird Thorn Orb";
            spike.transform.localPosition = new Vector3(0.9f, 0f, -0.12f);
            spike.SetActive(false);
            carriedSpike = spike.transform;

            GameObject shell = new GameObject("Turtle Guard Shell");
            shell.transform.SetParent(bossRoot, false);
            SpriteRenderer shellRenderer = shell.AddComponent<SpriteRenderer>();
            shellRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            shellRenderer.color = new Color(0.2f, 0.72f, 0.38f, 0.72f);
            shellRenderer.sortingOrder = 172;
            shell.transform.localScale = new Vector3(2.6f, 1.9f, 1f);
            shell.SetActive(false);
            shellOverlay = shell.transform;
        }

        private static void ApplyCosmicCharacterLook(
            Transform root, Transform visual, int seed, bool clone)
        {
            if (visual != null)
            {
                CosmicMimicRainbowTint tint = visual.GetComponent<CosmicMimicRainbowTint>();
                if (tint == null) tint = visual.gameObject.AddComponent<CosmicMimicRainbowTint>();
                tint.Configure(seed, clone ? 0.78f : 1f);
            }
            if (root == null || root.Find("Cosmic Crayon Aura") != null) return;
            GameObject aura = new GameObject("Cosmic Crayon Aura");
            aura.transform.SetParent(root, false);
            aura.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            int moteCount = clone ? 8 : 12;
            float radius = clone ? 1.28f : 1.58f;
            for (int i = 0; i < moteCount; i++)
            {
                float angle = (i * 360f / moteCount + seed * 13f) * Mathf.Deg2Rad;
                GameObject mote = new GameObject("Rainbow Pencil Light " + (i + 1));
                mote.transform.SetParent(aura.transform, false);
                mote.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.72f,
                    0f);
                float size = (i % 3 == 0 ? 0.2f : 0.13f) * (clone ? 0.82f : 1f);
                mote.transform.localScale = new Vector3(size * 1.35f, size, 1f);
                SpriteRenderer renderer = mote.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                Color color = Color.HSVToRGB(Mathf.Repeat(seed * 0.071f + i / (float)moteCount, 1f), 0.82f, 1f);
                color.a = clone ? 0.62f : 0.82f;
                renderer.color = color;
                renderer.sortingOrder = 158;
            }
            CosmicMimicAuraMotion motion = aura.AddComponent<CosmicMimicAuraMotion>();
            motion.Configure(seed, clone);
        }

        private void UpdateBossMotion()
        {
            if (bossRoot == null) return;
            if (Time.time < bossLockedUntil)
            {
                bossVelocity = Vector2.MoveTowards(bossVelocity, Vector2.zero, 18f * Time.deltaTime);
                return;
            }

            PlayerController2D target = FindNearestLivingPlayer(bossRoot.position);
            if (Time.time >= nextBossMoveDecisionAt)
            {
                float roll = UnityEngine.Random.value;
                if (roll < 0.3f || target == null)
                {
                    bossMoveDirection = 0f;
                    bossMoveSpeed = 0f;
                    nextBossMoveDecisionAt = Time.time + UnityEngine.Random.Range(0.45f, 1.35f);
                }
                else
                {
                    float toward = target.transform.position.x < bossRoot.position.x ? -1f : 1f;
                    bossMoveDirection = UnityEngine.Random.value < 0.78f ? toward : -toward;
                    float speciesSpeed = bossSpecies == DrawManager.Species.Cat ? 1.15f
                        : bossSpecies == DrawManager.Species.Turtle ? 0.72f : 1f;
                    bossMoveSpeed = UnityEngine.Random.Range(2.1f, 4.2f) * speciesSpeed;
                    nextBossMoveDecisionAt = Time.time + UnityEngine.Random.Range(0.65f, 1.75f);
                }
            }

            if (Time.time >= nextBossJumpAt)
            {
                // Match the player's low-gravity hops: a discrete jump rather
                // than unrestricted vertical steering.
                bossVelocity.y = Mathf.Max(bossVelocity.y, UnityEngine.Random.Range(3.2f, 4.4f));
                nextBossJumpAt = Time.time + UnityEngine.Random.Range(1.15f, 2.9f);
            }

            float desiredX = bossMoveDirection * bossMoveSpeed;
            bossVelocity.x = Mathf.MoveTowards(bossVelocity.x, desiredX, 7.5f * Time.deltaTime);
            float gravityMultiplier = bossSpecies == DrawManager.Species.Bird ? 0.1f : 0.18f;
            bossVelocity.y += Physics2D.gravity.y * gravityMultiplier * Time.deltaTime;
            bossVelocity.y = Mathf.Max(bossVelocity.y, -3.2f);
            Vector2 next = (Vector2)bossRoot.position + bossVelocity * Time.deltaTime;
            float minimumX = -ArenaHalfWidth + 1.3f;
            float maximumX = ArenaHalfWidth - 1.3f;
            float minimumY = ArenaBottom + 1.4f;
            float maximumY = ArenaTop - 1.5f;
            if (next.x <= minimumX || next.x >= maximumX)
            {
                next.x = Mathf.Clamp(next.x, minimumX, maximumX);
                bossMoveDirection *= -1f;
                bossVelocity.x *= -0.35f;
                nextBossMoveDecisionAt = Time.time + 0.35f;
            }
            if (next.y <= minimumY)
            {
                next.y = minimumY;
                bossVelocity.y = 0f;
            }
            else if (next.y >= maximumY)
            {
                next.y = maximumY;
                bossVelocity.y = Mathf.Min(0f, bossVelocity.y);
            }
            bossRoot.position = next;
            if (Mathf.Abs(bossVelocity.x) > 0.15f) bossFacing = bossVelocity.x < 0f ? -1 : 1;
            if (bossVisual != null) bossVisual.localScale = new Vector3(bossFacing, 1f, 1f);
            if (bazookaProp != null)
            {
                bazookaProp.localPosition = new Vector3(0.8f * bossFacing, 0.15f, -0.1f);
                bazookaProp.localScale = new Vector3(bossFacing, 1f, 1f);
            }
            if (carriedSpike != null)
            {
                carriedSpike.localPosition = new Vector3(0.9f * bossFacing, 0f, -0.12f);
                carriedSpike.localScale = new Vector3(bossFacing, 1f, 1f);
            }
        }

        private void ScheduleDueAttacks()
        {
            if (bossRoot != null && Time.time >= nextAttackAt)
            {
                ScheduleAttack(0, bossRoot.position, attackAlternator++);
                nextAttackAt = Time.time + GetAttackDelay();
            }

            for (int i = 0; i < mimicClones.Count; i++)
            {
                MimicClone clone = mimicClones[i];
                if (clone == null || clone.Root == null || Time.time < clone.NextAttackAt) continue;
                ScheduleAttack(clone.Id, clone.Root.position, clone.AttackAlternator++);
                // Each clone rolls its own small delay. The clocks are never
                // reset from the shared boss clock, so attacks remain staggered.
                clone.NextAttackAt = Time.time + GetAttackDelay();
            }
        }

        private float GetAttackDelay()
        {
            float baseDelay = bossSpecies == DrawManager.Species.Cat ? 2.7f
                : bossSpecies == DrawManager.Species.Turtle ? 4.7f : 3.5f;
            return UnityEngine.Random.Range(baseDelay * 0.82f, baseDelay * 1.22f);
        }

        private void ScheduleAttack(int attackerId, Vector2 origin, int ability)
        {
            PlayerController2D target = FindNearestLivingPlayer(origin);
            if (target == null) return;
            AttackType type = GetAttackType(bossSpecies, ability % 2);
            int variant = type == AttackType.TurtleLaser ? UnityEngine.Random.Range(0, 4) : 0;
            AttackPacket packet = new AttackPacket
            {
                Serial = ++attackSerial,
                Type = (int)type,
                Origin = origin,
                Target = target.transform.position,
                Variant = variant,
                Seed = UnityEngine.Random.Range(1, int.MaxValue),
                AttackerIds = new[] { attackerId },
                Origins = new[] { origin },
                Targets = new[] { (Vector2)target.transform.position },
                Variants = new[] { variant }
            };
            currentAttack = packet;
            lastPlayedAttackSerial = packet.Serial;
            PlayAttack(packet);
#if NICO_DRAW_AI_TEST
            Debug.Log($"[PICO 15-3 ATTACK] t={Time.time:0.000} attacker={attackerId} type={type} serial={packet.Serial}");
#endif
            BroadcastState(true);
        }

        private static AttackType GetAttackType(DrawManager.Species species, int ability)
        {
            switch (species)
            {
                case DrawManager.Species.Cat: return ability == 0 ? AttackType.CatScratch : AttackType.CatSpikeDash;
                case DrawManager.Species.Bird: return ability == 0 ? AttackType.BirdBomb : AttackType.BirdSpikeDash;
                case DrawManager.Species.Turtle: return ability == 0 ? AttackType.TurtleLaser : AttackType.TurtleBomb;
                case DrawManager.Species.Slime: return ability == 0 ? AttackType.SlimeSpikes : AttackType.SlimeMultiply;
                default: return ability == 0 ? AttackType.HumanRocket : AttackType.HumanBomb;
            }
        }

        private void PlayAttack(AttackPacket packet)
        {
            if (packet == null) return;
            int count = GetAttackCount(packet);
            switch ((AttackType)packet.Type)
            {
                case AttackType.HumanRocket:
                    for (int i = 0; i < count; i++) StartCoroutine(HumanRocket(packet, i));
                    break;
                case AttackType.HumanBomb:
                    for (int i = 0; i < count; i++)
                    {
                        CreateRainbowTargetMarker(GetAttackTarget(packet, i), 1.5f);
                        SpawnBomb(GetAttackOrigin(packet, i), GetAttackTarget(packet, i), 0.72f, 7.5f, 1.5f, GetAttackerId(packet, i));
                    }
                    break;
                case AttackType.CatScratch:
                    for (int i = 0; i < count; i++) StartCoroutine(CatScratch(packet, i));
                    break;
                case AttackType.CatSpikeDash:
                    for (int i = 0; i < count; i++) StartCoroutine(SpikeDash(packet, i, 14f, false));
                    break;
                case AttackType.BirdBomb:
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 origin = GetAttackOrigin(packet, i);
                        CreateRainbowTargetMarker(new Vector2(origin.x, ArenaBottom), 1f);
                        SpawnBomb(origin, new Vector2(origin.x, ArenaBottom), 0.62f, 6.2f, 1f, GetAttackerId(packet, i));
                    }
                    break;
                case AttackType.BirdSpikeDash:
                    for (int i = 0; i < count; i++) StartCoroutine(SpikeDash(packet, i, 11f, true));
                    break;
                case AttackType.TurtleLaser:
                    for (int i = 0; i < count; i++) StartCoroutine(TurtleLaser(packet, i));
                    break;
                case AttackType.TurtleBomb:
                    for (int i = 0; i < count; i++) StartCoroutine(TurtleBomb(packet, i));
                    break;
                case AttackType.SlimeSpikes: StartCoroutine(SlimeSpikes(packet, false)); break;
                case AttackType.SlimeMultiply: StartCoroutine(SlimeSpikes(packet, true)); break;
            }
        }

        private static int GetAttackCount(AttackPacket packet)
            => packet != null && packet.Origins != null && packet.Origins.Length > 0
                ? packet.Origins.Length : 1;

        private static int GetAttackerId(AttackPacket packet, int index)
            => packet != null && packet.AttackerIds != null && index >= 0 && index < packet.AttackerIds.Length
                ? packet.AttackerIds[index] : 0;

        private static Vector2 GetAttackOrigin(AttackPacket packet, int index)
            => packet != null && packet.Origins != null && index >= 0 && index < packet.Origins.Length
                ? packet.Origins[index] : packet != null ? packet.Origin : Vector2.zero;

        private static Vector2 GetAttackTarget(AttackPacket packet, int index)
            => packet != null && packet.Targets != null && index >= 0 && index < packet.Targets.Length
                ? packet.Targets[index] : packet != null ? packet.Target : Vector2.zero;

        private static int GetAttackVariant(AttackPacket packet, int index)
            => packet != null && packet.Variants != null && index >= 0 && index < packet.Variants.Length
                ? packet.Variants[index] : packet != null ? packet.Variant : 0;

        private MimicClone FindMimicClone(int id)
            => mimicClones.Find(clone => clone.Id == id);

        private Transform GetAttackerRoot(int id)
        {
            if (id == 0) return bossRoot;
            MimicClone clone = FindMimicClone(id);
            return clone != null ? clone.Root : null;
        }

        private void LockAttacker(int id, float duration)
        {
            float until = Time.time + duration;
            if (id == 0)
            {
                bossLockedUntil = Mathf.Max(bossLockedUntil, until);
                bossVelocity = Vector2.zero;
                return;
            }
            MimicClone clone = FindMimicClone(id);
            if (clone == null) return;
            clone.LockedUntil = Mathf.Max(clone.LockedUntil, until);
            clone.Velocity = Vector2.zero;
        }

        private Transform CreateTemporaryBazooka(Transform attacker)
        {
            if (attacker == null) return null;
            GameObject bazooka = new GameObject("Clone Crayon Bazooka");
            bazooka.transform.SetParent(attacker, false);
            if (!StageGun.TryCreateResourceSprite(
                bazooka.transform,
                "StageObjects/NicoDraw/bazooka",
                "Colored Pencil Bazooka",
                new Vector2(1.35f, 1.35f),
                170))
            {
                Color green = new Color(0.18f, 0.48f, 0.25f, 1f);
                Color dark = new Color(0.04f, 0.15f, 0.08f, 1f);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Tube", new Vector2(0.02f, 0.08f),
                    new Vector2(1.18f, 0.38f), green, 168);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Muzzle", new Vector2(0.63f, 0.08f),
                    new Vector2(0.25f, 0.58f), dark, 169);
                StageGun.CreateSprite(bazooka.transform, "Bazooka Grip", new Vector2(-0.08f, -0.26f),
                    new Vector2(0.22f, 0.44f), dark, 167, -10f);
            }
            return bazooka.transform;
        }

        private Transform CreateTemporarySpike(Transform attacker)
        {
            if (attacker == null) return null;
            GameObject spike = bossSpecies == DrawManager.Species.Cat
                ? CreateCatClawWeapon(attacker, new Vector2(0.9f, 0f), 1.5f, 165)
                : CreateBirdSpikeBallVisual(attacker, new Vector2(0.9f, 0f), 1.65f, 165);
            spike.name = bossSpecies == DrawManager.Species.Cat
                ? "Clone Cat Claw Fan"
                : "Clone Bird Thorn Orb";
            spike.transform.localPosition = new Vector3(0.9f, 0f, -0.12f);
            return spike.transform;
        }

        private Transform SetAttackerShelled(int id, bool value)
        {
            if (id == 0)
            {
                SetBossShelled(value);
                return shellOverlay;
            }
            if (!value) return null;
            Transform attacker = GetAttackerRoot(id);
            if (attacker == null) return null;
            GameObject shell = new GameObject("Clone Turtle Guard Shell");
            shell.transform.SetParent(attacker, false);
            SpriteRenderer renderer = shell.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.2f, 0.72f, 0.38f, 0.72f);
            renderer.sortingOrder = 172;
            shell.transform.localScale = new Vector3(2.6f, 1.9f, 1f);
            return shell.transform;
        }

        private IEnumerator HumanRocket(AttackPacket packet, int attackerIndex)
        {
            int attackerId = GetAttackerId(packet, attackerIndex);
            Vector2 origin = GetAttackOrigin(packet, attackerIndex);
            Vector2 target = GetAttackTarget(packet, attackerIndex);
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.1f) direction = Vector2.right;
            LockAttacker(attackerId, 1.25f);
            Transform prop = attackerId == 0 ? bazookaProp : CreateTemporaryBazooka(GetAttackerRoot(attackerId));
            if (prop != null)
            {
                prop.gameObject.SetActive(true);
                prop.localPosition = (Vector3)(direction * 0.82f) + Vector3.back * 0.1f;
                prop.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                prop.localScale = Vector3.one;
            }
            GameObject warning = CreateBazookaAimGuide(origin, direction, target);
            yield return new WaitForSeconds(1.05f);
            if (warning != null) Destroy(warning);
            SpawnProjectile(origin + direction * 1.05f, direction * 15.5f, 0.55f, 4f, true);
            if (prop != null)
            {
                if (attackerId == 0) prop.gameObject.SetActive(false);
                else Destroy(prop.gameObject);
            }
            GameSfx.PlayAt(SfxId.CannonFire, origin);
        }

        private IEnumerator CatScratch(AttackPacket packet, int attackerIndex)
        {
            int attackerId = GetAttackerId(packet, attackerIndex);
            Vector2 target = GetAttackTarget(packet, attackerIndex);
            LockAttacker(attackerId, 1.15f);
            Transform attacker = GetAttackerRoot(attackerId);
            GameObject warning = CreateScratchWarning(target);
            float warningElapsed = 0f;
            while (warningElapsed < 0.68f)
            {
                warningElapsed += Time.deltaTime;
                if (warning != null)
                {
                    float pulse = 0.9f + Mathf.Sin(warningElapsed * 18f) * 0.08f;
                    warning.transform.localScale = Vector3.one * pulse;
                }
                yield return null;
            }
            if (warning != null) Destroy(warning);
            if (attacker != null) attacker.position = Vector2.MoveTowards(attacker.position, target, 2.4f);
            GameObject slash = CreateCatSlashVisual(target);
            CosmicHazard hazard = slash.AddComponent<CosmicHazard>();
            hazard.Configure(this, 1.25f, 0.28f);
            yield return new WaitForSeconds(0.3f);
            if (slash != null) Destroy(slash);
        }

        private IEnumerator SpikeDash(
            AttackPacket packet, int attackerIndex, float speed, bool birdStyle)
        {
            int attackerId = GetAttackerId(packet, attackerIndex);
            Transform attacker = GetAttackerRoot(attackerId);
            Transform spike = attackerId == 0 ? carriedSpike : CreateTemporarySpike(attacker);
            if (spike != null) spike.gameObject.SetActive(true);
            Vector2 direction = (GetAttackTarget(packet, attackerIndex) - GetAttackOrigin(packet, attackerIndex)).normalized;
            if (direction.sqrMagnitude < 0.1f) direction = Vector2.right;
            if (spike != null)
            {
                spike.localPosition = (Vector3)(direction * 1.05f) + Vector3.back * 0.12f;
                spike.localRotation = birdStyle
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            LockAttacker(attackerId, 1.75f);
            float chargeElapsed = 0f;
            while (chargeElapsed < 0.48f && attacker != null)
            {
                chargeElapsed += Time.deltaTime;
                if (spike != null)
                {
                    float pulse = 1f + Mathf.Sin(chargeElapsed * 24f) * 0.1f;
                    spike.localScale = Vector3.one * pulse;
                    if (birdStyle) spike.Rotate(0f, 0f, 155f * Time.deltaTime);
                }
                yield return null;
            }
            float elapsed = 0f;
            while (elapsed < 1.12f && attacker != null)
            {
                elapsed += Time.deltaTime;
                attacker.position += (Vector3)(direction * speed * Time.deltaTime);
                if (birdStyle && spike != null) spike.Rotate(0f, 0f, 460f * Time.deltaTime);
                Vector2 p = attacker.position;
                if (Mathf.Abs(p.x) > ArenaHalfWidth - 1f) direction.x *= -1f;
                if (p.y < ArenaBottom + 1f || p.y > ArenaTop - 1f) direction.y *= -1f;
                CosmicHazard.HitPlayersNear(this, attacker.position, 1.35f);
                yield return null;
            }
            if (spike != null)
            {
                if (attackerId == 0) spike.gameObject.SetActive(false);
                else Destroy(spike.gameObject);
            }
        }

        private IEnumerator TurtleLaser(AttackPacket packet, int attackerIndex)
        {
            int attackerId = GetAttackerId(packet, attackerIndex);
            Transform temporaryShell = SetAttackerShelled(attackerId, true);
            LockAttacker(attackerId, 2.8f);
            Vector2 center; Vector2 size; Vector2 lineStart; Vector2 lineEnd;
            GetLaserGeometry(GetAttackVariant(packet, attackerIndex), out center, out size, out lineStart, out lineEnd);
            GameObject warning = CreateWarningLine(lineStart, lineEnd);
            yield return new WaitForSeconds(1.35f);
            if (warning != null) Destroy(warning);
            GameObject beam = BossAttackVisuals.CreateLaser(transform, lineStart, lineEnd,
                GetAttackVariant(packet, attackerIndex) < 2 ? size.x : size.y, 184);
            CosmicBoxHazard hazard = beam.AddComponent<CosmicBoxHazard>();
            hazard.Configure(this, center, size, 1.05f);
            yield return new WaitForSeconds(1.08f);
            if (beam != null) Destroy(beam);
            if (attackerId == 0) SetBossShelled(false);
            else if (temporaryShell != null) Destroy(temporaryShell.gameObject);
        }

        private IEnumerator TurtleBomb(AttackPacket packet, int attackerIndex)
        {
            int attackerId = GetAttackerId(packet, attackerIndex);
            Transform temporaryShell = SetAttackerShelled(attackerId, true);
            LockAttacker(attackerId, 2.5f);
            SpawnBomb(GetAttackOrigin(packet, attackerIndex), GetAttackTarget(packet, attackerIndex),
                1.15f, 4.5f, 3f, attackerId);
            yield return new WaitForSeconds(2.25f);
            if (attackerId == 0) SetBossShelled(false);
            else if (temporaryShell != null) Destroy(temporaryShell.gameObject);
        }

        private IEnumerator SlimeSpikes(AttackPacket packet, bool multiply)
        {
            int attackerCount = GetAttackCount(packet);
            int rayCount = multiply ? 8 : 6;
            List<GameObject> charges = new List<GameObject>();
            for (int i = 0; i < attackerCount; i++)
            {
                Transform attacker = GetAttackerRoot(GetAttackerId(packet, i));
                Transform parent = attacker != null ? attacker : transform;
                Vector2 localPosition = attacker != null ? Vector2.zero : GetAttackOrigin(packet, i);
                GameObject charge = CreateSlimeSpikeChargeVisual(parent, localPosition,
                    multiply ? 1.5f : 1.25f, 184);
                charge.name = multiply ? "Slime Eight Spike Charge" : "Slime Six Spike Charge";
                charges.Add(charge);
            }
            float chargeElapsed = 0f;
            while (chargeElapsed < 0.85f)
            {
                chargeElapsed += Time.deltaTime;
                for (int i = 0; i < charges.Count; i++)
                {
                    if (charges[i] == null) continue;
                    float pulse = 0.72f + chargeElapsed / 0.85f * 0.38f
                        + Mathf.Sin(chargeElapsed * 20f + i) * 0.07f;
                    charges[i].transform.localScale = Vector3.one * pulse;
                    charges[i].transform.Rotate(0f, 0f, 115f * Time.deltaTime);
                }
                yield return null;
            }
            for (int i = 0; i < attackerCount; i++)
            {
                Transform attacker = GetAttackerRoot(GetAttackerId(packet, i));
                Vector2 origin = attacker != null ? (Vector2)attacker.position : GetAttackOrigin(packet, i);
                if (i < charges.Count && charges[i] != null) Destroy(charges[i]);
                for (int ray = 0; ray < rayCount; ray++)
                {
                    float angle = (ray * (360f / rayCount) + (packet.Seed % 23)) * Mathf.Deg2Rad;
                    SpawnSpikeProjectile(origin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 8.2f);
                }
            }
        }

        private void SpawnProjectile(Vector2 origin, Vector2 velocity, float radius, float life, bool missile)
        {
            GameObject projectile = new GameObject(missile ? "Bazooka Rocket" : "Cosmic Projectile");
            projectile.transform.SetParent(transform, false);
            projectile.transform.position = origin;
            if (missile)
            {
                StageGun.CreateSprite(projectile.transform, "Rocket Body", Vector2.zero,
                    new Vector2(0.72f, 0.25f), new Color(0.18f, 0.78f, 1f, 1f), 188);
                StageGun.CreateSprite(projectile.transform, "Rocket Tip", new Vector2(0.38f, 0f),
                    new Vector2(0.28f, 0.3f), new Color(1f, 0.22f, 0.72f, 1f), 189);
                StageGun.CreateSprite(projectile.transform, "Rocket Gold Band", new Vector2(0.12f, 0f),
                    new Vector2(0.13f, 0.32f), new Color(1f, 0.78f, 0.12f, 1f), 190);
                TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
                trail.time = 0.28f;
                trail.startWidth = 0.24f;
                trail.endWidth = 0.02f;
                trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                trail.colorGradient = CreateFinaleGradient(0.95f, 0f);
                trail.sortingOrder = 187;
                projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            }
            CosmicProjectile motion = projectile.AddComponent<CosmicProjectile>();
            motion.Configure(this, velocity, radius, life, false);
        }

        private void SpawnSpikeProjectile(Vector2 origin, Vector2 velocity)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            Color color = FinaleColor(Mathf.RoundToInt((angle + 360f) / 45f));
            GameObject projectile = CreateSpikeVisual(transform, origin, 1.35f, 188, color);
            projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0.01f;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.colorGradient = CreateFinaleGradient(0.72f, 0f);
            trail.sortingOrder = 187;
            CosmicProjectile motion = projectile.AddComponent<CosmicProjectile>();
            motion.Configure(this, velocity, 0.5f, 4f, false);
        }

        private void SpawnBomb(Vector2 origin, Vector2 target, float radius, float speed,
            float fuseSeconds, int attackerId)
        {
            // Standard StageBomb art, fuse, warning ring and explosion are used
            // here. Give its Rigidbody a real ballistic launch instead of the
            // previous constant-velocity translation.
            if (IsOnline && !HasAuthority) return;
            Vector2 delta = target - origin;
            float flightTime = Mathf.Clamp(Mathf.Abs(delta.x) / Mathf.Max(2.5f, speed), 0.9f, 1.65f);
            Vector2 velocity = new Vector2(
                delta.x / flightTime,
                (delta.y - 0.5f * Physics2D.gravity.y * flightTime * flightTime) / flightTime);
            string objectId = "15-3_boss_bomb_" + formIndex + "_" +
                (currentAttack != null ? currentAttack.Serial : attackSerial) + "_" + attackerId;
            float size = Mathf.Clamp(radius * 1.25f, 0.7f, 1.45f);
            GameObject bomb = syncManager != null
                ? syncManager.SpawnDropperBox(objectId, StageObjectType.Bomb, origin, size,
                    fuseSeconds: fuseSeconds, launchVelocity: velocity)
                : objectFactory != null
                    ? objectFactory.CreateDroppedBox(StageObjectType.Bomb, objectId, origin, size, transform, fuseSeconds)
                    : null;
            Rigidbody2D body = bomb != null ? bomb.GetComponent<Rigidbody2D>() : null;
            if (body != null)
            {
                body.linearVelocity = velocity;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        private GameObject CreateScratchWarning(Vector2 position)
        {
            GameObject root = new GameObject("Cat Paw Warning");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            Color padColor = new Color(0.96f, 0.16f, 0.62f, 0.5f);
            CreateCrayonStamp(root.transform, "Paw Pad", new Vector2(0f, -0.28f),
                new Vector2(1.18f, 0.86f), padColor, 186, 0f);
            Vector2[] toes =
            {
                new Vector2(-0.67f, 0.5f), new Vector2(-0.23f, 0.72f),
                new Vector2(0.25f, 0.72f), new Vector2(0.68f, 0.48f)
            };
            for (int i = 0; i < toes.Length; i++)
            {
                CreateCrayonStamp(root.transform, "Toe " + (i + 1), toes[i],
                    new Vector2(0.42f, 0.55f),
                    WithAlpha(FinaleColor(i + 1), 0.58f), 187, (i - 1.5f) * -8f);
            }
            return root;
        }

        private void CreateRainbowTargetMarker(Vector2 position, float duration)
        {
            GameObject root = new GameObject("Cosmic Rainbow Target");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                float radius = i % 2 == 0 ? 0.82f : 1.02f;
                CreateCrayonStamp(root.transform, "Rainbow Warning " + (i + 1),
                    new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                    new Vector2(0.24f, 0.14f), WithAlpha(FinaleColor(i), 0.72f), 186,
                    angle * Mathf.Rad2Deg);
            }
            CosmicMimicAuraMotion motion = root.AddComponent<CosmicMimicAuraMotion>();
            motion.Configure(currentAttack != null ? currentAttack.Seed : attackSerial, false, 190f);
            Destroy(root, Mathf.Max(0.2f, duration));
        }

        private GameObject CreateCatSlashVisual(Vector2 position)
        {
            GameObject root = new GameObject("Painted Cat Claw Swipe");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            for (int claw = -1; claw <= 1; claw++)
            {
                Color clawStart = FinaleColor(claw + 3);
                Color clawEnd = FinaleColor(claw + 5);
                for (int step = 0; step < 10; step++)
                {
                    float t = step / 9f;
                    float x = Mathf.Lerp(-1.28f, 1.28f, t);
                    float y = claw * 0.29f + Mathf.Lerp(-0.34f, 0.34f, t)
                        + Mathf.Sin(t * Mathf.PI) * 0.2f;
                    float taper = 0.55f + Mathf.Sin(t * Mathf.PI) * 0.45f;
                    Color color = Color.Lerp(clawStart, clawEnd, t);
                    color.a = Mathf.Lerp(0.82f, 0.98f, taper);
                    CreateCrayonStamp(root.transform, $"Claw {claw + 2}-{step + 1}",
                        new Vector2(x, y), new Vector2(0.48f * taper, 0.19f * taper),
                        color, 190 + claw + 1, 16f - Mathf.Cos(t * Mathf.PI) * 9f);
                }
            }
            return root;
        }

        private static GameObject CreateCatClawWeapon(
            Transform parent, Vector2 position, float size, int order)
        {
            GameObject root = new GameObject("Three Painted Claws");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            for (int claw = -1; claw <= 1; claw++)
            {
                Color baseColor = FinaleColor(claw + 2);
                Color tipColor = Color.Lerp(FinaleColor(claw + 4), Color.white, 0.35f);
                float spread = claw * 0.28f;
                for (int step = 0; step < 6; step++)
                {
                    float t = step / 5f;
                    CreateCrayonStamp(root.transform, $"Claw Blade {claw + 2}-{step + 1}",
                        new Vector2(size * (0.12f + t * 0.66f), spread * size + Mathf.Sin(t * Mathf.PI) * 0.11f),
                        new Vector2(size * Mathf.Lerp(0.3f, 0.12f, t), size * Mathf.Lerp(0.2f, 0.07f, t)),
                        Color.Lerp(baseColor, tipColor, t), order + step, claw * 7f);
                }
            }
            CreateCrayonStamp(root.transform, "Claw Cuff", new Vector2(-size * 0.05f, 0f),
                new Vector2(size * 0.48f, size * 0.72f), new Color(0.28f, 0.08f, 0.52f, 1f), order, 0f);
            AddFinaleTrail(root, order - 1, size * 0.32f);
            return root;
        }

        private static GameObject CreateBirdSpikeBallVisual(
            Transform parent, Vector2 position, float size, int order)
        {
            return CreateSpikeBallVisual(parent, position, size, order, 12,
                new Color(0.22f, 0.08f, 0.62f, 0.94f),
                new Color(0.24f, 0.88f, 1f, 1f),
                new Color(1f, 0.74f, 0.12f, 1f),
                new Color(1f, 0.28f, 0.78f, 0.72f));
        }

        private static GameObject CreateSlimeSpikeChargeVisual(
            Transform parent, Vector2 position, float size, int order)
        {
            return CreateSpikeBallVisual(parent, position, size, order, 8,
                new Color(0.32f, 0.66f, 0.16f, 0.9f),
                new Color(0.72f, 0.18f, 0.76f, 1f),
                new Color(0.98f, 0.58f, 0.08f, 1f),
                new Color(0.38f, 1f, 0.74f, 0.7f));
        }

        private static GameObject CreateSpikeBallVisual(
            Transform parent, Vector2 position, float size, int order, int spikeCount,
            Color coreColor, Color spikeColor, Color alternateSpikeColor, Color hatchColor)
        {
            GameObject root = new GameObject("Crayon Spike Ball");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            GameObject core = new GameObject("Scribbled Core");
            core.transform.SetParent(root.transform, false);
            SpriteRenderer coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            coreRenderer.color = coreColor;
            coreRenderer.sortingOrder = order;
            core.transform.localScale = Vector3.one * size * 0.72f;

            for (int i = 0; i < spikeCount; i++)
            {
                float angle = i * 360f / spikeCount + (i % 2 == 0 ? -2.5f : 2f);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Color thornColor = Color.Lerp(spikeColor, alternateSpikeColor,
                    i / (float)Mathf.Max(1, spikeCount - 1));
                GameObject spike = CreateSpikeVisual(root.transform,
                    direction * size * 0.42f, size * 0.62f, order + 2, thornColor);
                spike.name = "Pencil Thorn " + (i + 1);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }

            StageEscortController.AddLine(root.transform,
                new Vector2(-size * 0.3f, -size * 0.22f),
                new Vector2(size * 0.28f, size * 0.25f), 0.045f, hatchColor, order + 3);
            StageEscortController.AddLine(root.transform,
                new Vector2(-size * 0.26f, size * 0.28f),
                new Vector2(size * 0.3f, -size * 0.18f), 0.045f, hatchColor, order + 3);
            AddFinaleTrail(root, order - 1, size * 0.28f);
            return root;
        }

        private static void AddFinaleTrail(GameObject root, int order, float width)
        {
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = width;
            trail.endWidth = 0.01f;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.colorGradient = CreateFinaleGradient(0.68f, 0f);
            trail.sortingOrder = order;
        }

        private static Gradient CreateFinaleGradient(float startAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.2f, 0.9f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.24f, 0.76f), 0.34f),
                    new GradientColorKey(new Color(1f, 0.82f, 0.12f), 0.67f),
                    new GradientColorKey(new Color(0.45f, 1f, 0.36f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(startAlpha * 0.75f, 0.55f),
                    new GradientAlphaKey(endAlpha, 1f)
                });
            return gradient;
        }

        private static Color FinaleColor(int index)
        {
            return Color.HSVToRGB(Mathf.Repeat(0.08f + index * 0.173f, 1f), 0.8f, 1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static GameObject CreateCrayonStamp(
            Transform parent, string name, Vector2 position, Vector2 size,
            Color color, int order, float rotation)
        {
            GameObject stamp = new GameObject(name);
            stamp.transform.SetParent(parent, false);
            stamp.transform.localPosition = position;
            stamp.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            stamp.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = stamp.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return stamp;
        }

        private static GameObject CreateSpikeVisual(Transform parent, Vector2 position, float size, int order)
        {
            return CreateSpikeVisual(parent, position, size, order, new Color(0.88f, 0.06f, 0.1f));
        }

        private static GameObject CreateSpikeVisual(
            Transform parent, Vector2 position, float size, int order, Color color)
        {
            GameObject root = new GameObject("Red Pencil Spike");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startWidth = line.endWidth = 0.09f;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.SetPosition(0, new Vector3(-size * 0.45f, -size * 0.45f));
            line.SetPosition(1, new Vector3(0f, size * 0.55f));
            line.SetPosition(2, new Vector3(size * 0.45f, -size * 0.45f));
            return root;
        }

        private void SpawnTimedClone()
        {
            if (bossVisual == null) return;
            float side = nextCloneId % 2 == 0 ? -1f : 1f;
            Vector2 position = (Vector2)bossRoot.position + new Vector2(side * (2.1f + nextCloneId * 0.25f), 0.8f);
            position.x = Mathf.Clamp(position.x, -ArenaHalfWidth + 1.3f, ArenaHalfWidth - 1.3f);
            position.y = Mathf.Clamp(position.y, ArenaBottom + 1.4f, ArenaTop - 1.5f);
            SpawnMimicClone(++nextCloneId, position);
            GameSfx.Play(SfxId.EmotePop);
        }

        private MimicClone SpawnMimicClone(int id, Vector2 position)
        {
            if (bossVisual == null) return null;
            GameObject root = new GameObject("Mimic Clone " + id);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            Transform visual = Instantiate(bossVisual.gameObject, root.transform).transform;
            visual.name = "GeneratedBody";
            visual.localPosition = bossVisual.localPosition;
            visual.localRotation = bossVisual.localRotation;
            visual.localScale = Vector3.one;
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            ApplyCosmicCharacterLook(root.transform, visual, formIndex * 41 + id * 17 + 11, true);
            MimicClone clone = new MimicClone
            {
                Id = id,
                Root = root.transform,
                Visual = visual,
                NextDecisionAt = Time.time + UnityEngine.Random.Range(0.2f, 0.8f),
                NextJumpAt = Time.time + UnityEngine.Random.Range(0.7f, 2.2f),
                NextAttackAt = HasAuthority
                    ? Time.time + UnityEngine.Random.Range(0.75f, 1.65f)
                    : float.PositiveInfinity,
                AttackAlternator = UnityEngine.Random.Range(0, 2)
            };
            mimicClones.Add(clone);
            return clone;
        }

        private void UpdateMimicClones(bool authoritative)
        {
            for (int i = 0; i < mimicClones.Count; i++)
            {
                MimicClone clone = mimicClones[i];
                if (clone.Root == null) continue;
                if (Time.time < clone.LockedUntil)
                {
                    clone.Velocity = Vector2.zero;
                    HitPlayersNear(clone.Root.position, bossContactRadius);
                    continue;
                }
                if (authoritative)
                {
                    PlayerController2D target = FindNearestLivingPlayer(clone.Root.position);
                    if (Time.time >= clone.NextDecisionAt)
                    {
                        if (target == null || UnityEngine.Random.value < 0.28f)
                        {
                            clone.MoveDirection = 0f;
                            clone.MoveSpeed = 0f;
                            clone.NextDecisionAt = Time.time + UnityEngine.Random.Range(0.4f, 1.25f);
                        }
                        else
                        {
                            float toward = target.transform.position.x < clone.Root.position.x ? -1f : 1f;
                            clone.MoveDirection = UnityEngine.Random.value < 0.76f ? toward : -toward;
                            float speciesSpeed = bossSpecies == DrawManager.Species.Cat ? 1.12f
                                : bossSpecies == DrawManager.Species.Turtle ? 0.74f : 1f;
                            clone.MoveSpeed = UnityEngine.Random.Range(1.8f, 3.65f) * speciesSpeed;
                            clone.NextDecisionAt = Time.time + UnityEngine.Random.Range(0.65f, 1.7f);
                        }
                    }
                    if (Time.time >= clone.NextJumpAt)
                    {
                        clone.Velocity.y = Mathf.Max(clone.Velocity.y, UnityEngine.Random.Range(3.3f, 4.5f));
                        clone.NextJumpAt = Time.time + UnityEngine.Random.Range(1.2f, 2.85f);
                    }
                    clone.Velocity.x = Mathf.MoveTowards(clone.Velocity.x,
                        clone.MoveDirection * clone.MoveSpeed, 7.2f * Time.deltaTime);
                    clone.Velocity.y += Physics2D.gravity.y
                        * (bossSpecies == DrawManager.Species.Bird ? 0.1f : 0.18f) * Time.deltaTime;
                    clone.Velocity.y = Mathf.Max(clone.Velocity.y, -3.2f);
                }

                Vector2 next = (Vector2)clone.Root.position + clone.Velocity * Time.deltaTime;
                float minX = -ArenaHalfWidth + 1.3f;
                float maxX = ArenaHalfWidth - 1.3f;
                float minY = ArenaBottom + 1.4f;
                float maxY = ArenaTop - 1.5f;
                if (next.x <= minX || next.x >= maxX)
                {
                    next.x = Mathf.Clamp(next.x, minX, maxX);
                    clone.MoveDirection *= -1f;
                    clone.Velocity.x *= -0.35f;
                }
                if (next.y <= minY) { next.y = minY; clone.Velocity.y = 0f; }
                else if (next.y >= maxY) { next.y = maxY; clone.Velocity.y = Mathf.Min(0f, clone.Velocity.y); }
                clone.Root.position = next;
                if (Mathf.Abs(clone.Velocity.x) > 0.15f) clone.Facing = clone.Velocity.x < 0f ? -1 : 1;
                if (clone.Visual != null) clone.Visual.localScale = new Vector3(clone.Facing, 1f, 1f);
                HitPlayersNear(clone.Root.position, bossContactRadius);
            }
        }

        private CloneSnapshot[] CaptureCloneSnapshots()
        {
            List<CloneSnapshot> snapshots = new List<CloneSnapshot>(mimicClones.Count);
            for (int i = 0; i < mimicClones.Count; i++)
            {
                MimicClone clone = mimicClones[i];
                if (clone.Root == null) continue;
                snapshots.Add(new CloneSnapshot
                {
                    Id = clone.Id,
                    Position = clone.Root.position,
                    Velocity = clone.Velocity,
                    Facing = clone.Facing
                });
            }
            return snapshots.ToArray();
        }

        private void ApplyCloneSnapshots(CloneSnapshot[] snapshots)
        {
            HashSet<int> incoming = new HashSet<int>();
            if (snapshots != null)
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    CloneSnapshot state = snapshots[i];
                    incoming.Add(state.Id);
                    MimicClone clone = mimicClones.Find(item => item.Id == state.Id);
                    if (clone == null) clone = SpawnMimicClone(state.Id, state.Position);
                    if (clone == null || clone.Root == null) continue;
                    clone.Root.position = Vector2.Lerp(clone.Root.position, state.Position, 0.68f);
                    clone.Velocity = state.Velocity;
                    clone.Facing = state.Facing == 0 ? 1 : state.Facing;
                    if (clone.Visual != null) clone.Visual.localScale = new Vector3(clone.Facing, 1f, 1f);
                }
            }
            for (int i = mimicClones.Count - 1; i >= 0; i--)
            {
                if (incoming.Contains(mimicClones[i].Id)) continue;
                if (mimicClones[i].Root != null) Destroy(mimicClones[i].Root.gameObject);
                mimicClones.RemoveAt(i);
            }
        }

        private void SetBossShelled(bool value)
        {
            bossShelled = value;
            if (shellOverlay != null) shellOverlay.gameObject.SetActive(value);
            if (bossVisual != null) bossVisual.localScale = value
                ? new Vector3(bossFacing * 0.72f, 0.72f, 1f)
                : new Vector3(bossFacing, 1f, 1f);
        }

        private static void GetLaserGeometry(int side, out Vector2 center, out Vector2 size,
            out Vector2 lineStart, out Vector2 lineEnd)
        {
            if (side == 0 || side == 1)
            {
                center = new Vector2(side == 0 ? -ArenaHalfWidth * 0.5f : ArenaHalfWidth * 0.5f, -0.25f);
                size = new Vector2(ArenaHalfWidth, ArenaTop - ArenaBottom);
                lineStart = new Vector2(center.x, ArenaBottom);
                lineEnd = new Vector2(center.x, ArenaTop);
            }
            else
            {
                center = new Vector2(0f, side == 2 ? (ArenaTop - 4.2f) : (ArenaBottom + 4.2f));
                size = new Vector2(ArenaHalfWidth * 2f, (ArenaTop - ArenaBottom) * 0.5f);
                lineStart = new Vector2(-ArenaHalfWidth, center.y);
                lineEnd = new Vector2(ArenaHalfWidth, center.y);
            }
        }

        private GameObject CreateWarningLine(Vector2 start, Vector2 end)
        {
            GameObject warning = new GameObject("Red Attack Prediction");
            warning.transform.SetParent(transform, false);
            LineRenderer line = warning.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startWidth = line.endWidth = 0.12f;
            line.startColor = line.endColor = new Color(1f, 0.04f, 0.08f, 0.78f);
            line.sortingOrder = 181;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            return warning;
        }

        private GameObject CreateBazookaAimGuide(Vector2 origin, Vector2 direction, Vector2 target)
        {
            GameObject guide = new GameObject("Bazooka Dotted Aim Guide");
            guide.transform.SetParent(transform, false);
            const int dashCount = 16;
            const float step = 1.05f;
            for (int i = 0; i < dashCount; i++)
            {
                float from = 0.9f + i * step;
                float to = from + 0.52f;
                float alpha = Mathf.Lerp(0.78f, 0.2f, i / (float)(dashCount - 1));
                StageEscortController.AddLine(guide.transform,
                    origin + direction * from,
                    origin + direction * to,
                    0.055f,
                    new Color(1f, 0.16f, 0.12f, alpha),
                    181);
            }

            GameObject sight = new GameObject("Aim Point");
            sight.transform.SetParent(guide.transform, false);
            sight.transform.position = target;
            LineRenderer ring = sight.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 24;
            ring.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            ring.startWidth = ring.endWidth = 0.065f;
            ring.startColor = ring.endColor = new Color(1f, 0.2f, 0.12f, 0.78f);
            ring.sortingOrder = 182;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                ring.SetPosition(i, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.42f);
            }
            StageEscortController.AddLine(sight.transform, new Vector2(-0.62f, 0f),
                new Vector2(0.62f, 0f), 0.045f, new Color(1f, 0.2f, 0.12f, 0.68f), 182);
            StageEscortController.AddLine(sight.transform, new Vector2(0f, -0.62f),
                new Vector2(0f, 0.62f), 0.045f, new Color(1f, 0.2f, 0.12f, 0.68f), 182);
            return guide;
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || state == BattleState.Cleared || state == BattleState.Failed) return;
            string id = ResolvePlayerId(target);
            if (IsOnline && !HasAuthority)
            {
                Send(HitKind, new HitRequest { PlayerId = id });
                return;
            }
            Eliminate(id, target);
        }

        private void Eliminate(string id, PlayerController2D player)
        {
            if (player == null || string.IsNullOrEmpty(id) || !eliminatedPlayers.Add(id)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            player.SetControlsEnabled(false);
            SetPlayerVisible(player, false);
            GameSfx.Play(SfxId.PlayerDeath);
            BroadcastState(true);
        }

        private bool AreAllPlayersEliminated()
        {
            int present = 0;
            for (int i = 0; i < orderedPlayerIds.Count; i++)
            {
                if (ResolvePlayer(orderedPlayerIds[i]) == null) continue;
                present++;
                if (!eliminatedPlayers.Contains(orderedPlayerIds[i])) return false;
            }
            return present > 0;
        }

        internal void HitPlayersNear(Vector2 point, float radius)
        {
            if (!IsStageActive) return;
            if (IsOnline && !HasAuthority)
            {
                IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerController2D localPlayer = localPlayers[i];
                    if (localPlayer != null && !eliminatedPlayers.Contains(ResolvePlayerId(localPlayer))
                        && Vector2.Distance(localPlayer.transform.position, point) <= radius)
                        RequestElimination(localPlayer);
                }
                return;
            }
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (!eliminatedPlayers.Contains(ResolvePlayerId(players[i]))
                    && Vector2.Distance(players[i].transform.position, point) <= radius)
                    RequestElimination(players[i]);
        }

        internal void HitPlayersInBox(Vector2 center, Vector2 size)
        {
            if (IsOnline && !HasAuthority)
            {
                Rect localArea = new Rect(center - size * 0.5f, size);
                IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerController2D localPlayer = localPlayers[i];
                    if (localPlayer != null && localArea.Contains(localPlayer.transform.position))
                        RequestElimination(localPlayer);
                }
                return;
            }
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Rect area = new Rect(center - size * 0.5f, size);
            for (int i = 0; i < players.Length; i++)
                if (area.Contains(players[i].transform.position)) RequestElimination(players[i]);
        }

        private PlayerController2D FindNearestLivingPlayer(Vector2 position)
        {
            PlayerController2D best = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < orderedPlayerIds.Count; i++)
            {
                if (eliminatedPlayers.Contains(orderedPlayerIds[i])) continue;
                PlayerController2D player = ResolvePlayer(orderedPlayerIds[i]);
                if (player == null) continue;
                float candidate = ((Vector2)player.transform.position - position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; best = player; }
            }
            return best;
        }

        private void SetPlayerVisible(PlayerController2D player, bool visible)
        {
            if (player == null) return;
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            if (!visible)
            {
                if (!hiddenRenderers.TryGetValue(player, out Dictionary<Renderer, bool> rendererStates))
                {
                    rendererStates = new Dictionary<Renderer, bool>();
                    hiddenRenderers[player] = rendererStates;
                }
                for (int i = 0; i < renderers.Length; i++)
                    if (!rendererStates.ContainsKey(renderers[i])) rendererStates[renderers[i]] = renderers[i].enabled;
                if (!hiddenColliders.TryGetValue(player, out Dictionary<Collider2D, bool> colliderStates))
                {
                    colliderStates = new Dictionary<Collider2D, bool>();
                    hiddenColliders[player] = colliderStates;
                }
                for (int i = 0; i < colliders.Length; i++)
                    if (!colliderStates.ContainsKey(colliders[i])) colliderStates[colliders[i]] = colliders[i].enabled;
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
                return;
            }
            if (hiddenRenderers.TryGetValue(player, out Dictionary<Renderer, bool> savedRendererStates))
                foreach (KeyValuePair<Renderer, bool> pair in savedRendererStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            if (hiddenColliders.TryGetValue(player, out Dictionary<Collider2D, bool> savedColliderStates))
                foreach (KeyValuePair<Collider2D, bool> pair in savedColliderStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenRenderers.Remove(player);
            hiddenColliders.Remove(player);
        }

        private void BroadcastState(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || (!force && Time.unscaledTime < nextBroadcastAt)) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            Send(StateKind, new BattleSnapshot
            {
                Sequence = ++stateSequence,
                State = (int)state,
                FormIndex = formIndex,
                FormCount = orderedPlayerIds.Count,
                Remaining = remaining,
                CurrentHp = currentHp,
                MaximumHp = maximumHp,
                OrderedPlayerIds = orderedPlayerIds.ToArray(),
                EliminatedPlayerIds = new List<string>(eliminatedPlayers).ToArray(),
                BossPosition = bossRoot != null ? (Vector2)bossRoot.position : Vector2.zero,
                BossVelocity = bossVelocity,
                BossFacing = bossFacing,
                BossShelled = bossShelled,
                Clones = CaptureCloneSnapshots(),
                Attack = currentAttack
            });
        }

        private void ApplySnapshot(BattleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Sequence <= lastStateSequence) return;
            lastStateSequence = snapshot.Sequence;
            BattleState previous = state;
            int previousForm = formIndex;
            state = (BattleState)Mathf.Clamp(snapshot.State, 0, (int)BattleState.Failed);
            formIndex = Mathf.Max(0, snapshot.FormIndex);
            remaining = snapshot.Remaining;
            currentHp = snapshot.CurrentHp;
            maximumHp = snapshot.MaximumHp;
            if (snapshot.OrderedPlayerIds != null)
            {
                orderedPlayerIds.Clear();
                orderedPlayerIds.AddRange(snapshot.OrderedPlayerIds);
            }
            if (state == BattleState.Active && (bossRoot == null || previousForm != formIndex || previous != BattleState.Active))
            {
                DestroyBoss();
                SpawnBossForCurrentForm();
            }
            if (bossRoot != null)
            {
                bossRoot.position = Vector2.Lerp(bossRoot.position, snapshot.BossPosition, 0.62f);
                bossVelocity = snapshot.BossVelocity;
                bossFacing = snapshot.BossFacing == 0 ? 1 : snapshot.BossFacing;
                SetBossShelled(snapshot.BossShelled);
            }
            ApplyCloneSnapshots(snapshot.Clones);
            ApplyEliminated(snapshot.EliminatedPlayerIds);
            if (snapshot.Attack != null && snapshot.Attack.Serial > lastPlayedAttackSerial)
            {
                lastPlayedAttackSerial = snapshot.Attack.Serial;
                currentAttack = snapshot.Attack;
                PlayAttack(snapshot.Attack);
            }
        }

        private void ApplyEliminated(string[] ids)
        {
            HashSet<string> incoming = ids != null ? new HashSet<string>(ids) : new HashSet<string>();
            for (int i = 0; i < orderedPlayerIds.Count; i++)
            {
                string id = orderedPlayerIds[i];
                PlayerController2D player = ResolvePlayer(id);
                if (player == null) continue;
                bool dead = incoming.Contains(id);
                if (dead)
                {
                    eliminatedPlayers.Add(id);
                    Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                    if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
                    player.SetControlsEnabled(false);
                    SetPlayerVisible(player, false);
                }
                else if (eliminatedPlayers.Remove(id))
                {
                    Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                    if (body != null) body.simulated = true;
                    SetPlayerVisible(player, true);
                }
            }
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (!IsStageActive || data == null || data.ObjectId != StageId) return;
            if (data.Kind == StateKind && !HasAuthority
                && onlineManager != null && onlineManager.IsHostPlayer(data.PlayerId))
                ApplySnapshot(JsonUtility.FromJson<BattleSnapshot>(data.Json));
            else if (data.Kind == HitKind && HasAuthority)
            {
                HitRequest request = JsonUtility.FromJson<HitRequest>(data.Json);
                if (request != null && request.PlayerId == data.PlayerId)
                    Eliminate(request.PlayerId, ResolvePlayer(request.PlayerId));
            }
        }

        private void Send(string kind, object value)
        {
            onlineManager?.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = kind,
                Json = JsonUtility.ToJson(value)
            });
        }

        private void BuildSpaceArena()
        {
            GameObject backdrop = new GameObject("Cosmic Doodle Backdrop");
            backdrop.transform.SetParent(transform, false);
            Color[] colors = { new Color(0.42f, 0.75f, 1f, 0.58f), new Color(1f, 0.86f, 0.32f, 0.62f), new Color(0.9f, 0.54f, 1f, 0.52f) };
            for (int i = 0; i < 54; i++)
            {
                float x = Mathf.Lerp(-18f, 18f, ((i * 37) % 53) / 52f);
                float y = Mathf.Lerp(-8f, 8f, ((i * 19 + 7) % 53) / 52f);
                GameObject star = new GameObject("Pencil Star " + i);
                star.transform.SetParent(backdrop.transform, false);
                star.transform.localPosition = new Vector3(x, y, 1.5f);
                SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = colors[i % colors.Length];
                renderer.sortingOrder = -80;
                float size = 0.06f + (i % 4) * 0.035f;
                star.transform.localScale = new Vector3(size, size, 1f);
            }
            AddPlanet(backdrop.transform, new Vector2(-13.8f, 5.7f), 2.2f, new Color(0.38f, 0.72f, 0.96f, 0.34f));
            AddPlanet(backdrop.transform, new Vector2(14.2f, -5.3f), 2.8f, new Color(0.9f, 0.45f, 0.62f, 0.28f));
        }

        private void BuildGravityFieldEffect()
        {
            GameObject field = new GameObject("Zero Gravity Field Effect");
            field.transform.SetParent(transform, false);
            Color[] colors =
            {
                new Color(0.35f, 0.9f, 1f, 0.34f),
                new Color(0.72f, 0.48f, 1f, 0.28f),
                new Color(1f, 0.82f, 0.32f, 0.26f)
            };
            for (int i = 0; i < 42; i++)
            {
                float x = Mathf.Lerp(-17.5f, 17.5f, ((i * 29 + 5) % 43) / 42f);
                float y = Mathf.Lerp(ArenaBottom + 0.7f, ArenaTop - 0.7f, ((i * 17 + 11) % 43) / 42f);
                GameObject mote = new GameObject("Floating Gravity Curl " + i);
                mote.transform.SetParent(field.transform, false);
                mote.transform.localPosition = new Vector3(x, y, 0.75f);
                float size = 0.32f + (i % 5) * 0.09f;
                Color color = colors[i % colors.Length];
                LineRenderer curl = mote.AddComponent<LineRenderer>();
                curl.useWorldSpace = false;
                curl.positionCount = 5;
                curl.numCapVertices = 4;
                curl.numCornerVertices = 3;
                curl.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                curl.startWidth = size * 0.12f;
                curl.endWidth = size * 0.045f;
                curl.startColor = curl.endColor = color;
                curl.sortingOrder = -34;
                curl.SetPosition(0, new Vector2(-size * 0.55f, -size * 0.2f));
                curl.SetPosition(1, new Vector2(-size * 0.15f, size * 0.08f));
                curl.SetPosition(2, new Vector2(size * 0.1f, size * 0.42f));
                curl.SetPosition(3, new Vector2(size * 0.42f, size * 0.2f));
                curl.SetPosition(4, new Vector2(size * 0.25f, size * 0.62f));
                CosmicGravityMote drift = mote.AddComponent<CosmicGravityMote>();
                drift.Configure(i, ArenaHalfWidth - 0.5f, ArenaBottom + 0.4f, ArenaTop - 0.4f);
            }

            // Broad translucent waves make the altered physics readable even
            // when the player happens to be between the smaller floating curls.
            for (int row = 0; row < 6; row++)
            {
                GameObject wave = new GameObject("Zero Gravity Current " + row);
                wave.transform.SetParent(field.transform, false);
                wave.transform.localPosition = new Vector3(0f, Mathf.Lerp(ArenaBottom + 1.2f, ArenaTop - 1.2f, row / 5f), 0.9f);
                LineRenderer line = wave.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 13;
                line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                line.startWidth = line.endWidth = 0.035f;
                line.startColor = line.endColor = new Color(0.35f, 0.82f, 1f, 0.12f);
                line.sortingOrder = -36;
                for (int p = 0; p < 13; p++)
                {
                    float x = Mathf.Lerp(-ArenaHalfWidth, ArenaHalfWidth, p / 12f);
                    line.SetPosition(p, new Vector2(x, Mathf.Sin(p * 1.35f + row) * 0.28f));
                }
                CosmicGravityMote drift = wave.AddComponent<CosmicGravityMote>();
                drift.Configure(100 + row, ArenaHalfWidth, ArenaBottom, ArenaTop, true);
            }
        }

        private static void AddPlanet(Transform parent, Vector2 position, float size, Color color)
        {
            GameObject planet = new GameObject("Crayon Planet");
            planet.transform.SetParent(parent, false);
            planet.transform.localPosition = new Vector3(position.x, position.y, 1f);
            SpriteRenderer renderer = planet.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = -82;
            planet.transform.localScale = Vector3.one * size;
            StageEscortController.AddLine(planet.transform, new Vector2(-size * 0.7f, -0.15f),
                new Vector2(size * 0.7f, 0.15f), 0.1f, new Color(color.r, color.g, color.b, 0.58f), -81);
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("15-3 Time Limit Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(0f, 9.15f, -0.5f);
            monitorRoot = monitor.transform;
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(15f, 2.35f), 218);
            GameObject segments = new GameObject("Character Time Segments");
            segments.transform.SetParent(monitor.transform, false);
            segments.transform.localPosition = new Vector3(0f, -0.1f, -0.05f);
            timeSegmentRoot = segments.transform;
            RebuildTimeSegments();
        }

        private void RefreshMonitor()
        {
            if (timeSegmentRoot == null) return;
            int expected = Mathf.Max(1, orderedPlayerIds.Count);
            if (timeSegmentFills.Count != expected) RebuildTimeSegments();
            for (int i = 0; i < timeSegmentFills.Count; i++)
            {
                Transform fill = timeSegmentFills[i];
                if (fill == null) continue;
                float ratio;
                if (i < formIndex || state == BattleState.Cleared) ratio = 0f;
                else if (i > formIndex || state == BattleState.Intro) ratio = 1f;
                else if (state == BattleState.Intermission) ratio = 0f;
                else ratio = Mathf.Clamp01(remaining / FormDuration);
                float width = timeSegmentWidths[i];
                fill.localScale = new Vector3(width * ratio, 0.58f, 1f);
                float segmentStart = GetSegmentStart(i, timeSegmentFills.Count);
                fill.localPosition = new Vector3(segmentStart + width * ratio * 0.5f, 0f, -0.03f);
            }
        }

        private void RebuildTimeSegments()
        {
            if (timeSegmentRoot == null) return;
            for (int i = timeSegmentRoot.childCount - 1; i >= 0; i--)
                Destroy(timeSegmentRoot.GetChild(i).gameObject);
            timeSegmentFills.Clear();
            timeSegmentWidths.Clear();
            int count = Mathf.Max(1, orderedPlayerIds.Count);
            const float totalWidth = 12.8f;
            const float gap = 0.16f;
            float width = (totalWidth - gap * (count - 1)) / count;
            Color[] colors =
            {
                new Color(0.2f, 0.78f, 0.96f),
                new Color(1f, 0.48f, 0.42f),
                new Color(1f, 0.78f, 0.2f),
                new Color(0.36f, 0.82f, 0.46f),
                new Color(0.76f, 0.42f, 0.94f)
            };
            for (int i = 0; i < count; i++)
            {
                float start = GetSegmentStart(i, count);
                StageEscortController.AddFilledRect(timeSegmentRoot, "Time Track " + (i + 1),
                    new Vector2(start + width * 0.5f, 0f), new Vector2(width, 0.72f),
                    new Color(0.18f, 0.25f, 0.3f, 0.72f), 222);
                GameObject fill = new GameObject("Character Time " + (i + 1));
                fill.transform.SetParent(timeSegmentRoot, false);
                fill.transform.localPosition = new Vector3(start + width * 0.5f, 0f, -0.03f);
                fill.transform.localScale = new Vector3(width, 0.58f, 1f);
                SpriteRenderer renderer = fill.AddComponent<SpriteRenderer>();
                renderer.sprite = StageLinkedShieldSurvivalController.GetSquareSprite();
                DrawManager.Species species = i < orderedPlayerIds.Count
                    ? GetSpecies(ResolvePlayer(orderedPlayerIds[i]))
                    : DrawManager.Species.Human;
                renderer.color = colors[Mathf.Clamp((int)species, 0, colors.Length - 1)];
                renderer.sortingOrder = 224;
                timeSegmentFills.Add(fill.transform);
                timeSegmentWidths.Add(width);
            }
        }

        private static float GetSegmentStart(int index, int count)
        {
            const float totalWidth = 12.8f;
            const float gap = 0.16f;
            float width = (totalWidth - gap * (count - 1)) / Mathf.Max(1, count);
            return -totalWidth * 0.5f + index * (width + gap);
        }

        private void LockCamera()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            if (!cameraCaptured)
            {
                cameraCaptured = true;
                previousCameraPosition = gameCamera.transform.position;
                previousCameraSize = gameCamera.orthographicSize;
                previousFollowEnabled = cameraFollow != null && cameraFollow.enabled;
            }
            MaintainCamera();
        }

        private void MaintainCamera()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            if (cameraFollow == null) cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            if (cameraFollow != null) cameraFollow.enabled = false;
            gameCamera.transform.position = new Vector3(0f, 0.4f, gameCamera.transform.position.z);
            gameCamera.orthographicSize = Mathf.Max(11.2f, 20.8f / Mathf.Max(0.1f, gameCamera.aspect));
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousFollowEnabled;
            cameraCaptured = false;
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private static DrawManager.Species GetSpecies(PlayerController2D player)
        {
            if (player == null) return DrawManager.Species.Human;
            BodyBuilder builder = player.GetComponent<BodyBuilder>();
            if (builder != null) return builder.BuiltSpecies;
            PlayerAbilityController ability = player.GetComponent<PlayerAbilityController>();
            return ability != null ? ability.CurrentProfile.Species : DrawManager.Species.Human;
        }

    }

    internal sealed class CosmicMimicRainbowTint : MonoBehaviour
    {
        private SpriteRenderer[] sprites;
        private LineRenderer[] lines;
        private float[] spriteAlpha;
        private float[] lineStartAlpha;
        private float[] lineEndAlpha;
        private float phase;
        private float intensity = 1f;
        private float nextRefreshAt;

        internal void Configure(int seed, float strength)
        {
            phase = Mathf.Repeat(seed * 0.071f, 1f);
            intensity = Mathf.Clamp01(strength);
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            lines = GetComponentsInChildren<LineRenderer>(true);
            spriteAlpha = new float[sprites.Length];
            lineStartAlpha = new float[lines.Length];
            lineEndAlpha = new float[lines.Length];
            for (int i = 0; i < sprites.Length; i++)
                spriteAlpha[i] = sprites[i] != null ? sprites[i].color.a : 1f;
            for (int i = 0; i < lines.Length; i++)
            {
                lineStartAlpha[i] = lines[i] != null ? lines[i].startColor.a : 1f;
                lineEndAlpha[i] = lines[i] != null ? lines[i].endColor.a : 1f;
            }
            ApplyColors();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshAt) return;
            nextRefreshAt = Time.unscaledTime + 0.08f;
            ApplyColors();
        }

        private void ApplyColors()
        {
            float clock = Time.unscaledTime * 0.055f + phase;
            for (int i = 0; sprites != null && i < sprites.Length; i++)
            {
                SpriteRenderer renderer = sprites[i];
                if (renderer == null) continue;
                Color rainbow = Color.HSVToRGB(Mathf.Repeat(clock + i * 0.137f, 1f), 0.78f, 1f);
                rainbow = Color.Lerp(Color.white, rainbow, intensity);
                rainbow.a = spriteAlpha[i];
                renderer.color = rainbow;
            }
            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                LineRenderer renderer = lines[i];
                if (renderer == null) continue;
                Color start = Color.HSVToRGB(Mathf.Repeat(clock + i * 0.113f, 1f), 0.86f, 1f);
                Color end = Color.HSVToRGB(Mathf.Repeat(clock + i * 0.113f + 0.2f, 1f), 0.86f, 1f);
                start.a = lineStartAlpha[i];
                end.a = lineEndAlpha[i];
                renderer.startColor = start;
                renderer.endColor = end;
            }
        }
    }

    internal sealed class CosmicMimicAuraMotion : MonoBehaviour
    {
        private float phase;
        private float spinSpeed;
        private float pulseAmount;

        internal void Configure(int seed, bool clone, float spin = 42f)
        {
            phase = Mathf.Repeat(seed * 0.037f, 1f) * Mathf.PI * 2f;
            spinSpeed = (seed & 1) == 0 ? spin : -spin;
            pulseAmount = clone ? 0.07f : 0.12f;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.6f + phase) * pulseAmount;
            transform.localScale = Vector3.one * pulse;
        }
    }

    internal sealed class CosmicProjectile : MonoBehaviour
    {
        private StageCosmicMimicBossController owner;
        private Vector2 velocity;
        private float radius;
        private float expiresAt;
        private bool bomb;

        internal void Configure(StageCosmicMimicBossController controller, Vector2 speed,
            float hitRadius, float lifetime, bool explode)
        {
            owner = controller;
            velocity = speed;
            radius = hitRadius;
            expiresAt = Time.time + lifetime;
            bomb = explode;
        }

        private void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            velocity = Vector2.Lerp(velocity, velocity * 0.97f, Time.deltaTime * 1.8f);
            owner?.HitPlayersNear(transform.position, radius);
            if (Time.time < expiresAt) return;
            if (bomb)
            {
                owner?.HitPlayersNear(transform.position, radius * 2.35f);
                GameSfx.PlayAt(SfxId.BombExplosion, transform.position);
                GameObject burst = new GameObject("Crayon Bomb Burst");
                burst.transform.position = transform.position;
                SpriteRenderer renderer = burst.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(1f, 0.45f, 0.08f, 0.7f);
                renderer.sortingOrder = 195;
                burst.transform.localScale = Vector3.one * radius * 3.4f;
                Destroy(burst, 0.28f);
            }
            Destroy(gameObject);
        }
    }

    internal sealed class CosmicHazard : MonoBehaviour
    {
        private StageCosmicMimicBossController owner;
        private float radius;
        private float expiresAt;

        internal void Configure(StageCosmicMimicBossController controller, float hitRadius, float duration)
        {
            owner = controller;
            radius = hitRadius;
            expiresAt = Time.time + duration;
        }

        private void Update()
        {
            owner?.HitPlayersNear(transform.position, radius);
            if (Time.time >= expiresAt) Destroy(gameObject);
        }

        internal static void HitPlayersNear(StageCosmicMimicBossController controller, Vector2 point, float radius)
            => controller?.HitPlayersNear(point, radius);
    }

    internal sealed class CosmicBoxHazard : MonoBehaviour
    {
        private StageCosmicMimicBossController owner;
        private Vector2 center;
        private Vector2 size;
        private float expiresAt;

        internal void Configure(StageCosmicMimicBossController controller, Vector2 areaCenter,
            Vector2 areaSize, float duration)
        {
            owner = controller;
            center = areaCenter;
            size = areaSize;
            expiresAt = Time.time + duration;
        }

        private void Update()
        {
            owner?.HitPlayersInBox(center, size);
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }

    internal sealed class CosmicGravityMote : MonoBehaviour
    {
        private float phase;
        private float halfWidth;
        private float bottom;
        private float top;
        private float riseSpeed;
        private bool broadWave;

        internal void Configure(int seed, float horizontalLimit, float lower, float upper,
            bool isBroadWave = false)
        {
            phase = seed * 0.731f;
            halfWidth = horizontalLimit;
            bottom = lower;
            top = upper;
            broadWave = isBroadWave;
            riseSpeed = isBroadWave ? 0.08f + seed % 3 * 0.025f : 0.22f + seed % 5 * 0.045f;
        }

        private void Update()
        {
            Vector3 position = transform.localPosition;
            position.y += riseSpeed * Time.deltaTime;
            position.x += Mathf.Sin(Time.time * (broadWave ? 0.22f : 0.58f) + phase)
                * (broadWave ? 0.018f : 0.055f) * Time.deltaTime;
            if (position.y > top) position.y = bottom;
            if (!broadWave)
            {
                if (position.x > halfWidth) position.x = -halfWidth;
                else if (position.x < -halfWidth) position.x = halfWidth;
                float pulse = 0.84f + Mathf.Sin(Time.time * 1.15f + phase) * 0.16f;
                transform.localScale = Vector3.one * pulse;
                transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(Time.time * 0.7f + phase) * 12f);
            }
            transform.localPosition = position;
        }
    }
}
