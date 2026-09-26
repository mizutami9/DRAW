using UnityEngine;

namespace DrawBody.Prototype
{
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrayonStageBackground : MonoBehaviour
    {
        private const string OldTextureObjectName = "Crayon Background Texture";
        private const float ViewPadding = 6f;
        private SpriteRenderer paperRenderer;
        private Material originalMaterial;
        private Material crayonMaterial;
        private Camera targetCamera;
        private float backgroundWorldZ;
        private bool capturedWorldZ;

        private void Awake()
        {
            paperRenderer = GetComponent<SpriteRenderer>();
            Configure(
                paperRenderer != null ? paperRenderer.color : StageBackgroundAppearance.DefaultColor,
                paperRenderer);
        }

        public void Configure(Color backgroundColor, SpriteRenderer sourceRenderer)
        {
            paperRenderer = sourceRenderer != null ? sourceRenderer : GetComponent<SpriteRenderer>();
            if (paperRenderer == null)
            {
                return;
            }

            DisableOldOverlay();
            EnsureMaterial();
            paperRenderer.color = backgroundColor;
            if (crayonMaterial != null && paperRenderer.sharedMaterial != crayonMaterial)
            {
                paperRenderer.sharedMaterial = crayonMaterial;
            }
            SyncToCamera();
        }

        private void LateUpdate()
        {
            SyncToCamera();
        }

        private void SyncToCamera()
        {
            if (paperRenderer == null)
            {
                paperRenderer = GetComponent<SpriteRenderer>();
            }
            if (paperRenderer == null || paperRenderer.sprite == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (targetCamera == null
                || !targetCamera.isActiveAndEnabled
                || (mainCamera != null && targetCamera != mainCamera))
            {
                targetCamera = mainCamera;
            }
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            if (!capturedWorldZ)
            {
                backgroundWorldZ = transform.position.z;
                capturedWorldZ = true;
            }

            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, backgroundWorldZ);

            float requiredWorldHeight = targetCamera.orthographicSize * 2f + ViewPadding * 2f;
            float requiredWorldWidth = targetCamera.orthographicSize
                * 2f
                * Mathf.Max(0.1f, targetCamera.aspect)
                + ViewPadding * 2f;
            Vector2 spriteSize = paperRenderer.sprite.bounds.size;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float scaleX = requiredWorldWidth
                / Mathf.Max(0.001f, Mathf.Abs(spriteSize.x * parentScale.x));
            float scaleY = requiredWorldHeight
                / Mathf.Max(0.001f, Mathf.Abs(spriteSize.y * parentScale.y));
            Vector3 localScale = transform.localScale;
            transform.localScale = new Vector3(scaleX, scaleY, localScale.z);
        }

        private void EnsureMaterial()
        {
            if (crayonMaterial != null)
            {
                return;
            }

            Shader shader = Resources.Load<Shader>("Shaders/CrayonBackground");
            if (shader == null) shader = Shader.Find("DrawBody/CrayonBackground");
            if (shader == null)
            {
                return;
            }

            originalMaterial = paperRenderer.sharedMaterial;
            crayonMaterial = new Material(shader)
            {
                name = "Notebook Crayon Background Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            crayonMaterial.SetFloat("_PencilStrength", 0.50f);
        }

        private void DisableOldOverlay()
        {
            Transform oldOverlay = transform.Find(OldTextureObjectName);
            if (oldOverlay != null)
            {
                oldOverlay.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (paperRenderer != null
                && crayonMaterial != null
                && paperRenderer.sharedMaterial == crayonMaterial)
            {
                paperRenderer.sharedMaterial = originalMaterial;
            }
            if (crayonMaterial != null)
            {
                Destroy(crayonMaterial);
            }
        }
    }
}
