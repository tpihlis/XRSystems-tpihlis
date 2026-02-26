using System.Collections;
using TMPro;
using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Very small world-space HUD that displays PlayerStats values and money.
    /// Polls the PlayerStats periodically and updates text fields.
    /// Designed to be placed on a World-space Canvas.
    /// </summary>
    [AddComponentMenu("Fishing/GameFlow/PlayerStatsDisplay")]
    public class PlayerStatsDisplay : MonoBehaviour
    {
        [Header("References")]
        public PlayerStats playerStats; // assign  PlayerStats MonoBehaviour (player GameObject)

        [Header("Text fields (TextMeshProUGUI)")]
        public TextMeshProUGUI fishingText;
        public TextMeshProUGUI strengthText;
        public TextMeshProUGUI luckText;
        public TextMeshProUGUI tradingText;

        [Header("Money")]
        [Tooltip("Optional text field to show player's money (reads playerStats.money directly).")]
        public TextMeshProUGUI moneyText;

        [Header("Update")]
        [Tooltip("How often (seconds) the HUD refreshes.")]
        public float updateInterval = 0.12f;

        void OnEnable()
        {
            StopAllCoroutines();
            if (playerStats != null)
                StartCoroutine(UpdateLoop());
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        IEnumerator UpdateLoop()
        {
            while (true)
            {
                RefreshNow();
                yield return new WaitForSeconds(updateInterval);
            }
        }

        /// <summary>
        /// Immediate refresh (safe to call from other scripts).
        /// </summary>
        public void RefreshNow()
        {
            if (playerStats == null) return;

            // numeric values are 0..100 recommended in PlayerStats
            int fishing = playerStats.fishing;
            int strength = playerStats.strength;
            int luck = playerStats.luck;
            int trading = playerStats.trading;

            if (fishingText != null) fishingText.text = $"Fishing\n{fishing}";
            if (strengthText != null) strengthText.text = $"Strength\n{strength}";
            if (luckText != null) luckText.text = $"Luck\n{luck}";
            if (tradingText != null) tradingText.text = $"Trading\n{trading}";

            if (moneyText != null)
            {
                // read money directly from playerStats; expects a public float money field
                moneyText.text = $"Money\n{playerStats.money:F2}€";
            }
        }
    }
}