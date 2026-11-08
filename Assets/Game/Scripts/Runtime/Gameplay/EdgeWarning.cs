using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// The "!" marker shown at the screen edge (in the incoming aircraft's landing color) before an aircraft enters.
    /// Pulses with scaled time (freezes on pause) and emits a soft radar ping; dismisses itself once the tracked
    /// aircraft is fully inside the view.
    /// </summary>
    public sealed class EdgeWarning : MonoBehaviour
    {
        private const float IconWidth = 0.55f;
        private const float ClampInset = 0.45f;
        private const float PulseHz = 3f;
        private const float PulseAmount = 0.1f;
        private const float AppearDuration = 0.22f;
        private const float DismissDuration = 0.22f;
        private const float UntrackedGrace = 4f;
        private const float TrackedTimeout = 12f;
        private const float PingHz = 1.5f;
        private const float PingStartWidth = 0.5f;
        private const float PingEndWidth = 1.15f;
        private const float PingAlpha = 0.55f;

        private GameManager _gm;
        private SpriteRenderer _icon;
        private SpriteRenderer _ping;
        private Vector2 _edgePoint;
        private Vector2 _outwardNormal;
        private Color _color = Color.white;
        private float _iconScale = 1f;
        private Sprite _pingSprite;
        private float _leadTime = 2.2f;

        private Aircraft _tracked;
        private bool _isTracking;
        private bool _dismissing;
        private float _age;
        private float _trackAge;
        private float _appear;
        private float _dismiss;

        /// <summary>Outward normal of the edge this warning sits on.</summary>
        public Vector2 OutwardNormal => _outwardNormal;

        /// <summary>True once <see cref="Dismiss"/> was called (fading out).</summary>
        public bool IsDismissing => _dismissing;

        /// <summary>Creates a warning at <paramref name="edgePoint"/> (kept inside the view) tinted <paramref name="color"/>.</summary>
        public static EdgeWarning Show(Vector2 edgePoint, Vector2 outwardNormal, Color color, GameManager gm)
        {
            if (gm == null) gm = GameManager.Instance;
            GameObject go = new GameObject("EdgeWarning");
            EdgeWarning w = go.AddComponent<EdgeWarning>();
            w.Setup(edgePoint, outwardNormal, color, gm);
            return w;
        }

        /// <summary>Auto-dismiss once <paramref name="a"/> is fully inside the view, or when it is gone.</summary>
        public void TrackAircraft(Aircraft a)
        {
            if (a == null)
            {
                Dismiss();
                return;
            }
            _tracked = a;
            _isTracking = true;
            _trackAge = 0f;
        }

        /// <summary>Fades the warning out and destroys it.</summary>
        public void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            _dismiss = 0f;
        }

        private void Setup(Vector2 edgePoint, Vector2 outwardNormal, Color color, GameManager gm)
        {
            _gm = gm;
            _edgePoint = edgePoint;
            _outwardNormal = outwardNormal;
            _color = color;

            GameConfig cfg = gm != null ? gm.Config : null;
            Sprite iconSprite = cfg != null ? cfg.edgeWarningSprite : null;
            _pingSprite = cfg != null ? cfg.ringSprite : null;
            if (cfg != null) _leadTime = Mathf.Max(0f, cfg.spawnWarningLeadTime);

            _ping = GameplayUtil.CreateSprite(transform, "Ping", _pingSprite, GameplayUtil.WithAlpha(color, 0f), SortOrder.EdgeWarning - 1);
            _ping.enabled = false;

            _icon = GameplayUtil.CreateSprite(transform, "Icon", iconSprite, color, SortOrder.EdgeWarning);
            _iconScale = GameplayUtil.ScaleForWidth(iconSprite, IconWidth);
            _icon.transform.localScale = Vector3.zero;

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            Vector2 p = _edgePoint;
            if (_gm != null)
            {
                // Re-project onto the current edge so the marker stays on the border after a window resize.
                Rect view = _gm.ViewRect;
                p = PlayBounds.ClampInside(view, GameplayUtil.ProjectOntoEdge(view, _edgePoint, _outwardNormal), ClampInset);
            }
            transform.position = new Vector3(p.x, p.y, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            _age += dt;

            if (!_dismissing)
            {
                if (_isTracking)
                {
                    _trackAge += dt;
                    Aircraft a = _tracked;
                    if (a == null || !a.IsAirborne || _trackAge > TrackedTimeout)
                    {
                        Dismiss();
                    }
                    else if (_gm != null && PlayBounds.Inset(_gm.ViewRect, a.Radius).Contains(a.Position))
                    {
                        Dismiss();
                    }
                }
                else if (_gm == null || _age > _leadTime + UntrackedGrace)
                {
                    Dismiss(); // failsafe: nobody claimed this warning
                }
            }

            UpdatePosition();

            _appear = Mathf.Min(1f, _appear + udt / AppearDuration);
            float alpha = 1f;
            float extraScale = 1f;
            if (_dismissing)
            {
                _dismiss += udt;
                float k = Mathf.Clamp01(_dismiss / DismissDuration);
                alpha = 1f - k;
                extraScale = 1f + 0.3f * k;
                if (k >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            float t = Time.time;
            float pulse = 1f + PulseAmount * Mathf.Sin(t * Mathf.PI * 2f * PulseHz);
            float s = _iconScale * GameplayUtil.EaseOutBack(_appear) * pulse * extraScale;
            _icon.transform.localScale = new Vector3(s, s, 1f);
            _icon.color = GameplayUtil.WithAlpha(_color, alpha);

            if (_pingSprite != null)
            {
                float phase = Mathf.Repeat(t * PingHz, 1f);
                float width = Mathf.Lerp(PingStartWidth, PingEndWidth, GameplayUtil.EaseOutCubic(phase));
                float ps = GameplayUtil.ScaleForWidth(_pingSprite, width);
                float pa = PingAlpha * (1f - phase) * alpha * _appear;
                bool show = pa > 0.003f;
                if (_ping.enabled != show) _ping.enabled = show;
                if (show)
                {
                    _ping.transform.localScale = new Vector3(ps, ps, 1f);
                    _ping.color = GameplayUtil.WithAlpha(_color, pa);
                }
            }
        }
    }
}
