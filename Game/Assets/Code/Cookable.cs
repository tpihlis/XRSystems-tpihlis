using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Put on raw fish prefabs and on cooked prefabs (if you want cooked -> burnt)
public class Cookable : MonoBehaviour
{
    [Header("Cook Prefabs")]
    [Tooltip("Prefab to spawn when this is cooked (e.g. 'cooked_fish').")]
    public GameObject cookedPrefab;

    [Tooltip("Prefab to spawn if overcooked (optional).")]
    public GameObject burnedPrefab;

    [Header("Cook tuning")]
    [Tooltip("Time in seconds on a hot pan until this object reaches its 'cooked' threshold.")]
    public float cookTime = 5f;

    [Tooltip("Extra seconds after cookTime before it is considered burned.")]
    public float burnGrace = 3f;

    [Header("Callbacks")]
    public UnityEvent onStartCooking;
    public UnityEvent onCooked;
    public UnityEvent onBurned;

    [Tooltip("Scale multiplier applied to the cooked prefab relative to the source's localScale.")]
    public float cookedScaleMultiplier = 1f;

    // internal
    [HideInInspector] public float accumulatedCook = 0f;
    [HideInInspector] public bool isOnPan = false;
    [HideInInspector] public bool isCooking = false;

    // events fire only once
    bool cookedNotified = false;
    bool burnedNotified = false;

    public void AddCookTime(float delta)
    {
        if (!isOnPan) return;

        if (!isCooking)
        {
            isCooking = true;
            onStartCooking?.Invoke();
        }

        accumulatedCook += delta;

        if (!cookedNotified && accumulatedCook >= cookTime)
        {
            cookedNotified = true;
            onCooked?.Invoke();
        }

        if (!burnedNotified && accumulatedCook >= cookTime + burnGrace)
        {
            burnedNotified = true;
            onBurned?.Invoke();
        }
    }

    public bool IsCooked() => accumulatedCook >= cookTime && accumulatedCook < cookTime + burnGrace;
    public bool IsBurned() => accumulatedCook >= cookTime + burnGrace;

    /// <summary>
    /// Replaces this GameObject with the cooked or burned prefab.
    /// DOES NOT touch tags — tag your prefabs in the Inspector instead.
    /// Initializes the new Cookable (if present) with sensible accumulated values.
    /// </summary>
    public GameObject ConvertToCooked(bool burned = false)
    {
        GameObject prefab = burned ? burnedPrefab : cookedPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[Cookable] No prefab assigned for {(burned ? "burned" : "cooked")} state on {name}.");
            return null;
        }

        Transform t = transform;
        Vector3 spawnPos = t.position + Vector3.up * 0.02f;
        Quaternion spawnRot = t.rotation;

        GameObject newObj = Instantiate(prefab, spawnPos, spawnRot, t.parent);

        // predictable scale: copy source's localScale and apply multiplier
        newObj.transform.localScale = t.localScale * cookedScaleMultiplier;

        // copy/dampen velocity if both have rigidbodies to avoid flinging
        Rigidbody rbOld = GetComponent<Rigidbody>();
        Rigidbody rbNew = newObj.GetComponent<Rigidbody>();
        if (rbOld != null && rbNew != null)
        {
            rbNew.linearVelocity = rbOld.linearVelocity * 0.25f;
            rbNew.angularVelocity = Vector3.zero;
            var cleaner = newObj.AddComponent<TempRigidbodyCleaner>();
            cleaner.ClearNextFrame(rbNew);
        }

        // Initialize new cookable state if the prefab has Cookable
        Cookable newCook = newObj.GetComponent<Cookable>();
        if (newCook != null)
        {
            if (!burned)
            {
                // fresh cooked object should normally start at 0 accumulated (so it requires time to burn)
                newCook.SetAccumulated(0f);
            }
            else
            {
                // burned: mark as fully cooked+burned
                newCook.SetAccumulated(newCook.cookTime + newCook.burnGrace);
            }
            newCook.ResetNotifications();
        }

        Destroy(gameObject);
        return newObj;
    }

    // Helper methods for initialization
    public void SetAccumulated(float value)
    {
        accumulatedCook = value;
        cookedNotified = accumulatedCook >= cookTime;
        burnedNotified = accumulatedCook >= (cookTime + burnGrace);
    }

    public void ResetNotifications()
    {
        cookedNotified = accumulatedCook >= cookTime;
        burnedNotified = accumulatedCook >= (cookTime + burnGrace);
    }

    // small helper to clear velocity on next frame
    private class TempRigidbodyCleaner : MonoBehaviour
    {
        public void ClearNextFrame(Rigidbody rb)
        {
            StartCoroutine(Clear(rb));
        }

        private IEnumerator Clear(Rigidbody rb)
        {
            yield return null;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Destroy(this);
        }
    }
}