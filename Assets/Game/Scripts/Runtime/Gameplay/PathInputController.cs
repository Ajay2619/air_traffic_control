using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ClearToLand
{
    /// <summary>
    /// Turns pointer drags into flight paths. Every touch (EnhancedTouch) owns at most one drag, so several fingers can
    /// draw for different aircraft at once; the left mouse button is a single extra pointer (ignored while touches are
    /// active, and briefly after, to swallow browser-emulated mouse events on mobile WebGL).
    /// Runs before aircraft so new points are followed in the same frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PathInputController : MonoBehaviour
    {
        private const int MousePointerId = int.MinValue;
        private const float MouseSuppressAfterTouch = 0.6f;
        private const float DrawClampInset = 0.1f;
        private const float EndDirectionLookback = 0.5f;
        private const float DefaultPointSpacing = 0.12f;
        private const float CaptureStepLength = 0.25f;
        private const int MaxCaptureSteps = 64;

        private sealed class Drag
        {
            public int PointerId;
            public Aircraft Aircraft;
            public bool Active;
            public bool Seen;
        }

        private readonly List<Drag> _drags = new List<Drag>(12);
        private readonly Stack<Drag> _pool = new Stack<Drag>(12);
        private GameManager _gm;
        private float _lastTouchTime = -100f;
        private bool _enhancedTouchEnabled;

        /// <summary>When false, all drags end and no input is processed. Default true.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Number of pointers currently drawing a path.</summary>
        public int ActiveDragCount => _drags.Count;

        /// <summary>Binds the controller to the game.</summary>
        public void Init(GameManager gm)
        {
            _gm = gm;
        }

        private void OnEnable()
        {
            if (!_enhancedTouchEnabled)
            {
                EnhancedTouchSupport.Enable();
                _enhancedTouchEnabled = true;
            }
        }

        private void OnDisable()
        {
            EndAllDrags();
            if (_enhancedTouchEnabled)
            {
                EnhancedTouchSupport.Disable();
                _enhancedTouchEnabled = false;
            }
        }

        private void Update()
        {
            if (_gm == null || !InputEnabled || _gm.State != GameState.Playing)
            {
                if (_drags.Count > 0) EndAllDrags();
                return;
            }

            int touchCount = ProcessTouches();
            ProcessMouse(touchCount);
        }

        // ------------------------------------------------------------------ touches

        private int ProcessTouches()
        {
            if (!EnhancedTouchSupport.enabled) return 0;

            var touches = ETouch.activeTouches;
            int n = touches.Count;
            if (n > 0) _lastTouchTime = Time.unscaledTime;

            for (int i = 0; i < _drags.Count; i++)
                _drags[i].Seen = _drags[i].PointerId == MousePointerId;

            for (int i = 0; i < n; i++)
            {
                ETouch touch = touches[i];
                int id = touch.touchId;
                Vector2 screen = touch.screenPosition;
                InputTouchPhase phase = touch.phase;
                Drag drag = FindDrag(id);

                switch (phase)
                {
                    case InputTouchPhase.Began:
                        if (drag != null) EndDrag(drag);
                        drag = TryBeginDrag(id, screen);
                        if (drag != null) drag.Seen = true;
                        break;

                    case InputTouchPhase.Moved:
                    case InputTouchPhase.Stationary:
                        if (drag != null)
                        {
                            drag.Seen = true;
                            UpdateDrag(drag, screen);
                        }
                        break;

                    case InputTouchPhase.Ended:
                    case InputTouchPhase.Canceled:
                        if (drag != null)
                        {
                            drag.Seen = true;
                            if (phase == InputTouchPhase.Ended) UpdateDrag(drag, screen);
                            if (drag.Active) EndDrag(drag);
                        }
                        break;
                }
            }

            // Touch drags whose touch vanished without an end phase.
            for (int i = _drags.Count - 1; i >= 0; i--)
            {
                if (i >= _drags.Count) continue;
                Drag d = _drags[i];
                if (!d.Seen) EndDrag(d);
            }

            return n;
        }

        // ------------------------------------------------------------------ mouse

        private void ProcessMouse(int touchCount)
        {
            Drag drag = FindDrag(MousePointerId);
            Mouse mouse = Mouse.current;
            bool suppressed = mouse == null || touchCount > 0 || Time.unscaledTime - _lastTouchTime < MouseSuppressAfterTouch;
            if (suppressed)
            {
                if (drag != null) EndDrag(drag);
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (drag != null) EndDrag(drag);
                TryBeginDrag(MousePointerId, screen);
            }
            else if (drag != null)
            {
                UpdateDrag(drag, screen);
                if (drag.Active && !mouse.leftButton.isPressed) EndDrag(drag);
            }
        }

        // ------------------------------------------------------------------ drag logic

        private Drag FindDrag(int pointerId)
        {
            for (int i = 0; i < _drags.Count; i++)
            {
                if (_drags[i].PointerId == pointerId) return _drags[i];
            }
            return null;
        }

        private bool IsDrawnByAnyPointer(Aircraft a)
        {
            for (int i = 0; i < _drags.Count; i++)
            {
                if (_drags[i].Aircraft == a) return true;
            }
            return false;
        }

        private Drag TryBeginDrag(int pointerId, Vector2 screen)
        {
            if (UiHitTest.IsOverUi(screen)) return null; // pointer is ignored until it is released

            Camera cam = _gm.WorldCamera;
            if (cam == null) return null;
            Vector2 world = ScreenToWorld(cam, screen);

            GameConfig cfg = _gm.Config;
            float padding = cfg != null ? Mathf.Max(0f, cfg.pickPadding) : 0.45f;

            Aircraft best = null;
            float bestDist2 = float.MaxValue;
            IReadOnlyList<Aircraft> list = _gm.ActiveAircraft;
            for (int i = 0; i < list.Count; i++)
            {
                Aircraft a = list[i];
                if (a == null || !a.IsControllable || IsDrawnByAnyPointer(a)) continue;
                float reach = a.Radius + padding;
                float d2 = (a.Position - world).sqrMagnitude;
                if (d2 <= reach * reach && d2 < bestDist2)
                {
                    best = a;
                    bestDist2 = d2;
                }
            }
            if (best == null) return null;

            Drag drag = _pool.Count > 0 ? _pool.Pop() : new Drag();
            drag.PointerId = pointerId;
            drag.Aircraft = best;
            drag.Active = true;
            drag.Seen = true;
            _drags.Add(drag);

            best.BeginPath();
            best.IsBeingDrawn = true;
            if (_gm.MapRuntime != null) _gm.MapRuntime.SetCandidateHighlight(best.Type);
            AudioManager.Instance.Play(Sfx.Select);
            return drag;
        }

        private void UpdateDrag(Drag drag, Vector2 screen)
        {
            Aircraft a = drag.Aircraft;
            if (a == null || !a.IsControllable)
            {
                EndDrag(drag);
                return;
            }

            Camera cam = _gm.WorldCamera;
            if (cam == null) return;

            GameConfig cfg = _gm.Config;
            float spacing = cfg != null && cfg.pathPointSpacing > 0.005f ? cfg.pathPointSpacing : DefaultPointSpacing;
            float maxAngle = cfg != null ? cfg.landingMaxAngleDeg : 60f;

            Vector2 target = PlayBounds.ClampInside(_gm.ViewRect, ScreenToWorld(cam, screen), DrawClampInset);
            FlightPath path = a.Path;
            MapRuntime map = _gm.MapRuntime;

            // Long pointer jumps (fast flicks, low WebGL/mobile frame rates) are walked in short steps so a path that
            // passes through a capture zone within one frame still locks there instead of overshooting it.
            Vector2 from = path.IsEmpty ? target : path.LastPoint;
            float stepLength = Mathf.Max(spacing * 2f, CaptureStepLength);
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, target) / stepLength), 1, MaxCaptureSteps);
            for (int s = 1; s <= steps; s++)
            {
                Vector2 world = s == steps ? target : Vector2.Lerp(from, target, (float)s / steps);
                if (path.AddPoint(world, spacing) <= 0 || map == null) continue;

                Vector2 heading = path.EndDirection(EndDirectionLookback);
                if (heading.sqrMagnitude < 1e-8f)
                {
                    // Path was consumed down to a single point: use the direction from the aircraft instead.
                    Vector2 delta = world - a.Position;
                    if (delta.sqrMagnitude > 1e-8f) heading = delta.normalized;
                }

                LandingSpot spot = map.FindCapture(a.Type, world, heading, maxAngle);
                if (spot == null) continue;

                path.LockForLanding(spot.Index, spot.Touchdown, spot.Direction, spacing);
                spot.Flash();
                AudioManager.Instance.Play(Sfx.Lock);
                EndDrag(drag);
                return;
            }
        }

        private void EndDrag(Drag drag)
        {
            if (drag == null || !drag.Active) return;
            drag.Active = false;
            _drags.Remove(drag);

            Aircraft a = drag.Aircraft;
            drag.Aircraft = null;
            if (a != null) a.IsBeingDrawn = false;
            _pool.Push(drag);

            RefreshHighlight();
        }

        private void EndAllDrags()
        {
            while (_drags.Count > 0) EndDrag(_drags[_drags.Count - 1]);
        }

        private void RefreshHighlight()
        {
            if (_gm == null) return;
            MapRuntime map = _gm.MapRuntime;
            if (map == null) return;

            AircraftType type = null;
            for (int i = 0; i < _drags.Count; i++)
            {
                Aircraft other = _drags[i].Aircraft;
                if (other != null)
                {
                    type = other.Type;
                    break;
                }
            }
            map.SetCandidateHighlight(type);
        }

        private static Vector2 ScreenToWorld(Camera cam, Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(w.x, w.y);
        }
    }
}
