using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BodyBuilder : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private BoxCollider2D fallbackCollider;
        [SerializeField] private SpriteRenderer fallbackRenderer;
        [SerializeField] private float pixelsPerWorldUnit = 200f;
        [SerializeField] private float colliderThickness = 0.18f;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private float walkAnimationSpeed = 9f;
        [SerializeField] private float walkLimbAngle = 18f;
        [SerializeField] private float walkBobAmount = 0.035f;
        [SerializeField] private float carryArmMaxLength = 1.45f;

        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private readonly List<GeneratedSegment> generatedSegments = new List<GeneratedSegment>();
        private Material lineMaterial;
        private Rigidbody2D rb;
        private PlayerController2D playerController;
        private PlayerAbilityController abilityController;
        private ArmSwingController armSwingController;
        private int facingDirection = 1;
        private bool carryingPose;
        private int carryingDirection = 1;
        private Vector3 carryingHandWorldPosition;

        private struct GeneratedSegment
        {
            public Transform Transform;
            public DrawManager.BodyPart Part;
            public LineRenderer Line;
            public CapsuleCollider2D Collider;
            public Vector3 BaseLocalPosition;
            public Quaternion BaseLocalRotation;
            public Vector2 StartLocal;
            public Vector2 EndLocal;
            public float BaseLength;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            playerController = GetComponent<PlayerController2D>();
            abilityController = GetComponent<PlayerAbilityController>();
            armSwingController = GetComponent<ArmSwingController>();

            if (bodyRoot == null)
            {
                GameObject root = new GameObject("GeneratedBody");
                root.transform.SetParent(transform, false);
                bodyRoot = root.transform;
            }

            if (fallbackCollider == null)
            {
                fallbackCollider = GetComponent<BoxCollider2D>();
            }

            if (fallbackRenderer == null)
            {
                fallbackRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            AnimateGeneratedBody();
        }

        public void BuildFromDrawing(DrawManager drawManager)
        {
            if (drawManager == null)
            {
                return;
            }

            ClearGeneratedBody();

            foreach (DrawManager.BodyPart part in drawManager.GetCurrentParts())
            {
                IReadOnlyList<Vector2> points = drawManager.GetBodyPoints(part);
                if (points.Count < 2)
                {
                    continue;
                }

                for (int i = 1; i < points.Count; i++)
                {
                    if (DrawManager.IsBreakPoint(points[i - 1]) || DrawManager.IsBreakPoint(points[i]))
                    {
                        continue;
                    }

                    Vector2 start = ToLocalBodyPoint(points[i - 1]);
                    Vector2 end = ToLocalBodyPoint(points[i]);
                    CreateSegment(part, start, end);
                }
            }

            if (generatedObjects.Count > 0 && fallbackCollider != null)
            {
                fallbackCollider.enabled = false;
            }

            if (generatedObjects.Count > 0 && fallbackRenderer != null)
            {
                fallbackRenderer.enabled = false;
            }

            ApplyFacing();
        }

        public void SetFacingDirection(int direction)
        {
            facingDirection = direction < 0 ? -1 : 1;
            ApplyFacing();
        }

        public Vector3 GetCarryAnchorWorld(int direction)
        {
            Bounds bounds;
            if (!TryGetBaseBodyBounds(out bounds))
            {
                Collider2D fallback = fallbackCollider != null ? fallbackCollider : GetComponent<Collider2D>();
                bounds = fallback != null ? fallback.bounds : new Bounds(transform.position, new Vector3(0.9f, 1.1f, 0f));
            }

            float side = direction < 0 ? -1f : 1f;
            float handY = Mathf.Max(bounds.max.y + 0.32f, bounds.center.y + bounds.size.y * 0.45f);
            float handX = bounds.center.x + side * (bounds.size.x * 0.36f + 0.28f);
            return new Vector3(handX, handY, transform.position.z);
        }

        public void SetCarryPose(bool active, int direction, Vector3 handWorldPosition)
        {
            carryingPose = active;
            carryingDirection = direction < 0 ? -1 : 1;
            carryingHandWorldPosition = handWorldPosition;

            if (!active)
            {
                RestoreGeneratedSegmentGeometry();
            }
        }

        private void ClearGeneratedBody()
        {
            generatedObjects.Clear();
            generatedSegments.Clear();

            if (bodyRoot != null)
            {
                for (int i = bodyRoot.childCount - 1; i >= 0; i--)
                {
                    DestroyObject(bodyRoot.GetChild(i).gameObject);
                }
            }

            if (fallbackCollider != null)
            {
                fallbackCollider.enabled = true;
            }

            if (fallbackRenderer != null)
            {
                fallbackRenderer.enabled = true;
            }
        }

        private void ApplyFacing()
        {
            if (bodyRoot != null)
            {
                bodyRoot.localScale = new Vector3(facingDirection, 1f, 1f);
            }

            if (fallbackRenderer != null)
            {
                fallbackRenderer.flipX = facingDirection < 0;
            }
        }

        private void CreateSegment(DrawManager.BodyPart part, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            GameObject segment = new GameObject($"{part}Segment");
            segment.transform.SetParent(bodyRoot, false);
            segment.transform.localPosition = (start + end) * 0.5f;
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            LineRenderer line = segment.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
            line.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 8;
            line.numCornerVertices = 4;
            line.sortingOrder = 10;
            line.material = GetLineMaterial();
            line.startColor = GetPartColor(part);
            line.endColor = GetPartColor(part);

            CapsuleCollider2D collider = segment.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.size = new Vector2(length + colliderThickness, colliderThickness);
            collider.offset = Vector2.zero;

            generatedObjects.Add(segment);
            generatedSegments.Add(new GeneratedSegment
            {
                Transform = segment.transform,
                Part = part,
                Line = line,
                Collider = collider,
                BaseLocalPosition = segment.transform.localPosition,
                BaseLocalRotation = segment.transform.localRotation,
                StartLocal = start,
                EndLocal = end,
                BaseLength = length
            });
        }

        private void AnimateGeneratedBody()
        {
            if (!Application.isPlaying || generatedSegments.Count == 0)
            {
                return;
            }

            float speed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            bool moving = speed > 0.12f;
            float moveBlend = moving && (playerController == null || playerController.IsGrounded) ? Mathf.Clamp01(speed / 4f) : 0f;
            DrawManager.Species species = abilityController != null ? abilityController.CurrentProfile.Species : DrawManager.Species.Human;
            float phase = Time.time * walkAnimationSpeed;

            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Transform == null)
                {
                    continue;
                }

                float offsetY = 0f;
                float angle = 0f;
                float scaleX = 1f;
                float scaleY = 1f;
                if (armSwingController != null && armSwingController.IsSwinging && IsHumanArm(segment.Part))
                {
                    continue;
                }

                if (carryingPose && IsFacingHumanArm(segment))
                {
                    ApplyCarryPose(segment);
                    continue;
                }

                GetWalkMotion(species, segment.Part, phase, moveBlend, ref angle, ref offsetY, ref scaleX, ref scaleY);

                segment.Transform.localPosition = segment.BaseLocalPosition + new Vector3(0f, offsetY, 0f);
                segment.Transform.localRotation = segment.BaseLocalRotation * Quaternion.Euler(0f, 0f, angle);
                segment.Transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        private static bool IsHumanArm(DrawManager.BodyPart part)
        {
            return part == DrawManager.BodyPart.LeftArm || part == DrawManager.BodyPart.RightArm;
        }

        private bool IsFacingHumanArm(GeneratedSegment segment)
        {
            if (!IsHumanArm(segment.Part))
            {
                return false;
            }

            float bodyScaleSign = bodyRoot != null && bodyRoot.lossyScale.x < 0f ? -1f : 1f;
            float armScreenSide = segment.Part == DrawManager.BodyPart.LeftArm ? -bodyScaleSign : bodyScaleSign;
            return Mathf.Sign(armScreenSide) == carryingDirection;
        }

        private void ApplyCarryPose(GeneratedSegment segment)
        {
            if (segment.Transform == null || bodyRoot == null)
            {
                return;
            }

            Vector3 targetLocal = bodyRoot.InverseTransformPoint(carryingHandWorldPosition);
            Vector2 shoulder = segment.StartLocal;
            Vector2 delta = (Vector2)targetLocal - shoulder;
            float length = Mathf.Clamp(delta.magnitude, 0.08f, carryArmMaxLength);
            if (delta.sqrMagnitude > 0.0001f)
            {
                delta = delta.normalized * length;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            segment.Transform.localPosition = shoulder + delta * 0.5f;
            segment.Transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            segment.Transform.localScale = Vector3.one;

            if (segment.Line != null)
            {
                segment.Line.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
                segment.Line.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
            }

            if (segment.Collider != null)
            {
                segment.Collider.size = new Vector2(length + colliderThickness, colliderThickness);
            }
        }

        private void RestoreGeneratedSegmentGeometry()
        {
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Transform == null)
                {
                    continue;
                }

                segment.Transform.localPosition = segment.BaseLocalPosition;
                segment.Transform.localRotation = segment.BaseLocalRotation;
                segment.Transform.localScale = Vector3.one;
                if (segment.Line != null)
                {
                    segment.Line.SetPosition(0, new Vector3(-segment.BaseLength * 0.5f, 0f, 0f));
                    segment.Line.SetPosition(1, new Vector3(segment.BaseLength * 0.5f, 0f, 0f));
                }

                if (segment.Collider != null)
                {
                    segment.Collider.size = new Vector2(segment.BaseLength + colliderThickness, colliderThickness);
                }
            }
        }

        private bool TryGetGeneratedBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != rb)
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

        private bool TryGetBaseBodyBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);
            bool hasNonArm = false;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < generatedSegments.Count; i++)
                {
                    GeneratedSegment segment = generatedSegments[i];
                    if (segment.Transform == null || bodyRoot == null)
                    {
                        continue;
                    }

                    bool isArm = IsHumanArm(segment.Part);
                    if (pass == 0 && isArm)
                    {
                        continue;
                    }

                    if (pass == 1 && hasNonArm)
                    {
                        continue;
                    }

                    Vector3 start = bodyRoot.TransformPoint(segment.StartLocal);
                    Vector3 end = bodyRoot.TransformPoint(segment.EndLocal);
                    EncapsulatePoint(ref bounds, ref hasBounds, start);
                    EncapsulatePoint(ref bounds, ref hasBounds, end);
                    if (!isArm)
                    {
                        hasNonArm = true;
                    }
                }

                if (hasBounds)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EncapsulatePoint(ref Bounds bounds, ref bool hasBounds, Vector3 point)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }

        private void GetWalkMotion(
            DrawManager.Species species,
            DrawManager.BodyPart part,
            float phase,
            float blend,
            ref float angle,
            ref float offsetY,
            ref float scaleX,
            ref float scaleY)
        {
            if (blend <= 0.001f)
            {
                return;
            }

            switch (species)
            {
                case DrawManager.Species.Cat:
                    ApplyCatWalk(part, phase, blend, ref angle, ref offsetY);
                    break;
                case DrawManager.Species.Bird:
                    ApplyBirdWalk(part, phase, blend, ref angle, ref offsetY);
                    break;
                case DrawManager.Species.Snake:
                    ApplySnakeWalk(part, phase, blend, ref angle, ref offsetY);
                    break;
                case DrawManager.Species.Slime:
                    ApplySlimeWalk(part, phase, blend, ref offsetY, ref scaleX, ref scaleY);
                    break;
                default:
                    ApplyHumanWalk(part, phase, blend, ref angle, ref offsetY);
                    break;
            }
        }

        private void ApplyHumanWalk(DrawManager.BodyPart part, float phase, float blend, ref float angle, ref float offsetY)
        {
            float swing = Mathf.Sin(phase) * walkLimbAngle * blend;
            float oppositeSwing = Mathf.Sin(phase + Mathf.PI) * walkLimbAngle * blend;

            switch (part)
            {
                case DrawManager.BodyPart.LeftLeg:
                    angle = swing;
                    break;
                case DrawManager.BodyPart.RightLeg:
                    angle = oppositeSwing;
                    break;
                case DrawManager.BodyPart.LeftArm:
                    angle = oppositeSwing * 0.75f;
                    break;
                case DrawManager.BodyPart.RightArm:
                    angle = swing * 0.75f;
                    break;
                case DrawManager.BodyPart.Torso:
                case DrawManager.BodyPart.Head:
                    offsetY = Mathf.Abs(Mathf.Sin(phase)) * walkBobAmount * blend;
                    angle = Mathf.Sin(phase * 0.5f) * 2f * blend;
                    break;
            }
        }

        private void ApplyCatWalk(DrawManager.BodyPart part, float phase, float blend, ref float angle, ref float offsetY)
        {
            float frontStep = Mathf.Max(0f, Mathf.Sin(phase)) * walkBobAmount * 0.9f * blend;
            float backStep = Mathf.Max(0f, Mathf.Sin(phase + Mathf.PI)) * walkBobAmount * 0.9f * blend;

            switch (part)
            {
                case DrawManager.BodyPart.LeftFrontLeg:
                case DrawManager.BodyPart.RightBackLeg:
                    offsetY = frontStep;
                    break;
                case DrawManager.BodyPart.RightFrontLeg:
                case DrawManager.BodyPart.LeftBackLeg:
                    offsetY = backStep;
                    break;
                case DrawManager.BodyPart.Tail:
                    angle = Mathf.Sin(phase * 0.65f) * 14f * blend;
                    offsetY = Mathf.Sin(phase * 0.65f) * walkBobAmount * 0.6f * blend;
                    break;
                case DrawManager.BodyPart.Torso:
                case DrawManager.BodyPart.Head:
                    offsetY = Mathf.Abs(Mathf.Sin(phase * 2f)) * walkBobAmount * 0.65f * blend;
                    break;
            }
        }

        private void ApplyBirdWalk(DrawManager.BodyPart part, float phase, float blend, ref float angle, ref float offsetY)
        {
            switch (part)
            {
                case DrawManager.BodyPart.LeftWing:
                    angle = Mathf.Sin(phase) * 24f * blend;
                    break;
                case DrawManager.BodyPart.RightWing:
                    angle = Mathf.Sin(phase + Mathf.PI) * 24f * blend;
                    break;
                case DrawManager.BodyPart.Head:
                    offsetY = Mathf.Abs(Mathf.Sin(phase * 1.4f)) * walkBobAmount * 1.2f * blend;
                    angle = Mathf.Sin(phase * 1.4f) * 5f * blend;
                    break;
                case DrawManager.BodyPart.Torso:
                    offsetY = Mathf.Abs(Mathf.Sin(phase * 1.4f)) * walkBobAmount * blend;
                    break;
            }
        }

        private void ApplySnakeWalk(DrawManager.BodyPart part, float phase, float blend, ref float angle, ref float offsetY)
        {
            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    angle = Mathf.Sin(phase * 0.9f) * 12f * blend;
                    offsetY = Mathf.Sin(phase * 0.9f) * walkBobAmount * 0.6f * blend;
                    break;
                case DrawManager.BodyPart.Torso:
                    angle = Mathf.Sin(phase * 0.9f + Mathf.PI * 0.5f) * 8f * blend;
                    offsetY = Mathf.Sin(phase * 0.9f + Mathf.PI * 0.5f) * walkBobAmount * 0.5f * blend;
                    break;
            }
        }

        private void ApplySlimeWalk(DrawManager.BodyPart part, float phase, float blend, ref float offsetY, ref float scaleX, ref float scaleY)
        {
            if (part != DrawManager.BodyPart.SlimeBody)
            {
                return;
            }

            float squash = Mathf.Sin(phase * 1.2f) * 0.08f * blend;
            scaleX = 1f + squash;
            scaleY = 1f - squash * 0.75f;
            offsetY = Mathf.Abs(Mathf.Sin(phase * 1.2f)) * walkBobAmount * 0.8f * blend;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (target is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Vector2 ToLocalBodyPoint(Vector2 drawPoint)
        {
            return drawPoint / pixelsPerWorldUnit;
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
            return lineMaterial;
        }

        private static Color GetPartColor(DrawManager.BodyPart part)
        {
            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    return new Color(1f, 0.75f, 0.22f);
                case DrawManager.BodyPart.Torso:
                    return new Color(0.1f, 0.35f, 1f);
                case DrawManager.BodyPart.LeftArm:
                case DrawManager.BodyPart.RightArm:
                case DrawManager.BodyPart.LeftFrontLeg:
                case DrawManager.BodyPart.RightFrontLeg:
                case DrawManager.BodyPart.LeftBackLeg:
                case DrawManager.BodyPart.RightBackLeg:
                    return new Color(0.98f, 0.32f, 0.28f);
                case DrawManager.BodyPart.LeftLeg:
                case DrawManager.BodyPart.RightLeg:
                    return new Color(0.16f, 0.75f, 0.32f);
                case DrawManager.BodyPart.Tail:
                case DrawManager.BodyPart.TailFeather:
                    return new Color(0.95f, 0.55f, 0.18f);
                case DrawManager.BodyPart.LeftWing:
                case DrawManager.BodyPart.RightWing:
                    return new Color(0.45f, 0.35f, 0.95f);
                case DrawManager.BodyPart.SlimeBody:
                    return new Color(0.3f, 0.85f, 0.75f);
                default:
                    return Color.white;
            }
        }
    }
}
