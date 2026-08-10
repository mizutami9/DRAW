using UnityEngine;

namespace DrawBody.Prototype
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, -0.6f, -10f);
        [SerializeField] private float followSpeed = 8f;
        [Header("Group Framing")]
        [SerializeField] private bool frameAllActivePlayers = true;
        [SerializeField] private float minimumOrthographicSize = 8f;
        [SerializeField] private float maximumOrthographicSize = 16f;
        [SerializeField] private Vector2 groupPadding = new Vector2(3f, 2.8f);
        [SerializeField] private float zoomSpeed = 4.5f;

        private Camera controlledCamera;

        public float MinimumOrthographicSize => minimumOrthographicSize;

        public void SetMinimumOrthographicSize(float value)
        {
            minimumOrthographicSize = Mathf.Clamp(value, 2f, maximumOrthographicSize);
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }
            if (controlledCamera != null && controlledCamera.orthographic)
            {
                controlledCamera.orthographicSize = Mathf.Max(
                    controlledCamera.orthographicSize,
                    minimumOrthographicSize);
            }
        }

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();

            if (target == null)
            {
                PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (controlledCamera != null && controlledCamera.orthographic)
            {
                controlledCamera.orthographicSize =
                    Mathf.Max(controlledCamera.orthographicSize, minimumOrthographicSize);
            }
        }

        private void LateUpdate()
        {
            Vector3 focusPosition;
            float desiredSize;
            if (TryGetGroupFrame(out Vector2 groupCenter, out desiredSize))
            {
                focusPosition = new Vector3(groupCenter.x, groupCenter.y, 0f);
            }
            else if (target != null)
            {
                focusPosition = target.position;
                desiredSize = minimumOrthographicSize;
            }
            else
            {
                return;
            }

            Vector3 desired = focusPosition + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            if (controlledCamera != null && controlledCamera.orthographic)
            {
                controlledCamera.orthographicSize = Mathf.Lerp(
                    controlledCamera.orthographicSize,
                    desiredSize,
                    1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
            }
        }

        private bool TryGetGroupFrame(out Vector2 center, out float desiredSize)
        {
            center = Vector2.zero;
            desiredSize = minimumOrthographicSize;
            if (!frameAllActivePlayers || controlledCamera == null || !controlledCamera.orthographic)
            {
                return false;
            }

            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                return false;
            }

            Vector2 localPosition = target != null
                ? (Vector2)target.position
                : (Vector2)players[0].transform.position;
            Vector2 maximumDistance = Vector2.zero;
            for (int i = 0; i < players.Length; i++)
            {
                Vector2 position = players[i].transform.position;
                Vector2 distance = position - localPosition;
                maximumDistance.x = Mathf.Max(maximumDistance.x, Mathf.Abs(distance.x));
                maximumDistance.y = Mathf.Max(maximumDistance.y, Mathf.Abs(distance.y));
            }

            // Keep the local player as the visual anchor. The view grows toward
            // distant partners instead of moving the camera to the group midpoint.
            center = localPosition;
            float aspect = Mathf.Max(0.1f, controlledCamera.aspect);
            float sizeForHeight = maximumDistance.y + groupPadding.y;
            float sizeForWidth = (maximumDistance.x + groupPadding.x) / aspect;
            desiredSize = Mathf.Clamp(
                Mathf.Max(minimumOrthographicSize, sizeForHeight, sizeForWidth),
                minimumOrthographicSize,
                maximumOrthographicSize);
            return true;
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
        }
    }
}
