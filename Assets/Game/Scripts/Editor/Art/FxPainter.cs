using UnityEngine;
using static ClearToLand.EditorTools.ArtKit;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Paints the world-space effect sprites (rings, glows, warning marker, chevron, debris). They are white so the
    /// gameplay code can tint them; dark accents (warning glyph, rims) survive tinting.
    /// </summary>
    internal static class FxPainter
    {
        /// <summary>fx_ring (256x256): crisp white ring ~12 px thick with a faint soft halo.</summary>
        public static void PaintRing(PixelCanvas canvas)
        {
            Vector2 c = canvas.CenterPoint;
            const float thickness = 12f, glow = 9f;
            float radius = canvas.Width * 0.5f - thickness * 0.5f - glow; // halo fades out exactly at the edge
            float half = thickness * 0.5f;
            canvas.Shade(canvas.FullRect, p =>
            {
                float d = Mathf.Abs((p - c).magnitude - radius) - half;
                if (d <= 0f) return Transparent;
                float k = 1f - Mathf.Clamp01(d / glow);
                return White(0.2f * k * k);
            });
            canvas.FillSdf(p => Sdf.Ring(p, c, radius, thickness), canvas.FullRect, Color.white);
        }

        /// <summary>fx_soft_circle (128x128): white disc with a smooth radial alpha falloff to the edge.</summary>
        public static void PaintSoftCircle(PixelCanvas canvas)
        {
            Vector2 c = canvas.CenterPoint;
            float radius = canvas.Width * 0.5f - 0.5f;
            canvas.Shade(canvas.FullRect, p =>
            {
                float t = Mathf.Clamp01((p - c).magnitude / radius);
                float k = 1f - t * t;
                return White(k * k);
            });
        }

        /// <summary>fx_debris (32x16): two-tone faceted shard.</summary>
        public static void PaintDebris(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas, Vector2.zero) { Tilt = 0f };
            ArtShape shard = ArtShape.Polygon(V(2.5f, 7.5f), V(9f, 13f), V(19f, 12f), V(29.5f, 8.5f), V(21f, 3f), V(8f, 2.5f));
            b.Fill(shard, Gray(1f));
            b.Fill(ArtShape.Polygon(V(2.5f, 7.5f), V(29.5f, 8.5f), V(21f, 3f), V(8f, 2.5f)), Gray(0.72f));
            b.Line(V(4f, 7.6f), V(28f, 8.5f), 0.8f, Gray(1f, 0.6f), shard.Inflate(-0.5f));
            canvas.AddOutline(Gray(0.42f), 0.8f);
        }

        /// <summary>fx_edge_warning (128x128): white disc with a thin dark rim and a dark-gray "!" glyph.</summary>
        public static void PaintEdgeWarning(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas) { Tilt = 0f };
            const float face = 57f;
            b.Fill(ArtShape.Circle(Vector2.zero, 61.5f), Gray(0.16f));
            b.Fill(ArtShape.Circle(Vector2.zero, face),
                p => Gray(1f - 0.08f * Smooth01(0.5f - Vector2.Dot(p, ArtBrush.LightDir) / (2f * face))));
            ArtShape glyph = ArtShape.Capsule(V(0f, 27f), V(0f, -5f), 8.5f, 6f)
                .Union(ArtShape.Circle(V(0f, -26f), 7.5f));
            b.Fill(glyph, Gray(0.16f));
        }

        /// <summary>
        /// fx_spot_glow_runway (128x128, 9-slice border 48): translucent white wash in the stretchable center, a bright
        /// rim right at the slice border (the runway edge when sliced to runway size + 0.5) and a soft halo outside.
        /// </summary>
        public static void PaintSpotGlowRunway(PixelCanvas canvas)
        {
            Vector2 c = canvas.CenterPoint;
            const float border = 48f;
            Vector2 innerHalf = new Vector2(canvas.Width * 0.5f - border, canvas.Height * 0.5f - border);
            canvas.Shade(canvas.FullRect, p => White(GlowAlpha(Sdf.Box(p, c, innerHalf), 6f, border - 6f)));
        }

        /// <summary>
        /// fx_spot_glow_pad (128x128): radial version of the runway glow. When scaled to pad diameter + 0.6 units the
        /// bright rim sits just outside a 1.0-unit pad.
        /// </summary>
        public static void PaintSpotGlowPad(PixelCanvas canvas)
        {
            Vector2 c = canvas.CenterPoint;
            float half = canvas.Width * 0.5f;
            float padRadius = half * (1.0f / 1.6f); // pad diameter 1.0 inside a sprite sized 1.0 + 0.6
            const float rim = 6f;
            float halo = half - 0.5f - padRadius - rim;
            canvas.Shade(canvas.FullRect, p => White(GlowAlpha((p - c).magnitude - padRadius, rim, halo)));
        }

        /// <summary>fx_chevron (64x64): bold white chevron pointing +X with a faint dark rim for contrast.</summary>
        public static void PaintChevron(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas) { Tilt = 0f };
            ArtShape chevron = ArtShape.Polygon(V(-17f, 22f), V(-5f, 22f), V(17f, 0f), V(-5f, -22f), V(-17f, -22f), V(5f, 0f))
                .Inflate(2f);
            b.Fill(chevron.Inflate(2f), Gray(0.08f, 0.35f));
            b.Fill(chevron, Color.white);
        }

        /// <summary>
        /// Alpha profile shared by the spot glows, by signed distance <paramref name="sd"/> from the spot edge:
        /// interior wash, bright rim of <paramref name="rimWidth"/> px, then a halo fading over <paramref name="haloWidth"/> px.
        /// </summary>
        private static float GlowAlpha(float sd, float rimWidth, float haloWidth)
        {
            const float interior = 0.45f, haloStart = 0.5f;
            if (sd <= -0.5f) return interior;
            if (sd < 0.5f) return Mathf.Lerp(interior, 1f, sd + 0.5f);
            if (sd <= rimWidth - 0.5f) return 1f;
            if (sd < rimWidth + 0.5f) return Mathf.Lerp(1f, haloStart, sd - (rimWidth - 0.5f));
            float k = 1f - Mathf.Clamp01((sd - rimWidth) / Mathf.Max(haloWidth, 0.01f));
            return haloStart * k * k;
        }
    }
}
