using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Player builds for the three targets. Each build applies <see cref="ProjectBuilder.ApplyPlayerSettings"/> first,
    /// switches the active build target when needed, builds the enabled scenes from the build settings and returns (and logs)
    /// a one-line summary: result | size | time | errors/warnings | output path [| extra].
    /// Paths are relative to the project root (next to Assets/).
    /// </summary>
    public static class BuildScripts
    {
        /// <summary>WebGL output folder (contains index.html).</summary>
        public const string WebGLOutputPath = "Builds/WebGL";
        /// <summary>Android APK path.</summary>
        public const string AndroidOutputPath = "Builds/Android/ClearToLand.apk";
        /// <summary>Windows executable path.</summary>
        public const string WindowsOutputPath = "Builds/Windows/ClearToLand.exe";
        /// <summary>Zip of the WebGL folder (index.html at the zip root), ready to upload to itch.io as an HTML game.</summary>
        public const string ItchZipPath = "Builds/ClearToLand-WebGL.zip";

        private const string LogPrefix = "[ClearToLand] ";
        private const string BuildsFolder = "Builds";

        /// <summary>
        /// Builds WebGL into Builds/WebGL (cleaned first), restyles the page so the canvas fills its iframe (any itch.io
        /// viewport size works) and packs Builds/ClearToLand-WebGL.zip (index.html at the root) for itch.io.
        /// </summary>
        public static string BuildWebGL()
        {
            string output = ToAbsolute(WebGLOutputPath);
            return Build("WebGL", BuildTarget.WebGL, BuildTargetGroup.WebGL, output, output,
                () => CleanOutputFolder(output),
                () => PackageForItch(output));
        }

        /// <summary>Builds an APK (not an App Bundle) to Builds/Android/ClearToLand.apk. Needs the Android Build Support module.</summary>
        public static string BuildAndroid()
        {
            string output = ToAbsolute(AndroidOutputPath);
            return Build("Android", BuildTarget.Android, BuildTargetGroup.Android, output, output,
                () =>
                {
                    EditorUserBuildSettings.buildAppBundle = false;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                },
                null);
        }

        /// <summary>Builds a 64-bit Windows player to Builds/Windows/ClearToLand.exe.</summary>
        public static string BuildWindows()
        {
            string output = ToAbsolute(WindowsOutputPath);
            return Build("Windows", BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, output,
                Path.GetDirectoryName(output), null, null);
        }

        // Menu wrappers (menu callbacks ignore return values; the public methods above are what automation calls).
        [MenuItem("Tools/Clear to Land/Build/WebGL (itch.io)", false, 100)]
        private static void BuildWebGLMenu() => BuildWebGL();

        [MenuItem("Tools/Clear to Land/Build/Android APK", false, 101)]
        private static void BuildAndroidMenu() => BuildAndroid();

        [MenuItem("Tools/Clear to Land/Build/Windows", false, 102)]
        private static void BuildWindowsMenu() => BuildWindows();

        [MenuItem("Tools/Clear to Land/Build/Open Builds Folder", false, 120)]
        private static void OpenBuildsFolder()
        {
            string folder = ToAbsolute(BuildsFolder);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        // ------------------------------------------------------------------------------------------------------------

        private static string Build(string label, BuildTarget target, BuildTargetGroup group, string outputPath,
            string measurePath, Action prepare, Func<string> afterSuccess)
        {
            string blocker = CheckPreconditions(group, target);
            if (blocker != null) return Fail(label, blocker);

            List<string> scenes = ResolveScenes();
            if (scenes.Count == 0)
                return Fail(label, "no scenes to build; run Tools > Clear to Land > Rebuild Everything first");

            WarnAboutDirtyScenes();

            try
            {
                ProjectBuilder.ApplyPlayerSettings();

                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    Debug.Log($"{LogPrefix}Switching active build target to {target}...");
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                        return Fail(label, $"could not switch the active build target to {target}");
                }

                if (prepare != null) prepare();
                string parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = outputPath,
                    target = target,
                    targetGroup = group,
                    options = BuildOptions.None,
                };

                Debug.Log($"{LogPrefix}Building {label} ({scenes.Count} scenes) -> {outputPath}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report == null) return Fail(label, "BuildPipeline.BuildPlayer returned no report");
                return Summarize(label, report, outputPath, measurePath, afterSuccess);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Fail(label, e.Message);
            }
        }

        private static string CheckPreconditions(BuildTargetGroup group, BuildTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "exit Play Mode before building";
            if (EditorApplication.isCompiling) return "scripts are still compiling; try again when compilation finishes";
            if (BuildPipeline.isBuildingPlayer) return "another player build is already running";
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return $"{target} build support is not installed (add the module in Unity Hub)";
            return null;
        }

        /// <summary>Enabled, existing scenes from the build settings; falls back to the two generated scenes.</summary>
        private static List<string> ResolveScenes()
        {
            List<string> scenes = new List<string>();
            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            if (configured != null)
            {
                foreach (EditorBuildSettingsScene scene in configured)
                {
                    if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path)) continue;
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                    {
                        Debug.LogWarning($"{LogPrefix}Build settings list a missing scene: {scene.path} (skipped).");
                        continue;
                    }
                    scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                foreach (string path in new[] { ProjectBuilder.MainMenuScenePath, ProjectBuilder.GameScenePath })
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) scenes.Add(path);
                }
                if (scenes.Count > 0)
                    Debug.LogWarning($"{LogPrefix}Build settings have no enabled scenes; building {string.Join(", ", scenes)}.");
                return scenes;
            }

            if (scenes[0] != ProjectBuilder.MainMenuScenePath)
                Debug.LogWarning($"{LogPrefix}First build scene is {scenes[0]}, expected {ProjectBuilder.MainMenuScenePath}.");
            if (!scenes.Contains(ProjectBuilder.GameScenePath))
                Debug.LogWarning($"{LogPrefix}{ProjectBuilder.GameScenePath} is not in the build; maps cannot be played.");
            return scenes;
        }

        private static void WarnAboutDirtyScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    Debug.LogWarning($"{LogPrefix}Open scene '{scene.path}' has unsaved changes; the build uses the saved file.");
            }
        }

        private static string Summarize(string label, BuildReport report, string outputPath, string measurePath, Func<string> afterSuccess)
        {
            BuildSummary summary = report.summary;
            bool succeeded = summary.result == BuildResult.Succeeded;

            long size = 0;
            if (succeeded)
            {
                size = MeasurePath(measurePath);
                if (size <= 0) size = (long)summary.totalSize;
            }

            string line = $"{LogPrefix}{label} build {summary.result} | {FormatSize(size)} | {FormatDuration(summary.totalTime)} | " +
                          $"{summary.totalErrors} error(s), {summary.totalWarnings} warning(s) | {outputPath}";

            if (succeeded)
            {
                if (afterSuccess != null)
                {
                    string extra = afterSuccess();
                    if (!string.IsNullOrEmpty(extra)) line += " | " + extra;
                }
                Debug.Log(line);
            }
            else
            {
                string firstError = FirstError(report);
                if (!string.IsNullOrEmpty(firstError)) line += " | first error: " + firstError;
                if (summary.result == BuildResult.Cancelled) Debug.LogWarning(line);
                else Debug.LogError(line);
            }
            return line;
        }

        private static string Fail(string label, string reason)
        {
            string line = $"{LogPrefix}{label} build Failed | 0 B | 0m 00s | 1 error(s), 0 warning(s) | {OneLine(reason, 240)}";
            Debug.LogError(line);
            return line;
        }

        private static string FirstError(BuildReport report)
        {
            BuildStep[] steps = report.steps;
            if (steps == null) return null;
            foreach (BuildStep step in steps)
            {
                BuildStepMessage[] messages = step.messages;
                if (messages == null) continue;
                foreach (BuildStepMessage message in messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception) return OneLine(message.content, 240);
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------------------
        // WebGL / itch.io
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Deletes a previous WebGL output so stale files never end up in the itch.io zip.</summary>
        private static void CleanOutputFolder(string folder)
        {
            string buildsRoot = ToAbsolute(BuildsFolder) + "/";
            string full = folder.Replace('\\', '/');
            if (!full.StartsWith(buildsRoot, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full)) return;
            try
            {
                Directory.Delete(full, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix}Could not clean {full} ({e.Message}); building over the old files.");
            }
        }

        /// <summary>Marker that makes <see cref="PatchTemplateForEmbedding"/> idempotent.</summary>
        private const string EmbedCssMarker = "/* ClearToLand: fill the embedding frame */";

        /// <summary>
        /// Appended to the Default template's TemplateData/style.css. The stock desktop layout centers a fixed
        /// WIDTH x HEIGHT canvas plus a 38 px footer, which an itch.io iframe of the same size clips (top of the game cut off,
        /// scrollbars). These rules make the canvas fill whatever frame/window it is in (CameraFitter adapts to any aspect)
        /// and hide the footer (itch.io has its own fullscreen button). `!important` beats the inline px size the template sets.
        /// Mobile browsers already get the full-screen layout from the template.
        /// </summary>
        private const string EmbedCss =
            "\n" + EmbedCssMarker + "\n" +
            "html, body { width: 100%; height: 100%; overflow: hidden; background: #0e1423 }\n" +
            "#unity-container.unity-desktop { left: 0; top: 0; width: 100%; height: 100%; transform: none }\n" +
            "#unity-container.unity-desktop #unity-canvas { display: block; width: 100% !important; height: 100% !important }\n" +
            "#unity-footer { display: none }\n";

        private static string PackageForItch(string webGLFolder)
        {
            if (!File.Exists(Path.Combine(webGLFolder, "index.html")))
                return "index.html not found; itch.io zip skipped";

            string layoutNote = PatchTemplateForEmbedding(webGLFolder);
            string zipPath = ToAbsolute(ItchZipPath);
            try
            {
                int files = StoredZipWriter.CreateFromDirectory(webGLFolder, zipPath);
                long bytes = new FileInfo(zipPath).Length;
                return $"itch.io zip: {zipPath} ({files} files, {FormatSize(bytes)}); upload as an HTML game" +
                       (string.IsNullOrEmpty(layoutNote) ? string.Empty : "; " + layoutNote);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix}Creating the itch.io zip failed: {e.Message}. Zip the contents of {webGLFolder} manually.");
                return "itch.io zip failed: " + OneLine(e.Message, 160);
            }
        }

        /// <summary>
        /// Makes the built page fill its iframe (see <see cref="EmbedCss"/>). Returns null on success, or a short note when the
        /// page could not be patched (custom template without TemplateData/style.css, IO error) so the summary can say so.
        /// </summary>
        private static string PatchTemplateForEmbedding(string webGLFolder)
        {
            string cssPath = Path.Combine(webGLFolder, "TemplateData", "style.css");
            try
            {
                if (!File.Exists(cssPath))
                    return "TemplateData/style.css not found (custom template?): set the itch.io viewport to the page's full size";

                string css = File.ReadAllText(cssPath);
                if (css.Contains(EmbedCssMarker)) return null;
                File.AppendAllText(cssPath, EmbedCss);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix}Could not patch {cssPath} for iframe embedding: {e.Message}");
                return "page layout not patched for iframes: " + OneLine(e.Message, 120);
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------------------------

        private static string ToAbsolute(string projectRelativePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, projectRelativePath)).Replace('\\', '/');
        }

        private static long MeasurePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                long total = 0;
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    total += new FileInfo(file).Length;
                return total;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):0.00} GB";
            if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):0.0} MB";
            if (bytes >= 1L << 10) return $"{bytes / (double)(1L << 10):0} KB";
            return bytes + " B";
        }

        private static string FormatDuration(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes}m {time.Seconds:00}s";
        }

        private static string OneLine(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string single = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return single.Length > maxLength ? single.Substring(0, maxLength) + "..." : single;
        }
    }
}
