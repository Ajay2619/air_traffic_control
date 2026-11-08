using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>
    /// <see cref="FlightPath.FinalLegWithin"/>: the straight leg LockForLanding would build from the last kept point to
    /// the threshold must come in from the approach end; side cuts and backwards legs are rejected.
    /// </summary>
    public sealed class FlightPathFinalLegTests
    {
        private const float Eps = 1e-4f;
        private const float MaxAngle = 60f;
        private const float Tolerance = 15f; // PathInputController locks with maxAngle + 15

        /// <summary>Straight path from <paramref name="start"/> along <paramref name="heading"/> for <paramref name="length"/> units.</summary>
        private static FlightPath StraightPath(Vector2 start, Vector2 heading, float length, float spacing)
        {
            FlightPath p = new FlightPath();
            p.Begin(start);
            p.AddPoint(start + heading.normalized * length, spacing);
            return p;
        }

        private static Vector2 DirectionDeg(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        // ---------------------------------------------------------------- accepted

        [Test]
        public void AlignedApproach_OntoTheRunway_IsAccepted()
        {
            // Threshold (0,0), landing +X; the drawn path runs down the centreline past the threshold.
            FlightPath p = StraightPath(new Vector2(-3f, 0f), Vector2.right, 3.3f, 0.25f);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle), Is.True);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, 1f), Is.True, "The leg is straight along the runway.");
        }

        [Test]
        public void AlignedApproach_WithNonUnitDirection_IsAccepted()
        {
            FlightPath p = StraightPath(new Vector2(2f, -3f), Vector2.up, 3.2f, 0.12f);
            Assert.That(p.FinalLegWithin(new Vector2(2f, 0f), new Vector2(0f, 4f), MaxAngle), Is.True);
        }

        [Test]
        public void SlightlyOffsetParallelApproach_IsAcceptedWithTheTolerance()
        {
            // Parallel to the runway, 0.6 to the side, captured when it enters the zone at along -0.25:
            // leg (0.25, -0.6) is about 67 degrees off the runway.
            FlightPath p = StraightPath(new Vector2(-3f, 0.6f), Vector2.right, 2.75f, 0.25f);
            Vec2Assert.Near(new Vector2(-0.25f, 0.6f), p.LastPoint);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle + Tolerance), Is.True);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle), Is.False, "Without the tolerance it would be refused.");
        }

        [Test]
        public void Helipad_ZeroDirection_IsAlwaysAccepted()
        {
            FlightPath p = StraightPath(new Vector2(3f, 3f), Vector2.left, 2f, 0.25f);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.zero, MaxAngle), Is.True);
        }

        [Test]
        public void EmptyPath_IsAccepted()
        {
            Assert.That(new FlightPath().FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle), Is.True);
        }

        // ---------------------------------------------------------------- rejected

        [Test]
        public void SideCut_FromBehindTheThreshold_IsRejected()
        {
            // Long runway (5.6 x 0.75), threshold (-2.5, 0), landing +X. The path comes from (-3.5, 2.5) at -50 degrees
            // and cuts onto the runway from the side: the capture zone accepts it, but every point past the threshold
            // line is trimmed and the leg from the last kept point (about (-2.56, 1.38)) is about 87 degrees off.
            Vector2 threshold = new Vector2(-2.5f, 0f);
            Vector2 heading = DirectionDeg(-50f);
            FlightPath p = StraightPath(new Vector2(-3.5f, 2.5f), heading, 3.4f, 0.12f);

            bool captured = false;
            for (int i = 0; i < p.Count && !captured; i++)
                captured = LandingMath.RunwayCaptures(threshold, Vector2.right, 5.6f, 0.75f, p.Points[i], heading, MaxAngle);
            Assert.That(captured, Is.True, "Precondition: the side cut reaches the capture zone with an accepted heading.");

            Assert.That(p.FinalLegWithin(threshold, Vector2.right, MaxAngle + Tolerance), Is.False);
            Assert.That(p.FinalLegWithin(threshold, Vector2.right, 85f), Is.False);
            Assert.That(p.FinalLegWithin(threshold, Vector2.right, 89f), Is.True, "The leg is just under 90 degrees off.");
        }

        [Test]
        public void SideCut_FromAheadOfTheThreshold_GivesABackwardsLeg_IsRejected()
        {
            // The aircraft is already ahead of the threshold line: everything is trimmed down to the first point and
            // the leg (-0.5, -1.2) back to the threshold is about 113 degrees off the landing direction.
            Vector2 threshold = new Vector2(-2.5f, 0f);
            FlightPath p = StraightPath(new Vector2(-2f, 1.2f), DirectionDeg(-50f), 1.6f, 0.12f);
            Assert.That(p.FinalLegWithin(threshold, Vector2.right, MaxAngle + Tolerance), Is.False);
            Assert.That(p.FinalLegWithin(threshold, Vector2.right, 110f), Is.False);
            Assert.That(p.FinalLegWithin(threshold, Vector2.right, 115f), Is.True);
        }

        [Test]
        public void FirstPointPastTheThreshold_GivesABackwardsLeg_IsRejected()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(1f, 0f)); // on the runway, past the threshold at (0,0)
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle + Tolerance), Is.False);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, 179f), Is.False, "The leg points straight back.");
        }

        // ---------------------------------------------------------------- agrees with LockForLanding

        [Test]
        public void MatchesTheLegLockForLandingBuilds()
        {
            Vector2 threshold = new Vector2(-2.5f, 0f);
            Vector2 heading = DirectionDeg(-50f);
            Vector2 start = new Vector2(-3.5f, 2.5f);
            FlightPath query = StraightPath(start, heading, 3.4f, 0.12f);
            FlightPath locked = StraightPath(start, heading, 3.4f, 0.12f);

            locked.LockForLanding(0, threshold, Vector2.right, 0.12f);
            Vec2Assert.Near(threshold, locked.LastPoint);
            float legAngle = Vector2.Angle(threshold - locked.Points[locked.Count - 2], Vector2.right);
            Assert.That(legAngle, Is.GreaterThan(80f).And.LessThan(90f), "The lock builds a near-perpendicular leg here.");

            Assert.That(query.FinalLegWithin(threshold, Vector2.right, legAngle + 0.5f), Is.True);
            Assert.That(query.FinalLegWithin(threshold, Vector2.right, legAngle - 0.5f), Is.False);
        }

        // ---------------------------------------------------------------- straight in from the aircraft

        [Test]
        public void CanFlyStraightIn_AircraftShortOfTheThreshold_AllPointsPast_IsTrue()
        {
            // A quick drag from an aircraft lined up just short of the runway re-seeds the path on the runway: the
            // drawn leg points backwards, but the aircraft itself can come straight in.
            FlightPath p = StraightPath(new Vector2(0.2f, 0f), Vector2.right, 0.8f, 0.12f);
            Vector2 aircraft = new Vector2(-0.4f, 0.05f);
            Assert.That(p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle + Tolerance), Is.False, "Precondition.");
            Assert.That(p.CanFlyStraightIn(aircraft, Vector2.zero, Vector2.right, MaxAngle + Tolerance), Is.True);

            int count = p.Count;
            p.CanFlyStraightIn(aircraft, Vector2.zero, Vector2.right, MaxAngle + Tolerance);
            Assert.That(p.Count, Is.EqualTo(count), "The query does not modify the path.");

            // What PathInputController then does: drop the points and lock, leaving just the touchdown point.
            p.Clear();
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.12f);
            Assert.That(p.Count, Is.EqualTo(1));
            Vec2Assert.Near(Vector2.zero, p.LastPoint);
            Assert.That(p.IsLanding, Is.True);
        }

        [Test]
        public void CanFlyStraightIn_AircraftPastTheThreshold_IsFalse()
        {
            // Side cut from ahead of the threshold: the aircraft is past the line too.
            Vector2 threshold = new Vector2(-2.5f, 0f);
            FlightPath p = StraightPath(new Vector2(-2f, 1.2f), DirectionDeg(-50f), 1.6f, 0.12f);
            Assert.That(p.CanFlyStraightIn(new Vector2(-2f, 1.2f), threshold, Vector2.right, MaxAngle + Tolerance), Is.False);
        }

        [Test]
        public void CanFlyStraightIn_AircraftOffToTheSide_IsFalse()
        {
            // Short of the line but 0.6 to the side and only 0.1 back: the leg is about 80 degrees off.
            FlightPath p = StraightPath(new Vector2(0.2f, 0f), Vector2.right, 0.8f, 0.12f);
            Assert.That(p.CanFlyStraightIn(new Vector2(-0.1f, 0.6f), Vector2.zero, Vector2.right, MaxAngle + Tolerance), Is.False);
        }

        [Test]
        public void CanFlyStraightIn_WithADrawnPointBeforeTheThreshold_IsFalse()
        {
            // A side cut from behind the threshold keeps a drawn point before the line: that point starts the leg.
            Vector2 threshold = new Vector2(-2.5f, 0f);
            FlightPath p = StraightPath(new Vector2(-3.5f, 2.5f), DirectionDeg(-50f), 3.4f, 0.12f);
            Assert.That(p.CanFlyStraightIn(new Vector2(-4f, 0f), threshold, Vector2.right, MaxAngle + Tolerance), Is.False);
        }

        [Test]
        public void CanFlyStraightIn_EmptyPathOrHelipad_IsFalse()
        {
            Assert.That(new FlightPath().CanFlyStraightIn(new Vector2(-1f, 0f), Vector2.zero, Vector2.right, MaxAngle), Is.False);
            FlightPath p = StraightPath(new Vector2(0.2f, 0f), Vector2.right, 0.8f, 0.12f);
            Assert.That(p.CanFlyStraightIn(new Vector2(-1f, 0f), Vector2.zero, Vector2.zero, MaxAngle), Is.False);
        }

        [Test]
        public void DoesNotModifyThePath()
        {
            FlightPath p = StraightPath(new Vector2(-3f, 0f), Vector2.right, 3.3f, 0.25f);
            int count = p.Count;
            Vector2 last = p.LastPoint;
            p.FinalLegWithin(Vector2.zero, Vector2.right, MaxAngle);
            Assert.That(p.Count, Is.EqualTo(count));
            Vec2Assert.Near(last, p.LastPoint, Eps);
            Assert.That(p.IsLocked, Is.False);
            Assert.That(p.IsLanding, Is.False);
        }
    }
}
