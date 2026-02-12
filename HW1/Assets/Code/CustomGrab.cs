using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CustomGrab : MonoBehaviour
{
    [Header("Input & Settings")]
    public InputActionReference action;
    public bool enableDoubleRotation = false; // Extra Credit Feature

    [Header("State")]
    public List<Transform> nearObjects = new List<Transform>();
    public Transform grabbedObject = null;
    
    private CustomGrab otherHand = null;
    private Vector3 lastPos;
    private Quaternion lastRot;

    private void Start()
    {
        if (action != null) action.action.Enable();

        // Initialize "Previous" data to prevent jump on first frame
        lastPos = transform.position;
        lastRot = transform.rotation;

        // Find the other hand automatically
        foreach(CustomGrab c in transform.parent.GetComponentsInChildren<CustomGrab>())
        {
            if (c != this) otherHand = c;
        }
    }

    void Update()
    {
        bool isPressed = action != null && action.action.IsPressed();

        // 1. GRAB LOGIC
        if (isPressed && grabbedObject == null)
        {
            // Priority: Near Object -> Steal from Other Hand
            if (nearObjects.Count > 0)
            {
                grabbedObject = nearObjects[0];
            }
            else if (otherHand != null && otherHand.grabbedObject != null)
            {
                // Allow grabbing the same object the other hand is holding (Shared Control)
                grabbedObject = otherHand.grabbedObject;
            }
        }
        else if (!isPressed && grabbedObject != null)
        {
            // Release
            grabbedObject = null;
        }

        // 2. MANIPULATION LOGIC (Delta Math)
        if (grabbedObject != null)
        {
            // Calculate Deltas
            Vector3 deltaPos = transform.position - lastPos;
            Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRot);

            // EXTRA CREDIT: Double Rotation Logic
            if (enableDoubleRotation)
            {
                float angle;
                Vector3 axis;
                deltaRot.ToAngleAxis(out angle, out axis);
                // Double the angle, keep axis same
                deltaRot = Quaternion.AngleAxis(angle * 2f, axis);
            }

            // A. Apply Rotation (Pivot around the controller)
            // "Rotate the vector from the controller origin to the grabbed object"
            Vector3 vectorToObj = grabbedObject.position - transform.position;
            Vector3 rotatedVector = deltaRot * vectorToObj;
            
            // Move object to new pivoted position + Apply pure Translation
            grabbedObject.position = transform.position + rotatedVector + deltaPos;

            // B. Apply Rotation to the object itself
            grabbedObject.rotation = deltaRot * grabbedObject.rotation;
        }

        // 3. Save state for next frame
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Simple tag check (Case insensitive)
        if (other.CompareTag("Grabbable") || other.tag.ToLower() == "grabbable")
        {
            if (!nearObjects.Contains(other.transform))
                nearObjects.Add(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (nearObjects.Contains(other.transform))
            nearObjects.Remove(other.transform);
    }
}