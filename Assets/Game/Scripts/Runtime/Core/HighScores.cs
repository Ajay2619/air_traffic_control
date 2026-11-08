using UnityEngine;

namespace ClearToLand
{
    /// <summary>Per-map best scores stored in PlayerPrefs (works on Android, PC and WebGL).</summary>
    public static class HighScores
    {
        public const string KeyPrefix = "ctl.best.";

        public static string Key(string mapId) => KeyPrefix + (mapId ?? string.Empty);

        public static int Get(string mapId) => PlayerPrefs.GetInt(Key(mapId), 0);

        /// <summary>Stores <paramref name="score"/> if it beats the current best. Returns true when it is a new best.</summary>
        public static bool Submit(string mapId, int score)
        {
            if (score <= Get(mapId)) return false;
            PlayerPrefs.SetInt(Key(mapId), score);
            PlayerPrefs.Save();
            return true;
        }

        public static void Clear(string mapId)
        {
            PlayerPrefs.DeleteKey(Key(mapId));
            PlayerPrefs.Save();
        }
    }
}
