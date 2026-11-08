using System.Collections.Generic;
using UnityEngine;
using static ClearToLand.EditorTools.ArtKit;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Paints the aircraft sprites: original top-down designs with the nose pointing +X, light grayscale surfaces
    /// lit from the top-left, gray panel lines and a dark outline, so a runtime tint reads as the landing color.
    /// All coordinates are pixels relative to the canvas center (the sprite pivot).
    /// </summary>
    internal static class AircraftPainter
    {
        private static readonly Color OutlineColor = Gray(0.12f);
        private const float OutlineWidth = 1.1f;
        private static readonly Color PanelLine = Gray(0.38f, 0.5f);
        private static readonly Color SoftPanelLine = Gray(0.38f, 0.3f);
        private static readonly Vector2 ShadowOffset = new Vector2(2.5f, -3.5f); // light from the top-left
        private const float ShadowAlpha = 0.2f;

        /// <summary>aircraft_prop (128x128): small high-wing single-engine plane with a spinning prop disc.</summary>
        public static void PaintProp(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas);

            ArtShape stab = ArtShape.MirroredPolygon(V(-35f, 0f), V(-37.5f, 17f), V(-40.5f, 20f), V(-46.5f, 20f),
                V(-49f, 17f), V(-48.5f, 0f)).Inflate(0.8f);
            ArtShape fuselage = ArtShape.Capsule(V(34f, 0f), V(16f, 0f), 7.5f, 10f)
                .SmoothUnion(ArtShape.Capsule(V(16f, 0f), V(-46f, 0f), 10f, 3f), 4f);
            ArtShape fin = ArtShape.Capsule(V(-29f, 0f), V(-51f, 0f), 2.4f, 1.6f);
            ArtShape wing = ArtShape.MirroredPolygon(V(8f, 0f), V(6.5f, 54.5f), V(-1.5f, 54.5f), V(-4.5f, 0f)).Inflate(4f);
            ArtShape spinner = ArtShape.Ellipse(V(42.5f, 0f), V(5f, 4.6f));

            // Tailplane with elevator hinge lines.
            b.Part(stab, 0.92f, 4f);
            MirroredLine(b, V(-44.2f, 2.5f), V(-45.2f, 18.5f), 1.1f, PanelLine, stab.Inflate(-1f));

            // Fuselage, darker engine cowling, windshield and rear window.
            b.Part(fuselage, 0.93f, 10f, 0.06f, 0.26f);
            b.Fill(fuselage.Intersect(ArtShape.HalfPlane(V(25f, 0f), Vector2.right)), Gray(0.25f, 0.14f));
            b.Line(V(25f, -11f), V(25f, 11f), 1.2f, PanelLine, fuselage.Inflate(-1f));
            Glass(b, ArtShape.Ellipse(V(12f, 0f), V(9.5f, 7.5f)).Intersect(fuselage.Inflate(-2.2f)));
            Glass(b, ArtShape.Ellipse(V(-9f, 0f), V(6f, 5.5f)).Intersect(fuselage.Inflate(-2.6f)));
            b.Part(fin, 0.86f, 2f, 0.12f, 0.2f, 1.2f);

            // High wing on top of everything, casting a soft shadow on the fuselage.
            b.CastShadow(wing, fuselage.Union(stab), ShadowOffset, ShadowAlpha);
            b.Part(wing, 0.95f, 5f, 0.05f, 0.2f);
            ArtShape wingInner = wing.Inflate(-1.2f);
            b.Fill(wing.Intersect(TipZones(53f)), Gray(0.2f, 0.12f));
            MirroredLine(b, V(-3.5f, 9f), V(-0.8f, 52f), 1.1f, PanelLine, wingInner);
            MirroredLine(b, V(-9.5f, 25f), V(-2.5f, 25f), 1.1f, PanelLine, wingInner);
            MirroredDot(b, V(3f, 36f), 1.3f, Gray(0.45f));
            b.Part(spinner, 0.8f, 3f, 0.15f, 0.22f, 1.2f);

            canvas.AddOutline(OutlineColor, OutlineWidth);
            PropDisc(b, V(44.5f, 0f), 27f);
        }

        /// <summary>aircraft_seaplane (152x152): high-wing floatplane with a radial cowling and two floats.</summary>
        public static void PaintSeaplane(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas);

            ArtShape floats = ArtShape.Capsule(V(50f, 24f), V(16f, 24f), 3.5f, 6.5f)
                .SmoothUnion(ArtShape.Capsule(V(16f, 24f), V(-34f, 24f), 6.5f, 3.5f), 3f).MirrorY();
            ArtShape struts = ArtShape.UnionAll(
                ArtShape.Segment(V(20f, 8f), V(20f, 21f), 2.6f),
                ArtShape.Segment(V(-15f, 7f), V(-15f, 21f), 2.6f)).MirrorY();
            ArtShape fuselage = ArtShape.Capsule(V(44f, 0f), V(20f, 0f), 10.5f, 11f)
                .SmoothUnion(ArtShape.Capsule(V(20f, 0f), V(-58f, 0f), 11f, 3.2f), 5f);
            ArtShape spinner = ArtShape.Ellipse(V(56f, 0f), V(5f, 4.8f));
            ArtShape stab = ArtShape.MirroredPolygon(V(-43f, 0f), V(-45.5f, 21f), V(-49f, 24.5f), V(-56f, 24.5f),
                V(-59.5f, 21f), V(-60f, 0f)).Inflate(0.8f);
            ArtShape fin = ArtShape.Capsule(V(-34f, 0f), V(-62.5f, 0f), 3f, 1.8f);
            ArtShape wing = ArtShape.MirroredPolygon(V(11f, 0f), V(10f, 64.5f), V(-6f, 64.5f), V(-7f, 0f)).Inflate(4f);

            // Floats with their step lines, then the struts that carry them.
            b.Part(floats, 0.9f, 5f, 0.06f, 0.24f);
            MirroredLine(b, V(-2f, 18.5f), V(-2f, 29.5f), 1.2f, PanelLine, floats.Inflate(-1f));
            MirroredLine(b, V(38f, 24f), V(8f, 24f), 1f, SoftPanelLine, floats.Inflate(-2f));
            b.Part(struts, 0.78f, 1.5f, 0.1f, 0.2f, 1.2f);

            // Tailplane.
            b.Part(stab, 0.92f, 4f);
            MirroredLine(b, V(-55.5f, 2.5f), V(-55.5f, 22f), 1.1f, PanelLine, stab.Inflate(-1f));

            // Fuselage with a blunt radial-engine cowling (dark open front), glazing and fin.
            b.Part(fuselage, 0.93f, 11f, 0.06f, 0.26f);
            b.Fill(fuselage.Intersect(ArtShape.HalfPlane(V(36f, 0f), Vector2.right)), Gray(0.25f, 0.16f));
            b.Fill(fuselage.Inflate(-1.2f).Intersect(ArtShape.HalfPlane(V(50.5f, 0f), Vector2.right)), Gray(0.15f, 0.4f));
            b.Line(V(36f, -12f), V(36f, 12f), 1.2f, PanelLine, fuselage.Inflate(-1f));
            Glass(b, ArtShape.Ellipse(V(15f, 0f), V(10f, 8.5f)).Intersect(fuselage.Inflate(-2.4f)));
            Glass(b, ArtShape.Ellipse(V(-11f, 0f), V(6.5f, 6f)).Intersect(fuselage.Inflate(-2.8f)));
            b.Line(V(-30f, -9f), V(-30f, 9f), 1.1f, SoftPanelLine, fuselage.Inflate(-1.5f));
            b.Part(fin, 0.86f, 2.5f, 0.12f, 0.2f, 1.2f);

            // High wing on top.
            b.CastShadow(wing, ArtShape.UnionAll(fuselage, floats, struts, stab), ShadowOffset, ShadowAlpha);
            b.Part(wing, 0.95f, 5f, 0.05f, 0.2f);
            ArtShape wingInner = wing.Inflate(-1.2f);
            b.Fill(wing.Intersect(TipZones(63f)), Gray(0.2f, 0.12f));
            MirroredLine(b, V(-5.5f, 10f), V(-4.5f, 62f), 1.1f, PanelLine, wingInner);
            MirroredLine(b, V(-11.5f, 31f), V(-5f, 31f), 1.1f, PanelLine, wingInner);
            MirroredDot(b, V(4f, 42f), 1.4f, Gray(0.45f));
            b.Part(spinner, 0.8f, 3f, 0.15f, 0.22f, 1.2f);

            canvas.AddOutline(OutlineColor, OutlineWidth);
            PropDisc(b, V(58.5f, 0f), 31f);
        }

        /// <summary>aircraft_airliner (208x208): twin-engine swept-wing airliner with winglets.</summary>
        public static void PaintAirliner(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas);

            ArtShape fuselage = ArtShape.Ellipse(V(64f, 0f), V(27f, 11.5f))
                .Union(ArtShape.Box(V(9f, 0f), V(55f, 11.5f)))
                .SmoothUnion(ArtShape.Capsule(V(-44f, 0f), V(-92f, 0f), 11.5f, 3.5f), 4f);
            ArtShape wings = ArtShape.MirroredPolygon(V(24f, 0f), V(-29.5f, 88.5f), V(-32.5f, 91.5f), V(-42f, 91.5f),
                V(-45f, 88.5f), V(-28f, 34f), V(-26f, 0f));
            ArtShape winglets = ArtShape.Capsule(V(-30.5f, 92.5f), V(-43.5f, 92.5f), 2.2f, 1.8f).MirrorY();
            ArtShape nacelles = ArtShape.Capsule(V(25f, 38f), V(-6f, 38f), 7.5f, 5.5f).MirrorY();
            ArtShape stabs = ArtShape.MirroredPolygon(V(-58f, 0f), V(-79f, 35f), V(-82f, 38f), V(-89f, 38f),
                V(-92f, 35f), V(-84f, 0f));
            ArtShape fin = ArtShape.Capsule(V(-54f, 0f), V(-96f, 0f), 3.4f, 1.6f);

            // Tailplane.
            b.Part(stabs, 0.92f, 5f);
            MirroredLine(b, V(-79.5f, 4f), V(-86.5f, 34f), 1.1f, PanelLine, stabs.Inflate(-1f));

            // Engines hang under the wing; only the front half with the dark intake shows.
            b.Part(nacelles, 0.9f, 7f, 0.06f, 0.26f);
            ArtShape intakes = nacelles.Intersect(ArtShape.HalfPlane(V(27.5f, 0f), Vector2.right));
            b.Fill(intakes, Gray(0.36f));
            b.Fill(intakes.Inflate(-1.6f), Gray(0.22f));
            MirroredLine(b, V(14f, 31f), V(14f, 45f), 1f, SoftPanelLine, nacelles.Inflate(-1f));

            // Wing: slats, flap / aileron hinge, spoilers.
            b.Part(wings, 0.94f, 6f, 0.05f, 0.2f);
            ArtShape wingInner = wings.Inflate(-1.2f);
            MirroredLine(b, V(10.9f, 13.9f), V(-30.2f, 81.9f), 1f, PanelLine, wingInner);
            b.Fill(ArtShape.Polyline(1.1f, V(-18f, 12f), V(-20f, 34f), V(-36.8f, 88f)).MirrorY().Intersect(wingInner), PanelLine);
            MirroredLine(b, V(-28.5f, 34f), V(-19.5f, 34f), 1.1f, PanelLine, wingInner);
            MirroredLine(b, V(-40.5f, 72f), V(-31.5f, 72f), 1.1f, PanelLine, wingInner);
            for (int i = 0; i < 3; i++)
            {
                float y = 44f + i * 8f;
                float x = -20f - 16.8f * ((y - 34f) / 54f);
                MirroredLine(b, V(x, y), V(x + 7f, y), 1f, SoftPanelLine, wingInner);
            }
            b.Part(winglets, 0.84f, 2f, 0.1f, 0.2f, 1.2f);

            // Fuselage over the wing root.
            b.CastShadow(fuselage, wings, ShadowOffset, ShadowAlpha);
            b.Part(fuselage, 0.94f, 11.5f, 0.06f, 0.28f);
            ArtShape fusInner = fuselage.Inflate(-1.5f);
            b.Line(V(50f, -12f), V(50f, 12f), 1f, SoftPanelLine, fusInner);
            b.Line(V(-34f, -12f), V(-34f, 12f), 1f, SoftPanelLine, fusInner);
            b.Fill(ArtShape.Circle(V(14f, 0f), 1.5f), Gray(0.45f));
            Glass(b, ArtShape.Ellipse(V(77f, 0f), V(7.5f, 9.5f))
                .Subtract(ArtShape.Ellipse(V(71.5f, 0f), V(7.5f, 11.5f)))
                .Intersect(fuselage.Inflate(-2f)));

            // Vertical fin (seen edge-on from above).
            b.CastShadow(fin, stabs.Union(fuselage), ShadowOffset, ShadowAlpha);
            b.Part(fin, 0.88f, 3f, 0.1f, 0.22f, 1.3f);

            canvas.AddOutline(OutlineColor, OutlineWidth);
        }

        /// <summary>aircraft_fighter (160x160): cranked-delta jet with leading-edge extensions and twin canted fins.</summary>
        public static void PaintFighter(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas);

            ArtShape fuselage = FighterFuselage();
            ArtShape wings = ArtShape.MirroredPolygon(V(46f, 0f), V(40f, 8f), V(26f, 13.5f), V(12f, 18f), V(-22f, 50f),
                V(-25.5f, 52.5f), V(-37f, 52.5f), V(-40.5f, 50f), V(-40f, 12f), V(-44f, 6f), V(-44f, 0f));
            ArtShape stabs = ArtShape.Polygon(V(-44f, 9f), V(-54f, 30f), V(-57f, 32.5f), V(-63f, 32.5f),
                V(-65.5f, 30f), V(-64f, 9f)).MirrorY();
            ArtShape fins = ArtShape.Capsule(V(-26f, 11f), V(-58f, 15.5f), 2.8f, 1.8f).MirrorY();
            ArtShape rails = ArtShape.Capsule(V(-16f, 53.5f), V(-42f, 53.5f), 2.2f, 2.4f).MirrorY();
            ArtShape nozzle = ArtShape.Box(V(-62.5f, 0f), V(5f, 7.2f), 2f);
            ArtShape canopy = ArtShape.Ellipse(V(34f, 0f), V(15f, 5.4f));

            // Tail surfaces and exhaust nozzle (under the fuselage).
            b.Part(stabs, 0.9f, 4f);
            b.Part(nozzle, 0.58f, 3f, 0.15f, 0.2f);
            b.Line(V(-60.5f, -7f), V(-60.5f, 7f), 1.1f, PanelLine, nozzle.Inflate(-0.5f));
            b.Line(V(-64.5f, -7f), V(-64.5f, 7f), 1.1f, PanelLine, nozzle.Inflate(-0.5f));

            // Delta wing with leading-edge flaps and flaperons, wingtip rails.
            b.Part(wings, 0.94f, 6f, 0.05f, 0.2f);
            ArtShape wingInner = wings.Inflate(-1.2f);
            MirroredLine(b, V(4.2f, 19.9f), V(-21.3f, 43.9f), 1f, PanelLine, wingInner);
            MirroredLine(b, V(-34.5f, 14f), V(-34.5f, 48f), 1.1f, PanelLine, wingInner);
            MirroredLine(b, V(-40.5f, 31f), V(-34.5f, 31f), 1.1f, PanelLine, wingInner);
            b.Part(rails, 0.88f, 2f, 0.1f, 0.2f, 1.2f);
            MirroredDot(b, V(-15.5f, 53.5f), 1.6f, Gray(0.4f));

            // Fuselage with radome seam, spine and bubble canopy.
            b.CastShadow(fuselage, wings.Union(stabs), ShadowOffset, ShadowAlpha);
            b.Part(fuselage, 0.94f, 10f, 0.06f, 0.28f);
            ArtShape fusInner = fuselage.Inflate(-1.5f);
            b.Line(V(57f, -9f), V(57f, 9f), 1f, PanelLine, fusInner);
            b.Line(V(17f, 0f), V(-56f, 0f), 1f, SoftPanelLine, fusInner);
            Glass(b, canopy);
            b.Line(V(43.5f, -6f), V(43.5f, 6f), 1.5f, Gray(0.78f), canopy);

            // Twin canted fins.
            b.CastShadow(fins, ArtShape.UnionAll(wings, stabs, fuselage), ShadowOffset, ShadowAlpha);
            b.Part(fins, 0.84f, 2f, 0.12f, 0.2f, 1.3f);

            canvas.AddOutline(OutlineColor, OutlineWidth);
        }

        /// <summary>aircraft_helicopter (144x144): cabin, skids, tail boom and tail rotor; the main-rotor mast is at the canvas center.</summary>
        public static void PaintHelicopter(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas);

            ArtShape skids = ArtShape.UnionAll(
                ArtShape.Capsule(V(30f, 21f), V(-22f, 21f), 2.2f, 2.2f),
                ArtShape.Capsule(V(30f, -21f), V(-22f, -21f), 2.2f, 2.2f),
                ArtShape.Segment(V(14f, -21f), V(14f, 21f), 3f),
                ArtShape.Segment(V(-10f, -21f), V(-10f, 21f), 3f));
            ArtShape cabin = ArtShape.Ellipse(V(16f, 0f), V(34f, 18f))
                .SmoothUnion(ArtShape.Capsule(V(4f, 0f), V(-24f, 0f), 17f, 10f), 8f);
            ArtShape body = cabin.SmoothUnion(ArtShape.Capsule(V(-24f, 0f), V(-58f, 0f), 6.5f, 3.2f), 5f);
            ArtShape stab = ArtShape.Box(V(-45f, 0f), V(3.5f, 13f), 3f);
            ArtShape fin = ArtShape.Capsule(V(-50f, 0f), V(-64f, 0f), 2.6f, 1.6f);
            ArtShape glass = cabin.Inflate(-2.4f).Intersect(ArtShape.Ellipse(V(40f, 0f), V(17f, 15.5f)));
            ArtShape cowl = ArtShape.Box(V(-10f, 0f), V(14f, 8.5f), 6f);
            ArtShape hub = ArtShape.Circle(Vector2.zero, 6f);

            // Landing skids peek out under the cabin.
            b.Part(skids, 0.74f, 1.5f, 0.12f, 0.2f, 1.2f);
            b.CastShadow(body, skids, ShadowOffset, 0.25f);

            // Cabin + tail boom, door seams, stabilizer and fin.
            b.Part(body, 0.93f, 12f, 0.06f, 0.27f);
            ArtShape bodyInner = body.Inflate(-1.5f);
            b.Line(V(21f, -18f), V(21f, 18f), 1.1f, PanelLine, bodyInner);
            b.Line(V(-4f, -18f), V(-4f, 18f), 1.1f, PanelLine, bodyInner);
            b.CastShadow(stab, body, ShadowOffset, ShadowAlpha);
            b.Part(stab, 0.9f, 3f);
            b.Part(fin, 0.86f, 2f, 0.12f, 0.2f, 1.2f);

            // Bubble windshield with a center post.
            Glass(b, glass);
            b.Line(V(56f, 0f), V(26f, 0f), 1.6f, Gray(0.75f), glass);

            // Engine cowling with exhausts, rotor mast.
            b.CastShadow(cowl, body, ShadowOffset, ShadowAlpha);
            b.Part(cowl, 0.86f, 5f, 0.08f, 0.22f, 1.3f);
            b.Line(V(-2f, -6f), V(-2f, 6f), 1f, SoftPanelLine, cowl.Inflate(-1f));
            b.Fill(ArtShape.Capsule(V(-14f, 5.6f), V(-22f, 5.6f), 1.2f, 1.6f).MirrorY(), Gray(0.3f, 0.85f));
            b.Part(hub, 0.62f, 3f, 0.15f, 0.2f, 1.2f);
            b.Fill(ArtShape.Circle(Vector2.zero, 2.4f), Gray(0.85f));

            canvas.AddOutline(OutlineColor, OutlineWidth);

            // Tail rotor, seen edge-on as a motion-blurred bar beside the fin.
            ArtShape tailDisc = ArtShape.Ellipse(V(-57f, 7.5f), V(10f, 2.6f));
            b.Fill(tailDisc, Gray(0.9f, 0.42f));
            b.Fill(tailDisc.Shell(0.9f), Gray(0.12f, 0.35f));
            b.Line(V(-65f, 7.5f), V(-49f, 7.5f), 1.1f, Gray(0.25f, 0.55f));
            b.Fill(ArtShape.Circle(V(-57f, 7.5f), 1.8f), Gray(0.3f));
        }

        /// <summary>aircraft_rotor (176x176): four dark main blades with a faint motion disc, centered on the mast.</summary>
        public static void PaintRotor(PixelCanvas canvas)
        {
            var b = new ArtBrush(canvas) { Tilt = 0f };
            const float tip = 82f;
            const int bladeCount = 4;
            const float firstBlade = 45f;
            float step = 360f / bladeCount;

            // Faint motion disc: fill, brighter blade-tip path and a thin rim.
            b.Fill(ArtShape.Circle(Vector2.zero, tip + 1f), Gray(0.92f, 0.1f));
            b.Fill(ArtShape.Ring(Vector2.zero, tip - 5f, 8f), Gray(0.95f, 0.12f));
            b.Fill(ArtShape.Ring(Vector2.zero, tip + 0.5f, 1.4f), Gray(0.95f, 0.28f));
            b.Shade(new Rect(-tip - 2f, -tip - 2f, tip * 2f + 4f, tip * 2f + 4f), p => BladeBlur(p, tip, bladeCount, firstBlade));

            // Blades, grips and hub on their own layer so only they get the dark outline.
            var layer = new PixelCanvas(canvas.Width, canvas.Height);
            var lb = new ArtBrush(layer) { Tilt = 0f };
            for (int i = 0; i < bladeCount; i++)
            {
                float angle = firstBlade + i * step;
                Vector2 dir = Dir(angle);
                ArtShape blade = ArtShape.Capsule(dir * 9f, dir * (tip - 3.2f), 4f, 3.2f);
                lb.Shaded(blade, 0.34f, 2.5f, 0.28f, 0.1f);
                lb.Fill(blade.Subtract(ArtShape.Circle(Vector2.zero, tip - 11f)), Gray(0.9f));
                lb.Shaded(ArtShape.Box(dir * 16f, V(7f, 5.2f), 1.5f, angle), 0.52f, 2f, 0.2f, 0.15f);
            }
            lb.Shaded(ArtShape.Circle(Vector2.zero, 12.5f), 0.56f, 5f, 0.22f, 0.2f);
            lb.Shaded(ArtShape.Circle(Vector2.zero, 6f), 0.82f, 3f, 0.12f, 0.2f);
            for (int i = 0; i < bladeCount; i++)
                lb.Fill(ArtShape.Circle(Dir(firstBlade + step * (i + 0.5f)) * 9.2f, 1.4f), Gray(0.3f));
            layer.AddOutline(OutlineColor, OutlineWidth);
            canvas.DrawCanvas(layer, 0, 0);
        }

        // ------------------------------------------------------------------ helpers

        private static ArtShape FighterFuselage()
        {
            var half = new List<Vector2>();
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                half.Add(new Vector2(Mathf.Lerp(74f, 12f, t), 11.5f * Mathf.Sin(t * Mathf.PI * 0.5f))); // ogive nose
            }
            half.Add(V(-32f, 11.5f));
            half.Add(V(-50f, 10f));
            half.Add(V(-60f, 8f));
            half.Add(V(-60f, 0f));
            return ArtShape.MirroredPolygon(half.ToArray()).Inflate(0.5f);
        }

        /// <summary>Dark tinted glazing with a glint along its top-left edge.</summary>
        private static void Glass(ArtBrush b, ArtShape glass)
        {
            b.Shaded(glass, 0.3f, 3f, 0.22f, 0.06f);
            ArtShape glint = glass.Inflate(-0.8f).Subtract(glass.Translate(new Vector2(1.8f, -1.8f)));
            b.Fill(glint, Gray(0.95f, 0.6f));
        }

        /// <summary>Spinning propeller seen edge-on: translucent disc with darker blade tips and a faint rim.</summary>
        private static void PropDisc(ArtBrush b, Vector2 center, float halfSpan)
        {
            ArtShape disc = ArtShape.Ellipse(center, V(2.8f, halfSpan));
            b.Fill(disc, Gray(0.9f, 0.38f));
            b.Fill(disc.Intersect(TipZones(halfSpan - 6f)), Gray(0.2f, 0.28f));
            b.Fill(disc.Shell(1f), Gray(0.12f, 0.3f));
        }

        /// <summary>Region with |y| greater than <paramref name="y"/> (wing tips, prop tips).</summary>
        private static ArtShape TipZones(float y)
            => ArtShape.HalfPlane(V(0f, y), Vector2.up).Union(ArtShape.HalfPlane(V(0f, -y), Vector2.down));

        private static void MirroredLine(ArtBrush b, Vector2 from, Vector2 to, float width, Color color, ArtShape clip)
            => b.Fill(ArtShape.Segment(from, to, width).MirrorY().Intersect(clip), color);

        private static void MirroredDot(ArtBrush b, Vector2 center, float radius, Color color)
            => b.Fill(ArtShape.Circle(center, radius).MirrorY(), color);

        private static Color BladeBlur(Vector2 p, float tip, int count, float firstAngle)
        {
            float r = p.magnitude;
            if (r < 10f || r > tip + 1f) return Transparent;
            float step = 360f / count;
            float rel = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg - firstAngle, step);
            float delta = Mathf.Min(rel, step - rel); // degrees to the nearest blade
            float a = 0.26f * (r / tip) * Mathf.Exp(-delta / 9f) * Mathf.Clamp01((tip + 1f - r) / 3f);
            return Gray(0.3f, a);
        }
    }
}
