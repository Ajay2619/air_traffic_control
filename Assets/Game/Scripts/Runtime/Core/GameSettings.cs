using System;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>Player preferences persisted in PlayerPrefs.</summary>
    public static class GameSettings
    {
        public const string SoundKey = "ctl.sound";
        public const string TutorialSeenKey = "ctl.tutorialSeen";

        /// <summary>Raised after any setting changes.</summary>
        public static event Action Changed;

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set
            {
                if (value == SoundEnabled) return;
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>True once the first-play drag hint has been shown.</summary>
        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(TutorialSeenKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;
    }
}
