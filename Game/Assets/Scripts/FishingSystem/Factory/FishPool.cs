using System.Collections.Generic;
using UnityEngine;

namespace FishingSystem
{
    public class FishPool : MonoBehaviour
    {
        [Header("Pooling")]
        public FishSpeciesSO speciesSO; // determines which prefab
        public int initialSize = 5;

        Queue<GameObject> pool = new Queue<GameObject>();

        void Awake()
        {
            if (speciesSO == null) return;
            for (int i = 0; i < initialSize; i++)
            {
                var go = InstantiatePooled();
                if (go != null) Return(go);
            }
            DebugLogger.Log("FishPool", $"Initialized pool for {speciesSO.speciesId} with {initialSize} items");
        }

        GameObject InstantiatePooled()
        {
            if (speciesSO == null || speciesSO.prefab == null)
            {
                DebugLogger.Log("FishPool", "InstantiatePooled failed: no speciesSO or prefab");
                return null;
            }
            var go = Instantiate(speciesSO.prefab);

            // Ensure safe pooled state immediately:
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // clear velocities while the body is still non-kinematic (safe)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
            {
                // when pooled, default to allowing throw later (safe default)
                grab.throwOnDetach = true;
            }

            go.SetActive(false);
            return go;
        }

        public GameObject Get(Vector3 pos, Quaternion rot)
        {
            GameObject go = null;
            if (pool.Count > 0)
                go = pool.Dequeue();
            else
                go = InstantiatePooled();

            if (go == null) return null;
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);

            DebugLogger.VerboseLog("FishPool", $"Get: returned {go.name}");
            return go;
        }

        public void Return(GameObject go)
        {
            if (go == null) return;

            var joint = go.GetComponent<FixedJoint>();
            if (joint != null) Destroy(joint);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // If currently non-kinematic, zero velocities first (safe).
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // POOLED state must be kinematic + no gravity to avoid falling away while inactive
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Make sure the grab behaviour is reset to a safe default
            var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
            {
                grab.throwOnDetach = true;
            }

            go.SetActive(false);
            pool.Enqueue(go);
            DebugLogger.VerboseLog("FishPool", $"Return: pooled {go.name}");
        }

        /// <summary>
        /// Ensure the pool contains at least initialSize items (instantiates additional prefabs and returns them to the pool).
        /// Useful to refill pools between runs.
        /// </summary>
        public void RefillPool()
        {
            // Protect against missing prefab/species
            if (speciesSO == null || speciesSO.prefab == null)
            {
                DebugLogger.Log("FishPool", $"RefillPool skipped for {gameObject.name} - missing speciesSO or prefab.");
                return;
            }

            // If pool already has at least initialSize items, do nothing.
            // Note: we don't know how many instances are currently active in scene; this ensures the pool has a healthy reserve.
            int need = initialSize - pool.Count;
            if (need <= 0)
            {
                DebugLogger.VerboseLog("FishPool", $"RefillPool: pool for {speciesSO.speciesId} already has {pool.Count} >= {initialSize}");
                return;
            }

            for (int i = 0; i < need; i++)
            {
                var go = InstantiatePooled();
                if (go != null) Return(go);
            }

            DebugLogger.Log("FishPool", $"RefillPool: added {need} items to pool for {speciesSO.speciesId} (now {pool.Count})");
        }
    }
}