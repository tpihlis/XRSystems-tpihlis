using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishingSystem
{
    /// <summary>
    /// Small UI component that displays one socket's contents (fish stats) or "Empty".
    /// Designed to be a child under a world-space Canvas.
    /// </summary>
    public class SocketSlotUI : MonoBehaviour
    {
        [Header("Text fields")]
        public TextMeshProUGUI headerText;   // e.g. "Slot 1"
        public TextMeshProUGUI speciesText;  // species/display name or "Empty"
        public TextMeshProUGUI sizeText;
        public TextMeshProUGUI qualityText;
        public TextMeshProUGUI rarityText;
        public TextMeshProUGUI priceText;
        public TextMeshProUGUI traitText;    // e.g. "Trait"

        [Header("Optional images")]
        public Image speciesIcon;            // optional sprite if you have one
        public GameObject emptyVisual;       // optional "Empty" overlay (enable/disable)

        /// <summary>
        /// Fill UI fields from a FishData object. Null => show "Empty"
        /// </summary>
        public void SetFishData(int slotIndex, FishingSystem.FishData data)
        {
            if (headerText != null) headerText.text = $"Slot {slotIndex+1}";

            if (data == null || data.speciesSO == null)
            {
                if (speciesText != null) speciesText.text = "Empty";
                if (sizeText != null) sizeText.text = "";
                if (qualityText != null) qualityText.text = "";
                if (rarityText != null) rarityText.text = "";
                if (priceText != null) priceText.text = "";
                if (traitText != null) traitText.text = "";
                if (emptyVisual != null) emptyVisual.SetActive(true);
                if (speciesIcon != null) speciesIcon.enabled = false;
                return;
            }

            if (emptyVisual != null) emptyVisual.SetActive(false);

            // Display species / name
            string name = data.speciesSO != null && !string.IsNullOrEmpty(data.speciesSO.displayName)
                ? data.speciesSO.displayName
                : data.speciesId;
            if (speciesText != null) speciesText.text = name;

            // size, quality, rarity, price (format nicely)
            if (sizeText != null) sizeText.text = $"Size: {data.sizeCm:F1} cm";
            if (qualityText != null) qualityText.text = $"Quality: {data.qualityDisplay:F1}/10";
            if (rarityText != null) rarityText.text = $"Rarity: {data.rarityDisplay:F1}/10";
            if (priceText != null) priceText.text = $"Price: {data.priceEuros:F2}€";

            if (traitText != null) traitText.text = (data.specialTrait ? "★ Special" : "");

            // optional icon (if speciesSO had a sprite field you could use; fallback disable)
            if (speciesIcon != null)
            {
                // FishSpeciesSO does not include an icon by default in your code.
                // If you add an icon to FishSpeciesSO, assign it here:
                speciesIcon.enabled = false;
            }
        }
    }
}