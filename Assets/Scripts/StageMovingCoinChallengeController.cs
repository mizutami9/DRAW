using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Runtime coin field for stage 12-3. All 300 transforms are advanced from one
    /// controller so the final coin rush does not create hundreds of Update calls.
    /// The paths are deterministic; online collection itself remains host-authoritative
    /// through StageCollectible and StageManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageMovingCoinChallengeController : MonoBehaviour
    {
        private enum MotionKind
        {
            Perimeter,
            OutsideWave,
            Wander,
            Flock,
            Flee,
            FastWave
        }

        private sealed class CoinEntry
        {
            public Transform Transform;
            public MotionKind Kind;
            public int Group;
            public int Member;
            public float Phase;
            public float Seed;
            public Vector2 Position;
            public Vector2 Velocity;
        }

        private const int PerimeterCount = 48;
        private const int OutsideWaveCount = 60;
        private const int WanderCount = 28;
        private const int FlockCount = 80;
        private const int FleeCount = 12;
        private const int FastWaveCount = 72;
        private const int TotalCoinCount = 300;

        private static bool creatingEditorPreview;
        private static Sprite warningSprite;
        private static AudioClip pickupClip;

        private readonly List<CoinEntry> coins = new List<CoinEntry>(TotalCoinCount);
        private readonly List<SpriteRenderer> warnings = new List<SpriteRenderer>(6);
        private PlayerController2D[] players = new PlayerController2D[0];
        private StageManager stageManager;
        private AudioSource pickupAudio;
        private bool previewOnly;
        private float motionClock;
        private float nextPlayerRefresh;
        private int lastCollectedCount;
        private int pickupCombo;
        private float lastPickupTime;

        internal static void CreateEditorPreview(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            GameObject preview = new GameObject("12-3 Moving Coin Preview");
            preview.transform.SetParent(parent, false);
            creatingEditorPreview = true;
            try
            {
                preview.AddComponent<StageMovingCoinChallengeController>();
            }
            finally
            {
                creatingEditorPreview = false;
            }
        }

        private void Awake()
        {
            previewOnly = creatingEditorPreview;
            BuildCoins();
            AdvanceCoins(0f);
            BuildWarningMarkers();
            if (!previewOnly)
            {
                pickupAudio = gameObject.AddComponent<AudioSource>();
                pickupAudio.playOnAwake = false;
                pickupAudio.loop = false;
                pickupAudio.spatialBlend = 0f;
                pickupAudio.dopplerLevel = 0f;
                pickupAudio.clip = GetPickupClip();
            }
        }

        private void Start()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            lastCollectedCount = stageManager != null ? stageManager.ChallengeCollectedCount : 0;
            RefreshPlayers();
        }

        private void Update()
        {
            bool running = previewOnly
                || (stageManager != null
                    && stageManager.IsGameplayActive
                    && stageManager.IsTimedCollectionChallenge
                    && !stageManager.ChallengeStarting
                    && !stageManager.ChallengeTimeUp);
            if (running)
            {
                float speedScale = ResolveSpeedScale();
                motionClock += Time.unscaledDeltaTime * speedScale;
                if (!previewOnly && Time.unscaledTime >= nextPlayerRefresh)
                {
                    RefreshPlayers();
                }
                AdvanceCoins(Time.unscaledDeltaTime * speedScale);
            }

            UpdateWarnings(running);
            UpdatePickupFeedback();
        }

        private void BuildCoins()
        {
            int index = 0;
            for (int i = 0; i < PerimeterCount; i++)
            {
                AddCoin(index++, MotionKind.Perimeter, 0, i, i / (float)PerimeterCount, i * 0.731f);
            }
            for (int i = 0; i < OutsideWaveCount; i++)
            {
                AddCoin(index++, MotionKind.OutsideWave, i / 20, i % 20, 0f, i * 1.117f);
            }
            for (int i = 0; i < WanderCount; i++)
            {
                AddCoin(index++, MotionKind.Wander, 0, i, 0f, i * 2.173f + 4.1f);
            }
            for (int i = 0; i < FlockCount; i++)
            {
                AddCoin(index++, MotionKind.Flock, i / 20, i % 20, 0f, i * 0.913f);
            }
            for (int i = 0; i < FleeCount; i++)
            {
                AddCoin(index++, MotionKind.Flee, 0, i, 0f, i * 3.117f + 8.3f);
            }
            for (int i = 0; i < FastWaveCount; i++)
            {
                AddCoin(index++, MotionKind.FastWave, i / 24, i % 24, 0f, i * 1.319f);
            }

            Debug.Assert(index == TotalCoinCount, $"12-3 coin count mismatch: {index}");
        }

        private void AddCoin(int index, MotionKind kind, int group, int member, float phase, float seed)
        {
            GameObject coin = new GameObject($"12-3_moving_coin_{index:000}");
            coin.transform.SetParent(transform, false);
            coin.transform.position = new Vector3(40f, 40f, -0.15f);
            float scale = 0.48f + (index % 5) * 0.018f;
            coin.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = coin.AddComponent<SpriteRenderer>();
            renderer.sprite = StageCoinRushController.GetSharedCoinSprite();
            renderer.color = index % 7 == 0
                ? new Color(1f, 0.92f, 0.48f, 1f)
                : Color.white;
            renderer.sortingOrder = 17;

            if (!previewOnly)
            {
                CircleCollider2D trigger = coin.AddComponent<CircleCollider2D>();
                trigger.radius = 0.58f;
                trigger.isTrigger = true;
                StageCollectible collectible = coin.AddComponent<StageCollectible>();
                collectible.Configure(coin.name, StageObjectType.CollectibleCoin);
            }

            Vector2 initial = new Vector2(
                Mathf.Lerp(-22f, 22f, Hash01(seed + 1.2f)),
                Mathf.Lerp(-10f, 10f, Hash01(seed + 7.4f)));
            float angle = Hash01(seed + 12.8f) * Mathf.PI * 2f;
            coins.Add(new CoinEntry
            {
                Transform = coin.transform,
                Kind = kind,
                Group = group,
                Member = member,
                Phase = phase,
                Seed = seed,
                Position = initial,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (2.2f + Hash01(seed) * 1.8f)
            });
        }

        private void AdvanceCoins(float deltaTime)
        {
            float progress = ResolveProgress();
            bool fastWavesEnabled = previewOnly || progress >= 0.28f;
            for (int i = 0; i < coins.Count; i++)
            {
                CoinEntry coin = coins[i];
                if (coin.Transform == null || !coin.Transform.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 position;
                switch (coin.Kind)
                {
                    case MotionKind.Perimeter:
                        position = PerimeterPosition(coin);
                        break;
                    case MotionKind.OutsideWave:
                        position = OutsideWavePosition(coin);
                        break;
                    case MotionKind.Wander:
                        position = AdvanceWander(coin, deltaTime, false);
                        break;
                    case MotionKind.Flock:
                        position = FlockPosition(coin);
                        break;
                    case MotionKind.Flee:
                        position = AdvanceWander(coin, deltaTime, true);
                        break;
                    default:
                        position = fastWavesEnabled ? FastWavePosition(coin) : new Vector2(38f, 30f + coin.Member * 0.05f);
                        break;
                }

                coin.Transform.position = new Vector3(position.x, position.y, -0.15f);
                coin.Transform.Rotate(0f, 0f, deltaTime * (85f + coin.Group * 13f));
            }
        }

        private Vector2 PerimeterPosition(CoinEntry coin)
        {
            Vector2[] route =
            {
                new Vector2(-24.5f, -10.2f),
                new Vector2(24.5f, -10.2f),
                new Vector2(24.5f, 9.6f),
                new Vector2(-24.5f, 9.6f)
            };
            return SampleClosedRoute(route, coin.Phase + motionClock * 0.022f);
        }

        private Vector2 OutsideWavePosition(CoinEntry coin)
        {
            float period = ResolveProgress() >= 0.67f ? 8.6f : 13f;
            float phase = Mathf.Repeat(motionClock + coin.Group * 3.7f, period);
            if (phase > 4.8f)
            {
                return new Vector2(38f + coin.Group, 28f + coin.Member * 0.04f);
            }
            float travel = Mathf.InverseLerp(0f, 4.8f, phase);
            float trail = coin.Member * 0.58f;
            if (coin.Group == 0)
            {
                return new Vector2(Mathf.Lerp(-34f, 34f, travel) - trail, -3.3f);
            }
            if (coin.Group == 1)
            {
                return new Vector2(Mathf.Lerp(34f, -34f, travel) + trail, 5.3f);
            }
            return new Vector2(0.2f, Mathf.Lerp(-19f, 19f, travel) - trail * 0.7f);
        }

        private Vector2 FlockPosition(CoinEntry coin)
        {
            Vector2[][] routes =
            {
                new[] { new Vector2(-24f,-10f), new Vector2(-12f,-3f), new Vector2(0f,-3f), new Vector2(12f,5f), new Vector2(23f,9f), new Vector2(17f,-10f) },
                new[] { new Vector2(23f,-10f), new Vector2(13f,-3f), new Vector2(0f,5f), new Vector2(-12f,5f), new Vector2(-23f,9f), new Vector2(-17f,-10f) },
                new[] { new Vector2(-20f,-9f), new Vector2(0f,-3f), new Vector2(20f,-9f), new Vector2(10f,5f), new Vector2(0f,9f), new Vector2(-10f,5f) },
                new[] { new Vector2(20f,8.5f), new Vector2(0f,5f), new Vector2(-20f,8.5f), new Vector2(-10f,-3f), new Vector2(0f,-9f), new Vector2(10f,-3f) }
            };
            float spacing = coin.Member * 0.0085f;
            float phase = motionClock * (0.042f + coin.Group * 0.003f) - spacing + coin.Group * 0.19f;
            Vector2 basePosition = SampleClosedRoute(routes[coin.Group], phase);
            float wiggle = Mathf.Sin(motionClock * 2.1f + coin.Seed) * 0.16f;
            return basePosition + new Vector2(0f, wiggle);
        }

        private Vector2 FastWavePosition(CoinEntry coin)
        {
            float period = ResolveProgress() >= 0.67f ? 6.2f : 10.5f;
            float phase = Mathf.Repeat(motionClock + coin.Group * 2.45f, period);
            if (phase > 2.15f)
            {
                return new Vector2(40f + coin.Group, 30f + coin.Member * 0.04f);
            }
            float travel = Mathf.InverseLerp(0f, 2.15f, phase);
            float trail = coin.Member * 0.54f;
            if (coin.Group == 0)
            {
                return new Vector2(Mathf.Lerp(-37f, 37f, travel) - trail, -3.3f);
            }
            if (coin.Group == 1)
            {
                return new Vector2(0.2f, Mathf.Lerp(-21f, 21f, travel) - trail * 0.62f);
            }
            return new Vector2(Mathf.Lerp(37f, -37f, travel) + trail, 5.3f);
        }

        private Vector2 AdvanceWander(CoinEntry coin, float deltaTime, bool flee)
        {
            float steerAngle = Mathf.Sin(motionClock * (0.42f + Hash01(coin.Seed) * 0.2f) + coin.Seed) * 0.95f;
            Vector2 desired = new Vector2(Mathf.Cos(steerAngle + coin.Seed), Mathf.Sin(steerAngle + coin.Seed));
            coin.Velocity = Vector2.Lerp(coin.Velocity, desired * (flee ? 3.25f : 2.15f), deltaTime * 0.7f);

            if (flee)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null || !players[i].gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    Vector2 away = coin.Position - (Vector2)players[i].transform.position;
                    float distance = away.magnitude;
                    if (distance < 6.5f && distance > 0.05f)
                    {
                        coin.Velocity += away / distance * ((6.5f - distance) * 2.1f);
                    }
                }
                coin.Velocity = Vector2.ClampMagnitude(coin.Velocity, 6.4f);
            }

            coin.Position += coin.Velocity * deltaTime;
            const float left = -24.5f;
            const float right = 24.5f;
            const float bottom = -10.2f;
            const float top = 9.6f;
            if (coin.Position.x < left || coin.Position.x > right)
            {
                coin.Position.x = Mathf.Clamp(coin.Position.x, left, right);
                coin.Velocity.x = -coin.Velocity.x;
            }
            if (coin.Position.y < bottom || coin.Position.y > top)
            {
                coin.Position.y = Mathf.Clamp(coin.Position.y, bottom, top);
                coin.Velocity.y = -coin.Velocity.y;
            }
            return coin.Position;
        }

        private void BuildWarningMarkers()
        {
            Vector2[] positions =
            {
                new Vector2(-27.1f,-3.3f), new Vector2(27.1f,5.3f), new Vector2(0.2f,-12.7f),
                new Vector2(-27.1f,-3.3f), new Vector2(0.2f,-12.7f), new Vector2(27.1f,5.3f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject marker = new GameObject($"12-3_wave_warning_{i}");
                marker.transform.SetParent(transform, false);
                marker.transform.position = new Vector3(positions[i].x, positions[i].y, -0.4f);
                marker.transform.localScale = Vector3.one * (i < 3 ? 1.1f : 1.45f);
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = GetWarningSprite();
                renderer.sortingOrder = 31;
                renderer.color = Color.clear;
                warnings.Add(renderer);
            }
        }

        private void UpdateWarnings(bool running)
        {
            float progress = ResolveProgress();
            for (int i = 0; i < warnings.Count; i++)
            {
                bool fast = i >= 3;
                int group = fast ? i - 3 : i;
                float period = fast
                    ? progress >= 0.67f ? 6.2f : 10.5f
                    : progress >= 0.67f ? 8.6f : 13f;
                float phase = Mathf.Repeat(motionClock + group * (fast ? 2.45f : 3.7f), period);
                bool enabled = running
                    && (!fast || previewOnly || progress >= 0.28f)
                    && phase >= period - (fast ? 1.2f : 1.55f);
                float pulse = 0.45f + Mathf.PingPong(Time.unscaledTime * 4.5f, 0.55f);
                warnings[i].color = enabled
                    ? new Color(1f, fast ? 0.22f : 0.72f, 0.05f, pulse)
                    : Color.clear;
                warnings[i].transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 8f + i) * 9f);
            }
        }

        private void RefreshPlayers()
        {
            players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            nextPlayerRefresh = Time.unscaledTime + 0.5f;
        }

        private float ResolveProgress()
        {
            if (previewOnly || stageManager == null)
            {
                return Mathf.Repeat(motionClock / 80f, 1f);
            }
            int count = Mathf.Clamp(stageManager.GetInkBudgetPlayerCount(), 1, 4);
            float duration = count >= 4 ? 50f : count == 3 ? 60f : count == 2 ? 75f : 90f;
            return Mathf.Clamp01(1f - stageManager.ChallengeRemainingSeconds / duration);
        }

        private float ResolveSpeedScale()
        {
            float progress = ResolveProgress();
            return Mathf.Lerp(0.82f, 1.12f, progress) + (progress >= 0.67f ? 0.28f : 0f);
        }

        private void UpdatePickupFeedback()
        {
            if (previewOnly || stageManager == null || pickupAudio == null)
            {
                return;
            }
            int current = stageManager.ChallengeCollectedCount;
            if (current <= lastCollectedCount)
            {
                lastCollectedCount = current;
                return;
            }

            if (Time.unscaledTime - lastPickupTime > 0.3f)
            {
                pickupCombo = 0;
            }
            pickupCombo += current - lastCollectedCount;
            lastCollectedCount = current;
            lastPickupTime = Time.unscaledTime;
            pickupAudio.pitch = Mathf.Min(1.62f, 0.9f + pickupCombo * 0.028f);
            pickupAudio.volume = Mathf.Clamp01(GameSfx.MasterVolume * 0.72f);
            pickupAudio.PlayOneShot(GetPickupClip(), Mathf.Min(1f, 0.55f + pickupCombo * 0.025f));
        }

        private static Vector2 SampleClosedRoute(IReadOnlyList<Vector2> points, float normalizedDistance)
        {
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                total += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
            }
            float distance = Mathf.Repeat(normalizedDistance, 1f) * total;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                float length = Vector2.Distance(start, end);
                if (distance <= length)
                {
                    return Vector2.Lerp(start, end, length > 0f ? distance / length : 0f);
                }
                distance -= length;
            }
            return points[0];
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
        }

        private static Sprite GetWarningSprite()
        {
            if (warningSprite != null)
            {
                return warningSprite;
            }

            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "12-3 Wave Warning",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color clear = Color.clear;
            Color ink = Color.white;
            Vector2 center = new Vector2(23.5f, 23.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x, y) - center;
                    float radius = d.magnitude;
                    bool rays = (Mathf.Abs(d.x) < 1.8f || Mathf.Abs(d.y) < 1.8f || Mathf.Abs(Mathf.Abs(d.x) - Mathf.Abs(d.y)) < 1.4f)
                        && radius > 12f && radius < 22f;
                    bool mark = (Mathf.Abs(d.x) < 3.2f && d.y > -3f && d.y < 11f)
                        || (d - new Vector2(0f, -9f)).sqrMagnitude < 10f;
                    texture.SetPixel(x, y, rays || mark ? ink : clear);
                }
            }
            texture.Apply(false, true);
            warningSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            warningSprite.name = "12-3 Wave Warning Sprite";
            warningSprite.hideFlags = HideFlags.HideAndDontSave;
            return warningSprite;
        }

        private static AudioClip GetPickupClip()
        {
            if (pickupClip != null)
            {
                return pickupClip;
            }

            const int sampleRate = 22050;
            const int sampleCount = 1764;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 27f);
                samples[i] = (Mathf.Sin(t * Mathf.PI * 2f * 880f)
                    + Mathf.Sin(t * Mathf.PI * 2f * 1320f) * 0.42f) * envelope * 0.34f;
            }
            pickupClip = AudioClip.Create("12-3 Coin Jingle", sampleCount, 1, sampleRate, false);
            pickupClip.SetData(samples, 0);
            return pickupClip;
        }
    }
}
