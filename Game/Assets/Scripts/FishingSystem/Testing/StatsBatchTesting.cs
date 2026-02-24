// File: Assets/Scripts/FishingSystem/Testing/StatsBatchTesterComponent.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishingSystem
{
    [AddComponentMenu("Fishing/Testing/StatsBatchTester (Component)")]
    public class StatsBatchTesterComponent : MonoBehaviour
    {
        [Header("References (assign)")]
        public FishFactory factory;
        public FishSpeciesSO testSpecies;
        public LureSO testLure; // optional

        [Header("Samples")]
        [Tooltip("How many fish to sample per preset")]
        public int samplesPerPreset = 500;

        [Header("Presets")]
        public List<PlayerPreset> presets = new List<PlayerPreset>()
        {
            new PlayerPreset() { presetName = "Low", fishing = 5, strength = 5, luck = 2, trading = 5 },
            new PlayerPreset() { presetName = "Medium", fishing = 30, strength = 30, luck = 20, trading = 30 },
            new PlayerPreset() { presetName = "High", fishing = 70, strength = 70, luck = 60, trading = 60 }
        };

        [Header("Behaviour")]
        public bool silenceVerboseDuringRun = true;
        public bool autoRunOnEnable = false;
        public bool printCompletionLog = true;

        [System.Serializable]
        public class PlayerPreset
        {
            public string presetName = "Preset";
            [Range(0, 100)] public int fishing = 0;
            [Range(0, 100)] public int strength = 0;
            [Range(0, 100)] public int luck = 0;
            [Range(0, 100)] public int trading = 0;
            public RodSO rod;
            public LureSO lure;
        }

        bool isRunning = false;

        // Inspector context menu
        [ContextMenu("Run Batch Tests (component)")]
        public void RunBatchTestsContextMenu()
        {
            if (!Application.isPlaying)
            {
                // In editor non-play mode we still need to start coroutine; StartCoroutine works in edit mode for playmode testing only when using EditorCoroutineUtility.
                // Simpler: warn user & run only in play mode.
                DebugLogger.Log("StatsBatchTesterComponent", "RunBatchTests must be executed in Play mode. Enable the GameObject and press Play, then use the context menu or call RunBatchTests().");
                return;
            }
            RunBatchTests();
        }

        // Public runner (usable from other scripts)
        public void RunBatchTests()
        {
            if (isRunning)
            {
                DebugLogger.Log("StatsBatchTesterComponent", "Batch test already running.");
                return;
            }

            if (factory == null || testSpecies == null)
            {
                DebugLogger.Log("StatsBatchTesterComponent", "Missing factory or testSpecies - aborting batch test.");
                return;
            }

            StartCoroutine(RunBatchTestsCoroutine());
        }

        void OnEnable()
        {
            if (autoRunOnEnable && Application.isPlaying)
                RunBatchTests();
        }

        IEnumerator RunBatchTestsCoroutine()
        {
            isRunning = true;

            // Save + silence verbose optionally
            bool prevVerbose = DebugLogger.Verbose;
            if (silenceVerboseDuringRun) DebugLogger.Verbose = false;

            // temporary player object for sampling
            var tempGO = new GameObject("StatsBatchTester_PlayerTemp");
            tempGO.hideFlags = HideFlags.HideAndDontSave;
            var tempPlayer = tempGO.AddComponent<PlayerStats>();

            foreach (var preset in presets)
            {
                tempPlayer.fishing = preset.fishing;
                tempPlayer.strength = preset.strength;
                tempPlayer.luck = preset.luck;
                tempPlayer.trading = preset.trading;
                tempPlayer.equippedRod = preset.rod;
                tempPlayer.equippedLure = preset.lure ?? testLure;

                var qualities = new List<float>(samplesPerPreset);
                var rarities = new List<float>(samplesPerPreset);
                var prices = new List<float>(samplesPerPreset);

                for (int i = 0; i < samplesPerPreset; i++)
                {
                    var fd = factory.GenerateFishData(testSpecies, tempPlayer.equippedLure, tempPlayer);

                    qualities.Add(fd.qualityDisplay);
                    rarities.Add(fd.rarityDisplay);
                    prices.Add(fd.priceEuros);

                    // keep the frame responsive
                    if (i % 200 == 0) yield return null;
                }

                // compute metrics
                var qAvg = qualities.Average();
                var qMin = qualities.Min();
                var qMax = qualities.Max();
                var qMed = Median(qualities);

                var rAvg = rarities.Average();
                var rMin = rarities.Min();
                var rMax = rarities.Max();
                var rMed = Median(rarities);

                var pAvg = prices.Average();
                var pMin = prices.Min();
                var pMax = prices.Max();
                var pMed = Median(prices);

                DebugLogger.Log("StatsBatchTesterComponent",
                    $"Preset='{preset.presetName}' Species='{testSpecies.speciesId}' samples={samplesPerPreset} | " +
                    $"Quality avg={qAvg:F2} med={qMed:F2} min={qMin:F2} max={qMax:F2} | " +
                    $"Rarity avg={rAvg:F2} med={rMed:F2} min={rMin:F2} max={rMax:F2} | " +
                    $"Price avg={pAvg:F2} med={pMed:F2} min={pMin:F2} max={pMax:F2}"
                );

                yield return null;
            }

            // restore verbose
            if (silenceVerboseDuringRun) DebugLogger.Verbose = prevVerbose;

            // cleanup
            if (tempPlayer != null)
            {
                if (Application.isPlaying) Destroy(tempPlayer);
                else DestroyImmediate(tempPlayer);
            }
            if (tempGO != null)
            {
                if (Application.isPlaying) Destroy(tempGO);
                else DestroyImmediate(tempGO);
            }

            if (printCompletionLog)
                DebugLogger.Log("StatsBatchTesterComponent", "Batch tests completed.");

            isRunning = false;
        }

        static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            var s = values.OrderBy(x => x).ToList();
            int n = s.Count;
            if (n % 2 == 1) return s[n / 2];
            return (s[n / 2 - 1] + s[n / 2]) * 0.5f;
        }
    }
}