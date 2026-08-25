using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageEnemyDropper : MonoBehaviour, IStageLinkActivatable
    {
        private const int MaximumLiveEnemies = 14;

        private readonly Queue<GameObject> spawnedEnemies = new Queue<GameObject>();
        private StageObjectFactory factory;
        private Transform spawnParent;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private StageDropperVisualAnimator visualAnimator;
        private Vector2 deviceSize;
        private float interval;
        private int enemyPattern;
        private float enemySize;
        private int sequence;
        private float nextSpawnTime;
        private bool linkedMode;

        public void Configure(
            StageObjectFactory targetFactory,
            Transform targetParent,
            Vector2 size,
            float seconds,
            int pattern,
            float spawnedEnemySize)
        {
            factory = targetFactory;
            spawnParent = targetParent;
            deviceSize = size;
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            enemyPattern = Mathf.Clamp(pattern, 0, 4);
            enemySize = Mathf.Clamp(spawnedEnemySize > 0f ? spawnedEnemySize : 0.9f, 0.7f, 2f);
        }

        private void Start()
        {
            marker = GetComponent<StageEditorObject>();
            syncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            visualAnimator = GetComponent<StageDropperVisualAnimator>();
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            nextSpawnTime = Time.time + interval;
        }

        private void Update()
        {
            if (linkedMode || factory == null || spawnParent == null || Time.time < nextSpawnTime
                || syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }
            nextSpawnTime = Time.time + interval;
            visualAnimator?.PlayDispense();
            SpawnEnemy();
        }

        public void PrepareForLink()
        {
            linkedMode = true;
        }

        public void ActivateFromLink()
        {
            if (factory != null && spawnParent != null
                && (syncManager == null || !syncManager.IsOnlineActive || syncManager.IsHost))
            {
                visualAnimator?.PlayDispense();
                SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            Vector2 direction = -(Vector2)transform.up;
            Vector2 position = (Vector2)transform.position
                + direction * (deviceSize.y * 0.5f + enemySize * 0.65f);
            string spawnerId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string objectId = spawnerId + "_enemy_" + sequence.ToString("D5");
            sequence++;

            float facing = Mathf.Abs(direction.x) > 0.1f ? Mathf.Sign(direction.x) : 1f;
            Vector2 launchVelocity = direction * 5.2f;
            GameObject enemy = syncManager != null
                ? syncManager.SpawnDropperEnemy(
                    objectId,
                    ResolveEnemyType(),
                    position,
                    enemySize,
                    2.4f,
                    facing,
                    launchVelocity)
                : factory.CreateSpawnedEnemy(
                    ResolveEnemyType(),
                    objectId,
                    position,
                    enemySize,
                    spawnParent,
                    2.4f,
                    facing);
            if (enemy == null)
            {
                return;
            }

            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = launchVelocity;
            }
            GameSfx.PlayAt(SfxId.CannonFire, position, 0.52f);

            spawnedEnemies.Enqueue(enemy);
            while (spawnedEnemies.Count > MaximumLiveEnemies)
            {
                GameObject oldest = spawnedEnemies.Dequeue();
                if (oldest == null) continue;
                StageEditorObject oldestMarker = oldest.GetComponent<StageEditorObject>();
                if (syncManager != null && oldestMarker != null)
                    syncManager.RemoveDropperEnemy(oldestMarker.objectId);
                else
                    Destroy(oldest);
            }
        }

        private StageObjectType ResolveEnemyType()
        {
            switch (enemyPattern)
            {
                case 1: return StageObjectType.EnemyJumper;
                case 2: return StageObjectType.EnemyCharger;
                case 3: return StageObjectType.EnemyFlyer;
                case 4: return StageObjectType.EnemyShooter;
                default: return StageObjectType.EnemyWalker;
            }
        }
    }
}
