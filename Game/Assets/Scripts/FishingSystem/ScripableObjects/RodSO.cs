// File: RodSO.cs
using UnityEngine;

namespace FishingSystem
{
    [CreateAssetMenu(menuName = "Fishing/RodSO", fileName = "RodSO")]
    public class RodSO : ScriptableObject
    {
        public string rodId;
        public string displayName;
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