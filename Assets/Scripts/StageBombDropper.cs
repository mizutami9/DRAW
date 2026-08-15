using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBombDropper : MonoBehaviour, IStageLinkActivatable
    {
        private const int MaximumLiveBombs = 16;

        private readonly Queue<GameObject> spawnedBombs = new Queue<GameObject>();
        private StageObjectFactory factory;
        private Transform spawnParent;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private Vector2 deviceSize = new Vector2(1.8f, 1.4f);
        private float interval = 2f;
        private int pattern;
        private float bombSize = 0.9f;
        private float fuseSeconds = 5f;
        private int sequence;
        private float nextSpawnTime;
        private bool linkedMode;

        public void Configure(
            StageObjectFactory targetFactory,
            Transform targetParent,
            Vector2 size,
            float seconds,
            int bombPattern,
            float droppedBombSize,
            float bombFuseSeconds)
        {
            factory = targetFactory;
            spawnParent = targetParent;
            deviceSize = size;
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            pattern = Mathf.Clamp(bombPattern, 0, 2);
            bombSize = Mathf.Clamp(droppedBombSize > 0f ? droppedBombSize : 0.9f, 0.5f, 2f);
            fuseSeconds = Mathf.Clamp(bombFuseSeconds > 0f ? bombFuseSeconds : 5f, 1f, 15f);
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
            nextSpawnTime = Time.time + interval;
        }

        private void Update()
        {
            if (linkedMode || factory == null || spawnParent == null || Time.time < nextSpawnTime)
            {
                return;
            }
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }

            nextSpawnTime = Time.time + interval;
            SpawnBomb();
        }

        public void PrepareForLink()
        {
            linkedMode = true;
        }

        public void ActivateFromLink()
        {
            if (factory == null || spawnParent == null
                || syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }

            SpawnBomb();
        }

        private void SpawnBomb()
        {
            StageObjectType type = ResolveNextBombType();
            Vector2 position = (Vector2)transform.position
                - (Vector2)transform.up * (deviceSize.y * 0.5f + bombSize * 0.62f);
            string dropperId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string objectId = dropperId + "_bomb_" + sequence.ToString("D5");
            sequence++;
            Vector2 launchVelocity = -(Vector2)transform.up * 5.5f;

            GameObject spawned = syncManager != null && syncManager.IsOnlineActive
                ? syncManager.SpawnDropperBox(
                    objectId,
                    type,
                    position,
                    bombSize,
                    fuseSeconds: fuseSeconds,
                    launchVelocity: launchVelocity)
                : factory.CreateDroppedBox(type, objectId, position, bombSize, spawnParent, fuseSeconds);
            if (spawned == null)
            {
                return;
            }

            Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = launchVelocity;
            }

            spawnedBombs.Enqueue(spawned);
            while (spawnedBombs.Count > MaximumLiveBombs)
            {
                GameObject oldest = spawnedBombs.Dequeue();
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

        private StageObjectType ResolveNextBombType()
        {
            if (pattern == 1)
            {
                return StageObjectType.Bomb;
            }
            if (pattern == 2)
            {
                return StageObjectType.PickupFuseBomb;
            }
            return sequence % 2 == 0 ? StageObjectType.Bomb : StageObjectType.PickupFuseBomb;
        }
    }
}
