using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float acceleration = 70f;
        [SerializeField] private float deceleration = 90f;
        [SerializeField] private float jumpVelocity = 13f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.1f;
        [SerializeField] private float glideFallSpeed = -2.8f;
        [SerializeField] private float slimeStickDuration = 0.28f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(1.2f, 0.18f);
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundProbePadding = 0.08f;
        [SerializeField] private float slopeProbeDistance = 0.55f;
        [SerializeField] private float slopeAssistMaxAngle = 50f;

        private Rigidbody2D rb;
        private BodyBuilder bodyBuilder;
        private float horizontalInput;
        private float lastGroundedAt = -100f;
        private float lastJumpPressedAt = -100f;
        private bool controlsEnabled = true;
        private float jumpMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;
        private float jumpVelocityMultiplier = 1f;
        private bool canGlide;
        private bool canWallStick;
        private bool slimMode;
        private float lastWallContactAt = -100f;
        private int facingDirection = 1;
        private Vector2 groundNormal = Vector2.up;

        public bool IsGrounded { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public int FacingDirection => facingDirection;

        public void SetJumpMultiplier(float multiplier)
        {
            jumpMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void ApplySpeciesMovement(DrawManager.Species species)
        {
            moveSpeedMultiplier = 1f;
            jumpVelocityMultiplier = 1f;
            canGlide = false;
            canWallStick = false;
            slimMode = false;

            switch (species)
            {
                case DrawManager.Species.Cat:
                    moveSpeedMultiplier = 1.25f;
                    jumpVelocityMultiplier = 0.9f;
                    break;
                case DrawManager.Species.Bird:
                    canGlide = true;
                    jumpVelocityMultiplier = 0.85f;
                    break;
                case DrawManager.Species.Snake:
                    slimMode = true;
                    moveSpeedMultiplier = 0.9f;
                    jumpVelocityMultiplier = 0.65f;
                    break;
                case DrawManager.Species.Slime:
                    canWallStick = true;
                    moveSpeedMultiplier = 0.8f;
                    jumpVelocityMultiplier = 0.75f;
                    break;
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyBuilder = GetComponent<BodyBuilder>();

            if (groundCheck == null)
            {
                GameObject check = new GameObject("GroundCheck");
                check.transform.SetParent(transform);
                check.transform.localPosition = new Vector3(0f, -1.25f, 0f);
                groundCheck = check.transform;
            }
        }

        private void Update()
        {
            if (!controlsEnabled)
            {
                horizontalInput = 0f;
                return;
            }

            horizontalInput = Input.GetAxisRaw("Horizontal");
            UpdateFacing();

            if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                lastJumpPressedAt = Time.time;
            }
        }

        private void FixedUpdate()
        {
            UpdateGrounded();
            UpdateWallContact();
            Move();
            ApplyAirAbility();
            TryJump();
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled)
            {
                horizontalInput = 0f;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        public void ResetMotion()
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        private void UpdateGrounded()
        {
            Vector2 probeCenter = groundCheck.position;
            Vector2 probeSize = groundCheckSize;
            if (TryGetBodyBounds(out Bounds bodyBounds))
            {
                probeCenter = new Vector2(bodyBounds.center.x, bodyBounds.min.y - groundProbePadding);
                float widthFactor = slimMode ? 0.45f : 0.75f;
                probeSize = new Vector2(Mathf.Max(groundCheckSize.x, bodyBounds.size.x * widthFactor), groundCheckSize.y);
            }

            IsGrounded = Physics2D.OverlapBox(probeCenter, probeSize, 0f, groundLayer);
            groundNormal = IsGrounded ? FindGroundNormal(probeSize) : Vector2.up;
            if (IsGrounded)
            {
                lastGroundedAt = Time.time;
            }
        }

        private bool TryGetBodyBounds(out Bounds bodyBounds)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            bool hasBounds = false;
            bodyBounds = new Bounds(transform.position, Vector3.zero);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D current = colliders[i];
                if (current == null || !current.enabled || current.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bodyBounds = current.bounds;
                    hasBounds = true;
                }
                else
                {
                    bodyBounds.Encapsulate(current.bounds);
                }
            }

            return hasBounds;
        }

        private void Move()
        {
            float targetSpeed = horizontalInput * moveSpeed * moveSpeedMultiplier;
            float speedDifference = targetSpeed - rb.linearVelocity.x;
            float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            float movement = Mathf.Clamp(speedDifference, -rate * Time.fixedDeltaTime, rate * Time.fixedDeltaTime);
            Vector2 nextVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
            nextVelocity = ApplySlopeAssist(nextVelocity);
            rb.linearVelocity = nextVelocity;
        }

        private Vector2 ApplySlopeAssist(Vector2 velocity)
        {
            if (!IsGrounded || Mathf.Abs(horizontalInput) <= 0.01f || groundNormal.y <= 0.2f)
            {
                return velocity;
            }

            float slopeAngle = Vector2.Angle(groundNormal, Vector2.up);
            if (slopeAngle <= 1f || slopeAngle > slopeAssistMaxAngle)
            {
                return velocity;
            }

            Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x).normalized;
            if (Mathf.Sign(tangent.x) != Mathf.Sign(horizontalInput))
            {
                tangent = -tangent;
            }

            if (tangent.y <= 0f)
            {
                return velocity;
            }

            float slopeY = Mathf.Abs(velocity.x) * (tangent.y / Mathf.Max(Mathf.Abs(tangent.x), 0.1f));
            return new Vector2(velocity.x, Mathf.Max(velocity.y, slopeY));
        }

        private Vector2 FindGroundNormal(Vector2 probeSize)
        {
            if (!TryGetBodyBounds(out Bounds bodyBounds))
            {
                return Vector2.up;
            }

            float y = bodyBounds.min.y + 0.12f;
            Vector2[] origins =
            {
                new Vector2(bodyBounds.center.x, y),
                new Vector2(bodyBounds.min.x + Mathf.Max(0.08f, probeSize.x * 0.2f), y),
                new Vector2(bodyBounds.max.x - Mathf.Max(0.08f, probeSize.x * 0.2f), y)
            };

            Vector2 normal = Vector2.up;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < origins.Length; i++)
            {
                RaycastHit2D hit = Physics2D.Raycast(origins[i], Vector2.down, slopeProbeDistance, groundLayer);
                if (hit.collider == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                normal = hit.normal;
            }

            return normal.y > 0.2f ? normal.normalized : Vector2.up;
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(horizontalInput) <= 0.01f)
            {
                return;
            }

            int nextDirection = horizontalInput < 0f ? -1 : 1;
            if (nextDirection == facingDirection)
            {
                return;
            }

            facingDirection = nextDirection;
            bodyBuilder?.SetFacingDirection(facingDirection);
        }

        private void TryJump()
        {
            bool canUseCoyoteTime = Time.time - lastGroundedAt <= coyoteTime;
            bool hasBufferedJump = Time.time - lastJumpPressedAt <= jumpBufferTime;

            if (!canUseCoyoteTime || !hasBufferedJump)
            {
                return;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity * jumpMultiplier * jumpVelocityMultiplier);
            lastJumpPressedAt = -100f;
            lastGroundedAt = -100f;
        }

        private void ApplyAirAbility()
        {
            if (!IsGrounded && canGlide && Input.GetButton("Jump") && rb.linearVelocity.y < glideFallSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, glideFallSpeed);
            }

            if (!IsGrounded && canWallStick && Time.time - lastWallContactAt <= slimeStickDuration && rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }

        private void UpdateWallContact()
        {
            if (!canWallStick || !TryGetBodyBounds(out Bounds bodyBounds))
            {
                return;
            }

            Vector2 size = new Vector2(0.12f, Mathf.Max(0.5f, bodyBounds.size.y * 0.75f));
            Vector2 left = new Vector2(bodyBounds.min.x - 0.06f, bodyBounds.center.y);
            Vector2 right = new Vector2(bodyBounds.max.x + 0.06f, bodyBounds.center.y);
            Vector2 topSize = new Vector2(Mathf.Max(0.5f, bodyBounds.size.x * 0.75f), 0.12f);
            Vector2 top = new Vector2(bodyBounds.center.x, bodyBounds.max.y + 0.06f);
            if (Physics2D.OverlapBox(left, size, 0f, groundLayer)
                || Physics2D.OverlapBox(right, size, 0f, groundLayer)
                || Physics2D.OverlapBox(top, topSize, 0f, groundLayer))
            {
                lastWallContactAt = Time.time;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);

            if (TryGetBodyBounds(out Bounds bodyBounds))
            {
                Vector2 probeCenter = new Vector2(bodyBounds.center.x, bodyBounds.min.y - groundProbePadding);
                Vector2 probeSize = new Vector2(Mathf.Max(groundCheckSize.x, bodyBounds.size.x * 0.75f), groundCheckSize.y);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(probeCenter, probeSize);
            }
        }
    }
}
