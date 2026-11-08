using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Entry point of MainMenu.unity. Fits the camera, builds the dimmed map backdrop with a few drifting decorative
    /// aircraft, ensures an EventSystem, and builds the menu UI (<see cref="MainMenuUi"/>).
    /// The project builder assigns <see cref="Config"/> through the private serialized field "config".
    /// </summary>
    public sealed class MenuBootstrap : MonoBehaviour
    {
        private const int MinAmbientAircraft = 3;
        private const int MaxAmbientAircraft = 5;
        private static readonly Color BackdropTint = new Color(0.46f, 0.51f, 0.6f, 1f);
        private static readonly Color FallbackSky = new Color(0.06f, 0.09f, 0.14f, 1f);

        [SerializeField] private GameConfig config;

        private CameraFitter _fitter;
        private Camera _cam;
        private Transform _background;
        private Vector2 _backgroundSize;

        /// <summary>The game config used to build the menu.</summary>
        public GameConfig Config
        {
            get => config;
            set => config = value;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            UiFactory.EnsureEventSystem();
            _cam = EnsureCamera();

            if (config != null)
            {
                UiTheme.Use(config);
                BuildBackdrop();
            }
            else
            {
                Debug.LogError("[ClearToLand] MenuBootstrap has no GameConfig assigned. Run Tools > Clear to Land > Rebuild Everything.");
                if (_cam != null) _cam.backgroundColor = FallbackSky;
            }

            MainMenuUi.Create(config);
            _ = AudioManager.Instance; // make sure the persistent audio object exists from the first scene
        }

        private void OnDestroy()
        {
            if (_fitter != null) _fitter.ViewChanged -= FitBackground;
        }

        private Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                go.transform.position = new Vector3(0f, 0f, -10f);
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FallbackSky;
            }

            _fitter = cam.GetComponent<CameraFitter>();
            if (_fitter == null) _fitter = cam.gameObject.AddComponent<CameraFitter>();
            _fitter.Apply();
            _fitter.ViewChanged += FitBackground;
            return cam;
        }

        private void BuildBackdrop()
        {
            MapDefinition first = config.maps != null && config.maps.Count > 0 ? config.maps[0] : null;

            Color sky = first != null ? first.cameraColor : FallbackSky;
            _cam.backgroundColor = new Color(sky.r * 0.42f, sky.g * 0.45f, sky.b * 0.5f, 1f);

            GameObject root = new GameObject("MenuBackdrop");

            if (first != null && first.background != null)
            {
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(root.transform, false);
                SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
                sr.sprite = first.background;
                sr.color = BackdropTint;
                sr.sortingOrder = SortOrder.Background;
                _background = bg.transform;
                _backgroundSize = first.background.bounds.size;
                FitBackground();
            }

            SpawnAmbientAircraft(root.transform);
        }

        /// <summary>Scales the backdrop up when the view is larger than the map art (e.g. a portrait browser window).</summary>
        private void FitBackground()
        {
            if (_background == null || _cam == null || _backgroundSize.x <= 0f || _backgroundSize.y <= 0f) return;
            Rect view = PlayBounds.ViewRect(_cam);
            float s = Mathf.Max(1f, view.width / _backgroundSize.x, view.height / _backgroundSize.y);
            _background.localScale = new Vector3(s, s, 1f);
        }

        private void SpawnAmbientAircraft(Transform parent)
        {
            if (config.aircraftTypes == null) return;

            int typeCount = 0;
            for (int i = 0; i < config.aircraftTypes.Count; i++)
            {
                AircraftType t = config.aircraftTypes[i];
                if (t != null && t.bodySprite != null) typeCount++;
            }
            if (typeCount == 0) return;

            AircraftType[] types = new AircraftType[typeCount];
            for (int i = 0, k = 0; i < config.aircraftTypes.Count; i++)
            {
                AircraftType t = config.aircraftTypes[i];
                if (t != null && t.bodySprite != null) types[k++] = t;
            }

            int count = Mathf.Clamp(typeCount, MinAmbientAircraft, MaxAmbientAircraft);
            Rect view = PlayBounds.ViewRect(_cam);
            Vector2 shadowOffset = config.shadowOffset * 1.25f;
            int offset = Random.Range(0, typeCount);

            for (int i = 0; i < count; i++)
            {
                AircraftType type = types[(i + offset) % typeCount];
                float fx = (i + 0.5f) / count;
                float x = Mathf.Lerp(view.xMin + 1.2f, view.xMax - 1.2f, fx) + Random.Range(-0.8f, 0.8f);
                float y = Random.Range(view.yMin + 1.2f, view.yMax - 1.2f);
                // Mostly sideways headings read best behind the menu cards.
                float heading = (i % 2 == 0 ? 0f : 180f) + Random.Range(-35f, 35f);
                float speed = Random.Range(0.35f, 0.65f) * (type.IsHelicopter ? 0.75f : 1f);
                MenuAmbientAircraft.Create(type, _cam, new Vector2(x, y), heading, speed, 1.1f, shadowOffset, parent);
            }
        }
    }
}
