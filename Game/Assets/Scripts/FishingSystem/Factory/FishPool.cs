// File: FishPool.cs
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
                Return(go);
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
            // cleanup: remove physics joints, reset kinematic, reset state
            var joint = go.GetComponent<FixedJoint>();
            if (joint != null) Destroy(joint);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            go.SetActive(false);
            pool.Enqueue(go);
            DebugLogger.VerboseLog("FishPool", $"Return: pooled {go.name}");
        }
    }
}