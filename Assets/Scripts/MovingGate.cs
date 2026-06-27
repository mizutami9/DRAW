using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class MovingGate : MonoBehaviour
    {
        [SerializeField] private Vector3 closedLocalPosition;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float moveSpeed = 8f;

        private bool open;

        private void Awake()
        {
            closedLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            Vector3 target = closedLocalPosition + (open ? openLocalOffset : Vector3.zero);
            transform.localPosition = Vector3.Lerp(transform.localPosition, target, 1f - Mathf.Exp(-moveSpeed * Time.deltaTime));
        }

        public void SetOpen(bool value)
        {
            open = value;
        }
    }
}
