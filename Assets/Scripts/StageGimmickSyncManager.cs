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

        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private float transformSendRate = 12f;

        private readonly Dictionary<string, SyncTransformEntry> transformEntries = new Dictionary<string, SyncTransformEntry>();
        private readonly Dictionary<string, string> ownersByObjectId = new Dictionary<string, string>();
        private readonly HashSet<string> locallyHeldObjectIds = new HashSet<string>();
        private readonly Dictionary<string, StageCrumblingFloor> crumblingFloors =
            new Dictionary<string, StageCrumblingFloor>();
        private readonly Dictionary<string, DropperBoxSpawnState> dropperBoxStates =
            new Dictionary<string, DropperBoxSpawnState>();
        private readonly Dictionary<string, GameObject> dropperBoxes =
            new Dictionary<string, GameObject>();
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
            }
        }

        public GameObject SpawnDropperBox(
            string objectId,
            StageObjectType type,
            Vector2 position,
            float size,
            float rotation = 0f)
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
                Rotation = rotation
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
            OwnershipState release = new OwnershipState
            {
                OwnerPlayerId = onlineManager.LocalPlayerId,
                ReleaseVelocity = releaseVelocity
            };
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

            if (data.Kind == KindTransform)
            {
                if (IsHost)
                {
                    ownersByObjectId.TryGetValue(data.ObjectId, out string ownerId);
                    if (ownerId != data.PlayerId)
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

            if (transformEntries.TryGetValue(objectId, out SyncTransformEntry entry) && entry?.Body != null)
            {
                entry.Body.linearVelocity = release.ReleaseVelocity;
            }

            ownersByObjectId.Remove(objectId);
            BroadcastOwnership(objectId, string.Empty, release.ReleaseVelocity);
        }

        private void BroadcastOwnership(string objectId, string ownerPlayerId, Vector2 releaseVelocity)
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
                    ReleaseVelocity = releaseVelocity
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
                ReleaseDeniedLocalCarry(objectId);
                if (transformEntries.TryGetValue(objectId, out SyncTransformEntry released) && released?.Body != null)
                {
                    released.Body.linearVelocity = state != null ? state.ReleaseVelocity : Vector2.zero;
                }
                return;
            }

            ownersByObjectId[objectId] = state.OwnerPlayerId;
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

            entry.Transform.gameObject.SetActive(state.Active);
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
                transform);
            if (spawned == null)
            {
                return null;
            }

            spawned.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);

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
                transformEntries[id] = new SyncTransformEntry(body.transform, body);
            }
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
            public bool HasSentState;
            public Vector2 LastPosition;
            public Vector2 LastVelocity;
            public float LastRotation;
            public float LastAngularVelocity;
            public bool LastActive;
            public float LastSentTime;

            public SyncTransformEntry(Transform transform, Rigidbody2D body)
            {
                Transform = transform;
                Body = body;
            }
        }
    }
}
