using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>
    /// Footprint geometry of a <see cref="LandingSpotDef"/> for layout validation: directional spots are oriented
    /// rectangles from the threshold to the far end (width across), helipads are circles of diameter width.
    /// </summary>
    internal static class SpotGeometry
    {
        /// <summary>The four corners of a directional spot's rectangle (helipads: corners of the pad's bounding square).</summary>
        public static Vector2[] Corners(LandingSpotDef s)
        {
            if (!LandingColors.IsDirectional(s.kind))
            {
                float r = s.width * 0.5f;
                Vector2 c = s.position;
                return new[] { c + new Vector2(-r, -r), c + new Vector2(r, -r), c + new Vector2(r, r), c + new Vector2(-r, r) };
            }

            Vector2 d = s.Direction;
            Vector2 n = new Vector2(-d.y, d.x) * (s.width * 0.5f);
            Vector2 a = s.position, b = s.FarEnd;
            return new[] { a + n, a - n, b - n, b + n };
        }

        /// <summary>
        /// True when the two footprints overlap by more than <paramref name="tolerance"/> world units
        /// (touching or nearly touching is allowed).
        /// </summary>
        public static bool Overlap(LandingSpotDef a, LandingSpotDef b, float tolerance)
        {
            bool aBox = LandingColors.IsDirectional(a.kind);
            bool bBox = LandingColors.IsDirectional(b.kind);
            if (aBox && bBox) return BoxBox(a, b, tolerance);
            if (aBox) return BoxCircle(a, b.position, b.width * 0.5f, tolerance);
            if (bBox) return BoxCircle(b, a.position, a.width * 0.5f, tolerance);
            float sum = a.width * 0.5f + b.width * 0.5f;
            return Vector2.Distance(a.position, b.position) < sum - tolerance;
        }

        private static void Axes(LandingSpotDef s, out Vector2 center, out Vector2 u, out Vector2 v, out float hu, out float hv)
        {
            center = s.Center;
            u = s.Direction.normalized;
            v = new Vector2(-u.y, u.x);
            hu = s.length * 0.5f;
            hv = s.width * 0.5f;
        }

        private static bool BoxBox(LandingSpotDef a, LandingSpotDef b, float tolerance)
        {
            Axes(a, out Vector2 ca, out Vector2 ua, out Vector2 va, out float hua, out float hva);
            Axes(b, out Vector2 cb, out Vector2 ub, out Vector2 vb, out float hub, out float hvb);
            Vector2[] axes = { ua, va, ub, vb };
            Vector2 delta = cb - ca;
            foreach (Vector2 l in axes)
            {
                float ra = hua * Mathf.Abs(Vector2.Dot(ua, l)) + hva * Mathf.Abs(Vector2.Dot(va, l));
                float rb = hub * Mathf.Abs(Vector2.Dot(ub, l)) + hvb * Mathf.Abs(Vector2.Dot(vb, l));
                if (Mathf.Abs(Vector2.Dot(delta, l)) >= ra + rb - tolerance) return false; // separating axis
            }
            return true;
        }

        private static bool BoxCircle(LandingSpotDef box, Vector2 circleCenter, float radius, float tolerance)
        {
            Axes(box, out Vector2 c, out Vector2 u, out Vector2 v, out float hu, out float hv);
            Vector2 rel = circleCenter - c;
            Vector2 local = new Vector2(Vector2.Dot(rel, u), Vector2.Dot(rel, v));
            Vector2 clamped = new Vector2(Mathf.Clamp(local.x, -hu, hu), Mathf.Clamp(local.y, -hv, hv));
            return Vector2.Distance(local, clamped) < radius - tolerance;
        }
    }
}
