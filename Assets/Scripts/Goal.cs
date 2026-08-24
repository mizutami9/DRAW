using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Goal : MonoBehaviour
    {
        [SerializeField] private StageManager stageManager;
        private readonly Dictionary<PlayerController2D, int> playerColliderCounts =
            new Dictionary<PlayerController2D, int>();

        private void Awake()
        {
            Collider2D goalCollider = GetComponent<Collider2D>();
            goalCollider.isTrigger = true;

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D goalPlayer = other.GetComponentInParent<PlayerController2D>();
            if (goalPlayer == null)
            {
                return;
            }

            playerColliderCounts.TryGetValue(goalPlayer, out int count);
            playerColliderCounts[goalPlayer] = count + 1;
            if (count == 0)
            {
                stageManager?.SetPlayerGoalState(goalPlayer, true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerController2D goalPlayer = other.GetComponentInParent<PlayerController2D>();
            if (goalPlayer == null || !playerColliderCounts.TryGetValue(goalPlayer, out int count))
            {
                return;
            }

            count--;
            if (count > 0)
            {
                playerColliderCounts[goalPlayer] = count;
                return;
            }

            playerColliderCounts.Remove(goalPlayer);
            stageManager?.SetPlayerGoalState(goalPlayer, false);
        }
    }
}
