using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Dune Sea: rippled sand dunes with relief shading, terraced red mesas, a dry cracked riverbed, an oasis with
    /// palms beside the helipad, graded runway strips, dirt tracks, rocks and dry shrubs.
    /// </summary>
    internal static class DuneSeaPainter
    {
        private static readonly Color SandLight = MapKit.Rgb(0xF1D49E);
        private static readonly Color SandDark = MapKit.Rgb(0xCFA169);
        private static readonly Color Graded = MapKit.Rgb(0xDDB987);
        private static readonly Color WetSand = MapKit.Rgb(0xB9976B);
        private static readonly Color OasisGrass = MapKit.Rgb(0x7F9C48);
        private static readonly Color Clay = MapKit.Rgb(0xE2CCA6);
        private static readonly Color ClayDark = MapKit.Rgb(0xB89C77);
        private static readonly Color Track = MapKit.Rgb(0xC7A57A);
        private static readonly Color MesaTop = MapKit.Rgb(0xCB8C63);
        private static readonly Color MesaCliff = MapKit.Rgb(0xA5603F);
        private static readonly Color Scree = MapKit.Rgb(0xB98660);

        private static readonly Color[] RockColors = { MapKit.Rgb(0x8E6A4E), MapKit.Rgb(0x7B5B45), MapKit.Rgb(0xA27A5A), MapKit.Rgb(0x96765F) };
        private static readonly Color[] ShrubColors = { MapKit.Rgb(0x7D8146), MapKit.Rgb(0x6F7541), MapKit.Rgb(0x8A8A50) };

        private static readonly Vector2[] Riverbed =
        {
            new Vector2(-0.4f, 7.8f), new Vector2(-1.0f, 5.2f), new Vector2(-2.3f, 3.4f), new Vector2(-1.5f, 1.4f),
            new Vector2(-2.0f, -0.6f), new Vector2(-3.2f, -2.4f), new Vector2(-2.8f, -4.4f), new Vector2(-4.0f, -7.8f),
        };

        // x, y, radius x, radius y (world); angle and height in the parallel arrays below.
        private static readonly Vector4[] Mesas =
        {
            new Vector4(1.6f, 3.75f, 1.55f, 0.8f), new Vector4(-7.3f, -3.5f, 1.9f, 1.0f), new Vector4(-10.8f, 1.4f, 0.75f, 0.55f),
            new Vector4(9.4f, -4.7f, 0.95f, 0.6f), new Vector4(11.0f, 3.4f, 1.3f, 0.85f), new Vector4(-11.4f, 5.7f, 1.5f, 0.9f),
            new Vector4(4.6f, -6.0f, 1.1f, 0.5f), new Vector4(-5.0f, 6.0f, 1.0f, 0.55f), new Vector4(7.6f, 4.65f, 0.45f, 0.35f),
            new Vector4(-1.4f, -6.0f, 0.6f, 0.4f),
        };
        private static readonly float[] MesaAngles = { 8f, -12f, 20f, -5f, 15f, 0f, 10f, -10f, 0f, 25f };
        private static readonly float[] MesaHeights = { 0.34f, 0.4f, 0.28f, 0.3f, 0.35f, 0.35f, 0.25f, 0.3f, 0.22f, 0.24f };

        public static void Paint(PixelCanvas c, MapDefinition def, int seed)
        {
            LandingSpotDef longRw = MapKit.FindSpot(def, LandingKind.LongRunway);
            LandingSpotDef shortRw = MapKit.FindSpot(def, LandingKind.ShortRunway);
            LandingSpotDef pad = MapKit.FindSpot(def, LandingKind.Helipad);
            Vector2 padPos = pad != null ? pad.position : new Vector2(4.4f, 2.6f);
            Vector2 oasis = padPos + new Vector2(1.9f, -0.6f);
            Vector2 oasisRadii = new Vector2(1.0f, 0.62f);

            // Apron south of the main runway (parallel to it) and a hut beside the short strip.
            bool hasApron = longRw != null;
            float apronAngle = 0f;
            Vector2 apronC = Vector2.zero, taxiA = Vector2.zero, taxiB = Vector2.zero;
            if (hasApron)
            {
                Vector2 d = longRw.Direction;
                Vector2 left = MapKit.Perp(d);
                apronAngle = longRw.angleDeg + 180f;
                apronC = longRw.position + d * (longRw.length * 0.55f) + left * 1.6f;
                taxiA = apronC - left * 0.45f;
                taxiB = MapKit.ClosestOnCenterline(longRw, taxiA);
            }
            Vector2 hut = Vector2.zero;
            bool hasHut = shortRw != null;
            if (hasHut)
                hut = shortRw.position + shortRw.Direction * (shortRw.length * 0.4f) + MapKit.Perp(shortRw.Direction) * 0.95f;

            List<MapZone> graded = new List<MapZone>();
            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots)
                {
                    if (s != null) graded.Add(MapZone.AroundSpot(s, 0.42f, RunwayPainter.StubLength + 0.2f));
                }
            }
            if (hasApron)
            {
                graded.Add(new MapZone(apronC, apronAngle, 1.65f, 0.8f, 0.3f));
                graded.Add(MapZone.Segment(taxiA, taxiB, 0.32f));
            }
            if (hasHut) graded.Add(MapZone.Circle(hut, 0.55f));
            MapZone[] zones = graded.ToArray();

            Terrain(c, seed, zones, oasis, oasisRadii);

            List<Vector2> river = MapKit.Spline(Riverbed, 12);
            PaintRiverbed(c, river, seed + 3);

            List<Vector2> trackA = MapKit.Spline(new[]
            {
                new Vector2(13.6f, 0.2f), new Vector2(9.4f, 0.5f), new Vector2(7.4f, 0.9f), new Vector2(5.6f, 1.15f),
                new Vector2(4.4f, 1.6f), new Vector2(padPos.x, padPos.y - 0.6f),
            }, 10);
            List<Vector2> trackB = MapKit.Spline(new[]
            {
                new Vector2(5.6f, 1.15f), new Vector2(3.4f, 0.75f), new Vector2(1.6f, 0.55f), new Vector2(-0.4f, 0.75f),
                new Vector2(-1.8f, 0.95f), new Vector2(-3.0f, 1.6f), hasHut ? hut + new Vector2(0.35f, 0.05f) : new Vector2(-4.5f, 2.2f),
            }, 10);
            List<Vector2> trackC = hasApron
                ? MapKit.Spline(new[] { apronC + new Vector2(0f, -0.45f), apronC + new Vector2(-0.5f, -1.6f), apronC + new Vector2(-1.4f, -4.5f) }, 10)
                : new List<Vector2>();
            PaintTrack(c, trackA, seed + 4);
            PaintTrack(c, trackB, seed + 5);
            if (trackC.Count > 1) PaintTrack(c, trackC, seed + 6);

            for (int i = 0; i < Mesas.Length; i++)
            {
                Vector4 m = Mesas[i];
                PaintMesa(c, new Vector2(m.x, m.y), new Vector2(m.z, m.w), MesaAngles[i], MesaHeights[i], seed + 100 + i);
            }

            MapKit.Pond(c, oasis, oasisRadii, 10f, MapKit.Rgb(0x5DB0A8), MapKit.Rgb(0x2E7F8C), MapKit.Rgb(0x8E8A55), 0.1f, seed + 7);

            // Airfield.
            if (hasApron)
            {
                List<Vector2> taxi = new List<Vector2> { MapKit.Px(taxiA), MapKit.Px(taxiB) };
                c.StrokePolyline(taxi, MapKit.Len(0.36f), MapKit.Rgb(0x5C5A57));
                c.StrokePolyline(taxi, MapKit.Len(0.3f), MapKit.Rgb(0x6B6964));
                MapKit.Slab(c, apronC, new Vector2(2.6f, 0.95f), apronAngle, 0.1f, MapKit.Rgb(0x8A8780), 0.5f, seed + 8, 0.22f);
            }
            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots) RunwayPainter.Paint(c, s, RunwaySurface.Asphalt, true, seed + 20);
            }
            if (hasApron)
            {
                MapKit.Building(c, MapKit.Local(apronC, apronAngle, -0.6f, -0.05f), new Vector2(0.95f, 0.75f), apronAngle, MapKit.Rgb(0xC9C2B0), MapRoof.Arched, 0.3f, seed + 30);
                MapKit.Building(c, MapKit.Local(apronC, apronAngle, 0.6f, -0.12f), new Vector2(0.55f, 0.42f), apronAngle, MapKit.Rgb(0xE3D7BF), MapRoof.Flat, 0.2f, seed + 31);
                Drum(c, MapKit.Local(apronC, apronAngle, 1.1f, 0.22f), 0.09f, seed + 32);
                Drum(c, MapKit.Local(apronC, apronAngle, 1.1f, -0.02f), 0.09f, seed + 33);
            }
            if (hasHut)
            {
                float ang = shortRw.angleDeg;
                MapKit.Building(c, hut, new Vector2(0.42f, 0.32f), ang, MapKit.Rgb(0xD9C7A6), MapRoof.Flat, 0.16f, seed + 34);
                Drum(c, hut + MapKit.Local(Vector2.zero, ang, 0.38f, 0.1f), 0.05f, seed + 35);
                Drum(c, hut + MapKit.Local(Vector2.zero, ang, 0.38f, -0.06f), 0.05f, seed + 36);
            }

            // Oasis village: adobe houses and tents near the pad.
            MapKit.Building(c, padPos + new Vector2(-0.95f, 0.95f), new Vector2(0.6f, 0.45f), 4f, MapKit.Rgb(0xE6D6B8), MapRoof.Flat, 0.2f, seed + 40);
            MapKit.Building(c, padPos + new Vector2(-0.2f, 1.1f), new Vector2(0.45f, 0.4f), -6f, MapKit.Rgb(0xDCC8A4), MapRoof.Flat, 0.18f, seed + 41);
            MapKit.Building(c, padPos + new Vector2(0.95f, 1.05f), new Vector2(0.42f, 0.3f), 20f, MapKit.Rgb(0xF2EDE2), MapRoof.Gable, 0.14f, seed + 42);
            MapKit.Building(c, padPos + new Vector2(1.5f, 1.3f), new Vector2(0.38f, 0.28f), -10f, MapKit.Rgb(0xEDE3CF), MapRoof.Gable, 0.13f, seed + 43);

            System.Random rng = new System.Random(seed);
            Func<Vector2, float, bool> clear = (w, margin) =>
            {
                if (MapZone.Min(zones, w.x, w.y) < margin) return false;
                if ((w - padPos).sqrMagnitude < 0.85f * 0.85f) return false;
                Vector2 o = w - oasis;
                if ((o.x * o.x) / (oasisRadii.x * oasisRadii.x) + (o.y * o.y) / (oasisRadii.y * oasisRadii.y) < 1.15f) return false;
                return MapKit.DistToPolyline(w, river) > 0.32f;
            };

            Scatter(c, rng, clear, oasis, oasisRadii, river, seed + 200);

            // Palms around the oasis.
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f + MapKit.Range(rng, -0.12f, 0.12f);
                float r = MapKit.Range(rng, 1.12f, 1.55f);
                Vector2 w = oasis + new Vector2(Mathf.Cos(a) * oasisRadii.x * r, Mathf.Sin(a) * oasisRadii.y * r);
                if ((w - padPos).sqrMagnitude < 0.8f * 0.8f) continue;
                MapKit.Palm(c, w, MapKit.Range(rng, 0.2f, 0.28f), seed + 300 + i);
            }
        }

        // ------------------------------------------------------------------------------------------------ terrain

        private static void Terrain(PixelCanvas c, int seed, MapZone[] zones, Vector2 oasis, Vector2 oasisRadii)
        {
            MapNoiseField warpA = new MapNoiseField(c.Width, c.Height, 380f, 3, seed + 1);
            MapNoiseField warpB = new MapNoiseField(c.Width, c.Height, 120f, 2, seed + 2);
            MapNoiseField amp = new MapNoiseField(c.Width, c.Height, 300f, 2, seed + 3);
            MapNoiseField patch = new MapNoiseField(c.Width, c.Height, 170f, 3, seed + 4);
            MapNoiseField fine = new MapNoiseField(c.Width, c.Height, 26f, 2, seed + 5, 2);

            // Transverse dunes: crests run across the wind; gentle sunlit windward slopes rise to a crisp crest,
            // steep lee slopes fall away in shade. Crests meander with the warp noise.
            Vector2 wind = MapKit.Dir(-25f);
            const float wavelength = 1.7f;
            const float crestAt = 0.74f;
            const float crestSoft = 0.008f;
            Vector2 ripDir = MapKit.Dir(-15f);
            float ripFreq = Mathf.PI * 2f / 0.15f;
            for (int y = 0; y < c.Height; y++)
            {
                float wy = MapKit.WorldY(y);
                for (int x = 0; x < c.Width; x++)
                {
                    float wx = MapKit.WorldX(x);
                    float wb = warpB.Sample(x, y);
                    float s = (wx * wind.x + wy * wind.y) / wavelength + (warpA.Sample(x, y) - 0.5f) * 1.7f + (wb - 0.5f) * 0.4f;
                    float t = s - Mathf.Floor(s);
                    float a = MapKit.Smooth(0.36f, 0.6f, amp.Sample(x, y));
                    float w = Mathf.Min(t, crestAt) / crestAt;
                    float l = Mathf.Max(0f, t - crestAt) / (1f - crestAt);
                    float windward = 1f + 0.09f * a * w * w;
                    float lee = 1f - 0.16f * a * Mathf.Pow(1f - l, 1.6f);
                    float leeT = MapKit.Smooth(crestAt - crestSoft, crestAt + crestSoft, t) * (1f - MapKit.Smooth(1f - crestSoft, 1f, t));
                    float shade = Mathf.Lerp(windward, lee, leeT);

                    float p = patch.Sample(x, y);
                    float fn = fine.Sample(x, y);
                    Color col = MapKit.Mix(SandDark, SandLight, MapKit.Smooth(0.28f, 0.72f, p));
                    col = MapKit.Mix(col, SandDark, leeT * a * 0.35f);

                    float gz = MapZone.Min(zones, wx, wy);
                    float gradedT = 1f - MapKit.Smooth(-0.06f, 0.22f, gz);
                    float rm = a * (1f - leeT) * (1f - gradedT) * MapKit.Smooth(0.2f, 0.6f, w);
                    float ripple = Mathf.Sin((wx * ripDir.x + wy * ripDir.y) * ripFreq + wb * 20f);
                    float k = Mathf.Lerp(shade, 1f, gradedT) * (1f + 0.04f * ripple * rm) * (1f + (fn - 0.5f) * 0.1f);
                    col = MapKit.Shade(col, k);
                    if (gradedT > 0f) col = MapKit.Mix(col, MapKit.Shade(Graded, 1f + (fn - 0.5f) * 0.1f), gradedT * 0.9f);

                    // Oasis: damp sand and grass fringe.
                    float ex = (wx - oasis.x) / oasisRadii.x, ey = (wy - oasis.y) / oasisRadii.y;
                    float od = (Mathf.Sqrt(ex * ex + ey * ey) - 1f) * oasisRadii.y + (p - 0.5f) * 0.9f;
                    if (od < 1.3f)
                    {
                        col = MapKit.Mix(col, WetSand, (1f - MapKit.Smooth(0.45f, 1.25f, od)) * 0.55f);
                        float veg = 1f - MapKit.Smooth(0.25f, 0.62f, od + (fn - 0.5f) * 0.5f);
                        if (veg > 0f) col = MapKit.Mix(col, MapKit.Shade(OasisGrass, 0.88f + 0.3f * fn), veg);
                    }
                    c[x, y] = col;
                }
            }
        }

        /// <summary>
        /// Dry wash: a soft bleached band, a pale cracked-clay bed with ragged edges, two faint braided channels
        /// and scattered pebbles.
        /// </summary>
        private static void PaintRiverbed(PixelCanvas c, List<Vector2> river, int seed)
        {
            List<Vector2> px = MapKit.ToPx(river);
            float ox = MapKit.SeedOffset(seed, 1), oy = MapKit.SeedOffset(seed, 2);
            c.StrokePolyline(px, MapKit.Len(1.25f), p =>
            {
                float n = Mathf.PerlinNoise(p.x / 30f + ox, p.y / 30f + oy);
                return MapKit.Alpha(Clay, 0.18f + 0.3f * MapKit.Smooth(0.3f, 0.7f, n));
            });
            c.StrokePolyline(px, MapKit.Len(0.58f), p =>
            {
                float edgeN = Mathf.PerlinNoise(p.x / 11f + oy, p.y / 11f + ox);
                float n = Mathf.PerlinNoise(p.x / 7f + ox, p.y / 7f + oy);
                float crack = 1f - MapKit.Smooth(0.0f, 0.03f, Mathf.Abs(n - 0.5f));
                float n2 = Mathf.PerlinNoise(p.x / 3.2f + oy, p.y / 3.2f + ox);
                crack = Mathf.Max(crack, (1f - MapKit.Smooth(0.0f, 0.03f, Mathf.Abs(n2 - 0.5f))) * 0.55f);
                float k = (1f - 0.2f * crack) * (1f + (Mathf.PerlinNoise(p.x / 40f + ox, p.y / 40f) - 0.5f) * 0.1f);
                return MapKit.Alpha(MapKit.Shade(Clay, k), 0.55f + 0.4f * MapKit.Smooth(0.25f, 0.55f, edgeN));
            });
            for (int ch = -1; ch <= 1; ch += 2)
            {
                List<Vector2> braid = new List<Vector2>(px.Count);
                float along = 0f;
                for (int i = 0; i < px.Count; i++)
                {
                    if (i > 0) along += (px[i] - px[i - 1]).magnitude;
                    Vector2 tan = px[Mathf.Min(px.Count - 1, i + 1)] - px[Mathf.Max(0, i - 1)];
                    if (tan.sqrMagnitude < 1e-6f) tan = Vector2.up;
                    tan.Normalize();
                    float off = ch * MapKit.Len(0.1f) + MapKit.Len(0.09f) * Mathf.Sin(along / 37f + ch * 1.7f + seed % 7);
                    braid.Add(px[i] + new Vector2(-tan.y, tan.x) * off);
                }
                c.StrokePolyline(braid, 2.4f, MapKit.Alpha(ClayDark, 0.4f));
            }
            System.Random rng = new System.Random(seed);
            for (int i = 0; i < 160; i++)
            {
                Vector2 p = px[rng.Next(px.Count)] + new Vector2(MapKit.Range(rng, -18f, 18f), MapKit.Range(rng, -18f, 18f));
                float r = MapKit.Range(rng, 1.1f, 2.3f);
                c.FillCircle(p + MapKit.ShadowDir * 0.8f, r, new Color(0.3f, 0.22f, 0.15f, 0.25f));
                c.FillCircle(p, r, MapKit.Shade(MapKit.Rgb(0xB7A48A), MapKit.Range(rng, 0.85f, 1.1f)));
            }
        }

        private static void PaintTrack(PixelCanvas c, List<Vector2> world, int seed)
        {
            List<Vector2> px = MapKit.ToPx(world);
            c.StrokePolyline(px, MapKit.Len(0.2f), MapKit.Alpha(Track, 0.7f));
            c.StrokePolyline(MapKit.OffsetPolyline(px, 3.2f), 1.6f, MapKit.Alpha(MapKit.Shade(Track, 0.8f), 0.55f));
            c.StrokePolyline(MapKit.OffsetPolyline(px, -3.2f), 1.6f, MapKit.Alpha(MapKit.Shade(Track, 0.8f), 0.55f));
        }

        // ------------------------------------------------------------------------------------------------ mesas

        private static void PaintMesa(PixelCanvas c, Vector2 centerW, Vector2 radiiW, float angleDeg, float heightW, int seed)
        {
            Vector2 cp = MapKit.Px(centerW);
            float rx = MapKit.Len(radiiW.x), ry = MapKit.Len(radiiW.y);
            float rmin = Mathf.Min(rx, ry);
            float ph1 = MapKit.Hash01(seed, 1, 1) * 6.2832f, ph2 = MapKit.Hash01(seed, 2, 1) * 6.2832f, ph3 = MapKit.Hash01(seed, 3, 1) * 6.2832f;
            float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Func<Vector2, float> sdf = p =>
            {
                float px = p.x - cp.x, py = p.y - cp.y;
                float qx = px * ca - py * sa, qy = px * sa + py * ca;
                float ex = qx / rx, ey = qy / ry;
                float e = Mathf.Sqrt(ex * ex + ey * ey);
                float ang = Mathf.Atan2(ey, ex);
                float wob = 1f + 0.1f * Mathf.Sin(3f * ang + ph1) + 0.06f * Mathf.Sin(7f * ang + ph2) + 0.03f * Mathf.Sin(13f * ang + ph3);
                return (e - wob) * rmin;
            };
            Rect bounds = PixelCanvas.Square(cp, Mathf.Max(rx, ry) * 1.25f);
            float hPx = MapKit.Len(heightW);
            MapKit.SoftShadow(c, sdf, bounds, MapKit.ShadowOffset(heightW), hPx * 0.16f, 0.4f, true);

            float cliff = MapKit.Len(0.1f + heightW * 0.32f);
            const float scree = 9f;
            float ox = MapKit.SeedOffset(seed, 4), oy = MapKit.SeedOffset(seed, 5);
            Vector2 sun = -MapKit.ShadowDir;
            Rect b = new Rect(bounds.xMin - scree, bounds.yMin - scree, bounds.width + scree * 2f, bounds.height + scree * 2f);
            if (!MapKit.Clip(c, b, out int x0, out int y0, out int x1, out int y1)) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = sdf(p);
                    if (d > scree) continue;
                    float n = Mathf.PerlinNoise(x / 9f + ox, y / 9f + oy);
                    if (d > -0.5f)
                    {
                        float s = (1f - MapKit.Smooth(0f, scree, d)) * (0.35f + 0.5f * n);
                        c.BlendPixel(x, y, MapKit.Shade(Scree, 0.9f + 0.2f * MapKit.Hash01(x, y, seed)), s * Mathf.Clamp01(d + 0.5f));
                    }
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;

                    // Outward normal (ellipse gradient) rotated back to pixel space.
                    float px = p.x - cp.x, py = p.y - cp.y;
                    float qx = px * ca - py * sa, qy = px * sa + py * ca;
                    Vector2 nl = new Vector2(qx / (rx * rx), qy / (ry * ry));
                    if (nl.sqrMagnitude > 1e-12f) nl.Normalize();
                    Vector2 nw = Sdf.Rotate(nl, angleDeg);
                    float lit = Vector2.Dot(nw, sun);

                    Color col;
                    float depth = -d;
                    if (depth < cliff)
                    {
                        float t = depth / cliff;
                        float strata = Mathf.Sin(t * Mathf.PI * 7f + n * 5f) * 0.06f;
                        float k = (lit > 0f ? 1f + 0.14f * lit : 1f + 0.42f * lit) + strata;
                        col = MapKit.Shade(MesaCliff, k * (0.92f + 0.12f * t));
                    }
                    else
                    {
                        float k = 1f + (n - 0.5f) * 0.18f + (MapKit.Hash01(x, y, seed) - 0.5f) * 0.05f;
                        float rim = 1f - MapKit.Smooth(cliff + 1f, cliff + 4f, depth);
                        k *= 1f + 0.14f * rim * (0.6f + 0.4f * Mathf.Clamp01(lit + 0.5f));
                        float g = Mathf.PerlinNoise(x / 16f + oy, y / 16f + ox);
                        float gully = 1f - MapKit.Smooth(0.0f, 0.025f, Mathf.Abs(g - 0.5f));
                        k *= 1f - 0.13f * gully * MapKit.Smooth(cliff + 4f, cliff + 10f, depth);
                        col = MapKit.Shade(MesaTop, k);
                    }
                    c.BlendPixel(x, y, col, cov);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ props

        private static void Drum(PixelCanvas c, Vector2 w, float radiusW, int seed)
        {
            Vector2 p = MapKit.Px(w);
            float r = MapKit.Len(radiusW);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowOffset(radiusW * 1.4f), 1.5f, 0.4f, true);
            MapKit.Blob(c, p, r, MapKit.Rgb(0xC9CCCB), seed, 0f, 0.05f);
        }

        private static void Scatter(PixelCanvas c, System.Random rng, Func<Vector2, float, bool> clear, Vector2 oasis,
            Vector2 oasisRadii, List<Vector2> river, int seed)
        {
            // Rocks: clustered around mesas plus a light sprinkling across the plain.
            List<Vector3> rocks = new List<Vector3>();
            for (int i = 0; i < Mesas.Length; i++)
            {
                Vector4 m = Mesas[i];
                int n = 10 + Mathf.RoundToInt(m.z * 8f);
                for (int k = 0; k < n; k++)
                {
                    float a = MapKit.Range(rng, 0f, Mathf.PI * 2f);
                    float r = MapKit.Range(rng, 1.05f, 1.6f);
                    Vector2 w = new Vector2(m.x, m.y) + Sdf.Rotate(new Vector2(Mathf.Cos(a) * m.z * r, Mathf.Sin(a) * m.w * r), MesaAngles[i]);
                    if (clear(w, 0.25f)) rocks.Add(new Vector3(w.x, w.y, MapKit.Range(rng, 0.04f, 0.11f)));
                }
            }
            for (int i = 0; i < 45; i++)
            {
                Vector2 w = new Vector2(MapKit.Range(rng, -12.6f, 12.6f), MapKit.Range(rng, -7f, 7f));
                if (clear(w, 0.3f)) rocks.Add(new Vector3(w.x, w.y, MapKit.Range(rng, 0.03f, 0.07f)));
            }
            foreach (Vector3 rk in rocks)
            {
                Vector2 p = MapKit.Px(new Vector2(rk.x, rk.y));
                float r = MapKit.Len(rk.z);
                MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowDir * (r * 0.7f), r * 0.3f, 0.38f);
                MapKit.Blob(c, p, r, RockColors[rng.Next(RockColors.Length)], seed + rng.Next(100000), 0.18f, 0.3f);
            }

            // Dry shrubs along the riverbed banks and scattered elsewhere.
            List<Vector3> shrubs = new List<Vector3>();
            for (int i = 0; i < 110; i++)
            {
                Vector2 basePt = river[rng.Next(river.Count)];
                Vector2 w = basePt + new Vector2(MapKit.Range(rng, -0.75f, 0.75f), MapKit.Range(rng, -0.75f, 0.75f));
                if (clear(w, 0.3f)) shrubs.Add(new Vector3(w.x, w.y, MapKit.Range(rng, 0.05f, 0.09f)));
            }
            for (int i = 0; i < 90; i++)
            {
                Vector2 w = new Vector2(MapKit.Range(rng, -12.6f, 12.6f), MapKit.Range(rng, -7f, 7f));
                if (clear(w, 0.35f)) shrubs.Add(new Vector3(w.x, w.y, MapKit.Range(rng, 0.04f, 0.07f)));
            }
            // A denser ring of green shrubs around the oasis.
            for (int i = 0; i < 40; i++)
            {
                float a = MapKit.Range(rng, 0f, Mathf.PI * 2f);
                float r = MapKit.Range(rng, 1.3f, 2.0f);
                Vector2 w = oasis + new Vector2(Mathf.Cos(a) * oasisRadii.x * r, Mathf.Sin(a) * oasisRadii.y * r);
                if (clear(w, 0.3f)) shrubs.Add(new Vector3(w.x, w.y, -MapKit.Range(rng, 0.06f, 0.1f)));
            }
            foreach (Vector3 sh in shrubs)
            {
                bool green = sh.z < 0f;
                Vector2 p = MapKit.Px(new Vector2(sh.x, sh.y));
                float r = MapKit.Len(Mathf.Abs(sh.z));
                MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowDir * (r * 0.6f), r * 0.35f, 0.3f);
                Color col = green ? MapKit.Rgb(0x5E8A3A) : ShrubColors[rng.Next(ShrubColors.Length)];
                MapKit.Blob(c, p, r, col, seed + rng.Next(100000), 0.25f, 0.6f);
            }
        }
    }
}
