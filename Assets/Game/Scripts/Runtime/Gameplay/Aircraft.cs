using UnityEngine;

namespace ClearToLand
{
    /// <summary>Lifecycle of an aircraft.</summary>
    public enum AircraftState
    {
        Entering,
        Flying,
        Landing,
        Landed,
        Crashed,
    }

    /// <summary>
    /// One aircraft: flies its drawn <see cref="FlightPath"/> (or straight ahead), turns back smoothly at the screen
    /// edges, and plays the landing roll / helipad descent once its path ends on a landing spot.
    /// Create with <see cref="Spawn"/>.
    /// </summary>
    public sealed class Aircraft : MonoBehaviour
    {
        private const float VisualTurnRateDeg = 720f;
        private const float EdgeBandExtra = 0.15f;
        private const float BackstopInset = 0.05f;
        private const float RotorSpinDeg = 900f;
        private const float RotorLandedSpinDeg = 140f;
        private const float LandingFadeDuration = 0.35f;
        private const float HelipadDescentDuration = 1.0f;
        private const float RunwayLandedScale = 0.8f;
        private const float HelipadLandedScale = 0.75f;
        private const float RollEndSpeedFraction = 0.3f;
        private const float RollHeadingAlignTime = 0.25f;
        private const float RollTouchdownSettleTime = 0.35f;
        private const float ApproachDescentDistance = 1.8f;
        private const float ApproachMinAltitude = 0.3f;
        private const float ApproachMinScale = 0.94f;
        private const float AltitudeChangeRate = 1.2f;
        private const float ScaleChangeRate = 0.3f;
        private const float HelipadSlowdownDistance = 0.9f;
        private const float HelipadMinApproachSpeed = 0.45f;
        private const float WarningRingExtra = 0.4f;
        private const float SelectRingExtra = 0.3f;
        private const float SelectRingPopDuration = 0.15f;

        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color WarningRingColor = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color SelectRingColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color CrashedColor = new Color(0.23f, 0.23f, 0.25f, 1f);

        private static int s_nextId;

        private readonly FlightPath _path = new FlightPath();
        private GameManager _gm;
        private GameConfig _config;

        // Simulation
        private Vector2 _position;
        private Vector2 _heading = Vector2.right;
        private bool _edgeTurning;
        private Vector2 _edgeTarget;
        private bool _warning;
        private bool _isBeingDrawn;

        // Landing
        private LandingSpot _spot;
        private float _landTimer;
        private float _fadeTimer = -1f;
        private float _rollDistance;
        private float _rollLength;
        private float _rollSpeed;
        private float _rollEndSpeed;
        private float _rollDecel;
        private float _landTurnRate;
        private float _landStartScale = 1f;
        private float _landStartAltitude = 1f;
        private Vector2 _landStartPos;

        // Visual state
        private float _visualAngle;
        private float _altitude = 1f;
        private float _scale = 1f;
        private float _rotorAngle;
        private float _rotorSpeed = RotorSpinDeg;
        private float _alpha = 1f;
        private float _appliedAlpha = -1f;
        private Color _bodyColor = Color.white;
        private Color _rotorColor = Color.white;
        private float _z;
        private float _selectShownAt;

        private Transform _bodyT;
        private Transform _shadowT;
        private Transform _rotorT;
        private Transform _warnT;
        private Transform _selectT;
        private SpriteRenderer _bodyR;
        private SpriteRenderer _shadowR;
        private SpriteRenderer _rotorR;
        private SpriteRenderer _warnR;
        private SpriteRenderer _selectR;
        private float _warnBaseScale = 1f;
        private float _selectBaseScale = 1f;

        /// <summary>Unique, incrementing id.</summary>
        public int Id { get; private set; }

        /// <summary>The aircraft's type (never null after Spawn).</summary>
        public AircraftType Type { get; private set; }

        /// <summary>Current lifecycle state.</summary>
        public AircraftState State { get; private set; }

        /// <summary>The path being followed (consumed from the front).</summary>
        public FlightPath Path => _path;

        /// <summary>World position (XY).</summary>
        public Vector2 Position => _position;

        /// <summary>Unit direction of travel.</summary>
        public Vector2 Heading => _heading;

        /// <summary>Cruise speed: type.speed * map.speedMultiplier.</summary>
        public float Speed { get; private set; }

        /// <summary>Collision radius (type.radius).</summary>
        public float Radius { get; private set; }

        /// <summary>True while Entering or Flying.</summary>
        public bool IsAirborne => State == AircraftState.Entering || State == AircraftState.Flying;

        /// <summary>True when the GameManager's view rect contains <see cref="Position"/>.</summary>
        public bool IsVisible => _gm != null && _gm.ViewRect.Contains(_position);

        /// <summary>True when the player may draw a path for this aircraft.</summary>
        public bool IsControllable => IsAirborne && IsVisible;

        /// <summary>The spot this aircraft is landing on (set once State is Landing), else null.</summary>
        public LandingSpot LandingTarget => _spot;

        /// <summary>Set by PathInputController while a pointer draws this aircraft's path; shows the selection ring.</summary>
        public bool IsBeingDrawn
        {
            get => _isBeingDrawn;
            set
            {
                if (value && !IsAirborne) value = false;
                if (_isBeingDrawn == value) return;
                _isBeingDrawn = value;
                if (value) _selectShownAt = Time.unscaledTime;
            }
        }

        /// <summary>
        /// Creates an aircraft GameObject (body, shadow, rotor, rings, path line), initializes it and registers it with
        /// <paramref name="gm"/> (GameManager.Instance when null). Returns null if the type or manager is missing.
        /// </summary>
        public static Aircraft Spawn(AircraftType type, Vector2 position, Vector2 heading, GameManager gm)
        {
            if (gm == null) gm = GameManager.Instance;
            if (type == null || gm == null)
            {
                Debug.LogError("[ClearToLand] Aircraft.Spawn: " + (type == null ? "type is null" : "no GameManager"));
                return null;
            }

            int id = ++s_nextId;
            GameObject go = new GameObject("Aircraft_" + type.id + "_" + id);
            Aircraft a = go.AddComponent<Aircraft>();
            a.Init(id, type, position, heading, gm);
            gm.RegisterAircraft(a);
            return a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_nextId = 0;
        }

        /// <summary>Starts a fresh path at the current position (replaces any existing path, including a landing lock).</summary>
        public void BeginPath()
        {
            if (!IsAirborne) return;
            _path.Begin(_position);
            _edgeTurning = false;
            if (State == AircraftState.Entering && IsVisible) State = AircraftState.Flying;
        }

        /// <summary>Shows / hides the red pulsing proximity ring (ignored unless airborne).</summary>
        public void SetProximityWarning(bool on)
        {
            _warning = on && IsAirborne;
        }

        /// <summary>Mid-air collision: stops, clears the path, tints the body dark gray, stops the rotor, hides rings.</summary>
        public void Crash()
        {
            if (State == AircraftState.Crashed || State == AircraftState.Landed) return;
            State = AircraftState.Crashed;
            _path.Clear();
            _isBeingDrawn = false;
            _warning = false;
            _edgeTurning = false;
            _rotorSpeed = 0f;
            _alpha = 1f;
            _bodyColor = CrashedColor;
            _rotorColor = new Color(0.3f, 0.3f, 0.32f, 1f);
            _appliedAlpha = -1f; // force color refresh
            ApplyVisuals(0f);
        }

        private void Init(int id, AircraftType type, Vector2 position, Vector2 heading, GameManager gm)
        {
            Id = id;
            Type = type;
            _gm = gm;
            _config = gm.Config != null ? gm.Config : ScriptableObject.CreateInstance<GameConfig>();

            float mult = gm.Map != null ? gm.Map.speedMultiplier : 1f;
            Speed = Mathf.Max(0f, type.speed * mult);
            Radius = Mathf.Max(0f, type.radius);

            _position = position;
            _heading = heading.sqrMagnitude > 1e-8f ? heading.normalized : Vector2.right;
            _visualAngle = GameplayUtil.AngleDeg(_heading);
            _bodyColor = type.Color;
            _rotorColor = Color.Lerp(type.Color, Color.white, 0.55f);
            _z = -(id % 1000) * 0.0005f; // stable draw order between overlapping aircraft (newer on top)
            State = AircraftState.Entering;

            BuildVisuals();
            ApplyVisuals(0f);
        }

        private void BuildVisuals()
        {
            Transform root = transform;
            Sprite body = Type.bodySprite;
            Sprite ring = _config.ringSprite;

            _shadowR = GameplayUtil.CreateSprite(root, "Shadow", body, ShadowColor, SortOrder.Shadow);
            _shadowT = _shadowR.transform;

            _bodyR = GameplayUtil.CreateSprite(root, "Body", body, _bodyColor, SortOrder.Aircraft);
            _bodyT = _bodyR.transform;

            if (Type.rotorSprite != null)
            {
                _rotorR = GameplayUtil.CreateSprite(root, "Rotor", Type.rotorSprite, _rotorColor, SortOrder.AircraftRotor);
                _rotorT = _rotorR.transform;
                _rotorAngle = (Id * 37) % 360;
            }

            _warnR = GameplayUtil.CreateSprite(root, "WarningRing", ring, WarningRingColor, SortOrder.AircraftOverlay);
            _warnT = _warnR.transform;
            _warnBaseScale = GameplayUtil.ScaleForWidth(ring, Radius * 2f + WarningRingExtra);
            _warnT.localScale = new Vector3(_warnBaseScale, _warnBaseScale, 1f);
            _warnR.enabled = false;

            _selectR = GameplayUtil.CreateSprite(root, "SelectRing", ring, SelectRingColor, SortOrder.AircraftOverlay);
            _selectT = _selectR.transform;
            _selectBaseScale = GameplayUtil.ScaleForWidth(ring, Radius * 2f + SelectRingExtra);
            _selectT.localScale = new Vector3(_selectBaseScale, _selectBaseScale, 1f);
            _selectR.enabled = false;

            GameObject lineGo = new GameObject("PathLine");
            lineGo.transform.SetParent(root, false);
            PathLineRenderer line = lineGo.AddComponent<PathLineRenderer>();
            line.Init(this, _config.pathLineMaterial);
        }

        private void Update()
        {
            if (_gm == null) return;

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                switch (State)
                {
                    case AircraftState.Entering:
                    case AircraftState.Flying:
                        TickAirborne(dt);
                        break;
                    case AircraftState.Landing:
                        TickLanding(dt);
                        break;
                }
            }

            if (State != AircraftState.Landed) ApplyVisuals(dt);
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.UnregisterAircraft(this);
        }

        // ------------------------------------------------------------------ flight

        private void TickAirborne(float dt)
        {
            Rect view = _gm.ViewRect;
            Vector2 pos = _position;
            Vector2 heading = _heading;
            float dist = Speed * dt;

            float targetAltitude = 1f;
            float targetScale = 1f;

            if (!_path.IsEmpty)
            {
                _edgeTurning = false;
                LandingSpot target = _path.IsLanding ? GetPathTarget() : null;

                if (target != null)
                {
                    if (target.IsDirectional)
                    {
                        // Final approach: descend (shadow closes in, slight shrink) over the last stretch.
                        float rem = RemainingPathDistance(pos, ApproachDescentDistance);
                        float k = GameplayUtil.SmoothStep01(rem / ApproachDescentDistance);
                        targetAltitude = Mathf.Lerp(ApproachMinAltitude, 1f, k);
                        targetScale = Mathf.Lerp(ApproachMinScale, 1f, k);
                    }
                    else
                    {
                        // Helicopters slow down as they come over the pad.
                        float rem = RemainingPathDistance(pos, HelipadSlowdownDistance);
                        dist *= Mathf.Lerp(HelipadMinApproachSpeed, 1f, GameplayUtil.SmoothStep01(rem / HelipadSlowdownDistance));
                    }
                }

                float travelled = _path.Advance(ref pos, dist, ref heading);
                if (_path.IsEmpty)
                {
                    if (_path.IsLanding && target != null)
                    {
                        _position = pos;
                        _heading = heading;
                        BeginLanding(target);
                        return;
                    }

                    _path.Clear(); // drop stale landing flags (e.g. spot missing)
                    float rest = dist - travelled;
                    if (rest > 0f) pos += heading * rest;
                }
            }
            else
            {
                if (State == AircraftState.Flying) SteerFromEdges(ref heading, pos, view, dt);
                else SteerEntering(ref heading, pos, view, dt);
                pos += heading * dist;
            }

            if (State == AircraftState.Entering && PlayBounds.Inset(view, Radius).Contains(pos))
                State = AircraftState.Flying;

            // Hard backstop: a free-flying aircraft never leaves the view. (Not applied while following a path so a
            // path drawn before a window shrink can still be completed.)
            if (State == AircraftState.Flying && _path.IsEmpty)
                pos = PlayBounds.ClampInside(view, pos, BackstopInset);

            _position = pos;
            _heading = heading;

            _altitude = Mathf.MoveTowards(_altitude, targetAltitude, AltitudeChangeRate * dt);
            _scale = Mathf.MoveTowards(_scale, targetScale, ScaleChangeRate * dt);
        }

        private LandingSpot GetPathTarget()
        {
            MapRuntime map = _gm.MapRuntime;
            return map != null ? map.GetSpot(_path.LandingSpotIndex) : null;
        }

        /// <summary>Distance left along the path from <paramref name="pos"/>, stopping early once it exceeds <paramref name="cap"/>.</summary>
        private float RemainingPathDistance(Vector2 pos, float cap)
        {
            var points = _path.Points;
            int n = points.Count;
            if (n == 0) return 0f;
            float acc = Vector2.Distance(pos, points[0]);
            for (int i = 1; i < n && acc < cap; i++) acc += Vector2.Distance(points[i - 1], points[i]);
            return Mathf.Min(acc, cap);
        }

        private void SteerFromEdges(ref Vector2 heading, Vector2 pos, Rect view, float dt)
        {
            Rect inner = PlayBounds.Inset(view, Radius + EdgeBandExtra);

            // Reflect the outward components of the current goal heading (corner-safe: both axes can flip).
            Vector2 goal = _edgeTurning ? _edgeTarget : heading;
            bool reflected = false;
            if (pos.x > inner.xMax && goal.x > 0f) { goal.x = -goal.x; reflected = true; }
            else if (pos.x < inner.xMin && goal.x < 0f) { goal.x = -goal.x; reflected = true; }
            if (pos.y > inner.yMax && goal.y > 0f) { goal.y = -goal.y; reflected = true; }
            else if (pos.y < inner.yMin && goal.y < 0f) { goal.y = -goal.y; reflected = true; }

            if (reflected && goal.sqrMagnitude > 1e-8f)
            {
                _edgeTarget = goal.normalized;
                _edgeTurning = true;
            }

            if (!_edgeTurning) return;

            float current = GameplayUtil.AngleDeg(heading);
            float delta = Mathf.DeltaAngle(current, GameplayUtil.AngleDeg(_edgeTarget));
            if (Mathf.Abs(delta) > 179f)
            {
                // Head-on reflection: turn toward the side the view center is on.
                Vector2 toCenter = view.center - pos;
                float cross = heading.x * toCenter.y - heading.y * toCenter.x;
                delta = cross >= 0f ? 180f : -180f;
            }

            // Configured rate, raised just enough for fast aircraft so the turn radius fits inside the edge band
            // (otherwise they would scrape along the hard backstop).
            float band = Mathf.Max(0.1f, Radius + EdgeBandExtra - BackstopInset);
            float fitRate = Speed / band * Mathf.Rad2Deg * 1.1f;
            float step = Mathf.Max(Mathf.Max(1f, _config.edgeTurnRateDeg), fitRate) * dt;
            if (Mathf.Abs(delta) <= step)
            {
                heading = _edgeTarget;
                _edgeTurning = false;
            }
            else
            {
                heading = GameplayUtil.FromAngleDeg(current + Mathf.Sign(delta) * step);
            }
        }

        private void SteerEntering(ref Vector2 heading, Vector2 pos, Rect view, float dt)
        {
            // Failsafe only: spawns already head inward. If something points an entering aircraft away from the
            // view, bend it back toward the center.
            Vector2 toCenter = view.center - pos;
            if (toCenter.sqrMagnitude < 1e-6f) return;
            Vector2 dir = toCenter.normalized;
            if (Vector2.Dot(heading, dir) < 0.2f)
                heading = GameplayUtil.RotateTowards(heading, dir, Mathf.Max(1f, _config.edgeTurnRateDeg) * dt);
        }

        // ------------------------------------------------------------------ landing

        private void BeginLanding(LandingSpot spot)
        {
            State = AircraftState.Landing;
            _spot = spot;
            _path.Clear();
            _edgeTurning = false;
            _isBeingDrawn = false;
            _warning = false;

            _landTimer = 0f;
            _fadeTimer = -1f;
            _landStartScale = _scale;
            _landStartAltitude = _altitude;
            _landStartPos = _position;

            if (spot.IsDirectional)
            {
                _position = spot.Touchdown;
                _rollLength = Vector2.Distance(spot.Touchdown, spot.RolloutEnd);
                _rollSpeed = Mathf.Max(Speed, 0.2f);
                _rollEndSpeed = _rollSpeed * RollEndSpeedFraction;
                _rollDistance = 0f;
                _rollDecel = _rollLength > 1e-3f
                    ? (_rollSpeed * _rollSpeed - _rollEndSpeed * _rollEndSpeed) / (2f * _rollLength)
                    : 0f;
                float turn = Vector2.Angle(_heading, spot.Direction);
                _landTurnRate = Mathf.Max(60f, turn / RollHeadingAlignTime);
            }

            _bodyR.sortingOrder = SortOrder.LandingAircraft;
            if (_rotorR != null) _rotorR.sortingOrder = SortOrder.LandingAircraft + 1;

            _gm.NotifyTouchdown(this);
        }

        private void TickLanding(float dt)
        {
            _landTimer += dt;

            if (_spot == null)
            {
                if (_fadeTimer < 0f) _fadeTimer = 0f;
            }
            else if (_spot.IsDirectional)
            {
                Vector2 dir = _spot.Direction;
                if (_fadeTimer < 0f)
                {
                    _rollSpeed = Mathf.Max(_rollEndSpeed, _rollSpeed - _rollDecel * dt);
                    _rollDistance += _rollSpeed * dt;
                    if (_rollDistance >= _rollLength) _fadeTimer = 0f;
                }
                else
                {
                    _rollDistance += _rollEndSpeed * dt; // keep creeping while fading out
                }

                _position = _spot.Touchdown + dir * _rollDistance;
                _heading = GameplayUtil.RotateTowards(_heading, dir, _landTurnRate * dt);

                float p = _rollLength > 1e-3f ? Mathf.Clamp01(_rollDistance / _rollLength) : 1f;
                _scale = Mathf.Lerp(_landStartScale, RunwayLandedScale, GameplayUtil.SmoothStep01(p));
                _altitude = Mathf.Lerp(_landStartAltitude, 0f, GameplayUtil.SmoothStep01(_landTimer / RollTouchdownSettleTime));
            }
            else
            {
                float p = Mathf.Clamp01(_landTimer / HelipadDescentDuration);
                float e = GameplayUtil.SmoothStep01(p);
                _position = Vector2.Lerp(_landStartPos, _spot.Touchdown, e);
                _scale = Mathf.Lerp(_landStartScale, HelipadLandedScale, e);
                _altitude = Mathf.Lerp(_landStartAltitude, 0f, e);
                _rotorSpeed = Mathf.Lerp(RotorSpinDeg, RotorLandedSpinDeg, e);
                if (p >= 1f && _fadeTimer < 0f) _fadeTimer = 0f;
            }

            if (_fadeTimer >= 0f)
            {
                _fadeTimer += dt;
                _alpha = 1f - Mathf.Clamp01(_fadeTimer / LandingFadeDuration);
                if (_fadeTimer >= LandingFadeDuration) FinishLanding();
            }
        }

        private void FinishLanding()
        {
            State = AircraftState.Landed;
            _alpha = 0f;
            ApplyVisuals(0f);
            if (_gm != null) _gm.UnregisterAircraft(this);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ visuals

        private void ApplyVisuals(float dt)
        {
            if (_bodyT == null) return;

            if (State != AircraftState.Crashed)
            {
                float target = GameplayUtil.AngleDeg(_heading);
                _visualAngle = dt > 0f ? Mathf.MoveTowardsAngle(_visualAngle, target, VisualTurnRateDeg * dt) : _visualAngle;
            }

            transform.position = new Vector3(_position.x, _position.y, _z);

            Quaternion rot = Quaternion.Euler(0f, 0f, _visualAngle);
            Vector3 scale = new Vector3(_scale, _scale, 1f);
            _bodyT.localRotation = rot;
            _bodyT.localScale = scale;

            // Shadow offset is a world offset (root is never rotated) so the light direction stays fixed.
            Vector2 offset = _config.shadowOffset * _altitude;
            _shadowT.localPosition = new Vector3(offset.x, offset.y, 0f);
            _shadowT.localRotation = rot;
            _shadowT.localScale = scale;

            if (_rotorT != null)
            {
                _rotorAngle = Mathf.Repeat(_rotorAngle + _rotorSpeed * dt, 360f);
                _rotorT.localRotation = Quaternion.Euler(0f, 0f, _rotorAngle);
                _rotorT.localScale = scale;
            }

            if (!Mathf.Approximately(_appliedAlpha, _alpha))
            {
                _appliedAlpha = _alpha;
                _bodyR.color = GameplayUtil.WithAlpha(_bodyColor, _bodyColor.a * _alpha);
                _shadowR.color = GameplayUtil.WithAlpha(ShadowColor, ShadowColor.a * _alpha);
                if (_rotorR != null) _rotorR.color = GameplayUtil.WithAlpha(_rotorColor, _rotorColor.a * _alpha);
            }

            bool airborne = IsAirborne;

            bool showWarn = _warning && airborne;
            if (_warnR.enabled != showWarn) _warnR.enabled = showWarn;
            if (showWarn)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 2.5f);
                float s = _warnBaseScale * (1f + 0.08f * wave);
                _warnT.localScale = new Vector3(s, s, 1f);
                _warnR.color = GameplayUtil.WithAlpha(WarningRingColor, 0.6f + 0.4f * wave);
            }

            bool showSelect = _isBeingDrawn && airborne;
            if (_selectR.enabled != showSelect) _selectR.enabled = showSelect;
            if (showSelect)
            {
                float pop = 1f - GameplayUtil.EaseOutCubic((Time.unscaledTime - _selectShownAt) / SelectRingPopDuration);
                float breathe = 0.03f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.5f);
                float s = _selectBaseScale * (1f + 0.25f * pop + breathe);
                _selectT.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
