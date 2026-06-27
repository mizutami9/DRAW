using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, -10f);
        [SerializeField] private float followSpeed = 8f;

        private void Awake()
        {
            if (target == null)
            {
                PlayerController2D player = FindObjectOfType<PlayerController2D>();
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
        }
    }
}
