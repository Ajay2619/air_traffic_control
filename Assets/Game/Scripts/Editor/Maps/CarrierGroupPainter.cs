using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Carrier Group: open ocean with stylized waves, an aircraft carrier whose angled flight deck is the long
    /// runway (island superstructure, elevators, catapults, wake), an escort frigate with a stern helipad, a
    /// decorative destroyer, and a rocky atoll carrying the short strip with the water lane in its reef lagoon.
    /// Ships and the atoll are positioned from the map's spots.
    /// </summary>
    internal static class CarrierGroupPainter
    {
        private static readonly Color OceanDeep = MapKit.Rgb(0x194D78);
        private static readonly Color OceanLight = MapKit.Rgb(0x2A72A2);
        private static readonly Color Shallow = MapKit.Rgb(0x45A9B6);
        private static readonly Color LagoonShallow = MapKit.Rgb(0x7FD6CC);
        private static readonly Color LagoonDeep = MapKit.Rgb(0x3FAAB8);
        private static readonly Color Reef = MapKit.Rgb(0xC9E6D3);
        private static readonly Color Beach = MapKit.Rgb(0xEEDDAF);
        private static readonly Color RockLand = MapKit.Rgb(0x9A917F);
        private static readonly Color Vegetation = MapKit.Rgb(0x5F8D44);
        private static readonly Color DeckGray = MapKit.Rgb(0x5D646D);
        private static readonly Color HullGray = MapKit.Rgb(0x434A53);
        private static readonly Color ShipLight = MapKit.Rgb(0xA4AAB0);

        /// <summary>Carrier flight deck outline in hull space (world units, bow toward +x, port toward +y).</summary>
        private static readonly Vector2[] DeckShape =
        {
            new Vector2(-4.05f, -0.72f), new Vector2(-4.05f, 0.55f), new Vector2(-3.0f, 0.78f), new Vector2(-1.5f, 1.0f),
            new Vector2(0.0f, 1.22f), new Vector2(1.5f, 1.45f), new Vector2(2.65f, 1.45f), new Vector2(3.05f, 1.08f),
            new Vector2(3.9f, 0.58f), new Vector2(4.35f, 0.18f), new Vector2(4.4f, -0.05f), new Vector2(4.15f, -0.38f),
            new Vector2(3.3f, -0.72f), new Vector2(1.9f, -0.98f), new Vector2(-1.0f, -1.0f), new Vector2(-3.0f, -0.88f),
        };

        /// <summary>Runway threshold in hull space; the landing direction is rotated <see cref="AngledDeck"/> to port.</summary>
        private static readonly Vector2 CarrierThreshold = new Vector2(-3.3f, -0.05f);
        private const float AngledDeck = 9f;

        /// <summary>Frigate hull outline (world units, bow +x). The stern deck carries the helipad at x = <see cref="FrigatePadX"/>.</summary>
        private static readonly Vector2[] FrigateShape =
        {
            new Vector2(-1.72f, -0.6f), new Vector2(-1.72f, 0.6f), new Vector2(0.1f, 0.62f), new Vector2(0.95f, 0.48f),
            new Vector2(1.55f, 0.2f), new Vector2(1.78f, 0f), new Vector2(1.55f, -0.2f), new Vector2(0.95f, -0.48f),
            new Vector2(0.1f, -0.62f),
        };
        private const float FrigatePadX = -1.05f;
        private const float FleetHeading = 6f;

        /// <summary>Atoll geometry (world units) derived from the short strip and the water lane.</summary>
        private sealed class Atoll
        {
            public bool HasIsland, HasLagoon;
            public MapZone Island;
            public Vector2 ArmA, ArmB;
            public float ArmRadius;
            public Vector2 LaneOrigin, LaneDir;
            public float LagoonCenterU, LagoonHalfU, LagoonHalfV, LagoonRadius;

            public float IslandSdf(float x, float y)
            {
                if (!HasIsland) return float.MaxValue;
                float a = Island.Sdf(x, y);
                if (!HasLagoon) return a;
                float b = Sdf.Segment(new Vector2(x, y), ArmA, ArmB, ArmRadius);
                return Sdf.SmoothUnion(a, b, 0.35f);
            }

            public void LaneLocal(float x, float y, out float u, out float v)
            {
                float rx = x - LaneOrigin.x, ry = y - LaneOrigin.y;
                u = rx * LaneDir.x + ry * LaneDir.y;
                v = -rx * LaneDir.y + ry * LaneDir.x;
            }

            public float LagoonSdf(float u, float v)
                => HasLagoon ? MapKit.BoxSdf(u - LagoonCenterU, v, LagoonHalfU, LagoonHalfV, LagoonRadius) : float.MaxValue;
        }

        public static void Paint(PixelCanvas c, MapDefinition def, int seed)
        {
            LandingSpotDef longRw = MapKit.FindSpot(def, LandingKind.LongRunway);
            LandingSpotDef pad = MapKit.FindSpot(def, LandingKind.Helipad);
            LandingSpotDef strip = MapKit.FindSpot(def, LandingKind.ShortRunway);
            LandingSpotDef lane = MapKit.FindSpot(def, LandingKind.WaterLane);

            Atoll atoll = BuildAtoll(strip, lane);
            MapNoiseField edge = new MapNoiseField(c.Width, c.Height, 70f, 3, seed + 1);
            Terrain(c, seed, atoll, edge);

            System.Random rng = new System.Random(seed);
            if (atoll.HasIsland) IslandGround(c, rng, atoll, edge, strip, seed + 10);
            if (strip != null) RunwayPainter.Paint(c, strip, RunwaySurface.Asphalt, true, seed + 20);
            if (atoll.HasIsland) IslandProps(c, rng, atoll, edge, strip, seed + 30);
            if (lane != null) RunwayPainter.Paint(c, lane, RunwaySurface.Asphalt, false, seed + 40);

            // Decorative destroyer (top middle), escort frigate with the helipad, then the carrier.
            PaintFrigate(c, new Vector2(2.6f, 3.55f), FleetHeading, 0.8f, null, 2.4f, seed + 50);
            if (pad != null)
            {
                Vector2 frigateC = pad.position - Sdf.Rotate(new Vector2(FrigatePadX, 0f), FleetHeading);
                PaintFrigate(c, frigateC, FleetHeading, 1f, pad, 4.5f, seed + 60);
            }
            if (longRw != null) PaintCarrier(c, longRw, seed + 70);

            // Any other spots (custom edits) still get painted.
            if (def.landingSpots != null)
            {
                foreach (LandingSpotDef s in def.landingSpots)
                {
                    if (s != null && s != longRw && s != pad && s != strip && s != lane)
                        RunwayPainter.Paint(c, s, RunwaySurface.Asphalt, false, seed + 90);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ atoll

        private static Atoll BuildAtoll(LandingSpotDef strip, LandingSpotDef lane)
        {
            Atoll a = new Atoll();
            if (strip != null)
            {
                a.HasIsland = true;
                Vector2 d = strip.Direction, n = MapKit.Perp(d);
                const float uMin = -1.25f, vMin = -1.3f, vMax = 0.95f;
                float uMax = strip.length + 0.75f;
                Vector2 center = strip.position + d * ((uMin + uMax) * 0.5f) + n * ((vMin + vMax) * 0.5f);
                a.Island = new MapZone(center, strip.angleDeg, (uMax - uMin) * 0.5f, (vMax - vMin) * 0.5f, 0.6f);
            }
            if (lane != null)
            {
                a.HasLagoon = true;
                a.LaneOrigin = lane.position;
                a.LaneDir = lane.Direction;
                a.LagoonCenterU = (lane.length - 0.2f) * 0.5f;
                a.LagoonHalfU = (lane.length + 1.4f) * 0.5f;
                a.LagoonHalfV = 1.0f;
                a.LagoonRadius = 0.85f;
                Vector2 farTip = lane.position + lane.Direction * (lane.length + 0.6f);
                Vector2 islandEast = strip != null ? strip.position + strip.Direction * (-0.9f) + MapKit.Perp(strip.Direction) * 0.4f : farTip + new Vector2(0f, 2f);
                a.ArmA = islandEast;
                a.ArmB = farTip + new Vector2(0.55f, -0.2f);
                a.ArmRadius = 0.45f;
            }
            return a;
        }

        private static void Terrain(PixelCanvas c, int seed, Atoll atoll, MapNoiseField edge)
        {
            MapNoiseField low = new MapNoiseField(c.Width, c.Height, 620f, 3, seed + 2);
            MapNoiseField swell = new MapNoiseField(c.Width, c.Height, 200f, 2, seed + 3);
            MapNoiseField fine = new MapNoiseField(c.Width, c.Height, 20f, 2, seed + 4, 2);
            MapNoiseField veg = new MapNoiseField(c.Width, c.Height, 45f, 3, seed + 5);
            for (int y = 0; y < c.Height; y++)
            {
                float wy = MapKit.WorldY(y);
                for (int x = 0; x < c.Width; x++)
                {
                    float wx = MapKit.WorldX(x);
                    float en = edge.Sample(x, y);
                    float fn = fine.Sample(x, y);
                    float sw = swell.Sample(x, y);

                    // Open ocean.
                    Color col = MapKit.Mix(OceanDeep, OceanLight, MapKit.Smooth(0.2f, 0.8f, low.Sample(x, y) + (sw - 0.5f) * 0.25f));
                    float band = Mathf.Sin((wx * 0.3f + wy) * 5f + sw * 10f);
                    col = MapKit.Shade(col, 1f + 0.03f * band + (fn - 0.5f) * 0.05f);

                    float isl = atoll.IslandSdf(wx, wy) + (en - 0.5f) * 0.35f;
                    float lagoonSdf = float.MaxValue, laneU = 0f;
                    if (atoll.HasLagoon)
                    {
                        atoll.LaneLocal(wx, wy, out laneU, out float laneV);
                        lagoonSdf = atoll.LagoonSdf(laneU, laneV) + (en - 0.5f) * 0.16f + (sw - 0.5f) * 0.45f;
                    }

                    // Shallow halos around the island and the lagoon.
                    float shallowT = Mathf.Max(1f - MapKit.Smooth(0f, 0.75f, isl), 1f - MapKit.Smooth(0.1f, 1.0f, lagoonSdf));
                    if (shallowT > 0f) col = MapKit.Mix(col, MapKit.Shade(Shallow, 1f + (fn - 0.5f) * 0.06f), shallowT * 0.62f);

                    // Wave crests on deep water only.
                    float deepT = 1f - shallowT;
                    if (deepT > 0.05f)
                    {
                        float crest = MapKit.WaveMark(x, y, 30f, 0.5f, seed + 9) * deepT;
                        float trough = MapKit.WaveMark(x, y + 2.4f, 30f, 0.5f, seed + 9) * deepT;
                        col = MapKit.Shade(col, 1f - 0.12f * trough);
                        col = MapKit.Mix(col, MapKit.Foam, crest * 0.4f);
                    }

                    // Lagoon water and the reef ring (open toward the approach end of the lane).
                    if (atoll.HasLagoon && lagoonSdf < 0.45f)
                    {
                        float mouth = MapKit.Smooth(-0.1f, 0.6f, laneU);
                        float reefW = 0.28f * mouth;
                        if (lagoonSdf < 0f)
                        {
                            Color lag = MapKit.Mix(LagoonShallow, LagoonDeep, MapKit.Smooth(0.05f, 0.75f, -lagoonSdf));
                            lag = MapKit.Shade(lag, 1f + (fn - 0.5f) * 0.06f + (sw - 0.5f) * 0.05f);
                            col = MapKit.Mix(col, lag, Mathf.Clamp01(-lagoonSdf * MapKit.Ppu + 0.5f));
                        }
                        if (reefW > 0.02f)
                        {
                            float rd = Mathf.Abs(lagoonSdf - reefW * 0.5f) - reefW * 0.5f;
                            float reefCov = Mathf.Clamp01(0.5f - rd * MapKit.Ppu) * (0.55f + 0.45f * MapKit.Smooth(0.35f, 0.6f, en));
                            col = MapKit.Mix(col, MapKit.Shade(Reef, 0.95f + 0.1f * fn), reefCov * 0.85f);
                            float surf = Mathf.Clamp01(1f - Mathf.Abs(lagoonSdf - reefW - 0.03f) / 0.035f) * mouth;
                            col = MapKit.Mix(col, MapKit.Foam, surf * (0.4f + 0.5f * fn));
                        }
                    }

                    // Island: beach rim, rocky ground with vegetation patches, surf line.
                    if (isl < 0.08f)
                    {
                        float surf = Mathf.Clamp01(1f - Mathf.Abs(isl - 0.035f) / 0.03f);
                        col = MapKit.Mix(col, MapKit.Foam, surf * 0.7f);
                        float landCov = Mathf.Clamp01(-isl * MapKit.Ppu + 0.5f);
                        if (landCov > 0f)
                        {
                            float inland = -isl;
                            Color ground = MapKit.Shade(RockLand, 1f + (fn - 0.5f) * 0.14f + (en - 0.5f) * 0.2f);
                            float vg = MapKit.Smooth(0.5f, 0.6f, veg.Sample(x, y)) * MapKit.Smooth(0.18f, 0.35f, inland);
                            ground = MapKit.Mix(ground, MapKit.Shade(Vegetation, 0.85f + 0.3f * fn), vg);
                            float beach = 1f - MapKit.Smooth(0.1f, 0.17f, inland);
                            ground = MapKit.Mix(ground, MapKit.Shade(Beach, 1f + (fn - 0.5f) * 0.08f), beach);
                            col = MapKit.Mix(col, ground, landCov);
                        }
                    }
                    col.a = 1f;
                    c[x, y] = col;
                }
            }
        }

        private static void IslandGround(PixelCanvas c, System.Random rng, Atoll atoll, MapNoiseField edge, LandingSpotDef strip, int seed)
        {
            MapZone keep = strip != null ? MapZone.AroundSpot(strip, 0.3f, RunwayPainter.StubLength + 0.2f) : new MapZone(Vector2.zero, 0f, 0f, 0f, 0f);
            Color[] rockCols = { MapKit.Rgb(0x857D70), MapKit.Rgb(0x9C9384), MapKit.Rgb(0x756E63) };
            for (int i = 0, placed = 0; i < 3000 && placed < 70; i++)
            {
                Vector2 w = atoll.Island.Center + new Vector2(MapKit.Range(rng, -3.5f, 3.5f), MapKit.Range(rng, -3.5f, 3.5f));
                Vector2 p = MapKit.Px(w);
                float isl = atoll.IslandSdf(w.x, w.y) + (edge.Sample(p.x, p.y) - 0.5f) * 0.35f;
                if (isl > -0.22f) continue;
                if (strip != null && keep.Sdf(w) < 0f) continue;
                float r = MapKit.Len(MapKit.Range(rng, 0.04f, 0.1f));
                MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowDir * (r * 0.7f), r * 0.3f, 0.35f);
                MapKit.Blob(c, p, r, rockCols[rng.Next(rockCols.Length)], seed + i, 0.2f, 0.3f);
                placed++;
            }
        }

        private static void IslandProps(PixelCanvas c, System.Random rng, Atoll atoll, MapNoiseField edge, LandingSpotDef strip, int seed)
        {
            if (strip != null)
            {
                float ang = strip.angleDeg;
                MapKit.Slab(c, MapKit.Local(strip.position, ang, 1.75f, -0.8f), new Vector2(1.2f, 0.6f), ang, 0.06f, MapKit.Rgb(0xA8A69E), 0.4f, seed);
                MapKit.Building(c, MapKit.Local(strip.position, ang, 1.55f, -0.84f), new Vector2(0.55f, 0.42f), ang, MapKit.Rgb(0xC5CACB), MapRoof.Arched, 0.26f, seed + 1);
                Tank(c, MapKit.Local(strip.position, ang, 2.15f, -0.88f), 0.09f, seed + 2);
                Tank(c, MapKit.Local(strip.position, ang, 2.15f, -0.68f), 0.09f, seed + 3);
            }
            if (atoll.HasLagoon)
            {
                // Beacon at the southern tip of the eastern arm.
                Vector2 bp = MapKit.Px(atoll.ArmB);
                float br = MapKit.Len(0.11f);
                MapKit.SoftShadow(c, q => Sdf.Circle(q, bp, br), PixelCanvas.Square(bp, br), MapKit.ShadowOffset(0.6f), 2f, 0.38f, true);
                MapKit.Blob(c, bp, br, MapKit.Rgb(0xF1F0EA), seed + 4, 0f, 0.03f);
                c.FillCircle(bp, br * 0.55f, MapKit.Rgb(0x3F5F73));
            }

            // Palms along the beaches, clear of the strip, its pad and the buildings.
            MapZone keep = strip != null ? MapZone.AroundSpot(strip, 0.35f, RunwayPainter.StubLength + 0.3f) : new MapZone(Vector2.zero, 0f, 0f, 0f, 0f);
            MapZone keepHangar = strip != null ? new MapZone(MapKit.Local(strip.position, strip.angleDeg, 1.75f, -0.8f), strip.angleDeg, 0.8f, 0.45f, 0.2f)
                : new MapZone(Vector2.zero, 0f, 0f, 0f, 0f);
            for (int i = 0, placed = 0; i < 4000 && placed < 34; i++)
            {
                Vector2 w = atoll.Island.Center + new Vector2(MapKit.Range(rng, -4f, 4f), MapKit.Range(rng, -4.5f, 3f));
                Vector2 p = MapKit.Px(w);
                float isl = atoll.IslandSdf(w.x, w.y) + (edge.Sample(p.x, p.y) - 0.5f) * 0.35f;
                if (isl > -0.1f || isl < -0.45f) continue;
                if (strip != null && (keep.Sdf(w) < 0f || keepHangar.Sdf(w) < 0f)) continue;
                if (atoll.HasLagoon && (w - atoll.ArmB).sqrMagnitude < 0.3f * 0.3f) continue;
                MapKit.Palm(c, w, MapKit.Range(rng, 0.18f, 0.25f), seed + 100 + i);
                placed++;
            }
        }

        private static void Tank(PixelCanvas c, Vector2 w, float radiusW, int seed)
        {
            Vector2 p = MapKit.Px(w);
            float r = MapKit.Len(radiusW);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowOffset(radiusW * 1.5f), 1.6f, 0.4f, true);
            MapKit.Blob(c, p, r, MapKit.Rgb(0xD6D8D7), seed, 0f, 0.05f);
        }

        // ------------------------------------------------------------------------------------------------ ships

        /// <summary>Foam hugging a hull, strongest at the bow (pixel-space hull outline, hull-space bow threshold).</summary>
        private static void HullFoam(PixelCanvas c, Vector2[] hull, Vector2 centerPx, float headingDeg, float bowStartPx, float bowEndPx, int seed)
        {
            const float reach = 12f;
            if (!MapKit.Clip(c, Sdf.Bounds(hull, reach + 2f), out int x0, out int y0, out int x1, out int y1)) return;
            Vector2 fwd = MapKit.Dir(headingDeg);
            float ox = MapKit.SeedOffset(seed, 1), oy = MapKit.SeedOffset(seed, 2);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Sdf.Polygon(p, hull);
                    if (d <= -0.5f || d > reach) continue;
                    float along = Vector2.Dot(p - centerPx, fwd);
                    float bow = MapKit.Smooth(bowStartPx, bowEndPx, along);
                    float fall = 1f - d / reach;
                    float a = fall * fall * (0.22f + 0.78f * bow) * (0.55f + 0.6f * Mathf.PerlinNoise(x / 5f + ox, y / 5f + oy));
                    c.BlendPixel(x, y, MapKit.Foam, Mathf.Clamp01(a) * Mathf.Clamp01(d + 0.5f));
                }
            }
        }

        private static void PaintCarrier(PixelCanvas c, LandingSpotDef rw, int seed)
        {
            float hullAngle = rw.angleDeg - AngledDeck;
            Vector2 hullC = rw.position - Sdf.Rotate(CarrierThreshold, hullAngle);
            Vector2 cp = MapKit.Px(hullC);
            Vector2[] deck = MapKit.Transform(DeckShape, cp, hullAngle, MapKit.Ppu, MapKit.Ppu);
            Rect b = Sdf.Bounds(deck, 2f);

            Vector2 stern = MapKit.Px(MapKit.Local(hullC, hullAngle, -4.05f, -0.08f));
            MapKit.Wake(c, stern, hullAngle, MapKit.Len(7.5f), MapKit.Len(0.42f), 0.9f, seed + 1);
            HullFoam(c, deck, cp, hullAngle, MapKit.Len(1.5f), MapKit.Len(4.2f), seed + 2);
            MapKit.SoftShadow(c, p => Sdf.Polygon(p, deck), b, MapKit.ShadowOffset(0.24f), MapKit.Len(0.05f), 0.42f);
            c.FillSdf(p => Sdf.Polygon(p, deck) - 3f, Sdf.Bounds(deck, 5f), HullGray);

            // Flight deck: plated steel with a dashed safety line along the edge.
            if (MapKit.Clip(c, b, out int x0, out int y0, out int x1, out int y1))
            {
                float a = -hullAngle * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Sdf.Polygon(p, deck);
                        float cov = Mathf.Clamp01(0.5f - d);
                        if (cov <= 0f) continue;
                        float rx = p.x - cp.x, ry = p.y - cp.y;
                        float qx = rx * ca - ry * sa, qy = rx * sa + ry * ca;
                        float k = 1f + (MapKit.Hash01(x, y, seed) - 0.5f) * 0.05f;
                        if (Mathf.Repeat(qx, MapKit.Len(0.5f)) < 1f || Mathf.Repeat(qy, MapKit.Len(0.36f)) < 1f) k *= 0.94f;
                        if (d > -1.6f) k *= 0.82f;
                        Color col = MapKit.Shade(DeckGray, k);
                        float line = Mathf.Clamp01(0.5f - (Mathf.Abs(d + 4.6f) - 0.8f));
                        if (line > 0f && Mathf.Repeat(qx + qy, 14f) < 8f) col = MapKit.Mix(col, new Color(0.9f, 0.9f, 0.88f, 1f), line * 0.8f);
                        c.BlendPixel(x, y, col, cov);
                    }
                }
            }

            // Elevators and catapult tracks.
            DeckRect(c, hullC, hullAngle, new Vector2(-1.25f, -0.74f), new Vector2(0.62f, 0.46f));
            DeckRect(c, hullC, hullAngle, new Vector2(2.25f, -0.66f), new Vector2(0.55f, 0.4f));
            Catapult(c, hullC, hullAngle, new Vector2(2.6f, -0.02f), new Vector2(4.1f, 0.0f));
            Catapult(c, hullC, hullAngle, new Vector2(2.68f, -0.4f), new Vector2(3.85f, -0.37f));

            RunwayPainter.Paint(c, rw, RunwaySurface.CarrierDeck, false, seed + 5);

            // Island superstructure on the starboard side: base block, bridge with windows, funnel, radar dome, mast.
            MapKit.Building(c, MapKit.Local(hullC, hullAngle, 1.12f, -0.78f), new Vector2(1.45f, 0.32f), hullAngle, MapKit.Rgb(0x7C858F), MapRoof.Flat, 0.45f, seed + 6);
            MapKit.Building(c, MapKit.Local(hullC, hullAngle, 0.95f, -0.77f), new Vector2(0.72f, 0.24f), hullAngle, MapKit.Rgb(0x96A0AA), MapRoof.Flat, 0.2f, seed + 7);
            c.FillBox(MapKit.Px(MapKit.Local(hullC, hullAngle, 1.29f, -0.77f)), new Vector2(MapKit.Len(0.035f), MapKit.Len(0.2f)), 0.5f, hullAngle, MapKit.Rgb(0x2E3A46));
            MapKit.Building(c, MapKit.Local(hullC, hullAngle, 1.62f, -0.78f), new Vector2(0.22f, 0.2f), hullAngle, MapKit.Rgb(0x4A5058), MapRoof.Flat, 0.15f, seed + 8);
            Vector2 dome = MapKit.Px(MapKit.Local(hullC, hullAngle, 0.62f, -0.78f));
            float domeR = MapKit.Len(0.08f);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, dome, domeR), PixelCanvas.Square(dome, domeR), MapKit.ShadowOffset(0.1f), 1.5f, 0.35f, true);
            MapKit.Blob(c, dome, domeR, MapKit.Rgb(0xEEF0F2), seed + 9, 0f, 0f);
            Vector2 mast = MapKit.Px(MapKit.Local(hullC, hullAngle, 1.2f, -0.77f));
            Vector2 arm = MapKit.Dir(hullAngle + 90f) * MapKit.Len(0.15f);
            c.StrokeLine(mast - arm, mast + arm, 1.6f, MapKit.Rgb(0x3A4048));
            c.FillCircle(mast, 2.6f, MapKit.Rgb(0xD5D9DD));
        }

        private static void DeckRect(PixelCanvas c, Vector2 hullC, float hullAngle, Vector2 local, Vector2 size)
        {
            Vector2 p = MapKit.Px(MapKit.Local(hullC, hullAngle, local.x, local.y));
            Vector2 half = new Vector2(MapKit.Len(size.x), MapKit.Len(size.y)) * 0.5f;
            c.FillBox(p, half * 2f, 1f, hullAngle, MapKit.Shade(DeckGray, 0.78f));
            c.FillBox(p, half * 2f - new Vector2(3f, 3f), 1f, hullAngle, MapKit.Shade(DeckGray, 1.06f));
        }

        private static void Catapult(PixelCanvas c, Vector2 hullC, float hullAngle, Vector2 from, Vector2 to)
        {
            Vector2 a = MapKit.Px(MapKit.Local(hullC, hullAngle, from.x, from.y));
            Vector2 b = MapKit.Px(MapKit.Local(hullC, hullAngle, to.x, to.y));
            c.StrokeLine(a, b, 1.8f, new Color(0.78f, 0.8f, 0.82f, 0.85f));
            Vector2 n = MapKit.Perp((b - a).normalized) * 3f;
            c.StrokeLine(a - n, a + n, 1.6f, new Color(0.78f, 0.8f, 0.82f, 0.85f));
        }

        /// <summary>
        /// Frigate / destroyer: wake, bow foam, shadow, hull, deck, superstructure and guns. With a helipad spot the
        /// stern becomes a flight deck carrying the pad.
        /// </summary>
        private static void PaintFrigate(PixelCanvas c, Vector2 centerW, float headingDeg, float scale, LandingSpotDef pad, float wakeLen, int seed)
        {
            Vector2 cp = MapKit.Px(centerW);
            float s = MapKit.Ppu * scale;
            Vector2[] hull = MapKit.Transform(FrigateShape, cp, headingDeg, s, s);
            Rect b = Sdf.Bounds(hull, 2f);
            Vector2 stern = MapKit.Px(MapKit.Local(centerW, headingDeg, -1.72f * scale, 0f));
            MapKit.Wake(c, stern, headingDeg, MapKit.Len(wakeLen), MapKit.Len(0.28f * scale), 0.85f, seed + 1);
            HullFoam(c, hull, cp, headingDeg, MapKit.Len(0.6f * scale), MapKit.Len(1.7f * scale), seed + 2);
            MapKit.SoftShadow(c, p => Sdf.Polygon(p, hull), b, MapKit.ShadowOffset(0.16f), MapKit.Len(0.04f), 0.42f);
            c.FillSdf(p => Sdf.Polygon(p, hull), b, HullGray);
            Vector2[] deck = MapKit.Transform(FrigateShape, cp, headingDeg, s * 0.96f, s * 0.9f);
            float ox = MapKit.SeedOffset(seed, 3);
            c.FillSdf(p => Sdf.Polygon(p, deck), b, p => MapKit.Shade(ShipLight, 1f + (Mathf.PerlinNoise(p.x / 14f + ox, p.y / 14f) - 0.5f) * 0.08f));

            DashedOutline(c, MapKit.Transform(FrigateShape, cp, headingDeg, s * 0.9f, s * 0.8f), 1.2f, new Color(0.93f, 0.94f, 0.95f, 0.75f));

            if (pad != null)
            {
                // Stern flight deck (darker) carrying the helipad.
                Vector2 fd = MapKit.Local(centerW, headingDeg, -1.07f * scale, 0f);
                c.FillBox(MapKit.Px(fd), new Vector2(MapKit.Len(1.22f * scale), MapKit.Len(1.1f * scale)), 2f, headingDeg, MapKit.Rgb(0x6F777F));
                RunwayPainter.PaintHelipad(c, pad.position, pad.width, seed + 4);
            }
            else
            {
                // Aft gun and missile cells on the destroyer.
                Gun(c, MapKit.Local(centerW, headingDeg, -1.15f * scale, 0f), headingDeg + 180f, scale);
                c.FillBox(MapKit.Px(MapKit.Local(centerW, headingDeg, -0.72f * scale, 0f)), new Vector2(MapKit.Len(0.26f * scale), MapKit.Len(0.34f * scale)), 1f,
                    headingDeg, MapKit.Rgb(0x7E858C));
            }

            // Hangar, bridge (with windows), funnel, radar dome, lifeboats, forward gun and mast.
            MapKit.Building(c, MapKit.Local(centerW, headingDeg, -0.08f * scale, 0f), new Vector2(0.62f * scale, 0.86f * scale), headingDeg,
                MapKit.Rgb(0xB9BEC3), MapRoof.Flat, 0.22f, seed + 5);
            MapKit.Building(c, MapKit.Local(centerW, headingDeg, 0.42f * scale, 0f), new Vector2(0.3f * scale, 0.62f * scale), headingDeg,
                MapKit.Rgb(0xC9CDD1), MapRoof.Flat, 0.3f, seed + 6);
            c.FillBox(MapKit.Px(MapKit.Local(centerW, headingDeg, 0.545f * scale, 0f)), new Vector2(MapKit.Len(0.035f * scale), MapKit.Len(0.5f * scale)), 0.5f,
                headingDeg, MapKit.Rgb(0x2E3A46));
            MapKit.Building(c, MapKit.Local(centerW, headingDeg, 0.06f * scale, 0f), new Vector2(0.16f * scale, 0.22f * scale), headingDeg,
                MapKit.Rgb(0x50565E), MapRoof.Flat, 0.2f, seed + 7);
            Vector2 dome = MapKit.Px(MapKit.Local(centerW, headingDeg, -0.24f * scale, 0f));
            float domeR = MapKit.Len(0.09f * scale);
            MapKit.SoftShadow(c, q => Sdf.Circle(q, dome, domeR), PixelCanvas.Square(dome, domeR), MapKit.ShadowOffset(0.12f), 1.5f, 0.35f, true);
            MapKit.Blob(c, dome, domeR, MapKit.Rgb(0xEEF0F2), seed + 8, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                c.FillBox(MapKit.Px(MapKit.Local(centerW, headingDeg, 0.02f * scale, side * 0.5f * scale)),
                    new Vector2(MapKit.Len(0.24f * scale), MapKit.Len(0.075f * scale)), MapKit.Len(0.035f * scale), headingDeg, MapKit.Rgb(0xF2F2EF));
            }
            c.FillBox(MapKit.Px(MapKit.Local(centerW, headingDeg, 0.75f * scale, 0f)), new Vector2(MapKit.Len(0.18f * scale), MapKit.Len(0.3f * scale)), 1f,
                headingDeg, MapKit.Rgb(0x7E858C));
            Gun(c, MapKit.Local(centerW, headingDeg, 1.1f * scale, 0f), headingDeg, scale);
            Vector2 mast = MapKit.Px(MapKit.Local(centerW, headingDeg, 0.24f * scale, 0f));
            Vector2 arm = MapKit.Dir(headingDeg + 90f) * MapKit.Len(0.14f * scale);
            c.StrokeLine(mast - arm, mast + arm, 1.4f, MapKit.Rgb(0x3A4048));
            c.FillCircle(mast, 2.2f, MapKit.Rgb(0xD5D9DD));
        }

        private static void DashedOutline(PixelCanvas c, Vector2[] poly, float width, Color col)
        {
            const float dash = 5f, gap = 4f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = (b - a).magnitude;
                if (len < 1f) continue;
                Vector2 d = (b - a) / len;
                for (float t = 0f; t < len; t += dash + gap) c.StrokeLine(a + d * t, a + d * Mathf.Min(len, t + dash), width, col);
            }
        }

        private static void Gun(PixelCanvas c, Vector2 w, float facingDeg, float scale)
        {
            Vector2 p = MapKit.Px(w);
            float r = MapKit.Len(0.09f * scale);
            Vector2 tip = p + MapKit.Dir(facingDeg) * MapKit.Len(0.34f * scale);
            c.StrokeLine(p, tip, 2f, MapKit.Rgb(0x50565E));
            MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowOffset(0.05f), 1.2f, 0.35f);
            MapKit.Blob(c, p, r, MapKit.Rgb(0xA9AFB5), 7, 0f, 0f);
        }
    }
}
