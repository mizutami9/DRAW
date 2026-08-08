using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageEditorObject : MonoBehaviour
    {
        public string objectId;
        public StageObjectType type;
        public Vector2 size = Vector2.one;
        public float actionStrength;
        public float movementAngle;
        public int spawnPattern;
        public float spawnBoxSize;
        public string linkTargetId;
        public string linkAction;
    }
}
