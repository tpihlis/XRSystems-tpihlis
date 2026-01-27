using UnityEngine;
using UnityEngine.InputSystem;

public class Quit : MonoBehaviour
{
    public InputActionReference action;
    void Start()
    {
        action.action.Enable();
        action.action.performed += HandleQuit;
    }

    private void HandleQuit(InputAction.CallbackContext ctx)
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        # else
            Application.Quit();
        #endif
    }

    private void OnDestroy()
    {
        action.action.performed -= HandleQuit;
    }
}
