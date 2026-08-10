using System.Collections.Generic;
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
        public const float MaximumSlimeAbilityInk = 350f;
        private const int WallJumpTrajectoryDashCount = 14;
        private const float WallJumpTrajectoryDuration = 0.95f;
        [SerializeField] private float slimeWallJumpHorizontalSpeed = 22f;
        [SerializeField] private float slimeWallJumpVerticalSpeed = 14.5f;
        [SerializeField] private float slimeWallJumpControlLockDuration = 0.26f;
        [SerializeField] private float slimeWallJumpMomentumDuration = 0.9f;
        [SerializeField] private float slimeWallJumpAirDrag = 1.5f;

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
        private bool isTouchingSlimeWall;
        private float nextFootstepTime;
        private bool slimMode;
        private int lastWallSide;
        private float wallJumpControlLockUntil = -100f;
        private float wallJumpMomentumUntil = -100f;
        private float wallJumpLockedVelocityX;
        private int wallJumpSourceSide;
        private int facingDirection = 1;
        private bool turtleShelled;
        private bool turtleTurnHeld;
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
        private readonly List<ContactPoint2D> bodyContacts = new List<ContactPoint2D>(32);
        private readonly List<Collider2D> wallOverlapResults = new List<Collider2D>(16);
        private ContactFilter2D groundContactFilter;
        private ContactFilter2D wallContactFilter;
        private Collider2D[] bodyColliderCache;
        private LineRenderer[] wallJumpTrajectoryLines;
        private Material wallJumpTrajectoryMaterial;
        private bool scriptedInputEnabled;
        private float scriptedHorizontalInput;
        private bool scriptedJumpHeld;
        private bool scriptedJumpPressed;

        public bool IsGrounded { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public int FacingDirection => facingDirection;
        public bool IsInvulnerable => currentSpecies == DrawManager.Species.Turtle && turtleShelled;
        public bool IsTurtleShelled => currentSpecies == DrawManager.Species.Turtle && turtleShelled;
        public bool IsWallSticking => wasWallSticking;

        public void SetScriptedInput(float horizontal, bool jumpHeld, bool jumpPressed = false)
        {
            scriptedInputEnabled = true;
            scriptedHorizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
            horizontalInput = scriptedHorizontalInput;
            scriptedJumpHeld = jumpHeld;
            if (jumpPressed)
            {
                lastJumpPressedAt = Time.time;
            }
        }

        public void ClearScriptedInput()
        {
            scriptedInputEnabled = false;
            scriptedHorizontalInput = 0f;
            scriptedJumpHeld = false;
            scriptedJumpPressed = false;
        }

        public void ApplyRemoteTurtleShellState(bool active)
        {
            SetTurtleShellState(active && currentSpecies == DrawManager.Species.Turtle);
        }

        public void InvalidateBodyColliderCache()
        {
            bodyColliderCache = null;
        }

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

        public static float CalculateSlimeStickStrength(float slimeInk)
        {
            float inkRatio = Mathf.Clamp01(Mathf.Max(0f, slimeInk) / MaximumSlimeAbilityInk);
            return Mathf.Lerp(1f, 0.15f, inkRatio);
        }

        public static float CalculateSlimeMoveSpeedMultiplier(float slimeInk)
        {
            float inkRatio = Mathf.Clamp01(Mathf.Max(0f, slimeInk) / MaximumSlimeAbilityInk);
            return Mathf.Lerp(0.55f, 1.75f, inkRatio);
        }

        public static float CalculateSlimeJumpMultiplier(float slimeInk)
        {
            float inkRatio = Mathf.Clamp01(Mathf.Max(0f, slimeInk) / MaximumSlimeAbilityInk);
            return Mathf.Lerp(0.6f, 1.55f, inkRatio);
        }

        public void ApplySpeciesMovement(
            DrawManager.Species species,
            float wingInk = 0f,
            float catLegInk = 0f,
            float turtleInk = 0f,
            float slimeInk = 0f)
        {
            SetTurtleShellState(false);
            SetTurtleRotation(false);
            currentSpecies = species;
            moveSpeedMultiplier = 1f;
            jumpVelocityMultiplier = 1f;
            canGlide = false;
            canWallStick = false;
            slimMode = false;
            currentGlideFallSpeed = CalculateBirdGlideFallSpeed(wingInk);
            currentSlimeWallSlideSpeed = Mathf.Lerp(-2.6f, 0f, CalculateSlimeStickStrength(slimeInk));

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
                case DrawManager.Species.Turtle:
                    moveSpeedMultiplier = 0.78f;
                    jumpVelocityMultiplier = 0.82f;
                    break;
                case DrawManager.Species.Slime:
                    canWallStick = true;
                    moveSpeedMultiplier = CalculateSlimeMoveSpeedMultiplier(slimeInk);
                    jumpVelocityMultiplier = CalculateSlimeJumpMultiplier(slimeInk);
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
            wallContactFilter = new ContactFilter2D();
            wallContactFilter.useTriggers = false;
            wallContactFilter.useLayerMask = false;
            wallContactFilter.useDepth = false;
            wallContactFilter.useNormalAngle = false;

            if (groundCheck == null)
            {
                GameObject check = new GameObject("GroundCheck");
                check.transform.SetParent(transform);
                check.transform.localPosition = new Vector3(0f, -1.25f, 0f);
                groundCheck = check.transform;
            }

            CreateWallJumpTrajectoryPreview();
        }

        private void OnDestroy()
        {
            if (wallJumpTrajectoryMaterial != null)
            {
                Destroy(wallJumpTrajectoryMaterial);
            }
        }

        private void Update()
        {
            if (!controlsEnabled)
            {
                horizontalInput = 0f;
                return;
            }

            horizontalInput = scriptedInputEnabled
                ? scriptedHorizontalInput
                : Input.GetAxisRaw("Horizontal");
            UpdateFacing();
            UpdateTurtleAbilityInput();

            bool jumpPressed;
            if (scriptedInputEnabled)
            {
                jumpPressed = scriptedJumpPressed;
                scriptedJumpPressed = false;
            }
            else
            {
                jumpPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
                if (currentSpecies != DrawManager.Species.Turtle)
                {
                    jumpPressed |= Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
                }
            }

            if (jumpPressed)
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
            UpdateWallJumpTrajectoryPreview();
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled)
            {
                horizontalInput = 0f;
                SetTurtleShellState(false);
                SetTurtleRotation(false);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        public void ResetMotion()
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            wallJumpControlLockUntil = -100f;
            wallJumpMomentumUntil = -100f;
            wallJumpSourceSide = 0;
            isTouchingSlimeWall = false;
            lastWallSide = 0;
            SetTurtleShellState(false);
            SetTurtleRotation(false);
        }

        private void UpdateTurtleAbilityInput()
        {
            if (currentSpecies != DrawManager.Species.Turtle)
            {
                SetTurtleShellState(false);
                SetTurtleRotation(false);
                return;
            }

            SetTurtleShellState(scriptedInputEnabled ? scriptedJumpHeld : Input.GetKey(KeyCode.Space));
            SetTurtleRotation(!scriptedInputEnabled && Input.GetKey(KeyCode.F));
            if (turtleShelled)
            {
                horizontalInput = 0f;
            }
        }

        private void SetTurtleShellState(bool active)
        {
            if (turtleShelled == active)
            {
                return;
            }

            turtleShelled = active;
            bodyBuilder?.SetTurtleShellPose(active);
        }

        private void SetTurtleRotation(bool active)
        {
            if (turtleTurnHeld == active && !active)
            {
                return;
            }

            turtleTurnHeld = active;
            if (rb != null)
            {
                rb.rotation = active && currentSpecies == DrawManager.Species.Turtle
                    ? -90f * facingDirection
                    : 0f;
                rb.angularVelocity = 0f;
            }
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
            Collider2D[] colliders = bodyColliderCache;
            if (colliders == null)
            {
                colliders = GetComponentsInChildren<Collider2D>();
                bodyColliderCache = colliders;
            }
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
            if (Time.fixedTime < wallJumpControlLockUntil)
            {
                rb.linearVelocity = new Vector2(wallJumpLockedVelocityX, rb.linearVelocity.y);
                return;
            }

            if (Time.fixedTime < wallJumpMomentumUntil)
            {
                float velocityX = rb.linearVelocity.x;
                if (Mathf.Abs(horizontalInput) <= 0.01f)
                {
                    velocityX = Mathf.MoveTowards(
                        velocityX,
                        0f,
                        slimeWallJumpAirDrag * Time.fixedDeltaTime);
                }
                else
                {
                    float desiredX = horizontalInput * Mathf.Max(
                        moveSpeed * moveSpeedMultiplier,
                        slimeWallJumpHorizontalSpeed);
                    bool steeringWithMomentum = Mathf.Sign(desiredX) == Mathf.Sign(velocityX);
                    if (steeringWithMomentum)
                    {
                        if (Mathf.Abs(desiredX) > Mathf.Abs(velocityX))
                        {
                            velocityX = Mathf.MoveTowards(
                                velocityX,
                                desiredX,
                                acceleration * 0.35f * Time.fixedDeltaTime);
                        }
                    }
                    else
                    {
                        velocityX = Mathf.MoveTowards(
                            velocityX,
                            desiredX,
                            acceleration * 0.12f * Time.fixedDeltaTime);
                    }
                }

                rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
                return;
            }

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

            RecordSlimeWallContact(collision);

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

            bool canWallJump = HasSlimeWallGrip()
                && lastWallSide != 0
                && isTouchingSlimeWall;
            if (hasBufferedJump && canWallJump)
            {
                float wallTakeoffVelocity = Mathf.Max(
                    slimeWallJumpVerticalSpeed,
                    jumpVelocity * jumpMultiplier * jumpVelocityMultiplier);
                float horizontalSpeed = Mathf.Max(
                    slimeWallJumpHorizontalSpeed,
                    moveSpeed * moveSpeedMultiplier * 2.9f);
                float launchDirection = -lastWallSide;
                wallJumpSourceSide = lastWallSide;
                wallJumpLockedVelocityX = launchDirection * horizontalSpeed;
                wallJumpControlLockUntil = Time.fixedTime + slimeWallJumpControlLockDuration;
                wallJumpMomentumUntil = Time.fixedTime + slimeWallJumpMomentumDuration;
                rb.linearVelocity = new Vector2(wallJumpLockedVelocityX, wallTakeoffVelocity);
                facingDirection = launchDirection < 0f ? -1 : 1;
                bodyBuilder?.SetFacingDirection(facingDirection);
                GameSfx.PlayAt(SfxId.SlimeRelease, transform.position);
                PlayJumpSound();
                // Wall-jump vertical motion must be a pure gravity arc. The
                // regular load-protection correction is only for floor jumps.
                jumpProtectionUntil = -100f;
                lastJumpPressedAt = -100f;
                lastGroundedAt = -100f;
                lastWallSide = 0;
                return;
            }

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
            bool wallJumping = Time.fixedTime < wallJumpMomentumUntil;
            bool reachedOppositeWall = wallJumping
                && wallJumpSourceSide != 0
                && lastWallSide == -wallJumpSourceSide
                && isTouchingSlimeWall;
            if (reachedOppositeWall)
            {
                wallJumpControlLockUntil = -100f;
                wallJumpMomentumUntil = -100f;
                wallJumpSourceSide = 0;
                wallJumping = false;
            }

            bool isBuiltAsBird = bodyBuilder == null
                ? currentSpecies == DrawManager.Species.Bird
                : bodyBuilder.BuiltSpecies == DrawManager.Species.Bird;
            bool gliding = !IsGrounded
                && !wallJumping
                && canGlide
                && currentSpecies == DrawManager.Species.Bird
                && isBuiltAsBird
                && (scriptedInputEnabled ? scriptedJumpHeld : Input.GetButton("Jump"))
                && rb.linearVelocity.y < currentGlideFallSpeed;
            if (gliding)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentGlideFallSpeed);
                GameSfx.PlayAt(wasGliding ? SfxId.BirdGlideLoop : SfxId.BirdFlap, transform.position);
            }
            wasGliding = gliding;

            bool wallSticking = !wallJumping
                && HasSlimeWallGrip()
                && isTouchingSlimeWall
                && rb.linearVelocity.y <= 0.1f;
            if (wallSticking)
            {
                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x,
                    Mathf.Max(rb.linearVelocity.y, currentSlimeWallSlideSpeed));
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
                DrawManager.Species.Turtle => SfxId.TurtleJump,
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

            SfxId id = currentSpecies == DrawManager.Species.Turtle
                ? SfxId.TurtleLand
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

            RecordSlimeWallContact(collision);

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

        private void RecordSlimeWallContact(Collision2D collision)
        {
            if (!HasSlimeWallGrip() || collision == null)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector2 normal = collision.GetContact(i).normal;
                // A real side contact is more reliable than an AABB probe for
                // densely hand-drawn slime bodies. Sloped floors and ceilings
                // must not activate the wall grip.
                if (Mathf.Abs(normal.x) >= 0.55f && Mathf.Abs(normal.y) <= 0.78f)
                {
                    isTouchingSlimeWall = true;
                    lastWallSide = collision.GetContact(i).point.x >= rb.worldCenterOfMass.x ? 1 : -1;
                    return;
                }
            }
        }

        private void UpdateWallContact()
        {
            // Wall grip is deliberately not a coyote-time ability. Clear the
            // state every physics step so moving away from a wall immediately
            // disables sticking, wall jumping, and the trajectory preview.
            isTouchingSlimeWall = false;
            lastWallSide = 0;
            if (!HasSlimeWallGrip())
            {
                return;
            }

            // Read contacts from the Rigidbody itself. A hand-drawn body can own
            // dozens of child colliders, so relying only on a small overlap
            // buffer can fill it with the slime's own lines before finding the wall.
            bodyContacts.Clear();
            rb.GetContacts(bodyContacts);
            for (int i = 0; i < bodyContacts.Count; i++)
            {
                Vector2 normal = bodyContacts[i].normal;
                if (Mathf.Abs(normal.x) >= 0.55f && Mathf.Abs(normal.y) <= 0.78f)
                {
                    isTouchingSlimeWall = true;
                    lastWallSide = bodyContacts[i].point.x >= rb.worldCenterOfMass.x ? 1 : -1;
                    return;
                }
            }

            if (!TryGetBodyBounds(out Bounds bodyBounds))
            {
                return;
            }

            if (HasNearbyVerticalSurface(bodyBounds, Vector2.left)
                || HasNearbyVerticalSurface(bodyBounds, Vector2.right))
            {
                isTouchingSlimeWall = true;
            }
        }

        private bool HasSlimeWallGrip()
        {
            return canWallStick
                || (bodyBuilder != null && bodyBuilder.BuiltSpecies == DrawManager.Species.Slime);
        }

        private bool HasNearbyVerticalSurface(Bounds bodyBounds, Vector2 direction)
        {
            float sideX = direction.x < 0f
                ? bodyBounds.min.x - 0.01f
                : bodyBounds.max.x + 0.01f;
            Vector2 center = new Vector2(sideX, bodyBounds.center.y);
            Vector2 size = new Vector2(0.025f, Mathf.Max(0.45f, bodyBounds.size.y * 0.82f));
            wallOverlapResults.Clear();
            Physics2D.OverlapBox(center, size, 0f, wallContactFilter, wallOverlapResults);
            for (int i = 0; i < wallOverlapResults.Count; i++)
            {
                Collider2D hit = wallOverlapResults[i];
                if (hit != null
                    && hit.enabled
                    && !hit.isTrigger
                    && hit.attachedRigidbody != rb)
                {
                    lastWallSide = direction.x < 0f ? -1 : 1;
                    return true;
                }
            }

            return false;
        }

        private void CreateWallJumpTrajectoryPreview()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            wallJumpTrajectoryMaterial = new Material(shader)
            {
                name = "Slime Wall Jump Trajectory Material"
            };
            GameObject root = new GameObject("Slime Wall Jump Trajectory");
            root.transform.SetParent(transform, false);
            wallJumpTrajectoryLines = new LineRenderer[WallJumpTrajectoryDashCount + 2];
            for (int i = 0; i < wallJumpTrajectoryLines.Length; i++)
            {
                GameObject lineObject = new GameObject(i < WallJumpTrajectoryDashCount
                    ? "Trajectory Dash " + i
                    : "Trajectory Arrow " + (i - WallJumpTrajectoryDashCount));
                lineObject.transform.SetParent(root.transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = i < WallJumpTrajectoryDashCount ? 0.045f : 0.055f;
                line.numCapVertices = 2;
                line.sharedMaterial = wallJumpTrajectoryMaterial;
                line.sortingOrder = 215;
                line.enabled = false;
                wallJumpTrajectoryLines[i] = line;
            }
        }

        private void UpdateWallJumpTrajectoryPreview()
        {
            if (wallJumpTrajectoryLines == null)
            {
                return;
            }

            bool show = controlsEnabled
                && rb != null
                && rb.simulated
                && HasSlimeWallGrip()
                && lastWallSide != 0
                && isTouchingSlimeWall
                && Time.fixedTime >= wallJumpControlLockUntil;
            if (!show || !TryGetBodyBounds(out Bounds bodyBounds))
            {
                SetWallJumpTrajectoryVisible(false);
                return;
            }

            float verticalSpeed = Mathf.Max(
                slimeWallJumpVerticalSpeed,
                jumpVelocity * jumpMultiplier * jumpVelocityMultiplier);
            float horizontalSpeed = Mathf.Max(
                slimeWallJumpHorizontalSpeed,
                moveSpeed * moveSpeedMultiplier * 2.9f);
            Vector2 start = new Vector2(bodyBounds.center.x, bodyBounds.center.y);
            Vector2 initialVelocity = new Vector2(-lastWallSide * horizontalSpeed, verticalSpeed);
            Vector2 gravity = Physics2D.gravity * rb.gravityScale;

            Color previewColor = bodyBuilder != null
                ? Color.Lerp(bodyBuilder.PlayerColor, Color.white, 0.32f)
                : new Color(0.2f, 0.72f, 1f, 1f);
            previewColor.a = 0.28f;

            for (int i = 0; i < WallJumpTrajectoryDashCount; i++)
            {
                float dashStart = i / (float)WallJumpTrajectoryDashCount * WallJumpTrajectoryDuration;
                float dashEnd = (i + 0.58f) / WallJumpTrajectoryDashCount * WallJumpTrajectoryDuration;
                SetTrajectoryLine(
                    wallJumpTrajectoryLines[i],
                    EvaluateTrajectory(start, initialVelocity, gravity, dashStart),
                    EvaluateTrajectory(start, initialVelocity, gravity, dashEnd),
                    previewColor);
            }

            Vector2 end = EvaluateTrajectory(start, initialVelocity, gravity, WallJumpTrajectoryDuration);
            Vector2 endVelocity = initialVelocity + gravity * WallJumpTrajectoryDuration;
            Vector2 direction = endVelocity.sqrMagnitude > 0.001f ? endVelocity.normalized : Vector2.right;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            const float arrowLength = 0.28f;
            SetTrajectoryLine(
                wallJumpTrajectoryLines[WallJumpTrajectoryDashCount],
                end + (-direction + normal * 0.52f) * arrowLength,
                end,
                previewColor);
            SetTrajectoryLine(
                wallJumpTrajectoryLines[WallJumpTrajectoryDashCount + 1],
                end + (-direction - normal * 0.52f) * arrowLength,
                end,
                previewColor);
        }

        private static Vector2 EvaluateTrajectory(
            Vector2 start,
            Vector2 initialVelocity,
            Vector2 gravity,
            float time)
        {
            return start + initialVelocity * time + gravity * (0.5f * time * time);
        }

        private static void SetTrajectoryLine(LineRenderer line, Vector2 start, Vector2 end, Color color)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void SetWallJumpTrajectoryVisible(bool visible)
        {
            for (int i = 0; i < wallJumpTrajectoryLines.Length; i++)
            {
                if (wallJumpTrajectoryLines[i] != null)
                {
                    wallJumpTrajectoryLines[i].enabled = visible;
                }
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
