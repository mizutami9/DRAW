using System.Collections;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed partial class StageManager
    {
        private static void ClearTransientStageVisuals()
        {
            StageTransientObject.ClearAll();

            // Compatibility sweep for effects created by older stage code. New
            // runtime-only effects should call StageTransientObject.Register.
            BombExplosionVisual[] explosions = Object.FindObjectsByType<BombExplosionVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < explosions.Length; i++)
                if (explosions[i] != null) HideAndDestroyTransient(explosions[i].gameObject);
            StageBossImpactFlash[] impacts = Object.FindObjectsByType<StageBossImpactFlash>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < impacts.Length; i++)
                if (impacts[i] != null) HideAndDestroyTransient(impacts[i].gameObject);
            StageBossBeam[] beams = Object.FindObjectsByType<StageBossBeam>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < beams.Length; i++)
                if (beams[i] != null) HideAndDestroyTransient(beams[i].gameObject);

            SpriteRenderer[] sprites = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null || sprite.gameObject.name != "Explosion White Flash") continue;
                sprite.enabled = false;
                GameObject root = sprite.transform.parent != null
                    ? sprite.transform.parent.gameObject
                    : sprite.gameObject;
                HideAndDestroyTransient(root);
            }
        }

        private static IEnumerator ClearTransientStageVisualsAfterTransition()
        {
            // Network callbacks and Destroy() complete at frame boundaries. Sweep
            // after both boundaries so a final packet cannot revive an old effect.
            yield return null;
            ClearTransientStageVisuals();
            yield return null;
            ClearTransientStageVisuals();
        }

        private static void HideAndDestroyTransient(GameObject root)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = false;
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = false;
            root.SetActive(false);
            Object.Destroy(root);
        }
    }
}
