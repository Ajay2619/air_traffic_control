using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Fades a CanvasGroup in/out (and optionally pops a child panel's scale) using unscaled time, so it animates
    /// while the game is paused (timeScale 0) or in crash slow motion. Hidden overlays are deactivated.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiFader : MonoBehaviour
    {
        private const float MaxStep = 0.05f;

        private CanvasGroup _group;
        private float _progress;
        private float _target;

        /// <summary>Optional transform that scales in with a slight overshoot while showing.</summary>
        public RectTransform ScaleTarget { get; set; }

        /// <summary>Seconds for a full fade.</summary>
        public float Duration { get; set; } = 0.28f;

        /// <summary>Deactivate the GameObject once fully hidden (default true).</summary>
        public bool DeactivateWhenHidden { get; set; } = true;

        /// <summary>Whether the group blocks raycasts while shown (false for purely informative banners).</summary>
        public bool BlocksRaycastsWhenShown { get; set; } = true;

        /// <summary>True when showing or shown (target state).</summary>
        public bool IsVisible => _target > 0.5f;

        /// <summary>Current animation progress, 0 = hidden, 1 = shown.</summary>
        public float Progress => _progress;

        private void Awake() => EnsureGroup();

        /// <summary>Activates the object and fades it in.</summary>
        public void Show(bool instant = false)
        {
            EnsureGroup();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _target = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = BlocksRaycastsWhenShown;
            if (instant) _progress = 1f;
            ApplyVisuals();
        }

        /// <summary>Fades out, then deactivates (when <see cref="DeactivateWhenHidden"/>).</summary>
        public void Hide(bool instant = false)
        {
            EnsureGroup();
            _target = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            if (instant || !gameObject.activeInHierarchy || _progress <= 0f)
            {
                _progress = 0f;
                ApplyVisuals();
                FinishHide();
            }
        }

        private void Update()
        {
            // Exact comparison on purpose: MoveTowards lands exactly on the target, while Approximately could stop a few
            // ulps short of it (Progress would then never reach 1 / FinishHide would never run).
            if (_progress == _target) return;

            float step = Mathf.Min(Time.unscaledDeltaTime, MaxStep) / Mathf.Max(0.01f, Duration);
            _progress = Mathf.MoveTowards(_progress, _target, step);
            ApplyVisuals();
            if (_target <= 0f && _progress <= 0f) FinishHide();
        }

        private void ApplyVisuals()
        {
            _group.alpha = _progress * _progress * (3f - 2f * _progress);
            if (ScaleTarget != null)
            {
                float s = _target > 0.5f
                    ? Mathf.LerpUnclamped(0.88f, 1f, UiTheme.EaseOutBack(_progress))
                    : Mathf.Lerp(0.95f, 1f, _progress);
                ScaleTarget.localScale = new Vector3(s, s, 1f);
            }
        }

        private void FinishHide()
        {
            if (ScaleTarget != null) ScaleTarget.localScale = Vector3.one;
            if (DeactivateWhenHidden && gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void EnsureGroup()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
        }
    }
}
