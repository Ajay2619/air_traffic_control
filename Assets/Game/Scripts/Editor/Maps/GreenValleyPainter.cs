using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Green Valley: patchwork farmland with hedgerows, woods, a pond, a winding country road, a farmstead and a
    /// small grass airfield with a hangar apron. Decor near spots is derived from the map's spot positions.
    /// </summary>
    internal static class GreenValleyPainter
    {
        private const float FieldAngle = 14f;
        private const float CellU = 1.75f;
        private const float CellV = 1.25f;
        private const float HedgeHalf = 0.04f;

        private static readonly Color AirfieldGrass = MapKit.Rgb(0x8CB45E);
        private static readonly Color HedgeGreen = MapKit.Rgb(0x46682F);
        private static readonly Color FieldMargin = MapKit.Rgb(0xA5A16E);
        private static readonly Color Concrete = MapKit.Rgb(0xB4B3AA);
        private static readonly Color RoadEdge = MapKit.Rgb(0x857C66);
        private static readonly Color RoadTop = MapKit.Rgb(0xA8A08A);
        private static readonly Color Dirt = MapKit.Rgb(0xA89270);

        private static readonly Color[] FieldColors =
        {
            MapKit.Rgb(0x8DB65B), MapKit.Rgb(0x7AA54F), MapKit.Rgb(0x6C9A48), MapKit.Rgb(0xA3BF62), MapKit.Rgb(0xC4BE6C),
            MapKit.Rgb(0xD3C07E), MapKit.Rgb(0xA88D65), MapKit.Rgb(0x8DA75C), MapKit.Rgb(0x80B064),
        };
        private static readonly bool[] FieldHasRows = { false, false, true, true, true, true, true, false, false };

        private static readonly Color[] TreeColors =
        {
            MapKit.Rgb(0x4A7A32), MapKit.Rgb(0x568A38), MapKit.Rgb(0x426E30), MapKit.Rgb(0x5F8C3A), MapKit.Rgb(0x3E6A35),
        };

        private static readonly Vector2[] MainRoad =
        {
            new Vector2(-13.6f, 2.5f), new Vector2(-10.8f, 3.1f), new Vector2(-8.7f, 2.7f), new Vector2(-6.9f, 3.7f),
            new Vector2(-4.4f, 4.25f), new Vector2(-1.6f, 3.95f), new Vector2(0.9f, 3.3f), new Vector2(2.7f, 3.8f),
            new Vector2(4.8f, 4.1f), new Vector2(6.9f, 3.5f), new Vector2(8.4f, 2.4f), new Vector2(9.1f, 0.4f),
            new Vector2(8.3f, -1.3f), new Vector2(8.7f, -3.3f), new Vector2(9.9f, -4.9f), new Vector2(10.9f, -7.8f),
        };

        private static readonly Vector2[] SouthRoad =
        {
            new Vector2(3.3f, -7.8f), new Vector2(4.3f, -5.7f), new Vector2(5.5f, -4.2f), new Vector2(6.3f, -3.15f),
        };

        private static readonly Vector2[] FarmTrack =
        {
            new Vector2(8.45f, -1.55f), new Vector2(7.7f, -1.95f), new Vector2(6.9f, -2.25f),
        };

        private static readonly Vector2 PondCenter = new Vector2(2.9f, -3.0f);
        private static readonly Vector2 PondRadii = new Vector2(0.95f, 0.62f);
        private static readonly Vector2 FarmCenter = new Vector2(6.85f, -2.55f);

        private sealed class FieldNoise
        {
            public MapNoiseField Low, WarpA, WarpB, Mid, Hedge, Gaps;
            public float Cos, Sin;
            public int Seed;
        }

        public static void Paint(PixelCanvas c, MapDefinition def, int seed)
        {
            LandingSpotDef longRw = MapKit.FindSpot(def, LandingKind.LongRunway);
            LandingSpotDef pad = MapKit.FindSpot(def, LandingKind.Helipad);
            Vector2 padPos = pad != null ? pad.position : new Vector2(-3.4f, 2.5f);
            Rect apron = Rect.MinMaxRect(padPos.x - 2.3f, padPos.y - 0.85f, padPos.x + 1.55f, padPos.y + 0.9f);
            Vector2 taxiA = new Vector2(apron.xMax - 0.75f, apron.yMin + 0.1f);
            Vector2 taxiB = longRw != null ? MapKit.ClosestOnCenterline(longRw, taxiA) : taxiA;

            // Airfield zones: mowed grass instead of fields.
            List<MapZone> zoneList = new List<MapZone>();
            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots)
                {
                    if (s == null) continue;
                    zoneList.Add(MapZone.AroundSpot(s, LandingColors.IsDirectional(s.kind) ? 0.85f : 0.7f, RunwayPainter.StubLength + 0.25f));
                }
            }
            zoneList.Add(MapZone.FromRect(apron, 0.45f));
            if (longRw != null) zoneList.Add(MapZone.Segment(taxiA, taxiB, 0.55f));
            MapZone[] zones = zoneList.ToArray();

            FieldNoise f = new FieldNoise
            {
                Low = new MapNoiseField(c.Width, c.Height, 520f, 4, seed + 1),
                WarpA = new MapNoiseField(c.Width, c.Height, 260f, 2, seed + 2),
                WarpB = new MapNoiseField(c.Width, c.Height, 260f, 2, seed + 3),
                Mid = new MapNoiseField(c.Width, c.Height, 70f, 3, seed + 4),
                Hedge = new MapNoiseField(c.Width, c.Height, 14f, 2, seed + 5, 2),
                Gaps = new MapNoiseField(c.Width, c.Height, 150f, 2, seed + 6),
                Cos = Mathf.Cos(FieldAngle * Mathf.Deg2Rad),
                Sin = Mathf.Sin(FieldAngle * Mathf.Deg2Rad),
                Seed = seed,
            };
            Terrain(c, f, zones, longRw != null ? longRw.Direction : Vector2.right);

            // Farm yard, roads, pond.
            FarmYard(c, seed);
            List<Vector2> mainRoad = MapKit.Spline(MainRoad, 12);
            List<Vector2> southRoad = MapKit.Spline(SouthRoad, 10);
            List<Vector2> farmTrack = MapKit.Spline(FarmTrack, 8);
            List<Vector2> spur = new List<Vector2>
            {
                new Vector2(apron.center.x + 0.3f, apron.yMax + 0.95f), new Vector2(apron.center.x + 0.3f, apron.yMax - 0.05f),
            };
            DrawRoad(c, farmTrack, 0.16f, Dirt, Dirt);
            DrawRoad(c, southRoad, 0.24f, RoadEdge, RoadTop);
            DrawRoad(c, spur, 0.22f, RoadEdge, RoadTop);
            DrawRoad(c, mainRoad, 0.28f, RoadEdge, RoadTop);

            MapKit.Pond(c, PondCenter, PondRadii, -12f, MapKit.Rgb(0x6FA7A2), MapKit.Rgb(0x3D7586), MapKit.Rgb(0x8C8460), 0.09f, seed + 11);
            PondPlants(c, seed + 12);

            // Airfield surfaces, then the spots.
            MapKit.Slab(c, apron.center, apron.size, 0f, 0.12f, Concrete, 0.45f, seed + 13);
            if (longRw != null)
            {
                List<Vector2> taxi = new List<Vector2> { MapKit.Px(taxiA), MapKit.Px(taxiB) };
                c.StrokePolyline(taxi, MapKit.Len(0.4f), MapKit.Shade(Concrete, 0.8f));
                c.StrokePolyline(taxi, MapKit.Len(0.33f), Concrete);
            }
            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots) RunwayPainter.Paint(c, s, RunwaySurface.Asphalt, true, seed + 20);
            }

            // Buildings around the apron (relative to the helipad) and the farmstead.
            MapKit.Building(c, padPos + new Vector2(-1.45f, 0.05f), new Vector2(1.15f, 1.0f), 0f, MapKit.Rgb(0xB9C0C2), MapRoof.Arched, 0.3f, seed + 30);
            MapKit.Building(c, padPos + new Vector2(1.1f, 0.48f), new Vector2(0.6f, 0.45f), 0f, MapKit.Rgb(0xD8D2C2), MapRoof.Flat, 0.2f, seed + 31);
            Tank(c, padPos + new Vector2(1.25f, -0.5f), 0.11f, seed + 32);
            Tank(c, padPos + new Vector2(1.25f, -0.22f), 0.11f, seed + 33);
            Farmstead(c, seed + 40);

            // Trees and sheep.
            System.Random rng = new System.Random(seed);
            List<MapTree> trees = new List<MapTree>();
            PlantWoods(trees, rng, zones, mainRoad, southRoad);
            PlantHedgeTrees(trees, rng, f, zones, mainRoad, southRoad);
            MapKit.DrawTrees(c, trees, seed + 50);
            Sheep(c, rng, zones, mainRoad);
        }

        // ------------------------------------------------------------------------------------------------ terrain

        private static void Terrain(PixelCanvas c, FieldNoise f, MapZone[] zones, Vector2 mowAxis)
        {
            Vector2 mowN = MapKit.Perp(mowAxis);
            for (int y = 0; y < c.Height; y++)
            {
                float wy = MapKit.WorldY(y);
                for (int x = 0; x < c.Width; x++)
                {
                    float wx = MapKit.WorldX(x);
                    Color col = FieldColor(f, x, y, wx, wy);
                    float az = MapZone.Min(zones, wx, wy) + (f.WarpA.Sample(x, y) - 0.5f) * 0.45f + (f.Mid.Sample(x, y) - 0.5f) * 0.12f;
                    if (az < 0.2f)
                    {
                        float m = Mathf.Sin((wx * mowN.x + wy * mowN.y) * (Mathf.PI / 0.42f));
                        Color grass = MapKit.Shade(AirfieldGrass, 1f + 0.035f * Mathf.Clamp(m * 3f, -1f, 1f));
                        col = MapKit.Mix(grass, col, MapKit.Smooth(-0.04f, 0.04f, az));
                        float edge = Mathf.Clamp01((0.04f - Mathf.Abs(az - 0.045f)) * MapKit.Ppu + 0.5f);
                        if (edge > 0f) col = MapKit.Mix(col, MapKit.Shade(HedgeGreen, 0.8f + 0.45f * f.Hedge.Sample(x, y)), edge * 0.9f);
                    }
                    float low = f.Low.Sample(x, y);
                    float relief = (low - f.Low.Sample(x - 6f, y + 8f)) * 9f;
                    float k = (0.9f + 0.2f * low) * (1f + relief) * (1f + (f.Mid.Sample(x, y) - 0.5f) * 0.1f);
                    c[x, y] = MapKit.Shade(col, k);
                }
            }
        }

        /// <summary>Patchwork field color with crop rows, hedgerows (with gaps), field margins and hedge shadows.</summary>
        private static Color FieldColor(FieldNoise f, int x, int y, float wx, float wy)
        {
            float wxw = wx + (f.WarpA.Sample(x, y) - 0.5f) * 1.2f;
            float wyw = wy + (f.WarpB.Sample(x, y) - 0.5f) * 1.2f;
            float fu = wxw * f.Cos + wyw * f.Sin;
            float fv = -wxw * f.Sin + wyw * f.Cos;
            int col = Mathf.FloorToInt(fu / CellU);
            float fvo = fv + MapKit.Hash01(col, 3, f.Seed) * CellV;
            int row = Mathf.FloorToInt(fvo / CellV);
            float lu = fu - col * CellU;
            float lv = fvo - row * CellV;
            uint h = MapKit.Hash(col, row, f.Seed);

            // Hedges at the cell borders cast shadows toward +u (weak) and -v (strong) in field space.
            float border = Mathf.Min(Mathf.Min(lu, CellU - lu), Mathf.Min(lv, CellV - lv));
            float shadow = Mathf.Max(0.39f * (1f - MapKit.Smooth(0.02f, 0.15f, lu)), 0.92f * (1f - MapKit.Smooth(0.02f, 0.15f, CellV - lv)));
            int sub = 0;
            uint split = h % 4u;
            float s = 0.36f + 0.28f * (((h >> 8) & 255u) / 255f);
            if (split == 1u)
            {
                float su = s * CellU;
                border = Mathf.Min(border, Mathf.Abs(lu - su));
                if (lu >= su)
                {
                    sub = 1;
                    shadow = Mathf.Max(shadow, 0.39f * (1f - MapKit.Smooth(0.02f, 0.15f, lu - su)));
                }
            }
            else if (split == 2u)
            {
                float sv = s * CellV;
                border = Mathf.Min(border, Mathf.Abs(lv - sv));
                if (lv >= sv) sub = 1;
                else shadow = Mathf.Max(shadow, 0.92f * (1f - MapKit.Smooth(0.02f, 0.15f, sv - lv)));
            }

            uint fh = MapKit.Hash(col * 2 + sub, row * 2 + sub, f.Seed + 101);
            int idx = (int)(fh % (uint)FieldColors.Length);
            float k = 0.95f + 0.1f * (((fh >> 16) & 255u) / 255f);
            if (FieldHasRows[idx])
            {
                float coord = ((fh >> 12) & 1u) == 0u ? fu : fv;
                k *= 1f + 0.06f * Mathf.Sin(coord * (Mathf.PI * 2f / 0.12f));
            }
            else
            {
                k *= 1f + (MapKit.Hash01(x, y, f.Seed) - 0.5f) * 0.06f;
            }
            Color field = MapKit.Shade(FieldColors[idx], k);

            float hedgePresent = MapKit.Smooth(0.4f, 0.47f, f.Gaps.Sample(x, y));
            float bump = f.Hedge.Sample(x, y);
            float hedgeHalf = HedgeHalf * (0.7f + 0.65f * bump);
            float hedgeCov = Mathf.Clamp01((hedgeHalf - border) * MapKit.Ppu + 0.5f) * hedgePresent;
            float marginCov = Mathf.Clamp01((0.022f - border) * MapKit.Ppu + 0.5f) * (1f - hedgePresent);
            field = MapKit.Shade(field, 1f - 0.22f * shadow * hedgePresent);
            if (marginCov > 0f) field = MapKit.Mix(field, FieldMargin, marginCov * 0.8f);
            if (hedgeCov > 0f) field = MapKit.Mix(field, MapKit.Shade(HedgeGreen, 0.75f + 0.55f * bump), hedgeCov);
            return field;
        }

        /// <summary>True when the world point lies on a visible hedgerow (same math as <see cref="FieldColor"/>).</summary>
        private static bool OnHedge(FieldNoise f, Vector2 w)
        {
            Vector2 p = MapKit.Px(w);
            float wxw = w.x + (f.WarpA.Sample(p.x, p.y) - 0.5f) * 1.2f;
            float wyw = w.y + (f.WarpB.Sample(p.x, p.y) - 0.5f) * 1.2f;
            float fu = wxw * f.Cos + wyw * f.Sin;
            float fv = -wxw * f.Sin + wyw * f.Cos;
            int col = Mathf.FloorToInt(fu / CellU);
            float fvo = fv + MapKit.Hash01(col, 3, f.Seed) * CellV;
            int row = Mathf.FloorToInt(fvo / CellV);
            float lu = fu - col * CellU, lv = fvo - row * CellV;
            float border = Mathf.Min(Mathf.Min(lu, CellU - lu), Mathf.Min(lv, CellV - lv));
            return border < 0.03f && f.Gaps.Sample(p.x, p.y) > 0.47f;
        }

        // ------------------------------------------------------------------------------------------------ features

        private static void DrawRoad(PixelCanvas c, List<Vector2> world, float widthW, Color edge, Color top)
        {
            List<Vector2> px = MapKit.ToPx(world);
            c.StrokePolyline(px, MapKit.Len(widthW) + 3f, MapKit.Alpha(edge, 0.85f));
            c.StrokePolyline(px, MapKit.Len(widthW) - 1f, p => MapKit.Shade(top, 1f + (Mathf.PerlinNoise(p.x / 9f, p.y / 9f) - 0.5f) * 0.12f));
        }

        private static void FarmYard(PixelCanvas c, int seed)
        {
            Vector2 cp = MapKit.Px(FarmCenter);
            float r = MapKit.Len(0.95f);
            float ox = MapKit.SeedOffset(seed, 5), oy = MapKit.SeedOffset(seed, 6);
            c.FillSdf(p =>
            {
                Vector2 q = p - cp;
                float wob = 1f + 0.12f * (Mathf.PerlinNoise(q.x / 40f + ox, q.y / 40f + oy) - 0.5f) * 2f;
                return new Vector2(q.x, q.y * 1.35f).magnitude - r * wob;
            }, PixelCanvas.Square(cp, r * 1.2f), p => MapKit.Shade(Dirt, 1f + (Mathf.PerlinNoise(p.x / 7f + ox, p.y / 7f) - 0.5f) * 0.16f));
        }

        private static void Farmstead(PixelCanvas c, int seed)
        {
            Vector2 fc = FarmCenter;
            MapKit.Building(c, fc + new Vector2(-0.55f, 0.32f), new Vector2(0.8f, 0.5f), 8f, MapKit.Rgb(0x6F7480), MapRoof.Gable, 0.22f, seed);
            MapKit.Building(c, fc + new Vector2(0.4f, -0.18f), new Vector2(1.15f, 0.62f), 8f, MapKit.Rgb(0x8A5A44), MapRoof.Gable, 0.3f, seed + 1);
            MapKit.Building(c, fc + new Vector2(-0.35f, -0.55f), new Vector2(0.5f, 0.34f), 8f, MapKit.Rgb(0x9C9A92), MapRoof.Flat, 0.16f, seed + 2);
            Tank(c, fc + new Vector2(1.2f, 0.25f), 0.17f, seed + 3);
            Tank(c, fc + new Vector2(1.2f, 0.62f), 0.14f, seed + 4);
        }

        /// <summary>Round silo / fuel tank: cast shadow + shaded dome.</summary>
        private static void Tank(PixelCanvas c, Vector2 w, float radiusW, int seed)
        {
            Vector2 p = MapKit.Px(w);
            float r = MapKit.Len(radiusW);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowOffset(radiusW * 1.6f), 2f, 0.4f, true);
            MapKit.Blob(c, p, r, MapKit.Rgb(0xD5D7D6), seed, 0f, 0.05f);
            c.StrokeCircle(p, r * 0.55f, 1f, new Color(0.55f, 0.57f, 0.58f, 0.5f));
        }

        private static void PondPlants(PixelCanvas c, int seed)
        {
            System.Random rng = new System.Random(seed);
            // reeds along the north-west bank
            for (int i = 0; i < 26; i++)
            {
                float a = MapKit.Range(rng, 95f, 215f) * Mathf.Deg2Rad;
                Vector2 w = PondCenter + new Vector2(Mathf.Cos(a) * PondRadii.x * 1.02f, Mathf.Sin(a) * PondRadii.y * 1.02f);
                w = PondCenter + Sdf.Rotate(w - PondCenter, -12f);
                MapKit.Blob(c, MapKit.Px(w), MapKit.Len(MapKit.Range(rng, 0.05f, 0.09f)), MapKit.Rgb(0x5E7E3A), seed + i, 0.25f, 0.6f);
            }
            // lily pads
            for (int i = 0; i < 9; i++)
            {
                Vector2 w = PondCenter + new Vector2(MapKit.Range(rng, -0.5f, 0.5f), MapKit.Range(rng, -0.3f, 0.3f));
                Vector2 p = MapKit.Px(w);
                float r = MapKit.Len(MapKit.Range(rng, 0.035f, 0.06f));
                c.FillCircle(p, r, MapKit.Rgb(0x6E9A4A));
                c.FillCircle(p + new Vector2(-r * 0.25f, r * 0.25f), r * 0.55f, MapKit.Rgb(0x86B05A));
            }
        }

        // ------------------------------------------------------------------------------------------------ trees

        private static bool Clear(Vector2 w, MapZone[] zones, List<Vector2> roadA, List<Vector2> roadB, float zoneMargin)
        {
            if (MapZone.Min(zones, w.x, w.y) < zoneMargin) return false;
            if (MapKit.DistToPolyline(w, roadA) < 0.3f || MapKit.DistToPolyline(w, roadB) < 0.28f) return false;
            Vector2 pq = w - PondCenter;
            if ((pq.x * pq.x) / (1.25f * 1.25f) + (pq.y * pq.y) / (0.9f * 0.9f) < 1f) return false;
            if ((w - FarmCenter).sqrMagnitude < 1.25f * 1.25f) return false;
            return true;
        }

        private static void PlantWoods(List<MapTree> trees, System.Random rng, MapZone[] zones, List<Vector2> roadA, List<Vector2> roadB)
        {
            Vector3[] woods =
            {
                new Vector3(-8.3f, 0.6f, 0.75f), new Vector3(-0.7f, 1.5f, 0.5f), new Vector3(1.8f, -4.0f, 0.6f),
                new Vector3(4.5f, -1.1f, 0.45f), new Vector3(-6.3f, -4.5f, 0.8f), new Vector3(0.6f, 4.8f, 0.7f),
                new Vector3(10.6f, 2.6f, 0.9f), new Vector3(-11.0f, -2.6f, 1.0f), new Vector3(-11.6f, 5.6f, 0.8f),
                new Vector3(11.2f, -5.8f, 0.9f), new Vector3(-2.8f, -5.7f, 0.8f), new Vector3(6.0f, 5.7f, 0.8f),
                new Vector3(-9.6f, 4.9f, 0.6f), new Vector3(11.6f, 0.2f, 0.7f), new Vector3(3.6f, -6.4f, 0.7f),
            };
            foreach (Vector3 wd in woods)
            {
                int count = Mathf.RoundToInt(wd.z * wd.z * 40f) + 6;
                Vector2 center = new Vector2(wd.x, wd.y);
                for (int i = 0, attempts = 0; i < count && attempts < count * 5; attempts++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = wd.z * Mathf.Sqrt((float)rng.NextDouble()) * 1.25f;
                    Vector2 w = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f) * rad;
                    if (!Clear(w, zones, roadA, roadB, 0.3f)) continue;
                    trees.Add(new MapTree(w, MapKit.Range(rng, 0.15f, 0.25f), TreeColors[rng.Next(TreeColors.Length)]));
                    i++;
                }
            }
            // A few trees around the farmstead.
            for (int i = 0; i < 7; i++)
            {
                float ang = MapKit.Range(rng, 0f, Mathf.PI * 2f);
                Vector2 w = FarmCenter + new Vector2(Mathf.Cos(ang) * 1.45f, Mathf.Sin(ang) * 1.05f);
                if (MapZone.Min(zones, w.x, w.y) < 0.3f || MapKit.DistToPolyline(w, roadA) < 0.3f) continue;
                trees.Add(new MapTree(w, MapKit.Range(rng, 0.16f, 0.22f), TreeColors[rng.Next(TreeColors.Length)]));
            }
        }

        private static void PlantHedgeTrees(List<MapTree> trees, System.Random rng, FieldNoise f, MapZone[] zones,
            List<Vector2> roadA, List<Vector2> roadB)
        {
            int placed = 0;
            for (int i = 0; i < 9000 && placed < 150; i++)
            {
                Vector2 w = new Vector2(MapKit.Range(rng, -12.6f, 12.6f), MapKit.Range(rng, -7.0f, 7.0f));
                if (!OnHedge(f, w) || !Clear(w, zones, roadA, roadB, 0.35f)) continue;
                trees.Add(new MapTree(w, MapKit.Range(rng, 0.1f, 0.17f), TreeColors[rng.Next(TreeColors.Length)]));
                placed++;
            }
        }

        private static void Sheep(PixelCanvas c, System.Random rng, MapZone[] zones, List<Vector2> road)
        {
            Vector2[] flocks = { new Vector2(-7.6f, -3.4f), new Vector2(-1.6f, -3.5f), new Vector2(4.6f, -0.2f), new Vector2(-9.0f, 1.9f) };
            foreach (Vector2 fc in flocks)
            {
                int n = 6 + rng.Next(5);
                for (int i = 0; i < n; i++)
                {
                    Vector2 w = fc + new Vector2(MapKit.Range(rng, -0.45f, 0.45f), MapKit.Range(rng, -0.3f, 0.3f));
                    if (MapZone.Min(zones, w.x, w.y) < 0.3f || MapKit.DistToPolyline(w, road) < 0.3f) continue;
                    Vector2 p = MapKit.Px(w);
                    c.FillCircle(p + MapKit.ShadowDir * 1.6f, 2.4f, new Color(0.05f, 0.08f, 0.1f, 0.3f));
                    c.FillEllipse(p, new Vector2(2.6f, 1.9f), MapKit.Range(rng, 0f, 180f), new Color(0.95f, 0.94f, 0.9f, 1f));
                    c.FillCircle(p + new Vector2(1.6f, 0.4f), 0.9f, new Color(0.3f, 0.28f, 0.26f, 1f));
                }
            }
        }
    }
}
