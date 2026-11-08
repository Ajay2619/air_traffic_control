using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>
    /// Static source scan for the project's hard rules: runtime code must stay WebGL / IL2CPP safe (no threads, tasks,
    /// System.IO, reflection or legacy Input), use Unity 6 APIs, and every script must be C# 9 compatible with the
    /// agreed namespaces. Comments are ignored. (System.IO is fine here: this is an editor-only test.)
    /// </summary>
    public sealed class CodeRulesTests
    {
        private static string RuntimeFolder => Path.Combine(Application.dataPath, "Game", "Scripts", "Runtime");
        private static string EditorFolder => Path.Combine(Application.dataPath, "Game", "Scripts", "Editor");
        private static string GameFolder => Path.Combine(Application.dataPath, "Game");

        private static readonly KeyValuePair<string, Regex>[] RuntimeForbidden =
        {
            Rule("System.Threading (WebGL has no threads)", @"\bSystem\.Threading\b"),
            Rule("System.IO at runtime (WebGL)", @"\bSystem\.IO\b"),
            Rule("new Thread", @"\bnew\s+Thread\s*\("),
            Rule("Task usage", @"\bTask\s*\.\s*(Run|Delay|Factory|WhenAll|WhenAny)\b"),
            Rule("async methods", @"\basync\s+(Task|void|ValueTask)\b"),
            Rule("System.Reflection (IL2CPP stripping)", @"\bSystem\.Reflection\b"),
            Rule("System.Net (WebGL has no sockets; use UnityWebRequest)", @"\bSystem\.Net\b"),
            Rule("legacy UnityEngine.Input", @"\bUnityEngine\.Input\s*\."),
            // Any member of the legacy Input class throws with activeInputHandler = Input System only
            // (Input.touchSupported, Input.mouseScrollDelta, Input.deviceOrientation, ... not just GetKey/GetTouch).
            Rule("legacy Input API", @"(?<![\w.])Input\s*\.\s*[A-Za-z_]"),
            Rule("FindObjectOfType (use FindAnyObjectByType / FindFirstObjectByType)", @"\bFindObjectsOfType\b|\bFindObjectOfType\b"),
            Rule("TMP enableWordWrapping (use textWrappingMode)", @"\benableWordWrapping\b"),
        };

        private static readonly KeyValuePair<string, Regex>[] CSharp9Forbidden =
        {
            Rule("file-scoped namespace", @"^\s*namespace\s+[\w.]+\s*;"),
            Rule("global using", @"^\s*global\s+using\b"),
            Rule("init accessor", @"\binit\s*;"),
            Rule("record type", @"^\s*(public\s+|internal\s+|private\s+|sealed\s+|abstract\s+)*record\s+(class\s+|struct\s+)?\w+"),
        };

        private static KeyValuePair<string, Regex> Rule(string label, string pattern)
            => new KeyValuePair<string, Regex>(label, new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant));

        private static string StripComments(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        private static string[] Sources(string folder)
        {
            if (!Directory.Exists(folder)) Assert.Inconclusive("Folder not found: " + folder);
            return Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        }

        private static string Relative(string fullPath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            string p = Path.GetFullPath(fullPath).Replace('\\', '/');
            return p.StartsWith(root) ? p.Substring(root.Length + 1) : p;
        }

        private static void AssertNoViolations(string[] files, KeyValuePair<string, Regex>[] rules)
        {
            StringBuilder report = new StringBuilder();
            foreach (string file in files)
            {
                string code = StripComments(File.ReadAllText(file));
                foreach (KeyValuePair<string, Regex> rule in rules)
                {
                    Match m = rule.Value.Match(code);
                    if (m.Success) report.AppendLine(Relative(file) + ": " + rule.Key + " -> '" + m.Value.Trim() + "'");
                }
            }
            Assert.That(report.Length, Is.EqualTo(0), "Rule violations:\n" + report);
        }

        [Test]
        public void RuntimeScripts_AreWebGlAndIl2CppSafe()
        {
            AssertNoViolations(Sources(RuntimeFolder), RuntimeForbidden);
        }

        [Test]
        public void AllGameScripts_AreCSharp9Compatible()
        {
            AssertNoViolations(Sources(GameFolder), CSharp9Forbidden);
        }

        [Test]
        public void RuntimeScripts_UseClearToLandNamespace()
        {
            Regex ns = new Regex(@"^\s*namespace\s+ClearToLand\s*(\{|$)", RegexOptions.Multiline);
            StringBuilder report = new StringBuilder();
            foreach (string file in Sources(RuntimeFolder))
                if (!ns.IsMatch(StripComments(File.ReadAllText(file)))) report.AppendLine(Relative(file));
            Assert.That(report.Length, Is.EqualTo(0), "Expected 'namespace ClearToLand' in:\n" + report);
        }

        [Test]
        public void EditorScripts_UseEditorToolsNamespace()
        {
            Regex ns = new Regex(@"^\s*namespace\s+ClearToLand\.EditorTools\s*(\{|$)", RegexOptions.Multiline);
            StringBuilder report = new StringBuilder();
            foreach (string file in Sources(EditorFolder))
                if (!ns.IsMatch(StripComments(File.ReadAllText(file)))) report.AppendLine(Relative(file));
            Assert.That(report.Length, Is.EqualTo(0), "Expected 'namespace ClearToLand.EditorTools' in:\n" + report);
        }

        [Test]
        public void StripComments_IgnoresMentionsInComments()
        {
            string code = "// FindObjectOfType is banned\n/* System.Threading */\nint x = 1;";
            string stripped = StripComments(code);
            Assert.That(stripped.Contains("FindObjectOfType"), Is.False);
            Assert.That(stripped.Contains("System.Threading"), Is.False);
            Assert.That(stripped.Contains("int x = 1;"), Is.True);
        }
    }
}
