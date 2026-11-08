using System;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Player preferences persisted through <see cref="PrefsStore"/> (PlayerPrefs, plus browser localStorage on
    /// WebGL so settings survive new itch.io uploads).
    /// </summary>
    public static class GameSettings
    {
        public const string SoundKey = "ctl.sound";
        public const string TutorialSeenKey = "ctl.tutorialSeen";

        /// <summary>Raised after any setting changes.</summary>
        public static event Action Changed;

        public static bool SoundEnabled
        {
            get => PrefsStore.GetInt(SoundKey, 1) == 1;
            set
            {
                if (value == SoundEnabled) return;
                PrefsStore.SetInt(SoundKey, value ? 1 : 0);
                PrefsStore.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>True once the first-play drag hint has been shown.</summary>
        public static bool TutorialSeen
        {
            get => PrefsStore.GetInt(TutorialSeenKey, 0) == 1;
            set
            {
                PrefsStore.SetInt(TutorialSeenKey, value ? 1 : 0);
                PrefsStore.Save();
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;
    }
}
