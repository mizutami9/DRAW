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
        [SerializeField] private float maximumOrthographicSize = 20f;
        [SerializeField] private Vector2 groupPadding = new Vector2(4.5f, 4f);
        [SerializeField] private float zoomSpeed = 4.5f;

        private Camera controlledCamera;
        private PlayerController2D[] cachedPlayers = System.Array.Empty<PlayerController2D>();
        private float nextPlayerCacheRefreshAt;
        private const float PlayerCacheRefreshInterval = 0.25f;

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
            Transform activeFocus = ResolveActiveFocusTarget();
            Vector3 focusPosition;
            float desiredSize;
            if (TryGetGroupFrame(activeFocus, out Vector2 groupCenter, out desiredSize))
            {
                focusPosition = new Vector3(groupCenter.x, groupCenter.y, 0f);
            }
            else if (activeFocus != null)
            {
                focusPosition = activeFocus.position;
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

        private bool TryGetGroupFrame(Transform activeFocus, out Vector2 center, out float desiredSize)
        {
            center = Vector2.zero;
            desiredSize = minimumOrthographicSize;
            if (!frameAllActivePlayers || controlledCamera == null || !controlledCamera.orthographic)
            {
                return false;
            }

            PlayerController2D[] players = GetCachedPlayers();
            PlayerController2D firstActive = FindFirstActive(players);
            if (firstActive == null)
            {
                return false;
            }

            Vector2 localPosition = activeFocus != null
                ? (Vector2)activeFocus.position
                : (Vector2)firstActive.transform.position;
            Vector2 maximumDistance = Vector2.zero;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].gameObject.activeInHierarchy) continue;
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

        private Transform ResolveActiveFocusTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                return target;
            }

            PlayerController2D[] players = GetCachedPlayers();
            PlayerController2D firstActive = FindFirstActive(players);
            if (firstActive == null)
            {
                return null;
            }

            // In no-respawn modes the local avatar is disabled on elimination.
            // Spectate the closest surviving teammate without replacing the saved
            // local target, so the camera automatically returns after a retry.
            Vector2 lastLocalPosition = target != null
                ? (Vector2)target.position
                : (Vector2)transform.position;
            Transform closest = firstActive.transform;
            float closestDistance = Vector2.SqrMagnitude((Vector2)closest.position - lastLocalPosition);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].gameObject.activeInHierarchy
                    || players[i] == firstActive) continue;
                float distance = Vector2.SqrMagnitude((Vector2)players[i].transform.position - lastLocalPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = players[i].transform;
                }
            }
            return closest;
        }

        private PlayerController2D[] GetCachedPlayers()
        {
            if (cachedPlayers == null || Time.unscaledTime >= nextPlayerCacheRefreshAt)
            {
                cachedPlayers = FindObjectsByType<PlayerController2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                nextPlayerCacheRefreshAt = Time.unscaledTime + PlayerCacheRefreshInterval;
            }
            return cachedPlayers;
        }

        private static PlayerController2D FindFirstActive(PlayerController2D[] players)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].gameObject.activeInHierarchy)
                    return players[i];
            }
            return null;
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
        }
    }
}
