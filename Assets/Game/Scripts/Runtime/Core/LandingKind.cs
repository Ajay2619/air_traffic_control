using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Where an aircraft is allowed to land. Each aircraft type has exactly one kind, and each kind
    /// has one signature color (aircraft are tinted with it, landing-spot markings are painted with it).
    /// </summary>
    public enum LandingKind
    {
        ShortRunway = 0, // props        - red
        LongRunway = 1,  // jets         - blue
        Helipad = 2,     // helicopters  - yellow
        WaterLane = 3,   // seaplanes    - purple
    }

    public static class LandingColors
    {
        public static readonly Color ShortRunway = new Color32(0xE8, 0x4A, 0x3C, 0xFF);
        public static readonly Color LongRunway = new Color32(0x2F, 0x80, 0xED, 0xFF);
        public static readonly Color Helipad = new Color32(0xF5, 0xC2, 0x1B, 0xFF);
        public static readonly Color WaterLane = new Color32(0x9B, 0x5D, 0xE5, 0xFF);

        public static Color For(LandingKind kind)
        {
            switch (kind)
            {
                case LandingKind.ShortRunway: return ShortRunway;
                case LandingKind.LongRunway: return LongRunway;
                case LandingKind.Helipad: return Helipad;
                case LandingKind.WaterLane: return WaterLane;
                default: return Color.white;
            }
        }

        public static string Label(LandingKind kind)
        {
            switch (kind)
            {
                case LandingKind.ShortRunway: return "Short runway";
                case LandingKind.LongRunway: return "Long runway";
                case LandingKind.Helipad: return "Helipad";
                case LandingKind.WaterLane: return "Water lane";
                default: return kind.ToString();
            }
        }

        /// <summary>True for every kind that has a direction (approach end -> far end).</summary>
        public static bool IsDirectional(LandingKind kind) => kind != LandingKind.Helipad;
    }
}
