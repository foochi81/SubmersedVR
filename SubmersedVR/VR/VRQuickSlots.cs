using UnityEngine;
using System.Linq;
using UnityEngine.UI;

namespace SubmersedVR
{
    extern alias SteamVRActions;
    extern alias SteamVRRef;
    using SteamVRRef.Valve.VR;
    using SteamVRActions.Valve.VR;

    // This class implements a way to switch between tools on the quickbar using a radial menu.
    // It was inspired by the Inventory/Quick Select form Half Life: Alyx.
    public class VRQuickSlots : uGUI_QuickSlots
    {
        private bool setup = false;
        private bool active = false;
        private SteamVR_Action_Boolean action;

        private Transform controllerTarget;
        public float wheelRadius = 100.0f;

        public float threshold = 0.025f;
        public float angleOffset = -Mathf.PI / 2.0f;

        public int lastSlot = -1;
        private int currentSlot = -2;
        private Canvas canvas;

        private int nSlots
        {
            get
            {
                return icons.Length;
            }
        }

        void Awake()
        {
            gameObject.AddComponent<RectTransform>();
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.layer = LayerID.UI;
            transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
            controllerTarget = VRCameraRig.instance.rightControllerUI.transform;
            canvas.enabled = false;
        }

        new void Start()
        {
            var qs = FindObjectsOfType<uGUI_QuickSlots>().First(obj => obj.name == "QuickSlots");
            Mod.logger.LogInfo($"[nameof{this.GetType()}] Start, stealing stuff from on {qs.name}");
            materialBackground = qs.materialBackground;
            spriteLeft = qs.spriteLeft;
            spriteCenter = qs.spriteCenter;
            spriteRight = qs.spriteRight;
            spriteNormal = qs.spriteNormal;
            spriteHighlighted = qs.spriteHighlighted;
            spriteExosuitArm = qs.spriteExosuitArm;
            spriteSelected = qs.spriteSelected;
        }

        new void Init(IQuickSlots newTarget)
        {
            Mod.logger.LogInfo($"[nameof{this.GetType()}] Init on {newTarget}");
            base.Init(newTarget);
            ArangeIconsInCircle(wheelRadius);
            OnSelect(this.target.GetActiveSlotID());
        }

        void ArangeIconsInCircle(float radius)
        {
            for (int i = 0; i < nSlots; i++)
            {
                var pos = CirclePosition(i, nSlots, radius);
                icons[i].rectTransform.anchoredPosition = new Vector3(pos.x, pos.y, 0);
                backgrounds[i].rectTransform.anchoredPosition = new Vector3(pos.x, pos.y, 0);
            }
        }

        int DetermineSlot(float angle)
        {
            return Mathf.RoundToInt((angle / (2 * Mathf.PI)) * nSlots) % nSlots;
        }

        new void Update()
        {
            IQuickSlots quickSlots = this.GetTarget();
            if (this.target != quickSlots)
            {
                this.target = quickSlots;
                this.Init(this.target);
            }
            if (active)
            {
                var proejctedControllerPos = Vector3.ProjectOnPlane(controllerTarget.position, transform.forward);

                var from = controllerTarget.position;
                var origin = transform.position;
                var pX = Vector3.Dot(from - origin, transform.right);
                var pY = Vector3.Dot(from - origin, transform.up);
                var projected = new Vector2(pX, pY);

                // var relPos = transform.position - controllerTarget.position;
                var angle = Mathf.Atan2(projected.y, projected.x);
                angle += angleOffset;
                if (angle < 0.0f)
                {
                    angle += 2 * Mathf.PI;
                }

                var distance = projected.sqrMagnitude;
                // float stepSize = 2 * Mathf.PI / nSlots;

                var doSwitch = distance > threshold;
                // angle += Mathf.PI/2.0f;
                // var angleWithOffset = angle + angleOffset;
                // TODO: Test if this works with vehicles too
                // TODO: Probably should use events to determine current slot, extending interface methods
                if (doSwitch)
                {
                    lastSlot = currentSlot;
                    currentSlot = DetermineSlot(angle);
                    if (currentSlot != lastSlot)
                    {
                        QuickSlots qs = GetTarget() as QuickSlots;
                        qs.Select(currentSlot);
                        SteamVR_Actions.subnautica_HapticsRight.Execute(0.0f, 0.1f, 10f, 0.5f, SteamVR_Input_Sources.Any);
                    }
                }
                else
                {
                    QuickSlots qs = GetTarget() as QuickSlots;
                    qs.Deselect();
                }

                // DebugPanel.Show($"y:{projected.y:f3} x:{projected.x:f3} -> {angle:f3}\n {distance:f3} -> {doSwitch}\n {angle:f3} -> {currentSlot}");
                this.selector.enabled = false;
            }
        }

        public void Activate(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            canvas.enabled = true;
            transform.position = controllerTarget.transform.position;

            // TODO: This still could use some tweaking, maybe just align with the controller
            var targetPos = VRCameraRig.instance.uiCamera.transform.position;
            SteamVR_Actions.subnautica_HapticsRight.Execute(0.0f, 0.1f, 10f, 0.5f, SteamVR_Input_Sources.Any);
            targetPos.y = transform.position.y;
            this.transform.LookAt(targetPos);

            currentSlot = -2;
            lastSlot = -1;
            active = true;
        }
        public void Deactivate(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            canvas.enabled = false;
            active = false;
        }

        public void Setup(SteamVR_Action_Boolean activeAction)
        {
            if (setup)
            {
                Mod.logger.LogWarning($"Trying to setup {nameof(VRQuickSlots)} twice!");
                return;
            }
            // Setup the actions/callbacks from steam
            // TODO: Not sure if this is the right call here relying totaly on the SteamVR Input system
            action = activeAction;
            action.onStateDown += Activate;
            action.onStateUp += Deactivate;
            setup = true;
        }

        private static Vector2 CirclePosition(int i, int nSlots, float radius = 10.0f)
        {
            float stepSize = 2 * Mathf.PI / nSlots;
            float angle = i * stepSize;
            angle += Mathf.PI / 2.0f; // Offset by 90°, so the layout is better with item 1 being at the top
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
