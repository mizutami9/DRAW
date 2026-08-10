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

        // A hand-drawn body can contain far more than 64 segment colliders.
        // A fixed NonAlloc buffer could fill with the player's own long legs
        // before the crate underneath was returned by the physics query.
        private readonly List<Collider2D> pickupHits = new List<Collider2D>(128);
        private readonly List<Collider2D> heldColliders = new List<Collider2D>();
        private readonly List<bool> heldColliderEnabledStates = new List<bool>();
        private readonly List<bool> heldColliderTriggerStates = new List<bool>();
        private readonly List<Renderer> heldRenderers = new List<Renderer>();
        private readonly List<int> heldRendererSortingOrders = new List<int>();
        private readonly List<bool> heldRendererEnabledStates = new List<bool>();
        private Transform heldTransform;
        private CarryableObject heldObject;
        private PlayerController2D heldPlayerController;
        private Rigidbody2D heldBody;
        private RigidbodyType2D previousBodyType;
        private float previousGravityScale;
        private bool previousFreezeRotation;
        private LineRenderer throwPreviewLine;
        private LineRenderer throwPreviewHeadA;
        private LineRenderer throwPreviewHeadB;
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
        private bool scriptedSlimeAttachment;
        private bool scriptedSlimeAttachmentHeld;
        private bool scriptedActionEnabled;

        public bool IsHolding => heldTransform != null;
        public string SlimeAttachedOnlinePlayerId => slimeAttachedPlayer != null && stageManager != null
            ? stageManager.GetOnlinePlayerId(slimeAttachedPlayer)
            : null;

        public bool IsHoldingTarget(Transform target)
        {
            return target != null && heldTransform == target;
        }

        public bool IsDraggingFriend(Transform target)
        {
            return IsCat() && target != null && slimeAttachedPlayer != null
                && slimeAttachedPlayer.transform == target;
        }

        public bool ReleaseIfHolding(Transform target)
        {
            if (target == null || heldTransform != target)
            {
                return false;
            }

            DropHeld(Vector2.zero);
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
            Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 baseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 throwVelocity = baseVelocity + normalized * GetCurrentThrowSpeed() * multiplier;
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
                if (slimeAttachedPlayer == null) TryAttachSlimeToFriend();
            }
            else
            {
                DetachSlimeFromFriend(true);
            }
            return slimeAttachedPlayer != null;
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
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            carryableLayerMask |= 1 << gameObject.layer;
            pickupContactFilter = new ContactFilter2D();
            pickupContactFilter.SetLayerMask(carryableLayerMask);
            pickupContactFilter.useTriggers = false;
            CreateThrowPreview();
            CreateSlimeAttachmentVisual();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (playerController != null && !playerController.ControlsEnabled)
            {
                DetachSlimeFromFriend(false);
                return;
            }

            if (scriptedActionEnabled)
            {
                return;
            }

            if (CanAttachToFriend())
            {
                DropHeld(Vector2.zero);
                bool attachHeld = scriptedSlimeAttachment
                    ? scriptedSlimeAttachmentHeld
                    : Input.GetKey(KeyCode.F);
                if (attachHeld)
                {
                    if (slimeAttachedPlayer == null)
                    {
                        TryAttachSlimeToFriend();
                    }
                }
                else
                {
                    DetachSlimeFromFriend(true);
                }
                return;
            }

            DetachSlimeFromFriend(false);
            if (!IsHuman())
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
                else
                {
                    ThrowHeld();
                }
            }
        }

        private void LateUpdate()
        {
            if (slimeAttachedPlayer != null)
            {
                FollowSlimeAttachedFriend();
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
                SetThrowPreviewVisible(false);
                return;
            }

            Vector3 anchor = GetHoldPosition();
            heldTransform.position = anchor;
            heldTransform.rotation = Quaternion.identity;
            if (heldBody != null)
            {
                heldBody.linearVelocity = Vector2.zero;
                heldBody.angularVelocity = 0f;
            }

            bodyBuilder?.SetCarryPose(true, GetFacingDirection(), anchor);
            UpdateThrowPreview(anchor);
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

                Rigidbody2D candidateBody = candidate.GetComponent<Rigidbody2D>();
                if (candidateBody == null || !candidateBody.simulated)
                {
                    continue;
                }

                Collider2D[] candidateColliders = candidate.GetComponentsInChildren<Collider2D>(false);
                float distance = GetClosestColliderDistance(ownColliders, candidateColliders);
                float attachReach = IsCat() ? Mathf.Max(0.65f, slimeFriendAttachReach) : slimeFriendAttachReach;
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
            slimeAttachLocalOffset = IsCat()
                ? transform.InverseTransformVector(bestPlayer.transform.position - transform.position)
                : bestPlayer.transform.InverseTransformVector(transform.position - bestPlayer.transform.position);
            if (slimeAttachLocalOffset.sqrMagnitude < 0.12f)
            {
                slimeAttachLocalOffset = IsCat()
                    ? new Vector3(GetFacingDirection() * 0.78f, 0.18f, 0f)
                    : Vector3.right * -bestPlayer.FacingDirection * 0.55f;
            }

            Rigidbody2D bodyToSuspend = IsCat() ? slimeAttachedBody : playerBody;
            slimePreviousBodyType = bodyToSuspend.bodyType;
            slimePreviousGravityScale = bodyToSuspend.gravityScale;
            slimePreviousFreezeRotation = bodyToSuspend.freezeRotation;
            bodyToSuspend.bodyType = RigidbodyType2D.Kinematic;
            bodyToSuspend.gravityScale = 0f;
            bodyToSuspend.freezeRotation = true;
            bodyToSuspend.linearVelocity = Vector2.zero;
            bodyToSuspend.angularVelocity = 0f;
            slimeAttachedTargetPreviousControlsEnabled = bestPlayer.ControlsEnabled;
            if (IsCat())
            {
                bestPlayer.SetControlsEnabled(false);
                friendAttachedOnlinePlayerId = GetHeldOnlinePlayerId(bestPlayer);
                SendFriendAttachEvent("cat_grab", friendAttachedOnlinePlayerId, Vector2.zero);
            }

            slimeOwnColliders = GetComponentsInChildren<Collider2D>(false);
            slimeTargetColliders = bestPlayer.GetComponentsInChildren<Collider2D>(false);
            SetCollisionIgnored(slimeOwnColliders, slimeTargetColliders, true);
            FollowSlimeAttachedFriend();
            GameSfx.PlayAt(IsCat() ? SfxId.CatClawAttach : SfxId.SlimeStick, transform.position, 1.25f);
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

            if (IsCat())
            {
                Vector3 targetAnchor = transform.position + transform.TransformVector(slimeAttachLocalOffset);
                slimeAttachedPlayer.transform.position = targetAnchor;
                slimeAttachedPlayer.transform.rotation = Quaternion.identity;
                slimeAttachedBody.position = targetAnchor;
                slimeAttachedBody.rotation = 0f;
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

        private void DetachSlimeFromFriend(bool playSound)
        {
            if (slimeAttachedPlayer == null)
            {
                return;
            }

            PlayerController2D releasedTarget = slimeAttachedPlayer;
            bool catDragging = IsCat();
            Vector2 releaseVelocity = catDragging && playerBody != null
                ? playerBody.linearVelocity
                : slimeAttachedBody != null ? slimeAttachedBody.linearVelocity : Vector2.zero;
            Collider2D[] releasedOwnColliders = slimeOwnColliders;
            Collider2D[] releasedTargetColliders = slimeTargetColliders;
            bool separatedFromTarget = !catDragging && SeparateFromAttachmentTarget(
                releasedTarget, releasedOwnColliders, releasedTargetColliders);
            if (catDragging)
            {
                SendFriendAttachEvent("cat_release", friendAttachedOnlinePlayerId, releaseVelocity);
            }
            slimeAttachedPlayer = null;
            slimeAttachedBody = null;
            friendAttachedOnlinePlayerId = null;
            slimeOwnColliders = new Collider2D[0];
            slimeTargetColliders = new Collider2D[0];
            SetSlimeAttachmentVisualVisible(false);

            Rigidbody2D bodyToRestore = catDragging ? releasedTarget.GetComponent<Rigidbody2D>() : playerBody;
            if (bodyToRestore != null)
            {
                bodyToRestore.bodyType = slimePreviousBodyType;
                bodyToRestore.gravityScale = slimePreviousGravityScale;
                bodyToRestore.freezeRotation = slimePreviousFreezeRotation;
                bodyToRestore.rotation = 0f;
                bodyToRestore.linearVelocity = releaseVelocity;
                bodyToRestore.angularVelocity = 0f;
            }
            if (catDragging)
            {
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
                    StartCoroutine(RestoreReleasedCollisions(releasedOwnColliders, releasedTargetColliders));
                }
            }
            if (playSound)
            {
                GameSfx.PlayAt(IsCat() ? SfxId.CatClawRelease : SfxId.SlimeRelease, transform.position, 1.1f);
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
                GameSfx.PlayAt(IsCat() ? SfxId.CatClawAttach : SfxId.SlimeStick, transform.position, 1.1f);
            }
            else if (wasAttached)
            {
                GameSfx.PlayAt(IsCat() ? SfxId.CatClawRelease : SfxId.SlimeRelease, transform.position, 0.9f);
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

                Rigidbody2D candidateBody = candidateTransform.GetComponent<Rigidbody2D>();
                if (candidateBody == null || candidateBody.bodyType == RigidbodyType2D.Static)
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
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            heldTransform.GetComponentsInChildren(heldColliders);
            bool holdingKey = IsHeldGameplayKey();
            for (int i = 0; i < heldColliders.Count; i++)
            {
                Collider2D heldCollider = heldColliders[i];
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
            heldPlayerController?.SetControlsEnabled(false);
            heldTransform.GetComponent<StageBomb>()?.NotifyPickedUp();
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
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 baseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            float currentThrowSpeed = GetCurrentThrowSpeed();
            if (IsHeldGameplayKey())
            {
                // Arm ink can raise the generic throw speed to roughly 77 units/s.
                // Keep puzzle keys controllable and on screen.
                currentThrowSpeed = Mathf.Clamp(currentThrowSpeed * 0.45f, 8f, 14f);
            }
            Vector2 throwVelocity = baseVelocity + GetThrowDirection() * currentThrowSpeed * multiplier;
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
                return;
            }

            bodyBuilder?.SetCarryPose(false, GetFacingDirection(), transform.position);
            if (heldObject != null)
            {
                ResolveGimmickSyncManager()?.EndLocalObjectCarry(heldTransform, releaseVelocity);
            }
            Collider2D[] releasedColliders = heldColliders.ToArray();
            Collider2D[] carrierColliders = GetComponentsInChildren<Collider2D>(false);

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

            heldPlayerController?.ResetMotion();
            heldPlayerController?.SetControlsEnabled(true);
            if (heldBody != null)
            {
                heldBody.bodyType = previousBodyType;
                heldBody.gravityScale = previousGravityScale;
                heldBody.freezeRotation = previousFreezeRotation;
                heldBody.linearVelocity = releaseVelocity;
                heldBody.angularVelocity = releaseVelocity.x * -18f;
            }

            heldTransform = null;
            heldObject = null;
            heldPlayerController = null;
            heldBody = null;
            heldOnlinePlayerId = null;
            heldColliders.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            SetThrowPreviewVisible(false);
            StartCoroutine(RestoreReleasedCollisions(releasedColliders, carrierColliders));
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

        private bool CanAttachToFriend()
        {
            return IsSlime() || IsCat();
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
            SetSlimeAttachmentVisualVisible(false);
        }

        private void UpdateFriendAttachmentVisual(PlayerController2D target, bool useAttachedAnchor)
        {
            if (IsCat())
            {
                UpdateCatAttachmentVisual(target, useAttachedAnchor);
            }
            else
            {
                UpdateSlimeAttachmentVisual(target, useAttachedAnchor);
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

            previewMaterial = new Material(Shader.Find("Sprites/Default"));
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

        private static void SetCollisionIgnored(Collider2D[] first, Collider2D[] second, bool ignored)
        {
            for (int i = 0; i < first.Length; i++)
            {
                Collider2D a = first[i];
                if (a == null)
                {
                    continue;
                }

                for (int j = 0; j < second.Length; j++)
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
                gimmickSyncManager = FindObjectOfType<StageGimmickSyncManager>();
            }

            return gimmickSyncManager;
        }
    }
}
