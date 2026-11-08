using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Paints and imports the map background sprites: 2048 x 1152 px at 80 px per world unit (25.6 x 14.4 units),
    /// centered on the origin. Each known map id has its own painter; unknown ids get a plain grass field with
    /// their landing spots so custom maps still work.
    /// </summary>
    public static class MapPainter
    {
        /// <summary>Project folder the background PNGs are written to.</summary>
        public const string OutputFolder = "Assets/Game/Art/Generated/Maps";

        /// <summary>
        /// Paints <c>{OutputFolder}/{def.id}.png</c>, imports it as a compressed sprite (PPU 80), assigns it to
        /// <see cref="MapDefinition.background"/> and marks the asset dirty. Layout problems are logged as warnings.
        /// </summary>
        public static Sprite GenerateBackground(MapDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            string id = string.IsNullOrEmpty(def.id) ? "map" : def.id;

            List<string> problems = new List<string>();
            if (!MapCatalog.Validate(def, problems))
            {
                foreach (string p in problems) Debug.LogWarning($"[ClearToLand] Map '{id}': {p}");
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            PixelCanvas canvas = Paint(def);
            long paintMs = sw.ElapsedMilliseconds;

            string path = $"{OutputFolder}/{id}.png";
            Sprite sprite = SpriteImportUtil.SavePngAsSprite(canvas, path, GameConstants.BackgroundPixelsPerUnit, default, compressed: true);
            if (sprite != null)
            {
                def.background = sprite;
                EditorUtility.SetDirty(def);
            }
            else
            {
                // SavePngAsSprite already logged the import failure; keep the previous reference instead of wiping it.
                Debug.LogWarning($"[ClearToLand] Map '{id}': background sprite did not import, kept the previous background reference.");
            }
            Debug.Log($"[ClearToLand] Map background '{id}': painted in {paintMs} ms, saved + imported in {sw.ElapsedMilliseconds - paintMs} ms -> {path}");
            return sprite;
        }

        /// <summary>Paints the background for <paramref name="def"/> into a new canvas without saving it (previews, tests).</summary>
        public static PixelCanvas Paint(MapDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            string id = def.id ?? string.Empty;
            int seed = MapKit.StableSeed(id);
            PixelCanvas c = new PixelCanvas(MapKit.Width, MapKit.Height, new Color(0f, 0f, 0f, 1f));
            switch (id)
            {
                case "green_valley":
                    GreenValleyPainter.Paint(c, def, seed);
                    MapKit.Finish(c, seed, 0.05f, 0.12f);
                    break;
                case "dune_sea":
                    DuneSeaPainter.Paint(c, def, seed);
                    MapKit.Finish(c, seed, 0.045f, 0.1f);
                    break;
                case "harbor_bay":
                    HarborBayPainter.Paint(c, def, seed);
                    MapKit.Finish(c, seed, 0.045f, 0.12f);
                    break;
                case "carrier_group":
                    CarrierGroupPainter.Paint(c, def, seed);
                    MapKit.Finish(c, seed, 0.04f, 0.14f);
                    break;
                default:
                    PaintGeneric(c, def, seed);
                    MapKit.Finish(c, seed, 0.05f, 0.12f);
                    break;
            }
            return c;
        }

        /// <summary>Fallback for maps without a dedicated painter: soft meadow plus the map's landing spots.</summary>
        private static void PaintGeneric(PixelCanvas c, MapDefinition def, int seed)
        {
            MapNoiseField low = new MapNoiseField(c.Width, c.Height, 380f, 4, seed + 1);
            MapNoiseField mid = new MapNoiseField(c.Width, c.Height, 60f, 3, seed + 2);
            Color a = MapKit.Rgb(0x6E9B4A), b = MapKit.Rgb(0x93B862);
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float l = low.Sample(x, y);
                    Color col = MapKit.Mix(a, b, MapKit.Smooth(0.3f, 0.7f, l));
                    c[x, y] = MapKit.Shade(col, 1f + (mid.Sample(x, y) - 0.5f) * 0.14f);
                }
            }
            if (def.landingSpots == null) return;
            foreach (LandingSpotDef s in def.landingSpots)
                RunwayPainter.Paint(c, s, RunwaySurface.Asphalt, true, seed + 5);
        }
    }
}
