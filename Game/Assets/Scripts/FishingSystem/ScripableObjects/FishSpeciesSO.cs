using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// FishSpeciesSO: per-species designer data. Tooltips explain each field and which factory fields affect them.
    /// </summary>
    [CreateAssetMenu(menuName = "Fishing/SpeciesSO", fileName = "SpeciesSO")]
    public class FishSpeciesSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique id (used in code)")]
        public string speciesId;
        [Tooltip("Display name shown to players")]
        public string displayName;
        [Tooltip("Prefab for this species. Prefab should contain XRGrabInteractable, Rigidbody and Collider.")]
        public GameObject prefab;

        [Header("Size (cm)")]
        [Tooltip("Minimum plausible adult size (cm). Used as lower bound when sampling sizes.\nAffected by: FishFactory.sizeMeanFraction, sizeSdFraction.")]
        public float sizeMin = 5f;

        [Tooltip("Maximum plausible size (cm). Used as upper bound when sampling sizes and for base price calculation.\nAffected by: FishFactory.priceBasePerCm (basePrice uses sizeMax).")]
        public float sizeMax = 100f;

        [Tooltip("Designer-set typical/average size (cm) for display and quick tuning. Not strictly required by sampling (factory samples from distribution).\nAffected by: none directly (informational).")]
        public float sizeAverage = 50f;

        [Header("Age (years)")]
        [Tooltip("Minimum possible age (years).")]
        public float ageMin = 0f;

        [Tooltip("Maximum possible age (years).")]
        public float ageMax = 10f;

        [Tooltip("Age (years) at which the fish is considered 'optimal' for quality scoring.\nAffected by: FishFactory age scoring weights.")]
        public float optimalAge = 2f;

        [Header("Baseline quality / rarity (0..1)")]
        [Tooltip("Base species quality baseline (0..1). The factory mixes this with size/age and player/gear bonuses to produce final quality.\nAffected by: FishFactory quality composition weights, player Fishing stat, lure.qualityBonus, rod.gearQualityBoost.")]
        [Range(0f, 1f)] public float baseQualityNorm = 0.5f;

        [Tooltip("Base species rarity baseline (0..1). The factory mixes this with size and luck to produce final rarity.\nAffected by: FishFactory rarity composition weights, player Luck, lure.rarityBonus.")]
        [Range(0f, 1f)] public float baseRarityNorm = 0.5f;

        [Header("Spawn / economy")]
        [Tooltip("Relative weight when sampling which species spawns. Higher = more common.\nAffected by: Player luck (SpawnManager may adjust) and lure.spawnBias if SpawnManager multiplies by it.")]
        public float spawnWeight = 1f;

        [Tooltip("Per-species price scale multiplier applied to basePrice (lets designers tune species value separately).\nFinal basePrice = sizeMax * FishFactory.priceBasePerCm * priceScale")]
        public float priceScale = 1f;

        [Header("Editor hints")]
        [Tooltip("Recommended Rigidbody mass for this species prefab (helps consistent physics).")]
        public float recommendedMass = 1f;
    }
}