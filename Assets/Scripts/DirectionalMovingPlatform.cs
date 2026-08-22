using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Moves a linked platform from held directional button input. Releasing every
    /// button stops the platform at its current position instead of returning it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DirectionalMovingPlatform : MonoBehaviour
    {
        private readonly Dictionary<string, Vector2> activeInputs =
            new Dictionary<string, Vector2>();

        private Rigidbody2D body;
        private StageGimmickSyncManager syncManager;
        private Vector2 startPosition;
        private float movementLimit = 6f;
        private float movementSpeed = 3.2f;
        private bool configured;

        public Vector2 SurfaceVelocity { get; private set; }

        public void Configure(float limit, float speed)
        {
            movementLimit = Mathf.Clamp(limit, 1f, 100f);
            movementSpeed = Mathf.Max(0.1f, speed);
            if (configured)
            {
                return;
            }

            body = GetComponent<Rigidbody2D>();
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            startPosition = body != null ? body.position : (Vector2)transform.position;
            configured = true;
        }

        public void SetInput(string sourceId, Vector2 direction, bool pressed)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return;
            }

            if (pressed)
            {
                activeInputs[sourceId] = direction.normalized;
            }
            else
            {
                activeInputs.Remove(sourceId);
            }
        }

        private void FixedUpdate()
        {
            if (!configured
                || activeInputs.Count == 0
                || syncManager != null && syncManager.ShouldAskHost)
            {
                SurfaceVelocity = Vector2.zero;
                return;
            }

            Vector2 input = Vector2.zero;
            foreach (Vector2 direction in activeInputs.Values)
            {
                input += direction;
            }

            if (input.sqrMagnitude < 0.001f)
            {
                SurfaceVelocity = Vector2.zero;
                return;
            }

            input = input.normalized;
            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            Vector2 next = current + input * (movementSpeed * Time.fixedDeltaTime);
            Vector2 offset = next - startPosition;
            offset.x = Mathf.Clamp(offset.x, -movementLimit, movementLimit);
            offset.y = Mathf.Clamp(offset.y, -movementLimit, movementLimit);
            next = startPosition + offset;
            SurfaceVelocity = (next - current) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);

            if (body != null)
            {
                body.MovePosition(next);
            }
            else
            {
                transform.position = next;
            }
        }
    }
}
