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
        private const float LargestWingGlideFallSpeed = -0.3f;
        private const float SmallestWingGlideFallSpeed = -3f;
        private const float FullGlideWingInk = 350f;
        private const float FullSpeedCatLegInk = 120f;
        private const float FullJumpSnakeInk = 350f;
        private const float FullStickSlimeInk = 200f;
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
        private float currentGlideFallSpeed;
        private bool canWallStick;
        private float currentSlimeWallSlideSpeed;
        private DrawManager.Species currentSpecies = DrawManager.Species.Human;
        private bool wasGliding;
        private bool wasWallSticking;
        private float nextFootstepTime;
        private bool slimMode;
        private float lastWallContactAt = -100f;
        private int facingDirection = 1;
        private Vector2 groundNormal = Vector2.up;
        private Vector2 supportVelocity;
        private Rigidbody2D supportBody;
        private float supportNormalScore;
        private float supportSampleFixedTime = -100f;
        private float jumpProtectionStartedAt = -100f;
        private float jumpProtectionUntil = -100f;
        private float protectedJumpVelocity;
        private readonly Collider2D[] overlapResults = new Collider2D[24];
        private readonly RaycastHit2D[] groundRayResults = new RaycastHit2D[24];
        private ContactFilter2D groundContactFilter;

        public bool IsGrounded { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public int FacingDirection => facingDirection;

        public void SetJumpMultiplier(float multiplier)
        {
            jumpMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public static float CalculateBirdGlideFallSpeed(float wingInk)
        {
            float wingSize = Mathf.Clamp01(Mathf.Max(0f, wingInk) / FullGlideWingInk);
            return Mathf.Lerp(SmallestWingGlideFallSpeed, LargestWingGlideFallSpeed, wingSize);
        }

        public static float CalculateCatMoveSpeedMultiplier(float legInk)
        {
            return Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(Mathf.Max(0f, legInk) / FullSpeedCatLegInk));
        }

        public static float CalculateSnakeJumpMultiplier(float totalInk)
        {
            return Mathf.Lerp(0.65f, 1.3f, Mathf.Clamp01(Mathf.Max(0f, totalInk) / FullJumpSnakeInk));
        }

        public static float CalculateSlimeStickStrength(float slimeInk)
        {
            return Mathf.Clamp01(Mathf.Max(0f, slimeInk) / FullStickSlimeInk);
        }

        public void ApplySpeciesMovement(
            DrawManager.Species species,
            float wingInk = 0f,
            float catLegInk = 0f,
            float snakeInk = 0f,
            float slimeInk = 0f)
        {
            currentSpecies = species;
            moveSpeedMultiplier = 1f;
            jumpVelocityMultiplier = 1f;
            canGlide = false;
            canWallStick = false;
            slimMode = false;
            currentGlideFallSpeed = CalculateBirdGlideFallSpeed(wingInk);
            currentSlimeWallSlideSpeed = Mathf.Lerp(-3f, 0f, CalculateSlimeStickStrength(slimeInk));

            switch (species)
            {
                case DrawManager.Species.Cat:
                    moveSpeedMultiplier = CalculateCatMoveSpeedMultiplier(catLegInk);
                    jumpVelocityMultiplier = 0.9f;
                    break;
                case DrawManager.Species.Bird:
                    canGlide = true;
                    jumpVelocityMultiplier = 0.85f;
                    break;
                case DrawManager.Species.Snake:
                    slimMode = true;
                    moveSpeedMultiplier = 0.9f;
                    jumpVelocityMultiplier = CalculateSnakeJumpMultiplier(snakeInk);
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
            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(groundLayer);
            groundContactFilter.useTriggers = false;

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
            bool groundedBeforeProbe = IsGrounded;
            float verticalSpeedBeforeProbe = rb.linearVelocity.y;
            UpdateGrounded();
            PlayLandingSound(groundedBeforeProbe, verticalSpeedBeforeProbe);
            UpdateWallContact();
            Move();
            PlayMovementSound();
            ApplyAirAbility();
            TryJump();
            ApplyJumpLoadProtection();
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

            IsGrounded = HasExternalOverlap(probeCenter, probeSize);
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
            Vector2 currentSupportVelocity = GetCurrentSupportVelocity();
            float targetSpeed = currentSupportVelocity.x
                + horizontalInput * moveSpeed * moveSpeedMultiplier;
            float speedDifference = targetSpeed - rb.linearVelocity.x;
            float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            float movement = Mathf.Clamp(speedDifference, -rate * Time.fixedDeltaTime, rate * Time.fixedDeltaTime);
            Vector2 nextVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
            nextVelocity = ApplySlopeAssist(nextVelocity);
            rb.linearVelocity = nextVelocity;
        }

        private Vector2 GetCurrentSupportVelocity()
        {
            float age = Time.fixedTime - supportSampleFixedTime;
            return age >= -0.001f && age <= Time.fixedDeltaTime * 1.6f
                ? supportVelocity
                : Vector2.zero;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision == null || collision.otherCollider == null || collision.collider == null)
            {
                return;
            }

            if (Mathf.Abs(supportSampleFixedTime - Time.fixedTime) > 0.0001f)
            {
                supportNormalScore = float.NegativeInfinity;
            }

            Rigidbody2D otherBody = collision.rigidbody;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                Vector2 normal = contact.normal;
                Vector2 supportCenter = otherBody != null
                    ? otherBody.worldCenterOfMass
                    : (Vector2)collision.collider.bounds.center;
                Vector2 towardThisBody = rb.worldCenterOfMass - supportCenter;
                if (Vector2.Dot(normal, towardThisBody) < 0f)
                {
                    normal = -normal;
                }

                if (normal.y < 0.35f || normal.y <= supportNormalScore)
                {
                    continue;
                }

                supportNormalScore = normal.y;
                supportBody = otherBody != rb ? otherBody : null;
                StageConveyorBelt conveyor = collision.collider.GetComponentInParent<StageConveyorBelt>();
                supportVelocity = conveyor != null
                    ? conveyor.SurfaceVelocity
                    : otherBody != null && otherBody != rb
                        ? otherBody.GetPointVelocity(contact.point)
                        : Vector2.zero;
                supportSampleFixedTime = Time.fixedTime;
            }
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
                int hitCount = Physics2D.Raycast(
                    origins[i],
                    Vector2.down,
                    groundContactFilter,
                    groundRayResults,
                    slopeProbeDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit2D hit = groundRayResults[hitIndex];
                    if (hit.collider == null || hit.collider.attachedRigidbody == rb || hit.distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = hit.distance;
                    normal = hit.normal;
                }
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

            float takeoffVelocity = jumpVelocity * jumpMultiplier * jumpVelocityMultiplier;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, takeoffVelocity);
            PlayJumpSound();
            BeginJumpLoadProtection(takeoffVelocity);
            CarrySupportedPlayersWithJump(takeoffVelocity);
            lastJumpPressedAt = -100f;
            lastGroundedAt = -100f;
        }

        private void BeginJumpLoadProtection(float takeoffVelocity)
        {
            protectedJumpVelocity = takeoffVelocity;
            jumpProtectionStartedAt = Time.fixedTime;
            jumpProtectionUntil = Time.fixedTime + 0.1f;
        }

        private void ApplyJumpLoadProtection()
        {
            if (Time.fixedTime > jumpProtectionUntil)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, Time.fixedTime - jumpProtectionStartedAt);
            float expectedVelocity = protectedJumpVelocity
                + Physics2D.gravity.y * rb.gravityScale * elapsed;
            if (rb.linearVelocity.y < expectedVelocity)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, expectedVelocity);
            }
        }

        private void CarrySupportedPlayersWithJump(float takeoffVelocity)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            bool[] launched = new bool[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                launched[i] = players[i] == this;
            }

            // Repeat so a stack of three or more characters is carried from the
            // bottom upward, not only the character touching this body directly.
            for (int pass = 0; pass < players.Length; pass++)
            {
                bool changed = false;
                for (int i = 0; i < players.Length; i++)
                {
                    PlayerController2D passenger = players[i];
                    if (passenger == null || launched[i])
                    {
                        continue;
                    }

                    for (int supportIndex = 0; supportIndex < players.Length; supportIndex++)
                    {
                        PlayerController2D support = players[supportIndex];
                        if (!launched[supportIndex]
                            || support == null
                            || !passenger.IsCurrentlySupportedBy(support.rb))
                        {
                            continue;
                        }

                        passenger.rb.linearVelocity = new Vector2(
                            passenger.rb.linearVelocity.x,
                            Mathf.Max(passenger.rb.linearVelocity.y, takeoffVelocity));
                        passenger.BeginJumpLoadProtection(takeoffVelocity);
                        launched[i] = true;
                        changed = true;
                        break;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private bool IsCurrentlySupportedBy(Rigidbody2D candidate)
        {
            if (candidate == null || supportBody != candidate)
            {
                return false;
            }

            float age = Time.fixedTime - supportSampleFixedTime;
            return age >= -0.001f && age <= Time.fixedDeltaTime * 1.6f;
        }

        private void ApplyAirAbility()
        {
            bool gliding = !IsGrounded
                && canGlide
                && Input.GetButton("Jump")
                && rb.linearVelocity.y < currentGlideFallSpeed;
            if (gliding)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentGlideFallSpeed);
                GameSfx.PlayAt(wasGliding ? SfxId.BirdGlideLoop : SfxId.BirdFlap, transform.position);
            }
            wasGliding = gliding;

            bool wallSticking = !IsGrounded
                && canWallStick
                && Time.time - lastWallContactAt <= slimeStickDuration
                && rb.linearVelocity.y < currentSlimeWallSlideSpeed;
            if (wallSticking)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentSlimeWallSlideSpeed);
            }
            if (wallSticking != wasWallSticking)
            {
                GameSfx.PlayAt(wallSticking ? SfxId.SlimeStick : SfxId.SlimeRelease, transform.position);
            }
            wasWallSticking = wallSticking;
        }

        private void PlayJumpSound()
        {
            SfxId id = currentSpecies switch
            {
                DrawManager.Species.Cat => SfxId.CatJump,
                DrawManager.Species.Bird => SfxId.BirdFlap,
                DrawManager.Species.Snake => SfxId.SnakeJump,
                _ => SfxId.PlayerJump
            };
            GameSfx.PlayAt(id, transform.position);
        }

        private void PlayLandingSound(bool groundedBeforeProbe, float verticalSpeedBeforeProbe)
        {
            if (groundedBeforeProbe || !IsGrounded)
            {
                return;
            }

            SfxId id = currentSpecies == DrawManager.Species.Snake
                ? SfxId.SnakeLand
                : verticalSpeedBeforeProbe < -11f ? SfxId.PlayerLandHard : SfxId.PlayerLandSoft;
            GameSfx.PlayAt(id, transform.position);
        }

        private void PlayMovementSound()
        {
            if (!IsGrounded || Mathf.Abs(horizontalInput) < 0.2f || Mathf.Abs(rb.linearVelocity.x) < 0.5f || Time.time < nextFootstepTime)
            {
                return;
            }

            float speedRatio = Mathf.Clamp(Mathf.Abs(rb.linearVelocity.x) / Mathf.Max(1f, moveSpeed * moveSpeedMultiplier), 0.35f, 1f);
            nextFootstepTime = Time.time + Mathf.Lerp(0.42f, 0.19f, speedRatio);
            GameSfx.PlayAt(currentSpecies == DrawManager.Species.Cat ? SfxId.CatRunLoop : SfxId.PlayerFootstepPaper, transform.position);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            PlayerController2D otherPlayer = collision.collider.GetComponentInParent<PlayerController2D>();
            if (otherPlayer != null && otherPlayer != this && transform.position.y > otherPlayer.transform.position.y + 0.2f)
            {
                GameSfx.PlayAt(SfxId.PlayerStacked, transform.position);
                return;
            }

            if (collision.contactCount > 0
                && collision.collider.GetComponentInParent<CarryableObject>() != null
                && Mathf.Abs(horizontalInput) > 0.2f)
            {
                GameSfx.PlayAt(SfxId.PlayerPush, collision.GetContact(0).point);
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
            if (HasExternalOverlap(left, size)
                || HasExternalOverlap(right, size)
                || HasExternalOverlap(top, topSize))
            {
                lastWallContactAt = Time.time;
            }
        }

        private bool HasExternalOverlap(Vector2 center, Vector2 size)
        {
            int hitCount = Physics2D.OverlapBox(center, size, 0f, groundContactFilter, overlapResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit != null && hit.enabled && !hit.isTrigger && hit.attachedRigidbody != rb)
                {
                    return true;
                }
            }

            return false;
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
