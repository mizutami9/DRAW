using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageGimmickLinkController : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> objectsById = new Dictionary<string, Transform>();
        private readonly Dictionary<string, LinkRuntime> linksBySourceId = new Dictionary<string, LinkRuntime>();
        private readonly List<LinkRuntime> links = new List<LinkRuntime>();
        private readonly HashSet<string> updatedTargetKeys = new HashSet<string>();
        private StageGimmickSyncManager syncManager;

        private void Start()
        {
            MovingPlatformMountBinder.Bind(transform);
            syncManager = GetComponent<StageGimmickSyncManager>();
            StageEditorObject[] objects = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject stageObject = objects[i];
                if (stageObject == null || string.IsNullOrEmpty(stageObject.objectId))
                {
                    continue;
                }

                objectsById[stageObject.objectId] = stageObject.transform;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject source = objects[i];
                if (source == null || string.IsNullOrEmpty(source.linkTargetId))
                {
                    continue;
                }

                if (!objectsById.TryGetValue(source.linkTargetId, out Transform target))
                {
                    continue;
                }

                StageEditorObject targetStageObject = target.GetComponent<StageEditorObject>();
                if (source.type == StageObjectType.Key
                    && targetStageObject != null
                    && targetStageObject.type == StageObjectType.Keyhole)
                {
                    KeyLockReceiver receiver = target.GetComponent<KeyLockReceiver>();
                    if (receiver == null)
                    {
                        receiver = target.gameObject.AddComponent<KeyLockReceiver>();
                    }

                    string keyholeId = targetStageObject.objectId;
                    receiver.Configure(
                        source.transform,
                        () => ActivateFromTrigger(keyholeId),
                        () => syncManager != null && syncManager.ShouldAskHost);
                    continue;
                }

                LinkRuntime link = new LinkRuntime(source.transform, target, source.linkAction);
                links.Add(link);
                linksBySourceId[source.objectId] = link;

                InkWeightScale inkScale = source.GetComponent<InkWeightScale>();
                if (inkScale != null)
                {
                    inkScale.ConfigureActivation(
                        () => ActivateFromTrigger(source.objectId),
                        () => syncManager != null && syncManager.ShouldAskHost);
                }
                else if (source.type != StageObjectType.Keyhole)
                {
                    StageGimmickTrigger trigger = source.gameObject.AddComponent<StageGimmickTrigger>();
                    if (source.type == StageObjectType.SimultaneousButton
                        || source.type == StageObjectType.HoldButton)
                    {
                        trigger.Configure(
                            () => SetHeldButtonStateFromTrigger(source.objectId, true),
                            () => SetHeldButtonStateFromTrigger(source.objectId, false));
                    }
                    else
                    {
                        trigger.Configure(() => ActivateFromTrigger(source.objectId));
                    }
                }
            }

            // All links must capture the target's original transform before any
            // reveal link changes it during preparation.
            for (int i = 0; i < links.Count; i++)
            {
                links[i].Prepare();
            }

            ConfigureAutomaticMovingPlatforms(objects);
        }

        private static void ConfigureAutomaticMovingPlatforms(StageEditorObject[] objects)
        {
            HashSet<string> linkedTargetIds = new HashSet<string>();
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject source = objects[i];
                if (source != null && !string.IsNullOrEmpty(source.linkTargetId))
                {
                    linkedTargetIds.Add(source.linkTargetId);
                }
            }

            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject platform = objects[i];
                if (platform == null
                    || (platform.type != StageObjectType.MovingPlatform
                        && platform.type != StageObjectType.MovingOneWayPlatform)
                    || linkedTargetIds.Contains(platform.objectId))
                {
                    continue;
                }

                Rigidbody2D body = platform.GetComponent<Rigidbody2D>();
                if (body == null)
                {
                    continue;
                }

                AutomaticMovingPlatform automatic = platform.GetComponent<AutomaticMovingPlatform>();
                if (automatic == null)
                {
                    automatic = platform.gameObject.AddComponent<AutomaticMovingPlatform>();
                }
                automatic.Configure(
                    body,
                    platform.actionStrength > 0f ? platform.actionStrength : 6f,
                    platform.movementAngle,
                    platform.movementSpeed > 0f ? platform.movementSpeed : 3.2f);
            }
        }

        private void Update()
        {
            updatedTargetKeys.Clear();
            for (int i = 0; i < links.Count; i++)
            {
                LinkRuntime link = links[i];
                if (link.NeedsAnimatedUpdate
                    && (!link.IsMovement || syncManager == null || !syncManager.ShouldAskHost)
                    && updatedTargetKeys.Add(link.TargetActionKey))
                {
                    link.Update(Time.deltaTime);
                    for (int peerIndex = 0; peerIndex < links.Count; peerIndex++)
                    {
                        LinkRuntime peer = links[peerIndex];
                        if (peer != link
                            && (peer.Active || peer.SharesProgressWhileInactive)
                            && peer.TargetActionKey == link.TargetActionKey)
                        {
                            peer.SyncProgress(link.Progress);
                        }
                    }
                }
            }
        }

        public void ActivateFromNetwork(string sourceObjectId, bool broadcast)
        {
            if (string.IsNullOrEmpty(sourceObjectId) || !linksBySourceId.TryGetValue(sourceObjectId, out LinkRuntime link))
            {
                return;
            }

            link.SetPressed(true);
            ApplyPressedVisual(sourceObjectId, true);
            if (objectsById.TryGetValue(sourceObjectId, out Transform source) && source != null)
            {
                source.GetComponent<InkWeightScale>()?.ApplyActivatedState();
            }

            List<LinkRuntime> activationGroup = GetActivationGroup(link);
            bool allPressed = true;
            for (int i = 0; i < activationGroup.Count; i++)
            {
                if (!activationGroup[i].Pressed)
                {
                    allPressed = false;
                    break;
                }
            }

            if (!allPressed)
            {
                if (broadcast)
                {
                    syncManager?.BroadcastLinkState(sourceObjectId, link.CreateState());
                }
                return;
            }

            for (int i = 0; i < activationGroup.Count; i++)
            {
                LinkRuntime groupedLink = activationGroup[i];
                groupedLink.Activate();
                if (broadcast)
                {
                    syncManager?.BroadcastLinkState(groupedLink.SourceId, groupedLink.CreateState());
                }
            }
        }

        public void HandleActivationRequest(string sourceObjectId)
        {
            if (string.IsNullOrEmpty(sourceObjectId))
            {
                return;
            }

            if (objectsById.TryGetValue(sourceObjectId, out Transform source)
                && source != null
                && source.TryGetComponent(out StageEditorObject stageObject)
                && stageObject.type == StageObjectType.Keyhole)
            {
                source.GetComponent<KeyLockReceiver>()?.TryUnlockAuthoritatively();
                return;
            }

            ActivateFromNetwork(sourceObjectId, true);
        }

        public void HandleHeldButtonRequest(string sourceObjectId, bool held)
        {
            SetHeldButtonStateFromNetwork(sourceObjectId, held, true);
        }

        public void BroadcastAllStates()
        {
            if (syncManager == null || !syncManager.IsHost)
            {
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                LinkRuntime link = links[i];
                syncManager.BroadcastLinkState(link.SourceId, link.CreateState());
            }
        }

        public void ApplyNetworkState(string sourceObjectId, OnlineLinkGimmickState state)
        {
            if (state == null || string.IsNullOrEmpty(sourceObjectId) || !linksBySourceId.TryGetValue(sourceObjectId, out LinkRuntime link))
            {
                return;
            }

            if (state.Active
                && objectsById.TryGetValue(sourceObjectId, out Transform source)
                && source != null)
            {
                source.GetComponent<KeyLockReceiver>()?.ApplyUnlockedState();
                source.GetComponent<InkWeightScale>()?.ApplyActivatedState();
            }
            link.ApplyState(state, syncManager == null || !syncManager.ShouldAskHost);
            if (state.Active && link.IsSimultaneousButtonSource)
            {
                ApplyLatchedVisual(sourceObjectId);
            }
            else
            {
                ApplyPressedVisual(sourceObjectId, state.Pressed);
            }
        }

        private List<LinkRuntime> GetActivationGroup(LinkRuntime activatedLink)
        {
            List<LinkRuntime> group = new List<LinkRuntime>();
            if (activatedLink == null || !activatedLink.IsButtonSource)
            {
                group.Add(activatedLink);
                return group;
            }

            for (int i = 0; i < links.Count; i++)
            {
                LinkRuntime candidate = links[i];
                if (candidate != null
                    && candidate.IsButtonSource
                    && candidate.ButtonGroupMode == activatedLink.ButtonGroupMode
                    && candidate.HasSameActivationTarget(activatedLink))
                {
                    group.Add(candidate);
                }
            }

            if (group.Count == 0)
            {
                group.Add(activatedLink);
            }
            return group;
        }

        private void ApplyPressedVisual(string sourceObjectId, bool pressed)
        {
            if (objectsById.TryGetValue(sourceObjectId, out Transform source) && source != null)
            {
                source.GetComponent<StageGimmickTrigger>()?.ApplyPressedState(pressed);
            }
        }

        private void ApplyLatchedVisual(string sourceObjectId)
        {
            if (objectsById.TryGetValue(sourceObjectId, out Transform source) && source != null)
            {
                source.GetComponent<StageGimmickTrigger>()?.ApplyLatchedState();
            }
        }

        private void ActivateFromTrigger(string sourceObjectId)
        {
            if (syncManager != null && syncManager.ShouldAskHost)
            {
                syncManager.RequestLinkActivation(sourceObjectId);
                return;
            }

            ActivateFromNetwork(sourceObjectId, syncManager != null && syncManager.IsOnlineActive && syncManager.IsHost);
        }

        private void SetHeldButtonStateFromTrigger(string sourceObjectId, bool held)
        {
            if (syncManager != null && syncManager.ShouldAskHost)
            {
                syncManager.RequestHeldButtonState(sourceObjectId, held);
                return;
            }

            SetHeldButtonStateFromNetwork(
                sourceObjectId,
                held,
                syncManager != null && syncManager.IsOnlineActive && syncManager.IsHost);
        }

        private void SetHeldButtonStateFromNetwork(string sourceObjectId, bool held, bool broadcast)
        {
            if (string.IsNullOrEmpty(sourceObjectId)
                || !linksBySourceId.TryGetValue(sourceObjectId, out LinkRuntime link)
                || (!link.IsSimultaneousButtonSource && !link.IsHoldButtonSource))
            {
                return;
            }

            if (link.IsSimultaneousButtonSource && link.Active)
            {
                ApplyLatchedVisual(sourceObjectId);
                return;
            }

            link.SetPressed(held);
            ApplyPressedVisual(sourceObjectId, held);
            List<LinkRuntime> activationGroup = GetActivationGroup(link);
            bool allPressed = held;
            for (int i = 0; i < activationGroup.Count && allPressed; i++)
            {
                allPressed = activationGroup[i].Pressed;
            }

            if (link.IsHoldButtonSource)
            {
                for (int i = 0; i < activationGroup.Count; i++)
                {
                    LinkRuntime groupedLink = activationGroup[i];
                    if (allPressed)
                    {
                        groupedLink.Activate();
                    }
                    else
                    {
                        groupedLink.Deactivate();
                    }

                    if (broadcast)
                    {
                        syncManager?.BroadcastLinkState(groupedLink.SourceId, groupedLink.CreateState());
                    }
                }
                return;
            }

            if (!allPressed)
            {
                if (broadcast)
                {
                    syncManager?.BroadcastLinkState(sourceObjectId, link.CreateState());
                }
                return;
            }

            for (int i = 0; i < activationGroup.Count; i++)
            {
                LinkRuntime groupedLink = activationGroup[i];
                groupedLink.SetPressed(true);
                groupedLink.Activate();
                ApplyLatchedVisual(groupedLink.SourceId);
                if (broadcast)
                {
                    syncManager?.BroadcastLinkState(groupedLink.SourceId, groupedLink.CreateState());
                }
            }
        }

        private sealed class LinkRuntime
        {
            private const float MinRevealProgress = 0.025f;

            private readonly Transform source;
            private readonly Transform target;
            private readonly string action;
            private readonly string targetId;
            private readonly Collider2D targetCollider;
            private readonly Vector3 fullScale;
            private readonly Vector3 fullLocalPosition;
            private readonly Quaternion fullLocalRotation;
            private readonly float revealWidth;
            private readonly Vector3 movementOffset;
            private readonly float movementSpeed;
            private readonly Rigidbody2D targetBody;
            private readonly DirectionalMovingPlatform directionalPlatform;
            private readonly StageDynamite dynamite;
            private readonly IStageLinkActivatable linkActivatable;

            private bool active;
            private bool pressed;
            private float progress;

            public string SourceId
            {
                get
                {
                    StageEditorObject stageObject = source != null ? source.GetComponent<StageEditorObject>() : null;
                    return stageObject != null ? stageObject.objectId : string.Empty;
                }
            }

            public bool Pressed => pressed;
            public bool Active => active;
            public float Progress => progress;
            public bool NeedsAnimatedUpdate => target != null
                && ((active && IsRevealGrowAction())
                    || (IsMoveAction()
                        && !IsDirectionalMoveAction()
                        && !Mathf.Approximately(progress, active ? 1f : 0f)));
            public bool SharesProgressWhileInactive => IsMoveAction() && !IsDirectionalMoveAction();
            public bool IsMovement => IsMoveAction();
            public string TargetActionKey => targetId + "\n" + action;

            public bool IsButtonSource
            {
                get
                {
                    StageEditorObject stageObject = source != null ? source.GetComponent<StageEditorObject>() : null;
                    return stageObject != null
                        && (stageObject.type == StageObjectType.Button
                            || stageObject.type == StageObjectType.WeightButton
                            || stageObject.type == StageObjectType.SimultaneousButton
                            || stageObject.type == StageObjectType.HoldButton
                            || stageObject.type == StageObjectType.PressurePlate);
                }
            }

            public bool IsSimultaneousButtonSource
            {
                get
                {
                    StageEditorObject stageObject = source != null ? source.GetComponent<StageEditorObject>() : null;
                    return stageObject != null && stageObject.type == StageObjectType.SimultaneousButton;
                }
            }

            public bool IsHoldButtonSource
            {
                get
                {
                    StageEditorObject stageObject = source != null ? source.GetComponent<StageEditorObject>() : null;
                    return stageObject != null && stageObject.type == StageObjectType.HoldButton;
                }
            }

            public int ButtonGroupMode => IsSimultaneousButtonSource ? 1 : IsHoldButtonSource ? 2 : 0;

            public LinkRuntime(Transform linkSource, Transform linkTarget, string linkAction)
            {
                source = linkSource;
                target = linkTarget;
                action = string.IsNullOrEmpty(linkAction) ? "Reveal" : linkAction;
                StageEditorObject targetStageObject = target != null ? target.GetComponent<StageEditorObject>() : null;
                targetId = targetStageObject != null ? targetStageObject.objectId : string.Empty;
                targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
                fullScale = target != null ? target.localScale : Vector3.one;
                fullLocalPosition = target != null ? target.localPosition : Vector3.zero;
                fullLocalRotation = target != null ? target.localRotation : Quaternion.identity;
                revealWidth = ResolveRevealWidth(target, targetCollider, fullScale);
                float movementDistance = targetStageObject != null && targetStageObject.actionStrength > 0f
                    ? Mathf.Clamp(targetStageObject.actionStrength, 1f, 100f)
                    : 6f;
                float movementAngle = targetStageObject != null ? targetStageObject.movementAngle : 0f;
                movementOffset = Quaternion.Euler(0f, 0f, movementAngle) * Vector3.right * movementDistance;
                movementSpeed = targetStageObject != null && targetStageObject.movementSpeed > 0f
                    ? Mathf.Clamp(targetStageObject.movementSpeed, 0.5f, 10f)
                    : 3.2f;
                targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
                dynamite = target != null ? target.GetComponent<StageDynamite>() : null;
                linkActivatable = target != null ? target.GetComponent<IStageLinkActivatable>() : null;
                if (IsDirectionalMoveAction() && target != null)
                {
                    directionalPlatform = target.GetComponent<DirectionalMovingPlatform>();
                    if (directionalPlatform == null)
                    {
                        directionalPlatform = target.gameObject.AddComponent<DirectionalMovingPlatform>();
                    }
                    directionalPlatform.Configure(movementDistance, movementSpeed);
                }
            }

            public void Prepare()
            {
                if (target == null)
                {
                    return;
                }

                if (dynamite != null)
                {
                    active = false;
                    progress = 0f;
                    target.gameObject.SetActive(true);
                    if (targetCollider != null) targetCollider.enabled = true;
                    return;
                }

                if (linkActivatable != null)
                {
                    active = false;
                    progress = 0f;
                    target.gameObject.SetActive(true);
                    if (targetCollider != null) targetCollider.enabled = true;
                    linkActivatable.PrepareForLink();
                    return;
                }

                if (IsMoveAction())
                {
                    active = false;
                    progress = 0f;
                    target.gameObject.SetActive(true);
                    if (!IsDirectionalMoveAction())
                    {
                        ApplyMoveProgress(0f);
                    }
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = true;
                    }
                    return;
                }

                if (IsHideAction())
                {
                    active = false;
                    target.gameObject.SetActive(true);
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = true;
                    }
                    return;
                }

                if (IsRevealGrowAction())
                {
                    PrepareRevealBridgeVisuals();
                    progress = 0f;
                    ApplyRevealProgress(0f);
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = false;
                    }
                }

                target.gameObject.SetActive(false);
            }

            public void SetPressed(bool value)
            {
                pressed = value;
            }

            public bool HasSameActivationTarget(LinkRuntime other)
            {
                return other != null
                    && targetId == other.targetId
                    && action == other.action;
            }

            public void Activate()
            {
                if (target == null)
                {
                    return;
                }

                if (dynamite != null)
                {
                    target.gameObject.SetActive(true);
                    active = true;
                    dynamite.ActivateFromLink();
                    return;
                }

                if (linkActivatable != null)
                {
                    target.gameObject.SetActive(true);
                    active = true;
                    linkActivatable.ActivateFromLink();
                    return;
                }

                if (IsMoveAction())
                {
                    target.gameObject.SetActive(true);
                    active = true;
                    if (IsDirectionalMoveAction())
                    {
                        directionalPlatform?.SetInput(SourceId, GetMoveDirection(), true);
                    }
                    return;
                }

                if (IsHideAction())
                {
                    active = true;
                    target.gameObject.SetActive(false);
                    return;
                }

                target.gameObject.SetActive(true);
                active = true;
                if (IsRevealGrowAction())
                {
                    ApplyRevealProgress(progress);
                }
            }

            public void Deactivate()
            {
                if (target == null)
                {
                    return;
                }

                if (dynamite != null)
                {
                    // Once its fuse has started, releasing a hold button cannot
                    // put the explosive back into an unlit state.
                    return;
                }

                if (linkActivatable != null)
                {
                    active = false;
                    target.gameObject.SetActive(true);
                    return;
                }

                active = false;
                if (IsMoveAction())
                {
                    target.gameObject.SetActive(true);
                    if (IsDirectionalMoveAction())
                    {
                        directionalPlatform?.SetInput(SourceId, GetMoveDirection(), false);
                    }
                    return;
                }

                progress = 0f;
                if (IsHideAction())
                {
                    target.gameObject.SetActive(true);
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = true;
                    }
                    return;
                }

                if (IsRevealGrowAction())
                {
                    ApplyRevealProgress(0f);
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = false;
                    }
                }

                target.gameObject.SetActive(false);
            }

            public void Update(float deltaTime)
            {
                if (target == null)
                {
                    return;
                }

                if (IsMoveAction())
                {
                    if (IsDirectionalMoveAction())
                    {
                        return;
                    }

                    float distance = Mathf.Max(0.1f, movementOffset.magnitude);
                    progress = Mathf.MoveTowards(
                        progress,
                        active ? 1f : 0f,
                        deltaTime * movementSpeed / distance);
                    ApplyMoveProgress(progress);
                }
                else if (active && IsRevealGrowAction())
                {
                    progress = Mathf.MoveTowards(progress, 1f, deltaTime * 0.38f);
                    ApplyRevealProgress(progress);
                }
            }

            public void SyncProgress(float value)
            {
                progress = Mathf.Clamp01(value);
            }

            public OnlineLinkGimmickState CreateState()
            {
                return new OnlineLinkGimmickState
                {
                    TargetId = targetId,
                    Action = action,
                    Progress = progress,
                    Active = active,
                    Pressed = pressed
                };
            }

            public void ApplyState(OnlineLinkGimmickState state, bool applyMovementTransform)
            {
                if (state == null || target == null)
                {
                    return;
                }

                pressed = state.Pressed;
                bool wasActive = active;
                active = state.Active;
                progress = Mathf.Clamp01(state.Progress);
                if (dynamite != null)
                {
                    target.gameObject.SetActive(true);
                    if (active) dynamite.ActivateFromLink();
                    return;
                }
                if (linkActivatable != null)
                {
                    target.gameObject.SetActive(true);
                    if (active && !wasActive) linkActivatable.ActivateFromLink();
                    return;
                }
                if (IsMoveAction())
                {
                    target.gameObject.SetActive(true);
                    if (IsDirectionalMoveAction())
                    {
                        directionalPlatform?.SetInput(SourceId, GetMoveDirection(), active);
                    }
                    else if (applyMovementTransform)
                    {
                        ApplyMoveProgress(progress);
                    }
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = true;
                    }
                    return;
                }

                target.gameObject.SetActive(IsHideAction() ? !active : active);
                if (IsRevealGrowAction())
                {
                    ApplyRevealProgress(progress);
                }
                else if (active && targetCollider != null)
                {
                    targetCollider.enabled = true;
                }
            }

            private void ApplyRevealProgress(float value)
            {
                if (target == null)
                {
                    return;
                }

                float visibleProgress = Mathf.Clamp01(value);
                float scaleProgress = Mathf.Max(MinRevealProgress, visibleProgress);
                target.localScale = new Vector3(fullScale.x * scaleProgress, fullScale.y, fullScale.z);

                // Keep the right edge fixed so linked bridges grow from right to left.
                float shift = revealWidth * Mathf.Abs(fullScale.x) * (1f - scaleProgress) * 0.5f;
                Vector3 localRight = fullLocalRotation * Vector3.right;
                target.localPosition = fullLocalPosition + localRight * shift;

                if (targetCollider != null)
                {
                    targetCollider.enabled = scaleProgress > MinRevealProgress * 0.5f;
                }
            }

            private void ApplyMoveProgress(float value)
            {
                if (target == null)
                {
                    return;
                }

                Vector3 localPosition = Vector3.Lerp(
                    fullLocalPosition,
                    fullLocalPosition + movementOffset,
                    Mathf.Clamp01(value));
                if (targetBody != null && target.parent != null && Application.isPlaying)
                {
                    targetBody.MovePosition(target.parent.TransformPoint(localPosition));
                }
                else
                {
                    target.localPosition = localPosition;
                }
            }

            private bool IsRevealGrowAction()
            {
                return action == "RevealGrowRightToLeft" || action == "RevealGrow";
            }

            private bool IsHideAction()
            {
                return action == "Hide";
            }

            private bool IsMoveAction()
            {
                return action == "Move"
                    || action == "MoveRight"
                    || action == "MoveUp"
                    || action == "MoveLeft"
                    || action == "MoveDown";
            }

            private bool IsDirectionalMoveAction()
            {
                return action == "Move"
                    || action == "MoveRight"
                    || action == "MoveUp"
                    || action == "MoveLeft"
                    || action == "MoveDown";
            }

            private Vector2 GetMoveDirection()
            {
                switch (action)
                {
                    case "MoveUp":
                        return Vector2.up;
                    case "MoveLeft":
                        return Vector2.left;
                    case "MoveDown":
                        return Vector2.down;
                    default:
                        return Vector2.right;
                }
            }

            private void PrepareRevealBridgeVisuals()
            {
                if (target == null || target.Find("Reveal Bridge Visuals Prepared") != null)
                {
                    return;
                }

                GameObject marker = new GameObject("Reveal Bridge Visuals Prepared");
                marker.transform.SetParent(target, false);

                LineRenderer[] lines = target.GetComponentsInChildren<LineRenderer>(true);
                for (int i = 0; i < lines.Length; i++)
                {
                    LineRenderer line = lines[i];
                    if (line == null
                        || !line.name.StartsWith("Solid Sketch Outline", System.StringComparison.Ordinal)
                        || line.name.Contains("Bridge Top")
                        || line.positionCount < 4)
                    {
                        continue;
                    }

                    Vector3 bottomStart = line.GetPosition(0);
                    Vector3 bottomEnd = line.GetPosition(1);
                    Vector3 topStart = line.GetPosition(3);
                    Vector3 topEnd = line.GetPosition(2);

                    line.positionCount = 2;
                    line.SetPosition(0, bottomStart);
                    line.SetPosition(1, bottomEnd);

                    LineRenderer topLine = CreateLineCopy(line, line.name + " Bridge Top");
                    topLine.positionCount = 2;
                    topLine.SetPosition(0, topStart);
                    topLine.SetPosition(1, topEnd);
                }
            }

            private static LineRenderer CreateLineCopy(LineRenderer source, string name)
            {
                GameObject copy = new GameObject(name);
                copy.transform.SetParent(source.transform.parent, false);
                copy.transform.localPosition = source.transform.localPosition;
                copy.transform.localRotation = source.transform.localRotation;
                copy.transform.localScale = source.transform.localScale;

                LineRenderer line = copy.AddComponent<LineRenderer>();
                line.useWorldSpace = source.useWorldSpace;
                line.startWidth = source.startWidth;
                line.endWidth = source.endWidth;
                line.numCapVertices = source.numCapVertices;
                line.numCornerVertices = source.numCornerVertices;
                line.material = source.material;
                line.startColor = source.startColor;
                line.endColor = source.endColor;
                line.sortingOrder = source.sortingOrder;
                return line;
            }

            private static float ResolveRevealWidth(Transform target, Collider2D collider, Vector3 scale)
            {
                if (target != null && target.TryGetComponent(out StageEditorObject stageObject))
                {
                    return Mathf.Max(0.01f, stageObject.size.x);
                }

                if (collider is BoxCollider2D box)
                {
                    return Mathf.Max(0.01f, box.size.x);
                }

                if (collider != null)
                {
                    float scaleX = Mathf.Max(0.01f, Mathf.Abs(scale.x));
                    return Mathf.Max(0.01f, collider.bounds.size.x / scaleX);
                }

                return 1f;
            }
        }
    }

    public sealed class KeyLockReceiver : MonoBehaviour
    {
        private Transform expectedKey;
        private System.Action unlockAction;
        private System.Func<bool> requestAuthority;
        private bool unlocked;
        private float nextRequestTime;

        public void Configure(Transform key, System.Action onUnlocked, System.Func<bool> shouldRequestAuthority)
        {
            expectedKey = key;
            unlockAction = onUnlocked;
            requestAuthority = shouldRequestAuthority;
        }

        private void Update()
        {
            if (unlocked || expectedKey == null || !expectedKey.gameObject.activeInHierarchy)
            {
                return;
            }

            Collider2D receiverCollider = GetComponent<Collider2D>();
            Collider2D keyCollider = expectedKey.GetComponent<Collider2D>();
            if (!IsKeyWithinInsertionArea(receiverCollider, keyCollider))
            {
                return;
            }

            if (requestAuthority != null && requestAuthority())
            {
                if (Time.unscaledTime >= nextRequestTime)
                {
                    nextRequestTime = Time.unscaledTime + 0.5f;
                    unlockAction?.Invoke();
                }
                return;
            }

            UnlockAndConsume(true);
        }

        public bool TryUnlockAuthoritatively()
        {
            if (unlocked || expectedKey == null || !expectedKey.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!IsKeyWithinInsertionArea(GetComponent<Collider2D>(), expectedKey.GetComponent<Collider2D>()))
            {
                return false;
            }

            UnlockAndConsume(true);
            return true;
        }

        public void ApplyUnlockedState()
        {
            if (!unlocked)
            {
                UnlockAndConsume(false);
            }
        }

        private void UnlockAndConsume(bool invokeAction)
        {
            unlocked = true;
            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                carriers[i]?.ReleaseIfHolding(expectedKey);
            }

            Rigidbody2D keyBody = expectedKey.GetComponent<Rigidbody2D>();
            if (keyBody != null)
            {
                keyBody.linearVelocity = Vector2.zero;
                keyBody.angularVelocity = 0f;
                keyBody.simulated = false;
            }

            Collider2D[] keyColliders = expectedKey.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < keyColliders.Length; i++)
            {
                keyColliders[i].enabled = false;
            }

            expectedKey.position = transform.position;
            expectedKey.rotation = transform.rotation;
            expectedKey.localScale *= 0.82f;
            if (invokeAction)
            {
                unlockAction?.Invoke();
            }
        }

        private bool IsKeyWithinInsertionArea(Collider2D receiverCollider, Collider2D keyCollider)
        {
            const float extraTolerance = 0.2f;
            // Never consume a key based only on invalid/empty collider bounds.
            // Carrying used to disable the key collider, which could make the
            // bounds query report an unrelated point and unlock from far away.
            float receiverRadius = receiverCollider != null && receiverCollider.enabled
                ? receiverCollider.bounds.extents.magnitude
                : 0.8f;
            float keyRadius = keyCollider != null && keyCollider.enabled
                ? keyCollider.bounds.extents.magnitude
                : 0.8f;
            float centerDistance = Vector2.Distance(transform.position, expectedKey.position);
            float maximumCenterDistance = Mathf.Max(1.1f, receiverRadius + keyRadius + extraTolerance);
            bool carriedNearKeyhole = IsExpectedKeyCarriedNear(receiverCollider);
            if (!carriedNearKeyhole && centerDistance > maximumCenterDistance)
            {
                return false;
            }

            // The carry pose displays the key above the character's hands. When
            // the carrier walks right up to the keyhole, treat that deliberate
            // approach as insertion and snap the key into the hole on unlock.
            if (carriedNearKeyhole)
            {
                return true;
            }

            if (receiverCollider != null && keyCollider != null)
            {
                Bounds insertionArea = receiverCollider.bounds;
                insertionArea.Expand(extraTolerance * 2f);
                if (insertionArea.Intersects(keyCollider.bounds))
                {
                    return true;
                }

                Vector2 receiverPoint = receiverCollider.ClosestPoint(keyCollider.bounds.center);
                Vector2 keyPoint = keyCollider.ClosestPoint(receiverPoint);
                return Vector2.Distance(receiverPoint, keyPoint) <= extraTolerance;
            }

            float fallbackDistance = receiverCollider != null
                ? Mathf.Max(0.8f, receiverCollider.bounds.extents.magnitude + extraTolerance)
                : 0.9f;
            return Vector2.Distance(transform.position, expectedKey.position) <= fallbackDistance;
        }

        private bool IsExpectedKeyCarriedNear(Collider2D receiverCollider)
        {
            PlayerCarryController[] carriers =
                Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                PlayerCarryController carrier = carriers[i];
                if (carrier == null || !carrier.IsHoldingTarget(expectedKey))
                {
                    continue;
                }

                if (receiverCollider != null)
                {
                    Collider2D[] carrierColliders = carrier.GetComponentsInChildren<Collider2D>(false);
                    for (int colliderIndex = 0; colliderIndex < carrierColliders.Length; colliderIndex++)
                    {
                        Collider2D carrierCollider = carrierColliders[colliderIndex];
                        if (carrierCollider == null || !carrierCollider.enabled || carrierCollider.isTrigger)
                        {
                            continue;
                        }

                        if (Mathf.Max(0f, receiverCollider.Distance(carrierCollider).distance) <= 0.85f)
                        {
                            return true;
                        }
                    }
                }

                if (Vector2.Distance(transform.position, carrier.transform.position) <= 1.8f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class StageGimmickTrigger : MonoBehaviour
    {
        private static readonly Color PressedCapColor = new Color(0.12f, 0.72f, 0.22f, 0.62f);
        private static readonly Color PressedPencilColor = new Color(0.05f, 0.58f, 0.15f, 0.82f);

        private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
        private System.Action pressAction;
        private System.Action releaseAction;
        private bool pressed;
        private bool latched;
        private Transform cap;
        private Vector3 capReleasedLocalPosition;
        private SpriteRenderer capRenderer;
        private Color capReleasedColor;
        private LineRenderer[] capLines;
        private Color[] releasedLineStartColors;
        private Color[] releasedLineEndColors;

        public void Configure(System.Action onPress)
        {
            Configure(onPress, null);
        }

        public void Configure(System.Action onPress, System.Action onRelease)
        {
            pressAction = onPress;
            releaseAction = onRelease;
            CacheCapVisuals();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (latched || !IsValidPresser(other) || !contacts.Add(other))
            {
                return;
            }

            if (pressed)
            {
                return;
            }

            ApplyPressedState(true);
            pressAction?.Invoke();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (latched || releaseAction == null || other == null || !contacts.Remove(other) || contacts.Count > 0)
            {
                return;
            }

            ApplyPressedState(false);
            releaseAction.Invoke();
        }

        public void ApplyPressedState(bool value)
        {
            if (latched && !value)
            {
                return;
            }

            pressed = value;
            CacheCapVisuals();
            if (cap == null)
            {
                return;
            }

            cap.localPosition = capReleasedLocalPosition + (pressed ? Vector3.down * 0.12f : Vector3.zero);
            if (capRenderer != null)
            {
                capRenderer.color = pressed ? PressedCapColor : capReleasedColor;
            }

            for (int i = 0; i < capLines.Length; i++)
            {
                LineRenderer line = capLines[i];
                if (line == null)
                {
                    continue;
                }

                bool recolor = pressed && line.name != "Outline";
                line.startColor = recolor ? PressedPencilColor : releasedLineStartColors[i];
                line.endColor = recolor ? PressedPencilColor : releasedLineEndColors[i];
            }
        }

        public void ApplyLatchedState()
        {
            latched = true;
            contacts.Clear();
            ApplyPressedState(true);
        }

        private void CacheCapVisuals()
        {
            if (cap != null)
            {
                return;
            }

            cap = transform.Find("Button Cap");
            if (cap == null)
            {
                capLines = System.Array.Empty<LineRenderer>();
                releasedLineStartColors = System.Array.Empty<Color>();
                releasedLineEndColors = System.Array.Empty<Color>();
                return;
            }

            capReleasedLocalPosition = cap.localPosition;
            capRenderer = cap.GetComponent<SpriteRenderer>();
            capReleasedColor = capRenderer != null ? capRenderer.color : Color.white;
            capLines = cap.GetComponentsInChildren<LineRenderer>(true);
            releasedLineStartColors = new Color[capLines.Length];
            releasedLineEndColors = new Color[capLines.Length];
            for (int i = 0; i < capLines.Length; i++)
            {
                if (capLines[i] == null)
                {
                    continue;
                }

                releasedLineStartColors[i] = capLines[i].startColor;
                releasedLineEndColors[i] = capLines[i].endColor;
            }
        }

        private static bool IsValidPresser(Collider2D other)
        {
            return other != null
                && (other.GetComponentInParent<PlayerController2D>() != null
                    || other.GetComponentInParent<CarryableObject>() != null);
        }
    }

    public sealed class InkWeightScale : MonoBehaviour
    {
        private readonly Dictionary<PlayerAbilityController, int> contacts = new Dictionary<PlayerAbilityController, int>();

        private float perPlayerThreshold = 300f;
        private float displayedWeight;
        private float targetWeight;
        private StageManager stageManager;
        private TextMesh meterText;
        private SpriteRenderer body;
        private Transform gaugeFillTransform;
        private SpriteRenderer gaugeFill;
        private System.Action activation;
        private System.Func<bool> shouldWaitForAuthority;
        private bool activated;
        private bool activationSent;
        private int lastShownWeight = -1;
        private int lastShownThreshold = -1;

        public void Configure(
            float targetThreshold,
            TextMesh targetText,
            SpriteRenderer targetBody,
            Transform targetGaugeFill,
            SpriteRenderer targetGaugeRenderer)
        {
            perPlayerThreshold = Mathf.Clamp(targetThreshold, 1f, 2000f);
            if (stageManager == null)
            {
                stageManager = Object.FindFirstObjectByType<StageManager>();
            }
            meterText = targetText;
            body = targetBody;
            gaugeFillTransform = targetGaugeFill;
            gaugeFill = targetGaugeRenderer;
            RefreshVisual(true);
        }

        public void ConfigureActivation(System.Action onActivated, System.Func<bool> waitForAuthority)
        {
            activation = onActivated;
            shouldWaitForAuthority = waitForAuthority;
            if (activated && !activationSent)
            {
                activationSent = true;
                activation?.Invoke();
            }
        }

        public void ApplyActivatedState()
        {
            activated = true;
            activationSent = true;
            RefreshVisual(true);
        }

        private void Update()
        {
            targetWeight = CalculateTotalWeight();
            displayedWeight = Mathf.MoveTowards(displayedWeight, targetWeight, 900f * Time.deltaTime);
            float effectiveThreshold = GetEffectiveThreshold();
            // Reaching the configured value is sufficient. The old strict ">"
            // comparison left a scale showing e.g. 600 / 600 without firing.
            const float thresholdTolerance = 0.01f;
            bool targetReached = targetWeight >= effectiveThreshold - thresholdTolerance;
            bool displayReached = displayedWeight >= effectiveThreshold - thresholdTolerance;
            if (!activated && !activationSent && targetReached && displayReached)
            {
                activationSent = true;
                activation?.Invoke();
                if (shouldWaitForAuthority == null || !shouldWaitForAuthority())
                {
                    activated = true;
                    RefreshVisual(true);
                }
            }

            RefreshVisual(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerAbilityController player = other != null
                ? other.GetComponentInParent<PlayerAbilityController>()
                : null;
            if (player == null)
            {
                return;
            }

            contacts.TryGetValue(player, out int count);
            contacts[player] = count + 1;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerAbilityController player = other != null
                ? other.GetComponentInParent<PlayerAbilityController>()
                : null;
            if (player == null || !contacts.TryGetValue(player, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                contacts.Remove(player);
            }
            else
            {
                contacts[player] = count - 1;
            }
        }

        private void OnDisable()
        {
            contacts.Clear();
            targetWeight = 0f;
        }

        private float CalculateTotalWeight()
        {
            float total = 0f;
            List<PlayerAbilityController> missing = null;
            foreach (KeyValuePair<PlayerAbilityController, int> pair in contacts)
            {
                if (pair.Key == null)
                {
                    if (missing == null)
                    {
                        missing = new List<PlayerAbilityController>();
                    }
                    missing.Add(pair.Key);
                    continue;
                }

                PlayerController2D movement = pair.Key.GetComponent<PlayerController2D>();
                if (movement != null && !movement.IsGrounded)
                {
                    continue;
                }

                total += Mathf.Max(0f, pair.Key.CurrentProfile.TotalInk);
            }

            if (missing != null)
            {
                for (int i = 0; i < missing.Count; i++)
                {
                    contacts.Remove(missing[i]);
                }
            }
            return total;
        }

        private void RefreshVisual(bool force)
        {
            int shownWeight = Mathf.Max(0, Mathf.RoundToInt(displayedWeight));
            float effectiveThreshold = GetEffectiveThreshold();
            int shownThreshold = Mathf.Max(1, Mathf.RoundToInt(effectiveThreshold));
            if (force || shownWeight != lastShownWeight || shownThreshold != lastShownThreshold)
            {
                lastShownWeight = shownWeight;
                lastShownThreshold = shownThreshold;
                if (meterText != null)
                {
                    meterText.text = $"{shownWeight} / {shownThreshold}";
                    meterText.color = activated
                        ? new Color(0.04f, 0.28f, 0.08f, 1f)
                        : new Color(0.08f, 0.07f, 0.05f, 1f);
                }
            }

            float ratio = Mathf.Clamp01(displayedWeight / Mathf.Max(1f, effectiveThreshold));
            if (gaugeFillTransform != null)
            {
                gaugeFillTransform.localScale = new Vector3(0.62f * ratio, 0.065f, 1f);
                gaugeFillTransform.localPosition = new Vector3(-0.31f + 0.31f * ratio, -0.29f, -0.045f);
            }
            if (gaugeFill != null)
            {
                gaugeFill.color = activated
                    ? new Color(0.2f, 0.9f, 0.3f, 1f)
                    : new Color(0.3f, 0.82f, 0.96f, 1f);
            }
            if (body != null)
            {
                body.color = activated
                    ? new Color(0.38f, 0.92f, 0.38f, 0.96f)
                    : new Color(0.96f, 0.77f, 0.24f, 0.94f);
            }
        }

        private float GetEffectiveThreshold()
        {
            int playerCount = stageManager != null
                ? stageManager.GetInkBudgetPlayerCount()
                : 1;
            return perPlayerThreshold * Mathf.Max(1, playerCount);
        }
    }
}
