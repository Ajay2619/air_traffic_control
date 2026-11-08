using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ClearToLand.Tests
{
    /// <summary>
    /// Minimal game world built in code for PlayMode tests (no scene load): fitted orthographic camera, default
    /// <see cref="GameConfig"/> with a tiny generated sprite, a map with a long runway (threshold (0,0), +X, length 5)
    /// and a helipad, test aircraft types, <see cref="MapRuntime"/> and an initialised <see cref="GameManager"/>.
    /// <see cref="Destroy"/> removes every object and asset created during the test and restores global state.
    /// </summary>
    internal sealed class TestWorld
    {
        /// <summary>Index of the long runway in <see cref="MapRuntime.Spots"/>.</summary>
        public const int RunwayIndex = 0;
        /// <summary>Index of the helipad in <see cref="MapRuntime.Spots"/>.</summary>
        public const int HelipadIndex = 1;
        /// <summary>Runway length in world units.</summary>
        public const float RunwayLength = 5f;
        /// <summary>Runway threshold (approach end); landing direction is +X.</summary>
        public static readonly Vector2 RunwayThreshold = Vector2.zero;
        /// <summary>Helipad center (diameter 1).</summary>
        public static readonly Vector2 HelipadCenter = new Vector2(-5f, -3f);

        /// <summary>Config created with ScriptableObject defaults (plus generated sprites / path material).</summary>
        public GameConfig Config { get; private set; }
        /// <summary>Test map (unique id so high scores never collide with real maps).</summary>
        public MapDefinition Map { get; private set; }
        /// <summary>Orthographic world camera with a <see cref="CameraFitter"/>.</summary>
        public Camera Camera { get; private set; }
        /// <summary>Built map runtime (runway + helipad).</summary>
        public MapRuntime MapRuntime { get; private set; }
        /// <summary>Initialised game manager (state Playing).</summary>
        public GameManager Manager { get; private set; }
        /// <summary>LongRunway type, speed 0.9, radius 0.36.</summary>
        public AircraftType Airliner { get; private set; }
        /// <summary>ShortRunway type, speed 0.72, radius 0.24.</summary>
        public AircraftType Prop { get; private set; }
        /// <summary>Helipad type, speed 0.55, radius 0.26.</summary>
        public AircraftType Helicopter { get; private set; }

        private readonly HashSet<GameObject> _preexistingRoots = new HashSet<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        private TestWorld() { }

        /// <summary>Builds the world. Call from a [UnitySetUp] and yield one frame afterwards so Start() runs.</summary>
        public static TestWorld Create()
        {
            if (GameManager.Instance != null) Object.DestroyImmediate(GameManager.Instance.gameObject);
            Time.timeScale = 1f;

            TestWorld w = new TestWorld();
            w.SnapshotRoots();
            w.BuildAssets();
            w.BuildScene();
            return w;
        }

        /// <summary>Spawns an aircraft through the real gameplay entry point.</summary>
        public Aircraft Spawn(AircraftType type, Vector2 position, Vector2 heading)
            => Aircraft.Spawn(type, position, heading, Manager);

        /// <summary>Adds a <see cref="CollisionMonitor"/> bound to the manager.</summary>
        public CollisionMonitor AddCollisionMonitor()
        {
            CollisionMonitor monitor = new GameObject("CollisionMonitor").AddComponent<CollisionMonitor>();
            monitor.Init(Manager);
            return monitor;
        }

        /// <summary>Yields frames until <paramref name="condition"/> is true or <paramref name="timeoutRealSeconds"/> of real time pass.</summary>
        public static IEnumerator WaitFor(Func<bool> condition, float timeoutRealSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutRealSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Yields frames for <paramref name="realSeconds"/> of real time (works while timeScale is 0).</summary>
        public static IEnumerator WaitRealtime(float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>True when <paramref name="list"/> holds <paramref name="a"/>.</summary>
        public static bool Contains(IReadOnlyList<Aircraft> list, Aircraft a)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == a) return true;
            return false;
        }

        /// <summary>Destroys everything created since <see cref="Create"/>, the AudioManager, test assets and the
        /// test high score; restores Time.timeScale. Call from a [UnityTearDown].</summary>
        public IEnumerator Destroy()
        {
            Time.timeScale = 1f;

            // Aircraft first so their OnDestroy still sees a live GameManager.
            if (Manager != null)
            {
                List<Aircraft> aircraft = new List<Aircraft>(Manager.ActiveAircraft);
                foreach (Aircraft a in aircraft) if (a != null) Object.Destroy(a.gameObject);
                yield return null;
            }

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != null && !_preexistingRoots.Contains(root)) Object.Destroy(root);
            }
            yield return null;

            // After the GameManager is gone (its OnDestroy may still talk to the AudioManager).
            AudioManager audio = Object.FindAnyObjectByType<AudioManager>();
            if (audio != null) Object.Destroy(audio.gameObject);
            yield return null;

            // Clear the test high score before the map asset (which carries the id) is destroyed.
            if (Map != null) HighScores.Clear(Map.id);
            foreach (Object asset in _assets) if (asset != null) Object.Destroy(asset);
            _assets.Clear();
            Time.timeScale = 1f;
        }

        private void SnapshotRoots()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) _preexistingRoots.Add(root);
            }
        }

        private T Track<T>(T asset) where T : Object
        {
            _assets.Add(asset);
            return asset;
        }

        private void BuildAssets()
        {
            Texture2D tex = Track(new Texture2D(16, 16, TextureFormat.RGBA32, false));
            Color32[] pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Track(Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f, 0,
                SpriteMeshType.FullRect, new Vector4(4f, 4f, 4f, 4f)));

            Config = Track(ScriptableObject.CreateInstance<GameConfig>());
            Config.edgeWarningSprite = sprite;
            Config.ringSprite = sprite;
            Config.softCircleSprite = sprite;
            Config.debrisSprite = sprite;
            Config.spotGlowRunwaySprite = sprite;
            Config.spotGlowPadSprite = sprite;
            Config.chevronSprite = sprite;
            Config.circleSprite = sprite;
            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader != null) Config.pathLineMaterial = Track(new Material(lineShader));

            Airliner = CreateType("airliner", "Airliner", LandingKind.LongRunway, 0.9f, 0.36f, sprite, null);
            Prop = CreateType("prop", "Prop plane", LandingKind.ShortRunway, 0.72f, 0.24f, sprite, null);
            Helicopter = CreateType("helicopter", "Helicopter", LandingKind.Helipad, 0.55f, 0.26f, sprite, sprite);
            Config.aircraftTypes.Add(Airliner);
            Config.aircraftTypes.Add(Prop);
            Config.aircraftTypes.Add(Helicopter);

            Map = Track(ScriptableObject.CreateInstance<MapDefinition>());
            Map.id = "playmode_test_" + Guid.NewGuid().ToString("N");
            Map.displayName = "Test Field";
            Map.speedMultiplier = 1f;
            Map.landingSpots.Add(new LandingSpotDef
            {
                name = "Runway", kind = LandingKind.LongRunway, position = RunwayThreshold, angleDeg = 0f,
                length = RunwayLength, width = 0.75f,
            });
            Map.landingSpots.Add(new LandingSpotDef
            {
                name = "Pad", kind = LandingKind.Helipad, position = HelipadCenter, angleDeg = 0f, length = 1f, width = 1f,
            });
            Config.maps.Add(Map);
        }

        private AircraftType CreateType(string id, string displayName, LandingKind kind, float speed, float radius,
            Sprite body, Sprite rotor)
        {
            AircraftType t = Track(ScriptableObject.CreateInstance<AircraftType>());
            t.id = id;
            t.displayName = displayName;
            t.landingKind = kind;
            t.speed = speed;
            t.radius = radius;
            t.scoreValue = 1;
            t.bodySprite = body;
            t.rotorSprite = rotor;
            return t;
        }

        private void BuildScene()
        {
            GameObject camGo = new GameObject("TestCamera");
            camGo.tag = "MainCamera";
            Camera = camGo.AddComponent<Camera>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            Camera.orthographic = true;
            Camera.aspect = 16f / 9f;
            Camera.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<CameraFitter>().Apply();

            MapRuntime = new GameObject("Map").AddComponent<MapRuntime>();
            MapRuntime.Build(Map, Config);

            Manager = new GameObject("GameManager").AddComponent<GameManager>();
            Manager.Init(Config, Map, Camera, MapRuntime);
        }
    }
}
