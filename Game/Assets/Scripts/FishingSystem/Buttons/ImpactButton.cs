using UnityEngine;
using UnityEngine.Events;

namespace FishingSystem
{
    /// <summary>
    /// Simple trigger-based "physical" button for VR:
    /// - place a trigger collider where the player should press
    /// - when a collider with the given tag enters, the button fires once and debounces
    /// - uses UnityEvent so you can hook SellManager.SellAll() in the inspector
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ImpactButton : MonoBehaviour
    {
        [Header("Press targets")]
        [Tooltip("Only colliders with this tag will trigger the button. Leave empty to accept any collider.")]
        public string interactorTag = "Interactor";

        [Header("Timing")]
        [Tooltip("Minimum seconds between allowed presses (debounce)")]
        public float cooldownSeconds = 0.5f;

        [Tooltip("Require the interactor to exit the trigger before allowing next press")]
        public bool requireTriggerExitBeforeNext = true;

        [Header("Feedback")]
        public UnityEvent onPressed; // hook SellManager.SellAll() here in inspector

        [Header("Optional visuals")]
        [Tooltip("Optional transform representing the movable button cap/top (will move on press).")]
        public Transform visualCap;
        [Tooltip("How far the cap moves inward when pressed (local Z units).")]
        public float visualPressDistance = 0.02f;
        [Tooltip("How fast the visual cap moves.")]
        public float visualLerpSpeed = 12f;

        bool isCoolingDown = false;
        bool waitingForExit = false;

        // visual state
        Vector3 visualRestLocalPos;
        Vector3 visualPressedLocalPos;

        void Reset()
        {
            // make collider a trigger by default on Add Component
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void Awake()
        {
            if (visualCap != null)
            {
                visualRestLocalPos = visualCap.localPosition;
                visualPressedLocalPos = visualRestLocalPos - (Vector3.forward * Mathf.Abs(visualPressDistance));
            }
        }

        void Update()
        {
            // smoothly move visual cap (if present)
            if (visualCap != null)
            {
                Vector3 target = isCoolingDown && requireTriggerExitBeforeNext ? visualPressedLocalPos : visualRestLocalPos;
                visualCap.localPosition = Vector3.Lerp(visualCap.localPosition, target, Time.deltaTime * visualLerpSpeed);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (isCoolingDown) return;
            if (!string.IsNullOrEmpty(interactorTag) && !other.CompareTag(interactorTag)) return;
            if (requireTriggerExitBeforeNext && waitingForExit) return;

            // Press
            Press();
        }

        void OnTriggerExit(Collider other)
        {
            if (!string.IsNullOrEmpty(interactorTag) && !other.CompareTag(interactorTag)) return;
            // Allow pressing again after the interactor leaves (if that option is on).
            waitingForExit = false;
        }

        void Press()
        {
            // immediate feedback: invoke event
            onPressed?.Invoke();

            // start cooldown and optionally require exit before next
            isCoolingDown = true;
            waitingForExit = requireTriggerExitBeforeNext;

            if (cooldownSeconds > 0f)
                Invoke(nameof(EndCooldown), cooldownSeconds);
            else
                EndCooldown();
        }

        void EndCooldown()
        {
            isCoolingDown = false;
            // do not automatically clear waitingForExit here; it will be cleared on OnTriggerExit
        }

        // Public helper: call this to force a visual press (useful for Play-mode testing)
        public void ForcePressVisual()
        {
            isCoolingDown = true;
            waitingForExit = requireTriggerExitBeforeNext;
            if (cooldownSeconds > 0f)
                Invoke(nameof(EndCooldown), cooldownSeconds);
            else
                EndCooldown();
        }
    }
}