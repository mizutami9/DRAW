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
        private Vector2 heldAimWorld;
        private bool hasHeldAim;

        public PlayerCarryController Holder => holder;
        public Vector2 MuzzleWorldPosition => transform.TransformPoint(new Vector2(0.82f, 0.08f));

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

            if (!TryCreateResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/handgun",
                "Colored Pencil Handgun",
                new Vector2(1.68f, 0.92f),
                38))
            {
            CreateSprite(root.transform, "Gun Body", new Vector2(0.02f, 0.08f), new Vector2(0.95f, 0.42f), new Color(0.17f, 0.25f, 0.34f, 1f), 34);
            CreateSprite(root.transform, "Gun Barrel", new Vector2(0.52f, 0.14f), new Vector2(0.48f, 0.2f), new Color(0.08f, 0.12f, 0.17f, 1f), 35);
            CreateSprite(root.transform, "Gun Handle", new Vector2(-0.16f, -0.28f), new Vector2(0.28f, 0.52f), new Color(0.42f, 0.2f, 0.1f, 1f), 34, -14f);
            AddLine(root.transform, "Gun Crayon Outline", new[]
            {
                new Vector2(-0.48f, -0.08f), new Vector2(0.72f, -0.08f), new Vector2(0.72f, 0.28f),
                new Vector2(-0.48f, 0.28f), new Vector2(-0.48f, -0.08f)
            }, 0.055f, new Color(0.03f, 0.05f, 0.08f, 1f), 38);
            }

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
            heldAimWorld = aimWorld;
            hasHeldAim = true;
            Vector2 direction = aimWorld - (Vector2)anchor;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public bool TryGetCurrentShotRay(out Vector2 origin, out Vector2 direction)
        {
            origin = MuzzleWorldPosition;
            direction = hasHeldAim
                ? heldAimWorld - origin
                : (Vector2)transform.right;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.zero;
                return false;
            }
            direction.Normalize();
            return true;
        }

        public void TryFire(Vector2 aimWorld)
        {
            if (holder == null || Time.time < nextFireAt) return;
            heldAimWorld = aimWorld;
            hasHeldAim = true;
            if (!TryGetCurrentShotRay(out Vector2 origin, out Vector2 direction)) return;
            nextFireAt = Time.time + FireInterval;
            if (system == null) system = StageGunSystem.Ensure(transform);
            system?.RequestFire(GetObjectId(), origin, direction,
                holder != null ? holder.GetComponent<PlayerController2D>() : null);
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
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return obj;
        }

        internal static bool TryCreateResourceSprite(
            Transform parent,
            string resourcePath,
            string name,
            Vector2 localSize,
            int sortingOrder)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (parent == null || sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                return false;
            }

            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0f, -0.035f);
            visual.transform.localScale = new Vector3(
                localSize.x / sprite.bounds.size.x,
                localSize.y / sprite.bounds.size.y,
                1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return true;
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
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
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
            Font font = DoodleRuntimeAssets.HandwrittenFont;
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
        private const string SnapshotKind = "gun_bullet_snapshot";

        [System.Serializable]
        private sealed class FireData
        {
            public int Sequence;
            public string GunId;
            public string OwnerPlayerId;
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
            public string OwnerPlayerId;
            public Vector2 Position;
            public Vector2 Direction;
        }

        [System.Serializable]
        private sealed class BulletSnapshotData
        {
            public BulletMotionData[] Bullets;
        }

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private int shotSequence;
        private int lastShotSequence;
        private readonly Dictionary<int, StageGunBullet> bullets = new Dictionary<int, StageGunBullet>();
        private float nextSnapshotAt;

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

        private void Update()
        {
            if (!IsOnline() || !HasAuthority() || Time.unscaledTime < nextSnapshotAt) return;
            nextSnapshotAt = Time.unscaledTime + 0.75f;
            BroadcastBulletSnapshot();
        }

        public void RequestFire(string gunId, Vector2 origin, Vector2 direction, PlayerController2D owner)
        {
            FireData data = new FireData
            {
                GunId = gunId,
                OwnerPlayerId = ResolvePlayerId(owner),
                Origin = origin,
                Direction = direction.normalized
            };
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
            StageGunBullet.Create(transform, this, data.Sequence, data.OwnerPlayerId, data.Origin, data.Direction, true);
            GameSfx.PlayAt(SfxId.GunShot, data.Origin, 0.66f);
            if (IsOnline()) Send(FireKind, data);
        }

        internal void HitWall(StageBulletBreakableWall wall, Vector2 point)
        {
            if (!HasAuthority() || wall == null || wall.IsBroken) return;
            int hits = wall.HitByBullet(point);
            GameSfx.PlayAt(wall.IsBroken ? SfxId.BombWallBreak : SfxId.Ricochet, point, 0.72f);
            if (IsOnline()) Send(WallKind, new WallData { WallId = wall.ObjectId, Hits = hits, Point = point });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != SystemId) return;
            if (message.Kind == FireRequestKind && HasAuthority())
            {
                FireData data = JsonUtility.FromJson<FireData>(message.Json);
                if (data != null)
                {
                    // The transport-authenticated sender, not a client supplied
                    // value, owns this shot.
                    data.OwnerPlayerId = message.PlayerId;
                    ConfirmFire(data);
                }
            }
            else if (message.Kind == FireKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                FireData data = JsonUtility.FromJson<FireData>(message.Json);
                if (data == null || data.Sequence <= lastShotSequence) return;
                lastShotSequence = data.Sequence;
                StageGunBullet.Create(transform, this, data.Sequence, data.OwnerPlayerId, data.Origin, data.Direction, false);
                GameSfx.PlayAt(SfxId.GunShot, data.Origin, 0.66f);
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
            else if (message.Kind == SnapshotKind && !HasAuthority() && IsHost(message.PlayerId))
            {
                ApplyBulletSnapshot(JsonUtility.FromJson<BulletSnapshotData>(message.Json));
            }
        }

        private void BroadcastBulletSnapshot()
        {
            List<BulletMotionData> states = new List<BulletMotionData>(bullets.Count);
            foreach (KeyValuePair<int, StageGunBullet> pair in bullets)
            {
                StageGunBullet bullet = pair.Value;
                if (bullet == null || bullet.IsEnding) continue;
                states.Add(new BulletMotionData
                {
                    Sequence = pair.Key,
                    OwnerPlayerId = bullet.OwnerPlayerId,
                    Position = bullet.transform.position,
                    Direction = bullet.Direction
                });
            }
            Send(SnapshotKind, new BulletSnapshotData { Bullets = states.ToArray() });
        }

        private void ApplyBulletSnapshot(BulletSnapshotData snapshot)
        {
            if (snapshot?.Bullets == null) return;
            HashSet<int> active = new HashSet<int>();
            for (int i = 0; i < snapshot.Bullets.Length; i++)
            {
                BulletMotionData state = snapshot.Bullets[i];
                if (state == null || state.Sequence <= 0) continue;
                active.Add(state.Sequence);
                lastShotSequence = Mathf.Max(lastShotSequence, state.Sequence);
                if (!bullets.TryGetValue(state.Sequence, out StageGunBullet bullet) || bullet == null)
                {
                    StageGunBullet.Create(transform, this, state.Sequence, state.OwnerPlayerId,
                        state.Position, state.Direction, false);
                    bullets.TryGetValue(state.Sequence, out bullet);
                }
                bullet?.ApplyNetworkReflection(state.Position, state.Direction);
            }

            List<StageGunBullet> stale = new List<StageGunBullet>();
            foreach (KeyValuePair<int, StageGunBullet> pair in bullets)
                if (!active.Contains(pair.Key) && pair.Value != null) stale.Add(pair.Value);
            for (int i = 0; i < stale.Count; i++) stale[i].ApplyNetworkEnd();
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

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return string.Empty;
            return IsOnline() ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
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
        private string ownerPlayerId;
        private StageManager stageManager;
        private PlayerController2D lastReflectPlayer;
        private float lastReflectAt;

        internal Vector2 Direction => direction;
        internal string OwnerPlayerId => ownerPlayerId;
        internal bool IsEnding => ending;

        public static void Create(Transform parent, StageGunSystem system, int sequence, string ownerPlayerId,
            Vector2 origin, Vector2 direction, bool authoritative)
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
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.startColor = new Color(0.1f, 0.55f, 1f, 0.72f);
            trail.endColor = new Color(0.1f, 0.55f, 1f, 0f);
            trail.sortingOrder = 46;
            StageGunBullet bullet = root.AddComponent<StageGunBullet>();
            bullet.system = system;
            bullet.sequence = sequence;
            bullet.ownerPlayerId = ownerPlayerId;
            bullet.direction = direction.normalized;
            bullet.authoritative = authoritative;
            bullet.stageManager = Object.FindFirstObjectByType<StageManager>();
            bullet.ricochetChallenge = Object.FindFirstObjectByType<StageRicochetChallengeController>();
            bullet.system?.RegisterBullet(sequence, bullet);
            if (authoritative) bullet.ricochetChallenge?.RegisterBullet(bullet);
        }

        private void Update()
        {
            float distance = Speed * Time.deltaTime;
            bool reflected = false;
            if (authoritative && TryHit(distance, out reflected))
            {
                EndAuthoritative();
                return;
            }
            // TryHit already places a reflected bullet just beyond the impact
            // point. Advancing a second full frame here made it skip the next
            // thin wall and disagree with the aiming prediction at low FPS.
            if (!reflected) transform.position += (Vector3)(direction * distance);
            life += Time.deltaTime;
            if (life >= 3f)
            {
                if (authoritative) EndAuthoritative();
                else ApplyNetworkEnd();
            }
        }

        private bool TryHit(float distance, out bool reflected)
        {
            reflected = false;
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, distance + 0.15f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                StageMovingGauntletGhost gauntletGhost = collider != null
                    ? collider.GetComponentInParent<StageMovingGauntletGhost>()
                    : null;
                if (collider == null || collider.GetComponentInParent<StageGun>() != null) continue;

                StageBalloonTarget balloon = collider.GetComponentInParent<StageBalloonTarget>();
                if (balloon != null)
                {
                    balloon.Hit(hits[i].point);
                    return true;
                }

                if (collider.isTrigger && gauntletGhost == null) continue;

                PlayerController2D player = collider.GetComponentInParent<PlayerController2D>();
                if (player != null)
                {
                    if (ricochetChallenge == null || !ricochetChallenge.IsRoundActive) continue;
                    string playerId = stageManager != null && stageManager.IsOnlineStageActive
                        ? stageManager.GetOnlinePlayerId(player)
                        : "local_" + player.GetInstanceID();
                    if (!string.IsNullOrEmpty(ownerPlayerId) && playerId == ownerPlayerId) continue;
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
                    reflected = true;
                    return false;
                }

                StageRicochetBulletPassage passage = collider.GetComponentInParent<StageRicochetBulletPassage>();
                if (passage != null && passage.AllowsBullet) continue;

                PlatformEffector2D oneWay = collider.GetComponentInParent<PlatformEffector2D>();
                if (oneWay != null && oneWay.useOneWay) continue;

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
            if (!StageGun.TryCreateResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/spike-planet",
                "Colored Pencil Spike Planet",
                new Vector2(0.96f, 0.96f),
                27))
            {
            GameObject core = new GameObject("Spike Planet Core");
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer renderer = core.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
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
            }
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

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBalloonGalleryController : MonoBehaviour
    {
        private const string StageId = "1-2";
        private const string StateKind = "balloon_gallery_state";
        private const string BarrierFloorId = "obj_cd4ea0b0ec634b7d";
        private const int BalloonsPerPlayer = 5;
        private const float BalloonVisualScale = 0.55f;

        [System.Serializable]
        private sealed class GalleryState
        {
            public int Sequence;
            public int BrokenMask;
        }

        private StageBalloonTarget[] balloons = new StageBalloonTarget[0];
        private readonly List<GameObject> barrierObjects = new List<GameObject>();
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private int brokenMask;
        private int sequence;
        private int appliedSequence = -1;
        private int balloonCount = BalloonsPerPlayer;
        private float nextSnapshotAt;
        private bool barrierClearedApplied;

        private bool HasAuthority => stageManager == null
            || !stageManager.IsOnlineStageActive
            || stageManager.IsOnlineStageHost;

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

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            int playerCount = Mathf.Clamp(
                stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1,
                1,
                4);
            balloonCount = BalloonsPerPlayer * playerCount;
            balloons = new StageBalloonTarget[balloonCount];
            FindBarrierObjects();
            CreateBalloons();
            ApplyState();
            if (HasAuthority) BroadcastState();
        }

        private void Update()
        {
            if (stageManager != null && stageManager.CurrentStageId != StageId) return;
            if (!HasAuthority || !IsOnline() || Time.unscaledTime < nextSnapshotAt) return;
            nextSnapshotAt = Time.unscaledTime + 1f;
            BroadcastState();
        }

        private void CreateBalloons()
        {
            Vector2[] positions =
            {
                new Vector2(16f, -29f), new Vector2(29.5f, -37.5f),
                new Vector2(22f, -33f), new Vector2(28f, -29.5f), new Vector2(18.5f, -38.5f),
                new Vector2(14.5f, -35.5f), new Vector2(24f, -28f),
                new Vector2(31f, -32f), new Vector2(20f, -36f), new Vector2(27f, -39f),
                new Vector2(14f, -32f), new Vector2(22f, -29.5f),
                new Vector2(31f, -35.5f), new Vector2(25f, -37f), new Vector2(18f, -26.8f),
                new Vector2(15.5f, -39f), new Vector2(25f, -34.5f),
                new Vector2(30f, -27f), new Vector2(18.5f, -31.5f), new Vector2(28f, -33.5f)
            };
            Color[] colors =
            {
                new Color(0.98f, 0.27f, 0.33f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f),
                new Color(0.2f, 0.72f, 0.98f, 1f),
                new Color(0.28f, 0.78f, 0.38f, 1f),
                new Color(0.72f, 0.35f, 0.92f, 1f)
            };

            for (int index = 0; index < balloonCount; index++)
            {
                int kind = index % BalloonsPerPlayer;
                int group = index / BalloonsPerPlayer;
                StageBalloonTarget.Motion motion = kind < 2
                    ? StageBalloonTarget.Motion.Fixed
                    : kind < 4 ? StageBalloonTarget.Motion.Oscillate : StageBalloonTarget.Motion.Blink;
                Vector2 travel = kind == 2
                    ? new Vector2(1.35f, 0f)
                    : kind == 3 ? new Vector2(0f, 1.45f)
                    : kind == 4 ? new Vector2(0.55f, 0.22f) : Vector2.zero;
                float speed = kind == 2 ? 1.15f : kind == 3 ? 0.92f : kind == 4 ? 0.75f : 0f;
                Color color = Color.Lerp(colors[kind], Color.white, group * 0.055f);
                CreateBalloon(index, positions[index], color, motion, travel, speed,
                    0.4f + group * 0.83f + kind * 0.37f);
            }
        }

        private void CreateBalloon(int index, Vector2 position, Color color,
            StageBalloonTarget.Motion motion, Vector2 travel, float speed, float phase)
        {
            GameObject root = new GameObject("1-2 Balloon " + (index + 1));
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * BalloonVisualScale;
            balloons[index] = root.AddComponent<StageBalloonTarget>();
            balloons[index].Configure(this, index, color, motion, travel, speed, phase);
        }

        private void FindBarrierObjects()
        {
            barrierObjects.Clear();
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            StageEditorObject explicitFloor = null;
            float spikeMinX = float.PositiveInfinity;
            float spikeMaxX = float.NegativeInfinity;
            float spikeY = 0f;
            int spikeCount = 0;

            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker == null) continue;
                if (marker.objectId == BarrierFloorId) explicitFloor = marker;
                if (marker.type != StageObjectType.Spike) continue;
                barrierObjects.Add(marker.gameObject);
                spikeMinX = Mathf.Min(spikeMinX, marker.transform.position.x);
                spikeMaxX = Mathf.Max(spikeMaxX, marker.transform.position.x);
                spikeY += marker.transform.position.y;
                spikeCount++;
            }

            if (explicitFloor != null)
            {
                barrierObjects.Add(explicitFloor.gameObject);
                return;
            }

            if (spikeCount == 0) return;
            spikeY /= spikeCount;
            StageEditorObject bestFloor = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < markers.Length; i++)
            {
                StageEditorObject marker = markers[i];
                if (marker == null || marker.type != StageObjectType.Platform) continue;
                float halfWidth = marker.size.x * 0.5f;
                float left = marker.transform.position.x - halfWidth;
                float right = marker.transform.position.x + halfWidth;
                if (right < spikeMaxX - 0.5f || left > spikeMinX + 0.5f) continue;
                float score = Mathf.Abs(marker.transform.position.y - (spikeY - 1.5f));
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFloor = marker;
                }
            }
            if (bestFloor != null && bestScore < 3f) barrierObjects.Add(bestFloor.gameObject);
        }

        internal void HitBalloon(int index, Vector2 hitPoint)
        {
            if (!HasAuthority || index < 0 || index >= balloonCount) return;
            int bit = 1 << index;
            if ((brokenMask & bit) != 0) return;
            brokenMask |= bit;
            sequence++;
            balloons[index]?.Pop(hitPoint);
            ApplyBarrierState();
            BroadcastState();
        }

        private void ApplyState()
        {
            for (int i = 0; i < balloons.Length; i++)
            {
                if ((brokenMask & (1 << i)) != 0) balloons[i]?.Pop(balloons[i].transform.position);
            }
            ApplyBarrierState();
        }

        private void ApplyBarrierState()
        {
            bool cleared = brokenMask == (1 << balloonCount) - 1;
            for (int i = 0; i < barrierObjects.Count; i++)
            {
                if (barrierObjects[i] != null) barrierObjects[i].SetActive(!cleared);
            }
            if (cleared && !barrierClearedApplied)
            {
                GameSfx.PlayAt(SfxId.BombWallBreak, new Vector2(20f, -43f), 0.82f);
            }
            barrierClearedApplied = cleared;
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId || message.Kind != StateKind
                || HasAuthority || !IsHost(message.PlayerId)) return;
            GalleryState state = JsonUtility.FromJson<GalleryState>(message.Json);
            if (state == null || state.Sequence < appliedSequence) return;
            appliedSequence = state.Sequence;
            brokenMask = state.BrokenMask;
            ApplyState();
        }

        private void BroadcastState()
        {
            if (!IsOnline() || onlineManager == null || !HasAuthority) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new GalleryState
                {
                    Sequence = sequence,
                    BrokenMask = brokenMask
                })
            });
        }

        private bool IsOnline()
        {
            return stageManager != null && stageManager.IsOnlineStageActive;
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            }
            return false;
        }
    }

    public sealed class StageBalloonTarget : MonoBehaviour
    {
        public enum Motion
        {
            Fixed,
            Oscillate,
            Blink
        }

        private StageBalloonGalleryController controller;
        private int index;
        private Motion motion;
        private Vector2 origin;
        private Vector2 travel;
        private float speed;
        private float phase;
        private bool popped;
        private CircleCollider2D hitbox;
        private Renderer[] renderers;

        internal void Configure(StageBalloonGalleryController owner, int balloonIndex, Color color,
            Motion motionMode, Vector2 movement, float movementSpeed, float movementPhase)
        {
            controller = owner;
            index = balloonIndex;
            motion = motionMode;
            origin = transform.position;
            travel = movement;
            speed = movementSpeed;
            phase = movementPhase;
            BuildVisual(color);
        }

        private void Update()
        {
            if (popped) return;
            float clock = Time.time * Mathf.Max(0.01f, speed) + phase;
            if (motion == Motion.Oscillate || motion == Motion.Blink)
            {
                transform.position = origin + travel * Mathf.Sin(clock);
            }
            if (motion != Motion.Blink) return;

            float cycle = Mathf.Repeat(Time.time + phase, 4.2f);
            float alpha = cycle < 2.55f ? 1f
                : cycle < 2.9f ? Mathf.InverseLerp(2.9f, 2.55f, cycle)
                : cycle < 3.65f ? 0f
                : Mathf.InverseLerp(3.65f, 4.2f, cycle);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                SpriteRenderer spriteRenderer = renderers[i] as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    Color tint = spriteRenderer.color;
                    tint.a = alpha;
                    spriteRenderer.color = tint;
                }
                else
                {
                    renderers[i].enabled = alpha >= 0.5f;
                }
            }
            hitbox.enabled = alpha >= 0.78f;
        }

        internal void Hit(Vector2 point)
        {
            if (!popped) controller?.HitBalloon(index, point);
        }

        internal void Pop(Vector2 hitPoint)
        {
            if (popped) return;
            popped = true;
            if (hitbox != null) hitbox.enabled = false;
            CreatePopFragments(hitPoint);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }
            GameSfx.PlayAt(SfxId.CoinCollect, hitPoint, 0.72f);
        }

        private void BuildVisual(Color color)
        {
            GameObject body = new GameObject("Crayon Balloon Body");
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(1.35f, 1.62f, 1f);
            SpriteRenderer fill = body.AddComponent<SpriteRenderer>();
            fill.sprite = DoodleRuntimeAssets.CircleSprite;
            fill.color = color;
            fill.sortingOrder = 45;

            Color outline = new Color(Mathf.Max(0f, color.r - 0.34f), Mathf.Max(0f, color.g - 0.34f),
                Mathf.Max(0f, color.b - 0.34f), 1f);
            StageGun.AddLine(transform, "Balloon Crayon Outline", CreateEllipsePoints(26, 0.69f, 0.82f),
                0.075f, outline, 47);
            StageGun.AddLine(transform, "Balloon Highlight", new[]
            {
                new Vector2(-0.3f, 0.46f), new Vector2(-0.42f, 0.2f), new Vector2(-0.44f, -0.02f)
            }, 0.085f, new Color(1f, 1f, 1f, 0.72f), 48);
            StageGun.AddLine(transform, "Balloon Knot", new[]
            {
                new Vector2(-0.13f, -0.78f), new Vector2(0f, -0.98f), new Vector2(0.14f, -0.78f)
            }, 0.065f, outline, 47);
            StageGun.AddLine(transform, "Balloon String", new[]
            {
                new Vector2(0f, -0.94f), new Vector2(0.12f, -1.35f), new Vector2(-0.08f, -1.75f)
            }, 0.035f, new Color(0.22f, 0.2f, 0.18f, 0.86f), 43);

            hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.radius = 0.72f;
            hitbox.offset = new Vector2(0f, 0.03f);
            hitbox.isTrigger = true;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void CreatePopFragments(Vector2 hitPoint)
        {
            Transform parent = transform.parent;
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f + index * 0.31f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageBalloonPopFragment.Create(parent, hitPoint, direction);
            }
        }

        private static Vector2[] CreateEllipsePoints(int count, float radiusX, float radiusY)
        {
            Vector2[] points = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float wobble = 1f + Mathf.Sin(i * 2.7f) * 0.018f;
                points[i] = new Vector2(Mathf.Cos(angle) * radiusX * wobble,
                    Mathf.Sin(angle) * radiusY * wobble);
            }
            return points;
        }
    }

    internal sealed class StageBalloonPopFragment : MonoBehaviour
    {
        private Vector2 velocity;
        private float life;
        private SpriteRenderer sprite;

        internal static void Create(Transform parent, Vector2 position, Vector2 direction)
        {
            GameObject fragment = new GameObject("Balloon Crayon Pop");
            fragment.transform.SetParent(parent, false);
            fragment.transform.position = position;
            fragment.transform.localScale = Vector3.one * 0.13f;
            SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(1f, 0.48f + direction.y * 0.12f, 0.16f, 0.9f);
            renderer.sortingOrder = 50;
            StageBalloonPopFragment effect = fragment.AddComponent<StageBalloonPopFragment>();
            effect.velocity = direction * 3.2f + Vector2.up * 0.8f;
            effect.sprite = renderer;
        }

        private void Update()
        {
            life += Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            velocity += Vector2.down * (3.5f * Time.deltaTime);
            Color color = sprite.color;
            color.a = Mathf.Clamp01(1f - life / 0.55f);
            sprite.color = color;
            if (life >= 0.55f) Destroy(gameObject);
        }
    }
}
