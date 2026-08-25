using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageDropperVisualAnimator : MonoBehaviour
    {
        private const float AnimationDuration = 0.38f;

        private Transform artwork;
        private Transform puffRoot;
        private LineRenderer[] puffLines;
        private Vector3 artworkBaseScale;
        private Vector3 artworkBasePosition;
        private Vector3 puffBasePosition;
        private float puffTravel;
        private float elapsed = AnimationDuration;

        public void Configure(Transform targetArtwork, Transform targetPuffRoot, float travelDistance)
        {
            artwork = targetArtwork;
            puffRoot = targetPuffRoot;
            puffTravel = Mathf.Max(0.06f, travelDistance);
            if (artwork != null)
            {
                artworkBaseScale = artwork.localScale;
                artworkBasePosition = artwork.localPosition;
            }
            if (puffRoot != null)
            {
                puffBasePosition = puffRoot.localPosition;
                puffLines = puffRoot.GetComponentsInChildren<LineRenderer>(true);
                puffRoot.gameObject.SetActive(false);
            }
        }

        public void PlayDispense()
        {
            elapsed = 0f;
            if (puffRoot != null)
            {
                puffRoot.localPosition = puffBasePosition;
                puffRoot.localScale = Vector3.one * 0.35f;
                puffRoot.gameObject.SetActive(true);
                SetPuffAlpha(0.88f);
            }
        }

        private void Update()
        {
            if (elapsed >= AnimationDuration) return;

            elapsed = Mathf.Min(AnimationDuration, elapsed + Time.deltaTime);
            float t = elapsed / AnimationDuration;
            float kick = Mathf.Sin(t * Mathf.PI);
            float rebound = Mathf.Sin(Mathf.Clamp01((t - 0.22f) / 0.78f) * Mathf.PI);

            if (artwork != null)
            {
                artwork.localScale = new Vector3(
                    artworkBaseScale.x * (1f + kick * 0.055f),
                    artworkBaseScale.y * (1f - kick * 0.075f + rebound * 0.035f),
                    artworkBaseScale.z);
                artwork.localPosition = artworkBasePosition + Vector3.up * (rebound - kick * 0.35f) * puffTravel * 0.18f;
            }

            if (puffRoot != null)
            {
                puffRoot.localPosition = puffBasePosition + Vector3.down * (puffTravel * t);
                puffRoot.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.55f, t);
                SetPuffAlpha(0.88f * (1f - t));
            }

            if (elapsed < AnimationDuration) return;
            RestoreVisuals();
        }

        private void OnDisable()
        {
            RestoreVisuals();
        }

        private void RestoreVisuals()
        {
            elapsed = AnimationDuration;
            if (artwork != null)
            {
                artwork.localScale = artworkBaseScale;
                artwork.localPosition = artworkBasePosition;
            }
            if (puffRoot != null)
            {
                puffRoot.localPosition = puffBasePosition;
                puffRoot.gameObject.SetActive(false);
            }
        }

        private void SetPuffAlpha(float alpha)
        {
            if (puffLines == null) return;
            for (int i = 0; i < puffLines.Length; i++)
            {
                LineRenderer line = puffLines[i];
                if (line == null) continue;
                Color start = line.startColor;
                Color end = line.endColor;
                start.a = alpha;
                end.a = alpha;
                line.startColor = start;
                line.endColor = end;
            }
        }
    }
}
