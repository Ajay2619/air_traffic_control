using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Entry point of the Game scene: fits the camera, builds the map, the GameManager and its helpers
    /// (spawning, collisions, path input), then the HUD. The editor builder assigns <c>config</c>.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        /// <summary>The game configuration used to build the round.</summary>
        public GameConfig Config
        {
            get => config;
            set => config = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureFrameRate()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Mobile players default to 30 fps, which makes path drawing feel laggy. WebGL keeps the browser's
            // requestAnimationFrame pacing (targetFrameRate -1); desktop keeps vSync.
            Application.targetFrameRate = 60;
#endif
        }

        private void Start()
        {
            Time.timeScale = 1f;

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            CameraFitter fitter = cam.GetComponent<CameraFitter>();
            if (fitter == null) fitter = cam.gameObject.AddComponent<CameraFitter>();
            fitter.Apply();

            if (config == null)
                Debug.LogError("[ClearToLand] GameBootstrap: no GameConfig assigned (run Tools > Clear to Land > Rebuild Everything).");

            MapDefinition map = GameSession.ResolveMap(config);
            if (map == null)
            {
                Debug.LogError("[ClearToLand] GameBootstrap: no map to play (GameConfig missing or has no maps).");
                return;
            }

            GameConfig cfg = config != null ? config : ScriptableObject.CreateInstance<GameConfig>();

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = map.cameraColor;

            MapRuntime mapRuntime = new GameObject("Map").AddComponent<MapRuntime>();
            mapRuntime.Build(map, cfg);

            GameObject gmGo = new GameObject("GameManager");
            GameManager gm = gmGo.AddComponent<GameManager>();
            gm.Init(cfg, map, cam, mapRuntime);

            gmGo.AddComponent<SpawnDirector>().Init(gm, System.Environment.TickCount);
            gmGo.AddComponent<CollisionMonitor>().Init(gm);
            gmGo.AddComponent<PathInputController>().Init(gm);

            UiFactory.EnsureEventSystem();
            GameUi.Create(gm, cfg);

            // Make sure the audio singleton exists before the first sound is needed.
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null) Debug.LogWarning("[ClearToLand] GameBootstrap: AudioManager unavailable.");
        }
    }
}
