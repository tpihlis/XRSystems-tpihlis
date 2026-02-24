// File: Assets/Scripts/FishingSystem/Interaction/SocketInteractionHandler.cs
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FishingSystem
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor))]
    public class SocketInteractionHandler : MonoBehaviour
    {
        public UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;
        public LureController lureController;

        GameObject pending;
        Coroutine acceptTimeout;

        void Awake()
        {
            if (socket == null)
                socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();

            if (socket != null)
            {
                socket.selectEntered.AddListener(OnSelectEntered);
                socket.selectExited.AddListener(OnSelectExited);
            }

            DebugLogger.VerboseLog("SocketHandler", "Awake completed");
        }

        void OnDestroy()
        {
            if (socket != null)
            {
                socket.selectEntered.RemoveListener(OnSelectEntered);
                socket.selectExited.RemoveListener(OnSelectExited);
            }
        }

        /// <summary>
        /// Register a pending fish and start accept timeout.
        /// Pending fish will be made kinematic and not throwable while waiting for acceptance.
        /// </summary>
        public void SetPending(GameObject fish, float timeout = 0.25f)
        {
            if (fish == null) return;
            pending = fish;

            // Make pending fish kinematic & not affected by gravity while waiting
            var rb = pending.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Prevent throwing while pending
            var grab = pending.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
                grab.throwOnDetach = false;

            DebugLogger.Log("SocketHandler", $"SetPending called for {fish.name} timeout={timeout}");
            if (acceptTimeout != null) StopCoroutine(acceptTimeout);
            acceptTimeout = StartCoroutine(AcceptTimeout(timeout));
        }

        IEnumerator AcceptTimeout(float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (pending == null) yield break;
                if (socket != null && socket.hasSelection) yield break;
                t += Time.deltaTime;
                yield return null;
            }

            if (pending != null)
            {
                var fi = pending.GetComponent<FishInstance>();
                if (fi != null) fi.ReturnToPool();
                DebugLogger.Log("SocketHandler", "AcceptTimeout expired, pending fish returned to pool");
                pending = null;
            }
        }

        /// <summary>
        /// Returns true if the socket either currently has a selection or a pending fish is awaiting acceptance.
        /// </summary>
        public bool HasPendingOrSelection()
        {
            bool sel = (socket != null) ? socket.hasSelection : false;
            bool pend = pending != null;
            return sel || pend;
        }

        /// <summary>
        /// Force-accept the pending fish (used by TestRunner). First tries StartManualInteraction, falls back to direct accept.
        /// </summary>
        public void AcceptPendingManually()
        {
            if (pending == null)
            {
                DebugLogger.Log("SocketHandler", "AcceptPendingManually called but no pending fish");
                return;
            }

            // Try StartManualInteraction first (preferred)
            try
            {
                var grab = pending.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null && socket != null)
                {
                    var ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                    if (ixr != null)
                    {
                        socket.StartManualInteraction(ixr);
                        DebugLogger.Log("SocketHandler", $"AcceptPendingManually called StartManualInteraction for {pending.name}");
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log("SocketHandler", $"AcceptPendingManually StartManualInteraction failed: {ex.Message}");
            }

            // Fallback: accept pending directly (bypass XR event)
            DebugLogger.Log("SocketHandler", $"AcceptPendingManually accepting {pending.name}");
            AcceptPendingDirect();
        }

        void AcceptPendingDirect()
        {
            if (pending == null) return;

            if (acceptTimeout != null) { StopCoroutine(acceptTimeout); acceptTimeout = null; }

            // Make sure it's kinematic and not throwable (should already be from SetPending)
            var rb = pending.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var grab = pending.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null) grab.throwOnDetach = false;

            if (lureController != null)
            {
                try { lureController.NotifyHooked(pending); } catch { }
            }

            DebugLogger.Log("SocketHandler", $"AcceptPendingDirect accepted {pending.name}");
            pending = null;
        }

        // Called by XR when something is selected into the socket (or manual StartManualInteraction succeeded).
        void OnSelectEntered(SelectEnterEventArgs args)
        {
            Component c = args.interactableObject as Component;
            if (!c) return;

            GameObject selected = c.gameObject;

            // If we had a pending fish, only accept that specific pending
            if (pending != null)
            {
                if (selected != pending) return;

                if (acceptTimeout != null) { StopCoroutine(acceptTimeout); acceptTimeout = null; }

                DebugLogger.Log("SocketHandler", $"OnSelectEntered accepted pending {pending.name}");

                // Keep it kinematic while in the socket (stable)
                var rb = pending.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    // DO NOT set velocities on kinematic bodies (causes errors)
                }

                var grab = pending.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null) grab.throwOnDetach = false;

                if (lureController != null)
                {
                    try { lureController.NotifyHooked(pending); } catch { }
                }

                pending = null;
                return;
            }

            // Fallback: a normal selection happened (not our pending). If it's a fish, ensure it's kinematic in socket.
            var fi = selected.GetComponent<FishInstance>();
            if (fi != null)
            {
                DebugLogger.VerboseLog("SocketHandler", $"OnSelectEntered with non-pending fish {selected.name}");
                var rb2 = selected.GetComponent<Rigidbody>();
                if (rb2 != null)
                {
                    rb2.isKinematic = true;
                    rb2.useGravity = false;
                }

                if (lureController != null)
                {
                    try { lureController.NotifyHooked(selected); } catch { }
                }
            }
        }

        // Called by XR when a selection exits the socket (fish removed)
        void OnSelectExited(SelectExitEventArgs args)
        {
            Component comp = args.interactableObject as Component;
            if (!comp) return;
            GameObject go = comp.gameObject;

            var fi = go.GetComponent<FishInstance>();
            if (fi != null)
            {
                // Make the fish non-kinematic so XRInteractionToolkit can set velocities (throws)
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    // Do NOT set velocities here — XRInteractionToolkit will apply the throw velocities itself.
                }

                // Allow throw on detach now that it's non-kinematic
                var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null) grab.throwOnDetach = true;

                // Notify lure controller (release hook)
                if (lureController != null)
                {
                    try { lureController.NotifyReleased(go); } catch { }
                }

                DebugLogger.VerboseLog("SocketHandler", $"OnSelectExited for {go.name}: set non-kinematic and enabled throw");
            }
        }
    }
}