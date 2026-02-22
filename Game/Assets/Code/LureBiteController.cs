using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class LureBiteController : MonoBehaviour
{
    [System.Serializable]
    public class FishEntry
    {
        [Tooltip("Fish prefab (must have XRGrabInteractable, Rigidbody, Collider)")]
        public GameObject prefab;

        [Tooltip("Relative spawn weight (higher = more common)")]
        public float weight = 1f;

        [Tooltip("Random scale multiplier range")]
        public Vector2 sizeRange = new Vector2(0.8f, 1.3f);
    }

    // ================= REFERENCES =================
    [Header("References")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor fishSocket;
    public LureController lureController;

    // ================= FISH SETTINGS =================
    [Header("Fish Pool")]
    public List<FishEntry> fishPool = new List<FishEntry>();

    // ================= BITE SETTINGS =================
    [Header("Bite Settings")]
    [Range(0f, 1f)] public float biteChance = 0.25f;
    public Vector2 biteCheckInterval = new Vector2(1f, 5f);

    // ================= SPAWN SAFETY =================
    [Header("Spawn Safety")]
    public float minScaleMultiplier = 0.3f;
    public float socketAcceptTimeout = 0.25f;

    // ================= WATER =================
    [Header("Water")]
    public string waterTag = "Water";

    // ================= DEBUG =================
    [Header("Debug")]
    public bool debugLogs = false;

    // ================= RUNTIME =================
    GameObject currentFish;
    GameObject pendingFish;
    Coroutine biteRoutine;

    int waterOverlapCount = 0;

    bool IsInWater => waterOverlapCount > 0;

    // ================= UNITY =================

    void Awake()
    {
        if (!fishSocket)
            fishSocket = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();

        if (!lureController)
            lureController = GetComponent<LureController>();

        if (fishSocket)
        {
            fishSocket.selectEntered.AddListener(OnSocketSelectEntered);
            fishSocket.selectExited.AddListener(OnSocketSelectExited);
        }
    }

    void OnDestroy()
    {
        if (fishSocket)
        {
            fishSocket.selectEntered.RemoveListener(OnSocketSelectEntered);
            fishSocket.selectExited.RemoveListener(OnSocketSelectExited);
        }
    }

    // ================= WATER TRIGGERS =================

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(waterTag)) return;

        waterOverlapCount++;

        if (waterOverlapCount == 1)
        {
            if (debugLogs) Debug.Log("[LureBite] Entered water.");
            biteRoutine ??= StartCoroutine(BiteLoop());
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(waterTag)) return;

        waterOverlapCount = Mathf.Max(0, waterOverlapCount - 1);

        if (waterOverlapCount == 0)
        {
            if (debugLogs) Debug.Log("[LureBite] Exited water.");
            if (biteRoutine != null)
            {
                StopCoroutine(biteRoutine);
                biteRoutine = null;
            }
        }
    }

    // ================= MAIN LOOP =================

    IEnumerator BiteLoop()
    {
        yield return new WaitForSeconds(0.25f);

        while (IsInWater)
        {
            // Pause while fish exists or is pending
            yield return new WaitUntil(() => currentFish == null && pendingFish == null);

            float wait = Random.Range(biteCheckInterval.x, biteCheckInterval.y);
            yield return new WaitForSeconds(wait);

            if (!IsInWater) yield break;

            if (Random.value > biteChance)
                continue;

            SpawnFishFromPool();
        }

        biteRoutine = null;
    }

    // ================= SPAWNING =================

    void SpawnFishFromPool()
    {
        FishEntry entry = PickWeightedFish();
        if (entry == null || entry.prefab == null) return;

        Vector3 spawnPos = fishSocket.transform.position + Vector3.up * 0.02f;
        Quaternion spawnRot = fishSocket.transform.rotation;

        GameObject fish = Instantiate(entry.prefab, spawnPos, spawnRot);
        pendingFish = fish;

        // Scale
        float scale = Mathf.Max(Random.Range(entry.sizeRange.x, entry.sizeRange.y), minScaleMultiplier);
        fish.transform.localScale *= scale;

        // Rigidbody
        Rigidbody rb = fish.GetComponent<Rigidbody>();
        if (!rb) rb = fish.AddComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Grab interactable
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = fish.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (!grab)
        {
            Destroy(fish);
            pendingFish = null;
            return;
        }

        // Start socket interaction
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
        if (ixr == null)
        {
            CancelPending();
            return;
        }

        fishSocket.StartManualInteraction(ixr);
        StartCoroutine(ConfirmSocketAccepted(socketAcceptTimeout));
    }

    IEnumerator ConfirmSocketAccepted(float timeout)
    {
        float t = 0f;
        while (t < timeout)
        {
            if (pendingFish == null) yield break;
            if (fishSocket.hasSelection) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        CancelPending();
    }

    void CancelPending()
    {
        if (pendingFish)
            Destroy(pendingFish);
        pendingFish = null;
    }

    // ================= SOCKET EVENTS =================

    void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        Component c = args.interactableObject as Component;
        if (!c) return;

        currentFish = c.gameObject;
        pendingFish = null;

        Rigidbody rb = currentFish.GetComponent<Rigidbody>();
        if (rb && lureController)
            lureController.HookOnto(rb);
    }

    void OnSocketSelectExited(SelectExitEventArgs args)
    {
        currentFish = null;
        if (lureController && lureController.hooked)
            lureController.ReleaseHook();
    }

    // ================= UTIL =================

    FishEntry PickWeightedFish()
    {
        float total = 0f;
        foreach (var f in fishPool)
            total += Mathf.Max(0f, f.weight);

        if (total <= 0f) return null;

        float roll = Random.value * total;
        float acc = 0f;

        foreach (var f in fishPool)
        {
            acc += Mathf.Max(0f, f.weight);
            if (roll <= acc) return f;
        }

        return fishPool[Random.Range(0, fishPool.Count)];
    }

    // ================= EDITOR =================

    [ContextMenu("Force Spawn (in water only)")]
    void ForceSpawn()
    {
        if (IsInWater)
            SpawnFishFromPool();
        else
            Debug.LogWarning("[LureBite] Cannot force spawn — not in water.");
    }
}