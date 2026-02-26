using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Per-gameobject component that stores the FishData produced by FishFactory,
    /// tracks pending/active state, and knows how to return itself to its origin pool.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FishInstance : MonoBehaviour
    {
        [HideInInspector] public FishData data;      // runtime data calculated by FishFactory
        [HideInInspector] public bool isPending = false; // true while waiting to be accepted into socket
        [HideInInspector] public FishPool originPool;    // pool that produced/owns this instance

        public void AssignData(FishData d)
        {
            data = d;
        }

        /// <summary>
        /// Return this GameObject to its origin pool (or destroy if none).
        /// Performs cleanup: removes joints, clears selection state, resets physics and preserves grab.throwOnDetach default (enabled).
        /// </summary>
        public void ReturnToPool()
        {
            isPending = false;

            // remove any FixedJoint attached to the lure (safety)
            var joint = GetComponent<FixedJoint>();
            if (joint != null) Destroy(joint);

            // If currently selected by XR, try to cancel selection via the interaction manager safely.
            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                var manager = grab.interactionManager;
                var ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                if (manager != null && ixr != null)
                {
                    try
                    {
                        manager.CancelInteractableSelection(ixr);
                    }
                    catch
                    {
                        // swallow; cancellation is best-effort
                    }
                }
            }

            // Reset rigidbody to safe state (kinematic while pooled)
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // If currently non-kinematic, zero velocities first (allowed).
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;   // pooled = kinematic
                rb.useGravity = false;   // pooled = no gravity
            }

            // Reset grab properties to default - allow throwing by default when handed to player later
            if (grab != null)
            {
                grab.throwOnDetach = true;
            }

            // Return to pool if available, otherwise destroy
            if (originPool != null)
            {
                originPool.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            DebugLogger.VerboseLog("FishInstance", $"ReturnToPool called for {gameObject.name}");
        }

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = false;
        }

        void OnDestroy()
        {
            var joint = GetComponent<FixedJoint>();
            if (joint != null) Destroy(joint);
        }
    }
}