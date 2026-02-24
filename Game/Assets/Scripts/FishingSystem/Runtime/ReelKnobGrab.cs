// File: ReelKnobGrab.cs
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace FishingSystem
{
    [RequireComponent(typeof(Collider))]
    public class ReelKnobGrab : MonoBehaviour
    {
        [Header("Parts")]
        public Transform axis;
        public Transform disc;
        public FishingLine line;

        [Header("Settings")]
        public float spoolRadius = 0.025f;
        public float turnMultiplier = 1.0f;
        public float maxGrabDistance = 0.8f;
        public XRNode preferredHand = XRNode.RightHand;

        [Header("Debug")]
        public bool debugLogs = false;

        private Transform controller = null;
        private bool isGrabbed = false;
        private float prevAngleDeg = 0.0f;

        private const float EPS = 1e-4f;

        void Awake()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (controller == null)
            {
                controller = other.transform;
                if (debugLogs) DebugLogger.Log("ReelKnob", $"Controller entered trigger: {controller.name}");
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.transform == controller)
            {
                if (debugLogs) DebugLogger.Log("ReelKnob", $"controller left trigger: {controller.name}");
                controller = null;
                if (isGrabbed) Release();
            }
        }

        void Update()
        {
            if (isGrabbed && controller == null)
            {
                if (debugLogs) DebugLogger.Log("ReelKnob", "lost controller while grabbed -> releasing");
                Release();
                return;
            }

            bool grabPressed = ReadGrabInput(preferredHand);

            if (!isGrabbed && controller != null && grabPressed)
            {
                BeginGrab();
            }
            else if (isGrabbed && !grabPressed)
            {
                Release();
            }

            if (isGrabbed) HandleSpin();
        }

        bool ReadGrabInput(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;

            bool pressed = false;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out pressed) && pressed) return true;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed) && pressed) return true;
            return false;
        }

        void BeginGrab()
        {
            isGrabbed = true;
            if (controller != null)
            {
                prevAngleDeg = ComputeAngleDeg(controller.position);
                if (debugLogs) DebugLogger.VerboseLog("ReelKnob", $"BeginGrab prevAngleDeg={prevAngleDeg:F2}");
            }
            else
            {
                prevAngleDeg = 0.0f;
                if (debugLogs) DebugLogger.Log("ReelKnob", "BeginGrab called but controller is null");
            }
        }

        void Release()
        {
            isGrabbed = false;
            if (controller != null)
            {
                prevAngleDeg = ComputeAngleDeg(controller.position);
                if (debugLogs) DebugLogger.VerboseLog("ReelKnob", $"Release updated prevAngleDeg={prevAngleDeg:F2}");
            }
            else
            {
                if (debugLogs) DebugLogger.VerboseLog("ReelKnob", "Release with no controller");
            }
        }

        float ComputeAngleDeg(Vector3 pos)
        {
            Vector3 center = (axis != null) ? axis.position : transform.position;
            Vector3 normal = (axis != null) ? axis.up : transform.up;
            Vector3 rel = pos - center;
            Vector3 proj = Vector3.ProjectOnPlane(rel, normal);

            Vector3 basisX = Vector3.Cross(normal, Vector3.up);
            if (basisX.sqrMagnitude < 1e-4f)
                basisX = Vector3.Cross(normal, Vector3.forward);
            basisX.Normalize();
            Vector3 basisY = Vector3.Cross(normal, basisX);

            float x = Vector3.Dot(proj, basisX);
            float y = Vector3.Dot(proj, basisY);

            float angleRad = Mathf.Atan2(y, x);
            float angleDeg = angleRad * Mathf.Rad2Deg;
            return angleDeg;
        }

        void HandleSpin()
        {
            if (controller == null) { Release(); return; }
            if (disc == null) { if (debugLogs) DebugLogger.Log("ReelKnob", "disc is null - cannot spin"); Release(); return; }

            Vector3 center = (axis != null) ? axis.position : transform.position;
            float dist = Vector3.Distance(controller.position, center);
            if (dist > maxGrabDistance)
            {
                if (debugLogs) DebugLogger.Log("ReelKnob", $"controller too far ({dist:F2}) -> releasing");
                Release();
                return;
            }

            float angle = ComputeAngleDeg(controller.position);
            float delta = Mathf.DeltaAngle(prevAngleDeg, angle);
            float lengthDelta = -delta * Mathf.Deg2Rad * spoolRadius * turnMultiplier;

            if (line != null)
            {
                bool atMin = (line.CurrentLength <= line.minLength + EPS);
                bool atMax = (line.CurrentLength >= line.maxLength - EPS);
                if ((atMin && lengthDelta < 0.0f) || (atMax && lengthDelta > 0.0f))
                {
                    prevAngleDeg = angle;
                    return;
                }
            }

            prevAngleDeg = angle;

            Vector3 rotAxis = (axis != null) ? axis.up : transform.up;
            Vector3 pivot = center;
            if (disc != null) disc.RotateAround(pivot, rotAxis, delta);

            if (line != null && Mathf.Abs(lengthDelta) > EPS)
            {
                line.ApplySpoolDelta(lengthDelta);
            }
        }
    }
}