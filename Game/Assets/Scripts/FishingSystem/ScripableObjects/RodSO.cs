using UnityEngine;

namespace FishingSystem
{
    [CreateAssetMenu(menuName = "Fishing/RodSO", fileName = "RodSO")]
    public class RodSO : ScriptableObject
    {
        public string rodId;
        public string displayName;

        // NEW: physical prefab for the rod (spawned in world)
        [Tooltip("Prefab for the physical rod (optional). If set, GearSpawner will Instantiate this at start.")]
        public GameObject physicalPrefab;

        public Sprite icon;

        [Header("Stat requirements")]
        public int minFishing = 0;
        public int minStrength = 0;

        [Header("Bonuses")]
        public float fishingBonus = 0f;
        public float strengthBonus = 0f;
        public float gearQualityBoost = 0f;
    }
}