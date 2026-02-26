using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Persistent player stats and equipped gear.
    /// Extended with money and simple helpers to modify stats.
    /// Money is stored as float euros (rounded to 2 decimals when changed).
    /// </summary>
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

        [Header("Owned gear (simple inventory)")]
        public List<RodSO> ownedRods = new List<RodSO>();
        public List<LureSO> ownedLures = new List<LureSO>();

        [Header("Money (euros)")]
        [Tooltip("Player money in euros (float). Uses 2-decimal rounding on changes.")]
        public float money = 0f;

        [Header("Debug")]
        public bool debugLogs = true;

        // Event fired when gear changes (payload optional)
        public event Action OnGearChanged;

        // Event fired when money changed: payload = new money value
        public event Action<float> OnMoneyChanged;

        // Derived normalized bonuses (0..1)
        public float FishingBonusNorm => fishing * 0.01f + (equippedRod ? equippedRod.fishingBonus : 0f);
        public float StrengthBonusNorm => strength * 0.01f + (equippedRod ? equippedRod.strengthBonus : 0f);
        public float LuckBonusNorm => luck * 0.01f;
        public float TradingBonusNorm => trading * 0.01f;

        // --- Inventory helpers ---
        public bool HasRod(RodSO rod) => rod != null && ownedRods.Contains(rod);
        public bool HasLure(LureSO lure) => lure != null && ownedLures.Contains(lure);

        public void AddOwnedRod(RodSO rod)
        {
            if (rod == null) return;
            if (!ownedRods.Contains(rod)) ownedRods.Add(rod);
        }

        public void AddOwnedLure(LureSO lure)
        {
            if (lure == null) return;
            if (!ownedLures.Contains(lure)) ownedLures.Add(lure);
        }

        // --- Equip methods ---
        public void EquipRod(RodSO rod)
        {
            equippedRod = rod;
            if (rod != null) AddOwnedRod(rod);
            OnGearChanged?.Invoke();
            if (debugLogs) Debug.Log($"[PlayerStats] Equipped rod: {(rod != null ? rod.displayName : "none")}");
        }

        public void EquipLure(LureSO lure)
        {
            equippedLure = lure;
            if (lure != null) AddOwnedLure(lure);
            OnGearChanged?.Invoke();
            if (debugLogs) Debug.Log($"[PlayerStats] Equipped lure: {(lure != null ? lure.displayName : "none")}");
        }

        // Optionally a convenience method to clear gear (used by LevelManager on fail)
        public void UnequipAllGear(bool keepOwned = true)
        {
            equippedRod = null;
            equippedLure = null;
            OnGearChanged?.Invoke();
            if (debugLogs) Debug.Log("[PlayerStats] Unequipped all gear.");
            if (!keepOwned)
            {
                ownedRods.Clear();
                ownedLures.Clear();
            }
        }

        // --- Money helpers ---
        public void AddMoney(float euros)
        {
            if (euros == 0f) return;
            money = RoundMoneyToCents(money + euros);
            OnMoneyChanged?.Invoke(money);
            if (debugLogs) Debug.Log($"[PlayerStats] Added money: {euros:F2}€. New balance: {money:F2}€");
        }

        /// <summary>
        /// Try to spend `euros`. Returns true if spent (and reduces balance), false if insufficient funds.
        /// </summary>
        public bool SpendMoney(float euros)
        {
            euros = Mathf.Max(0f, RoundMoneyToCents(euros));
            if (money + 1e-6f >= euros)
            {
                money = RoundMoneyToCents(money - euros);
                OnMoneyChanged?.Invoke(money);
                if (debugLogs) Debug.Log($"[PlayerStats] Spent {euros:F2}€. New balance: {money:F2}€");
                return true;
            }
            else
            {
                if (debugLogs) Debug.Log($"[PlayerStats] Not enough money to spend {euros:F2}€ (balance {money:F2}€)");
                return false;
            }
        }

        float RoundMoneyToCents(float v) => Mathf.Round(v * 100f) / 100f;

        // --- Stat increment helpers (used by buttons) ---
        public void IncreaseFishing(int delta = 1)
        {
            fishing = Mathf.Clamp(fishing + delta, 0, 100);
            if (debugLogs) Debug.Log($"[PlayerStats] fishing -> {fishing}");
        }

        public void IncreaseStrength(int delta = 1)
        {
            strength = Mathf.Clamp(strength + delta, 0, 100);
            if (debugLogs) Debug.Log($"[PlayerStats] strength -> {strength}");
        }

        public void IncreaseLuck(int delta = 1)
        {
            luck = Mathf.Clamp(luck + delta, 0, 100);
            if (debugLogs) Debug.Log($"[PlayerStats] luck -> {luck}");
        }

        public void IncreaseTrading(int delta = 1)
        {
            trading = Mathf.Clamp(trading + delta, 0, 100);
            if (debugLogs) Debug.Log($"[PlayerStats] trading -> {trading}");
        }

        // --- Reset helpers (used on fail) ---
        /// <summary>
        /// Reset numeric player stats and money to default zero.
        /// Leaves owned gear list intact, but unequips current gear.
        /// </summary>
        public void ResetStatsAndMoney()
        {
            fishing = 0;
            strength = 0;
            luck = 0;
            trading = 0;
            money = 0f;
            // also unequip gear (keeps owned lists)
            equippedRod = null;
            equippedLure = null;

            OnMoneyChanged?.Invoke(money);
            OnGearChanged?.Invoke();

            if (debugLogs) Debug.Log("[PlayerStats] Reset stats and money due to run failure.");
        }
    }
}