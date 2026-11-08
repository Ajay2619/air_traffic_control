using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Immutable signed-distance shape (negative inside, distance in pixels) with conservative bounds, built from
    /// <see cref="Sdf"/> primitives and combined with boolean operators. Painters work in pixels relative to the
    /// canvas center and fill shapes through <see cref="ArtBrush"/>.
    /// </summary>
    internal sealed class ArtShape
    {
        private const float Pad = 1.5f;
        private static readonly Rect Everywhere = Rect.MinMaxRect(-100000f, -100000f, 100000f, 100000f);

        /// <summary>Signed distance function (negative inside).</summary>
        public readonly Func<Vector2, float> Distance;

        /// <summary>Bounds that contain every point where <see cref="Distance"/> is below ~1 px.</summary>
        public readonly Rect Bounds;

        /// <summary>Wraps a distance function and its bounds.</summary>
        public ArtShape(Func<Vector2, float> distance, Rect bounds)
        {
            Distance = distance ?? throw new ArgumentNullException(nameof(distance));
            Bounds = bounds;
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Filled circle.</summary>
        public static ArtShape Circle(Vector2 center, float radius)
            => new ArtShape(p => Sdf.Circle(p, center, radius), Around(center, radius));

        /// <summary>Annulus of total <paramref name="thickness"/> centered on <paramref name="radius"/>.</summary>
        public static ArtShape Ring(Vector2 center, float radius, float thickness)
            => new ArtShape(p => Sdf.Ring(p, center, radius, thickness), Around(center, radius + thickness * 0.5f));

        /// <summary>(Rotated) ellipse.</summary>
        public static ArtShape Ellipse(Vector2 center, Vector2 radii, float angleDeg = 0f)
            => new ArtShape(p => Sdf.Ellipse(p, center, radii, angleDeg), Around(center, Mathf.Max(radii.x, radii.y)));

        /// <summary>(Rounded, rotated) box; <paramref name="halfSize"/> is half the full size.</summary>
        public static ArtShape Box(Vector2 center, Vector2 halfSize, float cornerRadius = 0f, float angleDeg = 0f)
        {
            Rect bounds = angleDeg == 0f
                ? Rect.MinMaxRect(center.x - halfSize.x - Pad, center.y - halfSize.y - Pad,
                    center.x + halfSize.x + Pad, center.y + halfSize.y + Pad)
                : Around(center, halfSize.magnitude);
            return new ArtShape(p => Sdf.Box(p, center, halfSize, cornerRadius, angleDeg), bounds);
        }

        /// <summary>Tapered capsule from <paramref name="a"/> (radius <paramref name="radiusA"/>) to <paramref name="b"/> (radius <paramref name="radiusB"/>).</summary>
        public static ArtShape Capsule(Vector2 a, Vector2 b, float radiusA, float radiusB)
        {
            Rect bounds = Rect.MinMaxRect(
                Mathf.Min(a.x - radiusA, b.x - radiusB) - Pad, Mathf.Min(a.y - radiusA, b.y - radiusB) - Pad,
                Mathf.Max(a.x + radiusA, b.x + radiusB) + Pad, Mathf.Max(a.y + radiusA, b.y + radiusB) + Pad);
            return new ArtShape(p => UnevenCapsule(p, a, b, radiusA, radiusB), bounds);
        }

        /// <summary>Round-capped line of full <paramref name="thickness"/>.</summary>
        public static ArtShape Segment(Vector2 a, Vector2 b, float thickness)
            => Capsule(a, b, thickness * 0.5f, thickness * 0.5f);

        /// <summary>Simple polygon (any winding).</summary>
        public static ArtShape Polygon(params Vector2[] points)
        {
            if (points == null || points.Length < 3) throw new ArgumentException("A polygon needs at least 3 points.", nameof(points));
            Vector2[] pts = (Vector2[])points.Clone();
            return new ArtShape(p => Sdf.Polygon(p, pts), Sdf.Bounds(pts, Pad));
        }

        /// <summary>
        /// Polygon symmetric about y = 0. <paramref name="half"/> lists the outline on the +Y side in order, starting and
        /// ending on (or near) the axis; the mirrored -Y side is appended automatically.
        /// </summary>
        public static ArtShape MirroredPolygon(params Vector2[] half)
        {
            var pts = new List<Vector2>(half.Length * 2);
            pts.AddRange(half);
            for (int i = half.Length - 1; i >= 0; i--)
            {
                if (Mathf.Abs(half[i].y) > 1e-4f) pts.Add(new Vector2(half[i].x, -half[i].y));
            }
            return Polygon(pts.ToArray());
        }

        /// <summary>Triangle with corners rounded by <paramref name="radius"/> while keeping its outer extents.</summary>
        public static ArtShape RoundedTriangle(Vector2 a, Vector2 b, Vector2 c, float radius)
        {
            float la = (b - c).magnitude, lb = (c - a).magnitude, lc = (a - b).magnitude;
            float perimeter = Mathf.Max(la + lb + lc, 1e-4f);
            Vector2 incenter = (a * la + b * lb + c * lc) / perimeter;
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            float inradius = Mathf.Max(2f * area / perimeter, 1e-4f);
            float r = Mathf.Clamp(radius, 0f, inradius * 0.9f);
            float k = (inradius - r) / inradius; // inward offset of a triangle = homothety about the incenter
            return Polygon(incenter + (a - incenter) * k, incenter + (b - incenter) * k, incenter + (c - incenter) * k).Inflate(r);
        }

        /// <summary>Star polygon with <paramref name="points"/> tips; the first tip points at <paramref name="rotationDeg"/>.</summary>
        public static ArtShape Star(Vector2 center, int points, float outerRadius, float innerRadius, float rotationDeg)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                float ang = (rotationDeg + i * 180f / points) * Mathf.Deg2Rad;
                pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }
            return Polygon(pts);
        }

        /// <summary>Open polyline with round joins and caps.</summary>
        public static ArtShape Polyline(float thickness, params Vector2[] points)
        {
            if (points == null || points.Length < 2) throw new ArgumentException("A polyline needs at least 2 points.", nameof(points));
            Vector2[] pts = (Vector2[])points.Clone();
            float r = thickness * 0.5f;
            return new ArtShape(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i + 1 < pts.Length; i++) d = Mathf.Min(d, Sdf.Segment(p, pts[i], pts[i + 1], r));
                return d;
            }, Sdf.Bounds(pts, r + Pad));
        }

        /// <summary>
        /// Round-capped circular arc centered on direction <paramref name="directionDeg"/>, spanning
        /// ±<paramref name="halfApertureDeg"/> (0..180) around it.
        /// </summary>
        public static ArtShape Arc(Vector2 center, float radius, float thickness, float directionDeg, float halfApertureDeg)
        {
            float half = thickness * 0.5f;
            float ap = Mathf.Clamp(halfApertureDeg, 0f, 180f) * Mathf.Deg2Rad;
            Vector2 sc = new Vector2(Mathf.Sin(ap), Mathf.Cos(ap));
            float rot = 90f - directionDeg; // map the arc direction onto +Y
            return new ArtShape(p =>
            {
                Vector2 q = Sdf.Rotate(p - center, rot);
                q.x = Mathf.Abs(q.x);
                float d = sc.y * q.x > sc.x * q.y ? (q - sc * radius).magnitude : Mathf.Abs(q.magnitude - radius);
                return d - half;
            }, Around(center, radius + half));
        }

        /// <summary>Infinite half-plane through <paramref name="point"/>; inside is the side <paramref name="inward"/> points to.</summary>
        public static ArtShape HalfPlane(Vector2 point, Vector2 inward)
        {
            Vector2 n = inward.normalized;
            return new ArtShape(p => -Vector2.Dot(p - point, n), Everywhere);
        }

        /// <summary>Union of any number of shapes (single flat loop, cheaper than chained unions).</summary>
        public static ArtShape UnionAll(params ArtShape[] shapes)
        {
            if (shapes == null || shapes.Length == 0) throw new ArgumentException("No shapes to union.", nameof(shapes));
            var fns = new Func<Vector2, float>[shapes.Length];
            Rect bounds = shapes[0].Bounds;
            for (int i = 0; i < shapes.Length; i++)
            {
                fns[i] = shapes[i].Distance;
                bounds = Encapsulate(bounds, shapes[i].Bounds);
            }
            return new ArtShape(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < fns.Length; i++)
                {
                    float v = fns[i](p);
                    if (v < d) d = v;
                }
                return d;
            }, bounds);
        }

        // ------------------------------------------------------------------ operators

        /// <summary>Boolean union.</summary>
        public ArtShape Union(ArtShape other)
        {
            Func<Vector2, float> a = Distance, b = other.Distance;
            return new ArtShape(p => Mathf.Min(a(p), b(p)), Encapsulate(Bounds, other.Bounds));
        }

        /// <summary>Union blended with a fillet of radius <paramref name="k"/> px.</summary>
        public ArtShape SmoothUnion(ArtShape other, float k)
        {
            Func<Vector2, float> a = Distance, b = other.Distance;
            return new ArtShape(p => Sdf.SmoothUnion(a(p), b(p), k), Grow(Encapsulate(Bounds, other.Bounds), k));
        }

        /// <summary>This shape minus <paramref name="other"/>.</summary>
        public ArtShape Subtract(ArtShape other)
        {
            Func<Vector2, float> a = Distance, b = other.Distance;
            return new ArtShape(p => Sdf.Subtract(a(p), b(p)), Bounds);
        }

        /// <summary>Boolean intersection.</summary>
        public ArtShape Intersect(ArtShape other)
        {
            Func<Vector2, float> a = Distance, b = other.Distance;
            return new ArtShape(p => Sdf.Intersect(a(p), b(p)), Overlap(Bounds, other.Bounds));
        }

        /// <summary>Grows (positive) or shrinks (negative) the shape by <paramref name="amount"/> px; growing rounds corners.</summary>
        public ArtShape Inflate(float amount)
        {
            Func<Vector2, float> a = Distance;
            return new ArtShape(p => a(p) - amount, Grow(Bounds, Mathf.Max(0f, amount)));
        }

        /// <summary>Band of total <paramref name="thickness"/> centered on the outline.</summary>
        public ArtShape Shell(float thickness)
        {
            Func<Vector2, float> a = Distance;
            float h = thickness * 0.5f;
            return new ArtShape(p => Mathf.Abs(a(p)) - h, Grow(Bounds, h));
        }

        /// <summary>Moves the shape by <paramref name="offset"/>.</summary>
        public ArtShape Translate(Vector2 offset)
        {
            Func<Vector2, float> a = Distance;
            return new ArtShape(p => a(p - offset), new Rect(Bounds.position + offset, Bounds.size));
        }

        /// <summary>Rotates the shape counter-clockwise by <paramref name="angleDeg"/> about <paramref name="pivot"/>.</summary>
        public ArtShape Rotate(float angleDeg, Vector2 pivot)
        {
            Func<Vector2, float> a = Distance;
            Rect b = Bounds;
            Vector2 c0 = pivot + Sdf.Rotate(new Vector2(b.xMin, b.yMin) - pivot, angleDeg);
            Vector2 c1 = pivot + Sdf.Rotate(new Vector2(b.xMax, b.yMin) - pivot, angleDeg);
            Vector2 c2 = pivot + Sdf.Rotate(new Vector2(b.xMax, b.yMax) - pivot, angleDeg);
            Vector2 c3 = pivot + Sdf.Rotate(new Vector2(b.xMin, b.yMax) - pivot, angleDeg);
            Rect rb = Rect.MinMaxRect(
                Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x)), Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y)),
                Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x)), Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y)));
            return new ArtShape(p => a(pivot + Sdf.Rotate(p - pivot, -angleDeg)), rb);
        }

        /// <summary>Union of this shape and its mirror image across y = 0.</summary>
        public ArtShape MirrorY()
        {
            Func<Vector2, float> a = Distance;
            Rect b = Bounds;
            Rect mirrored = Rect.MinMaxRect(b.xMin, -b.yMax, b.xMax, -b.yMin);
            return new ArtShape(p => Mathf.Min(a(p), a(new Vector2(p.x, -p.y))), Encapsulate(b, mirrored));
        }

        // ------------------------------------------------------------------ math

        /// <summary>Exact distance to a capsule whose radius changes linearly from <paramref name="ra"/> at a to <paramref name="rb"/> at b.</summary>
        public static float UnevenCapsule(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 ab = b - a;
            float h = ab.magnitude;
            if (h < 1e-4f) return (p - a).magnitude - Mathf.Max(ra, rb);
            float slope = (ra - rb) / h;
            if (Mathf.Abs(slope) >= 1f) return Mathf.Min((p - a).magnitude - ra, (p - b).magnitude - rb);
            Vector2 axisY = ab / h;
            Vector2 axisX = new Vector2(axisY.y, -axisY.x);
            Vector2 q = p - a;
            float qx = Mathf.Abs(Vector2.Dot(q, axisX));
            float qy = Vector2.Dot(q, axisY);
            float c = Mathf.Sqrt(1f - slope * slope);
            float k = -slope * qx + c * qy;
            if (k < 0f) return Mathf.Sqrt(qx * qx + qy * qy) - ra;
            if (k > c * h) return Mathf.Sqrt(qx * qx + (qy - h) * (qy - h)) - rb;
            return qx * c + qy * slope - ra;
        }

        private static Rect Around(Vector2 c, float r)
            => Rect.MinMaxRect(c.x - r - Pad, c.y - r - Pad, c.x + r + Pad, c.y + r + Pad);

        private static Rect Grow(Rect r, float amount)
            => Rect.MinMaxRect(r.xMin - amount, r.yMin - amount, r.xMax + amount, r.yMax + amount);

        private static Rect Encapsulate(Rect a, Rect b)
            => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static Rect Overlap(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin);
            float x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            if (x1 < x0) x1 = x0;
            if (y1 < y0) y1 = y0;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
