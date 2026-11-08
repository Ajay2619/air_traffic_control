using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClearToLand.Tests
{
    /// <summary>CollisionMonitor + GameManager crash flow: warnings, head-on crash, slow motion, game over.</summary>
    public sealed class CollisionTests
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
        [Timeout(30000)]
        public IEnumerator HeadOnAircraft_Collide_AndGameEnds()
        {
            GameManager gm = _world.Manager;
            CollisionMonitor monitor = _world.AddCollisionMonitor();
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-2f, 2.5f), Vector2.right);
            Aircraft b = _world.Spawn(_world.Airliner, new Vector2(2f, 2.5f), Vector2.left);

            bool sawWarning = false;
            float end = Time.realtimeSinceStartup + 10f;
            while (gm.State == GameState.Playing && Time.realtimeSinceStartup < end)
            {
                sawWarning |= monitor.AnyWarning;
                yield return null;
            }

            Assert.That(gm.State, Is.EqualTo(GameState.CrashSlowMo).Or.EqualTo(GameState.GameOver));
            Assert.That(sawWarning, Is.True, "The proximity warning should show before the collision.");
            Assert.That(a.State, Is.EqualTo(AircraftState.Crashed));
            Assert.That(b.State, Is.EqualTo(AircraftState.Crashed));
            Assert.That(a.IsAirborne, Is.False);
            Assert.That(Mathf.Abs(a.Position.x), Is.LessThan(1f), "They meet near the middle.");

            yield return TestWorld.WaitFor(() => gm.State == GameState.GameOver, 6f);
            Assert.That(gm.State, Is.EqualTo(GameState.GameOver));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TriggerCrash_UsesSlowMotion_ThenGameOverFreezesTime()
        {
            GameManager gm = _world.Manager;
            List<GameState> states = new List<GameState>();
            gm.StateChanged += s => states.Add(s);
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-4f, 1f), Vector2.right);
            Aircraft b = _world.Spawn(_world.Prop, new Vector2(4f, 1f), Vector2.left);

            gm.TriggerCrash(a, b);
            Assert.That(gm.State, Is.EqualTo(GameState.CrashSlowMo));
            Assert.That(a.State, Is.EqualTo(AircraftState.Crashed));
            Assert.That(b.State, Is.EqualTo(AircraftState.Crashed));
            Assert.That(a.Path.IsEmpty, Is.True);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(_world.Config.crashSlowMotionScale).Within(1e-4f));

            yield return TestWorld.WaitFor(() => gm.State == GameState.GameOver, _world.Config.crashSlowMotionDuration + 4f);
            Assert.That(gm.State, Is.EqualTo(GameState.GameOver));
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(states, Is.EqualTo(new[] { GameState.CrashSlowMo, GameState.GameOver }));

            // Game over is final: pause / fast-forward requests change nothing.
            gm.SetPaused(true);
            gm.SetFastForward(true);
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.GameOver));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator TriggerCrash_WhilePaused_IsIgnored()
        {
            GameManager gm = _world.Manager;
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-4f, 1f), Vector2.right);
            Aircraft b = _world.Spawn(_world.Prop, new Vector2(4f, 1f), Vector2.left);
            gm.SetPaused(true);
            gm.TriggerCrash(a, b);
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.Paused));
            Assert.That(a.State, Is.Not.EqualTo(AircraftState.Crashed));
            Assert.That(b.State, Is.Not.EqualTo(AircraftState.Crashed));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator SeparatedAircraft_NoWarningNoCrash()
        {
            GameManager gm = _world.Manager;
            CollisionMonitor monitor = _world.AddCollisionMonitor();
            _world.Spawn(_world.Airliner, new Vector2(-4f, 3f), Vector2.right);
            _world.Spawn(_world.Airliner, new Vector2(-4f, -2f), Vector2.right);

            bool sawWarning = false;
            float end = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < end)
            {
                sawWarning |= monitor.AnyWarning;
                yield return null;
            }
            Assert.That(sawWarning, Is.False);
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator CloseParallelAircraft_WarnWithoutCrashing()
        {
            // Sum of radii 0.72, warning up to 0.72 + 0.9 = 1.62; 1.2 apart = warning only.
            GameManager gm = _world.Manager;
            CollisionMonitor monitor = _world.AddCollisionMonitor();
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-3f, 1f), Vector2.right);
            Aircraft b = _world.Spawn(_world.Airliner, new Vector2(-3f, 2.2f), Vector2.right);

            yield return TestWorld.WaitFor(() => monitor.AnyWarning, 3f);
            Assert.That(monitor.AnyWarning, Is.True);
            yield return TestWorld.WaitRealtime(0.3f);
            Assert.That(monitor.AnyWarning, Is.True, "Still close: the warning persists.");
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
            Assert.That(a.State, Is.EqualTo(AircraftState.Flying));
            Assert.That(b.State, Is.EqualTo(AircraftState.Flying));
        }
    }
}
