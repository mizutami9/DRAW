using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class OnlinePlayerSync : MonoBehaviour
    {
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private float sendRate = 30f;
        [SerializeField] private float remoteSmoothRate = 18f;
        [SerializeField] private float remotePredictionSeconds = 0.075f;
        [SerializeField] private float remoteSnapDistance = 1.75f;

        private float nextSendTime;
        private int nextStateSequence;
        private float nextBodyResyncTime;
        private bool bodyResyncPending;
        private int bodyResyncAttempts;
        private DrawManager.Species? lastLocalSpecies;
        private OnlineConnectionState lastBodySyncConnectionState = OnlineConnectionState.Offline;
        private string lastBodySyncRoster = string.Empty;
        private OnlineLobbyInfo pendingRosterLobby;
        private string pendingRosterLocalPlayerId;
        private readonly Dictionary<string, RemoteTarget> remoteTargets = new Dictionary<string, RemoteTarget>();
        private readonly Dictionary<string, int> lastRemoteSequences = new Dictionary<string, int>();
        private readonly Dictionary<string, float> lastRemoteStateReceivedAt = new Dictionary<string, float>();
        private readonly Dictionary<string, OnlineBodyData> pendingRemoteBodyData =
            new Dictionary<string, OnlineBodyData>();
        private readonly Dictionary<string, int> lastRemoteBodyRevisions =
            new Dictionary<string, int>();
        private readonly Dictionary<string, float> lastRemoteBodyReceivedAt =
            new Dictionary<string, float>();

        private sealed class RemoteTarget
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public bool Redrawing;
            public bool TurtleShelled;
            public string SlimeAttachedToPlayerId;
        }

        private void Awake()
        {
            if (onlineManager == null)
            {
                onlineManager = FindFirstObjectByType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }
        }

        private void OnEnable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged += HandleOnlineStateChanged;
                onlineManager.PlayerStateReceived += ApplyRemoteState;
                onlineManager.BodyDataReceived += ApplyRemoteBodyData;
                onlineManager.CarryDataReceived += ApplyRemoteCarryData;
            }

            if (onlineManager != null
                && (onlineManager.State == OnlineConnectionState.InLobby
                    || onlineManager.State == OnlineConnectionState.Matching
                    || onlineManager.State == OnlineConnectionState.Playing))
            {
                RequestBodyResync();
            }
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged -= HandleOnlineStateChanged;
                onlineManager.PlayerStateReceived -= ApplyRemoteState;
                onlineManager.BodyDataReceived -= ApplyRemoteBodyData;
                onlineManager.CarryDataReceived -= ApplyRemoteCarryData;
            }
        }

        private void Update()
        {
            if (onlineManager == null || stageManager == null)
            {
                return;
            }

            if (onlineManager.State != OnlineConnectionState.InLobby
                && onlineManager.State != OnlineConnectionState.Matching
                && onlineManager.State != OnlineConnectionState.Playing)
            {
                return;
            }

            ApplyRemoteTarget();
            FlushPendingRosterSync();
            FlushPendingRemoteBodyData();
            DetectLocalSpeciesChange();
            FlushPendingBodyResync();

            if (Time.unscaledTime < nextSendTime)
            {
                return;
            }

            nextSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            Transform localTransform = stageManager.ActivePlayerTransform;
            if (localTransform == null)
            {
                return;
            }

            Rigidbody2D body = stageManager.ActivePlayerBody;
            PlayerCarryController localCarry = localTransform.GetComponent<PlayerCarryController>();
            onlineManager.SendPlayerState(new OnlinePlayerState
            {
                PlayerId = onlineManager.LocalPlayerId,
                Sequence = ++nextStateSequence,
                Position = localTransform.position,
                Velocity = body != null ? body.linearVelocity : Vector2.zero,
                Rotation = body != null ? body.rotation : localTransform.eulerAngles.z,
                Redrawing = stageManager.IsDrawingMode,
                Respawning = stageManager.IsPlayerRespawning(localTransform.GetComponent<PlayerController2D>()),
                TurtleShelled = localTransform.GetComponent<PlayerController2D>()?.IsTurtleShelled ?? false,
                SlimeAttachedToPlayerId = localCarry?.SlimeAttachedOnlinePlayerId,
                CarriedPlayerId = localCarry?.CurrentOnlineCarriedPlayerId,
                CarryAction = localCarry?.CurrentOnlineCarryAction,
                CarryOffset = localCarry != null ? localCarry.CurrentOnlineCarryOffset : Vector2.zero
            });
        }

        private void ApplyRemoteState(OnlinePlayerState state)
        {
            if (state == null || onlineManager == null || stageManager == null)
            {
                return;
            }

            if (state.PlayerId == onlineManager.LocalPlayerId)
            {
                return;
            }
            if (state.Sequence > 0
                && lastRemoteSequences.TryGetValue(state.PlayerId, out int lastSequence)
                && state.Sequence <= lastSequence
                && (state.Sequence == lastSequence
                    || lastRemoteStateReceivedAt.TryGetValue(state.PlayerId, out float lastReceivedAt)
                        && Time.unscaledTime - lastReceivedAt < 2f))
            {
                return;
            }
            if (state.Sequence > 0)
            {
                lastRemoteSequences[state.PlayerId] = state.Sequence;
            }
            lastRemoteStateReceivedAt[state.PlayerId] = Time.unscaledTime;
            stageManager.ApplyOnlineRemoteRedrawing(state.PlayerId, state.Redrawing);
            stageManager.ReconcileOnlineCarryState(
                state.PlayerId,
                state.Redrawing ? null : state.CarriedPlayerId,
                state.Redrawing ? string.Empty : state.CarryAction,
                state.Redrawing ? Vector2.zero : state.CarryOffset,
                onlineManager.LocalPlayerId,
                state.Redrawing ? Vector2.zero : state.Velocity);
            if (stageManager.IsOnlineRemotePlayerHeldByLocal(state.PlayerId))
            {
                remoteTargets.Remove(state.PlayerId);
                return;
            }

            remoteTargets[state.PlayerId] = new RemoteTarget
            {
                Position = state.Position,
                Velocity = state.Velocity,
                Rotation = state.Rotation,
                Redrawing = state.Redrawing,
                TurtleShelled = !state.Redrawing && state.TurtleShelled,
                SlimeAttachedToPlayerId = state.Redrawing ? null : state.SlimeAttachedToPlayerId
            };
            ApplyLobbyColors(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
        }

        private void ApplyRemoteTarget()
        {
            if (stageManager == null)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-remoteSmoothRate * Time.unscaledDeltaTime);
            foreach (KeyValuePair<string, RemoteTarget> pair in remoteTargets)
            {
                if (stageManager.IsOnlineRemotePlayerHeldByLocal(pair.Key))
                {
                    continue;
                }
                Transform remoteTransform = stageManager.GetOnlinePlayerTransform(pair.Key);
                Vector2 predictedPosition = pair.Value.Position
                    + pair.Value.Velocity * Mathf.Clamp(remotePredictionSeconds, 0f, 0.15f);
                Vector2 position = remoteTransform != null
                    ? Vector2.Distance(remoteTransform.position, predictedPosition) >= remoteSnapDistance
                        ? predictedPosition
                        : Vector2.Lerp(remoteTransform.position, predictedPosition, t)
                    : predictedPosition;
                float rotation = remoteTransform != null
                    ? Mathf.LerpAngle(remoteTransform.eulerAngles.z, pair.Value.Rotation, t)
                    : pair.Value.Rotation;
                stageManager.ApplyOnlineRemoteState(pair.Key, position, pair.Value.Velocity, rotation);
                if (remoteTransform != null)
                {
                    remoteTransform.GetComponent<PlayerController2D>()?.ApplyRemoteTurtleShellState(pair.Value.TurtleShelled);
                    PlayerController2D attachmentTarget = string.IsNullOrEmpty(pair.Value.SlimeAttachedToPlayerId)
                        ? null
                        : stageManager.GetOnlinePlayerController(pair.Value.SlimeAttachedToPlayerId);
                    remoteTransform.GetComponent<PlayerCarryController>()?.ApplyRemoteSlimeAttachment(attachmentTarget);
                }
            }
        }

        private void ApplyRemoteBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || onlineManager == null || stageManager == null)
            {
                return;
            }

            if (bodyData.PlayerId == onlineManager.LocalPlayerId)
            {
                return;
            }

            if (bodyData.Revision > 0
                && lastRemoteBodyRevisions.TryGetValue(bodyData.PlayerId, out int appliedRevision)
                && bodyData.Revision <= appliedRevision
                && (bodyData.Revision == appliedRevision
                    || lastRemoteBodyReceivedAt.TryGetValue(bodyData.PlayerId, out float bodyReceivedAt)
                        && Time.unscaledTime - bodyReceivedAt < 2f))
            {
                return;
            }

            if (stageManager.IsDrawingMode || stageManager.IsOnlineBodyRebuildBlocked(bodyData.PlayerId))
            {
                // StageManager temporarily points the shared DrawManager at the
                // remote body while rebuilding it. Doing that during a local mouse
                // stroke resets DrawManager's in-progress stroke flag, so retain
                // only the newest body for this player until DRAW closes.
                if (!pendingRemoteBodyData.TryGetValue(bodyData.PlayerId, out OnlineBodyData pending)
                    || bodyData.Revision <= 0
                    || pending.Revision <= 0
                    || bodyData.Revision > pending.Revision)
                {
                    pendingRemoteBodyData[bodyData.PlayerId] = bodyData;
                }
                return;
            }

            stageManager.ApplyOnlineRemoteBodyData(bodyData);
            if (bodyData.Revision > 0)
                lastRemoteBodyRevisions[bodyData.PlayerId] = bodyData.Revision;
            lastRemoteBodyReceivedAt[bodyData.PlayerId] = Time.unscaledTime;
            ApplyLobbyColors(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
        }

        private void FlushPendingRemoteBodyData()
        {
            if (stageManager == null || stageManager.IsDrawingMode || pendingRemoteBodyData.Count == 0)
            {
                return;
            }

            List<string> readyPlayerIds = new List<string>();
            foreach (KeyValuePair<string, OnlineBodyData> pair in pendingRemoteBodyData)
            {
                if (!stageManager.IsOnlineBodyRebuildBlocked(pair.Key))
                {
                    readyPlayerIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < readyPlayerIds.Count; i++)
            {
                string playerId = readyPlayerIds[i];
                OnlineBodyData pending = pendingRemoteBodyData[playerId];
                pendingRemoteBodyData.Remove(playerId);
                ApplyRemoteBodyData(pending);
            }
        }

        private void ApplyRemoteCarryData(OnlineCarryData carryData)
        {
            if (carryData == null || onlineManager == null || stageManager == null)
            {
                return;
            }

            stageManager.ApplyOnlineCarryData(carryData, onlineManager.LocalPlayerId);
        }

        private void HandleOnlineStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            string localPlayerId = onlineManager != null ? onlineManager.LocalPlayerId : string.Empty;
            PruneRemoteTracking(lobby, localPlayerId);
            if (stageManager != null && stageManager.IsDrawingMode)
            {
                // Adding/removing a remote avatar snapshots the shared DrawManager.
                // CreateState intentionally finishes a stroke, so roster recovery
                // must wait until the local player releases the pen.
                pendingRosterLobby = lobby;
                pendingRosterLocalPlayerId = localPlayerId;
            }
            else
            {
                stageManager?.SyncOnlinePlayers(lobby, localPlayerId);
            }
            ApplyLobbyColors(state, lobby, message);

            string roster = BuildRosterSignature(lobby);
            bool connectionChanged = state != lastBodySyncConnectionState;
            bool rosterChanged = roster != lastBodySyncRoster;
            lastBodySyncConnectionState = state;
            lastBodySyncRoster = roster;
            if ((state == OnlineConnectionState.InLobby
                    || state == OnlineConnectionState.Matching
                    || state == OnlineConnectionState.Playing)
                && (connectionChanged || rosterChanged))
            {
                RequestBodyResync();
            }
        }

        private void PruneRemoteTracking(OnlineLobbyInfo lobby, string localPlayerId)
        {
            HashSet<string> active = new HashSet<string>();
            if (lobby?.Players != null)
            {
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    string id = lobby.Players[i]?.PlayerId;
                    if (!string.IsNullOrEmpty(id) && id != localPlayerId) active.Add(id);
                }
            }
            List<string> removed = new List<string>();
            foreach (string id in lastRemoteSequences.Keys)
                if (!active.Contains(id)) removed.Add(id);
            for (int i = 0; i < removed.Count; i++)
            {
                string id = removed[i];
                remoteTargets.Remove(id);
                lastRemoteSequences.Remove(id);
                lastRemoteStateReceivedAt.Remove(id);
                lastRemoteBodyRevisions.Remove(id);
                lastRemoteBodyReceivedAt.Remove(id);
                pendingRemoteBodyData.Remove(id);
            }
        }

        private void FlushPendingRosterSync()
        {
            if (stageManager == null || stageManager.IsDrawingMode || pendingRosterLobby == null)
            {
                return;
            }

            OnlineLobbyInfo lobby = pendingRosterLobby;
            string localPlayerId = pendingRosterLocalPlayerId;
            pendingRosterLobby = null;
            pendingRosterLocalPlayerId = null;
            stageManager.SyncOnlinePlayers(lobby, localPlayerId);
        }

        private static string BuildRosterSignature(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null || lobby.Players.Length == 0)
            {
                return string.Empty;
            }

            List<string> ids = new List<string>();
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                if (lobby.Players[i] != null && !string.IsNullOrEmpty(lobby.Players[i].PlayerId))
                {
                    ids.Add(lobby.Players[i].PlayerId);
                }
            }
            ids.Sort(System.StringComparer.Ordinal);
            return string.Join("|", ids);
        }

        private void RequestBodyResync()
        {
            RequestBodyResync(1);
        }

        private void RequestBodyResync(int attempts)
        {
            bodyResyncPending = true;
            bodyResyncAttempts = Mathf.Max(bodyResyncAttempts, Mathf.Max(1, attempts));
            nextBodyResyncTime = Mathf.Min(
                nextBodyResyncTime > 0f ? nextBodyResyncTime : float.PositiveInfinity,
                Time.unscaledTime + 0.25f);
        }

        private void DetectLocalSpeciesChange()
        {
            Transform local = stageManager != null ? stageManager.ActivePlayerTransform : null;
            BodyBuilder builder = local != null ? local.GetComponent<BodyBuilder>() : null;
            if (builder == null) return;

            DrawManager.Species species = builder.BuiltSpecies;
            if (lastLocalSpecies.HasValue && lastLocalSpecies.Value == species) return;
            lastLocalSpecies = species;
            RequestBodyResync(3);
        }

        private void FlushPendingBodyResync()
        {
            if (!bodyResyncPending
                || stageManager != null && stageManager.IsDrawingMode
                || Time.unscaledTime < nextBodyResyncTime)
            {
                return;
            }

            stageManager?.SendLocalOnlineBodyData();
            bodyResyncAttempts = Mathf.Max(0, bodyResyncAttempts - 1);
            bodyResyncPending = bodyResyncAttempts > 0;
            nextBodyResyncTime = bodyResyncPending
                ? Time.unscaledTime + 0.65f
                : 0f;
        }

        private void ApplyLobbyColors(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            if (stageManager == null || onlineManager == null)
            {
                return;
            }

            stageManager.ApplyOnlinePlayerColors(lobby, onlineManager.LocalPlayerId, stageManager.RemotePlayerId);
        }

    }
}
