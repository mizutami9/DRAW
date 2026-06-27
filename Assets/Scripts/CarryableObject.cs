using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class CarryableObject : MonoBehaviour
    {
        [SerializeField] private float throwMultiplier = 1f;

        public float ThrowMultiplier => Mathf.Max(0.1f, throwMultiplier);
    }
}
