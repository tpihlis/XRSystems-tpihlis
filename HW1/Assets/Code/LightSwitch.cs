using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class LightColorShifter : MonoBehaviour
{
    public Light targetLight;
    public InputActionReference toggleAction;

    [Header("Color Settings")]
    public Color colorA = Color.blue;
    public Color colorB = Color.red;
    public float transitionDuration = 1.0f;

    private bool isColorA = true;
    private Coroutine colorChangeCoroutine;

    void Start()
    {
        if (toggleAction != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnTogglePressed;
        }
        
        // Set initial color
        if (targetLight != null) targetLight.color = colorA;
    }

    private void OnTogglePressed(InputAction.CallbackContext ctx)
    {
        if (targetLight == null) return;

        // Swap the target state
        isColorA = !isColorA;
        Color nextColor = isColorA ? colorA : colorB;

        // Stop the current transition if one is running so they don't fight
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        
        // Start the smooth slide
        colorChangeCoroutine = StartCoroutine(SmoothColorChange(nextColor));
    }

    private IEnumerator SmoothColorChange(Color endColor)
    {
        float elapsed = 0;
        Color startColor = targetLight.color;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            // The magic formula: (Start, End, 0-1 percentage)
            targetLight.color = Color.Lerp(startColor, endColor, elapsed / transitionDuration);
            yield return null; 
        }

        targetLight.color = endColor;
    }

    private void OnDestroy()
    {
        if (toggleAction != null)
            toggleAction.action.performed -= OnTogglePressed;
    }
}