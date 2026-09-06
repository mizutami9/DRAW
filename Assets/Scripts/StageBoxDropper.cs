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
        private StageDropperVisualAnimator visualAnimator;
        private Vector2 deviceSize = new Vector2(1.8f, 1.4f);
        private float interval = 2f;
        private int pattern;
        private float droppedBoxSize = 0.9f;
        private int sequence;
        private float nextSpawnTime;
        private bool manualDispense;

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
            visualAnimator = GetComponent<StageDropperVisualAnimator>();
            nextSpawnTime = Time.time + interval;
        }

        private void Update()
        {
            if (manualDispense || factory == null || spawnParent == null || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + interval;
            visualAnimator?.PlayDispense();
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }

            SpawnBox(Vector2.one * droppedBoxSize);
        }

        public void ConfigureManualDispense()
        {
            manualDispense = true;
        }

        public bool DispenseSelectedSize(float selectedSize)
        {
            return DispenseSelectedSize(Vector2.one * selectedSize);
        }

        public bool DispenseSelectedSize(Vector2 selectedSize)
        {
            if (factory == null || spawnParent == null) return false;
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost) return false;
            visualAnimator?.PlayDispense();
            selectedSize = new Vector2(Mathf.Clamp(selectedSize.x, 0.5f, 3f), Mathf.Clamp(selectedSize.y, 0.5f, 3f));
            return SpawnBox(selectedSize);
        }

        public void ClearSpawnedBoxes()
        {
            while (spawnedBoxes.Count > 0)
            {
                GameObject spawned = spawnedBoxes.Dequeue();
                if (spawned == null) continue;

                StageEditorObject spawnedMarker = spawned.GetComponent<StageEditorObject>();
                if (syncManager != null && syncManager.IsOnlineActive && spawnedMarker != null)
                {
                    if (syncManager.IsHost)
                        syncManager.RemoveDropperBox(spawnedMarker.objectId);
                }
                else
                {
                    Destroy(spawned);
                }
            }
        }

        private bool SpawnBox(Vector2 selectedSize)
        {
            StageObjectType type = ResolveNextBoxType();
            Vector2 boxSize = selectedSize;
            Vector2 position = (Vector2)transform.position
                - (Vector2)transform.up * (deviceSize.y * 0.5f + boxSize.y * 0.62f);
            string dropperId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string objectId = dropperId + "_box_" + sequence.ToString("D5");
            sequence++;

            GameObject spawned = syncManager != null && syncManager.IsOnlineActive
                ? syncManager.SpawnDropperBox(objectId, type, position, boxSize)
                : factory.CreateDroppedBox(type, objectId, position, 1f, spawnParent);
            if (spawned == null)
            {
                return false;
            }
            if (syncManager == null || !syncManager.IsOnlineActive)
                spawned.transform.localScale = new Vector3(boxSize.x, boxSize.y, 1f);

            Rigidbody2D spawnedBody = spawned.GetComponent<Rigidbody2D>();
            if (spawnedBody != null && spawnedBody.bodyType == RigidbodyType2D.Dynamic)
            {
                spawnedBody.linearVelocity += -(Vector2)transform.up * 1.4f;
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
            return true;
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
