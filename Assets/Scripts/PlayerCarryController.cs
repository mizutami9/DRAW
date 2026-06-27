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
        [SerializeField] private float throwSpeed = 22f;
        [SerializeField] private float armInkThrowScale = 0.0175f;
        [SerializeField] private float heldPlayerThrowMultiplier = 1.25f;
        [SerializeField] private float throwAimSpeed = 1.35f;
        [SerializeField] private float throwPreviewLength = 1.8f;

        private readonly Collider2D[] pickupHits = new Collider2D[16];
        private readonly List<Collider2D> heldColliders = new List<Collider2D>();
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

        public bool IsHolding => heldTransform != null;

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

            carryableLayerMask |= 1 << gameObject.layer;
            CreateThrowPreview();
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

        private void TryPickup()
        {
            Vector3 searchCenter = GetPickupSearchCenter();
            int hitCount = Physics2D.OverlapCircleNonAlloc(searchCenter, pickupRadius, pickupHits, carryableLayerMask);
            Transform bestTransform = null;
            CarryableObject bestCarryable = null;
            PlayerController2D bestPlayer = null;
            Rigidbody2D bestBody = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
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

                float distance = Vector2.Distance(searchCenter, candidateTransform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
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
            heldTransform.GetComponentsInChildren(heldColliders);
            for (int i = 0; i < heldColliders.Count; i++)
            {
                heldColliders[i].enabled = false;
            }

            heldPlayerController?.SetControlsEnabled(false);
        }

        private void ThrowHeld()
        {
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 baseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            Vector2 throwVelocity = baseVelocity + GetThrowDirection() * GetCurrentThrowSpeed() * multiplier;
            DropHeld(throwVelocity);
        }

        private void DropHeld(Vector2 releaseVelocity)
        {
            if (heldTransform == null)
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

            heldPlayerController?.ResetMotion();
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
            heldColliders.Clear();
            SetThrowPreviewVisible(false);
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
            float previewScale = Mathf.Clamp(GetCurrentThrowSpeed() / Mathf.Max(throwSpeed, 0.1f), 1f, 3f);
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
    }
}
