#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Int key/value persistence used by <see cref="HighScores"/> and <see cref="GameSettings"/>.
    /// In the editor and on Android/Windows this is plain PlayerPrefs.
    /// WebGL players also store every value in the browser's localStorage (Plugins/WebGL/CtlStorage.jslib, key prefix
    /// "cleartoland."). Unity keeps WebGL PlayerPrefs in IndexedDB under a folder hashed from the page URL, and itch.io
    /// serves each uploaded build from a new URL, so PlayerPrefs alone would reset saves on every update.
    /// PlayerPrefs is still written as a second copy, and a key missing from localStorage is read from PlayerPrefs and
    /// copied over, so saves made by earlier builds on the same URL carry forward.
    /// </summary>
    public static class PrefsStore
    {
        public static bool HasKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CtlStorageHasKey(key) != 0 || PlayerPrefs.HasKey(key);
#else
            return PlayerPrefs.HasKey(key);
#endif
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (CtlStorageHasKey(key) != 0) return CtlStorageGetInt(key, defaultValue);
            if (!PlayerPrefs.HasKey(key)) return defaultValue;

            // Migration: a value saved only in PlayerPrefs (older build on this URL) is copied to localStorage so it
            // survives the next upload.
            int value = PlayerPrefs.GetInt(key, defaultValue);
            CtlStorageSetInt(key, value);
            return value;
#else
            return PlayerPrefs.GetInt(key, defaultValue);
#endif
        }

        public static void SetInt(string key, int value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CtlStorageSetInt(key, value);
#endif
            PlayerPrefs.SetInt(key, value);
        }

        public static void DeleteKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CtlStorageDeleteKey(key);
#endif
            PlayerPrefs.DeleteKey(key);
        }

        /// <summary>Flushes PlayerPrefs to disk. localStorage writes are already persisted when they are made.</summary>
        public static void Save() => PlayerPrefs.Save();

#if UNITY_WEBGL && !UNITY_EDITOR
        // Implemented in Assets/Game/Plugins/WebGL/CtlStorage.jslib. Ints only: no bool marshalling across the boundary.
        [DllImport("__Internal")] private static extern int CtlStorageHasKey(string key);
        [DllImport("__Internal")] private static extern int CtlStorageGetInt(string key, int defaultValue);
        [DllImport("__Internal")] private static extern int CtlStorageSetInt(string key, int value);
        [DllImport("__Internal")] private static extern int CtlStorageDeleteKey(string key);
#endif
    }
}
