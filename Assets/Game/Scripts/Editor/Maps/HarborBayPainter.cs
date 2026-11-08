using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Harbor Bay: green coast with a harbor town in the north-west, a natural beach and breakwater with a
    /// lighthouse in the west, a stone quay with marina, warehouses and a cargo jetty in the east, and a wavy bay
    /// with boats. The long runway runs along the shore, the short strip sits on the hill, the water lane lies in
    /// the bay and the helipad on a pier platform.
    /// </summary>
    internal static class HarborBayPainter
    {
        private static readonly Color Grass = MapKit.Rgb(0x8AB262);
        private static readonly Color GrassDark = MapKit.Rgb(0x6E9A4E);
        private static readonly Color AirGrass = MapKit.Rgb(0x93BA68);
        private static readonly Color Sand = MapKit.Rgb(0xEAD8A8);
        private static readonly Color WetSand = MapKit.Rgb(0xCDB98C);
        private static readonly Color QuayStone = MapKit.Rgb(0xB3AFA4);
        private static readonly Color SeaShallow = MapKit.Rgb(0x5DB4BA);
        private static readonly Color SeaDeep = MapKit.Rgb(0x22618E);
        private static readonly Color Street = MapKit.Rgb(0x8F8E8A);
        private static readonly Color Pavement = MapKit.Rgb(0xCBC7BB);
        private static readonly Color Concrete = MapKit.Rgb(0xB9B8B1);
        private static readonly Color Wood = MapKit.Rgb(0x9B7A55);

        private static readonly Color[] RoofColors =
        {
            MapKit.Rgb(0xB4705A), MapKit.Rgb(0xA4654F), MapKit.Rgb(0x7E8895), MapKit.Rgb(0x939FAA), MapKit.Rgb(0xD9CFB8),
            MapKit.Rgb(0xE7E2D7), MapKit.Rgb(0x8E9B80), MapKit.Rgb(0xBE8A60),
        };
        private static readonly Color[] TreeColors =
        {
            MapKit.Rgb(0x4B7B34), MapKit.Rgb(0x578B3A), MapKit.Rgb(0x447132), MapKit.Rgb(0x5E8E3E),
        };
        private static readonly Color[] ContainerColors =
        {
            MapKit.Rgb(0x9A5F4C), MapKit.Rgb(0x4F7D86), MapKit.Rgb(0x7B8B5C), MapKit.Rgb(0x8C8F94), MapKit.Rgb(0xA98A5C), MapKit.Rgb(0x5D6C8A),
        };

        private const float StreetW = 0.2f;
        private const float QuayY = 1.05f;

        public static void Paint(PixelCanvas c, MapDefinition def, int seed)
        {
            LandingSpotDef longRw = MapKit.FindSpot(def, LandingKind.LongRunway);
            LandingSpotDef shortRw = MapKit.FindSpot(def, LandingKind.ShortRunway);
            LandingSpotDef pad = MapKit.FindSpot(def, LandingKind.Helipad);
            Vector2 padPos = pad != null ? pad.position : new Vector2(7.4f, -0.5f);

            // Airport layout relative to the runways.
            bool hasTerminal = longRw != null;
            Vector2 apronC = Vector2.zero, terminalC = Vector2.zero;
            float rwAngle = 0f;
            if (hasTerminal)
            {
                Vector2 d = longRw.Direction, left = MapKit.Perp(d);
                rwAngle = longRw.angleDeg;
                apronC = longRw.position + d * (longRw.length * 0.4f) + left * 1.05f;
                terminalC = apronC + left * 0.52f;
            }
            bool hasHangar = shortRw != null;
            Vector2 hangarC = Vector2.zero;
            if (hasHangar)
                hangarC = shortRw.position + shortRw.Direction * (shortRw.length * 0.75f) - MapKit.Perp(shortRw.Direction) * 0.95f;

            List<MapZone> zl = new List<MapZone>();
            if (longRw != null) zl.Add(MapZone.AroundSpot(longRw, 0.7f, RunwayPainter.StubLength + 0.2f));
            if (shortRw != null) zl.Add(MapZone.AroundSpot(shortRw, 0.7f, RunwayPainter.StubLength + 0.2f));
            if (hasTerminal) zl.Add(new MapZone(apronC + MapKit.Perp(longRw.Direction) * 0.25f, rwAngle, 1.75f, 0.65f, 0.3f));
            if (hasHangar) zl.Add(MapZone.Circle(hangarC, 0.75f));
            MapZone[] airZones = zl.ToArray();

            Terrain(c, seed, airZones);

            System.Random rng = new System.Random(seed);
            List<MapTree> trees = new List<MapTree>();

            // Roads and the town.
            List<Vector2> road = MapKit.Spline(new[]
            {
                new Vector2(-13.6f, 2.42f), new Vector2(-6f, 2.42f), new Vector2(0.9f, 2.42f), new Vector2(1.8f, 1.66f),
                new Vector2(5f, 1.62f), new Vector2(13.6f, 1.62f),
            }, 8);
            List<Vector2> hillRoad = MapKit.Spline(new[]
            {
                new Vector2(1.2f, 2.4f), new Vector2(2.2f, 3.35f), new Vector2(3.2f, 3.75f), hasHangar ? hangarC + new Vector2(-0.45f, 0.1f) : new Vector2(4f, 3.6f),
            }, 8);
            PaintTown(c, rng, trees, seed + 10);
            DrawRoad(c, road, 0.24f);
            DrawRoad(c, hillRoad, 0.18f);

            // Airport surfaces.
            if (hasTerminal)
            {
                Vector2 left = MapKit.Perp(longRw.Direction);
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 a = MapKit.Local(apronC, rwAngle, i * 1.0f, 0f);
                    Vector2 b = MapKit.ClosestOnCenterline(longRw, a - left * 0.6f);
                    List<Vector2> taxi = new List<Vector2> { MapKit.Px(a), MapKit.Px(b) };
                    c.StrokePolyline(taxi, MapKit.Len(0.34f), MapKit.Shade(Concrete, 0.8f));
                    c.StrokePolyline(taxi, MapKit.Len(0.28f), Concrete);
                }
                MapKit.Slab(c, apronC, new Vector2(2.9f, 0.6f), rwAngle, 0.08f, Concrete, 0.4f, seed + 20);
            }
            if (hasHangar) MapKit.Slab(c, hangarC, new Vector2(1.0f, 0.82f), shortRw.angleDeg + 180f, 0.08f, Concrete, 0.4f, seed + 21);

            // Breakwater, piers, platform, jetty.
            PaintBreakwater(c, seed + 30);
            PaintPiers(c, rng, padPos, seed + 40);

            // Wakes and boats in the bay (kept clear of the water lane and its approach).
            BayBoats(c, seed + 60);

            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots) RunwayPainter.Paint(c, s, RunwaySurface.Asphalt, true, seed + 70);
            }

            // Airport buildings.
            if (hasTerminal)
            {
                MapKit.Building(c, terminalC, new Vector2(2.2f, 0.42f), rwAngle, MapKit.Rgb(0xDAD9D3), MapRoof.Flat, 0.24f, seed + 80);
                Vector2 tower = MapKit.Local(apronC, rwAngle, 1.75f, 0.42f);
                Vector2 tp = MapKit.Px(tower);
                float tr = MapKit.Len(0.15f);
                MapKit.SoftShadow(c, q => Sdf.Circle(q, tp, tr), PixelCanvas.Square(tp, tr), MapKit.ShadowOffset(0.5f), 2f, 0.4f, true);
                MapKit.Blob(c, tp, tr, MapKit.Rgb(0xE4E6E8), seed + 81, 0f, 0.04f);
                c.FillCircle(tp, tr * 0.62f, MapKit.Rgb(0x52718A));
            }
            if (hasHangar)
                MapKit.Building(c, MapKit.Local(hangarC, shortRw.angleDeg + 180f, 0f, 0.08f), new Vector2(0.72f, 0.5f), shortRw.angleDeg + 180f,
                    MapKit.Rgb(0xC3C8CA), MapRoof.Arched, 0.26f, seed + 82);

            // Warehouses along the quay road.
            float[] wx0 = { 1.55f, 7.95f, 9.35f, 10.85f, 12.2f };
            float[] wx1 = { 2.55f, 9.15f, 10.65f, 12.0f, 13.4f };
            for (int i = 0; i < wx0.Length; i++)
            {
                Vector2 center = new Vector2((wx0[i] + wx1[i]) * 0.5f, 2.12f);
                MapKit.Building(c, center, new Vector2(wx1[i] - wx0[i], 0.58f), 0f, i % 2 == 0 ? MapKit.Rgb(0x8D9AA5) : MapKit.Rgb(0xB3B0A6),
                    MapRoof.Flat, 0.26f, seed + 90 + i);
            }

            // Greenery: suburb in the east, trees along the shore and roads, away from runways and the town.
            Suburb(c, rng, trees, airZones, seed + 120);
            for (int i = 0; i < 260; i++)
            {
                Vector2 w = new Vector2(MapKit.Range(rng, -12.8f, 12.8f), MapKit.Range(rng, -3f, 7.2f));
                float sd = w.y - CoastY(w.x, 0.5f);
                if (sd < 0.45f) continue;
                if (MapZone.Min(airZones, w.x, w.y) < 0.25f) continue;
                if (w.x < 1.4f && w.y > 2.5f) continue;                        // town
                if (w.y > 1.3f && w.y < 2.65f && w.x > 1.3f) continue;          // quay road + warehouses
                if (MapKit.DistToPolyline(w, road) < 0.3f || MapKit.DistToPolyline(w, hillRoad) < 0.26f) continue;
                if (w.x > 9.5f && w.y > 2.7f) continue;                          // suburb handles its own trees
                trees.Add(new MapTree(w, MapKit.Range(rng, 0.13f, 0.22f), TreeColors[rng.Next(TreeColors.Length)]));
            }
            MapKit.DrawTrees(c, trees, seed + 150);
        }

        // ------------------------------------------------------------------------------------------------ terrain

        /// <summary>Coastline height (world y) at world x; land lies above. <paramref name="n"/> is local detail noise.</summary>
        private static float CoastY(float wx, float n)
        {
            float t = MapKit.Smooth(-0.8f, 2.6f, wx);
            float baseY = Mathf.Lerp(-1.35f, QuayY, t);
            float natural = 1f - MapKit.Smooth(0.4f, 1.9f, wx);
            float wig = 0.17f * Mathf.Sin(wx * 0.9f + 1.3f) + 0.09f * Mathf.Sin(wx * 2.1f + 0.4f);
            return baseY + (wig + (n - 0.5f) * 0.4f) * natural;
        }

        private static void Terrain(PixelCanvas c, int seed, MapZone[] airZones)
        {
            MapNoiseField coastN = new MapNoiseField(c.Width, c.Height, 110f, 3, seed + 1);
            MapNoiseField low = new MapNoiseField(c.Width, c.Height, 420f, 3, seed + 2);
            MapNoiseField mid = new MapNoiseField(c.Width, c.Height, 60f, 3, seed + 3);
            MapNoiseField swell = new MapNoiseField(c.Width, c.Height, 210f, 2, seed + 4);
            MapNoiseField fine = new MapNoiseField(c.Width, c.Height, 22f, 2, seed + 5, 2);
            for (int y = 0; y < c.Height; y++)
            {
                float wy = MapKit.WorldY(y);
                for (int x = 0; x < c.Width; x++)
                {
                    float wx = MapKit.WorldX(x);
                    float n = coastN.Sample(x, y);
                    float sd = wy - CoastY(wx, n);
                    float landCov = Mathf.Clamp01(sd * MapKit.Ppu + 0.5f);
                    float quay = MapKit.Smooth(0.4f, 1.9f, wx);
                    float fn = fine.Sample(x, y);
                    Color land = Grass, sea = SeaDeep;

                    if (landCov > 0f)
                    {
                        float l = low.Sample(x, y);
                        land = MapKit.Mix(GrassDark, Grass, MapKit.Smooth(0.3f, 0.72f, l));
                        land = MapKit.Shade(land, 1f + (mid.Sample(x, y) - 0.5f) * 0.14f + (fn - 0.5f) * 0.06f);
                        float az = MapZone.Min(airZones, wx, wy);
                        if (az < 0.1f)
                        {
                            float m = Mathf.Sin((wx * 0.1f + wy) * (Mathf.PI / 0.4f));
                            Color g = MapKit.Shade(AirGrass, 1f + 0.03f * Mathf.Clamp(m * 3f, -1f, 1f));
                            land = MapKit.Mix(g, land, MapKit.Smooth(-0.05f, 0.08f, az));
                        }
                        // Natural beach in the west, stone quay in the east.
                        float sandW = 0.26f * (1f - quay);
                        if (sandW > 0.01f)
                        {
                            float st = 1f - MapKit.Smooth(sandW - 0.05f, sandW + 0.05f, sd + (n - 0.5f) * 0.08f);
                            Color sand = MapKit.Mix(WetSand, Sand, MapKit.Smooth(0.0f, 0.08f, sd));
                            land = MapKit.Mix(land, MapKit.Shade(sand, 1f + (fn - 0.5f) * 0.08f), st);
                        }
                        float stoneW = 0.15f * quay;
                        if (stoneW > 0.01f)
                        {
                            float qt = Mathf.Clamp01((stoneW - sd) * MapKit.Ppu + 0.5f) * quay;
                            float blocks = Mathf.Repeat(wx * 5f, 1f) < 0.06f ? 0.9f : 1f;
                            Color stone = MapKit.Shade(QuayStone, blocks * (sd < 0.025f ? 1.12f : 1f) * (1f + (fn - 0.5f) * 0.08f));
                            land = MapKit.Mix(land, stone, qt);
                        }
                    }

                    if (landCov < 1f)
                    {
                        float depth = Mathf.Max(0f, -sd);
                        float sw = swell.Sample(x, y);
                        sea = MapKit.Mix(SeaShallow, SeaDeep, MapKit.Smooth(0.0f, 2.8f, depth + (sw - 0.5f) * 0.9f));
                        sea = MapKit.Mix(sea, MapKit.Rgb(0x8FD0C6), (1f - MapKit.Smooth(0.0f, 0.55f, depth)) * (1f - quay) * 0.55f);
                        float band = Mathf.Sin((wx * 0.35f + wy) * 5.5f + sw * 9f);
                        sea = MapKit.Shade(sea, 1f + 0.025f * band + (fn - 0.5f) * 0.05f);
                        float deepT = MapKit.Smooth(0.25f, 0.9f, depth);
                        float crest = MapKit.WaveMark(x, y, 26f, 0.55f, seed + 9) * deepT;
                        float trough = MapKit.WaveMark(x, y + 2.2f, 26f, 0.55f, seed + 9) * deepT;
                        sea = MapKit.Shade(sea, 1f - 0.1f * trough);
                        sea = MapKit.Mix(sea, MapKit.Foam, crest * 0.42f);
                        float foam = (1f - MapKit.Smooth(0.015f, 0.1f, depth)) * (0.5f + 0.5f * n) * (1f - quay);
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(depth - 0.21f - (n - 0.5f) * 0.12f) / 0.025f) * 0.35f * (1f - quay);
                        sea = MapKit.Mix(sea, MapKit.Foam, Mathf.Max(foam * 0.85f, ring));
                        sea = MapKit.Shade(sea, 1f - 0.3f * (1f - MapKit.Smooth(0f, 0.12f, depth)) * quay);
                    }
                    Color col = MapKit.Mix(sea, land, landCov);
                    col.a = 1f;
                    c[x, y] = col;
                }
            }
        }

        private static void DrawRoad(PixelCanvas c, List<Vector2> world, float widthW)
        {
            List<Vector2> px = MapKit.ToPx(world);
            c.StrokePolyline(px, MapKit.Len(widthW) + 3f, MapKit.Rgb(0xB9B5A8));
            c.StrokePolyline(px, MapKit.Len(widthW), p => MapKit.Shade(MapKit.Rgb(0x7E7D7A), 1f + (Mathf.PerlinNoise(p.x / 11f, p.y / 11f) - 0.5f) * 0.1f));
        }

        // ------------------------------------------------------------------------------------------------ town

        private static void PaintTown(PixelCanvas c, System.Random rng, List<MapTree> trees, int seed)
        {
            Rect town = Rect.MinMaxRect(-13.0f, 2.64f, 1.3f, 7.4f);
            MapKit.Slab(c, town.center, town.size, 0f, 0.3f, Street, 0f, seed, 0.12f);
            float x = town.xMin + 0.1f;
            int col = 0;
            while (x < town.xMax - 0.45f)
            {
                float bw = 1.15f + 0.45f * MapKit.Hash01(col, 1, seed);
                float xr = Mathf.Min(x + bw, town.xMax - 0.1f);
                float y = town.yMin + 0.1f;
                int row = 0;
                float colShift = (MapKit.Hash01(col, 2, seed) - 0.5f) * 0.3f;
                while (y < town.yMax - 0.3f)
                {
                    float bh = 0.85f + 0.35f * MapKit.Hash01(col, row + 10, seed) + (row == 0 ? colShift : 0f);
                    float yt = Mathf.Min(y + bh, town.yMax);
                    PaintBlock(c, rng, trees, Rect.MinMaxRect(x, y, xr, yt), seed + col * 31 + row * 7);
                    y = yt + StreetW;
                    row++;
                }
                x = xr + StreetW;
                col++;
            }
        }

        private static void PaintBlock(PixelCanvas c, System.Random rng, List<MapTree> trees, Rect block, int seed)
        {
            if (block.width < 0.35f || block.height < 0.35f) return;
            bool park = MapKit.Hash01(seed, 1, 3) < 0.13f;
            if (park)
            {
                MapKit.Slab(c, block.center, block.size, 0f, 0.08f, MapKit.Rgb(0x86B05C), 0f, seed, 0.1f);
                int n = 3 + rng.Next(4);
                for (int i = 0; i < n; i++)
                {
                    Vector2 w = new Vector2(MapKit.Range(rng, block.xMin + 0.18f, block.xMax - 0.18f), MapKit.Range(rng, block.yMin + 0.18f, block.yMax - 0.18f));
                    trees.Add(new MapTree(w, MapKit.Range(rng, 0.13f, 0.19f), TreeColors[rng.Next(TreeColors.Length)]));
                }
                return;
            }
            MapKit.Slab(c, block.center, block.size, 0f, 0.06f, Pavement, 0f, seed, 0.1f);
            bool alongX = block.width >= block.height;
            int lots = Mathf.Clamp(Mathf.RoundToInt((alongX ? block.width : block.height) / 0.5f), 2, 4);
            bool backToBack = (alongX ? block.height : block.width) > 0.8f;
            const float inset = 0.06f;
            for (int i = 0; i < lots; i++)
            {
                for (int j = 0; j < (backToBack ? 2 : 1); j++)
                {
                    Rect lot;
                    if (alongX)
                    {
                        float lw = block.width / lots;
                        float lh = backToBack ? block.height * 0.5f : block.height;
                        lot = new Rect(block.xMin + i * lw, block.yMin + j * lh, lw, lh);
                    }
                    else
                    {
                        float lh = block.height / lots;
                        float lw = backToBack ? block.width * 0.5f : block.width;
                        lot = new Rect(block.xMin + j * lw, block.yMin + i * lh, lw, lh);
                    }
                    int ls = seed + i * 13 + j * 101;
                    if (MapKit.Hash01(ls, 4, 9) < 0.12f)
                    {
                        trees.Add(new MapTree(lot.center, MapKit.Range(rng, 0.12f, 0.17f), TreeColors[rng.Next(TreeColors.Length)]));
                        continue;
                    }
                    Vector2 size = new Vector2(lot.width - inset * 2f, lot.height - inset * 2f);
                    size *= 0.86f + 0.14f * MapKit.Hash01(ls, 5, 9);
                    if (size.x < 0.15f || size.y < 0.15f) continue;
                    Color roof = RoofColors[(int)(MapKit.Hash(ls, 6, 9) % (uint)RoofColors.Length)];
                    MapRoof kind = MapKit.Hash01(ls, 7, 9) < 0.62f ? MapRoof.Gable : MapRoof.Flat;
                    MapKit.Building(c, lot.center, size, 0f, roof, kind, MapKit.Range(rng, 0.1f, 0.26f), ls);
                }
            }
        }

        private static void Suburb(PixelCanvas c, System.Random rng, List<MapTree> trees, MapZone[] airZones, int seed)
        {
            // Detached houses on a loose grid (jittered, never overlapping) with garden trees.
            int n = 0;
            for (float gx = 9.9f; gx < 12.8f; gx += 0.85f)
            {
                for (float gy = 3.0f; gy < 7.1f; gy += 0.75f, n++)
                {
                    Vector2 w = new Vector2(gx + MapKit.Range(rng, -0.12f, 0.12f), gy + MapKit.Range(rng, -0.1f, 0.1f));
                    if (MapZone.Min(airZones, w.x, w.y) < 0.45f) continue;
                    if (rng.NextDouble() < 0.25)
                    {
                        trees.Add(new MapTree(w, MapKit.Range(rng, 0.15f, 0.22f), TreeColors[rng.Next(TreeColors.Length)]));
                        continue;
                    }
                    float ang = MapKit.Range(rng, -8f, 8f);
                    MapKit.Building(c, w, new Vector2(MapKit.Range(rng, 0.4f, 0.52f), MapKit.Range(rng, 0.3f, 0.38f)), ang,
                        RoofColors[rng.Next(RoofColors.Length)], MapRoof.Gable, 0.15f, seed + n);
                    trees.Add(new MapTree(w + new Vector2(0.36f, -0.28f), MapKit.Range(rng, 0.1f, 0.14f), TreeColors[rng.Next(TreeColors.Length)]));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ harbor

        private static void PaintBreakwater(PixelCanvas c, int seed)
        {
            List<Vector2> pts = MapKit.Spline(new[]
            {
                new Vector2(-3.25f, -0.85f), new Vector2(-2.95f, -1.9f), new Vector2(-2.45f, -2.5f), new Vector2(-1.8f, -2.85f),
            }, 10);
            List<Vector2> px = MapKit.ToPx(pts);
            float ox = MapKit.SeedOffset(seed, 1), oy = MapKit.SeedOffset(seed, 2);
            c.StrokePolyline(px, MapKit.Len(0.5f), MapKit.Alpha(MapKit.Foam, 0.35f));
            c.StrokePolyline(MapKit.ToPx(MapKit.OffsetPolyline(pts, -0.04f)), MapKit.Len(0.32f), MapKit.Alpha(MapKit.ShadowTint, 0.35f));
            c.StrokePolyline(px, MapKit.Len(0.3f), p =>
            {
                float n = Mathf.PerlinNoise(p.x / 4f + ox, p.y / 4f + oy);
                float boulder = MapKit.Smooth(0.35f, 0.65f, n);
                return MapKit.Shade(MapKit.Rgb(0x9A978F), 0.75f + 0.4f * boulder);
            });
            Vector2 end = px[px.Count - 1];
            float r = MapKit.Len(0.13f);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, end, r), PixelCanvas.Square(end, r), MapKit.ShadowOffset(0.7f), 2f, 0.38f, true);
            MapKit.Blob(c, end, r, MapKit.Rgb(0xF2F1EC), seed + 3, 0f, 0.03f);
            c.FillCircle(end, r * 0.58f, MapKit.Rgb(0x3E6658));
            c.FillCircle(end + new Vector2(-1f, 1f), r * 0.25f, MapKit.Rgb(0xF6EBC8));
        }

        private static void PaintPiers(PixelCanvas c, System.Random rng, Vector2 padPos, int seed)
        {
            // Marina: wooden piers with finger jetties and moored boats.
            float[] pierX = { 2.3f, 3.2f, 4.1f, 5.0f };
            for (int i = 0; i < pierX.Length; i++)
            {
                float len = 1.05f + 0.25f * MapKit.Hash01(i, 1, seed);
                Vector2 top = new Vector2(pierX[i], QuayY + 0.02f), bottom = new Vector2(pierX[i], QuayY - len);
                WoodenPier(c, top, bottom, 0.1f, seed + i);
                for (float y = QuayY - 0.3f; y > QuayY - len + 0.1f; y -= 0.34f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 a = new Vector2(pierX[i], y), b = new Vector2(pierX[i] + side * 0.26f, y);
                        WoodenPier(c, a, b, 0.05f, seed + 17);
                        if (rng.NextDouble() < 0.7)
                        {
                            Vector2 boatC = new Vector2(pierX[i] + side * 0.2f, y - 0.11f);
                            float ang = side > 0 ? 0f : 180f;
                            MapKit.Boat(c, boatC, ang + MapKit.Range(rng, -4f, 4f), MapKit.Range(rng, 0.26f, 0.34f), 0.12f,
                                MapKit.Rgb(0xF2F2EE), MapKit.Rgb(0xD8C7A3), rng.NextDouble() < 0.5 ? MapKit.Rgb(0xE8ECEF) : MapKit.Rgb(0x7D97AE),
                                false, seed + rng.Next(10000));
                        }
                    }
                }
            }

            // Helipad pier: concrete causeway to a square platform (the pad itself is painted with the spots).
            Vector2 platTop = padPos + new Vector2(0f, 0.68f);
            if (platTop.y < QuayY)
            {
                Vector2 causewayC = new Vector2(padPos.x, (QuayY + platTop.y) * 0.5f);
                Vector2 causewaySize = new Vector2(0.38f, QuayY - platTop.y + 0.1f);
                PlatformShadow(c, causewayC, causewaySize);
                MapKit.Slab(c, causewayC, causewaySize, 0f, 0.02f, Concrete, 0.3f, seed + 30, 0.25f);
            }
            PlatformShadow(c, padPos, new Vector2(1.36f, 1.36f));
            MapKit.Slab(c, padPos, new Vector2(1.36f, 1.36f), 0f, 0.1f, MapKit.Rgb(0xA9A9A3), 0f, seed + 31, 0.25f);
            for (int k = 0; k < 4; k++)
            {
                Vector2 corner = padPos + new Vector2((k & 1) == 0 ? -0.58f : 0.58f, (k & 2) == 0 ? -0.58f : 0.58f);
                c.FillCircle(MapKit.Px(corner), 2.2f, MapKit.Rgb(0x6F6F6A));
            }

            // Cargo jetty with containers and a moored freighter.
            Vector2 jettyC = new Vector2(10.2f, -0.65f);
            Vector2 jettySize = new Vector2(0.62f, 3.4f);
            PlatformShadow(c, jettyC, jettySize);
            MapKit.Slab(c, jettyC, jettySize, 0f, 0.04f, Concrete, 0.45f, seed + 32, 0.25f);
            for (int k = 0; k < 6; k++)
            {
                Vector2 w = new Vector2(10.2f + ((k & 1) == 0 ? -0.12f : 0.12f), 0.3f - (k / 2) * 0.32f);
                MapKit.Building(c, w, new Vector2(0.2f, 0.28f), 0f, ContainerColors[rng.Next(ContainerColors.Length)], MapRoof.Flat, 0.1f, seed + 40 + k);
            }
            Freighter(c, new Vector2(11.05f, -1.0f), 90f, 2.9f, 0.62f, rng, seed + 50);
        }

        private static void WoodenPier(PixelCanvas c, Vector2 aW, Vector2 bW, float widthW, int seed)
        {
            List<Vector2> seg = new List<Vector2> { MapKit.Px(aW), MapKit.Px(bW) };
            List<Vector2> sh = new List<Vector2> { MapKit.Px(aW) + MapKit.ShadowOffset(0.04f), MapKit.Px(bW) + MapKit.ShadowOffset(0.04f) };
            c.StrokePolyline(sh, MapKit.Len(widthW), MapKit.Alpha(MapKit.ShadowTint, 0.3f));
            Vector2 dir = (bW - aW).normalized;
            c.StrokePolyline(seg, MapKit.Len(widthW), p =>
            {
                float along = Vector2.Dot(p, dir);
                return MapKit.Shade(Wood, Mathf.Repeat(along, 3f) < 0.8f ? 0.82f : 1f);
            });
        }

        private static void PlatformShadow(PixelCanvas c, Vector2 centerW, Vector2 sizeW)
        {
            Vector2 cp = MapKit.Px(centerW);
            Vector2 half = new Vector2(MapKit.Len(sizeW.x), MapKit.Len(sizeW.y)) * 0.5f;
            MapKit.SoftShadow(c, p => Sdf.Box(p, cp, half, 2f), PixelCanvas.Square(cp, half.magnitude), MapKit.ShadowOffset(0.08f), 3f, 0.35f);
        }

        private static void Freighter(PixelCanvas c, Vector2 centerW, float angleDeg, float lengthW, float beamW, System.Random rng, int seed)
        {
            Vector2 cp = MapKit.Px(centerW);
            float len = MapKit.Len(lengthW), beam = MapKit.Len(beamW);
            Vector2[] local =
            {
                new Vector2(-0.5f, -0.46f), new Vector2(0.3f, -0.5f), new Vector2(0.43f, -0.36f), new Vector2(0.5f, 0f),
                new Vector2(0.43f, 0.36f), new Vector2(0.3f, 0.5f), new Vector2(-0.5f, 0.46f),
            };
            Vector2[] outer = MapKit.Transform(local, cp, angleDeg, len, beam);
            Vector2[] inner = MapKit.Transform(local, cp, angleDeg, len - 4f, beam - 4f);
            Rect b = Sdf.Bounds(outer, 2f);
            MapKit.SoftShadow(c, p => Sdf.Polygon(p, outer), b, MapKit.ShadowOffset(0.1f), 3f, 0.38f);
            c.FillSdf(p => Sdf.Polygon(p, outer), b, MapKit.Rgb(0x2C5463));
            c.FillSdf(p => Sdf.Polygon(p, inner), b, MapKit.Rgb(0x8F9493));
            for (int i = 0; i < 7; i++)
            {
                float lx = -0.26f + i * 0.088f;
                for (int j = -1; j <= 1; j += 2)
                {
                    Vector2 w = centerW + Sdf.Rotate(new Vector2(lx * lengthW, j * 0.2f * beamW), angleDeg);
                    MapKit.Building(c, w, new Vector2(0.08f * lengthW, 0.36f * beamW), angleDeg, ContainerColors[rng.Next(ContainerColors.Length)],
                        MapRoof.Flat, 0.08f, seed + i * 3 + j);
                }
            }
            Vector2 bridge = centerW + Sdf.Rotate(new Vector2(-0.39f * lengthW, 0f), angleDeg);
            MapKit.Building(c, bridge, new Vector2(0.11f * lengthW, 0.8f * beamW), angleDeg, MapKit.Rgb(0xEDEEEC), MapRoof.Flat, 0.3f, seed + 99);
        }

        private static void BayBoats(PixelCanvas c, int seed)
        {
            // heading, length; positions chosen in open water away from the lane and its approach corridor.
            MapKit.Wake(c, MapKit.Px(new Vector2(3.0f, -0.35f)) + (Vector2)(-MapKit.Dir(205f) * MapKit.Len(0.2f)), 205f, MapKit.Len(2.2f), 3.5f, 0.75f, seed + 1);
            MapKit.Boat(c, new Vector2(3.0f, -0.35f), 205f, 0.4f, 0.15f, MapKit.Rgb(0xF4F4F0), MapKit.Rgb(0xE2E4E6), MapKit.Rgb(0x6E8BA3), false, seed + 2);

            MapKit.Wake(c, MapKit.Px(new Vector2(8.3f, -3.6f)) - MapKit.Dir(168f) * MapKit.Len(0.48f), 168f, MapKit.Len(3.2f), 6f, 0.8f, seed + 3);
            MapKit.Boat(c, new Vector2(8.3f, -3.6f), 168f, 0.95f, 0.32f, MapKit.Rgb(0xF2F2EE), MapKit.Rgb(0xC9CDD0), MapKit.Rgb(0xE9ECEE), false, seed + 4);

            MapKit.Wake(c, MapKit.Px(new Vector2(-5.3f, -3.0f)) - MapKit.Dir(15f) * MapKit.Len(0.24f), 15f, MapKit.Len(1.4f), 2.5f, 0.55f, seed + 5);
            MapKit.Boat(c, new Vector2(-5.3f, -3.0f), 15f, 0.46f, 0.16f, MapKit.Rgb(0xF5F5F1), MapKit.Rgb(0xC9A979), MapKit.Rgb(0xF1F1EE), true, seed + 6);
            MapKit.Boat(c, new Vector2(-7.5f, -2.2f), -20f, 0.42f, 0.15f, MapKit.Rgb(0xF5F5F1), MapKit.Rgb(0xC9A979), MapKit.Rgb(0xF1F1EE), true, seed + 7);
            MapKit.Boat(c, new Vector2(-3.4f, -4.6f), 40f, 0.38f, 0.14f, MapKit.Rgb(0xF5F5F1), MapKit.Rgb(0xC9A979), MapKit.Rgb(0xF1F1EE), true, seed + 8);
            MapKit.Boat(c, new Vector2(10.6f, -4.8f), 120f, 0.5f, 0.17f, MapKit.Rgb(0xF5F5F1), MapKit.Rgb(0xC9A979), MapKit.Rgb(0xF1F1EE), true, seed + 9);
        }
    }
}
