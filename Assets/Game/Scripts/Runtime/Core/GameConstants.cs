namespace ClearToLand
{
    /// <summary>
    /// World-space layout constants shared by gameplay, art generation and map layouts.
    /// The world is 2D in the XY plane; the camera looks down +Z. 1 world unit = 100 px at 1080p.
    /// </summary>
    public static class GameConstants
    {
        public const string GameTitle = "Clear to Land";

        /// <summary>Area that is always visible on every aspect ratio (CameraFitter guarantees it).</summary>
        public const float SafeWidth = 19.2f;
        public const float SafeHeight = 10.8f;

        /// <summary>Map background size in world units. Covers aspect ratios from 4:3 up to ~21:9.</summary>
        public const float BackgroundWidth = 25.6f;
        public const float BackgroundHeight = 14.4f;
        public const int BackgroundPixelWidth = 2048;
        public const int BackgroundPixelHeight = 1152;
        public const float BackgroundPixelsPerUnit = 80f;

        /// <summary>Pixels-per-unit for world sprites (aircraft, effects, highlights).</summary>
        public const float SpritePixelsPerUnit = 200f;

        /// <summary>Pixels-per-unit for UI sprites (matches CanvasScaler reference PPU).</summary>
        public const float UiPixelsPerUnit = 100f;

        /// <summary>World units at the top/bottom of the safe area kept free of landing spots (HUD lives there).</summary>
        public const float HudTopMargin = 0.9f;
        public const float HudBottomMargin = 1.1f;

        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";
    }

    /// <summary>SpriteRenderer sorting orders (all on the Default sorting layer).</summary>
    public static class SortOrder
    {
        public const int Background = 0;
        public const int SpotHighlight = 15;
        public const int PathLine = 30;
        public const int Shadow = 40;
        public const int LandingAircraft = 45;
        public const int Aircraft = 50;
        public const int AircraftRotor = 51;
        public const int AircraftOverlay = 55; // proximity / selection rings
        public const int Effects = 70;
        public const int ScorePopup = 75;
        public const int EdgeWarning = 80;
    }
}
