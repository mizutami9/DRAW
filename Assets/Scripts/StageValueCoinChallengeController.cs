using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageValueCoinChallengeController : MonoBehaviour
    {
        private const string StageId = "12-1";
        private const string BreakKind = "value_crate_break";
        private const string CollectRequestKind = "value_coin_collect_request";
        private const string CollectKind = "value_coin_collect";
        private const string SnapshotKind = "value_coin_snapshot";
        private const int TargetValue = 100;
        private const int CrateColumns = 24;
        private const int CrateRows = 7;
        private const int BaseCrateCount = CrateColumns * CrateRows;

        [System.Serializable]
        private sealed class ItemMessage
        {
            public string Id;
            public int Value;
            public int Total;
        }

        [System.Serializable]
        private sealed class ChallengeSnapshot
        {
            public int Sequence;
            public int Total;
            public float Remaining;
            public int Phase;
            public string[] Broken;
            public string[] Collected;
        }

        private readonly Dictionary<string, StageValueCrate> crates = new Dictionary<string, StageValueCrate>();
        private readonly Dictionary<string, int> crateValues = new Dictionary<string, int>();
        private readonly Dictionary<string, StageValueCoin> coins = new Dictionary<string, StageValueCoin>();
        private readonly HashSet<string> brokenIds = new HashSet<string>();
        private readonly HashSet<string> collectedIds = new HashSet<string>();
        private readonly List<TextMesh> amountTexts = new List<TextMesh>();
        private readonly List<TextMesh> timeTexts = new List<TextMesh>();
        private readonly List<TextMesh> stateTexts = new List<TextMesh>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private StageObjectFactory factory;
        private StageGimmickSyncManager syncManager;
        private StageBombDropper bombLauncher;
        private StageMissileLauncher missileLauncher;
        private StageOscillatingAim bombAim;
        private StageOscillatingAim missileAim;
        private float remaining = 60f;
        private float configuredTimeLimit = 60f;
        private float retryAt;
        private float nextSnapshotAt;
        private int total;
        private int phase;
        private int sequence;
        private int appliedSequence = -1;
        private float startCountdownRemaining = 3f;
        private float startFlashUntil;
        private bool startCountdownActive;
        private PlayerController2D countdownPlayer;

        public bool HasAuthority => stageManager == null || !stageManager.IsOnlineStageActive || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
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
            if (countdownPlayer != null) countdownPlayer.SetControlsEnabled(true);
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            remaining = ResolveTimeLimit();
            configuredTimeLimit = remaining;
            BuildCrateField();
            ConfigureLaunchers();
            CreateMonitors();
            BeginStartCountdown();
            RefreshMonitors();
            if (HasAuthority) BroadcastSnapshot(true);
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;
            RefreshLauncherAimClock();
            if (UpdateStartCountdown()) return;
            if (!HasAuthority) return;
            if (phase == 0)
            {
                remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                if (remaining <= 0f)
                {
                    phase = 1;
                    retryAt = Time.time + 5f;
                    BroadcastSnapshot(true);
                }
                else
                {
                    BroadcastSnapshot(false);
                }
                RefreshMonitors();
            }
            else if (phase == 1 && Time.time >= retryAt)
            {
                stageManager.Retry();
            }
        }

        public void RequestBreak(StageValueCrate crate, Vector2 hitPoint)
        {
            if (crate == null || phase != 0 || !HasAuthority) return;
            ApplyBreak(crate.CrateId, crate.Value, hitPoint, true);
        }

        public void RequestCollect(string coinId)
        {
            if (string.IsNullOrEmpty(coinId) || phase != 0) return;
            if (!HasAuthority)
            {
                onlineManager?.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = coinId,
                    Kind = CollectRequestKind,
                    Json = JsonUtility.ToJson(new ItemMessage { Id = coinId })
                });
                return;
            }
            ApplyCollect(coinId, true);
        }

        public static void BreakCratesInRadius(Vector2 center, float radius)
        {
            StageValueCrate[] candidates = Object.FindObjectsByType<StageValueCrate>(FindObjectsSortMode.None);
            float radiusSquared = radius * radius;
            for (int i = 0; i < candidates.Length; i++)
            {
                StageValueCrate crate = candidates[i];
                if (crate == null || crate.IsBroken) continue;
                Collider2D collider = crate.GetComponentInChildren<Collider2D>();
                Vector2 closest = collider != null ? collider.ClosestPoint(center) : (Vector2)crate.transform.position;
                if ((closest - center).sqrMagnitude <= radiusSquared) crate.Hit(closest);
            }
        }

        private void BuildCrateField()
        {
            if (factory == null) return;
            int playerCount = ResolvePlayerCount();
            int crateCount = Mathf.Max(BaseCrateCount,
                Mathf.RoundToInt(BaseCrateCount * Mathf.Pow(1.3f, playerCount - 1)));
            List<int> values = BuildGuaranteedValues(crateCount);
            Dictionary<string, GameObject> authoredCrates = new Dictionary<string, GameObject>();
            StageEditorObject[] authoredObjects = Object.FindObjectsByType<StageEditorObject>(FindObjectsSortMode.None);
            for (int i = 0; i < authoredObjects.Length; i++)
            {
                StageEditorObject marker = authoredObjects[i];
                if (marker != null && !string.IsNullOrEmpty(marker.objectId)
                    && marker.objectId.StartsWith(StageId + "_crate_", System.StringComparison.Ordinal))
                {
                    authoredCrates[marker.objectId] = marker.gameObject;
                }
            }
            StageObjectType[] shapes =
            {
                StageObjectType.WoodBox,
                StageObjectType.TriangleBox,
                StageObjectType.Ball,
                StageObjectType.Barrel,
                StageObjectType.RubberBox
            };
            const float spacingX = 1.78f;
            const float spacingY = 1.72f;
            float startX = -(CrateColumns - 1) * spacingX * 0.5f;
            float startY = -10.65f;
            for (int index = 0; index < crateCount; index++)
            {
                int layoutColumns = playerCount > 1 ? 30 : CrateColumns;
                int row = index / layoutColumns;
                int column = index % layoutColumns;
                Vector2 position;
                float size;
                if (playerCount > 1)
                {
                    int layoutRows = Mathf.CeilToInt(crateCount / (float)layoutColumns);
                    float compactSpacingX = 1.5f;
                    float compactSpacingY = layoutRows > 1 ? Mathf.Min(1.45f, 10.8f / (layoutRows - 1f)) : 1.45f;
                    position = new Vector2(
                        -(layoutColumns - 1) * compactSpacingX * 0.5f + column * compactSpacingX,
                        startY + row * compactSpacingY);
                    size = Mathf.Clamp(compactSpacingY * 0.82f, 0.72f, 1.24f);
                }
                else if (index < BaseCrateCount)
                {
                    position = new Vector2(startX + column * spacingX, startY + row * spacingY);
                    size = 1.5f + ((index * 13) % 4) * 0.06f;
                }
                else
                {
                    int extraIndex = index - BaseCrateCount;
                    const int extraColumns = 30;
                    const float extraSpacingX = 1.54f;
                    const float extraSpacingY = 1.48f;
                    int extraRow = extraIndex / extraColumns;
                    int extraColumn = extraIndex % extraColumns;
                    position = new Vector2(
                        -(extraColumns - 1) * extraSpacingX * 0.5f + extraColumn * extraSpacingX,
                        1.05f + extraRow * extraSpacingY);
                    size = 1.18f + ((index * 13) % 3) * 0.05f;
                }

                string id = StageId + "_crate_" + index.ToString("D3");
                StageObjectType shape = shapes[(index * 7 + row) % shapes.Length];
                StageObjectData data = StageObjectFactory.CreateDefaultData(shape, position);
                data.objectId = id;
                data.size = new Vector2(size, size);
                data.rotation = shape == StageObjectType.TriangleBox && (index & 1) == 1 ? 180f : 0f;
                GameObject crateObject = authoredCrates.TryGetValue(id, out GameObject authored)
                    ? authored
                    : factory.Create(data, transform);
                if (crateObject == null) continue;
                if (playerCount > 1)
                {
                    crateObject.transform.position = position;
                    crateObject.transform.localScale = new Vector3(size, size, 1f);
                }
                Rigidbody2D body = crateObject.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.gravityScale = 1f;
                    body.mass = Mathf.Max(1.4f, size * size);
                    body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                    body.sleepMode = RigidbodySleepMode2D.StartAwake;
                    body.WakeUp();
                }
                if (crateObject.GetComponent<CarryableObject>() == null)
                    crateObject.AddComponent<CarryableObject>();
                StageValueCrate crate = crateObject.GetComponent<StageValueCrate>();
                if (crate == null) crate = crateObject.AddComponent<StageValueCrate>();
                crate.Configure(this, id, values[index]);
                crates[id] = crate;
                crateValues[id] = values[index];
                if (index >= BaseCrateCount) syncManager?.RegisterRuntimeObject(crateObject.transform);
            }
        }

        private static List<int> BuildGuaranteedValues(int count)
        {
            List<int> values = new List<int>(count);
            for (int i = 0; i < count; i++) values.Add(0);
            int cursor = 0;
            float scale = count / (float)BaseCrateCount;
            for (int i = 0; i < Mathf.RoundToInt(4 * scale) && cursor < count; i++) values[cursor++] = 10;
            for (int i = 0; i < Mathf.RoundToInt(8 * scale) && cursor < count; i++) values[cursor++] = 5;
            for (int i = 0; i < Mathf.RoundToInt(10 * scale) && cursor < count; i++) values[cursor++] = 3;
            for (int i = 0; i < Mathf.RoundToInt(20 * scale) && cursor < count; i++) values[cursor++] = 1;
            uint shuffle = 0x12C01u;
            for (int i = values.Count - 1; i > 0; i--)
            {
                shuffle = shuffle * 1664525u + 1013904223u;
                int swap = (int)(shuffle % (uint)(i + 1));
                int temporary = values[i];
                values[i] = values[swap];
                values[swap] = temporary;
            }
            return values;
        }

        private void ConfigureLaunchers()
        {
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject marker = objects[i];
                if (marker == null) continue;
                if (marker.objectId == "12-1_bomb_launcher")
                {
                    bombAim = marker.GetComponent<StageOscillatingAim>();
                    if (bombAim == null) bombAim = marker.gameObject.AddComponent<StageOscillatingAim>();
                    // BombDropper launches along -transform.up. A 60 degree body
                    // angle therefore aims down-right at roughly -30 degrees.
                    bombAim.Configure(60f, 20f, 1.05f);
                    bombLauncher = marker.GetComponent<StageBombDropper>();
                    bombLauncher?.SetLinkedLaunchTuning(5f, 11.5f);
                }
                else if (marker.objectId == "12-1_missile_launcher")
                {
                    missileAim = marker.GetComponent<StageOscillatingAim>();
                    if (missileAim == null) missileAim = marker.gameObject.AddComponent<StageOscillatingAim>();
                    // Missiles launch along transform.right. Keep the full sweep
                    // down-left so the launcher never points at the ceiling.
                    missileAim.Configure(-150f, 20f, 1.18f);
                    missileLauncher = marker.GetComponent<StageMissileLauncher>();
                    missileLauncher?.SetLinkCooldown(5f);
                }
            }

            StageLauncherTrajectoryPreview preview = GetComponent<StageLauncherTrajectoryPreview>();
            if (preview == null) preview = gameObject.AddComponent<StageLauncherTrajectoryPreview>();
            preview.Configure(bombLauncher, missileLauncher, 28f, -13f, 13f);
        }

        private void RefreshLauncherAimClock()
        {
            float clock = startCountdownActive
                ? Mathf.Max(0f, 3f - startCountdownRemaining)
                : 3f + Mathf.Max(0f, configuredTimeLimit - remaining);
            bombAim?.ApplyExternalClock(clock);
            missileAim?.ApplyExternalClock(clock);
        }

        private int ResolvePlayerCount()
        {
            int reported = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            int spawned = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None).Length;
            return Mathf.Clamp(Mathf.Max(reported, spawned), 1, 4);
        }

        private float ResolveTimeLimit()
        {
            int players = ResolvePlayerCount();
            return players >= 4 ? 30f : players == 3 ? 45f : 60f;
        }

        private void BeginStartCountdown()
        {
            startCountdownRemaining = 3f;
            startCountdownActive = true;
            countdownPlayer = stageManager != null && stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            countdownPlayer?.SetControlsEnabled(false);
            uiManager?.SetChallengeCountdown(true, "3");
        }

        private bool UpdateStartCountdown()
        {
            if (startCountdownActive)
            {
                startCountdownRemaining = Mathf.Max(0f, startCountdownRemaining - Time.unscaledDeltaTime);
                if (startCountdownRemaining > 0f)
                {
                    uiManager?.SetChallengeCountdown(true, Mathf.CeilToInt(startCountdownRemaining).ToString());
                    RefreshMonitors();
                    return true;
                }

                startCountdownActive = false;
                startFlashUntil = Time.unscaledTime + 0.65f;
                countdownPlayer?.SetControlsEnabled(true);
                uiManager?.SetChallengeCountdown(true, LocalizationManager.T("survival_start"));
                RefreshMonitors();
            }
            if (startFlashUntil > 0f && Time.unscaledTime >= startFlashUntil)
            {
                startFlashUntil = 0f;
                uiManager?.SetChallengeCountdown(false, string.Empty);
            }
            return false;
        }

        private void ApplyBreak(string crateId, int value, Vector2 hitPoint, bool broadcast)
        {
            if (string.IsNullOrEmpty(crateId) || !brokenIds.Add(crateId)) return;
            if (crateValues.TryGetValue(crateId, out int configuredValue)) value = configuredValue;
            Vector2 coinPosition = hitPoint;
            if (crates.TryGetValue(crateId, out StageValueCrate crate) && crate != null)
            {
                coinPosition = crate.transform.position;
                crate.ApplyBroken(hitPoint);
            }
            if (value > 0 && !collectedIds.Contains(crateId + "_coin")) SpawnCoin(crateId + "_coin", value, coinPosition);
            if (broadcast && IsOnline())
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = crateId,
                    Kind = BreakKind,
                    Json = JsonUtility.ToJson(new ItemMessage { Id = crateId, Value = value })
                });
            }
        }

        private void SpawnCoin(string coinId, int value, Vector2 position)
        {
            if (coins.ContainsKey(coinId)) return;
            StageValueCoin coin = StageValueCoin.Create(transform, this, coinId, value, position + Vector2.up * 0.18f);
            coins[coinId] = coin;
        }

        private void ApplyCollect(string coinId, bool broadcast)
        {
            if (string.IsNullOrEmpty(coinId) || !collectedIds.Add(coinId)) return;
            if (!coins.TryGetValue(coinId, out StageValueCoin coin) || coin == null)
            {
                collectedIds.Remove(coinId);
                return;
            }
            int value = coin.Value;
            coin.ApplyCollected();
            coins.Remove(coinId);
            total += value;
            GameSfx.PlayAt(SfxId.CoinCollect, coin.transform.position, 0.82f);
            if (total >= TargetValue && phase == 0)
            {
                total = Mathf.Max(total, TargetValue);
                phase = 2;
                stageManager?.ClearStage();
            }
            RefreshMonitors();
            if (broadcast && IsOnline())
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = coinId,
                    Kind = CollectKind,
                    Json = JsonUtility.ToJson(new ItemMessage { Id = coinId, Value = value, Total = total })
                });
            }
            if (HasAuthority) BroadcastSnapshot(true);
        }

        private void CreateMonitors()
        {
            float[] positions = { -17.2f, 0f, 17.2f };
            for (int i = 0; i < positions.Length; i++) CreateMonitor(positions[i]);
            float[] sideHeights = { 4.7f, 0.4f, -4.4f, -9.1f };
            for (int i = 0; i < sideHeights.Length; i++)
            {
                CreateCompactMonitor(-23.7f, sideHeights[i]);
                CreateCompactMonitor(23.7f, sideHeights[i]);
            }
        }

        private void CreateMonitor(float x)
        {
            GameObject monitor = new GameObject("12-1 Value Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(x, 10.45f, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(10.2f, 2.35f), -32);
            TextMesh amount = StageEscortController.CreateText(monitor.transform, "Amount", new Vector3(0f, 0.4f, -0.03f), 54, 0.12f, new Color(0.78f, 0.39f, 0.06f), -26);
            TextMesh time = StageEscortController.CreateText(monitor.transform, "Time", new Vector3(-2.75f, -0.42f, -0.03f), 42, 0.09f, new Color(0.04f, 0.43f, 0.58f), -26);
            TextMesh state = StageEscortController.CreateText(monitor.transform, "State", new Vector3(1.65f, -0.42f, -0.03f), 36, 0.075f, new Color(0.1f, 0.28f, 0.5f), -26);
            amountTexts.Add(amount);
            timeTexts.Add(time);
            stateTexts.Add(state);
        }

        private void CreateCompactMonitor(float x, float y)
        {
            GameObject monitor = new GameObject("12-1 Side Value Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.localPosition = new Vector3(x, y, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(4.2f, 1.65f), -32);
            TextMesh amount = StageEscortController.CreateText(monitor.transform, "Amount", new Vector3(0f, 0.28f, -0.03f), 40, 0.085f, new Color(0.78f, 0.39f, 0.06f), -26);
            TextMesh time = StageEscortController.CreateText(monitor.transform, "Time", new Vector3(0f, -0.34f, -0.03f), 34, 0.07f, new Color(0.04f, 0.43f, 0.58f), -26);
            amountTexts.Add(amount);
            timeTexts.Add(time);
        }

        private void RefreshMonitors()
        {
            string amount = LocalizationManager.Format("value_coin_amount", total, TargetValue);
            string time = LocalizationManager.Format("value_coin_time", Mathf.CeilToInt(remaining));
            string state = startCountdownActive
                ? Mathf.CeilToInt(startCountdownRemaining).ToString()
                : LocalizationManager.T(phase == 1 ? "value_coin_time_up" : phase == 2 ? "value_coin_clear" : "value_coin_hint");
            for (int i = 0; i < amountTexts.Count; i++) amountTexts[i].text = amount;
            for (int i = 0; i < timeTexts.Count; i++) timeTexts[i].text = time;
            for (int i = 0; i < stateTexts.Count; i++) stateTexts[i].text = state;
        }

        private void BroadcastSnapshot(bool immediate)
        {
            if (!HasAuthority || !IsOnline() || !immediate && Time.time < nextSnapshotAt) return;
            nextSnapshotAt = Time.time + 0.75f;
            ChallengeSnapshot snapshot = new ChallengeSnapshot
            {
                Sequence = ++sequence,
                Total = total,
                Remaining = remaining,
                Phase = phase,
                Broken = new List<string>(brokenIds).ToArray(),
                Collected = new List<string>(collectedIds).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = SnapshotKind,
                Json = JsonUtility.ToJson(snapshot)
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (message.Kind == CollectRequestKind && HasAuthority)
            {
                ItemMessage request = JsonUtility.FromJson<ItemMessage>(message.Json);
                if (request != null) ApplyCollect(request.Id, true);
                return;
            }
            if (HasAuthority || onlineManager == null || !onlineManager.IsHostPlayer(message.PlayerId)) return;
            if (message.Kind == BreakKind)
            {
                ItemMessage item = JsonUtility.FromJson<ItemMessage>(message.Json);
                if (item != null) ApplyBreak(item.Id, item.Value, crates.TryGetValue(item.Id, out StageValueCrate crate) && crate != null ? (Vector2)crate.transform.position : Vector2.zero, false);
            }
            else if (message.Kind == CollectKind)
            {
                ItemMessage item = JsonUtility.FromJson<ItemMessage>(message.Json);
                if (item != null)
                {
                    ApplyCollectRemote(item.Id, true);
                    total = item.Total;
                    RefreshMonitors();
                }
            }
            else if (message.Kind == SnapshotKind)
            {
                ApplySnapshot(JsonUtility.FromJson<ChallengeSnapshot>(message.Json));
            }
        }

        private void ApplyCollectRemote(string coinId, bool playFeedback)
        {
            if (!collectedIds.Add(coinId)) return;
            if (coins.TryGetValue(coinId, out StageValueCoin coin) && coin != null)
            {
                Vector3 soundPosition = coin.transform.position;
                coin.ApplyCollected(playFeedback);
                if (playFeedback) GameSfx.PlayAt(SfxId.CoinCollect, soundPosition, 0.82f);
            }
            coins.Remove(coinId);
        }

        private void ApplySnapshot(ChallengeSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Sequence <= appliedSequence) return;
            appliedSequence = snapshot.Sequence;
            total = snapshot.Total;
            remaining = snapshot.Remaining;
            phase = snapshot.Phase;
            if (snapshot.Collected != null)
                for (int i = 0; i < snapshot.Collected.Length; i++) ApplyCollectRemote(snapshot.Collected[i], false);
            if (snapshot.Broken != null)
            {
                for (int i = 0; i < snapshot.Broken.Length; i++)
                {
                    string id = snapshot.Broken[i];
                    int value = crateValues.TryGetValue(id, out int knownValue) ? knownValue : 0;
                    Vector2 point = crates.TryGetValue(id, out StageValueCrate crate) && crate != null ? (Vector2)crate.transform.position : Vector2.zero;
                    ApplyBreak(id, value, point, false);
                }
            }
            RefreshMonitors();
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive && onlineManager != null;
    }

    public sealed class StageValueCrate : MonoBehaviour
    {
        private StageValueCoinChallengeController owner;
        private float nextImpactSfxAt;
        public string CrateId { get; private set; }
        public int Value { get; private set; }
        public bool IsBroken { get; private set; }

        public void Configure(StageValueCoinChallengeController challenge, string crateId, int value)
        {
            owner = challenge;
            CrateId = crateId;
            Value = value;
        }

        public void Hit(Vector2 point)
        {
            if (!IsBroken) owner?.RequestBreak(this, point);
        }

        public void ApplyBroken(Vector2 point)
        {
            if (IsBroken) return;
            IsBroken = true;
            GameSfx.PlayAt(SfxId.CrateBreak, point, 0.72f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsBroken || collision == null || collision.relativeVelocity.sqrMagnitude < 4.84f || Time.time < nextImpactSfxAt)
            {
                return;
            }

            nextImpactSfxAt = Time.time + 0.12f;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            GameSfx.PlayAt(SfxId.CrateImpact, point, Mathf.Clamp(collision.relativeVelocity.magnitude / 7f, 0.7f, 1.2f));
        }
    }

    public sealed class StageValueCoin : MonoBehaviour
    {
        private StageValueCoinChallengeController owner;
        private string coinId;
        private Transform collectorTransform;
        private bool collected;
        public int Value { get; private set; }

        public static StageValueCoin Create(Transform parent, StageValueCoinChallengeController challenge, string id, int value, Vector2 position)
        {
            GameObject root = new GameObject(id);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.layer = 0;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.mass = 0.22f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0.12f;
            body.angularDamping = 1.8f;
            CircleCollider2D solid = root.AddComponent<CircleCollider2D>();
            solid.radius = 0.36f;
            GameObject pickupArea = new GameObject("Pickup Area");
            pickupArea.transform.SetParent(root.transform, false);
            CircleCollider2D trigger = pickupArea.AddComponent<CircleCollider2D>();
            trigger.radius = 0.52f;
            trigger.isTrigger = true;
            StageValueCoin coin = root.AddComponent<StageValueCoin>();
            coin.owner = challenge;
            coin.coinId = id;
            coin.Value = value;
            coin.BuildVisual();
            return coin;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (!collected && player != null)
            {
                collectorTransform = player.transform;
                owner?.RequestCollect(coinId);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null && collision.collider != null
                && collision.collider.GetComponentInParent<PlayerController2D>() is PlayerController2D player)
            {
                collectorTransform = player.transform;
                owner?.RequestCollect(coinId);
            }
        }

        public void ApplyCollected(bool playFeedback = true)
        {
            if (collected) return;
            collected = true;
            if (!playFeedback)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            Color color = Value >= 10
                ? new Color(1f, 0.7f, 0.05f, 1f)
                : Value >= 5 ? new Color(0.78f, 0.86f, 0.94f, 1f)
                : Value >= 3 ? new Color(0.18f, 0.68f, 1f, 1f)
                : new Color(0.82f, 0.38f, 0.12f, 1f);
            CollectibleAbsorbMover.Begin(gameObject, collectorTransform, color);
        }

        private void BuildVisual()
        {
            Color color = Value >= 10
                ? new Color(1f, 0.7f, 0.05f, 1f)
                : Value >= 5 ? new Color(0.78f, 0.86f, 0.94f, 1f)
                : Value >= 3 ? new Color(0.18f, 0.68f, 1f, 1f)
                : new Color(0.82f, 0.38f, 0.12f, 1f);
            Sprite coloredPencilCoin = Resources.Load<Sprite>("StageObjects/NicoDraw/coin");
            if (coloredPencilCoin != null)
            {
                float valueScale = Value >= 10 ? 1f : Value >= 5 ? 0.88f : Value >= 3 ? 0.78f : 0.68f;
                transform.localScale = Vector3.one * valueScale;
                GameObject art = new GameObject("Colored Pencil Value Coin");
                art.transform.SetParent(transform, false);
                art.transform.localPosition = new Vector3(0f, 0f, -0.025f);
                art.transform.localScale = new Vector3(
                    1f / coloredPencilCoin.bounds.size.x,
                    1f / coloredPencilCoin.bounds.size.y,
                    1f);
                SpriteRenderer artRenderer = art.AddComponent<SpriteRenderer>();
                artRenderer.sprite = coloredPencilCoin;
                artRenderer.sortingOrder = 212;
                artRenderer.color = Value >= 10
                    ? Color.white
                    : Color.Lerp(Color.white, color, Value >= 5 ? 0.2f : 0.38f);
                return;
            }
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = 210;
            transform.localScale = Vector3.one * (Value >= 10 ? 1f : Value >= 5 ? 0.88f : Value >= 3 ? 0.78f : 0.68f);
            Color ink = Color.Lerp(color, Color.black, 0.58f);
            AddCoinDisc("Soft Glow", Vector2.zero, 1.24f, new Color(color.r, color.g, color.b, 0.22f), 208);
            AddCoinDisc("Bright Face", Vector2.zero, 0.73f, Color.Lerp(color, Color.white, 0.3f), 211);
            StageGun.AddLine(transform, "Coin Outer Ring", BuildCircle(0.44f), 0.065f, ink, 212);
            AddCoinDot(new Vector2(-0.18f, 0.2f), 0.105f, new Color(1f, 1f, 1f, 0.86f));
            if (Value == 1)
            {
                StageGun.AddLine(transform, "Copper Leaf", new[]
                {
                    new Vector2(-0.2f, -0.13f), new Vector2(0f, 0.17f), new Vector2(0.21f, -0.13f),
                    new Vector2(0f, -0.02f), new Vector2(-0.2f, -0.13f)
                }, 0.075f, ink, 214);
            }
            else if (Value == 3)
            {
                AddCoinDot(new Vector2(-0.2f, -0.05f), 0.12f, ink);
                AddCoinDot(new Vector2(0f, 0.16f), 0.12f, ink);
                AddCoinDot(new Vector2(0.2f, -0.05f), 0.12f, ink);
            }
            else if (Value == 5)
            {
                Color jewel = new Color(0.62f, 0.3f, 0.92f, 1f);
                AddCoinDot(Vector2.zero, 0.13f, jewel);
                AddCoinDot(new Vector2(0f, 0.23f), 0.105f, jewel);
                AddCoinDot(new Vector2(0.22f, 0f), 0.105f, jewel);
                AddCoinDot(new Vector2(0f, -0.23f), 0.105f, jewel);
                AddCoinDot(new Vector2(-0.22f, 0f), 0.105f, jewel);
            }
            else
            {
                StageGun.AddLine(transform, "Gold Star", BuildStar(0.29f, 0.13f), 0.07f, ink, 214);
                AddCoinDot(Vector2.zero, 0.1f, new Color(1f, 0.94f, 0.45f, 1f));
            }
        }

        private void AddCoinDisc(string name, Vector2 position, float size, Color color, int order)
        {
            GameObject disc = new GameObject(name);
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(position.x, position.y, 0.015f);
            disc.transform.localScale = Vector3.one * size;
            SpriteRenderer renderer = disc.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private void AddCoinDot(Vector2 position, float size, Color color)
        {
            GameObject dot = new GameObject("Coin Mark");
            dot.transform.SetParent(transform, false);
            dot.transform.localPosition = new Vector3(position.x, position.y, -0.025f);
            dot.transform.localScale = Vector3.one * size;
            SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = 214;
        }

        private static Vector2[] BuildCircle(float radius)
        {
            const int segments = 24;
            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static Vector2[] BuildStar(float outerRadius, float innerRadius)
        {
            Vector2[] points = new Vector2[11];
            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? outerRadius : innerRadius;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }
    }

    public sealed class StageOscillatingAim : MonoBehaviour
    {
        private float centerAngle;
        private float amplitude;
        private float speed;
        private float phase;
        private bool usesExternalClock;

        public void Configure(float center, float range, float cycles)
        {
            centerAngle = center;
            amplitude = Mathf.Abs(range);
            speed = Mathf.Max(0.1f, cycles);
            phase = center > 120f ? 1.7f : 0f;
        }

        private void Update()
        {
            if (!usesExternalClock) ApplyClock(Time.time);
        }

        public void ApplyExternalClock(float clock)
        {
            usesExternalClock = true;
            ApplyClock(clock);
        }

        private void ApplyClock(float clock)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, centerAngle + Mathf.Sin(clock * speed + phase) * amplitude);
        }
    }
}
