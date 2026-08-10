using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageElevator : MonoBehaviour
    {
        private Rigidbody2D cabin;
        private StageGimmickSyncManager syncManager;
        private Vector2 bottomPosition;
        private Vector2 travelDirection;
        private float travelDistance = 8f;
        private float movementSpeed = 2.2f;
        private const float StopDuration = 3f;
        private float currentDistance;
        private float stopTimer;
        private ElevatorPhase phase;
        private bool configured;

        private enum ElevatorPhase
        {
            MovingUp,
            WaitingAtTop,
            MovingDown,
            WaitingAtBottom
        }

        public void Configure(Rigidbody2D cabinBody, float distance, float speed)
        {
            cabin = cabinBody;
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            travelDistance = Mathf.Max(1f, distance);
            movementSpeed = Mathf.Max(0.1f, speed);
            bottomPosition = cabin != null ? cabin.position : (Vector2)transform.position;
            travelDirection = transform.up;
            currentDistance = 0f;
            stopTimer = 0f;
            phase = ElevatorPhase.MovingUp;
            configured = cabin != null;
        }

        private void FixedUpdate()
        {
            if (!configured
                || cabin == null
                || syncManager != null && syncManager.ShouldAskHost)
            {
                return;
            }

            switch (phase)
            {
                case ElevatorPhase.MovingUp:
                    currentDistance = Mathf.MoveTowards(
                        currentDistance,
                        travelDistance,
                        movementSpeed * Time.fixedDeltaTime);
                    if (Mathf.Approximately(currentDistance, travelDistance))
                    {
                        phase = ElevatorPhase.WaitingAtTop;
                        stopTimer = StopDuration;
                    }
                    break;

                case ElevatorPhase.WaitingAtTop:
                    stopTimer -= Time.fixedDeltaTime;
                    if (stopTimer <= 0f)
                    {
                        phase = ElevatorPhase.MovingDown;
                    }
                    break;

                case ElevatorPhase.MovingDown:
                    currentDistance = Mathf.MoveTowards(
                        currentDistance,
                        0f,
                        movementSpeed * Time.fixedDeltaTime);
                    if (Mathf.Approximately(currentDistance, 0f))
                    {
                        phase = ElevatorPhase.WaitingAtBottom;
                        stopTimer = StopDuration;
                    }
                    break;

                case ElevatorPhase.WaitingAtBottom:
                    stopTimer -= Time.fixedDeltaTime;
                    if (stopTimer <= 0f)
                    {
                        phase = ElevatorPhase.MovingUp;
                    }
                    break;
            }

            cabin.MovePosition(bottomPosition + travelDirection * currentDistance);
        }
    }
}
