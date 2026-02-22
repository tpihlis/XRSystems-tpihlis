using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FryingPan : MonoBehaviour
{
    [Header("Heat")]
    public bool isHot = true;
    public float heatRate = 1.0f;

    [Header("Cooking rules")]
    [Tooltip("If object moves faster than this, cooking is paused (flopping).")]
    public float maxVelocityForCooking = 0.2f;

    [Header("Tags (assign in inspector)")]
    [Tooltip("Tag used for raw fish prefabs")]
    public string fishTag = "Fish";

    [Tooltip("Tag used for cooked prefabs")]
    public string cookedTag = "Cooked";

    [Header("Events")]
    public UnityEvent onAnyCooked;
    public UnityEvent onAnyBurned;

    [Header("Debug")]
    public bool debugLogs = false;

    // tracked cookables on this pan
    private readonly Dictionary<Cookable, float> tracking = new Dictionary<Cookable, float>();
    private Collider panCollider;

    void Awake()
    {
        panCollider = GetComponent<Collider>();
        if (panCollider == null) Debug.LogError("[FryingPan] No collider on pan!");
        else if (!panCollider.isTrigger)
        {
            // enforce trigger for surface detection (safe because pan should have separate non-trigger collider for grabbing)
            panCollider.isTrigger = true;
            if (debugLogs) Debug.Log("[FryingPan] Pan collider set to trigger automatically.");
        }
    }

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Cookable cook = other.GetComponentInParent<Cookable>();
        if (cook == null) return;
        if (!MatchesTag(cook.gameObject, fishTag) && !MatchesTag(cook.gameObject, cookedTag)) return;
        AddCookableToTracking(cook);
    }

    void OnTriggerExit(Collider other)
    {
        Cookable cook = other.GetComponentInParent<Cookable>();
        if (cook == null) return;
        RemoveCookableFromTracking(cook);
    }

    void Update()
    {
        if (!isHot) return;

        // fallback: if nothing tracked, try to find overlapping cookables (handles prefabs spawned while overlapping)
        if (tracking.Count == 0)
        {
            TryAddOverlappingCookables();
            if (tracking.Count == 0) return;
        }

        var keys = new List<Cookable>(tracking.Keys);
        foreach (var cook in keys)
        {
            if (cook == null)
            {
                tracking.Remove(cook);
                continue;
            }

            // ensure object is still physically touching the pan (constant checker)
            if (!IsPhysicallyTouching(cook))
            {
                // not physically touching -> stop tracking and do not cook
                RemoveCookableFromTracking(cook);
                if (debugLogs) Debug.Log("[FryingPan] Object no longer touching pan -> stopped tracking: " + cook.name);
                continue;
            }

            // ensure tag still matches one we care about
            var go = cook.gameObject;
            bool isFish = MatchesTag(go, fishTag);
            bool isCooked = MatchesTag(go, cookedTag);

            if (!isFish && !isCooked)
            {
                RemoveCookableFromTracking(cook);
                continue;
            }

            // flopping check
            Rigidbody rb = cook.GetComponent<Rigidbody>();
            bool stationary = true;
            if (rb != null) stationary = rb.linearVelocity.magnitude <= maxVelocityForCooking;
            if (!stationary) continue;

            // add cooking time
            float add = Time.deltaTime * heatRate;
            cook.AddCookTime(add);
            tracking[cook] = cook.accumulatedCook;

            // raw fish -> cooked
            if (isFish && cook.accumulatedCook >= cook.cookTime)
            {
                if (debugLogs) Debug.Log("[FryingPan] Fish reached cookTime -> converting to cooked prefab.");
                GameObject newObj = cook.ConvertToCooked(burned: false);
                tracking.Remove(cook);

                if (newObj != null)
                {
                    Cookable newCook = newObj.GetComponent<Cookable>();
                    if (newCook != null && MatchesTag(newObj, cookedTag) && IsPhysicallyTouching(newCook))
                    {
                        AddCookableToTracking(newCook);
                    }
                    else
                    {
                        // fallback: try to find any Cookable overlapping the pan
                        TryAddOverlappingCookables();
                    }
                }

                onAnyCooked?.Invoke();
                continue;
            }

            // cooked -> burnt (burnt is final by default)
            if (isCooked && cook.accumulatedCook >= cook.cookTime)
            {
                if (debugLogs) Debug.Log("[FryingPan] Cooked object reached cookTime -> converting to burned prefab.");
                GameObject burned = cook.ConvertToCooked(burned: true);
                tracking.Remove(cook);

                // optional: if burned prefab has Cookable and should be tracked, add it (usually not)
                Cookable burnedCook = burned != null ? burned.GetComponent<Cookable>() : null;
                if (burnedCook != null && IsPhysicallyTouching(burnedCook))
                {
                    AddCookableToTracking(burnedCook);
                }

                onAnyBurned?.Invoke();
                continue;
            }
        }
    }

    void AddCookableToTracking(Cookable cook)
    {
        if (cook == null) return;
        if (!tracking.ContainsKey(cook))
        {
            // only add if physically touching right now
            if (!IsPhysicallyTouching(cook))
            {
                if (debugLogs) Debug.Log("[FryingPan] Tried to add Cookable but it's not touching pan: " + cook.name);
                return;
            }

            tracking[cook] = cook.accumulatedCook;
            cook.isOnPan = true;
            cook.isCooking = true;
            cook.onStartCooking?.Invoke();
            if (debugLogs) Debug.Log("[FryingPan] Tracking Cookable: " + cook.name);
        }
    }

    void RemoveCookableFromTracking(Cookable cook)
    {
        if (cook == null) return;
        if (tracking.ContainsKey(cook))
        {
            tracking.Remove(cook);
            cook.isOnPan = false;
            cook.isCooking = false;
            if (debugLogs) Debug.Log("[FryingPan] Removed Cookable: " + cook.name);
        }
    }

    // Finds Cookable components overlapping the pan bounds and starts tracking them.
    // This uses the physical bounds check before adding.
    void TryAddOverlappingCookables()
    {
        if (panCollider == null) panCollider = GetComponent<Collider>();
        if (panCollider == null) return;

        Bounds b = panCollider.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents, panCollider.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        foreach (var col in hits)
        {
            if (col == null) continue;
            Cookable cook = col.GetComponentInParent<Cookable>();
            if (cook == null) continue;
            if (!MatchesTag(cook.gameObject, fishTag) && !MatchesTag(cook.gameObject, cookedTag)) continue;
            // Only add if physically touching (double-check)
            if (IsPhysicallyTouching(cook)) AddCookableToTracking(cook);
        }
    }

    bool MatchesTag(GameObject obj, string tagToMatch)
    {
        if (string.IsNullOrEmpty(tagToMatch)) return false;
        return obj.CompareTag(tagToMatch);
    }

    // **Constant physical contact check**:
    // Returns true if any collider on the cookable's GameObject (or children) intersects the pan collider bounds.
    bool IsPhysicallyTouching(Cookable cook)
    {
        if (cook == null || panCollider == null) return false;

        // Get all colliders on the cookable's root/object and children
        Collider[] colliders = cook.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0) return false;

        Bounds panBounds = panCollider.bounds;
        foreach (var c in colliders)
        {
            if (c == null) continue;
            // Quick bounds intersection test
            if (panBounds.Intersects(c.bounds))
            {
                return true;
            }
        }

        return false;
    }
}