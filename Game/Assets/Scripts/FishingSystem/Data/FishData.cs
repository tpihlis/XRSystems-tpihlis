using UnityEngine;

namespace FishingSystem
{
    [System.Serializable]
    public class FishData
    {
        public string speciesId;
        public FishSpeciesSO speciesSO;
        public float sizeCm;
        public float ageYears;

        public float qualityNorm;
        public float rarityNorm;

        // Display-friendly values (1..10)
        public float qualityDisplay;
        public float rarityDisplay;

        // Final computed price (set by FishFactory)
        public float priceEuros;

        public bool specialTrait = false;

        // compute only the display fields 
        public void ComputeDisplayValues()
        {
            qualityDisplay = 1f + qualityNorm * 9f;
            rarityDisplay = 1f + rarityNorm * 9f;
        }
    }
}