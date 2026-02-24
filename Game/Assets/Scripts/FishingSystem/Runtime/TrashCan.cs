// File: TrashCan.cs
using System;
using System.Collections.Generic;
using UnityEngine;


namespace FishingSystem
{
    [RequireComponent(typeof(Collider))]
    public class TrashCan : MonoBehaviour
    {
        [Header("Settings")]
        public List<string> discardTags = new List<string> { "Fish" };
        public float destroyDelay = 0.2f;

        [Header("Effects")]
        public AudioClip disposeSound;
        public ParticleSystem disposeParticlesPrefab;

        [Header("Debug")]
        public bool debugLogs = false;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var obj = other.GetComponentInParent<Transform>()?.gameObject;
            if (obj == null) return;
            if (!MatchesAnyTag(obj)) return;

            if (debugLogs) DebugLogger.Log("TrashCan", $"Detected tagged object '{obj.name}' -> disposing.");
            DisposeObject(obj);
        }

        bool MatchesAnyTag(GameObject go)
        {
            if (go == null || discardTags == null || discardTags.Count == 0) return false;
            foreach (var tag in discardTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (go.CompareTag(tag)) return true;
            }
            return false;
        }

        void DisposeObject(GameObject obj)
        {
            if (obj == null) return;

            var grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                var manager = grab.interactionManager;
                if (manager != null)
                {
                    var ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                    if (ixr != null)
                    {
                        try
                        {
                            manager.CancelInteractableSelection(ixr);
                        }
                        catch (Exception ex)
                        {
                            if (debugLogs) DebugLogger.Log("TrashCan", "CancelInteractableSelection failed: " + ex.Message);
                        }
                    }
                    else
                    {
                        if (debugLogs) DebugLogger.Log("TrashCan", "XRGrabInteractable does not implement IXRSelectInteractable (unexpected).");
                    }
                }
                else
                {
                    if (debugLogs) DebugLogger.Log("TrashCan", "Grab interactionManager was null.");
                }
            }

            if (disposeSound != null)
            {
                AudioSource.PlayClipAtPoint(disposeSound, transform.position);
            }

            if (disposeParticlesPrefab != null)
            {
                try
                {
                    var ps = Instantiate(disposeParticlesPrefab, obj.transform.position, Quaternion.identity);
                    ps.Play();
                    float lifetime = ps.main.duration + 1f;
                    Destroy(ps.gameObject, lifetime);
                }
                catch (Exception ex)
                {
                    if (debugLogs) DebugLogger.Log("TrashCan", "Failed to spawn disposeParticlesPrefab: " + ex.Message);
                }
            }

            Destroy(obj, Mathf.Max(0f, destroyDelay));
            if (debugLogs) DebugLogger.Log("TrashCan", $"Scheduled destruction of '{obj.name}' (delay {destroyDelay}s).");
        }
    }
}