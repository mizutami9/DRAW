using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class UfoGoalVisual : MonoBehaviour
    {
        [SerializeField] private float bobAmount = 0.12f;
        [SerializeField] private float bobSpeed = 2.1f;

        private Vector3 origin;

        private void Awake()
        {
            origin = transform.localPosition;
        }

        private void Update()
        {
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);
        }
    }
}
