using UnityEngine;
using UnityEngine.InputSystem;

public class HandController : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference gripAction;
    public InputActionReference triggerAction;

    [Header("Hand")]
    public Hand hand;

    void OnEnable()
    {
        gripAction.action.Enable();
        triggerAction.action.Enable();
    }

    void OnDisable()
    {
        gripAction.action.Disable();
        triggerAction.action.Disable();
    }

    void Update()
    {
        hand.SetGrip(gripAction.action.ReadValue<float>());
        hand.SetTrigger(triggerAction.action.ReadValue<float>());
    }
}
