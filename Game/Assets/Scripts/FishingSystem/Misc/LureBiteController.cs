using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace FishingSystem
{
    [RequireComponent(typeof(Collider))]
    public class LureBiteController : MonoBehaviour
    {
        [Header("References")]
        public UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor fishSocket;
        public LureController lureController;

        [Header("Migration")]
        [Tooltip("If true use original Instantiate-based spawning (legacy). Otherwise call SpawnManager.")]
        public bool useLegacySpawn = false;

        [Tooltip("Spawn manager to use when not using legacy path.")]
        public SpawnManager spawnManager;

        // Legacy fields (kept for compatibility/testing)
        [Header("Legacy Fish Pool (kept for migration)")]
        public List<GameObject> legacyFishPrefabs = new List<GameObject>(); // simple list of prefabs
        [Range(0f, 1f)] public float biteChance = 0.25f;
        public Vector2 biteCheckInterval = new Vector2(1f, 5f);
        public float minScaleMultiplier = 0.3f;
        public float socketAcceptTimeout = 0.25f;
        public string waterTag = "Water";
        public bool debugLogs = false;

        // runtime
        GameObject currentFish;
        GameObject pendingFish;
        Coroutine biteRoutine;
        int waterOverlapCount = 0;
        bool IsInWater => waterOverlapCount > 0;

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

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(waterTag)) return;

            waterOverlapCount++;

            if (waterOverlapCount == 1)
            {
                DebugLogger.Log("LureBite", "Entered water.");
                biteRoutine ??= StartCoroutine(BiteLoop());
                // Start external spawn manager if provided and not using legacy
                if (!useLegacySpawn && spawnManager != null)
                {
                    spawnManager.BeginListening();
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(waterTag)) return;

            waterOverlapCount = Mathf.Max(0, waterOverlapCount - 1);

            if (waterOverlapCount == 0)
            {
                DebugLogger.Log("LureBite", "Exited water.");
                if (biteRoutine != null)
                {
                    StopCoroutine(biteRoutine);
                    biteRoutine = null;
                }

                if (!useLegacySpawn && spawnManager != null)
                {
                    spawnManager.StopListening();
                }
            }
        }

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

                if (useLegacySpawn)
                {
                    // legacy direct spawning path (kept for migration)
                    SpawnFishLegacy();
                }
                else
                {
                    // new path: ask SpawnManager to spawn (SpawnManager handles species picking & factory)
                    if (spawnManager != null)
                    {
                        // SpawnManager will call into factory and should broadcast pending spawn,
                        // but for quick compatibility we call BeginListening earlier and rely on its loop.
                        DebugLogger.VerboseLog("LureBite", "Requested spawn via SpawnManager (spawn loop will handle).");
                    }
                    else
                    {
                        DebugLogger.Log("LureBite", "SpawnManager not assigned; cannot spawn in new flow.");
                    }
                }
            }

            biteRoutine = null;
        }

        // ---------------- Legacy spawn (kept for migration) ----------------
        void SpawnFishLegacy()
        {
            if (legacyFishPrefabs == null || legacyFishPrefabs.Count == 0)
            {
                DebugLogger.Log("LureBite", "No legacy fish prefabs assigned.");
                return;
            }

            // pick a random prefab (kept simple; prior code used weighted entries)
            GameObject prefab = legacyFishPrefabs[Random.Range(0, legacyFishPrefabs.Count)];
            if (prefab == null) return;

            Vector3 spawnPos = (fishSocket != null ? fishSocket.transform.position : transform.position) + Vector3.up * 0.02f;
            Quaternion spawnRot = fishSocket != null ? fishSocket.transform.rotation : transform.rotation;

            GameObject fish = Instantiate(prefab, spawnPos, spawnRot);
            pendingFish = fish;

            // Scale
            float scale = Mathf.Max(Random.Range(0.8f, 1.3f), minScaleMultiplier);
            fish.transform.localScale *= scale;

            // Rigidbody
            Rigidbody rb = fish.GetComponent<Rigidbody>();
            if (!rb) rb = fish.AddComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Grab interactable (compat)
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = fish.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (!grab)
            {
                Destroy(fish);
                pendingFish = null;
                return;
            }

            UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable ixr = grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
            if (ixr == null)
            {
                CancelPending();
                return;
            }

            if (fishSocket != null)
            {
                // prefer accept via socket if available
                fishSocket.StartManualInteraction(ixr);
                StartCoroutine(ConfirmSocketAccepted(socketAcceptTimeout));
            }
            else
            {
                // no socket — leave pending in world
            }
        }

        IEnumerator ConfirmSocketAccepted(float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (pendingFish == null) yield break;
                if (fishSocket != null && fishSocket.hasSelection) yield break;
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

        // ---------------- Socket events (legacy hookup kept) ----------------

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
    }
}