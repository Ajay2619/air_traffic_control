using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ClearToLand
{
    /// <summary>Top-level round state.</summary>
    public enum GameState
    {
        Playing,
        Paused,
        CrashSlowMo,
        GameOver,
    }

    /// <summary>
    /// Owns the round: state machine and time scale, score / best score, the registry of aircraft, pause and
    /// fast-forward, crash handling and scene transitions. Created and initialized by <see cref="GameBootstrap"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        private static bool s_quitting;

        private readonly List<Aircraft> _aircraft = new List<Aircraft>(32);
        private bool _initialized;
        private int _bestAtStart;
        private float _slowMoEndTime;
        private bool _scoreSubmitted;

        /// <summary>The active manager (set in Init, cleared in OnDestroy).</summary>
        public static GameManager Instance { get; private set; }

        /// <summary>Game configuration (never null after Init).</summary>
        public GameConfig Config { get; private set; }

        /// <summary>The map being played.</summary>
        public MapDefinition Map { get; private set; }

        /// <summary>Runtime map (spots, background).</summary>
        public MapRuntime MapRuntime { get; private set; }

        /// <summary>The orthographic world camera.</summary>
        public Camera WorldCamera { get; private set; }

        /// <summary>Current round state.</summary>
        public GameState State { get; private set; }

        /// <summary>True while fast-forward is toggled on (applies while Playing).</summary>
        public bool IsFastForward { get; private set; }

        /// <summary>Points scored this round.</summary>
        public int Score { get; private set; }

        /// <summary>Best score for this map: stored best at Init, rising live once Score exceeds it.</summary>
        public int BestScore { get; private set; }

        /// <summary>True once Score beats the best score stored when the round started (and is above 0).</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>Scaled seconds spent in Playing.</summary>
        public float ElapsedPlayTime { get; private set; }

        /// <summary>World rect visible through <see cref="WorldCamera"/>, refreshed every frame.</summary>
        public Rect ViewRect { get; private set; }

        /// <summary>Every registered aircraft (any state), in registration order.</summary>
        public IReadOnlyList<Aircraft> ActiveAircraft => _aircraft;

        /// <summary>Raised with the new score after each touchdown.</summary>
        public event Action<int> ScoreChanged;

        /// <summary>Raised with the new state on every state change.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Raised when fast-forward is toggled.</summary>
        public event Action<bool> FastForwardChanged;

        /// <summary>Raised at touchdown (start of the landing roll / helipad descent).</summary>
        public event Action<Aircraft> AircraftLanded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            s_quitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            s_quitting = true;
        }

        /// <summary>Starts a round on <paramref name="map"/>. A null config falls back to defaults.</summary>
        public void Init(GameConfig config, MapDefinition map, Camera worldCamera, MapRuntime mapRuntime)
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[ClearToLand] A second GameManager was initialized; it replaces the previous Instance.");
            Instance = this;

            if (config == null)
            {
                Debug.LogWarning("[ClearToLand] GameManager.Init: no GameConfig, using defaults.");
                config = ScriptableObject.CreateInstance<GameConfig>();
            }

            Config = config;
            Map = map;
            WorldCamera = worldCamera != null ? worldCamera : Camera.main;
            MapRuntime = mapRuntime;

            _bestAtStart = map != null ? HighScores.Get(map.id) : 0;
            BestScore = _bestAtStart;
            Score = 0;
            IsNewBest = false;
            ElapsedPlayTime = 0f;
            IsFastForward = false;
            _scoreSubmitted = false;
            State = GameState.Playing;
            Time.timeScale = 1f;
            _initialized = true;
            RefreshViewRect();
            ApplyScreenSleep();
        }

        /// <summary>Adds an aircraft to <see cref="ActiveAircraft"/> (Aircraft.Spawn does this).</summary>
        public void RegisterAircraft(Aircraft a)
        {
            if (a == null || _aircraft.Contains(a)) return;
            _aircraft.Add(a);
        }

        /// <summary>Removes an aircraft from <see cref="ActiveAircraft"/>.</summary>
        public void UnregisterAircraft(Aircraft a)
        {
            if ((object)a == null) return;
            _aircraft.Remove(a);
        }

        /// <summary>Scores a touchdown: Score += type.scoreValue, events, "+n" popup, landed sound. Ignored after a crash.</summary>
        public void NotifyTouchdown(Aircraft a)
        {
            if (a == null || !_initialized) return;
            if (State == GameState.CrashSlowMo || State == GameState.GameOver) return;

            int value = a.Type != null ? Mathf.Max(0, a.Type.scoreValue) : 1;
            Score += value;
            if (Score > BestScore) BestScore = Score;
            if (Score > _bestAtStart && Score > 0) IsNewBest = true;

            ScoreChanged?.Invoke(Score);
            AircraftLanded?.Invoke(a);

            Color popupColor = a.Type != null ? Color.Lerp(a.Type.Color, Color.white, 0.35f) : Color.white;
            ScorePopup.Play(a.Position + new Vector2(0f, 0.45f), "+" + value, popupColor);
            AudioManager.Instance.Play(Sfx.Landed);
        }

        /// <summary>Mid-air collision (only from Playing): both crash, explosion, crash sound, slow motion then game over.</summary>
        public void TriggerCrash(Aircraft a, Aircraft b)
        {
            if (!_initialized || State != GameState.Playing) return;

            Vector2 pa = a != null ? a.Position : (b != null ? b.Position : Vector2.zero);
            Vector2 pb = b != null ? b.Position : pa;
            if (a != null) a.Crash();
            if (b != null) b.Crash();

            ExplosionEffect.Play((pa + pb) * 0.5f, Config);
            AudioManager.Instance.Play(Sfx.Crash);
            SetState(GameState.CrashSlowMo);
        }

        /// <summary>Pauses (Playing -> Paused) or resumes (Paused -> Playing); other states are unaffected.</summary>
        public void SetPaused(bool paused)
        {
            if (!_initialized) return;
            if (paused && State == GameState.Playing) SetState(GameState.Paused);
            else if (!paused && State == GameState.Paused) SetState(GameState.Playing);
        }

        /// <summary>Toggles between Playing and Paused.</summary>
        public void TogglePause()
        {
            if (State == GameState.Playing) SetPaused(true);
            else if (State == GameState.Paused) SetPaused(false);
        }

        /// <summary>Turns fast-forward on/off (time scale config.fastForwardScale while Playing).</summary>
        public void SetFastForward(bool on)
        {
            if (IsFastForward == on) return;
            IsFastForward = on;
            if (_initialized && State == GameState.Playing) Time.timeScale = PlayingTimeScale();
            FastForwardChanged?.Invoke(on);
        }

        /// <summary>Toggles fast-forward.</summary>
        public void ToggleFastForward()
        {
            SetFastForward(!IsFastForward);
        }

        /// <summary>Reloads the Game scene on the same map (time scale reset to 1).</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            if (Map != null && GameSession.SelectedMap == null) GameSession.SelectedMap = Map;
            SubmitScore();
            SceneManager.LoadScene(GameConstants.GameScene);
        }

        /// <summary>Loads the main menu (time scale reset to 1).</summary>
        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SubmitScore();
            SceneManager.LoadScene(GameConstants.MainMenuScene);
        }

        private float PlayingTimeScale()
        {
            if (!IsFastForward || Config == null) return 1f;
            return Mathf.Clamp(Config.fastForwardScale, 0.1f, 10f);
        }

        private void SetState(GameState next)
        {
            GameState prev = State;
            if (prev == next) return;
            State = next;

            if (prev == GameState.Playing) SetWarningSound(false);

            switch (next)
            {
                case GameState.Playing:
                    Time.timeScale = PlayingTimeScale();
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.CrashSlowMo:
                    Time.timeScale = Mathf.Clamp(Config.crashSlowMotionScale, 0.01f, 1f);
                    _slowMoEndTime = Time.unscaledTime + Mathf.Max(0f, Config.crashSlowMotionDuration);
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    SubmitScore();
                    SetWarningSound(false);
                    break;
            }

            ApplyScreenSleep();
            StateChanged?.Invoke(next);
        }

        private void SubmitScore()
        {
            if (_scoreSubmitted || Map == null || Score <= 0) return;
            _scoreSubmitted = true;
            HighScores.Submit(Map.id, Score);
        }

        /// <summary>
        /// Stores the round's score so far if it beats the saved best (app backgrounded, focus lost, quitting), so a
        /// live new best survives the process being killed or the tab being closed. Unlike <see cref="SubmitScore"/>
        /// it does not mark the score as submitted, so a higher final score is still saved later; HighScores.Submit
        /// only writes when the score is higher. IsNewBest keeps comparing against the best at round start.
        /// </summary>
        private void SaveBestSoFar()
        {
            if (_initialized && Map != null && Score > 0) HighScores.Submit(Map.id, Score);
        }

        /// <summary>Keeps the screen awake while a round is live (Playing / CrashSlowMo); otherwise uses the system setting.</summary>
        private void ApplyScreenSleep()
        {
            Screen.sleepTimeout = State == GameState.Playing || State == GameState.CrashSlowMo
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;
        }

        private static void SetWarningSound(bool on)
        {
            if (s_quitting) return;
            AudioManager.Instance.SetWarningActive(on);
        }

        private void RefreshViewRect()
        {
            if (WorldCamera != null) ViewRect = PlayBounds.ViewRect(WorldCamera);
        }

        private void Update()
        {
            if (!_initialized) return;

            RefreshViewRect();

            for (int i = _aircraft.Count - 1; i >= 0; i--)
            {
                if (_aircraft[i] == null) _aircraft.RemoveAt(i); // destroyed without unregistering
            }

            HandleKeyboard();

            switch (State)
            {
                case GameState.Playing:
                    ElapsedPlayTime += Time.deltaTime;
                    break;
                case GameState.CrashSlowMo:
                    if (Time.unscaledTime >= _slowMoEndTime) SetState(GameState.GameOver);
                    break;
            }
        }

        private void HandleKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Esc (Android back maps to Escape) toggles pause. P / F shortcuts are handled by GameUi.
            if (kb.escapeKey.wasPressedThisFrame && (State == GameState.Playing || State == GameState.Paused))
                TogglePause();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) return;
            SaveBestSoFar(); // the OS may kill a backgrounded app
            if (_initialized && State == GameState.Playing) SetPaused(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            SaveBestSoFar(); // e.g. the browser tab is switched away and then closed
#if !UNITY_EDITOR
            if (_initialized && State == GameState.Playing) SetPaused(true);
#endif
        }

        private void OnApplicationQuit()
        {
            SaveBestSoFar(); // window closed (X / Alt+F4) mid-round
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            if (_initialized)
            {
                SetWarningSound(false);
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
        }
    }
}
