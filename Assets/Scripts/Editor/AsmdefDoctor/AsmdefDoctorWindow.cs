using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Editor
{
    public class AsmdefDoctorWindow : EditorWindow
    {
        private const string ManagedListPath = "Assets/Scripts/Editor/AsmdefDoctor/managed_asmdefs.json";

        private static readonly HashSet<string> ExcludedTopFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Demos", "Scripts", "Scenes", "Settings", "Resources",
        };

        private static readonly Dictionary<string, string> WellKnownNamespaceToAsmdef = new()
        {
            { "TMPro", "Unity.TextMeshPro" },
            { "Unity.VisualScripting", "Unity.VisualScripting.Core" },
            { "UnityEngine.InputSystem", "Unity.InputSystem" },
            { "Cinemachine", "Unity.Cinemachine" },
            { "Unity.Cinemachine", "Unity.Cinemachine" },
            { "Unity.Mathematics", "Unity.Mathematics" },
            { "Unity.Collections", "Unity.Collections" },
            { "Unity.Burst", "Unity.Burst" },
            { "UnityEngine.Timeline", "Unity.Timeline" },
            { "UnityEngine.Playables", "Unity.Timeline" },
            { "UnityEngine.AddressableAssets", "Unity.Addressables" },
            { "UnityEngine.ResourceManagement", "Unity.ResourceManager" },
            { "UnityEngine.AI", "Unity.AI.Navigation" },
            { "UnityEngine.Splines", "Unity.Splines" },
            { "UnityEngine.Rendering.Universal", "Unity.RenderPipelines.Universal.Runtime" },
        };

        private static readonly Regex UsingRegex = new(
            @"^\s*using\s+(?:static\s+)?([A-Za-z_][A-Za-z0-9_.]*)\s*;",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private VisualElement _resultsContainer;
        private VisualElement _restoreContainer;
        private Button _fixButton;
        private Label _statusLabel;

        private readonly List<ScanResult> _scanResults = new();
        private readonly List<ManagedAsmdefEntry> _missingManaged = new();

        [MenuItem("Tools/DemoTools/ASMDEF Doctor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AsmdefDoctorWindow>();
            window.titleContent = new GUIContent("ASMDEF Doctor");
            window.minSize = new Vector2(480, 360);
        }

        public void CreateGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Scripts/Editor/AsmdefDoctor/AsmdefDoctorWindow.uxml");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Scripts/Editor/AsmdefDoctor/AsmdefDoctorWindow.uss");

            if (uxml == null || uss == null)
            {
                rootVisualElement.Add(new Label("Failed to load UXML or USS."));
                return;
            }

            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            _resultsContainer = rootVisualElement.Q<VisualElement>("results-container");
            _restoreContainer = rootVisualElement.Q<VisualElement>("restore-container");
            _fixButton = rootVisualElement.Q<Button>("fix-button");
            _statusLabel = rootVisualElement.Q<Label>("status-label");

            rootVisualElement.Q<Button>("scan-button").clicked += OnScanClicked;
            _fixButton.clicked += OnFixClicked;
            _fixButton.SetEnabled(false);
        }

        // ── Scan ──

        private void OnScanClicked()
        {
            _resultsContainer.Clear();
            _restoreContainer.Clear();
            _scanResults.Clear();
            _missingManaged.Clear();

            Scan();
            CheckManagedAsmdefs();
            RenderResults();

            _fixButton.SetEnabled(_scanResults.Count > 0 || _missingManaged.Count > 0);

            if (_scanResults.Count == 0 && _missingManaged.Count == 0)
                SetStatus("All scripts are covered by assembly definitions.", false);
            else
                SetStatus($"Found {_scanResults.Count} uncovered folder(s), {_missingManaged.Count} missing managed.", true);
        }

        private void Scan()
        {
            var coveredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
                coveredDirs.Add(NormalizePath(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid))));

            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionReferenceAsset"))
                coveredDirs.Add(NormalizePath(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid))));

            var uncoveredScripts = new Dictionary<string, List<string>>();

            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith(".cs")) continue;

                string dir = NormalizePath(Path.GetDirectoryName(scriptPath));
                if (IsCovered(dir, coveredDirs)) continue;

                string topFolder = GetTopFolder(scriptPath);
                if (topFolder == null || ExcludedTopFolders.Contains(topFolder)) continue;

                string pluginRoot = GetPluginRoot(scriptPath);
                if (pluginRoot == null) continue;

                if (!uncoveredScripts.ContainsKey(pluginRoot))
                    uncoveredScripts[pluginRoot] = new List<string>();
                uncoveredScripts[pluginRoot].Add(scriptPath);
            }

            foreach (var kvp in uncoveredScripts.OrderBy(x => x.Key))
            {
                var runtimeScripts = new List<string>();
                var editorScripts = new List<string>();
                var editorDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string script in kvp.Value)
                {
                    if (IsInEditorFolder(script))
                    {
                        editorScripts.Add(script);
                        string editorDir = FindEditorFolder(script);
                        if (editorDir != null)
                            editorDirs.Add(editorDir);
                    }
                    else
                    {
                        runtimeScripts.Add(script);
                    }
                }

                _scanResults.Add(new ScanResult
                {
                    PluginRoot = kvp.Key,
                    RuntimeScriptPaths = runtimeScripts,
                    EditorScriptPaths = editorScripts,
                    EditorFolderPaths = editorDirs.ToList(),
                    Selected = true,
                });
            }
        }

        private static bool IsCovered(string dir, HashSet<string> coveredDirs)
        {
            string current = dir;
            while (!string.IsNullOrEmpty(current) && !current.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                if (coveredDirs.Contains(current)) return true;
                current = NormalizePath(Path.GetDirectoryName(current));
            }
            return false;
        }

        private static string GetTopFolder(string assetPath)
        {
            string relative = assetPath.Substring("Assets/".Length);
            int sep = relative.IndexOfAny(new[] { '/', '\\' });
            return sep > 0 ? relative.Substring(0, sep) : null;
        }

        private static string GetPluginRoot(string assetPath)
        {
            string relative = assetPath.Substring("Assets/".Length);
            string[] parts = relative.Split('/', '\\');

            if (parts.Length < 2) return null;

            string topFolder = parts[0];

            if (topFolder.Equals("Plugins", StringComparison.OrdinalIgnoreCase) ||
                topFolder.Equals("ThirdParty", StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length >= 3 ? $"Assets/{parts[0]}/{parts[1]}" : null;
            }

            return $"Assets/{topFolder}";
        }

        private static bool IsInEditorFolder(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.Contains("/Editor/") || normalized.EndsWith("/Editor");
        }

        private static string FindEditorFolder(string scriptPath)
        {
            string normalized = scriptPath.Replace('\\', '/');
            int idx = normalized.LastIndexOf("/Editor/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = normalized.LastIndexOf("/Editor", StringComparison.OrdinalIgnoreCase);
            return idx > 0 ? normalized.Substring(0, idx + "/Editor".Length) : null;
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/');
        }

        // ── DLL collision detection ──

        private static bool HasDllNameCollision(string asmdefName, string directory)
        {
            if (!Directory.Exists(directory)) return false;
            foreach (string dll in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
            {
                if (Path.GetFileNameWithoutExtension(dll).Equals(asmdefName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string AvoidDllCollision(string baseName, string directory)
        {
            if (HasDllNameCollision(baseName, directory))
            {
                string resolved = baseName + ".Scripts";
                Debug.Log($"[ASMDEF Doctor] Renamed '{baseName}' → '{resolved}' to avoid DLL name collision.");
                return resolved;
            }
            return baseName;
        }

        // ── Dependency detection ──

        private static Dictionary<string, string> BuildNamespaceMap()
        {
            var map = new Dictionary<string, string>(WellKnownNamespaceToAsmdef);

            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<AsmdefJson>(json);
                    if (string.IsNullOrEmpty(data.name)) continue;

                    if (!string.IsNullOrEmpty(data.rootNamespace) && !map.ContainsKey(data.rootNamespace))
                        map[data.rootNamespace] = data.name;

                    if (!map.ContainsKey(data.name))
                        map[data.name] = data.name;
                }
                catch { }
            }

            return map;
        }

        private static List<string> DetectDependencies(List<string> scriptPaths, string ownAsmdefName,
            Dictionary<string, string> namespaceMap)
        {
            var usedNamespaces = new HashSet<string>();

            foreach (string scriptPath in scriptPaths)
            {
                try
                {
                    string content = File.ReadAllText(scriptPath);
                    foreach (Match match in UsingRegex.Matches(content))
                        usedNamespaces.Add(match.Groups[1].Value);
                }
                catch { }
            }

            var references = new HashSet<string>();

            foreach (string ns in usedNamespaces)
            {
                foreach (var kvp in namespaceMap)
                {
                    if ((ns == kvp.Key || ns.StartsWith(kvp.Key + ".")) && kvp.Value != ownAsmdefName)
                    {
                        references.Add(kvp.Value);
                        break;
                    }
                }
            }

            return references.OrderBy(r => r).ToList();
        }

        // ── Managed ASMDEF tracking ──

        private void CheckManagedAsmdefs()
        {
            if (!File.Exists(ManagedListPath)) return;

            try
            {
                var data = JsonUtility.FromJson<ManagedAsmdefList>(File.ReadAllText(ManagedListPath));
                if (data?.entries == null) return;

                foreach (var entry in data.entries)
                {
                    if (!File.Exists(entry.path))
                        _missingManaged.Add(entry);
                }
            }
            catch { }
        }

        // ── UI rendering ──

        private void RenderResults()
        {
            foreach (var result in _scanResults)
            {
                var row = new VisualElement();
                row.AddToClassList("result-row");

                var toggle = new Toggle { value = result.Selected };
                toggle.AddToClassList("result-toggle");
                var capturedResult = result;
                toggle.RegisterValueChangedCallback(evt => capturedResult.Selected = evt.newValue);

                var pathLabel = new Label(result.PluginRoot);
                pathLabel.AddToClassList("result-path");

                string info = $"{result.RuntimeScriptPaths.Count} runtime";
                if (result.EditorScriptPaths.Count > 0)
                    info += $" + {result.EditorScriptPaths.Count} editor";

                var infoLabel = new Label(info);
                infoLabel.AddToClassList("result-info");

                row.Add(toggle);
                row.Add(pathLabel);
                row.Add(infoLabel);
                _resultsContainer.Add(row);
            }

            for (int i = 0; i < _missingManaged.Count; i++)
            {
                var entry = _missingManaged[i];
                var row = new VisualElement();
                row.AddToClassList("restore-row");
                row.userData = i;

                var toggle = new Toggle { value = true };
                toggle.AddToClassList("result-toggle");

                var label = new Label($"Missing: {entry.path}");
                label.AddToClassList("restore-label");

                row.Add(toggle);
                row.Add(label);
                _restoreContainer.Add(row);
            }
        }

        // ── Fix ──

        private void OnFixClicked()
        {
            var generatedEntries = new List<ManagedAsmdefEntry>();
            var namespaceMap = BuildNamespaceMap();
            int detectedDepCount = 0;

            try
            {
                foreach (var result in _scanResults)
                {
                    if (!result.Selected) continue;

                    string asmdefName = SanitizeAsmdefName(Path.GetFileName(result.PluginRoot));
                    asmdefName = AvoidDllCollision(asmdefName, result.PluginRoot);

                    if (result.RuntimeScriptPaths.Count > 0)
                    {
                        var deps = DetectDependencies(result.RuntimeScriptPaths, asmdefName, namespaceMap);
                        detectedDepCount += deps.Count;

                        string runtimePath = $"{result.PluginRoot}/{asmdefName}.asmdef";
                        string content = BuildAsmdefJson(asmdefName, false, deps);
                        File.WriteAllText(runtimePath, content);
                        generatedEntries.Add(new ManagedAsmdefEntry { path = runtimePath, content = content });

                        if (deps.Count > 0)
                            Debug.Log($"[ASMDEF Doctor] {asmdefName}: auto-detected references: {string.Join(", ", deps)}");
                    }

                    if (result.EditorScriptPaths.Count > 0)
                    {
                        var editorDeps = DetectDependencies(result.EditorScriptPaths, asmdefName, namespaceMap);
                        if (result.RuntimeScriptPaths.Count > 0 && !editorDeps.Contains(asmdefName))
                            editorDeps.Insert(0, asmdefName);
                        detectedDepCount += editorDeps.Count;

                        bool needsDiscriminator = result.EditorFolderPaths.Count > 1;

                        foreach (string editorDir in result.EditorFolderPaths)
                        {
                            string editorAsmdefName = asmdefName + ".Editor";

                            if (needsDiscriminator)
                            {
                                string parentOfEditor = Path.GetFileName(Path.GetDirectoryName(editorDir));
                                string sanitizedParent = SanitizeAsmdefName(parentOfEditor);
                                if (!sanitizedParent.Equals(asmdefName, StringComparison.OrdinalIgnoreCase))
                                    editorAsmdefName = $"{asmdefName}.{sanitizedParent}.Editor";
                            }

                            editorAsmdefName = AvoidDllCollision(editorAsmdefName, editorDir);

                            string editorPath = $"{editorDir}/{editorAsmdefName}.asmdef";
                            if (!File.Exists(editorPath))
                            {
                                string content = BuildAsmdefJson(editorAsmdefName, true, editorDeps);
                                File.WriteAllText(editorPath, content);
                                generatedEntries.Add(new ManagedAsmdefEntry { path = editorPath, content = content });

                                if (editorDeps.Count > 0)
                                    Debug.Log($"[ASMDEF Doctor] {editorAsmdefName}: auto-detected references: {string.Join(", ", editorDeps)}");
                            }
                        }
                    }
                }

                RestoreMissingManaged();
                SaveManagedList(generatedEntries);
                AssetDatabase.Refresh();

                SetStatus($"Generated {generatedEntries.Count} ASMDEF(s) with {detectedDepCount} auto-detected reference(s).", false);
            }
            catch (Exception e)
            {
                SetStatus($"Error: {e.Message}", true);
                Debug.LogException(e);
            }
        }

        private void RestoreMissingManaged()
        {
            foreach (var row in _restoreContainer.Children())
            {
                var toggle = row.Q<Toggle>();
                if (toggle == null || !toggle.value) continue;
                if (row.userData is not int index) continue;

                var entry = _missingManaged[index];
                if (File.Exists(entry.path)) continue;

                string dir = Path.GetDirectoryName(entry.path);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(entry.path, entry.content);
            }
        }

        // ── ASMDEF generation ──

        private static string BuildAsmdefJson(string name, bool editorOnly, List<string> references)
        {
            var asmdef = new AsmdefJson
            {
                name = name,
                rootNamespace = "",
                references = references?.ToArray() ?? Array.Empty<string>(),
                includePlatforms = editorOnly ? new[] { "Editor" } : Array.Empty<string>(),
                excludePlatforms = Array.Empty<string>(),
                allowUnsafeCode = false,
                autoReferenced = true,
                overrideReferences = false,
                precompiledReferences = Array.Empty<string>(),
                defineConstraints = Array.Empty<string>(),
                noEngineReferences = false,
            };

            return JsonUtility.ToJson(asmdef, true);
        }

        // ── Managed list persistence ──

        private void SaveManagedList(List<ManagedAsmdefEntry> newEntries)
        {
            var existing = new List<ManagedAsmdefEntry>();

            if (File.Exists(ManagedListPath))
            {
                try
                {
                    var data = JsonUtility.FromJson<ManagedAsmdefList>(File.ReadAllText(ManagedListPath));
                    if (data?.entries != null)
                        existing.AddRange(data.entries);
                }
                catch { }
            }

            foreach (var entry in newEntries)
            {
                int idx = existing.FindIndex(e => e.path == entry.path);
                if (idx >= 0)
                    existing[idx] = entry;
                else
                    existing.Add(entry);
            }

            existing.RemoveAll(e => !File.Exists(e.path) && newEntries.All(n => n.path != e.path));

            var list = new ManagedAsmdefList { entries = existing.ToArray() };
            File.WriteAllText(ManagedListPath, JsonUtility.ToJson(list, true));
        }

        // ── Utilities ──

        private static string SanitizeAsmdefName(string folderName)
        {
            var chars = new List<char>();
            foreach (char c in folderName)
            {
                if (char.IsLetterOrDigit(c) || c == '.')
                    chars.Add(c);
            }
            string result = new string(chars.ToArray());
            return string.IsNullOrEmpty(result) ? "Generated" : result;
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLabel.text = message;
            _statusLabel.RemoveFromClassList("status-success");
            _statusLabel.RemoveFromClassList("status-error");
            _statusLabel.AddToClassList(isError ? "status-error" : "status-success");
        }

        // ── Data structures ──

        [Serializable]
        private class AsmdefJson
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool autoReferenced;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public string[] defineConstraints;
            public bool noEngineReferences;
        }

        [Serializable]
        private class ManagedAsmdefEntry
        {
            public string path;
            public string content;
        }

        [Serializable]
        private class ManagedAsmdefList
        {
            public ManagedAsmdefEntry[] entries;
        }

        private class ScanResult
        {
            public string PluginRoot;
            public List<string> RuntimeScriptPaths;
            public List<string> EditorScriptPaths;
            public List<string> EditorFolderPaths;
            public bool Selected;
        }
    }
}
