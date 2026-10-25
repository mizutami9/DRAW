using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageGrainRainController : MonoBehaviour
    {
        private const string StageId = "9-3";
        private const string StateKind = "grain_rain_state";
        private const string CarryStateKind = "grain_rain_carry_state";
        private const int TotalRounds = 3;
        private const float IntroSeconds = 7f;
        private const float RainSeconds = 10f;
        private const float BlizzardSeconds = 12f;
        private const float ForecastRainSeconds = 18f;
        private const float SettleSeconds = 1.25f;
        private const float SpawnInterval = 0.1f;
        private const float BlizzardSpawnInterval = 0.075f;
        private const float ForecastSeconds = 1.15f;
        private const float ForecastRestSeconds = 0.8f;
        private const int BurstGrainsPerPoint = 5;
        private const float TargetGramsPerPlayer = 70f;
        private const float StepJumpHeight = 10.2f;
        // 9-3 deliberately uses a double-size arena while player drawings keep
        // their normal world size. Scale every spatial part of the challenge
        // through this value so terrain, monitor, grain field and camera stay in
        // the same composition and the characters read as smaller on screen.
        private const float SpatialScale = 2f;
        private const float ArenaHalfWidth = 14.4f * SpatialScale;
        private const float SpawnY = 12.9f * SpatialScale;

        private enum RoundState { Intro, Rain, Settle, Result }

        [System.Serializable]
        private sealed class RainState
        {
            public int Sequence;
            public int Round;
            public int Attempt;
            public int State;
            public int SpawnSequence;
            public int ForecastSequence;
            public int ThirdRoundSpawnBase;
            public float Remaining;
            public float ResultRemaining;
            public float MeasuredGrams;
            public float TargetGrams;
            public bool Success;
        }

        [System.Serializable]
        private sealed class CarryState
        {
            public int Sequence;
            public int Round;
            public int Attempt;
            public string PackedSequences;
            [System.NonSerialized]
            public float ReceivedAt;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageGimmickSyncManager syncManager;
        private UIManager uiManager;
        private StageCountdownPresenter countdownPresenter;
        private TextMesh timerText;
        private TextMesh statusText;
        private TextMesh scoreText;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private bool cameraLocked;
        private bool cameraWasEnabled;
        private Vector3 oldCameraPosition;
        private float oldCameraSize;
        private RoundState state;
        private float remaining = IntroSeconds;
        private float resultRemaining;
        private float nextSpawnAt;
        private float nextBroadcastAt;
        private float measuredGrams;
        private float targetGrams;
        private bool success;
        private int authoritativeSpawnSequence;
        private int localSpawnSequence;
        private int authoritativeForecastSequence;
        private int localForecastSequence;
        private int thirdRoundSpawnBase;
        private int round = 1;
        private int roundAttempt;
        private int stateSequence;
        private int receivedStateSequence;
        private int playerCount = 1;
        private float nextForecastAt;
        private float forecastBurstAt = -1f;
        private readonly List<GameObject> forecastMarkers = new List<GameObject>();
        private readonly Dictionary<string, CarryState> remoteCarryStates = new Dictionary<string, CarryState>();
        private readonly Dictionary<string, int> remoteCarrySequences = new Dictionary<string, int>();
        private float nextCarryStateAt;
        private int localCarryStateSequence;

        private bool HasAuthority => syncManager == null || !syncManager.IsOnlineActive || syncManager.IsHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            syncManager = GetComponent<StageGimmickSyncManager>();
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
            RestoreCamera();
            ClearParticles();
            StageGrainCarrier[] carriers = Object.FindObjectsByType<StageGrainCarrier>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                if (carriers[i] == null) continue;
                carriers[i].SetGrams(0f);
                Destroy(carriers[i]);
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

            playerCount = Mathf.Clamp(
                stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
            targetGrams = TargetGramsPerPlayer * playerCount;
            EnsureCarriers(true);
            ConfigureStepJumpPads();
            BuildMonitor();
            LockCamera();
            if (HasAuthority) BeginRound(1);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            EnsureCarriers(false);
            if (!HasAuthority && (state == RoundState.Rain || state == RoundState.Settle)
                && Time.unscaledTime >= nextCarryStateAt)
            {
                nextCarryStateAt = Time.unscaledTime + 0.12f;
                SendLocalCarryState();
            }
            if (state == RoundState.Intro) countdownPresenter?.Show(remaining);
            else if (state == RoundState.Result && !success) countdownPresenter?.ShowGameOver();
            else countdownPresenter?.Hide();

            if (HasAuthority)
            {
                if (state == RoundState.Intro)
                {
                    remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    if (remaining <= 0f)
                    {
                        state = RoundState.Rain;
                        remaining = GetRainDuration();
                        nextSpawnAt = Time.time + 0.2f;
                        nextForecastAt = Time.time + 0.25f;
                        forecastBurstAt = -1f;
                    }
                }
                else if (state == RoundState.Rain)
                {
                    if (!LocalMultiplayerDebugMode.NoTimeLimit)
                        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    if (round == 3) UpdateForecastRain();
                    else
                    {
                        float interval = round == 2 ? BlizzardSpawnInterval : SpawnInterval;
                        while ((LocalMultiplayerDebugMode.NoTimeLimit || remaining > 0f)
                            && Time.time >= nextSpawnAt)
                        {
                            authoritativeSpawnSequence++;
                            nextSpawnAt += interval;
                        }
                    }
                    if (LocalMultiplayerDebugMode.NoTimeLimit && GetCurrentlyCarriedGrams() >= targetGrams)
                    {
                        ClearForecastMarkers();
                        state = RoundState.Settle;
                        remaining = SettleSeconds;
                    }
                    else if (!LocalMultiplayerDebugMode.NoTimeLimit && remaining <= 0f)
                    {
                        ClearForecastMarkers();
                        state = RoundState.Settle;
                        remaining = SettleSeconds;
                    }
                }
                else if (state == RoundState.Settle)
                {
                    remaining -= Time.deltaTime;
                    if (remaining <= 0f) MeasureRound();
                }
                else
                {
                    resultRemaining -= Time.deltaTime;
                    if (resultRemaining <= 0f)
                    {
                        if (success && round < TotalRounds) BeginRound(round + 1);
                        else if (success) stageManager.ClearStage();
                        else stageManager.Retry();
                    }
                }
                BroadcastState(false);
            }

            while (localSpawnSequence < authoritativeSpawnSequence)
            {
                localSpawnSequence++;
                SpawnParticle(localSpawnSequence);
            }
            while (localForecastSequence < authoritativeForecastSequence)
            {
                localForecastSequence++;
                CreateForecastMarkers(localForecastSequence);
            }
            RefreshDisplay();
        }

        private static void ConfigureStepJumpPads()
        {
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject item = objects[i];
                if (item == null || string.IsNullOrEmpty(item.objectId)
                    || !item.objectId.StartsWith("9-3_step_jump_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                JumpPad pad = item.GetComponentInChildren<JumpPad>(true);
                pad?.ConfigureTargetHeight(StepJumpHeight);
            }
        }

        private void BeginRound(int nextRound)
        {
            round = Mathf.Clamp(nextRound, 1, TotalRounds);
            roundAttempt++;
            state = RoundState.Intro;
            remaining = 4f;
            resultRemaining = 0f;
            measuredGrams = 0f;
            success = false;
            authoritativeForecastSequence = 0;
            localForecastSequence = 0;
            forecastBurstAt = -1f;
            if (round == 3) thirdRoundSpawnBase = authoritativeSpawnSequence;
            ClearForecastMarkers();
            ClearParticles();
            EnsureCarriers(true);
            remoteCarryStates.Clear();
            remoteCarrySequences.Clear();
            BroadcastState(true);
        }

        private float GetRainDuration()
        {
            return round == 2 ? BlizzardSeconds : round == 3 ? ForecastRainSeconds : RainSeconds;
        }

        private void UpdateForecastRain()
        {
            if (forecastBurstAt >= 0f && Time.time >= forecastBurstAt)
            {
                int burstCount = playerCount * BurstGrainsPerPoint;
                for (int i = 0; i < burstCount; i++) authoritativeSpawnSequence++;
                forecastBurstAt = -1f;
                nextForecastAt = Time.time + ForecastRestSeconds;
                ClearForecastMarkers();
            }
            else if (forecastBurstAt < 0f && Time.time >= nextForecastAt
                && remaining > ForecastSeconds + 0.15f)
            {
                authoritativeForecastSequence++;
                forecastBurstAt = Time.time + ForecastSeconds;
            }
        }

        private void EnsureCarriers(bool clear)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                StageGrainCarrier carrier = StageGrainCarryController.GetOrAddCarrier(players[i]);
                if (clear) carrier?.SetGrams(0f);
            }
        }

        private void SpawnParticle(int sequence)
        {
            System.Random random = new System.Random(930031 + sequence * 7919);
            float roll = (float)random.NextDouble();
            float grams;
            Color color;
            bool rainbow = false;
            if (roll < 0.52f)
            {
                grams = 3f;
                color = new Color(0.45f, 0.76f, 0.96f, 1f);
            }
            else if (roll < 0.78f)
            {
                grams = 6f;
                color = new Color(0.28f, 0.88f, 0.48f, 1f);
            }
            else if (roll < 0.91f)
            {
                grams = 12f;
                color = new Color(1f, 0.72f, 0.08f, 1f);
            }
            else if (roll < 0.98f)
            {
                grams = 20f;
                color = new Color(0.72f, 0.26f, 0.96f, 1f);
            }
            else
            {
                grams = 40f;
                color = Color.white;
                rainbow = true;
            }

            float x = Mathf.Lerp(
                -ArenaHalfWidth + ScaleDistance(0.5f),
                ArenaHalfWidth - ScaleDistance(0.5f),
                (float)random.NextDouble());
            float y = SpawnY;
            float size = ScaleDistance(Mathf.Lerp(0.17f, 0.35f, (float)random.NextDouble()));
            size *= Mathf.Lerp(0.92f, 1.18f, grams / 40f);
            float fallSpeed = ScaleDistance(Mathf.Lerp(0.6f, 3.4f, (float)random.NextDouble()));
            float drift = ScaleDistance(Mathf.Lerp(-0.55f, 0.55f, (float)random.NextDouble()));
            float gravity = ScaleDistance(Mathf.Lerp(0.42f, 0.9f, (float)random.NextDouble()));
            Vector2 playerContactFreeFallVelocity = new Vector2(drift, -fallSpeed);
            float playerContactGravity = gravity;

            if (round == 2)
            {
                // Spawn just inside the authored right boundary so the grains
                // can actually enter the room instead of hitting its outer face.
                // The blizzard travels horizontally; gravity and initial
                // vertical velocity would otherwise collect it at bottom-left.
                x = ArenaHalfWidth - ScaleDistance(0.25f);
                y = ScaleDistance(Mathf.Lerp(-5.45f, 9.45f, (float)random.NextDouble()));
                fallSpeed = 0f;
                drift = -ScaleDistance(Mathf.Lerp(7.2f, 11.2f, (float)random.NextDouble()));
                gravity = 0f;
                size *= 0.82f;
            }
            else if (round == 3)
            {
                int offset = Mathf.Max(0, sequence - thirdRoundSpawnBase - 1);
                int grainsPerWave = Mathf.Max(1, playerCount * BurstGrainsPerPoint);
                int wave = offset / grainsPerWave + 1;
                int withinWave = offset % grainsPerWave;
                int targetIndex = withinWave / BurstGrainsPerPoint;
                x = GetForecastX(wave, targetIndex)
                    + ScaleDistance(Mathf.Lerp(-0.3f, 0.3f, (float)random.NextDouble()));
                y = SpawnY + ScaleDistance(Mathf.Lerp(0f, 0.7f, (float)random.NextDouble()));
                fallSpeed = ScaleDistance(Mathf.Lerp(9.5f, 13.5f, (float)random.NextDouble()));
                drift = ScaleDistance(Mathf.Lerp(-0.12f, 0.12f, (float)random.NextDouble()));
                gravity = ScaleDistance(0.32f);
                size *= 0.94f;
            }

            SpriteRenderer renderer = StageGrainCarryObjectFactory.AddDot(
                transform, "Rain Grain " + sequence, new Vector2(x, y), size, color, 72);
            if (grams >= 20f)
            {
                Color glowColor = rainbow ? new Color(1f, 0.85f, 0.2f, 0.3f) : new Color(color.r, color.g, color.b, 0.28f);
                SpriteRenderer glow = StageGrainCarryObjectFactory.AddDot(
                    renderer.transform, "Rare Glow", Vector2.zero, 1.55f, glowColor, 71);
                glow.gameObject.AddComponent<StageRareGrainGlow>();
            }
            StageGrainParticle particle = renderer.gameObject.AddComponent<StageGrainParticle>();
            particle.Configure(DrawManager.Species.Human, new Vector2(drift, -fallSpeed), grams, gravity, true);
            particle.SetNetworkSequence(sequence);
            if (round == 2)
            {
                // The blizzard enters horizontally, but once a grain actually
                // strikes a character it behaves like an ordinary falling grain
                // instead of continuing to push sideways through the player.
                particle.ConfigurePlayerContactFreeFall(
                    playerContactFreeFallVelocity,
                    playerContactGravity);
            }
            if (round == 2) renderer.gameObject.AddComponent<StageBlizzardGrainCleanup>();
            if (rainbow) renderer.gameObject.AddComponent<StageRainbowGrainVisual>();
        }

        private float GetForecastX(int wave, int targetIndex)
        {
            int count = Mathf.Max(1, playerCount);
            float segmentWidth = (ArenaHalfWidth * 2f - ScaleDistance(2f)) / count;
            float center = -ArenaHalfWidth + ScaleDistance(1f) + segmentWidth * (targetIndex + 0.5f);
            System.Random random = new System.Random(930300 + wave * 3571 + targetIndex * 811);
            float jitter = Mathf.Lerp(-segmentWidth * 0.28f, segmentWidth * 0.28f,
                (float)random.NextDouble());
            return Mathf.Clamp(
                center + jitter,
                -ArenaHalfWidth + ScaleDistance(0.8f),
                ArenaHalfWidth - ScaleDistance(0.8f));
        }

        private void CreateForecastMarkers(int wave)
        {
            ClearForecastMarkers();
            for (int i = 0; i < playerCount; i++)
            {
                float x = GetForecastX(wave, i);
                GameObject marker = new GameObject("Forecast Drop Point " + (i + 1));
                marker.transform.SetParent(transform, false);
                marker.transform.position = new Vector3(x, ScaleDistance(-6.12f), 0f);

                SpriteRenderer outer = StageGrainCarryObjectFactory.AddDot(
                    marker.transform, "Warning Outer", Vector2.zero, ScaleDistance(1.2f),
                    new Color(1f, 0.34f, 0.08f, 0.34f), 68);
                SpriteRenderer inner = StageGrainCarryObjectFactory.AddDot(
                    marker.transform, "Warning Center", Vector2.zero, ScaleDistance(0.5f),
                    new Color(1f, 0.78f, 0.08f, 0.62f), 69);
                StageGun.AddLine(marker.transform, "Forecast Falling Guide", new[]
                {
                    new Vector2(0f, ScaleDistance(0.35f)),
                    new Vector2(0f, SpawnY + ScaleDistance(5.8f))
                }, ScaleDistance(0.075f), new Color(1f, 0.3f, 0.08f, 0.3f), 67);
                Transform guideTransform = marker.transform.Find("Forecast Falling Guide");
                LineRenderer guide = guideTransform != null
                    ? guideTransform.GetComponent<LineRenderer>()
                    : null;
                marker.AddComponent<StageGrainForecastVisual>().Configure(
                    outer, inner, guide, ForecastSeconds + 0.3f, i * 0.7f);
                forecastMarkers.Add(marker);
            }
        }

        private void ClearForecastMarkers()
        {
            for (int i = 0; i < forecastMarkers.Count; i++)
                if (forecastMarkers[i] != null) Destroy(forecastMarkers[i]);
            forecastMarkers.Clear();
        }

        private void MeasureRound()
        {
            measuredGrams = Mathf.Round(GetCurrentlyCarriedGrams());
            success = measuredGrams >= targetGrams;
            state = RoundState.Result;
            resultRemaining = success ? 3.2f : 5f;
            BroadcastState(true);
        }

        private float GetCurrentlyCarriedGrams()
        {
            if (syncManager != null && syncManager.IsOnlineActive && syncManager.IsHost)
            {
                HashSet<int> uniqueSequences = GetLocalCarriedParticleSequences();
                foreach (KeyValuePair<string, CarryState> pair in remoteCarryStates)
                {
                    if (stageManager.GetOnlinePlayerController(pair.Key) == null
                        || pair.Value == null
                        || Time.unscaledTime - pair.Value.ReceivedAt > 0.75f) continue;
                    AddPackedSequences(pair.Value.PackedSequences, authoritativeSpawnSequence, uniqueSequences);
                }
                float total = 0f;
                foreach (int sequence in uniqueSequences) total += GetGramsForSequence(sequence);
                return total;
            }

            float grams = 0f;
            foreach (StageGrainParticle particle in StageGrainParticle.All)
            {
                if (particle == null) continue;
                bool carried = false;
                foreach (StageGrainCarrier carrier in StageGrainCarrier.All)
                {
                    if (particle.IsContainedByForMeasurement(carrier))
                    {
                        carried = true;
                        break;
                    }
                }
                if (carried) grams += particle.Grams;
            }
            return grams;
        }

        private HashSet<int> GetLocalCarriedParticleSequences()
        {
            HashSet<int> sequences = new HashSet<int>();
            if (stageManager == null) return sequences;
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                StageGrainCarrier carrier = localPlayers[i] != null
                    ? localPlayers[i].GetComponent<StageGrainCarrier>()
                    : null;
                if (carrier == null) continue;
                foreach (StageGrainParticle particle in StageGrainParticle.All)
                    if (particle != null && particle.NetworkSequence > 0
                        && particle.IsContainedByForMeasurement(carrier))
                        sequences.Add(particle.NetworkSequence);
            }
            return sequences;
        }

        private string PackLocalCarriedParticleSequences()
        {
            int byteCount = Mathf.Max(0, (authoritativeSpawnSequence + 7) / 8);
            if (byteCount == 0) return string.Empty;
            byte[] bits = new byte[byteCount];
            foreach (int sequence in GetLocalCarriedParticleSequences())
            {
                int index = sequence - 1;
                if (index < 0 || index >= byteCount * 8) continue;
                bits[index >> 3] |= (byte)(1 << (index & 7));
            }
            return System.Convert.ToBase64String(bits);
        }

        private static void AddPackedSequences(string packed, int maximumSequence, HashSet<int> target)
        {
            if (string.IsNullOrEmpty(packed) || target == null || maximumSequence <= 0) return;
            byte[] bits;
            try { bits = System.Convert.FromBase64String(packed); }
            catch (System.FormatException) { return; }
            int count = Mathf.Min(maximumSequence, bits.Length * 8);
            for (int index = 0; index < count; index++)
                if ((bits[index >> 3] & 1 << (index & 7)) != 0) target.Add(index + 1);
        }

        private static float GetGramsForSequence(int sequence)
        {
            System.Random random = new System.Random(930031 + sequence * 7919);
            float roll = (float)random.NextDouble();
            return roll < 0.52f ? 3f : roll < 0.78f ? 6f : roll < 0.91f ? 12f : roll < 0.98f ? 20f : 40f;
        }

        private void SendLocalCarryState()
        {
            if (onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = CarryStateKind,
                Json = JsonUtility.ToJson(new CarryState
                {
                    Sequence = ++localCarryStateSequence,
                    Round = round,
                    Attempt = roundAttempt,
                    PackedSequences = PackLocalCarriedParticleSequences()
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == CarryStateKind && HasAuthority)
            {
                CarryState carry = JsonUtility.FromJson<CarryState>(data.Json);
                if (carry != null && carry.Round == round && carry.Attempt == roundAttempt
                    && !string.IsNullOrEmpty(data.PlayerId)
                    && stageManager.GetOnlinePlayerController(data.PlayerId) != null)
                {
                    if (remoteCarrySequences.TryGetValue(data.PlayerId, out int appliedSequence)
                        && carry.Sequence <= appliedSequence) return;
                    remoteCarrySequences[data.PlayerId] = carry.Sequence;
                    carry.ReceivedAt = Time.unscaledTime;
                    remoteCarryStates[data.PlayerId] = carry;
                }
                return;
            }
            if (data.Kind != StateKind || HasAuthority || onlineManager == null
                || !onlineManager.IsHostPlayer(data.PlayerId)) return;
            RainState snapshot = JsonUtility.FromJson<RainState>(data.Json);
            if (snapshot == null || snapshot.Sequence <= receivedStateSequence) return;
            receivedStateSequence = snapshot.Sequence;
            if (snapshot.Round != round || snapshot.Attempt != roundAttempt)
            {
                round = Mathf.Clamp(snapshot.Round, 1, TotalRounds);
                roundAttempt = snapshot.Attempt;
                ClearParticles();
                ClearForecastMarkers();
                EnsureCarriers(true);
                localForecastSequence = 0;
            }
            state = (RoundState)Mathf.Clamp(snapshot.State, 0, 3);
            authoritativeSpawnSequence = Mathf.Max(authoritativeSpawnSequence, snapshot.SpawnSequence);
            authoritativeForecastSequence = snapshot.ForecastSequence;
            thirdRoundSpawnBase = snapshot.ThirdRoundSpawnBase;
            remaining = snapshot.Remaining;
            resultRemaining = snapshot.ResultRemaining;
            measuredGrams = snapshot.MeasuredGrams;
            targetGrams = snapshot.TargetGrams;
            success = snapshot.Success;
        }

        private void BroadcastState(bool force)
        {
            if (syncManager == null || !syncManager.IsOnlineActive || !syncManager.IsHost || onlineManager == null) return;
            if (!force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.18f;
            RainState snapshot = new RainState
            {
                Sequence = ++stateSequence,
                Round = round,
                Attempt = roundAttempt,
                State = (int)state,
                SpawnSequence = authoritativeSpawnSequence,
                ForecastSequence = authoritativeForecastSequence,
                ThirdRoundSpawnBase = thirdRoundSpawnBase,
                Remaining = remaining,
                ResultRemaining = resultRemaining,
                MeasuredGrams = measuredGrams,
                TargetGrams = targetGrams,
                Success = success
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(snapshot)
            });
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("9-3 Grain Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(0f, ScaleDistance(10.8f), 0.2f);
            DoodleMonitorVisuals.Build(
                monitor.transform,
                new Vector2(ScaleDistance(10.8f), ScaleDistance(2.65f)),
                3);
            timerText = CreateText(
                monitor.transform,
                new Vector2(0f, ScaleDistance(0.42f)),
                ScaleDistance(0.16f),
                9,
                new Color(0.04f, 0.43f, 0.58f));
            scoreText = CreateText(
                monitor.transform,
                new Vector2(0f, ScaleDistance(-0.45f)),
                ScaleDistance(0.095f),
                9,
                new Color(0.1f, 0.28f, 0.5f));

        }

        private void RefreshDisplay()
        {
            if (timerText == null) return;
            string roundLabel = LocalizationManager.Format("grain_rain_round", round, TotalRounds);
            if (state == RoundState.Intro)
            {
                timerText.text = roundLabel + "   " + Mathf.CeilToInt(remaining).ToString("00") + ".0";
                scoreText.text = LocalizationManager.T("grain_rain_catch");
            }
            else if (state == RoundState.Rain)
            {
                timerText.text = roundLabel + "   " + (LocalMultiplayerDebugMode.NoTimeLimit
                    ? "\u221e"
                    : Mathf.CeilToInt(remaining).ToString("00") + ".0");
                scoreText.text = targetGrams.ToString("0") + "g";
            }
            else if (state == RoundState.Settle)
            {
                timerText.text = LocalizationManager.T("grain_rain_measuring");
                scoreText.text = targetGrams.ToString("0") + "g";
            }
            else
            {
                timerText.text = success
                    ? roundLabel + "   " + LocalizationManager.T("grain_rain_clear")
                    : LocalizationManager.T("grain_rain_failed");
                scoreText.text = LocalizationManager.Format("grain_rain_result", measuredGrams, targetGrams);
            }
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

        private void LockCamera()
        {
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
            if (gameCamera == null) return;
            cameraWasEnabled = cameraFollow != null && cameraFollow.enabled;
            if (cameraFollow != null) cameraFollow.enabled = false;
            oldCameraPosition = gameCamera.transform.position;
            oldCameraSize = gameCamera.orthographicSize;
            float widthSize = ScaleDistance(15.8f) / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, ScaleDistance(3.3f), oldCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(ScaleDistance(10.2f), widthSize);
            cameraLocked = true;
        }

        private static float ScaleDistance(float value)
        {
            return value * SpatialScale;
        }

        private void RestoreCamera()
        {
            if (!cameraLocked) return;
            if (cameraFollow != null) cameraFollow.enabled = cameraWasEnabled;
            if (gameCamera != null)
            {
                gameCamera.transform.position = oldCameraPosition;
                gameCamera.orthographicSize = oldCameraSize;
            }
            cameraLocked = false;
        }

        private static void ClearParticles()
        {
            List<StageGrainParticle> particles = new List<StageGrainParticle>(StageGrainParticle.All);
            for (int i = 0; i < particles.Count; i++) if (particles[i] != null) particles[i].Consume();
        }
    }

    public sealed class StageRainbowGrainVisual : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float hueOffset;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hueOffset = Mathf.Repeat(transform.position.x * 0.173f + transform.position.y * 0.071f, 1f);
        }

        private void Update()
        {
            if (spriteRenderer != null) spriteRenderer.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.32f + hueOffset, 1f), 0.78f, 1f);
        }
    }

    public sealed class StageRareGrainGlow : MonoBehaviour
    {
        private Vector3 baseScale;

        private void Awake() { baseScale = transform.localScale; }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7f + transform.position.x) * 0.12f;
            transform.localScale = baseScale * pulse;
        }
    }

    public sealed class StageGrainForecastVisual : MonoBehaviour
    {
        private SpriteRenderer outer;
        private SpriteRenderer inner;
        private LineRenderer guide;
        private Color outerColor;
        private Color innerColor;
        private Color guideColor;
        private Vector3 outerScale;
        private float createdAt;
        private float lifetime;
        private float phase;

        public void Configure(
            SpriteRenderer outerRenderer, SpriteRenderer innerRenderer,
            LineRenderer guideRenderer, float duration, float phaseOffset)
        {
            outer = outerRenderer;
            inner = innerRenderer;
            guide = guideRenderer;
            if (outer != null)
            {
                outerColor = outer.color;
                outerScale = outer.transform.localScale;
            }
            if (inner != null) innerColor = inner.color;
            if (guide != null) guideColor = guide.startColor;
            createdAt = Time.unscaledTime;
            lifetime = Mathf.Max(0.1f, duration);
            phase = phaseOffset;
        }

        private void Update()
        {
            float age = Time.unscaledTime - createdAt;
            float pulse = 0.86f + Mathf.PingPong(age * 2.8f + phase, 0.34f);
            float urgency = Mathf.Clamp01(age / Mathf.Max(0.1f, lifetime - 0.25f));
            float blink = Mathf.Lerp(0.55f, 1f,
                0.5f + Mathf.Sin(Time.unscaledTime * Mathf.Lerp(7f, 18f, urgency) + phase) * 0.5f);
            if (outer != null)
            {
                outer.transform.localScale = outerScale * pulse;
                Color color = outerColor;
                color.a *= blink;
                outer.color = color;
            }
            if (inner != null)
            {
                Color color = innerColor;
                color.a *= Mathf.Lerp(0.65f, 1f, blink);
                inner.color = color;
            }
            if (guide != null)
            {
                Color color = guideColor;
                color.a *= Mathf.Lerp(0.45f, 1f, blink);
                guide.startColor = guide.endColor = color;
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }

    public sealed class StageBlizzardGrainCleanup : MonoBehaviour
    {
        private StageGrainParticle particle;
        private float bornAt;

        private void Awake()
        {
            particle = GetComponent<StageGrainParticle>();
            bornAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (particle == null || particle.IsInsideCarrier) return;
            if (transform.position.x <= -28.3f || Time.unscaledTime - bornAt >= 16f)
                particle.Consume();
        }
    }
}
