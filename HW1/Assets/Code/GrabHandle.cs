// GrabHandle.cs
using UnityEngine;

/// <summary>
/// Simple handle that should be a child of the object that will move when grabbed.
/// Set objectToMove to the parent transform you want to control (rod, reel disc, etc.)
/// </summary>
public class GrabHandle : MonoBehaviour
{
    public Transform objectToMove;
    public bool isReel = false;
    public Vector3 snapRotationOffset = Vector3.zero;
}
