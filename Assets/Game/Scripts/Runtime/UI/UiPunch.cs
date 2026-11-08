using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// "Punch" scale effect: jumps to 1 + <see cref="Amount"/> and eases back to 1 using unscaled time.
    /// Idle (disabled) between punches, so it costs nothing per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPunch : MonoBehaviour
    {
        private float _t = 1f;

        /// <summary>Extra scale at the start of a punch (0.25 = 125%).</summary>
        public float Amount { get; set; } = 0.25f;

        /// <summary>Seconds to settle back to 1.</summary>
        public float Duration { get; set; } = 0.3f;

        private void Awake()
        {
            if (_t >= 1f) enabled = false;
        }

        /// <summary>Starts (or restarts) the punch.</summary>
        public void Play()
        {
            _t = 0f;
            enabled = true;
            Apply();
        }

        private void Update()
        {
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / Mathf.Max(0.01f, Duration);
            Apply();
            if (_t >= 1f) enabled = false;
        }

        private void Apply()
        {
            float k = 1f - Mathf.Clamp01(_t);
            float s = 1f + Amount * k * k;
            transform.localScale = new Vector3(s, s, 1f);
        }

        private void OnDisable()
        {
            _t = 1f;
            transform.localScale = Vector3.one;
        }
    }
}
