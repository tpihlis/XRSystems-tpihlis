using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Spawns starting gear (rod, hook, lure) from RodSO/LureSO physicalPrefab fields.
    /// Wires:
    ///  - FishingLine.lureRb -> hook Rigidbody
    ///  - SpawnManager.fishSocketTransform -> the socket transform found on the spawned lure
    ///  - SpawnManager.socketHandler -> SocketInteractionHandler found on the socket (if present)
    ///  - LureBiteController.spawnManager -> the SpawnManager in scene (if found)
    /// Also equips PlayerStats with the SOs.
    /// </summary>
    public class GearSpawner : MonoBehaviour
    {
        [Header("Source SOs")]
        public RodSO startingRod;
        public LureSO startingLure;

        [Header("Hook prefab (no SO)")]
        [Tooltip("Hook physical prefab (hook object with Rigidbody+Collider).")]
        public GameObject hookPrefab;

        [Header("Spawn placement")]
        [Tooltip("Where to spawn spawned gear. If null, spawn at this GameObject position.")]
        public Transform spawnParent;

        [Header("Auto-run")]
        public bool spawnOnStart = true;

        void Start()
        {
            if (spawnOnStart) SpawnStartingGear();
        }

        public void SpawnStartingGear()
        {
            // find scene systems (robust to Unity version)
#if UNITY_2023_2_OR_NEWER
            var spawnManager = UnityEngine.Object.FindAnyObjectByType<SpawnManager>();
            var playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
#else
            var spawnManager = FindObjectOfType<SpawnManager>();
            var playerStats = FindObjectOfType<PlayerStats>();
#endif

            Vector3 basePos = spawnParent != null ? spawnParent.position : transform.position;
            Quaternion baseRot = spawnParent != null ? spawnParent.rotation : transform.rotation;

            GameObject rodGO = null;
            GameObject hookGO = null;
            GameObject lureGO = null;

            // --- spawn rod ---
            if (startingRod != null && startingRod.physicalPrefab != null)
            {
                rodGO = Instantiate(startingRod.physicalPrefab, basePos, baseRot, spawnParent);
                DebugLogger.Log("GearSpawner", $"Spawned rod '{startingRod.displayName}' at {rodGO.transform.position}");
            }
            else
            {
                DebugLogger.Log("GearSpawner", "No startingRod or startingRod.physicalPrefab assigned.");
            }

            // --- spawn hook ---
            if (hookPrefab != null)
            {
                // try to place hook near rod tip if possible
                Vector3 hookPos = basePos;
                if (rodGO != null)
                {
                    // attempt to find a child named "HookSocket" or "Hook" on rod to position it
                    var socket = rodGO.transform.Find("HookSocket") ?? rodGO.transform.Find("Hook");
                    if (socket != null) hookPos = socket.position;
                    else hookPos = rodGO.transform.position + rodGO.transform.forward * 0.3f + Vector3.down * 0.1f;
                }

                hookGO = Instantiate(hookPrefab, hookPos, baseRot, spawnParent);
                DebugLogger.Log("GearSpawner", $"Spawned hook '{hookGO.name}' at {hookGO.transform.position}");
            }
            else
            {
                DebugLogger.VerboseLog("GearSpawner", "No hookPrefab assigned (hook not spawned).");
            }

            // --- spawn lure ---
            if (startingLure != null && startingLure.physicalPrefab != null)
            {
                // place lure near hook or rod tip
                Vector3 lurePos = basePos;
                if (hookGO != null) lurePos = hookGO.transform.position + Vector3.down * 0.05f;
                else if (rodGO != null) lurePos = rodGO.transform.position + rodGO.transform.forward * 0.35f;

                lureGO = Instantiate(startingLure.physicalPrefab, lurePos, baseRot, spawnParent);
                DebugLogger.Log("GearSpawner", $"Spawned lure '{startingLure.displayName}' at {lureGO.transform.position}");
            }
            else
            {
                DebugLogger.Log("GearSpawner", "No startingLure or startingLure.physicalPrefab assigned.");
            }

            // --- wire fishing line: find FishingLine component (prefer on rodGO) ---
            FishingLine line = null;
            if (rodGO != null)
            {
                line = rodGO.GetComponentInChildren<FishingLine>();
                if (line == null)
                {
                    DebugLogger.VerboseLog("GearSpawner", "No FishingLine found on rod prefab; searching scene for a FishingLine");
                }
            }

            if (line == null)
            {
#if UNITY_2023_2_OR_NEWER
                line = UnityEngine.Object.FindAnyObjectByType<FishingLine>();
#else
                line = FindObjectOfType<FishingLine>();
#endif
            }

            if (line != null && hookGO != null)
            {
                var hookRb = hookGO.GetComponent<Rigidbody>();
                if (hookRb != null)
                {
                    line.lureRb = hookRb;
                    DebugLogger.Log("GearSpawner", $"Assigned hook Rigidbody '{hookGO.name}' to FishingLine.");
                }
                else
                {
                    DebugLogger.Log("GearSpawner", $"Hook spawned but missing Rigidbody: {hookGO.name}");
                }
            }
            else if (line == null)
            {
                DebugLogger.Log("GearSpawner", "No FishingLine found in scene to wire to the hook.");
            }

            // --- Wire SpawnManager socket settings using the spawned lure ---
            if (spawnManager != null && lureGO != null)
            {
                // try to find XRSocketInteractor on the lure (LureBiteController often has it as child)
                var socketInteractor = lureGO.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
                if (socketInteractor != null)
                {
                    spawnManager.fishSocketTransform = socketInteractor.transform;

                    // try get the SocketInteractionHandler on the same GameObject (or child)
                    var handler = socketInteractor.GetComponent<SocketInteractionHandler>();
                    if (handler == null)
                        handler = socketInteractor.GetComponentInChildren<SocketInteractionHandler>();

                    if (handler != null)
                    {
                        spawnManager.socketHandler = handler;
                        DebugLogger.Log("GearSpawner", $"Assigned SpawnManager.fishSocketTransform -> {socketInteractor.transform.name} and socketHandler -> {handler.gameObject.name}");
                    }
                    else
                    {
                        DebugLogger.Log("GearSpawner", $"SpawnManager assigned fishSocketTransform={socketInteractor.transform.name} but SocketInteractionHandler not found on socket.");
                    }
                }
                else
                {
                    DebugLogger.Log("GearSpawner", "SpawnManager wiring: XRSocketInteractor not found on spawned lure.");
                }
            }

            // --- assign SpawnManager reference to LureBiteController on spawned lure ---
            if (lureGO != null)
            {
                var bite = lureGO.GetComponentInChildren<LureBiteController>();
                if (bite != null)
                {
                    if (spawnManager != null)
                    {
                        bite.spawnManager = spawnManager;
                        DebugLogger.Log("GearSpawner", $"Assigned SpawnManager to LureBiteController on {lureGO.name}");
                    }
                    else
                    {
#if UNITY_2023_2_OR_NEWER
                        var sm = UnityEngine.Object.FindAnyObjectByType<SpawnManager>();
#else
                        var sm = FindObjectOfType<SpawnManager>();
#endif
                        if (sm != null)
                        {
                            bite.spawnManager = sm;
                            DebugLogger.Log("GearSpawner", $"Found and assigned SpawnManager to LureBiteController on {lureGO.name}");
                        }
                        else DebugLogger.Log("GearSpawner", "No SpawnManager found to assign to LureBiteController.");
                    }
                }
                else DebugLogger.VerboseLog("GearSpawner", "No LureBiteController found on spawned lure prefab.");
            }

            // --- equip player stats for consistency ---
            if (playerStats != null)
            {
                if (startingRod != null) playerStats.EquipRod(startingRod);
                if (startingLure != null) playerStats.EquipLure(startingLure);
            }

            DebugLogger.Log("GearSpawner", "SpawnStartingGear finished.");
        }
    }
}