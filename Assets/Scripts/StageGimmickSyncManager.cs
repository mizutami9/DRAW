using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageGimmickSyncManager : MonoBehaviour
    {
        private const string KindLinkRequest = "link_request";
        private const string KindHeldButtonRequest = "held_button_request";
        private const string KindLinkState = "link_state";
        private const string KindTransform = "transform";
        private const string KindOwnershipRequest = "ownership_request";
        private const string KindOwnershipState = "ownership_state";
        private const string KindOwnershipRelease = "ownership_release";
        private const string KindCrumblingFloorRequest = "crumbling_floor_request";
        private const string KindCrumblingFloorState = "crumbling_floor_state";
        private const string KindDropperBoxSpawn = "dropper_box_spawn";
        private const string KindDropperBoxRemove = "dropper_box_remove";
        private const string KindBombExplosion = "bomb_explosion";
        private const string KindBombWallDamage = "bomb_wall_damage";
        private const string KindBombArmRequest = "bomb_arm_request";
        private const string KindBombArmState = "bomb_arm_state";
        private const string KindPlacedEnemyDefeatRequest = "placed_enemy_defeat_request";
        private const string KindPlacedEnemyDefeatState = "placed_enemy_defeat_state";

        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private float transformSendRate = 20f;

        private readonly Dictionary<string, SyncTransformEntry> transformEntries = new Dictionary<string, SyncTransformEntry>();
        private readonly Dictionary<string, string> ownersByObjectId = new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastTransformSequenceBySenderAndObject =
            new Dictionary<string, int>();
        private readonly HashSet<string> locallyHeldObjectIds = new HashSet<string>();
        private readonly Dictionary<string, OwnershipState> pendingLocalReleases =
            new Dictionary<string, OwnershipState>();
        private readonly Dictionary<string, StageCrumblingFloor> crumblingFloors =
            new Dictionary<string, StageCrumblingFloor>();
        private readonly Dictionary<string, DropperBoxSpawnState> dropperBoxStates =
            new Dictionary<string, DropperBoxSpawnState>();
        private readonly Dictionary<string, GameObject> dropperBoxes =
            new Dictionary<string, GameObject>();
        private readonly Dictionary<string, BombExplosionState> explodedPlacedBombs =
            new Dictionary<string, BombExplosionState>();
        private readonly HashSet<string> appliedBombExplosions = new HashSet<string>();
        private readonly Dictionary<string, BombWallDamageState> bombWallDamageStates =
            new Dictionary<string, BombWallDamageState>();
        private readonly HashSet<string> armedPickupBombs = new HashSet<string>();
        private readonly HashSet<string> defeatedPlacedEnemies = new HashSet<string>();
        private StageGimmickLinkController linkController;
        private StageObjectFactory objectFactory;
        private float nextTransformSendTime;
        private float nextSnapshotTime;
        private int sequence;

        [System.Serializable]
        private sealed class OwnershipState
        {
            public string OwnerPlayerId;
            public Vector2 ReleaseVelocity;
            public bool HasReleasePose;
            public Vector2 ReleasePosition;
            public float ReleaseRotation;
        }

        [System.Serializable]
        private sealed class HeldButtonState
        {
            public bool Held;
        }

        [System.Serializable]
        private sealed class DropperBoxSpawnState
        {
            public int BoxType;
            public Vector2 Position;
            public float Size;
            public float Rotation;
            public float FuseSeconds;
            public Vector2 LaunchVelocity;
        }

        [System.Serializable]
        private sealed class BombExplosionState
        {
            public Vector2 Position;
            public float Radius;
        }

        [System.Serializable]
        private sealed class BombWallDamageState
        {
            public int Hits;
            public Vector2 BlastCenter;
        }

        public bool IsOnlineActive => onlineManager != null
            && onlineManager.CurrentLobby != null
            && (onlineManager.State == OnlineConnectionState.InLobby
                || onlineManager.State == OnlineConnectionState.Matching
                || onlineManager.State == OnlineConnectionState.Playing);

        public bool IsHost => IsLocalHost();
        public bool ShouldAskHost => IsOnlineActive && !IsHost;

        private void Awake()
        {
            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            linkController = GetComponent<StageGimmickLinkController>();
            RebuildTransformEntries();
            RebuildCrumblingFloorEntries();
        }

        private void OnEnable()
        {
            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (onlineManager != null)
            {
                onlineManager.GimmickDataReceived += ApplyNetworkGimmickData;
            }
        }

        private void OnDisable()
        {
            if (onlineManager != null)
            {
                onlineManager.GimmickDataReceived -= ApplyNetworkGimmickData;
            }
        }

        private void Start()
        {
            linkController = GetComponent<StageGimmickLinkController>();
            RebuildTransformEntries();
            RebuildCrumblingFloorEntries();
        }

        private void Update()
        {
            if (!IsOnlineActive || Time.unscaledTime < nextTransformSendTime)
            {
                return;
            }

            nextTransformSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, transformSendRate);
            SendTransformStates();
            if (IsHost && Time.unscaledTime >= nextSnapshotTime)
            {
                nextSnapshotTime = Time.unscaledTime + 1f;
                ResolveLinkController()?.BroadcastAllStates();
                BroadcastOwnershipSnapshot();
                BroadcastCrumblingFloorStates();
                BroadcastDropperBoxSnapshot();
                BroadcastBombSnapshot();
                BroadcastPlacedEnemySnapshot();
            }
        }

        private void FixedUpdate()
        {
            if (!IsOnlineActive)
            {
                return;
            }

            foreach (KeyValuePair<string, SyncTransformEntry> pair in transformEntries)
            {
                SyncTransformEntry entry = pair.Value;
                ownersByObjectId.TryGetValue(pair.Key, out string ownerId);
                bool remotelyOwned = !string.IsNullOrEmpty(ownerId)
                    && ownerId != onlineManager.LocalPlayerId;
                bool networkDrivenPlatform = ShouldAskHost && entry != null && entry.IsHostDrivenPlatform;
                if (entry == null || !entry.HasNetworkTarget || (!remotelyOwned && !networkDrivenPlatform))
                {
                    continue;
                }

                entry.ApplyNetworkTarget(Time.fixedDeltaTime);
            }
        }

        public void DetonateBomb(string objectId, Vector2 position, float radius)
        {
            if (string.IsNullOrEmpty(objectId) || IsOnlineActive && !IsHost)
            {
                return;
            }

            bool spawnedByDropper = dropperBoxStates.ContainsKey(objectId);
            BombExplosionState state = new BombExplosionState
            {
                Position = position,
                Radius = Mathf.Max(0.5f, radius)
            };
            ApplyBombExplosion(objectId, state, true);
            if (!spawnedByDropper)
            {
                explodedPlacedBombs[objectId] = state;
            }
            if (IsOnlineActive)
            {
                SendBombExplosion(objectId, state);
            }
        }

        public void RegisterBombWallDamage(string objectId, int hits, Vector2 blastCenter)
        {
            if (string.IsNullOrEmpty(objectId) || IsOnlineActive && !IsHost)
            {
                return;
            }
            bombWallDamageStates[objectId] = new BombWallDamageState
            {
                Hits = Mathf.Clamp(hits, 0, 5),
                BlastCenter = blastCenter
            };
        }

        public void RequestArmBomb(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return;
            }
            if (!IsOnlineActive || IsHost)
            {
                ApplyBombArm(objectId);
                if (IsOnlineActive)
                {
                    SendBombArmState(objectId);
                }
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindBombArmRequest,
                Json = "{}"
            });
        }

        public void RequestPlacedEnemyDefeat(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            if (!IsOnlineActive || IsHost)
            {
                ApplyPlacedEnemyDefeat(objectId);
                if (IsOnlineActive) SendPlacedEnemyDefeatState(objectId);
                return;
            }
            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindPlacedEnemyDefeatRequest,
                Json = "{}"
            });
        }

        public GameObject SpawnDropperBox(
            string objectId,
            StageObjectType type,
            Vector2 position,
            float size,
            float rotation = 0f,
            float fuseSeconds = 5f,
            Vector2 launchVelocity = default)
        {
            if (string.IsNullOrEmpty(objectId) || (IsOnlineActive && !IsHost))
            {
                return null;
            }

            DropperBoxSpawnState state = new DropperBoxSpawnState
            {
                BoxType = (int)type,
                Position = position,
                Size = size,
                Rotation = rotation,
                FuseSeconds = Mathf.Clamp(fuseSeconds > 0f ? fuseSeconds : 5f, 1f, 15f),
                LaunchVelocity = launchVelocity
            };
            GameObject spawned = ApplyDropperBoxSpawn(objectId, state);
            if (spawned != null && IsOnlineActive)
            {
                SendDropperBoxSpawn(objectId, state);
            }
            return spawned;
        }

        public void RemoveDropperBox(string objectId)
        {
            if (string.IsNullOrEmpty(objectId) || (IsOnlineActive && !IsHost))
            {
                return;
            }

            ApplyDropperBoxRemove(objectId);
            if (IsOnlineActive)
            {
                Send(new OnlineGimmickData
                {
                    ObjectId = objectId,
                    Kind = KindDropperBoxRemove,
                    Json = "{}"
                });
            }
        }

        public void RequestCrumblingFloor(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return;
            }

            if (!IsOnlineActive || IsHost)
            {
                ActivateCrumblingFloor(objectId, IsOnlineActive && IsHost);
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindCrumblingFloorRequest,
                Json = "{}"
            });
        }

        public void NotifyCrumblingFloorChanged(StageCrumblingFloor floor)
        {
            if (floor == null || !IsOnlineActive || !IsHost)
            {
                return;
            }

            BroadcastCrumblingFloorState(floor);
        }

        public void BeginLocalObjectCarry(Transform target)
        {
            if (!IsOnlineActive || !TryGetSyncId(target, out string objectId))
            {
                return;
            }

            locallyHeldObjectIds.Add(objectId);
            pendingLocalReleases.Remove(objectId);
            if (IsHost)
            {
                GrantOwnership(objectId, onlineManager.LocalPlayerId);
            }
            else
            {
                Send(new OnlineGimmickData { ObjectId = objectId, Kind = KindOwnershipRequest, Json = "{}" });
            }
        }

        public void EndLocalObjectCarry(Transform target, Vector2 releaseVelocity)
        {
            if (!IsOnlineActive || !TryGetSyncId(target, out string objectId))
            {
                return;
            }

            locallyHeldObjectIds.Remove(objectId);
            Rigidbody2D releaseBody = target.GetComponent<Rigidbody2D>();
            OwnershipState release = new OwnershipState
            {
                OwnerPlayerId = onlineManager.LocalPlayerId,
                ReleaseVelocity = releaseVelocity,
                HasReleasePose = true,
                ReleasePosition = releaseBody != null ? releaseBody.position : (Vector2)target.position,
                ReleaseRotation = releaseBody != null ? releaseBody.rotation : target.eulerAngles.z
            };
            if (!IsHost)
            {
                // Keep the complete release frame until the host confirms it. When
                // the carrier is walking, sending only velocity can launch the
                // host copy from an older position inside a player or the floor.
                pendingLocalReleases[objectId] = release;
            }
            if (IsHost)
            {
                ReleaseOwnership(objectId, release);
            }
            else
            {
                Send(new OnlineGimmickData
                {
                    ObjectId = objectId,
                    Kind = KindOwnershipRelease,
                    Json = JsonUtility.ToJson(release)
                });
            }
        }

        public void RequestLinkActivation(string sourceObjectId)
        {
            if (string.IsNullOrEmpty(sourceObjectId) || !IsOnlineActive)
            {
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = sourceObjectId,
                Kind = KindLinkRequest,
                Json = "{}"
            });
        }

        public void RequestHeldButtonState(string sourceObjectId, bool held)
        {
            if (string.IsNullOrEmpty(sourceObjectId) || !IsOnlineActive)
            {
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = sourceObjectId,
                Kind = KindHeldButtonRequest,
                Json = JsonUtility.ToJson(new HeldButtonState { Held = held })
            });
        }

        public void BroadcastLinkState(string sourceObjectId, OnlineLinkGimmickState state)
        {
            if (string.IsNullOrEmpty(sourceObjectId) || state == null || !IsOnlineActive || !IsHost)
            {
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = sourceObjectId,
                Kind = KindLinkState,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void SendTransformStates()
        {
            foreach (KeyValuePair<string, SyncTransformEntry> pair in transformEntries)
            {
                SyncTransformEntry entry = pair.Value;
                if (entry == null || entry.Transform == null)
                {
                    continue;
                }

                ownersByObjectId.TryGetValue(pair.Key, out string ownerId);
                if (IsHost)
                {
                    if (!string.IsNullOrEmpty(ownerId) && ownerId != onlineManager.LocalPlayerId)
                    {
                        continue;
                    }
                }
                else if (ownerId != onlineManager.LocalPlayerId || !locallyHeldObjectIds.Contains(pair.Key))
                {
                    continue;
                }

                Rigidbody2D body = entry.Body;
                Vector2 position = body != null ? body.position : (Vector2)entry.Transform.position;
                Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
                float rotation = body != null ? body.rotation : entry.Transform.eulerAngles.z;
                float angularVelocity = body != null ? body.angularVelocity : 0f;
                bool active = entry.Transform.gameObject.activeInHierarchy;
                bool locallyHeld = locallyHeldObjectIds.Contains(pair.Key);
                bool changed = !entry.HasSentState
                    || Vector2.SqrMagnitude(position - entry.LastPosition) > 0.0004f
                    || Vector2.SqrMagnitude(velocity - entry.LastVelocity) > 0.0025f
                    || Mathf.Abs(Mathf.DeltaAngle(rotation, entry.LastRotation)) > 0.1f
                    || Mathf.Abs(angularVelocity - entry.LastAngularVelocity) > 0.1f
                    || active != entry.LastActive;

                // A stage with many sleeping crates should not emit a packet for
                // every crate twelve times a second. Send motion, final resting
                // states, ownership-driven carries, and an occasional recovery
                // snapshot only.
                if (!locallyHeld && !changed && Time.unscaledTime - entry.LastSentTime < 2f)
                {
                    continue;
                }

                OnlineTransformGimmickState state = new OnlineTransformGimmickState
                {
                    Position = position,
                    Velocity = velocity,
                    Rotation = rotation,
                    AngularVelocity = angularVelocity,
                    Active = active
                };

                Send(new OnlineGimmickData
                {
                    ObjectId = pair.Key,
                    Kind = KindTransform,
                    Json = JsonUtility.ToJson(state)
                });
                entry.HasSentState = true;
                entry.LastPosition = position;
                entry.LastVelocity = velocity;
                entry.LastRotation = rotation;
                entry.LastAngularVelocity = angularVelocity;
                entry.LastActive = active;
                entry.LastSentTime = Time.unscaledTime;
            }
        }

        private void ApplyNetworkGimmickData(OnlineGimmickData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Kind) || string.IsNullOrEmpty(data.ObjectId))
            {
                return;
            }

            if (onlineManager != null && data.PlayerId == onlineManager.LocalPlayerId)
            {
                return;
            }

            if (data.Kind == KindLinkRequest)
            {
                if (IsHost)
                {
                    ResolveLinkController()?.HandleActivationRequest(data.ObjectId);
                }

                return;
            }

            if (data.Kind == KindLinkState)
            {
                OnlineLinkGimmickState state = JsonUtility.FromJson<OnlineLinkGimmickState>(data.Json);
                ResolveLinkController()?.ApplyNetworkState(data.ObjectId, state);
                return;
            }

            if (data.Kind == KindHeldButtonRequest)
            {
                if (IsHost)
                {
                    HeldButtonState state =
                        JsonUtility.FromJson<HeldButtonState>(data.Json);
                    ResolveLinkController()?.HandleHeldButtonRequest(
                        data.ObjectId,
                        state != null && state.Held);
                }
                return;
            }

            if (data.Kind == KindCrumblingFloorRequest)
            {
                if (IsHost)
                {
                    ActivateCrumblingFloor(data.ObjectId, true);
                }
                return;
            }

            if (data.Kind == KindCrumblingFloorState)
            {
                if (!crumblingFloors.TryGetValue(data.ObjectId, out StageCrumblingFloor floor))
                {
                    RebuildCrumblingFloorEntries();
                    crumblingFloors.TryGetValue(data.ObjectId, out floor);
                }
                floor?.ApplyNetworkState(JsonUtility.FromJson<OnlineCrumblingFloorState>(data.Json));
                return;
            }

            if (data.Kind == KindDropperBoxSpawn)
            {
                if (IsHost || !IsLobbyHost(data.PlayerId))
                {
                    return;
                }
                ApplyDropperBoxSpawn(
                    data.ObjectId,
                    JsonUtility.FromJson<DropperBoxSpawnState>(data.Json));
                return;
            }

            if (data.Kind == KindDropperBoxRemove)
            {
                if (IsHost || !IsLobbyHost(data.PlayerId))
                {
                    return;
                }
                ApplyDropperBoxRemove(data.ObjectId);
                return;
            }

            if (data.Kind == KindBombExplosion)
            {
                if (IsHost || !IsLobbyHost(data.PlayerId))
                {
                    return;
                }
                ApplyBombExplosion(
                    data.ObjectId,
                    JsonUtility.FromJson<BombExplosionState>(data.Json),
                    true);
                return;
            }

            if (data.Kind == KindBombArmRequest)
            {
                if (IsHost)
                {
                    ApplyBombArm(data.ObjectId);
                    SendBombArmState(data.ObjectId);
                }
                return;
            }

            if (data.Kind == KindBombArmState)
            {
                if (IsHost || !IsLobbyHost(data.PlayerId))
                {
                    return;
                }
                ApplyBombArm(data.ObjectId);
                return;
            }

            if (data.Kind == KindPlacedEnemyDefeatRequest)
            {
                if (IsHost)
                {
                    ApplyPlacedEnemyDefeat(data.ObjectId);
                    SendPlacedEnemyDefeatState(data.ObjectId);
                }
                return;
            }

            if (data.Kind == KindPlacedEnemyDefeatState)
            {
                if (!IsHost && IsLobbyHost(data.PlayerId)) ApplyPlacedEnemyDefeat(data.ObjectId);
                return;
            }

            if (data.Kind == KindBombWallDamage)
            {
                if (IsHost || !IsLobbyHost(data.PlayerId))
                {
                    return;
                }
                BombWallDamageState state = JsonUtility.FromJson<BombWallDamageState>(data.Json);
                ApplyBombWallDamage(data.ObjectId, state);
                return;
            }

            if (data.Kind == KindTransform)
            {
                string transformSequenceKey = (data.PlayerId ?? string.Empty) + "|" + (data.ObjectId ?? string.Empty);
                if (data.Sequence > 0
                    && lastTransformSequenceBySenderAndObject.TryGetValue(transformSequenceKey, out int lastSequence)
                    && data.Sequence <= lastSequence)
                {
                    return;
                }
                if (data.Sequence > 0)
                {
                    lastTransformSequenceBySenderAndObject[transformSequenceKey] = data.Sequence;
                }

                if (IsHost)
                {
                    ownersByObjectId.TryGetValue(data.ObjectId, out string ownerId);
                    if (ownerId != data.PlayerId)
                    {
                        return;
                    }
                }
                else if (!IsLobbyHost(data.PlayerId))
                {
                    // A participant may only drive an object while the host's
                    // confirmed ownership state names that participant. This also
                    // rejects delayed unreliable transforms after a throw/release.
                    ownersByObjectId.TryGetValue(data.ObjectId, out string confirmedOwnerId);
                    if (confirmedOwnerId != data.PlayerId)
                    {
                        return;
                    }
                }

                ApplyTransformState(data.ObjectId, JsonUtility.FromJson<OnlineTransformGimmickState>(data.Json));
                return;
            }

            if (data.Kind == KindOwnershipRequest)
            {
                if (IsHost
                    && (!ownersByObjectId.TryGetValue(data.ObjectId, out string currentOwner)
                        || string.IsNullOrEmpty(currentOwner))
                    && CanGrantOwnership(data.ObjectId, data.PlayerId))
                {
                    GrantOwnership(data.ObjectId, data.PlayerId);
                }
                else if (IsHost)
                {
                    ownersByObjectId.TryGetValue(data.ObjectId, out string owner);
                    BroadcastOwnership(data.ObjectId, owner, Vector2.zero);
                }
                return;
            }

            if (data.Kind == KindOwnershipRelease)
            {
                if (IsHost)
                {
                    OwnershipState release = JsonUtility.FromJson<OwnershipState>(data.Json);
                    if (ownersByObjectId.TryGetValue(data.ObjectId, out string ownerId) && ownerId == data.PlayerId)
                    {
                        ReleaseOwnership(data.ObjectId, release);
                    }
                }
                return;
            }

            if (data.Kind == KindOwnershipState)
            {
                OwnershipState state = JsonUtility.FromJson<OwnershipState>(data.Json);
                ApplyOwnershipState(data.ObjectId, state);
            }
        }

        private void GrantOwnership(string objectId, string ownerPlayerId)
        {
            if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(ownerPlayerId) || !transformEntries.ContainsKey(objectId))
            {
                return;
            }

            ownersByObjectId[objectId] = ownerPlayerId;
            if (transformEntries.TryGetValue(objectId, out SyncTransformEntry entry) && entry != null)
            {
                if (ownerPlayerId != onlineManager.LocalPlayerId) entry.BeginRemoteOwnership();
                else entry.EndRemoteOwnership(Vector2.zero);
            }
            BroadcastOwnership(objectId, ownerPlayerId, Vector2.zero);
        }

        private bool CanGrantOwnership(string objectId, string playerId)
        {
            if (!transformEntries.TryGetValue(objectId, out SyncTransformEntry entry)
                || entry?.Transform == null)
            {
                return false;
            }

            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            PlayerController2D player = stageManager != null ? stageManager.GetOnlinePlayerController(playerId) : null;
            if (player == null)
            {
                return false;
            }

            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(false);
            Collider2D[] objectColliders = entry.Transform.GetComponentsInChildren<Collider2D>(false);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || playerCollider.isTrigger)
                {
                    continue;
                }
                for (int j = 0; j < objectColliders.Length; j++)
                {
                    Collider2D objectCollider = objectColliders[j];
                    if (objectCollider == null || objectCollider.isTrigger)
                    {
                        continue;
                    }
                    closest = Mathf.Min(closest, Mathf.Max(0f, playerCollider.Distance(objectCollider).distance));
                }
            }

            return closest <= 1.15f;
        }

        private void ReleaseOwnership(string objectId, OwnershipState release)
        {
            if (release == null)
            {
                release = new OwnershipState();
            }

            if (transformEntries.TryGetValue(objectId, out SyncTransformEntry entry) && entry != null)
            {
                ApplyReleasePose(entry, release);
                entry.EndRemoteOwnership(release.ReleaseVelocity);
            }

            ownersByObjectId.Remove(objectId);
            BroadcastOwnership(
                objectId,
                string.Empty,
                release.ReleaseVelocity,
                release.HasReleasePose,
                release.ReleasePosition,
                release.ReleaseRotation);
        }

        private static void ApplyReleasePose(SyncTransformEntry entry, OwnershipState release)
        {
            if (entry == null || entry.Transform == null || release == null || !release.HasReleasePose)
            {
                return;
            }

            if (entry.Body != null)
            {
                entry.Body.position = release.ReleasePosition;
                entry.Body.rotation = release.ReleaseRotation;
            }
            else
            {
                entry.Transform.position = release.ReleasePosition;
                entry.Transform.rotation = Quaternion.Euler(0f, 0f, release.ReleaseRotation);
            }
            Physics2D.SyncTransforms();
        }

        private void BroadcastOwnership(
            string objectId,
            string ownerPlayerId,
            Vector2 releaseVelocity,
            bool hasReleasePose = false,
            Vector2 releasePosition = default,
            float releaseRotation = 0f)
        {
            if (!IsHost)
            {
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindOwnershipState,
                Json = JsonUtility.ToJson(new OwnershipState
                {
                    OwnerPlayerId = ownerPlayerId,
                    ReleaseVelocity = releaseVelocity,
                    HasReleasePose = hasReleasePose,
                    ReleasePosition = releasePosition,
                    ReleaseRotation = releaseRotation
                })
            });
        }

        private void BroadcastOwnershipSnapshot()
        {
            HashSet<string> lobbyPlayers = new HashSet<string>();
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId))
                    {
                        lobbyPlayers.Add(players[i].PlayerId);
                    }
                }
            }

            List<string> abandoned = new List<string>();
            foreach (KeyValuePair<string, string> pair in ownersByObjectId)
            {
                if (!lobbyPlayers.Contains(pair.Value))
                {
                    abandoned.Add(pair.Key);
                }
            }
            for (int i = 0; i < abandoned.Count; i++)
            {
                ReleaseOwnership(abandoned[i], new OwnershipState());
            }

            foreach (KeyValuePair<string, string> pair in ownersByObjectId)
            {
                BroadcastOwnership(pair.Key, pair.Value, Vector2.zero);
            }
        }

        private void ApplyOwnershipState(string objectId, OwnershipState state)
        {
            if (state == null || string.IsNullOrEmpty(state.OwnerPlayerId))
            {
                ownersByObjectId.Remove(objectId);
                pendingLocalReleases.Remove(objectId);
                ReleaseDeniedLocalCarry(objectId);
                if (transformEntries.TryGetValue(objectId, out SyncTransformEntry released) && released?.Body != null)
                {
                    ApplyReleasePose(released, state);
                    released.EndRemoteOwnership(state != null ? state.ReleaseVelocity : Vector2.zero);
                }
                return;
            }

            ownersByObjectId[objectId] = state.OwnerPlayerId;
            if (transformEntries.TryGetValue(objectId, out SyncTransformEntry entry) && entry != null)
            {
                if (state.OwnerPlayerId != onlineManager.LocalPlayerId)
                {
                    pendingLocalReleases.Remove(objectId);
                    entry.BeginRemoteOwnership();
                }
                else if (!locallyHeldObjectIds.Contains(objectId)
                    && pendingLocalReleases.TryGetValue(objectId, out OwnershipState pendingRelease))
                {
                    // The participant already threw before the host's ownership
                    // grant arrived. Preserve the throw locally and immediately
                    // repeat the release against the now-confirmed ownership.
                    entry.EndRemoteOwnership(pendingRelease.ReleaseVelocity);
                    Send(new OnlineGimmickData
                    {
                        ObjectId = objectId,
                        Kind = KindOwnershipRelease,
                        Json = JsonUtility.ToJson(pendingRelease)
                    });
                }
                else
                {
                    entry.EndRemoteOwnership(Vector2.zero);
                }
            }
            if (state.OwnerPlayerId != onlineManager.LocalPlayerId && locallyHeldObjectIds.Contains(objectId))
            {
                ReleaseDeniedLocalCarry(objectId);
            }
        }

        private void ReleaseDeniedLocalCarry(string objectId)
        {
            if (!locallyHeldObjectIds.Remove(objectId)
                || !transformEntries.TryGetValue(objectId, out SyncTransformEntry denied)
                || denied?.Transform == null)
            {
                return;
            }

            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                carriers[i]?.ReleaseIfHolding(denied.Transform);
            }
        }

        private void ApplyTransformState(string objectId, OnlineTransformGimmickState state)
        {
            if (state == null)
            {
                return;
            }

            if (!transformEntries.TryGetValue(objectId, out SyncTransformEntry entry) || entry == null || entry.Transform == null)
            {
                RebuildTransformEntries();
                if (!transformEntries.TryGetValue(objectId, out entry) || entry == null || entry.Transform == null)
                {
                    return;
                }
            }

            // While a participant is holding an object, or waiting for the host
            // to acknowledge its throw, host snapshots describe an older frame.
            // Applying them here cancels the carried position or release speed.
            if (locallyHeldObjectIds.Contains(objectId)
                || pendingLocalReleases.ContainsKey(objectId))
            {
                return;
            }

            entry.Transform.gameObject.SetActive(state.Active);
            ownersByObjectId.TryGetValue(objectId, out string ownerId);
            bool remotelyOwned = !string.IsNullOrEmpty(ownerId)
                && ownerId != onlineManager.LocalPlayerId;
            if (remotelyOwned)
            {
                entry.BeginRemoteOwnership();
                entry.SetNetworkTarget(state);
                return;
            }
            if (ShouldAskHost && entry.IsHostDrivenPlatform)
            {
                entry.SetNetworkTarget(state);
                return;
            }

            if (entry.Body != null)
            {
                entry.Body.position = state.Position;
                entry.Body.rotation = state.Rotation;
                entry.Body.linearVelocity = state.Velocity;
                entry.Body.angularVelocity = state.AngularVelocity;
            }
            else
            {
                entry.Transform.position = state.Position;
                entry.Transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
            }
        }

        private StageGimmickLinkController ResolveLinkController()
        {
            if (linkController == null)
            {
                linkController = GetComponent<StageGimmickLinkController>();
            }

            return linkController;
        }

        private void Send(OnlineGimmickData data)
        {
            if (onlineManager == null || data == null)
            {
                return;
            }

            data.Sequence = ++sequence;
            onlineManager.SendGimmickData(data);
        }

        private void RebuildTransformEntries()
        {
            transformEntries.Clear();
            StageEditorObject[] objects = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject stageObject = objects[i];
                if (stageObject == null || string.IsNullOrEmpty(stageObject.objectId))
                {
                    continue;
                }

                AddRigidbodiesForObject(stageObject);
            }
        }

        private void RebuildCrumblingFloorEntries()
        {
            crumblingFloors.Clear();
            StageCrumblingFloor[] floors = GetComponentsInChildren<StageCrumblingFloor>(true);
            for (int i = 0; i < floors.Length; i++)
            {
                StageCrumblingFloor floor = floors[i];
                if (floor != null && !string.IsNullOrEmpty(floor.ObjectId))
                {
                    crumblingFloors[floor.ObjectId] = floor;
                }
            }
        }

        private void ActivateCrumblingFloor(string objectId, bool broadcast)
        {
            if (!crumblingFloors.TryGetValue(objectId, out StageCrumblingFloor floor) || floor == null)
            {
                RebuildCrumblingFloorEntries();
                if (!crumblingFloors.TryGetValue(objectId, out floor) || floor == null)
                {
                    return;
                }
            }

            floor.TriggerAuthoritatively();
            if (broadcast)
            {
                BroadcastCrumblingFloorState(floor);
            }
        }

        private void BroadcastCrumblingFloorStates()
        {
            foreach (StageCrumblingFloor floor in crumblingFloors.Values)
            {
                if (floor != null && floor.HasTriggered)
                {
                    BroadcastCrumblingFloorState(floor);
                }
            }
        }

        private void BroadcastCrumblingFloorState(StageCrumblingFloor floor)
        {
            if (floor == null || !IsOnlineActive || !IsHost)
            {
                return;
            }

            Send(new OnlineGimmickData
            {
                ObjectId = floor.ObjectId,
                Kind = KindCrumblingFloorState,
                Json = JsonUtility.ToJson(floor.CreateNetworkState())
            });
        }

        private GameObject ApplyDropperBoxSpawn(string objectId, DropperBoxSpawnState state)
        {
            if (state == null || string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            if (dropperBoxes.TryGetValue(objectId, out GameObject existing) && existing != null)
            {
                dropperBoxStates[objectId] = state;
                return existing;
            }

            if (objectFactory == null)
            {
                objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            }
            if (objectFactory == null)
            {
                return null;
            }

            StageObjectType type = (StageObjectType)state.BoxType;
            GameObject spawned = objectFactory.CreateDroppedBox(
                type,
                objectId,
                state.Position,
                state.Size,
                transform,
                state.FuseSeconds > 0f ? state.FuseSeconds : 5f);
            if (spawned == null)
            {
                return null;
            }

            spawned.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
            Rigidbody2D spawnedBody = spawned.GetComponent<Rigidbody2D>();
            if (spawnedBody != null)
            {
                spawnedBody.linearVelocity = state.LaunchVelocity;
            }

            dropperBoxes[objectId] = spawned;
            dropperBoxStates[objectId] = state;
            StageEditorObject marker = spawned.GetComponent<StageEditorObject>();
            if (marker != null)
            {
                AddRigidbodiesForObject(marker);
            }
            return spawned;
        }

        private void ApplyDropperBoxRemove(string objectId)
        {
            dropperBoxStates.Remove(objectId);
            if (dropperBoxes.TryGetValue(objectId, out GameObject spawned) && spawned != null)
            {
                Destroy(spawned);
            }
            dropperBoxes.Remove(objectId);

            string prefix = objectId + "/";
            List<string> transformIds = new List<string>();
            foreach (string transformId in transformEntries.Keys)
            {
                if (transformId.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    transformIds.Add(transformId);
                }
            }
            for (int i = 0; i < transformIds.Count; i++)
            {
                transformEntries.Remove(transformIds[i]);
                ownersByObjectId.Remove(transformIds[i]);
                locallyHeldObjectIds.Remove(transformIds[i]);
            }
        }

        private void BroadcastDropperBoxSnapshot()
        {
            if (!IsOnlineActive || !IsHost)
            {
                return;
            }

            foreach (KeyValuePair<string, DropperBoxSpawnState> pair in dropperBoxStates)
            {
                if (dropperBoxes.TryGetValue(pair.Key, out GameObject spawned) && spawned != null)
                {
                    SendDropperBoxSpawn(pair.Key, pair.Value);
                }
            }
        }

        private void SendDropperBoxSpawn(string objectId, DropperBoxSpawnState state)
        {
            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindDropperBoxSpawn,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyBombExplosion(string objectId, BombExplosionState state, bool applyGameplay)
        {
            if (state == null || string.IsNullOrEmpty(objectId) || !appliedBombExplosions.Add(objectId))
            {
                return;
            }

            StageBomb[] bombs = Object.FindObjectsByType<StageBomb>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] != null && bombs[i].ObjectId == objectId)
                {
                    bombs[i].ApplyNetworkExplosion(state.Position, state.Radius, applyGameplay);
                    break;
                }
            }

            dropperBoxStates.Remove(objectId);
            dropperBoxes.Remove(objectId);
            armedPickupBombs.Remove(objectId);
        }

        private void ApplyBombArm(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return;
            }
            armedPickupBombs.Add(objectId);
            StageBomb[] bombs = Object.FindObjectsByType<StageBomb>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] != null && bombs[i].ObjectId == objectId)
                {
                    bombs[i].ArmFromNetwork();
                    return;
                }
            }
        }

        private void ApplyBombWallDamage(string objectId, BombWallDamageState state)
        {
            if (state == null)
            {
                return;
            }
            StageBombBreakableWall[] walls = Object.FindObjectsByType<StageBombBreakableWall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] != null && walls[i].ObjectId == objectId)
                {
                    walls[i].ApplyNetworkDamage(state.Hits, state.BlastCenter);
                    return;
                }
            }
        }

        private void BroadcastBombSnapshot()
        {
            if (!IsOnlineActive || !IsHost)
            {
                return;
            }

            foreach (KeyValuePair<string, BombExplosionState> pair in explodedPlacedBombs)
            {
                SendBombExplosion(pair.Key, pair.Value);
            }
            foreach (KeyValuePair<string, BombWallDamageState> pair in bombWallDamageStates)
            {
                Send(new OnlineGimmickData
                {
                    ObjectId = pair.Key,
                    Kind = KindBombWallDamage,
                    Json = JsonUtility.ToJson(pair.Value)
                });
            }
            foreach (string objectId in armedPickupBombs)
            {
                SendBombArmState(objectId);
            }
        }

        private void SendBombExplosion(string objectId, BombExplosionState state)
        {
            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindBombExplosion,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void SendBombArmState(string objectId)
        {
            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindBombArmState,
                Json = "{}"
            });
        }

        private bool TryGetSyncId(Transform target, out string id)
        {
            id = null;
            if (target == null)
            {
                return false;
            }

            StageEditorObject stageObject = target.GetComponentInParent<StageEditorObject>();
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (stageObject == null || body == null || string.IsNullOrEmpty(stageObject.objectId))
            {
                return false;
            }

            id = stageObject.objectId + "/" + GetRelativePath(stageObject.transform, body.transform);
            return transformEntries.ContainsKey(id);
        }

        private void AddRigidbodiesForObject(StageEditorObject stageObject)
        {
            Rigidbody2D[] bodies = stageObject.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D body = bodies[i];
                if (body == null)
                {
                    continue;
                }

                string id = stageObject.objectId + "/" + GetRelativePath(stageObject.transform, body.transform);
                bool hostDrivenPlatform = stageObject.type == StageObjectType.MovingPlatform
                    || stageObject.type == StageObjectType.MovingOneWayPlatform
                    || stageObject.type == StageObjectType.Elevator
                    || IsPlacedEnemyType(stageObject.type);
                transformEntries[id] = new SyncTransformEntry(body.transform, body, hostDrivenPlatform);
            }
        }

        private static bool IsPlacedEnemyType(StageObjectType type)
        {
            return type == StageObjectType.EnemyWalker
                || type == StageObjectType.EnemyJumper
                || type == StageObjectType.EnemyCharger
                || type == StageObjectType.EnemyFlyer
                || type == StageObjectType.EnemyShooter
                || type == StageObjectType.EnemyFlyerZigzag
                || type == StageObjectType.EnemyFlyerOrbit;
        }

        private void ApplyPlacedEnemyDefeat(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            defeatedPlacedEnemies.Add(objectId);
            StageEnemyCharacter[] enemies = Object.FindObjectsByType<StageEnemyCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].ObjectId == objectId)
                {
                    enemies[i].ApplyDefeated();
                    return;
                }
            }
        }

        private void SendPlacedEnemyDefeatState(string objectId)
        {
            Send(new OnlineGimmickData
            {
                ObjectId = objectId,
                Kind = KindPlacedEnemyDefeatState,
                Json = "{}"
            });
        }

        private void BroadcastPlacedEnemySnapshot()
        {
            foreach (string objectId in defeatedPlacedEnemies) SendPlacedEnemyDefeatState(objectId);
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null || root == target)
            {
                return ".";
            }

            Stack<string> names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return names.Count == 0 ? "." : string.Join("/", names.ToArray());
        }

        private bool IsLocalHost()
        {
            if (onlineManager == null || onlineManager.CurrentLobby == null || onlineManager.CurrentLobby.Players == null)
            {
                return false;
            }

            string localId = onlineManager.LocalPlayerId;
            OnlinePlayerInfo[] players = onlineManager.CurrentLobby.Players;
            for (int i = 0; i < players.Length; i++)
            {
                OnlinePlayerInfo player = players[i];
                if (player != null && player.IsHost && player.PlayerId == localId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLobbyHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null || string.IsNullOrEmpty(playerId))
            {
                return false;
            }

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class SyncTransformEntry
        {
            public readonly Transform Transform;
            public readonly Rigidbody2D Body;
            public readonly bool IsHostDrivenPlatform;
            public bool HasSentState;
            public Vector2 LastPosition;
            public Vector2 LastVelocity;
            public float LastRotation;
            public float LastAngularVelocity;
            public bool LastActive;
            public float LastSentTime;
            public bool HasNetworkTarget;
            private Vector2 networkPosition;
            private Vector2 networkVelocity;
            private float networkRotation;
            private float networkAngularVelocity;
            private float networkReceivedAt;
            private bool remoteOwnershipActive;
            private RigidbodyType2D bodyTypeBeforeRemoteOwnership;
            private float gravityBeforeRemoteOwnership;
            private bool freezeRotationBeforeRemoteOwnership;

            public SyncTransformEntry(Transform transform, Rigidbody2D body, bool isHostDrivenPlatform)
            {
                Transform = transform;
                Body = body;
                IsHostDrivenPlatform = isHostDrivenPlatform;
            }

            public void SetNetworkTarget(OnlineTransformGimmickState state)
            {
                networkPosition = state.Position;
                networkVelocity = state.Velocity;
                networkRotation = state.Rotation;
                networkAngularVelocity = state.AngularVelocity;
                networkReceivedAt = Time.unscaledTime;
                HasNetworkTarget = true;
            }

            public void BeginRemoteOwnership()
            {
                if (remoteOwnershipActive || Body == null)
                {
                    return;
                }

                remoteOwnershipActive = true;
                bodyTypeBeforeRemoteOwnership = Body.bodyType;
                gravityBeforeRemoteOwnership = Body.gravityScale;
                freezeRotationBeforeRemoteOwnership = Body.freezeRotation;
                Body.bodyType = RigidbodyType2D.Kinematic;
                Body.gravityScale = 0f;
                Body.linearVelocity = Vector2.zero;
                Body.angularVelocity = 0f;
            }

            public void EndRemoteOwnership(Vector2 releaseVelocity)
            {
                if (Body == null)
                {
                    HasNetworkTarget = false;
                    return;
                }

                if (remoteOwnershipActive)
                {
                    Body.bodyType = bodyTypeBeforeRemoteOwnership;
                    Body.gravityScale = gravityBeforeRemoteOwnership;
                    Body.freezeRotation = freezeRotationBeforeRemoteOwnership;
                    remoteOwnershipActive = false;
                }
                Body.linearVelocity = releaseVelocity;
                Body.angularVelocity = 0f;
                HasNetworkTarget = false;
            }

            public void ApplyNetworkTarget(float deltaTime)
            {
                if (Transform == null)
                {
                    return;
                }

                float age = Mathf.Clamp(Time.unscaledTime - networkReceivedAt, 0f, 0.12f);
                Vector2 predictedPosition = networkPosition + networkVelocity * age;
                float predictedRotation = networkRotation + networkAngularVelocity * age;
                Vector2 currentPosition = Body != null ? Body.position : (Vector2)Transform.position;
                float currentRotation = Body != null ? Body.rotation : Transform.eulerAngles.z;
                float positionError = Vector2.Distance(currentPosition, predictedPosition);
                float blend = positionError > 3f
                    ? 1f
                    : 1f - Mathf.Exp(-22f * Mathf.Max(0.001f, deltaTime));
                Vector2 nextPosition = Vector2.Lerp(currentPosition, predictedPosition, blend);
                float nextRotation = Mathf.LerpAngle(currentRotation, predictedRotation, blend);

                if (Body != null)
                {
                    Body.MovePosition(nextPosition);
                    Body.MoveRotation(nextRotation);
                }
                else
                {
                    Transform.position = nextPosition;
                    Transform.rotation = Quaternion.Euler(0f, 0f, nextRotation);
                }
            }
        }
    }
}
