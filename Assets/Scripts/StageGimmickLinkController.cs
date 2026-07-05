using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageGimmickLinkController : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> objectsById = new Dictionary<string, Transform>();
        private readonly List<LinkRuntime> links = new List<LinkRuntime>();

        private void Start()
        {
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

                LinkRuntime link = new LinkRuntime(source.transform, target, source.linkAction);
                link.Prepare();
                links.Add(link);

                StageGimmickTrigger trigger = source.gameObject.AddComponent<StageGimmickTrigger>();
                trigger.Configure(link.Activate);
            }
        }

        private void Update()
        {
            for (int i = 0; i < links.Count; i++)
            {
                links[i].Update(Time.deltaTime);
            }
        }

        private sealed class LinkRuntime
        {
            private readonly Transform source;
            private readonly Transform target;
            private readonly string action;
            private readonly Collider2D targetCollider;
            private readonly Vector3 fullScale;

            private bool active;
            private float progress;

            public LinkRuntime(Transform linkSource, Transform linkTarget, string linkAction)
            {
                source = linkSource;
                target = linkTarget;
                action = string.IsNullOrEmpty(linkAction) ? "Reveal" : linkAction;
                targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
                fullScale = target != null ? target.localScale : Vector3.one;
            }

            public void Prepare()
            {
                if (target == null)
                {
                    return;
                }

                if (action == "RevealGrow")
                {
                    target.localScale = new Vector3(0.03f, fullScale.y, fullScale.z);
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = false;
                    }
                }

                target.gameObject.SetActive(false);
            }

            public void Activate()
            {
                if (target == null)
                {
                    return;
                }

                target.gameObject.SetActive(true);
                active = true;
            }

            public void Update(float deltaTime)
            {
                if (!active || target == null || action != "RevealGrow")
                {
                    return;
                }

                progress = Mathf.MoveTowards(progress, 1f, deltaTime * 0.38f);
                target.localScale = new Vector3(Mathf.Max(0.03f, fullScale.x * progress), fullScale.y, fullScale.z);
                if (targetCollider != null)
                {
                    targetCollider.enabled = progress > 0.92f;
                }
            }
        }
    }

    public sealed class StageGimmickTrigger : MonoBehaviour
    {
        private System.Action action;
        private bool pressed;
        private Transform cap;

        public void Configure(System.Action onPress)
        {
            action = onPress;
            cap = transform.Find("Button Cap");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (pressed)
            {
                return;
            }

            if (other.GetComponentInParent<PlayerController2D>() == null && other.GetComponentInParent<CarryableObject>() == null)
            {
                return;
            }

            pressed = true;
            if (cap != null)
            {
                cap.localPosition += Vector3.down * 0.12f;
            }

            action?.Invoke();
        }
    }
}
