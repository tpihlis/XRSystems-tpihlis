using UnityEngine;

public class MagnifyingLens : MonoBehaviour
{
    [Header("Setup")]
    public Camera lensCamera;       // The hidden camera rendering the texture
    public Camera mainCamera;       // The VR Headset Camera
    public Renderer lensRenderer;   // The glass mesh

    [Header("Settings")]
    public float magnification = 2f;

    void Start()
    {
        if (lensCamera != null) lensCamera.enabled = false; // We render manually
    }

    void LateUpdate()
    {
        if (lensCamera == null || mainCamera == null || lensRenderer == null) return;

        // 1. Move Lens Camera to Player's Head (The "Eye" position)
        lensCamera.transform.position = mainCamera.transform.position;

        // 2. Calculate Direction from Eye to Glass Center
        Vector3 lensCenter = lensRenderer.bounds.center;
        Vector3 directionToGlass = (lensCenter - mainCamera.transform.position).normalized;

        // 3. Rotate Lens Camera to look at the glass
        // We use the player's Up vector to prevent the image from spinning weirdly when you tilt your head
        if (directionToGlass != Vector3.zero)
        {
            lensCamera.transform.rotation = Quaternion.LookRotation(directionToGlass, mainCamera.transform.up);
        }

        // 4. Calculate Zoom (FOV)
        // Smaller FOV = High Zoom. 
        lensCamera.fieldOfView = mainCamera.fieldOfView / magnification;

        // 5. Render ONCE (Prevents VR stereo glitching)
        if (lensRenderer.isVisible)
        {
            lensCamera.Render();
        }
    }
}