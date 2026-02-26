using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace FishingSystem
{
    /// <summary>
    /// Manage a group of XRSocketInteractors (e.g. 5 sockets) that act as the player's sell slots.
    /// Player places fish into sockets physically, then presses a Sell button (or calls SellAll).
    ///
    /// This version enforces one-sell-per-round if LevelManager.oneSellAttemptPerRound is true.
    /// It also caches the sale summary before ending the batch so logs won't show data reset by StartRound().
    /// </summary>
    public class SellManager : MonoBehaviour
    {
        [Header("Sockets (drag XRSocketInteractor GameObjects)")]
        [Tooltip("Assign the socket interactors representing the physical sell slots.")]
        public List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor> sellSockets = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();

        [Header("References")]
        public LevelManager levelManager;

        [Header("Options")]
        [Tooltip("If true, selling will also remove the item from the socket via FishInstance.ReturnToPool().")]
        public bool returnToPoolOnSell = true;

        [Header("Empty-socket behaviour (tunable)")]
        [Tooltip("If true, each empty socket encountered during SellAll() consumes one sell attempt (registers a 0€ sale).")]
        public bool emptySocketConsumesSell = false;

        [Tooltip("If true and SellAll() finds no fish at all, a single Sell press consumes one sell attempt (registers a 0€ sale).")]
        public bool emptyPressConsumesOne = false;

        [Header("Inspector helpers (optional)")]
        [Tooltip("Parent transform containing socket GameObjects (auto-populate helper).")]
        public Transform socketsParent;
        [Tooltip("If true, auto-populate sellSockets from socketsParent at Start (only if the list is currently empty).")]
        public bool autoPopulateFromParent = true;
        [Tooltip("If true, show debug logs about socket contents.")]
        public bool verboseDebug = false;

        void Start()
        {
            TryAutoPopulateSockets();
        }

        private void TryAutoPopulateSockets()
        {
            if (!autoPopulateFromParent) return;
            if (socketsParent == null) return;
            if (sellSockets != null && sellSockets.Count > 0) return;

            var found = socketsParent.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(includeInactive: true);
            sellSockets = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(found);
            if (sellSockets.Count == 0)
            {
                Debug.LogWarning("[SellManager] Auto-populate: found 0 XRSocketInteractors under socketsParent.");
            }
            else
            {
                Debug.Log($"[SellManager] Auto-populated {sellSockets.Count} sockets from {socketsParent.name}");
            }
        }

        /// <summary>
        /// Call this from a world button / input action when player presses the Sell button.
        /// This performs an atomic batch sell: it sells up to RemainingSells items present in sockets,
        /// then invokes LevelManager checks once at the end.
        /// </summary>
        public void SellAll()
        {
            if (levelManager == null)
            {
                Debug.LogError("[SellManager] No LevelManager assigned.");
                return;
            }

            if (!levelManager.RoundActive)
            {
                Debug.Log("[SellManager] Round is not active — cannot sell now.");
                return;
            }

            // Enforce one-sell-per-round if configured
            if (levelManager.oneSellAttemptPerRound && levelManager.SellActionUsedThisRound)
            {
                Debug.Log("[SellManager] Sell action already used this round — cannot sell again.");
                return;
            }

            if (levelManager.RemainingSells <= 0)
            {
                Debug.Log("[SellManager] No sells remaining for this round.");
                return;
            }

            // Mark that the player used their sell attempt (one-time-per-round semantics)
            if (levelManager.oneSellAttemptPerRound)
                levelManager.MarkSellActionUsed();

            // Snapshot before selling begins (useful for computing how many this SellAll sells relative to prior state)
            int sellsBefore = levelManager.SellsThisRound;
            float moneyBefore = levelManager.RoundMoney;

            // Begin batch mode so LevelManager doesn't EndRound mid-loop
            levelManager.BeginBatchSell();

            bool anyFishFound = false;

            // iterate sockets in order; attempt to sell until limit reached
            for (int i = 0; i < sellSockets.Count && levelManager.RemainingSells > 0; i++)
            {
                var socket = sellSockets[i];
                if (socket == null) continue;

                var selectedObj = GetSelectedInteractableFromSocket(socket);
                if (selectedObj == null)
                {
                    if (verboseDebug) Debug.Log($"[SellManager] Socket {i} has no selected interactable.");

                    // Optionally count empty socket as a consumed sell attempt (0€)
                    if (emptySocketConsumesSell && levelManager.RemainingSells > 0)
                    {
                        levelManager.RegisterFishSale(0f);
                        if (verboseDebug) Debug.Log($"[SellManager] Empty socket {i} consumed one sell attempt (0€). RemainingSells={levelManager.RemainingSells}");
                    }

                    continue;
                }

                anyFishFound = true;
                if (verboseDebug) Debug.Log($"[SellManager] Socket {i} selected object type = {selectedObj.GetType().FullName}");

                // try to treat selectedObj as a Component (most XR interactables are Components/MonoBehaviours)
                Component comp = selectedObj as Component;

                // fallback: if not a Component, try to fetch a 'transform' property (some versions return interface objs)
                if (comp == null)
                {
                    var tprop = selectedObj.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (tprop != null)
                    {
                        var t = tprop.GetValue(selectedObj, null) as Transform;
                        if (t != null) comp = t as Component;
                    }
                }

                if (comp == null)
                {
                    Debug.Log($"[SellManager] Socket {i} selected object not a Component; ignoring.");
                    continue;
                }

                var fishInstance = comp.GetComponent<FishInstance>();
                if (fishInstance == null)
                {
                    Debug.Log($"[SellManager] Socket {i} has non-fish object {comp.name}, ignoring.");
                    continue;
                }

                // Sell it (LevelManager is in batch mode so CheckRoundEnd is deferred)
                float price = fishInstance.data != null ? fishInstance.data.priceEuros : 0f;
                levelManager.RegisterFishSale(price);

                // Return to pool (this will also attempt to cancel the socket selection via FishInstance code)
                if (returnToPoolOnSell)
                {
                    fishInstance.ReturnToPool();
                }
            }

            // If no fish found at all, optionally consume a single attempt
            if (!anyFishFound && emptyPressConsumesOne && levelManager.RemainingSells > 0)
            {
                levelManager.RegisterFishSale(0f);
                if (verboseDebug) Debug.Log($"[SellManager] Empty press consumed one sell attempt (0€). RemainingSells={levelManager.RemainingSells}");
            }

            // CACHE the post-sell numbers BEFORE we call EndBatchSell (which may trigger StartRound and reset them)
            int sellsAfterCache = levelManager.SellsThisRound;
            float roundMoneyAfterCache = levelManager.RoundMoney;
            int soldThisAction = Mathf.Max(0, sellsAfterCache - sellsBefore);

            // End batch and evaluate win/fail once (this may reset round state if autoStartNextRound = true)
            levelManager.EndBatchSell(evaluate: true);

            // Now log using cached values (accurate even if LevelManager started next round)
            Debug.Log($"[SellManager] SellAll finished. Sold this action={soldThisAction}/{levelManager.maxSellsPerRound}  — Round total (at end)={roundMoneyAfterCache:F2}€");

            // NOTE: levelManager.SellsThisRound may now be zero if a new round started; we intentionally used the cached values above.
        }

        // Utility: sell single socket by index (public) - still uses batch semantics for consistency
        public void SellSocketByIndex(int index)
        {
            if (index < 0 || index >= sellSockets.Count) return;
            var socket = sellSockets[index];
            if (socket == null) return;

            var selectedObj = GetSelectedInteractableFromSocket(socket);
            if (selectedObj == null) return;

            Component comp = selectedObj as Component;
            if (comp == null)
            {
                var tprop = selectedObj.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tprop != null)
                {
                    var t = tprop.GetValue(selectedObj, null) as Transform;
                    if (t != null) comp = t as Component;
                }
            }
            if (comp == null) return;

            var fishInstance = comp.GetComponent<FishInstance>();
            if (fishInstance == null) return;

            if (levelManager == null)
            {
                Debug.LogError("[SellManager] No LevelManager assigned.");
                return;
            }

            if (!levelManager.RoundActive)
            {
                Debug.Log("[SellManager] Round not active - cannot sell.");
                return;
            }

            if (levelManager.RemainingSells <= 0)
            {
                Debug.Log("[SellManager] No sells remaining.");
                return;
            }

            // Begin batch for single-socket sell to keep logic uniform
            if (levelManager.oneSellAttemptPerRound && levelManager.SellActionUsedThisRound)
            {
                Debug.Log("[SellManager] Sell action already used this round — cannot sell again.");
                return;
            }

            if (levelManager.oneSellAttemptPerRound)
                levelManager.MarkSellActionUsed();

            // Snapshot before selling
            int sellsBefore = levelManager.SellsThisRound;

            levelManager.BeginBatchSell();
            float price = fishInstance.data != null ? fishInstance.data.priceEuros : 0f;
            levelManager.RegisterFishSale(price);
            if (returnToPoolOnSell) fishInstance.ReturnToPool();

            // Cache results before EndBatchSell
            int sellsAfterCache = levelManager.SellsThisRound;
            float roundMoneyAfterCache = levelManager.RoundMoney;
            int soldThisAction = Mathf.Max(0, sellsAfterCache - sellsBefore);

            levelManager.EndBatchSell(evaluate: true);

            Debug.Log($"[SellManager] Sold socket {index} for {price:F2}€. Sold this action={soldThisAction}/{levelManager.maxSellsPerRound} — Round total (at end)={roundMoneyAfterCache:F2}€");
        }

        /// <summary>
        /// Try several common APIs via reflection to obtain the selected interactable from the XRSocketInteractor.
        /// Returns null if nothing is selected or if reflection fails.
        /// </summary>
        private object GetSelectedInteractableFromSocket(UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket)
        {
            if (socket == null) return null;

            object result = null;
            Type t = socket.GetType();

            try
            {
                // try method: GetOldestInteractableSelected()
                MethodInfo mi = t.GetMethod("GetOldestInteractableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    result = mi.Invoke(socket, null);
                    if (result != null) return result;
                }

                // try method: GetOldestSelectedInteractable() (alternate name in some versions)
                mi = t.GetMethod("GetOldestSelectedInteractable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    result = mi.Invoke(socket, null);
                    if (result != null) return result;
                }

                // try property: firstInteractableSelected
                PropertyInfo pi = t.GetProperty("firstInteractableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    result = pi.GetValue(socket, null);
                    if (result != null) return result;
                }

                // try property: interactableSelected (less common)
                pi = t.GetProperty("interactableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    result = pi.GetValue(socket, null);
                    if (result != null) return result;
                }

                // As a last resort, inspect the socket's m_InteractablesSelected field
                FieldInfo fi = t.GetField("m_InteractablesSelected", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    var val = fi.GetValue(socket);
                    if (val is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var entry in enumerable)
                        {
                            if (entry == null) continue;
                            // try property 'interactable' on the entry
                            var p = entry.GetType().GetProperty("interactable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (p != null)
                            {
                                var maybe = p.GetValue(entry, null);
                                if (maybe != null) return maybe;
                            }
                            // fallback: return the entry itself
                            return entry;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SellManager] Reflection attempt failed: {ex.Message}");
            }

            return null;
        }
    }
}