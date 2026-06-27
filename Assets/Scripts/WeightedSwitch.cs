using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class WeightedSwitch : MonoBehaviour
    {
        [SerializeField] private MovingGate targetGate;
        [SerializeField] private SpriteRenderer indicator;
        [SerializeField] private bool requireHeavyTorso = true;

        private int activeContacts;
        private bool active;

        private void Awake()
        {
            Collider2D switchCollider = GetComponent<Collider2D>();
            switchCollider.isTrigger = true;
            RefreshVisual();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!CanPress(other))
            {
                return;
            }

            activeContacts++;
            SetActive(activeContacts > 0);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!CanPress(other))
            {
                return;
            }

            activeContacts = Mathf.Max(0, activeContacts - 1);
            SetActive(activeContacts > 0);
        }

        private bool CanPress(Collider2D other)
        {
            PlayerAbilityController ability = other.GetComponentInParent<PlayerAbilityController>();
            if (ability == null)
            {
                return false;
            }

            if (!requireHeavyTorso)
            {
                return true;
            }

            return ability.CurrentProfile.Torso != PlayerAbilityController.TorsoTier.Normal;
        }

        private void SetActive(bool value)
        {
            if (active == value)
            {
                return;
            }

            active = value;
            targetGate?.SetOpen(active);
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (indicator != null)
            {
                indicator.color = active ? new Color(0.25f, 1f, 0.32f) : new Color(0.85f, 0.2f, 0.18f);
            }
        }
    }
}
