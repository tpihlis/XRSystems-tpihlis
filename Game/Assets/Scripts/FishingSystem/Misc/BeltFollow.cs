using UnityEngine;

namespace FishingSystem
{
    public class BeltFollow : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;

        [Header("Settings")]
        [Tooltip("How far below the belt is")]
        public float yOffset = -0.5f;

        [Tooltip("How much the belt stays behind when leaning")]
        public float followSpeed = 10f;

        void LateUpdate()
        {
            if (cameraTransform == null)
            {
                DebugLogger.VerboseLog("BeltFollow", "cameraTransform is null");
                return;
            }

            Vector3 targetPosition = new Vector3(
                cameraTransform.position.x,
                cameraTransform.position.y + yOffset,
                cameraTransform.position.z
            );

            Quaternion targetRotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
        }
    }
}