using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(PlayerAbilityController))]
    public sealed class ArmSwingController : MonoBehaviour
    {
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerCarryController carryController;
        [SerializeField] private Transform swingPivot;
        [SerializeField] private float normalReach = 1.2f;
        [SerializeField] private float longReach = 1.9f;
        [SerializeField] private float normalDuration = 0.42f;
        [SerializeField] private float fastDuration = 0.24f;
        [SerializeField] private float armThickness = 0.42f;
        [SerializeField] private float pushImpulse = 16f;
        [SerializeField] private float swingReachMultiplier = 2f;
        [SerializeField] private float characterLaunchMultiplier = 2.6f;
        [SerializeField] private float armInkLaunchScale = 0.018f;
        [SerializeField] private float characterLaunchUpSpeed = 30f;
        [SerializeField] private float characterLaunchSideSpeed = 7f;
        [SerializeField] private bool swingEnabled;
        [SerializeField] private LayerMask pushableLayerMask = ~0;

        private GameObject swingObject;
        private LineRenderer line;
        private CapsuleCollider2D swingCollider;
        private ContactFilter2D contactFilter;
        private readonly Collider2D[] hits = new Collider2D[12];
        private bool swinging;
        private float swingTime;
        private float swingDuration;
        private float swingReach;
        private int swingDirection = 1;
        private float lastHorizontalDirection = 1f;
        private Material lineMaterial;
        private VisualArmSegment[] visualArmSegments = new VisualArmSegment[0];
        private readonly HashSet<Rigidbody2D> pushedBodies = new HashSet<Rigidbody2D>();
        public bool IsSwinging => swinging;

        private struct VisualArmSegment
        {
            public Transform Transform;
            public Transform Parent;
            public Collider2D Collider;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public Vector3 PivotLocalPosition;
            public bool ColliderEnabled;
        }

        private void Awake()
        {
            if (abilityController == null)
            {
                abilityController = GetComponent<PlayerAbilityController>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController2D>();
            }

            if (carryController == null)
            {
                carryController = GetComponent<PlayerCarryController>();
            }

            if (swingPivot == null)
            {
                GameObject pivot = new GameObject("ArmSwingPivot");
                pivot.transform.SetParent(transform, false);
                pivot.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                swingPivot = pivot.transform;
            }

            contactFilter = new ContactFilter2D();
            pushableLayerMask |= 1 << gameObject.layer;
            contactFilter.SetLayerMask(pushableLayerMask);
            contactFilter.useTriggers = false;

            CreateSwingObject();
        }

        private void OnDisable()
        {
            RestoreVisualArms();
        }

        private void Update()
        {
            if (!swingEnabled)
            {
                return;
            }

            if (playerController != null && !playerController.ControlsEnabled)
            {
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                lastHorizontalDirection = Mathf.Sign(horizontal);
            }

            if (Input.GetMouseButtonDown(0) && !swinging && Time.timeScale > 0f && (carryController == null || !carryController.IsHolding))
            {
                StartSwing();
            }

            if (swinging)
            {
                UpdateSwing();
            }
        }

        private void StartSwing()
        {
            PlayerAbilityController.AbilityProfile profile = abilityController.CurrentProfile;
            swingReach = (profile.Arm == PlayerAbilityController.ArmTier.LongReach ? longReach : normalReach) * swingReachMultiplier;
            swingDuration = profile.Arm == PlayerAbilityController.ArmTier.FastSwing ? fastDuration : normalDuration;
            swingDirection = lastHorizontalDirection < 0f ? -1 : 1;
            swingTime = 0f;
            swinging = true;
            pushedBodies.Clear();

            ConfigureSwingShape();
            CacheVisualArmSegments(profile.Species);
            swingObject.SetActive(true);
        }

        private void UpdateSwing()
        {
            swingTime += Time.deltaTime;
            float t = Mathf.Clamp01(swingTime / swingDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float swingAngle = Mathf.Lerp(-70f, 75f, eased);
            float hitboxAngle = swingDirection > 0 ? swingAngle : 180f - swingAngle;
            swingObject.transform.localRotation = Quaternion.Euler(0f, 0f, hitboxAngle);
            RotateVisualArms(swingAngle);

            PushOverlappingBodies();

            if (t >= 1f)
            {
                swinging = false;
                swingObject.SetActive(false);
                RestoreVisualArms();
            }
        }

        private void PushOverlappingBodies()
        {
            int hitCount = swingCollider.Overlap(contactFilter, hits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                LeverSwitch lever = hit.GetComponentInParent<LeverSwitch>();
                if (lever != null)
                {
                    lever.Activate();
                }

                Rigidbody2D rb = hit.attachedRigidbody;
                if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
                {
                    continue;
                }

                if (!pushedBodies.Add(rb))
                {
                    continue;
                }

                bool hitPlayer = rb.GetComponentInParent<PlayerController2D>() != null;
                Vector2 direction;
                if (hitPlayer)
                {
                    direction = new Vector2(swingDirection * 0.25f, 1.2f).normalized;
                }
                else
                {
                    direction = (rb.worldCenterOfMass - (Vector2)swingPivot.position).normalized;
                    if (direction.sqrMagnitude <= Mathf.Epsilon)
                    {
                        direction = Vector2.right * swingDirection;
                    }
                }

                float armInk = abilityController != null ? abilityController.CurrentProfile.ArmInk : 0f;
                float inkMultiplier = 1f + Mathf.Clamp(armInk * armInkLaunchScale, 0f, 5f);
                float targetMultiplier = hitPlayer ? characterLaunchMultiplier : 1f;
                if (hitPlayer)
                {
                    float upSpeed = characterLaunchUpSpeed + Mathf.Clamp(armInk * 0.08f, 0f, 24f);
                    float sideSpeed = characterLaunchSideSpeed + Mathf.Clamp(armInk * 0.018f, 0f, 7f);
                    rb.linearVelocity = new Vector2(
                        swingDirection * sideSpeed,
                        Mathf.Max(rb.linearVelocity.y, upSpeed));
                }

                rb.AddForce(direction * pushImpulse * inkMultiplier * targetMultiplier, ForceMode2D.Impulse);
            }
        }

        private void CreateSwingObject()
        {
            swingObject = new GameObject("ArmSwingHitbox");
            swingObject.transform.SetParent(swingPivot, false);
            swingObject.transform.localPosition = Vector3.zero;

            line = swingObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.numCapVertices = 8;
            line.numCornerVertices = 4;
            line.sortingOrder = 20;
            line.material = GetLineMaterial();
            line.startColor = new Color(1f, 0.25f, 0.2f, 0.85f);
            line.endColor = new Color(1f, 0.25f, 0.2f, 0.85f);

            swingCollider = swingObject.AddComponent<CapsuleCollider2D>();
            swingCollider.direction = CapsuleDirection2D.Horizontal;
            swingCollider.isTrigger = true;

            swingObject.SetActive(false);
        }

        private void ConfigureSwingShape()
        {
            line.startWidth = armThickness;
            line.endWidth = armThickness;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, new Vector3(swingReach, 0f, 0f));

            swingObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            swingCollider.size = new Vector2(swingReach, armThickness);
            swingCollider.offset = new Vector2(swingReach * 0.5f, 0f);
        }

        private void CacheVisualArmSegments(DrawManager.Species species)
        {
            RestoreVisualArms();

            if (species != DrawManager.Species.Human)
            {
                visualArmSegments = new VisualArmSegment[0];
                return;
            }

            Transform bodyRoot = transform.Find("GeneratedBody");
            if (bodyRoot == null)
            {
                visualArmSegments = new VisualArmSegment[0];
                return;
            }

            float bodyScaleSign = bodyRoot.lossyScale.x < 0f ? -1f : 1f;
            Transform[] children = bodyRoot.GetComponentsInChildren<Transform>();
            System.Collections.Generic.List<VisualArmSegment> segments = new System.Collections.Generic.List<VisualArmSegment>();
            for (int i = 0; i < children.Length; i++)
            {
                Transform candidate = children[i];
                if (candidate == bodyRoot)
                {
                    continue;
                }

                bool isLeftArm = candidate.name.StartsWith("LeftArmSegment");
                bool isRightArm = candidate.name.StartsWith("RightArmSegment");
                float armScreenSide = isLeftArm ? -bodyScaleSign : bodyScaleSign;
                bool isFacingArm = (isLeftArm || isRightArm) && Mathf.Sign(armScreenSide) == swingDirection;
                if (!isFacingArm)
                {
                    continue;
                }

                Transform parent = candidate.parent;
                Collider2D armCollider = candidate.GetComponent<Collider2D>();
                bool colliderEnabled = armCollider != null && armCollider.enabled;
                if (armCollider != null)
                {
                    armCollider.enabled = false;
                }

                segments.Add(new VisualArmSegment
                {
                    Transform = candidate,
                    Parent = parent,
                    Collider = armCollider,
                    LocalPosition = candidate.localPosition,
                    LocalRotation = candidate.localRotation,
                    LocalScale = candidate.localScale,
                    PivotLocalPosition = parent.InverseTransformPoint(swingPivot.position),
                    ColliderEnabled = colliderEnabled
                });
            }

            visualArmSegments = segments.ToArray();
        }

        private void RotateVisualArms(float angle)
        {
            for (int i = 0; i < visualArmSegments.Length; i++)
            {
                VisualArmSegment segment = visualArmSegments[i];
                if (segment.Transform == null)
                {
                    continue;
                }

                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                Vector3 relative = (segment.LocalPosition - segment.PivotLocalPosition) * swingReachMultiplier;
                segment.Transform.localPosition = segment.PivotLocalPosition + rotation * relative;
                segment.Transform.localRotation = rotation * segment.LocalRotation;
                segment.Transform.localScale = new Vector3(
                    segment.LocalScale.x * swingReachMultiplier,
                    segment.LocalScale.y,
                    segment.LocalScale.z);
            }
        }

        private void RestoreVisualArms()
        {
            for (int i = 0; i < visualArmSegments.Length; i++)
            {
                VisualArmSegment segment = visualArmSegments[i];
                if (segment.Transform == null)
                {
                    continue;
                }

                segment.Transform.localPosition = segment.LocalPosition;
                segment.Transform.localRotation = segment.LocalRotation;
                segment.Transform.localScale = segment.LocalScale;
                if (segment.Collider != null)
                {
                    segment.Collider.enabled = segment.ColliderEnabled;
                }
            }

            visualArmSegments = new VisualArmSegment[0];
            pushedBodies.Clear();
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            return lineMaterial;
        }
    }
}
