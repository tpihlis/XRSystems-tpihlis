using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(Collider))]
public class ReelKnobGrab : MonoBehaviour
{
    [Header("Parts")]
    // spool center (child of pole) - used as the rotation center
    public Transform axis;
    // visual disc that should rotate around the axis
    public Transform disc;
    // reference to the fishing line script to change length
    public FishingLine line;

    [Header("Settings")]
    public float spoolRadius = 0.025f;
    public float turnMultiplier = 1.0f;
    public float maxGrabDistance = 0.8f;
    public XRNode preferredHand = XRNode.RightHand;

    [Header("Debug")]
    // set true to enable helpful logs
    public bool debugLogs = false;

    // runtime state
    private Transform controller = null; // the controller transform that is inside the trigger
    private bool isGrabbed = false;      // true while user holds grab
    private float prevAngleDeg = 0.0f;   // angle measured when last frame handled

    // Keep a small epsilon for safety
    private const float EPS = 1e-4f;

    void Awake()
    {
        // make sure this collider acts as a trigger detector
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    // When some collider enters the knob area, remember it as the possible controller
    void OnTriggerEnter(Collider other)
    {
        if (controller == null)
        {
            controller = other.transform;
            if (debugLogs) Debug.Log("[ReelKnob] controller set to " + controller.name);
        }
    }

    // When that collider leaves, clear it and release if currently grabbed
    void OnTriggerExit(Collider other)
    {
        if (other.transform == controller)
        {
            if (debugLogs) Debug.Log("[ReelKnob] controller left trigger: " + controller.name);
            controller = null;
            if (isGrabbed)
            {
                Release();
            }
        }
    }

    void Update()
    {
        // If we think we are grabbed but controller vanished for some reason, release
        if (isGrabbed && controller == null)
        {
            if (debugLogs) Debug.Log("[ReelKnob] lost controller while grabbed -> releasing");
            Release();
            return;
        }

        // Read whether the user is pressing grab on the preferred hand
        bool grabPressed = ReadGrabInput(preferredHand);

        // Start a grab if the controller is present and the user just pressed
        if (!isGrabbed && controller != null && grabPressed)
        {
            BeginGrab();
        }
        // End a grab if we are grabbed and the user released inputs
        else if (isGrabbed && !grabPressed)
        {
            Release();
        }

        // If we are currently grabbed, handle rotation and line spool
        if (isGrabbed)
        {
            HandleSpin();
        }
    }

    // Returns true if the user is pressing grip or trigger on the XRNode
    bool ReadGrabInput(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;

        bool pressed = false;
        // check grip first then trigger
        if (device.TryGetFeatureValue(CommonUsages.gripButton, out pressed) && pressed) return true;
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed) && pressed) return true;
        return false;
    }

    // Called when the player begins grabbing the knob
    void BeginGrab()
    {
        isGrabbed = true;
        if (controller != null)
        {
            prevAngleDeg = ComputeAngleDeg(controller.position);
            if (debugLogs) Debug.Log("[ReelKnob] BeginGrab prevAngleDeg=" + prevAngleDeg.ToString("F2"));
        }
        else
        {
            // safety fallback
            prevAngleDeg = 0.0f;
            if (debugLogs) Debug.LogWarning("[ReelKnob] BeginGrab called but controller is null");
        }
    }

    // Called when the player releases grab (or controller leaves)
    void Release()
    {
        isGrabbed = false;

        // keep prevAngleDeg in a safe state: if controller still present, update to current
        if (controller != null)
        {
            prevAngleDeg = ComputeAngleDeg(controller.position);
            if (debugLogs) Debug.Log("[ReelKnob] Release updated prevAngleDeg=" + prevAngleDeg.ToString("F2"));
        }
        else
        {
            // if controller is missing, leave prevAngleDeg untouched;
            // BeginGrab will overwrite it on next grab to avoid jumps.
            if (debugLogs) Debug.Log("[ReelKnob] Release with no controller");
        }
    }

    // Compute angle (degrees) of a world position around the axis
    float ComputeAngleDeg(Vector3 pos)
    {
        // center = axis position if available, else this object's position
        Vector3 center = (axis != null) ? axis.position : transform.position;
        // normal vector for the rotation plane (axis.up if axis set, else this transform.up)
        Vector3 normal = (axis != null) ? axis.up : transform.up;

        // vector from center to the position
        Vector3 rel = pos - center;

        // project that vector onto the plane defined by normal
        Vector3 proj = Vector3.ProjectOnPlane(rel, normal);

        // Create an orthonormal basis on the plane: basisX and basisY
        Vector3 basisX = Vector3.Cross(normal, Vector3.up);
        if (basisX.sqrMagnitude < 1e-4f)
        {
            // normal was nearly parallel to Vector3.up; pick another fallback axis
            basisX = Vector3.Cross(normal, Vector3.forward);
        }
        basisX.Normalize();
        Vector3 basisY = Vector3.Cross(normal, basisX);

        float x = Vector3.Dot(proj, basisX);
        float y = Vector3.Dot(proj, basisY);

        float angleRad = Mathf.Atan2(y, x);
        float angleDeg = angleRad * Mathf.Rad2Deg;
        return angleDeg;
    }

    // Handle rotating the disc and applying spool delta to the line
    void HandleSpin()
    {
        if (controller == null)
        {
            // safety: if controller unexpectedly lost, release
            Release();
            return;
        }

        if (disc == null)
        {
            // nothing to rotate
            if (debugLogs) Debug.LogWarning("[ReelKnob] disc is null - cannot spin");
            Release();
            return;
        }

        // distance safety check
        Vector3 center = (axis != null) ? axis.position : transform.position;
        float dist = Vector3.Distance(controller.position, center);
        if (dist > maxGrabDistance)
        {
            if (debugLogs) Debug.Log("[ReelKnob] controller too far (" + dist.ToString("F2") + ") -> releasing");
            Release();
            return;
        }

        // compute current angle and delta since last frame
        float angle = ComputeAngleDeg(controller.position);
        float delta = Mathf.DeltaAngle(prevAngleDeg, angle); // handles wrap-around

        // convert angular delta to linear spool delta (negative -> reel in)
        float lengthDelta = -delta * Mathf.Deg2Rad * spoolRadius * turnMultiplier;

        // handle line limits: if at min/max, just update prevAngle and don't rotate or apply delta
        if (line != null)
        {
            bool atMin = (line.CurrentLength <= line.minLength + EPS);
            bool atMax = (line.CurrentLength >= line.maxLength - EPS);
            if ((atMin && lengthDelta < 0.0f) || (atMax && lengthDelta > 0.0f))
            {
                // avoid applying rotation/length change; update prevAngle to avoid jump later
                prevAngleDeg = angle;
                return;
            }
        }

        // commit the angle for next frame
        prevAngleDeg = angle;

        // rotate the disc visually around the axis center (this avoids "orbiting" / floating)
        Vector3 rotAxis = (axis != null) ? axis.up : transform.up;

        // We rotate the disc around the axis position so it spins correctly.
        // Using RotateAround ensures the disc stays centered on the spool.
        Vector3 pivot = center;
        disc.RotateAround(pivot, rotAxis, delta);

        // finally, apply the spool length delta to the line
        if (line != null && Mathf.Abs(lengthDelta) > EPS)
        {
            line.ApplySpoolDelta(lengthDelta);
        }
    }
}