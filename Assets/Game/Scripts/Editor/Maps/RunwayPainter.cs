using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>Surface style of a painted runway.</summary>
    internal enum RunwaySurface
    {
        /// <summary>Dark asphalt (land airfields).</summary>
        Asphalt,
        /// <summary>Steel-gray carrier flight deck (no shoulders, stronger edge occlusion).</summary>
        CarrierDeck,
    }

    /// <summary>
    /// Paints landing spots so their landing color reads instantly:
    /// runways (asphalt, darker edges, white side stripes, dashed centerline, piano keys, aiming blocks, plus in the
    /// landing color: edge bands, a threshold bar and big chevrons on a paved approach pad before the threshold),
    /// water lanes (calm tinted water, two rows of buoys, roped edges, chevrons at the approach end) and helipads
    /// (dark disc, white rim, landing-color ring and big "H").
    /// </summary>
    internal static class RunwayPainter
    {
        /// <summary>Length (world units) of the paved approach pad painted before a runway threshold.</summary>
        public const float StubLength = 0.6f;

        /// <summary>Length (world units) before a water-lane threshold that holds the approach chevrons.</summary>
        public const float LaneLeadIn = 0.85f;

        private static readonly Color MarkWhite = new Color(0.97f, 0.97f, 0.95f, 1f);

        /// <summary>Paints any spot kind.</summary>
        public static void Paint(PixelCanvas c, LandingSpotDef spot, RunwaySurface surface, bool shoulders, int seed)
        {
            if (spot == null) return;
            switch (spot.kind)
            {
                case LandingKind.Helipad:
                    PaintHelipad(c, spot.position, spot.width, seed);
                    break;
                case LandingKind.WaterLane:
                    PaintWaterLane(c, spot, seed);
                    break;
                default:
                    PaintRunway(c, spot, surface, shoulders, seed);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ runway

        /// <summary>Paints a runway (long or short) including its approach pad, in a single anti-aliased pass.</summary>
        public static void PaintRunway(PixelCanvas c, LandingSpotDef spot, RunwaySurface surface, bool shoulders, int seed)
        {
            Color mark = LandingColors.For(spot.kind);
            bool deck = surface == RunwaySurface.CarrierDeck;
            Color baseCol = deck ? new Color(0.37f, 0.39f, 0.42f, 1f) : new Color(0.29f, 0.3f, 0.32f, 1f);
            Color stubCol = deck ? new Color(0.33f, 0.35f, 0.38f, 1f) : new Color(0.24f, 0.25f, 0.27f, 1f);
            Color shoulderCol = new Color(0.68f, 0.67f, 0.62f, 1f);

            Vector2 t = MapKit.Px(spot.position);
            Vector2 d = MapKit.Dir(spot.angleDeg);
            Vector2 n = MapKit.Perp(d);
            float len = MapKit.Len(Mathf.Max(0.5f, spot.length));
            float hw = MapKit.Len(Mathf.Max(0.2f, spot.width)) * 0.5f;
            float stub = MapKit.Len(StubLength);
            bool isShort = spot.kind == LandingKind.ShortRunway;

            // Cross-section (px from the centerline): color band | gap | white stripe | gap | inner area.
            float band = Mathf.Max(3.5f, hw * 0.17f);
            float gap = 1.6f;
            float stripe = Mathf.Max(2.2f, hw * 0.09f);
            float stripeOuter = hw - band - gap;
            float stripeInner = stripeOuter - stripe;
            float inner = stripeInner - gap;

            // Along the runway (px from the threshold).
            float barLen = MapKit.Len(0.07f);
            float keyStart = barLen + MapKit.Len(0.07f);
            float keyLen = MapKit.Len(isShort ? 0.3f : 0.38f);
            int keys = isShort ? 2 : 3;
            const float keyCenterGap = 2.5f;
            float slot = (inner - keyCenterGap) / keys;
            float keyHalfW = slot * 0.3f;
            float aimStart = MapKit.Len(isShort ? 0.85f : 1.2f), aimLen = MapKit.Len(isShort ? 0.3f : 0.42f);
            float aimV = inner * 0.56f, aimHalfW = Mathf.Max(1.8f, inner * 0.17f);
            float dashStart = keyStart + keyLen + MapKit.Len(0.3f);
            float dashEnd = len - MapKit.Len(0.25f);
            float dashLen = MapKit.Len(0.3f), dashPeriod = dashLen + MapKit.Len(0.22f);
            float centerHalf = Mathf.Max(1.1f, hw * 0.045f);
            float endBarU = len - MapKit.Len(0.05f), endBarHalf = MapKit.Len(0.022f);

            // Approach chevrons on the pad (pointing along the landing direction).
            float chevBack = MapKit.Len(0.19f), chevSpread = hw - band - 2.5f, chevR = MapKit.Len(0.085f) * 0.5f;
            float chevTip1 = -MapKit.Len(0.09f), chevTip2 = -MapKit.Len(0.33f);

            float shoulderW = shoulders ? 3.2f : 0f;
            const float aoW = 5f;
            float ext = shoulderW + aoW + 2f;
            Vector2[] corners =
            {
                t + d * (-stub - ext) + n * (hw + ext), t + d * (-stub - ext) - n * (hw + ext),
                t + d * (len + ext) + n * (hw + ext), t + d * (len + ext) - n * (hw + ext),
            };
            if (!MapKit.Clip(c, Sdf.Bounds(corners, 1f), out int x0, out int y0, out int x1, out int y1)) return;

            float uc = (len - stub) * 0.5f, hu = (len + stub) * 0.5f;
            float ox = MapKit.SeedOffset(seed, 61), oy = MapKit.SeedOffset(seed, 62);
            float aoAlpha = deck ? 0.32f : 0.2f;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float rx = x + 0.5f - t.x, ry = y + 0.5f - t.y;
                    float u = rx * d.x + ry * d.y;
                    float v = rx * n.x + ry * n.y;
                    float av = Mathf.Abs(v);
                    float dBody = MapKit.BoxSdf(u - uc, v, hu, hw, 1.5f);
                    if (dBody > shoulderW + aoW + 0.5f) continue;

                    float bodyCov = Mathf.Clamp01(0.5f - dBody);
                    if (bodyCov < 1f)
                    {
                        float ao = 1f - MapKit.Smooth(shoulderW, shoulderW + aoW, dBody);
                        if (ao > 0f) c.BlendPixel(x, y, new Color(0f, 0f, 0f, aoAlpha), ao * Mathf.Clamp01(dBody - shoulderW + 0.5f));
                        if (shoulders)
                        {
                            float sc = Mathf.Clamp01(0.5f - (dBody - shoulderW));
                            if (sc > 0f) c.BlendPixel(x, y, shoulderCol, sc * 0.9f);
                        }
                        if (bodyCov <= 0f) continue;
                    }

                    // Surface: textured asphalt / deck, darker toward the side edges, tyre marks after touchdown.
                    float tex = 1f + (MapKit.Hash01(x, y, seed) - 0.5f) * 0.07f
                                   + (Mathf.PerlinNoise(u / 38f + ox, v / 38f + oy) - 0.5f) * 0.12f;
                    float k = tex * (1f - 0.13f * MapKit.Smooth(hw - 7f, hw, av));
                    if (!deck && u > keyStart + keyLen && u < len * 0.36f && av < inner * 0.62f)
                    {
                        float streak = Mathf.PerlinNoise(u / 60f + ox * 1.3f, v / 2.6f + oy);
                        float fade = 1f - MapKit.Smooth(len * 0.12f, len * 0.36f, u);
                        k *= 1f - 0.22f * MapKit.Smooth(0.52f, 0.68f, streak) * fade;
                    }
                    Color col = MapKit.Shade(u < 0f ? stubCol : baseCol, k);

                    // White markings.
                    float wCov = 0f;
                    float dStripe = Mathf.Max(Mathf.Max(stripeInner - av, av - stripeOuter), Mathf.Max(barLen - u, u - (len - 1.5f)));
                    wCov = Mathf.Max(wCov, Mathf.Clamp01(0.5f - dStripe));

                    float avk = av - keyCenterGap;
                    if (avk > -2f && av < inner + 2f)
                    {
                        int ki = Mathf.Clamp(Mathf.FloorToInt(avk / slot), 0, keys - 1);
                        float local = avk - (ki + 0.5f) * slot;
                        float dKey = Mathf.Max(Mathf.Abs(local) - keyHalfW, Mathf.Abs(u - (keyStart + keyLen * 0.5f)) - keyLen * 0.5f);
                        wCov = Mathf.Max(wCov, Mathf.Clamp01(0.5f - dKey));
                    }

                    float dAim = Mathf.Max(Mathf.Abs(av - aimV) - aimHalfW, Mathf.Abs(u - (aimStart + aimLen * 0.5f)) - aimLen * 0.5f);
                    wCov = Mathf.Max(wCov, Mathf.Clamp01(0.5f - dAim));

                    if (u > dashStart - 2f && u < dashEnd + 2f)
                    {
                        float m = Mathf.Repeat(u - dashStart, dashPeriod);
                        float dd = m < dashLen ? -Mathf.Min(m, dashLen - m) : Mathf.Min(m - dashLen, dashPeriod - m);
                        dd = Mathf.Max(Mathf.Max(dd, av - centerHalf), Mathf.Max(dashStart - u, u - dashEnd));
                        wCov = Mathf.Max(wCov, Mathf.Clamp01(0.5f - dd));
                    }

                    float dEnd = Mathf.Max(Mathf.Abs(u - endBarU) - endBarHalf, av - stripeOuter);
                    wCov = Mathf.Max(wCov, Mathf.Clamp01(0.5f - dEnd) * 0.85f);
                    if (wCov > 0f) col = MapKit.Mix(col, MapKit.Shade(MarkWhite, Mathf.Lerp(1f, tex, 0.5f)), wCov * 0.95f);

                    // Landing-color markings: edge bands, threshold bar, approach chevrons.
                    float cCov = Mathf.Clamp01(0.5f + av - (hw - band));
                    float dBar = Mathf.Max(Mathf.Abs(u - barLen * 0.5f) - barLen * 0.5f, av - (hw - band) - 0.5f);
                    cCov = Mathf.Max(cCov, Mathf.Clamp01(0.5f - dBar));
                    if (u < 2f)
                    {
                        float dc = Mathf.Min(Chevron(u, av, chevTip1, chevBack, chevSpread, chevR),
                            Chevron(u, av, chevTip2, chevBack, chevSpread, chevR));
                        cCov = Mathf.Max(cCov, Mathf.Clamp01(0.5f - dc));
                    }
                    if (cCov > 0f) col = MapKit.Mix(col, MapKit.Shade(mark, Mathf.Lerp(1f, tex, 0.4f)), cCov);

                    c.BlendPixel(x, y, col, bodyCov);
                }
            }
        }

        /// <summary>Distance to a symmetric chevron (tip at (tipU, 0), arms back to (tipU - back, +-spread)) in (u, |v|) space.</summary>
        private static float Chevron(float u, float av, float tipU, float back, float spread, float radius)
        {
            float pax = u - tipU, pay = av;
            float bax = -back, bay = spread;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        // ------------------------------------------------------------------------------------------------ water lane

        /// <summary>Paints a seaplane water lane on top of already painted water.</summary>
        public static void PaintWaterLane(PixelCanvas c, LandingSpotDef spot, int seed)
        {
            Color mark = LandingColors.WaterLane;
            Vector2 t = MapKit.Px(spot.position);
            Vector2 d = MapKit.Dir(spot.angleDeg);
            Vector2 n = MapKit.Perp(d);
            float len = MapKit.Len(Mathf.Max(0.5f, spot.length));
            float hw = MapKit.Len(Mathf.Max(0.2f, spot.width)) * 0.5f;
            float lead = MapKit.Len(LaneLeadIn);

            float chevBack = MapKit.Len(0.18f), chevSpread = hw * 0.74f, chevR = MapKit.Len(0.08f) * 0.5f;
            float[] tips = { -MapKit.Len(0.1f), -MapKit.Len(0.37f), -MapKit.Len(0.64f) };
            float dashStart = MapKit.Len(0.55f), dashEnd = len - MapKit.Len(0.3f);
            float dashLen = MapKit.Len(0.2f), dashPeriod = MapKit.Len(0.48f);
            Color calm = MapKit.Mix(new Color(0.86f, 0.94f, 1f, 1f), mark, 0.3f);
            Color rope = new Color(1f, 1f, 1f, 0.6f);
            Color outline = new Color(1f, 1f, 1f, 0.95f);

            const float pad = 12f;
            Vector2[] corners =
            {
                t + d * (-lead - pad) + n * (hw + pad), t + d * (-lead - pad) - n * (hw + pad),
                t + d * (len + pad) + n * (hw + pad), t + d * (len + pad) - n * (hw + pad),
            };
            if (MapKit.Clip(c, Sdf.Bounds(corners, 1f), out int x0, out int y0, out int x1, out int y1))
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float rx = x + 0.5f - t.x, ry = y + 0.5f - t.y;
                        float u = rx * d.x + ry * d.y;
                        float v = rx * n.x + ry * n.y;
                        float av = Mathf.Abs(v);

                        // Calm, slightly purple-tinted water inside the lane.
                        float dLane = MapKit.BoxSdf(u - len * 0.5f, v, len * 0.5f, hw, 6f);
                        float inside = 1f - MapKit.Smooth(-7f, 1f, dLane);
                        if (inside > 0f) c.BlendPixel(x, y, MapKit.Alpha(calm, 0.24f), inside);

                        // Ropes along both edges between the buoys.
                        float dRope = Mathf.Max(Mathf.Abs(av - hw) - 0.65f, Mathf.Max(-u, u - len));
                        if (dRope < 1f) c.BlendPixel(x, y, rope, Mathf.Clamp01(0.5f - dRope));

                        // Faint centre streaks.
                        if (u > dashStart - 2f && u < dashEnd + 2f && av < 3f)
                        {
                            float m = Mathf.Repeat(u - dashStart, dashPeriod);
                            float dd = m < dashLen ? -Mathf.Min(m, dashLen - m) : Mathf.Min(m - dashLen, dashPeriod - m);
                            dd = Mathf.Max(Mathf.Max(dd, av - 1.1f), Mathf.Max(dashStart - u, u - dashEnd));
                            if (dd < 1f) c.BlendPixel(x, y, new Color(1f, 1f, 1f, 0.45f), Mathf.Clamp01(0.5f - dd));
                        }

                        // Approach chevrons: white outline, then landing color.
                        if (u < 2f && u > -lead - 4f)
                        {
                            float dc = float.MaxValue;
                            for (int i = 0; i < tips.Length; i++) dc = Mathf.Min(dc, Chevron(u, av, tips[i], chevBack, chevSpread, chevR));
                            if (dc < 4f)
                            {
                                c.BlendPixel(x, y, outline, Mathf.Clamp01(0.5f - (dc - 2.2f)));
                                c.BlendPixel(x, y, mark, Mathf.Clamp01(0.5f - dc));
                            }
                        }
                    }
                }
            }

            // Two rows of buoys marking the lane edges (larger ones at the threshold).
            int count = Mathf.Max(3, Mathf.RoundToInt(spot.length / 0.42f) + 1);
            for (int i = 0; i < count; i++)
            {
                float u = len * i / (count - 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 p = t + d * u + n * (side * hw);
                    Buoy(c, p, i == 0 ? 7.5f : 5.8f, mark, seed + i * 7 + side);
                }
            }
        }

        private static void Buoy(PixelCanvas c, Vector2 p, float r, Color color, int seed)
        {
            c.StrokeCircle(p, r + 4.6f, 1.2f, new Color(1f, 1f, 1f, 0.3f));
            MapKit.SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), MapKit.ShadowOffset(0.035f), 1.6f, 0.35f);
            c.FillCircle(p, r + 1.7f, new Color(0.98f, 0.98f, 1f, 0.95f));
            MapKit.Blob(c, p, r, color, seed, 0f, 0f, 0.85f);
            c.FillCircle(p + new Vector2(-r * 0.33f, r * 0.36f), r * 0.28f, new Color(1f, 1f, 1f, 0.75f));
        }

        // ------------------------------------------------------------------------------------------------ helipad

        /// <summary>Paints a helipad: dark disc, white rim, landing-color ring and a big "H" (world units).</summary>
        public static void PaintHelipad(PixelCanvas c, Vector2 centerW, float diameterW, int seed)
        {
            Color yellow = LandingColors.Helipad;
            Color padCol = new Color(0.29f, 0.31f, 0.33f, 1f);
            Vector2 p = MapKit.Px(centerW);
            float r = MapKit.Len(Mathf.Max(0.2f, diameterW) * 0.5f);
            const float ext = 6f;
            if (!MapKit.Clip(c, PixelCanvas.Square(p, r + ext), out int x0, out int y0, out int x1, out int y1)) return;

            float ringR = r * 0.74f, ringHalf = r * 0.065f;
            float hHalfH = r * 0.43f, barX = r * 0.27f, barHalfW = r * 0.085f, crossHalfH = r * 0.075f;
            float ox = MapKit.SeedOffset(seed, 71), oy = MapKit.SeedOffset(seed, 72);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float dd = dist - r;
                    if (dd > ext) continue;
                    if (dd > -0.5f)
                    {
                        float ao = 1f - MapKit.Smooth(0f, ext, dd);
                        c.BlendPixel(x, y, new Color(0f, 0f, 0f, 0.3f), ao * Mathf.Clamp01(dd + 0.5f));
                    }
                    float cov = Mathf.Clamp01(0.5f - dd);
                    if (cov <= 0f) continue;

                    float tex = 1f + (MapKit.Hash01(x, y, seed) - 0.5f) * 0.06f + (Mathf.PerlinNoise(dx / 20f + ox, dy / 20f + oy) - 0.5f) * 0.1f;
                    Color col = MapKit.Shade(padCol, tex);
                    float dWhite = Mathf.Abs(dist - (r - 3.2f)) - 1.3f;
                    col = MapKit.Mix(col, MarkWhite, Mathf.Clamp01(0.5f - dWhite) * 0.9f);

                    float dRing = Mathf.Abs(dist - ringR) - ringHalf;
                    float dV = Mathf.Max(Mathf.Abs(Mathf.Abs(dx) - barX) - barHalfW, Mathf.Abs(dy) - hHalfH);
                    float dC = Mathf.Max(Mathf.Abs(dx) - barX, Mathf.Abs(dy) - crossHalfH);
                    float dH = Mathf.Min(dV, dC);
                    float dark = Mathf.Max(Mathf.Clamp01(0.5f - (dH - 1.5f)), Mathf.Clamp01(0.5f - (dRing - 1.5f)));
                    col = MapKit.Mix(col, MapKit.Shade(col, 0.72f), dark * 0.6f);
                    float yCov = Mathf.Max(Mathf.Clamp01(0.5f - dRing), Mathf.Clamp01(0.5f - dH));
                    col = MapKit.Mix(col, MapKit.Shade(yellow, Mathf.Lerp(1f, tex, 0.4f)), yCov);
                    c.BlendPixel(x, y, col, cov);
                }
            }
        }
    }
}
