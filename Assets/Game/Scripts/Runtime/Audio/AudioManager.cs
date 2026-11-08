using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClearToLand
{
    /// <summary>
    /// Persistent 2D sound-effect player. Created lazily on first access to <see cref="Instance"/> (from any scene),
    /// kept alive across scene loads, and plays procedurally synthesized clips (<see cref="SfxSynth"/>) through a
    /// small pool of AudioSources. Clips are generated once, on first use, as non-streamed clips filled with
    /// SetData, so everything works on WebGL. Respects <see cref="GameSettings.SoundEnabled"/> at play time.
    /// Provides its own AudioListener when the loaded scene has none.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>Default overall volume applied on top of the pre-balanced clip levels.</summary>
        public const float DefaultMasterVolume = 0.8f;

        /// <summary>Real-time seconds between proximity warning beeps while the warning is active.</summary>
        public const float WarningInterval = 0.45f;

        private const int VoiceCount = 6;
        private const int SfxCount = (int)Sfx.Click + 1;
        private const int DefaultPriority = 128;
        private const int WarningPriority = 40;

        private static AudioManager s_instance;
        private static bool s_quitting;

        private AudioSource[] _voices;
        private float[] _voiceStartTimes;
        private AudioSource _warningSource;
        private AudioClip[] _clips;
        private AudioClip _warningClip;
        private float[] _lastPlayTimes;
        private GameObject _listenerObject;
        private float _masterVolume = DefaultMasterVolume;
        private float _nextBeepTime;
        private int _warmupIndex;
        private bool _warningActive;
        private bool _initialized;
        private bool _destroyed;
        private uint _jitterState = 0x2545F491u;

        /// <summary>
        /// The shared audio manager. Lazily creates a root "AudioManager" GameObject (DontDestroyOnLoad in play mode)
        /// on first access; never null. While the application is shutting down it may return the already destroyed
        /// instance, whose methods then do nothing, so that no new object is spawned during teardown.
        /// </summary>
        public static AudioManager Instance
        {
            get
            {
                if (s_instance is not null && (s_instance != null || s_quitting)) return s_instance;
                return CreateInstance();
            }
        }

        /// <summary>Overall volume in [0, 1] applied to every sound started from now on (default 0.8).</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                if (_warningSource != null) _warningSource.volume = _masterVolume;
            }
        }

        /// <summary>True while the proximity warning loop has been requested via <see cref="SetWarningActive"/>.</summary>
        public bool IsWarningActive => _warningActive;

        private bool IsUsable => _initialized && !_destroyed;

        /// <summary>
        /// Plays a one-shot effect at <paramref name="volume"/> (0..1, multiplied by <see cref="MasterVolume"/>).
        /// Does nothing when sound is disabled in <see cref="GameSettings"/>. Rapid retriggers of the same effect
        /// within a few tens of milliseconds are merged so overlapping events never stack into a loud burst.
        /// Plays regardless of Time.timeScale (UI clicks work while paused).
        /// </summary>
        public void Play(Sfx sfx, float volume = 1f)
        {
            if (!IsUsable) return;

            int index = (int)sfx;
            if (index < 0 || index >= SfxCount) return;
            if (!(volume > 0f) || !GameSettings.SoundEnabled) return;

            float now = Time.unscaledTime;
            if (now - _lastPlayTimes[index] < RetriggerGuard(sfx)) return;

            AudioClip clip = GetClip(index);
            if (clip == null) return;
            _lastPlayTimes[index] = now;

            int voiceIndex = AcquireVoice();
            AudioSource voice = _voices[voiceIndex];
            if (voice.isPlaying) voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume * _masterVolume);
            voice.pitch = 1f + PitchJitter(sfx) * NextJitter();
            voice.priority = Priority(sfx);
            voice.Play();
            _voiceStartTimes[voiceIndex] = now;
        }

        /// <summary>
        /// Starts or stops the repeating proximity beep (every <see cref="WarningInterval"/> real-time seconds).
        /// Cheap to call every frame; the beep rhythm is kept when the warning flickers off and on. The loop is
        /// silent while Time.timeScale == 0 or sound is disabled, and is cleared when a new scene is loaded.
        /// </summary>
        public void SetWarningActive(bool active)
        {
            if (!IsUsable || active == _warningActive) return;

            _warningActive = active;
            if (active)
            {
                float now = Time.unscaledTime;
                if (_nextBeepTime < now) _nextBeepTime = now;
            }
        }

        /// <summary>
        /// Generates every clip immediately. Optional: clips are otherwise built on first use and also warmed up
        /// automatically, one per frame, during the first frames after the manager is created.
        /// </summary>
        public void Prewarm()
        {
            if (!IsUsable) return;

            for (int i = 0; i < SfxCount; i++)
            {
                GetClip(i);
            }

            GetWarningClip();
        }

        /// <summary>Immediately stops every playing effect and the current warning beep (the warning request itself is kept).</summary>
        public void StopAll()
        {
            if (!IsUsable) return;

            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource voice = _voices[i];
                if (voice != null && voice.isPlaying) voice.Stop();
            }

            if (_warningSource != null && _warningSource.isPlaying) _warningSource.Stop();
        }

        // ------------------------------------------------------------------------------------------------
        // Lifetime
        // ------------------------------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_quitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            s_quitting = true;
        }

        private static AudioManager CreateInstance()
        {
            AudioManager existing = FindAnyObjectByType<AudioManager>();
            if (existing != null && !existing._destroyed)
            {
                existing.Initialize();
                return existing;
            }

            var go = new GameObject("AudioManager");
            if (!Application.isPlaying) go.hideFlags = HideFlags.HideAndDontSave;

            // In play mode AddComponent runs Awake (which initializes); in edit mode Awake is not called,
            // so Initialize is invoked explicitly. Initialize is idempotent.
            AudioManager manager = go.AddComponent<AudioManager>();
            manager.Initialize();
            return manager;
        }

        private void Awake()
        {
            if (s_instance is not null && s_instance != null && !ReferenceEquals(s_instance, this))
            {
                // A second manager (e.g. one placed in a scene) – the persistent one wins.
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;

            _initialized = true;
            s_instance = this;

            if (Application.isPlaying)
            {
                if (transform.parent != null) transform.SetParent(null, false);
                DontDestroyOnLoad(gameObject);
            }

            _clips = new AudioClip[SfxCount];
            _lastPlayTimes = new float[SfxCount];
            for (int i = 0; i < SfxCount; i++)
            {
                _lastPlayTimes[i] = float.NegativeInfinity;
            }

            _voices = new AudioSource[VoiceCount];
            _voiceStartTimes = new float[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = CreateSource(DefaultPriority);
            }

            _warningSource = CreateSource(WarningPriority);
            _warningSource.volume = _masterVolume;
            _nextBeepTime = 0f;

            GameSettings.Changed += OnSettingsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureListener();
        }

        private void Update()
        {
            if (!IsUsable) return;

            // Spread clip generation over the first frames after creation (one clip per frame) so no
            // gameplay moment (first spawn, first crash) pays for synthesis. Each clip is still built once.
            if (_warmupIndex <= SfxCount)
            {
                if (_warmupIndex < SfxCount) GetClip(_warmupIndex);
                else GetWarningClip();
                _warmupIndex++;
            }

            if (!_warningActive || Time.timeScale <= 0f) return;

            float now = Time.unscaledTime;
            if (now < _nextBeepTime) return;

            _nextBeepTime += WarningInterval;
            if (_nextBeepTime <= now) _nextBeepTime = now + WarningInterval;

            if (!GameSettings.SoundEnabled) return;

            if (_warningSource == null) _warningSource = CreateSource(WarningPriority);
            AudioClip clip = GetWarningClip();
            if (_warningSource.clip != clip) _warningSource.clip = clip;
            _warningSource.volume = _masterVolume;
            _warningSource.pitch = 1f;
            _warningSource.Play();
        }

        private void OnApplicationQuit()
        {
            s_quitting = true;
        }

        private void OnDestroy()
        {
            if (!_initialized || _destroyed) return;

            _destroyed = true;
            _warningActive = false;
            GameSettings.Changed -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            for (int i = 0; i < _clips.Length; i++)
            {
                ReleaseClip(_clips[i]);
                _clips[i] = null;
            }

            ReleaseClip(_warningClip);
            _warningClip = null;

            // Keep the (destroyed) reference while quitting so late callers do not spawn a new manager.
            if (ReferenceEquals(s_instance, this) && !s_quitting) s_instance = null;
        }

        private void OnSettingsChanged()
        {
            if (IsUsable && !GameSettings.SoundEnabled) StopAll();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsUsable) return;

            // A replaced scene can never still be in danger: never let the beep leak into the next scene.
            if (mode == LoadSceneMode.Single) _warningActive = false;
            EnsureListener();
        }

        // ------------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Our sounds are 2D, so any listener works. Use the scene's listener (e.g. on the main camera) when one
        /// exists; otherwise enable a listener of our own so audio is never silent for lack of one.
        /// </summary>
        private void EnsureListener()
        {
            if (!Application.isPlaying || _destroyed) return;

            if (_listenerObject == null)
            {
                _listenerObject = new GameObject("AudioListener");
                _listenerObject.SetActive(false);
                _listenerObject.transform.SetParent(transform, false);
                _listenerObject.AddComponent<AudioListener>();
            }

            // Deactivate ours for the search (inactive objects are not found); both changes happen in the same
            // frame, so there is no audible gap.
            if (_listenerObject.activeSelf) _listenerObject.SetActive(false);
            AudioListener sceneListener = FindAnyObjectByType<AudioListener>();
            bool needOwnListener = sceneListener == null || !sceneListener.enabled;
            if (needOwnListener) _listenerObject.SetActive(true);
        }

        private AudioSource CreateSource(int priority)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            source.priority = priority;
            source.volume = 1f;
            source.pitch = 1f;
            return source;
        }

        /// <summary>A free voice, or else the least important / oldest one (a crash is never cut by a click).</summary>
        private int AcquireVoice()
        {
            int victim = 0;
            int victimPriority = -1;
            float victimStart = float.PositiveInfinity;

            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource voice = _voices[i];
                if (voice == null)
                {
                    _voices[i] = CreateSource(DefaultPriority);
                    return i;
                }

                if (!voice.isPlaying) return i;

                int priority = voice.priority; // higher number = less important
                float started = _voiceStartTimes[i];
                if (priority > victimPriority || (priority == victimPriority && started < victimStart))
                {
                    victim = i;
                    victimPriority = priority;
                    victimStart = started;
                }
            }

            return victim;
        }

        private AudioClip GetClip(int index)
        {
            AudioClip clip = _clips[index];
            if (clip == null)
            {
                Sfx sfx = (Sfx)index;
                clip = BuildClip(ClipName(sfx), SfxSynth.Render(sfx));
                _clips[index] = clip;
            }

            return clip;
        }

        private AudioClip GetWarningClip()
        {
            if (_warningClip == null) _warningClip = BuildClip("Sfx.Warning", SfxSynth.RenderWarningBeep());
            return _warningClip;
        }

        private static AudioClip BuildClip(string clipName, float[] samples)
        {
            AudioClip clip = SfxSynth.CreateClip(clipName, samples);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        private static void ReleaseClip(AudioClip clip)
        {
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip);
            else DestroyImmediate(clip);
        }

        /// <summary>Uniform value in [-1, 1] from a private xorshift32 (does not disturb UnityEngine.Random).</summary>
        private float NextJitter()
        {
            uint x = _jitterState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _jitterState = x;
            return (x >> 8) * (2f / 16777215f) - 1f;
        }

        private static string ClipName(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Alert: return "Sfx.Alert";
                case Sfx.Select: return "Sfx.Select";
                case Sfx.Lock: return "Sfx.Lock";
                case Sfx.Landed: return "Sfx.Landed";
                case Sfx.Crash: return "Sfx.Crash";
                case Sfx.Click: return "Sfx.Click";
                default: return "Sfx";
            }
        }

        /// <summary>Minimum real-time seconds between two starts of the same effect.</summary>
        private static float RetriggerGuard(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Alert: return 0.15f;
                case Sfx.Select: return 0.05f;
                case Sfx.Lock: return 0.06f;
                case Sfx.Landed: return 0.08f;
                case Sfx.Crash: return 0.4f;
                case Sfx.Click: return 0.035f;
                default: return 0.05f;
            }
        }

        /// <summary>Random pitch spread (fraction) so frequent sounds do not feel machine-gunned; musical cues stay in tune.</summary>
        private static float PitchJitter(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Select: return 0.035f;
                case Sfx.Lock: return 0.015f;
                case Sfx.Crash: return 0.04f;
                case Sfx.Click: return 0.04f;
                default: return 0f;
            }
        }

        /// <summary>AudioSource priority (0 = most important).</summary>
        private static int Priority(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Crash: return 16;
                case Sfx.Landed: return 48;
                case Sfx.Alert: return 56;
                case Sfx.Lock: return 64;
                case Sfx.Select: return 96;
                case Sfx.Click: return 96;
                default: return DefaultPriority;
            }
        }
    }
}
