using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrayonStageBackground : MonoBehaviour
    {
        private const string OldTextureObjectName = "Crayon Background Texture";
        private SpriteRenderer paperRenderer;
        private Material originalMaterial;
        private Material crayonMaterial;

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
