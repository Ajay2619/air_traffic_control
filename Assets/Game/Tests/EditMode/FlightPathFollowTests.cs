using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="FlightPath"/>: Advance consumption / direction / distance, EndDirection, LockForLanding.</summary>
    public sealed class FlightPathFollowTests
    {
        private const float Eps = 1e-4f;

        /// <summary>Path (0,0) -> (1,0) sampled every 0.25.</summary>
        private static FlightPath StraightUnitPath()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(1f, 0f), 0.25f);
            return p;
        }

        /// <summary>L-shape: (0,0) -> (2,0) -> (2,1), sampled every 0.25 (13 points).</summary>
        private static FlightPath LShapedPath()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(2f, 0f), 0.25f);
            p.AddPoint(new Vector2(2f, 1f), 0.25f);
            return p;
        }

        // ---------------------------------------------------------------- Advance

        [Test]
        public void Advance_ConsumesReachedWaypoints_AndStopsMidSegment()
        {
            FlightPath p = StraightUnitPath();
            Vector2 pos = Vector2.zero, dir = Vector2.up;
            float travelled = p.Advance(ref pos, 0.6f, ref dir);
            Assert.That(travelled, Is.EqualTo(0.6f).Within(Eps));
            Vec2Assert.Near(new Vector2(0.6f, 0f), pos);
            Vec2Assert.Near(Vector2.right, dir);
            Assert.That(p.Count, Is.EqualTo(2));
            Vec2Assert.Near(new Vector2(0.75f, 0f), p.Points[0]);
        }

        [Test]
        public void Advance_PastEnd_ReturnsRemainingDistanceAndEmptiesPath()
        {
            FlightPath p = StraightUnitPath();
            Vector2 pos = Vector2.zero, dir = Vector2.up;
            float travelled = p.Advance(ref pos, 5f, ref dir);
            Assert.That(travelled, Is.EqualTo(1f).Within(Eps));
            Vec2Assert.Near(new Vector2(1f, 0f), pos);
            Vec2Assert.Near(Vector2.right, dir);
            Assert.That(p.IsEmpty, Is.True);
        }

        [Test]
        public void Advance_IncludesGapFromPositionToFirstPoint()
        {
            FlightPath p = new FlightPath();
            p.AddPoint(new Vector2(2f, 0f), 0.25f);
            Vector2 pos = Vector2.zero, dir = Vector2.up;
            float travelled = p.Advance(ref pos, 0.5f, ref dir);
            Assert.That(travelled, Is.EqualTo(0.5f).Within(Eps));
            Vec2Assert.Near(new Vector2(0.5f, 0f), pos);
            Vec2Assert.Near(Vector2.right, dir);
            Assert.That(p.Count, Is.EqualTo(1), "The target point is not reached yet.");
        }

        [Test]
        public void Advance_AroundCorner_ReportsNewDirection()
        {
            FlightPath p = LShapedPath();
            Vector2 pos = Vector2.zero, dir = Vector2.left;
            float travelled = p.Advance(ref pos, 2.5f, ref dir);
            Assert.That(travelled, Is.EqualTo(2.5f).Within(Eps));
            Vec2Assert.Near(new Vector2(2f, 0.5f), pos);
            Vec2Assert.Near(Vector2.up, dir);
        }

        [Test]
        public void Advance_OnEmptyPath_LeavesPositionAndDirectionUnchanged()
        {
            FlightPath p = new FlightPath();
            Vector2 pos = new Vector2(3f, 3f), dir = Vector2.up;
            Assert.That(p.Advance(ref pos, 1f, ref dir), Is.EqualTo(0f));
            Vec2Assert.Near(new Vector2(3f, 3f), pos);
            Vec2Assert.Near(Vector2.up, dir);
        }

        [Test]
        public void Advance_ZeroDistance_ConsumesNothing()
        {
            FlightPath p = StraightUnitPath();
            Vector2 pos = Vector2.zero, dir = Vector2.up;
            Assert.That(p.Advance(ref pos, 0f, ref dir), Is.EqualTo(0f));
            Assert.That(p.Count, Is.EqualTo(5));
            Vec2Assert.Near(Vector2.up, dir);
        }

        [Test]
        public void Advance_ZeroLengthStepOntoCurrentPoint_KeepsPreviousDirection()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(1f, 1f));
            Vector2 pos = new Vector2(1f, 1f), dir = Vector2.up;
            float travelled = p.Advance(ref pos, 1f, ref dir);
            Assert.That(travelled, Is.EqualTo(0f).Within(Eps));
            Assert.That(p.IsEmpty, Is.True, "The point under the aircraft is consumed.");
            Vec2Assert.Near(Vector2.up, dir);
        }

        [Test]
        public void Advance_ManySmallSteps_MatchesOneLargeStep()
        {
            FlightPath a = LShapedPath();
            FlightPath b = LShapedPath();
            Vector2 posA = Vector2.zero, dirA = Vector2.right;
            Vector2 posB = Vector2.zero, dirB = Vector2.right;
            float sum = 0f;
            for (int i = 0; i < 46; i++) sum += a.Advance(ref posA, 0.05f, ref dirA);
            float big = b.Advance(ref posB, 2.3f, ref dirB);
            Assert.That(sum, Is.EqualTo(big).Within(1e-3f));
            Vec2Assert.Near(posB, posA, 1e-3f);
            Vec2Assert.Near(dirB, dirA, 1e-3f);
            Assert.That(a.Count, Is.EqualTo(b.Count));
        }

        // ---------------------------------------------------------------- EndDirection

        [Test]
        public void EndDirection_WithFewerThanTwoPoints_IsZero()
        {
            FlightPath p = new FlightPath();
            Assert.That(p.EndDirection(), Is.EqualTo(Vector2.zero));
            p.Begin(new Vector2(1f, 1f));
            Assert.That(p.EndDirection(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void EndDirection_StraightPath_PointsAlongPath()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(0f, -2f), 0.25f);
            Vec2Assert.Near(Vector2.down, p.EndDirection(0.5f));
        }

        [Test]
        public void EndDirection_ShortLookback_UsesOnlyTheLastSegments()
        {
            Vec2Assert.Near(Vector2.up, LShapedPath().EndDirection(0.5f));
        }

        [Test]
        public void EndDirection_LongLookback_AveragesAcrossTheTurn()
        {
            // Walking back 2 units from (2,1) reaches (1,0): direction (1,1)/sqrt2.
            Vec2Assert.Near(new Vector2(1f, 1f).normalized, LShapedPath().EndDirection(2f));
        }

        [Test]
        public void EndDirection_LookbackLongerThanPath_UsesFirstPoint()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(1f, 0f), 0.25f);
            p.AddPoint(new Vector2(1f, 1f), 0.25f);
            Vec2Assert.Near(new Vector2(1f, 1f).normalized, p.EndDirection(10f));
        }

        [Test]
        public void EndDirection_IsUnitLength()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-1f, 2f));
            p.AddPoint(new Vector2(3f, -1.5f), 0.12f);
            Assert.That(p.EndDirection(0.5f).magnitude, Is.EqualTo(1f).Within(Eps));
        }

        // ---------------------------------------------------------------- LockForLanding

        [Test]
        public void LockForLanding_SetsLockedLandingAndSpotIndex()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-3f, 0f));
            p.LockForLanding(2, Vector2.zero, Vector2.right, 0.25f);
            Assert.That(p.IsLocked, Is.True);
            Assert.That(p.IsLanding, Is.True);
            Assert.That(p.LandingSpotIndex, Is.EqualTo(2));
        }

        [Test]
        public void LockForLanding_TrimsPointsPastThreshold_AndEndsOnTouchdown()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-3f, 0f));
            p.AddPoint(new Vector2(1f, 0f), 0.25f); // overshoots the threshold at x = 0
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.25f);

            Vec2Assert.Near(Vector2.zero, p.LastPoint);
            Assert.That(p.Count, Is.EqualTo(13), "-3 .. -0.25 (12 points) + touchdown.");
            for (int i = 0; i < p.Count - 1; i++)
                Assert.That(p.Points[i].x, Is.LessThanOrEqualTo(-0.05f + Eps), "Point " + i + " is past the threshold.");
        }

        [Test]
        public void LockForLanding_UsesNormalizedRunwayDirection()
        {
            // Last drawn point is 0.02 before the threshold: inside the 0.05 trim band only when the
            // (non-unit) runway direction is normalized before the dot product.
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(2f, -1f));
            p.AddPoint(new Vector2(2f, 1.98f), 0.25f); // 11 subdivisions -> 12 points
            p.LockForLanding(0, new Vector2(2f, 2f), new Vector2(0f, 5f), 0.25f);

            Vec2Assert.Near(new Vector2(2f, 2f), p.LastPoint);
            Assert.That(p.Count, Is.EqualTo(13), "1.98 trimmed (11 left) + 2 approach points.");
            for (int i = 0; i < p.Count - 1; i++)
                Assert.That(p.Points[i].y, Is.LessThanOrEqualTo(1.95f + Eps), "Point " + i + " is past the threshold.");
        }

        [Test]
        public void LockForLanding_FromFarAway_FillsGapAtSpacing()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-3f, 0f));
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.25f);
            Assert.That(p.Count, Is.EqualTo(13));
            Vec2Assert.Near(Vector2.zero, p.LastPoint);
            for (int i = 1; i < p.Count; i++)
                Assert.That(Vector2.Distance(p.Points[i - 1], p.Points[i]), Is.LessThanOrEqualTo(0.25f + Eps));
        }

        [Test]
        public void LockForLanding_Helipad_DoesNotTrim()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(2f, 0f), 0.25f); // 9 points, passes over the pad at (1,0)
            p.LockForLanding(1, new Vector2(1f, 0f), Vector2.zero, 0.25f);
            Assert.That(p.Count, Is.EqualTo(13));
            Vec2Assert.Near(new Vector2(2f, 0f), p.Points[8], Eps, "Points past a helipad are kept.");
            Vec2Assert.Near(new Vector2(1f, 0f), p.LastPoint);
            Assert.That(p.LandingSpotIndex, Is.EqualTo(1));
        }

        [Test]
        public void LockForLanding_NeverTrimsTheFirstPoint()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(1f, 0f)); // already past the threshold
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.25f);
            Vec2Assert.Near(new Vector2(1f, 0f), p.Points[0]);
            Vec2Assert.Near(Vector2.zero, p.LastPoint);
            Assert.That(p.Count, Is.EqualTo(5));
        }

        [Test]
        public void LockForLanding_OnEmptyPath_AddsOnlyTouchdown()
        {
            FlightPath p = new FlightPath();
            p.LockForLanding(4, new Vector2(3f, 3f), Vector2.right, 0.25f);
            Assert.That(p.Count, Is.EqualTo(1));
            Vec2Assert.Near(new Vector2(3f, 3f), p.LastPoint);
            Assert.That(p.IsLocked, Is.True);
        }

        [Test]
        public void LockForLanding_ZeroSpacing_UsesMinimumStep()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-0.1f, 0f));
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0f);
            Vec2Assert.Near(Vector2.zero, p.LastPoint);
            Assert.That(p.Count, Is.GreaterThan(2));
            for (int i = 1; i < p.Count; i++)
                Assert.That(Vector2.Distance(p.Points[i - 1], p.Points[i]), Is.LessThanOrEqualTo(0.01f + Eps));
        }

        [Test]
        public void LockedPath_IsConsumedUpToTouchdown()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-2f, 0f));
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.12f);
            Vector2 pos = new Vector2(-2f, 0f), dir = Vector2.right;
            float travelled = p.Advance(ref pos, 10f, ref dir);
            Assert.That(travelled, Is.EqualTo(2f).Within(1e-3f));
            Vec2Assert.Near(Vector2.zero, pos);
            Assert.That(p.IsEmpty, Is.True);
            Assert.That(p.IsLanding, Is.True, "Landing info survives consumption so the aircraft knows where to land.");
            Assert.That(p.LandingSpotIndex, Is.EqualTo(0));
        }
    }
}
