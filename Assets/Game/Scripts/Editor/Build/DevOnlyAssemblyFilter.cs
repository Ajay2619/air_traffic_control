using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Keeps the Unity MCP plugin (used only to drive the Editor during development) out of every player build:
    /// Android, Windows and WebGL. Unity calls this after compiling the player scripts; it drops the plugin's runtime
    /// assembly and the NuGet DLLs the plugin installed under Assets/Plugins/NuGet (listed in .nuget-installed.json).
    /// The Editor and its MCP connection are unaffected. Toggle with Tools > Clear to Land > Build > Include MCP Plugin In Builds.
    /// </summary>
    public sealed class DevOnlyAssemblyFilter : IFilterBuildAssemblies
    {
        public const string McpRuntimeAssembly = "com.IvanMurzak.Unity.MCP.Runtime.dll";
        public const string NuGetManifestPath = "Assets/Plugins/NuGet/.nuget-installed.json";

        private const string PrefKey = "ClearToLand.IncludeDevPluginsInBuilds";
        private const string MenuPath = "Tools/Clear to Land/Build/Include MCP Plugin In Builds";

        public int callbackOrder => 0;

        /// <summary>When true the filter is skipped and the plugin ships in builds (per-machine EditorPrefs, default false).</summary>
        public static bool IncludeDevPluginsInBuilds
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            if (assemblies == null || IncludeDevPluginsInBuilds) return assemblies;

            HashSet<string> drop = DevOnlyFileNames();
            List<string> kept = new List<string>(assemblies.Length);
            List<string> removed = new List<string>();
            foreach (string assembly in assemblies)
            {
                string file = Path.GetFileName(assembly);
                if (drop.Contains(file)) removed.Add(file);
                else kept.Add(assembly);
            }

            if (removed.Count > 0)
                Debug.Log($"[ClearToLand] Excluded {removed.Count} editor-only plugin assemblies from the player build: {string.Join(", ", removed)}");
            return kept.ToArray();
        }

        /// <summary>File names to exclude: the MCP runtime assembly plus every DLL listed in the plugin's NuGet manifest.</summary>
        public static HashSet<string> DevOnlyFileNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { McpRuntimeAssembly };
            try
            {
                string manifest = Path.GetFullPath(NuGetManifestPath);
                if (File.Exists(manifest))
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(manifest), "\"([^\"\\\\/]+\\.dll)\""))
                        names.Add(m.Groups[1].Value);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClearToLand] Could not read {NuGetManifestPath}: {e.Message}. Only {McpRuntimeAssembly} is excluded.");
            }
            return names;
        }

        [MenuItem(MenuPath, false, 110)]
        private static void ToggleInclude()
        {
            IncludeDevPluginsInBuilds = !IncludeDevPluginsInBuilds;
            Debug.Log($"[ClearToLand] MCP plugin {(IncludeDevPluginsInBuilds ? "will be included in" : "will be excluded from")} player builds.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleIncludeValidate()
        {
            Menu.SetChecked(MenuPath, IncludeDevPluginsInBuilds);
            return true;
        }
    }
}
