using UnityEngine;

namespace DrawBody.Prototype
{
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

            Vector2 minimum = players[0].transform.position;
            Vector2 maximum = minimum;
            for (int i = 1; i < players.Length; i++)
            {
                Vector2 position = players[i].transform.position;
                minimum = Vector2.Min(minimum, position);
                maximum = Vector2.Max(maximum, position);
            }

            center = (minimum + maximum) * 0.5f;
            Vector2 halfSpread = (maximum - minimum) * 0.5f;
            float aspect = Mathf.Max(0.1f, controlledCamera.aspect);
            float sizeForHeight = halfSpread.y + groupPadding.y;
            float sizeForWidth = (halfSpread.x + groupPadding.x) / aspect;
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
