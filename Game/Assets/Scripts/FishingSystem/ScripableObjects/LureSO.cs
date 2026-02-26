using UnityEngine;

namespace FishingSystem
{
    [CreateAssetMenu(menuName = "Fishing/LureSO", fileName = "LureSO")]
    public class LureSO : ScriptableObject
    {
        public string lureId;
        public string displayName;

        // NEW: physical prefab for the lure (spawned in world)
        [Tooltip("Prefab for the physical lure (optional). If set, GearSpawner will Instantiate this at start.")]
        public GameObject physicalPrefab;

        public Sprite icon;

        [Header("Spawn biases")]
        public float spawnBias = 0f;
        public float sizeBonus = 0f;
        public float rarityBonus = 0f;
        public float qualityBonus = 0f;
    }
}