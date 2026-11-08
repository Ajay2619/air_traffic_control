using TMPro;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>Animates a <see cref="ScorePopup"/>: pop-in, rise and fade on scaled time, then self-destroys.</summary>
    internal sealed class ScorePopupRunner : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private const float Rise = 0.6f;
        private const float PopDuration = 0.18f;
        private const float FadeStart = 0.45f;

        private TextMeshPro _text;
        private Vector3 _start;
        private Color _color;
        private float _age;
        private float _lastAlpha = -1f;

        /// <summary>Binds the text and its starting point.</summary>
        public void Init(TextMeshPro text, Vector2 start, Color color)
        {
            _text = text;
            _start = new Vector3(start.x, start.y, 0f);
            _color = color;
            Apply();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Apply();
            if (_age >= Duration) Destroy(gameObject);
        }

        private void Apply()
        {
            float t = Mathf.Clamp01(_age / Duration);
            transform.position = _start + new Vector3(0f, Rise * GameplayUtil.EaseOutCubic(t), 0f);

            float pop = Mathf.Lerp(0.6f, 1f, GameplayUtil.EaseOutBack(_age / PopDuration));
            transform.localScale = new Vector3(pop, pop, 1f);

            float alpha = t <= FadeStart ? 1f : 1f - (t - FadeStart) / (1f - FadeStart);
            alpha = Mathf.Clamp01(alpha) * _color.a;
            if (_text != null && Mathf.Abs(alpha - _lastAlpha) > 0.004f)
            {
                _lastAlpha = alpha;
                _text.color = GameplayUtil.WithAlpha(_color, alpha);
            }
        }
    }
}
