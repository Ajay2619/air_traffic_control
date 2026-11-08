using System.Collections.Generic;
using UnityEngine;
using static ClearToLand.EditorTools.ArtKit;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Paints the 128x128 UI icons: bold, rounded white glyphs on transparent, optically centered with ~16 px margins.
    /// </summary>
    internal static class IconPainter
    {
        /// <summary>icon_pause: two rounded bars.</summary>
        public static void PaintPause(PixelCanvas canvas)
            => Glyph(canvas, ArtShape.Box(V(-18f, 0f), V(11f, 37f), 5f).Union(ArtShape.Box(V(18f, 0f), V(11f, 37f), 5f)));

        /// <summary>icon_play: rounded triangle pointing right.</summary>
        public static void PaintPlay(PixelCanvas canvas)
            => Glyph(canvas, ArtShape.RoundedTriangle(V(-22f, -38f), V(-22f, 38f), V(42f, 0f), 7f));

        /// <summary>icon_fastforward: two rounded triangles pointing right.</summary>
        public static void PaintFastForward(PixelCanvas canvas)
            => Glyph(canvas, ArtShape.RoundedTriangle(V(-46f, -32f), V(-46f, 32f), V(0f, 0f), 5f)
                .Union(ArtShape.RoundedTriangle(V(-2f, -32f), V(-2f, 32f), V(44f, 0f), 5f)));

        /// <summary>icon_sound_on: speaker with two sound waves.</summary>
        public static void PaintSoundOn(PixelCanvas canvas)
        {
            const float x = 2f;
            ArtShape waves = ArtShape.Arc(V(x - 2f, 0f), 16f, 9f, 0f, 45f)
                .Union(ArtShape.Arc(V(x - 2f, 0f), 30f, 9f, 0f, 45f));
            Glyph(canvas, Speaker(x).Union(waves));
        }

        /// <summary>icon_sound_off: speaker with a cross.</summary>
        public static void PaintSoundOff(PixelCanvas canvas)
        {
            const float x = -4f;
            ArtShape cross = ArtShape.Segment(V(13f, -14f), V(37f, 14f), 10f)
                .Union(ArtShape.Segment(V(13f, 14f), V(37f, -14f), 10f));
            Glyph(canvas, Speaker(x).Union(cross));
        }

        /// <summary>icon_home: house with chimney and door cut-out.</summary>
        public static void PaintHome(PixelCanvas canvas)
        {
            ArtShape roof = ArtShape.RoundedTriangle(V(0f, 48f), V(-52f, 4f), V(52f, 4f), 5f);
            ArtShape body = ArtShape.Box(V(0f, -19f), V(33f, 27f), 5f);
            ArtShape chimney = ArtShape.Box(V(29f, 30f), V(6.5f, 13f), 2f);
            ArtShape door = ArtShape.Box(V(0f, -33f), V(9.5f, 16f), 4f);
            Glyph(canvas, ArtShape.UnionAll(roof, body, chimney).Subtract(door));
        }

        /// <summary>icon_restart: clockwise circular arrow.</summary>
        public static void PaintRestart(PixelCanvas canvas)
        {
            Vector2 c = V(0f, -4f);
            const float radius = 34f;
            const float endDeg = 87f;                     // arc runs clockwise from 3 deg round to 87 deg
            ArtShape arc = ArtShape.Arc(c, radius, 12f, 225f, 138f);
            Vector2 n = Dir(endDeg);
            Vector2 t = new Vector2(n.y, -n.x);          // clockwise tangent at the arrow end
            Vector2 e = c + n * radius;
            ArtShape head = ArtShape.RoundedTriangle(e - t * 3f + n * 16f, e - t * 3f - n * 16f, e + t * 17f, 2.5f);
            Glyph(canvas, arc.Union(head));
        }

        /// <summary>icon_help: question mark.</summary>
        public static void PaintHelp(PixelCanvas canvas)
        {
            var pts = new List<Vector2>();
            Vector2 c = V(0f, 18f);
            const float radius = 20f;
            for (int i = 0; i <= 16; i++) pts.Add(c + Dir(Mathf.Lerp(162f, -48f, i / 16f)) * radius);
            pts.Add(V(1.5f, -6f));
            pts.Add(V(0f, -13f));
            ArtShape hook = ArtShape.Polyline(13f, pts.ToArray());
            Glyph(canvas, hook.Union(ArtShape.Circle(V(0f, -34f), 8.5f)));
        }

        /// <summary>icon_close: bold X.</summary>
        public static void PaintClose(PixelCanvas canvas)
            => Glyph(canvas, ArtShape.Segment(V(-30f, -30f), V(30f, 30f), 15f).Union(ArtShape.Segment(V(-30f, 30f), V(30f, -30f), 15f)));

        /// <summary>icon_star: rounded five-point star.</summary>
        public static void PaintStar(PixelCanvas canvas)
            => Glyph(canvas, ArtShape.Star(V(0f, -4f), 5, 47f, 19f, 90f).Inflate(3f));

        private static ArtShape Speaker(float x)
        {
            ArtShape box = ArtShape.Box(V(x - 26f, 0f), V(10f, 14f), 3f);
            ArtShape cone = ArtShape.Polygon(V(x - 22f, -12f), V(x - 22f, 12f), V(x - 3f, 31f), V(x - 3f, -31f)).Inflate(3f);
            return box.Union(cone);
        }

        private static void Glyph(PixelCanvas canvas, ArtShape shape)
            => new ArtBrush(canvas) { Tilt = 0f }.Fill(shape, Color.white);
    }
}
