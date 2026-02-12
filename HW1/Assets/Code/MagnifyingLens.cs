using UnityEngine;

public class MagnifyingLens : MonoBehaviour
{
    [Header("Setup")]
    public Camera lensCamera;       // hidden camera rendering the texture
    public Camera mainCamera;       // VR Headset Camera
    public Renderer lensRenderer;   // glass mesh

    [Header("Settings")]
    public float magnification = 2f;

    void Start()
    {
        if (lensCamera != null) lensCamera.enabled = false; 
    }

    void LateUpdate()
    {
        if (lensCamera == null || mainCamera == null || lensRenderer == null) return;

        // move lens camera to player's head
        lensCamera.transform.position = mainCamera.transform.position;

        //calculate direction from eye to glass center
        Vector3 lensCenter = lensRenderer.bounds.center;
        Vector3 directionToGlass = (lensCenter - mainCamera.transform.position).normalized;

        // rotate lens camera to look at the glass
        // using player's ip vector to prevent the image from spinning weirdly when tilting head
        if (directionToGlass != Vector3.zero)
        {
            lensCamera.transform.rotation = Quaternion.LookRotation(directionToGlass, mainCamera.transform.up);
        }

        // zoom 
        lensCamera.fieldOfView = mainCamera.fieldOfView / magnification;

        // render once
        if (lensRenderer.isVisible)
        {
            lensCamera.Render();
        }
    }
}