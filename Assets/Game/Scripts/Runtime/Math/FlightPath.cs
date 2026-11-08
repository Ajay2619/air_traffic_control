using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// The route an aircraft follows: an evenly sampled polyline that is consumed from the front as the
    /// aircraft flies along it. Pure C# (no MonoBehaviour) so it can be unit tested.
    /// </summary>
    public sealed class FlightPath
    {
        /// <summary>Hard cap so a player drawing in circles forever cannot grow the list unbounded.</summary>
        public const int MaxPoints = 1500;

        /// <summary>When locking onto a runway, trailing points closer than this to the threshold line (or past it) are trimmed.</summary>
        private const float ThresholdTrimBand = 0.05f;

        private readonly List<Vector2> _points = new List<Vector2>(256);

        /// <summary>Remaining waypoints, front = next target.</summary>
        public IReadOnlyList<Vector2> Points => _points;
        public int Count => _points.Count;
        public bool IsEmpty => _points.Count == 0;

        /// <summary>True once the path ends on a landing spot; no more points are accepted.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>True when the path ends on a landing spot that the aircraft will touch down on.</summary>
        public bool IsLanding { get; private set; }

        /// <summary>Index into MapRuntime.Spots of the landing target, or -1.</summary>
        public int LandingSpotIndex { get; private set; } = -1;

        public Vector2 LastPoint => _points[_points.Count - 1];

        public void Clear()
        {
            _points.Clear();
            IsLocked = false;
            IsLanding = false;
            LandingSpotIndex = -1;
        }

        /// <summary>Starts a fresh path at <paramref name="origin"/> (normally the aircraft's position).</summary>
        public void Begin(Vector2 origin)
        {
            Clear();
            _points.Add(origin);
        }

        /// <summary>
        /// Appends a drawn point. Points closer than <paramref name="spacing"/> to the last point are ignored;
        /// long jumps are subdivided so consecutive points are at most ~spacing*2 apart.
        /// Returns how many points were added (0 when ignored, locked, or full).
        /// </summary>
        public int AddPoint(Vector2 p, float spacing)
        {
            if (IsLocked || spacing <= 0f) return 0;
            if (_points.Count == 0)
            {
                _points.Add(p);
                return 1;
            }

            Vector2 last = _points[_points.Count - 1];
            float d = Vector2.Distance(last, p);
            if (d < spacing) return 0;

            int n = Mathf.Max(1, Mathf.FloorToInt(d / spacing));
            int added = 0;
            for (int i = 1; i <= n && _points.Count < MaxPoints; i++)
            {
                _points.Add(Vector2.Lerp(last, p, (float)i / n));
                added++;
            }
            return added;
        }

        /// <summary>
        /// Moves <paramref name="position"/> along the path by up to <paramref name="distance"/>, consuming reached
        /// waypoints. <paramref name="direction"/> receives the direction of travel (left unchanged if nothing moved).
        /// Returns the distance actually travelled (less than requested when the path ran out).
        /// </summary>
        public float Advance(ref Vector2 position, float distance, ref Vector2 direction)
        {
            float travelled = 0f;
            while (distance > 1e-6f && _points.Count > 0)
            {
                Vector2 delta = _points[0] - position;
                float len = delta.magnitude;
                if (len <= distance)
                {
                    if (len > 1e-5f) direction = delta / len;
                    position = _points[0];
                    distance -= len;
                    travelled += len;
                    _points.RemoveAt(0);
                }
                else
                {
                    direction = delta / len;
                    position += direction * distance;
                    travelled += distance;
                    distance = 0f;
                }
            }
            return travelled;
        }

        /// <summary>
        /// Average heading over the last <paramref name="lookback"/> world units of the path (normalized),
        /// or Vector2.zero when there are fewer than 2 points.
        /// </summary>
        public Vector2 EndDirection(float lookback = 0.5f)
        {
            int n = _points.Count;
            if (n < 2) return Vector2.zero;
            Vector2 end = _points[n - 1];
            Vector2 start = _points[n - 2];
            float acc = 0f;
            for (int i = n - 2; i >= 0; i--)
            {
                acc += Vector2.Distance(_points[i + 1], _points[i]);
                start = _points[i];
                if (acc >= lookback) break;
            }
            Vector2 dir = end - start;
            return dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.zero;
        }

        /// <summary>
        /// Locks the path as a landing approach. For directional spots (<paramref name="runwayDirection"/> non-zero),
        /// trailing points already past the threshold are trimmed so the aircraft does not overfly and come back.
        /// Then points are added up to <paramref name="touchdownPoint"/>, which becomes the final waypoint.
        /// </summary>
        public void LockForLanding(int spotIndex, Vector2 touchdownPoint, Vector2 runwayDirection, float spacing)
        {
            if (runwayDirection.sqrMagnitude > 1e-6f && _points.Count > 1)
            {
                int keep = LastKeptBeforeThreshold(touchdownPoint, runwayDirection.normalized);
                _points.RemoveRange(keep + 1, _points.Count - keep - 1);
            }

            IsLocked = false; // allow AddPoint for the final approach
            if (_points.Count == 0) _points.Add(touchdownPoint);
            else
            {
                Vector2 last = _points[_points.Count - 1];
                float d = Vector2.Distance(last, touchdownPoint);
                float step = Mathf.Max(spacing, 0.01f);
                int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
                for (int i = 1; i <= n; i++) _points.Add(Vector2.Lerp(last, touchdownPoint, (float)i / n));
            }

            IsLocked = true;
            IsLanding = true;
            LandingSpotIndex = spotIndex;
        }

        /// <summary>
        /// True when the straight final leg that <see cref="LockForLanding"/> would build for a directional spot (from
        /// the last point its trim keeps to <paramref name="touchdownPoint"/>) is within <paramref name="maxAngleDeg"/>
        /// of <paramref name="runwayDirection"/>, i.e. the aircraft would still come in from the approach end. A path
        /// that cuts onto the runway from the side, or whose kept points all lie past the threshold, yields a leg
        /// that is near perpendicular or points backwards. Also true for an empty path, a zero direction (helipad)
        /// or a zero-length leg.
        /// </summary>
        public bool FinalLegWithin(Vector2 touchdownPoint, Vector2 runwayDirection, float maxAngleDeg)
        {
            if (_points.Count == 0 || runwayDirection.sqrMagnitude <= 1e-6f) return true;
            Vector2 dir = runwayDirection.normalized;
            Vector2 leg = touchdownPoint - _points[LastKeptBeforeThreshold(touchdownPoint, dir)];
            return leg.sqrMagnitude < 1e-6f || Vector2.Angle(leg, dir) <= maxAngleDeg;
        }

        /// <summary>
        /// True when the route can still come straight in from <paramref name="origin"/> (the aircraft, which flies to
        /// the first point) although no drawn point is left before the threshold: every point is at or past the
        /// threshold band (so <see cref="FinalLegWithin"/> sees a backwards leg from the first one), while
        /// <paramref name="origin"/> is still short of the threshold line and the leg from it to
        /// <paramref name="touchdownPoint"/> is within <paramref name="maxAngleDeg"/> of
        /// <paramref name="runwayDirection"/>. This is a quick drag onto the runway from an aircraft lined up just
        /// short of it; the caller then clears the path before <see cref="LockForLanding"/> so the aircraft flies
        /// straight to the touchdown point. False for an empty path or a zero direction (helipad).
        /// </summary>
        public bool CanFlyStraightIn(Vector2 origin, Vector2 touchdownPoint, Vector2 runwayDirection, float maxAngleDeg)
        {
            if (_points.Count == 0 || runwayDirection.sqrMagnitude <= 1e-6f) return false;
            Vector2 dir = runwayDirection.normalized;
            if (Vector2.Dot(origin - touchdownPoint, dir) > -ThresholdTrimBand) return false;
            for (int i = 0; i < _points.Count; i++)
            {
                if (Vector2.Dot(_points[i] - touchdownPoint, dir) <= -ThresholdTrimBand) return false;
            }
            return Vector2.Angle(touchdownPoint - origin, dir) <= maxAngleDeg;
        }

        /// <summary>
        /// Index of the last point the landing trim keeps: trailing points less than <see cref="ThresholdTrimBand"/>
        /// before the threshold line (or past it) along unit <paramref name="dir"/> are dropped, never the first point.
        /// -1 for an empty path.
        /// </summary>
        private int LastKeptBeforeThreshold(Vector2 touchdownPoint, Vector2 dir)
        {
            int i = _points.Count - 1;
            while (i > 0 && Vector2.Dot(_points[i] - touchdownPoint, dir) > -ThresholdTrimBand) i--;
            return i;
        }

        /// <summary>Total remaining length of the polyline (not including the gap from the aircraft to the first point).</summary>
        public float Length()
        {
            float len = 0f;
            for (int i = 1; i < _points.Count; i++) len += Vector2.Distance(_points[i - 1], _points[i]);
            return len;
        }
    }
}
