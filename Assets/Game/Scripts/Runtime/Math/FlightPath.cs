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
            if (runwayDirection.sqrMagnitude > 1e-6f)
            {
                Vector2 dir = runwayDirection.normalized;
                while (_points.Count > 1 && Vector2.Dot(_points[_points.Count - 1] - touchdownPoint, dir) > -0.05f)
                    _points.RemoveAt(_points.Count - 1);
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

        /// <summary>Total remaining length of the polyline (not including the gap from the aircraft to the first point).</summary>
        public float Length()
        {
            float len = 0f;
            for (int i = 1; i < _points.Count; i++) len += Vector2.Distance(_points[i - 1], _points[i]);
            return len;
        }
    }
}
