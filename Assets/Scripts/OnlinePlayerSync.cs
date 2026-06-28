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
        private bool hasRemoteTarget;
        private Vector2 remoteTargetPosition;
        private Vector2 remoteTargetVelocity;
        private float remoteTargetRotation;

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
                onlineManager.PlayerStateReceived += ApplyRemoteState;
                onlineManager.BodyDataReceived += ApplyRemoteBodyData;
            }
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.PlayerStateReceived -= ApplyRemoteState;
                onlineManager.BodyDataReceived -= ApplyRemoteBodyData;
            }
        }

        private void Update()
        {
            if (onlineManager == null || stageManager == null)
            {
                return;
            }

            if (onlineManager.State != OnlineConnectionState.InLobby && onlineManager.State != OnlineConnectionState.Playing)
            {
                return;
            }

            ApplyRemoteTarget();

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
                Rotation = body != null ? body.rotation : localTransform.eulerAngles.z
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

            remoteTargetPosition = state.Position;
            remoteTargetVelocity = state.Velocity;
            remoteTargetRotation = state.Rotation;
            hasRemoteTarget = true;
        }

        private void ApplyRemoteTarget()
        {
            if (!hasRemoteTarget || stageManager == null)
            {
                return;
            }

            Transform remoteTransform = stageManager.RemotePlayerTransform;
            if (remoteTransform == null)
            {
                stageManager.ApplyOnlineRemoteState(remoteTargetPosition, remoteTargetVelocity, remoteTargetRotation);
                return;
            }

            float t = 1f - Mathf.Exp(-remoteSmoothRate * Time.unscaledDeltaTime);
            Vector2 position = Vector2.Lerp(remoteTransform.position, remoteTargetPosition, t);
            float rotation = Mathf.LerpAngle(remoteTransform.eulerAngles.z, remoteTargetRotation, t);
            stageManager.ApplyOnlineRemoteState(position, remoteTargetVelocity, rotation);
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
        }

    }
}
