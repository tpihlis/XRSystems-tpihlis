// File: DebugToggle.cs
using UnityEngine;

namespace FishingSystem
{
    public class DebugToggle : MonoBehaviour
    {
        public bool enableLogs = true;
        public bool verboseLogs = true;

        void Start()
        {
            Apply();
        }

        [ContextMenu("Apply Debug Settings")]
        public void Apply()
        {
            DebugLogger.EnableLogs = enableLogs;
            DebugLogger.Verbose = verboseLogs;
            DebugLogger.Log("DebugToggle", $"Applied debug settings: Enable={enableLogs} Verbose={verboseLogs}");
        }

        [ContextMenu("Enable All Logs")]
        public void EnableAll()
        {
            enableLogs = true;
            verboseLogs = true;
            Apply();
        }

        [ContextMenu("Disable All Logs")]
        public void DisableAll()
        {
            enableLogs = false;
            verboseLogs = false;
            Apply();
        }
    }
}