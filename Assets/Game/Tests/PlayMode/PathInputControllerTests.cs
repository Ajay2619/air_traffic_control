using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClearToLand.Tests
{
    /// <summary>
    /// <see cref="PathInputController"/> driven through its fed-pointer API (device polling off): a tap or a held, still
    /// pointer must not steer the aircraft, a moving pointer still draws, and a drag onto a runway only locks when it
    /// comes in from the approach end.
    /// </summary>
    public sealed class PathInputControllerTests
    {
        private const int PointerId = 7;

        private TestWorld _world;
        private PathInputController _input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = TestWorld.Create();
            _input = new GameObject("PathInput").AddComponent<PathInputController>();
            _input.PollDevices = false; // ignore the real mouse / touchscreen; the test feeds the pointer
            _input.Init(_world.Manager);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _input = null; // its GameObject is destroyed with the rest of the test world
            if (_world != null) yield return _world.Destroy();
            _world = null;
        }

        private Vector2 ToScreen(Vector2 world)
        {
            Vector3 s = _world.Camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(s.x, s.y);
        }

        /// <summary>Same conversion as the controller (screen -> world on the z = 0 plane).</summary>
        private Vector2 ToWorld(Vector2 screen)
        {
            Camera cam = _world.Camera;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        private void AssumeScreenMapping(Vector2 world)
        {
            Vector2 back = ToWorld(ToScreen(world));
            Assume.That(float.IsNaN(back.x) || float.IsNaN(back.y), Is.False, "The camera has no usable screen mapping.");
            Assume.That((back - world).magnitude, Is.LessThan(1e-3f), "The camera has no usable screen mapping.");
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Tap_OnAircraft_AddsNoWaypointAndKeepsHeading()
        {
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-4f, 2f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            // Centre tap (used to turn the aircraft round) and off-centre tap within reach (used to veer it).
            Vector2[] offsets = { Vector2.zero, new Vector2(0f, 0.35f) };
            foreach (Vector2 offset in offsets)
            {
                Vector2 screen = ToScreen(a.Position + offset);
                Assert.That(_input.PointerDown(PointerId, screen), Is.True, "The press picks the aircraft.");
                Assert.That(a.IsBeingDrawn, Is.True);

                // The new path is just the aircraft's own position, consumed on its next update.
                yield return TestWorld.WaitFor(() => a.Path.IsEmpty, 3f);
                Assert.That(a.Path.IsEmpty, Is.True, "The press point under the aircraft is consumed at once.");
                yield return null;

                _input.PointerUp(PointerId, screen); // release where it was pressed
                Assert.That(a.IsBeingDrawn, Is.False);
                Assert.That(_input.ActiveDragCount, Is.EqualTo(0));
                Assert.That(a.Path.IsEmpty, Is.True, "A tap must not leave a waypoint for the aircraft to turn to (offset " + offset + ").");

                Vector2 p0 = a.Position;
                float t0 = Time.time;
                yield return TestWorld.WaitFor(() => Time.time - t0 >= 0.4f, 5f);
                Assert.That(Vector2.Distance(a.Heading, Vector2.right), Is.LessThan(1e-3f), "A tap keeps the heading (offset " + offset + ").");
                Assert.That(a.Position.y, Is.EqualTo(p0.y).Within(1e-3f));
                Assert.That(a.Position.x - p0.x, Is.GreaterThan(0.2f), "The aircraft keeps flying forward.");
            }
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator HeldStillPointer_LetsTheAircraftFlyOn()
        {
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-4f, -1f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            Vector2 press = a.Position;
            Vector2 screen = ToScreen(press);
            Assert.That(_input.PointerDown(PointerId, screen), Is.True);

            // Hold the pointer still for a second, feeding it every frame like a held mouse button / stationary touch.
            float t0 = Time.time;
            float realEnd = Time.realtimeSinceStartup + 10f;
            while (Time.time - t0 < 1f && Time.realtimeSinceStartup < realEnd)
            {
                _input.PointerMove(PointerId, screen);
                yield return null;
            }
            float held = Time.time - t0;

            Assert.That(a.IsBeingDrawn, Is.True, "The pointer is still down.");
            Assert.That(Vector2.Distance(a.Heading, Vector2.right), Is.LessThan(1e-3f), "A still pointer does not steer.");
            Assert.That(Mathf.Abs(a.Position.y - press.y), Is.LessThan(1e-3f));
            Assert.That(a.Position.x - press.x, Is.GreaterThan(0.9f * held * 0.6f),
                "The aircraft flies on instead of hovering at the pointer.");

            _input.PointerUp(PointerId, screen);
            Assert.That(a.IsBeingDrawn, Is.False);
            Assert.That(a.Path.IsEmpty, Is.True, "Releasing a still pointer adds no waypoint.");
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator MovingThePointer_AfterThePress_StillDraws()
        {
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-4f, 1f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            Assert.That(_input.PointerDown(PointerId, ToScreen(a.Position)), Is.True);
            yield return TestWorld.WaitFor(() => a.Path.IsEmpty, 3f);

            Vector2 target = a.Position + new Vector2(0.3f, -1.5f);
            _input.PointerMove(PointerId, ToScreen(target));
            Assert.That(a.Path.IsEmpty, Is.False, "Moving the pointer re-seeds the consumed path.");
            Assert.That(Vector2.Distance(a.Path.LastPoint, target), Is.LessThan(1e-3f));

            yield return TestWorld.WaitFor(() => a.Heading.y < -0.8f, 3f);
            Assert.That(a.Heading.y, Is.LessThan(-0.8f), "The aircraft turns toward the drawn point.");
            _input.PointerUp(PointerId, ToScreen(target));
            Assert.That(a.IsBeingDrawn, Is.False);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator AlignedDrag_OntoTheRunway_LocksAndLands()
        {
            GameManager gm = _world.Manager;
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-3f, 0f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            // Press and drag along the centreline toward the threshold at (0,0) in 0.2-unit moves (same frame).
            Vector2 start = a.Position;
            Assert.That(_input.PointerDown(PointerId, ToScreen(start)), Is.True);
            for (int k = 1; k <= 20 && _input.ActiveDragCount > 0; k++)
                _input.PointerMove(PointerId, ToScreen(start + new Vector2(0.2f * k, 0f)));

            Assert.That(a.Path.IsLanding, Is.True, "A path into the approach end locks onto the runway.");
            Assert.That(a.Path.IsLocked, Is.True);
            Assert.That(Vector2.Distance(a.Path.LastPoint, TestWorld.RunwayThreshold), Is.LessThan(1e-3f));
            Assert.That(a.IsBeingDrawn, Is.False, "Locking ends the drag.");
            Assert.That(_input.ActiveDragCount, Is.EqualTo(0));

            yield return TestWorld.WaitFor(() => gm.Score >= 1, 15f);
            Assert.That(gm.Score, Is.EqualTo(1), "The locked aircraft touches down and scores.");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator QuickDrag_FromJustShortOfTheThreshold_LocksStraightIn()
        {
            GameManager gm = _world.Manager;
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(-1.2f, 0f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            // Press, let the aircraft consume the press point, then flick onto the runway in one frame: the re-seeded
            // path's only point is past the threshold, but the aircraft is still short of it and lined up.
            Assert.That(_input.PointerDown(PointerId, ToScreen(a.Position)), Is.True);
            yield return TestWorld.WaitFor(() => a.Path.IsEmpty, 3f);
            Assume.That(a.Position.x, Is.LessThan(-0.2f), "The aircraft must still be short of the threshold (slow frames).");

            Vector2 onRunway = new Vector2(a.Position.x + 1.4f, 0f);
            _input.PointerMove(PointerId, ToScreen(onRunway));

            Assert.That(a.Path.IsLanding, Is.True, "A lined-up aircraft flicked onto the runway locks.");
            Assert.That(a.Path.Count, Is.EqualTo(1), "It flies straight to the touchdown point, not out and back.");
            Assert.That(Vector2.Distance(a.Path.LastPoint, TestWorld.RunwayThreshold), Is.LessThan(1e-3f));
            Assert.That(_input.ActiveDragCount, Is.EqualTo(0), "Locking ends the drag.");

            yield return TestWorld.WaitFor(() => gm.Score >= 1, 15f);
            Assert.That(gm.Score, Is.EqualTo(1), "The aircraft touches down and scores.");
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator SideCutDrag_OntoTheRunway_DoesNotLock()
        {
            Aircraft a = _world.Spawn(_world.Airliner, new Vector2(0.5f, 1.5f), Vector2.right);
            yield return null;
            AssumeScreenMapping(a.Position);

            LandingSpot runway = _world.MapRuntime.GetSpot(TestWorld.RunwayIndex);
            float maxAngle = _world.Config.landingMaxAngleDeg;
            Vector2 dir = new Vector2(1f, -1.5f).normalized; // about 56 degrees off the runway: inside the capture angle

            // The aircraft is ahead of the threshold, beside the runway; cut diagonally onto it (same frame).
            Vector2 start = a.Position;
            Assert.That(_input.PointerDown(PointerId, ToScreen(start)), Is.True);
            bool zoneReached = false;
            for (int k = 1; k <= 12; k++)
            {
                Vector2 p = start + dir * (0.2f * k);
                if (runway.Captures(p, dir, maxAngle)) zoneReached = true;
                _input.PointerMove(PointerId, ToScreen(p));
            }

            Assert.That(zoneReached, Is.True, "Precondition: the drawn points enter the runway's capture zone.");
            Assert.That(a.Path.IsLanding, Is.False, "A side cut must not lock: its final leg would point backwards.");
            Assert.That(a.Path.IsLocked, Is.False);
            Assert.That(a.IsBeingDrawn, Is.True, "Drawing goes on so the player can come round to the approach end.");

            _input.PointerUp(PointerId, ToScreen(start + dir * 2.4f));
            Assert.That(a.IsBeingDrawn, Is.False);
            Assert.That(a.Path.IsLanding, Is.False);
        }
    }
}
