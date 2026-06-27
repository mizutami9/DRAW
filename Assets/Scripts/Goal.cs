using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Goal : MonoBehaviour
    {
        [SerializeField] private StageManager stageManager;

        private void Awake()
        {
            Collider2D goalCollider = GetComponent<Collider2D>();
            goalCollider.isTrigger = true;

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.GetComponentInParent<PlayerController2D>())
            {
                return;
            }

            stageManager?.ClearStage();
        }
    }
}
