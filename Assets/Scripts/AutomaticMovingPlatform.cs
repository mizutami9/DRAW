using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class AutomaticMovingPlatform : MonoBehaviour
    {
        private const float EndPauseSeconds = 0.35f;

        private Rigidbody2D body;
        private StageGimmickSyncManager syncManager;
        private Vector2 startPosition;
        private Vector2 endPosition;
        private float movementSpeed = 3.2f;
        private float resumeAt;
        private bool movingToEnd = true;
        private bool configured;

        public Vector2 SurfaceVelocity { get; private set; }

        public void Configure(Rigidbody2D targetBody, float distance, float angle, float speed)
        {
            body = targetBody;
            movementSpeed = Mathf.Clamp(speed, 0.5f, 10f);
            startPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            endPosition = startPosition + direction.normalized * Mathf.Clamp(distance, 1f, 100f);
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            movingToEnd = true;
            resumeAt = Time.time;
            configured = true;
        }

        private void FixedUpdate()
        {
            if (!configured
                || syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                SurfaceVelocity = Vector2.zero;
                return;
            }
            if (Time.time < resumeAt)
            {
                SurfaceVelocity = Vector2.zero;
                return;
            }

            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            Vector2 target = movingToEnd ? endPosition : startPosition;
            Vector2 next = Vector2.MoveTowards(current, target, movementSpeed * Time.fixedDeltaTime);
            SurfaceVelocity = (next - current) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            if (body != null)
            {
                body.MovePosition(next);
            }
            else
            {
                transform.position = next;
            }

            if ((next - target).sqrMagnitude > 0.0001f)
            {
                return;
            }

            movingToEnd = !movingToEnd;
            resumeAt = Time.time + EndPauseSeconds;
        }
    }
}
