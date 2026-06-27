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
        [SerializeField] private LayerMask carryableLayerMask = ~0;
        [SerializeField] private float pickupRadius = 1.7f;
        [SerializeField] private float throwHorizontalSpeed = 6f;
        [SerializeField] private float throwVerticalSpeed = 2.5f;

        private readonly Collider2D[] pickupHits = new Collider2D[16];
        private readonly List<Collider2D> heldColliders = new List<Collider2D>();
        private CarryableObject heldObject;
        private Rigidbody2D heldBody;
        private RigidbodyType2D previousBodyType;
        private float previousGravityScale;
        private bool previousFreezeRotation;

        public bool IsHolding => heldObject != null;

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
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (playerController != null && !playerController.ControlsEnabled)
            {
                return;
            }

            if (!IsHuman())
            {
                DropHeld(Vector2.zero);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (heldObject == null)
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
            if (heldObject == null)
            {
                return;
            }

            Vector3 anchor = GetHoldPosition();
            heldObject.transform.position = anchor;
            heldObject.transform.rotation = Quaternion.identity;
            if (heldBody != null)
            {
                heldBody.linearVelocity = Vector2.zero;
                heldBody.angularVelocity = 0f;
            }

            bodyBuilder?.SetCarryPose(true, GetFacingDirection(), anchor);
        }

        private void TryPickup()
        {
            Vector3 searchCenter = GetPickupSearchCenter();
            int hitCount = Physics2D.OverlapCircleNonAlloc(searchCenter, pickupRadius, pickupHits, carryableLayerMask);
            CarryableObject best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = pickupHits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                CarryableObject candidate = hit.GetComponentInParent<CarryableObject>();
                if (candidate == null)
                {
                    continue;
                }

                Rigidbody2D candidateBody = candidate.GetComponent<Rigidbody2D>();
                if (candidateBody == null || candidateBody.bodyType == RigidbodyType2D.Static)
                {
                    continue;
                }

                float distance = Vector2.Distance(searchCenter, candidate.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return;
            }

            heldObject = best;
            heldBody = heldObject.GetComponent<Rigidbody2D>();
            previousBodyType = heldBody.bodyType;
            previousGravityScale = heldBody.gravityScale;
            previousFreezeRotation = heldBody.freezeRotation;
            heldBody.bodyType = RigidbodyType2D.Kinematic;
            heldBody.gravityScale = 0f;
            heldBody.freezeRotation = true;
            heldBody.linearVelocity = Vector2.zero;
            heldBody.angularVelocity = 0f;

            heldColliders.Clear();
            heldObject.GetComponentsInChildren(heldColliders);
            for (int i = 0; i < heldColliders.Count; i++)
            {
                heldColliders[i].enabled = false;
            }
        }

        private void ThrowHeld()
        {
            int direction = GetFacingDirection();
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 baseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            Vector2 throwVelocity = baseVelocity + new Vector2(direction * throwHorizontalSpeed * multiplier, throwVerticalSpeed * multiplier);
            DropHeld(throwVelocity);
        }

        private void DropHeld(Vector2 releaseVelocity)
        {
            if (heldObject == null)
            {
                return;
            }

            for (int i = 0; i < heldColliders.Count; i++)
            {
                if (heldColliders[i] != null)
                {
                    heldColliders[i].enabled = true;
                }
            }

            if (heldBody != null)
            {
                heldBody.bodyType = previousBodyType;
                heldBody.gravityScale = previousGravityScale;
                heldBody.freezeRotation = previousFreezeRotation;
                heldBody.linearVelocity = releaseVelocity;
                heldBody.angularVelocity = releaseVelocity.x * -18f;
            }

            heldObject = null;
            heldBody = null;
            heldColliders.Clear();
            bodyBuilder?.SetCarryPose(false, GetFacingDirection(), transform.position);
        }

        public void ForceDrop()
        {
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
    }
}
