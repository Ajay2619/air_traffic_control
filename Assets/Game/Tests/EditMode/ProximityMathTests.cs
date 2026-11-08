using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="ProximityMath.Evaluate"/>: collision / warning / clear thresholds.</summary>
    public sealed class ProximityMathTests
    {
        private const float R = 0.5f;      // both radii -> contact distance 1.0
        private const float Margin = 0.9f; // warning up to 1.9

        private static ProximityLevel At(float distance, float margin = Margin)
            => ProximityMath.Evaluate(Vector2.zero, R, new Vector2(distance, 0f), R, margin);

        [Test]
        public void SamePosition_IsCollision()
        {
            Assert.That(At(0f), Is.EqualTo(ProximityLevel.Collision));
        }

        [Test]
        public void Overlapping_IsCollision()
        {
            Assert.That(At(0.7f), Is.EqualTo(ProximityLevel.Collision));
        }

        [Test]
        public void ExactlyTouching_IsCollision()
        {
            Assert.That(At(1f), Is.EqualTo(ProximityLevel.Collision));
        }

        [TestCase(1.05f)]
        [TestCase(1.5f)]
        [TestCase(1.85f)]
        public void WithinWarnMargin_IsWarning(float distance)
        {
            Assert.That(At(distance), Is.EqualTo(ProximityLevel.Warning));
        }

        [TestCase(1.95f)]
        [TestCase(10f)]
        public void BeyondWarnMargin_IsClear(float distance)
        {
            Assert.That(At(distance), Is.EqualTo(ProximityLevel.Clear));
        }

        [Test]
        public void ZeroMargin_HasNoWarningBand()
        {
            Assert.That(At(1.01f, 0f), Is.EqualTo(ProximityLevel.Clear));
            Assert.That(At(0.99f, 0f), Is.EqualTo(ProximityLevel.Collision));
        }

        [Test]
        public void NegativeMargin_IsTreatedAsZero()
        {
            Assert.That(At(1.01f, -1f), Is.EqualTo(ProximityLevel.Clear));
            Assert.That(At(0.99f, -1f), Is.EqualTo(ProximityLevel.Collision));
        }

        [Test]
        public void UnequalRadii_UseTheirSum()
        {
            Vector2 a = new Vector2(1f, 1f);
            Assert.That(ProximityMath.Evaluate(a, 0.2f, a + new Vector2(0f, 0.55f), 0.36f, 0.9f), Is.EqualTo(ProximityLevel.Collision));
            Assert.That(ProximityMath.Evaluate(a, 0.2f, a + new Vector2(0f, 0.6f), 0.36f, 0.9f), Is.EqualTo(ProximityLevel.Warning));
            Assert.That(ProximityMath.Evaluate(a, 0.2f, a + new Vector2(0f, 1.5f), 0.36f, 0.9f), Is.EqualTo(ProximityLevel.Clear));
        }

        [Test]
        public void Diagonal_UsesEuclideanDistance()
        {
            // (0.8, 0.8) is 1.13 away: past contact (1.0) but inside warning (1.9).
            Assert.That(ProximityMath.Evaluate(Vector2.zero, R, new Vector2(0.8f, 0.8f), R, Margin), Is.EqualTo(ProximityLevel.Warning));
        }

        [Test]
        public void Evaluate_IsSymmetric()
        {
            Vector2 a = new Vector2(-1f, 2f), b = new Vector2(0.3f, 2.4f);
            for (float m = 0f; m <= 2f; m += 0.25f)
            {
                Assert.That(ProximityMath.Evaluate(a, 0.3f, b, 0.4f, m), Is.EqualTo(ProximityMath.Evaluate(b, 0.4f, a, 0.3f, m)));
            }
        }
    }
}
