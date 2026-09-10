using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBalloonCourseController : MonoBehaviour
    {
        private const string StageId = "3-2";
        private const string StateKind = "balloon_course_state";
        private const int BalloonsPerSection = 5;
        private const int BalloonCount = BalloonsPerSection * 3;
        private const float BalloonVisualScale = 0.55f;

        private static readonly string[] FloorIds =
        {
            "obj_6cf99e183c0b4af0", // editor list 45
            "obj_ac58a2c705ee4a95", // editor list 46
            "obj_3abf7097dc314536"  // editor list 47
        };

        [System.Serializable]
        private sealed class CourseState
        {
            public int Sequence;
            public int BrokenMask;
        }

        private readonly StageBalloonTarget[] balloons = new StageBalloonTarget[BalloonCount];
        private readonly GameObject[] floors = new GameObject[3];
        private readonly bool[] floorClearedApplied = new bool[3];
        private StageManager stageManager;
        private OnlineManager onlineManager;
        private int brokenMask;
        private int sequence;
        private int appliedSequence = -1;
        private float nextSnapshotAt;

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

            FindFloors();
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

        private void FindFloors()
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                StageEditorObject marker = markers[markerIndex];
                if (marker == null) continue;
                for (int floorIndex = 0; floorIndex < FloorIds.Length; floorIndex++)
                {
                    if (marker.objectId == FloorIds[floorIndex]) floors[floorIndex] = marker.gameObject;
                }
            }
        }

        private void CreateBalloons()
        {
            Color[] colors =
            {
                new Color(0.98f, 0.27f, 0.33f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f),
                new Color(0.2f, 0.72f, 0.98f, 1f),
                new Color(0.28f, 0.78f, 0.38f, 1f),
                new Color(0.72f, 0.35f, 0.92f, 1f)
            };

            // Section 1: fixed targets strictly inside the vertical shaft between
            // editor floors 21 (right edge x=81) and 45 (left edge x=97.5).
            // Spread them vertically to use the full height of the room.
            Vector2[] firstPositions =
            {
                new Vector2(84.0f, 25.0f), new Vector2(95.5f, 19.0f),
                new Vector2(87.0f, 13.0f), new Vector2(93.0f, 7.0f),
                new Vector2(89.5f, 1.0f)
            };
            for (int i = 0; i < BalloonsPerSection; i++)
            {
                CreateBalloon(i, firstPositions[i], colors[i], StageBalloonTarget.Motion.Fixed,
                    Vector2.zero, 0f, i * 0.43f);
            }

            // Section 2: every motion path stays inside the shaft between floor
            // 45 (right edge x=99.5) and floor 46 (left edge x=111.5).
            Vector2[] secondPositions =
            {
                new Vector2(103.0f, 25.0f), new Vector2(107.5f, 19.0f),
                new Vector2(105.0f, 13.0f), new Vector2(108.0f, 7.0f),
                new Vector2(104.0f, 1.0f)
            };
            Vector2[] secondTravel =
            {
                new Vector2(2.3f, 0f), new Vector2(3.0f, 0.7f),
                new Vector2(4.5f, 0f), new Vector2(2.5f, 0.9f),
                new Vector2(3.0f, 0.65f)
            };
            for (int i = 0; i < BalloonsPerSection; i++)
            {
                int index = BalloonsPerSection + i;
                CreateBalloon(index, secondPositions[i], colors[(i + 2) % colors.Length],
                    StageBalloonTarget.Motion.Oscillate, secondTravel[i], 1.25f + i * 0.11f, i * 0.77f);
            }

            // Section 3: fast targets stay high in the right-side shaft between
            // floor 46 (right edge x=113.5) and boundary 29 (inner edge x=130.5).
            Vector2[] thirdPositions =
            {
                new Vector2(118.0f, 14.0f), new Vector2(123.0f, 17.0f),
                new Vector2(125.0f, 20.0f), new Vector2(119.5f, 23.0f),
                new Vector2(125.0f, 26.0f)
            };
            Vector2[] thirdTravel =
            {
                new Vector2(3.5f, 0.8f), new Vector2(5.5f, 0f),
                new Vector2(3.5f, 0.65f), new Vector2(5.0f, 0f),
                new Vector2(3.0f, 0.7f)
            };
            for (int i = 0; i < BalloonsPerSection; i++)
            {
                int index = BalloonsPerSection * 2 + i;
                CreateBalloon(index, thirdPositions[i], colors[(i + 4) % colors.Length],
                    StageBalloonTarget.Motion.Oscillate, thirdTravel[i], 2.75f + i * 0.18f, i * 0.91f);
            }
        }

        private void CreateBalloon(int index, Vector2 position, Color color,
            StageBalloonTarget.Motion motion, Vector2 travel, float speed, float phase)
        {
            GameObject root = new GameObject("3-2 Balloon " + (index + 1));
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * BalloonVisualScale;
            StageBalloonTarget balloon = root.AddComponent<StageBalloonTarget>();
            balloon.Configure(HitBalloon, index, color, motion, travel, speed, phase);
            balloons[index] = balloon;
        }

        private void HitBalloon(int index, Vector2 hitPoint)
        {
            if (!HasAuthority || index < 0 || index >= BalloonCount) return;
            int bit = 1 << index;
            if ((brokenMask & bit) != 0) return;
            brokenMask |= bit;
            sequence++;
            balloons[index]?.Pop(hitPoint);
            ApplyFloorStates();
            BroadcastState();
        }

        private void ApplyState()
        {
            for (int i = 0; i < balloons.Length; i++)
            {
                if ((brokenMask & (1 << i)) != 0) balloons[i]?.Pop(balloons[i].transform.position);
            }
            ApplyFloorStates();
        }

        private void ApplyFloorStates()
        {
            for (int section = 0; section < floors.Length; section++)
            {
                int sectionMask = ((1 << BalloonsPerSection) - 1) << (section * BalloonsPerSection);
                bool cleared = (brokenMask & sectionMask) == sectionMask;
                GameObject floor = floors[section];
                if (floor != null) floor.SetActive(!cleared);
                if (cleared && !floorClearedApplied[section])
                {
                    Vector2 effectPosition = floor != null ? floor.transform.position : Vector2.zero;
                    GameSfx.PlayAt(SfxId.BombWallBreak, effectPosition, 0.82f);
                    if (section == 2) CreateFloorBreakEffect(effectPosition);
                }
                floorClearedApplied[section] = cleared;
            }
        }

        private void CreateFloorBreakEffect(Vector2 center)
        {
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageBalloonPopFragment.Create(
                    transform,
                    center + Vector2.right * ((i % 7) - 3) * 0.7f,
                    direction);
            }
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId || message.Kind != StateKind
                || HasAuthority || !IsHost(message.PlayerId)) return;
            CourseState state = JsonUtility.FromJson<CourseState>(message.Json);
            if (state == null || state.Sequence < appliedSequence) return;
            appliedSequence = state.Sequence;
            sequence = state.Sequence;
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
                Json = JsonUtility.ToJson(new CourseState
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
}
