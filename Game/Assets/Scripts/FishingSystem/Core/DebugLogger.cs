// File: Assets/Scripts/FishingSystem/Core/DebugLogger.cs
using System;
using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Lightweight centralized logger used by the fishing system.
    /// Put this in a runtime folder (NOT in Assets/Editor).
    /// </summary>
    public static class DebugLogger
    {
        public static bool EnableLogs = true; // global toggle
        public static bool Verbose = false;   // verbose toggle

        // small circular buffer for in-game HUD
        static readonly int BufferSize = 200;
        static readonly string[] buffer = new string[BufferSize];
        static int writeIndex = 0;

        static string Timestamp() => DateTime.Now.ToString("HH:mm:ss.fff");

        public static void Log(string tag, string message)
        {
            if (!EnableLogs) return;
            string msg = $"[{Timestamp()}] [{tag}] {message}";
            UnityEngine.Debug.Log(msg);
            buffer[writeIndex] = msg;
            writeIndex = (writeIndex + 1) % BufferSize;
        }

        public static void VerboseLog(string tag, string message)
        {
            if (!EnableLogs || !Verbose) return;
            Log(tag, message);
        }

        // Return the last `count` logs (most recent first).
        public static string[] GetRecentLogs(int count)
        {
            count = Mathf.Clamp(count, 1, BufferSize);
            var outArr = new string[count];
            int idx = writeIndex - 1;
            if (idx < 0) idx += BufferSize;
            for (int i = 0; i < count; i++)
            {
                outArr[i] = buffer[idx];
                idx--;
                if (idx < 0) idx += BufferSize;
            }
            return outArr;
        }
    }
}