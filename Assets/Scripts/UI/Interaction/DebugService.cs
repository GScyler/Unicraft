using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class DebugService
    {
        private static DebugService _instance;
        public static DebugService Instance => _instance ??= new DebugService();

        public bool enabled = true;
        public bool verboseLog = false;

        private readonly List<DebugEntry> _entries = new List<DebugEntry>();
        private const int MAX_ENTRIES = 100;

        private float _lastLogTime = 0f;

        public void Log(string message, string category = "General")
        {
            if (!enabled) return;
            _entries.Add(new DebugEntry(Time.time, category, message));
            if (_entries.Count > MAX_ENTRIES) _entries.RemoveAt(0);
            Debug.Log($"[MT-Debug][{category}] {message}");
        }

        public void LogInput(MouseButton button, string action, int slotIdx, bool shift = false)
        {
            if (!enabled) return;
            string info = $"Button={button}, Slot={slotIdx}, Shift={shift}";
            Log($"{action}: {info}", "Input");
        }

        public void LogHandler(string handlerName, string action, string details = "")
        {
            if (!enabled) return;
            string msg = string.IsNullOrEmpty(details) ? action : $"{action} | {details}";
            Log(msg, handlerName);
        }

        public void LogState(string handlerName, DragState state, string extra = "")
        {
            string info = string.IsNullOrEmpty(extra) ? $"State={state}" : $"State={state} | {extra}";
            Log(info, handlerName);
        }

        public void LogCursor(string action, ushort itemID, byte amount)
        {
            if (!enabled || !verboseLog) return;
            Log($"{action}: ItemID={itemID}, Amount={amount}", "Cursor");
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public string GetRecentLog(int count = 20)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Recent Debug Log ===");
            int start = Mathf.Max(0, _entries.Count - count);
            for (int i = start; i < _entries.Count; i++)
            {
                var e = _entries[i];
                sb.AppendLine($"[{e.Time:F3}][{e.Category}] {e.Message}");
            }
            return sb.ToString();
        }

        private struct DebugEntry
        {
            public float Time;
            public string Category;
            public string Message;

            public DebugEntry(float time, string category, string message)
            {
                Time = time;
                Category = category;
                Message = message;
            }
        }
    }
}