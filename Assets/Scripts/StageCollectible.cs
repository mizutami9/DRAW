using System.Collections;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageCollectible : MonoBehaviour
    {
        [SerializeField] private string objectId;
        [SerializeField] private StageObjectType collectibleType;
        private bool collected;
        private Transform collectorTransform;

        public string ObjectId => objectId;
        public StageObjectType CollectibleType => collectibleType;
        public Transform CollectorTransform => collectorTransform;

        public void Configure(string id, StageObjectType type)
        {
            objectId = id;
            collectibleType = type;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (collected || player == null)
            {
                return;
            }

            collectorTransform = player.transform;
            StageManager manager = FindFirstObjectByType<StageManager>();
            manager?.TryCollect(this);
        }

        public void ApplyCollected(Transform collector = null, bool playFeedback = true)
        {
            if (collected)
            {
                return;
            }

            collected = true;
            collectorTransform = collector != null ? collector : collectorTransform;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            if (!playFeedback)
            {
                gameObject.SetActive(false);
                return;
            }

            if (collectorTransform == null) collectorTransform = FindNearestPlayer();
            if (isActiveAndEnabled) StartCoroutine(AnimateIntoCollector());
            else gameObject.SetActive(false);
        }

        private IEnumerator AnimateIntoCollector()
        {
            Transform target = collectorTransform;
            Vector3 start = transform.position;
            Vector3 startScale = transform.localScale;
            Quaternion startRotation = transform.rotation;
            Color feedbackColor = collectibleType == StageObjectType.CollectibleCoin
                ? new Color(1f, 0.7f, 0.08f, 1f)
                : collectibleType == StageObjectType.CollectibleFish
                    ? new Color(0.1f, 0.65f, 1f, 1f)
                    : new Color(1f, 0.36f, 0.18f, 1f);

            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.035f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0.005f;
            trail.numCapVertices = 4;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            Color trailColor = Color.Lerp(feedbackColor, Color.white, 0.38f);
            trail.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0.72f);
            trail.endColor = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, 0f);
            trail.sortingOrder = 58;

            const float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                Vector3 end = target != null ? target.position + Vector3.up * 0.35f : start + Vector3.up * 0.7f;
                float arc = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(Vector3.Distance(start, end) * 0.08f + 0.45f, 0.45f, 1.15f);
                transform.position = Vector3.Lerp(start, end, eased) + Vector3.up * arc;
                float pop = 1f + Mathf.Sin(Mathf.Min(1f, t / 0.35f) * Mathf.PI) * 0.3f;
                float vanish = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, t));
                transform.localScale = startScale * (pop * vanish);
                transform.rotation = startRotation * Quaternion.Euler(0f, 0f, 260f * t);
                yield return null;
            }

            CollectiblePickupFeedback.Play(target, transform.position, feedbackColor);
            gameObject.SetActive(false);
        }

        private Transform FindNearestPlayer()
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                float distance = ((Vector2)(players[i].transform.position - transform.position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = players[i].transform;
            }
            return nearest;
        }
    }
}
