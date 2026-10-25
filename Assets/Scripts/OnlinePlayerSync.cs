using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class OnlinePlayerSync : MonoBehaviour
    {
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private PlayerEmoteController emoteController;
        [SerializeField] private float sendRate = 30f;
        [SerializeField] private float remoteSmoothRate = 18f;
        [SerializeField] private float remotePredictionSeconds = 0.075f;
        [SerializeField] private float remoteSnapDistance = 1.75f;
        [SerializeField] private float participantMinInterpolationDelay = 0.05f;
        [SerializeField] private float participantMaxInterpolationDelay = 0.1f;

        private const int MaxRemoteSnapshots = 12;
        private const float MaxBufferedExtrapolation = 0.08f;

        private float nextSendTime;
        private int nextStateSequence;
        private readonly Dictionary<string, int> localStateSequences = new Dictionary<string, int>();
        private float nextBodyResyncTime;
        private bool bodyResyncPending;
        private int bodyResyncAttempts;
        private readonly Dictionary<string, DrawManager.Species> lastLocalSpeciesByPlayer =
            new Dictionary<string, DrawManager.Species>();
        private OnlineConnectionState lastBodySyncConnectionState = OnlineConnectionState.Offline;
        private string lastBodySyncRoster = string.Empty;
        private OnlineLobbyInfo pendingRosterLobby;
        private string pendingRosterLocalPlayerId;
        private readonly Dictionary<string, RemoteTarget> remoteTargets = new Dictionary<string, RemoteTarget>();
        private readonly Dictionary<string, int> lastRemoteSequences = new Dictionary<string, int>();
        private readonly Dictionary<string, float> lastRemoteStateReceivedAt = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> lastRemoteRespawningStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, OnlineBodyData> pendingRemoteBodyData =
            new Dictionary<string, OnlineBodyData>();
        private readonly Dictionary<string, int> lastRemoteBodyRevisions =
            new Dictionary<string, int>();
        private readonly Dictionary<string, float> lastRemoteBodyReceivedAt =
            new Dictionary<string, float>();
        private readonly Dictionary<string, string> lastRemoteBodyJson =
            new Dictionary<string, string>();
        private readonly Dictionary<string, string> lastRemoteBodyHashes =
            new Dictionary<string, string>();
        private readonly Dictionary<string, string> lastRemoteBodySpecies =
            new Dictionary<string, string>();
        private readonly Dictionary<string, float> nextRemoteBodyRequestAt =
            new Dictionary<string, float>();
        private readonly Dictionary<string, PendingRemoteThrow> pendingRemoteThrows =
            new Dictionary<string, PendingRemoteThrow>();
        private readonly Dictionary<string, RemoteThrowStamp> lastPredictedRemoteThrows =
            new Dictionary<string, RemoteThrowStamp>();
        private readonly Dictionary<string, string> lastAppliedLocalThrowCarrierIds =
            new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastAppliedLocalThrowSequences =
            new Dictionary<string, int>();

        private sealed class RemoteTarget
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public int FacingDirection;
            public int PlayerSlot;
            public bool Redrawing;
            public bool TurtleShelled;
            public bool Grounded;
            public float ReceivedAt;
            public string SlimeAttachedToPlayerId;
            public float SmoothedReceiveInterval = 1f / 30f;
            public float SmoothedReceiveJitter;
            public readonly List<RemoteSnapshot> Snapshots = new List<RemoteSnapshot>(MaxRemoteSnapshots);
        }

        private sealed class RemoteSnapshot
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public bool Grounded;
            public float ReceivedAt;
        }

        private sealed class PendingRemoteThrow
        {
            public string CarrierPlayerId;
            public int EventSequence;
            public float ExpiresAt;
        }

        private sealed class RemoteThrowStamp
        {
            public string CarrierPlayerId;
            public int EventSequence;
            public int StageRevision;
            public int RetryRevision;
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
            if (emoteController == null) emoteController = FindFirstObjectByType<PlayerEmoteController>();
        }

        private void OnEnable()
        {
            if (onlineManager != null)
            {
                onlineManager.StateChanged += HandleOnlineStateChanged;
                onlineManager.PlayerStateReceived += ApplyRemoteState;
                onlineManager.BodyDataReceived += ApplyRemoteBodyData;
                onlineManager.CarryDataReceived += ApplyRemoteCarryData;
                onlineManager.GimmickDataReceived += ApplyBodyResyncRequest;
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
                onlineManager.GimmickDataReceived -= ApplyBodyResyncRequest;
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

            SendLocalState(false);
        }

        private void SendLocalState(bool immediate)
        {
            if (!immediate && Time.unscaledTime < nextSendTime)
            {
                return;
            }

            nextSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            if (emoteController == null) emoteController = FindFirstObjectByType<PlayerEmoteController>();
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            for (int localIndex = 0; localIndex < localPlayers.Count; localIndex++)
            {
                PlayerController2D localPlayer = localPlayers[localIndex];
                if (localPlayer == null) continue;
                SendOneLocalState(localPlayer, localIndex);
            }
        }

        private void SendOneLocalState(PlayerController2D localPlayer, int localIndex)
        {
            Transform localTransform = localPlayer.transform;
            Rigidbody2D body = localPlayer.GetComponent<Rigidbody2D>();
            PlayerCarryController localCarry = localPlayer.GetComponent<PlayerCarryController>();
            string connectionId = onlineManager.LocalPlayerId;
            string gamePlayerId = OnlinePlayerIdentity.Compose(connectionId, localIndex);
            lastAppliedLocalThrowCarrierIds.TryGetValue(gamePlayerId, out string appliedThrowCarrierId);
            lastAppliedLocalThrowSequences.TryGetValue(gamePlayerId, out int appliedThrowSequence);
            int sequence = localStateSequences.TryGetValue(gamePlayerId, out int previous)
                ? previous + 1
                : ++nextStateSequence;
            localStateSequences[gamePlayerId] = sequence;
            LocalCoopSessionManager localCoop = stageManager.GetComponent<LocalCoopSessionManager>();
            int baseSlot = PlayerColorPalette.GetLobbyPlayerSlot(onlineManager.CurrentLobby, connectionId);
            onlineManager.SendPlayerState(new OnlinePlayerState
            {
                PlayerId = gamePlayerId,
                ConnectionId = connectionId,
                LocalPlayerIndex = localIndex,
                InputDevice = localPlayer.InputBinding != null ? localPlayer.InputBinding.DeviceDisplayName : string.Empty,
                DisplayName = localIndex == 0 ? PlayerNameSettings.CurrentName : $"{PlayerNameSettings.CurrentName} P{localIndex + 1}",
                Sequence = sequence,
                PlayerSlot = Mathf.Clamp(baseSlot + localIndex, 0, 3),
                BodyRevision = onlineManager.GetLocalBodyRevision(gamePlayerId),
                BodyHash = onlineManager.GetLocalBodyHash(gamePlayerId),
                BodySpecies = onlineManager.GetLocalBodySpecies(gamePlayerId),
                Position = localTransform.position,
                Velocity = body != null ? body.linearVelocity : Vector2.zero,
                Rotation = body != null ? body.rotation : localTransform.eulerAngles.z,
                FacingDirection = localPlayer != null ? localPlayer.FacingDirection : 1,
                Redrawing = localCoop != null && localCoop.LocalPlayerCount > 1
                    ? localCoop.IsPlayerDrawing(localPlayer)
                    : stageManager.IsDrawingMode,
                Respawning = stageManager.IsPlayerRespawning(localPlayer),
                Eliminated = stageManager.IsPlayerEliminated(localPlayer),
                TurtleShelled = localPlayer?.IsTurtleShelled ?? false,
                Grounded = localPlayer?.IsAnimationGrounded ?? false,
                WallSticking = localPlayer?.IsWallSticking ?? false,
                Gliding = localPlayer?.IsGliding ?? false,
                JumpSequence = localPlayer?.JumpEventSequence ?? 0,
                LandingSequence = localPlayer?.LandingEventSequence ?? 0,
                HardLanding = localPlayer?.LastLandingWasHard ?? false,
                AimingWeapon = localCarry?.IsAimingWeapon ?? false,
                AimDirection = localCarry?.CurrentOnlineWeaponAimDirection ?? Vector2.right,
                SpeedBoostMultiplier = localPlayer?.TemporarySpeedBoostMultiplier ?? 1f,
                SpeedBoostRemaining = localPlayer?.TemporarySpeedBoostRemaining ?? 0f,
                RespawnRemaining = stageManager.GetPlayerRespawnRemaining(localPlayer),
                RespawnGraceRemaining = stageManager.GetPlayerRespawnGraceRemaining(localPlayer),
                EmoteId = emoteController != null ? emoteController.CurrentLocalEmoteId : -1,
                EmoteSequence = emoteController != null ? emoteController.CurrentLocalEmoteSequence : 0,
                EmoteRemaining = emoteController != null ? emoteController.CurrentLocalEmoteRemaining : 0f,
                SlimeAttachedToPlayerId = localCarry?.SlimeAttachedOnlinePlayerId,
                CarriedPlayerId = localCarry?.CurrentOnlineCarriedPlayerId,
                CarryAction = localCarry?.CurrentOnlineCarryAction,
                CarryOffset = localCarry != null ? localCarry.CurrentOnlineCarryOffset : Vector2.zero,
                CarryEventSequence = localCarry?.CurrentOnlineCarryEventSequence ?? 0,
                HumanCarryStruggling = localPlayer?.IsHumanCarryStruggling ?? false,
                LastAppliedThrowCarrierId = appliedThrowCarrierId,
                LastAppliedThrowSequence = appliedThrowSequence
            });
        }

        private void ApplyRemoteState(OnlinePlayerState state)
        {
            if (state == null || onlineManager == null || stageManager == null)
            {
                return;
            }

            if (OnlinePlayerIdentity.IsOwnedByConnection(state.PlayerId, onlineManager.LocalPlayerId))
            {
                return;
            }
            ApplyRemoteDisplayName(state.PlayerId, state.DisplayName);
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
            EnsureRemoteBodyMatchesState(state);

            if (pendingRemoteThrows.TryGetValue(state.PlayerId, out PendingRemoteThrow pendingThrow))
            {
                bool throwAcknowledged = state.LastAppliedThrowCarrierId == pendingThrow.CarrierPlayerId
                    && state.LastAppliedThrowSequence >= pendingThrow.EventSequence;
                if (throwAcknowledged || Time.unscaledTime >= pendingThrow.ExpiresAt)
                {
                    pendingRemoteThrows.Remove(state.PlayerId);
                }
                else
                {
                    // This snapshot was sent before the carried owner received the
                    // throw event. Applying it would visibly pull the predicted
                    // character back into the carrier for one or two frames.
                    return;
                }
            }

            stageManager.ApplyOnlineRemoteRedrawing(state.PlayerId, state.Redrawing);
            stageManager.ApplyOnlineRemoteEliminationState(state.PlayerId, state.Eliminated);
            if (state.Eliminated)
            {
                remoteTargets.Remove(state.PlayerId);
                ApplyRemoteRespawnTransition(state);
                return;
            }
            string localCarryTargetId = OnlinePlayerIdentity.IsOwnedByConnection(
                    state.CarriedPlayerId, onlineManager.LocalPlayerId)
                ? state.CarriedPlayerId
                : stageManager.GetOnlineCarriedLocalPlayerId(state.PlayerId)
                    ?? onlineManager.LocalPlayerId;
            stageManager.ReconcileOnlineCarryState(
                state.PlayerId,
                state.Redrawing ? null : state.CarriedPlayerId,
                state.Redrawing ? string.Empty : state.CarryAction,
                state.Redrawing ? Vector2.zero : state.CarryOffset,
                state.CarryEventSequence,
                localCarryTargetId,
                state.Redrawing ? Vector2.zero : state.Velocity);
            PlayerController2D remotePlayer = stageManager.GetOnlinePlayerController(state.PlayerId);
            remotePlayer?.ApplyRemoteFacingDirection(state.FacingDirection);
            if (remotePlayer != null && !stageManager.IsOnlineRemotePlayerHeldByLocal(state.PlayerId))
            {
                remotePlayer.SetHumanCarryStruggling(!state.Redrawing && state.HumanCarryStruggling);
            }
            remotePlayer?.ApplyRemoteActionState(
                state.Grounded,
                state.WallSticking,
                state.Gliding,
                state.JumpSequence,
                state.LandingSequence,
                state.HardLanding,
                state.SpeedBoostMultiplier,
                state.SpeedBoostRemaining,
                state.RespawnGraceRemaining);
            remotePlayer?.GetComponent<PlayerCarryController>()?.ApplyRemoteWeaponAim(
                state.AimingWeapon,
                state.AimDirection);
            if (emoteController == null) emoteController = FindFirstObjectByType<PlayerEmoteController>();
            emoteController?.ApplyRemoteSnapshot(
                state.PlayerId,
                state.EmoteId,
                state.EmoteSequence,
                state.EmoteRemaining);
            int canonicalPlayerSlot = PlayerColorPalette.GetLobbyPlayerSlot(
                onlineManager.CurrentLobby,
                state.PlayerId);
            int resolvedPlayerSlot = canonicalPlayerSlot >= 0
                ? canonicalPlayerSlot
                : Mathf.Clamp(state.PlayerSlot, 0, 3);
            stageManager.ApplyOnlinePlayerColor(state.PlayerId, resolvedPlayerSlot);
            ApplyRemoteRespawnTransition(state);
            if (stageManager.IsOnlineRemotePlayerHeldByLocal(state.PlayerId))
            {
                remoteTargets.Remove(state.PlayerId);
                return;
            }

            if (!remoteTargets.TryGetValue(state.PlayerId, out RemoteTarget target))
            {
                target = new RemoteTarget();
                remoteTargets[state.PlayerId] = target;
            }
            target.Position = state.Position;
            target.Velocity = state.Velocity;
            target.Rotation = state.Rotation;
            target.FacingDirection = state.FacingDirection;
            target.PlayerSlot = resolvedPlayerSlot;
            target.Redrawing = state.Redrawing;
            target.TurtleShelled = !state.Redrawing && state.TurtleShelled;
            target.Grounded = !state.Redrawing && state.Grounded;
            target.ReceivedAt = Time.unscaledTime;
            target.SlimeAttachedToPlayerId = state.Redrawing ? null : state.SlimeAttachedToPlayerId;
            AppendRemoteSnapshot(target);
            ApplyLobbyColors(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
        }

        private void ApplyRemoteRespawnTransition(OnlinePlayerState state)
        {
            bool hadPrevious = lastRemoteRespawningStates.TryGetValue(state.PlayerId, out bool wasRespawning);
            lastRemoteRespawningStates[state.PlayerId] = state.Respawning;

            if (state.Respawning && (!hadPrevious || !wasRespawning))
            {
                stageManager.PlayOnlinePlayerLifeEffect(state.PlayerId, state.Position, false);
            }
            else if (!state.Respawning && hadPrevious && wasRespawning)
            {
                stageManager.PlayOnlinePlayerLifeEffect(state.PlayerId, state.Position, true);
            }
        }

        private void ApplyRemoteDisplayName(string playerId, string displayName)
        {
            string sanitized = PlayerNameSettings.Sanitize(displayName);
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(sanitized)) return;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].PlayerId == playerId)
                {
                    players[i].DisplayName = sanitized;
                    break;
                }
            }
        }

        private void ApplyRemoteTarget()
        {
            if (stageManager == null)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-remoteSmoothRate * Time.unscaledDeltaTime);
            bool isParticipantClient = stageManager.IsOnlineStageActive
                && !stageManager.IsOnlineStageHost;
            foreach (KeyValuePair<string, RemoteTarget> pair in remoteTargets)
            {
                if (stageManager.IsOnlineRemotePlayerHeldByLocal(pair.Key))
                {
                    continue;
                }
                Transform remoteTransform = stageManager.GetOnlinePlayerTransform(pair.Key);
                Vector2 position;
                Vector2 animationVelocity;
                float rotation;
                bool grounded;
                bool useBufferedInterpolation = isParticipantClient && !IsLobbyHostPlayer(pair.Key);
                if (useBufferedInterpolation
                    && TryGetBufferedPose(pair.Value, out Vector2 bufferedPosition,
                        out Vector2 bufferedVelocity, out float bufferedRotation,
                        out bool bufferedGrounded))
                {
                    position = bufferedPosition;
                    animationVelocity = bufferedVelocity;
                    rotation = bufferedRotation;
                    grounded = bufferedGrounded;
                }
                else
                {
                    float stateAge = Mathf.Clamp(
                        Time.unscaledTime - pair.Value.ReceivedAt,
                        0f,
                        0.12f);
                    float predictionTime = Mathf.Min(remotePredictionSeconds, 0.04f) + stateAge;
                    Vector2 predictionVelocity = pair.Value.Velocity;
                    // A grounded packet can still contain the last downward physics
                    // velocity. Extrapolating it is what made remote players appear
                    // buried in the floor immediately after landing.
                    if (pair.Value.Grounded && predictionVelocity.y <= 0.5f)
                        predictionVelocity.y = 0f;
                    Vector2 predictedPosition = pair.Value.Position
                        + predictionVelocity * Mathf.Clamp(predictionTime, 0f, 0.12f);
                    position = remoteTransform != null
                        ? Vector2.Distance(remoteTransform.position, predictedPosition) >= remoteSnapDistance
                            ? predictedPosition
                            : Vector2.Lerp(remoteTransform.position, predictedPosition, t)
                        : predictedPosition;
                    animationVelocity = pair.Value.Velocity;
                    rotation = remoteTransform != null
                        ? Mathf.LerpAngle(remoteTransform.eulerAngles.z, pair.Value.Rotation, t)
                        : pair.Value.Rotation;
                    grounded = pair.Value.Grounded;
                }
                if (remoteTransform != null && grounded)
                {
                    float groundedY = position.y;
                    float currentY = remoteTransform.position.y;
                    if (currentY < groundedY - 0.025f)
                    {
                        position.y = groundedY;
                    }
                    else
                    {
                        float groundedT = 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime);
                        position.y = Mathf.Lerp(currentY, groundedY, groundedT);
                        if (Mathf.Abs(position.y - groundedY) < 0.012f) position.y = groundedY;
                    }
                }
                stageManager.ApplyOnlineRemoteState(pair.Key, position, animationVelocity, rotation);
                if (remoteTransform != null)
                {
                    PlayerController2D remotePlayer = remoteTransform.GetComponent<PlayerController2D>();
                    remotePlayer?.ApplyRemoteFacingDirection(pair.Value.FacingDirection);
                    remotePlayer?.ApplyRemoteTurtleShellState(pair.Value.TurtleShelled);
                    PlayerController2D attachmentTarget = string.IsNullOrEmpty(pair.Value.SlimeAttachedToPlayerId)
                        ? null
                        : stageManager.GetOnlinePlayerController(pair.Value.SlimeAttachedToPlayerId);
                    remoteTransform.GetComponent<PlayerCarryController>()?.ApplyRemoteSlimeAttachment(attachmentTarget);
                }
            }
        }

        private bool IsLobbyHostPlayer(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null || string.IsNullOrEmpty(playerId)) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].PlayerId == playerId)
                    return players[i].IsHost;
            return false;
        }

        private void AppendRemoteSnapshot(RemoteTarget target)
        {
            if (target == null) return;
            List<RemoteSnapshot> snapshots = target.Snapshots;
            if (snapshots.Count > 0)
            {
                RemoteSnapshot previous = snapshots[snapshots.Count - 1];
                float elapsed = Mathf.Clamp(target.ReceivedAt - previous.ReceivedAt, 0.001f, 0.5f);
                float receiveDeviation = Mathf.Abs(elapsed - target.SmoothedReceiveInterval);
                target.SmoothedReceiveInterval = Mathf.Lerp(
                    target.SmoothedReceiveInterval,
                    elapsed,
                    0.12f);
                target.SmoothedReceiveJitter = Mathf.Lerp(
                    target.SmoothedReceiveJitter,
                    receiveDeviation,
                    0.2f);
                float expectedTravel = Mathf.Max(
                    remoteSnapDistance,
                    (previous.Velocity.magnitude + target.Velocity.magnitude)
                        * 0.5f * elapsed + 0.75f);
                if (Vector2.Distance(previous.Position, target.Position) >= expectedTravel)
                {
                    // Respawns, round changes and other teleports must not glide
                    // through the level merely because normal movement is buffered.
                    // A high-speed throw is expected travel, not a teleport.
                    snapshots.Clear();
                }
            }
            snapshots.Add(new RemoteSnapshot
            {
                Position = target.Position,
                Velocity = target.Velocity,
                Rotation = target.Rotation,
                Grounded = target.Grounded,
                ReceivedAt = target.ReceivedAt
            });
            while (snapshots.Count > MaxRemoteSnapshots) snapshots.RemoveAt(0);
        }

        private bool TryGetBufferedPose(
            RemoteTarget target,
            out Vector2 position,
            out Vector2 velocity,
            out float rotation,
            out bool grounded)
        {
            position = target != null ? target.Position : Vector2.zero;
            velocity = target != null ? target.Velocity : Vector2.zero;
            rotation = target != null ? target.Rotation : 0f;
            grounded = target != null && target.Grounded;
            if (target == null || target.Snapshots.Count == 0) return false;

            List<RemoteSnapshot> snapshots = target.Snapshots;
            float minimumDelay = Mathf.Clamp(participantMinInterpolationDelay, 0.03f, 0.1f);
            float maximumDelay = Mathf.Clamp(
                participantMaxInterpolationDelay,
                minimumDelay,
                0.2f);
            float adaptiveDelay = Mathf.Clamp(
                Mathf.Max(minimumDelay, target.SmoothedReceiveInterval * 1.5f)
                    + target.SmoothedReceiveJitter * 2f,
                minimumDelay,
                maximumDelay);
            float renderAt = Time.unscaledTime - adaptiveDelay;
            while (snapshots.Count > 2 && snapshots[1].ReceivedAt <= renderAt)
                snapshots.RemoveAt(0);

            if (renderAt <= snapshots[0].ReceivedAt)
            {
                RemoteSnapshot earliest = snapshots[0];
                position = earliest.Position;
                velocity = earliest.Velocity;
                rotation = earliest.Rotation;
                grounded = earliest.Grounded;
                return true;
            }

            if (snapshots.Count >= 2
                && snapshots[0].ReceivedAt <= renderAt
                && renderAt <= snapshots[1].ReceivedAt)
            {
                RemoteSnapshot from = snapshots[0];
                RemoteSnapshot to = snapshots[1];
                float duration = Mathf.Max(0.0001f, to.ReceivedAt - from.ReceivedAt);
                float amount = Mathf.Clamp01((renderAt - from.ReceivedAt) / duration);
                position = Vector2.Lerp(from.Position, to.Position, amount);
                velocity = Vector2.Lerp(from.Velocity, to.Velocity, amount);
                rotation = Mathf.LerpAngle(from.Rotation, to.Rotation, amount);
                grounded = amount < 0.5f ? from.Grounded : to.Grounded;
                return true;
            }

            RemoteSnapshot latest = snapshots[snapshots.Count - 1];
            velocity = latest.Velocity;
            grounded = latest.Grounded;
            if (grounded && velocity.y <= 0.5f) velocity.y = 0f;
            float extrapolation = Mathf.Clamp(
                renderAt - latest.ReceivedAt,
                0f,
                MaxBufferedExtrapolation);
            position = latest.Position + velocity * extrapolation;
            rotation = latest.Rotation;
            return true;
        }

        private void ApplyRemoteBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || onlineManager == null || stageManager == null)
            {
                return;
            }

            if (OnlinePlayerIdentity.IsOwnedByConnection(bodyData.PlayerId, onlineManager.LocalPlayerId))
            {
                if (LocalMultiplayerDebugMode.Enabled)
                    Debug.Log($"[PICO BODY] IGNORE SELF id={bodyData.PlayerId} rev={bodyData.Revision}");
                return;
            }

            if (lastRemoteBodyJson.TryGetValue(bodyData.PlayerId, out string appliedJson)
                && string.Equals(appliedJson, bodyData.Json, System.StringComparison.Ordinal))
            {
                // Redraw confirmation deliberately resends the finalized body.
                // Advance the revision without rebuilding identical colliders.
                if (bodyData.Revision > 0)
                {
                    lastRemoteBodyRevisions[bodyData.PlayerId] = Mathf.Max(
                        lastRemoteBodyRevisions.TryGetValue(bodyData.PlayerId, out int knownRevision)
                            ? knownRevision
                            : 0,
                        bodyData.Revision);
                }
                lastRemoteBodyReceivedAt[bodyData.PlayerId] = Time.unscaledTime;
                CacheRemoteBodyIdentity(bodyData);
                stageManager.GetOnlinePlayerTransform(bodyData.PlayerId)
                    ?.GetComponent<BodyBuilder>()?.SetNetworkBodyReady(true);
                if (LocalMultiplayerDebugMode.Enabled)
                    Debug.Log($"[PICO BODY] DEDUPE id={bodyData.PlayerId} rev={bodyData.Revision}");
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
                if (LocalMultiplayerDebugMode.Enabled)
                    Debug.Log($"[PICO BODY] DEFER id={bodyData.PlayerId} rev={bodyData.Revision} localDraw={stageManager.IsDrawingMode} carryBlock={stageManager.IsOnlineBodyRebuildBlocked(bodyData.PlayerId)}");
                return;
            }

            if (!stageManager.ApplyOnlineRemoteBodyData(bodyData))
            {
                pendingRemoteBodyData[bodyData.PlayerId] = bodyData;
                return;
            }
            if (bodyData.Revision > 0)
                lastRemoteBodyRevisions[bodyData.PlayerId] = bodyData.Revision;
            lastRemoteBodyReceivedAt[bodyData.PlayerId] = Time.unscaledTime;
            lastRemoteBodyJson[bodyData.PlayerId] = bodyData.Json;
            CacheRemoteBodyIdentity(bodyData);
            if (LocalMultiplayerDebugMode.Enabled)
                Debug.Log($"[PICO BODY] APPLIED id={bodyData.PlayerId} rev={bodyData.Revision} bytes={bodyData.Json.Length}");
            ApplyLobbyColors(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
        }

        public bool HasAppliedRemoteBodyRevision(string playerId, int requiredRevision)
        {
            // Revision 0 is used by older/local debug backends. In that case the
            // ready request itself remains the compatibility signal.
            if (requiredRevision <= 0)
            {
                return true;
            }

            return !string.IsNullOrEmpty(playerId)
                && lastRemoteBodyRevisions.TryGetValue(playerId, out int appliedRevision)
                && appliedRevision >= requiredRevision;
        }

        public void RequestLocalBodyResync(int attempts = 3)
        {
            RequestBodyResync(Mathf.Clamp(attempts, 1, 5));
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

            bool targetsLocalPlayer = OnlinePlayerIdentity.IsOwnedByConnection(
                carryData.TargetPlayerId, onlineManager.LocalPlayerId);
            bool isThrow = carryData.Action == "throw";
            lastAppliedLocalThrowCarrierIds.TryGetValue(
                carryData.TargetPlayerId, out string lastThrowCarrierId);
            lastAppliedLocalThrowSequences.TryGetValue(
                carryData.TargetPlayerId, out int lastThrowSequence);
            if (isThrow && targetsLocalPlayer && carryData.EventSequence > 0
                && carryData.CarrierPlayerId == lastThrowCarrierId
                && carryData.EventSequence <= lastThrowSequence)
            {
                return;
            }
            if (isThrow && !targetsLocalPlayer)
            {
                PredictRemoteThrow(carryData);
            }

            stageManager.ApplyOnlineCarryData(carryData, carryData.TargetPlayerId);
            if (isThrow && targetsLocalPlayer)
            {
                lastAppliedLocalThrowCarrierIds[carryData.TargetPlayerId] = carryData.CarrierPlayerId;
                lastAppliedLocalThrowSequences[carryData.TargetPlayerId] = carryData.EventSequence;
                // Do not wait up to one normal 30 Hz interval before telling the
                // other peers that the carried owner has started its flight.
                SendLocalState(true);
            }
        }

        private void PredictRemoteThrow(OnlineCarryData carryData)
        {
            if (string.IsNullOrEmpty(carryData.TargetPlayerId))
            {
                return;
            }

            if (carryData.EventSequence > 0
                && lastPredictedRemoteThrows.TryGetValue(carryData.TargetPlayerId, out RemoteThrowStamp lastThrow)
                && lastThrow.CarrierPlayerId == carryData.CarrierPlayerId
                && lastThrow.StageRevision == carryData.StageRevision
                && lastThrow.RetryRevision == carryData.RetryRevision
                && lastThrow.EventSequence >= carryData.EventSequence)
            {
                // The sender applies its event locally and may receive the same
                // reliable event back from the backend. Never restart prediction
                // from the release point when that echo arrives.
                return;
            }

            Transform remoteTransform = stageManager.GetOnlinePlayerTransform(carryData.TargetPlayerId);
            if (remoteTransform == null)
            {
                return;
            }

            RemoteTarget previous = remoteTargets.TryGetValue(carryData.TargetPlayerId, out RemoteTarget known)
                ? known
                : null;
            float rotation = previous != null ? previous.Rotation : remoteTransform.eulerAngles.z;
            int facing = previous != null ? previous.FacingDirection : 1;
            int slot = previous != null ? previous.PlayerSlot : 0;
            RemoteTarget predictedThrow = new RemoteTarget
            {
                Position = carryData.ReleasePosition,
                Velocity = carryData.ReleaseVelocity,
                Rotation = rotation,
                FacingDirection = facing,
                PlayerSlot = slot,
                Redrawing = false,
                TurtleShelled = false,
                Grounded = false,
                ReceivedAt = Time.unscaledTime,
                SlimeAttachedToPlayerId = null
            };
            remoteTargets[carryData.TargetPlayerId] = predictedThrow;
            AppendRemoteSnapshot(predictedThrow);
            stageManager.ApplyOnlineRemoteState(
                carryData.TargetPlayerId,
                carryData.ReleasePosition,
                carryData.ReleaseVelocity,
                rotation);

            if (carryData.EventSequence > 0)
            {
                lastPredictedRemoteThrows[carryData.TargetPlayerId] = new RemoteThrowStamp
                {
                    CarrierPlayerId = carryData.CarrierPlayerId,
                    EventSequence = carryData.EventSequence,
                    StageRevision = carryData.StageRevision,
                    RetryRevision = carryData.RetryRevision
                };
                if (!pendingRemoteThrows.TryGetValue(carryData.TargetPlayerId, out PendingRemoteThrow pending)
                    || pending.CarrierPlayerId != carryData.CarrierPlayerId
                    || pending.EventSequence < carryData.EventSequence)
                {
                    pendingRemoteThrows[carryData.TargetPlayerId] = new PendingRemoteThrow
                    {
                        CarrierPlayerId = carryData.CarrierPlayerId,
                        EventSequence = carryData.EventSequence,
                        ExpiresAt = Time.unscaledTime + 0.4f
                    };
                }
            }
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
            if (lobby?.Players == null)
            {
                remoteTargets.Clear();
                lastRemoteSequences.Clear();
                lastRemoteStateReceivedAt.Clear();
                lastRemoteRespawningStates.Clear();
                lastRemoteBodyRevisions.Clear();
                lastRemoteBodyReceivedAt.Clear();
                lastRemoteBodyJson.Clear();
                lastRemoteBodyHashes.Clear();
                lastRemoteBodySpecies.Clear();
                nextRemoteBodyRequestAt.Clear();
                pendingRemoteBodyData.Clear();
                pendingRemoteThrows.Clear();
                lastPredictedRemoteThrows.Clear();
                pendingRosterLobby = null;
                pendingRosterLocalPlayerId = null;
                return;
            }
            HashSet<string> active = new HashSet<string>();
            if (lobby?.Players != null)
            {
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    string id = lobby.Players[i]?.PlayerId;
                    if (!string.IsNullOrEmpty(id)
                        && !OnlinePlayerIdentity.IsOwnedByConnection(id, localPlayerId)) active.Add(id);
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
                lastRemoteRespawningStates.Remove(id);
                lastRemoteBodyRevisions.Remove(id);
                lastRemoteBodyReceivedAt.Remove(id);
                lastRemoteBodyJson.Remove(id);
                lastRemoteBodyHashes.Remove(id);
                lastRemoteBodySpecies.Remove(id);
                nextRemoteBodyRequestAt.Remove(id);
                pendingRemoteBodyData.Remove(id);
                pendingRemoteThrows.Remove(id);
                lastPredictedRemoteThrows.Remove(id);
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

        private void EnsureRemoteBodyMatchesState(OnlinePlayerState state)
        {
            if (state == null || string.IsNullOrEmpty(state.PlayerId) || state.BodyRevision <= 0)
            {
                return;
            }

            bool missingRevision = !lastRemoteBodyRevisions.TryGetValue(state.PlayerId, out int revision)
                || revision < state.BodyRevision;
            bool wrongHash = !string.IsNullOrEmpty(state.BodyHash)
                && (!lastRemoteBodyHashes.TryGetValue(state.PlayerId, out string hash)
                    || !string.Equals(hash, state.BodyHash, System.StringComparison.Ordinal));
            bool wrongSpecies = !string.IsNullOrEmpty(state.BodySpecies)
                && (!lastRemoteBodySpecies.TryGetValue(state.PlayerId, out string species)
                    || !string.Equals(species, state.BodySpecies, System.StringComparison.OrdinalIgnoreCase));
            if (!missingRevision && !wrongHash && !wrongSpecies)
            {
                return;
            }

            if (nextRemoteBodyRequestAt.TryGetValue(state.PlayerId, out float nextRequest)
                && Time.unscaledTime < nextRequest)
            {
                return;
            }

            nextRemoteBodyRequestAt[state.PlayerId] = Time.unscaledTime + 1.5f;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                Kind = "body_resync_request",
                ObjectId = state.PlayerId
            });
        }

        private void ApplyBodyResyncRequest(OnlineGimmickData data)
        {
            if (data == null || onlineManager == null
                || data.Kind != "body_resync_request"
                || !OnlinePlayerIdentity.IsOwnedByConnection(data.ObjectId, onlineManager.LocalPlayerId))
            {
                return;
            }

            stageManager?.SendLocalOnlineBodyData(data.ObjectId);
        }

        private void CacheRemoteBodyIdentity(OnlineBodyData bodyData)
        {
            if (bodyData == null || string.IsNullOrEmpty(bodyData.PlayerId)) return;
            string hash = !string.IsNullOrEmpty(bodyData.BodyHash)
                ? bodyData.BodyHash
                : ContentIntegrityVerifier.HashString(bodyData.Json ?? string.Empty);
            lastRemoteBodyHashes[bodyData.PlayerId] = hash;
            SerializableBodyDrawing serialized = !string.IsNullOrEmpty(bodyData.Json)
                ? JsonUtility.FromJson<SerializableBodyDrawing>(bodyData.Json)
                : null;
            lastRemoteBodySpecies[bodyData.PlayerId] = serialized?.Species ?? string.Empty;
        }

        private void DetectLocalSpeciesChange()
        {
            if (stageManager == null || onlineManager == null) return;
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            bool changed = false;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                BodyBuilder builder = local != null ? local.GetComponent<BodyBuilder>() : null;
                if (builder == null) continue;
                string gamePlayerId = OnlinePlayerIdentity.Compose(onlineManager.LocalPlayerId, i);
                DrawManager.Species species = builder.BuiltSpecies;
                if (lastLocalSpeciesByPlayer.TryGetValue(gamePlayerId, out DrawManager.Species previous)
                    && previous == species) continue;
                lastLocalSpeciesByPlayer[gamePlayerId] = species;
                changed = true;
            }

            if (changed) RequestBodyResync(3);
        }

        private void FlushPendingBodyResync()
        {
            LocalCoopSessionManager localCoop = stageManager != null
                ? stageManager.GetComponent<LocalCoopSessionManager>()
                : null;
            if (!bodyResyncPending
                || stageManager != null && stageManager.IsDrawingMode
                || localCoop != null && localCoop.AnyDrawingSessionActive
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
