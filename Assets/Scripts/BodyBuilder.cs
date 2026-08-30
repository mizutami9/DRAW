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
        [SerializeField] private Color playerColor = new Color(0.95f, 0.12f, 0.1f, 1f);

        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private readonly List<GeneratedSegment> generatedSegments = new List<GeneratedSegment>();
        private const float RuntimePointMergeDistance = 0.025f;
        private const float RuntimeSimplifyTolerance = 0.022f;
        private const float RuntimeCoverageCellSize = 0.075f;
        private const int RuntimeOrientationBins = 8;
        private const int MaxRuntimePointsPerStroke = 512;
        private const int MaxRuntimeSegmentsPerPart = 32;
        private Material lineMaterial;
        private static PhysicsMaterial2D playerContactMaterial;
        private Rigidbody2D rb;
        private PlayerController2D playerController;
        private ArmSwingController armSwingController;
        private DrawManager.Species builtSpecies = DrawManager.Species.Human;
        private int facingDirection = 1;
        private bool turtleShellPose;
        private float turtleShellPoseBlend;
        private bool carryingPose;
        private int carryingDirection = 1;
        private Vector3 carryingHandWorldPosition;
        private bool bodyAnimationWasActive;
        private bool remoteAnimationVelocityEnabled;
        private Vector2 remoteAnimationVelocity;

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
            public Vector2 PivotLocal;
            public float BaseLength;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            playerController = GetComponent<PlayerController2D>();
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

            ApplyPlayerContactMaterial();
        }

        private void Update()
        {
            RestoreTurtleHeadBeforeAnimation();
            AnimateGeneratedBody();
            UpdateTurtleHeadRetraction();
        }

        public void BuildFromDrawing(DrawManager drawManager)
        {
            if (drawManager == null)
            {
                return;
            }

            Bounds previousBodyBounds = new Bounds(transform.position, Vector3.zero);
            bool preserveGroundContact = playerController != null
                && playerController.IsGrounded
                && TryGetBuiltBodyColliderBounds(out previousBodyBounds);
            ClearGeneratedBody();
            builtSpecies = drawManager.CurrentSpecies;
            turtleShellPose = false;
            turtleShellPoseBlend = 0f;
            bool hasTorsoBounds = TryGetPartLocalBounds(drawManager.GetBodyPoints(DrawManager.BodyPart.Torso), out Bounds torsoBounds);

            foreach (DrawManager.BodyPart part in drawManager.GetCurrentParts())
            {
                IReadOnlyList<Vector2> points = drawManager.GetBodyPoints(part);
                if (points.Count < 2)
                {
                    continue;
                }

                Vector2 pivot = GetPartAnimationPivot(part, points, builtSpecies, hasTorsoBounds, torsoBounds);
                List<RuntimeBodySegment> visualSegments = BuildRuntimeVisualSegments(points);
                int colliderLimit = part == DrawManager.BodyPart.Head ? 96 : MaxRuntimeSegmentsPerPart;
                List<RuntimeBodySegment> optimizedSegments = BuildOptimizedRuntimeSegments(visualSegments, colliderLimit);
                for (int i = 0; i < visualSegments.Count; i++)
                {
                    CreateSegment(
                        part,
                        visualSegments[i].Start,
                        visualSegments[i].End,
                        pivot,
                        true,
                        false);
                }
                for (int i = 0; i < optimizedSegments.Count; i++)
                {
                    CreateSegment(
                        part,
                        optimizedSegments[i].Start,
                        optimizedSegments[i].End,
                        pivot,
                        false,
                        true);
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
            ApplyPlayerColor();
            playerController?.InvalidateBodyColliderCache();

            // Drawn species use very different local extents. Rebuilding around
            // the same transform origin made a large destination body extend
            // below the floor. While grounded, preserve the previous body's
            // lowest collider point using the newly built collider bounds.
            if (preserveGroundContact)
            {
                Physics2D.SyncTransforms();
                if (TryGetBuiltBodyColliderBounds(out Bounds rebuiltBodyBounds))
                {
                    float verticalCorrection = previousBodyBounds.min.y - rebuiltBodyBounds.min.y;
                    if (Mathf.Abs(verticalCorrection) > 0.0001f)
                    {
                        Vector2 correctedPosition = (Vector2)transform.position + Vector2.up * verticalCorrection;
                        if (rb != null)
                        {
                            rb.position = correctedPosition;
                        }
                        else
                        {
                            transform.position = new Vector3(
                                correctedPosition.x,
                                correctedPosition.y,
                                transform.position.z);
                        }
                        Physics2D.SyncTransforms();
                    }
                }
            }
        }

        private bool TryGetBuiltBodyColliderBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                CapsuleCollider2D collider = generatedSegments[i].Collider;
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

            if (!hasBounds && fallbackCollider != null && fallbackCollider.enabled && !fallbackCollider.isTrigger)
            {
                bounds = fallbackCollider.bounds;
                hasBounds = true;
            }
            return hasBounds;
        }

        private struct RuntimeBodySegment
        {
            public Vector2 Start;
            public Vector2 End;
            public int StrokeIndex;
        }

        private List<RuntimeBodySegment> BuildRuntimeVisualSegments(IReadOnlyList<Vector2> source)
        {
            List<RuntimeBodySegment> candidates = new List<RuntimeBodySegment>();
            List<Vector2> stroke = new List<Vector2>();
            int strokeIndex = 0;
            for (int i = 0; i <= source.Count; i++)
            {
                bool endOfStroke = i == source.Count || DrawManager.IsBreakPoint(source[i]);
                if (!endOfStroke)
                {
                    Vector2 point = ToLocalBodyPoint(source[i]);
                    if (stroke.Count == 0
                        || Vector2.Distance(stroke[stroke.Count - 1], point) >= RuntimePointMergeDistance)
                    {
                        stroke.Add(point);
                    }
                    continue;
                }

                AppendSimplifiedStrokeSegments(stroke, candidates, strokeIndex++);
                stroke.Clear();
            }

            return candidates;
        }

        private List<RuntimeBodySegment> BuildOptimizedRuntimeSegments(
            List<RuntimeBodySegment> candidates,
            int segmentLimit)
        {

            if (candidates.Count <= 1)
            {
                return new List<RuntimeBodySegment>(candidates);
            }

            // Never truncate the tail of a drawing. Each distinct stroke gets at
            // least one continuous collider, and dense strokes are represented
            // by evenly distributed chords from their beginning to their end.
            // Thus detail can be simplified for physics cost, but no region or
            // later stroke silently loses collision altogether.
            List<List<RuntimeBodySegment>> strokes = new List<List<RuntimeBodySegment>>();
            Dictionary<int, int> strokeSlots = new Dictionary<int, int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                RuntimeBodySegment segment = candidates[i];
                if (!strokeSlots.TryGetValue(segment.StrokeIndex, out int slot))
                {
                    slot = strokes.Count;
                    strokeSlots.Add(segment.StrokeIndex, slot);
                    strokes.Add(new List<RuntimeBodySegment>());
                }
                strokes[slot].Add(segment);
            }

            int effectiveLimit = Mathf.Max(segmentLimit, strokes.Count);
            int[] quotas = new int[strokes.Count];
            int assigned = 0;
            for (int i = 0; i < strokes.Count; i++)
            {
                quotas[i] = 1;
                assigned++;
            }
            while (assigned < effectiveLimit)
            {
                int best = -1;
                float bestPressure = 1f;
                for (int i = 0; i < strokes.Count; i++)
                {
                    if (quotas[i] >= strokes[i].Count) continue;
                    float pressure = strokes[i].Count / (float)quotas[i];
                    if (pressure <= bestPressure) continue;
                    bestPressure = pressure;
                    best = i;
                }
                if (best < 0) break;
                quotas[best]++;
                assigned++;
            }

            List<RuntimeBodySegment> optimized = new List<RuntimeBodySegment>(assigned);
            for (int stroke = 0; stroke < strokes.Count; stroke++)
            {
                List<RuntimeBodySegment> source = strokes[stroke];
                int quota = Mathf.Min(quotas[stroke], source.Count);
                for (int part = 0; part < quota; part++)
                {
                    int first = Mathf.FloorToInt(part * source.Count / (float)quota);
                    int lastExclusive = Mathf.FloorToInt((part + 1) * source.Count / (float)quota);
                    int last = Mathf.Max(first, lastExclusive - 1);
                    optimized.Add(new RuntimeBodySegment
                    {
                        Start = source[first].Start,
                        End = source[last].End,
                        StrokeIndex = source[first].StrokeIndex
                    });
                }
            }

            return optimized;
        }

        private static void AppendSimplifiedStrokeSegments(
            List<Vector2> stroke,
            List<RuntimeBodySegment> destination,
            int strokeIndex)
        {
            if (stroke.Count < 2)
            {
                return;
            }

            List<Vector2> runtimePoints = stroke;
            if (stroke.Count > MaxRuntimePointsPerStroke)
            {
                runtimePoints = new List<Vector2>(MaxRuntimePointsPerStroke);
                runtimePoints.Add(stroke[0]);
                float step = (stroke.Count - 1f) / (MaxRuntimePointsPerStroke - 1f);
                for (int i = 1; i < MaxRuntimePointsPerStroke - 1; i++)
                {
                    runtimePoints.Add(stroke[Mathf.RoundToInt(i * step)]);
                }
                runtimePoints.Add(stroke[stroke.Count - 1]);
            }

            List<Vector2> simplified = SimplifyRuntimeStroke(runtimePoints, RuntimeSimplifyTolerance);
            for (int i = 1; i < simplified.Count; i++)
            {
                if (Vector2.Distance(simplified[i - 1], simplified[i]) < RuntimePointMergeDistance)
                {
                    continue;
                }

                destination.Add(new RuntimeBodySegment
                {
                    Start = simplified[i - 1],
                    End = simplified[i],
                    StrokeIndex = strokeIndex
                });
            }
        }

        private static List<Vector2> SimplifyRuntimeStroke(List<Vector2> points, float tolerance)
        {
            if (points.Count <= 2)
            {
                return new List<Vector2>(points);
            }

            bool[] keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;
            float toleranceSquared = tolerance * tolerance;
            Stack<Vector2Int> pendingRanges = new Stack<Vector2Int>();
            pendingRanges.Push(new Vector2Int(0, points.Count - 1));
            while (pendingRanges.Count > 0)
            {
                Vector2Int range = pendingRanges.Pop();
                int first = range.x;
                int last = range.y;
                if (last <= first + 1)
                {
                    continue;
                }

                Vector2 start = points[first];
                Vector2 end = points[last];
                float greatestDistance = 0f;
                int greatestIndex = -1;
                for (int i = first + 1; i < last; i++)
                {
                    float distance = DistanceToSegmentSquared(points[i], start, end);
                    if (distance > greatestDistance)
                    {
                        greatestDistance = distance;
                        greatestIndex = i;
                    }
                }

                if (greatestIndex < 0 || greatestDistance <= toleranceSquared)
                {
                    continue;
                }

                keep[greatestIndex] = true;
                pendingRanges.Push(new Vector2Int(first, greatestIndex));
                pendingRanges.Push(new Vector2Int(greatestIndex, last));
            }

            List<Vector2> result = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    result.Add(points[i]);
                }
            }
            return result;
        }

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return (point - start).sqrMagnitude;
            }

            float amount = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
            Vector2 nearest = start + delta * amount;
            return (point - nearest).sqrMagnitude;
        }

        private static bool AddsNewRuntimeCoverage(RuntimeBodySegment segment, HashSet<long> occupied)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(
                Vector2.Distance(segment.Start, segment.End) / RuntimeCoverageCellSize));
            for (int i = 0; i <= sampleCount; i++)
            {
                Vector2 point = Vector2.Lerp(segment.Start, segment.End, i / (float)sampleCount);
                if (!occupied.Contains(GetRuntimeCoverageKey(point, segment.End - segment.Start)))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddRuntimeCoverage(RuntimeBodySegment segment, HashSet<long> occupied)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(
                Vector2.Distance(segment.Start, segment.End) / RuntimeCoverageCellSize));
            for (int i = 0; i <= sampleCount; i++)
            {
                Vector2 point = Vector2.Lerp(segment.Start, segment.End, i / (float)sampleCount);
                occupied.Add(GetRuntimeCoverageKey(point, segment.End - segment.Start));
            }
        }

        private static long GetRuntimeCoverageKey(Vector2 point, Vector2 direction)
        {
            int x = Mathf.RoundToInt(point.x / RuntimeCoverageCellSize);
            int y = Mathf.RoundToInt(point.y / RuntimeCoverageCellSize);
            float directionlessAngle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x), Mathf.PI);
            int orientation = Mathf.RoundToInt(directionlessAngle / Mathf.PI * RuntimeOrientationBins)
                % RuntimeOrientationBins;
            unchecked
            {
                long key = ((long)(uint)x << 32) | (uint)y;
                return key * 397L ^ (uint)orientation;
            }
        }

        public Color PlayerColor => playerColor;
        public DrawManager.Species BuiltSpecies => builtSpecies;

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            ApplyPlayerColor();
        }

        public void SetFacingDirection(int direction)
        {
            int nextFacing = direction < 0 ? -1 : 1;
            if (nextFacing != facingDirection && bodyRoot != null)
            {
                // An asymmetric hand-drawn head moves to the opposite side when
                // it is mirrored. Move the grains already inside it through the
                // same reflection so turning does not dump the entire load.
                GetComponent<StageGrainCarrier>()?.MirrorContainedParticles(bodyRoot.position.x);
            }
            facingDirection = nextFacing;
            ApplyFacing();
            Physics2D.SyncTransforms();
        }

        public void SetRemoteAnimationVelocity(Vector2 velocity)
        {
            remoteAnimationVelocityEnabled = true;
            remoteAnimationVelocity = velocity;
        }

        public void SetTurtleShellPose(bool active)
        {
            turtleShellPose = active && builtSpecies == DrawManager.Species.Turtle;
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Part != DrawManager.BodyPart.Head)
                {
                    continue;
                }

                if (segment.Line != null)
                {
                    segment.Line.enabled = true;
                }
                if (segment.Collider != null)
                {
                    // The head becomes protected as soon as SPACE is pressed.
                    // It is enabled again after the release animation extends it.
                    segment.Collider.enabled = !turtleShellPose && turtleShellPoseBlend <= 0.12f;
                }
            }
            ApplyFacing();
        }

        private void RestoreTurtleHeadBeforeAnimation()
        {
            if (builtSpecies != DrawManager.Species.Turtle
                || (!turtleShellPose && turtleShellPoseBlend <= 0f))
            {
                return;
            }

            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Part != DrawManager.BodyPart.Head || segment.Transform == null)
                {
                    continue;
                }

                segment.Transform.localPosition = segment.BaseLocalPosition;
                segment.Transform.localRotation = segment.BaseLocalRotation;
                segment.Transform.localScale = Vector3.one;
                if (segment.Line != null)
                {
                    segment.Line.enabled = true;
                    segment.Line.SetPosition(0, new Vector3(-segment.BaseLength * 0.5f, 0f, 0f));
                    segment.Line.SetPosition(1, new Vector3(segment.BaseLength * 0.5f, 0f, 0f));
                }
                if (segment.Collider != null)
                {
                    segment.Collider.size = new Vector2(segment.BaseLength + colliderThickness, colliderThickness);
                }
            }
        }

        private void UpdateTurtleHeadRetraction()
        {
            if (builtSpecies != DrawManager.Species.Turtle)
            {
                turtleShellPoseBlend = 0f;
                return;
            }

            float target = turtleShellPose ? 1f : 0f;
            turtleShellPoseBlend = Mathf.MoveTowards(turtleShellPoseBlend, target, Time.deltaTime * 8f);
            float eased = Mathf.SmoothStep(0f, 1f, turtleShellPoseBlend);
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Part != DrawManager.BodyPart.Head || segment.Transform == null)
                {
                    continue;
                }

                Vector3 shellOpening = new Vector3(segment.PivotLocal.x, segment.PivotLocal.y, segment.Transform.localPosition.z);
                segment.Transform.localPosition = Vector3.Lerp(segment.Transform.localPosition, shellOpening, eased);
                float visibleScale = Mathf.Lerp(1f, 0.08f, eased);
                segment.Transform.localScale = Vector3.one * visibleScale;
                if (segment.Collider != null)
                {
                    segment.Collider.enabled = !turtleShellPose && turtleShellPoseBlend <= 0.12f;
                }
            }
        }

        public Vector3 GetCarryAnchorWorld(int direction)
        {
            Bounds bounds;
            DrawManager.BodyPart corePart = builtSpecies == DrawManager.Species.Slime
                ? DrawManager.BodyPart.SlimeBody
                : DrawManager.BodyPart.Torso;
            if (!TryGetGeneratedPartWorldBounds(corePart, out bounds)
                && !TryGetBaseBodyBounds(out bounds))
            {
                Collider2D fallback = fallbackCollider != null ? fallbackCollider : GetComponent<Collider2D>();
                bounds = fallback != null ? fallback.bounds : new Bounds(transform.position, new Vector3(0.9f, 1.1f, 0f));
            }

            // Drawn limbs can contain distant points or disconnected strokes.
            // Carrying must remain near the playable body, otherwise the held key
            // and throw preview appear to vanish at a remote drawing coordinate.
            // Use the visible torso itself as the origin. Drawn bodies are not
            // guaranteed to be centred on the player Transform, so clamping back
            // to transform.position can move a held object far away from the
            // character that is visibly picking it up.
            float centerX = bounds.center.x;
            float halfWidth = Mathf.Clamp(bounds.extents.x, 0.24f, 0.78f);
            float halfHeight = Mathf.Clamp(bounds.extents.y, 0.28f, 1.1f);
            float topY = bounds.center.y + halfHeight;
            float side = direction < 0 ? -1f : 1f;
            float handY = topY + 0.28f;
            float handX = centerX + side * (halfWidth * 0.72f + 0.28f);
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
                    DestroyUnityObject(bodyRoot.GetChild(i).gameObject);
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
                float shellWidth = turtleShellPose ? 1.12f : 1f;
                float shellHeight = turtleShellPose ? 0.72f : 1f;
                bodyRoot.localScale = new Vector3(facingDirection * shellWidth, shellHeight, 1f);
            }

            if (fallbackRenderer != null)
            {
                fallbackRenderer.flipX = facingDirection < 0;
            }
        }

        private Vector2 GetPartAnimationPivot(
            DrawManager.BodyPart part,
            IReadOnlyList<Vector2> drawPoints,
            DrawManager.Species species,
            bool hasTorsoBounds,
            Bounds torsoBounds)
        {
            if (!TryGetPartLocalBounds(drawPoints, out Bounds bounds))
            {
                return Vector2.zero;
            }

            if (part != DrawManager.BodyPart.Torso
                && part != DrawManager.BodyPart.SlimeBody
                && hasTorsoBounds
                && TryGetTorsoAnimationConnection(part, species, torsoBounds, out Vector2 connectionPivot))
            {
                return connectionPivot;
            }

            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    return GetHeadPivot(bounds, species);
                case DrawManager.BodyPart.SlimeBody:
                    return new Vector2(bounds.center.x, bounds.min.y);
                default:
                    return new Vector2(bounds.center.x, bounds.center.y);
            }
        }

        private Vector2 GetHeadPivot(Bounds bounds, DrawManager.Species species)
        {
            if (species == DrawManager.Species.Cat || species == DrawManager.Species.Turtle)
            {
                return new Vector2(bounds.min.x, bounds.center.y);
            }

            return new Vector2(bounds.center.x, bounds.min.y);
        }

        private bool TryGetTorsoAnimationConnection(
            DrawManager.BodyPart part,
            DrawManager.Species species,
            Bounds torsoBounds,
            out Vector2 point)
        {
            point = Vector2.zero;
            float centerX = torsoBounds.center.x;
            float centerY = torsoBounds.center.y;
            float lowerLeftX = Mathf.Lerp(torsoBounds.min.x, torsoBounds.max.x, 0.25f);
            float lowerRightX = Mathf.Lerp(torsoBounds.min.x, torsoBounds.max.x, 0.75f);

            if (species == DrawManager.Species.Cat)
            {
                float frontX = Mathf.Lerp(torsoBounds.min.x, torsoBounds.max.x, 0.72f);
                float backX = Mathf.Lerp(torsoBounds.min.x, torsoBounds.max.x, 0.28f);
                switch (part)
                {
                    case DrawManager.BodyPart.Head:
                        point = new Vector2(torsoBounds.max.x, centerY);
                        return true;
                    case DrawManager.BodyPart.Tail:
                        point = new Vector2(torsoBounds.min.x, centerY);
                        return true;
                    case DrawManager.BodyPart.LeftFrontLeg:
                        point = new Vector2(frontX - 14f / pixelsPerWorldUnit, torsoBounds.min.y);
                        return true;
                    case DrawManager.BodyPart.RightFrontLeg:
                        point = new Vector2(frontX + 14f / pixelsPerWorldUnit, torsoBounds.min.y);
                        return true;
                    case DrawManager.BodyPart.LeftBackLeg:
                        point = new Vector2(backX - 14f / pixelsPerWorldUnit, torsoBounds.min.y);
                        return true;
                    case DrawManager.BodyPart.RightBackLeg:
                        point = new Vector2(backX + 14f / pixelsPerWorldUnit, torsoBounds.min.y);
                        return true;
                }
            }

            if (species == DrawManager.Species.Turtle && part == DrawManager.BodyPart.Head)
            {
                point = new Vector2(torsoBounds.max.x, centerY);
                return true;
            }

            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    point = new Vector2(centerX, torsoBounds.max.y);
                    return true;
                case DrawManager.BodyPart.LeftArm:
                case DrawManager.BodyPart.LeftFrontLeg:
                case DrawManager.BodyPart.LeftWing:
                    point = new Vector2(torsoBounds.min.x, centerY);
                    return true;
                case DrawManager.BodyPart.RightArm:
                case DrawManager.BodyPart.RightFrontLeg:
                case DrawManager.BodyPart.RightWing:
                    point = new Vector2(torsoBounds.max.x, centerY);
                    return true;
                case DrawManager.BodyPart.LeftLeg:
                case DrawManager.BodyPart.LeftBackLeg:
                    point = new Vector2(lowerLeftX, torsoBounds.min.y);
                    return true;
                case DrawManager.BodyPart.RightLeg:
                case DrawManager.BodyPart.RightBackLeg:
                    point = new Vector2(lowerRightX, torsoBounds.min.y);
                    return true;
                case DrawManager.BodyPart.Tail:
                case DrawManager.BodyPart.TailFeather:
                    point = new Vector2(centerX, torsoBounds.min.y);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetPartLocalBounds(IReadOnlyList<Vector2> drawPoints, out Bounds bounds)
        {
            bool hasPoint = false;
            bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int i = 0; i < drawPoints.Count; i++)
            {
                if (DrawManager.IsBreakPoint(drawPoints[i]))
                {
                    continue;
                }

                Vector2 localPoint = ToLocalBodyPoint(drawPoints[i]);
                if (!hasPoint)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }

            return hasPoint;
        }

        private void CreateSegment(
            DrawManager.BodyPart part,
            Vector2 start,
            Vector2 end,
            Vector2 pivot,
            bool createLine,
            bool createCollider)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            GameObject segment = new GameObject($"{part}Segment");
            segment.layer = gameObject.layer;
            segment.transform.SetParent(bodyRoot, false);
            segment.transform.localPosition = (start + end) * 0.5f;
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            LineRenderer line = null;
            if (createLine)
            {
                line = segment.AddComponent<LineRenderer>();
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
                line.startColor = playerColor;
                line.endColor = playerColor;
            }

            CapsuleCollider2D collider = null;
            if (createCollider)
            {
                collider = segment.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Horizontal;
                collider.size = new Vector2(length + colliderThickness, colliderThickness);
                collider.offset = Vector2.zero;
                collider.sharedMaterial = GetPlayerContactMaterial();
                // Arms are animated and mirrored independently from the locomotion body.
                // Keeping them solid can teleport a long asymmetric arm into a wall on turn.
                collider.isTrigger = IsHumanArm(part);
            }

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
                PivotLocal = pivot,
                BaseLength = length
            });
        }

        private void ApplyPlayerContactMaterial()
        {
            PhysicsMaterial2D material = GetPlayerContactMaterial();
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].sharedMaterial = material;
                }
            }
        }

        private static PhysicsMaterial2D GetPlayerContactMaterial()
        {
            if (playerContactMaterial == null)
            {
                playerContactMaterial = new PhysicsMaterial2D("Player Low Friction")
                {
                    friction = 0.02f,
                    bounciness = 0f,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return playerContactMaterial;
        }

        private void AnimateGeneratedBody()
        {
            if (!Application.isPlaying || generatedSegments.Count == 0)
            {
                return;
            }

            float speed = remoteAnimationVelocityEnabled
                ? Mathf.Abs(remoteAnimationVelocity.x)
                : rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            bool moving = speed > 0.12f;
            float moveBlend = moving && (playerController == null || playerController.IsAnimationGrounded) ? Mathf.Clamp01(speed / 4f) : 0f;
            bool armSwinging = armSwingController != null && armSwingController.IsSwinging;
            bool animationActive = moveBlend > 0.001f || carryingPose || armSwinging;
            if (!animationActive)
            {
                if (bodyAnimationWasActive)
                {
                    RestoreGeneratedSegmentGeometry();
                    if (turtleShellPose)
                    {
                        SetTurtleShellPose(true);
                    }
                    bodyAnimationWasActive = false;
                }
                return;
            }

            bodyAnimationWasActive = true;
            DrawManager.Species species = builtSpecies;
            float phase = Time.time * walkAnimationSpeed;
            Vector2 carryShoulder = Vector2.zero;
            Quaternion carryRotation = Quaternion.identity;
            bool hasCarryArmRotation = carryingPose
                && TryGetCarryArmRotation(out carryShoulder, out carryRotation);

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
                if (armSwinging && IsHumanArm(segment.Part))
                {
                    continue;
                }

                if (carryingPose && IsFacingHumanArm(segment))
                {
                    if (hasCarryArmRotation)
                    {
                        ApplyRigidCarryPose(segment, carryShoulder, carryRotation);
                    }
                    else
                    {
                        RestoreSegmentGeometry(segment);
                    }

                    continue;
                }

                GetWalkMotion(species, segment.Part, phase, moveBlend, ref angle, ref offsetY, ref scaleX, ref scaleY);

                Quaternion motionRotation = Quaternion.Euler(0f, 0f, angle);
                Vector3 pivotedPosition = RotateAroundPivot(segment.BaseLocalPosition, segment.PivotLocal, motionRotation);
                segment.Transform.localPosition = pivotedPosition + new Vector3(0f, offsetY, 0f);
                segment.Transform.localRotation = motionRotation * segment.BaseLocalRotation;
                segment.Transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        private static Vector3 RotateAroundPivot(Vector3 position, Vector2 pivot, Quaternion rotation)
        {
            Vector3 pivot3 = new Vector3(pivot.x, pivot.y, position.z);
            return pivot3 + rotation * (position - pivot3);
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

        private bool TryGetCarryArmRotation(out Vector2 shoulder, out Quaternion rotation)
        {
            shoulder = Vector2.zero;
            rotation = Quaternion.identity;
            if (bodyRoot == null)
            {
                return false;
            }

            bool found = false;
            Vector2 originalHand = Vector2.zero;
            float farthestSqrDistance = 0f;
            DrawManager.BodyPart carryPart = DrawManager.BodyPart.LeftArm;
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment candidate = generatedSegments[i];
                if (candidate.Transform == null || !IsFacingHumanArm(candidate))
                {
                    continue;
                }

                if (!found)
                {
                    found = true;
                    carryPart = candidate.Part;
                    shoulder = candidate.PivotLocal;
                }
                if (candidate.Part != carryPart)
                {
                    continue;
                }

                float startDistance = (candidate.StartLocal - shoulder).sqrMagnitude;
                if (startDistance > farthestSqrDistance)
                {
                    farthestSqrDistance = startDistance;
                    originalHand = candidate.StartLocal;
                }
                float endDistance = (candidate.EndLocal - shoulder).sqrMagnitude;
                if (endDistance > farthestSqrDistance)
                {
                    farthestSqrDistance = endDistance;
                    originalHand = candidate.EndLocal;
                }
            }

            if (!found || farthestSqrDistance < 0.0001f)
            {
                return false;
            }

            Vector3 targetLocal = bodyRoot.InverseTransformPoint(carryingHandWorldPosition);
            Vector2 originalDirection = originalHand - shoulder;
            Vector2 targetDirection = (Vector2)targetLocal - shoulder;
            if (targetDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float originalAngle = Mathf.Atan2(originalDirection.y, originalDirection.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(originalAngle, targetAngle));
            return true;
        }

        private void ApplyRigidCarryPose(GeneratedSegment segment, Vector2 shoulder, Quaternion rotation)
        {
            if (segment.Transform == null)
            {
                return;
            }

            segment.Transform.localPosition = RotateAroundPivot(segment.BaseLocalPosition, shoulder, rotation);
            segment.Transform.localRotation = rotation * segment.BaseLocalRotation;
            segment.Transform.localScale = Vector3.one;
            RestoreSegmentShape(segment);
        }

        private void RestoreSegmentGeometry(GeneratedSegment segment)
        {
            if (segment.Transform == null)
            {
                return;
            }
            segment.Transform.localPosition = segment.BaseLocalPosition;
            segment.Transform.localRotation = segment.BaseLocalRotation;
            segment.Transform.localScale = Vector3.one;
            RestoreSegmentShape(segment);
        }

        private void RestoreSegmentShape(GeneratedSegment segment)
        {
            if (segment.Line != null)
            {
                segment.Line.SetPosition(0, new Vector3(-segment.BaseLength * 0.5f, 0f, 0f));
                segment.Line.SetPosition(1, new Vector3(segment.BaseLength * 0.5f, 0f, 0f));
            }
            if (segment.Collider != null)
            {
                segment.Collider.enabled = true;
                segment.Collider.size = new Vector2(segment.BaseLength + colliderThickness, colliderThickness);
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
                    segment.Collider.enabled = true;
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

        private bool TryGetGeneratedPartWorldBounds(DrawManager.BodyPart part, out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            if (bodyRoot == null)
            {
                return false;
            }

            List<int> candidates = new List<int>();
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                GeneratedSegment segment = generatedSegments[i];
                if (segment.Transform != null && segment.Part == part)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            // A part may contain several separate strokes. Use the largest
            // connected stroke group so one accidental/distant torso line does
            // not drag the carry anchor away from the visible character.
            const float connectionDistance = 0.12f;
            float connectionDistanceSquared = connectionDistance * connectionDistance;
            HashSet<int> visited = new HashSet<int>();
            int bestSegmentCount = 0;
            float bestLength = -1f;
            Bounds bestBounds = bounds;

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                int seed = candidates[candidateIndex];
                if (!visited.Add(seed))
                {
                    continue;
                }

                Queue<int> pending = new Queue<int>();
                pending.Enqueue(seed);
                bool hasComponentBounds = false;
                Bounds componentBounds = new Bounds(transform.position, Vector3.zero);
                int componentCount = 0;
                float componentLength = 0f;

                while (pending.Count > 0)
                {
                    int currentIndex = pending.Dequeue();
                    GeneratedSegment current = generatedSegments[currentIndex];
                    componentCount++;
                    componentLength += Vector2.Distance(current.StartLocal, current.EndLocal);
                    EncapsulatePoint(ref componentBounds, ref hasComponentBounds, bodyRoot.TransformPoint(current.StartLocal));
                    EncapsulatePoint(ref componentBounds, ref hasComponentBounds, bodyRoot.TransformPoint(current.EndLocal));

                    for (int otherCandidate = 0; otherCandidate < candidates.Count; otherCandidate++)
                    {
                        int otherIndex = candidates[otherCandidate];
                        if (visited.Contains(otherIndex))
                        {
                            continue;
                        }

                        GeneratedSegment other = generatedSegments[otherIndex];
                        bool connected =
                            (current.StartLocal - other.StartLocal).sqrMagnitude <= connectionDistanceSquared
                            || (current.StartLocal - other.EndLocal).sqrMagnitude <= connectionDistanceSquared
                            || (current.EndLocal - other.StartLocal).sqrMagnitude <= connectionDistanceSquared
                            || (current.EndLocal - other.EndLocal).sqrMagnitude <= connectionDistanceSquared;
                        if (connected)
                        {
                            visited.Add(otherIndex);
                            pending.Enqueue(otherIndex);
                        }
                    }
                }

                if (hasComponentBounds
                    && (componentCount > bestSegmentCount
                        || (componentCount == bestSegmentCount && componentLength > bestLength)))
                {
                    bestSegmentCount = componentCount;
                    bestLength = componentLength;
                    bestBounds = componentBounds;
                }
            }

            if (bestSegmentCount <= 0)
            {
                return false;
            }

            bounds = bestBounds;
            return true;
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
                case DrawManager.Species.Turtle:
                    ApplyTurtleWalk(part, phase, blend, ref angle, ref offsetY);
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
            float diagonalA = Mathf.Sin(phase);
            float diagonalB = Mathf.Sin(phase + Mathf.PI);
            float diagonalAAngle = diagonalA * walkLimbAngle * 0.72f * blend;
            float diagonalBAngle = diagonalB * walkLimbAngle * 0.72f * blend;
            float diagonalALift = Mathf.Max(0f, diagonalA) * walkBobAmount * 1.35f * blend;
            float diagonalBLift = Mathf.Max(0f, diagonalB) * walkBobAmount * 1.35f * blend;

            switch (part)
            {
                case DrawManager.BodyPart.LeftFrontLeg:
                case DrawManager.BodyPart.RightBackLeg:
                    angle = diagonalAAngle;
                    offsetY = diagonalALift;
                    break;
                case DrawManager.BodyPart.RightFrontLeg:
                case DrawManager.BodyPart.LeftBackLeg:
                    angle = diagonalBAngle;
                    offsetY = diagonalBLift;
                    break;
                case DrawManager.BodyPart.Tail:
                    angle = (Mathf.Sin(phase * 0.72f) * 19f + Mathf.Sin(phase * 1.85f) * 4f) * blend;
                    offsetY = Mathf.Sin(phase * 0.72f) * walkBobAmount * 0.75f * blend;
                    break;
                case DrawManager.BodyPart.Head:
                    offsetY = Mathf.Abs(Mathf.Sin(phase * 2f)) * walkBobAmount * 0.65f * blend;
                    angle = -Mathf.Sin(phase * 2f) * 1.8f * blend;
                    break;
                case DrawManager.BodyPart.Torso:
                    offsetY = Mathf.Abs(Mathf.Sin(phase * 2f)) * walkBobAmount * 0.65f * blend;
                    angle = Mathf.Sin(phase) * 1.2f * blend;
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

        private void ApplyTurtleWalk(DrawManager.BodyPart part, float phase, float blend, ref float angle, ref float offsetY)
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

        private static void DestroyUnityObject(Object target)
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

        private void ApplyPlayerColor()
        {
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                LineRenderer line = generatedSegments[i].Line;
                if (line == null)
                {
                    continue;
                }

                line.startColor = playerColor;
                line.endColor = playerColor;
            }

            if (bodyRoot != null)
            {
                LineRenderer[] bodyLines = bodyRoot.GetComponentsInChildren<LineRenderer>(true);
                for (int i = 0; i < bodyLines.Length; i++)
                {
                    if (bodyLines[i] == null)
                    {
                        continue;
                    }

                    bodyLines[i].startColor = playerColor;
                    bodyLines[i].endColor = playerColor;
                }
            }

            if (fallbackRenderer != null)
            {
                fallbackRenderer.color = playerColor;
            }
        }
    }
}
