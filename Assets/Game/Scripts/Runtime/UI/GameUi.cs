using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// In-game UI built in code: HUD (score, best, fast-forward, pause), first-play hint, pause overlay and game over
    /// overlay. Subscribes to <see cref="GameManager"/> events and unsubscribes in OnDestroy. All animation runs on
    /// unscaled time (the game sets timeScale 0 when paused / game over).
    /// Keyboard extras (desktop / WebGL): F = fast-forward, P = pause, Enter / R = retry on the game over screen.
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        private const float TutorialDuration = 8f;
        private const string TutorialText = "Drag from an aircraft to the spot with its color";
        private const int CanvasSortingOrder = 10;
        private const float BadgeY = -392f;            // game over panel: "New best!" badge center
        private const float BestLineY = -398f;         // best line center when there is no badge
        private const float BestLineBelowBadgeY = -458f; // best line center under the badge

        private GameManager _gm;
        private GameConfig _config;
        private bool _bound;

        // HUD
        private TextMeshProUGUI _scoreValue;
        private TextMeshProUGUI _bestValue;
        private UiPunch _scorePunch;
        private UiPunch _bestPunch;
        private UiFader _hudButtons;
        private UiButtonParts _ffButton;
        private UiFader _speedBadge;
        private int _shownScore = -1;
        private int _bestAtStart;
        private bool _bestHighlighted;

        // First-play hint
        private UiFader _tutorial;
        private bool _tutorialActive;
        private float _tutorialTime;

        // Pause
        private UiFader _pauseOverlay;
        private TextMeshProUGUI _pauseSubtitle;

        // Game over
        private UiFader _gameOverOverlay;
        private TextMeshProUGUI _goMap;
        private TextMeshProUGUI _goScore;
        private UiPunch _goScorePunch;
        private TextMeshProUGUI _goBest;
        private GameObject _goBadge;
        private UiPunch _goBadgePunch;
        private bool _gameOverShown;
        private bool _countingUp;
        private float _countTime;
        private float _countDuration;
        private int _finalScore;
        private int _lastCount;
        private bool _pendingNewBest;

        private UiFader _curtain;

        /// <summary>
        /// Builds the in-game UI canvas for <paramref name="gm"/> (sorting order 10) and binds it to the game's events.
        /// Returns null (and logs an error) when <paramref name="gm"/> is null.
        /// </summary>
        public static GameUi Create(GameManager gm, GameConfig config)
        {
            if (gm == null)
            {
                Debug.LogError("[ClearToLand] GameUi.Create: GameManager is null.");
                return null;
            }

            UiTheme.Use(config);
            Canvas canvas = UiFactory.CreateCanvas("GameUi", CanvasSortingOrder);
            GameUi ui = canvas.gameObject.AddComponent<GameUi>();
            ui.Build(gm, config);
            return ui;
        }

        // ================================================================== build

        private void Build(GameManager gm, GameConfig config)
        {
            _gm = gm;
            _config = config;
            _bestAtStart = gm.BestScore;

            RectTransform root = (RectTransform)transform;
            UiFactory.CreateEdgeFade(root, "TopFade", true, 170f, 0.42f);

            RectTransform safe = UiFactory.Stretch(UiFactory.CreateRect("Safe", root));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildScores(safe);
            BuildHudButtons(safe);
            BuildTutorial(safe);
            BuildPauseOverlay(root);
            BuildGameOverOverlay(root);

            _curtain = UiFactory.CreateCurtain(root, false);
            _curtain.Duration = 0.35f;
            _curtain.Hide();

            SetScore(gm.Score, false);
            RefreshFastForward(gm.IsFastForward, true);
            Subscribe();
            ApplyState(gm.State, true);

            if (!GameSettings.TutorialSeen && gm.State == GameState.Playing) StartTutorial();
        }

        private void BuildScores(RectTransform safe)
        {
            float m = UiTheme.ScreenMargin;
            Material hud = UiTheme.HudTextMaterial;
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 topRight = new Vector2(1f, 1f);

            TextMeshProUGUI scoreCaption = UiFactory.CreateCaption(safe, "ScoreCaption", "SCORE", UiTheme.TextSecondary,
                TextAlignmentOptions.Left, hud);
            UiFactory.Place(scoreCaption.rectTransform, topLeft, topLeft, new Vector2(m + 3f, -16f), new Vector2(320f, 30f));

            _scoreValue = UiFactory.CreateText(safe, "ScoreValue", "0", UiTheme.FontHudValue, UiTheme.TextPrimary,
                TextAlignmentOptions.Left, FontStyles.Bold, hud);
            UiFactory.Place(_scoreValue.rectTransform, topLeft, new Vector2(0f, 0.5f), new Vector2(m, -16f - 30f - 36f),
                new Vector2(380f, 74f));
            _scorePunch = _scoreValue.gameObject.AddComponent<UiPunch>();
            _scorePunch.Amount = 0.25f;
            _scorePunch.Duration = 0.32f;

            TextMeshProUGUI bestCaption = UiFactory.CreateCaption(safe, "BestCaption", "BEST", UiTheme.TextSecondary,
                TextAlignmentOptions.Right, hud);
            UiFactory.Place(bestCaption.rectTransform, topRight, topRight, new Vector2(-m - 3f, -16f), new Vector2(320f, 30f));

            _bestValue = UiFactory.CreateText(safe, "BestValue", "0", UiTheme.FontHudSecondary, UiTheme.TextPrimary,
                TextAlignmentOptions.Right, FontStyles.Bold, hud);
            UiFactory.Place(_bestValue.rectTransform, topRight, new Vector2(1f, 0.5f), new Vector2(-m, -16f - 30f - 28f),
                new Vector2(380f, 58f));
            _bestPunch = _bestValue.gameObject.AddComponent<UiPunch>();
            _bestPunch.Amount = 0.18f;
            _bestPunch.Duration = 0.3f;
        }

        private void BuildHudButtons(RectTransform safe)
        {
            float m = UiTheme.ScreenMargin;
            float size = UiTheme.HudButtonSize;

            RectTransform group = UiFactory.Stretch(UiFactory.CreateRect("HudButtons", safe));
            group.gameObject.AddComponent<CanvasGroup>();
            _hudButtons = group.gameObject.AddComponent<UiFader>();
            _hudButtons.Duration = 0.2f;

            _ffButton = UiFactory.CreateRoundButton(group, "FastForward", UiTheme.IconFastForward, ">>", size, size,
                UiTheme.HudButtonColor, UiTheme.TextPrimary, OnFastForwardClicked);
            UiFactory.Place(_ffButton.Rect, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(m + size * 0.5f, m + size * 0.5f), new Vector2(size, size));

            RectTransform badge = UiFactory.CreateRect("SpeedBadge", group);
            UiFactory.Place(badge, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(m + size + 14f, m + size * 0.5f),
                new Vector2(140f, 64f));
            badge.gameObject.AddComponent<CanvasGroup>();
            float ffScale = _config != null ? _config.fastForwardScale : 2f;
            string speedText = ffScale.ToString("0.#", CultureInfo.InvariantCulture) + "x";
            TextMeshProUGUI speed = UiFactory.CreateText(badge, "Label", speedText, 46f, UiTheme.AccentColor,
                TextAlignmentOptions.Left, FontStyles.Bold, UiTheme.HudTextMaterial);
            UiFactory.Stretch(speed.rectTransform);
            _speedBadge = badge.gameObject.AddComponent<UiFader>();
            _speedBadge.BlocksRaycastsWhenShown = false;
            _speedBadge.Duration = 0.16f;
            _speedBadge.ScaleTarget = badge;

            UiButtonParts pause = UiFactory.CreateRoundButton(group, "Pause", UiTheme.IconPause, "II", size, size,
                UiTheme.HudButtonColor, UiTheme.TextPrimary, OnPauseClicked);
            UiFactory.Place(pause.Rect, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-m - size * 0.5f, m + size * 0.5f), new Vector2(size, size));
        }

        private void BuildTutorial(RectTransform safe)
        {
            RectTransform banner = UiFactory.CreateRect("TutorialBanner", safe);
            banner.anchorMin = new Vector2(0.5f, 0f);
            banner.anchorMax = new Vector2(0.5f, 0f);
            banner.pivot = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(0f, UiTheme.ScreenMargin + 12f);
            banner.sizeDelta = new Vector2(900f, 84f);
            banner.gameObject.AddComponent<CanvasGroup>();

            Image shadow = UiFactory.AddSoftShadow(banner, 18f, new Vector2(0f, -6f), 0.45f);
            if (shadow != null) shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image bg = UiFactory.CreateImage(banner, "Background", UiTheme.ButtonSprite, UiTheme.WithAlpha(UiTheme.Ink, 0.86f), true);
            UiFactory.Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            HorizontalLayoutGroup h = banner.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(36, 44, 22, 22);
            h.spacing = 20f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            ContentSizeFitter fitter = banner.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Four landing-color dots: "match the colors".
            RectTransform dots = UiFactory.CreateRect("Dots", banner);
            HorizontalLayoutGroup dh = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dh.spacing = 7f;
            dh.childAlignment = TextAnchor.MiddleCenter;
            dh.childControlWidth = true;
            dh.childControlHeight = true;
            dh.childForceExpandWidth = false;
            dh.childForceExpandHeight = false;
            for (int i = 0; i < 4; i++)
            {
                Image dot = UiFactory.CreateImage(dots, "Dot", UiTheme.CircleSprite, LandingColors.For((LandingKind)i), false, true);
                LayoutElement le = dot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 20f;
                le.preferredHeight = 20f;
            }

            UiFactory.CreateText(banner, "Text", TutorialText, UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.Left);

            _tutorial = banner.gameObject.AddComponent<UiFader>();
            _tutorial.BlocksRaycastsWhenShown = false;
            _tutorial.Duration = 0.4f;
            _tutorial.Hide(true);
        }

        private void BuildPauseOverlay(RectTransform root)
        {
            const float panelW = 640f;
            const float panelH = 768f;
            _pauseOverlay = UiFactory.CreateOverlay(root, "PauseOverlay", new Vector2(panelW, panelH),
                out RectTransform panel, out Image _);

            TextMeshProUGUI title = UiFactory.CreateText(panel, "Title", "Paused", UiTheme.FontTitle, UiTheme.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceTopBand(title.rectTransform, 40f, 40f, 44f, 76f);

            _pauseSubtitle = UiFactory.CreateText(panel, "Subtitle", string.Empty, UiTheme.FontSmall, UiTheme.TextSecondary);
            _pauseSubtitle.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.PlaceTopBand(_pauseSubtitle.rectTransform, 40f, 40f, 124f, 36f);

            Vector2 size = new Vector2(520f, UiTheme.ButtonHeight);
            float y = 188f;
            const float gap = 16f;

            UiButtonParts resume = UiFactory.CreateButton(panel, "Resume", "Resume", UiTheme.IconPlay, UiTheme.AccentColor,
                UiTheme.Ink, size, OnResumeClicked);
            PlaceInColumn(resume.Rect, y, size);
            y += size.y + gap;

            UiButtonParts restart = UiFactory.CreateButton(panel, "Restart", "Restart", UiTheme.IconRestart, UiTheme.ButtonColor,
                UiTheme.TextPrimary, size, OnRestartClicked);
            PlaceInColumn(restart.Rect, y, size);
            y += size.y + gap;

            UiButtonParts menu = UiFactory.CreateButton(panel, "Menu", "Menu", UiTheme.IconHome, UiTheme.ButtonColor,
                UiTheme.TextPrimary, size, OnMenuClicked);
            PlaceInColumn(menu.Rect, y, size);
            y += size.y + gap;

            UiButtonParts sound = UiFactory.CreateButton(panel, "Sound", "Sound on", UiTheme.IconSoundOn, UiTheme.ButtonColor,
                UiTheme.TextPrimary, size, null, false);
            PlaceInColumn(sound.Rect, y, size);
            SoundToggleButton toggle = sound.Rect.gameObject.AddComponent<SoundToggleButton>();
            toggle.Init(sound.Icon, sound.Label, "Sound on", "Sound off");
            sound.Button.onClick.AddListener(toggle.Toggle);

            _pauseOverlay.Hide(true);
        }

        private void BuildGameOverOverlay(RectTransform root)
        {
            const float panelW = 760f;
            const float panelH = 680f;
            _gameOverOverlay = UiFactory.CreateOverlay(root, "GameOverOverlay", new Vector2(panelW, panelH),
                out RectTransform panel, out Image _);
            _gameOverOverlay.Duration = 0.38f;

            TextMeshProUGUI title = UiFactory.CreateText(panel, "Title", "Mid-air collision!", 58f, UiTheme.DangerColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceTopBand(title.rectTransform, 30f, 30f, 46f, 72f);

            _goMap = UiFactory.CreateText(panel, "Map", string.Empty, UiTheme.FontSmall, UiTheme.TextSecondary);
            _goMap.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.PlaceTopBand(_goMap.rectTransform, 30f, 30f, 122f, 36f);

            TextMeshProUGUI caption = UiFactory.CreateCaption(panel, "ScoreCaption", "SCORE", UiTheme.TextMuted);
            UiFactory.PlaceTopBand(caption.rectTransform, 30f, 30f, 184f, 30f);

            _goScore = UiFactory.CreateText(panel, "Score", "0", 132f, UiTheme.TextPrimary, TextAlignmentOptions.Center,
                FontStyles.Bold);
            UiFactory.Place(_goScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -284f),
                new Vector2(600f, 140f));
            _goScorePunch = _goScore.gameObject.AddComponent<UiPunch>();
            _goScorePunch.Amount = 0.12f;
            _goScorePunch.Duration = 0.3f;

            _goBest = UiFactory.CreateText(panel, "Best", string.Empty, 34f, UiTheme.TextSecondary, TextAlignmentOptions.Center,
                FontStyles.Bold);
            UiFactory.Place(_goBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, BestLineY),
                new Vector2(600f, 48f));

            RectTransform badge = UiFactory.CreateRect("NewBestBadge", panel);
            UiFactory.Place(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, BadgeY), new Vector2(300f, 66f));
            UiFactory.AddSoftShadow(badge, 16f, new Vector2(0f, -5f), 0.4f);
            Image badgeBg = UiFactory.CreateImage(badge, "Background", UiTheme.ButtonSprite, UiTheme.GoldColor, true);
            UiFactory.Stretch(badgeBg.rectTransform);
            TextMeshProUGUI badgeText = UiFactory.CreateText(badge, "Label", "New best!", 32f, UiTheme.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.UpperCase);
            badgeText.characterSpacing = 6f;
            UiFactory.Stretch(badgeText.rectTransform);
            _goBadge = badge.gameObject;
            _goBadgePunch = badge.gameObject.AddComponent<UiPunch>();
            _goBadgePunch.Amount = 0.35f;
            _goBadgePunch.Duration = 0.45f;

            Vector2 size = new Vector2(320f, UiTheme.ButtonHeight);
            UiButtonParts retry = UiFactory.CreateButton(panel, "Retry", "Retry", UiTheme.IconRestart, UiTheme.AccentColor,
                UiTheme.Ink, size, OnRestartClicked);
            UiFactory.Place(retry.Rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-size.x * 0.5f - 12f, 44f + size.y * 0.5f), size);

            UiButtonParts menu = UiFactory.CreateButton(panel, "Menu", "Menu", UiTheme.IconHome, UiTheme.ButtonColor,
                UiTheme.TextPrimary, size, OnMenuClicked);
            UiFactory.Place(menu.Rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(size.x * 0.5f + 12f, 44f + size.y * 0.5f), size);

            _goBest.gameObject.SetActive(false);
            _goBadge.SetActive(false);
            _gameOverOverlay.Hide(true);
        }

        private static void PlaceInColumn(RectTransform rt, float top, Vector2 size)
        {
            UiFactory.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -(top + size.y * 0.5f)), size);
        }

        // ================================================================== events

        private void Subscribe()
        {
            if (_bound) return;
            _gm.ScoreChanged += OnScoreChanged;
            _gm.StateChanged += OnStateChanged;
            _gm.FastForwardChanged += OnFastForwardChanged;
            _gm.AircraftLanded += OnAircraftLanded;
            _bound = true;
        }

        private void OnDestroy()
        {
            // ReferenceEquals: the GameManager may already be destroyed during scene unload; its C# events are still safe to touch.
            if (_bound && !ReferenceEquals(_gm, null))
            {
                _gm.ScoreChanged -= OnScoreChanged;
                _gm.StateChanged -= OnStateChanged;
                _gm.FastForwardChanged -= OnFastForwardChanged;
                _gm.AircraftLanded -= OnAircraftLanded;
            }
            _bound = false;
        }

        private void OnScoreChanged(int score) => SetScore(score, true);

        private void OnStateChanged(GameState state) => ApplyState(state, false);

        private void OnFastForwardChanged(bool on) => RefreshFastForward(on, false);

        private void OnAircraftLanded(Aircraft aircraft) => StopTutorial(true);

        // ================================================================== button actions

        private void OnFastForwardClicked()
        {
            if (_gm != null) _gm.ToggleFastForward();
        }

        private void OnPauseClicked()
        {
            if (_gm != null) _gm.TogglePause();
        }

        private void OnResumeClicked()
        {
            if (_gm != null) _gm.SetPaused(false);
        }

        private void OnRestartClicked()
        {
            if (_gm != null) _gm.Restart();
        }

        private void OnMenuClicked()
        {
            if (_gm != null) _gm.QuitToMenu();
        }

        // ================================================================== state

        private void ApplyState(GameState state, bool instant)
        {
            bool buttons = state == GameState.Playing || state == GameState.Paused;
            if (buttons) _hudButtons.Show(instant);
            else _hudButtons.Hide(instant);

            if (state == GameState.Paused)
            {
                RefreshPauseSubtitle();
                _pauseOverlay.Show(instant);
            }
            else
            {
                _pauseOverlay.Hide(instant);
            }

            if (state == GameState.CrashSlowMo || state == GameState.GameOver) StopTutorial(false);

            if (state == GameState.GameOver)
            {
                if (!_gameOverShown) ShowGameOver();
            }
        }

        private void SetScore(int score, bool animate)
        {
            if (score == _shownScore) return;
            bool increased = _shownScore >= 0 && score > _shownScore;
            _shownScore = score;
            _scoreValue.SetText("{0:0}", score);
            if (animate && increased) _scorePunch.Play();
            RefreshBest(animate && increased);
        }

        private void RefreshBest(bool animate)
        {
            int best = Mathf.Max(_gm.BestScore, _shownScore);
            _bestValue.SetText("{0:0}", best);

            bool isNew = _shownScore > 0 && (_gm.IsNewBest || _shownScore > _bestAtStart);
            if (isNew != _bestHighlighted)
            {
                _bestHighlighted = isNew;
                _bestValue.color = isNew ? UiTheme.GoldColor : UiTheme.TextPrimary;
            }
            if (animate && isNew) _bestPunch.Play();
        }

        private void RefreshFastForward(bool on, bool instant)
        {
            _ffButton.Background.color = on ? UiTheme.AccentColor : UiTheme.HudButtonColor;
            Color content = on ? UiTheme.Ink : UiTheme.TextPrimary;
            if (_ffButton.Icon != null) _ffButton.Icon.color = content;
            if (_ffButton.Label != null) _ffButton.Label.color = content;
            if (on) _speedBadge.Show(instant);
            else _speedBadge.Hide(instant);
        }

        private void RefreshPauseSubtitle()
        {
            string map = MapName;
            _pauseSubtitle.text = string.IsNullOrEmpty(map)
                ? "Score " + _gm.Score
                : map + "  ·  Score " + _gm.Score; // middle dot is in the static LiberationSans SDF atlas
        }

        private string MapName => _gm != null && _gm.Map != null ? _gm.Map.displayName : string.Empty;

        // ================================================================== tutorial

        private void StartTutorial()
        {
            _tutorialActive = true;
            _tutorialTime = 0f;
            _tutorial.Show();
        }

        private void StopTutorial(bool markSeen)
        {
            if (!_tutorialActive) return;
            _tutorialActive = false;
            if (markSeen) GameSettings.TutorialSeen = true;
            _tutorial.Hide();
        }

        // ================================================================== game over

        private void ShowGameOver()
        {
            _gameOverShown = true;
            _finalScore = Mathf.Max(0, _gm.Score);
            _pendingNewBest = _finalScore > 0 && (_gm.IsNewBest || _finalScore > _bestAtStart);
            int best = Mathf.Max(_gm.BestScore, _finalScore > _bestAtStart ? _finalScore : 0);

            _goMap.text = MapName;
            if (_pendingNewBest)
            {
                // The badge already says it; show what was beaten (nothing to show on a first score).
                _goBest.text = _bestAtStart > 0 ? "Previous best " + _bestAtStart : string.Empty;
                _goBest.rectTransform.anchoredPosition = new Vector2(0f, BestLineBelowBadgeY);
            }
            else
            {
                _goBest.text = "Best " + best;
                _goBest.rectTransform.anchoredPosition = new Vector2(0f, BestLineY);
            }
            _goBest.gameObject.SetActive(false);
            _goBadge.SetActive(false);
            _goScore.SetText("{0:0}", 0);

            _lastCount = 0;
            _countTime = -0.35f; // let the panel pop in first
            _countDuration = Mathf.Clamp(0.2f + _finalScore * 0.045f, 0.2f, 1.1f);
            _countingUp = true;

            _gameOverOverlay.Show();
        }

        private void UpdateCountUp()
        {
            _countTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_countTime < 0f) return;

            float t = _countTime / _countDuration;
            int value = Mathf.RoundToInt(UiTheme.EaseOutCubic(t) * _finalScore);
            if (value != _lastCount)
            {
                _lastCount = value;
                _goScore.SetText("{0:0}", value);
            }
            if (t < 1f) return;

            _countingUp = false;
            _goScore.SetText("{0:0}", _finalScore);
            if (_finalScore > 0) _goScorePunch.Play();

            if (_pendingNewBest)
            {
                _goBadge.SetActive(true);
                _goBadgePunch.Play();
                AudioManager.Instance.Play(Sfx.Landed);
            }
            if (!string.IsNullOrEmpty(_goBest.text)) _goBest.gameObject.SetActive(true);
        }

        // ================================================================== frame

        private void Update()
        {
            if (_gm == null) return;

            if (_tutorialActive && _gm.State == GameState.Playing)
            {
                _tutorialTime += Time.unscaledDeltaTime;
                if (_tutorialTime >= TutorialDuration) StopTutorial(true);
            }

            if (_countingUp) UpdateCountUp();

            HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            GameState state = _gm.State;
            if (state == GameState.Playing && kb.fKey.wasPressedThisFrame)
            {
                UiFactory.PlayClick();
                _gm.ToggleFastForward();
            }
            else if ((state == GameState.Playing || state == GameState.Paused) && kb.pKey.wasPressedThisFrame)
            {
                UiFactory.PlayClick();
                _gm.TogglePause();
            }
            else if (state == GameState.GameOver && _gameOverOverlay.Progress >= 1f
                     && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame))
            {
                UiFactory.PlayClick();
                _gm.Restart();
            }
        }
    }
}
