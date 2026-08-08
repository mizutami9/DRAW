using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageCollectible : MonoBehaviour
    {
        [SerializeField] private string objectId;
        [SerializeField] private StageObjectType collectibleType;
        private bool collected;

        public string ObjectId => objectId;
        public StageObjectType CollectibleType => collectibleType;

        public void Configure(string id, StageObjectType type)
        {
            objectId = id;
            collectibleType = type;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other.GetComponentInParent<PlayerController2D>() == null)
            {
                return;
            }

            StageManager manager = FindFirstObjectByType<StageManager>();
            manager?.TryCollect(this);
        }

        public void ApplyCollected()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            gameObject.SetActive(false);
        }
    }
}
