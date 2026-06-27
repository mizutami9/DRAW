using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageEditorObject : MonoBehaviour
    {
        public string objectId;
        public StageObjectType type;
        public Vector2 size = Vector2.one;
    }
}
