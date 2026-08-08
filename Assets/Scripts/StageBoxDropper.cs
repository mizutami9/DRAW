using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBoxDropper : MonoBehaviour
    {
        private const int MaximumLiveBoxes = 32;

        private readonly Queue<GameObject> spawnedBoxes = new Queue<GameObject>();
        private StageObjectFactory factory;
        private Transform spawnParent;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private Vector2 deviceSize = new Vector2(1.8f, 1.4f);
        private float interval = 2f;
        private int pattern;
        private float droppedBoxSize = 0.9f;
        private int sequence;
        private float nextSpawnTime;

        public void Configure(
            StageObjectFactory targetFactory,
            Transform targetParent,
            Vector2 size,
            float seconds,
            int boxPattern,
            float boxSize)
        {
            factory = targetFactory;
            spawnParent = targetParent;
            deviceSize = size;
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            pattern = Mathf.Clamp(boxPattern, 0, 3);
            droppedBoxSize = Mathf.Clamp(boxSize > 0f ? boxSize : 0.9f, 0.5f, 2f);
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
            if (factory == null || spawnParent == null || Time.time < nextSpawnTime)
            {
                return;
            }

            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }

            nextSpawnTime = Time.time + interval;
            SpawnBox();
        }

        private void SpawnBox()
        {
            StageObjectType type = ResolveNextBoxType();
            float boxSize = droppedBoxSize;
            Vector2 position = (Vector2)transform.position
                - (Vector2)transform.up * (deviceSize.y * 0.5f + boxSize * 0.62f);
            string dropperId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string objectId = dropperId + "_box_" + sequence.ToString("D5");
            sequence++;

            GameObject spawned = syncManager != null && syncManager.IsOnlineActive
                ? syncManager.SpawnDropperBox(objectId, type, position, boxSize)
                : factory.CreateDroppedBox(type, objectId, position, boxSize, spawnParent);
            if (spawned == null)
            {
                return;
            }

            spawnedBoxes.Enqueue(spawned);
            while (spawnedBoxes.Count > MaximumLiveBoxes)
            {
                GameObject oldest = spawnedBoxes.Dequeue();
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

        private StageObjectType ResolveNextBoxType()
        {
            switch (pattern)
            {
                case 1:
                    return StageObjectType.WoodBox;
                case 2:
                    return StageObjectType.Ball;
                case 3:
                    return StageObjectType.TriangleBox;
                default:
                    switch (sequence % 3)
                    {
                        case 1:
                            return StageObjectType.Ball;
                        case 2:
                            return StageObjectType.TriangleBox;
                        default:
                            return StageObjectType.WoodBox;
                    }
            }
        }
    }
}
