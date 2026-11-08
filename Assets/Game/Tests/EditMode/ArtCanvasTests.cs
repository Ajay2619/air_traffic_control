using System;
using ClearToLand.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>Editor art foundation: <see cref="Sdf"/> signs and <see cref="PixelCanvas"/> rasterization / PNG output.</summary>
    public sealed class ArtCanvasTests
    {
        private const float Eps = 1e-3f;

        // ---------------------------------------------------------------- Sdf

        [Test]
        public void Circle_NegativeInside_ZeroOnEdge_PositiveOutside()
        {
            Vector2 c = new Vector2(10f, 10f);
            Assert.That(Sdf.Circle(c, c, 5f), Is.EqualTo(-5f).Within(Eps));
            Assert.That(Sdf.Circle(new Vector2(15f, 10f), c, 5f), Is.EqualTo(0f).Within(Eps));
            Assert.That(Sdf.Circle(new Vector2(10f, 18f), c, 5f), Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void Ring_IsNegativeOnlyNearTheRadius()
        {
            Vector2 c = Vector2.zero;
            Assert.That(Sdf.Ring(new Vector2(10f, 0f), c, 10f, 2f), Is.LessThan(0f));
            Assert.That(Sdf.Ring(c, c, 10f, 2f), Is.GreaterThan(0f), "Ring center is empty.");
            Assert.That(Sdf.Ring(new Vector2(20f, 0f), c, 10f, 2f), Is.GreaterThan(0f));
        }

        [Test]
        public void Box_InsideNegative_OutsidePositive_DistanceExact()
        {
            Vector2 c = new Vector2(5f, 5f);
            Vector2 half = new Vector2(4f, 2f);
            Assert.That(Sdf.Box(c, c, half), Is.EqualTo(-2f).Within(Eps));
            Assert.That(Sdf.Box(new Vector2(12f, 5f), c, half), Is.EqualTo(3f).Within(Eps));
            Assert.That(Sdf.Box(new Vector2(5f, 10f), c, half), Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void Box_Rotated90_SwapsExtents()
        {
            Vector2 half = new Vector2(4f, 1f);
            Assert.That(Sdf.Box(new Vector2(0f, 3f), Vector2.zero, half, 0f, 90f), Is.LessThan(0f));
            Assert.That(Sdf.Box(new Vector2(3f, 0f), Vector2.zero, half, 0f, 90f), Is.GreaterThan(0f));
        }

        [Test]
        public void Segment_IsCapsuleAroundLine()
        {
            Vector2 a = Vector2.zero, b = new Vector2(10f, 0f);
            Assert.That(Sdf.Segment(new Vector2(5f, 0f), a, b, 1f), Is.EqualTo(-1f).Within(Eps));
            Assert.That(Sdf.Segment(new Vector2(5f, 3f), a, b, 1f), Is.EqualTo(2f).Within(Eps));
            Assert.That(Sdf.Segment(new Vector2(13f, 0f), a, b, 1f), Is.EqualTo(2f).Within(Eps), "Past the end cap.");
        }

        [Test]
        public void Polygon_SignIsIndependentOfWinding()
        {
            Vector2[] ccw = { new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(4f, 4f), new Vector2(0f, 4f) };
            Vector2[] cw = { new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(4f, 4f), new Vector2(4f, 0f) };
            Vector2 inside = new Vector2(2f, 1f), outside = new Vector2(7f, 2f);
            Assert.That(Sdf.Polygon(inside, ccw), Is.EqualTo(-1f).Within(Eps));
            Assert.That(Sdf.Polygon(inside, cw), Is.EqualTo(-1f).Within(Eps));
            Assert.That(Sdf.Polygon(outside, ccw), Is.EqualTo(3f).Within(Eps));
            Assert.That(Sdf.Polygon(outside, cw), Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void BooleanOps_CombineDistances()
        {
            Assert.That(Sdf.Union(-1f, 2f), Is.EqualTo(-1f));
            Assert.That(Sdf.Intersect(-1f, 2f), Is.EqualTo(2f));
            Assert.That(Sdf.Subtract(-1f, -3f), Is.EqualTo(3f));
            Assert.That(Sdf.SmoothUnion(1f, 1f, 0.5f), Is.LessThan(1f), "Smooth union bulges where shapes meet.");
        }

        // ---------------------------------------------------------------- PixelCanvas

        [Test]
        public void Constructor_NonPositiveSize_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PixelCanvas(0, 10));
            Assert.Throws<ArgumentException>(() => new PixelCanvas(10, -1));
        }

        [Test]
        public void NewCanvas_IsTransparent()
        {
            PixelCanvas canvas = new PixelCanvas(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    Assert.That(canvas[x, y].a, Is.EqualTo(0f));
        }

        [Test]
        public void FillCircle_CenterOpaque_FarOutsideTransparent()
        {
            PixelCanvas canvas = new PixelCanvas(64, 64);
            Color red = new Color(1f, 0f, 0f, 1f);
            canvas.FillCircle(new Vector2(32f, 32f), 10f, red);

            Color center = canvas[32, 32];
            Assert.That(center.a, Is.EqualTo(1f).Within(Eps));
            Assert.That(center.r, Is.EqualTo(1f).Within(Eps));
            Assert.That(center.g, Is.EqualTo(0f).Within(Eps));
            Assert.That(canvas[0, 0].a, Is.EqualTo(0f));
            Assert.That(canvas[63, 63].a, Is.EqualTo(0f));
            Assert.That(canvas[32 + 15, 32].a, Is.EqualTo(0f));
        }

        [Test]
        public void FillCircle_EdgeIsAntiAliased()
        {
            PixelCanvas canvas = new PixelCanvas(64, 64);
            canvas.FillCircle(new Vector2(32f, 32f), 9.75f, Color.white);
            // Pixel 41's center (41.5, 32.5) is ~0.24 px inside the rim -> partial coverage.
            bool foundPartial = false;
            for (int x = 38; x <= 45; x++)
            {
                float a = canvas[x, 32].a;
                if (a > 0.01f && a < 0.99f) foundPartial = true;
            }
            Assert.That(foundPartial, Is.True);
        }

        [Test]
        public void StrokeCircle_RimOpaque_CenterEmpty()
        {
            PixelCanvas canvas = new PixelCanvas(64, 64);
            canvas.StrokeCircle(new Vector2(32f, 32f), 20f, 4f, Color.white);
            Assert.That(canvas[32 + 20, 32].a, Is.GreaterThan(0.9f));
            Assert.That(canvas[32, 32].a, Is.EqualTo(0f));
        }

        [Test]
        public void BlendPixel_OutOfBounds_IsIgnored()
        {
            PixelCanvas canvas = new PixelCanvas(4, 4);
            Assert.DoesNotThrow(() =>
            {
                canvas.BlendPixel(-1, 0, Color.white);
                canvas.BlendPixel(0, 4, Color.white);
                canvas.BlendPixel(100, 100, Color.white);
            });
        }

        [Test]
        public void BlendPixel_HalfAlphaOverOpaque_MixesColors()
        {
            PixelCanvas canvas = new PixelCanvas(1, 1, Color.black);
            canvas.BlendPixel(0, 0, new Color(1f, 1f, 1f, 0.5f));
            Color c = canvas[0, 0];
            Assert.That(c.a, Is.EqualTo(1f).Within(Eps));
            Assert.That(c.r, Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void AddOutline_PaintsAroundShapeOnly()
        {
            PixelCanvas canvas = new PixelCanvas(32, 32);
            canvas.FillBox(new Vector2(16f, 16f), new Vector2(8f, 8f), 0f, 0f, Color.white);
            canvas.AddOutline(Color.black, 2f);
            Color outline = canvas[16 + 4, 16];  // pixel 20: center 0.5 px outside the box edge at x = 20
            Assert.That(outline.a, Is.GreaterThan(0.9f));
            Assert.That(outline.r, Is.LessThan(0.1f));
            Assert.That(canvas[16, 16].r, Is.GreaterThan(0.9f), "Shape interior keeps its color.");
            Assert.That(canvas[0, 0].a, Is.EqualTo(0f));
        }

        [Test]
        public void SilhouetteAndClone_AreIndependentCopies()
        {
            PixelCanvas canvas = new PixelCanvas(16, 16);
            canvas.FillCircle(new Vector2(8f, 8f), 4f, Color.white);
            PixelCanvas sil = canvas.Silhouette(Color.red);
            PixelCanvas clone = canvas.Clone();
            Assert.That(sil[8, 8].a, Is.EqualTo(canvas[8, 8].a).Within(Eps));
            Assert.That(sil[8, 8].g, Is.EqualTo(0f).Within(Eps));
            canvas.Clear(Color.clear);
            Assert.That(clone[8, 8].a, Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void ToTexture2D_HasCanvasSizeAndPixels()
        {
            PixelCanvas canvas = new PixelCanvas(12, 7);
            canvas[3, 4] = Color.green;
            Texture2D tex = canvas.ToTexture2D();
            try
            {
                Assert.That(tex.width, Is.EqualTo(12));
                Assert.That(tex.height, Is.EqualTo(7));
                Assert.That(tex.GetPixel(3, 4).g, Is.EqualTo(1f).Within(0.01f));
                Assert.That(tex.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void EncodeToPng_IsNonEmptyPngThatRoundTrips()
        {
            PixelCanvas canvas = new PixelCanvas(32, 16);
            canvas.FillCircle(new Vector2(8f, 8f), 6f, Color.white);
            byte[] png = canvas.EncodeToPng();
            Assert.That(png, Is.Not.Null);
            Assert.That(png.Length, Is.GreaterThan(8));
            Assert.That(png[0], Is.EqualTo(0x89));
            Assert.That(png[1], Is.EqualTo((byte)'P'));
            Assert.That(png[2], Is.EqualTo((byte)'N'));
            Assert.That(png[3], Is.EqualTo((byte)'G'));

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(tex.LoadImage(png), Is.True);
                Assert.That(tex.width, Is.EqualTo(32));
                Assert.That(tex.height, Is.EqualTo(16));
                Assert.That(tex.GetPixel(8, 8).a, Is.EqualTo(1f).Within(0.01f));
                Assert.That(tex.GetPixel(30, 8).a, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
