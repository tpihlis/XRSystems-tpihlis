// File: PlayerStats.cs
using System;
using UnityEngine;

namespace FishingSystem
{
    [Serializable]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Persistent player stats (0..100 recommended)")]
        [Range(0, 100)] public int fishing = 0;
        [Range(0, 100)] public int strength = 0;
        [Range(0, 100)] public int luck = 0;
        [Range(0, 100)] public int trading = 0;

        [Header("Equipped items")]
        public RodSO equippedRod;
        public LureSO equippedLure;

        public bool debugLogs = true;

        public float FishingBonusNorm => fishing * 0.01f + (equippedRod ? equippedRod.fishingBonus : 0f);
        public float StrengthBonusNorm => strength * 0.01f + (equippedRod ? equippedRod.strengthBonus : 0f);
        public float LuckBonusNorm => luck * 0.01f;
        public float TradingBonusNorm => trading * 0.01f;
    }
}