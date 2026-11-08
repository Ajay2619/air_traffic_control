using UnityEngine;

namespace ClearToLand
{
    /// <summary>How strongly a landing spot is highlighted.</summary>
    public enum SpotHighlight
    {
        None,
        Candidate,
        Target,
    }

    /// <summary>
    /// Runtime view of one <see cref="LandingSpotDef"/>: capture tests plus the glow / approach-chevron highlight
    /// shown while the player draws a path for an aircraft that can land here. The runway markings themselves are
    /// painted into the map background; this component only adds the dynamic overlay.
    /// </summary>
    public sealed class LandingSpot : MonoBehaviour
    {
        private const float RunwayGlowPadding = 0.5f;
        private const float PadGlowPadding = 0.6f;
        private const int ChevronCount = 3;
        private const float ChevronSpacing = 0.35f;
        private const float ChevronFirstOffset = 0.4f;
        private const float ChevronWidth = 0.28f;
        private const float CandidateAlphaMin = 0.22f;
        private const float CandidateAlphaMax = 0.45f;
        private const float TargetAlphaMin = 0.7f;
        private const float TargetAlphaMax = 0.85f;
        private const float FlashAlpha = 0.85f;
        private const float FlashDuration = 0.7f;
        private const float AlphaFadeRate = 3.5f;
        private const float ChevronFadeRate = 4f;

        private GameConfig _config;
        private Color _color = Color.white;
        private SpriteRenderer _glow;
        private Transform _glowTransform;
        private Vector3 _glowBaseScale = Vector3.one;
        private SpriteRenderer[] _chevrons;
        private SpotHighlight _level = SpotHighlight.None;
        private float _levelAlpha;
        private float _chevronAlpha;
        private float _flash;
        private Vector2 _touchdown;
        private Vector2 _direction;
        private Vector2 _rolloutEnd;

        /// <summary>Index of this spot in MapDefinition.landingSpots (and FlightPath.LandingSpotIndex).</summary>
        public int Index { get; private set; }

        /// <summary>The authored definition.</summary>
        public LandingSpotDef Def { get; private set; }

        /// <summary>Which aircraft kind may land here.</summary>
        public LandingKind Kind => Def != null ? Def.kind : LandingKind.Helipad;

        /// <summary>True for runways and water lanes (must be approached from the threshold end).</summary>
        public bool IsDirectional => LandingColors.IsDirectional(Kind);

        /// <summary>Touchdown point: runway threshold or helipad center (Def.position).</summary>
        public Vector2 Touchdown => _touchdown;

        /// <summary>Landing direction (unit) for directional kinds, Vector2.zero for helipads.</summary>
        public Vector2 Direction => _direction;

        /// <summary>Where the landing roll ends: Touchdown + Direction * length * 0.8 (helipad: Touchdown).</summary>
        public Vector2 RolloutEnd => _rolloutEnd;

        /// <summary>Current highlight level.</summary>
        public SpotHighlight Highlight => _level;

        /// <summary>Sets up geometry and highlight visuals. <paramref name="config"/> may be null (no visuals).</summary>
        public void Init(int index, LandingSpotDef def, GameConfig config)
        {
            Index = index;
            Def = def ?? new LandingSpotDef();
            _config = config;
            _color = LandingColors.For(Def.kind);

            _touchdown = Def.position;
            if (IsDirectional)
            {
                _direction = Def.Direction;
                _rolloutEnd = _touchdown + _direction * (Mathf.Max(0f, Def.length) * 0.8f);
            }
            else
            {
                _direction = Vector2.zero;
                _rolloutEnd = _touchdown;
            }

            Vector2 center = Def.Center;
            transform.position = new Vector3(center.x, center.y, 0f);
            BuildVisuals();
        }

        /// <summary>True when an aircraft of <paramref name="type"/> may land here.</summary>
        public bool Accepts(AircraftType type)
        {
            return type != null && LandingMath.Accepts(Kind, type.landingKind);
        }

        /// <summary>True when a path end at <paramref name="point"/> heading <paramref name="heading"/> captures this spot.</summary>
        public bool Captures(Vector2 point, Vector2 heading, float maxAngleDeg)
        {
            return LandingMath.Captures(Def, point, heading, maxAngleDeg);
        }

        /// <summary>Sets the steady highlight level (fades smoothly).</summary>
        public void SetHighlight(SpotHighlight level)
        {
            _level = level;
        }

        /// <summary>Brief bright pulse, used when a path locks onto this spot.</summary>
        public void Flash()
        {
            _flash = 1f;
            if (_glow != null && _glow.sprite != null) _glow.enabled = true;
        }

        private void BuildVisuals()
        {
            // Rebuild-safe: remove old children.
            for (int i = transform.childCount - 1; i >= 0; i--)
                GameplayUtil.DestroySafe(transform.GetChild(i).gameObject);
            _glow = null;
            _chevrons = null;

            Color transparent = GameplayUtil.WithAlpha(_color, 0f);
            float angle = Def.angleDeg;

            if (IsDirectional)
            {
                Sprite glowSprite = _config != null ? _config.spotGlowRunwaySprite : null;
                _glow = GameplayUtil.CreateSprite(transform, "Glow", glowSprite, transparent, SortOrder.SpotHighlight);
                if (glowSprite != null)
                {
                    _glow.drawMode = SpriteDrawMode.Sliced;
                    _glow.size = new Vector2(Mathf.Max(0.1f, Def.length) + RunwayGlowPadding, Mathf.Max(0.1f, Def.width) + RunwayGlowPadding);
                }
                _glow.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                _glowBaseScale = Vector3.one;

                Sprite chevronSprite = _config != null ? _config.chevronSprite : null;
                float chevronScale = GameplayUtil.ScaleForWidth(chevronSprite, ChevronWidth);
                _chevrons = new SpriteRenderer[ChevronCount];
                for (int k = 0; k < ChevronCount; k++)
                {
                    SpriteRenderer c = GameplayUtil.CreateSprite(transform, "Chevron" + k, chevronSprite, transparent, SortOrder.SpotHighlight);
                    Vector2 p = _touchdown - _direction * (ChevronFirstOffset + ChevronSpacing * k);
                    c.transform.position = new Vector3(p.x, p.y, 0f);
                    c.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    c.transform.localScale = new Vector3(chevronScale, chevronScale, 1f);
                    c.enabled = false;
                    _chevrons[k] = c;
                }
            }
            else
            {
                Sprite padSprite = _config != null ? _config.spotGlowPadSprite : null;
                _glow = GameplayUtil.CreateSprite(transform, "Glow", padSprite, transparent, SortOrder.SpotHighlight);
                float s = GameplayUtil.ScaleForWidth(padSprite, Mathf.Max(0.1f, Def.width) + PadGlowPadding);
                _glowBaseScale = new Vector3(s, s, 1f);
            }

            _glowTransform = _glow.transform;
            _glowTransform.localScale = _glowBaseScale;
            _glow.enabled = false;
            _levelAlpha = 0f;
            _chevronAlpha = 0f;
        }

        private void Update()
        {
            if (_glow == null) return;

            float dt = Time.unscaledDeltaTime;
            bool idle = _level == SpotHighlight.None && _flash <= 0f && _levelAlpha <= 0f && _chevronAlpha <= 0f;
            if (idle)
            {
                if (_glow.enabled) _glow.enabled = false;
                return;
            }

            float t = Time.unscaledTime;
            float target;
            switch (_level)
            {
                case SpotHighlight.Candidate:
                    target = Mathf.Lerp(CandidateAlphaMin, CandidateAlphaMax, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 1.1f));
                    break;
                case SpotHighlight.Target:
                    target = Mathf.Lerp(TargetAlphaMin, TargetAlphaMax, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 2f));
                    break;
                default:
                    target = 0f;
                    break;
            }
            _levelAlpha = Mathf.MoveTowards(_levelAlpha, target, dt * AlphaFadeRate);

            _flash = Mathf.Max(0f, _flash - dt / FlashDuration);
            float flashAlpha = FlashAlpha * GameplayUtil.SmoothStep01(_flash);
            float alpha = Mathf.Max(_levelAlpha, flashAlpha);

            bool show = alpha > 0.003f && _glow.sprite != null;
            if (_glow.enabled != show) _glow.enabled = show;
            if (show)
            {
                _glow.color = GameplayUtil.WithAlpha(_color, alpha);
                float pop = 1f + 0.08f * _flash;
                _glowTransform.localScale = new Vector3(_glowBaseScale.x * pop, _glowBaseScale.y * pop, 1f);
            }

            if (_chevrons != null)
            {
                float chevronTarget = _level == SpotHighlight.Candidate ? 1f : 0f;
                _chevronAlpha = Mathf.MoveTowards(_chevronAlpha, chevronTarget, dt * ChevronFadeRate);
                float wave = Mathf.Repeat(t * 1.4f, 1f);
                for (int k = 0; k < _chevrons.Length; k++)
                {
                    SpriteRenderer c = _chevrons[k];
                    if (c == null) continue;
                    // Approach-light chase: the farthest chevron lights first, running toward the threshold.
                    float phase = (ChevronCount - 1 - k) / (float)ChevronCount;
                    float d = Mathf.Abs(Mathf.Repeat(wave - phase + 0.5f, 1f) - 0.5f);
                    float chase = Mathf.Clamp01(1f - d * 3f);
                    float a = _chevronAlpha * (0.4f + 0.6f * chase);
                    bool visible = a > 0.003f && c.sprite != null;
                    if (c.enabled != visible) c.enabled = visible;
                    if (visible) c.color = GameplayUtil.WithAlpha(_color, a);
                }
            }
        }
    }
}
