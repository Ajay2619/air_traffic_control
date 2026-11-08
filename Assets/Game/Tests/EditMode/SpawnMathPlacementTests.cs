using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="SpawnMath"/>: perimeter walk, edge-point picking (corners / avoid points), determinism.</summary>
    public sealed class SpawnMathPlacementTests
    {
        private const float Eps = 1e-4f;
        private static readonly Rect Small = new Rect(-2f, -1f, 4f, 2f);   // perimeter 12
        private static readonly Rect View = new Rect(-9.6f, -5.4f, 19.2f, 10.8f);

        private static bool StrictlyOutside(Rect r, Vector2 p)
            => p.x < r.xMin || p.x > r.xMax || p.y < r.yMin || p.y > r.yMax;

        private static void AssertOnEdgeWithNormal(Rect r, Vector2 p, Vector2 n, string context)
        {
            Vec2Assert.Inside(r, p, Eps, context);
            Assert.That(n.magnitude, Is.EqualTo(1f).Within(Eps), context + " normal must be unit length");
            if (n == Vector2.down) Assert.That(p.y, Is.EqualTo(r.yMin).Within(Eps), context);
            else if (n == Vector2.up) Assert.That(p.y, Is.EqualTo(r.yMax).Within(Eps), context);
            else if (n == Vector2.left) Assert.That(p.x, Is.EqualTo(r.xMin).Within(Eps), context);
            else if (n == Vector2.right) Assert.That(p.x, Is.EqualTo(r.xMax).Within(Eps), context);
            else Assert.Fail(context + " normal is not axis aligned: " + Vec2Assert.Format(n));
            Assert.That(StrictlyOutside(r, p + n * 0.01f), Is.True, context + " normal must point outward");
        }

        // ---------------------------------------------------------------- PointOnPerimeter

        [TestCase(0f, -2f, -1f, 0f, -1f)]
        [TestCase(2f / 12f, 0f, -1f, 0f, -1f)]
        [TestCase(5f / 12f, 2f, 0f, 1f, 0f)]
        [TestCase(8f / 12f, 0f, 1f, 0f, 1f)]
        [TestCase(11f / 12f, -2f, 0f, -1f, 0f)]
        public void PointOnPerimeter_WalksBottomRightTopLeft(float t, float px, float py, float nx, float ny)
        {
            Vector2 p = SpawnMath.PointOnPerimeter(Small, t, out Vector2 n);
            Vec2Assert.Near(new Vector2(px, py), p, 1e-3f);
            Vec2Assert.Near(new Vector2(nx, ny), n);
        }

        [Test]
        public void PointOnPerimeter_WrapsOutOfRangeFractions()
        {
            Vector2 p0 = SpawnMath.PointOnPerimeter(Small, 0f, out Vector2 n0);
            Vector2 p1 = SpawnMath.PointOnPerimeter(Small, 1f, out Vector2 n1);
            Vec2Assert.Near(p0, p1);
            Vec2Assert.Near(n0, n1);

            Vec2Assert.Near(new Vector2(1f, -1f), SpawnMath.PointOnPerimeter(Small, 1.25f, out Vector2 _), 1e-3f);
            Vec2Assert.Near(new Vector2(-1f, 1f), SpawnMath.PointOnPerimeter(Small, -0.25f, out Vector2 nNeg), 1e-3f);
            Vec2Assert.Near(Vector2.up, nNeg);
        }

        [Test]
        public void PointOnPerimeter_EverySampleLiesOnEdgeWithOutwardNormal()
        {
            for (int i = 0; i < 200; i++)
            {
                float t = i / 200f;
                Vector2 p = SpawnMath.PointOnPerimeter(View, t, out Vector2 n);
                AssertOnEdgeWithNormal(View, p, n, "t=" + t);
            }
        }

        // ---------------------------------------------------------------- TryPickEdgePoint

        [Test]
        public void TryPickEdgePoint_AvoidsCornersAndStaysOnEdge()
        {
            Vector2[] corners =
            {
                new Vector2(View.xMin, View.yMin), new Vector2(View.xMax, View.yMin),
                new Vector2(View.xMin, View.yMax), new Vector2(View.xMax, View.yMax),
            };
            for (int seed = 0; seed < 50; seed++)
            {
                System.Random rng = new System.Random(seed);
                bool ok = SpawnMath.TryPickEdgePoint(View, rng, null, 2.5f, 2.2f, 12, out Vector2 p, out Vector2 n);
                Assert.That(ok, Is.True, "seed " + seed);
                AssertOnEdgeWithNormal(View, p, n, "seed " + seed);
                foreach (Vector2 c in corners)
                    Assert.That(Vector2.Distance(p, c), Is.GreaterThanOrEqualTo(2.2f - Eps), "seed " + seed + " too close to a corner");
            }
        }

        [Test]
        public void TryPickEdgePoint_KeepsAwayFromAvoidPoints()
        {
            List<Vector2> avoid = new List<Vector2> { new Vector2(0f, -5.4f), new Vector2(9.6f, 0f), new Vector2(0f, 5.4f) };
            int successes = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                System.Random rng = new System.Random(seed);
                if (!SpawnMath.TryPickEdgePoint(View, rng, avoid, 4f, 2.2f, 50, out Vector2 p, out Vector2 n)) continue;
                successes++;
                AssertOnEdgeWithNormal(View, p, n, "seed " + seed);
                foreach (Vector2 a in avoid)
                    Assert.That(Vector2.Distance(p, a), Is.GreaterThanOrEqualTo(4f - Eps), "seed " + seed);
            }
            Assert.That(successes, Is.GreaterThan(40), "Most of the perimeter is still free, picks should succeed.");
        }

        [Test]
        public void TryPickEdgePoint_CornerDistanceLargerThanRect_Fails()
        {
            bool ok = SpawnMath.TryPickEdgePoint(Small, new System.Random(1), null, 0f, 10f, 30, out Vector2 p, out Vector2 n);
            Assert.That(ok, Is.False);
            Assert.That(p, Is.EqualTo(default(Vector2)));
            Assert.That(n, Is.EqualTo(default(Vector2)));
        }

        [Test]
        public void TryPickEdgePoint_AvoidPointCoveringEverything_Fails()
        {
            Vector2[] avoid = { Vector2.zero };
            Assert.That(SpawnMath.TryPickEdgePoint(View, new System.Random(1), avoid, 100f, 2.2f, 30, out Vector2 _, out Vector2 _), Is.False);
        }

        [Test]
        public void TryPickEdgePoint_ZeroAttempts_Fails()
        {
            Assert.That(SpawnMath.TryPickEdgePoint(View, new System.Random(1), null, 0f, 0f, 0, out Vector2 _, out Vector2 _), Is.False);
        }

        [Test]
        public void TryPickEdgePoint_EmptyAvoidList_MatchesNull()
        {
            SpawnMath.TryPickEdgePoint(View, new System.Random(77), null, 2.5f, 2.2f, 12, out Vector2 a, out Vector2 na);
            SpawnMath.TryPickEdgePoint(View, new System.Random(77), new Vector2[0], 2.5f, 2.2f, 12, out Vector2 b, out Vector2 nb);
            Assert.That(b, Is.EqualTo(a));
            Assert.That(nb, Is.EqualTo(na));
        }

        [Test]
        public void TryPickEdgePoint_SameSeed_GivesSameSequence()
        {
            System.Random r1 = new System.Random(1234);
            System.Random r2 = new System.Random(1234);
            List<Vector2> avoid1 = new List<Vector2>();
            List<Vector2> avoid2 = new List<Vector2>();
            for (int i = 0; i < 20; i++)
            {
                bool ok1 = SpawnMath.TryPickEdgePoint(View, r1, avoid1, 2.5f, 2.2f, 12, out Vector2 p1, out Vector2 n1);
                bool ok2 = SpawnMath.TryPickEdgePoint(View, r2, avoid2, 2.5f, 2.2f, 12, out Vector2 p2, out Vector2 n2);
                Assert.That(ok2, Is.EqualTo(ok1), "pick " + i);
                Assert.That(p2, Is.EqualTo(p1), "pick " + i);
                Assert.That(n2, Is.EqualTo(n1), "pick " + i);
                if (ok1) { avoid1.Add(p1); avoid2.Add(p2); }
            }
        }

        // ---------------------------------------------------------------- RandomPointInRect

        [Test]
        public void RandomPointInRect_StaysInsideAndCoversQuadrants()
        {
            Rect r = new Rect(-6.6f, -3.4f, 13.2f, 6.8f);
            System.Random rng = new System.Random(5);
            bool q1 = false, q2 = false, q3 = false, q4 = false;
            for (int i = 0; i < 1000; i++)
            {
                Vector2 p = SpawnMath.RandomPointInRect(r, rng);
                Vec2Assert.Inside(r, p, Eps, "sample " + i);
                q1 |= p.x > 0f && p.y > 0f;
                q2 |= p.x < 0f && p.y > 0f;
                q3 |= p.x < 0f && p.y < 0f;
                q4 |= p.x > 0f && p.y < 0f;
            }
            Assert.That(q1 && q2 && q3 && q4, Is.True, "1000 uniform samples should hit every quadrant.");
        }

        [Test]
        public void RandomPointInRect_SameSeed_IsDeterministic()
        {
            System.Random r1 = new System.Random(99), r2 = new System.Random(99);
            for (int i = 0; i < 50; i++)
                Assert.That(SpawnMath.RandomPointInRect(View, r2), Is.EqualTo(SpawnMath.RandomPointInRect(View, r1)));
        }
    }
}
