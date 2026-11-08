using System;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Fits its RectTransform (a full-screen child of a Screen Space Overlay canvas) to <see cref="Screen.safeArea"/>,
    /// so notches, rounded corners and home indicators never cover UI. Re-applies whenever the safe area, the screen
    /// size or the orientation changes (phone rotation, WebGL / desktop window resize).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        /// <summary>Raised after the anchors were updated for a new safe area.</summary>
        public event Action Changed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Screen.orientation != _lastOrientation
                || Screen.safeArea != _lastSafeArea)
            {
                Apply();
            }
        }

        /// <summary>Recomputes the anchors from the current safe area immediately.</summary>
        public void Apply()
        {
            if (_rect == null) _rect = (RectTransform)transform;

            int w = Screen.width;
            int h = Screen.height;
            Rect safe = Screen.safeArea;
            _lastWidth = w;
            _lastHeight = h;
            _lastSafeArea = safe;
            _lastOrientation = Screen.orientation;

            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.one;
            if (w > 0 && h > 0 && safe.width > 0f && safe.height > 0f)
            {
                min = new Vector2(Mathf.Clamp01(safe.xMin / w), Mathf.Clamp01(safe.yMin / h));
                max = new Vector2(Mathf.Clamp01(safe.xMax / w), Mathf.Clamp01(safe.yMax / h));
            }

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _rect.pivot = new Vector2(0.5f, 0.5f);
            Changed?.Invoke();
        }
    }
}
