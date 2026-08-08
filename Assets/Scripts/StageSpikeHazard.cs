using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSpikeHazard : MonoBehaviour
    {
        private StageManager stageManager;

        private void Awake()
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D targetPlayer = other.GetComponentInParent<PlayerController2D>();
            if (targetPlayer == null)
            {
                return;
            }

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }
            stageManager?.RespawnFromHazard(targetPlayer);
        }
    }
}
