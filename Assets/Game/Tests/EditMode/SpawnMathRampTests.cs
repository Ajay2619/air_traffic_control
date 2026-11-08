using NUnit.Framework;

namespace ClearToLand.Tests
{
    /// <summary><see cref="SpawnMath"/>: spawn-interval ramp, max-aircraft steps and weighted picks.</summary>
    public sealed class SpawnMathRampTests
    {
        private const float Eps = 1e-4f;

        // ---------------------------------------------------------------- SpawnInterval

        [Test]
        public void SpawnInterval_AtStart_IsInitial()
        {
            Assert.That(SpawnMath.SpawnInterval(0f, 8f, 3f, 400f), Is.EqualTo(8f).Within(Eps));
        }

        [Test]
        public void SpawnInterval_HalfwayThroughRamp_IsMidpoint()
        {
            Assert.That(SpawnMath.SpawnInterval(200f, 8f, 3f, 400f), Is.EqualTo(5.5f).Within(Eps));
        }

        [TestCase(400f)]
        [TestCase(10000f)]
        public void SpawnInterval_AfterRamp_IsMin(float elapsed)
        {
            Assert.That(SpawnMath.SpawnInterval(elapsed, 8f, 3f, 400f), Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void SpawnInterval_NegativeElapsed_IsInitial()
        {
            Assert.That(SpawnMath.SpawnInterval(-50f, 8f, 3f, 400f), Is.EqualTo(8f).Within(Eps));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void SpawnInterval_NonPositiveRamp_IsMinImmediately(float ramp)
        {
            Assert.That(SpawnMath.SpawnInterval(0f, 8f, 3f, ramp), Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void SpawnInterval_NeverIncreasesOverTime()
        {
            float prev = float.MaxValue;
            for (float t = 0f; t <= 600f; t += 7.5f)
            {
                float v = SpawnMath.SpawnInterval(t, 7f, 2.7f, 380f);
                Assert.That(v, Is.LessThanOrEqualTo(prev + Eps), "t=" + t);
                Assert.That(v, Is.InRange(2.7f - Eps, 7f + Eps), "t=" + t);
                prev = v;
            }
        }

        // ---------------------------------------------------------------- MaxAircraft

        [Test]
        public void MaxAircraft_AtStart_IsInitial()
        {
            Assert.That(SpawnMath.MaxAircraft(0f, 3, 10, 40f), Is.EqualTo(3));
        }

        [TestCase(39.9f, 3)]
        [TestCase(40f, 4)]
        [TestCase(100f, 5)]
        [TestCase(279f, 9)]
        public void MaxAircraft_GrowsByOneEveryStep(float elapsed, int expected)
        {
            Assert.That(SpawnMath.MaxAircraft(elapsed, 3, 10, 40f), Is.EqualTo(expected));
        }

        [TestCase(280f)]
        [TestCase(100000f)]
        public void MaxAircraft_IsCapped(float elapsed)
        {
            Assert.That(SpawnMath.MaxAircraft(elapsed, 3, 10, 40f), Is.EqualTo(10));
        }

        [Test]
        public void MaxAircraft_CapBelowInitial_WinsOverInitial()
        {
            Assert.That(SpawnMath.MaxAircraft(0f, 5, 3, 40f), Is.EqualTo(3));
        }

        [Test]
        public void MaxAircraft_IsNeverBelowOne()
        {
            Assert.That(SpawnMath.MaxAircraft(0f, 0, 10, 40f), Is.EqualTo(1));
            Assert.That(SpawnMath.MaxAircraft(0f, 3, 0, 40f), Is.EqualTo(1));
            Assert.That(SpawnMath.MaxAircraft(0f, -4, -2, 40f), Is.EqualTo(1));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void MaxAircraft_NonPositiveStep_StaysAtInitial(float step)
        {
            Assert.That(SpawnMath.MaxAircraft(1000f, 3, 10, step), Is.EqualTo(3));
        }

        [Test]
        public void MaxAircraft_NegativeElapsed_IsInitial()
        {
            Assert.That(SpawnMath.MaxAircraft(-500f, 3, 10, 40f), Is.EqualTo(3));
        }

        // ---------------------------------------------------------------- PickWeighted

        [TestCase(0.0, 0)]
        [TestCase(0.49, 0)]
        [TestCase(0.51, 1)]
        [TestCase(0.999, 1)]
        public void PickWeighted_EqualWeights_SplitsRange(double r, int expected)
        {
            Assert.That(SpawnMath.PickWeighted(new[] { 1f, 1f }, r), Is.EqualTo(expected));
        }

        [TestCase(0.0, 1)]
        [TestCase(0.49, 1)]
        [TestCase(0.51, 3)]
        [TestCase(0.99, 3)]
        public void PickWeighted_SkipsZeroWeights(double r, int expected)
        {
            Assert.That(SpawnMath.PickWeighted(new[] { 0f, 2f, 0f, 2f, 0f }, r), Is.EqualTo(expected));
        }

        [Test]
        public void PickWeighted_NeverPicksNonPositiveWeights()
        {
            float[] w = { 0f, 1.5f, -3f, 0.5f, 0f };
            for (int i = 0; i <= 1000; i++)
            {
                int pick = SpawnMath.PickWeighted(w, i / 1000.0);
                Assert.That(pick == 1 || pick == 3, Is.True, "r=" + (i / 1000.0) + " picked " + pick);
            }
        }

        [Test]
        public void PickWeighted_AllZero_ReturnsMinusOne()
        {
            Assert.That(SpawnMath.PickWeighted(new[] { 0f, 0f, 0f }, 0.5), Is.EqualTo(-1));
        }

        [Test]
        public void PickWeighted_AllNegative_ReturnsMinusOne()
        {
            Assert.That(SpawnMath.PickWeighted(new[] { -1f, -2f }, 0.5), Is.EqualTo(-1));
        }

        [Test]
        public void PickWeighted_Empty_ReturnsMinusOne()
        {
            Assert.That(SpawnMath.PickWeighted(new float[0], 0.5), Is.EqualTo(-1));
        }

        [Test]
        public void PickWeighted_SinglePositive_IsAlwaysPicked()
        {
            float[] w = { -1f, 0f, 3f };
            Assert.That(SpawnMath.PickWeighted(w, 0.0), Is.EqualTo(2));
            Assert.That(SpawnMath.PickWeighted(w, 0.5), Is.EqualTo(2));
            Assert.That(SpawnMath.PickWeighted(w, 0.9999), Is.EqualTo(2));
        }

        [Test]
        public void PickWeighted_ROfOneOrMore_ReturnsLastPositive()
        {
            float[] w = { 1f, 1f, 0f };
            Assert.That(SpawnMath.PickWeighted(w, 1.0), Is.EqualTo(1));
            Assert.That(SpawnMath.PickWeighted(w, 5.0), Is.EqualTo(1));
        }

        [Test]
        public void PickWeighted_NegativeR_ReturnsFirstPositive()
        {
            Assert.That(SpawnMath.PickWeighted(new[] { 0f, 2f, 1f }, -0.5), Is.EqualTo(1));
        }

        [Test]
        public void PickWeighted_SelectionIsProportionalToWeight()
        {
            float[] w = { 1f, 3f };
            int[] counts = new int[2];
            const int samples = 4000;
            for (int i = 0; i < samples; i++) counts[SpawnMath.PickWeighted(w, (i + 0.5) / samples)]++;
            Assert.That(counts[0], Is.EqualTo(1000).Within(2));
            Assert.That(counts[1], Is.EqualTo(3000).Within(2));
        }
    }
}
