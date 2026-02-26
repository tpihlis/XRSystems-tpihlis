using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using System.Reflection;

namespace FishingSystem
{
    /// <summary>
    /// LevelHUD: world-space UI that displays:
    /// - current level number
    /// - current price goal
    /// - per-socket visuals showing fish stats (size, quality, rarity, price)
    ///
    /// Hookup:
    /// - assign LevelManager
    /// - assign either sellSockets list (XRSocketInteractor) OR socketsParent + autoPopulateFromParent
    /// - assign slotPrefab (a GameObject that contains SocketSlotUI) and slotsParent (RectTransform)
    /// </summary>
    public class LevelHUD : MonoBehaviour
    {
        [Header("References")]
        public LevelManager levelManager;

        [Header("Top-line UI")]
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI goalText;

        [Header("Sockets (either assign list OR set parent & auto-populate)")]
        public List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor> sellSockets = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        public Transform socketsParent; // optional: parent under which XRSocketInteractor GameObjects live
        public bool autoPopulateFromParent = true;

        [Header("Socket UI prefab")]
        [Tooltip("Prefab that contains a SocketSlotUI component (single slot).")]
        public GameObject slotUIPrefab;
        public Transform slotsParent; // where slot UI instances will be created

        [Header("Update")]
        [Tooltip("HUD refresh interval (seconds)")]
        public float updateInterval = 0.15f;

        // runtime
        List<SocketSlotUI> slotUIs = new List<SocketSlotUI>();
        float updateTimer = 0f;

        void Start()
        {
            TryAutoPopulateSockets();
            BuildSlotUI();
            RefreshAll();
            if (levelManager != null)
            {
                levelManager.onRoundStarted?.AddListener(RefreshAll);
                levelManager.onRoundEnded?.AddListener(RefreshAll);
            }
        }

        void Update()
        {
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                RefreshAll();
            }
        }

        void TryAutoPopulateSockets()
        {
            if (!autoPopulateFromParent) return;
            if (socketsParent == null) return;
            if (sellSockets != null && sellSockets.Count > 0) return;

            var found = socketsParent.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(includeInactive: true);
            sellSockets = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(found);
            Debug.Log($"[LevelHUD] Auto-populated {sellSockets.Count} sockets from {socketsParent.name}");
        }

        void BuildSlotUI()
        {
            // destroy old
            foreach (var s in slotUIs)
                if (s != null) Destroy(s.gameObject);
            slotUIs.Clear();

            if (slotUIPrefab == null || slotsParent == null)
            {
                Debug.LogWarning("[LevelHUD] slotUIPrefab or slotsParent not assigned. Cannot build socket UI.");
                return;
            }

            int count = Math.Max( sellSockets != null ? sellSockets.Count : 0, 0);
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(slotUIPrefab, slotsParent, false);
                var ui = go.GetComponent<SocketSlotUI>();
                if (ui == null)
                {
                    Debug.LogWarning("[LevelHUD] slotUIPrefab missing SocketSlotUI component.");
                    Destroy(go);
                    continue;
                }
                slotUIs.Add(ui);
            }
        }

        /// <summary>
        /// Update all UI elements (level, goal and each socket).
        /// </summary>
        public void RefreshAll()
        {
            if (levelManager != null)
            {
                if (levelText != null) levelText.text = $"Level {levelManager.currentLevel}";
                if (goalText != null) goalText.text = $"Goal: {levelManager.GetCurrentGoal():F2}€";
            }

            // update each socket UI
            for (int i = 0; i < slotUIs.Count; i++)
            {
                FishingSystem.FishData data = null;
                if (i < sellSockets.Count && sellSockets[i] != null)
                {
                    var selected = GetSelectedInteractableFromSocket(sellSockets[i]);
                    // convert selected interactable to Component to read FishInstance
                    Component comp = selected as Component;
                    if (comp == null && selected != null)
                    {
                        var tprop = selected.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (tprop != null)
                        {
                            var t = tprop.GetValue(selected, null) as Transform;
                            if (t != null) comp = t as Component;
                        }
                    }
                    if (comp != null)
                    {
                        var fi = comp.GetComponent<FishInstance>();
                        if (fi != null)
                        {
                            data = fi.data;
                        }
                    }
                }

                slotUIs[i].SetFishData(i, data);
            }
        }

        /// <summary>
        /// Reflection helper (same tolerant approach as SellManager).
        /// Attempts common properties/methods in XRSocketInteractor to get the selected interactable.
        /// </summary>
        private object GetSelectedInteractableFromSocket(UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket)
        {
            if (socket == null) return null;
            object result = null;
            Type t = socket.GetType();

            try
            {
                MethodInfo mi = t.GetMethod("GetOldestInteractableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    result = mi.Invoke(socket, null);
                    if (result != null) return result;
                }

                mi = t.GetMethod("GetOldestSelectedInteractable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    result = mi.Invoke(socket, null);
                    if (result != null) return result;
                }

                PropertyInfo pi = t.GetProperty("firstInteractableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    result = pi.GetValue(socket, null);
                    if (result != null) return result;
                }

                pi = t.GetProperty("interactableSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    result = pi.GetValue(socket, null);
                    if (result != null) return result;
                }

                FieldInfo fi = t.GetField("m_InteractablesSelected", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    var val = fi.GetValue(socket);
                    if (val is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var entry in enumerable)
                        {
                            if (entry == null) continue;
                            var p = entry.GetType().GetProperty("interactable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (p != null)
                            {
                                var maybe = p.GetValue(entry, null);
                                if (maybe != null) return maybe;
                            }
                            return entry;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelHUD] Reflection failed: {ex.Message}");
            }

            return null;
        }
    }
}