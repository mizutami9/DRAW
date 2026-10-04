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
        private const int MaxRuntimeVisualSegmentsPerBody = 96;
        private const int MaxRuntimeColliderSegmentsPerBody = 40;
        private const int GrainCatcherLayer = 30;
        private const int GrainLayer = 31;
        private static bool grainCatcherCollisionConfigured;
        private Material lineMaterial;
        private static PhysicsMaterial2D playerContactMaterial;
        private Rigidbody2D rb;
        private PlayerController2D playerController;
        private ArmSwingController armSwingController;
        private DrawManager.Species builtSpecies = DrawManager.Species.Human;
        private int facingDirection = 1;
        private int appliedFacingDirection = 1;
        private bool facingFlipPending;
        private bool turtleShellPose;
        private float turtleShellPoseBlend;
        private bool carryingPose;
        private int carryingDirection = 1;
        private Vector3 carryingHandWorldPosition;
        private bool bodyAnimationWasActive;
        private bool remoteAnimationVelocityEnabled;
        private Vector2 remoteAnimationVelocity;
        public Transform CelebrationVisualRoot => bodyRoot;

        private struct GeneratedSegment
        {
            public Transform Transform;
            public DrawManager.BodyPart Part;
            public LineRenderer Line;
            public CapsuleCollider2D Collider;
            public Vector2 BaseColliderSize;
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

        private void FixedUpdate()
        {
            TryApplyPendingFacingFlip();
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

            List<DrawManager.BodyPart> runtimeParts = new List<DrawManager.BodyPart>();
            List<Vector2> runtimePivots = new List<Vector2>();
            List<List<RuntimeBodySegment>> rawSegmentLists = new List<List<RuntimeBodySegment>>();
            foreach (DrawManager.BodyPart part in drawManager.GetCurrentParts())
            {
                IReadOnlyList<Vector2> points = drawManager.GetBodyPoints(part);
                IReadOnlyList<float> strokeWidths = drawManager.GetBodyStrokeWidths(part);
                if (points.Count < 2)
                {
                    continue;
                }

                Vector2 pivot = GetPartAnimationPivot(part, points, builtSpecies, hasTorsoBounds, torsoBounds);
                runtimeParts.Add(part);
                runtimePivots.Add(pivot);
                rawSegmentLists.Add(BuildRuntimeVisualSegments(points, strokeWidths));
            }

            int[] visualQuotas = AllocateRuntimeSegmentQuotas(
                rawSegmentLists,
                MaxRuntimeVisualSegmentsPerBody,
                4);
            List<List<RuntimeBodySegment>> visualSegmentLists =
                new List<List<RuntimeBodySegment>>(rawSegmentLists.Count);
            for (int i = 0; i < rawSegmentLists.Count; i++)
            {
                visualSegmentLists.Add(LimitRuntimeVisualComplexity(
                    rawSegmentLists[i],
                    visualQuotas[i]));
            }

            int[] colliderQuotas = AllocateRuntimeSegmentQuotas(
                visualSegmentLists,
                MaxRuntimeColliderSegmentsPerBody,
                2);
            for (int partIndex = 0; partIndex < runtimeParts.Count; partIndex++)
            {
                DrawManager.BodyPart part = runtimeParts[partIndex];
                Vector2 pivot = runtimePivots[partIndex];
                List<RuntimeBodySegment> visualSegments = visualSegmentLists[partIndex];
                List<RuntimeBodySegment> optimizedSegments = BuildOptimizedRuntimeSegments(
                    visualSegments,
                    colliderQuotas[partIndex]);
                for (int i = 0; i < visualSegments.Count; i++)
                {
                    CreateSegment(
                        part,
                        visualSegments[i].Start,
                        visualSegments[i].End,
                        pivot,
                        visualSegments[i].Width,
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
                        optimizedSegments[i].Width,
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

        internal bool CoversWorldPoint(Vector2 worldPoint, float tolerance)
        {
            float allowedDistance = Mathf.Max(0f, tolerance);
            for (int i = 0; i < generatedSegments.Count; i++)
            {
                CapsuleCollider2D collider = generatedSegments[i].Collider;
                if (collider == null || !collider.enabled) continue;

                // Human arms intentionally use trigger colliders so a long arm
                // cannot push the whole player through a wall while turning.
                // They are still part of the drawn silhouette for stage puzzles.
                if (Vector2.Distance(collider.ClosestPoint(worldPoint), worldPoint)
                    <= allowedDistance)
                {
                    return true;
                }
            }

            return fallbackCollider != null
                && fallbackCollider.enabled
                && Vector2.Distance(fallbackCollider.ClosestPoint(worldPoint), worldPoint)
                    <= allowedDistance;
        }

        private struct RuntimeBodySegment
        {
            public Vector2 Start;
            public Vector2 End;
            public int StrokeIndex;
            public float Width;
        }

        private List<RuntimeBodySegment> BuildRuntimeVisualSegments(
            IReadOnlyList<Vector2> source,
            IReadOnlyList<float> strokeWidths)
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

                if (stroke.Count > 0)
                {
                    float sourceWidth = strokeIndex < strokeWidths.Count
                        ? strokeWidths[strokeIndex]
                        : DrawManager.DefaultBrushWidth;
                    float runtimeWidth = GetRuntimeStrokeWidth(sourceWidth);
                    AppendSimplifiedStrokeSegments(stroke, candidates, strokeIndex++, runtimeWidth);
                }
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

            int effectiveLimit = Mathf.Max(1, segmentLimit);
            List<int> includedStrokes = new List<int>(Mathf.Min(strokes.Count, effectiveLimit));
            if (strokes.Count <= effectiveLimit)
            {
                for (int i = 0; i < strokes.Count; i++) includedStrokes.Add(i);
            }
            else
            {
                // A dense drawing can contain hundreds of separate mouse-down
                // strokes. Keeping one collider for every stroke defeated the
                // old per-part limit. Sample strokes across the full drawing so
                // late strokes are represented without allowing unbounded cost.
                for (int i = 0; i < effectiveLimit; i++)
                {
                    includedStrokes.Add(Mathf.RoundToInt(
                        i * (strokes.Count - 1f) / Mathf.Max(1, effectiveLimit - 1)));
                }
            }

            int[] quotas = new int[strokes.Count];
            int assigned = 0;
            for (int i = 0; i < includedStrokes.Count; i++)
            {
                quotas[includedStrokes[i]] = 1;
                assigned++;
            }
            while (assigned < effectiveLimit)
            {
                int best = -1;
                float bestPressure = 1f;
                for (int included = 0; included < includedStrokes.Count; included++)
                {
                    int i = includedStrokes[included];
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
                if (quota <= 0) continue;
                for (int part = 0; part < quota; part++)
                {
                    int first = Mathf.FloorToInt(part * source.Count / (float)quota);
                    int lastExclusive = Mathf.FloorToInt((part + 1) * source.Count / (float)quota);
                    int last = Mathf.Max(first, lastExclusive - 1);
                    optimized.Add(new RuntimeBodySegment
                    {
                        Start = source[first].Start,
                        End = source[last].End,
                        StrokeIndex = source[first].StrokeIndex,
                        Width = source[first].Width
                    });
                }
            }

            return optimized;
        }

        private static int[] AllocateRuntimeSegmentQuotas(
            List<List<RuntimeBodySegment>> segmentLists,
            int totalLimit,
            int minimumPerPart)
        {
            int[] quotas = new int[segmentLists.Count];
            int totalSegments = 0;
            for (int i = 0; i < segmentLists.Count; i++)
            {
                totalSegments += segmentLists[i].Count;
            }

            if (totalSegments <= totalLimit)
            {
                for (int i = 0; i < segmentLists.Count; i++) quotas[i] = segmentLists[i].Count;
                return quotas;
            }

            int assigned = 0;
            int safeMinimum = Mathf.Max(1, minimumPerPart);
            for (int i = 0; i < segmentLists.Count; i++)
            {
                quotas[i] = Mathf.Min(segmentLists[i].Count, safeMinimum);
                assigned += quotas[i];
            }

            int safeLimit = Mathf.Max(assigned, totalLimit);
            while (assigned < safeLimit)
            {
                int best = -1;
                float bestPressure = 1f;
                for (int i = 0; i < segmentLists.Count; i++)
                {
                    if (quotas[i] >= segmentLists[i].Count) continue;
                    float pressure = segmentLists[i].Count / (float)Mathf.Max(1, quotas[i]);
                    if (pressure <= bestPressure) continue;
                    bestPressure = pressure;
                    best = i;
                }

                if (best < 0) break;
                quotas[best]++;
                assigned++;
            }

            return quotas;
        }

        private static List<RuntimeBodySegment> LimitRuntimeVisualComplexity(
            List<RuntimeBodySegment> candidates,
            int segmentLimit)
        {
            int safeLimit = Mathf.Max(2, segmentLimit);
            if (candidates.Count <= safeLimit)
            {
                return candidates;
            }

            // Collapse strokes that occupy the same small cell with the same
            // direction. Dense scribbles retain their silhouette and crosshatch
            // directions, while repeated passes no longer create another
            // GameObject/LineRenderer for identical coverage.
            HashSet<long> occupied = new HashSet<long>();
            List<RuntimeBodySegment> coverage = new List<RuntimeBodySegment>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                RuntimeBodySegment segment = candidates[i];
                if (!AddsNewRuntimeCoverage(segment, occupied))
                {
                    continue;
                }

                coverage.Add(segment);
                AddRuntimeCoverage(segment, occupied);
            }

            if (coverage.Count <= safeLimit)
            {
                return coverage;
            }

            // Keep samples from the entire drawing rather than truncating the
            // tail, which would make later strokes disappear completely.
            List<RuntimeBodySegment> limited = new List<RuntimeBodySegment>(safeLimit);
            for (int i = 0; i < safeLimit; i++)
            {
                int sourceIndex = Mathf.RoundToInt(
                    i * (coverage.Count - 1f) / Mathf.Max(1, safeLimit - 1));
                limited.Add(coverage[sourceIndex]);
            }
            return limited;
        }

        private static void AppendSimplifiedStrokeSegments(
            List<Vector2> stroke,
            List<RuntimeBodySegment> destination,
            int strokeIndex,
            float width)
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
                    StrokeIndex = strokeIndex,
                    Width = width
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

        public float GetRuntimeStrokeWidthInDrawingUnits(float sourceStrokeWidth)
        {
            return GetRuntimeStrokeWidth(sourceStrokeWidth) * pixelsPerWorldUnit;
        }

        private float GetRuntimeStrokeWidth(float sourceStrokeWidth)
        {
            return lineWidth * Mathf.Clamp(sourceStrokeWidth, 1f, 30f)
                / DrawManager.DefaultBrushWidth;
        }

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            ApplyPlayerColor();
        }

        public void SetFacingDirection(int direction)
        {
            int nextFacing = direction < 0 ? -1 : 1;
            facingDirection = nextFacing;
            facingFlipPending = bodyRoot != null && appliedFacingDirection != facingDirection;
        }

        private void TryApplyPendingFacingFlip()
        {
            if (!facingFlipPending || bodyRoot == null)
            {
                return;
            }

            // A drawn body can contain well over one hundred colliders. Mirroring
            // all of them through another player in Update, followed by an
            // immediate SyncTransforms, can create tens of thousands of contacts
            // in one physics step and make the standalone build stop responding.
            // Keep the old physical pose until the swept AABB is clear. Movement
            // is not blocked, so the queued visual/physical turn is applied on the
            // first safe FixedUpdate after the players separate.
            if (WouldFacingFlipIntersectAnotherPlayer())
            {
                return;
            }

            GetComponent<StageGrainCarrier>()?.MirrorCarriedParticles(bodyRoot.position.x);
            appliedFacingDirection = facingDirection;
            facingFlipPending = false;
            ApplyFacing();
        }

        private bool WouldFacingFlipIntersectAnotherPlayer()
        {
            if (playerController == null
                || !TryGetBuiltBodyColliderBounds(out Bounds currentBounds))
            {
                return false;
            }

            float pivotX = bodyRoot.position.x;
            float mirroredMinX = pivotX * 2f - currentBounds.max.x;
            float mirroredMaxX = pivotX * 2f - currentBounds.min.x;
            float sweptMinX = Mathf.Min(currentBounds.min.x, mirroredMinX);
            float sweptMaxX = Mathf.Max(currentBounds.max.x, mirroredMaxX);

            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D other = players[i];
                if (other == null || other == playerController || !other.isActiveAndEnabled)
                {
                    continue;
                }

                BodyBuilder otherBuilder = other.GetComponent<BodyBuilder>();
                if (otherBuilder == null
                    || !otherBuilder.TryGetBuiltBodyColliderBounds(out Bounds otherBounds))
                {
                    continue;
                }

                float horizontalOverlap = Mathf.Min(sweptMaxX, otherBounds.max.x)
                    - Mathf.Max(sweptMinX, otherBounds.min.x);
                if (horizontalOverlap <= 0.01f)
                {
                    continue;
                }

                float verticalOverlap = Mathf.Min(currentBounds.max.y, otherBounds.max.y)
                    - Mathf.Max(currentBounds.min.y, otherBounds.min.y);
                if (verticalOverlap > 0.02f)
                {
                    return true;
                }
            }

            return false;
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
                RestoreSegmentColliderSize(segment);
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
                bodyRoot.localScale = new Vector3(appliedFacingDirection * shellWidth, shellHeight, 1f);
            }

            if (fallbackRenderer != null)
            {
                fallbackRenderer.flipX = appliedFacingDirection < 0;
            }
        }

        public void RestoreBuiltBodyFallbackState()
        {
            bool hasGeneratedBody = false;
            for (int i = 0; i < generatedObjects.Count; i++)
            {
                if (generatedObjects[i] == null) continue;
                hasGeneratedBody = true;
                break;
            }
            if (!hasGeneratedBody) return;
            if (fallbackCollider != null) fallbackCollider.enabled = false;
            if (fallbackRenderer != null) fallbackRenderer.enabled = false;
        }

        public void RestoreAfterRespawnOrRetry()
        {
            carryingPose = false;
            RestoreTurtleHeadBeforeAnimation();
            turtleShellPose = false;
            turtleShellPoseBlend = 0f;
            RestoreGeneratedSegmentGeometry();
            ApplyFacing();
            RestoreBuiltBodyFallbackState();
            playerController?.InvalidateBodyColliderCache();
            Physics2D.SyncTransforms();
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
            float width,
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
                line.startWidth = width;
                line.endWidth = width;
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
                float safeBaseLineWidth = Mathf.Max(0.001f, lineWidth);
                float segmentColliderThickness = Mathf.Max(
                    colliderThickness,
                    colliderThickness * width / safeBaseLineWidth);
                collider.size = new Vector2(
                    length + segmentColliderThickness,
                    segmentColliderThickness);
                collider.offset = Vector2.zero;
                collider.sharedMaterial = GetPlayerContactMaterial();
                // Expressive appendages can be drawn far beyond the locomotion
                // body. They still participate in trigger-based interactions,
                // but must not wedge the player into a narrow passage or ceiling.
                collider.isTrigger = IsNonLocomotionAppendage(part);
                if (IsHumanArm(part))
                {
                    CreateHumanArmGrainCatcher(segment.transform, collider.size);
                }
            }

            generatedObjects.Add(segment);
            generatedSegments.Add(new GeneratedSegment
            {
                Transform = segment.transform,
                Part = part,
                Line = line,
                Collider = collider,
                BaseColliderSize = collider != null ? collider.size : Vector2.zero,
                BaseLocalPosition = segment.transform.localPosition,
                BaseLocalRotation = segment.transform.localRotation,
                StartLocal = start,
                EndLocal = end,
                PivotLocal = pivot,
                BaseLength = length
            });
        }

        private static void CreateHumanArmGrainCatcher(Transform segment, Vector2 size)
        {
            if (segment == null) return;
            if (!grainCatcherCollisionConfigured)
            {
                grainCatcherCollisionConfigured = true;
                for (int layer = 0; layer < 32; layer++)
                    Physics2D.IgnoreLayerCollision(GrainCatcherLayer, layer, layer != GrainLayer);
            }

            // This collider shares the player's Rigidbody2D but lives on a
            // collision layer that only touches grain particles. Human arms
            // therefore catch grains without becoming solid against terrain.
            GameObject catcher = new GameObject("HumanArmGrainCatcher");
            catcher.layer = GrainCatcherLayer;
            catcher.transform.SetParent(segment, false);
            CapsuleCollider2D grainCollider = catcher.AddComponent<CapsuleCollider2D>();
            grainCollider.direction = CapsuleDirection2D.Horizontal;
            grainCollider.size = new Vector2(size.x, Mathf.Max(size.y, 0.22f));
            grainCollider.sharedMaterial = GetPlayerContactMaterial();
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
            bool humanCarryStruggling = playerController != null && playerController.IsHumanCarryStruggling;
            bool animationActive = moveBlend > 0.001f || carryingPose || armSwinging || humanCarryStruggling;
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
                if (humanCarryStruggling && segment.Line != null)
                {
                    float offsetX = 0f;
                    GetHumanCarryStruggleMotion(
                        species,
                        segment.Part,
                        phase,
                        ref angle,
                        ref offsetX,
                        ref offsetY,
                        ref scaleX,
                        ref scaleY);
                    Quaternion struggleRotation = Quaternion.Euler(0f, 0f, angle);
                    segment.Transform.localPosition = RotateAroundPivot(
                        segment.BaseLocalPosition,
                        segment.PivotLocal,
                        struggleRotation) + new Vector3(offsetX, offsetY, 0f);
                    segment.Transform.localRotation = struggleRotation * segment.BaseLocalRotation;
                    segment.Transform.localScale = new Vector3(scaleX, scaleY, 1f);
                    continue;
                }

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

        private static bool IsNonLocomotionAppendage(DrawManager.BodyPart part)
        {
            return IsHumanArm(part)
                || part == DrawManager.BodyPart.Tail
                || part == DrawManager.BodyPart.LeftWing
                || part == DrawManager.BodyPart.RightWing
                || part == DrawManager.BodyPart.TailFeather;
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
                RestoreSegmentColliderSize(segment);
            }
        }

        private static void RestoreSegmentColliderSize(GeneratedSegment segment)
        {
            if (segment.Collider != null)
            {
                segment.Collider.size = segment.BaseColliderSize;
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
                RestoreSegmentShape(segment);
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

        private static void GetHumanCarryStruggleMotion(
            DrawManager.Species species,
            DrawManager.BodyPart part,
            float phase,
            ref float angle,
            ref float offsetX,
            ref float offsetY,
            ref float scaleX,
            ref float scaleY)
        {
            float fast = Mathf.Sin(phase * 1.7f);
            float opposite = Mathf.Sin(phase * 1.7f + Mathf.PI);
            switch (species)
            {
                case DrawManager.Species.Cat:
                    if (part == DrawManager.BodyPart.LeftFrontLeg
                        || part == DrawManager.BodyPart.RightBackLeg)
                    {
                        angle = fast * 34f;
                        offsetY = Mathf.Abs(fast) * 0.035f;
                    }
                    else if (part == DrawManager.BodyPart.RightFrontLeg
                        || part == DrawManager.BodyPart.LeftBackLeg)
                    {
                        angle = opposite * 34f;
                        offsetY = Mathf.Abs(opposite) * 0.035f;
                    }
                    break;
                case DrawManager.Species.Bird:
                    if (part == DrawManager.BodyPart.LeftWing)
                    {
                        angle = fast * 42f;
                    }
                    else if (part == DrawManager.BodyPart.RightWing)
                    {
                        angle = opposite * 42f;
                    }
                    break;
                case DrawManager.Species.Turtle:
                    if (part == DrawManager.BodyPart.Head)
                    {
                        angle = fast * 20f;
                        offsetX = fast * 0.055f;
                    }
                    break;
                case DrawManager.Species.Slime:
                    if (part == DrawManager.BodyPart.SlimeBody)
                    {
                        offsetX = fast * 0.035f;
                        offsetY = opposite * 0.018f;
                        scaleX = 1f + fast * 0.025f;
                        scaleY = 1f - fast * 0.018f;
                    }
                    break;
                default:
                    if (part == DrawManager.BodyPart.LeftLeg)
                    {
                        angle = fast * 32f;
                    }
                    else if (part == DrawManager.BodyPart.RightLeg)
                    {
                        angle = opposite * 32f;
                    }
                    break;
            }
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
