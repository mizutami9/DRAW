using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class DrawingPresetPreviewGraphic : MaskableGraphic
    {
        private enum Edge { Left, Right, Top, Bottom }

        private DrawManager.DrawingState state;
        private DrawManager.Species species;

        public void SetDrawing(DrawManager.DrawingState value, DrawManager.Species valueSpecies)
        {
            state = value;
            species = valueSpecies;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (state == null
                || !state.Points.TryGetValue(
                    species,
                    out Dictionary<DrawManager.BodyPart, List<Vector2>> parts))
            {
                return;
            }

            Dictionary<DrawManager.BodyPart, Vector2> offsets = BuildPartOffsets(parts);
            bool found = false;
            Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> part in parts)
            {
                Vector2 offset = offsets.TryGetValue(part.Key, out Vector2 value) ? value : Vector2.zero;
                IReadOnlyList<Vector2> points = part.Value;
                if (points == null) continue;
                for (int i = 0; i < points.Count; i++)
                {
                    if (DrawManager.IsBreakPoint(points[i])) continue;
                    Vector2 point = points[i] + offset;
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                    found = true;
                }
            }
            if (!found) return;

            Rect area = rectTransform.rect;
            const float padding = 3f;
            Vector2 content = maximum - minimum;
            float scale = Mathf.Min(
                Mathf.Max(1f, area.width - padding * 2f) / Mathf.Max(1f, content.x),
                Mathf.Max(1f, area.height - padding * 2f) / Mathf.Max(1f, content.y));
            Vector2 sourceCenter = (minimum + maximum) * 0.5f;
            Vector2 targetCenter = area.center;
            float width = Mathf.Clamp(Mathf.Min(area.width, area.height) * 0.035f, 1.25f, 2.4f);

            foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> part in parts)
            {
                Vector2 offset = offsets.TryGetValue(part.Key, out Vector2 value) ? value : Vector2.zero;
                IReadOnlyList<Vector2> points = part.Value;
                if (points == null) continue;
                for (int i = 1; i < points.Count; i++)
                {
                    if (DrawManager.IsBreakPoint(points[i - 1])
                        || DrawManager.IsBreakPoint(points[i])) continue;
                    Vector2 start = targetCenter + (points[i - 1] + offset - sourceCenter) * scale;
                    Vector2 end = targetCenter + (points[i] + offset - sourceCenter) * scale;
                    AddSegment(helper, start, end, width, color);
                }
            }
        }

        private Dictionary<DrawManager.BodyPart, Vector2> BuildPartOffsets(
            Dictionary<DrawManager.BodyPart, List<Vector2>> parts)
        {
            Dictionary<DrawManager.BodyPart, Vector2> result = new Dictionary<DrawManager.BodyPart, Vector2>();
            foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> part in parts)
            {
                Vector2 offset = Vector2.zero;
                if (species != DrawManager.Species.Slime
                    && part.Key != DrawManager.BodyPart.Torso
                    && TryGetPartConnection(part.Key, part.Value, out Vector2 source)
                    && TryGetTorsoConnection(parts, part.Key, out Vector2 target))
                {
                    offset = target - source;
                }
                result[part.Key] = offset;
            }
            return result;
        }

        private bool TryGetTorsoConnection(
            Dictionary<DrawManager.BodyPart, List<Vector2>> parts,
            DrawManager.BodyPart part,
            out Vector2 point)
        {
            point = Vector2.zero;
            if (!parts.TryGetValue(DrawManager.BodyPart.Torso, out List<Vector2> torsoPoints)
                || !TryGetBounds(torsoPoints, out Rect torso)) return false;
            float centerX = torso.center.x;
            float centerY = torso.center.y;
            float leftLegX = Mathf.Lerp(torso.xMin, torso.xMax, 0.25f);
            float rightLegX = Mathf.Lerp(torso.xMin, torso.xMax, 0.75f);
            if (species == DrawManager.Species.Cat)
            {
                float frontX = Mathf.Lerp(torso.xMin, torso.xMax, 0.72f);
                float backX = Mathf.Lerp(torso.xMin, torso.xMax, 0.28f);
                switch (part)
                {
                    case DrawManager.BodyPart.Head: point = new Vector2(torso.xMax, centerY); return true;
                    case DrawManager.BodyPart.Tail: point = new Vector2(torso.xMin, centerY); return true;
                    case DrawManager.BodyPart.LeftFrontLeg: point = new Vector2(frontX - 14f, torso.yMin); return true;
                    case DrawManager.BodyPart.RightFrontLeg: point = new Vector2(frontX + 14f, torso.yMin); return true;
                    case DrawManager.BodyPart.LeftBackLeg: point = new Vector2(backX - 14f, torso.yMin); return true;
                    case DrawManager.BodyPart.RightBackLeg: point = new Vector2(backX + 14f, torso.yMin); return true;
                }
            }
            if (species == DrawManager.Species.Turtle && part == DrawManager.BodyPart.Head)
            {
                point = new Vector2(torso.xMax, centerY);
                return true;
            }
            switch (part)
            {
                case DrawManager.BodyPart.Head: point = new Vector2(centerX, torso.yMax); return true;
                case DrawManager.BodyPart.LeftArm:
                case DrawManager.BodyPart.LeftFrontLeg:
                case DrawManager.BodyPart.LeftWing: point = new Vector2(torso.xMin, centerY); return true;
                case DrawManager.BodyPart.RightArm:
                case DrawManager.BodyPart.RightFrontLeg:
                case DrawManager.BodyPart.RightWing: point = new Vector2(torso.xMax, centerY); return true;
                case DrawManager.BodyPart.LeftLeg:
                case DrawManager.BodyPart.LeftBackLeg: point = new Vector2(leftLegX, torso.yMin); return true;
                case DrawManager.BodyPart.RightLeg:
                case DrawManager.BodyPart.RightBackLeg: point = new Vector2(rightLegX, torso.yMin); return true;
                case DrawManager.BodyPart.Tail:
                case DrawManager.BodyPart.TailFeather: point = new Vector2(centerX, torso.yMin); return true;
                default: return false;
            }
        }

        private bool TryGetPartConnection(
            DrawManager.BodyPart part,
            IReadOnlyList<Vector2> points,
            out Vector2 point)
        {
            if (species == DrawManager.Species.Cat)
            {
                switch (part)
                {
                    case DrawManager.BodyPart.Head: return TryGetEdgeCenter(points, Edge.Left, out point);
                    case DrawManager.BodyPart.Tail: return TryGetEdgeCenter(points, Edge.Right, out point);
                    case DrawManager.BodyPart.LeftFrontLeg:
                    case DrawManager.BodyPart.RightFrontLeg:
                    case DrawManager.BodyPart.LeftBackLeg:
                    case DrawManager.BodyPart.RightBackLeg: return TryGetEdgeCenter(points, Edge.Top, out point);
                }
            }
            if (species == DrawManager.Species.Turtle && part == DrawManager.BodyPart.Head)
                return TryGetEdgeCenter(points, Edge.Left, out point);
            switch (part)
            {
                case DrawManager.BodyPart.Head: return TryGetEdgeCenter(points, Edge.Bottom, out point);
                case DrawManager.BodyPart.LeftArm:
                case DrawManager.BodyPart.LeftWing: return TryGetEdgeCenter(points, Edge.Right, out point);
                case DrawManager.BodyPart.RightArm:
                case DrawManager.BodyPart.RightWing: return TryGetEdgeCenter(points, Edge.Left, out point);
                default: return TryGetEdgeCenter(points, Edge.Top, out point);
            }
        }

        private static bool TryGetEdgeCenter(IReadOnlyList<Vector2> points, Edge edge, out Vector2 point)
        {
            point = Vector2.zero;
            if (!TryGetBounds(points, out Rect bounds)) return false;
            point = edge switch
            {
                Edge.Left => new Vector2(bounds.xMin, bounds.center.y),
                Edge.Right => new Vector2(bounds.xMax, bounds.center.y),
                Edge.Top => new Vector2(bounds.center.x, bounds.yMax),
                _ => new Vector2(bounds.center.x, bounds.yMin)
            };
            return true;
        }

        private static bool TryGetBounds(IReadOnlyList<Vector2> points, out Rect bounds)
        {
            bool found = false;
            Vector2 minimum = Vector2.zero;
            Vector2 maximum = Vector2.zero;
            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (DrawManager.IsBreakPoint(points[i])) continue;
                    if (!found) { minimum = maximum = points[i]; found = true; }
                    else { minimum = Vector2.Min(minimum, points[i]); maximum = Vector2.Max(maximum, points[i]); }
                }
            }
            bounds = found
                ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y)
                : new Rect();
            return found;
        }

        private static void AddSegment(
            VertexHelper helper, Vector2 start, Vector2 end, float width, Color32 tint)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
            int index = helper.currentVertCount;
            helper.AddVert(start - normal, tint, Vector2.zero);
            helper.AddVert(start + normal, tint, Vector2.zero);
            helper.AddVert(end + normal, tint, Vector2.zero);
            helper.AddVert(end - normal, tint, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
