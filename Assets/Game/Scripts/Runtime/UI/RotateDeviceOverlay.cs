using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// Full-screen "Rotate your device" prompt for phones / tablets held upright. Mobile browsers ignore the
    /// landscape-only player setting (the orientation lock only works in fullscreen on Chrome for Android, and not at
    /// all on iOS), and in portrait both the menu and the playfield are far too small to use.
    /// Shown while <see cref="IsPortraitMobile"/>; it is the last sibling of its canvas (drawn over every other panel)
    /// and blocks input only while shown. Owners may call <see cref="Refresh"/> to react in the same frame
    /// (GameUi pauses the round while it is shown). Animates with unscaled time and allocates nothing per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotateDeviceOverlay : MonoBehaviour
    {
        private const string Title = "Rotate your device";

        // Content box in reference px; scaled up to fill a portrait screen so the text stays legible on phones.
        private const float ContentWidth = 1000f;
        private const float ContentHeight = 830f;
        private const float ContentPadding = 60f;
        private const float ContentMaxScale = 2.2f;
        private const float TitleSize = 80f;
        private const float MessageSize = 44f;

        private const float GraphicCenterFromTop = 250f;
        private const float ArcDiameter = 440f;
        private const float PhoneWidth = 180f;
        private const float PhoneHeight = 310f;

        // Animation cycle (unscaled seconds): hold upright, turn a quarter clockwise, hold, fade out, fade back in upright.
        private const float HoldUpright = 0.7f;
        private const float TurnTime = 0.8f;
        private const float FadeTime = 0.3f;
        private const float Cycle = 3.2f;

        private UiFader _fader;
        private GameObject _screen;
        private RectTransform _phone;
        private Image _arc;
        private CanvasGroup _graphicGroup;
        private bool _shown;
        private float _animTime;

        /// <summary>True on a mobile device (native or mobile browser) whose screen is taller than wide.</summary>
        public static bool IsPortraitMobile => Application.isMobilePlatform && Screen.height > Screen.width;

        /// <summary>True while the prompt is shown (target state; it may still be fading in).</summary>
        public bool IsShown => _shown;

        /// <summary>
        /// Builds the overlay as the last child of <paramref name="parent"/> (normally the canvas root) and shows it at
        /// once when the device is already upright. <paramref name="message"/> is the line under the title.
        /// </summary>
        public static RotateDeviceOverlay Create(Transform parent, string message)
        {
            RectTransform root = UiFactory.Stretch(UiFactory.CreateRect("RotateDevice", parent));
            RotateDeviceOverlay overlay = root.gameObject.AddComponent<RotateDeviceOverlay>();
            overlay.Build(message);
            return overlay;
        }

        /// <summary>Shows / hides the prompt for the current orientation and returns <see cref="IsShown"/>.</summary>
        public bool Refresh()
        {
            if (_fader == null) return false;
            bool portrait = IsPortraitMobile;
            if (portrait == _shown) return _shown;

            _shown = portrait;
            if (portrait)
            {
                transform.SetAsLastSibling(); // stay above anything added to the canvas later
                _animTime = 0f;
                _fader.Show();
            }
            else
            {
                _fader.Hide();
            }
            return _shown;
        }

        // ------------------------------------------------------------------ build

        private void Build(string message)
        {
            RectTransform root = (RectTransform)transform;
            Vector2 center = new Vector2(0.5f, 0.5f);

            // The controller root has no graphic; only this child blocks input, and only while shown.
            RectTransform screen = UiFactory.Stretch(UiFactory.CreateRect("Overlay", root));
            Image backdrop = screen.gameObject.AddComponent<Image>();
            backdrop.color = UiTheme.WithAlpha(UiTheme.Ink, 0.96f);
            backdrop.raycastTarget = true;
            screen.gameObject.AddComponent<CanvasGroup>();
            _screen = screen.gameObject;

            RectTransform safe = UiFactory.Stretch(UiFactory.CreateRect("Safe", screen));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform content = UiFactory.Place(UiFactory.CreateRect("Content", safe), center, center, Vector2.zero,
                new Vector2(ContentWidth, ContentHeight));
            UiFitInParent fit = content.gameObject.AddComponent<UiFitInParent>();
            fit.Padding = ContentPadding;
            fit.MaxScale = ContentMaxScale;
            // OnEnable already fitted with the defaults (MaxScale 1); LateUpdate only refits when a size changes, so
            // without this a phone that starts upright would keep the unenlarged (hard to read) content.
            fit.Refit();

            // Separate child for the fader's pop-in scale (UiFitInParent owns the content's scale).
            RectTransform body = UiFactory.Stretch(UiFactory.CreateRect("Body", content));
            BuildGraphic(body);

            TextMeshProUGUI title = UiFactory.CreateText(body, "Title", Title, TitleSize, UiTheme.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceTopBand(title.rectTransform, 0f, 0f, 520f, 100f);

            TextMeshProUGUI text = UiFactory.CreateText(body, "Message", message, MessageSize, UiTheme.TextSecondary,
                TextAlignmentOptions.Top);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.PlaceTopBand(text.rectTransform, 40f, 40f, 640f, 180f);

            _fader = screen.gameObject.AddComponent<UiFader>();
            _fader.Duration = 0.22f;
            _fader.ScaleTarget = body;

            root.SetAsLastSibling();
            _shown = IsPortraitMobile;
            if (_shown) _fader.Show(true);
            else _fader.Hide(true);
            Animate(0f);
        }

        private void BuildGraphic(RectTransform body)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);

            RectTransform graphic = UiFactory.CreateRect("Graphic", body);
            UiFactory.Place(graphic, new Vector2(0.5f, 1f), center, new Vector2(0f, -GraphicCenterFromTop),
                new Vector2(ArcDiameter, ArcDiameter));
            _graphicGroup = graphic.gameObject.AddComponent<CanvasGroup>();
            _graphicGroup.interactable = false;
            _graphicGroup.blocksRaycasts = false;

            // Faint full ring + an accent arc that sweeps a quarter turn clockwise with the phone.
            Sprite ring = UiTheme.RingSprite;
            if (ring != null)
            {
                Image track = UiFactory.CreateImage(graphic, "Track", ring, UiTheme.WithAlpha(UiTheme.TextPrimary, 0.1f), false, true);
                UiFactory.Stretch(track.rectTransform);

                _arc = UiFactory.CreateImage(graphic, "Arc", ring, UiTheme.AccentColor, false, true);
                UiFactory.Stretch(_arc.rectTransform);
                _arc.type = Image.Type.Filled;
                _arc.fillMethod = Image.FillMethod.Radial360;
                _arc.fillOrigin = (int)Image.Origin360.Top;
                _arc.fillClockwise = true;
                _arc.fillAmount = 0f;
            }

            _phone = UiFactory.CreateRect("Phone", graphic);
            UiFactory.Place(_phone, center, center, Vector2.zero, new Vector2(PhoneWidth, PhoneHeight));

            Image frame = UiFactory.CreateImage(_phone, "Frame", UiTheme.ButtonSprite, UiTheme.TextPrimary, true);
            UiFactory.Stretch(frame.rectTransform);
            frame.pixelsPerUnitMultiplier = 0.8f;

            Image display = UiFactory.CreateImage(_phone, "Display", UiTheme.ButtonSprite, UiTheme.ButtonColor, true);
            UiFactory.Stretch(display.rectTransform, 14f, 34f, 14f, 34f);
            display.pixelsPerUnitMultiplier = 1.6f;

            if (UiTheme.IconPlay != null)
            {
                Image play = UiFactory.CreateImage(display.rectTransform, "Play", UiTheme.IconPlay, UiTheme.AccentColor, false, true);
                UiFactory.Place(play.rectTransform, center, center, new Vector2(3f, 0f), new Vector2(64f, 64f));
            }
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            Refresh();
            if (_screen != null && _screen.activeSelf) Animate(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        private void Animate(float dt)
        {
            _animTime += dt;
            float t = Mathf.Repeat(_animTime, Cycle);
            float turn;
            float alpha = 1f;
            if (t < HoldUpright)
            {
                turn = 0f;
            }
            else if (t < HoldUpright + TurnTime)
            {
                turn = Smooth((t - HoldUpright) / TurnTime);
            }
            else if (t < Cycle - 2f * FadeTime)
            {
                turn = 1f;
            }
            else if (t < Cycle - FadeTime)
            {
                turn = 1f;
                alpha = 1f - Smooth((t - (Cycle - 2f * FadeTime)) / FadeTime);
            }
            else
            {
                turn = 0f;
                alpha = Smooth((t - (Cycle - FadeTime)) / FadeTime);
            }

            if (_phone != null) _phone.localEulerAngles = new Vector3(0f, 0f, -90f * turn);
            if (_arc != null) _arc.fillAmount = 0.25f * turn;
            if (_graphicGroup != null) _graphicGroup.alpha = alpha;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
