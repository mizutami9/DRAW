using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Makes a player non-interactive while their owner is on the DRAW screen and
    /// presents a visible status to the other players.
    /// </summary>
    public sealed class PlayerRedrawStateController : MonoBehaviour
    {
        private readonly Dictionary<Collider2D, bool> colliderStates = new Dictionary<Collider2D, bool>();
        private readonly Dictionary<SpriteRenderer, Color> spriteColors = new Dictionary<SpriteRenderer, Color>();
        private readonly Dictionary<LineRenderer, Color[]> lineColors = new Dictionary<LineRenderer, Color[]>();
        private PlayerController2D controller;
        private Rigidbody2D body;
        private bool bodyWasSimulated;
        private bool redrawing;
        private GameObject indicatorRoot;
        private TextMesh indicatorText;

        public bool IsRedrawing => redrawing;

        private void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            body = GetComponent<Rigidbody2D>();
            CreateIndicator();
        }

        private void LateUpdate()
        {
            if (!redrawing)
            {
                return;
            }

            // BodyBuilder can replace every line and collider while the DRAW screen
            // is open, so newly created components must also be made non-interactive.
            CaptureAndDisableColliders();
            CaptureAndFadeRenderers();
            UpdateIndicatorPosition();
            if (indicatorText != null)
            {
                indicatorText.text = LocalizationManager.T("player_redrawing_status");
            }
        }

        public void SetRedrawing(bool active)
        {
            if (redrawing == active)
            {
                if (active)
                {
                    CaptureAndDisableColliders();
                    CaptureAndFadeRenderers();
                }
                return;
            }

            redrawing = active;
            if (active)
            {
                controller?.GetComponent<PlayerCarryController>()?.ForceDrop();
                controller?.SetControlsEnabled(false);
                controller?.ResetMotion();
                if (body != null)
                {
                    bodyWasSimulated = body.simulated;
                    body.simulated = false;
                }
                CaptureAndDisableColliders();
                CaptureAndFadeRenderers();
                UpdateIndicatorPosition();
                if (indicatorRoot != null) indicatorRoot.SetActive(true);
            }
            else
            {
                RestoreColliders();
                RestoreRenderers();
                if (body != null) body.simulated = bodyWasSimulated;
                if (indicatorRoot != null) indicatorRoot.SetActive(false);
                Physics2D.SyncTransforms();
            }
        }

        private void CaptureAndDisableColliders()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null) continue;
                if (!colliderStates.ContainsKey(collider)) colliderStates[collider] = collider.enabled;
                collider.enabled = false;
            }
        }

        private void RestoreColliders()
        {
            foreach (KeyValuePair<Collider2D, bool> pair in colliderStates)
            {
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            }
            colliderStates.Clear();
        }

        private void CaptureAndFadeRenderers()
        {
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null || indicatorRoot != null && sprite.transform.IsChildOf(indicatorRoot.transform)) continue;
                if (!spriteColors.TryGetValue(sprite, out Color original))
                {
                    original = sprite.color;
                    spriteColors[sprite] = original;
                }
                Color faded = original;
                faded.a = Mathf.Min(original.a, 0.28f);
                sprite.color = faded;
            }

            LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                if (line == null || indicatorRoot != null && line.transform.IsChildOf(indicatorRoot.transform)) continue;
                if (!lineColors.TryGetValue(line, out Color[] original))
                {
                    original = new[] { line.startColor, line.endColor };
                    lineColors[line] = original;
                }
                Color start = original[0];
                Color end = original[1];
                start.a = Mathf.Min(start.a, 0.28f);
                end.a = Mathf.Min(end.a, 0.28f);
                line.startColor = start;
                line.endColor = end;
            }
        }

        private void RestoreRenderers()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> pair in spriteColors)
            {
                if (pair.Key != null) pair.Key.color = pair.Value;
            }
            foreach (KeyValuePair<LineRenderer, Color[]> pair in lineColors)
            {
                if (pair.Key == null) continue;
                pair.Key.startColor = pair.Value[0];
                pair.Key.endColor = pair.Value[1];
            }
            spriteColors.Clear();
            lineColors.Clear();
        }

        private void CreateIndicator()
        {
            indicatorRoot = new GameObject("Redrawing Status");
            indicatorRoot.transform.SetParent(transform, false);

            Sprite circle = StageSurvivalController.GetCircleSprite();
            GameObject outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(indicatorRoot.transform, false);
            outlineObject.transform.localScale = new Vector3(2.8f, 0.92f, 1f);
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sprite = circle;
            outline.color = new Color(0.12f, 0.1f, 0.08f, 0.92f);
            outline.sortingOrder = 485;

            GameObject paperObject = new GameObject("Paper");
            paperObject.transform.SetParent(indicatorRoot.transform, false);
            paperObject.transform.localScale = new Vector3(2.62f, 0.78f, 1f);
            SpriteRenderer paper = paperObject.AddComponent<SpriteRenderer>();
            paper.sprite = circle;
            paper.color = new Color(1f, 0.94f, 0.62f, 0.97f);
            paper.sortingOrder = 486;

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(indicatorRoot.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.02f, -0.05f);
            indicatorText = textObject.AddComponent<TextMesh>();
            indicatorText.anchor = TextAnchor.MiddleCenter;
            indicatorText.alignment = TextAlignment.Center;
            indicatorText.fontSize = 64;
            indicatorText.characterSize = 0.055f;
            indicatorText.fontStyle = FontStyle.Bold;
            indicatorText.color = new Color(0.16f, 0.12f, 0.08f, 1f);
            Font font = StageSurvivalController.FindHandwrittenFont();
            if (font != null)
            {
                indicatorText.font = font;
                textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            textObject.GetComponent<MeshRenderer>().sortingOrder = 487;
            indicatorRoot.SetActive(false);
        }

        private void UpdateIndicatorPosition()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            bool found = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled
                    || indicatorRoot != null && renderer.transform.IsChildOf(indicatorRoot.transform)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Vector3 worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.72f, transform.position.z - 0.2f);
            indicatorRoot.transform.position = worldPosition;
            Camera camera = Camera.main;
            float scale = camera != null && camera.orthographic
                ? Mathf.Clamp(camera.orthographicSize / 8f, 1f, 1.65f)
                : 1f;
            indicatorRoot.transform.localScale = Vector3.one * scale;
        }

        private void OnDisable()
        {
            if (redrawing) SetRedrawing(false);
        }
    }
}
