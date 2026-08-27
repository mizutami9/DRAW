using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSpikeChaseController : StageEliminationChallengeController
    {
        private const string StageId = "6-3";
        private const string StateKind = "spike_chase_state";
        private const string EliminateRequestKind = "spike_chase_eliminate_request";
        private const string EliminatedKind = "spike_chase_eliminated";
        private const float StartX = -31.5f;
        private const float WallY = 2.2f;
        private const float StartDelay = 3f;

        [System.Serializable]
        private sealed class ChaseState
        {
            public int Sequence;
            public float WallX;
            public float Elapsed;
            public bool Failed;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class EliminationState { public string PlayerId; }

        private readonly HashSet<string> participantIds = new HashSet<string>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private Transform spikeWall;
        private TextMesh monitorMain;
        private float elapsed;
        private float replicaTargetX = StartX;
        private float nextBroadcastAt;
        private float nextPlayerRefreshAt;
        private int sequence;
        private int receivedSequence;
        private bool failed;
        private bool retryStarted;
        private bool restoredPlayers;
        private bool controlsReleased;
        private PlayerController2D[] players = System.Array.Empty<PlayerController2D>();

        private bool HasAuthority => stageManager == null || !stageManager.IsOnlineStageActive || stageManager.IsOnlineStageHost;
        public bool IsPlayerEliminated(string playerId) => !string.IsNullOrEmpty(playerId) && eliminatedIds.Contains(playerId);

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            RestoreHiddenPlayers();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            BuildWall();
            RefreshPlayers();
            CaptureParticipants();
            SetLocalControls(false);
            RefreshStartSequence();
        }

        private void Update()
        {
            if (spikeWall == null || stageManager == null || stageManager.CurrentStageId != StageId) return;
            if (failed) return;

            RefreshStartSequence();

            if (HasAuthority)
            {
                elapsed += Time.deltaTime;
                if (elapsed > StartDelay)
                {
                    float progress = Mathf.Clamp01((spikeWall.position.x - StartX) / 267f);
                    spikeWall.position += Vector3.right * Mathf.Lerp(6.375f, 9.225f, progress) * Time.deltaTime;
                }
                CheckPlayers();
                if (AreAllPlayersEliminated()) BeginFailure();
                BroadcastState();
            }
            else
            {
                Vector3 position = spikeWall.position;
                position.x = Mathf.Lerp(position.x, replicaTargetX, 1f - Mathf.Exp(-18f * Time.deltaTime));
                spikeWall.position = position;
                CheckLocalPlayer();
            }
        }

        private void RefreshStartSequence()
        {
            float remaining = StartDelay - elapsed;
            if (!controlsReleased && remaining <= 0f)
            {
                controlsReleased = true;
                SetLocalControls(true);
                GameSfx.Play(SfxId.UiToggleOn, 1.15f);
            }

            string main;
            if (remaining > 0f) main = Mathf.CeilToInt(remaining).ToString();
            else if (elapsed < StartDelay + 0.65f) main = LocalizationManager.T("survival_start");
            else main = string.Empty;

            bool overlayVisible = elapsed < StartDelay + 0.65f;
            uiManager?.SetChallengeCountdown(overlayVisible, overlayVisible ? main : string.Empty);
            if (monitorMain != null)
            {
                monitorMain.text = main;
                monitorMain.characterSize = main.Length > 4 ? 0.12f : 0.22f;
            }
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            PlayerController2D active = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            active?.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            if (!stageManager.IsOnlineStageActive)
                stageManager.RemotePlayerController?.SetControlsEnabled(enabled);
        }

        private void CheckPlayers()
        {
            if (Time.unscaledTime >= nextPlayerRefreshAt) RefreshPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                Vector3 p = player.transform.position;
                if (p.x <= spikeWall.position.x + 0.7f || p.y < -11f) RequestElimination(player);
            }
        }

        private void CheckLocalPlayer()
        {
            if (Time.unscaledTime >= nextPlayerRefreshAt) RefreshPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.ControlsEnabled || !player.gameObject.activeInHierarchy) continue;
                Vector3 p = player.transform.position;
                if (p.x <= spikeWall.position.x + 0.45f || p.y < -11f) RequestElimination(player);
            }
        }

        public void Catch(PlayerController2D player) => RequestElimination(player);

        public override void RequestElimination(PlayerController2D player)
        {
            if (player == null || failed) return;
            string id = ResolvePlayerId(player);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;
            if (!stageManager.IsOnlineStageActive) participantIds.Add(id);
            if (stageManager.IsOnlineStageActive)
            {
                if (id != onlineManager?.LocalPlayerId) return;
                if (!HasAuthority)
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = StageId,
                        Kind = EliminateRequestKind,
                        Json = JsonUtility.ToJson(new EliminationState { PlayerId = id })
                    });
                    ApplyElimination(id);
                    return;
                }
            }
            ConfirmElimination(id, stageManager.IsOnlineStageActive);
        }

        private void ConfirmElimination(string id, bool broadcast)
        {
            ApplyElimination(id);
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = StageId,
                    Kind = EliminatedKind,
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

        private void HidePlayer(PlayerController2D player)
        {
            if (player == null || hiddenPlayers.Contains(player)) return;
            player.GetComponent<PlayerCarryController>()?.ForceDrop();
            player.ResetMotion();
            player.SetControlsEnabled(false);
            hiddenPlayers.Add(player);
            player.gameObject.SetActive(false);
        }

        private void BeginFailure()
        {
            if (failed) return;
            failed = true;
            BroadcastState(true);
            if (!retryStarted)
            {
                retryStarted = true;
                StartCoroutine(RetryAfterDelay());
            }
        }

        private IEnumerator RetryAfterDelay()
        {
            yield return new WaitForSeconds(2.2f);
            if (stageManager != null && stageManager.CurrentStageId == StageId && HasAuthority) stageManager.Retry();
        }

        private void RefreshPlayers()
        {
            players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (stageManager != null && !stageManager.IsOnlineStageActive)
                for (int i = 0; i < players.Length; i++) participantIds.Add(ResolvePlayerId(players[i]));
            nextPlayerRefreshAt = Time.unscaledTime + 0.5f;
        }

        private void CaptureParticipants()
        {
            if (stageManager.IsOnlineStageActive)
            {
                OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
                if (lobbyPlayers != null)
                    for (int i = 0; i < lobbyPlayers.Length; i++)
                        if (lobbyPlayers[i] != null && !string.IsNullOrEmpty(lobbyPlayers[i].PlayerId)) participantIds.Add(lobbyPlayers[i].PlayerId);
                return;
            }
            for (int i = 0; i < players.Length; i++) participantIds.Add(ResolvePlayerId(players[i]));
        }

        private bool AreAllPlayersEliminated()
        {
            if (participantIds.Count == 0) return false;
            foreach (string id in participantIds) if (!eliminatedIds.Contains(id)) return false;
            return true;
        }

        private void BroadcastState(bool force = false)
        {
            if (onlineManager == null || stageManager == null || !stageManager.IsOnlineStageActive || !HasAuthority
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            ChaseState state = new ChaseState
            {
                Sequence = ++sequence,
                WallX = spikeWall != null ? spikeWall.position.x : StartX,
                Elapsed = elapsed,
                Failed = failed,
                EliminatedIds = new List<string>(eliminatedIds).ToArray()
            };
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = StageId, Kind = StateKind, Json = JsonUtility.ToJson(state) });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId) return;
            if (message.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationState request = JsonUtility.FromJson<EliminationState>(message.Json);
                string id = request != null ? request.PlayerId : null;
                if (id == message.PlayerId) ConfirmElimination(id, true);
                return;
            }
            if (message.Kind == EliminatedKind && !HasAuthority && IsHost(message.PlayerId))
            {
                EliminationState state = JsonUtility.FromJson<EliminationState>(message.Json);
                if (state != null) ApplyElimination(state.PlayerId);
                return;
            }
            if (message.Kind != StateKind || HasAuthority || !IsHost(message.PlayerId)) return;
            ChaseState chase = JsonUtility.FromJson<ChaseState>(message.Json);
            if (chase == null || chase.Sequence <= receivedSequence) return;
            receivedSequence = chase.Sequence;
            replicaTargetX = chase.WallX;
            elapsed = chase.Elapsed;
            if (chase.EliminatedIds != null)
                for (int i = 0; i < chase.EliminatedIds.Length; i++) ApplyElimination(chase.EliminatedIds[i]);
            failed = chase.Failed;
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return stageManager.IsOnlineStageActive
                ? stageManager.GetOnlinePlayerId(player)
                : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (stageManager.IsOnlineStageActive) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] all = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) if (ResolvePlayerId(all[i]) == id) return all[i];
            return null;
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null) return false;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].IsHost && lobbyPlayers[i].PlayerId == playerId) return true;
            return false;
        }

        private void RestoreHiddenPlayers()
        {
            if (restoredPlayers) return;
            restoredPlayers = true;
            for (int i = 0; i < hiddenPlayers.Count; i++) if (hiddenPlayers[i] != null) hiddenPlayers[i].gameObject.SetActive(true);
            hiddenPlayers.Clear();
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        private void BuildWall()
        {
            GameObject root = new GameObject("6-3 Chasing Spike Wall");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(StartX, WallY, -0.3f);
            spikeWall = root.transform;
            GameObject body = new GameObject("Wall Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
            body.transform.localScale = new Vector3(1.15f, 25f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = DoodleRuntimeAssets.SquareSprite;
            bodyRenderer.color = new Color(0.95f, 0.72f, 0.68f, 0.94f);
            bodyRenderer.sortingOrder = 31;
            AddWallPencilDetails(root.transform);
            for (int i = 0; i < 13; i++) CreateSpike(root.transform, -11.4f + i * 1.9f, i % 2 == 0 ? 1.55f : 1.35f);
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.3f, 25f);
            trigger.offset = new Vector2(0.15f, 0f);
            root.AddComponent<StageSpikeChaseHazard>().Configure(this);

            GameObject monitor = new GameObject("6-3 Start Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(-17f, -5.05f, 0.7f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(7.2f, 2.15f), -34);
            monitorMain = StagePillarSurvivalController.CreateText(
                monitor.transform, "Countdown", new Vector3(0f, -0.05f, -0.03f), 72, 0.22f,
                new Color(0.04f, 0.43f, 0.58f, 1f), -27);
            RefreshStartSequence();
        }

        private static void CreateSpike(Transform parent, float y, float length)
        {
            GameObject obj = new GameObject("Crayon Spike");
            obj.transform.SetParent(parent, false);
            Sprite sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/spike");
            if (sprite != null && sprite.bounds.size.x > 0f && sprite.bounds.size.y > 0f)
            {
                float fullLength = length + 0.42f;
                obj.transform.localPosition = new Vector3((length - 0.42f) * 0.5f, y, -0.02f);
                obj.transform.localRotation = Quaternion.Euler(0f, 0f, -90f + Mathf.Sin(y * 1.71f) * 1.4f);
                obj.transform.localScale = new Vector3(1.56f / sprite.bounds.size.x, fullLength / sprite.bounds.size.y, 1f);
                SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingOrder = 33;
                return;
            }

            obj.transform.localPosition = new Vector3(0.02f, y, -0.02f);
            Mesh mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.42f, -0.78f), new Vector3(-0.42f, 0.78f), new Vector3(length, 0f) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.colors = new[] { new Color(0.92f, 0.17f, 0.2f), new Color(1f, 0.35f, 0.28f), new Color(0.72f, 0.04f, 0.08f) };
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            renderer.sortingOrder = 32;
        }

        private static void AddWallPencilDetails(Transform parent)
        {
            Color graphite = new Color(0.3f, 0.08f, 0.09f, 0.9f);
            StageGun.AddLine(parent, "Crooked Wall Front Edge", new[]
            {
                new Vector2(0.02f, -12.45f), new Vector2(-0.04f, -7f),
                new Vector2(0.04f, -1f), new Vector2(-0.03f, 5.5f), new Vector2(0.03f, 12.45f)
            }, 0.09f, graphite, 34);
            StageGun.AddLine(parent, "Crooked Wall Back Edge", new[]
            {
                new Vector2(-1.1f, -12.45f), new Vector2(-1.14f, -5f),
                new Vector2(-1.07f, 2f), new Vector2(-1.13f, 8f), new Vector2(-1.08f, 12.45f)
            }, 0.075f, graphite, 32);
            for (int i = 0; i < 18; i++)
            {
                float y = -11.8f + i * 1.38f;
                StageGun.AddLine(parent, "Red Wall Pencil Stroke", new[]
                {
                    new Vector2(-1.02f, y - 0.22f), new Vector2(-0.08f, y + 0.22f)
                }, 0.045f, new Color(0.72f, 0.08f, 0.1f, 0.48f), 32);
            }
        }
    }

    public sealed class StageSpikeChaseHazard : MonoBehaviour
    {
        private StageSpikeChaseController owner;
        public void Configure(StageSpikeChaseController value) => owner = value;
        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null) owner?.Catch(player);
        }
    }
}
