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

    public sealed class VerticalBalanceScale : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D leftTray;
        [SerializeField] private Rigidbody2D rightTray;
        [SerializeField] private float travel = 2.2f;
        [SerializeField] private float moveSpeed = 3.0f;
        [SerializeField] private float massToTravel = 0.22f;

        private Vector2 leftOrigin;
        private Vector2 rightOrigin;
        private float leftLoad;
        private float rightLoad;

        public void Configure(Rigidbody2D left, Rigidbody2D right, float maxTravel)
        {
            leftTray = left;
            rightTray = right;
            travel = Mathf.Max(0.8f, maxTravel);
            if (leftTray != null)
            {
                leftOrigin = leftTray.position;
            }

            if (rightTray != null)
            {
                rightOrigin = rightTray.position;
            }
        }

        private void FixedUpdate()
        {
            float diff = Mathf.Clamp((rightLoad - leftLoad) * massToTravel, -travel, travel);
            MoveTray(leftTray, leftOrigin + Vector2.up * diff);
            MoveTray(rightTray, rightOrigin - Vector2.up * diff);
            leftLoad = 0f;
            rightLoad = 0f;
        }

        public void ReportLoad(int side, Rigidbody2D body)
        {
            if (body == null)
            {
                return;
            }

            if (side < 0)
            {
                leftLoad += Mathf.Max(0.1f, body.mass);
            }
            else
            {
                rightLoad += Mathf.Max(0.1f, body.mass);
            }
        }

        private void MoveTray(Rigidbody2D tray, Vector2 target)
        {
            if (tray == null)
            {
                return;
            }

            Vector2 next = Vector2.MoveTowards(tray.position, target, moveSpeed * Time.fixedDeltaTime);
            tray.MovePosition(next);
        }
    }

    public sealed class VerticalBalanceTray : MonoBehaviour
    {
        [SerializeField] private VerticalBalanceScale scale;
        [SerializeField] private int side;

        public void Configure(VerticalBalanceScale owner, int traySide)
        {
            scale = owner;
            side = traySide;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (scale == null)
            {
                return;
            }

            scale.ReportLoad(side, FindBody(collision.collider));
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (scale == null)
            {
                return;
            }

            scale.ReportLoad(side, FindBody(other));
        }

        private static Rigidbody2D FindBody(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.attachedRigidbody != null)
            {
                return collider.attachedRigidbody;
            }

            return collider.GetComponentInParent<Rigidbody2D>();
        }
    }
}
