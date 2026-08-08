using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class OnlinePlayerSync : MonoBehaviour
    {
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private float sendRate = 30f;
        [SerializeField] private float remoteSmoothRate = 14f;

        private float nextSendTime;
        private float nextBodyResyncTime;
        private bool bodyResyncPending;
        private readonly Dictionary<string, RemoteTarget> remoteTargets = new Dictionary<string, RemoteTarget>();

        private sealed class RemoteTarget
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public bool TurtleShelled;
        }

        private void Awake()
        {
            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
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
            onlineManager.SendPlayerState(new OnlinePlayerState
            {
                PlayerId = onlineManager.LocalPlayerId,
                Position = localTransform.position,
                Velocity = body != null ? body.linearVelocity : Vector2.zero,
                Rotation = body != null ? body.rotation : localTransform.eulerAngles.z,
                TurtleShelled = localTransform.GetComponent<PlayerController2D>()?.IsTurtleShelled ?? false
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

            remoteTargets[state.PlayerId] = new RemoteTarget
            {
                Position = state.Position,
                Velocity = state.Velocity,
                Rotation = state.Rotation,
                TurtleShelled = state.TurtleShelled
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
                Transform remoteTransform = stageManager.GetOnlinePlayerTransform(pair.Key);
                Vector2 position = remoteTransform != null
                    ? Vector2.Lerp(remoteTransform.position, pair.Value.Position, t)
                    : pair.Value.Position;
                float rotation = remoteTransform != null
                    ? Mathf.LerpAngle(remoteTransform.eulerAngles.z, pair.Value.Rotation, t)
                    : pair.Value.Rotation;
                stageManager.ApplyOnlineRemoteState(pair.Key, position, pair.Value.Velocity, rotation);
                if (remoteTransform != null)
                {
                    remoteTransform.GetComponent<PlayerController2D>()?.ApplyRemoteTurtleShellState(pair.Value.TurtleShelled);
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

            stageManager.ApplyOnlineRemoteBodyData(bodyData);
            ApplyLobbyColors(onlineManager.State, onlineManager.CurrentLobby, string.Empty);
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
            stageManager?.SyncOnlinePlayers(lobby, onlineManager != null ? onlineManager.LocalPlayerId : string.Empty);
            ApplyLobbyColors(state, lobby, message);

            if (state == OnlineConnectionState.InLobby
                || state == OnlineConnectionState.Matching
                || state == OnlineConnectionState.Playing)
            {
                RequestBodyResync();
            }
        }

        private void RequestBodyResync()
        {
            bodyResyncPending = true;
            nextBodyResyncTime = Time.unscaledTime + 0.25f;
        }

        private void FlushPendingBodyResync()
        {
            if (!bodyResyncPending || Time.unscaledTime < nextBodyResyncTime)
            {
                return;
            }

            bodyResyncPending = false;
            stageManager?.SendLocalOnlineBodyData();
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
