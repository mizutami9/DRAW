using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageConveyorBelt : MonoBehaviour
    {
        private const float Acceleration = 28f;

        private StageEditorObject marker;
        private float speed = 3f;
        private float direction = 1f;

        public Vector2 SurfaceVelocity => (Vector2)transform.right * (speed * direction);

        private void Awake()
        {
            marker = GetComponent<StageEditorObject>();
            speed = marker != null && marker.actionStrength > 0f
                ? Mathf.Clamp(marker.actionStrength, 0.5f, 10f)
                : 3f;
            direction = marker != null && Mathf.Cos(marker.movementAngle * Mathf.Deg2Rad) < 0f
                ? -1f
                : 1f;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Rigidbody2D body = collision.collider.attachedRigidbody;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }
            if (body.GetComponent<PlayerController2D>() != null)
            {
                // PlayerController2D already adds SurfaceVelocity to player input.
                // Applying it here as well would make walking against the belt
                // unnecessarily difficult.
                return;
            }

            Vector2 relative = body.worldCenterOfMass - (Vector2)transform.position;
            if (Vector2.Dot(relative, transform.up) <= 0f)
            {
                return;
            }

            Vector2 tangent = SurfaceVelocity.normalized;
            float targetSpeed = speed;
            float currentSpeed = Vector2.Dot(body.linearVelocity, tangent);
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Acceleration * Time.fixedDeltaTime);
            body.linearVelocity += tangent * (nextSpeed - currentSpeed);
        }
    }
}
