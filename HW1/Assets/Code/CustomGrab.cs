// CustomGrab.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class CustomGrab : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference action;

    [Header("Grab")]
    public float grabRadius = 0.1f;
    public LayerMask grabLayer;
    public Transform handVisualModel; // optional model that snaps to handle while grabbing

    [Header("Reel")]
    public FishingLine fishingLine;
    public float linePerRevolution = 0.5f; // meters per full rotation

    // state
    private GrabHandle currentHandle;
    private Transform grabbedObject;
    private Rigidbody grabbedRb;

    // visuals restore
    private Vector3 originalVisualLocalPos;
    private Quaternion originalVisualLocalRot;

    // velocity estimation (for release)
    private Vector3 lastHandPos;
    private Quaternion lastHandRot;
    private Vector3 estimatedLinearVelocity;
    private Vector3 estimatedAngularVelocity;

    // reel state
    private float lastReelAngle;

    // non-alloc overlap buffer
    private readonly Collider[] overlapResults = new Collider[8];

    private void OnEnable()
    {
        if (action != null) action.action.Enable();
    }

    private void OnDisable()
    {
        if (action != null) action.action.Disable();
    }

    private void Update()
    {
        bool isPressed = action != null && action.action.IsPressed();

        if (isPressed && currentHandle == null)
        {
            var target = GetClosestHandle();
            if (target != null) Grab(target);
        }
        else if (!isPressed && currentHandle != null)
        {
            Release();
        }

        if (currentHandle != null)
        {
            EstimateVelocity();

            if (currentHandle.isReel) UpdateReel();
            else UpdateRod();
        }
    }

    private void EstimateVelocity()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        estimatedLinearVelocity = (transform.position - lastHandPos) / dt;

        Quaternion delta = transform.rotation * Quaternion.Inverse(lastHandRot);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        estimatedAngularVelocity = axis.sqrMagnitude > 0f ? axis.normalized * (angleRad / dt) : Vector3.zero;

        lastHandPos = transform.position;
        lastHandRot = transform.rotation;
    }

    private void Grab(GrabHandle handle)
    {
        currentHandle = handle;
        grabbedObject = handle.objectToMove;
        grabbedRb = grabbedObject ? grabbedObject.GetComponent<Rigidbody>() : null;

        // Save visual transform so we can restore on release
        if (handVisualModel)
        {
            originalVisualLocalPos = handVisualModel.localPosition;
            originalVisualLocalRot = handVisualModel.localRotation;
        }

        // Initialize velocity sampling
        lastHandPos = transform.position;
        lastHandRot = transform.rotation;

        // If grabbing a physical object (not a reel), make it kinematic while held
        if (grabbedRb != null && !currentHandle.isReel)
        {
            grabbedRb.isKinematic = true;
        }

        // If starting a reel, set initial angle
        if (currentHandle.isReel && grabbedObject != null)
        {
            lastReelAngle = ComputeDiscAngle(grabbedObject);
        }

        // Move visual to the handle immediately
        if (handVisualModel)
        {
            handVisualModel.position = currentHandle.transform.position;
            handVisualModel.rotation = currentHandle.transform.rotation * Quaternion.Euler(currentHandle.snapRotationOffset);
        }
    }

    private void Release()
    {
        if (grabbedRb != null && !currentHandle.isReel)
        {
            grabbedRb.isKinematic = false;
            grabbedRb.linearVelocity = estimatedLinearVelocity;
            grabbedRb.angularVelocity = estimatedAngularVelocity;
        }

        if (handVisualModel)
        {
            handVisualModel.localPosition = originalVisualLocalPos;
            handVisualModel.localRotation = originalVisualLocalRot;
        }

        currentHandle = null;
        grabbedObject = null;
        grabbedRb = null;
    }

    private GrabHandle GetClosestHandle()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, overlapResults, grabLayer, QueryTriggerInteraction.Ignore);
        GrabHandle closest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var h = overlapResults[i].GetComponent<GrabHandle>();
            if (h == null) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = h;
            }
        }

        return closest;
    }

    private void UpdateRod()
    {
        if (grabbedObject == null || currentHandle == null) return;

        // Match rotation so handle orientation aligns with controller
        grabbedObject.rotation = transform.rotation * Quaternion.Inverse(currentHandle.transform.localRotation);

        // Compute local handle point and match object position so handle sits at controller
        Vector3 handleLocalPos = grabbedObject.InverseTransformPoint(currentHandle.transform.position);
        grabbedObject.position = transform.position - (grabbedObject.rotation * handleLocalPos);

        if (handVisualModel)
        {
            handVisualModel.position = currentHandle.transform.position;
            handVisualModel.rotation = currentHandle.transform.rotation * Quaternion.Euler(currentHandle.snapRotationOffset);
        }
    }

    private void UpdateReel()
    {
        if (currentHandle == null || grabbedObject == null) return;

        float angle = ComputeDiscAngle(grabbedObject);

        // rotate disc to face hand direction (local Y rotation)
        grabbedObject.localRotation = Quaternion.Euler(0f, angle, 0f);

        if (handVisualModel)
        {
            handVisualModel.position = currentHandle.transform.position;
            handVisualModel.rotation = currentHandle.transform.rotation * Quaternion.Euler(currentHandle.snapRotationOffset);
        }

        float deltaAngle = Mathf.DeltaAngle(lastReelAngle, angle);
        lastReelAngle = angle;

        float deltaLine = (deltaAngle / 360f) * linePerRevolution;
        if (fishingLine != null && Mathf.Abs(deltaLine) > Mathf.Epsilon)
        {
            fishingLine.AddLineLength(deltaLine);
        }
    }

    private float ComputeDiscAngle(Transform disc)
    {
        // compute angle of vector from disc to hand in disc's parent local space
        if (disc.parent == null) return disc.localEulerAngles.y;
        Vector3 dir = transform.position - disc.position;
        Vector3 localDir = disc.parent.InverseTransformDirection(dir);
        float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}
