using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageSpikeDropper : MonoBehaviour
    {
        private const int MaximumLiveSpikes = 32;

        private readonly Queue<GameObject> spawnedSpikes = new Queue<GameObject>();
        private StageObjectFactory factory;
        private Transform spawnParent;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private StageDropperVisualAnimator visualAnimator;
        private Vector2 deviceSize = new Vector2(1.8f, 1.4f);
        private float interval = 2f;
        private float droppedSpikeSize = 0.9f;
        private int sequence;
        private float nextSpawnTime;

        public void Configure(
            StageObjectFactory targetFactory,
            Transform targetParent,
            Vector2 size,
            float seconds,
            float spikeSize)
        {
            factory = targetFactory;
            spawnParent = targetParent;
            deviceSize = size;
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            droppedSpikeSize = Mathf.Clamp(spikeSize > 0f ? spikeSize : 0.9f, 0.5f, 2f);
        }

        private void Start()
        {
            marker = GetComponent<StageEditorObject>();
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            visualAnimator = GetComponent<StageDropperVisualAnimator>();
            nextSpawnTime = Time.time + interval;
        }

        private void Update()
        {
            if (factory == null || spawnParent == null || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + interval;
            visualAnimator?.PlayDispense();
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }

            SpawnSpike();
        }

        private void SpawnSpike()
        {
            Vector2 position = (Vector2)transform.position
                - (Vector2)transform.up * (deviceSize.y * 0.5f + droppedSpikeSize * 0.55f);
            string dropperId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string objectId = dropperId + "_spike_" + sequence.ToString("D5");
            sequence++;

            GameObject spawned = syncManager != null && syncManager.IsOnlineActive
                ? syncManager.SpawnDropperBox(
                    objectId,
                    StageObjectType.Spike,
                    position,
                    droppedSpikeSize,
                    transform.eulerAngles.z)
                : factory.CreateDroppedBox(StageObjectType.Spike, objectId, position, droppedSpikeSize, spawnParent);
            if (spawned == null)
            {
                return;
            }

            spawned.transform.rotation = transform.rotation;
            Rigidbody2D spawnedBody = spawned.GetComponent<Rigidbody2D>();
            if (spawnedBody != null && spawnedBody.bodyType == RigidbodyType2D.Dynamic)
            {
                spawnedBody.linearVelocity += -(Vector2)transform.up * 1.15f;
            }
            spawnedSpikes.Enqueue(spawned);
            while (spawnedSpikes.Count > MaximumLiveSpikes)
            {
                GameObject oldest = spawnedSpikes.Dequeue();
                if (oldest == null)
                {
                    continue;
                }

                StageEditorObject oldestMarker = oldest.GetComponent<StageEditorObject>();
                if (syncManager != null && syncManager.IsOnlineActive && oldestMarker != null)
                {
                    syncManager.RemoveDropperBox(oldestMarker.objectId);
                }
                else
                {
                    Destroy(oldest);
                }
            }
        }
    }
}
