// File: SpawnManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishingSystem
{
    public class SpawnManager : MonoBehaviour
    {
        [Header("Socket / spawn")]
        public Transform fishSocketTransform;

        // Reference to the socket handler so we can avoid spawning while a fish is pending/selected
        public SocketInteractionHandler socketHandler;

        [Header("Bite settings")]
        public float biteChance = 0.25f;
        public Vector2 biteInterval = new Vector2(1f, 5f);

        [Header("Pools / refs")]
        public List<FishSpeciesSO> speciesPool = new List<FishSpeciesSO>();
        public FishFactory factory;
        public PlayerStats player;

        [Header("Debug")]
        public bool debugLogs = true;

        Coroutine biteRoutine;

        public void BeginListening()
        {
            DebugLogger.Log("SpawnManager", "BeginListening called");
            if (biteRoutine == null)
                biteRoutine = StartCoroutine(BiteLoop());
        }

        public void StopListening()
        {
            DebugLogger.Log("SpawnManager", "StopListening called");
            if (biteRoutine != null)
            {
                StopCoroutine(biteRoutine);
                biteRoutine = null;
            }
        }

        IEnumerator BiteLoop()
        {
            // small initial delay so other systems initialize
            yield return new WaitForSeconds(0.25f);

            while (true)
            {
                // wait for a randomized interval between bite checks
                float wait = RNGService.Range(biteInterval.x, biteInterval.y);
                if (debugLogs) DebugLogger.VerboseLog("SpawnManager", $"Waiting {wait:F2}s for next bite check");
                yield return new WaitForSeconds(wait);

                // If socket handler is assigned, skip spawn attempt while socket is busy
                if (socketHandler != null && socketHandler.HasPendingOrSelection())
                {
                    DebugLogger.VerboseLog("SpawnManager", "Socket busy (selection or pending) — skipping spawn check and waiting a bit");
                    // wait a short time before next check to avoid busy-looping
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                float roll = RNGService.Range(0f, 1f);
                if (debugLogs) DebugLogger.VerboseLog("SpawnManager", $"Bite roll {roll:F3} vs chance {biteChance:F3}");
                if (roll > biteChance) continue;

                // build weighted list from species pool
                if (speciesPool == null || speciesPool.Count == 0)
                {
                    DebugLogger.VerboseLog("SpawnManager", "No species in speciesPool — skipping spawn");
                    continue;
                }

                List<float> weights = new List<float>(speciesPool.Count);
                for (int i = 0; i < speciesPool.Count; i++)
                {
                    var s = speciesPool[i];
                    float w = Mathf.Max(0f, s.spawnWeight) * (1f + player.LuckBonusNorm * 0.1f);
                    if (player.equippedLure != null) w *= (1f + player.equippedLure.spawnBias);
                    weights.Add(w);
                    DebugLogger.VerboseLog("SpawnManager", $"Species {s.speciesId} baseWeight={s.spawnWeight} adjWeight={w}");
                }

                int idx = RNGService.WeightedChoiceIndex(weights);
                if (idx < 0 || idx >= speciesPool.Count)
                {
                    DebugLogger.Log("SpawnManager", "WeightedChoiceIndex returned invalid index");
                    continue;
                }

                var species = speciesPool[idx];
                DebugLogger.Log("SpawnManager", $"Selected species {species.speciesId} (idx {idx})");

                var pending = factory.SpawnPendingFish(species, player.equippedLure, player, fishSocketTransform);
                if (pending != null)
                {
                    DebugLogger.Log("SpawnManager", $"Pending fish spawned: {pending.name}");
                    // notify socket handler if you want it to start its accept timeout tracking
                    if (socketHandler != null)
                    {
                        socketHandler.SetPending(pending, 5f);
                    }
                }
            }
        }
    }
}