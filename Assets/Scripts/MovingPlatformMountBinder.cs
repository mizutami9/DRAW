using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Mounts stationary stage objects placed on/inside a moving platform so
    /// switches, hazards, emitters and decorations travel with the platform.
    /// Dynamic objects remain independent and are carried by normal physics.
    /// </summary>
    public static class MovingPlatformMountBinder
    {
        private const float ContactTolerance = 0.28f;
        private const float MaximumRelativeSpan = 2.5f;

        public static void Bind(Transform stageRoot)
        {
            if (stageRoot == null)
            {
                return;
            }

            StageEditorObject[] allObjects = stageRoot.GetComponentsInChildren<StageEditorObject>(true);
            List<StageEditorObject> platforms = new List<StageEditorObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                StageEditorObject candidate = allObjects[i];
                if (candidate != null && candidate.type == StageObjectType.MovingPlatform)
                {
                    platforms.Add(candidate);
                }
            }

            if (platforms.Count == 0)
            {
                return;
            }

            for (int i = 0; i < allObjects.Length; i++)
            {
                StageEditorObject candidate = allObjects[i];
                if (!CanMount(candidate, stageRoot))
                {
                    continue;
                }

                StageEditorObject bestPlatform = null;
                float bestScore = float.PositiveInfinity;
                for (int platformIndex = 0; platformIndex < platforms.Count; platformIndex++)
                {
                    StageEditorObject platform = platforms[platformIndex];
                    if (!TryGetMountScore(platform, candidate, out float score) || score >= bestScore)
                    {
                        continue;
                    }

                    bestPlatform = platform;
                    bestScore = score;
                }

                if (bestPlatform != null)
                {
                    candidate.transform.SetParent(bestPlatform.transform, true);
                }
            }
        }

        private static bool CanMount(StageEditorObject candidate, Transform stageRoot)
        {
            if (candidate == null
                || candidate.transform == stageRoot
                || candidate.type == StageObjectType.MovingPlatform
                || candidate.type == StageObjectType.Elevator
                || candidate.type == StageObjectType.StageBoundary)
            {
                return false;
            }

            Rigidbody2D body = candidate.GetComponentInChildren<Rigidbody2D>(true);
            if (body != null)
            {
                // Boxes, balls, scales and other simulated objects must keep
                // their own physics body instead of becoming a child fixture.
                return false;
            }

            return candidate.transform.parent == stageRoot;
        }

        private static bool TryGetMountScore(
            StageEditorObject platform,
            StageEditorObject candidate,
            out float score)
        {
            score = float.PositiveInfinity;
            if (platform == null || candidate == null || platform == candidate)
            {
                return false;
            }

            Vector2 platformHalfSize = new Vector2(
                Mathf.Max(0.1f, platform.size.x * 0.5f),
                Mathf.Max(0.1f, platform.size.y * 0.5f));
            if (!TryGetLocalBounds(platform.transform, candidate, out Vector2 minimum, out Vector2 maximum))
            {
                return false;
            }

            Vector2 span = maximum - minimum;
            if (span.x > platformHalfSize.x * 2f * MaximumRelativeSpan
                || span.y > platformHalfSize.y * 2f * MaximumRelativeSpan + 1.5f)
            {
                return false;
            }

            Vector2 center = (minimum + maximum) * 0.5f;
            bool centerInside = Mathf.Abs(center.x) <= platformHalfSize.x + ContactTolerance
                && Mathf.Abs(center.y) <= platformHalfSize.y + ContactTolerance;
            bool centeredAcrossWidth = Mathf.Abs(center.x) <= platformHalfSize.x + ContactTolerance;
            bool centeredAcrossHeight = Mathf.Abs(center.y) <= platformHalfSize.y + ContactTolerance;
            float topGap = Mathf.Abs(minimum.y - platformHalfSize.y);
            float bottomGap = Mathf.Abs(maximum.y + platformHalfSize.y);
            float leftGap = Mathf.Abs(maximum.x + platformHalfSize.x);
            float rightGap = Mathf.Abs(minimum.x - platformHalfSize.x);
            bool touchesSurface = centeredAcrossWidth
                && (topGap <= ContactTolerance || bottomGap <= ContactTolerance)
                || centeredAcrossHeight
                && (leftGap <= ContactTolerance || rightGap <= ContactTolerance);
            if (!centerInside && !touchesSurface)
            {
                return false;
            }

            float surfaceGap = Mathf.Min(Mathf.Min(topGap, bottomGap), Mathf.Min(leftGap, rightGap));
            score = centerInside ? center.sqrMagnitude * 0.1f : surfaceGap + center.sqrMagnitude * 0.01f;
            return true;
        }

        private static bool TryGetLocalBounds(
            Transform platform,
            StageEditorObject candidate,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            // Use the root footprint. Functional child triggers can be much
            // larger than the visible object and must not affect mounting.
            Collider2D[] colliders = candidate.GetComponents<Collider2D>();
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                Include(platform, new Vector2(bounds.min.x, bounds.min.y), ref minimum, ref maximum);
                Include(platform, new Vector2(bounds.min.x, bounds.max.y), ref minimum, ref maximum);
                Include(platform, new Vector2(bounds.max.x, bounds.min.y), ref minimum, ref maximum);
                Include(platform, new Vector2(bounds.max.x, bounds.max.y), ref minimum, ref maximum);
                found = true;
            }

            if (found)
            {
                return true;
            }

            Vector2 halfSize = candidate.size * 0.5f;
            Include(platform, candidate.transform.TransformPoint(new Vector2(-halfSize.x, -halfSize.y)), ref minimum, ref maximum);
            Include(platform, candidate.transform.TransformPoint(new Vector2(-halfSize.x, halfSize.y)), ref minimum, ref maximum);
            Include(platform, candidate.transform.TransformPoint(new Vector2(halfSize.x, -halfSize.y)), ref minimum, ref maximum);
            Include(platform, candidate.transform.TransformPoint(new Vector2(halfSize.x, halfSize.y)), ref minimum, ref maximum);
            return true;
        }

        private static void Include(
            Transform platform,
            Vector2 worldPoint,
            ref Vector2 minimum,
            ref Vector2 maximum)
        {
            Vector2 local = platform.InverseTransformPoint(worldPoint);
            minimum = Vector2.Min(minimum, local);
            maximum = Vector2.Max(maximum, local);
        }
    }
}
