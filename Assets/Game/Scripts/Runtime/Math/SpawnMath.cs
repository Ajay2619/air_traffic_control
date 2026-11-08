using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>Pure difficulty-ramp and spawn-placement math (deterministic given a System.Random).</summary>
    public static class SpawnMath
    {
        /// <summary>Seconds between spawns after <paramref name="elapsed"/> seconds of play (linear ramp).</summary>
        public static float SpawnInterval(float elapsed, float initial, float min, float rampDuration)
        {
            float t = rampDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / rampDuration);
            return Mathf.Lerp(initial, min, t);
        }

        /// <summary>Simultaneous-aircraft limit: grows by one every <paramref name="stepSeconds"/>, up to <paramref name="cap"/>.</summary>
        public static int MaxAircraft(float elapsed, int initial, int cap, float stepSeconds)
        {
            int extra = stepSeconds <= 0f ? 0 : Mathf.FloorToInt(Mathf.Max(0f, elapsed) / stepSeconds);
            return Mathf.Max(1, Mathf.Min(cap, initial + extra));
        }

        /// <summary>
        /// Weighted pick. <paramref name="r01"/> in [0,1). Non-positive weights are never picked.
        /// Returns -1 when no weight is positive.
        /// </summary>
        public static int PickWeighted(IReadOnlyList<float> weights, double r01)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0f) total += weights[i];
            if (total <= 0) return -1;

            double target = Mathf.Clamp01((float)r01) * total;
            double acc = 0;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                lastPositive = i;
                acc += weights[i];
                if (target < acc) return i;
            }
            return lastPositive;
        }

        /// <summary>
        /// Point on the rect's perimeter at fraction <paramref name="t01"/> (walking bottom -> right -> top -> left,
        /// counter-clockwise from the bottom-left corner) and the outward normal of that edge.
        /// </summary>
        public static Vector2 PointOnPerimeter(Rect r, float t01, out Vector2 outwardNormal)
        {
            float w = r.width, h = r.height;
            float s = Mathf.Repeat(t01, 1f) * 2f * (w + h);
            if (s < w) { outwardNormal = Vector2.down; return new Vector2(r.xMin + s, r.yMin); }
            s -= w;
            if (s < h) { outwardNormal = Vector2.right; return new Vector2(r.xMax, r.yMin + s); }
            s -= h;
            if (s < w) { outwardNormal = Vector2.up; return new Vector2(r.xMax - s, r.yMax); }
            s -= w;
            outwardNormal = Vector2.left;
            return new Vector2(r.xMin, r.yMax - s);
        }

        /// <summary>
        /// Picks a random point on the edge of <paramref name="view"/> that is at least
        /// <paramref name="cornerAvoidDistance"/> from every corner (the HUD lives there) and at least
        /// <paramref name="avoidDistance"/> from every point in <paramref name="avoidPoints"/>.
        /// </summary>
        public static bool TryPickEdgePoint(Rect view, System.Random rng, IReadOnlyList<Vector2> avoidPoints,
            float avoidDistance, float cornerAvoidDistance, int attempts, out Vector2 edgePoint, out Vector2 outwardNormal)
        {
            float avoid2 = avoidDistance * avoidDistance;
            float corner2 = cornerAvoidDistance * cornerAvoidDistance;
            for (int a = 0; a < attempts; a++)
            {
                Vector2 p = PointOnPerimeter(view, (float)rng.NextDouble(), out Vector2 n);
                if ((p - new Vector2(view.xMin, view.yMin)).sqrMagnitude < corner2) continue;
                if ((p - new Vector2(view.xMax, view.yMin)).sqrMagnitude < corner2) continue;
                if ((p - new Vector2(view.xMin, view.yMax)).sqrMagnitude < corner2) continue;
                if ((p - new Vector2(view.xMax, view.yMax)).sqrMagnitude < corner2) continue;

                bool blocked = false;
                if (avoidPoints != null)
                {
                    for (int i = 0; i < avoidPoints.Count; i++)
                    {
                        if ((avoidPoints[i] - p).sqrMagnitude < avoid2) { blocked = true; break; }
                    }
                }
                if (blocked) continue;

                edgePoint = p;
                outwardNormal = n;
                return true;
            }
            edgePoint = default;
            outwardNormal = default;
            return false;
        }

        /// <summary>Uniform random point inside a rect.</summary>
        public static Vector2 RandomPointInRect(Rect r, System.Random rng)
        {
            return new Vector2(r.xMin + (float)rng.NextDouble() * r.width, r.yMin + (float)rng.NextDouble() * r.height);
        }
    }
}
