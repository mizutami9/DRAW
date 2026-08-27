using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    internal sealed class CollectiblePickupFeedback : MonoBehaviour
    {
        private const float Duration = 0.32f;
        private Transform followTarget;
        private Vector3 fallbackPosition;
        private Color effectColor;
        private float radius;
        private float elapsed;
        private LineRenderer[] diamonds;
        private LineRenderer[] rays;

        internal static void Play(Transform target, Vector3 position, Color color)
        {
            GameObject root = new GameObject("Collected Pencil Spark");
            CollectiblePickupFeedback effect = root.AddComponent<CollectiblePickupFeedback>();
            effect.Initialize(target, position, color);
        }

        private void Initialize(Transform target, Vector3 position, Color color)
        {
            followTarget = target;
            fallbackPosition = position;
            effectColor = color;
            radius = ResolveRadius(target);
            transform.position = target != null ? target.position + Vector3.up * 0.15f : position;

            diamonds = new[]
            {
                CreateLine("Inner Sharp Flash", 5, radius * 0.035f, 60),
                CreateLine("Outer Sharp Flash", 5, radius * 0.025f, 60)
            };
            rays = new LineRenderer[7];
            for (int i = 0; i < rays.Length; i++)
                rays[i] = CreateLine("Fast Pencil Slash " + (i + 1), 2, radius * (i % 2 == 0 ? 0.035f : 0.025f), 61);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float fade = 1f - Mathf.SmoothStep(0.25f, 1f, t);
            transform.position = followTarget != null ? followTarget.position + Vector3.up * 0.15f : fallbackPosition;

            Color bright = Color.Lerp(effectColor, Color.white, 0.48f);
            for (int ringIndex = 0; ringIndex < diamonds.Length; ringIndex++)
            {
                float delayed = Mathf.Clamp01((t - ringIndex * 0.09f) / (1f - ringIndex * 0.09f));
                float ringRadius = radius * Mathf.Lerp(0.12f + ringIndex * 0.08f, 1.15f + ringIndex * 0.22f, delayed);
                for (int i = 0; i < 5; i++)
                {
                    float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                    float wobble = 1f + Mathf.Sin(i * 2.7f + ringIndex) * 0.045f;
                    diamonds[ringIndex].SetPosition(i,
                        new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius * 0.68f, 0f) * wobble);
                }
                diamonds[ringIndex].transform.localRotation = Quaternion.Euler(0f, 0f,
                    (ringIndex == 0 ? 34f : -25f) * delayed);
                diamonds[ringIndex].startColor = diamonds[ringIndex].endColor =
                    WithAlpha(ringIndex == 0 ? bright : effectColor, (ringIndex == 0 ? 0.95f : 0.62f) * fade);
            }

            for (int i = 0; i < rays.Length; i++)
            {
                float angle = (18f + i / (float)rays.Length * 360f + i * 11f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float inner = radius * Mathf.Lerp(0.08f, 0.92f, t);
                float outer = inner + radius * Mathf.Lerp(i % 2 == 0 ? 0.72f : 0.48f, 0.08f, t);
                rays[i].SetPosition(0, direction * inner);
                rays[i].SetPosition(1, direction * outer);
                rays[i].startColor = rays[i].endColor = WithAlpha(i % 2 == 0 ? bright : effectColor, 0.95f * fade);
            }

            if (elapsed >= Duration) Destroy(gameObject);
        }

        private LineRenderer CreateLine(string objectName, int count, float width, int order)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(transform, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = count;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.sortingOrder = order;
            return line;
        }

        private static float ResolveRadius(Transform target)
        {
            if (target == null) return 0.9f;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return 0.9f;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f, 0.8f, 2.2f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class CollectibleAbsorbMover : MonoBehaviour
    {
        private Transform target;
        private Vector3 start;
        private Vector3 startScale;
        private Quaternion startRotation;
        private Color color;
        private float elapsed;

        internal static void Begin(GameObject item, Transform collector, Color effectColor)
        {
            if (item == null) return;
            CollectibleAbsorbMover mover = item.AddComponent<CollectibleAbsorbMover>();
            mover.Initialize(collector, effectColor);
        }

        private void Initialize(Transform collector, Color effectColor)
        {
            target = collector != null ? collector : FindNearestPlayer(transform.position);
            start = transform.position;
            startScale = transform.localScale;
            startRotation = transform.rotation;
            color = effectColor;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;

            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.035f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0.005f;
            trail.numCapVertices = 4;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            Color trailColor = Color.Lerp(color, Color.white, 0.38f);
            trail.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0.72f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = 218;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.28f);
            float eased = t * t * (3f - 2f * t);
            Vector3 end = target != null ? target.position + Vector3.up * 0.35f : start + Vector3.up * 0.7f;
            float arc = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(Vector3.Distance(start, end) * 0.08f + 0.45f, 0.45f, 1.15f);
            transform.position = Vector3.Lerp(start, end, eased) + Vector3.up * arc;
            float pop = 1f + Mathf.Sin(Mathf.Min(1f, t / 0.35f) * Mathf.PI) * 0.3f;
            float vanish = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, t));
            transform.localScale = startScale * (pop * vanish);
            transform.rotation = startRotation * Quaternion.Euler(0f, 0f, 260f * t);
            if (t < 1f) return;

            CollectiblePickupFeedback.Play(target, transform.position, color);
            Destroy(gameObject);
        }

        private static Transform FindNearestPlayer(Vector3 position)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                float distance = ((Vector2)(players[i].transform.position - position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = players[i].transform;
            }
            return nearest;
        }
    }
}
