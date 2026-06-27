using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageLoader : MonoBehaviour
    {
        [SerializeField] private Transform stageRoot;
        [SerializeField] private GameObject fallbackStageRoot;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private StageObjectFactory objectFactory;

        public void ShowFallbackStage()
        {
            ClearStageRoot();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(true);
            }
        }

        public void HideStages()
        {
            ClearStageRoot();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }
        }

        public bool LoadStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                return false;
            }

            TextAsset asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset == null)
            {
                Debug.LogWarning($"Stage JSON not found: Resources/Stages/{stageId}. Falling back to debug stage.");
                ShowFallbackStage();
                return false;
            }

            StageData data = JsonUtility.FromJson<StageData>(asset.text);
            if (data == null)
            {
                ShowFallbackStage();
                return false;
            }

            LoadStage(data);
            return true;
        }

        public void LoadStage(StageData data)
        {
            if (data == null)
            {
                return;
            }

            EnsureReferences();
            ClearStageRoot();
            if (fallbackStageRoot != null)
            {
                fallbackStageRoot.SetActive(false);
            }

            for (int i = 0; i < data.objects.Length; i++)
            {
                StageObjectData obj = data.objects[i];
                if (obj == null)
                {
                    continue;
                }

                if (obj.type == StageObjectType.Spawn)
                {
                    if (spawnPoint != null)
                    {
                        spawnPoint.position = obj.position;
                    }
                    objectFactory.Create(obj, stageRoot);
                    continue;
                }

                objectFactory.Create(obj, stageRoot);
            }
        }

        private void EnsureReferences()
        {
            if (stageRoot == null)
            {
                GameObject root = GameObject.Find("RuntimeStageRoot");
                if (root == null)
                {
                    root = new GameObject("RuntimeStageRoot");
                }

                stageRoot = root.transform;
            }

            if (objectFactory == null)
            {
                objectFactory = GetComponent<StageObjectFactory>();
                if (objectFactory == null)
                {
                    objectFactory = gameObject.AddComponent<StageObjectFactory>();
                }
            }
        }

        private void ClearStageRoot()
        {
            if (stageRoot == null)
            {
                return;
            }

            for (int i = stageRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(stageRoot.GetChild(i).gameObject);
            }
        }
    }
}
