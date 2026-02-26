// CatchManager.cs
using System;
using System.Collections;
using UnityEngine;

namespace FishingSystem
{
    public class CatchManager : MonoBehaviour
    {
        public float balanceFactor = 5f; 

        /// <summary>
        /// Unused code
        /// </summary>
        public IEnumerator ResolveFight(Rigidbody fishRb, FishData fishData, PlayerStats player, RodSO rod, Action<bool> onComplete)
        {
            if (fishData == null || fishData.speciesSO == null)
            {
                DebugLogger.Log("CatchManager", "ResolveFight called with missing fish data or speciesSO");
                onComplete?.Invoke(true);
                yield break;
            }

            float fishStrength = (fishData.sizeCm / Mathf.Max(0.0001f, fishData.speciesSO.sizeMax)) * 10f;

            float playerEff = (player != null ? player.StrengthBonusNorm * 10f : 0f) + (rod != null ? rod.strengthBonus : 0f);

            float diff = playerEff - fishStrength;
            float prob = 1f / (1f + Mathf.Exp(-diff / balanceFactor));

            DebugLogger.Log("CatchManager", $"ResolveFight: fishStrength={fishStrength:F2}, playerEff={playerEff:F2}, prob={prob:F3}");

            float duration = 1.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                yield return null;
            }

            bool success = UnityEngine.Random.value < prob;
            DebugLogger.Log("CatchManager", $"ResolveFight result success={success}");
            onComplete?.Invoke(success);
        }
    }
}