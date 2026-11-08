using System;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Paints <see cref="ArtShape"/>s onto a <see cref="PixelCanvas"/> in a local coordinate system whose origin is
    /// <see cref="Origin"/> (canvas pixels, y up). Adds flat-vector "part" shading lit from the top-left.
    /// </summary>
    internal sealed class ArtBrush
    {
        /// <summary>Direction towards the light (top-left), unit length.</summary>
        public static readonly Vector2 LightDir = new Vector2(-0.70710677f, 0.70710677f);

        /// <summary>Dark seam drawn around every part so overlapping parts read clearly.</summary>
        public static readonly Color EdgeColor = ArtKit.Gray(0.2f);

        /// <summary>Target canvas.</summary>
        public readonly PixelCanvas Canvas;

        /// <summary>Canvas position of local (0, 0).</summary>
        public readonly Vector2 Origin;

        /// <summary>Strength of the global top-left (lighter) to bottom-right (darker) value gradient in shaded fills.</summary>
        public float Tilt = 0.03f;

        private readonly float _extent;

        /// <summary>Brush whose origin is the canvas center.</summary>
        public ArtBrush(PixelCanvas canvas) : this(canvas, canvas.CenterPoint) { }

        /// <summary>Brush whose local origin sits at canvas position <paramref name="origin"/>.</summary>
        public ArtBrush(PixelCanvas canvas, Vector2 origin)
        {
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            Origin = origin;
            _extent = Mathf.Max(1f, Mathf.Min(canvas.Width, canvas.Height) * 0.5f);
        }

        /// <summary>Anti-aliased solid fill.</summary>
        public void Fill(ArtShape shape, Color color)
        {
            Func<Vector2, float> d = shape.Distance;
            Vector2 o = Origin;
            Canvas.FillSdf(p => d(p - o), ToCanvas(shape.Bounds), color);
        }

        /// <summary>Anti-aliased fill whose color comes from <paramref name="shader"/> (local coordinates).</summary>
        public void Fill(ArtShape shape, Func<Vector2, Color> shader)
        {
            Func<Vector2, float> d = shape.Distance;
            Vector2 o = Origin;
            Canvas.FillSdf(p => d(p - o), ToCanvas(shape.Bounds), p => shader(p - o));
        }

        /// <summary>Blends <paramref name="shader"/> over every pixel of <paramref name="localBounds"/> (no mask).</summary>
        public void Shade(Rect localBounds, Func<Vector2, Color> shader)
        {
            Vector2 o = Origin;
            Canvas.Shade(ToCanvas(localBounds), p => shader(p - o));
        }

        /// <summary>
        /// Gray fill with a soft bevel: edges facing the light brighten by up to <paramref name="highlight"/>, edges facing
        /// away darken by up to <paramref name="shadow"/>, fading out <paramref name="bevel"/> px inside the shape.
        /// </summary>
        public void Shaded(ArtShape shape, float baseValue, float bevel, float highlight, float shadow, float alpha = 1f)
            => Fill(shape, p => ArtKit.Gray(Value(shape, p, baseValue, bevel, highlight, shadow), alpha));

        /// <summary>A shaded part with a dark seam of <paramref name="edge"/> px around it.</summary>
        public void Part(ArtShape shape, float baseValue, float bevel = 5f, float highlight = 0.05f, float shadow = 0.2f, float edge = 1.5f)
        {
            if (edge > 0f) Fill(shape.Inflate(edge), EdgeColor);
            Shaded(shape, baseValue, bevel, highlight, shadow);
        }

        /// <summary>Round-capped line.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color)
            => Fill(ArtShape.Segment(a, b, width), color);

        /// <summary>Round-capped line clipped to <paramref name="clip"/>.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color, ArtShape clip)
            => Fill(ArtShape.Segment(a, b, width).Intersect(clip), color);

        /// <summary>Translucent black copy of <paramref name="caster"/> shifted by <paramref name="offset"/>, painted only on <paramref name="receiver"/>.</summary>
        public void CastShadow(ArtShape caster, ArtShape receiver, Vector2 offset, float alpha)
            => Fill(caster.Translate(offset).Intersect(receiver), ArtKit.Gray(0f, alpha));

        /// <summary>The shaded gray value used by <see cref="Shaded"/> at local point <paramref name="p"/>.</summary>
        public float Value(ArtShape shape, Vector2 p, float baseValue, float bevel, float highlight, float shadow)
        {
            float depth = -shape.Distance(p);
            float falloff = 1f - ArtKit.Smooth01(depth / Mathf.Max(bevel, 0.01f));
            float light = Vector2.Dot(Normal(shape, p), LightDir);
            float v = baseValue + (light >= 0f ? highlight * light : shadow * light) * falloff;
            v += Tilt * Vector2.Dot(p / _extent, LightDir);
            return v;
        }

        /// <summary>Outward unit normal of <paramref name="shape"/> at <paramref name="p"/> (central differences).</summary>
        public static Vector2 Normal(ArtShape shape, Vector2 p)
        {
            const float h = 0.6f;
            Func<Vector2, float> d = shape.Distance;
            float gx = d(new Vector2(p.x + h, p.y)) - d(new Vector2(p.x - h, p.y));
            float gy = d(new Vector2(p.x, p.y + h)) - d(new Vector2(p.x, p.y - h));
            float m = Mathf.Sqrt(gx * gx + gy * gy);
            return m > 1e-6f ? new Vector2(gx / m, gy / m) : Vector2.zero;
        }

        private Rect ToCanvas(Rect local)
        {
            float x0 = Mathf.Max(local.xMin + Origin.x, -2f);
            float y0 = Mathf.Max(local.yMin + Origin.y, -2f);
            float x1 = Mathf.Min(local.xMax + Origin.x, Canvas.Width + 2f);
            float y1 = Mathf.Min(local.yMax + Origin.y, Canvas.Height + 2f);
            if (x1 < x0) x1 = x0;
            if (y1 < y0) y1 = y0;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
