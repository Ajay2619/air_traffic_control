using System;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Keeps the orthographic camera sized so the whole safe area (GameConstants.SafeWidth x SafeHeight) is always
    /// visible, re-fitting whenever the screen/window size changes (WebGL/PC resize, device rotation).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class CameraFitter : MonoBehaviour
    {
        private Camera _cam;
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private float _lastAspect = -1f;

        /// <summary>World rect currently visible through the camera.</summary>
        public Rect ViewRect
        {
            get
            {
                Camera cam = Cam;
                return cam != null ? PlayBounds.ViewRect(cam) : default;
            }
        }

        /// <summary>Raised after every <see cref="Apply"/> (initial fit and every resize).</summary>
        public event Action ViewChanged;

        private Camera Cam
        {
            get
            {
                if (_cam == null) _cam = GetComponent<Camera>();
                return _cam;
            }
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (_lastWidth < 0) Apply();
        }

        /// <summary>Makes the camera orthographic, fits its size to the safe area and centers it at (0,0,-10).</summary>
        public void Apply()
        {
            Camera cam = Cam;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = PlayBounds.FitOrthographicSize(cam.aspect);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastAspect = cam.aspect;

            ViewChanged?.Invoke();
        }

        private void LateUpdate()
        {
            Camera cam = Cam;
            if (cam == null) return;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Mathf.Abs(cam.aspect - _lastAspect) > 1e-4f)
                Apply();
        }
    }
}
