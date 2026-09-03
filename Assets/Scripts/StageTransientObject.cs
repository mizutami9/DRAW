using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Marks a runtime-only stage object that must never survive a stage/session
    /// transition. Registration is explicit so cleanup does not depend on names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageTransientObject : MonoBehaviour
    {
        private static readonly HashSet<StageTransientObject> Active = new HashSet<StageTransientObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Active.Clear();
        }

        public static void Register(GameObject root)
        {
            if (root == null) return;
            StageTransientObject marker = root.GetComponent<StageTransientObject>();
            if (marker == null) marker = root.AddComponent<StageTransientObject>();
            Active.Add(marker);
        }

        public static void ClearAll()
        {
            if (Active.Count == 0) return;
            StageTransientObject[] snapshot = new StageTransientObject[Active.Count];
            Active.CopyTo(snapshot);
            Active.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                StageTransientObject marker = snapshot[i];
                if (marker == null) continue;
                GameObject root = marker.gameObject;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    if (renderers[r] != null) renderers[r].enabled = false;
                Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < colliders.Length; c++)
                    if (colliders[c] != null) colliders[c].enabled = false;
                root.SetActive(false);
                Object.Destroy(root);
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }
    }
}
