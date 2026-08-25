using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageConveyorVisualAnimator : MonoBehaviour
    {
        private Transform[] treads;
        private Transform[] rollers;
        private float travelHalfWidth;
        private float rollerRadius;
        private float direction = 1f;
        private float visualSpeed = 1f;

        public void Configure(
            Transform[] movingTreads,
            Transform[] turningRollers,
            float halfWidth,
            float radius,
            float movementDirection,
            float configuredSpeed)
        {
            treads = movingTreads;
            rollers = turningRollers;
            travelHalfWidth = Mathf.Max(0.05f, halfWidth);
            rollerRadius = Mathf.Max(0.03f, radius);
            direction = movementDirection < 0f ? -1f : 1f;
            visualSpeed = Mathf.Lerp(0.35f, 1.8f, Mathf.InverseLerp(0.5f, 10f, configuredSpeed));
        }

        private void Update()
        {
            float delta = visualSpeed * direction * Time.deltaTime;
            float span = travelHalfWidth * 2f;
            if (treads != null && span > 0.001f)
            {
                for (int i = 0; i < treads.Length; i++)
                {
                    Transform tread = treads[i];
                    if (tread == null) continue;

                    Vector3 position = tread.localPosition;
                    position.x += delta;
                    while (position.x > travelHalfWidth) position.x -= span;
                    while (position.x < -travelHalfWidth) position.x += span;
                    tread.localPosition = position;
                }
            }

            if (rollers == null) return;
            float degrees = -delta / rollerRadius * Mathf.Rad2Deg;
            for (int i = 0; i < rollers.Length; i++)
            {
                if (rollers[i] != null)
                {
                    rollers[i].Rotate(0f, 0f, degrees, Space.Self);
                }
            }
        }
    }
}
