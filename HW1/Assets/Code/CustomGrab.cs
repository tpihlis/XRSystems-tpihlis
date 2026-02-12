using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CustomGrab : MonoBehaviour
{
    // This script should be attached to both controller objects in the scene
    // Make sure to define the input in the editor (LeftHand/Grip and RightHand/Grip recommended respectively)
    
    [Header("Input & Settings")]
    public InputActionReference action;
    public bool enableDoubleRotation = false; // extra credit

    [Header("State")]
    public List<Transform> nearObjects = new List<Transform>();
    public Transform grabbedObject = null;
    
    private CustomGrab otherHand = null;
    
    // keep track of where the controller was in the last frame
    private Vector3 lastPos;
    private Quaternion lastRot;

    private void Start()
    {
        if (action != null) action.action.Enable();

        // storing the starting position and rotation so the object doesn't 
        // teleport or "jump" the very first second I grab it.
        lastPos = transform.position;
        lastRot = transform.rotation;

        // Find the other hand
        foreach(CustomGrab c in transform.parent.GetComponentsInChildren<CustomGrab>())
        {
            if (c != this) otherHand = c;
        }
    }

    void Update()
    {
        // checking if the grip button is actually down
        bool isPressed = action != null && action.action.IsPressed();

        // GRABBING LOGIC
        if (isPressed && grabbedObject == null)
        {
            // if touching something, grab that first. 
            // if not, check if the other hand is holding something so we can do the 2-handed grab.
            if (nearObjects.Count > 0)
            {
                grabbedObject = nearObjects[0];
            }
            else if (otherHand != null && otherHand.grabbedObject != null)
            {
                grabbedObject = otherHand.grabbedObject;
            }
        }
        else if (!isPressed && grabbedObject != null)
        {
            // let go if the button isn't pressed
            grabbedObject = null;
        }

        // MOVEMENT LOGIC 
        if (grabbedObject != null)
        {
            // calculate how much the controller moved and rotated since the last frame
            Vector3 deltaPos = transform.position - lastPos;
            
            // using inverse here because the order of operations matters for Quaternions
            Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRot);

            // make the rotation twice as fast if the toggle is on
            if (enableDoubleRotation)
            {
                float angle;
                Vector3 axis;
                // nreak it down into axis/angle so can just multiply the angle by 2
                deltaRot.ToAngleAxis(out angle, out axis);
                deltaRot = Quaternion.AngleAxis(angle * 2f, axis);
            }

            // PIVOTING
            // need to rotate the offset vector between the hand and the object
            Vector3 vectorToObj = grabbedObject.position - transform.position;
            Vector3 rotatedVector = deltaRot * vectorToObj;
            
            // move the object to the new rotated offset and then add the hand's move 
            grabbedObject.position = transform.position + rotatedVector + deltaPos;

            // OBJECT ROTATION
            // apply the hand's rotation change to the object's current rotation
            grabbedObject.rotation = deltaRot * grabbedObject.rotation;
        }

        // Should save the current position and rotation here
        // saving these at the very end so they are ready for the next frame's math
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Make sure to tag grabbable objects with the "grabbable" tag
        // checking tag
        if (other.CompareTag("Grabbable") || other.tag.ToLower() == "grabbable")
        {
            if (!nearObjects.Contains(other.transform))
                nearObjects.Add(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // remove it from the list when moved hand away
        if (nearObjects.Contains(other.transform))
            nearObjects.Remove(other.transform);
    }
}