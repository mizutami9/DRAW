using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Draws miniature copies of the participating characters inside the clear
    /// popup. It never moves or enables the gameplay-side player objects.
    /// </summary>
    public sealed class ClearScreenCharacterParade : MonoBehaviour
    {
        private const float WalkSpeed = 0.12f;
        private const float ArriveDistance = 0.012f;

        private static readonly Vector2[][] Routes =
        {
            new[]
            {
                new Vector2(0.08f, 0.17f), new Vector2(0.34f, 0.18f),
                new Vector2(0.66f, 0.17f), new Vector2(0.92f, 0.18f)
            },
            new[]
            {
                new Vector2(0.89f, 0.54f), new Vector2(0.67f, 0.55f),
                new Vector2(0.35f, 0.54f), new Vector2(0.11f, 0.55f)
            },
            new[]
            {
                new Vector2(0.10f, 0.25f), new Vector2(0.11f, 0.45f),
                new Vector2(0.10f, 0.67f), new Vector2(0.12f, 0.82f)
            },
            new[]
            {
                new Vector2(0.90f, 0.80f), new Vector2(0.89f, 0.65f),
                new Vector2(0.90f, 0.43f), new Vector2(0.88f, 0.24f)
            }
        };

        private sealed class Walker
        {
            public RectTransform Rect;
            public ClearCharacterGraphic Graphic;
            public Vector2 Position;
            public int RouteIndex;
            public int WaypointIndex = 1;
            public int RouteDirection = 1;
            public int Facing = 1;
            public float PauseRemaining;
            public float LookBackAt;
            public bool LookedBack;
            public float SpeedScale;
            public float WalkPhase;
            public float BobPhase;
        }

        private readonly List<PlayerController2D> players = new List<PlayerController2D>(4);
        private readonly List<Walker> walkers = new List<Walker>(4);
        private RectTransform popup;
        private RectTransform layer;
        private bool active;

        public void Begin(StageManager stageManager, RectTransform popupRoot)
        {
            End();
            if (stageManager == null || popupRoot == null)
            {
                return;
            }

            popup = popupRoot;
            GameObject layerObject = new GameObject(
                "ClearCharacterParadeLayer",
                typeof(RectTransform),
                typeof(RectMask2D));
            layerObject.transform.SetParent(popup, false);
            layer = layerObject.GetComponent<RectTransform>();
            Stretch(layer);
            layer.SetAsFirstSibling();

            stageManager.GetClearCelebrationPlayers(players);
            int visibleCount = Mathf.Min(4, players.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                PlayerController2D player = players[i];
                BodyBuilder builder = player != null ? player.GetComponent<BodyBuilder>() : null;
                if (builder == null || builder.CelebrationVisualRoot == null)
                {
                    continue;
                }

                GameObject characterObject = new GameObject(
                    "ClearCharacter" + (i + 1),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(ClearCharacterGraphic));
                characterObject.transform.SetParent(layer, false);
                RectTransform characterRect = characterObject.GetComponent<RectTransform>();
                characterRect.anchorMin = characterRect.anchorMax = new Vector2(0.5f, 0.5f);
                characterRect.pivot = new Vector2(0.5f, 0.5f);
                characterRect.sizeDelta = new Vector2(150f, 150f);

                ClearCharacterGraphic graphic = characterObject.GetComponent<ClearCharacterGraphic>();
                graphic.raycastTarget = false;
                if (!graphic.Capture(builder))
                {
                    Destroy(characterObject);
                    continue;
                }

                walkers.Add(new Walker
                {
                    Rect = characterRect,
                    Graphic = graphic,
                    Position = Routes[i % Routes.Length][0],
                    RouteIndex = i % Routes.Length,
                    Facing = i % 2 == 0 ? 1 : -1,
                    PauseRemaining = 0.3f + i * 0.18f,
                    LookBackAt = 0.16f,
                    SpeedScale = 0.88f + i * 0.08f,
                    BobPhase = i * 1.73f
                });
            }

            active = walkers.Count > 0;
            PlaceWalkers();
        }

        public void End()
        {
            active = false;
            walkers.Clear();
            players.Clear();
            popup = null;
            if (layer != null)
            {
                Destroy(layer.gameObject);
                layer = null;
            }
        }

        private void OnDisable()
        {
            End();
        }

        private void LateUpdate()
        {
            if (!active || popup == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < walkers.Count; i++)
            {
                UpdateWalker(walkers[i], deltaTime, i);
            }
            PlaceWalkers();
        }

        private static void UpdateWalker(Walker walker, float deltaTime, int walkerIndex)
        {
            bool walking = walker.PauseRemaining <= 0f;
            if (!walking)
            {
                walker.PauseRemaining -= deltaTime;
                if (!walker.LookedBack && walker.PauseRemaining <= walker.LookBackAt)
                {
                    walker.Facing *= -1;
                    walker.LookedBack = true;
                }
                walker.Graphic.SetMotion(false, walker.WalkPhase, walker.Facing);
                return;
            }

            Vector2[] route = Routes[walker.RouteIndex];
            Vector2 target = route[walker.WaypointIndex];
            Vector2 previous = walker.Position;
            walker.Position = Vector2.MoveTowards(
                previous,
                target,
                WalkSpeed * walker.SpeedScale * deltaTime);
            Vector2 movement = walker.Position - previous;
            if (Mathf.Abs(movement.x) > 0.00001f)
            {
                walker.Facing = movement.x < 0f ? -1 : 1;
            }

            walker.WalkPhase += deltaTime * (7.5f + walkerIndex * 0.45f);
            walker.Graphic.SetMotion(true, walker.WalkPhase, walker.Facing);
            if (Vector2.Distance(walker.Position, target) > ArriveDistance)
            {
                return;
            }

            if (walker.WaypointIndex == route.Length - 1)
            {
                walker.RouteDirection = -1;
            }
            else if (walker.WaypointIndex == 0)
            {
                walker.RouteDirection = 1;
            }
            walker.WaypointIndex += walker.RouteDirection;

            float rhythm = Mathf.Abs(Mathf.Sin(
                (walker.WaypointIndex + 1) * 1.91f + walkerIndex * 0.83f));
            walker.PauseRemaining = Mathf.Lerp(0.55f, 1.65f, rhythm);
            walker.LookBackAt = walker.PauseRemaining * (0.32f + walkerIndex * 0.06f);
            walker.LookedBack = false;
            walker.Graphic.SetMotion(false, walker.WalkPhase, walker.Facing);
        }

        private void PlaceWalkers()
        {
            if (popup == null)
            {
                return;
            }

            float width = popup.rect.width;
            float height = popup.rect.height;
            float characterSize = Mathf.Clamp(Mathf.Min(width * 0.16f, height * 0.29f), 92f, 165f);
            for (int i = 0; i < walkers.Count; i++)
            {
                Walker walker = walkers[i];
                if (walker.Rect == null)
                {
                    continue;
                }

                walker.Rect.sizeDelta = Vector2.one * characterSize;
                float bob = walker.PauseRemaining <= 0f
                    ? Mathf.Sin(walker.WalkPhase * 2f + walker.BobPhase) * 3f
                    : Mathf.Sin(Time.unscaledTime * 1.8f + walker.BobPhase) * 0.8f;
                walker.Rect.anchoredPosition = new Vector2(
                    (walker.Position.x - 0.5f) * width,
                    (walker.Position.y - 0.5f) * height + bob);
                float lean = walker.PauseRemaining <= 0f
                    ? Mathf.Sin(walker.WalkPhase) * 1.4f
                    : 0f;
                walker.Rect.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>UI mesh snapshot of a BodyBuilder's visible pencil lines.</summary>
    public sealed class ClearCharacterGraphic : MaskableGraphic
    {
        private struct Stroke
        {
            public Vector2 Start;
            public Vector2 End;
            public Vector2 Pivot;
            public float Width;
            public int MotionSign;
        }

        private sealed class PendingStroke
        {
            public Vector2 Start;
            public Vector2 End;
            public float Width;
            public string Part;
        }

        private readonly List<Stroke> strokes = new List<Stroke>();
        private Vector2 drawingCenter;
        private Vector2 drawingSize = Vector2.one;
        private bool walking;
        private float walkPhase;
        private int facing = 1;

        public bool Capture(BodyBuilder builder)
        {
            strokes.Clear();
            Transform root = builder != null ? builder.CelebrationVisualRoot : null;
            if (root == null)
            {
                return false;
            }

            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            List<PendingStroke> pending = new List<PendingStroke>();
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                if (line == null || !line.enabled || line.positionCount < 2)
                {
                    continue;
                }

                string part = line.gameObject.name.Replace("Segment", string.Empty);
                for (int point = 1; point < line.positionCount; point++)
                {
                    Vector2 start = root.InverseTransformPoint(
                        line.transform.TransformPoint(line.GetPosition(point - 1)));
                    Vector2 end = root.InverseTransformPoint(
                        line.transform.TransformPoint(line.GetPosition(point)));
                    pending.Add(new PendingStroke
                    {
                        Start = start,
                        End = end,
                        Width = Mathf.Max(line.startWidth, line.endWidth),
                        Part = part
                    });
                    if (!hasBounds)
                    {
                        bounds = new Bounds(start, Vector3.zero);
                        hasBounds = true;
                    }
                    bounds.Encapsulate(start);
                    bounds.Encapsulate(end);
                }
            }

            if (!hasBounds || pending.Count == 0)
            {
                return false;
            }

            drawingCenter = bounds.center;
            drawingSize = new Vector2(
                Mathf.Max(0.01f, bounds.size.x),
                Mathf.Max(0.01f, bounds.size.y));

            Dictionary<string, Vector2> pivots = FindPartPivots(pending, drawingCenter);
            for (int i = 0; i < pending.Count; i++)
            {
                PendingStroke source = pending[i];
                strokes.Add(new Stroke
                {
                    Start = source.Start,
                    End = source.End,
                    Pivot = pivots[source.Part],
                    Width = source.Width,
                    MotionSign = GetMotionSign(source.Part)
                });
            }

            color = builder.PlayerColor;
            SetVerticesDirty();
            return true;
        }

        public void SetMotion(bool isWalking, float phase, int direction)
        {
            walking = isWalking;
            walkPhase = phase;
            facing = direction < 0 ? -1 : 1;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (strokes.Count == 0)
            {
                return;
            }

            Rect area = rectTransform.rect;
            float scale = Mathf.Min(
                area.width * 0.78f / drawingSize.x,
                area.height * 0.82f / drawingSize.y);
            for (int i = 0; i < strokes.Count; i++)
            {
                Stroke stroke = strokes[i];
                Vector2 start = AnimatePoint(stroke.Start, stroke.Pivot, stroke.MotionSign);
                Vector2 end = AnimatePoint(stroke.End, stroke.Pivot, stroke.MotionSign);
                start = (start - drawingCenter) * scale;
                end = (end - drawingCenter) * scale;
                start.x *= facing;
                end.x *= facing;
                AddLine(vertexHelper, start, end, Mathf.Clamp(stroke.Width * scale, 2.2f, 5.5f));
            }
        }

        private Vector2 AnimatePoint(Vector2 point, Vector2 pivot, int motionSign)
        {
            if (!walking || motionSign == 0)
            {
                return point;
            }

            float angle = Mathf.Sin(walkPhase) * motionSign * 10f * Mathf.Deg2Rad;
            Vector2 offset = point - pivot;
            float sine = Mathf.Sin(angle);
            float cosine = Mathf.Cos(angle);
            return pivot + new Vector2(
                offset.x * cosine - offset.y * sine,
                offset.x * sine + offset.y * cosine);
        }

        private void AddLine(VertexHelper helper, Vector2 start, Vector2 end, float width)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
            int index = helper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = start - normal;
            helper.AddVert(vertex);
            vertex.position = start + normal;
            helper.AddVert(vertex);
            vertex.position = end + normal;
            helper.AddVert(vertex);
            vertex.position = end - normal;
            helper.AddVert(vertex);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }

        private static Dictionary<string, Vector2> FindPartPivots(
            List<PendingStroke> pending,
            Vector2 bodyCenter)
        {
            Dictionary<string, Vector2> pivots = new Dictionary<string, Vector2>();
            Dictionary<string, float> distances = new Dictionary<string, float>();
            for (int i = 0; i < pending.Count; i++)
            {
                PendingStroke stroke = pending[i];
                ConsiderPivot(stroke.Part, stroke.Start, bodyCenter, pivots, distances);
                ConsiderPivot(stroke.Part, stroke.End, bodyCenter, pivots, distances);
            }
            return pivots;
        }

        private static void ConsiderPivot(
            string part,
            Vector2 point,
            Vector2 bodyCenter,
            Dictionary<string, Vector2> pivots,
            Dictionary<string, float> distances)
        {
            float distance = (point - bodyCenter).sqrMagnitude;
            if (!distances.TryGetValue(part, out float best) || distance < best)
            {
                distances[part] = distance;
                pivots[part] = point;
            }
        }

        private static int GetMotionSign(string part)
        {
            bool limb = part.Contains("Arm")
                || part.Contains("Leg")
                || part.Contains("Wing")
                || part.Contains("Tail");
            if (!limb)
            {
                return 0;
            }

            int side = part.Contains("Left") ? 1 : part.Contains("Right") ? -1 : 1;
            if (part.Contains("Arm") || part.Contains("Wing"))
            {
                side *= -1;
            }
            return side;
        }
    }
}
