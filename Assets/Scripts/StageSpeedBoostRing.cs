using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSpeedBoostRing : MonoBehaviour
    {
        private readonly HashSet<PlayerController2D> playersInside = new HashSet<PlayerController2D>();
        private float multiplier;
        private float duration;
        private Transform visual;
        private Vector3 visualBaseScale = Vector3.one;
        private Color ringColor;

        public static GameObject CreateObject(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId) { name = data.type.ToString() };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(Mathf.Max(0.5f, data.size.x), Mathf.Max(0.5f, data.size.y), 1f);

            float multiplier = data.actionStrength > 1f
                ? data.actionStrength
                : data.type == StageObjectType.SpeedRing3X ? 3f : 2f;
            float duration = data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 1.5f;
            Color color = multiplier >= 2.5f
                ? new Color(0.08f, 0.48f, 0.96f, 1f)
                : new Color(0.05f, 0.68f, 1f, 1f);

            GameObject ringGroup = new GameObject("Floating Speed Chevrons Visual");
            ringGroup.transform.SetParent(root.transform, false);
            Color dark = Color.Lerp(color, Color.black, 0.42f);
            Color light = Color.Lerp(color, Color.white, 0.48f);
            AddChevron(ringGroup.transform, "Speed Chevron Left Shadow", -0.28f, new Vector2(0.035f, -0.035f), dark, 0.115f, 23);
            AddChevron(ringGroup.transform, "Speed Chevron Right Shadow", 0.25f, new Vector2(0.035f, -0.035f), dark, 0.115f, 23);
            AddChevron(ringGroup.transform, "Speed Chevron Left", -0.28f, Vector2.zero, color, 0.09f, 25);
            AddChevron(ringGroup.transform, "Speed Chevron Right", 0.25f, Vector2.zero, color, 0.09f, 25);
            // A second loose pass keeps the mark visibly hand-drawn.
            AddChevron(ringGroup.transform, "Speed Chevron Left Pencil Pass", -0.27f, new Vector2(-0.015f, 0.018f), light, 0.035f, 26);
            AddChevron(ringGroup.transform, "Speed Chevron Right Pencil Pass", 0.26f, new Vector2(-0.015f, 0.018f), light, 0.035f, 26);

            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.46f;
            trigger.isTrigger = true;

            StageSpeedBoostRing ring = root.AddComponent<StageSpeedBoostRing>();
            ring.multiplier = Mathf.Clamp(multiplier, 1f, 3f);
            ring.duration = Mathf.Clamp(duration, 0.1f, 10f);
            ring.visual = ringGroup.transform;
            ring.ringColor = color;

            StageEditorObject marker = root.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
            marker.actionStrength = data.actionStrength;
            marker.movementAngle = data.movementAngle;
            marker.movementSpeed = data.movementSpeed;
            marker.spawnPattern = data.spawnPattern;
            marker.spawnBoxSize = data.spawnBoxSize;
            marker.bombFuseSeconds = data.bombFuseSeconds;
            marker.linkTargetId = data.linkTargetId;
            marker.linkAction = data.linkAction;
            return root;
        }

        private static void AddChevron(
            Transform parent,
            string name,
            float centerX,
            Vector2 offset,
            Color color,
            float width,
            int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.numCapVertices = 6;
            line.numCornerVertices = 5;
            line.startWidth = width;
            line.endWidth = width * 0.88f;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = Color.Lerp(color, Color.white, 0.18f);
            line.sortingOrder = order;
            line.SetPosition(0, new Vector3(centerX - 0.22f, 0.36f, 0f));
            line.SetPosition(1, new Vector3(centerX + 0.18f, -0.015f, 0f));
            line.SetPosition(2, new Vector3(centerX - 0.2f, -0.38f, 0f));
        }

        private void Update()
        {
            if (visual == null) return;
            float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * 0.035f;
            visual.localScale = new Vector3(
                visualBaseScale.x * pulse,
                visualBaseScale.y * pulse,
                1f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null || !playersInside.Add(player)) return;
            player.ApplySpeedBoost(multiplier, duration);
            GameSfx.PlayAt(SfxId.SpeedBoost, transform.position, multiplier >= 2.5f ? 1.22f : 1.08f);
            StartCoroutine(Flash());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null) playersInside.Remove(player);
        }

        private System.Collections.IEnumerator Flash()
        {
            if (visual == null) yield break;
            LineRenderer[] lines = visual.GetComponentsInChildren<LineRenderer>();
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].startColor = Color.white;
                lines[i].endColor = Color.white;
            }
            yield return new WaitForSeconds(0.12f);
            if (visual == null) yield break;
            lines = visual.GetComponentsInChildren<LineRenderer>();
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].startColor = ringColor;
                lines[i].endColor = Color.Lerp(ringColor, Color.white, 0.18f);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerSpeedBoostEffect : MonoBehaviour
    {
        private const int StreakCount = 7;
        private readonly LineRenderer[] streaks = new LineRenderer[StreakCount];
        private Rigidbody2D body;
        private PlayerController2D player;
        private Material material;
        private float remaining;
        private float multiplier = 1f;

        public void Activate(float value, float duration)
        {
            multiplier = Mathf.Max(multiplier, value);
            remaining = Mathf.Max(remaining, duration);
            EnsureVisuals();
            SetVisible(true);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController2D>();
        }

        private void LateUpdate()
        {
            if (remaining <= 0f)
            {
                multiplier = 1f;
                SetVisible(false);
                return;
            }
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            EnsureVisuals();

            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
            Vector2 direction = velocity.sqrMagnitude > 1f
                ? velocity.normalized
                : new Vector2(player != null ? player.FacingDirection : 1, 0f);
            Vector2 behind = -direction;
            float length = Mathf.Lerp(1.1f, 2.8f, Mathf.InverseLerp(1f, 3f, multiplier));
            float flicker = 0.85f + Mathf.Sin(Time.unscaledTime * 24f) * 0.15f;
            Color core = multiplier >= 2.5f
                ? new Color(1f, 0.3f, 0.08f, 0.78f)
                : new Color(0.05f, 0.72f, 1f, 0.72f);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 center = transform.position;
            for (int i = 0; i < streaks.Length; i++)
            {
                LineRenderer line = streaks[i];
                if (line == null) continue;
                float lane = (i - (StreakCount - 1) * 0.5f) * 0.28f;
                float wave = Mathf.Sin(Time.unscaledTime * 18f + i * 1.7f) * 0.11f;
                Vector2 start = center + perpendicular * (lane + wave) + behind * 0.25f;
                float laneLength = length * (0.62f + (i % 3) * 0.18f) * flicker;
                line.SetPosition(0, start);
                line.SetPosition(1, start + behind * laneLength);
                line.startColor = core;
                line.endColor = new Color(core.r, core.g, core.b, 0f);
            }
        }

        private void EnsureVisuals()
        {
            if (streaks[0] != null) return;
            material = DoodleRuntimeAssets.LineMaterial;
            for (int i = 0; i < streaks.Length; i++)
            {
                GameObject obj = new GameObject("Boost Speed Line " + (i + 1));
                obj.transform.SetParent(transform, false);
                LineRenderer line = obj.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 5;
                line.startWidth = 0.075f + (i % 2) * 0.025f;
                line.endWidth = 0.015f;
                line.sharedMaterial = material;
                line.sortingOrder = 218 + i;
                streaks[i] = line;
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < streaks.Length; i++) if (streaks[i] != null) streaks[i].enabled = visible;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
