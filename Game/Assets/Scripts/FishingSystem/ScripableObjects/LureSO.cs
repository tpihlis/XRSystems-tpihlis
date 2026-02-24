// File: LureSO.cs
using UnityEngine;

namespace FishingSystem
{
    [CreateAssetMenu(menuName = "Fishing/LureSO", fileName = "LureSO")]
    public class LureSO : ScriptableObject
    {
        public string lureId;
        public string displayName;
        public Sprite icon;

        [Header("Spawn biases")]
        public float spawnBias = 0f;
        public float sizeBonus = 0f;
        public float rarityBonus = 0f;
        public float qualityBonus = 0f;
    }
}