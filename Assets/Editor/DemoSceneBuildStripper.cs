using DrawBody.Prototype;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrawBody.EditorTools
{
    /// <summary>
    /// Removes the runtime stage-authoring surface from distributable demo scenes.
    /// This runs against Unity's temporary build scene and never edits GameScene.
    /// </summary>
    internal sealed class DemoSceneBuildStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!DemoStageBuildFilter.IsFilteringDemoBuild) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DestroyStageEditorObjects(roots[i].transform);
            }

            // Named editor subtrees contain most authoring components. Remove
            // them first so the fallback component scans only inspect survivors.
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                StripComponents<RuntimeStageEditor>(roots[i]);
                StripComponents<RuntimeStageEditorButtonCommand>(roots[i]);
                StripComponents<StageEditorVisualPolisher>(roots[i]);
            }
        }

        private static void StripComponents<T>(GameObject root) where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null) Object.DestroyImmediate(components[i]);
            }
        }

        private static void DestroyStageEditorObjects(Transform parent)
        {
            if (parent.name == "RuntimeStageEditorPanel"
                || parent.name == "RuntimeStageEditorRoot")
            {
                Object.DestroyImmediate(parent.gameObject);
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                DestroyStageEditorObjects(child);
            }
        }
    }
}
