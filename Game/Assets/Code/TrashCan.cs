using System;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Collider))]
public class TrashCan : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Any object whose root GameObject has one of these tags will be disposed.")]
    public List<string> discardTags = new List<string> { "Fish" };

    [Tooltip("Delay before actually destroying the object (seconds). Short delay helps avoid issues).")]
    public float destroyDelay = 0.2f;

    [Header("Effects")]
    [Tooltip("Sound to play when disposing an object")]
    public AudioClip disposeSound;

    [Tooltip("Particle effect prefab to spawn at the disposed object's position (optional)")]
    public ParticleSystem disposeParticlesPrefab;

    [Header("Debug")]
    public bool debugLogs = false;

    void Reset()
    {
        // ensure the trash can has a trigger collider (we use trigger to detect overlaps)
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // get the topmost GameObject for the thing that entered (handles child colliders)
        var obj = other.GetComponentInParent<Transform>()?.gameObject;
        if (obj == null) return;

        if (!MatchesAnyTag(obj)) return;

        if (debugLogs) Debug.Log($"[TrashCan] Detected tagged object '{obj.name}' -> disposing.");

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

        // 1) If the object is currently grabbed (selected), cancel the selection to avoid ghost-hold
        var grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected)
        {
            var manager = grab.interactionManager;
            if (manager != null)
            {
                // modern API expects IXRSelectInteractable
                var ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                if (ixr != null)
                {
                    try
                    {
                        manager.CancelInteractableSelection(ixr);
                    }
                    catch (Exception ex)
                    {
                        if (debugLogs) Debug.LogWarning("[TrashCan] CancelInteractableSelection failed: " + ex.Message);
                    }
                }
                else
                {
                    if (debugLogs) Debug.LogWarning("[TrashCan] XRGrabInteractable does not implement IXRSelectInteractable (unexpected).");
                }
            }
            else
            {
                if (debugLogs) Debug.LogWarning("[TrashCan] Grab interactionManager was null.");
            }
        }

        // 2) feedback: sound
        if (disposeSound != null)
        {
            AudioSource.PlayClipAtPoint(disposeSound, transform.position);
        }

        // 3) feedback: particle prefab (instantiate at object's position)
        if (disposeParticlesPrefab != null)
        {
            try
            {
                var ps = Instantiate(disposeParticlesPrefab, obj.transform.position, Quaternion.identity);
                ps.Play();
                // destroy particle object after its duration + buffer
                float lifetime = ps.main.duration + 1f;
                Destroy(ps.gameObject, lifetime);
            }
            catch (Exception ex)
            {
                if (debugLogs) Debug.LogWarning("[TrashCan] Failed to spawn disposeParticlesPrefab: " + ex.Message);
            }
        }

        // 4) destroy the object after a short delay
        Destroy(obj, Mathf.Max(0f, destroyDelay));

        if (debugLogs) Debug.Log($"[TrashCan] Scheduled destruction of '{obj.name}' (delay {destroyDelay}s).");
    }
}