using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SocketAttach : MonoBehaviour
{
    [Header("Attach Points")]
    [Tooltip("Transform when grabbing (epmty = default)")]
    public Transform handAttachPoint;

    [Tooltip("Transform when in socket")]
    public Transform socketAttachPoint;

    private XRGrabInteractable grabInteractable;
    private Transform originalAttach;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        originalAttach = grabInteractable.attachTransform;
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        grabInteractable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            grabInteractable.attachTransform = socketAttachPoint;
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (!grabInteractable.isSelected || !(grabInteractable.interactorsSelecting[0] is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor))
        {
            ApplyDefaultAttach();
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor)
        {
            grabInteractable.attachTransform = socketAttachPoint;
        }
    }

    private void ApplyDefaultAttach()
    {
        grabInteractable.attachTransform = (handAttachPoint != null) ? handAttachPoint : originalAttach;
    }  
}
