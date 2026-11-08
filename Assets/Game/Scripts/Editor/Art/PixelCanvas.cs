using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// CPU raster canvas used to generate all game art. Pixel space: origin bottom-left (same as Texture2D),
    /// x right, y up, pixel centers at (x + 0.5, y + 0.5). Colors are stored straight (non-premultiplied)
    /// in sRGB and composited with source-over. All shape fills are anti-aliased via signed distances.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Color[] _px;

        public PixelCanvas(int width, int height) : this(width, height, new Color(0f, 0f, 0f, 0f)) { }

        public PixelCanvas(int width, int height, Color fill)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Canvas size must be positive");
            Width = width;
            Height = height;
            _px = new Color[width * height];
            Clear(fill);
        }

        public Vector2 Size => new Vector2(Width, Height);
        public Vector2 CenterPoint => new Vector2(Width * 0.5f, Height * 0.5f);
        public Rect FullRect => new Rect(0, 0, Width, Height);

        public Color this[int x, int y]
        {
            get => _px[y * Width + x];
            set => _px[y * Width + x] = value;
        }

        public Color GetPixelClamped(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Width - 1);
            y = Mathf.Clamp(y, 0, Height - 1);
            return _px[y * Width + x];
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < _px.Length; i++) _px[i] = c;
        }

        /// <summary>Source-over blend of <paramref name="c"/> scaled by <paramref name="coverage"/> onto pixel (x, y).</summary>
        public void BlendPixel(int x, int y, Color c, float coverage = 1f)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            float a = c.a * Mathf.Clamp01(coverage);
            if (a <= 0f) return;
            int i = y * Width + x;
            Color d = _px[i];
            float outA = a + d.a * (1f - a);
            if (outA <= 1e-6f) { _px[i] = new Color(0, 0, 0, 0); return; }
            float k = d.a * (1f - a);
            _px[i] = new Color(
                (c.r * a + d.r * k) / outA,
                (c.g * a + d.g * k) / outA,
                (c.b * a + d.b * k) / outA,
                outA);
        }

        /// <summary>Anti-aliased fill of the region where <paramref name="sdf"/> &lt; 0, evaluated only inside <paramref name="bounds"/>.</summary>
        public void FillSdf(Func<Vector2, float> sdf, Rect bounds, Color color)
        {
            ForEachPixel(bounds, (x, y, p) =>
            {
                float cov = Mathf.Clamp01(0.5f - sdf(p));
                if (cov > 0f) BlendPixel(x, y, color, cov);
            });
        }

        /// <summary>Like <see cref="FillSdf(Func{Vector2,float},Rect,Color)"/> but the color comes from a per-pixel shader.</summary>
        public void FillSdf(Func<Vector2, float> sdf, Rect bounds, Func<Vector2, Color> shader)
        {
            ForEachPixel(bounds, (x, y, p) =>
            {
                float cov = Mathf.Clamp01(0.5f - sdf(p));
                if (cov > 0f) BlendPixel(x, y, shader(p), cov);
            });
        }

        /// <summary>Blends <paramref name="shader"/> over every pixel in bounds (no shape mask).</summary>
        public void Shade(Rect bounds, Func<Vector2, Color> shader)
        {
            ForEachPixel(bounds, (x, y, p) => BlendPixel(x, y, shader(p), 1f));
        }

        public void FillCircle(Vector2 center, float radius, Color color)
            => FillSdf(p => Sdf.Circle(p, center, radius), Square(center, radius), color);

        public void StrokeCircle(Vector2 center, float radius, float thickness, Color color)
            => FillSdf(p => Sdf.Ring(p, center, radius, thickness), Square(center, radius + thickness), color);

        public void FillEllipse(Vector2 center, Vector2 radii, float angleDeg, Color color)
            => FillSdf(p => Sdf.Ellipse(p, center, radii, angleDeg), Square(center, Mathf.Max(radii.x, radii.y)), color);

        public void FillBox(Vector2 center, Vector2 size, float cornerRadius, float angleDeg, Color color)
        {
            Vector2 half = size * 0.5f;
            FillSdf(p => Sdf.Box(p, center, half, cornerRadius, angleDeg), Square(center, half.magnitude), color);
        }

        public void FillPolygon(IReadOnlyList<Vector2> points, Color color)
        {
            Vector2[] pts = Copy(points);
            FillSdf(p => Sdf.Polygon(p, pts), Sdf.Bounds(pts, 1f), color);
        }

        public void StrokeLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            float r = thickness * 0.5f;
            FillSdf(p => Sdf.Segment(p, a, b, r), Rect.MinMaxRect(Mathf.Min(a.x, b.x) - r, Mathf.Min(a.y, b.y) - r,
                Mathf.Max(a.x, b.x) + r, Mathf.Max(a.y, b.y) + r), color);
        }

        /// <summary>
        /// Strokes a polyline as one shape (joints do not double-blend). Uses a per-call distance buffer so
        /// long polylines (rivers, roads, coastlines) stay fast.
        /// </summary>
        public void StrokePolyline(IReadOnlyList<Vector2> points, float thickness, Color color, bool closed = false)
        {
            StrokePolyline(points, thickness, _ => color, closed);
        }

        public void StrokePolyline(IReadOnlyList<Vector2> points, float thickness, Func<Vector2, Color> shader, bool closed = false)
        {
            if (points == null || points.Count < 2) return;
            float r = thickness * 0.5f;
            Rect b = Sdf.Bounds(points, r + 1.5f);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(b.xMin)), y0 = Mathf.Max(0, Mathf.FloorToInt(b.yMin));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(b.xMax)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(b.yMax));
            if (x1 < x0 || y1 < y0) return;
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            float[] dist = new float[bw * bh];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;

            int segs = closed ? points.Count : points.Count - 1;
            for (int s = 0; s < segs; s++)
            {
                Vector2 a = points[s], c = points[(s + 1) % points.Count];
                int sx0 = Mathf.Max(x0, Mathf.FloorToInt(Mathf.Min(a.x, c.x) - r - 1.5f));
                int sy0 = Mathf.Max(y0, Mathf.FloorToInt(Mathf.Min(a.y, c.y) - r - 1.5f));
                int sx1 = Mathf.Min(x1, Mathf.CeilToInt(Mathf.Max(a.x, c.x) + r + 1.5f));
                int sy1 = Mathf.Min(y1, Mathf.CeilToInt(Mathf.Max(a.y, c.y) + r + 1.5f));
                for (int y = sy0; y <= sy1; y++)
                {
                    for (int x = sx0; x <= sx1; x++)
                    {
                        float d = Sdf.Segment(new Vector2(x + 0.5f, y + 0.5f), a, c, r);
                        int k = (y - y0) * bw + (x - x0);
                        if (d < dist[k]) dist[k] = d;
                    }
                }
            }

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float cov = Mathf.Clamp01(0.5f - dist[(y - y0) * bw + (x - x0)]);
                    if (cov > 0f) BlendPixel(x, y, shader(new Vector2(x + 0.5f, y + 0.5f)), cov);
                }
            }
        }

        /// <summary>Multiplies RGB of every non-transparent pixel by 1 + (fbm - 0.5) * strength.</summary>
        public void AddNoise(float scale, float strength, int seed, int octaves = 3)
        {
            float ox = (seed * 37.17f) % 1000f, oy = (seed * 91.31f) % 1000f;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Color c = _px[i];
                    if (c.a <= 0f) continue;
                    float n = Fbm((x + ox) / scale, (y + oy) / scale, octaves);
                    float m = 1f + (n - 0.5f) * strength;
                    _px[i] = new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
                }
            }
        }

        /// <summary>Fractal Perlin noise in [0,1] (approximately).</summary>
        public static float Fbm(float x, float y, int octaves = 3)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int o = 0; o < Mathf.Max(1, octaves); o++)
            {
                sum += Mathf.PerlinNoise(x * f + o * 17.3f, y * f + o * 29.1f) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Adds an anti-aliased outline of <paramref name="width"/> px behind existing content (alpha dilation).</summary>
        public void AddOutline(Color color, float width)
        {
            if (width <= 0f) return;
            int r = Mathf.CeilToInt(width + 1f);
            float[] dil = new float[_px.Length];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float best = 0f;
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int yy = y + dy;
                        if ((uint)yy >= (uint)Height) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx;
                            if ((uint)xx >= (uint)Width) continue;
                            float a = _px[yy * Width + xx].a;
                            if (a <= best) continue;
                            float cov = Mathf.Clamp01(width + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                            float v = a * cov;
                            if (v > best) best = v;
                        }
                    }
                    dil[y * Width + x] = best;
                }
            }

            for (int i = 0; i < _px.Length; i++)
            {
                Color top = _px[i];
                Color under = new Color(color.r, color.g, color.b, color.a * dil[i]);
                // composite top over the outline layer
                float a = top.a;
                float outA = a + under.a * (1f - a);
                if (outA <= 1e-6f) { _px[i] = new Color(0, 0, 0, 0); continue; }
                float k = under.a * (1f - a);
                _px[i] = new Color((top.r * a + under.r * k) / outA, (top.g * a + under.g * k) / outA,
                    (top.b * a + under.b * k) / outA, outA);
            }
        }

        /// <summary>Separable box blur on premultiplied color (soft glows, shadows, terrain blending).</summary>
        public void BoxBlur(int radius, int passes = 1)
        {
            if (radius <= 0) return;
            Color[] tmp = new Color[_px.Length];
            for (int i = 0; i < _px.Length; i++)
            {
                Color c = _px[i];
                _px[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
            for (int p = 0; p < passes; p++)
            {
                BlurLine(_px, tmp, radius, true);
                BlurLine(tmp, _px, radius, false);
            }
            for (int i = 0; i < _px.Length; i++)
            {
                Color c = _px[i];
                _px[i] = c.a > 1e-6f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : new Color(0, 0, 0, 0);
            }
        }

        private void BlurLine(Color[] src, Color[] dst, int radius, bool horizontal)
        {
            int len = horizontal ? Width : Height;
            int lines = horizontal ? Height : Width;
            float inv = 1f / (radius * 2 + 1);
            for (int l = 0; l < lines; l++)
            {
                Color acc = new Color(0, 0, 0, 0);
                for (int k = -radius; k <= radius; k++) acc += src[Index(Mathf.Clamp(k, 0, len - 1), l, horizontal)];
                for (int t = 0; t < len; t++)
                {
                    dst[Index(t, l, horizontal)] = acc * inv;
                    int outIdx = Mathf.Clamp(t - radius, 0, len - 1);
                    int inIdx = Mathf.Clamp(t + radius + 1, 0, len - 1);
                    acc += src[Index(inIdx, l, horizontal)] - src[Index(outIdx, l, horizontal)];
                }
            }
        }

        private int Index(int t, int line, bool horizontal) => horizontal ? line * Width + t : t * Width + line;

        /// <summary>Composites <paramref name="src"/> over this canvas with its bottom-left at (ox, oy).</summary>
        public void DrawCanvas(PixelCanvas src, int ox, int oy, float opacity = 1f)
        {
            for (int y = 0; y < src.Height; y++)
            {
                int ty = y + oy;
                if ((uint)ty >= (uint)Height) continue;
                for (int x = 0; x < src.Width; x++)
                {
                    int tx = x + ox;
                    if ((uint)tx >= (uint)Width) continue;
                    BlendPixel(tx, ty, src[x, y], opacity);
                }
            }
        }

        /// <summary>Returns a copy with every pixel's RGB replaced by <paramref name="color"/> (alpha kept, scaled by color.a).</summary>
        public PixelCanvas Silhouette(Color color)
        {
            PixelCanvas c = new PixelCanvas(Width, Height);
            for (int i = 0; i < _px.Length; i++) c._px[i] = new Color(color.r, color.g, color.b, _px[i].a * color.a);
            return c;
        }

        public PixelCanvas Clone()
        {
            PixelCanvas c = new PixelCanvas(Width, Height);
            Array.Copy(_px, c._px, _px.Length);
            return c;
        }

        public Texture2D ToTexture2D()
        {
            Texture2D t = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            t.SetPixels(_px);
            t.Apply(false, false);
            return t;
        }

        public byte[] EncodeToPng()
        {
            Texture2D t = ToTexture2D();
            try { return t.EncodeToPNG(); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        /// <summary>Square pixel-space bounds around a center (helper for shape bounds).</summary>
        public static Rect Square(Vector2 center, float halfExtent)
        {
            float h = halfExtent + 1.5f;
            return new Rect(center.x - h, center.y - h, h * 2f, h * 2f);
        }

        private void ForEachPixel(Rect bounds, Action<int, int, Vector2> body)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(bounds.xMin - 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(bounds.yMin - 1f));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(bounds.xMax + 1f));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(bounds.yMax + 1f));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    body(x, y, new Vector2(x + 0.5f, y + 0.5f));
        }

        private static Vector2[] Copy(IReadOnlyList<Vector2> pts)
        {
            Vector2[] a = new Vector2[pts.Count];
            for (int i = 0; i < a.Length; i++) a[i] = pts[i];
            return a;
        }
    }
}
