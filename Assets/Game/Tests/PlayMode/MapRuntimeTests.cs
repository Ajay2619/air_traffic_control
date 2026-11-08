using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClearToLand.Tests
{
    /// <summary>MapRuntime / LandingSpot built from a MapDefinition: spot data, acceptance and capture lookup.</summary>
    public sealed class MapRuntimeTests
    {
        private TestWorld _world;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = TestWorld.Create();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_world != null) yield return _world.Destroy();
            _world = null;
        }

        private static void Near(Vector2 expected, Vector2 actual, string message)
        {
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(1e-4f),
                message + ": expected " + expected.ToString("F4") + " but was " + actual.ToString("F4"));
        }

        [UnityTest]
        public IEnumerator Build_CreatesOneSpotPerDefinition()
        {
            MapRuntime mr = _world.MapRuntime;
            yield return null;
            Assert.That(mr.Map, Is.SameAs(_world.Map));
            Assert.That(mr.Spots.Count, Is.EqualTo(2));
            Assert.That(mr.GetSpot(TestWorld.RunwayIndex), Is.SameAs(mr.Spots[TestWorld.RunwayIndex]));
            Assert.That(mr.GetSpot(2) == null, Is.True, "Out of range -> null");
            Assert.That(mr.GetSpot(-1) == null, Is.True, "Negative -> null");
        }

        [UnityTest]
        public IEnumerator RunwaySpot_ExposesThresholdDirectionAndRollout()
        {
            LandingSpot runway = _world.MapRuntime.GetSpot(TestWorld.RunwayIndex);
            yield return null;
            Assert.That(runway.Index, Is.EqualTo(TestWorld.RunwayIndex));
            Assert.That(runway.Kind, Is.EqualTo(LandingKind.LongRunway));
            Assert.That(runway.IsDirectional, Is.True);
            Assert.That(runway.Def, Is.Not.Null);
            Assert.That(runway.Def.length, Is.EqualTo(TestWorld.RunwayLength).Within(1e-4f));
            Near(TestWorld.RunwayThreshold, runway.Touchdown, "Touchdown");
            Near(Vector2.right, runway.Direction, "Direction");
            Near(new Vector2(TestWorld.RunwayLength * 0.8f, 0f), runway.RolloutEnd, "RolloutEnd");
        }

        [UnityTest]
        public IEnumerator HelipadSpot_HasNoDirection()
        {
            LandingSpot pad = _world.MapRuntime.GetSpot(TestWorld.HelipadIndex);
            yield return null;
            Assert.That(pad.Index, Is.EqualTo(TestWorld.HelipadIndex));
            Assert.That(pad.Kind, Is.EqualTo(LandingKind.Helipad));
            Assert.That(pad.IsDirectional, Is.False);
            Near(TestWorld.HelipadCenter, pad.Touchdown, "Touchdown");
            Near(Vector2.zero, pad.Direction, "Direction");
            Near(TestWorld.HelipadCenter, pad.RolloutEnd, "RolloutEnd");
        }

        [UnityTest]
        public IEnumerator Accepts_OnlyMatchingAircraftType()
        {
            LandingSpot runway = _world.MapRuntime.GetSpot(TestWorld.RunwayIndex);
            LandingSpot pad = _world.MapRuntime.GetSpot(TestWorld.HelipadIndex);
            yield return null;
            Assert.That(runway.Accepts(_world.Airliner), Is.True);
            Assert.That(runway.Accepts(_world.Prop), Is.False, "Props need a short runway.");
            Assert.That(runway.Accepts(_world.Helicopter), Is.False);
            Assert.That(pad.Accepts(_world.Helicopter), Is.True);
            Assert.That(pad.Accepts(_world.Airliner), Is.False);
        }

        [UnityTest]
        public IEnumerator FindCapture_RequiresMatchingTypeZoneAndHeading()
        {
            MapRuntime mr = _world.MapRuntime;
            float maxAngle = _world.Config.landingMaxAngleDeg;
            Vector2 onApproach = new Vector2(0.2f, 0f);
            yield return null;

            Assert.That(mr.FindCapture(_world.Airliner, onApproach, Vector2.right, maxAngle), Is.SameAs(mr.GetSpot(TestWorld.RunwayIndex)));
            Assert.That(mr.FindCapture(_world.Airliner, onApproach, Vector2.left, maxAngle) == null, Is.True, "Wrong end of the runway.");
            Assert.That(mr.FindCapture(_world.Airliner, new Vector2(4f, 0f), Vector2.right, maxAngle) == null, Is.True, "Too far down the runway.");
            Assert.That(mr.FindCapture(_world.Prop, onApproach, Vector2.right, maxAngle) == null, Is.True, "Wrong aircraft type.");
            Assert.That(mr.FindCapture(_world.Helicopter, TestWorld.HelipadCenter, Vector2.up, maxAngle), Is.SameAs(mr.GetSpot(TestWorld.HelipadIndex)));
            Assert.That(mr.FindCapture(_world.Airliner, TestWorld.HelipadCenter, Vector2.up, maxAngle) == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Highlighting_AndFlash_RunWithoutErrors()
        {
            MapRuntime mr = _world.MapRuntime;
            mr.SetCandidateHighlight(_world.Airliner);
            mr.GetSpot(TestWorld.RunwayIndex).Flash();
            mr.GetSpot(TestWorld.HelipadIndex).SetHighlight(SpotHighlight.Target);
            yield return TestWorld.WaitRealtime(0.2f);
            mr.SetCandidateHighlight(null);
            yield return null;
            Assert.That(mr.Spots.Count, Is.EqualTo(2));
        }
    }
}
