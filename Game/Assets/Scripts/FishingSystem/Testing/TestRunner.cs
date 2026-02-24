// File: Assets/Scripts/FishingSystem/Testing/TestRunner.cs
using System;
using UnityEngine;

namespace FishingSystem
{
    public class TestRunner : MonoBehaviour
    {
        [Header("References (assign in Inspector)")]
        public FishFactory factory;
        public FishSpeciesSO testSpecies;
        public PlayerStats player;
        public SpawnManager spawnManager;
        public SocketInteractionHandler socketHandler;
        public LureController lureController;

        [ContextMenu("Seed RNG with 12345")]
        public void Seed()
        {
            RNGService.Seed(12345);
            DebugLogger.Log("TestRunner", "Seeded RNG with 12345");
        }

        [ContextMenu("Spawn Pending Quick")]
        public void SpawnPendingQuick()
        {
            if (factory == null || testSpecies == null)
            {
                DebugLogger.Log("TestRunner", "Missing factory or testSpecies");
                return;
            }

            var go = factory.SpawnPendingFish(testSpecies, player?.equippedLure, player,
                (spawnManager != null && spawnManager.fishSocketTransform != null) ? spawnManager.fishSocketTransform : transform);

            if (go == null)
            {
                DebugLogger.Log("TestRunner", "Spawn failed");
                return;
            }

            // Register pending with socket handler (optional)
            if (socketHandler != null) socketHandler.SetPending(go, 5f);

            DebugLogger.Log("TestRunner", $"Spawned pending: {go.name}");
        }

        [ContextMenu("Spawn & Hook (simulate socket accept)")]
        public void SpawnAndHook()
        {
            if (factory == null || testSpecies == null)
            {
                DebugLogger.Log("TestRunner", "Missing factory or testSpecies");
                return;
            }

            var go = factory.SpawnPendingFish(testSpecies, player?.equippedLure, player,
                (spawnManager != null && spawnManager.fishSocketTransform != null) ? spawnManager.fishSocketTransform : transform);

            if (go == null)
            {
                DebugLogger.Log("TestRunner", "Spawn failed");
                return;
            }

            // register pending with the handler (keeps the handler aware and its timeout running)
            if (socketHandler != null) socketHandler.SetPending(go, 5f);

            // Let the socket handler attempt to accept (it will try StartManualInteraction and fall back).
            if (socketHandler != null)
            {
                try
                {
                    socketHandler.AcceptPendingManually();
                    DebugLogger.Log("TestRunner", $"Socket AcceptPendingManually called for {go.name}");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("TestRunner", "AcceptPendingManually failed: " + ex.Message);
                }
            }
            else
            {
                DebugLogger.Log("TestRunner", "No socketHandler assigned — leaving pending");
            }

            // Optionally notify the lure controller (not strictly required if socketHandler caused OnSelectEntered)
            if (lureController != null)
            {
                try { lureController.NotifyHooked(go); } catch { }
            }

            DebugLogger.Log("TestRunner", $"Spawned & hooked (simulated): {go.name}");
        }
    }
}