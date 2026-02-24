// File: FishingLine.cs
using UnityEngine;

namespace FishingSystem
{
    [RequireComponent(typeof(LineRenderer))]
    public class FishingLine : MonoBehaviour
    {
        [Header("References")]
        public Transform rodTip;
        public Rigidbody lureRb;

        [Header("Length")]
        public float maxLength = 20f;
        public float minLength = 0.5f;
        float currentLength;
        public float CurrentLength => currentLength;

        [Header("Physics")]
        public float reelPullStrength = 30f;
        public float tensionStrength = 50f;
        [Range(0f, 1f)] public float velocityDamping = 0.8f;

        [Header("Rendering")]
        public int segments = 8;

        LineRenderer lr;

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.positionCount = Mathf.Max(2, segments + 1);

            if (rodTip && lureRb)
                currentLength = Mathf.Clamp(Vector3.Distance(rodTip.position, lureRb.position), minLength, maxLength);
            else
                currentLength = maxLength;
        }

        void FixedUpdate()
        {
            if (rodTip == null || lureRb == null) return;

            Vector3 toRod = rodTip.position - lureRb.position;
            float dist = toRod.magnitude;
            if (dist <= currentLength) return;

            Vector3 dir = toRod / dist;
            float excess = dist - currentLength;

            lureRb.AddForce(dir * excess * tensionStrength, ForceMode.Acceleration);

            Vector3 vel = lureRb.linearVelocity;
            float awaySpeed = Vector3.Dot(vel, -dir);
            if (awaySpeed > 0f)
                lureRb.linearVelocity += dir * awaySpeed * velocityDamping;
        }

        void Update()
        {
            if (rodTip == null || lureRb == null) return;

            Vector3 lurePos = lureRb.position;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                lr.SetPosition(i, Vector3.Lerp(rodTip.position, lurePos, t));
            }
        }

        public void ApplySpoolDelta(float delta)
        {
            float target = Mathf.Clamp(currentLength + delta, minLength, maxLength);
            float applied = target - currentLength;
            currentLength = target;

            if (applied < -1e-6f && lureRb != null)
            {
                Vector3 dir = (rodTip.position - lureRb.position).normalized;
                lureRb.AddForce(dir * (-applied) * reelPullStrength, ForceMode.Acceleration);
            }

            DebugLogger.VerboseLog("FishingLine", $"ApplySpoolDelta delta={delta:F4} applied={applied:F4} newLength={currentLength:F4}");
        }
    }
}