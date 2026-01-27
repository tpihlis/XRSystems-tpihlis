using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class VRTeleportToggle : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform locationA;
    public Transform locationB;
    
    public InputActionReference teleportAction;

    private bool isAtLocationB = false;

    private void Start()
    {
        teleportAction.action.Enable();
        teleportAction.action.performed += OnTeleportButtonPressed;
    }

    private void OnTeleportButtonPressed(InputAction.CallbackContext ctx)
    {
        // target
        Transform target = isAtLocationB ? locationA : locationB;

        // mvoe rig
        xrOrigin.MoveCameraToWorldLocation(target.position);
        
        // align rotation
        xrOrigin.MatchOriginUpCameraForward(target.up, target.forward);

        // update state
        isAtLocationB = !isAtLocationB;
    }

    private void OnDestroy()
    {
        teleportAction.action.performed -= OnTeleportButtonPressed;
    }
}