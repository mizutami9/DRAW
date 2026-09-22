using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Applies the shared nine-sliced hand-cut paper surface to UI cards and buttons.
    /// Full-screen backgrounds, icons, gauges, sliders, and decorative art should not use it.
    /// </summary>
    internal static class DoodlePaperUi
    {
        internal static void Apply(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            Apply(rect.GetComponent<Image>(), color);
        }

        internal static void Apply(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = DoodleRuntimeAssets.PaperCardSprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = color;

            // The paper sprite already includes its cut edge, faint pencil trace and a
            // diffuse 1-2 mm lift. Unity's mesh Outline/Shadow repeats the silhouette as a
            // hard digital rectangle, so suppress those legacy effects centrally.
            DisableLegacyEffects(image.gameObject);
            if (Application.isPlaying)
            {
                DoodlePaperEffectGuard guard = image.GetComponent<DoodlePaperEffectGuard>();
                if (guard == null)
                {
                    guard = image.gameObject.AddComponent<DoodlePaperEffectGuard>();
                }
                guard.Arm();
            }
        }

        internal static bool IsApplied(Image image)
        {
            return image != null && image.sprite == DoodleRuntimeAssets.PaperCardSprite;
        }

        internal static void DisableLegacyEffects(GameObject target)
        {
            if (target == null) return;
            Shadow[] effects = target.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null) effects[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// Some older screen polishers add a hard Shadow immediately after applying the common
    /// paper style. Re-check occasionally so those late additions cannot bring the black
    /// drop-shadow back. The shadow baked into the shared sprite remains visible.
    /// </summary>
    internal sealed class DoodlePaperEffectGuard : MonoBehaviour
    {
        private readonly List<Shadow> effects = new List<Shadow>(2);
        private int refreshThroughFrame;

        private void OnEnable()
        {
            Arm();
        }

        internal void Arm()
        {
            // A few legacy builders add their Shadow immediately after Apply(). Refresh
            // through the next frame so those late components are cached before drawing.
            refreshThroughFrame = Mathf.Max(refreshThroughFrame, Time.frameCount + 1);
        }

        private void LateUpdate()
        {
            if (Time.frameCount <= refreshThroughFrame)
            {
                effects.Clear();
                GetComponents<Shadow>(effects);
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null && effects[i].enabled)
                {
                    effects[i].enabled = false;
                }
            }
        }
    }
}
