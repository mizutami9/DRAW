using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageOneGimmickController : MonoBehaviour
    {
        private const string BridgeId = "stage1_bridge";
        private const string BridgeButtonId = "stage1_bridge_button";
        private const string JumpPadId = "stage1_jump_pad";
        private const string JumpButtonId = "stage1_jump_button";

        [SerializeField] private float bridgeBuildSpeed = 0.32f;

        private Transform bridge;
        private Collider2D bridgeCollider;
        private Vector3 bridgeFullScale = Vector3.one;
        private float bridgeProgress;
        private bool bridgeBuilding;
        private GameObject jumpPad;

        private void Start()
        {
            bridge = FindStageObject(BridgeId);
            if (bridge != null)
            {
                bridgeFullScale = bridge.localScale;
                bridge.localScale = new Vector3(0.03f, bridgeFullScale.y, bridgeFullScale.z);
                bridgeCollider = bridge.GetComponent<Collider2D>();
                if (bridgeCollider != null)
                {
                    bridgeCollider.enabled = false;
                }

                bridge.gameObject.SetActive(false);
            }

            Transform jumpPadTransform = FindStageObject(JumpPadId);
            if (jumpPadTransform != null)
            {
                jumpPad = jumpPadTransform.gameObject;
                jumpPad.SetActive(false);
            }

            AddButtonReporter(BridgeButtonId, ActivateBridge);
            AddButtonReporter(JumpButtonId, ActivateJumpPad);
        }

        private void Update()
        {
            if (!bridgeBuilding || bridge == null)
            {
                return;
            }

            bridgeProgress = Mathf.MoveTowards(bridgeProgress, 1f, bridgeBuildSpeed * Time.deltaTime);
            bridge.localScale = new Vector3(Mathf.Max(0.03f, bridgeFullScale.x * bridgeProgress), bridgeFullScale.y, bridgeFullScale.z);
            if (bridgeCollider != null)
            {
                bridgeCollider.enabled = bridgeProgress > 0.92f;
            }
        }

        private void ActivateBridge()
        {
            if (bridge != null && !bridge.gameObject.activeSelf)
            {
                bridge.gameObject.SetActive(true);
                bridge.localScale = new Vector3(0.03f, bridgeFullScale.y, bridgeFullScale.z);
            }

            bridgeBuilding = true;
        }

        private void ActivateJumpPad()
        {
            if (jumpPad != null)
            {
                jumpPad.SetActive(true);
            }
        }

        private void AddButtonReporter(string objectId, System.Action action)
        {
            Transform target = FindStageObject(objectId);
            if (target == null)
            {
                return;
            }

            StageOneButtonReporter reporter = target.gameObject.AddComponent<StageOneButtonReporter>();
            reporter.Configure(action);
        }

        private Transform FindStageObject(string objectId)
        {
            StageEditorObject[] objects = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].objectId == objectId)
                {
                    return objects[i].transform;
                }
            }

            return null;
        }
    }

    public sealed class StageOneButtonReporter : MonoBehaviour
    {
        private System.Action action;
        private bool pressed;
        private Transform cap;

        public void Configure(System.Action onPress)
        {
            action = onPress;
            Transform capTransform = transform.Find("Button Cap");
            if (capTransform != null)
            {
                cap = capTransform;
            }
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
