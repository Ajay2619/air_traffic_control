using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClearToLand.Tests
{
    /// <summary>GameManager lifecycle and time control: Init, pause, fast-forward, play-time clock, OnDestroy cleanup.</summary>
    public sealed class TimeControlTests
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
        public IEnumerator Init_ExposesWorldAndStartsPlaying()
        {
            GameManager gm = _world.Manager;
            yield return null;
            Assert.That(GameManager.Instance, Is.SameAs(gm));
            Assert.That(gm.Config, Is.SameAs(_world.Config));
            Assert.That(gm.Map, Is.SameAs(_world.Map));
            Assert.That(gm.MapRuntime, Is.SameAs(_world.MapRuntime));
            Assert.That(gm.WorldCamera, Is.SameAs(_world.Camera));
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
            Assert.That(gm.Score, Is.EqualTo(0));
            Assert.That(gm.BestScore, Is.EqualTo(0));
            Assert.That(gm.IsNewBest, Is.False);
            Assert.That(gm.IsFastForward, Is.False);
            Assert.That(gm.ActiveAircraft.Count, Is.EqualTo(0));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            Rect view = gm.ViewRect;
            Assert.That(view.width, Is.GreaterThanOrEqualTo(GameConstants.SafeWidth - 1e-3f));
            Assert.That(view.height, Is.GreaterThanOrEqualTo(GameConstants.SafeHeight - 1e-3f));
            Assert.That(view.center.magnitude, Is.LessThan(1e-3f));
        }

        [UnityTest]
        public IEnumerator SetPaused_StopsTime_AndResumeRestoresIt()
        {
            GameManager gm = _world.Manager;
            List<GameState> states = new List<GameState>();
            gm.StateChanged += s => states.Add(s);

            gm.SetPaused(true);
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            gm.SetPaused(false);
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(states, Is.EqualTo(new[] { GameState.Paused, GameState.Playing }));
        }

        [UnityTest]
        public IEnumerator TogglePause_AlternatesBetweenPlayingAndPaused()
        {
            GameManager gm = _world.Manager;
            gm.TogglePause();
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            gm.TogglePause();
            yield return null;
            Assert.That(gm.State, Is.EqualTo(GameState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SetFastForward_WhilePlaying_UsesFastForwardScale()
        {
            GameManager gm = _world.Manager;
            List<bool> ffEvents = new List<bool>();
            gm.FastForwardChanged += on => ffEvents.Add(on);

            gm.SetFastForward(true);
            yield return null;
            Assert.That(gm.IsFastForward, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Time.timeScale, Is.EqualTo(_world.Config.fastForwardScale).Within(1e-4f));

            gm.SetFastForward(false);
            yield return null;
            Assert.That(gm.IsFastForward, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(ffEvents, Is.EqualTo(new[] { true, false }));
        }

        [UnityTest]
        public IEnumerator ToggleFastForward_Flips()
        {
            GameManager gm = _world.Manager;
            gm.ToggleFastForward();
            yield return null;
            Assert.That(gm.IsFastForward, Is.True);
            gm.ToggleFastForward();
            yield return null;
            Assert.That(gm.IsFastForward, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Pause_DuringFastForward_ResumesAtFastForward()
        {
            GameManager gm = _world.Manager;
            gm.SetFastForward(true);
            gm.SetPaused(true);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            gm.SetPaused(false);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(_world.Config.fastForwardScale).Within(1e-4f));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator ElapsedPlayTime_AdvancesWhilePlaying_AndFreezesWhilePaused()
        {
            GameManager gm = _world.Manager;
            yield return TestWorld.WaitRealtime(0.25f);
            float playing = gm.ElapsedPlayTime;
            Assert.That(playing, Is.GreaterThan(0f));

            gm.SetPaused(true);
            yield return null;
            float frozen = gm.ElapsedPlayTime;
            yield return TestWorld.WaitRealtime(0.25f);
            Assert.That(gm.ElapsedPlayTime, Is.EqualTo(frozen).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator PausedWorld_DoesNotMoveAircraft()
        {
            GameManager gm = _world.Manager;
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-3f, 2f), Vector2.right);
            gm.SetPaused(true);
            yield return null;
            Vector2 p = a.Position;
            yield return TestWorld.WaitRealtime(0.2f);
            Assert.That(Vector2.Distance(a.Position, p), Is.LessThan(1e-5f));
        }

        [UnityTest]
        public IEnumerator DestroyingManager_ClearsInstance_AndRestoresTimeScale()
        {
            GameManager gm = _world.Manager;
            gm.SetPaused(true);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            Object.Destroy(gm.gameObject);
            yield return null;
            Assert.That(GameManager.Instance == null, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
