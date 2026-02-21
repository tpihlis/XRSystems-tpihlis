using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Robust LureBiteController: handles pending spawns, socket acceptance timeouts,
/// and clears stale state so bites keep functioning even if XR socket doesn't respond.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LureBiteController : MonoBehaviour
{
    [System.Serializable]
    public class FishEntry
    {
        public GameObject prefab;
        public float weight = 1f;
        public Vector2 sizeRange = new Vector2(0.8f, 1.3f);
    }

    [Header("References")]
    public Transform fishSocket;                 // usually lure/socket
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;  // socket component on the socket object
    public LureController lureController;        // your existing lure script

    [Header("Fish Pool")]
    public List<FishEntry> fishPool = new List<FishEntry>();

    [Header("Bite Settings")]
    [Range(0f, 1f)] public float biteChance = 0.25f;
    public Vector2 biteCheckInterval = new Vector2(2f, 6f);
    public string waterTag = "Water";
    [Tooltip("Seconds to wait for the socket to accept a spawned fish before giving up.")]
    public float pendingAcceptTimeout = 3.0f;

    [Header("Debug")]
    public bool debugLogs = true;

    // runtime state
    private GameObject currentFish = null;   // a fish that the socket accepted and is hooked (or sitting in socket)
    private GameObject pendingFish = null;   // a freshly spawned fish waiting for socket to accept
    private Coroutine pendingTimeoutRoutine = null;

    private bool inWater = false;
    private Coroutine biteRoutine = null;

    void Awake()
    {
        // Auto-assign helpful references if user forgot
        if (lureController == null)
        {
            lureController = GetComponent<LureController>();
            if (debugLogs) Debug.Log("[LureBite] Auto-assigned LureController: " + (lureController != null));
        }

        if (fishSocket == null)
        {
            Transform found = transform.Find("socket");
            if (found != null) fishSocket = found;
            if (debugLogs) Debug.Log("[LureBite] fishSocket assigned? " + (fishSocket != null));
        }

        if (socketInteractor == null && fishSocket != null)
        {
            socketInteractor = fishSocket.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            if (debugLogs) Debug.Log("[LureBite] socketInteractor auto-assigned? " + (socketInteractor != null));
        }

        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnSocketSelectEntered);
            socketInteractor.selectExited.AddListener(OnSocketSelectExited);
            if (debugLogs) Debug.Log("[LureBite] Subscribed to socket events.");
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[LureBite] socketInteractor is null - socket events will not fire.");
        }
    }

    void OnDestroy()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnSocketSelectEntered);
            socketInteractor.selectExited.RemoveListener(OnSocketSelectExited);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(waterTag)) return;
        if (debugLogs) Debug.Log("[LureBite] Entered water.");
        inWater = true;
        if (biteRoutine == null) biteRoutine = StartCoroutine(BiteLoop());
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(waterTag)) return;
        if (debugLogs) Debug.Log("[LureBite] Exited water.");
        inWater = false;
        if (biteRoutine != null)
        {
            StopCoroutine(biteRoutine);
            biteRoutine = null;
        }
    }

    IEnumerator BiteLoop()
    {
        if (debugLogs) Debug.Log("[LureBite] BiteLoop started.");
        while (inWater)
        {
            // random wait between checks
            float wait = Random.Range(biteCheckInterval.x, biteCheckInterval.y);
            if (debugLogs) Debug.Log($"[LureBite] Waiting {wait:F2}s for next check.");
            yield return new WaitForSeconds(wait);

            // Clean stale currentFish if destroyed or inactive
            if (currentFish == null)
            {
                // nothing to do, but ensure hook state is consistent
                if (lureController != null && lureController.hooked)
                {
                    if (debugLogs) Debug.Log("[LureBite] Hooked reported but no currentFish -> releasing hook.");
                    lureController.ReleaseHook();
                }
            }
            else
            {
                // if the object exists but got deactivated in scene, treat as gone
                if (!currentFish.activeInHierarchy)
                {
                    if (debugLogs) Debug.Log("[LureBite] currentFish not active -> clearing.");
                    currentFish = null;
                    if (lureController != null && lureController.hooked) lureController.ReleaseHook();
                }
            }

            // If there is already a fish accepted or pending, skip spawning
            if (currentFish != null)
            {
                if (debugLogs) Debug.Log("[LureBite] Skipping check: currentFish present.");
                continue;
            }
            if (pendingFish != null)
            {
                if (debugLogs) Debug.Log("[LureBite] Skipping check: pendingFish awaiting socket.");
                continue;
            }
            if (lureController != null && lureController.hooked)
            {
                if (debugLogs) Debug.Log("[LureBite] Skipping check: lureController reports hooked.");
                continue;
            }

            // bite roll
            float roll = Random.value;
            if (debugLogs) Debug.Log($"[LureBite] Bite roll = {roll:F3} (need <= {biteChance:F3})");
            if (roll <= biteChance)
            {
                if (debugLogs) Debug.Log("[LureBite] Bite succeeded -> spawning fish.");
                SpawnFishIntoSocket();
            }
            else
            {
                if (debugLogs) Debug.Log("[LureBite] Bite failed.");
            }
        }
        if (debugLogs) Debug.Log("[LureBite] BiteLoop ended.");
    }

    // Public test helper so you can force spawn from Inspector
    [ContextMenu("Force Spawn Fish")]
    public void ForceSpawnContext() => SpawnFishIntoSocket();

    public void SpawnFishIntoSocket()
    {
        // Validate
        if (fishPool == null || fishPool.Count == 0)
        {
            Debug.LogWarning("[LureBite] fishPool empty - cannot spawn.");
            return;
        }
        if (socketInteractor == null)
        {
            Debug.LogWarning("[LureBite] socketInteractor null - cannot auto-insert fish.");
            return;
        }
        if (pendingFish != null)
        {
            Debug.LogWarning("[LureBite] A pending fish already exists. Wait for it to be accepted or timeout.");
            return;
        }

        // choose fish
        FishEntry chosen = PickWeightedFish();
        if (chosen == null || chosen.prefab == null)
        {
            Debug.LogWarning("[LureBite] No valid fish chosen.");
            return;
        }

        // instantiate
        GameObject fish = Instantiate(chosen.prefab);
        if (debugLogs) Debug.Log("[LureBite] Instantiated " + chosen.prefab.name);

        // size randomization
        float scale = Random.Range(chosen.sizeRange.x, chosen.sizeRange.y);
        fish.transform.localScale = Vector3.Scale(fish.transform.localScale, Vector3.one * scale);
        if (debugLogs) Debug.Log($"[LureBite] Scaled fish by {scale:F2}");

        // ensure Rigidbody
        Rigidbody rb = fish.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = fish.AddComponent<Rigidbody>();
            if (debugLogs) Debug.Log("[LureBite] Added Rigidbody to fish (prefab missing one).");
        }
        rb.isKinematic = false;
        rb.mass = Mathf.Clamp(scale, 0.1f, 10f);

        // ensure XRGrabInteractable
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = fish.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
        {
            Debug.LogWarning("[LureBite] Spawned fish missing XRGrabInteractable. Add it to prefab.");
            Destroy(fish);
            return;
        }

        // set pending and start timeout
        pendingFish = fish;
        if (pendingTimeoutRoutine != null) StopCoroutine(pendingTimeoutRoutine);
        pendingTimeoutRoutine = StartCoroutine(PendingAcceptTimeout(pendingAcceptTimeout));

        // Try to start manual interaction (modern API requires IXRSelectInteractable)
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable selectInteract = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
        if (selectInteract == null)
        {
            Debug.LogWarning("[LureBite] XRGrabInteractable does not implement IXRSelectInteractable - cannot StartManualInteraction.");
            // Clean up
            CancelPendingSpawn();
            return;
        }

        // Attempt to have the socket accept the fish.
        try
        {
            socketInteractor.StartManualInteraction(selectInteract);
            if (debugLogs) Debug.Log("[LureBite] Called StartManualInteraction on socket.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[LureBite] StartManualInteraction threw: " + ex.Message);
            CancelPendingSpawn();
        }
    }

    // Waits for socket to accept. If nothing happens, clean up pending fish.
    IEnumerator PendingAcceptTimeout(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (pendingFish == null) yield break; // accepted or destroyed elsewhere
            t += Time.deltaTime;
            yield return null;
        }

        if (debugLogs) Debug.LogWarning("[LureBite] Pending fish was not accepted by socket in time. Destroying pending fish.");
        CancelPendingSpawn();
    }

    // Cancel pending spawn and destroy fish
    private void CancelPendingSpawn()
    {
        if (pendingTimeoutRoutine != null)
        {
            StopCoroutine(pendingTimeoutRoutine);
            pendingTimeoutRoutine = null;
        }
        if (pendingFish != null)
        {
            try { Destroy(pendingFish); } catch { }
            pendingFish = null;
        }
    }

    // Weighted pick
    FishEntry PickWeightedFish()
    {
        float total = 0f;
        foreach (var f in fishPool) total += Mathf.Max(0f, f.weight);
        if (total <= 0f) return null;
        float r = Random.value * total;
        float acc = 0f;
        foreach (var f in fishPool)
        {
            acc += Mathf.Max(0f, f.weight);
            if (r <= acc) return f;
        }
        return fishPool[Random.Range(0, fishPool.Count)];
    }

    // Called when socket accepts an item
    void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        if (debugLogs) Debug.Log("[LureBite] OnSocketSelectEntered fired.");

        // Extract component/GameObject from the IXRSelectInteractable
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable ixr = args.interactableObject;
        if (ixr == null)
        {
            if (debugLogs) Debug.LogWarning("[LureBite] selectEntered had null interactableObject.");
            return;
        }

        Component comp = ixr as Component;
        if (comp == null)
        {
            if (debugLogs) Debug.LogWarning("[LureBite] selectEntered interactable is not a Component.");
            return;
        }

        GameObject go = comp.gameObject;

        // If we have a pending fish and this is it, accept and clear pending
        if (pendingFish != null && go == pendingFish)
        {
            if (debugLogs) Debug.Log("[LureBite] Socket accepted pending fish.");
            currentFish = pendingFish;
            pendingFish = null;
            if (pendingTimeoutRoutine != null) { StopCoroutine(pendingTimeoutRoutine); pendingTimeoutRoutine = null; }
        }
        else
        {
            // If no pending fish matches, it's possible the user manually placed some other interactable into socket.
            currentFish = go;
            if (debugLogs) Debug.Log("[LureBite] Socket accepted an interactable that wasn't pending. Tracking it as currentFish.");
        }

        // Attach to lure via LureController
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && lureController != null)
        {
            lureController.HookOnto(rb);
            if (debugLogs) Debug.Log("[LureBite] Called lureController.HookOnto on " + go.name);
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[LureBite] Could not HookOnto: missing Rigidbody or LureController.");
        }
    }

    // Called when socket loses selection (fish removed)
    void OnSocketSelectExited(SelectExitEventArgs args)
    {
        if (debugLogs) Debug.Log("[LureBite] OnSocketSelectExited fired.");

        // clear current fish & release hook
        currentFish = null;
        if (lureController != null && lureController.hooked)
        {
            lureController.ReleaseHook();
            if (debugLogs) Debug.Log("[LureBite] Called ReleaseHook due to socket exit.");
        }
    }
}