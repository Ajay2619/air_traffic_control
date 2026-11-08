using System;
using UnityEditor;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Procedurally paints every non-map sprite of the game and imports it under <see cref="OutputRoot"/>.
    /// World sprites use <see cref="GameConstants.SpritePixelsPerUnit"/>, UI sprites <see cref="GameConstants.UiPixelsPerUnit"/>;
    /// all are imported uncompressed for crisp edges. Re-running overwrites the PNGs in place (GUIDs stay stable).
    /// </summary>
    public static class ArtGenerator
    {
        /// <summary>Project folder that receives the generated PNG sprites.</summary>
        public const string OutputRoot = "Assets/Game/Art/Generated";

        /// <summary>Every sprite key; the asset lives at <c>{OutputRoot}/{key}.png</c>.</summary>
        public static readonly string[] AllKeys =
        {
            "aircraft_prop", "aircraft_airliner", "aircraft_fighter", "aircraft_helicopter", "aircraft_rotor", "aircraft_seaplane",
            "fx_ring", "fx_soft_circle", "fx_debris", "fx_edge_warning", "fx_spot_glow_runway", "fx_spot_glow_pad", "fx_chevron",
            "ui_panel", "ui_button", "ui_card", "ui_circle", "ui_logo",
            "icon_pause", "icon_play", "icon_fastforward", "icon_sound_on", "icon_sound_off", "icon_home", "icon_restart",
            "icon_help", "icon_close", "icon_star",
        };

        /// <summary>Paints and imports every key in <see cref="AllKeys"/>. A failing key is logged and skipped.</summary>
        public static void GenerateSpriteArt()
        {
            SpriteImportUtil.EnsureFolder(OutputRoot);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int generated = 0;
            try
            {
                for (int i = 0; i < AllKeys.Length; i++)
                {
                    string key = AllKeys[i];
                    EditorUtility.DisplayProgressBar("Clear to Land", "Painting sprite " + key, (float)i / AllKeys.Length);
                    try
                    {
                        if (GenerateSprite(key) != null) generated++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ClearToLand] Failed to generate sprite '{key}': {e}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ClearToLand] Generated {generated}/{AllKeys.Length} sprites in {watch.Elapsed.TotalSeconds:0.0} s -> {OutputRoot}");
        }

        /// <summary>Loads a generated sprite by key (null if it has not been generated yet).</summary>
        public static Sprite LoadSprite(string key) => AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputRoot}/{key}.png");

        /// <summary>Project-relative PNG path for <paramref name="key"/>.</summary>
        public static string AssetPathFor(string key) => $"{OutputRoot}/{key}.png";

        /// <summary>Paints and imports a single sprite; throws <see cref="ArgumentException"/> for an unknown key.</summary>
        public static Sprite GenerateSprite(string key)
        {
            SpriteSpec spec = GetSpec(key);
            PixelCanvas canvas = PaintSpec(spec);
            return SpriteImportUtil.SavePngAsSprite(canvas, AssetPathFor(key), spec.PixelsPerUnit, spec.Border);
        }

        /// <summary>Paints a sprite in memory without touching the AssetDatabase (previews / tests).</summary>
        public static PixelCanvas Paint(string key) => PaintSpec(GetSpec(key));

        private static PixelCanvas PaintSpec(SpriteSpec spec)
        {
            var canvas = new PixelCanvas(spec.Width, spec.Height);
            spec.Painter(canvas);
            return canvas;
        }

        private static SpriteSpec GetSpec(string key)
        {
            const float world = GameConstants.SpritePixelsPerUnit;
            const float ui = GameConstants.UiPixelsPerUnit;
            Vector4 none = Vector4.zero;
            switch (key)
            {
                case "aircraft_prop": return new SpriteSpec(128, 128, world, none, AircraftPainter.PaintProp);
                case "aircraft_airliner": return new SpriteSpec(208, 208, world, none, AircraftPainter.PaintAirliner);
                case "aircraft_fighter": return new SpriteSpec(160, 160, world, none, AircraftPainter.PaintFighter);
                case "aircraft_helicopter": return new SpriteSpec(144, 144, world, none, AircraftPainter.PaintHelicopter);
                case "aircraft_rotor": return new SpriteSpec(176, 176, world, none, AircraftPainter.PaintRotor);
                case "aircraft_seaplane": return new SpriteSpec(152, 152, world, none, AircraftPainter.PaintSeaplane);

                case "fx_ring": return new SpriteSpec(256, 256, world, none, FxPainter.PaintRing);
                case "fx_soft_circle": return new SpriteSpec(128, 128, world, none, FxPainter.PaintSoftCircle);
                case "fx_debris": return new SpriteSpec(32, 16, world, none, FxPainter.PaintDebris);
                case "fx_edge_warning": return new SpriteSpec(128, 128, world, none, FxPainter.PaintEdgeWarning);
                case "fx_spot_glow_runway": return new SpriteSpec(128, 128, world, Border(48f), FxPainter.PaintSpotGlowRunway);
                case "fx_spot_glow_pad": return new SpriteSpec(128, 128, world, none, FxPainter.PaintSpotGlowPad);
                case "fx_chevron": return new SpriteSpec(64, 64, world, none, FxPainter.PaintChevron);

                case "ui_panel": return new SpriteSpec(96, 96, ui, Border(36f), c => UiPainter.PaintRoundedRect(c, 28f));
                case "ui_button": return new SpriteSpec(96, 96, ui, Border(32f), c => UiPainter.PaintRoundedRect(c, 24f));
                case "ui_card": return new SpriteSpec(96, 96, ui, Border(28f), c => UiPainter.PaintRoundedRect(c, 20f));
                case "ui_circle": return new SpriteSpec(128, 128, ui, none, UiPainter.PaintCircle);
                case "ui_logo": return new SpriteSpec(256, 256, ui, none, UiPainter.PaintLogo);

                case "icon_pause": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintPause);
                case "icon_play": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintPlay);
                case "icon_fastforward": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintFastForward);
                case "icon_sound_on": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintSoundOn);
                case "icon_sound_off": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintSoundOff);
                case "icon_home": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintHome);
                case "icon_restart": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintRestart);
                case "icon_help": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintHelp);
                case "icon_close": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintClose);
                case "icon_star": return new SpriteSpec(128, 128, ui, none, IconPainter.PaintStar);

                default: throw new ArgumentException($"Unknown sprite key '{key}'.", nameof(key));
            }
        }

        private static Vector4 Border(float px) => new Vector4(px, px, px, px);

        /// <summary>Canvas size, import settings and painter for one key.</summary>
        private readonly struct SpriteSpec
        {
            public readonly int Width;
            public readonly int Height;
            public readonly float PixelsPerUnit;
            public readonly Vector4 Border;
            public readonly Action<PixelCanvas> Painter;

            public SpriteSpec(int width, int height, float pixelsPerUnit, Vector4 border, Action<PixelCanvas> painter)
            {
                Width = width;
                Height = height;
                PixelsPerUnit = pixelsPerUnit;
                Border = border;
                Painter = painter;
            }
        }
    }
}
