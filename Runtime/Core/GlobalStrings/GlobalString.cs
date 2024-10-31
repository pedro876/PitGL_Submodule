using System;
using System.Collections.Generic;
using UnityEngine;

namespace PitGL
{
    /// <summary>
    /// This is a way to connect messages across different systems without connecting them directly.
    /// Each time a text is changed for a given key, all listeners will update correspondingly.
    /// </summary>
    public static class GlobalString
    {
        private static Dictionary<string, Entry> entries = new();
        public static void Set(string key, string value)
        {
            if(!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry(key);
                entries[key] = entry;
            }

            entry.value = value;
            entry.callbacks?.Invoke(value);
        }

        public static string Get(string key)
        {
            if(!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry(key);
                entries[key] = entry;
            }

            return entry.value;
        }

        public static void AddListener(string key, Action<string> callback)
        {
            if(!entries.TryGetValue(key,out Entry entry))
            {
                entry = new Entry(key);
                entries[key] = entry;
            }

            entry.callbacks += callback;
            callback(entry.value);
        }

        public static void RemoveListener(string key, Action<string> callback)
        {
            if (!entries.TryGetValue(key, out Entry entry))
            {
                return;
            }

            entry.callbacks -= callback;
        }

        private class Entry
        {
            public string key;
            public string value;
            public Action<string> callbacks;

            public Entry(string key)
            {
                this.key = key;
                this.value = $"{key}: No value";
                this.callbacks = null;
            }
        }
    }
}
