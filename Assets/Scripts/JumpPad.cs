using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class JumpPad : MonoBehaviour
    {
        [SerializeField] private float jumpVelocity = 27f;
        [SerializeField] private Transform animatedRoot;

        private Vector3 animatedOrigin;
        private float bounceTimer;

        public void Configure(Transform visualRoot, float launchVelocity)
        {
            animatedRoot = visualRoot;
            jumpVelocity = Mathf.Clamp(launchVelocity, 5f, 120f);
            if (animatedRoot != null)
            {
                animatedOrigin = animatedRoot.localPosition;
            }
        }

        private void Update()
        {
            if (animatedRoot == null)
            {
                return;
            }

            if (bounceTimer > 0f)
            {
                bounceTimer = Mathf.Max(0f, bounceTimer - Time.deltaTime);
                float t = 1f - bounceTimer / 0.22f;
                float squash = Mathf.Sin(t * Mathf.PI);
                animatedRoot.localPosition = animatedOrigin + Vector3.down * (0.1f * squash);
                return;
            }

            animatedRoot.localPosition = Vector3.Lerp(animatedRoot.localPosition, animatedOrigin, Time.deltaTime * 18f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null)
            {
                rb = other.GetComponentInParent<Rigidbody2D>();
            }

            if (rb == null)
            {
                return;
            }

            PlayerController2D player = rb.GetComponent<PlayerController2D>();
            float speciesMultiplier = player != null
                && player.CurrentSpecies == DrawManager.Species.Bird
                    ? 3f
                    : 1f;
            Vector2 velocity = rb.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, jumpVelocity * speciesMultiplier);
            rb.linearVelocity = velocity;
            bounceTimer = 0.22f;
        }
    }
}
