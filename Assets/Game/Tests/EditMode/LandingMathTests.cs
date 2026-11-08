using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="LandingMath"/>: runway capture zone, heading tolerance, helipad radius, kind matching.</summary>
    public sealed class LandingMathTests
    {
        // Runway: threshold (0,0), landing direction +X, length 5, width 0.75.
        // Capture box: along in [-0.45, 1.75], |lateral| <= 0.375 + 0.3 = 0.675.
        private static readonly Vector2 Threshold = Vector2.zero;
        private static readonly Vector2 Dir = Vector2.right;
        private const float Length = 5f;
        private const float Width = 0.75f;
        private const float MaxAngle = 60f;

        private static bool Runway(Vector2 point, Vector2 heading, float maxAngle = MaxAngle)
            => LandingMath.RunwayCaptures(Threshold, Dir, Length, Width, point, heading, maxAngle);

        private static Vector2 Heading(float deg)
            => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        [Test]
        public void Runway_AlignedAtThreshold_Captures()
        {
            Assert.That(Runway(Vector2.zero, Vector2.right), Is.True);
        }

        [Test]
        public void Runway_AlignedJustBeforeThreshold_Captures()
        {
            Assert.That(Runway(new Vector2(-0.4f, 0f), Vector2.right), Is.True);
        }

        [Test]
        public void Runway_TooFarBeforeThreshold_IsRejected()
        {
            Assert.That(Runway(new Vector2(-0.5f, 0f), Vector2.right), Is.False);
        }

        [Test]
        public void Runway_InsideFirstThirdOfRunway_Captures()
        {
            Assert.That(Runway(new Vector2(1.7f, 0.1f), Vector2.right), Is.True);
        }

        [Test]
        public void Runway_TooFarAhead_IsRejected()
        {
            Assert.That(Runway(new Vector2(1.8f, 0f), Vector2.right), Is.False);
            Assert.That(Runway(new Vector2(4f, 0f), Vector2.right), Is.False);
        }

        [TestCase(0.6f)]
        [TestCase(-0.6f)]
        public void Runway_WithinLateralPadding_Captures(float lateral)
        {
            Assert.That(Runway(new Vector2(0.5f, lateral), Vector2.right), Is.True);
        }

        [TestCase(0.7f)]
        [TestCase(-0.7f)]
        [TestCase(2f)]
        public void Runway_TooFarLateral_IsRejected(float lateral)
        {
            Assert.That(Runway(new Vector2(0.5f, lateral), Vector2.right), Is.False);
        }

        [Test]
        public void Runway_OppositeHeading_IsRejected()
        {
            Assert.That(Runway(Vector2.zero, Vector2.left), Is.False);
        }

        [Test]
        public void Runway_PerpendicularHeading_IsRejected()
        {
            Assert.That(Runway(Vector2.zero, Vector2.up), Is.False);
            Assert.That(Runway(Vector2.zero, Vector2.down), Is.False);
        }

        [TestCase(59f, true)]
        [TestCase(-59f, true)]
        [TestCase(61f, false)]
        [TestCase(-61f, false)]
        public void Runway_HeadingAngleLimit_IsRespected(float headingDeg, bool expected)
        {
            Assert.That(Runway(Vector2.zero, Heading(headingDeg)), Is.EqualTo(expected));
        }

        [Test]
        public void Runway_SmallerMaxAngle_IsStricter()
        {
            Assert.That(Runway(Vector2.zero, Heading(30f), 45f), Is.True);
            Assert.That(Runway(Vector2.zero, Heading(30f), 20f), Is.False);
        }

        [Test]
        public void Runway_NonNormalizedVectors_AreHandled()
        {
            Assert.That(LandingMath.RunwayCaptures(Threshold, new Vector2(3f, 0f), Length, Width,
                new Vector2(0.2f, 0f), new Vector2(7f, 0.5f), MaxAngle), Is.True);
        }

        [Test]
        public void Runway_ZeroHeadingOrDirection_IsRejected()
        {
            Assert.That(Runway(Vector2.zero, Vector2.zero), Is.False);
            Assert.That(LandingMath.RunwayCaptures(Threshold, Vector2.zero, Length, Width,
                Vector2.zero, Vector2.right, MaxAngle), Is.False);
        }

        [Test]
        public void Runway_RotatedRunway_UsesItsOwnAxes()
        {
            // Threshold (2,1), landing direction +Y.
            Vector2 t = new Vector2(2f, 1f);
            Assert.That(LandingMath.RunwayCaptures(t, Vector2.up, Length, Width, new Vector2(2f, 1.5f), Vector2.up, MaxAngle), Is.True);
            Assert.That(LandingMath.RunwayCaptures(t, Vector2.up, Length, Width, new Vector2(2.5f, 1f), Vector2.up, MaxAngle), Is.True);
            Assert.That(LandingMath.RunwayCaptures(t, Vector2.up, Length, Width, new Vector2(3f, 1f), Vector2.up, MaxAngle), Is.False);
            Assert.That(LandingMath.RunwayCaptures(t, Vector2.up, Length, Width, new Vector2(2f, 1.5f), Vector2.right, MaxAngle), Is.False);
        }

        [Test]
        public void Helipad_InsideRadiusPlusPadding_Captures()
        {
            Vector2 c = new Vector2(1f, 1f);
            float r = 0.5f + LandingMath.HelipadCapturePadding; // diameter 1
            Assert.That(LandingMath.HelipadCaptures(c, 1f, c), Is.True);
            Assert.That(LandingMath.HelipadCaptures(c, 1f, c + new Vector2(r - 0.01f, 0f)), Is.True);
            Assert.That(LandingMath.HelipadCaptures(c, 1f, c + new Vector2(0f, -(r - 0.01f))), Is.True);
        }

        [Test]
        public void Helipad_OutsideRadiusPlusPadding_IsRejected()
        {
            Vector2 c = new Vector2(1f, 1f);
            float r = 0.5f + LandingMath.HelipadCapturePadding;
            Assert.That(LandingMath.HelipadCaptures(c, 1f, c + new Vector2(r + 0.01f, 0f)), Is.False);
            Assert.That(LandingMath.HelipadCaptures(c, 1f, c + new Vector2(r, r)), Is.False);
        }

        [Test]
        public void Captures_RunwayDef_UsesThresholdAndAngle()
        {
            LandingSpotDef def = new LandingSpotDef
            {
                kind = LandingKind.ShortRunway, position = new Vector2(2f, 0f), angleDeg = 180f, length = 3.5f, width = 0.6f,
            };
            Assert.That(LandingMath.Captures(def, new Vector2(1.9f, 0f), Vector2.left, MaxAngle), Is.True);
            Assert.That(LandingMath.Captures(def, new Vector2(1.9f, 0f), Vector2.right, MaxAngle), Is.False);
            Assert.That(LandingMath.Captures(def, new Vector2(3f, 0f), Vector2.left, MaxAngle), Is.False, "Behind the approach end.");
        }

        [Test]
        public void Captures_WaterLaneDef_IsDirectional()
        {
            LandingSpotDef def = new LandingSpotDef
            {
                kind = LandingKind.WaterLane, position = Vector2.zero, angleDeg = 90f, length = 4.2f, width = 0.8f,
            };
            Assert.That(LandingMath.Captures(def, new Vector2(0f, 0.3f), Vector2.up, MaxAngle), Is.True);
            Assert.That(LandingMath.Captures(def, new Vector2(0f, 0.3f), Vector2.down, MaxAngle), Is.False);
        }

        [Test]
        public void Captures_HelipadDef_AcceptsAnyHeading()
        {
            LandingSpotDef def = new LandingSpotDef { kind = LandingKind.Helipad, position = new Vector2(-3f, 2f), width = 1f };
            Assert.That(LandingMath.Captures(def, new Vector2(-3.2f, 2.1f), Vector2.left, MaxAngle), Is.True);
            Assert.That(LandingMath.Captures(def, new Vector2(-3.2f, 2.1f), Vector2.down, MaxAngle), Is.True);
            Assert.That(LandingMath.Captures(def, new Vector2(-3.2f, 2.1f), Vector2.zero, MaxAngle), Is.True);
            Assert.That(LandingMath.Captures(def, new Vector2(-1f, 2f), Vector2.left, MaxAngle), Is.False);
        }

        [Test]
        public void Captures_NullDef_IsFalse()
        {
            Assert.That(LandingMath.Captures(null, Vector2.zero, Vector2.right, MaxAngle), Is.False);
        }

        [Test]
        public void Accepts_OnlyMatchingKinds()
        {
            foreach (LandingKind spot in System.Enum.GetValues(typeof(LandingKind)))
            {
                foreach (LandingKind aircraft in System.Enum.GetValues(typeof(LandingKind)))
                {
                    Assert.That(LandingMath.Accepts(spot, aircraft), Is.EqualTo(spot == aircraft), spot + " vs " + aircraft);
                }
            }
        }
    }
}
