using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class JumpPad : MonoBehaviour
    {
        [SerializeField] private float jumpVelocity = 27f;
        [SerializeField] private Transform animatedRoot;
        [SerializeField] private float birdLaunchMultiplier = 3f;

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

        public void ConfigureBirdMultiplier(float multiplier)
        {
            birdLaunchMultiplier = Mathf.Clamp(multiplier, 0.25f, 3f);
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
            if (player == null) player = other.GetComponentInParent<PlayerController2D>();
            if (player != null && (player.IsFriendCarried || rb.bodyType != RigidbodyType2D.Dynamic))
            {
                ResolveCarrierLaunchTarget(player.transform, out rb, out player);
            }

            // Held guns and carried players use kinematic bodies that are moved by
            // their carrier every LateUpdate. Launching those bodies creates a
            // second, conflicting velocity and can throw the whole carry chain in
            // an unrelated direction when the carrier reaches the pad.
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            float speciesMultiplier = player != null
                && player.CurrentSpecies == DrawManager.Species.Bird
                    ? birdLaunchMultiplier
                    : 1f;
            Vector2 velocity = rb.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, jumpVelocity * speciesMultiplier);
            rb.linearVelocity = velocity;
            bounceTimer = 0.22f;
            GameSfx.PlayAt(SfxId.JumpPadLaunch, transform.position, speciesMultiplier > 1f ? 1.15f : 1f);
        }

        private static void ResolveCarrierLaunchTarget(
            Transform carriedTarget,
            out Rigidbody2D carrierBody,
            out PlayerController2D carrierPlayer)
        {
            carrierBody = null;
            carrierPlayer = null;
            if (carriedTarget == null) return;

            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(
                FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                PlayerCarryController carrier = carriers[i];
                if (carrier == null || !carrier.IsDraggingFriend(carriedTarget)) continue;
                Rigidbody2D body = carrier.GetComponent<Rigidbody2D>();
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;
                carrierBody = body;
                carrierPlayer = carrier.GetComponent<PlayerController2D>();
                return;
            }
        }
    }
}
