namespace ClearToLand
{
    /// <summary>One-shot sound effects played through <see cref="AudioManager.Play"/>.</summary>
    public enum Sfx
    {
        /// <summary>Soft two-tone chime when an edge "!" warning appears (an aircraft is about to enter).</summary>
        Alert,

        /// <summary>Short blip when the player picks up an aircraft to draw its path.</summary>
        Select,

        /// <summary>Rising two-note confirmation when a drawn path locks onto a landing spot.</summary>
        Lock,

        /// <summary>Pleasant major arpeggio at touchdown.</summary>
        Landed,

        /// <summary>Low-passed noise burst with a low thump for a mid-air collision.</summary>
        Crash,

        /// <summary>Tiny tick for UI buttons.</summary>
        Click
    }
}
