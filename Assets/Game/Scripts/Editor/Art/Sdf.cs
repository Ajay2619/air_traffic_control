using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// 2D signed distance functions in pixel space: negative inside, positive outside, magnitude = distance in px.
    /// Combine with <see cref="Union"/>, <see cref="Subtract"/>, <see cref="Intersect"/> and fill with
    /// <see cref="PixelCanvas.FillSdf(System.Func{Vector2,float},Rect,Color)"/> for anti-aliased shapes.
    /// </summary>
    public static class Sdf
    {
        public static Vector2 Rotate(Vector2 v, float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static float Circle(Vector2 p, Vector2 center, float radius) => (p - center).magnitude - radius;

        /// <summary>Ring (annulus) centered on a circle of <paramref name="radius"/> with total <paramref name="thickness"/>.</summary>
        public static float Ring(Vector2 p, Vector2 center, float radius, float thickness)
            => Mathf.Abs((p - center).magnitude - radius) - thickness * 0.5f;

        /// <summary>(Rounded, rotated) box. <paramref name="halfSize"/> is half the full size.</summary>
        public static float Box(Vector2 p, Vector2 center, Vector2 halfSize, float cornerRadius = 0f, float angleDeg = 0f)
        {
            Vector2 q = angleDeg != 0f ? Rotate(p - center, -angleDeg) : p - center;
            cornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfSize.x, halfSize.y));
            float dx = Mathf.Abs(q.x) - (halfSize.x - cornerRadius);
            float dy = Mathf.Abs(q.y) - (halfSize.y - cornerRadius);
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - cornerRadius;
        }

        /// <summary>Capsule around segment a-b (radius = half the stroke thickness).</summary>
        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(ba.sqrMagnitude, 1e-6f));
            return (pa - ba * h).magnitude - radius;
        }

        /// <summary>Approximate ellipse distance (good enough for anti-aliasing and small outlines).</summary>
        public static float Ellipse(Vector2 p, Vector2 center, Vector2 radii, float angleDeg = 0f)
        {
            Vector2 q = angleDeg != 0f ? Rotate(p - center, -angleDeg) : p - center;
            radii = new Vector2(Mathf.Max(radii.x, 1e-3f), Mathf.Max(radii.y, 1e-3f));
            float k0 = new Vector2(q.x / radii.x, q.y / radii.y).magnitude;
            float k1 = new Vector2(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y)).magnitude;
            if (k1 < 1e-6f) return -Mathf.Min(radii.x, radii.y);
            return k0 * (k0 - 1f) / k1;
        }

        /// <summary>Exact distance to a simple polygon (any winding).</summary>
        public static float Polygon(Vector2 p, IReadOnlyList<Vector2> v)
        {
            int n = v.Count;
            if (n < 3) return float.MaxValue;
            float d = (p - v[0]).sqrMagnitude;
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(e.sqrMagnitude, 1e-9f));
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float Union(float a, float b) => Mathf.Min(a, b);
        public static float Subtract(float a, float b) => Mathf.Max(a, -b);
        public static float Intersect(float a, float b) => Mathf.Max(a, b);

        /// <summary>Smooth union with blend radius <paramref name="k"/> (px).</summary>
        public static float SmoothUnion(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>Axis-aligned bounds of a set of points, grown by <paramref name="pad"/>.</summary>
        public static Rect Bounds(IReadOnlyList<Vector2> pts, float pad = 0f)
        {
            if (pts == null || pts.Count == 0) return Rect.zero;
            Vector2 min = pts[0], max = pts[0];
            for (int i = 1; i < pts.Count; i++) { min = Vector2.Min(min, pts[i]); max = Vector2.Max(max, pts[i]); }
            return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }
    }
}
