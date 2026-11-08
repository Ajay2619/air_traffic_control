using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClearToLand.Tests
{
    /// <summary>End-to-end flight and landing: straight flight, runway touchdown via a locked path, helipad landing.</summary>
    public sealed class LandingFlowTests
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

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Aircraft_WithoutPath_FliesStraightAtTypeSpeed()
        {
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-3f, 2f), Vector2.right);
            Assert.That(a, Is.Not.Null);
            Assert.That(TestWorld.Contains(_world.Manager.ActiveAircraft, a), Is.True, "Spawn registers the aircraft.");
            Assert.That(a.Type, Is.SameAs(_world.Airliner));
            Assert.That(a.Speed, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(a.Radius, Is.EqualTo(0.36f).Within(1e-4f));
            Assert.That(a.Path.IsEmpty, Is.True);

            float t0 = Time.time;
            Vector2 p0 = a.Position;
            yield return TestWorld.WaitFor(() => Time.time - t0 >= 0.6f, 5f);
            float flown = Time.time - t0;

            Assert.That(a.State, Is.EqualTo(AircraftState.Flying));
            Assert.That(a.IsAirborne, Is.True);
            Assert.That(a.IsVisible, Is.True);
            Assert.That(a.IsControllable, Is.True);
            Assert.That(a.Position.y, Is.EqualTo(p0.y).Within(1e-3f), "No path: straight flight.");
            Assert.That(a.Position.x - p0.x, Is.EqualTo(0.9f * flown).Within(0.06f));
            Assert.That(Vector2.Distance(a.Heading, Vector2.right), Is.LessThan(1e-3f));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Airliner_OnPathLockedToRunway_TouchesDownAndScores()
        {
            GameManager gm = _world.Manager;
            int scoreEvents = 0, lastScore = -1;
            Aircraft landed = null;
            gm.ScoreChanged += s => { scoreEvents++; lastScore = s; };
            gm.AircraftLanded += x => landed = x;

            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-3f, 0f), Vector2.right);
            LandingSpot runway = _world.MapRuntime.GetSpot(TestWorld.RunwayIndex);

            // Draw like PathInputController: points up to a probe inside the capture zone, then lock on.
            a.BeginPath();
            Vector2 probe = new Vector2(0.2f, 0f);
            Assert.That(a.Path.AddPoint(probe, _world.Config.pathPointSpacing), Is.GreaterThan(0));
            Vector2 heading = a.Path.EndDirection(0.5f);
            LandingSpot captured = _world.MapRuntime.FindCapture(_world.Airliner, probe, heading, _world.Config.landingMaxAngleDeg);
            Assert.That(captured, Is.SameAs(runway), "The drawn path should reach the runway's capture zone.");
            a.Path.LockForLanding(runway.Index, runway.Touchdown, runway.Direction, _world.Config.pathPointSpacing);
            Assert.That(a.Path.IsLanding, Is.True);

            yield return TestWorld.WaitFor(() => gm.Score >= 1, 15f);

            Assert.That(gm.Score, Is.EqualTo(1), "Touchdown should score within the timeout.");
            Assert.That(scoreEvents, Is.EqualTo(1));
            Assert.That(lastScore, Is.EqualTo(1));
            Assert.That(landed, Is.SameAs(a));
            Assert.That(gm.BestScore, Is.EqualTo(1), "Best rises live once the score beats it.");
            Assert.That(gm.IsNewBest, Is.True);
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
            if (a != null)
            {
                Assert.That(a.State, Is.EqualTo(AircraftState.Landing).Or.EqualTo(AircraftState.Landed));
                Assert.That(a.IsAirborne, Is.False);
                Assert.That(a.Position.x, Is.InRange(-0.2f, TestWorld.RunwayLength), "Touchdown happens on the runway.");
                Assert.That(Mathf.Abs(a.Position.y), Is.LessThan(0.4f));
            }
        }

        [UnityTest]
        [Timeout(40000)]
        public IEnumerator Helicopter_OnPathLockedToHelipad_LandsAndIsRemoved()
        {
            GameManager gm = _world.Manager;
            Aircraft heli = _world.Spawn(_world.Helicopter, TestWorld.HelipadCenter + new Vector2(0f, 2f), Vector2.down);
            LandingSpot pad = _world.MapRuntime.GetSpot(TestWorld.HelipadIndex);

            heli.BeginPath();
            heli.Path.AddPoint(TestWorld.HelipadCenter, _world.Config.pathPointSpacing);
            LandingSpot captured = _world.MapRuntime.FindCapture(_world.Helicopter, TestWorld.HelipadCenter,
                heli.Path.EndDirection(0.5f), _world.Config.landingMaxAngleDeg);
            Assert.That(captured, Is.SameAs(pad));
            heli.Path.LockForLanding(pad.Index, pad.Touchdown, pad.Direction, _world.Config.pathPointSpacing);

            yield return TestWorld.WaitFor(() => gm.Score >= 1, 15f);
            Assert.That(gm.Score, Is.EqualTo(1), "Helipad touchdown should score.");

            yield return TestWorld.WaitFor(() => heli == null, 10f);
            Assert.That(heli == null, Is.True, "A landed aircraft fades out and is destroyed.");
            Assert.That(TestWorld.Contains(gm.ActiveAircraft, heli), Is.False, "...and unregistered.");
            Assert.That(gm.ActiveAircraft.Count, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator UnlockedPath_IsFollowedWithoutLanding()
        {
            GameManager gm = _world.Manager;
            Aircraft a = _world.Spawn(_world.Prop, new Vector2(-4f, 3f), Vector2.right);
            a.BeginPath();
            a.Path.AddPoint(new Vector2(-4f, 2.5f), _world.Config.pathPointSpacing);
            Assert.That(a.Path.IsLanding, Is.False);

            yield return TestWorld.WaitFor(() => a.Path.IsEmpty, 5f);
            Assert.That(a.Path.IsEmpty, Is.True, "The path is consumed.");
            Assert.That(Vector2.Distance(a.Heading, Vector2.down), Is.LessThan(0.05f), "Heading follows the path.");
            Assert.That(a.State, Is.EqualTo(AircraftState.Flying), "Without a locked landing the aircraft keeps flying.");
            Assert.That(gm.Score, Is.EqualTo(0));
        }
    }
}
