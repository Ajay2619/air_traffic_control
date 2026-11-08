using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Editor pipeline that turns code into the playable project: sprite art, aircraft/map data assets, map backgrounds,
    /// the path-line material, the GameConfig, both scenes, the build scene list and player settings.
    /// Every step is idempotent (existing assets are updated in place so GUIDs stay stable) and a failing step is logged
    /// without aborting the steps after it.
    /// </summary>
    public static class ProjectBuilder
    {
        /// <summary>Folder holding all generated data assets.</summary>
        public const string DataFolder = "Assets/Game/Data";
        /// <summary>Folder holding one AircraftType asset per <see cref="AircraftCatalog.Ids"/> entry.</summary>
        public const string AircraftFolder = DataFolder + "/Aircraft";
        /// <summary>Folder holding one MapDefinition asset per <see cref="MapCatalog.Ids"/> entry.</summary>
        public const string MapsFolder = DataFolder + "/Maps";
        /// <summary>The GameConfig asset referenced by both scenes.</summary>
        public const string ConfigPath = DataFolder + "/GameConfig.asset";
        /// <summary>Folder for generated materials.</summary>
        public const string MaterialsFolder = "Assets/Game/Materials";
        /// <summary>Material used by path LineRenderers.</summary>
        public const string PathLineMaterialPath = MaterialsFolder + "/PathLine.mat";
        /// <summary>Folder for the generated scenes.</summary>
        public const string ScenesFolder = "Assets/Game/Scenes";
        /// <summary>Main menu scene (build index 0).</summary>
        public const string MainMenuScenePath = ScenesFolder + "/" + GameConstants.MainMenuScene + ".unity";
        /// <summary>Gameplay scene (build index 1).</summary>
        public const string GameScenePath = ScenesFolder + "/" + GameConstants.GameScene + ".unity";
        /// <summary>Android package name.</summary>
        public const string AndroidApplicationId = "com.DefaultCompany.ClearToLand";

        /// <summary>Clear color of the scene cameras (dark navy; the Game scene replaces it with the map's color at runtime).</summary>
        public static readonly Color CameraBackground = new Color(0.055f, 0.078f, 0.137f, 1f);

        private const string LogPrefix = "[ClearToLand] ";
        private const string ProgressTitle = "Clear to Land";

        private static int _warnings;
        private static int _errors;
        private static bool _running;
        private static string _runTitle;
        private static System.Diagnostics.Stopwatch _clock;

        /// <summary>Project path of the AircraftType asset for <paramref name="id"/>.</summary>
        public static string AircraftAssetPath(string id) => AircraftFolder + "/" + id + ".asset";

        /// <summary>Project path of the MapDefinition asset for <paramref name="id"/>.</summary>
        public static string MapAssetPath(string id) => MapsFolder + "/" + id + ".asset";

        // ------------------------------------------------------------------------------------------------------------
        // Menu entry points
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Runs the full pipeline: sprites, aircraft assets, map assets, map backgrounds, path material, GameConfig,
        /// scenes + build list, player settings, save. Safe to re-run.
        /// </summary>
        [MenuItem("Tools/Clear to Land/Rebuild Everything", false, 0)]
        public static void RebuildEverything()
        {
            if (!BeginRun("Rebuild Everything")) return;
            try
            {
                // Ask about unsaved scene edits up front (interactive only) so the user is not prompted after minutes of work.
                if (!PrepareToReplaceOpenScenes()) return;

                Dictionary<string, AircraftType> aircraft = new Dictionary<string, AircraftType>();
                List<MapDefinition> maps = new List<MapDefinition>();
                Material pathLine = null;
                GameConfig config = null;

                RunStep("1/9 Generating sprite art", 0.02f, () =>
                {
                    ArtGenerator.GenerateSpriteArt();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    ValidateSpriteArt();
                });
                RunStep("2/9 Aircraft assets", 0.25f, () => aircraft = EnsureAircraftAssets());
                RunStep("3/9 Map assets", 0.28f, () => maps = EnsureMapAssets(aircraft));
                RunStep("4/9 Painting map backgrounds", 0.3f, () => PaintMapBackgrounds(maps, 0.3f, 0.82f));
                RunStep("5/9 Path line material", 0.83f, () => pathLine = EnsurePathLineMaterial());
                RunStep("6/9 Game config", 0.85f, () =>
                {
                    config = EnsureConfig(maps, aircraft, pathLine);
                    AssetDatabase.SaveAssets(); // scenes reference the config: make sure it is on disk first
                });
                RunStep("7/9 Scenes and build settings", 0.88f,
                    () => BuildScenesInternal(config != null ? config : LoadOrCreateConfig()));
                RunStep("8/9 Player settings", 0.95f, ApplyPlayerSettingsInternal);
                RunStep("9/9 Saving assets", 0.98f, () =>
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });
            }
            finally
            {
                EndRun();
            }
        }

        /// <summary>Regenerates every sprite (ArtGenerator) and re-links them into existing aircraft assets and the GameConfig.</summary>
        [MenuItem("Tools/Clear to Land/Generate Sprites", false, 20)]
        public static void GenerateSprites()
        {
            if (!BeginRun("Generate Sprites")) return;
            try
            {
                RunStep("Generating sprite art", 0.05f, () =>
                {
                    ArtGenerator.GenerateSpriteArt();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    ValidateSpriteArt();
                });
                RunStep("Re-linking sprites", 0.9f, RelinkSprites);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EndRun();
            }
        }

        /// <summary>Ensures aircraft + map assets exist (catalog data applied) and repaints every map background.</summary>
        [MenuItem("Tools/Clear to Land/Generate Map Backgrounds", false, 21)]
        public static void GenerateMaps()
        {
            if (!BeginRun("Generate Map Backgrounds")) return;
            try
            {
                Dictionary<string, AircraftType> aircraft = new Dictionary<string, AircraftType>();
                List<MapDefinition> maps = new List<MapDefinition>();
                RunStep("Aircraft assets", 0.02f, () => aircraft = EnsureAircraftAssets());
                RunStep("Map assets", 0.05f, () => maps = EnsureMapAssets(aircraft));
                RunStep("Painting map backgrounds", 0.08f, () => PaintMapBackgrounds(maps, 0.08f, 0.95f));
                RunStep("Updating GameConfig", 0.96f, () =>
                {
                    GameConfig existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
                    if (existing != null) SyncConfigContent(existing, maps, aircraft);
                });
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EndRun();
            }
        }

        /// <summary>Recreates MainMenu.unity and Game.unity, sets the build scene list and reopens MainMenu.</summary>
        [MenuItem("Tools/Clear to Land/Build Scenes", false, 22)]
        public static void BuildScenes()
        {
            if (!BeginRun("Build Scenes")) return;
            try
            {
                if (!PrepareToReplaceOpenScenes()) return;
                GameConfig config = null;
                RunStep("Loading GameConfig", 0.1f, () => config = LoadOrCreateConfig());
                if (config != null && (config.maps == null || config.maps.Count == 0))
                    Warn("GameConfig has no maps yet; run Tools > Clear to Land > Rebuild Everything to generate content.");
                RunStep("Building scenes", 0.4f, () => BuildScenesInternal(config));
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EndRun();
            }
        }

        /// <summary>
        /// Applies product name, landscape-only auto-rotation, Android id/IL2CPP/ARM64, windowed 1600x900 resizable standalone,
        /// runInBackground off and itch.io-friendly WebGL settings (Gzip + decompression fallback, data caching, 960x540).
        /// Also forces render scale 1 on every URP asset so the 2D art stays crisp. Idempotent.
        /// </summary>
        [MenuItem("Tools/Clear to Land/Apply Player Settings", false, 23)]
        public static void ApplyPlayerSettings()
        {
            try
            {
                ApplyPlayerSettingsInternal();
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix}Applying player settings failed: {e.Message}");
                Debug.LogException(e);
            }
        }

        /// <summary>Loads Assets/Game/Data/GameConfig.asset, creating an empty one (default tuning) if it does not exist.</summary>
        public static GameConfig LoadOrCreateConfig()
        {
            return LoadOrCreateAsset<GameConfig>(ConfigPath);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Steps
        // ------------------------------------------------------------------------------------------------------------

        private static void ValidateSpriteArt()
        {
            string[] keys = ArtGenerator.AllKeys;
            if (keys == null || keys.Length == 0)
            {
                Warn("ArtGenerator.AllKeys is empty; cannot verify generated sprites.");
                return;
            }

            List<string> missing = new List<string>();
            foreach (string key in keys)
            {
                if (ArtGenerator.LoadSprite(key) == null) missing.Add(key);
            }
            if (missing.Count > 0)
                Warn($"{missing.Count} sprite(s) missing under {ArtGenerator.OutputRoot}: {string.Join(", ", missing)}");
        }

        private static void RelinkSprites()
        {
            foreach (string id in AircraftCatalog.Ids)
            {
                AircraftType type = AssetDatabase.LoadAssetAtPath<AircraftType>(AircraftAssetPath(id));
                if (type == null) continue;
                AircraftCatalog.Apply(type, id);
                EditorUtility.SetDirty(type);
            }

            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null) AssignConfigSprites(config);
        }

        private static Dictionary<string, AircraftType> EnsureAircraftAssets()
        {
            SpriteImportUtil.EnsureFolder(AircraftFolder);
            Dictionary<string, AircraftType> result = new Dictionary<string, AircraftType>();
            foreach (string id in AircraftCatalog.Ids)
            {
                try
                {
                    AircraftType type = LoadOrCreateAsset<AircraftType>(AircraftAssetPath(id));
                    AircraftCatalog.Apply(type, id);
                    EditorUtility.SetDirty(type);
                    if (type.bodySprite == null)
                        Warn($"Aircraft '{id}' has no body sprite ({ArtGenerator.OutputRoot}/{AircraftCatalog.BodySpriteKey(id)}.png missing).");
                    if (id == "helicopter" && type.rotorSprite == null)
                        Warn($"Helicopter has no rotor sprite ({ArtGenerator.OutputRoot}/{AircraftCatalog.RotorSpriteKey}.png missing).");
                    result[id] = type;
                }
                catch (Exception e)
                {
                    LogException($"Aircraft asset '{id}'", e);
                }
            }
            return result;
        }

        private static List<MapDefinition> EnsureMapAssets(Dictionary<string, AircraftType> aircraft)
        {
            SpriteImportUtil.EnsureFolder(MapsFolder);
            Func<string, AircraftType> lookup = key => LookupAircraft(aircraft, key);
            List<MapDefinition> maps = new List<MapDefinition>();
            foreach (string id in MapCatalog.Ids)
            {
                try
                {
                    MapDefinition def = LoadOrCreateAsset<MapDefinition>(MapAssetPath(id));
                    MapCatalog.Apply(def, id, lookup);
                    EditorUtility.SetDirty(def);
                    if (def.landingSpots == null || def.landingSpots.Count == 0) Warn($"Map '{id}' has no landing spots.");
                    if (def.spawnTable == null || def.spawnTable.Count == 0) Warn($"Map '{id}' has an empty spawn table.");
                    maps.Add(def);
                }
                catch (Exception e)
                {
                    LogException($"Map asset '{id}'", e);
                }
            }
            return maps;
        }

        private static void PaintMapBackgrounds(List<MapDefinition> maps, float progressFrom, float progressTo)
        {
            if (maps == null || maps.Count == 0)
            {
                Warn("No map assets to paint.");
                return;
            }

            for (int i = 0; i < maps.Count; i++)
            {
                MapDefinition def = maps[i];
                if (def == null) continue;
                ShowProgress($"Painting map background '{def.id}' ({i + 1}/{maps.Count})",
                    Mathf.Lerp(progressFrom, progressTo, i / (float)maps.Count));
                try
                {
                    Sprite background = MapPainter.GenerateBackground(def);
                    if (background == null)
                    {
                        Warn($"Map '{def.id}': background generation returned no sprite.");
                    }
                    else if (def.background != background)
                    {
                        def.background = background;
                        EditorUtility.SetDirty(def);
                    }
                }
                catch (Exception e)
                {
                    LogException($"Map background '{def.id}'", e);
                }
            }
        }

        private static Material EnsurePathLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(PathLineMaterialPath);
            if (shader == null)
            {
                Warn("Neither 'Sprites/Default' nor the URP 2D sprite shader was found; PathLine.mat not created/updated.");
                return material;
            }

            if (material == null)
            {
                SpriteImportUtil.EnsureFolder(MaterialsFolder);
                material = new Material(shader) { name = "PathLine" };
                AssetDatabase.CreateAsset(material, PathLineMaterialPath);
                Debug.Log($"{LogPrefix}Created {PathLineMaterialPath} ({shader.name}).");
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameConfig EnsureConfig(List<MapDefinition> maps, Dictionary<string, AircraftType> aircraft, Material pathLine)
        {
            GameConfig config = LoadOrCreateConfig();
            SyncConfigContent(config, maps, aircraft);

            if (pathLine != null) config.pathLineMaterial = pathLine;
            else if (config.pathLineMaterial == null)
                Warn("GameConfig.pathLineMaterial is not set; path lines will fall back to a runtime Sprites/Default material.");

            AssignConfigSprites(config);
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>Replaces the config's map and aircraft lists (catalog order). Keeps the old list if nothing new is available.</summary>
        private static void SyncConfigContent(GameConfig config, List<MapDefinition> maps, Dictionary<string, AircraftType> aircraft)
        {
            if (config.maps == null) config.maps = new List<MapDefinition>();
            if (config.aircraftTypes == null) config.aircraftTypes = new List<AircraftType>();

            List<MapDefinition> newMaps = new List<MapDefinition>();
            if (maps != null)
            {
                foreach (MapDefinition map in maps)
                {
                    if (map != null) newMaps.Add(map);
                }
            }
            if (newMaps.Count > 0)
            {
                config.maps.Clear();
                config.maps.AddRange(newMaps);
            }
            else
            {
                Warn("No map assets available; GameConfig.maps left unchanged.");
            }

            List<AircraftType> newTypes = new List<AircraftType>();
            foreach (string id in AircraftCatalog.Ids)
            {
                AircraftType type = LookupAircraft(aircraft, id);
                if (type != null) newTypes.Add(type);
                else Warn($"Aircraft '{id}' is unavailable; it is missing from GameConfig.aircraftTypes.");
            }
            if (newTypes.Count > 0)
            {
                config.aircraftTypes.Clear();
                config.aircraftTypes.AddRange(newTypes);
            }

            EditorUtility.SetDirty(config);
        }

        private static void AssignConfigSprites(GameConfig c)
        {
            c.edgeWarningSprite = SpriteOrWarn("fx_edge_warning");
            c.ringSprite = SpriteOrWarn("fx_ring");
            c.softCircleSprite = SpriteOrWarn("fx_soft_circle");
            c.debrisSprite = SpriteOrWarn("fx_debris");
            c.spotGlowRunwaySprite = SpriteOrWarn("fx_spot_glow_runway");
            c.spotGlowPadSprite = SpriteOrWarn("fx_spot_glow_pad");
            c.chevronSprite = SpriteOrWarn("fx_chevron");

            c.panelSprite = SpriteOrWarn("ui_panel");
            c.buttonSprite = SpriteOrWarn("ui_button");
            c.cardSprite = SpriteOrWarn("ui_card");
            c.circleSprite = SpriteOrWarn("ui_circle");
            c.logoSprite = SpriteOrWarn("ui_logo");

            c.iconPause = SpriteOrWarn("icon_pause");
            c.iconPlay = SpriteOrWarn("icon_play");
            c.iconFastForward = SpriteOrWarn("icon_fastforward");
            c.iconSoundOn = SpriteOrWarn("icon_sound_on");
            c.iconSoundOff = SpriteOrWarn("icon_sound_off");
            c.iconHome = SpriteOrWarn("icon_home");
            c.iconRestart = SpriteOrWarn("icon_restart");
            c.iconHelp = SpriteOrWarn("icon_help");
            c.iconClose = SpriteOrWarn("icon_close");
            c.iconStar = SpriteOrWarn("icon_star");

            EditorUtility.SetDirty(c);
        }

        private static Sprite SpriteOrWarn(string key)
        {
            Sprite sprite = ArtGenerator.LoadSprite(key);
            if (sprite == null)
                Warn($"Sprite '{key}' not found at {ArtGenerator.OutputRoot}/{key}.png; run Tools > Clear to Land > Generate Sprites.");
            return sprite;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Scenes
        // ------------------------------------------------------------------------------------------------------------

        private static void BuildScenesInternal(GameConfig config)
        {
            if (config == null) Warn("No GameConfig available; scene bootstraps will have an empty 'config' field.");
            SpriteImportUtil.EnsureFolder(ScenesFolder);

            CreateScene(GameScenePath, "GameBootstrap", go =>
            {
                GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
                AssignConfig(bootstrap, config, () => bootstrap.Config = config);
            });

            // MainMenu last: it stays open (saved, not dirty) when this finishes.
            CreateScene(MainMenuScenePath, "MenuBootstrap", go =>
            {
                MenuBootstrap bootstrap = go.AddComponent<MenuBootstrap>();
                AssignConfig(bootstrap, config, () => bootstrap.Config = config);
            });

            ApplyBuildSceneList();
            ReopenMainMenu();
        }

        private static void CreateScene(string path, string bootstrapName, Action<GameObject> addBootstrap)
        {
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ConfigureSceneRendering();
                CreateMainCamera();
                GameObject bootstrapObject = new GameObject(bootstrapName);
                addBootstrap(bootstrapObject);

                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene, path))
                    Debug.Log($"{LogPrefix}Saved scene {path}");
                else
                    LogError($"Could not save scene {path}.");
            }
            catch (Exception e)
            {
                LogException($"Scene {path}", e);
            }
        }

        /// <summary>Unlit 2D sprites only: no skybox, flat white ambient, no fog.</summary>
        private static void ConfigureSceneRendering()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.fog = false;
        }

        private static void CreateMainCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = GameConstants.SafeHeight * 0.5f; // 5.4; CameraFitter adapts it at runtime
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CameraBackground;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.depth = -1f;

            // The only listener in the scene: AudioManager's sources play through it.
            go.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
            }
            else
            {
                Warn("Could not add UniversalAdditionalCameraData to the Main Camera.");
            }

            go.AddComponent<CameraFitter>();
        }

        private static void AssignConfig(MonoBehaviour behaviour, GameConfig config, Action fallbackSetter)
        {
            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty("config");
            if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                prop.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Warn($"{behaviour.GetType().Name} has no serialized 'config' field; assigning through its Config property instead.");
                fallbackSetter();
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static void ApplyBuildSceneList()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in new[] { MainMenuScenePath, GameScenePath })
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) scenes.Add(new EditorBuildSettingsScene(path, true));
                else Warn($"Scene {path} is missing; it was not added to the build settings.");
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ReopenMainMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null)
            {
                Warn($"{MainMenuScenePath} does not exist; nothing to reopen.");
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount == 1 && active.path == MainMenuScenePath && !active.isDirty) return;
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Interactive editor: offers to save dirty open scenes (Cancel aborts). Batch mode: warns that their changes are discarded.
        /// Returns false when the user cancelled.
        /// </summary>
        private static bool PrepareToReplaceOpenScenes()
        {
            List<string> dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) dirty.Add(string.IsNullOrEmpty(scene.path) ? "Untitled" : scene.path);
            }
            if (dirty.Count == 0) return true;

            if (!Application.isBatchMode)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
                Warn("Cancelled: open scenes have unsaved changes.");
                return false;
            }

            Warn($"Discarding unsaved changes in open scene(s): {string.Join(", ", dirty)}.");
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Player settings
        // ------------------------------------------------------------------------------------------------------------

        private static void ApplyPlayerSettingsInternal()
        {
            // Each group is applied independently so one failing setting cannot skip the others.
            ApplySettingsGroup("product name", () => PlayerSettings.productName = GameConstants.GameTitle);

            ApplySettingsGroup("orientation", () =>
            {
                // Landscape only, either way up (Android + mobile browsers).
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.useAnimatedAutorotation = true;
            });

            ApplySettingsGroup("standalone", () =>
            {
                // Windows: resizable 1600x900 window.
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.allowFullscreenSwitch = true;
                PlayerSettings.runInBackground = false;
            });

            ApplySettingsGroup("WebGL", () =>
            {
                // itch.io serves the compressed files without a Content-Encoding header, so the JS decompression fallback is required.
                PlayerSettings.defaultWebScreenWidth = 960;
                PlayerSettings.defaultWebScreenHeight = 540;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
            });

            ApplySettingsGroup("Android", () =>
            {
                // Package id, IL2CPP + ARM64 (required for 64-bit devices / Play Store).
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                    PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
            });

            ApplySettingsGroup("URP render scale", EnsureFullResolutionRendering);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogPrefix}Player settings applied: '{PlayerSettings.productName}', landscape auto-rotation, " +
                      $"Android {AndroidApplicationId} (IL2CPP/ARM64), Windows 1600x900 windowed+resizable, WebGL Gzip + decompression fallback.");
        }

        private static void ApplySettingsGroup(string label, Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception e)
            {
                LogException($"Player settings ({label})", e);
            }
        }

        /// <summary>
        /// The template's Mobile URP asset (used by Android and WebGL) renders at 0.8 scale, which blurs thin path lines and
        /// sprite edges. A 2D game this light can afford native resolution everywhere.
        /// </summary>
        private static void EnsureFullResolutionRendering()
        {
            HashSet<UniversalRenderPipelineAsset> assets = new HashSet<UniversalRenderPipelineAsset>();
            string[] levels = QualitySettings.names;
            for (int i = 0; i < levels.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset levelAsset) assets.Add(levelAsset);
            }
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset)
                assets.Add(defaultAsset);

            foreach (UniversalRenderPipelineAsset asset in assets)
            {
                if (asset == null || asset.renderScale >= 0.999f) continue;
                Debug.Log($"{LogPrefix}URP asset '{asset.name}': render scale {asset.renderScale:0.##} -> 1 (crisp 2D).");
                asset.renderScale = 1f;
                EditorUtility.SetDirty(asset);
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------------------------

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                Warn($"{path} exists but is not a {typeof(T).Name}; replacing it.");

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) SpriteImportUtil.EnsureFolder(folder.Replace('\\', '/'));

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"{LogPrefix}Created {path}");
            return asset;
        }

        private static AircraftType LookupAircraft(Dictionary<string, AircraftType> aircraft, string id)
        {
            if (aircraft == null || string.IsNullOrEmpty(id)) return null;
            AircraftType type;
            return aircraft.TryGetValue(id, out type) ? type : null;
        }

        private static bool BeginRun(string title)
        {
            if (_running)
            {
                Debug.LogWarning($"{LogPrefix}{title}: another Clear to Land builder task is already running.");
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LogPrefix}{title}: exit Play Mode first.");
                return false;
            }

            _running = true;
            _warnings = 0;
            _errors = 0;
            _runTitle = title;
            _clock = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log($"{LogPrefix}{title} started.");
            return true;
        }

        private static void EndRun()
        {
            EditorUtility.ClearProgressBar();
            _running = false;
            double seconds = _clock != null ? _clock.Elapsed.TotalSeconds : 0.0;
            string message = $"{LogPrefix}{_runTitle} finished in {seconds:0.0} s: {_errors} error(s), {_warnings} warning(s).";
            if (_errors > 0) Debug.LogError(message);
            else if (_warnings > 0) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        private static void RunStep(string label, float progress, Action action)
        {
            ShowProgress(label, progress);
            try
            {
                action();
            }
            catch (Exception e)
            {
                LogException(label, e);
            }
        }

        private static void ShowProgress(string info, float progress)
        {
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar(ProgressTitle, info, Mathf.Clamp01(progress));
        }

        private static void Warn(string message)
        {
            _warnings++;
            Debug.LogWarning(LogPrefix + message);
        }

        private static void LogError(string message)
        {
            _errors++;
            Debug.LogError(LogPrefix + message);
        }

        private static void LogException(string context, Exception e)
        {
            _errors++;
            Debug.LogError($"{LogPrefix}{context} failed: {e.Message}");
            Debug.LogException(e);
        }
    }
}
