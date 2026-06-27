using UnityEngine;
using System.Collections.Generic;

namespace DrawBody.Prototype
{
    public sealed class BalanceScale : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D beamBody;
        [SerializeField] private float maxTilt = 24f;
        [SerializeField] private float tiltPerMassDistance = 42f;
        [SerializeField] private float rotationSpeed = 160f;

        private readonly HashSet<Rigidbody2D> reportedBodies = new HashSet<Rigidbody2D>();
        private float loadTorque;

        private void FixedUpdate()
        {
            if (beamBody == null)
            {
                return;
            }

            float targetAngle = Mathf.Clamp(-loadTorque * tiltPerMassDistance, -maxTilt, maxTilt);
            float nextAngle = Mathf.MoveTowardsAngle(beamBody.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime);
            beamBody.MoveRotation(nextAngle);
            beamBody.angularVelocity = 0f;
            loadTorque = 0f;
            reportedBodies.Clear();
        }

        public void SetBeam(Rigidbody2D beam)
        {
            beamBody = beam;
        }

        public void ReportLoad(Rigidbody2D loadBody, Vector2 worldPoint)
        {
            if (loadBody == null || loadBody == beamBody || !reportedBodies.Add(loadBody))
            {
                return;
            }

            float leverArm = worldPoint.x - transform.position.x;
            loadTorque += Mathf.Max(0.01f, loadBody.mass) * leverArm;
        }
    }

    public sealed class BalanceScaleBeam : MonoBehaviour
    {
        [SerializeField] private BalanceScale scale;

        public void SetScale(BalanceScale owner)
        {
            scale = owner;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (scale == null || collision.rigidbody == null)
            {
                return;
            }

            Vector2 contactPoint = collision.GetContact(0).point;
            scale.ReportLoad(collision.rigidbody, contactPoint);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (scale == null || other == null || other.attachedRigidbody == null)
            {
                return;
            }

            scale.ReportLoad(other.attachedRigidbody, other.bounds.center);
        }
    }
}
