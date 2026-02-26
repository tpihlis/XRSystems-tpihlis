using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FishingSystem
{
    /// <summary>
    /// LevelManager - simplified and deterministic:
    /// - level goals are a pure function of the level number when `alwaysUseProcedural` is true
    /// - supports batch-selling, single-sell-per-round, basic-gear-on-fail
    /// - returns/refills fish pools between rounds
    /// - optionally manages the shop GameObject visibility (configurable)
    /// 
    /// Modifications:
    /// - on success: transfer roundMoney into playerStats.money (AddMoney)
    /// - on fail: reset player stats and money (ResetStatsAndMoney) and equip starting gear
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Progression")]
        public int currentLevel = 1;

        [Tooltip("If true, always compute goals using the procedural formula (recommended to avoid dips).")]
        public bool alwaysUseProcedural = true;

        [Tooltip("Optional explicit level goals. Used only if alwaysUseProcedural = false.")]
        public List<float> levelPriceGoals = new List<float> { 2f, 5f, 10f, 20f };

        [Header("Procedural goal formula (used when alwaysUseProcedural=true or fallback)")]
        [Tooltip("Base goal for level 1")]
        public float proceduralBaseGoal = 2f;
        [Tooltip("Exponential multiplier per level (e.g. 1.25 => +25% per level).")]
        public float proceduralGrowthFactor = 1.25f;
        [Tooltip("Linear additive per level (small).")]
        public float proceduralLinearAdd = 0.5f;

        [Header("Round rules")]
        public int maxSellsPerRound = 5;

        [Header("Runtime (read-only)")]
        [SerializeField] private float roundMoney = 0f;
        [SerializeField] private int sellsThisRound = 0;
        [SerializeField] private bool roundActive = false;

        [Header("References")]
        public PlayerStats playerStats;

        [Tooltip("Shop GameObject (optional). If assigned and manageShopVisibility=true, LevelManager will hide/show it between rounds.")]
        public GameObject shopGameObject;

        [Header("Events")]
        public UnityEvent onRoundStarted;
        public UnityEvent onRoundEnded;
        public UnityEvent onRoundWon;
        public UnityEvent onRoundFailed;

        [Header("Behaviour toggles")]
        public bool returnAllFishOnRoundEnd = true;
        public bool refillPoolsOnRoundEnd = true;
        public bool autoStartNextRound = true;
        public bool equipStartingGearAtRoundStart = true;

        [Header("Fail / restart gear")]
        public RodSO startingRod;
        public LureSO startingLure;

        [Header("Sell action rules")]
        [Tooltip("If true, player is allowed only one SellAll() action per round.")]
        public bool oneSellAttemptPerRound = true;

        [Header("Shop visibility control")]
        [Tooltip("If true, LevelManager will SetActive(false/true) on shopGameObject at round start/end. If false, it will not touch the shop GameObject.")]
        public bool manageShopVisibility = true;

        // internal flags
        bool inBatchSell = false;
        bool sellActionUsedThisRound = false;
        bool lastRoundWasWinInternal = false;

        // Simple read-only accessors
        public float RoundMoney => roundMoney;
        public int SellsThisRound => sellsThisRound;
        public bool RoundActive => roundActive;
        public int RemainingSells => Mathf.Max(0, maxSellsPerRound - sellsThisRound);
        public bool lastRoundWasWin => lastRoundWasWinInternal;

        void Start()
        {
            StartRound();
        }

        // --- Round lifecycle ---

        public void StartRound()
        {
            roundMoney = 0f;
            sellsThisRound = 0;
            roundActive = true;
            sellActionUsedThisRound = false;
            lastRoundWasWinInternal = false;

            // Equip starter gear only if player has none (keeps player's stats)
            if (equipStartingGearAtRoundStart && playerStats != null)
            {
                if (playerStats.equippedRod == null && startingRod != null)
                    playerStats.equippedRod = startingRod;
                if (playerStats.equippedLure == null && startingLure != null)
                    playerStats.equippedLure = startingLure;
            }

            if (manageShopVisibility && shopGameObject != null)
                shopGameObject.SetActive(false);

            DebugLogger.Log("LevelManager", $"Starting level {currentLevel}. Goal={GetCurrentGoal():F2}€");
            onRoundStarted?.Invoke();
        }

        /// <summary>
        /// Pure goal computation (deterministic). Use procedural when alwaysUseProcedural==true,
        /// otherwise prefer the explicit list and fall back to procedural when list is exhausted.
        /// </summary>
        public float GetCurrentGoal()
        {
            if (!alwaysUseProcedural && levelPriceGoals != null && levelPriceGoals.Count > 0)
            {
                int idx = currentLevel - 1;
                if (idx >= 0 && idx < levelPriceGoals.Count)
                    return Mathf.Round(levelPriceGoals[idx] * 100f) / 100f;
                // if beyond list, fall through to procedural
            }

            // Procedural formula (stateless calculation based on level number)
            int levelIndex = Mathf.Max(1, currentLevel);
            float expPart = proceduralBaseGoal * Mathf.Pow(proceduralGrowthFactor, levelIndex - 1);
            float linPart = proceduralLinearAdd * (levelIndex - 1);
            float goal = expPart + linPart;
            return Mathf.Round(goal * 100f) / 100f;
        }

        // --- Batch sell helpers (used by SellManager) ---
        public void BeginBatchSell() => inBatchSell = true;

        public void EndBatchSell(bool evaluate = true)
        {
            inBatchSell = false;
            if (evaluate) CheckRoundEndConditions();
        }

        // Mark the one-time sell action as used
        public void MarkSellActionUsed() => sellActionUsedThisRound = true;
        public bool SellActionUsedThisRound => sellActionUsedThisRound;

        // --- Register a sale of a fish priced at priceEuros ---
        public void RegisterFishSale(float priceEuros)
        {
            if (!roundActive)
            {
                DebugLogger.Log("LevelManager", "Tried to register sale while round inactive.");
                return;
            }

            sellsThisRound++;
            roundMoney += priceEuros;
            DebugLogger.Log("LevelManager", $"Fish sold: {priceEuros:F2}€. Round total={roundMoney:F2}€. Sells {sellsThisRound}/{maxSellsPerRound}");

            if (!inBatchSell)
                CheckRoundEndConditions();
        }

        // --- Evaluate end conditions ---
        void CheckRoundEndConditions()
        {
            if (roundMoney >= GetCurrentGoal())
            {
                EndRound(true);
                return;
            }

            if (sellsThisRound >= maxSellsPerRound)
            {
                EndRound(false);
            }
        }

        void EndRound(bool success)
        {
            roundActive = false;
            lastRoundWasWinInternal = success;

            DebugLogger.Log("LevelManager", $"Round ended. Success={success}. Total={roundMoney:F2}€ Goal={GetCurrentGoal():F2}€");

            if (success)
            {
                // award money to player
                if (playerStats != null && roundMoney > 0f)
                {
                    playerStats.AddMoney(roundMoney);
                }

                // advance level for next round
                currentLevel++;
            }
            else
            {
                // restart run: go back to level 1 and reset player's stats+money
                currentLevel = 1;

                if (playerStats != null)
                {
                    playerStats.ResetStatsAndMoney();
                }

                // equip basic gear on fail (keeps owned lists)
                if (playerStats != null)
                {
                    playerStats.equippedRod = startingRod;
                    playerStats.equippedLure = startingLure;
                }
            }

            // cleanup world
            if (returnAllFishOnRoundEnd) ReturnAllFishToPools();
            if (refillPoolsOnRoundEnd) RefillAllPools();

            onRoundEnded?.Invoke();
            if (success) onRoundWon?.Invoke(); else onRoundFailed?.Invoke();

            // auto-start or show shop (if managed)
            if (autoStartNextRound)
                StartRound();
            else if (manageShopVisibility && shopGameObject != null)
                shopGameObject.SetActive(true);

            // reset roundMoney for next round (safest)
            roundMoney = 0f;
        }

        // --- Pool helpers ---

        public void ReturnAllFishToPools()
        {
    #if UNITY_2023_2_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<FishInstance>(UnityEngine.FindObjectsSortMode.None);
    #else
            var all = FindObjectsOfType<FishInstance>(true);
    #endif
            DebugLogger.Log("LevelManager", $"Returning {all.Length} FishInstance(s) to pools.");
            foreach (var fi in all)
            {
                if (fi == null) continue;
                try { fi.ReturnToPool(); }
                catch (Exception ex) { DebugLogger.Log("LevelManager", $"ReturnToPool failed for {fi.gameObject.name}: {ex.Message}"); }
            }
        }

        public void RefillAllPools()
        {
    #if UNITY_2023_2_OR_NEWER
            var pools = UnityEngine.Object.FindObjectsByType<FishPool>(UnityEngine.FindObjectsSortMode.None);
    #else
            var pools = FindObjectsOfType<FishPool>(true);
    #endif
            DebugLogger.Log("LevelManager", $"Refilling {pools.Length} FishPool(s).");
            foreach (var p in pools)
            {
                try { p.RefillPool(); }
                catch (Exception ex) { DebugLogger.Log("LevelManager", $"RefillPool failed for {p.gameObject.name}: {ex.Message}"); }
            }
        }

        // --- Utility ---
        public string GetRoundSummary() => $"Level {currentLevel} — {roundMoney:F2}€ / {GetCurrentGoal():F2}€ — Sells {sellsThisRound}/{maxSellsPerRound}";
    }
}