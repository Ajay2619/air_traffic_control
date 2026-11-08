using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// Keeps a sound toggle's icon (iconSoundOn / iconSoundOff) and optional label in sync with
    /// <see cref="GameSettings.SoundEnabled"/>, including changes made elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundToggleButton : MonoBehaviour
    {
        private Image _icon;
        private TextMeshProUGUI _label;
        private string _onText;
        private string _offText;
        private bool _subscribed;

        /// <summary>Binds the visuals and subscribes to settings changes.</summary>
        public void Init(Image icon, TextMeshProUGUI label, string onText, string offText)
        {
            _icon = icon;
            _label = label;
            _onText = onText;
            _offText = offText;
            if (!_subscribed)
            {
                GameSettings.Changed += Refresh;
                _subscribed = true;
            }
            Refresh();
        }

        /// <summary>Flips the setting, then plays the click (audible only when sound was just turned on).</summary>
        public void Toggle()
        {
            GameSettings.SoundEnabled = !GameSettings.SoundEnabled;
            UiFactory.PlayClick();
        }

        /// <summary>Re-reads the setting and updates the visuals.</summary>
        public void Refresh()
        {
            bool on = GameSettings.SoundEnabled;
            if (_icon != null)
            {
                Sprite s = on ? UiTheme.IconSoundOn : UiTheme.IconSoundOff;
                if (s != null) _icon.sprite = s;
                Color c = _icon.color;
                c.a = on || UiTheme.IconSoundOff != null ? 1f : 0.45f;
                _icon.color = c;
            }
            if (_label != null) _label.text = on ? _onText : _offText;
        }

        private void OnDestroy()
        {
            if (_subscribed) GameSettings.Changed -= Refresh;
            _subscribed = false;
        }
    }
}
