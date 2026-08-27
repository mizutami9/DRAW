using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>Marker and shared zero-friction material for editable ice terrain.</summary>
    [DisallowMultipleComponent]
    public sealed class StageIceSurface : MonoBehaviour
    {
        private static PhysicsMaterial2D iceMaterial;

        public static PhysicsMaterial2D GetMaterial()
        {
            if (iceMaterial == null)
            {
                iceMaterial = new PhysicsMaterial2D("Stage Ice")
                {
                    friction = 0f,
                    bounciness = 0f,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return iceMaterial;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageIceSpeedrunController : MonoBehaviour
    {
        private const string StageId = "10-1";
        private const string StateKind = "ice_speedrun_state";
        private const string MissileKind = "ice_speedrun_missile";
        private const float IntroSeconds = 3f;
        private const float RetryDelay = 3.5f;

        [System.Serializable]
        private sealed class SpeedrunState
        {
            public float Remaining;
            public float IntroRemaining;
            public float StartFlashRemaining;
            public float RetryRemaining;
            public bool Started;
            public bool Failed;
        }

        [System.Serializable]
        private sealed class MissileState
        {
            public int Sequence;
            public Vector2 Position;
            public Vector2 Direction;
            public float Speed;
            public float Size;
        }

        private readonly List<TextMesh> clocks = new List<TextMesh>();
        private readonly HashSet<int> receivedMissiles = new HashSet<int>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageGimmickSyncManager syncManager;
        private float remaining = 25f;
        private float introRemaining = IntroSeconds;
        private float startFlashRemaining;
        private float retryRemaining;
        private float nextBroadcastAt;
        private float nextMissileAt;
        private int missileSequence;
        private int missileVolleySequence;
        private bool failed;
        private bool started;
        private bool locallyAppliedStart;
        private bool locallyAppliedFailure;

        public bool CanFinish => started && !failed && remaining > 0f;
        private bool HasAuthority => syncManager == null || !syncManager.IsOnlineActive || syncManager.IsHost;

        public void Configure(float seconds)
        {
            remaining = Mathf.Clamp(seconds > 0f ? seconds : 25f, 5f, 120f);
        }

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
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            BuildClocks();
            SetAllPlayerControls(false);
            RefreshClocks();
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (!stageManager.IsGameplayActive)
            {
                RefreshClocks();
                return;
            }

            if (!started) SetAllPlayerControls(false);

            if (HasAuthority)
            {
                if (!started)
                {
                    introRemaining = Mathf.Max(0f, introRemaining - Time.deltaTime);
                    if (introRemaining <= 0f)
                    {
                        started = true;
                        startFlashRemaining = 0.7f;
                        nextMissileAt = Time.time + 1.2f;
                        ApplyStartLocally();
                        Broadcast(true);
                    }
                }
                else if (!failed)
                {
                    startFlashRemaining = Mathf.Max(0f, startFlashRemaining - Time.deltaTime);
                    remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                    if (Time.time >= nextMissileAt) FireMissileVolley();
                    if (remaining <= 0f)
                    {
                        failed = true;
                        retryRemaining = RetryDelay;
                        ApplyFailureLocally();
                        Broadcast(true);
                    }
                }
                else
                {
                    retryRemaining = Mathf.Max(0f, retryRemaining - Time.deltaTime);
                    if (retryRemaining <= 0f) stageManager.Retry();
                }
                Broadcast(false);
            }
            else if (started && !locallyAppliedStart)
            {
                ApplyStartLocally();
            }
            if (!HasAuthority && failed)
            {
                ApplyFailureLocally();
            }

            RefreshClocks();
        }

        private void ApplyStartLocally()
        {
            if (locallyAppliedStart) return;
            locallyAppliedStart = true;
            SetAllPlayerControls(true);
        }

        private static void SetAllPlayerControls(bool enabled)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (!enabled) players[i].ResetMotion();
                players[i].SetControlsEnabled(enabled);
            }
        }

        private void ApplyFailureLocally()
        {
            if (locallyAppliedFailure) return;
            locallyAppliedFailure = true;
            SetAllPlayerControls(false);
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId) return;
            if (data.Kind == MissileKind)
            {
                if (!HasAuthority) ApplyMissile(JsonUtility.FromJson<MissileState>(data.Json));
                return;
            }
            if (data.Kind != StateKind || HasAuthority) return;
            SpeedrunState snapshot = JsonUtility.FromJson<SpeedrunState>(data.Json);
            if (snapshot == null) return;
            remaining = snapshot.Remaining;
            introRemaining = snapshot.IntroRemaining;
            startFlashRemaining = snapshot.StartFlashRemaining;
            retryRemaining = snapshot.RetryRemaining;
            started = snapshot.Started;
            failed = snapshot.Failed;
            if (started) ApplyStartLocally();
            else SetAllPlayerControls(false);
            if (failed) ApplyFailureLocally();
        }

        private void FireMissileVolley()
        {
            // U-turn 5 onward is always under fire. Deeper tiers deliberately fire more often.
            float[] tierY = { -34.5f, -42.4f, -50.3f, -58.2f, -66.1f, -74.0f };
            int[] cadence = { 4, 3, 2, 1, 1, 1 };
            int[] shotCount = { 1, 1, 1, 1, 2, 3 };
            int volley = missileVolleySequence++;
            for (int tier = 0; tier < tierY.Length; tier++)
            {
                if (volley % cadence[tier] != 0) continue;
                for (int shot = 0; shot < shotCount[tier]; shot++)
                {
                    float spread = shotCount[tier] <= 1
                        ? ((volley + tier) % 2 == 0 ? -1.05f : 1.05f)
                        : Mathf.Lerp(-1.45f, 1.45f, shot / (float)(shotCount[tier] - 1));
                    SpawnMissile(tierY[tier] + spread, tier, shot);
                }
            }
            nextMissileAt = Time.time + 1.25f;
        }

        private void SpawnMissile(float y, int tier, int shot)
        {
            int shotIndex = missileSequence;
            bool fromLeft = (shotIndex + tier + shot) % 2 == 0;
            MissileState state = new MissileState
            {
                Sequence = ++missileSequence,
                Position = new Vector2(fromLeft ? -9.3f : 9.3f, y),
                Direction = fromLeft ? Vector2.right : Vector2.left,
                Speed = 6.8f + tier * 0.38f,
                Size = Mathf.Lerp(0.72f, 1.0f, tier / 5f)
            };
            ApplyMissile(state);
            if (syncManager != null && syncManager.IsOnlineActive && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = MissileKind,
                    Json = JsonUtility.ToJson(state)
                });
            }
        }

        private void ApplyMissile(MissileState state)
        {
            if (state == null || !receivedMissiles.Add(state.Sequence)) return;
            StageMissileProjectile.Create(
                transform,
                transform,
                state.Position,
                state.Direction,
                state.Speed,
                true,
                state.Size);
            GameSfx.PlayAt(SfxId.CannonFire, state.Position, 0.72f);
        }

        private void Broadcast(bool force)
        {
            if (syncManager == null || !syncManager.IsOnlineActive || !syncManager.IsHost || onlineManager == null) return;
            if (!force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.18f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new SpeedrunState
                {
                    Remaining = remaining,
                    IntroRemaining = introRemaining,
                    StartFlashRemaining = startFlashRemaining,
                    RetryRemaining = retryRemaining,
                    Started = started,
                    Failed = failed
                })
            });
        }

        private void BuildClocks()
        {
            Vector2[] positions =
            {
                new Vector2(0f, 1.2f), new Vector2(0f, -9.8f),
                new Vector2(0f, -17.8f), new Vector2(0f, -25.8f),
                new Vector2(0f, -41.5f), new Vector2(0f, -57.2f),
                new Vector2(0f, -72.9f), new Vector2(0f, -77.5f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject monitor = new GameObject("10-1 Speed Clock " + i);
                monitor.transform.SetParent(transform, false);
                monitor.transform.position = new Vector3(positions[i].x, positions[i].y, 0.3f);
                DoodleMonitorVisuals.Build(monitor.transform,
                    new Vector2(i == 0 ? 7.2f : 4.4f, i == 0 ? 1.9f : 1.45f), 1);
                TextMesh clock = CreateText(monitor.transform, i == 0 ? 0.18f : 0.13f, 7);
                clocks.Add(clock);
            }
        }

        private void RefreshClocks()
        {
            string value;
            if (!started)
                value = Mathf.Max(1, Mathf.CeilToInt(introRemaining)).ToString();
            else if (startFlashRemaining > 0f)
                value = LocalizationManager.T("ice_speedrun_start");
            else if (failed)
                value = LocalizationManager.Format("ice_speedrun_time_up", Mathf.CeilToInt(retryRemaining));
            else
                value = remaining.ToString("00.0");
            Color color = failed
                ? new Color(1f, 0.28f, 0.18f)
                : !started || startFlashRemaining > 0f
                    ? new Color(1f, 0.82f, 0.2f)
                    : remaining <= 5f ? new Color(1f, 0.7f, 0.12f) : new Color(0.2f, 0.95f, 1f);
            for (int i = 0; i < clocks.Count; i++)
            {
                if (clocks[i] == null) continue;
                clocks[i].text = value;
                clocks[i].color = color;
            }
        }

        private static TextMesh CreateText(Transform parent, float size, int order)
        {
            GameObject obj = new GameObject("Clock Text");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0f, -0.08f, -0.08f);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = size;
            Font font = DoodleRuntimeAssets.HandwrittenFont;
            if (font != null)
            {
                text.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            obj.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }
    }
}
