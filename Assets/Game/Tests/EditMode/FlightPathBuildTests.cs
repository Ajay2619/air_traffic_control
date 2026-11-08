using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="FlightPath"/>: Begin / Clear / AddPoint spacing, subdivision, MaxPoints cap, Length.</summary>
    public sealed class FlightPathBuildTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void NewPath_IsEmptyAndUnlocked()
        {
            FlightPath p = new FlightPath();
            Assert.That(p.IsEmpty, Is.True);
            Assert.That(p.Count, Is.EqualTo(0));
            Assert.That(p.IsLocked, Is.False);
            Assert.That(p.IsLanding, Is.False);
            Assert.That(p.LandingSpotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Begin_StartsWithSingleOriginPoint()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(1f, 2f));
            Assert.That(p.Count, Is.EqualTo(1));
            Assert.That(p.IsEmpty, Is.False);
            Vec2Assert.Near(new Vector2(1f, 2f), p.Points[0]);
            Vec2Assert.Near(new Vector2(1f, 2f), p.LastPoint);
        }

        [Test]
        public void Begin_AfterLock_ReplacesPathAndResetsLandingState()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-2f, 0f));
            p.LockForLanding(3, Vector2.zero, Vector2.right, 0.25f);
            p.Begin(new Vector2(5f, 5f));
            Assert.That(p.IsLocked, Is.False);
            Assert.That(p.IsLanding, Is.False);
            Assert.That(p.LandingSpotIndex, Is.EqualTo(-1));
            Assert.That(p.Count, Is.EqualTo(1));
            Vec2Assert.Near(new Vector2(5f, 5f), p.Points[0]);
        }

        [Test]
        public void Clear_RemovesPointsAndLandingState()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-2f, 0f));
            p.LockForLanding(1, Vector2.zero, Vector2.right, 0.25f);
            p.Clear();
            Assert.That(p.IsEmpty, Is.True);
            Assert.That(p.IsLocked, Is.False);
            Assert.That(p.IsLanding, Is.False);
            Assert.That(p.LandingSpotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void AddPoint_OnEmptyPath_AddsThePointDirectly()
        {
            FlightPath p = new FlightPath();
            Assert.That(p.AddPoint(new Vector2(3f, 4f), 0.25f), Is.EqualTo(1));
            Assert.That(p.Count, Is.EqualTo(1));
            Vec2Assert.Near(new Vector2(3f, 4f), p.LastPoint);
        }

        [Test]
        public void AddPoint_CloserThanSpacing_IsIgnored()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            Assert.That(p.AddPoint(new Vector2(0.1f, 0f), 0.25f), Is.EqualTo(0));
            Assert.That(p.AddPoint(new Vector2(0.17f, 0.17f), 0.25f), Is.EqualTo(0));
            Assert.That(p.Count, Is.EqualTo(1));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void AddPoint_NonPositiveSpacing_IsIgnored(float spacing)
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            Assert.That(p.AddPoint(new Vector2(1f, 0f), spacing), Is.EqualTo(0));
            Assert.That(p.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddPoint_ExactlySpacingAway_AddsOnePoint()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            Assert.That(p.AddPoint(new Vector2(0.25f, 0f), 0.25f), Is.EqualTo(1));
            Vec2Assert.Near(new Vector2(0.25f, 0f), p.LastPoint);
        }

        [Test]
        public void AddPoint_LongJump_IsSubdividedEvenly()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            Assert.That(p.AddPoint(new Vector2(1f, 0f), 0.25f), Is.EqualTo(4));
            Assert.That(p.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++) Vec2Assert.Near(new Vector2(0.25f * i, 0f), p.Points[i], Eps, "Point " + i);
        }

        [Test]
        public void AddPoint_NonMultipleJump_EndsExactlyOnTarget()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            Assert.That(p.AddPoint(new Vector2(1f, 0f), 0.3f), Is.EqualTo(3));
            Vec2Assert.Near(new Vector2(1f, 0f), p.LastPoint);
            Vec2Assert.Near(new Vector2(1f / 3f, 0f), p.Points[1]);
            Vec2Assert.Near(new Vector2(2f / 3f, 0f), p.Points[2]);
        }

        [Test]
        public void AddPoint_ArbitraryJumps_KeepGapsBetweenSpacingAndTwiceSpacing()
        {
            const float spacing = 0.12f;
            Vector2[] targets =
            {
                new Vector2(2.3f, 1.7f), new Vector2(-1.1f, 0.4f), new Vector2(0.05f, -3.2f),
                new Vector2(0.2f, -3.1f), new Vector2(4.4f, 2.9f),
            };
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            foreach (Vector2 t in targets)
            {
                p.AddPoint(t, spacing);
                Vec2Assert.Near(t, p.LastPoint, Eps, "Last point should land exactly on the drawn point.");
            }

            for (int i = 1; i < p.Count; i++)
            {
                float gap = Vector2.Distance(p.Points[i - 1], p.Points[i]);
                Assert.That(gap, Is.GreaterThanOrEqualTo(spacing - Eps), "Gap " + i);
                Assert.That(gap, Is.LessThanOrEqualTo(spacing * 2f + Eps), "Gap " + i);
            }
        }

        [Test]
        public void AddPoint_HugeJump_StopsAtMaxPoints()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            int added = p.AddPoint(new Vector2(3000f, 0f), 1f);
            Assert.That(added, Is.EqualTo(FlightPath.MaxPoints - 1));
            Assert.That(p.Count, Is.EqualTo(FlightPath.MaxPoints));
            Assert.That(p.AddPoint(new Vector2(1600f, 0f), 1f), Is.EqualTo(0), "A full path accepts no more points.");
            Assert.That(p.Count, Is.EqualTo(FlightPath.MaxPoints));
        }

        [Test]
        public void AddPoint_ManySmallStrokes_NeverExceedsMaxPoints()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            for (int i = 1; i <= 3000; i++) p.AddPoint(new Vector2(i, (i % 2) * 0.5f), 0.5f);
            Assert.That(p.Count, Is.EqualTo(FlightPath.MaxPoints));
        }

        [Test]
        public void AddPoint_WhenLocked_IsIgnored()
        {
            FlightPath p = new FlightPath();
            p.Begin(new Vector2(-3f, 0f));
            p.LockForLanding(0, Vector2.zero, Vector2.right, 0.25f);
            int count = p.Count;
            Assert.That(p.AddPoint(new Vector2(4f, 4f), 0.25f), Is.EqualTo(0));
            Assert.That(p.Count, Is.EqualTo(count));
            Vec2Assert.Near(Vector2.zero, p.LastPoint);
        }

        [Test]
        public void Length_OfEmptyOrSinglePointPath_IsZero()
        {
            FlightPath p = new FlightPath();
            Assert.That(p.Length(), Is.EqualTo(0f));
            p.Begin(new Vector2(4f, 4f));
            Assert.That(p.Length(), Is.EqualTo(0f));
        }

        [Test]
        public void Length_SumsSegments()
        {
            FlightPath p = new FlightPath();
            p.Begin(Vector2.zero);
            p.AddPoint(new Vector2(1f, 0f), 0.25f);
            p.AddPoint(new Vector2(1f, 2f), 0.25f);
            Assert.That(p.Length(), Is.EqualTo(3f).Within(Eps));
        }
    }
}
