using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(PlayerAbilityController))]
    public sealed class PlayerCarryController : MonoBehaviour
    {
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private BodyBuilder bodyBuilder;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private LayerMask carryableLayerMask = ~0;
        [SerializeField] private float pickupRadius = 1.7f;
        [SerializeField] private float pickupReach = 0.9f;
        [SerializeField] private float throwSpeed = 22f;
        [SerializeField] private float armInkThrowScale = 0.0175f;
        [SerializeField] private float heldPlayerThrowMultiplier = 1.25f;
        [SerializeField] private float throwAimSpeed = 1.35f;
        [SerializeField] private float throwPreviewLength = 1.8f;
        [SerializeField] private float postThrowCollisionIgnoreTime = 0.22f;
        [SerializeField] private float postThrowCollisionRestoreTimeout = 1.2f;

        // A hand-drawn body can contain far more than 64 segment colliders.
        // A fixed NonAlloc buffer could fill with the player's own long legs
        // before the crate underneath was returned by the physics query.
        private readonly List<Collider2D> pickupHits = new List<Collider2D>(128);
        private readonly List<Collider2D> heldColliders = new List<Collider2D>();
        private readonly List<bool> heldColliderEnabledStates = new List<bool>();
        private readonly List<bool> heldColliderTriggerStates = new List<bool>();
        private readonly List<Renderer> heldRenderers = new List<Renderer>();
        private readonly List<int> heldRendererSortingOrders = new List<int>();
        private readonly List<bool> heldRendererEnabledStates = new List<bool>();
        private Transform heldTransform;
        private CarryableObject heldObject;
        private PlayerController2D heldPlayerController;
        private Rigidbody2D heldBody;
        private RigidbodyType2D previousBodyType;
        private float previousGravityScale;
        private bool previousFreezeRotation;
        private LineRenderer throwPreviewLine;
        private LineRenderer throwPreviewHeadA;
        private LineRenderer throwPreviewHeadB;
        private Material previewMaterial;
        private string heldOnlinePlayerId;
        private StageGimmickSyncManager gimmickSyncManager;
        private ContactFilter2D pickupContactFilter;

        public bool IsHolding => heldTransform != null;

        public bool IsHoldingTarget(Transform target)
        {
            return target != null && heldTransform == target;
        }

        public bool ReleaseIfHolding(Transform target)
        {
            if (target == null || heldTransform != target)
            {
                return false;
            }

            DropHeld(Vector2.zero);
            return true;
        }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController2D>();
            }

            if (abilityController == null)
            {
                abilityController = GetComponent<PlayerAbilityController>();
            }

            if (bodyBuilder == null)
            {
                bodyBuilder = GetComponent<BodyBuilder>();
            }

            if (playerBody == null)
            {
                playerBody = GetComponent<Rigidbody2D>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            carryableLayerMask |= 1 << gameObject.layer;
            pickupContactFilter = new ContactFilter2D();
            pickupContactFilter.SetLayerMask(carryableLayerMask);
            pickupContactFilter.useTriggers = false;
            CreateThrowPreview();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (playerController != null && !playerController.ControlsEnabled)
            {
                return;
            }

            if (!IsHuman())
            {
                DropHeld(Vector2.zero);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (heldTransform == null)
                {
                    TryPickup();
                }
                else
                {
                    ThrowHeld();
                }
            }
        }

        private void LateUpdate()
        {
            if (heldTransform == null)
            {
                SetThrowPreviewVisible(false);
                return;
            }

            Vector3 anchor = GetHoldPosition();
            heldTransform.position = anchor;
            heldTransform.rotation = Quaternion.identity;
            if (heldBody != null)
            {
                heldBody.linearVelocity = Vector2.zero;
                heldBody.angularVelocity = 0f;
            }

            bodyBuilder?.SetCarryPose(true, GetFacingDirection(), anchor);
            UpdateThrowPreview(anchor);
        }

        private void TryPickup()
        {
            Bounds playerBounds;
            if (!TryGetPlayerBounds(out playerBounds))
            {
                playerBounds = new Bounds(transform.position, new Vector3(pickupRadius, pickupRadius, 0f));
            }

            Vector2 searchCenter = playerBounds.center;
            Vector2 searchSize = new Vector2(
                Mathf.Max(pickupRadius, playerBounds.size.x + pickupReach * 2f),
                Mathf.Max(pickupRadius, playerBounds.size.y + pickupReach * 2f));
            pickupHits.Clear();
            Physics2D.OverlapBox(searchCenter, searchSize, 0f, pickupContactFilter, pickupHits);
            Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>(false);
            Transform bestTransform = null;
            CarryableObject bestCarryable = null;
            PlayerController2D bestPlayer = null;
            Rigidbody2D bestBody = null;
            float bestDistance = float.PositiveInfinity;
            float bestCenterDistance = float.PositiveInfinity;

            for (int i = 0; i < pickupHits.Count; i++)
            {
                Collider2D hit = pickupHits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                CarryableObject candidate = hit.GetComponentInParent<CarryableObject>();
                PlayerController2D candidatePlayer = hit.GetComponentInParent<PlayerController2D>();
                Transform candidateTransform = candidate != null ? candidate.transform : candidatePlayer != null ? candidatePlayer.transform : null;
                if (candidateTransform == null)
                {
                    continue;
                }

                Rigidbody2D candidateBody = candidateTransform.GetComponent<Rigidbody2D>();
                if (candidateBody == null || candidateBody.bodyType == RigidbodyType2D.Static)
                {
                    continue;
                }

                float distance = GetClosestColliderDistance(playerColliders, hit);
                if (distance > pickupReach)
                {
                    continue;
                }

                float centerDistance = Vector2.SqrMagnitude((Vector2)candidateTransform.position - searchCenter);
                if (distance < bestDistance - 0.001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.001f && centerDistance < bestCenterDistance))
                {
                    bestDistance = distance;
                    bestCenterDistance = centerDistance;
                    bestTransform = candidateTransform;
                    bestCarryable = candidate;
                    bestPlayer = candidatePlayer;
                    bestBody = candidateBody;
                }
            }

            if (bestTransform == null || bestBody == null)
            {
                return;
            }

            heldTransform = bestTransform;
            heldObject = bestCarryable;
            heldPlayerController = bestPlayer;
            heldBody = bestBody;
            previousBodyType = heldBody.bodyType;
            previousGravityScale = heldBody.gravityScale;
            previousFreezeRotation = heldBody.freezeRotation;
            heldBody.bodyType = RigidbodyType2D.Kinematic;
            heldBody.gravityScale = 0f;
            heldBody.freezeRotation = true;
            heldBody.linearVelocity = Vector2.zero;
            heldBody.angularVelocity = 0f;

            heldColliders.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            heldTransform.GetComponentsInChildren(heldColliders);
            bool holdingKey = IsHeldGameplayKey();
            for (int i = 0; i < heldColliders.Count; i++)
            {
                Collider2D heldCollider = heldColliders[i];
                heldColliderEnabledStates.Add(heldCollider != null && heldCollider.enabled);
                heldColliderTriggerStates.Add(heldCollider != null && heldCollider.isTrigger);
                if (heldCollider == null)
                {
                    continue;
                }

                if (holdingKey)
                {
                    // A key must keep valid world bounds while carried so the
                    // keyhole can test the real overlap. Trigger mode prevents it
                    // colliding with the carrier or terrain.
                    heldCollider.isTrigger = true;
                    heldCollider.enabled = true;
                }
                else
                {
                    heldCollider.enabled = false;
                }
            }

            BringHeldObjectToFront();
            heldPlayerController?.SetControlsEnabled(false);
            heldOnlinePlayerId = GetHeldOnlinePlayerId(heldPlayerController);
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("pickup", Vector2.zero);
            }
            else if (heldObject != null)
            {
                ResolveGimmickSyncManager()?.BeginLocalObjectCarry(heldTransform);
            }
            GameSfx.PlayAt(SfxId.HumanLift, transform.position);
        }

        private static float GetClosestColliderDistance(Collider2D[] playerColliders, Collider2D targetCollider)
        {
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger || playerCollider == targetCollider)
                {
                    continue;
                }

                ColliderDistance2D distance = playerCollider.Distance(targetCollider);
                bestDistance = Mathf.Min(bestDistance, Mathf.Max(0f, distance.distance));
            }

            return bestDistance;
        }

        private void ThrowHeld()
        {
            float multiplier = heldObject != null ? heldObject.ThrowMultiplier : 1f;
            Vector2 baseVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            float currentThrowSpeed = GetCurrentThrowSpeed();
            if (IsHeldGameplayKey())
            {
                // Arm ink can raise the generic throw speed to roughly 77 units/s.
                // Keep puzzle keys controllable and on screen.
                currentThrowSpeed = Mathf.Clamp(currentThrowSpeed * 0.45f, 8f, 14f);
            }
            Vector2 throwVelocity = baseVelocity + GetThrowDirection() * currentThrowSpeed * multiplier;
            GameSfx.PlayAt(SfxId.HumanThrow, transform.position);
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("throw", throwVelocity);
            }

            DropHeld(throwVelocity);
        }

        private void DropHeld(Vector2 releaseVelocity)
        {
            if (heldTransform == null)
            {
                return;
            }

            bodyBuilder?.SetCarryPose(false, GetFacingDirection(), transform.position);
            if (heldObject != null)
            {
                ResolveGimmickSyncManager()?.EndLocalObjectCarry(heldTransform, releaseVelocity);
            }
            Collider2D[] releasedColliders = heldColliders.ToArray();
            Collider2D[] carrierColliders = GetComponentsInChildren<Collider2D>(false);

            for (int i = 0; i < releasedColliders.Length; i++)
            {
                if (releasedColliders[i] != null)
                {
                    if (i < heldColliderTriggerStates.Count)
                    {
                        releasedColliders[i].isTrigger = heldColliderTriggerStates[i];
                    }
                    releasedColliders[i].enabled = i < heldColliderEnabledStates.Count
                        ? heldColliderEnabledStates[i]
                        : true;
                }
            }
            RestoreHeldObjectRendering();
            SetCollisionIgnored(releasedColliders, carrierColliders, true);

            heldPlayerController?.ResetMotion();
            if (heldBody != null)
            {
                heldBody.bodyType = previousBodyType;
                heldBody.gravityScale = previousGravityScale;
                heldBody.freezeRotation = previousFreezeRotation;
                heldBody.linearVelocity = releaseVelocity;
                heldBody.angularVelocity = releaseVelocity.x * -18f;
            }

            heldTransform = null;
            heldObject = null;
            heldPlayerController = null;
            heldBody = null;
            heldOnlinePlayerId = null;
            heldColliders.Clear();
            heldColliderEnabledStates.Clear();
            heldColliderTriggerStates.Clear();
            SetThrowPreviewVisible(false);
            StartCoroutine(RestoreReleasedCollisions(releasedColliders, carrierColliders));
        }

        private bool IsHeldGameplayKey()
        {
            StageEditorObject stageObject = heldTransform != null
                ? heldTransform.GetComponent<StageEditorObject>()
                : null;
            return stageObject != null && stageObject.type == StageObjectType.Key;
        }

        private void BringHeldObjectToFront()
        {
            heldRenderers.Clear();
            heldRendererSortingOrders.Clear();
            heldRendererEnabledStates.Clear();
            if (heldObject == null || heldTransform == null)
            {
                return;
            }

            heldTransform.GetComponentsInChildren(true, heldRenderers);
            for (int i = 0; i < heldRenderers.Count; i++)
            {
                Renderer renderer = heldRenderers[i];
                heldRendererSortingOrders.Add(renderer != null ? renderer.sortingOrder : 0);
                heldRendererEnabledStates.Add(renderer != null && renderer.enabled);
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.sortingOrder += 100;
                }
            }
        }

        private void RestoreHeldObjectRendering()
        {
            for (int i = 0; i < heldRenderers.Count; i++)
            {
                Renderer renderer = heldRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (i < heldRendererSortingOrders.Count)
                {
                    renderer.sortingOrder = heldRendererSortingOrders[i];
                }
                if (i < heldRendererEnabledStates.Count)
                {
                    renderer.enabled = heldRendererEnabledStates[i];
                }
            }

            heldRenderers.Clear();
            heldRendererSortingOrders.Clear();
            heldRendererEnabledStates.Clear();
        }

        public void ForceDrop()
        {
            if (!string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                SendCarryEvent("drop", Vector2.zero);
            }

            DropHeld(Vector2.zero);
        }

        private Vector3 GetHoldPosition()
        {
            int direction = GetFacingDirection();
            if (bodyBuilder != null)
            {
                return bodyBuilder.GetCarryAnchorWorld(direction);
            }

            return transform.position + new Vector3(direction * 0.55f, 1.15f, 0f);
        }

        private Vector3 GetPickupSearchCenter()
        {
            int direction = GetFacingDirection();
            if (TryGetPlayerBounds(out Bounds bounds))
            {
                return new Vector3(
                    bounds.center.x + direction * (bounds.extents.x + 0.45f),
                    bounds.center.y,
                    transform.position.z);
            }

            return transform.position + new Vector3(direction * 0.75f, 0.35f, 0f);
        }

        private bool TryGetPlayerBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private int GetFacingDirection()
        {
            return playerController != null ? playerController.FacingDirection : 1;
        }

        private bool IsHuman()
        {
            return abilityController == null || abilityController.CurrentProfile.Species == DrawManager.Species.Human;
        }

        private Vector2 GetThrowDirection()
        {
            float phase = Mathf.PingPong(Time.time * throwAimSpeed, 1f);
            float angle = Mathf.Lerp(88f, -88f, phase) * Mathf.Deg2Rad;
            int direction = GetFacingDirection();
            return new Vector2(direction * Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        private void CreateThrowPreview()
        {
            GameObject root = new GameObject("ThrowDirectionPreview");
            root.transform.SetParent(transform, false);

            throwPreviewLine = CreatePreviewLine("ThrowDirectionLine", root.transform, 0.055f);
            throwPreviewHeadA = CreatePreviewLine("ThrowDirectionHeadA", root.transform, 0.045f);
            throwPreviewHeadB = CreatePreviewLine("ThrowDirectionHeadB", root.transform, 0.045f);
            SetThrowPreviewVisible(false);
        }

        private LineRenderer CreatePreviewLine(string name, Transform parent, float width)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = 40;
            line.startWidth = width;
            line.endWidth = width;
            line.material = GetPreviewMaterial();
            line.startColor = Color.black;
            line.endColor = Color.black;
            return line;
        }

        private void UpdateThrowPreview(Vector3 anchor)
        {
            if (throwPreviewLine == null)
            {
                return;
            }

            SetThrowPreviewVisible(true);

            Vector2 direction = GetThrowDirection();
            Vector3 start = anchor + Vector3.up * 0.1f;
            float previewScale = Mathf.Clamp(GetCurrentThrowSpeed() / Mathf.Max(throwSpeed, 0.1f), 1f, 1.5f);
            Vector3 end = start + (Vector3)(direction * throwPreviewLength * previewScale);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 headBase = end - (Vector3)(direction * 0.32f);
            Vector3 headA = headBase + (Vector3)(perpendicular * 0.16f);
            Vector3 headB = headBase - (Vector3)(perpendicular * 0.16f);

            SetLine(throwPreviewLine, start, end);
            SetLine(throwPreviewHeadA, end, headA);
            SetLine(throwPreviewHeadB, end, headB);
        }

        private void SetThrowPreviewVisible(bool visible)
        {
            if (throwPreviewLine != null)
            {
                throwPreviewLine.enabled = visible;
            }

            if (throwPreviewHeadA != null)
            {
                throwPreviewHeadA.enabled = visible;
            }

            if (throwPreviewHeadB != null)
            {
                throwPreviewHeadB.enabled = visible;
            }
        }

        private static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            if (line == null)
            {
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private Material GetPreviewMaterial()
        {
            if (previewMaterial != null)
            {
                return previewMaterial;
            }

            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            return previewMaterial;
        }

        private float GetCurrentThrowSpeed()
        {
            float armInk = abilityController != null ? abilityController.CurrentProfile.ArmInk : 0f;
            float inkMultiplier = 1f + Mathf.Clamp(armInk * armInkThrowScale, 0f, 2.5f);
            float targetMultiplier = heldPlayerController != null ? heldPlayerThrowMultiplier : 1f;
            return throwSpeed * inkMultiplier * targetMultiplier;
        }

        private IEnumerator RestoreReleasedCollisions(Collider2D[] releasedColliders, Collider2D[] carrierColliders)
        {
            float minimumRestoreAt = Time.time + postThrowCollisionIgnoreTime;
            float restoreDeadline = Time.time + Mathf.Max(postThrowCollisionIgnoreTime, postThrowCollisionRestoreTimeout);
            while (Time.time < minimumRestoreAt
                || (Time.time < restoreDeadline && AnyCollidersOverlap(releasedColliders, carrierColliders)))
            {
                yield return new WaitForFixedUpdate();
            }

            SetCollisionIgnored(releasedColliders, carrierColliders, false);
        }

        private static bool AnyCollidersOverlap(Collider2D[] first, Collider2D[] second)
        {
            for (int i = 0; i < first.Length; i++)
            {
                Collider2D a = first[i];
                if (a == null || !a.enabled)
                {
                    continue;
                }

                for (int j = 0; j < second.Length; j++)
                {
                    Collider2D b = second[j];
                    if (b == null || !b.enabled || a == b)
                    {
                        continue;
                    }

                    if (a.Distance(b).isOverlapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void SetCollisionIgnored(Collider2D[] first, Collider2D[] second, bool ignored)
        {
            for (int i = 0; i < first.Length; i++)
            {
                Collider2D a = first[i];
                if (a == null)
                {
                    continue;
                }

                for (int j = 0; j < second.Length; j++)
                {
                    Collider2D b = second[j];
                    if (b != null && a != b)
                    {
                        Physics2D.IgnoreCollision(a, b, ignored);
                    }
                }
            }
        }

        private string GetHeldOnlinePlayerId(PlayerController2D heldPlayer)
        {
            if (onlineManager == null || stageManager == null || heldPlayer == null)
            {
                return null;
            }

            if (onlineManager.State != OnlineConnectionState.InLobby && onlineManager.State != OnlineConnectionState.Playing)
            {
                return null;
            }

            return stageManager.GetOnlinePlayerId(heldPlayer);
        }

        private void SendCarryEvent(string action, Vector2 releaseVelocity)
        {
            if (onlineManager == null || string.IsNullOrEmpty(heldOnlinePlayerId))
            {
                return;
            }

            onlineManager.SendCarryData(new OnlineCarryData
            {
                TargetPlayerId = heldOnlinePlayerId,
                Action = action,
                ReleaseVelocity = releaseVelocity
            });
        }

        private StageGimmickSyncManager ResolveGimmickSyncManager()
        {
            if (gimmickSyncManager == null)
            {
                gimmickSyncManager = FindObjectOfType<StageGimmickSyncManager>();
            }

            return gimmickSyncManager;
        }
    }
}
