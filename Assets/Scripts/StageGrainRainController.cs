using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageGrainRainController : MonoBehaviour
    {
        private const string StageId = "9-3";
        private const string StateKind = "grain_rain_state";
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
        private const float ArenaHalfWidth = 14.4f;
        private const float SpawnY = 12.9f;

        private enum RoundState { Intro, Rain, Settle, Result }

        [System.Serializable]
        private sealed class RainState
        {
            public int Round;
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

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageGimmickSyncManager syncManager;
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
        private int playerCount = 1;
        private float nextForecastAt;
        private float forecastBurstAt = -1f;
        private readonly List<GameObject> forecastMarkers = new List<GameObject>();

        private bool HasAuthority => syncManager == null || !syncManager.IsOnlineActive || syncManager.IsHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            syncManager = GetComponent<StageGimmickSyncManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
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
            targetGrams = 90f * playerCount;
            EnsureCarriers(true);
            BuildMonitor();
            LockCamera();
            if (HasAuthority) BeginRound(1);
            RefreshDisplay();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            EnsureCarriers(false);

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
                    remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    if (round == 3) UpdateForecastRain();
                    else
                    {
                        float interval = round == 2 ? BlizzardSpawnInterval : SpawnInterval;
                        while (remaining > 0f && Time.time >= nextSpawnAt)
                        {
                            authoritativeSpawnSequence++;
                            nextSpawnAt += interval;
                        }
                    }
                    if (remaining <= 0f)
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

        private void BeginRound(int nextRound)
        {
            round = Mathf.Clamp(nextRound, 1, TotalRounds);
            state = RoundState.Intro;
            remaining = round == 1 ? IntroSeconds : 3.5f;
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

            float x = Mathf.Lerp(-ArenaHalfWidth + 0.5f, ArenaHalfWidth - 0.5f, (float)random.NextDouble());
            float y = SpawnY;
            float size = Mathf.Lerp(0.17f, 0.35f, (float)random.NextDouble());
            size *= Mathf.Lerp(0.92f, 1.18f, grams / 40f);
            float fallSpeed = Mathf.Lerp(0.6f, 3.4f, (float)random.NextDouble());
            float drift = Mathf.Lerp(-0.55f, 0.55f, (float)random.NextDouble());
            float gravity = Mathf.Lerp(0.42f, 0.9f, (float)random.NextDouble());

            if (round == 2)
            {
                // Spawn just inside the authored right boundary so the grains
                // can actually enter the room instead of hitting its outer face.
                // The blizzard travels horizontally; gravity and initial
                // vertical velocity would otherwise collect it at bottom-left.
                x = ArenaHalfWidth - 0.25f;
                y = Mathf.Lerp(-5.45f, 9.45f, (float)random.NextDouble());
                fallSpeed = 0f;
                drift = -Mathf.Lerp(7.2f, 11.2f, (float)random.NextDouble());
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
                    + Mathf.Lerp(-0.3f, 0.3f, (float)random.NextDouble());
                y = SpawnY + Mathf.Lerp(0f, 0.7f, (float)random.NextDouble());
                fallSpeed = Mathf.Lerp(9.5f, 13.5f, (float)random.NextDouble());
                drift = Mathf.Lerp(-0.12f, 0.12f, (float)random.NextDouble());
                gravity = 0.32f;
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
            if (round == 2) renderer.gameObject.AddComponent<StageBlizzardGrainCleanup>();
            if (rainbow) renderer.gameObject.AddComponent<StageRainbowGrainVisual>();
        }

        private float GetForecastX(int wave, int targetIndex)
        {
            int count = Mathf.Max(1, playerCount);
            float segmentWidth = (ArenaHalfWidth * 2f - 2f) / count;
            float center = -ArenaHalfWidth + 1f + segmentWidth * (targetIndex + 0.5f);
            System.Random random = new System.Random(930300 + wave * 3571 + targetIndex * 811);
            float jitter = Mathf.Lerp(-segmentWidth * 0.28f, segmentWidth * 0.28f,
                (float)random.NextDouble());
            return Mathf.Clamp(center + jitter, -ArenaHalfWidth + 0.8f, ArenaHalfWidth - 0.8f);
        }

        private void CreateForecastMarkers(int wave)
        {
            ClearForecastMarkers();
            for (int i = 0; i < playerCount; i++)
            {
                float x = GetForecastX(wave, i);
                GameObject marker = new GameObject("Forecast Drop Point " + (i + 1));
                marker.transform.SetParent(transform, false);
                marker.transform.position = new Vector3(x, -6.12f, 0f);

                SpriteRenderer outer = StageGrainCarryObjectFactory.AddDot(
                    marker.transform, "Warning Outer", Vector2.zero, 1.2f,
                    new Color(1f, 0.34f, 0.08f, 0.34f), 68);
                SpriteRenderer inner = StageGrainCarryObjectFactory.AddDot(
                    marker.transform, "Warning Center", Vector2.zero, 0.5f,
                    new Color(1f, 0.78f, 0.08f, 0.62f), 69);
                StageGun.AddLine(marker.transform, "Forecast Falling Guide", new[]
                {
                    new Vector2(0f, 0.35f), new Vector2(0f, SpawnY + 5.8f)
                }, 0.075f, new Color(1f, 0.3f, 0.08f, 0.3f), 67);
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
            measuredGrams = 0f;
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
                if (carried) measuredGrams += particle.Grams;
            }

            measuredGrams = Mathf.Round(measuredGrams);
            success = measuredGrams >= targetGrams;
            state = RoundState.Result;
            resultRemaining = success ? 3.2f : 5f;
            BroadcastState(true);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind || HasAuthority) return;
            RainState snapshot = JsonUtility.FromJson<RainState>(data.Json);
            if (snapshot == null) return;
            if (snapshot.Round != round)
            {
                round = Mathf.Clamp(snapshot.Round, 1, TotalRounds);
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
                Round = round,
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
            monitor.transform.position = new Vector3(0f, 10.8f, 0.2f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(10.8f, 2.65f), 3);
            timerText = CreateText(monitor.transform, new Vector2(0f, 0.42f), 0.16f, 9, new Color(0.04f, 0.43f, 0.58f));
            scoreText = CreateText(monitor.transform, new Vector2(0f, -0.45f), 0.095f, 9, new Color(0.1f, 0.28f, 0.5f));

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
                timerText.text = roundLabel + "   " + Mathf.CeilToInt(remaining).ToString("00") + ".0";
                scoreText.text = LocalizationManager.Format("grain_rain_target", targetGrams);
            }
            else if (state == RoundState.Settle)
            {
                timerText.text = LocalizationManager.T("grain_rain_measuring");
                scoreText.text = LocalizationManager.Format("grain_rain_target", targetGrams);
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
            float widthSize = 15.8f / Mathf.Max(0.2f, gameCamera.aspect);
            gameCamera.transform.position = new Vector3(0f, 3.3f, oldCameraPosition.z);
            gameCamera.orthographicSize = Mathf.Max(10.2f, widthSize);
            cameraLocked = true;
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
            if (transform.position.x <= -14.15f || Time.unscaledTime - bornAt >= 16f)
                particle.Consume();
        }
    }
}
