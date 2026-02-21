// LureController.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LureController : MonoBehaviour
{
    public bool hooked = false;
    FixedJoint attachJoint;

    // attach the lure to a fish (or any Rigidbody)
    public void HookOnto(Rigidbody other)
    {
        if (hooked || other == null) return;
        attachJoint = gameObject.AddComponent<FixedJoint>();
        attachJoint.connectedBody = other;
        hooked = true;
    }

    public void ReleaseHook()
    {
        if (!hooked) return;
        if (attachJoint != null) Destroy(attachJoint);
        hooked = false;
    }
}