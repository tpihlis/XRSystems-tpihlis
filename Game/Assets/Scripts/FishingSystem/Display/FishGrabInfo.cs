using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FishingSystem
{
    /// <summary>
    /// Lightweight: when this interactable is grabbed by a player interactor (not a socket),
    /// it tells the global FishInfoPanel to show the FishData. On release it clears it.
    /// </summary>
    [RequireComponent(typeof(FishInstance))]
    public class FishGrabInfo : MonoBehaviour
    {
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

        // track last observed selection state so we only react to changes
        bool lastObservedSelectedState = false;

        void Awake()
        {
            // find the XRGrabInteractable on this object or a parent
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (grabInteractable == null)
            {
                Debug.LogWarning($"[FishGrabInfo] No XRGrabInteractable found on {name}. This script needs an XRGrabInteractable to work.");
                return;
            }

            // subscribe to events (primary mechanism)
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);

            // initialize state
            lastObservedSelectedState = grabInteractable.isSelected;
        }

        void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
        }

        // Event handlers (fast-path)
        void OnSelectEntered(SelectEnterEventArgs args)
        {
            // ignore socket selections (they auto-select on placement)
            if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return;

            var data = GetFishDataSafe();
            FishInfoPanel.Instance?.ShowFishData(data);

            // keep lastObservedSelectedState in sync
            lastObservedSelectedState = true;
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            // clear regardless of interactor type
            FishInfoPanel.Instance?.Clear();
            lastObservedSelectedState = false;
        }

        void Update()
        {
            if (grabInteractable == null) return;

            bool current = grabInteractable.isSelected;
            if (current == lastObservedSelectedState) return; // no change

            // selection state changed — determine whether the current selector is a socket
            bool anySocketSelecting = false;
            try
            {
                // interactorsSelecting may be an IEnumerable of different types across XRIT versions.
                var selectors = grabInteractable.interactorsSelecting;
                if (selectors != null)
                {
                    foreach (var sel in selectors)
                    {
                        if (sel == null) continue;

                        // Common case: it's an XRBaseInteractor or subclass
                        if (sel is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor xrBase)
                        {
                            if (xrBase is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor)
                            {
                                anySocketSelecting = true;
                                break;
                            }
                        }
                        else
                        {
                            // Try to treat selector as a UnityEngine.Object (many implementations are)
                            var uo = sel as UnityEngine.Object;
                            if (uo != null)
                            {
                                // If the runtime object has a XRSocketInteractor component, treat it as socket
                                var comp = (uo as Component);
                                if (comp != null && comp.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>() != null)
                                {
                                    anySocketSelecting = true;
                                    break;
                                }

                                // direct cast attempt
                                if (uo is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor)
                                {
                                    anySocketSelecting = true;
                                    break;
                                }
                            }

                            // Last-resort: type-name heuristic (very conservative)
                            var tname = sel.GetType().Name;
                            if (tname.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                anySocketSelecting = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Be conservative on error: assume not a socket
                anySocketSelecting = false;
            }

            if (current && !anySocketSelecting)
            {
                // now selected by a non-socket interactor -> show
                FishInfoPanel.Instance?.ShowFishData(GetFishDataSafe());
            }
            else
            {
                // no longer selected or selected only by a socket -> clear
                FishInfoPanel.Instance?.Clear();
            }

            lastObservedSelectedState = current;
        }

        FishData GetFishDataSafe()
        {
            var fi = GetComponent<FishInstance>();
            if (fi == null) fi = GetComponentInChildren<FishInstance>();
            if (fi == null) return null;
            return fi.data;
        }
    }
}