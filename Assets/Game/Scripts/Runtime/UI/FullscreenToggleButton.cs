using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClearToLand
{
    /// <summary>
    /// Toggles fullscreen on WebGL (itch.io) and desktop players. The request is made on pointer DOWN: browsers only
    /// allow fullscreen inside a user-gesture handler, and Unity's WebGL runtime fulfils the request in the browser's
    /// following mouseup / touchend event, which makes a single click work reliably.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FullscreenToggleButton : MonoBehaviour, IPointerDownHandler
    {
        private TextMeshProUGUI _label;
        private bool _lastState;

        /// <summary>True on platforms where an in-game fullscreen toggle makes sense (WebGL, desktop players).</summary>
        public static bool IsSupported
        {
            get
            {
                RuntimePlatform p = Application.platform;
                return p == RuntimePlatform.WebGLPlayer || p == RuntimePlatform.WindowsPlayer
                       || p == RuntimePlatform.OSXPlayer || p == RuntimePlatform.LinuxPlayer;
            }
        }

        /// <summary>Binds the label that shows the current state.</summary>
        public void Init(TextMeshProUGUI label)
        {
            _label = label;
            _lastState = Screen.fullScreen;
            Refresh();
        }

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            bool full = !Screen.fullScreen;
            if (Application.platform == RuntimePlatform.WebGLPlayer) Screen.fullScreen = full;
            else SetDesktopFullscreen(full);
            UiFactory.PlayClick();
        }

        /// <summary>
        /// Desktop players: fullscreen at the monitor's native resolution (a plain Screen.fullScreen = true would keep the
        /// 1600x900 window resolution and upscale it), windowed back at 1600x900 clamped to the monitor.
        /// </summary>
        private static void SetDesktopFullscreen(bool full)
        {
            int sw = Display.main.systemWidth;
            int sh = Display.main.systemHeight;
            if (full)
            {
                if (sw > 0 && sh > 0) Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow);
                else Screen.fullScreen = true;
                return;
            }

            int w = 1600;
            int h = 900;
            if (sw > 0 && sh > 0)
            {
                float k = Mathf.Min(1f, sw * 0.85f / w, sh * 0.85f / h);
                w = Mathf.Max(640, Mathf.RoundToInt(w * k));
                h = Mathf.Max(360, Mathf.RoundToInt(h * k));
            }
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }

        private void Update()
        {
            bool fs = Screen.fullScreen;
            if (fs == _lastState) return;
            _lastState = fs;
            Refresh();
        }

        private void Refresh()
        {
            if (_label != null) _label.text = _lastState ? "Windowed" : "Fullscreen";
        }
    }
}
