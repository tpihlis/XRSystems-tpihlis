using System.Collections.Generic;
using UnityEngine;

namespace FishingSystem
{
    public class FishFactory : MonoBehaviour
    {
        [Header("Pools (one per species recommended)")]
        public List<FishPool> pools = new List<FishPool>();

        [Header("Tuning - size generation")]
        [Tooltip("Mean fraction of sizeMax used when sampling size")]
        public float sizeMeanFraction = 0.7f;
        [Tooltip("Standard deviation fraction of sizeMax used when sampling size")]
        public float sizeSdFraction = 0.15f;

        // ----------------------
        // Pricing / Economy tuning (editable in inspector)
        // ----------------------
        [Header("Pricing / Economy Tuning")]
        [Tooltip("Base euro per cm to compute the species baseline price. basePrice = species.sizeMax * priceBasePerCm * species.priceScale")]
        public float priceBasePerCm = 0.01f;

        [Tooltip("Tier multipliers for main-stat quality (bad -> good). Use several entries to create power-law forks.")]
        public float[] mainTierMultipliers = new float[] { 0.05f, 0.15f, 0.5f, 1.5f, 6.0f };

        [Tooltip("Exponent shaping how combined quality+rarity (0..1) maps to the QR multiplier (higher => more emphasis on top-end).")]
        public float qualityRarityExponent = 1.6f;

        [Tooltip("Map quality/rarity combined to this multiplier range.")]
        public float qrMinMultiplier = 0.2f;
        public float qrMaxMultiplier = 3f;

        [Tooltip("How much the player's Trading stat influences final price (fraction). Example 0.5 => up to +50% at Trading=100.")]
        public float tradingPriceBoostFactor = 0.5f;

        // ----------------------
        // Jackpot tuning (rare high-value spikes)
        // ----------------------
        [Header("Jackpot (rare high-value spikes)")]
        [Tooltip("Base jackpot chance (0..1). Very small by default.")]
        [Range(0f, 1f)] public float baseJackpotChance = 0.0005f; // 0.05% base
        [Tooltip("How strongly player LUCK (0..1) increases jackpot chance (per unit).")]
        public float luckJackpotWeight = 0.015f; // Luck=1 -> +1.5% additional chance
        [Tooltip("Require main-tier index >= this to allow jackpots (0 = allow even lowest).")]
        public int minTierIndexForJackpot = 3; // require fairly high main-tier
        [Tooltip("Conditional jackpot outcome multipliers (drawn when jackpot triggers).")]
        public float[] jackpotMultipliers = new float[] { 3f, 8f, 20f, 50f };
        [Tooltip("Relative weights for the jackpot multipliers above (same length).")]
        public float[] jackpotWeights = new float[] { 80f, 15f, 4f, 1f };

        // ----------------------
        // Runtime / Pooling (keeps unchanged)
        // ----------------------

        public GameObject SpawnPendingFish(FishSpeciesSO species, LureSO lure, PlayerStats player, Transform attachTransform)
        {
            if (species == null)
            {
                DebugLogger.Log("FishFactory", "SpawnPendingFish called with null species");
                return null;
            }

            FishPool pool = pools.Find(p => p.speciesSO == species);
            if (pool == null)
            {
                DebugLogger.Log("FishFactory", $"No pool found for species {species.speciesId}");
                return null;
            }

            Vector3 pos = attachTransform.position + Vector3.up * 0.02f;
            Quaternion rot = attachTransform.rotation;
            GameObject fishGO = pool.Get(pos, rot);
            if (fishGO == null)
            {
                DebugLogger.Log("FishFactory", $"Pool.Get returned null for species {species.speciesId}");
                return null;
            }

            var grab = fishGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null)
            {
                DebugLogger.Log("FishFactory", $"Spawned fish prefab missing XRGrabInteractable: {fishGO.name}");
                pool.Return(fishGO);
                return null;
            }

            var fd = fishGO.GetComponent<FishInstance>();
            if (fd == null) fd = fishGO.AddComponent<FishInstance>();

            FishData data = GenerateFishData(species, lure, player);
            fd.AssignData(data);

            var rb = fishGO.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Pending fish are kinematic while waiting for socket acceptance (prevents physics jerk)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // disable throw while pending
            if (grab != null)
            {
                grab.throwOnDetach = false;
            }

            float scale = data.sizeCm / Mathf.Max(0.0001f, species.sizeMax);
            fishGO.transform.localScale = Vector3.one * scale;

            fd.isPending = true;
            fd.originPool = pool;

            DebugLogger.Log("FishFactory", $"Spawned pending fish {species.speciesId} size={data.sizeCm:F1}cm quality={data.qualityDisplay:F2} rarity={data.rarityDisplay:F2} price={data.priceEuros:F2}");
            return fishGO;
        }

        public FishData GenerateFishData(FishSpeciesSO species, LureSO lure, PlayerStats player)
        {
            var data = new FishData();
            data.speciesId = species.speciesId;
            data.speciesSO = species;

            // -- Size sampling --
            float mean = species.sizeMax * sizeMeanFraction;
            float sd = species.sizeMax * sizeSdFraction;
            float size = RNGService.TruncatedNormal(mean, sd, species.sizeMin, species.sizeMax);

            float sizeMultiplier = 1f + (player != null ? player.StrengthBonusNorm * 0.1f : 0f);
            size *= sizeMultiplier;
            data.sizeCm = Mathf.Clamp(size, species.sizeMin, species.sizeMax);

            // -- Age sampling --
            float age = RNGService.Range(species.ageMin, species.ageMax);
            data.ageYears = age;

            // -- Scoring components --
            float sizeScore = Mathf.Clamp01(data.sizeCm / Mathf.Max(0.0001f, species.sizeMax));
            float ageRange = Mathf.Max(0.01f, species.ageMax - species.optimalAge);
            float ageScore = Mathf.Clamp01(1f - Mathf.Abs(data.ageYears - species.optimalAge) / ageRange);

            // quality composition (weights tunable)
            float wSpecies = 0.35f;
            float wSize = 0.45f;
            float wAge = 0.20f;

            float fishingFactor = (player != null ? player.FishingBonusNorm * 0.5f : 0f);

            data.qualityNorm = Mathf.Clamp01(
                wSpecies * (species.baseQualityNorm) +
                wSize * sizeScore +
                wAge * ageScore +
                fishingFactor
            );

            // rarity composition
            float wBaseRarity = 0.6f;
            float wSizeRarity = 0.4f;
            float luckFactor = (player != null ? player.LuckBonusNorm * 0.5f : 0f);

            float baseRarity = (species.baseRarityNorm);
            data.rarityNorm = Mathf.Clamp01(
                wBaseRarity * baseRarity
                + wSizeRarity * sizeScore
                + luckFactor
            );

            // small chance of special trait
            float traitBase = 0.01f;
            float traitChance = traitBase + (player != null ? player.LuckBonusNorm * 0.02f : 0f);
            data.specialTrait = RNGService.Range(0f, 1f) < traitChance;

            // compute display values for convenience (1..10)
            data.ComputeDisplayValues();

            // --- Pricing computation (new, tunable) ---
            {
                // species-level base price (size-based) and per-species scale
                float basePrice = Mathf.Max(0.01f, species.sizeMax * priceBasePerCm * species.priceScale);

                // main-stat normalized combining size and age (same components used above)
                float sizeScoreForPrice = Mathf.Clamp01(data.sizeCm / Mathf.Max(0.0001f, species.sizeMax));
                float ageScoreForPrice = ageScore;
                float mainStatNorm = Mathf.Clamp01(sizeScoreForPrice * 0.7f + ageScoreForPrice * 0.3f);

                // determine tier multiplier from mainTierMultipliers using mainStatNorm
                int tierCount = Mathf.Max(1, mainTierMultipliers.Length);
                int tierIndex = Mathf.Clamp(Mathf.FloorToInt(mainStatNorm * (tierCount - 1)), 0, tierCount - 1);
                float mainMultiplier = mainTierMultipliers[tierIndex];

                // combine quality & rarity (0..1) and shape it with exponent
                float qrCombined = Mathf.Clamp01((data.qualityNorm + data.rarityNorm) * 0.5f);
                float qrT = Mathf.Pow(qrCombined, Mathf.Max(0.0001f, qualityRarityExponent));
                float qrMultiplier = Mathf.Lerp(qrMinMultiplier, qrMaxMultiplier, qrT);

                // JACKPOT: compute chance (player LUCK only affects jackpotChance)
                float playerLuck = (player != null ? player.LuckBonusNorm : 0f);
                float jackpotChance = baseJackpotChance + playerLuck * luckJackpotWeight;
                jackpotChance = Mathf.Clamp01(jackpotChance);

                float jackpotMultiplier = 1f;
                bool allowedTier = (tierIndex >= minTierIndexForJackpot);

                if (allowedTier)
                {
                    float roll = RNGService.Range(0f, 1f);
                    if (roll < jackpotChance)
                    {
                        // jackpot triggered -> pick a conditional outcome based on jackpotWeights
                        if (jackpotMultipliers != null && jackpotMultipliers.Length > 0 && jackpotWeights != null && jackpotWeights.Length == jackpotMultipliers.Length)
                        {
                            float totalW = 0f;
                            for (int i = 0; i < jackpotWeights.Length; i++) totalW += Mathf.Max(0f, jackpotWeights[i]);
                            float r = RNGService.Range(0f, totalW);
                            float acc = 0f;
                            for (int i = 0; i < jackpotWeights.Length; i++)
                            {
                                acc += Mathf.Max(0f, jackpotWeights[i]);
                                if (r <= acc)
                                {
                                    jackpotMultiplier = Mathf.Max(0.0001f, jackpotMultipliers[i]);
                                    break;
                                }
                            }
                            if (jackpotMultiplier <= 0f) jackpotMultiplier = jackpotMultipliers[0];
                        }
                        else
                        {
                            jackpotMultiplier = 5f;
                        }

                        DebugLogger.Log("FishFactory", $"Jackpot! species={species.speciesId} tier={tierIndex} jackpotMul={jackpotMultiplier:F2} chance={jackpotChance:F3}");
                    }
                }

                // player's trading bonus (small fractional boost)
                float tradingBoost = 1f + ((player != null ? player.TradingBonusNorm : 0f) * tradingPriceBoostFactor);

                // final price
                float rawPrice = basePrice * mainMultiplier * qrMultiplier * jackpotMultiplier * tradingBoost;
                data.priceEuros = Mathf.Round(rawPrice * 100f) / 100f;
            }

            return data;
        }
    }
}