using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBazooka : MonoBehaviour
    {
        private const float FireInterval = 0.9f;
        private const float RecoilVelocity = 13.5f;
        private PlayerCarryController holder;
        private StageBazookaSystem system;
        private float nextFireAt;

        public PlayerCarryController Holder => holder;

        public static GameObject CreateObject(StageObjectData data, Transform parent, int pushableLayer)
        {
            GameObject root = new GameObject(data.objectId) { name = StageObjectType.Bazooka.ToString(), layer = pushableLayer };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(Mathf.Max(0.9f, data.size.x), Mathf.Max(0.48f, data.size.y), 1f);
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 2.1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.96f, 0.48f);
            root.AddComponent<CarryableObject>();

            if (!StageGun.TryCreateResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/bazooka",
                "Colored Pencil Bazooka",
                // The source is a square texture whose actual drawing is already
                // 2.2:1. Keep the texture scale square so it is not stretched into
                // the extremely long silhouette seen in game.
                new Vector2(1.16f, 1.16f),
                39))
            {
            Color green = new Color(0.18f, 0.48f, 0.25f, 1f);
            Color dark = new Color(0.04f, 0.15f, 0.08f, 1f);
            StageGun.CreateSprite(root.transform, "Bazooka Tube", new Vector2(0.02f, 0.08f), new Vector2(1.18f, 0.38f), green, 35);
            StageGun.CreateSprite(root.transform, "Bazooka Muzzle", new Vector2(0.63f, 0.08f), new Vector2(0.25f, 0.58f), dark, 36);
            StageGun.CreateSprite(root.transform, "Bazooka Rear", new Vector2(-0.62f, 0.08f), new Vector2(0.24f, 0.5f), new Color(0.3f, 0.22f, 0.12f, 1f), 35);
            StageGun.CreateSprite(root.transform, "Bazooka Grip", new Vector2(-0.08f, -0.26f), new Vector2(0.22f, 0.44f), dark, 34, -10f);
            StageGun.AddLine(root.transform, "Bazooka Crayon Outline", new[]
            {
                new Vector2(-0.72f, -0.14f), new Vector2(0.72f, -0.14f), new Vector2(0.78f, 0.3f),
                new Vector2(-0.72f, 0.3f), new Vector2(-0.72f, -0.14f)
            }, 0.065f, dark, 39);
            StageGun.AddLine(root.transform, "Bazooka Highlight", new[]
            {
                new Vector2(-0.45f, 0.18f), new Vector2(-0.05f, 0.13f), new Vector2(0.38f, 0.2f)
            }, 0.055f, new Color(0.56f, 0.78f, 0.3f, 0.8f), 38);
            }

            StageBazooka bazooka = root.AddComponent<StageBazooka>();
            StageEditorObject marker = root.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
            marker.actionStrength = data.actionStrength;
            marker.movementAngle = data.movementAngle;
            marker.movementSpeed = data.movementSpeed;
            marker.spawnPattern = data.spawnPattern;
            marker.spawnBoxSize = data.spawnBoxSize;
            marker.bombFuseSeconds = data.bombFuseSeconds;
            marker.linkTargetId = data.linkTargetId;
            marker.linkAction = data.linkAction;
            return root;
        }

        private void Start() => system = StageBazookaSystem.Ensure(transform);

        public void SetHolder(PlayerCarryController value)
        {
            holder = value;
            if (system == null) system = StageBazookaSystem.Ensure(transform);
        }

        public void UpdateHeldPose(Vector3 anchor, Vector2 aimWorld)
        {
            transform.position = anchor;
            Vector2 direction = aimWorld - (Vector2)anchor;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public void TryFire(Vector2 aimWorld)
        {
            if (holder == null || Time.time < nextFireAt) return;
            Vector2 origin = transform.TransformPoint(new Vector2(0.58f, 0.08f));
            Vector2 direction = (aimWorld - origin).normalized;
            if (direction.sqrMagnitude < 0.1f) return;
            nextFireAt = Time.time + FireInterval;
            if (system == null) system = StageBazookaSystem.Ensure(transform);
            float recoilMultiplier = holder.IsCarriedForWeaponRecoil() ? 5f : 1f;
            Vector2 recoil = -direction * (RecoilVelocity * recoilMultiplier);
            if (system != null && system.IsOnline)
                system.RequestFire(GetObjectId(), origin, direction, holder.GetWeaponRecoilTargetOnlineId(), recoil);
            else
            {
                holder.ApplyDirectWeaponRecoil(recoil);
                system?.RequestFire(GetObjectId(), origin, direction, null, Vector2.zero);
            }
        }

        private string GetObjectId()
        {
            StageEditorObject marker = GetComponent<StageEditorObject>();
            return marker != null ? marker.objectId : gameObject.name;
        }
    }

    public sealed class StageBazookaSystem : MonoBehaviour
    {
        private const string SystemId = "stage_bazooka_system";
        private const string FireRequestKind = "bazooka_fire_request";
        private const string FireKind = "bazooka_fire";

        [System.Serializable]
        private sealed class FireData
        {
            public int Sequence;
            public string WeaponId;
            public Vector2 Origin;
            public Vector2 Direction;
            public string RecoilTargetId;
            public Vector2 Recoil;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private int sequence;
        private int lastSequence;

        public bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        public static StageBazookaSystem Ensure(Transform context)
        {
            if (context == null) return null;
            StageGimmickSyncManager sync = context.GetComponentInParent<StageGimmickSyncManager>();
            Transform root = sync != null ? sync.transform : context.root;
            StageBazookaSystem existing = root.GetComponent<StageBazookaSystem>();
            return existing != null ? existing : root.gameObject.AddComponent<StageBazookaSystem>();
        }

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
        }

        public void RequestFire(string weaponId, Vector2 origin, Vector2 direction, string recoilTargetId, Vector2 recoil)
        {
            FireData data = new FireData
            {
                WeaponId = weaponId,
                Origin = origin,
                Direction = direction.normalized,
                RecoilTargetId = recoilTargetId,
                Recoil = recoil
            };
            if (IsOnline && !HasAuthority) { Send(FireRequestKind, data); return; }
            ConfirmFire(data);
        }

        private void ConfirmFire(FireData data)
        {
            data.Sequence = ++sequence;
            StageBazookaRocket.Create(transform, data.Origin, data.Direction, true);
            ApplyRecoilToLocalPlayer(data.RecoilTargetId, data.Recoil);
            GameSfx.PlayAt(SfxId.CannonFire, data.Origin, 1.15f);
            if (IsOnline) Send(FireKind, data);
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != SystemId) return;
            if (message.Kind == FireRequestKind && HasAuthority)
            {
                FireData request = JsonUtility.FromJson<FireData>(message.Json);
                if (request != null) ConfirmFire(request);
            }
            else if (message.Kind == FireKind && !HasAuthority && IsHost(message.PlayerId))
            {
                FireData data = JsonUtility.FromJson<FireData>(message.Json);
                if (data == null || data.Sequence <= lastSequence) return;
                lastSequence = data.Sequence;
                StageBazookaRocket.Create(transform, data.Origin, data.Direction, false);
                ApplyRecoilToLocalPlayer(data.RecoilTargetId, data.Recoil);
                GameSfx.PlayAt(SfxId.CannonFire, data.Origin, 1.15f);
            }
        }

        private void ApplyRecoilToLocalPlayer(string playerId, Vector2 recoil)
        {
            if (!IsOnline || string.IsNullOrEmpty(playerId) || onlineManager == null
                || playerId != onlineManager.LocalPlayerId || recoil.sqrMagnitude < 0.01f) return;
            Transform local = stageManager != null ? stageManager.ActivePlayerTransform : null;
            local?.GetComponent<PlayerCarryController>()?.ApplyDirectWeaponRecoil(recoil);
        }

        private void Send(string kind, FireData data)
        {
            if (onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData { ObjectId = SystemId, Kind = kind, Json = JsonUtility.ToJson(data) });
        }

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }
    }

    public sealed class StageBazookaRocket : MonoBehaviour
    {
        private const float Speed = 26f;
        private Vector2 direction;
        private bool authoritative;
        private float life;

        public static void Create(Transform parent, Vector2 origin, Vector2 direction, bool authoritative)
        {
            GameObject root = new GameObject("Crayon Bazooka Rocket");
            root.transform.SetParent(parent, false);
            root.transform.position = origin;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            StageGun.CreateSprite(root.transform, "Rocket Body", Vector2.zero, new Vector2(0.72f, 0.25f), new Color(0.22f, 0.5f, 0.27f, 1f), 49);
            StageGun.CreateSprite(root.transform, "Rocket Tip", new Vector2(0.38f, 0f), new Vector2(0.28f, 0.3f), new Color(0.9f, 0.2f, 0.12f, 1f), 50);
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.24f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.startColor = new Color(1f, 0.65f, 0.12f, 0.9f);
            trail.endColor = new Color(1f, 0.15f, 0.05f, 0f);
            trail.sortingOrder = 48;
            StageBazookaRocket rocket = root.AddComponent<StageBazookaRocket>();
            rocket.direction = direction.normalized;
            rocket.authoritative = authoritative;
        }

        private void Update()
        {
            float distance = Speed * Time.deltaTime;
            if (TryHit(distance)) { Destroy(gameObject); return; }
            transform.position += (Vector3)(direction * distance);
            life += Time.deltaTime;
            if (life >= 4f) Destroy(gameObject);
        }

        private bool TryHit(float distance)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, distance + 0.25f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger
                    || collider.GetComponentInParent<PlayerController2D>() != null
                    || collider.GetComponentInParent<StageBazooka>() != null) continue;
                StageBazookaExplosion.Create(transform.parent, hits[i].point, authoritative);
                return true;
            }
            return false;
        }
    }

    public sealed class StageBazookaExplosion : MonoBehaviour
    {
        private SpriteRenderer outer;
        private SpriteRenderer inner;
        private float born;

        public static void Create(Transform parent, Vector2 point, bool applyDamage)
        {
            GameObject root = new GameObject("Bazooka Explosion");
            root.transform.SetParent(parent, false);
            root.transform.position = point;
            GameObject outerObj = StageGun.CreateSprite(root.transform, "Explosion Outer", Vector2.zero, Vector2.one,
                new Color(1f, 0.28f, 0.06f, 0.8f), 54);
            outerObj.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
            GameObject innerObj = StageGun.CreateSprite(root.transform, "Explosion Inner", Vector2.zero, Vector2.one,
                new Color(1f, 0.88f, 0.18f, 0.95f), 55);
            innerObj.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
            StageBazookaExplosion explosion = root.AddComponent<StageBazookaExplosion>();
            explosion.outer = outerObj.GetComponent<SpriteRenderer>();
            explosion.inner = innerObj.GetComponent<SpriteRenderer>();
            explosion.born = Time.time;
            GameSfx.PlayAt(SfxId.BombExplosion, point, 0.95f);
            if (applyDamage) DamageEnemies(point, 1.8f);
        }

        private static void DamageEnemies(Vector2 point, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                StageEnemyCharacter enemy = hits[i] != null ? hits[i].GetComponentInParent<StageEnemyCharacter>() : null;
                if (enemy != null && !enemy.IsDefeated) enemy.RequestDefeat();
            }
        }

        private void Update()
        {
            float t = (Time.time - born) / 0.38f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 3.4f, t);
            if (outer != null) outer.color = new Color(1f, 0.28f, 0.06f, Mathf.Lerp(0.8f, 0f, t));
            if (inner != null)
            {
                inner.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 0.2f, t);
                inner.color = new Color(1f, 0.88f, 0.18f, Mathf.Lerp(1f, 0f, t));
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
