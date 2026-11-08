using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>Small assertion helpers for Unity vector / rect values in EditMode tests.</summary>
    internal static class Vec2Assert
    {
        /// <summary>Default tolerance used for world-unit comparisons.</summary>
        public const float DefaultTolerance = 1e-4f;

        /// <summary>Fails unless <paramref name="actual"/> is within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
        public static void Near(Vector2 expected, Vector2 actual, float tolerance = DefaultTolerance, string message = null)
        {
            if ((expected - actual).magnitude > tolerance)
            {
                Assert.Fail((message == null ? string.Empty : message + " ") +
                            "Expected " + Format(expected) + " but was " + Format(actual) + " (tolerance " + tolerance + ").");
            }
        }

        /// <summary>Fails unless <paramref name="r"/> contains <paramref name="p"/> (edges inclusive, with tolerance).</summary>
        public static void Inside(Rect r, Vector2 p, float tolerance = DefaultTolerance, string message = null)
        {
            bool inside = p.x >= r.xMin - tolerance && p.x <= r.xMax + tolerance &&
                          p.y >= r.yMin - tolerance && p.y <= r.yMax + tolerance;
            if (!inside)
            {
                Assert.Fail((message == null ? string.Empty : message + " ") +
                            "Point " + Format(p) + " is outside rect " + r + ".");
            }
        }

        /// <summary>Formats a vector with enough precision for failure messages.</summary>
        public static string Format(Vector2 v) => "(" + v.x.ToString("F5") + ", " + v.y.ToString("F5") + ")";
    }
}
