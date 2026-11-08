using UnityEngine;

namespace ClearToLand
{
    /// <summary>Pure geometry for deciding when a drawn path "captures" a landing spot.</summary>
    public static class LandingMath
    {
        /// <summary>Capture zone starts this far (world units) before the runway threshold.</summary>
        public const float RunwayCaptureBehind = 0.45f;
        /// <summary>Capture zone extends this fraction of the runway length past the threshold.</summary>
        public const float RunwayCaptureAheadFraction = 0.35f;
        /// <summary>Added to the runway half-width laterally.</summary>
        public const float RunwayCaptureSidePadding = 0.3f;
        /// <summary>Added to the helipad radius.</summary>
        public const float HelipadCapturePadding = 0.15f;

        public static bool Accepts(LandingKind spotKind, LandingKind aircraftKind) => spotKind == aircraftKind;

        /// <summary>
        /// True when <paramref name="point"/> lies in the runway's capture zone (a box from slightly before the
        /// threshold to 35% down the runway) and <paramref name="heading"/> is within
        /// <paramref name="maxAngleDeg"/> of the landing direction.
        /// </summary>
        public static bool RunwayCaptures(Vector2 threshold, Vector2 direction, float length, float width,
            Vector2 point, Vector2 heading, float maxAngleDeg)
        {
            if (heading.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f) return false;
            Vector2 dir = direction.normalized;
            Vector2 rel = point - threshold;
            float along = Vector2.Dot(rel, dir);
            if (along < -RunwayCaptureBehind || along > length * RunwayCaptureAheadFraction) return false;
            float lateral = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
            if (lateral > width * 0.5f + RunwayCaptureSidePadding) return false;
            return Vector2.Dot(heading.normalized, dir) >= Mathf.Cos(maxAngleDeg * Mathf.Deg2Rad) - 1e-5f;
        }

        /// <summary>True when <paramref name="point"/> is on (or just around) a helipad of the given diameter.</summary>
        public static bool HelipadCaptures(Vector2 center, float diameter, Vector2 point)
        {
            float r = diameter * 0.5f + HelipadCapturePadding;
            return (point - center).sqrMagnitude <= r * r;
        }

        /// <summary>Dispatches to the runway or helipad test based on the spot's kind.</summary>
        public static bool Captures(LandingSpotDef spot, Vector2 point, Vector2 heading, float maxAngleDeg)
        {
            if (spot == null) return false;
            return LandingColors.IsDirectional(spot.kind)
                ? RunwayCaptures(spot.position, spot.Direction, spot.length, spot.width, point, heading, maxAngleDeg)
                : HelipadCaptures(spot.position, spot.width, point);
        }
    }
}
