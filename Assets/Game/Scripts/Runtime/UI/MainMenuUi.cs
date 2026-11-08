using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// Main menu canvas built in code: logo + title + subtitle, one card per map (preview, name, tagline, difficulty
    /// stars, best score), and a bottom bar (How to play, sound, fullscreen on WebGL / desktop, quit on desktop).
    /// Cards sit in one row (shrinking to fit) or a 2-row grid on narrow / portrait screens and re-layout on resize.
    /// A phone held upright gets the <see cref="RotateDeviceOverlay"/> on top. Esc / Android back closes How to play;
    /// otherwise Android back leaves the app (Unity delivers back as Escape and never quits on its own).
    /// </summary>
    public sealed class MainMenuUi : MonoBehaviour
    {
        private const float CardWidth = 400f;
        private const float CardHeight = 500f;
        private const float CardGap = 36f;
        private const float SideMargin = 60f;
        private const float TopMargin = 28f;
        private const float TitleHeight = 272f;
        private const float TitleGap = 26f;
        private const float BarHeight = 120f;
        private const float BarMargin = 28f;
        private const float PreviewInset = 16f;
        private const float PreviewHeight = 207f; // (CardWidth - 2 * inset) * 9 / 16
        private const string RotateMessage = "Clear to Land plays in landscape. Turn your phone sideways to choose a map.";

        private RectTransform _safe;
        private RectTransform _title;
        private RectTransform _cardsRoot;
        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private UiFader _howTo;
        private UiFader _curtain;
        private Vector2 _lastSafeSize = new Vector2(-1f, -1f);
        private bool _loading;
        private bool _loadIssued;

        /// <summary>
        /// Builds the main menu canvas (sorting order 0). With a null <paramref name="config"/> a visible error panel is shown.
        /// </summary>
        public static MainMenuUi Create(GameConfig config)
        {
            UiTheme.Use(config);
            Canvas canvas = UiFactory.CreateCanvas("MainMenuUi", 0);
            MainMenuUi ui = canvas.gameObject.AddComponent<MainMenuUi>();
            ui.Build(config);
            return ui;
        }

        // ================================================================== build

        private void Build(GameConfig config)
        {
            RectTransform root = (RectTransform)transform;

            UiFactory.CreateEdgeFade(root, "TopFade", true, 440f, 0.62f);
            UiFactory.CreateEdgeFade(root, "BottomFade", false, 320f, 0.66f);

            _safe = UiFactory.Stretch(UiFactory.CreateRect("Safe", root));
            _safe.gameObject.AddComponent<SafeAreaFitter>();

            if (config == null)
            {
                BuildMessage(_safe, "GameConfig is not assigned.\nIn the Unity Editor run  Tools > Clear to Land > Rebuild Everything.",
                    Vector2.zero);
                _curtain = UiFactory.CreateCurtain(root, true);
                _curtain.Hide();
                RotateDeviceOverlay.Create(root, RotateMessage);
                return;
            }

            BuildTitle();

            _cardsRoot = UiFactory.Stretch(UiFactory.CreateRect("Cards", _safe));
            if (config.maps != null)
            {
                for (int i = 0; i < config.maps.Count; i++)
                {
                    MapDefinition map = config.maps[i];
                    if (map != null) _cards.Add(BuildCard(_cardsRoot, map));
                }
            }
            if (_cards.Count == 0)
            {
                BuildMessage(_safe, "No maps found in the GameConfig.\nRun  Tools > Clear to Land > Rebuild Everything.",
                    new Vector2(0f, -60f));
            }

            BuildBottomBar();

            _howTo = HowToPlayOverlay.Build(root, config);

            _curtain = UiFactory.CreateCurtain(root, true);
            _curtain.Duration = 0.3f;
            _curtain.Hide();

            RotateDeviceOverlay.Create(root, RotateMessage); // last: drawn over every other panel

            Relayout();
        }

        private void BuildTitle()
        {
            _title = UiFactory.CreateRect("Title", _safe);
            UiFactory.Place(_title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopMargin),
                new Vector2(1400f, TitleHeight));
            Vector2 top = new Vector2(0.5f, 1f);

            if (UiTheme.SoftCircleSprite != null)
            {
                Image glow = UiFactory.CreateImage(_title, "Glow", UiTheme.SoftCircleSprite, UiTheme.WithAlpha(UiTheme.AccentColor, 0.22f));
                UiFactory.Place(glow.rectTransform, top, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(280f, 280f));
            }

            if (UiTheme.LogoSprite != null)
            {
                Image logo = UiFactory.CreateImage(_title, "Logo", UiTheme.LogoSprite, Color.white, false, true);
                UiFactory.Place(logo.rectTransform, top, top, Vector2.zero, new Vector2(124f, 124f));
            }

            TextMeshProUGUI title = UiFactory.CreateText(_title, "Name", GameConstants.GameTitle, UiTheme.FontDisplay,
                UiTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold, UiTheme.SoftShadowTextMaterial);
            title.characterSpacing = 2f;
            UiFactory.Place(title.rectTransform, top, top, new Vector2(0f, -124f), new Vector2(1400f, 110f));

            TextMeshProUGUI subtitle = UiFactory.CreateText(_title, "Subtitle",
                "Draw flight paths. Land by color. Keep the skies clear.", UiTheme.FontBody, UiTheme.TextSecondary,
                TextAlignmentOptions.Center, FontStyles.Normal, UiTheme.SoftShadowTextMaterial);
            UiFactory.Place(subtitle.rectTransform, top, top, new Vector2(0f, -232f), new Vector2(1400f, 40f));
        }

        private RectTransform BuildCard(RectTransform parent, MapDefinition map)
        {
            RectTransform card = UiFactory.CreateRect("Card_" + map.id, parent);
            UiFactory.Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

            // Body carries the button + press scale; the layout scales the outer card, so the two never fight.
            RectTransform body = UiFactory.Stretch(UiFactory.CreateRect("Body", card));
            UiFactory.AddSoftShadow(body, 26f, new Vector2(0f, -12f), 0.55f);
            Image bg = UiFactory.CreateImage(body, "Background", UiTheme.CardSprite, UiTheme.CardColor, true);
            UiFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            Button button = body.gameObject.AddComponent<Button>();
            UiFactory.StyleButton(button, bg);
            MapDefinition captured = map;
            button.onClick.AddListener(() => OnMapChosen(captured));

            // Preview: rounded mask showing the central (safe) 75% of the map background.
            RectTransform frame = UiFactory.CreateRect("Preview", body);
            UiFactory.PlaceTopBand(frame, PreviewInset, PreviewInset, PreviewInset, PreviewHeight);
            Image frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.sprite = UiTheme.ButtonSprite;
            frameImg.type = UiTheme.ButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frameImg.pixelsPerUnitMultiplier = 2f;
            frameImg.color = UiTheme.Ink;
            frameImg.raycastTarget = false;
            Mask mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            float frameWidth = CardWidth - PreviewInset * 2f;
            Image preview = UiFactory.CreateImage(frame, "Map", map.background,
                map.background != null ? Color.white : map.cameraColor, false, map.background != null);
            UiFactory.Place(preview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(frameWidth / 0.75f, PreviewHeight / 0.75f));

            // Play badge in the preview's bottom-right corner.
            RectTransform play = UiFactory.CreateRect("PlayBadge", body);
            const float badge = 76f;
            UiFactory.Place(play, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-PreviewInset - 12f - badge * 0.5f, -(PreviewInset + PreviewHeight - 12f - badge * 0.5f)),
                new Vector2(badge, badge));
            UiFactory.AddRoundShadow(play, badge * 1.5f, new Vector2(0f, -5f), 0.55f);
            Image disc = UiFactory.CreateImage(play, "Disc", UiTheme.CircleSprite, UiTheme.AccentColor, false, true);
            UiFactory.Stretch(disc.rectTransform);
            if (UiTheme.IconPlay != null)
            {
                Image icon = UiFactory.CreateImage(play, "Icon", UiTheme.IconPlay, UiTheme.Ink, false, true);
                UiFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 0f),
                    new Vector2(38f, 38f));
            }
            else
            {
                TextMeshProUGUI go = UiFactory.CreateText(play, "Icon", ">", 40f, UiTheme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
                UiFactory.Stretch(go.rectTransform);
            }

            // Texts.
            float textTop = PreviewInset + PreviewHeight + 20f;
            TextMeshProUGUI name = UiFactory.CreateText(body, "Name", map.displayName, UiTheme.FontHeading, UiTheme.TextPrimary,
                TextAlignmentOptions.Left, FontStyles.Bold);
            name.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.PlaceTopBand(name.rectTransform, 24f, 24f, textTop, 50f);

            TextMeshProUGUI tagline = UiFactory.CreateText(body, "Tagline", map.tagline, 26f, UiTheme.TextSecondary,
                TextAlignmentOptions.TopLeft);
            tagline.textWrappingMode = TextWrappingModes.Normal;
            tagline.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.PlaceTopBand(tagline.rectTransform, 24f, 24f, textTop + 58f, 72f);

            Image divider = UiFactory.CreateDivider(body, "Divider");
            RectTransform drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(-48f, 2f);
            drt.anchoredPosition = new Vector2(0f, 92f);

            // Difficulty stars (filled = difficulty, dim up to 4).
            int difficulty = Mathf.Max(0, map.difficulty);
            int starCount = Mathf.Max(4, difficulty);
            Sprite star = UiTheme.IconStar != null ? UiTheme.IconStar : UiTheme.CircleSprite;
            float starSize = UiTheme.IconStar != null ? 34f : 22f;
            for (int i = 0; i < starCount; i++)
            {
                Image s = UiFactory.CreateImage(body, "Star", star, i < difficulty ? UiTheme.GoldColor : UiTheme.StarDimColor,
                    false, true);
                UiFactory.Place(s.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(24f + starSize * 0.5f + i * 40f, 46f),
                    new Vector2(starSize, starSize));
            }

            string muted = ColorUtility.ToHtmlStringRGB(UiTheme.TextMuted);
            string bestText = "<size=22><color=#" + muted + ">BEST</color></size>  " + HighScores.Get(map.id);
            TextMeshProUGUI best = UiFactory.CreateText(body, "Best", bestText, 34f, UiTheme.TextPrimary,
                TextAlignmentOptions.Right, FontStyles.Bold);
            UiFactory.Place(best.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-24f, 46f),
                new Vector2(220f, 48f));

            return card;
        }

        private void BuildBottomBar()
        {
            RectTransform bar = UiFactory.CreateRect("BottomBar", _safe);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(-BarMargin * 2f, BarHeight);
            bar.anchoredPosition = new Vector2(0f, BarMargin);

            HorizontalLayoutGroup h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 24f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            Color fill = UiTheme.PanelColor;

            UiButtonParts help = UiFactory.CreateButton(bar, "HowToPlay", "How to play", UiTheme.IconHelp, fill,
                UiTheme.TextPrimary, new Vector2(390f, BarHeight), OpenHowTo, true, true);
            SetLayoutSize(help.Rect, 390f, BarHeight);

            UiButtonParts sound = UiFactory.CreateRoundButton(bar, "Sound", UiTheme.IconSoundOn, "On", BarHeight, BarHeight,
                fill, UiTheme.TextPrimary, null, false, true, true);
            SetLayoutSize(sound.Rect, BarHeight, BarHeight);
            SoundToggleButton toggle = sound.Rect.gameObject.AddComponent<SoundToggleButton>();
            toggle.Init(sound.Icon, sound.Label, "On", "Off");
            sound.Button.onClick.AddListener(toggle.Toggle);

            if (FullscreenToggleButton.IsSupported)
            {
                UiButtonParts fs = UiFactory.CreateButton(bar, "Fullscreen", "Fullscreen", null, fill, UiTheme.TextPrimary,
                    new Vector2(300f, BarHeight), null, false, true);
                SetLayoutSize(fs.Rect, 300f, BarHeight);
                fs.Rect.gameObject.AddComponent<FullscreenToggleButton>().Init(fs.Label);
            }

            if (IsDesktopPlayer)
            {
                UiButtonParts quit = UiFactory.CreateButton(bar, "Quit", "Quit", UiTheme.IconClose, fill, UiTheme.TextPrimary,
                    new Vector2(240f, BarHeight), Application.Quit, true, true);
                SetLayoutSize(quit.Rect, 240f, BarHeight);
            }
        }

        private static void SetLayoutSize(RectTransform rt, float width, float height)
        {
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
        }

        private static bool IsDesktopPlayer
        {
            get
            {
                RuntimePlatform p = Application.platform;
                return p == RuntimePlatform.WindowsPlayer || p == RuntimePlatform.OSXPlayer || p == RuntimePlatform.LinuxPlayer;
            }
        }

        private static void BuildMessage(RectTransform parent, string message, Vector2 offset)
        {
            RectTransform box = UiFactory.CreateRect("Message", parent);
            UiFactory.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, new Vector2(1240f, 260f));
            UiFactory.CreatePanel(box, UiTheme.PanelColor);
            TextMeshProUGUI text = UiFactory.CreateText(box, "Text", message, UiTheme.FontBody, UiTheme.DangerColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            text.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.Stretch(text.rectTransform, 48f, 32f, 48f, 32f);
        }

        // ================================================================== layout

        private void Relayout()
        {
            if (_safe == null) return;
            Vector2 size = _safe.rect.size;
            _lastSafeSize = size;
            if (_title == null || size.x <= 1f || size.y <= 1f) return;

            float bottomReserve = BarMargin + BarHeight + BarMargin;
            float contentTop = TopMargin;
            float contentBottom = size.y - bottomReserve;
            int n = _cards.Count;

            if (n == 0)
            {
                _title.anchoredPosition = new Vector2(0f, -contentTop);
                return;
            }

            float availW = Mathf.Max(1f, size.x - SideMargin * 2f);
            float availH = Mathf.Max(1f, contentBottom - contentTop - TitleHeight - TitleGap);

            // Option A: a single row.
            float rowScale = Mathf.Min((availW - (n - 1) * CardGap) / (n * CardWidth), availH / CardHeight, 1.18f);

            // Option B: two rows (only worth it on narrow / portrait screens).
            int cols = Mathf.CeilToInt(n / 2f);
            int rows = Mathf.CeilToInt(n / (float)cols);
            float gridScale = n > 1
                ? Mathf.Min((availW - (cols - 1) * CardGap) / (cols * CardWidth),
                    (availH - (rows - 1) * CardGap) / (rows * CardHeight), 1.6f)
                : 0f;

            bool grid = gridScale > rowScale * 1.08f;
            int c = grid ? cols : n;
            int r = grid ? rows : 1;
            float s = Mathf.Max(0.3f, grid ? gridScale : rowScale);

            float cw = CardWidth * s;
            float ch = CardHeight * s;
            float gridHeight = r * ch + (r - 1) * CardGap;
            float groupHeight = TitleHeight + TitleGap + gridHeight;
            float top = contentTop + Mathf.Max(0f, (contentBottom - contentTop - groupHeight) * 0.5f);

            _title.anchoredPosition = new Vector2(0f, -top);
            float cardsTop = top + TitleHeight + TitleGap;

            for (int i = 0; i < n; i++)
            {
                int row = i / c;
                int col = i % c;
                int inRow = Mathf.Min(c, n - row * c);
                float rowWidth = inRow * cw + (inRow - 1) * CardGap;
                float x = -rowWidth * 0.5f + cw * 0.5f + col * (cw + CardGap);
                float y = -(cardsTop + row * (ch + CardGap) + ch * 0.5f);
                RectTransform card = _cards[i];
                card.anchoredPosition = new Vector2(x, y);
                card.localScale = new Vector3(s, s, 1f);
            }
        }

        // ================================================================== actions

        private void OpenHowTo()
        {
            if (_howTo != null && !_loading) _howTo.Show();
        }

        private void OnMapChosen(MapDefinition map)
        {
            if (_loading || map == null) return;
            _loading = true;
            UiFactory.PlayClick();
            GameSession.SelectedMap = map;
            if (_curtain != null) _curtain.Show();
        }

        private void LateUpdate()
        {
            if (_safe != null && _safe.rect.size != _lastSafeSize) Relayout();
        }

        private void Update()
        {
            if (_loading && !_loadIssued && (_curtain == null || _curtain.Progress >= 1f))
            {
                _loadIssued = true;
                SceneManager.LoadScene(GameConstants.GameScene);
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            if (_howTo != null && _howTo.IsVisible)
            {
                UiFactory.PlayClick();
                _howTo.Hide();
            }
            else if (!_loading && Application.platform == RuntimePlatform.Android)
            {
                // Android back (delivered as Escape) on the menu leaves the app, as Android users expect.
                Application.Quit();
            }
        }
    }
}
