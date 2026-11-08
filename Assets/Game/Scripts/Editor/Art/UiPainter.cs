using System.Collections.Generic;
using UnityEngine;
using static ClearToLand.EditorTools.ArtKit;

namespace ClearToLand.EditorTools
{
    /// <summary>Paints the UI sprites: white 9-slice rounded rects, a circle and the title emblem.</summary>
    internal static class UiPainter
    {
        /// <summary>White rounded rectangle filling the whole canvas (ui_panel / ui_button / ui_card).</summary>
        public static void PaintRoundedRect(PixelCanvas canvas, float cornerRadius)
        {
            Vector2 c = canvas.CenterPoint;
            Vector2 half = canvas.Size * 0.5f;
            canvas.FillSdf(p => Sdf.Box(p, c, half, cornerRadius), canvas.FullRect, Color.white);
        }

        /// <summary>ui_circle: white filled circle touching the canvas edges.</summary>
        public static void PaintCircle(PixelCanvas canvas)
        {
            Vector2 c = canvas.CenterPoint;
            float r = Mathf.Min(canvas.Width, canvas.Height) * 0.5f;
            canvas.FillSdf(p => Sdf.Circle(p, c, r), canvas.FullRect, Color.white);
        }

        /// <summary>
        /// ui_logo (256x256): original emblem — a jet climbing out of a compass ring above a runway drawn in perspective.
        /// White and light grays only, with knock-out gaps around the jet so it reads on any background or tint.
        /// </summary>
        public static void PaintLogo(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas) { Tilt = 0f };

            // Jet (local nose +X), placed climbing to the top-right with its nose breaking the outer ring.
            ArtShape fuselage = ArtShape.Ellipse(V(40f, 0f), V(40f, 9f))
                .Union(ArtShape.Capsule(V(40f, 0f), V(-76f, 0f), 9f, 3.5f));
            ArtShape wings = ArtShape.MirroredPolygon(V(22f, 0f), V(-26f, 66f), V(-38f, 68f), V(-20f, 0f)).Inflate(1.5f);
            ArtShape tail = ArtShape.MirroredPolygon(V(-52f, 0f), V(-70f, 26f), V(-78f, 27f), V(-72f, 0f)).Inflate(1f);
            ArtShape cockpit = ArtShape.Ellipse(V(64f, 0f), V(6f, 7f))
                .Subtract(ArtShape.Ellipse(V(59.5f, 0f), V(6f, 8.5f)))
                .Intersect(fuselage.Inflate(-1.5f));
            ArtShape plane = PlaceJet(ArtShape.UnionAll(fuselage, wings, tail));
            ArtShape gap = plane.Inflate(5f);

            // Glassy inner disc.
            b.Fill(ArtShape.Circle(Vector2.zero, 87f).Subtract(gap), White(0.1f));

            // Runway in perspective, converging towards the center.
            ArtShape runway = ArtShape.Polygon(V(-33f, -84f), V(33f, -84f), V(10f, -6f), V(-10f, -6f))
                .Intersect(ArtShape.Circle(Vector2.zero, 84f));
            b.Fill(runway.Subtract(gap), Gray(0.74f));
            var marks = new List<ArtShape>
            {
                ArtShape.Segment(V(-29.6f, -82f), V(-8.4f, -9f), 2.2f),
                ArtShape.Segment(V(29.6f, -82f), V(8.4f, -9f), 2.2f),
                ArtShape.Segment(V(0f, -74f), V(0f, -60f), 3.2f),
                ArtShape.Segment(V(0f, -52f), V(0f, -42f), 2.8f),
                ArtShape.Segment(V(0f, -36f), V(0f, -29f), 2.4f),
                ArtShape.Segment(V(0f, -24f), V(0f, -19f), 2f),
                ArtShape.Segment(V(0f, -15f), V(0f, -12f), 1.6f),
            };
            for (int i = 0; i < 4; i++)
            {
                float x = 4f + 6.2f * i;
                marks.Add(ArtShape.Box(V(x, -79f), V(1.8f, 3.5f)));
                marks.Add(ArtShape.Box(V(-x, -79f), V(1.8f, 3.5f)));
            }
            b.Fill(ArtShape.UnionAll(marks.ToArray()).Intersect(runway.Inflate(-0.5f)).Subtract(gap), Gray(1f));

            // Compass: minor / major ticks, cardinal ticks, inner ring.
            var ticks = new List<ArtShape>();
            for (int i = 0; i < 36; i++)
            {
                Vector2 d = Dir(i * 10f);
                if (i % 9 == 0) ticks.Add(ArtShape.Segment(d * 92f, d * 101f, 3.4f));
                else ticks.Add(ArtShape.Segment(d * 95f, d * 101f, i % 3 == 0 ? 2.6f : 1.6f));
            }
            b.Fill(ArtShape.UnionAll(ticks.ToArray()).Subtract(gap), Gray(0.86f));
            b.Fill(ArtShape.Ring(Vector2.zero, 88.5f, 2.4f).Subtract(gap), Gray(0.86f));

            // Outer ring with a north pointer.
            ArtShape north = ArtShape.Polygon(V(0f, 124f), V(-8.5f, 107f), V(8.5f, 107f));
            b.Shaded(ArtShape.Ring(Vector2.zero, 110f, 9f).Union(north).Subtract(gap), 0.97f, 3f, 0.03f, 0.12f);

            // The jet itself.
            b.Shaded(plane, 1f, 7f, 0f, 0.16f);
            b.Fill(PlaceJet(cockpit), Gray(0.62f));
        }

        private static ArtShape PlaceJet(ArtShape shape) => shape.Rotate(40f, Vector2.zero).Translate(V(20f, 24f));
    }
}
