using UnityEngine;

namespace ClearToLand
{
    public enum ProximityLevel
    {
        Clear = 0,
        Warning = 1,
        Collision = 2,
    }

    /// <summary>Pure separation checks between two circular aircraft.</summary>
    public static class ProximityMath
    {
        /// <summary>
        /// Collision when the circles overlap (distance &lt;= ra + rb); Warning when within
        /// ra + rb + <paramref name="warnMargin"/>; otherwise Clear.
        /// </summary>
        public static ProximityLevel Evaluate(Vector2 a, float ra, Vector2 b, float rb, float warnMargin)
        {
            float d2 = (a - b).sqrMagnitude;
            float hit = ra + rb;
            if (d2 <= hit * hit) return ProximityLevel.Collision;
            float warn = hit + Mathf.Max(0f, warnMargin);
            return d2 <= warn * warn ? ProximityLevel.Warning : ProximityLevel.Clear;
        }
    }
}
