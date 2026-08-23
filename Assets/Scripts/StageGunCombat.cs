using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageGun : MonoBehaviour
    {
        private const float FireInterval = 0.22f;
        private PlayerCarryController holder;
        private StageGunSystem system;
        private float nextFireAt;

        public static GameObject CreateObject(StageObjectData data, Transform parent, int pushableLayer)
        {
            GameObject root = new GameObject(data.objectId) { name = StageObjectType.Handgun.ToString(), layer = pushableLayer };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(Mathf.Max(0.7f, data.size.x), Mathf.Max(0.45f, data.size.y), 1f);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 1.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.92f, 0.52f);
            root.AddComponent<CarryableObject>();

            CreateSprite(root.transform, "Gun Body", new Vector2(0.02f, 0.08f), new Vector2(0.95f, 0.42f), new Color(0.17f, 0.25f, 0.34f, 1f), 34);
            CreateSprite(root.transform, "Gun Barrel", new Vector2(0.52f, 0.14f), new Vector2(0.48f, 0.2f), new Color(0.08f, 0.12f, 0.17f, 1f), 35);
            CreateSprite(root.transform, "Gun Handle", new Vector2(-0.16f, -0.28f), new Vector2(0.28f, 0.52f), new Color(0.42f, 0.2f, 0.1f, 1f), 34, -14f);
            AddLine(root.transform, "Gun Crayon Outline", new[]
            {
                new Vector2(-0.48f, -0.08f), new Vector2(0.72f, -0.08f), new Vector2(0.72f, 0.28f),
                new Vector2(-0.48f, 0.28f), new Vector2(-0.48f, -0.08f)
            }, 0.055f, new Color(0.03f, 0.05f, 0.08f, 1f), 38);

            StageGun gun = root.AddComponent<StageGun>();
            AddMetadata(root, data);
            return root;
        }

        private void Start()
        {
            system = StageGunSystem.Ensure(transform);
        }

        public void SetHolder(PlayerCarryController value)
        {
            holder = value;
            if (system == null) system = StageGunSystem.Ensure(transform);
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
            Vector2 origin = transform.TransformPoint(new Vector2(0.78f, 0.12f));
            Vector2 direction = (aimWorld - origin).normalized;
            if (direction.sqrMagnitude < 0.1f) return;
            nextFireAt = Time.time + FireInterval;
            if (system == null) system = StageGunSystem.Ensure(transform);
            system?.RequestFire(GetObjectId(), origin, direction);
        }

        private string GetObjectId()
        {
            StageEditorObject marker = GetComponent<StageEditorObject>();
            return marker != null ? marker.objectId : gameObject.name;
        }

        private static void AddMetadata(GameObject root, StageObjectData data)
        {
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
        }

        internal static GameObject CreateSprite(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order, float rotation = 0f)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(position.x, position.y, -0.03f);
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            obj.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = StageSurvivalController.GetSquareSpriteForChallenges();
            renderer.color = color;
            renderer.sortingOrder = order;
            return obj;
        }

        internal static void AddLine(Transform parent, string name, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageBulletBreakableWall : MonoBehaviour
    {
        private StageBombBreakableWall damage;
        private TextMesh countText;

        public string ObjectId => damage != null ? damage.ObjectId : gameObject.name;
        public int CurrentHits => damage != null ? damage.CurrentHits : 0;
        public bool IsBroken => damage != null && damage.IsBroken;

        public void Configure(StageBombBreakableWall source, Vector2 size)
        {
            damage = source;
            CreateBadge(size);
            RefreshBadge();
        }

        public int HitByBullet(Vector2 point)
        {
            if (damage == null || damage.IsBroken) return CurrentHits;
            damage.HitByBomb(point);
            RefreshBadge();
            return damage.CurrentHits;
        }

        public void ApplyNetworkHits(int hits, Vector2 point)
        {
            if (damage == null) return;
            damage.ApplyNetworkDamage(hits, point);
            RefreshBadge();
        }

        private void CreateBadge(Vector2 size)
        {
            GameObject badge = new GameObject("Bullet Wall Requirement");
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = new Vector3(0f, 0f, -0.16f);
            float unit = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.34f, 0.18f, 0.38f);
            StageGun.CreateSprite(badge.transform, "Bullet Badge Back", Vector2.zero,
                new Vector2(unit * 3.8f, unit * 1.35f), new Color(0.73f, 0.9f, 1f, 0.92f), 31);
            GameObject bullet = StageGun.CreateSprite(badge.transform, "Bullet Mark",
                new Vector2(-unit * 0.95f, 0f), new Vector2(unit * 1.05f, unit * 0.35f), new Color(0.12f, 0.25f, 0.42f, 1f), 32);
            bullet.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            GameObject textObject = new GameObject("Bullet Count");
            textObject.transform.SetParent(badge.transform, false);
            textObject.transform.localPosition = new Vector3(unit * 0.55f, 0f, -0.02f);
            countText = textObject.AddComponent<TextMesh>();
            countText.anchor = TextAnchor.MiddleCenter;
            countText.alignment = TextAlignment.Center;
            countText.fontSize = 50;
            countText.characterSize = unit * 0.18f;
            countText.color = new Color(0.06f, 0.15f, 0.28f, 1f);
            Font font = StageSurvivalController.FindHandwrittenFont();
            if (font != null)
            {
                countText.font = font;
                textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            textObject.GetComponent<MeshRenderer>().sortingOrder = 33;
        }

        private void RefreshBadge()
        {
            if (countText != null && damage != null)
                countText.text = "×" + Mathf.Max(0, damage.RequiredHits - damage.CurrentHits);
        }
    }

    public sealed class StageGunSystem : MonoBehaviour
    {
        private const string SystemId = "stage_gun_system";
        private const string FireRequestKind = "gun_fire_request";
        private const string FireKind = "gun_fire";
        private const string WallKind = "gun_wall_state";
        private const string ReflectKind = "gun_bullet_reflect";
        private const string EndKind = "gun_bullet_end";

        [System.Serializable]
        private sealed class FireData
        {
            public int Sequence;
            public string GunId;
            public Vector2 Origin;
            public Vector2 Direction;
        }

        [System.Serializable]
        private sealed class WallData
        {
            public string WallId;
            public int Hits;
            public Vector2 Point;
        }

        [System.Serializable]
        private sealed class BulletMotionData
        {
            public int Sequence;
            public Vector2 Position;
            public Vector2 Direction;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private int shotSequence;
        private int lastShotSequence;
        private readonly Dictionary<int, StageGunBullet> bullets = new Dictionary<int, StageGunBullet>();

        public static StageGunSystem Ensure(Transform context)
        {
            if (context == null) return null;
            StageGimmickSyncManager sync = context.GetComponentInParent<StageGimmickSyncManager>();
            Transform root = sync != null ? sync.transform : context.root;
            StageGunSystem system = root.GetComponent<StageGunSystem>();
            return system != null ? system : root.gameObject.AddComponent<StageGunSystem>();
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

        public void RequestFire(string gunId, Vector2 origin, Vector2 direction)
        {
            FireData data = new FireData { GunId = gunId, Origin = origin, Direction = direction.normalized };
            if (IsOnline() && !HasAuthority())
            {
                Send(FireRequestKind, data);
                return;
            }
            ConfirmFire(data);
        }

        private void ConfirmFire(FireData data)
        {
            StageRicochetChallengeController challenge = Object.FindFirstObjectByType<StageRicochetChallengeController>();
            if (challenge != null && !challenge.TryConsumeShot()) return;
            data.Sequence = ++shotSequence;
            StageGunBullet.Create(transform, this, data.Sequence, data.Origin, data.Direction, true);
            GameSfx.PlayAt(SfxId.CannonFire, data.Origin, 0.66f);
            if (IsOnline()) Send(FireKind, data);
        }

        internal void HitWall(StageBulletBreakableWall wall, Vector2 point)
        {
            if (!HasAuthority() || wall == null || wall.IsBroken) return;
            int hits = wall.HitByBullet(point);
            GameSfx.PlayAt(wall.IsBroken ? SfxId.BombWallBreak : SfxId.EnemyShellBounce, point, 0.72f);
            if (IsOnline()) Send(WallKind, new WallData { WallId = wall.ObjectId, Hits = hits, Point = point });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != SystemId) return;
            if (message.Kind == FireRequestKind && HasAuthority())
            {
                FireData data = JsonUtility.FromJson<FireData>(message.Json);
                if (data != null) ConfirmFire(data);
            }
            else if (message.Kind == FireKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                FireData data = JsonUtility.FromJson<FireData>(message.Json);
                if (data == null || data.Sequence <= lastShotSequence) return;
                lastShotSequence = data.Sequence;
                StageGunBullet.Create(transform, this, data.Sequence, data.Origin, data.Direction, false);
                GameSfx.PlayAt(SfxId.CannonFire, data.Origin, 0.66f);
            }
            else if (message.Kind == ReflectKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                BulletMotionData data = JsonUtility.FromJson<BulletMotionData>(message.Json);
                if (data != null && bullets.TryGetValue(data.Sequence, out StageGunBullet bullet) && bullet != null)
                    bullet.ApplyNetworkReflection(data.Position, data.Direction);
            }
            else if (message.Kind == EndKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                BulletMotionData data = JsonUtility.FromJson<BulletMotionData>(message.Json);
                if (data != null && bullets.TryGetValue(data.Sequence, out StageGunBullet bullet) && bullet != null)
                    bullet.ApplyNetworkEnd();
            }
            else if (message.Kind == WallKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                WallData data = JsonUtility.FromJson<WallData>(message.Json);
                if (data == null) return;
                StageBulletBreakableWall[] walls = Object.FindObjectsByType<StageBulletBreakableWall>(FindObjectsSortMode.None);
                for (int i = 0; i < walls.Length; i++)
                    if (walls[i] != null && walls[i].ObjectId == data.WallId)
                    {
                        walls[i].ApplyNetworkHits(data.Hits, data.Point);
                        break;
                    }
            }
        }

        private void Send<T>(string kind, T value)
        {
            if (onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = SystemId,
                Kind = kind,
                Json = JsonUtility.ToJson(value)
            });
        }

        internal void RegisterBullet(int sequence, StageGunBullet bullet)
        {
            if (bullet != null) bullets[sequence] = bullet;
        }

        internal void UnregisterBullet(int sequence, StageGunBullet bullet)
        {
            if (bullets.TryGetValue(sequence, out StageGunBullet current) && current == bullet) bullets.Remove(sequence);
        }

        internal void BroadcastReflection(int sequence, Vector2 position, Vector2 direction)
        {
            if (IsOnline() && HasAuthority())
                Send(ReflectKind, new BulletMotionData { Sequence = sequence, Position = position, Direction = direction });
        }

        internal void BroadcastEnd(int sequence, Vector2 position)
        {
            if (IsOnline() && HasAuthority())
                Send(EndKind, new BulletMotionData { Sequence = sequence, Position = position });
        }

        private bool IsOnline() => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority() => !IsOnline() || stageManager.IsOnlineStageHost;

        private bool IsHost(string id)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == id) return true;
            return false;
        }
    }

    public sealed class StageGunBullet : MonoBehaviour
    {
        private const float Speed = 23f;
        private StageGunSystem system;
        private StageRicochetChallengeController ricochetChallenge;
        private Vector2 direction;
        private bool authoritative;
        private bool ending;
        private float life;
        private int sequence;
        private int reflectionCount;
        private PlayerController2D lastReflectPlayer;
        private float lastReflectAt;

        public static void Create(Transform parent, StageGunSystem system, int sequence, Vector2 origin, Vector2 direction, bool authoritative)
        {
            GameObject root = new GameObject("Crayon Gun Bullet");
            root.transform.SetParent(parent, false);
            root.transform.position = origin;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            StageGun.CreateSprite(root.transform, "Bullet", Vector2.zero, new Vector2(0.42f, 0.16f), new Color(0.08f, 0.35f, 0.75f, 1f), 47);
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0.01f;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(0.1f, 0.55f, 1f, 0.72f);
            trail.endColor = new Color(0.1f, 0.55f, 1f, 0f);
            trail.sortingOrder = 46;
            StageGunBullet bullet = root.AddComponent<StageGunBullet>();
            bullet.system = system;
            bullet.sequence = sequence;
            bullet.direction = direction.normalized;
            bullet.authoritative = authoritative;
            bullet.ricochetChallenge = Object.FindFirstObjectByType<StageRicochetChallengeController>();
            bullet.system?.RegisterBullet(sequence, bullet);
            if (authoritative) bullet.ricochetChallenge?.RegisterBullet(bullet);
        }

        private void Update()
        {
            float distance = Speed * Time.deltaTime;
            if (authoritative && TryHit(distance))
            {
                EndAuthoritative();
                return;
            }
            transform.position += (Vector3)(direction * distance);
            life += Time.deltaTime;
            if (life >= 3f)
            {
                if (authoritative) EndAuthoritative();
                else ApplyNetworkEnd();
            }
        }

        private bool TryHit(float distance)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, distance + 0.15f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<StageGun>() != null) continue;

                PlayerController2D player = collider.GetComponentInParent<PlayerController2D>();
                if (player != null)
                {
                    if (ricochetChallenge == null || !ricochetChallenge.IsRoundActive) continue;
                    if (player == lastReflectPlayer && Time.time - lastReflectAt < 0.08f) continue;
                    Vector2 normal = hits[i].normal.sqrMagnitude > 0.2f ? hits[i].normal.normalized : -direction;
                    direction = Vector2.Reflect(direction, normal).normalized;
                    transform.position = hits[i].point + direction * 0.12f;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    reflectionCount++;
                    lastReflectPlayer = player;
                    lastReflectAt = Time.time;
                    ricochetChallenge.NotifyReflection(hits[i].point);
                    system?.BroadcastReflection(sequence, transform.position, direction);
                    return false;
                }

                StageRicochetBulletPassage passage = collider.GetComponentInParent<StageRicochetBulletPassage>();
                if (passage != null && passage.AllowsBullet) continue;

                StageRicochetTarget ricochetTarget = collider.GetComponentInParent<StageRicochetTarget>();
                if (ricochetTarget != null)
                {
                    ricochetTarget.Hit(reflectionCount, hits[i].point);
                    return true;
                }
                StageValueCrate valueCrate = collider.GetComponentInParent<StageValueCrate>();
                if (valueCrate != null)
                {
                    valueCrate.Hit(hits[i].point);
                    return true;
                }
                StageBulletBreakableWall wall = collider.GetComponentInParent<StageBulletBreakableWall>();
                if (wall != null)
                {
                    system?.HitWall(wall, hits[i].point);
                    return true;
                }
                StageBossHitbox boss = collider.GetComponentInParent<StageBossHitbox>();
                if (boss != null)
                {
                    boss.HitByBullet(hits[i].point);
                    return true;
                }
                StageBossBomber bomber = collider.GetComponentInParent<StageBossBomber>();
                if (bomber != null)
                {
                    bomber.HitByBullet(hits[i].point);
                    return true;
                }
                StageEnemyCharacter enemy = collider.GetComponentInParent<StageEnemyCharacter>();
                if (enemy != null)
                {
                    StageTowerDefenseEnemyHealth defenseEnemy = enemy.GetComponent<StageTowerDefenseEnemyHealth>();
                    if (defenseEnemy != null) defenseEnemy.HitByBullet(hits[i].point);
                    else enemy.RequestDefeat();
                    GameSfx.PlayAt(SfxId.EnemyDefeat, hits[i].point, 0.72f);
                    return true;
                }
                return true;
            }
            return false;
        }

        public void ApplyNetworkReflection(Vector2 position, Vector2 reflectedDirection)
        {
            if (authoritative || ending) return;
            transform.position = position;
            direction = reflectedDirection.sqrMagnitude > 0.01f ? reflectedDirection.normalized : direction;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public void ApplyNetworkEnd()
        {
            if (ending) return;
            ending = true;
            Destroy(gameObject);
        }

        private void EndAuthoritative()
        {
            if (ending) return;
            ending = true;
            system?.BroadcastEnd(sequence, transform.position);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            system?.UnregisterBullet(sequence, this);
            if (authoritative) ricochetChallenge?.UnregisterBullet(this);
        }
    }

    public static class StageSpikePlanet
    {
        public static GameObject CreateObject(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId) { name = StageObjectType.SpikePlanet.ToString() };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(Mathf.Max(0.8f, data.size.x), Mathf.Max(0.8f, data.size.y), 1f);
            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.48f;
            trigger.isTrigger = true;
            root.AddComponent<StageSpikeHazard>();
            GameObject core = new GameObject("Spike Planet Core");
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer renderer = core.AddComponent<SpriteRenderer>();
            renderer.sprite = StageSurvivalController.GetCircleSprite();
            renderer.color = new Color(0.62f, 0.18f, 0.58f, 0.94f);
            renderer.sortingOrder = 24;
            Color ink = new Color(0.3f, 0.03f, 0.28f, 1f);
            const int spikes = 16;
            for (int i = 0; i < spikes; i++)
            {
                float angle = i / (float)spikes * Mathf.PI * 2f;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new Vector2(-radial.y, radial.x) * 0.11f;
                StageGun.AddLine(root.transform, "Planet Spike", new[]
                {
                    radial * 0.36f - tangent, radial * 0.61f, radial * 0.36f + tangent
                }, 0.045f, ink, 26);
            }
            StageGun.AddLine(root.transform, "Planet Ring", CreateCirclePoints(24, 0.37f), 0.045f, ink, 27);
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

        private static Vector2[] CreateCirclePoints(int count, float radius)
        {
            Vector2[] points = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }
    }
}
