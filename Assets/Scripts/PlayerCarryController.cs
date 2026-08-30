using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(PlayerAbilityController))]
    public sealed class PlayerCarryController : MonoBehaviour
    {
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private BodyBuilder bodyBuilder;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private LayerMask carryableLayerMask = ~0;
        [SerializeField] private float pickupRadius = 1.7f;
        [SerializeField] private float pickupReach = 0.9f;
        [SerializeField] private float throwSpeed = 22f;
        [SerializeField] private float armInkThrowScale = 0.0175f;
        [SerializeField] private float heldPlayerThrowMultiplier = 1.25f;
        [SerializeField] private float throwAimSpeed = 1.35f;
        [SerializeField] private float throwPreviewLength = 1.8f;
        [SerializeField] private float postThrowCollisionIgnoreTime = 0.22f;
        [SerializeField] private float postThrowCollisionRestoreTimeout = 1.2f;
        [SerializeField] private float slimeFriendAttachReach = 0.45f;

        private const float MaxHeldPlayerCarrierHorizontalSpeed = 28f;
        private const float CatScratchCooldown = 0.7f;

        // A hand-drawn body can contain far more than 64 segment colliders.
        // A fixed NonAlloc buffer could fill with the player's own long legs
        // before the crate underneath was returned by the physics query.
        private readonly List<Collider2D> pickupHits = new List<Collider2D>(128);
        private readonly List<Collider2D> heldColliders = new List<Collider2D>();
        private readonly HashSet<Collider2D> heldColliderSet = new HashSet<Collider2D>();
        private readonly List<bool> heldColliderEnabledStates = new List<bool>();
        private readonly List<bool> heldColliderTriggerStates = new List<bool>();
        private readonly List<Collider2D> heldPlayerColliderScratch = new List<Collider2D>();
        private readonly List<Collider2D> carrierColliderScratch = new List<Collider2D>();
        private readonly List<Renderer> heldRenderers = new List<Renderer>();
        private readonly List<int> heldRendererSortingOrders = new List<int>();
        private readonly List<bool> heldRendererEnabledStates = new List<bool>();
        private Transform heldTransform;
        private CarryableObject heldObject;
        private PlayerController2D heldPlayerController;
        private bool heldPlayerPreviousControlsEnabled;
        private Rigidbody2D heldBody;
        private RigidbodyType2D previousBodyType;
        private float previousGravityScale;
        private bool previousFreezeRotation;
        private LineRenderer throwPreviewLine;
        private LineRenderer throwPreviewHeadA;
        private LineRenderer throwPreviewHeadB;
        private Vector2 displayedThrowDirection = Vector2.up;
        private int displayedThrowFacingDirection = 1;
        private bool hasDisplayedThrowDirection;
        private Material previewMaterial;
        private string heldOnlinePlayerId;
        private StageGimmickSyncManager gimmickSyncManager;
        private ContactFilter2D pickupContactFilter;
        private PlayerController2D slimeAttachedPlayer;
        private Rigidbody2D slimeAttachedBody;
        private Vector3 slimeAttachLocalOffset;
        private RigidbodyType2D slimePreviousBodyType;
        private float slimePreviousGravityScale;
        private bool slimePreviousFreezeRotation;
        private bool slimeAttachedTargetPreviousControlsEnabled;
        private string friendAttachedOnlinePlayerId;
        private Collider2D[] slimeOwnColliders = new Collider2D[0];
        private Collider2D[] slimeTargetColliders = new Collider2D[0];
        private PlayerController2D remoteSlimeVisualTarget;
        private LineRenderer slimeAttachBridge;
        private LineRenderer slimeAttachRing;
        private readonly LineRenderer[] catClawLines = new LineRenderer[3];
        private readonly LineRenderer[] birdBeakLines = new LineRenderer[2];
        private CarryableObject catClawedObject;
        private Rigidbody2D catClawedBody;
        private RigidbodyType2D catClawedPreviousBodyType;
        private float catClawedPreviousGravityScale;
        private bool catClawedPreviousFreezeRotation;
        private Vector3 catClawedLocalOffset;
        private Quaternion catClawedLocalRotation = Quaternion.identity;
        private readonly List<Collider2D> catClawedColliders = new List<Collider2D>();
        private readonly List<bool> catClawedColliderEnabledStates = new List<bool>();
        private readonly List<bool> catClawedColliderTriggerStates = new List<bool>();
        private bool scriptedSlimeAttachment;
        private bool scriptedSlimeAttachmentHeld;
        private bool scriptedActionEnabled;
        private bool catScratchConsumesHold;
        private float nextCatScratchTime;
        private bool remoteWeaponAimEnabled;
        private Vector2 remoteWeaponAimDirection = Vector2.right;
        private Vector2 currentWeaponAimDirection = Vector2.right;

        public bool IsHolding => heldTransform != null;
        public bool IsAimingWeapon => heldTransform != null
            && (heldTransform.GetComponent<StageGun>() != null || heldTransform.GetComponent<StageBazooka>() != null);
        public Vector2 CurrentOnlineWeaponAimDirection => currentWeaponAimDirection.sqrMagnitude > 0.01f
            ? currentWeaponAimDirection.normalized
            : new Vector2(GetFacingDirection(), 0f);
        public string CurrentOnlineCarriedPlayerId => !string.IsNullOrEmpty(heldOnlinePlayerId)
            ? heldOnlinePlayerId
            : friendAttachedOnlinePlayerId;
        public string CurrentOnlineCarryAction => !string.IsNullOrEmpty(heldOnlinePlayerId)
            ? "pickup"
            : !string.IsNullOrEmpty(friendAttachedOnlinePlayerId) ? "friend_grab" : string.Empty;
        public Vector2 CurrentOnlineCarryOffset => !string.IsNullOrEmpty(friendAttachedOnlinePlayerId)
            ? (Vector2)slimeAttachLocalOffset
            : Vector2.zero;
        public string SlimeAttachedOnlinePlayerId => slimeAttachedPlayer != null && stageManager != null
            ? stageManager.GetOnlinePlayerId(slimeAttachedPlayer)
            : null;

        public bool IsHoldingTarget(Transform target)
        {
            return target != null
                && (heldTransform == target
                    || catClawedObject != null && catClawedObject.transform == target);
        }

        public void ApplyRemoteWeaponAim(bool aiming, Vector2 direction)
        {
            remoteWeaponAimEnabled = aiming;
            if (direction.sqrMagnitude > 0.01f)
            {
                remoteWeaponAimDirection = direction.normalized;
            }
        }

        public bool IsDraggingFriend(Transform target)
        {
            return IsFriendCarrier() && target != null && slimeAttachedPlayer != null
                && slimeAttachedPlayer.transform == target;
        }

        public bool IsCarryingFriend => IsFriendCarrier() && slimeAttachedPlayer != null;

        public bool ReleaseIfHolding(Transform target)
        {
            if (target != null && catClawedObject != null && catClawedObject.transform == target)
            {
                DetachCatFromObject(false);
                return true;
            }
            if (target == null || heldTransform != target)
            {
                return false;
            }

            DropHeld(Vector2.zero);
            return true;
        }

        public bool ReleaseIfDraggingFriend(Transform target)
        {
            if (!IsDraggingFriend(target))
            {
                return false;
            }

            DetachSlimeFromFriend(false);
            return true;
        }

        public bool TryPickupForScript()
        {
            if (heldTransform == null)
            {
                TryPickup();
            }
            return heldTransform != null;
        }

        public bool ThrowHeldForScript(Vector2 direction)
        {
            if (heldTransform == null)
            {
                return false;
            }
            if (heldTransform.GetComponent<StageGun>() != null || heldTransform.GetComponent<StageBazooka>() != null)
            {
                DropHeld(Vector2.zero);
                return true;
            }
            Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 throwVelocity = normalized * GetCurrentThrowSpeed() * multiplier;
            GameSfx.PlayAt(SfxId.HumanThrow, transform.position);
            DropHeld(throwVelocity);
            return true;
        }

        public bool SetSlimeAttachmentForScript(bool attached)
        {
            if (!CanAttachToFriend())
            {
                return false;
            }
            scriptedSlimeAttachment = true;
            scriptedSlimeAttachmentHeld = attached;
            if (attached)
            {
                if (slimeAttachedPlayer == null && catClawedObject == null)
                {
                    TryAttachSlimeToFriend();
                    if (slimeAttachedPlayer == null) TryAttachCatToObject();
                }
            }
            else
            {
                DetachSlimeFromFriend(true);
                DetachCatFromObject(true);
            }
            return slimeAttachedPlayer != null || catClawedObject != null;
        }

        public Vector2 GetThrowDirectionForScript()
        {
            return GetThrowDirection();
        }

        public void ApplyActionForScript(
            bool actionHeld,
            bool actionPressed,
            Vector2 recordedThrowDirection,
            bool useRecordedThrowDirection)
        {
            scriptedActionEnabled = true;
            if (CanAttachToFriend())
            {
                SetSlimeAttachmentForScript(actionHeld);
                return;
            }
            if (!IsHuman() || !actionPressed)
            {
                return;
            }
            if (heldTransform == null)
            {
                TryPickup();
            }
            else if (useRecordedThrowDirection)
            {
                ThrowHeldForScript(recordedThrowDirection);
            }
            else
            {
                ThrowHeld();
            }
        }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController2D>();
            }

            if (abilityController == null)
            {
                abilityController = GetComponent<PlayerAbilityController>();
            }

            if (bodyBuilder == null)
            {
                bodyBuilder = GetComponent<BodyBuilder>();
            }

            if (playerBody == null)
            {
                playerBody = GetComponent<Rigidbody2D>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindFirstObjectByType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }

            carryableLayerMask |= 1 << gameObject.layer;
            pickupContactFilter = new ContactFilter2D();
            pickupContactFilter.SetLayerMask(carryableLayerMask);
            pickupContactFilter.useTriggers = false;
            CreateThrowPreview();
            CreateSlimeAttachmentVisual();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            DetachSlimeFromFriend(false);
            DetachCatFromObject(false);
            DropHeld(Vector2.zero);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (IsRemoteOnlineReplica())
            {
                // A remote avatar exists for rendering and collisions only. It
                // must never consume this machine's F key, even for one frame
                // while a throw restores the carried body's physics state.
                return;
            }

            if (playerController != null && !playerController.ControlsEnabled)
            {
                DetachSlimeFromFriend(false);
                DetachCatFromObject(false);
                return;
            }

            if (playerController != null && playerController.IsFriendCarried)
            {
                // Prevent reciprocal bird/cat grabs while being carried. The
                // turtle's shell and turn inputs remain handled by its movement
                // controller, so those abilities are still available.
                DetachSlimeFromFriend(false);
                DetachCatFromObject(false);
                HandleHeldWeaponInput();
                return;
            }

            if (scriptedActionEnabled)
            {
                return;
            }

            bool ricochetWeaponMode = stageManager != null && stageManager.CurrentStageId == "10-3";
            if (!ricochetWeaponMode && CanAttachToFriend())
            {
                DropHeld(Vector2.zero);
                if (IsCat())
                {
                    if (Input.GetKeyDown(KeyCode.F) && Time.time >= nextCatScratchTime)
                    {
                        nextCatScratchTime = Time.time + CatScratchCooldown;
                        PlayCatScratchEffect();
                        if (TryScratchEnemy())
                        {
                            catScratchConsumesHold = true;
                            DetachSlimeFromFriend(false);
                            DetachCatFromObject(false);
                        }
                    }
                    if (catScratchConsumesHold)
                    {
                        if (!Input.GetKey(KeyCode.F)) catScratchConsumesHold = false;
                        return;
                    }
                }
                bool attachHeld = scriptedSlimeAttachment
                    ? scriptedSlimeAttachmentHeld
                    : Input.GetKey(KeyCode.F);
                if (attachHeld)
                {
                    if (slimeAttachedPlayer == null && catClawedObject == null)
                    {
                        TryAttachSlimeToFriend();
                        if (slimeAttachedPlayer == null)
                        {
                            TryAttachCatToObject();
                        }
                    }
                }
                else
                {
                    DetachSlimeFromFriend(true);
                    DetachCatFromObject(true);
                }
                return;
            }

            DetachSlimeFromFriend(false);
            DetachCatFromObject(false);
            if (!IsHuman() && !ricochetWeaponMode)
            {
                DropHeld(Vector2.zero);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (heldTransform == null)
                {
                    TryPickup();
                }
                else if (heldTransform.GetComponent<StageGun>() != null || heldTransform.GetComponent<StageBazooka>() != null)
                {
                    DropHeld(Vector2.zero);
                }
                else
                {
                    ThrowHeld();
                }
            }

            StageGun heldGun = heldTransform != null ? heldTransform.GetComponent<StageGun>() : null;
            StageBazooka heldBazooka = heldTransform != null ? heldTransform.GetComponent<StageBazooka>() : null;
            if (heldGun != null && Input.GetMouseButtonDown(0))
            {
                Camera camera = Camera.main;
                if (camera != null) heldGun.TryFire(camera.ScreenToWorldPoint(Input.mousePosition));
            }
            else if (heldBazooka != null && Input.GetMouseButtonDown(0))
            {
                Camera camera = Camera.main;
                if (camera != null) heldBazooka.TryFire(camera.ScreenToWorldPoint(Input.mousePosition));
            }
        }

        private void FixedUpdate()
        {
            bool draggingFriend = slimeAttachedPlayer != null && IsFriendCarrier();
            if (draggingFriend)
            {
                RefreshFriendAttachmentCollisionIgnores();
            }

            if ((heldPlayerController == null && !draggingFriend) || playerBody == null)
            {
                return;
            }

            Vector2 velocity = playerBody.linearVelocity;
            if (float.IsNaN(velocity.x) || float.IsInfinity(velocity.x)
                || float.IsNaN(velocity.y) || float.IsInfinity(velocity.y))
            {
                playerBody.linearVelocity = Vector2.zero;
                return;
            }

            // A newly rebuilt carried-body collider used to be able to overlap
            // the carrier for one physics step and launch the carrier backwards.
            // Human movement, wind and speed rings remain below this generous
            // ceiling; only the tunnelling-producing impulse is discarded.
            if (!playerController.HasWeaponRecoilMomentum
                && Mathf.Abs(velocity.x) > MaxHeldPlayerCarrierHorizontalSpeed)
            {
                velocity.x = Mathf.Clamp(
                    velocity.x,
                    -MaxHeldPlayerCarrierHorizontalSpeed,
                    MaxHeldPlayerCarrierHorizontalSpeed);
                playerBody.linearVelocity = velocity;
            }

        }

        private void LateUpdate()
        {
            if (catClawedObject == null && (catClawedBody != null || catClawedColliders.Count > 0))
            {
                DetachCatFromObject(false);
            }
            if (slimeAttachedPlayer != null)
            {
                FollowSlimeAttachedFriend();
            }
            else if (catClawedObject != null)
            {
                UpdateCatClawedObjectPose();
                if (IsBird()) UpdateBirdObjectAttachmentVisual();
                else UpdateCatObjectAttachmentVisual();
            }
            else if (remoteSlimeVisualTarget != null)
            {
                UpdateFriendAttachmentVisual(remoteSlimeVisualTarget, false);
            }
            else
            {
                SetSlimeAttachmentVisualVisible(false);
            }

            if (heldTransform == null)
            {
                if (heldBody != null
                    || heldPlayerController != null
                    || heldObject != null
                    || !string.IsNullOrEmpty(heldOnlinePlayerId)
                    || heldColliders.Count > 0)
                {
                    DropHeld(Vector2.zero);
                }
                SetThrowPreviewVisible(false);
                return;
            }

            RefreshHeldPlayerCollisionIgnores();

            Vector3 anchor = GetHoldPosition();
            heldTransform.position = anchor;
            StageGun gun = heldTransform.GetComponent<StageGun>();
            StageBazooka bazooka = heldTransform.GetComponent<StageBazooka>();
            if (gun != null)
            {
                Vector2 aim = ResolveHeldWeaponAim(anchor);
                gun.UpdateHeldPose(anchor, aim);
            }
            else if (bazooka != null)
            {
                Vector2 aim = ResolveHeldWeaponAim(anchor);
                bazooka.UpdateHeldPose(anchor, aim);
            }
            else
            {
                heldTransform.rotation = Quaternion.identity;
            }
            if (heldBody != null)
            {
                heldBody.linearVelocity = Vector2.zero;
                heldBody.angularVelocity = 0f;
            }

            bodyBuilder?.SetCarryPose(true, GetFacingDirection(), anchor);
            if (gun != null || bazooka != null) SetThrowPreviewVisible(false);
            else UpdateThrowPreview(anchor);
        }

        private Vector2 ResolveHeldWeaponAim(Vector3 anchor)
        {
            Vector2 direction;
            if (IsRemoteOnlineReplica() && remoteWeaponAimEnabled)
            {
                direction = remoteWeaponAimDirection;
            }
            else
            {
                Camera camera = Camera.main;
                Vector2 aimWorld = camera != null
                    ? (Vector2)camera.ScreenToWorldPoint(Input.mousePosition)
                    : (Vector2)anchor + Vector2.right * GetFacingDirection();
                direction = aimWorld - (Vector2)anchor;
            }

            if (direction.sqrMagnitude < 0.01f) direction = new Vector2(GetFacingDirection(), 0f);
            currentWeaponAimDirection = direction.normalized;
            return (Vector2)anchor + currentWeaponAimDirection * 8f;
        }

        private void HandleHeldWeaponInput()
        {
            if (!IsHuman() || heldTransform == null) return;
            if (Input.GetKeyDown(KeyCode.F)
                && (heldTransform.GetComponent<StageGun>() != null || heldTransform.GetComponent<StageBazooka>() != null))
            {
                DropHeld(Vector2.zero);
                return;
            }
            if (!Input.GetMouseButtonDown(0)) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector2 aim = camera.ScreenToWorldPoint(Input.mousePosition);
            StageGun gun = heldTransform.GetComponent<StageGun>();
            if (gun != null) gun.TryFire(aim);
            else heldTransform.GetComponent<StageBazooka>()?.TryFire(aim);
        }

        public string GetWeaponRecoilTargetOnlineId()
        {
            PlayerCarryController recoilTarget = FindCarrierOfThisPlayer() ?? this;
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            string onlineCarrierId = stageManager != null
                ? stageManager.GetOnlineCarrierPlayerId(playerController)
                : null;
            if (!string.IsNullOrEmpty(onlineCarrierId)) return onlineCarrierId;
            return stageManager != null ? stageManager.GetOnlinePlayerId(recoilTarget.playerController) : null;
        }

        public bool IsCarriedForWeaponRecoil()
        {
            if (FindCarrierOfThisPlayer() != null) return true;
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            return stageManager != null
                && !string.IsNullOrEmpty(stageManager.GetOnlineCarrierPlayerId(playerController));
        }

        public void ApplyDirectWeaponRecoil(Vector2 velocityChange)
        {
            PlayerCarryController recoilTarget = FindCarrierOfThisPlayer() ?? this;
            Rigidbody2D targetBody = recoilTarget.playerBody;
            if (targetBody == null || targetBody.bodyType != RigidbodyType2D.Dynamic) return;
            PlayerController2D targetController = recoilTarget.playerController;
            if (targetController != null)
            {
                targetController.ApplyWeaponRecoil(velocityChange);
                return;
            }
            float speedLimit = Mathf.Max(28f, velocityChange.magnitude);
            targetBody.linearVelocity = Vector2.ClampMagnitude(
                targetBody.linearVelocity + velocityChange, speedLimit);
        }

        private PlayerCarryController FindCarrierOfThisPlayer()
        {
            PlayerCarryController[] carriers = FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
                if (carriers[i] != null && carriers[i] != this && carriers[i].IsDraggingFriend(transform))
                    return carriers[i];
            return null;
        }

        private void TryAttachSlimeToFriend()
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(false);
            PlayerController2D bestPlayer = null;
            Rigidbody2D bestBody = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D candidate = players[i];
                if (candidate == null || candidate == playerController || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PlayerCarryController candidateCarry = candidate.GetComponent<PlayerCarryController>();
                if (candidateCarry != null
                    && (candidateCarry.IsHoldingTarget(transform)
                        || candidateCarry.IsDraggingFriend(transform)))
                {
                    continue;
                }

                Rigidbody2D candidateBody = candidate.GetComponent<Rigidbody2D>();
                if (candidateBody == null || !candidateBody.simulated)
                {
                    continue;
                }

                Collider2D[] candidateColliders = candidate.GetComponentsInChildren<Collider2D>(false);
                float distance = GetClosestColliderDistance(ownColliders, candidateColliders);
                float attachReach = IsFriendCarrier() ? Mathf.Max(0.75f, slimeFriendAttachReach) : slimeFriendAttachReach;
                if (distance <= attachReach && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPlayer = candidate;
                    bestBody = candidateBody;
                }
            }

            if (bestPlayer == null || bestBody == null || playerBody == null)
            {
                return;
            }

            slimeAttachedPlayer = bestPlayer;
            slimeAttachedBody = bestBody;
            remoteSlimeVisualTarget = null;
            slimeAttachLocalOffset = IsFriendCarrier()
                ? transform.InverseTransformVector(bestPlayer.transform.position - transform.position)
                : bestPlayer.transform.InverseTransformVector(transform.position - bestPlayer.transform.position);
            if (IsBird())
            {
                slimeAttachLocalOffset = CalculateBirdCarryOffset(bestPlayer);
            }
            if (slimeAttachLocalOffset.sqrMagnitude < 0.12f)
            {
                slimeAttachLocalOffset = IsFriendCarrier()
                    ? new Vector3(GetFacingDirection() * 0.78f, 0.18f, 0f)
                    : Vector3.right * -bestPlayer.FacingDirection * 0.55f;
            }

            Rigidbody2D bodyToSuspend = IsFriendCarrier() ? slimeAttachedBody : playerBody;
            slimePreviousBodyType = bodyToSuspend.bodyType;
            slimePreviousGravityScale = bodyToSuspend.gravityScale;
            slimePreviousFreezeRotation = bodyToSuspend.freezeRotation;
            bodyToSuspend.bodyType = RigidbodyType2D.Kinematic;
            bodyToSuspend.gravityScale = 0f;
            bodyToSuspend.freezeRotation = true;
            bodyToSuspend.linearVelocity = Vector2.zero;
            bodyToSuspend.angularVelocity = 0f;
            slimeAttachedTargetPreviousControlsEnabled = bestPlayer.ControlsEnabled;
            if (IsFriendCarrier())
            {
                bestPlayer.SetFriendCarried(true);
                friendAttachedOnlinePlayerId = GetHeldOnlinePlayerId(bestPlayer);
                SendFriendAttachEvent("friend_grab", friendAttachedOnlinePlayerId, Vector2.zero);
            }

            slimeOwnColliders = GetComponentsInChildren<Collider2D>(false);
            slimeTargetColliders = bestPlayer.GetComponentsInChildren<Collider2D>(false);
            SetCollisionIgnored(slimeOwnColliders, slimeTargetColliders, true);
            FollowSlimeAttachedFriend();
            GameSfx.PlayAt(GetFriendAttachSfx(), transform.position, 1.1f);
        }

        private Vector3 CalculateBirdCarryOffset(PlayerController2D target)
        {
            Collider2D[] own = GetComponentsInChildren<Collider2D>(false);
            Collider2D[] carried = target != null
                ? target.GetComponentsInChildren<Collider2D>(false)
                : null;
            if (!TryGetColliderBounds(own, out Bounds ownBounds)
                || !TryGetColliderBounds(carried, out Bounds carriedBounds))
            {
                return new Vector3(GetFacingDirection() * 0.38f, 1.12f, 0f);
            }

            // Place the carried drawing by its actual collider bounds, rather
            // than by its root. This keeps even unusually tall custom drawings
            // completely above the bird.
            const float verticalGap = 0.12f;
            float desiredCenterX = ownBounds.center.x + GetFacingDirection() * 0.2f;
            Vector3 desiredRoot = target.transform.position + new Vector3(
                desiredCenterX - carriedBounds.center.x,
                ownBounds.max.y + verticalGap - carriedBounds.min.y,
                0f);
            return transform.InverseTransformVector(desiredRoot - transform.position);
        }

        private void TryAttachCatToObject()
        {
            if (!CanAttachToFriend() || playerBody == null)
            {
                return;
            }

            Bounds playerBounds;
            if (!TryGetPlayerBounds(out playerBounds))
            {
                playerBounds = new Bounds(transform.position, Vector3.one * pickupRadius);
            }

            Vector2 searchSize = new Vector2(
                Mathf.Max(pickupRadius, playerBounds.size.x + pickupReach * 2f),
                Mathf.Max(pickupRadius, playerBounds.size.y + pickupReach * 2f));
            pickupHits.Clear();
            Physics2D.OverlapBox(playerBounds.center, searchSize, 0f, pickupContactFilter, pickupHits);
            Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(false);
            CarryableObject bestObject = null;
            Rigidbody2D bestBody = null;
            Collider2D bestCollider = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < pickupHits.Count; i++)
            {
                Collider2D hit = pickupHits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                CarryableObject candidate = hit.GetComponentInParent<CarryableObject>();
                Rigidbody2D candidateBody = candidate != null ? candidate.GetComponent<Rigidbody2D>() : null;
                if (candidate == null || candidateBody == null || !candidateBody.simulated
                    || candidateBody.bodyType == RigidbodyType2D.Static
                    || IsCarryableControlledByAnotherPlayer(candidate.transform))
                {
                    continue;
                }

                float distance = GetClosestColliderDistance(ownColliders, hit);
                if (distance <= Mathf.Max(0.75f, slimeFriendAttachReach) && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestObject = candidate;
                    bestBody = candidateBody;
                    bestCollider = hit;
                }
            }

            if (bestObject == null || bestBody == null || bestCollider == null)
            {
                return;
            }

            catClawedObject = bestObject;
            catClawedBody = bestBody;
            Vector2 contact = bestCollider.ClosestPoint(playerBounds.center);
            catClawedPreviousBodyType = catClawedBody.bodyType;
            catClawedPreviousGravityScale = catClawedBody.gravityScale;
            catClawedPreviousFreezeRotation = catClawedBody.freezeRotation;

            Bounds objectBounds = GetSolidBounds(catClawedObject.gameObject);
            float facing = GetFacingDirection();
            Vector3 desiredCenter = new Vector3(
                playerBounds.center.x + facing * (playerBounds.extents.x + objectBounds.extents.x + 0.16f),
                playerBounds.center.y,
                catClawedObject.transform.position.z);
            Vector3 desiredRoot = catClawedObject.transform.position + desiredCenter - objectBounds.center;
            desiredRoot.z = catClawedObject.transform.position.z;
            catClawedLocalOffset = transform.InverseTransformPoint(desiredRoot);
            catClawedLocalRotation = Quaternion.Inverse(transform.rotation) * catClawedObject.transform.rotation;

            catClawedColliders.Clear();
            catClawedColliderEnabledStates.Clear();
            catClawedColliderTriggerStates.Clear();
            catClawedObject.GetComponentsInChildren(true, catClawedColliders);
            for (int i = 0; i < catClawedColliders.Count; i++)
            {
                Collider2D collider = catClawedColliders[i];
                catClawedColliderEnabledStates.Add(collider != null && collider.enabled);
                catClawedColliderTriggerStates.Add(collider != null && collider.isTrigger);
                if (collider != null) collider.enabled = false;
            }

            catClawedBody.bodyType = RigidbodyType2D.Kinematic;
            catClawedBody.gravityScale = 0f;
            catClawedBody.freezeRotation = true;
            catClawedBody.linearVelocity = Vector2.zero;
            catClawedBody.angularVelocity = 0f;
            UpdateCatClawedObjectPose();
            ResolveGimmickSyncManager()?.BeginLocalObjectCarry(catClawedObject.transform);
            if (IsBird()) UpdateBirdObjectAttachmentVisual();
            else UpdateCatObjectAttachmentVisual();
            GameSfx.PlayAt(GetFriendAttachSfx(), contact, 1.1f);
        }

        private bool IsCarryableControlledByAnotherPlayer(Transform candidate)
        {
            PlayerCarryController[] carriers = FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                PlayerCarryController carrier = carriers[i];
                if (carrier != null && carrier != this && carrier.IsHoldingTarget(candidate))
                {
                    return true;
                }
            }
            return false;
        }

        private void DetachCatFromObject(bool playSound)
        {
            if (catClawedObject == null && catClawedBody == null && catClawedColliders.Count == 0)
            {
                return;
            }

            Transform releasedTransform = catClawedObject != null ? catClawedObject.transform : null;
            Vector2 releaseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            if (releasedTransform != null)
            {
                ResolveGimmickSyncManager()?.EndLocalObjectCarry(releasedTransform, releaseVelocity);
            }
            Collider2D[] releasedColliders = catClawedColliders.ToArray();
            Collider2D[] carrierColliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < catClawedColliders.Count; i++)
            {
                Collider2D collider = catClawedColliders[i];
                if (collider == null) continue;
                collider.isTrigger = i < catClawedColliderTriggerStates.Count && catClawedColliderTriggerStates[i];
                collider.enabled = i < catClawedColliderEnabledStates.Count
                    ? catClawedColliderEnabledStates[i]
                    : true;
            }
            if (catClawedBody != null)
            {
                catClawedBody.bodyType = catClawedPreviousBodyType;
                catClawedBody.gravityScale = catClawedPreviousGravityScale;
                catClawedBody.freezeRotation = catClawedPreviousFreezeRotation;
                catClawedBody.linearVelocity = releaseVelocity;
                catClawedBody.angularVelocity = 0f;
            }
            SetCollisionIgnored(releasedColliders, carrierColliders, true);
            RestoreReleasedCollisionsSafely(releasedColliders, carrierColliders);
            catClawedObject = null;
            catClawedBody = null;
            catClawedColliders.Clear();
            catClawedColliderEnabledStates.Clear();
            catClawedColliderTriggerStates.Clear();
            SetSlimeAttachmentVisualVisible(false);
            if (playSound)
            {
                GameSfx.PlayAt(GetFriendReleaseSfx(), transform.position, 0.9f);
            }
        }

        private void UpdateCatClawedObjectPose()
        {
            if (catClawedObject == null || catClawedBody == null) return;
            Vector3 localOffset = catClawedLocalOffset;
            localOffset.x = Mathf.Abs(localOffset.x) * GetFacingDirection();
            Vector3 target = transform.TransformPoint(localOffset);
            catClawedObject.transform.position = target;
            catClawedObject.transform.rotation = transform.rotation * catClawedLocalRotation;
            catClawedBody.position = target;
            catClawedBody.linearVelocity = Vector2.zero;
            catClawedBody.angularVelocity = 0f;
        }

        private void FollowSlimeAttachedFriend()
        {
            if (slimeAttachedPlayer == null
                || !slimeAttachedPlayer.gameObject.activeInHierarchy
                || slimeAttachedBody == null)
            {
                DetachSlimeFromFriend(false);
                return;
            }

            if (IsFriendCarrier())
            {
                Vector3 targetAnchor = transform.position + transform.TransformVector(slimeAttachLocalOffset);
                targetAnchor = ConstrainCarriedFriendToStageBoundary(targetAnchor);
                slimeAttachedPlayer.transform.position = targetAnchor;
                slimeAttachedBody.position = targetAnchor;
                slimeAttachedBody.linearVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
                slimeAttachedBody.angularVelocity = 0f;
            }
            else
            {
                Vector3 anchor = slimeAttachedPlayer.transform.position
                    + slimeAttachedPlayer.transform.TransformVector(slimeAttachLocalOffset);
                transform.position = anchor;
                transform.rotation = Quaternion.identity;
                if (playerBody != null)
                {
                    playerBody.position = anchor;
                    playerBody.rotation = 0f;
                    playerBody.linearVelocity = slimeAttachedBody.linearVelocity;
                    playerBody.angularVelocity = 0f;
                }
            }

            UpdateFriendAttachmentVisual(slimeAttachedPlayer, true);
        }

        private Vector3 ConstrainCarriedFriendToStageBoundary(Vector3 targetAnchor)
        {
            if (!TryGetSolidBounds(slimeAttachedPlayer, out Bounds carriedBounds))
            {
                return targetAnchor;
            }

            StageEditorObject[] stageObjects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < stageObjects.Length; i++)
            {
                StageEditorObject boundary = stageObjects[i];
                if (boundary == null || boundary.type != StageObjectType.StageBoundary) continue;

                const float margin = 0.10f;
                float left = boundary.transform.position.x - boundary.size.x * 0.5f + margin;
                float right = boundary.transform.position.x + boundary.size.x * 0.5f - margin;
                float top = boundary.transform.position.y + boundary.size.y * 0.5f - margin;
                Vector3 boundsCenterOffset = carriedBounds.center - slimeAttachedPlayer.transform.position;
                Vector3 proposedCenter = targetAnchor + boundsCenterOffset;
                Vector3 correction = Vector3.zero;

                if (proposedCenter.x - carriedBounds.extents.x < left)
                    correction.x = left - (proposedCenter.x - carriedBounds.extents.x);
                else if (proposedCenter.x + carriedBounds.extents.x > right)
                    correction.x = right - (proposedCenter.x + carriedBounds.extents.x);
                if (proposedCenter.y + carriedBounds.extents.y > top)
                    correction.y = top - (proposedCenter.y + carriedBounds.extents.y);

                if (correction.sqrMagnitude > 0.000001f)
                {
                    targetAnchor += correction;
                    transform.position += correction;
                    if (playerBody != null)
                    {
                        playerBody.position += (Vector2)correction;
                        Vector2 velocity = playerBody.linearVelocity;
                        if (correction.y < 0f && velocity.y > 0f) velocity.y = 0f;
                        if (Mathf.Abs(correction.x) > 0.001f) velocity.x = 0f;
                        playerBody.linearVelocity = velocity;
                    }
                }
                break;
            }

            return targetAnchor;
        }

        private void DetachSlimeFromFriend(bool playSound)
        {
            if (slimeAttachedPlayer == null)
            {
                return;
            }

            PlayerController2D releasedTarget = slimeAttachedPlayer;
            bool friendDragging = IsFriendCarrier();
            Vector2 releaseVelocity = friendDragging && playerBody != null
                ? playerBody.linearVelocity
                : slimeAttachedBody != null ? slimeAttachedBody.linearVelocity : Vector2.zero;
            Collider2D[] releasedOwnColliders = slimeOwnColliders;
            Collider2D[] releasedTargetColliders = slimeTargetColliders;
            bool separatedFromTarget = !friendDragging && SeparateFromAttachmentTarget(
                releasedTarget, releasedOwnColliders, releasedTargetColliders);
            if (friendDragging)
            {
                SendFriendAttachEvent("friend_release", friendAttachedOnlinePlayerId, releaseVelocity);
            }
            slimeAttachedPlayer = null;
            slimeAttachedBody = null;
            friendAttachedOnlinePlayerId = null;
            slimeOwnColliders = new Collider2D[0];
            slimeTargetColliders = new Collider2D[0];
            SetSlimeAttachmentVisualVisible(false);

            Rigidbody2D bodyToRestore = friendDragging ? releasedTarget.GetComponent<Rigidbody2D>() : playerBody;
            if (bodyToRestore != null)
            {
                bodyToRestore.bodyType = slimePreviousBodyType;
                bodyToRestore.gravityScale = slimePreviousGravityScale;
                bodyToRestore.freezeRotation = slimePreviousFreezeRotation;
                bodyToRestore.rotation = 0f;
                bodyToRestore.linearVelocity = releaseVelocity;
                bodyToRestore.angularVelocity = 0f;
            }
            if (friendDragging)
            {
                releasedTarget.SetFriendCarried(false);
                releasedTarget.SetControlsEnabled(slimeAttachedTargetPreviousControlsEnabled);
            }

            if (releasedOwnColliders.Length > 0 && releasedTargetColliders.Length > 0)
            {
                if (separatedFromTarget)
                {
                    SetCollisionIgnored(releasedOwnColliders, releasedTargetColliders, false);
                }
                else
                {
                    RestoreReleasedCollisionsSafely(releasedOwnColliders, releasedTargetColliders);
                }
            }
            if (playSound)
            {
                GameSfx.PlayAt(GetFriendReleaseSfx(), transform.position, 0.9f);
            }
        }

        private bool SeparateFromAttachmentTarget(
            PlayerController2D target,
            Collider2D[] ownColliders,
            Collider2D[] targetColliders)
        {
            if (target == null || playerBody == null
                || !TryGetColliderBounds(ownColliders, out Bounds ownBounds)
                || !TryGetColliderBounds(targetColliders, out Bounds targetBounds))
            {
                return false;
            }
            if (!ownBounds.Intersects(targetBounds))
            {
                return true;
            }

            Vector2 relative = ownBounds.center - targetBounds.center;
            if (relative.sqrMagnitude < 0.0001f)
            {
                relative = target.transform.TransformVector(slimeAttachLocalOffset);
            }
            if (relative.sqrMagnitude < 0.0001f)
            {
                relative = Vector2.up;
            }

            float horizontalScore = Mathf.Abs(relative.x)
                / Mathf.Max(0.01f, ownBounds.extents.x + targetBounds.extents.x);
            float verticalScore = Mathf.Abs(relative.y)
                / Mathf.Max(0.01f, ownBounds.extents.y + targetBounds.extents.y);
            const float separationGap = 0.065f;
            Vector3 correction = Vector3.zero;
            if (verticalScore >= horizontalScore)
            {
                correction.y = relative.y >= 0f
                    ? targetBounds.max.y + separationGap - ownBounds.min.y
                    : targetBounds.min.y - separationGap - ownBounds.max.y;
            }
            else
            {
                correction.x = relative.x >= 0f
                    ? targetBounds.max.x + separationGap - ownBounds.min.x
                    : targetBounds.min.x - separationGap - ownBounds.max.x;
            }

            transform.position += correction;
            playerBody.position = transform.position;
            Physics2D.SyncTransforms();
            return true;
        }

        private static bool TryGetColliderBounds(Collider2D[] colliders, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;
            if (colliders == null)
            {
                return false;
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            return found;
        }

        public void ApplyRemoteSlimeAttachment(PlayerController2D target)
        {
            if (remoteSlimeVisualTarget == target)
            {
                return;
            }

            bool wasAttached = remoteSlimeVisualTarget != null;
            remoteSlimeVisualTarget = target;
            if (target != null)
            {
                GameSfx.PlayAt(GetFriendAttachSfx(), transform.position, 0.9f);
            }
            else if (wasAttached)
            {
                GameSfx.PlayAt(GetFriendReleaseSfx(), transform.position, 0.8f);
                SetSlimeAttachmentVisualVisible(false);
            }
        }

        private void TryPickup()
        {
            Bounds playerBounds;
            if (!TryGetPlayerBounds(out playerBounds))
            {
                playerBounds = new Bounds(transform.position, new Vector3(pickupRadius, pickupRadius, 0f));
            }

            Vector2 searchCenter = playerBounds.center;
            Vector2 searchSize = new Vector2(
                Mathf.Max(pickupRadius, playerBounds.size.x + pickupReach * 2f),
                Mathf.Max(pickupRadius, playerBounds.size.y + pickupReach * 2f));
            pickupHits.Clear();
            Physics2D.OverlapBox(searchCenter, searchSize, 0f, pickupContactFilter, pickupHits);
            Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>(false);
            Transform bestTransform = null;
            CarryableObject bestCarryable = null;
            PlayerController2D bestPlayer = null;
            Rigidbody2D bestBody = null;
            float bestDistance = float.PositiveInfinity;
            float bestCenterDistance = float.PositiveInfinity;

            for (int i = 0; i < pickupHits.Count; i++)
            {
                Collider2D hit = pickupHits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                CarryableObject candidate = hit.GetComponentInParent<CarryableObject>();
                PlayerController2D candidatePlayer = hit.GetComponentInParent<PlayerController2D>();
                Transform candidateTransform = candidate != null ? candidate.transform : candidatePlayer != null ? candidatePlayer.transform : null;
                if (candidateTransform == null)
                {
                    continue;
                }

                PlayerCarryController candidateCarry = candidatePlayer != null
                    ? candidatePlayer.GetComponent<PlayerCarryController>()
                    : null;
                if (candidateCarry != null
                    && (candidateCarry.IsHoldingTarget(transform)
                        || candidateCarry.IsDraggingFriend(transform)))
                {
                    // Never create A -> B -> A. Both LateUpdate followers would
                    // otherwise keep adding their carry offsets forever.
                    continue;
                }

                Rigidbody2D candidateBody = candidateTransform.GetComponent<Rigidbody2D>();
                if (candidateBody == null || candidateBody.bodyType == RigidbodyType2D.Static)
                {
                    continue;
                }

                if (candidatePlayer != null
                    && HasSolidRoomBarrier(playerBounds.center, hit.bounds.center, transform, candidatePlayer.transform))
                {
                    continue;
                }

                float distance = GetClosestColliderDistance(playerColliders, hit);
                if (distance > pickupReach)
                {
                    continue;
                }

                float centerDistance = Vector2.SqrMagnitude((Vector2)candidateTransform.position - searchCenter);
                if (distance < bestDistance - 0.001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.001f && centerDistance < bestCenterDistance))
                {
                    bestDistance = distance;
                    bestCenterDistance = centerDistance;
                    bestTransform = candidateTransform;
                    bestCarryable = candidate;
                    bestPlayer = candidatePlayer;
                    bestBody = candidateBody;
                }
            }

            if (bestTransform == null || bestBody == null)
            {
                return;
            }

            heldTransform = bestTransform;
            heldObject = bestCarryable;
            heldPlayerController = bestPlayer;
            heldBody = bestBody;
            previousBodyType = heldBody.bodyType;
            previousGravityScale = heldBody.gravityScale;
            previousFreezeRotation = heldBody.freezeRotation;
            heldBody.bodyType = RigidbodyType2D.Kinematic;
            heldBody.gravityScale = 0f;
            heldBody.freezeRotation = true;
            heldBody.linearVelocity = Vector2.zero;
            heldBody.angularVelocity = 0f;

            heldColliders.Clear();
            heldColliderSet.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            heldTransform.GetComponentsInChildren(heldColliders);
            bool holdingKey = IsHeldGameplayKey();
            for (int i = 0; i < heldColliders.Count; i++)
            {
                Collider2D heldCollider = heldColliders[i];
                if (heldCollider != null)
                {
                    heldColliderSet.Add(heldCollider);
                }
                heldColliderEnabledStates.Add(heldCollider != null && heldCollider.enabled);
                heldColliderTriggerStates.Add(heldCollider != null && heldCollider.isTrigger);
                if (heldCollider == null)
                {
                    continue;
                }

                bool holdingPlayer = bestPlayer != null;
                if (holdingKey)
                {
                    // A key must keep valid world bounds while carried so the
                    // keyhole can test the real overlap. Trigger mode prevents it
                    // colliding with the carrier or terrain.
                    heldCollider.isTrigger = true;
                    heldCollider.enabled = true;
                }
                else if (holdingPlayer)
                {
                    // A carried player remains a real support surface so teammates
                    // already standing on them travel with the throw as in gameplay.
                    heldCollider.isTrigger = i < heldColliderTriggerStates.Count
                        && heldColliderTriggerStates[i];
                    heldCollider.enabled = true;
                }
                else
                {
                    heldCollider.enabled = false;
                }
            }

            if (bestPlayer != null)
            {
                SetCollisionIgnored(heldColliders.ToArray(), playerColliders, true);
            }

            BringHeldObjectToFront();
            heldPlayerPreviousControlsEnabled = heldPlayerController != null
                && heldPlayerController.ControlsEnabled;
            heldPlayerController?.SetControlsEnabled(false);
            heldPlayerController?.SetFriendCarried(true);
            heldTransform.GetComponent<StageBomb>()?.NotifyPickedUp();
            heldTransform.GetComponent<StageGun>()?.SetHolder(this);
            heldTransform.GetComponent<StageBazooka>()?.SetHolder(this);
            heldOnlinePlayerId = GetHeldOnlinePlayerId(heldPlayerController);
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("pickup", Vector2.zero);
            }
            else if (heldObject != null)
            {
                ResolveGimmickSyncManager()?.BeginLocalObjectCarry(heldTransform);
            }
            GameSfx.PlayAt(SfxId.HumanLift, transform.position);
        }

        private static float GetClosestColliderDistance(Collider2D[] playerColliders, Collider2D targetCollider)
        {
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger || playerCollider == targetCollider)
                {
                    continue;
                }

                ColliderDistance2D distance = playerCollider.Distance(targetCollider);
                bestDistance = Mathf.Min(bestDistance, Mathf.Max(0f, distance.distance));
            }

            return bestDistance;
        }

        private static bool HasSolidRoomBarrier(Vector2 from, Vector2 to, Transform carrier, Transform candidate)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return false;
            RaycastHit2D[] hits = Physics2D.RaycastAll(from, delta / distance, distance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D blocker = hits[i].collider;
                if (blocker == null || blocker.isTrigger
                    || blocker.transform.IsChildOf(carrier)
                    || blocker.transform.IsChildOf(candidate)) continue;
                if (blocker.gameObject.layer == 6 || blocker.CompareTag("Ground")) return true;
            }
            return false;
        }

        private static float GetClosestColliderDistance(Collider2D[] first, Collider2D[] second)
        {
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < first.Length; i++)
            {
                Collider2D a = first[i];
                if (a == null || !a.enabled || a.isTrigger)
                {
                    continue;
                }

                for (int j = 0; j < second.Length; j++)
                {
                    Collider2D b = second[j];
                    if (b == null || !b.enabled || b.isTrigger)
                    {
                        continue;
                    }

                    ColliderDistance2D distance = a.Distance(b);
                    bestDistance = Mathf.Min(bestDistance, Mathf.Max(0f, distance.distance));
                }
            }

            return bestDistance;
        }

        private void ThrowHeld()
        {
            // Update can run after a walking FixedUpdate but before LateUpdate has
            // moved the held object to the new hand position. Release from the
            // current anchor so neither the local nor host copy starts inside the
            // carrier's previous frame.
            if (heldTransform != null)
            {
                Vector3 releaseAnchor = GetHoldPosition();
                heldTransform.position = releaseAnchor;
                heldTransform.rotation = Quaternion.identity;
                if (heldBody != null)
                {
                    heldBody.position = releaseAnchor;
                    heldBody.rotation = 0f;
                }
                Physics2D.SyncTransforms();
            }

            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            float currentThrowSpeed = GetCurrentThrowSpeed();
            if (IsHeldGameplayKey())
            {
                // Arm ink can raise the generic throw speed to roughly 77 units/s.
                // Keep puzzle keys controllable and on screen.
                currentThrowSpeed = Mathf.Clamp(currentThrowSpeed * 0.45f, 8f, 14f);
            }
            int currentFacingDirection = GetFacingDirection();
            Vector2 throwDirection = hasDisplayedThrowDirection
                && displayedThrowFacingDirection == currentFacingDirection
                ? displayedThrowDirection
                : GetThrowDirection();
            Vector2 throwVelocity = throwDirection.normalized * currentThrowSpeed * multiplier;
            GameSfx.PlayAt(SfxId.HumanThrow, transform.position);
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("throw", throwVelocity);
            }

            DropHeld(throwVelocity);
        }

        private void DropHeld(Vector2 releaseVelocity)
        {
            if (heldTransform == null)
            {
                bool hasStaleHeldState = heldBody != null
                    || heldPlayerController != null
                    || heldObject != null
                    || !string.IsNullOrEmpty(heldOnlinePlayerId)
                    || heldColliders.Count > 0
                    || heldRenderers.Count > 0;
                if (!hasStaleHeldState)
                {
                    return;
                }

                // The carried avatar/object can be rebuilt by an online body sync
                // or destroyed by a stage gimmick before the release packet is
                // processed. Unity then makes the Transform compare as null while
                // the cached online id and physics state still remain. Clear every
                // cache here; otherwise our continuous player state keeps claiming
                // that we carry the other player and their controls stay disabled.
                ClearDestroyedHeldState(releaseVelocity);
                return;
            }

            StageGun releasedGun = heldTransform.GetComponent<StageGun>();
            releasedGun?.SetHolder(null);
            StageBazooka releasedBazooka = heldTransform.GetComponent<StageBazooka>();
            releasedBazooka?.SetHolder(null);
            bodyBuilder?.SetCarryPose(false, GetFacingDirection(), transform.position);
            if (heldObject != null)
            {
                ResolveGimmickSyncManager()?.EndLocalObjectCarry(heldTransform, releaseVelocity);
            }
            Collider2D[] releasedColliders = heldColliders.ToArray();
            // OnDisable can run after the player GameObject has already become
            // inactive. Include inactive body colliders so ignored collision
            // pairs are still fully restored for the next retry/respawn.
            Collider2D[] carrierColliders = GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < releasedColliders.Length; i++)
            {
                if (releasedColliders[i] != null)
                {
                    if (i < heldColliderTriggerStates.Count)
                    {
                        releasedColliders[i].isTrigger = heldColliderTriggerStates[i];
                    }
                    releasedColliders[i].enabled = i < heldColliderEnabledStates.Count
                        ? heldColliderEnabledStates[i]
                        : true;
                }
            }
            RestoreHeldObjectRendering();
            SetCollisionIgnored(releasedColliders, carrierColliders, true);

            heldPlayerController?.SetFriendCarried(false);
            heldPlayerController?.ResetMotion();
            heldPlayerController?.SetControlsEnabled(heldPlayerPreviousControlsEnabled);
            if (heldBody != null)
            {
                heldBody.bodyType = previousBodyType;
                heldBody.gravityScale = previousGravityScale;
                heldBody.freezeRotation = previousFreezeRotation;
                heldBody.linearVelocity = releaseVelocity;
                // A hand-drawn player can have long, irregular limb colliders.
                // Spinning that entire collider set at object-throw angular speed
                // can wedge it into a wall and create a huge reverse impulse.
                heldBody.angularVelocity = heldPlayerController != null
                    ? 0f
                    : releaseVelocity.x * -18f;
            }

            heldTransform = null;
            heldObject = null;
            heldPlayerController = null;
            heldPlayerPreviousControlsEnabled = false;
            heldBody = null;
            heldOnlinePlayerId = null;
            hasDisplayedThrowDirection = false;
            heldColliders.Clear();
            heldColliderSet.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            SetThrowPreviewVisible(false);
            RestoreReleasedCollisionsSafely(releasedColliders, carrierColliders);
        }

        private void ClearDestroyedHeldState(Vector2 releaseVelocity)
        {
            bodyBuilder?.SetCarryPose(false, GetFacingDirection(), transform.position);
            RestoreHeldObjectRendering();

            heldPlayerController?.SetFriendCarried(false);
            heldPlayerController?.ResetMotion();
            heldPlayerController?.SetControlsEnabled(heldPlayerPreviousControlsEnabled);
            if (heldBody != null)
            {
                heldBody.bodyType = previousBodyType;
                heldBody.gravityScale = previousGravityScale;
                heldBody.freezeRotation = previousFreezeRotation;
                heldBody.linearVelocity = releaseVelocity;
            }

            heldTransform = null;
            heldObject = null;
            heldPlayerController = null;
            heldPlayerPreviousControlsEnabled = false;
            heldBody = null;
            heldOnlinePlayerId = null;
            hasDisplayedThrowDirection = false;
            heldColliders.Clear();
            heldColliderSet.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            SetThrowPreviewVisible(false);
        }

        private bool IsHeldGameplayKey()
        {
            StageEditorObject stageObject = heldTransform != null
                ? heldTransform.GetComponent<StageEditorObject>()
                : null;
            return stageObject != null && stageObject.type == StageObjectType.Key;
        }

        private void BringHeldObjectToFront()
        {
            heldRenderers.Clear();
            heldRendererSortingOrders.Clear();
            heldRendererEnabledStates.Clear();
            if (heldObject == null || heldTransform == null)
            {
                return;
            }

            heldTransform.GetComponentsInChildren(true, heldRenderers);
            for (int i = 0; i < heldRenderers.Count; i++)
            {
                Renderer renderer = heldRenderers[i];
                heldRendererSortingOrders.Add(renderer != null ? renderer.sortingOrder : 0);
                heldRendererEnabledStates.Add(renderer != null && renderer.enabled);
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.sortingOrder += 100;
                }
            }
        }

        private void RestoreHeldObjectRendering()
        {
            for (int i = 0; i < heldRenderers.Count; i++)
            {
                Renderer renderer = heldRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (i < heldRendererSortingOrders.Count)
                {
                    renderer.sortingOrder = heldRendererSortingOrders[i];
                }
                if (i < heldRendererEnabledStates.Count)
                {
                    renderer.enabled = heldRendererEnabledStates[i];
                }
            }

            heldRenderers.Clear();
            heldRendererSortingOrders.Clear();
            heldRendererEnabledStates.Clear();
        }

        public void ForceDrop()
        {
            DetachSlimeFromFriend(false);
            DetachCatFromObject(false);
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("drop", Vector2.zero);
            }

            DropHeld(Vector2.zero);
        }

        private Vector3 GetHoldPosition()
        {
            int direction = GetFacingDirection();
            if (bodyBuilder != null)
            {
                return bodyBuilder.GetCarryAnchorWorld(direction);
            }

            return transform.position + new Vector3(direction * 0.55f, 1.15f, 0f);
        }

        private Vector3 GetPickupSearchCenter()
        {
            int direction = GetFacingDirection();
            if (TryGetPlayerBounds(out Bounds bounds))
            {
                return new Vector3(
                    bounds.center.x + direction * (bounds.extents.x + 0.45f),
                    bounds.center.y,
                    transform.position.z);
            }

            return transform.position + new Vector3(direction * 0.75f, 0.35f, 0f);
        }

        private bool TryGetPlayerBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private int GetFacingDirection()
        {
            return playerController != null ? playerController.FacingDirection : 1;
        }

        private bool IsHuman()
        {
            return abilityController == null || abilityController.CurrentProfile.Species == DrawManager.Species.Human;
        }

        private bool IsSlime()
        {
            return abilityController != null
                && abilityController.CurrentProfile.Species == DrawManager.Species.Slime;
        }

        private bool IsCat()
        {
            return abilityController != null
                && abilityController.CurrentProfile.Species == DrawManager.Species.Cat;
        }

        private bool IsBird()
        {
            return abilityController != null
                && abilityController.CurrentProfile.Species == DrawManager.Species.Bird;
        }

        private bool IsFriendCarrier()
        {
            return IsCat() || IsBird();
        }

        private SfxId GetFriendAttachSfx()
        {
            if (IsCat()) return SfxId.CatClawAttach;
            if (IsBird()) return SfxId.BirdFlap;
            return SfxId.SlimeStick;
        }

        private SfxId GetFriendReleaseSfx()
        {
            if (IsCat()) return SfxId.CatClawRelease;
            if (IsBird()) return SfxId.BirdFlap;
            return SfxId.SlimeRelease;
        }

        private bool CanAttachToFriend()
        {
            return IsSlime() || IsFriendCarrier();
        }

        private Vector2 GetThrowDirection()
        {
            float phase = Mathf.PingPong(Time.time * throwAimSpeed, 1f);
            float angle = Mathf.Lerp(88f, -88f, phase) * Mathf.Deg2Rad;
            int direction = GetFacingDirection();
            return new Vector2(direction * Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        private void CreateThrowPreview()
        {
            GameObject root = new GameObject("ThrowDirectionPreview");
            root.transform.SetParent(transform, false);

            throwPreviewLine = CreatePreviewLine("ThrowDirectionLine", root.transform, 0.055f);
            throwPreviewHeadA = CreatePreviewLine("ThrowDirectionHeadA", root.transform, 0.045f);
            throwPreviewHeadB = CreatePreviewLine("ThrowDirectionHeadB", root.transform, 0.045f);
            SetThrowPreviewVisible(false);
        }

        private void CreateSlimeAttachmentVisual()
        {
            GameObject root = new GameObject("SlimeFriendAttachmentVisual");
            root.transform.SetParent(transform, false);

            GameObject bridgeObject = new GameObject("SlimeFriendGooBridge");
            bridgeObject.transform.SetParent(root.transform, false);
            slimeAttachBridge = bridgeObject.AddComponent<LineRenderer>();
            slimeAttachBridge.useWorldSpace = true;
            slimeAttachBridge.positionCount = 3;
            slimeAttachBridge.numCapVertices = 8;
            slimeAttachBridge.numCornerVertices = 8;
            slimeAttachBridge.sortingOrder = 220;
            slimeAttachBridge.sharedMaterial = GetPreviewMaterial();

            GameObject ringObject = new GameObject("SlimeFriendContactRing");
            ringObject.transform.SetParent(root.transform, false);
            slimeAttachRing = ringObject.AddComponent<LineRenderer>();
            slimeAttachRing.useWorldSpace = true;
            slimeAttachRing.loop = true;
            slimeAttachRing.positionCount = 24;
            slimeAttachRing.numCapVertices = 5;
            slimeAttachRing.numCornerVertices = 5;
            slimeAttachRing.sortingOrder = 221;
            slimeAttachRing.sharedMaterial = GetPreviewMaterial();

            for (int i = 0; i < catClawLines.Length; i++)
            {
                GameObject clawObject = new GameObject("CatFriendClaw" + (i + 1));
                clawObject.transform.SetParent(root.transform, false);
                LineRenderer claw = clawObject.AddComponent<LineRenderer>();
                claw.useWorldSpace = true;
                claw.positionCount = 4;
                claw.numCapVertices = 6;
                claw.numCornerVertices = 5;
                claw.sortingOrder = 222 + i;
                claw.sharedMaterial = GetPreviewMaterial();
                catClawLines[i] = claw;
            }
            for (int i = 0; i < birdBeakLines.Length; i++)
            {
                GameObject beakObject = new GameObject("BirdFriendBeak" + (i + 1));
                beakObject.transform.SetParent(root.transform, false);
                LineRenderer beak = beakObject.AddComponent<LineRenderer>();
                beak.useWorldSpace = true;
                beak.positionCount = 3;
                beak.numCapVertices = 6;
                beak.numCornerVertices = 5;
                beak.sortingOrder = 226 + i;
                beak.sharedMaterial = GetPreviewMaterial();
                birdBeakLines[i] = beak;
            }
            SetSlimeAttachmentVisualVisible(false);
        }

        private void UpdateFriendAttachmentVisual(PlayerController2D target, bool useAttachedAnchor)
        {
            if (IsBird())
            {
                UpdateBirdAttachmentVisual(target, useAttachedAnchor);
            }
            else if (IsCat())
            {
                UpdateCatAttachmentVisual(target, useAttachedAnchor);
            }
            else
            {
                UpdateSlimeAttachmentVisual(target, useAttachedAnchor);
            }
        }

        private void UpdateBirdAttachmentVisual(PlayerController2D target, bool useAttachedAnchor)
        {
            if (target == null)
            {
                SetSlimeAttachmentVisualVisible(false);
                return;
            }
            if (slimeAttachBridge != null) slimeAttachBridge.enabled = false;
            if (slimeAttachRing != null) slimeAttachRing.enabled = false;
            for (int i = 0; i < catClawLines.Length; i++)
            {
                if (catClawLines[i] != null) catClawLines[i].enabled = false;
            }

            Bounds birdBounds = new Bounds(transform.position, new Vector3(0.9f, 0.7f, 0f));
            TryGetSolidBounds(playerController, out birdBounds);
            Bounds targetBounds = new Bounds(target.transform.position, Vector3.one * 0.5f);
            TryGetSolidBounds(target, out targetBounds);
            float facing = GetFacingDirection();
            Vector3 beakBase = new Vector3(
                birdBounds.center.x + facing * birdBounds.extents.x * 0.72f,
                birdBounds.center.y + birdBounds.extents.y * 0.28f,
                transform.position.z);
            Vector3 contact = useAttachedAnchor
                ? targetBounds.center + Vector3.up * targetBounds.extents.y
                : targetBounds.ClosestPoint(beakBase);
            DrawBirdBeak(beakBase, contact, facing);
        }

        private void UpdateBirdObjectAttachmentVisual()
        {
            if (catClawedObject == null)
            {
                SetSlimeAttachmentVisualVisible(false);
                return;
            }
            if (slimeAttachBridge != null) slimeAttachBridge.enabled = false;
            if (slimeAttachRing != null) slimeAttachRing.enabled = false;
            for (int i = 0; i < catClawLines.Length; i++)
            {
                if (catClawLines[i] != null) catClawLines[i].enabled = false;
            }

            Bounds birdBounds = new Bounds(transform.position, new Vector3(0.9f, 0.7f, 0f));
            TryGetSolidBounds(playerController, out birdBounds);
            float facing = GetFacingDirection();
            Vector3 beakBase = new Vector3(
                birdBounds.center.x + facing * birdBounds.extents.x * 0.72f,
                birdBounds.center.y + birdBounds.extents.y * 0.28f,
                transform.position.z);
            Bounds objectBounds = GetSolidBounds(catClawedObject.gameObject);
            DrawBirdBeak(beakBase, objectBounds.ClosestPoint(beakBase), facing);
        }

        private void DrawBirdBeak(Vector3 beakBase, Vector3 contact, float facing)
        {
            Vector3 forward = (contact - beakBase).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.right * facing;
            Vector3 normal = new Vector3(-forward.y, forward.x, 0f);
            Color beakColor = new Color(1f, 0.65f, 0.12f, 0.96f);
            float pinch = 0.035f + Mathf.Sin(Time.unscaledTime * 8f) * 0.012f;
            for (int i = 0; i < birdBeakLines.Length; i++)
            {
                LineRenderer beak = birdBeakLines[i];
                if (beak == null) continue;
                float side = i == 0 ? 1f : -1f;
                Vector3 jaw = beakBase + normal * side * 0.11f;
                Vector3 tip = contact + normal * side * pinch;
                beak.enabled = true;
                beak.startWidth = 0.075f;
                beak.endWidth = 0.035f;
                beak.startColor = beakColor;
                beak.endColor = beakColor;
                beak.SetPosition(0, beakBase);
                beak.SetPosition(1, jaw);
                beak.SetPosition(2, tip);
            }
        }

        private void UpdateCatAttachmentVisual(PlayerController2D target, bool useAttachedAnchor)
        {
            if (target == null)
            {
                SetSlimeAttachmentVisualVisible(false);
                return;
            }
            if (slimeAttachBridge != null) slimeAttachBridge.enabled = false;
            if (slimeAttachRing != null) slimeAttachRing.enabled = false;
            for (int i = 0; i < birdBeakLines.Length; i++)
            {
                if (birdBeakLines[i] != null) birdBeakLines[i].enabled = false;
            }

            Vector3 catCenter = transform.position;
            if (TryGetSolidBounds(playerController, out Bounds catBounds)) catCenter = catBounds.center;
            Vector3 targetCenter = target.transform.position;
            Bounds targetBounds = new Bounds(targetCenter, Vector3.one * 0.5f);
            if (TryGetSolidBounds(target, out Bounds measuredBounds))
            {
                targetBounds = measuredBounds;
                targetCenter = measuredBounds.center;
            }
            Vector3 contact = useAttachedAnchor
                ? target.transform.position + target.transform.TransformVector(slimeAttachLocalOffset) * 0.42f
                : targetBounds.ClosestPoint(catCenter);
            DrawCatClaws(catCenter, contact);
        }

        private void UpdateCatObjectAttachmentVisual()
        {
            if (catClawedObject == null)
            {
                SetSlimeAttachmentVisualVisible(false);
                return;
            }
            if (slimeAttachBridge != null) slimeAttachBridge.enabled = false;
            if (slimeAttachRing != null) slimeAttachRing.enabled = false;
            for (int i = 0; i < birdBeakLines.Length; i++)
            {
                if (birdBeakLines[i] != null) birdBeakLines[i].enabled = false;
            }

            Vector3 catCenter = transform.position;
            if (TryGetSolidBounds(playerController, out Bounds catBounds)) catCenter = catBounds.center;
            Bounds objectBounds = GetSolidBounds(catClawedObject.gameObject);
            Vector3 contact = objectBounds.ClosestPoint(catCenter);
            DrawCatClaws(catCenter, contact);
        }

        private void DrawCatClaws(Vector3 catCenter, Vector3 contact)
        {
            Vector3 direction = contact - catCenter;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.right;
            direction.Normalize();
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
            Color clawColor = bodyBuilder != null
                ? Color.Lerp(bodyBuilder.PlayerColor, new Color(0.22f, 0.1f, 0.04f, 1f), 0.48f)
                : new Color(0.7f, 0.25f, 0.08f, 1f);
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 10f) * 0.5f;
            for (int i = 0; i < catClawLines.Length; i++)
            {
                LineRenderer claw = catClawLines[i];
                if (claw == null) continue;
                float lane = i - 1f;
                Vector3 start = catCenter + perpendicular * lane * 0.11f;
                Vector3 knuckle = Vector3.Lerp(start, contact, 0.58f) + perpendicular * lane * 0.06f;
                Vector3 tip = contact + perpendicular * lane * 0.14f - direction * 0.03f;
                Vector3 hook = tip - direction * Mathf.Lerp(0.1f, 0.16f, pulse) - perpendicular * lane * 0.025f;
                claw.enabled = true;
                claw.startWidth = 0.065f;
                claw.endWidth = 0.045f;
                claw.startColor = clawColor;
                claw.endColor = clawColor;
                claw.SetPosition(0, start);
                claw.SetPosition(1, knuckle);
                claw.SetPosition(2, tip);
                claw.SetPosition(3, hook);
            }
        }

        private bool TryScratchEnemy()
        {
            StageBlockBreakerEnemy[] enemies = Object.FindObjectsByType<StageBlockBreakerEnemy>(FindObjectsSortMode.None);
            StageEnemyCharacter[] placedEnemies = Object.FindObjectsByType<StageEnemyCharacter>(FindObjectsSortMode.None);
            StageValueCrate[] valueCrates = Object.FindObjectsByType<StageValueCrate>(FindObjectsSortMode.None);
            StageMirrorFinalBossController mirrorBattle = Object.FindFirstObjectByType<StageMirrorFinalBossController>();
            if (mirrorBattle != null)
            {
                float mirrorFrontLegInk = abilityController != null
                    ? abilityController.CurrentProfile.CatFrontLegInk
                    : 0f;
                float mirrorRange = PlayerController2D.CalculateCatScratchRangeMultiplier(mirrorFrontLegInk);
                if (mirrorBattle.TryPlayerCatScratch(playerController, mirrorRange)) return true;
            }
            if (enemies.Length == 0 && placedEnemies.Length == 0 && valueCrates.Length == 0) return false;

            Bounds catBounds = new Bounds(transform.position, Vector3.one);
            if (!TryGetSolidBounds(playerController, out catBounds))
            {
                catBounds = new Bounds(transform.position, Vector3.one);
            }
            Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(false);
            float facing = GetFacingDirection();
            StageBlockBreakerEnemy closestEnemy = null;
            StageEnemyCharacter closestPlacedEnemy = null;
            StageValueCrate closestValueCrate = null;
            float closestDistance = float.PositiveInfinity;
            float frontLegInk = abilityController != null
                ? abilityController.CurrentProfile.CatFrontLegInk
                : 0f;
            float rangeMultiplier = PlayerController2D.CalculateCatScratchRangeMultiplier(frontLegInk);
            float scratchReach = Mathf.Max(1.35f, catBounds.extents.x * 0.55f + 0.9f) * rangeMultiplier;
            for (int i = 0; i < enemies.Length; i++)
            {
                StageBlockBreakerEnemy enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                if (enemyCollider == null) continue;
                Vector2 towardEnemy = (Vector2)enemyCollider.bounds.center - (Vector2)catBounds.center;
                if (towardEnemy.x * facing < -0.2f) continue;
                float distance = GetClosestColliderDistance(ownColliders, enemyCollider);
                if (distance <= scratchReach && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                    closestPlacedEnemy = null;
                    closestValueCrate = null;
                }
            }
            for (int i = 0; i < placedEnemies.Length; i++)
            {
                StageEnemyCharacter enemy = placedEnemies[i];
                if (enemy == null || enemy.IsDefeated || !enemy.gameObject.activeInHierarchy) continue;
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                if (enemyCollider == null) continue;
                Vector2 towardEnemy = (Vector2)enemyCollider.bounds.center - (Vector2)catBounds.center;
                if (towardEnemy.x * facing < -0.2f) continue;
                float distance = GetClosestColliderDistance(ownColliders, enemyCollider);
                if (distance <= scratchReach && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = null;
                    closestPlacedEnemy = enemy;
                    closestValueCrate = null;
                }
            }
            for (int i = 0; i < valueCrates.Length; i++)
            {
                StageValueCrate crate = valueCrates[i];
                if (crate == null || crate.IsBroken || !crate.gameObject.activeInHierarchy) continue;
                Collider2D crateCollider = crate.GetComponentInChildren<Collider2D>();
                if (crateCollider == null) continue;
                Vector2 towardCrate = (Vector2)crateCollider.bounds.center - (Vector2)catBounds.center;
                if (towardCrate.x * facing < -0.2f) continue;
                float distance = GetClosestColliderDistance(ownColliders, crateCollider);
                if (distance <= scratchReach && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = null;
                    closestPlacedEnemy = null;
                    closestValueCrate = crate;
                }
            }
            if (closestEnemy == null && closestPlacedEnemy == null && closestValueCrate == null) return false;
            if (closestEnemy != null) closestEnemy.HitByCatScratch();
            else if (closestPlacedEnemy != null) closestPlacedEnemy.HitByCatScratch();
            else closestValueCrate.Hit(closestValueCrate.transform.position);
            return true;
        }

        private void PlayCatScratchEffect()
        {
            PlayCatScratchEffect(true);
        }

        internal void PlayRemoteCatScratchEffect()
        {
            PlayCatScratchEffect(false);
        }

        private void PlayCatScratchEffect(bool broadcast)
        {
            Bounds catBounds = new Bounds(transform.position, Vector3.one);
            if (!TryGetSolidBounds(playerController, out catBounds))
            {
                catBounds = new Bounds(transform.position, Vector3.one);
            }
            float facing = GetFacingDirection();
            float frontLegInk = abilityController != null
                ? abilityController.CurrentProfile.CatFrontLegInk
                : 0f;
            float rangeMultiplier = PlayerController2D.CalculateCatScratchRangeMultiplier(frontLegInk);
            Vector3 origin = catBounds.center + Vector3.right * facing * Mathf.Max(0.2f, catBounds.extents.x * 0.45f);
            GameObject root = new GameObject("Cat Scratch Burst");
            root.transform.SetParent(transform, true);
            LineRenderer[] slashes = new LineRenderer[3];
            Color scratchColor = new Color(1f, 0.42f, 0.08f, 1f);
            for (int i = 0; i < slashes.Length; i++)
            {
                GameObject slashObject = new GameObject("Claw Slash " + (i + 1));
                slashObject.transform.SetParent(root.transform, false);
                LineRenderer slash = slashObject.AddComponent<LineRenderer>();
                slash.useWorldSpace = true;
                slash.positionCount = 4;
                slash.numCapVertices = 6;
                slash.numCornerVertices = 5;
                slash.sortingOrder = 245 + i;
                slash.sharedMaterial = GetPreviewMaterial();
                slash.startWidth = 0.11f;
                slash.endWidth = 0.035f;
                slash.startColor = scratchColor;
                slash.endColor = new Color(1f, 0.86f, 0.24f, 0.95f);
                float lane = i - 1f;
                Vector3 forward = Vector3.right * facing;
                slash.SetPosition(0, origin + Vector3.up * (lane * 0.2f - 0.25f));
                slash.SetPosition(1, origin + forward * (0.38f * rangeMultiplier) + Vector3.up * (lane * 0.16f + 0.22f));
                slash.SetPosition(2, origin + forward * (0.82f * rangeMultiplier) + Vector3.up * (lane * 0.09f + 0.34f));
                slash.SetPosition(3, origin + forward * (1.2f * rangeMultiplier) + Vector3.up * (lane * 0.03f + 0.12f));
                slashes[i] = slash;
            }
            GameSfx.PlayAt(SfxId.CatClawAttach, origin, 1.28f);
            Destroy(root, 0.32f);
            StartCoroutine(FadeCatScratch(slashes, 0.28f));
            if (broadcast)
                stageManager?.BroadcastLocalAbilityEffect(playerController, "cat_scratch");
        }

        private static IEnumerator FadeCatScratch(LineRenderer[] slashes, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < slashes.Length; i++)
                {
                    if (slashes[i] == null) continue;
                    Color start = slashes[i].startColor;
                    Color end = slashes[i].endColor;
                    start.a = alpha;
                    end.a = alpha;
                    slashes[i].startColor = start;
                    slashes[i].endColor = end;
                }
                yield return null;
            }
        }

        private static Bounds GetSolidBounds(GameObject target)
        {
            Bounds bounds = new Bounds(target != null ? target.transform.position : Vector3.zero, Vector3.one * 0.5f);
            if (target == null) return bounds;
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(false);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            return bounds;
        }

        private void UpdateSlimeAttachmentVisual(PlayerController2D target, bool useAttachedAnchor)
        {
            if (target == null || slimeAttachBridge == null || slimeAttachRing == null)
            {
                SetSlimeAttachmentVisualVisible(false);
                return;
            }
            for (int i = 0; i < catClawLines.Length; i++)
            {
                if (catClawLines[i] != null) catClawLines[i].enabled = false;
            }
            for (int i = 0; i < birdBeakLines.Length; i++)
            {
                if (birdBeakLines[i] != null) birdBeakLines[i].enabled = false;
            }

            Vector3 slimeCenter = transform.position;
            if (TryGetSolidBounds(playerController, out Bounds slimeBounds))
            {
                slimeCenter = slimeBounds.center;
            }

            Vector3 targetCenter = target.transform.position;
            Bounds targetBounds = new Bounds(targetCenter, Vector3.one * 0.5f);
            if (TryGetSolidBounds(target, out Bounds measuredTargetBounds))
            {
                targetBounds = measuredTargetBounds;
                targetCenter = measuredTargetBounds.center;
            }

            Vector3 contact = useAttachedAnchor
                ? target.transform.position + target.transform.TransformVector(slimeAttachLocalOffset) * 0.45f
                : targetBounds.ClosestPoint(slimeCenter);
            if ((contact - targetCenter).sqrMagnitude < 0.01f)
            {
                Vector3 towardSlime = (slimeCenter - targetCenter).normalized;
                contact = targetCenter + towardSlime * 0.25f;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            Vector3 delta = contact - slimeCenter;
            Vector3 perpendicular = delta.sqrMagnitude > 0.001f
                ? new Vector3(-delta.y, delta.x, 0f).normalized
                : Vector3.up;
            Vector3 middle = Vector3.Lerp(slimeCenter, contact, 0.55f)
                + perpendicular * Mathf.Lerp(-0.035f, 0.055f, pulse);
            Color gooColor = bodyBuilder != null
                ? Color.Lerp(bodyBuilder.PlayerColor, new Color(0.2f, 1f, 0.55f, 1f), 0.42f)
                : new Color(0.2f, 1f, 0.55f, 1f);
            gooColor.a = 0.82f;

            slimeAttachBridge.enabled = true;
            slimeAttachBridge.startWidth = Mathf.Lerp(0.13f, 0.2f, pulse);
            slimeAttachBridge.endWidth = Mathf.Lerp(0.2f, 0.27f, pulse);
            slimeAttachBridge.startColor = gooColor;
            slimeAttachBridge.endColor = gooColor;
            slimeAttachBridge.SetPosition(0, slimeCenter);
            slimeAttachBridge.SetPosition(1, middle);
            slimeAttachBridge.SetPosition(2, contact);

            slimeAttachRing.enabled = true;
            slimeAttachRing.startWidth = 0.055f;
            slimeAttachRing.endWidth = 0.055f;
            slimeAttachRing.startColor = gooColor;
            slimeAttachRing.endColor = gooColor;
            float radius = Mathf.Lerp(0.22f, 0.3f, pulse);
            for (int i = 0; i < slimeAttachRing.positionCount; i++)
            {
                float angle = i / (float)slimeAttachRing.positionCount * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(angle * 3f + Time.unscaledTime * 6f) * 0.09f;
                slimeAttachRing.SetPosition(
                    i,
                    contact + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * wobble);
            }
        }

        private void SetSlimeAttachmentVisualVisible(bool visible)
        {
            if (slimeAttachBridge != null)
            {
                slimeAttachBridge.enabled = visible;
            }
            if (slimeAttachRing != null)
            {
                slimeAttachRing.enabled = visible;
            }
            if (!visible)
            {
                for (int i = 0; i < catClawLines.Length; i++)
                {
                    if (catClawLines[i] != null) catClawLines[i].enabled = false;
                }
                for (int i = 0; i < birdBeakLines.Length; i++)
                {
                    if (birdBeakLines[i] != null) birdBeakLines[i].enabled = false;
                }
            }
        }

        private static bool TryGetSolidBounds(PlayerController2D controller, out Bounds bounds)
        {
            bounds = controller != null
                ? new Bounds(controller.transform.position, Vector3.zero)
                : new Bounds(Vector3.zero, Vector3.zero);
            if (controller == null)
            {
                return false;
            }

            bool hasBounds = false;
            Collider2D[] colliders = controller.GetComponentsInChildren<Collider2D>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private LineRenderer CreatePreviewLine(string name, Transform parent, float width)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = 40;
            line.startWidth = width;
            line.endWidth = width;
            line.material = GetPreviewMaterial();
            line.startColor = Color.black;
            line.endColor = Color.black;
            return line;
        }

        private void UpdateThrowPreview(Vector3 anchor)
        {
            if (throwPreviewLine == null)
            {
                return;
            }

            SetThrowPreviewVisible(true);

            Vector2 direction = GetThrowDirection();
            displayedThrowDirection = direction;
            displayedThrowFacingDirection = GetFacingDirection();
            hasDisplayedThrowDirection = true;
            Vector3 start = anchor + Vector3.up * 0.1f;
            float previewScale = Mathf.Clamp(GetCurrentThrowSpeed() / Mathf.Max(throwSpeed, 0.1f), 1f, 1.5f);
            Vector3 end = start + (Vector3)(direction * throwPreviewLength * previewScale);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 headBase = end - (Vector3)(direction * 0.32f);
            Vector3 headA = headBase + (Vector3)(perpendicular * 0.16f);
            Vector3 headB = headBase - (Vector3)(perpendicular * 0.16f);

            SetLine(throwPreviewLine, start, end);
            SetLine(throwPreviewHeadA, end, headA);
            SetLine(throwPreviewHeadB, end, headB);
        }

        private void SetThrowPreviewVisible(bool visible)
        {
            if (throwPreviewLine != null)
            {
                throwPreviewLine.enabled = visible;
            }

            if (throwPreviewHeadA != null)
            {
                throwPreviewHeadA.enabled = visible;
            }

            if (throwPreviewHeadB != null)
            {
                throwPreviewHeadB.enabled = visible;
            }
        }

        private static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            if (line == null)
            {
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private Material GetPreviewMaterial()
        {
            if (previewMaterial != null)
            {
                return previewMaterial;
            }

            previewMaterial = DoodleRuntimeAssets.LineMaterial;
            return previewMaterial;
        }

        private float GetCurrentThrowSpeed()
        {
            float armInk = abilityController != null ? abilityController.CurrentProfile.ArmInk : 0f;
            float inkMultiplier = 1f + Mathf.Clamp(armInk * armInkThrowScale, 0f, 2.5f);
            float targetMultiplier = heldPlayerController != null ? heldPlayerThrowMultiplier : 1f;
            return throwSpeed * inkMultiplier * targetMultiplier;
        }

        private IEnumerator RestoreReleasedCollisions(Collider2D[] releasedColliders, Collider2D[] carrierColliders)
        {
            float minimumRestoreAt = Time.time + postThrowCollisionIgnoreTime;
            float restoreDeadline = Time.time + Mathf.Max(postThrowCollisionIgnoreTime, postThrowCollisionRestoreTimeout);
            while (Time.time < minimumRestoreAt
                || (Time.time < restoreDeadline && AnyCollidersOverlap(releasedColliders, carrierColliders)))
            {
                yield return new WaitForFixedUpdate();
            }

            SetCollisionIgnored(releasedColliders, carrierColliders, false);
            stageManager?.RefreshOnlinePlayerCollisionSafety();
        }

        private void RestoreReleasedCollisionsSafely(Collider2D[] releasedColliders, Collider2D[] carrierColliders)
        {
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                StartCoroutine(RestoreReleasedCollisions(releasedColliders, carrierColliders));
                return;
            }

            // A disabled/inactive player cannot host a coroutine. Its colliders
            // are not participating in physics, so immediate restoration is safe
            // and prevents the ignore state leaking into the next retry.
            SetCollisionIgnored(releasedColliders, carrierColliders, false);
            stageManager?.RefreshOnlinePlayerCollisionSafety();
        }

        private static bool AnyCollidersOverlap(Collider2D[] first, Collider2D[] second)
        {
            for (int i = 0; i < first.Length; i++)
            {
                Collider2D a = first[i];
                if (a == null || !a.enabled)
                {
                    continue;
                }

                for (int j = 0; j < second.Length; j++)
                {
                    Collider2D b = second[j];
                    if (b == null || !b.enabled || a == b)
                    {
                        continue;
                    }

                    if (a.Distance(b).isOverlapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RefreshHeldPlayerCollisionIgnores()
        {
            if (heldPlayerController == null || heldTransform == null)
            {
                return;
            }

            heldPlayerColliderScratch.Clear();
            carrierColliderScratch.Clear();
            heldTransform.GetComponentsInChildren(true, heldPlayerColliderScratch);
            GetComponentsInChildren(true, carrierColliderScratch);

            for (int i = 0; i < heldPlayerColliderScratch.Count; i++)
            {
                Collider2D current = heldPlayerColliderScratch[i];
                if (current == null || heldColliderSet.Contains(current))
                {
                    continue;
                }

                // Online redraw and character rebuild can replace all segment
                // colliders without replacing the player root. Remember every
                // replacement so release restores it and never lets it push the
                // carrier from inside during the hold.
                heldColliderSet.Add(current);
                heldColliders.Add(current);
                heldColliderEnabledStates.Add(current.enabled);
                heldColliderTriggerStates.Add(current.isTrigger);
                current.enabled = true;
            }

            SetCollisionIgnored(heldPlayerColliderScratch, carrierColliderScratch, true);
        }

        private void RefreshFriendAttachmentCollisionIgnores()
        {
            if (slimeAttachedPlayer == null || !IsFriendCarrier()) return;

            heldPlayerColliderScratch.Clear();
            carrierColliderScratch.Clear();
            slimeAttachedPlayer.GetComponentsInChildren(true, heldPlayerColliderScratch);
            GetComponentsInChildren(true, carrierColliderScratch);

            if (ColliderListMatchesArray(carrierColliderScratch, slimeOwnColliders)
                && ColliderListMatchesArray(heldPlayerColliderScratch, slimeTargetColliders))
            {
                return;
            }

            // Online redraw and species changes can replace a player's many
            // segment colliders without replacing the player root. Ignore every
            // replacement before the next physics simulation so a carried body
            // cannot launch its cat/bird carrier from inside on a slope.
            SetCollisionIgnored(carrierColliderScratch, heldPlayerColliderScratch, true);
            slimeOwnColliders = carrierColliderScratch.ToArray();
            slimeTargetColliders = heldPlayerColliderScratch.ToArray();
        }

        private static bool ColliderListMatchesArray(IList<Collider2D> current, Collider2D[] previous)
        {
            if (previous == null || current.Count != previous.Length) return false;
            for (int i = 0; i < current.Count; i++)
            {
                Collider2D collider = current[i];
                bool found = false;
                for (int j = 0; j < previous.Length; j++)
                {
                    if (previous[j] != collider) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static void SetCollisionIgnored(
            IList<Collider2D> first,
            IList<Collider2D> second,
            bool ignored)
        {
            for (int i = 0; i < first.Count; i++)
            {
                Collider2D a = first[i];
                if (a == null)
                {
                    continue;
                }

                for (int j = 0; j < second.Count; j++)
                {
                    Collider2D b = second[j];
                    if (b != null && a != b)
                    {
                        Physics2D.IgnoreCollision(a, b, ignored);
                    }
                }
            }
        }

        private string GetHeldOnlinePlayerId(PlayerController2D heldPlayer)
        {
            if (onlineManager == null || stageManager == null || heldPlayer == null)
            {
                return null;
            }

            if (onlineManager.State != OnlineConnectionState.InLobby && onlineManager.State != OnlineConnectionState.Playing)
            {
                return null;
            }

            return stageManager.GetOnlinePlayerId(heldPlayer);
        }

        private bool IsRemoteOnlineReplica()
        {
            if (onlineManager == null
                || stageManager == null
                || playerController == null
                || (onlineManager.State != OnlineConnectionState.InLobby
                    && onlineManager.State != OnlineConnectionState.Matching
                    && onlineManager.State != OnlineConnectionState.Playing))
            {
                return false;
            }

            // The active player is always locally controlled. During lobby/body
            // resynchronisation the id dictionaries can be rebuilt one frame
            // before the EOS local id settles; never let that transient state make
            // the participant's own Human controller ignore F.
            if (stageManager.ActivePlayerTransform == transform)
            {
                return false;
            }

            string playerId = stageManager.GetOnlinePlayerId(playerController);
            return !string.IsNullOrEmpty(playerId)
                && playerId != onlineManager.LocalPlayerId;
        }

        private void SendCarryEvent(string action, Vector2 releaseVelocity)
        {
            if (onlineManager == null || string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                return;
            }

            onlineManager.SendCarryData(new OnlineCarryData
            {
                TargetPlayerId = heldOnlinePlayerId,
                Action = action,
                ReleaseVelocity = releaseVelocity
            });
        }

        private void SendFriendAttachEvent(string action, string targetPlayerId, Vector2 releaseVelocity)
        {
            if (onlineManager == null || string.IsNullOrEmpty(targetPlayerId)) return;
            onlineManager.SendCarryData(new OnlineCarryData
            {
                TargetPlayerId = targetPlayerId,
                Action = action,
                ReleaseVelocity = releaseVelocity,
                LocalOffset = slimeAttachLocalOffset
            });
        }

        private StageGimmickSyncManager ResolveGimmickSyncManager()
        {
            if (gimmickSyncManager == null)
            {
                gimmickSyncManager = FindFirstObjectByType<StageGimmickSyncManager>();
            }

            return gimmickSyncManager;
        }
    }
}
