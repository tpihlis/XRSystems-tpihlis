// File: DebugHUD.cs
using UnityEngine;

namespace FishingSystem
{
    public class DebugHUD : MonoBehaviour
    {
        public bool show = true;
        public int logLines = 12;
        public Rect windowRect = new Rect(10, 10, 600, 300);

        void OnGUI()
        {
            if (!show) return;
            GUILayout.BeginArea(windowRect, GUI.skin.box);
            GUILayout.Label("Fishing System Debug Logs (toggle DebugLogger.EnableLogs / Verbose)");
            var logs = DebugLogger.GetRecentLogs(logLines);
            foreach (var l in logs)
            {
                if (string.IsNullOrEmpty(l)) continue;
                GUILayout.Label(l);
            }
            GUILayout.EndArea();
        }
    }
}