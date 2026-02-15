// FishingLine.cs
using UnityEngine;

public class FishingLine : MonoBehaviour
{
    public LineRenderer line;
    public Transform rodTip;
    public Rigidbody hookRb;

    public float currentLength = 3f;
    public float minLength = 0.5f;
    public float maxLength = 20f;
    public float tensionStrength = 50f;

    public void AddLineLength(float delta)
    {
        // Reeling in reduces the visible/allowed length
        currentLength = Mathf.Clamp(currentLength - delta, minLength, maxLength);
    }

    private void FixedUpdate()
    {
        if (rodTip == null || hookRb == null) return;

        Vector3 dir = hookRb.position - rodTip.position;
        float dist = dir.magnitude;
        if (dist > currentLength)
        {
            Vector3 tension = -dir.normalized * (dist - currentLength) * tensionStrength;
            hookRb.AddForce(tension, ForceMode.Force);
        }
    }

    private void Update()
    {
        if (rodTip == null || hookRb == null || line == null) return;
        line.positionCount = 2;
        line.SetPosition(0, rodTip.position);
        line.SetPosition(1, hookRb.position);
    }
}
