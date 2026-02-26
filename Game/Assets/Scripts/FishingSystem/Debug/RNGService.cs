using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishingSystem
{
    public static class RNGService
    {
        private static System.Random rng = new System.Random();

        public static void Seed(int seed)
        {
            rng = new System.Random(seed);
            DebugLogger.Log("RNG", $"Seeded RNG with {seed}");
        }

        public static int WeightedChoiceIndex(IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f)
            {
                DebugLogger.Log("RNG", "WeightedChoiceIndex: total weight <= 0");
                return -1;
            }

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += Math.Max(0f, weights[i]);
                if (roll <= acc)
                {
                    DebugLogger.VerboseLog("RNG", $"WeightedChoiceIndex: roll={roll}, chosen={i}");
                    return i;
                }
            }
            DebugLogger.VerboseLog("RNG", $"WeightedChoiceIndex fallback, returning last index {weights.Count - 1}");
            return weights.Count - 1;
        }

        public static float Range(float min, float max)
        {
            var val = (float)(rng.NextDouble() * (max - min) + min);
            DebugLogger.VerboseLog("RNG", $"Range({min},{max}) => {val}");
            return val;
        }

        public static float TruncatedNormal(float mean, float sd, float minVal, float maxVal)
        {
            if (sd <= 0f)
            {
                DebugLogger.VerboseLog("RNG", $"TruncatedNormal sd<=0 returning clamped mean {mean}");
                return Mathf.Clamp(mean, minVal, maxVal);
            }

            for (int i = 0; i < 10; i++)
            {
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                double sample = mean + sd * randStdNormal;
                if (sample >= minVal && sample <= maxVal)
                {
                    DebugLogger.VerboseLog("RNG", $"TruncatedNormal sample {sample} in range [{minVal},{maxVal}]");
                    return (float)sample;
                }
            }
            DebugLogger.VerboseLog("RNG", $"TruncatedNormal fallback clamped mean {mean}");
            return Mathf.Clamp(mean, minVal, maxVal);
        }
    }
}