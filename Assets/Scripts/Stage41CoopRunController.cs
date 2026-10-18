using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class Stage41CoopRunController : MonoBehaviour
    {
        private const string UpperPlatformId = "4-1-coop-upper-platform";
        private const string DriveButtonId = "4-1-coop-drive-button";

        private Transform upperPlatform;
        private Rigidbody2D buttonBody;
        private Vector2 buttonOffset;

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform driveButton = null;
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject item = objects[i];
                if (item == null) continue;
                if (item.objectId == UpperPlatformId) upperPlatform = item.transform;
                else if (item.objectId == DriveButtonId) driveButton = item.transform;
            }

            if (upperPlatform == null || driveButton == null)
            {
                Debug.LogWarning("Stage 4-1 co-op run objects are incomplete.");
                enabled = false;
                return;
            }

            buttonOffset = driveButton.position - upperPlatform.position;
            buttonBody = EnsureKinematicBody(driveButton.gameObject);
        }

        private void FixedUpdate()
        {
            if (upperPlatform == null) return;
            Vector2 upper = upperPlatform.position;
            if (buttonBody != null) buttonBody.MovePosition(upper + buttonOffset);
        }

        private static Rigidbody2D EnsureKinematicBody(GameObject target)
        {
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body == null) body = target.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return body;
        }
    }
}
