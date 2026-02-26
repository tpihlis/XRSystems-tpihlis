using System;
using UnityEngine;

namespace FishingSystem
{
    public class LureController : MonoBehaviour
    {
        public enum LureState { Idle, InWater, Pending, Hooked, Reeling }
        public LureState state = LureState.Idle;

        public event Action<GameObject> OnPendingSpawn;
        public event Action<GameObject> OnFishHooked;
        public event Action<GameObject> OnFishReleased;

        FixedJoint attachJoint;
        public bool hooked => attachJoint != null;

        public CatchManager catchManager;
        public PlayerStats playerStats;
        public RodSO equippedRod;

        [Header("Optional auto wiring")]
        public FishingLine fishingLine;

        public void HookOnto(Rigidbody other)
        {
            if (other == null) { DebugLogger.VerboseLog("LureController", "HookOnto called with null Rigidbody"); return; }
            if (hooked) { DebugLogger.VerboseLog("LureController", "HookOnto called but already hooked"); return; }

            attachJoint = gameObject.AddComponent<FixedJoint>();
            attachJoint.connectedBody = other;
            DebugLogger.Log("LureController", $"HookOnto: attached to {other.gameObject.name}");
        }

        public void ReleaseHook()
        {
            if (!hooked) { DebugLogger.VerboseLog("LureController", "ReleaseHook called but not hooked"); return; }
            if (attachJoint != null) { Destroy(attachJoint); attachJoint = null; }
            DebugLogger.Log("LureController", "ReleaseHook: hook released");
        }

        public void NotifyPending(GameObject fish)
        {
            state = LureState.Pending;
            DebugLogger.Log("LureController", $"NotifyPending {fish?.name}");
            OnPendingSpawn?.Invoke(fish);
        }

        public void NotifyHooked(GameObject fish)
        {
            state = LureState.Hooked;
            DebugLogger.Log("LureController", $"NotifyHooked {fish?.name}");

            Rigidbody rb = null;
            if (fish != null)
            {
                rb = fish.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    HookOnto(rb);

                    if (fishingLine != null)
                    {
                        fishingLine.lureRb = rb;
                        DebugLogger.Log("LureController", $"Assigned lureRb to FishingLine for {fish.name}");
                    }
                }
            }

            OnFishHooked?.Invoke(fish);

            if (catchManager != null && fish != null)
            {
                var fi = fish.GetComponent<FishInstance>();
                if (fi != null && fi.data != null)
                {
                    StartCoroutine(catchManager.ResolveFight(
                        rb,
                        fi.data,
                        playerStats,
                        equippedRod,
                        success =>
                        {
                            if (success)
                            {
                                DebugLogger.Log("LureController", $"Fish landed: {fish.name}");
                            }
                            else
                            {
                                DebugLogger.Log("LureController", $"Fish escaped: {fish.name}");
                                ReleaseHook();
                                fi.ReturnToPool();
                                OnFishReleased?.Invoke(fish);
                            }
                        }));
                }
            }
        }

        public void NotifyReleased(GameObject fish)
        {
            state = LureState.InWater;
            DebugLogger.Log("LureController", $"NotifyReleased {fish?.name}");
            ReleaseHook();
            OnFishReleased?.Invoke(fish);
        }

        public void EnterWater()
        {
            state = LureState.InWater;
            DebugLogger.VerboseLog("LureController", "EnterWater");
        }

        public void ExitWater()
        {
            state = LureState.Idle;
            DebugLogger.VerboseLog("LureController", "ExitWater");
        }
    }
}