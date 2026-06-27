using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LeverSwitch : MonoBehaviour
    {
        [SerializeField] private MovingGate targetGate;
        [SerializeField] private SpriteRenderer indicator;

        private bool activated;

        private void Awake()
        {
            Collider2D leverCollider = GetComponent<Collider2D>();
            leverCollider.isTrigger = true;
            RefreshVisual();
        }

        public void Activate()
        {
            if (activated)
            {
                return;
            }

            activated = true;
            targetGate?.SetOpen(true);
            RefreshVisual();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activated)
            {
                return;
            }

            if (!other.GetComponentInParent<PlayerController2D>() && !other.GetComponentInParent<ArmSwingController>())
            {
                return;
            }

            Activate();
        }

        private void RefreshVisual()
        {
            if (indicator != null)
            {
                indicator.color = activated ? new Color(0.25f, 1f, 0.32f) : new Color(1f, 0.72f, 0.18f);
            }
        }
    }
}
