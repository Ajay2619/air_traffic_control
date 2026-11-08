namespace ClearToLand
{
    /// <summary>
    /// Per-map best scores stored through <see cref="PrefsStore"/>: PlayerPrefs on Android, PC and in the editor,
    /// plus browser localStorage on WebGL so scores survive new itch.io uploads.
    /// </summary>
    public static class HighScores
    {
        public const string KeyPrefix = "ctl.best.";

        public static string Key(string mapId) => KeyPrefix + (mapId ?? string.Empty);

        public static int Get(string mapId) => PrefsStore.GetInt(Key(mapId), 0);

        /// <summary>Stores <paramref name="score"/> if it beats the current best. Returns true when it is a new best.</summary>
        public static bool Submit(string mapId, int score)
        {
            if (score <= Get(mapId)) return false;
            PrefsStore.SetInt(Key(mapId), score);
            PrefsStore.Save();
            return true;
        }

        public static void Clear(string mapId)
        {
            PrefsStore.DeleteKey(Key(mapId));
            PrefsStore.Save();
        }
    }
}
