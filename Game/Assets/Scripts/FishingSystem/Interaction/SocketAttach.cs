using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SocketAttach : MonoBehaviour
{
    [Header("Attach Points")]
    public Transform handAttachPoint;
    public Transform socketAttachPoint;

    private XRGrabInteractable grabInteractable;
    private Transform originalAttach;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
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
        if (!grabInteractable.isSelected || !(grabInteractable.interactorsSelecting[0] is XRSocketInteractor))
        {
            ApplyDefaultAttach();
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            grabInteractable.attachTransform = socketAttachPoint;
        }
    }

    private void ApplyDefaultAttach()
    {
        grabInteractable.attachTransform = (handAttachPoint != null) ? handAttachPoint : originalAttach;
    }  
}