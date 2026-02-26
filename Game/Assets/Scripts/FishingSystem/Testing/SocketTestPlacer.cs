using UnityEngine;


namespace FishingSystem
{
    /// <summary>
    /// Debug helper: place a fish into an XRSocketInteractor for testing.
    /// Accepts either:
    ///  - an existing scene instance (fishInstance),
    ///  - a prefab to instantiate (fishPrefab),
    ///  - or a FishSpeciesSO to instantiate its species.prefab (or via FishFactory).
    ///
    /// IMPORTANT: do NOT assign a prefab asset to the 'fishInstance' slot.
    /// </summary>
    public class SocketTestPlacer : MonoBehaviour
    {
        [Header("Target socket")]
        public UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;

        [Header("Fish source (choose one)")]
        [Tooltip("An existing fish GameObject already present in the scene (instance).")]
        public GameObject fishInstance; // scene object to place (preferred for testing)

        [Tooltip("A fish prefab to instantiate at runtime and place into the socket.")]
        public GameObject fishPrefab;   // prefab asset to instantiate when placing

        [Tooltip("Alternative: species SO whose prefab will be instantiated when placed.")]
        public FishSpeciesSO speciesSO;

        [Header("Factory (optional)")]
        [Tooltip("If true and FishFactory assigned, use factory.SpawnPendingFish(...) to create fish (recommended if you want proper FishData).")]
        public bool useFactoryToSpawn = false;
        public FishFactory factory;     // optional: will call SpawnPendingFish if useFactoryToSpawn true
        public LureSO factoryLure;      // optional lure to pass to factory
        public PlayerStats factoryPlayer; // optional player stats to pass to factory

        [Tooltip("Disable Rigidbody kinematic on placed instance (false -> leave physics enabled).")]
        public bool makeKinematicWhenPlaced = true;

        /// <summary>
        /// Context menu utility to place into socket from inspector during Play mode.
        /// </summary>
        [ContextMenu("Place Fish Into Socket (smart)")]
        public void PlaceIntoSocket()
        {
            if (socket == null)
            {
                Debug.LogWarning("[SocketTestPlacer] No socket assigned.");
                return;
            }

            GameObject instanceToPlace = null;
            bool createdByFactory = false;

            // 1) If a scene instance is provided, use it (must be a scene object, not prefab asset)
            if (fishInstance != null)
            {
                // If the provided object is an asset (prefab) rather than a scene instance, its scene will be invalid.
                // In Play mode check scene validity; in edit mode, a prefab asset may still have scene.IsValid false.
                bool isSceneObj = fishInstance.scene.IsValid();
                if (!isSceneObj)
                {
                    Debug.LogWarning("[SocketTestPlacer] The 'fishInstance' you assigned is a prefab asset, not a scene instance. Assign a scene instance or use fishPrefab/speciesSO instead.");
                }
                else
                {
                    instanceToPlace = fishInstance;
                }
            }

            // 2) If no scene instance, try instantiating a direct prefab (fishPrefab)
            if (instanceToPlace == null && fishPrefab != null)
            {
                instanceToPlace = Instantiate(fishPrefab);
            }

            // 3) If still null, try speciesSO.prefab or factory
            if (instanceToPlace == null && speciesSO != null)
            {
                if (useFactoryToSpawn && factory != null)
                {
                    // SpawnPendingFish returns a GameObject (pending fish). We pass the socket's attach transform so it spawns there.
                    Transform attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                    instanceToPlace = factory.SpawnPendingFish(speciesSO, factoryLure, factoryPlayer, attach);
                    createdByFactory = true;
                }
                else
                {
                    if (speciesSO.prefab == null)
                    {
                        Debug.LogWarning("[SocketTestPlacer] speciesSO.prefab is null.");
                        return;
                    }
                    instanceToPlace = Instantiate(speciesSO.prefab);
                }
            }

            if (instanceToPlace == null)
            {
                Debug.LogWarning("[SocketTestPlacer] No fish source available. Assign fishInstance (scene), fishPrefab, or speciesSO.");
                return;
            }

            // Ensure instance is active
            instanceToPlace.SetActive(true);

            // Position & parent under socket (visual only). If created by factory, it may already be properly setup/parented.
            if (!createdByFactory)
            {
                var attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                instanceToPlace.transform.SetParent(socket.transform, worldPositionStays: false);
                instanceToPlace.transform.position = attach.position;
                instanceToPlace.transform.rotation = attach.rotation;
                // preserve the instance scale as-is (do not forcibly set localScale unless you want to).
            }

            // Manage Rigidbody: make kinematic to avoid immediate physics push-out
            var rb = instanceToPlace.GetComponent<Rigidbody>();
            if (rb != null && makeKinematicWhenPlaced)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Debug.Log($"[SocketTestPlacer] Placed '{instanceToPlace.name}' into socket '{socket.name}' (visual). Use SellManager.verboseDebug to check selection behavior.");
        }
    }
}