using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StageAerialHazardFactory
    {
        public static GameObject CreateMovingSpikePlanet(StageObjectData data, Transform parent)
        {
            GameObject root = StageSpikePlanet.CreateObject(data, parent);
            if (root == null) return null;
            root.name = StageObjectType.MovingSpikePlanet.ToString();
            StageMovingSpikePlanet mover = root.AddComponent<StageMovingSpikePlanet>();
            mover.Configure(
                data.position,
                data.movementAngle,
                data.actionStrength > 0f ? data.actionStrength : 8f,
                data.movementSpeed > 0f ? data.movementSpeed : 2.4f);
            return root;
        }

        public static GameObject CreateBombingEnemy(StageObjectData data, Transform parent, StageObjectFactory factory)
        {
            GameObject root = factory != null ? factory.CreateBombingEnemyBase(data, parent) : null;
            if (root == null) return null;
            StageBombingEnemy bomber = root.AddComponent<StageBombingEnemy>();
            bomber.Configure(
                factory,
                parent,
                data.actionStrength > 0f ? data.actionStrength : 3.2f,
                Mathf.Clamp(Mathf.Min(data.size.x, data.size.y) * 0.52f, 0.65f, 1.25f),
                data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 4.2f);
            return root;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageMovingSpikePlanet : MonoBehaviour
    {
        private Vector2 origin;
        private Vector2 direction;
        private float distance;
        private float speed;
        private float startedAt;
        private bool editing;

        public void Configure(Vector2 start, float angleDegrees, float moveDistance, float moveSpeed)
        {
            origin = start;
            float radians = angleDegrees * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            distance = Mathf.Clamp(moveDistance, 1f, 100f);
            speed = Mathf.Clamp(moveSpeed, 0.5f, 10f);
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            editing = editor != null && editor.IsEditing;
            if (editing) BuildEditorPath();
            else startedAt = Time.time;
        }

        private void FixedUpdate()
        {
            if (editing) return;
            float half = distance * 0.5f;
            float offset = Mathf.Sin((Time.time - startedAt) * speed / Mathf.Max(half, 0.5f)) * half;
            transform.position = origin + direction * offset;
        }

        private void BuildEditorPath()
        {
            GameObject preview = new GameObject("Movement Path Preview");
            preview.transform.SetParent(transform, false);
            LineRenderer line = preview.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, origin - direction * distance * 0.5f);
            line.SetPosition(1, origin + direction * distance * 0.5f);
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            line.numCapVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = new Color(0.15f, 0.55f, 1f, 0.48f);
            line.endColor = line.startColor;
            line.sortingOrder = 8;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageBombingEnemy : MonoBehaviour
    {
        private const int MaximumLiveBombs = 10;
        private readonly Queue<GameObject> spawnedBombs = new Queue<GameObject>();
        private StageObjectFactory factory;
        private Transform spawnParent;
        private StageEnemyCharacter enemy;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private float interval;
        private float bombSize;
        private float fuseSeconds = 4.2f;
        private float nextDropAt;
        private int sequence;

        public void Configure(
            StageObjectFactory targetFactory,
            Transform targetParent,
            float seconds,
            float size,
            float fuse = 4.2f)
        {
            factory = targetFactory;
            spawnParent = targetParent;
            interval = Mathf.Clamp(seconds, 0.5f, 10f);
            bombSize = Mathf.Clamp(size, 0.5f, 2f);
            fuseSeconds = Mathf.Clamp(fuse, 1f, 15f);
        }

        private void Start()
        {
            enemy = GetComponent<StageEnemyCharacter>();
            marker = GetComponent<StageEditorObject>();
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            nextDropAt = Time.time + interval * 0.7f;
        }

        private void Update()
        {
            if (factory == null || spawnParent == null || enemy != null && enemy.IsDefeated || Time.time < nextDropAt) return;
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost) return;
            nextDropAt = Time.time + interval;
            DropBomb();
        }

        private void DropBomb()
        {
            string sourceId = marker != null && !string.IsNullOrEmpty(marker.objectId) ? marker.objectId : gameObject.name;
            string objectId = sourceId + "_air_bomb_" + sequence.ToString("D5");
            sequence++;
            Vector2 position = (Vector2)transform.position + Vector2.down * 0.9f;
            Vector2 velocity = new Vector2(GetComponent<Rigidbody2D>() != null ? GetComponent<Rigidbody2D>().linearVelocity.x * 0.35f : 0f, -2.4f);
            GameObject spawned = syncManager != null && syncManager.IsOnlineActive
                ? syncManager.SpawnDropperBox(objectId, StageObjectType.Bomb, position, bombSize, fuseSeconds: fuseSeconds, launchVelocity: velocity)
                : factory.CreateDroppedBox(StageObjectType.Bomb, objectId, position, bombSize, spawnParent, fuseSeconds);
            if (spawned == null) return;
            Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = velocity;
            spawnedBombs.Enqueue(spawned);
            while (spawnedBombs.Count > MaximumLiveBombs)
            {
                GameObject oldest = spawnedBombs.Dequeue();
                if (oldest == null) continue;
                StageEditorObject oldestMarker = oldest.GetComponent<StageEditorObject>();
                if (syncManager != null && syncManager.IsOnlineActive && oldestMarker != null)
                    syncManager.RemoveDropperBox(oldestMarker.objectId);
                else
                    Destroy(oldest);
            }
        }
    }
}
