using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageGrainRainController : MonoBehaviour
    {
        private const string StageId = "9-3";
        private const string StateKind = "grain_rain_state";
        private const float IntroSeconds = 7f;
        private const float RainSeconds = 10f;
        private const float SettleSeconds = 1.25f;
        private const float SpawnInterval = 0.1f;
        private const float ArenaHalfWidth = 14.4f;
        private const float SpawnY = 12.9f;

        private enum RoundState { Intro, Rain, Settle, Result }

        [System.Serializable]
        private sealed class RainState
        {
            public int State;
            public int SpawnSequence;
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

            targetGrams = 50f * Mathf.Max(1, stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1);
            EnsureCarriers(true);
            BuildMonitor();
            LockCamera();
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
                        remaining = RainSeconds;
                        nextSpawnAt = Time.time + 0.2f;
                    }
                }
                else if (state == RoundState.Rain)
                {
                    remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    while (remaining > 0f && Time.time >= nextSpawnAt)
                    {
                        authoritativeSpawnSequence++;
                        nextSpawnAt += SpawnInterval;
                    }
                    if (remaining <= 0f)
                    {
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
                        if (success) stageManager.ClearStage();
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
            RefreshDisplay();
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
            float size = Mathf.Lerp(0.17f, 0.35f, (float)random.NextDouble());
            size *= Mathf.Lerp(0.92f, 1.18f, grams / 40f);
            float fallSpeed = Mathf.Lerp(0.6f, 3.4f, (float)random.NextDouble());
            float drift = Mathf.Lerp(-0.55f, 0.55f, (float)random.NextDouble());
            float gravity = Mathf.Lerp(0.42f, 0.9f, (float)random.NextDouble());

            SpriteRenderer renderer = StageGrainCarryObjectFactory.AddDot(
                transform, "Rain Grain " + sequence, new Vector2(x, SpawnY), size, color, 72);
            if (grams >= 20f)
            {
                Color glowColor = rainbow ? new Color(1f, 0.85f, 0.2f, 0.3f) : new Color(color.r, color.g, color.b, 0.28f);
                SpriteRenderer glow = StageGrainCarryObjectFactory.AddDot(
                    renderer.transform, "Rare Glow", Vector2.zero, 1.55f, glowColor, 71);
                glow.gameObject.AddComponent<StageRareGrainGlow>();
            }
            StageGrainParticle particle = renderer.gameObject.AddComponent<StageGrainParticle>();
            particle.Configure(DrawManager.Species.Human, new Vector2(drift, -fallSpeed), grams, gravity, true);
            if (rainbow) renderer.gameObject.AddComponent<StageRainbowGrainVisual>();
        }

        private void MeasureRound()
        {
            measuredGrams = 0f;
            List<StageGrainParticle> remove = new List<StageGrainParticle>();
            foreach (StageGrainParticle particle in StageGrainParticle.All)
            {
                if (particle == null) continue;
                bool carried = false;
                foreach (StageGrainCarrier carrier in StageGrainCarrier.All)
                {
                    if (!particle.IsOnGround
                        && carrier != null
                        && carrier.ContainsWorldPoint(particle.transform.position))
                    {
                        carried = true;
                        break;
                    }
                }
                if (carried) measuredGrams += particle.Grams;
                else remove.Add(particle);
            }
            for (int i = 0; i < remove.Count; i++) remove[i].Consume();

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
            state = (RoundState)Mathf.Clamp(snapshot.State, 0, 3);
            authoritativeSpawnSequence = Mathf.Max(authoritativeSpawnSequence, snapshot.SpawnSequence);
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
                State = (int)state,
                SpawnSequence = authoritativeSpawnSequence,
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
            if (state == RoundState.Intro)
            {
                timerText.text = Mathf.CeilToInt(remaining).ToString("00") + ".0";
                scoreText.text = LocalizationManager.Format("grain_rain_target", targetGrams);
            }
            else if (state == RoundState.Rain)
            {
                timerText.text = Mathf.CeilToInt(remaining).ToString("00") + ".0";
                scoreText.text = LocalizationManager.Format("grain_rain_target", targetGrams);
            }
            else if (state == RoundState.Settle)
            {
                timerText.text = LocalizationManager.T("grain_rain_measuring");
                scoreText.text = LocalizationManager.Format("grain_rain_target", targetGrams);
            }
            else
            {
                timerText.text = success ? LocalizationManager.T("grain_rain_clear") : LocalizationManager.T("grain_rain_failed");
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
}
