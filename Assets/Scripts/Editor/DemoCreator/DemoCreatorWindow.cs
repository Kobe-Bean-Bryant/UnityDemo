using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Editor
{
    /// <summary>
    /// Demo 脚手架工具 — 基于 UI Toolkit 构建的 EditorWindow。
    ///
    /// UI Toolkit 采用类似 Web 的三层架构：
    ///   UXML（≈ HTML）— 声明 UI 布局结构
    ///   USS （≈ CSS） — 声明样式与外观
    ///   C#  （≈ JS）  — 处理逻辑与交互
    ///
    /// 核心概念：
    ///   VisualElement   — 所有 UI 元素的基类，类似 HTML 的 div
    ///   rootVisualElement — EditorWindow 的根容器，所有子元素都挂载到这里
    ///   Q&lt;T&gt;("name") — 从可视化树中按 name 查询元素，类似 querySelector("#id")
    ///   USS class        — 通过 AddToClassList / RemoveFromClassList 切换，类似 classList.toggle
    /// </summary>
    public class DemoCreatorWindow : EditorWindow
    {
        private static readonly string[] AssetFolderNames =
            { "Prefabs", "Sprites", "Materials", "Audio", "Animations", "Textures", "Fonts", "ScriptableObjects" };

        private static readonly bool[] AssetFolderDefaults =
            { true, true, true, false, false, false, false, false };

        /// <summary>
        /// 游戏开发中最常引用的 Unity 官方包。
        /// 不在此列表中的包会归入"其他 Unity 包"（默认折叠），减少视觉噪音。
        /// </summary>
        private static readonly HashSet<string> CommonPackages = new()
        {
            "Unity.TextMeshPro",
            "UnityEngine.UI",
            "Unity.InputSystem",
            "Unity.Cinemachine",
            "Unity.Mathematics",
            "Unity.Burst",
            "Unity.Collections",
            "Unity.Timeline",
            "Unity.Addressables",
            "Unity.ResourceManager",
            "Unity.AI.Navigation",
            "Unity.Splines",
            "Unity.VisualScripting.Core",
        };

        // ── UI 元素引用（通过 Q<T> 从 VisualTree 查询获取） ──
        private TextField _demoNameField;
        private Label _previewLabel;
        private Label _namespaceLabel;
        private VisualElement _assetFoldersContainer;
        private Toggle _resourcesToggle;
        private Foldout _projectRefsFoldout;
        private Foldout _commonRefsFoldout;
        private Foldout _otherRefsFoldout;
        private RadioButtonGroup _renderingModeGroup;
        private Button _createButton;
        private Label _statusLabel;

        private readonly List<Toggle> _folderToggles = new();
        private readonly List<AsmdefToggleEntry> _asmdefToggles = new();

        [MenuItem("Tools/DemoTools/Demo Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<DemoCreatorWindow>();
            window.titleContent = new GUIContent("Demo Creator");
            window.minSize = new Vector2(420, 520);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  CreateGUI — UI Toolkit 的入口，替代传统 IMGUI 的 OnGUI()
        //  窗口创建时调用一次：加载布局 → 绑定引用 → 注册事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public void CreateGUI()
        {
            // 通过资源路径加载 UXML（布局模板）和 USS（样式表）
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Scripts/Editor/DemoCreator/DemoCreatorWindow.uxml");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Scripts/Editor/DemoCreator/DemoCreatorWindow.uss");

            if (uxml == null || uss == null)
            {
                rootVisualElement.Add(new Label("Failed to load UXML or USS. Check file paths."));
                return;
            }

            // CloneTree — 将 UXML 模板实例化并挂载到 rootVisualElement
            uxml.CloneTree(rootVisualElement);
            // styleSheets.Add — 将 USS 样式表应用到整棵可视化树，子元素自动继承
            rootVisualElement.styleSheets.Add(uss);

            // Q<T>("name") — 按 UXML 中的 name 属性查找元素，类似 document.getElementById
            _demoNameField = rootVisualElement.Q<TextField>("demo-name-field");
            _previewLabel = rootVisualElement.Q<Label>("preview-label");
            _namespaceLabel = rootVisualElement.Q<Label>("namespace-label");
            _assetFoldersContainer = rootVisualElement.Q<VisualElement>("asset-folders-container");
            _resourcesToggle = rootVisualElement.Q<Toggle>("resources-toggle");
            _projectRefsFoldout = rootVisualElement.Q<Foldout>("project-refs-foldout");
            _commonRefsFoldout = rootVisualElement.Q<Foldout>("common-refs-foldout");
            _otherRefsFoldout = rootVisualElement.Q<Foldout>("other-refs-foldout");
            _renderingModeGroup = rootVisualElement.Q<RadioButtonGroup>("rendering-mode-group");
            _createButton = rootVisualElement.Q<Button>("create-button");
            _statusLabel = rootVisualElement.Q<Label>("status-label");

            // 动态构建 Toggle 列表（UXML 只声明了容器，内容由 C# 填充）
            BuildAssetFolderToggles();
            BuildAsmdefToggles();

            // RegisterValueChangedCallback — 值变化时触发回调，类似 addEventListener("input", ...)
            _demoNameField.RegisterValueChangedCallback(_ => UpdatePreview());
            // Button.clicked — Action 委托，用 += 订阅点击回调
            _createButton.clicked += OnCreateClicked;

            UpdatePreview();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  动态构建 UI 元素
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildAssetFolderToggles()
        {
            _folderToggles.Clear();
            for (int i = 0; i < AssetFolderNames.Length; i++)
            {
                // new Toggle(label) — 用 C# 动态创建，等效于 UXML 中 <ui:Toggle label="..."/>
                var toggle = new Toggle(AssetFolderNames[i]) { value = AssetFolderDefaults[i] };
                // AddToClassList — 添加 USS 样式类，类似 element.classList.add("folder-toggle")
                toggle.AddToClassList("folder-toggle");
                // Add — 将子元素挂载到父容器，构建可视化树层级
                _assetFoldersContainer.Add(toggle);
                _folderToggles.Add(toggle);
            }
        }

        /// <summary>
        /// 动态扫描项目中所有 ASMDEF，按来源分三组：
        ///   项目 (Assets/)   — 用户和第三方插件的程序集
        ///   常用 Unity 包    — CommonPackages 中定义的高频官方包
        ///   其他 Unity 包    — 其余的包（默认折叠，减少噪音）
        /// </summary>
        private void BuildAsmdefToggles()
        {
            _projectRefsFoldout.Clear();
            _commonRefsFoldout.Clear();
            _otherRefsFoldout.Clear();
            _asmdefToggles.Clear();

            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!TryParseAsmdef(path, out string asmdefName, out bool isEditorOnly))
                    continue;

                // 过滤：Editor-only、自身、测试/代码生成程序集
                if (isEditorOnly || asmdefName == "Scripts.Editor")
                    continue;
                if (ShouldHideAsmdef(asmdefName))
                    continue;

                bool isPackage = path.StartsWith("Assets/");
                var toggle = new Toggle(asmdefName);
                toggle.AddToClassList("asmdef-toggle");

                if (asmdefName == "UnityDemo.Shared")
                    toggle.value = true;

                // 按来源分组到不同 Foldout
                if (isPackage)
                    _projectRefsFoldout.Add(toggle);
                else if (CommonPackages.Contains(asmdefName))
                    _commonRefsFoldout.Add(toggle);
                else
                    _otherRefsFoldout.Add(toggle);

                _asmdefToggles.Add(new AsmdefToggleEntry(toggle, asmdefName, guid));
            }

            // 默认折叠"其他 Unity 包"，避免列表过长
            _otherRefsFoldout.value = false;
        }

        private static readonly string[] HiddenAsmdefSuffixes =
        {
            ".Tests", ".Test", ".CodeGen",
            ".Demo", ".Demos", ".Debug",
            ".Sample", ".Samples", ".Installer",
            ".Example", ".Examples", ".Tutorial",
        };

        private static bool ShouldHideAsmdef(string name)
        {
            foreach (string suffix in HiddenAsmdefSuffixes)
            {
                if (name.EndsWith(suffix))
                    return true;
            }
            return name.Contains(".CodeGen");
        }

        private static bool TryParseAsmdef(string assetPath, out string asmdefName, out bool isEditorOnly)
        {
            asmdefName = null;
            isEditorOnly = false;

            try
            {
                string json = File.ReadAllText(assetPath);
                var data = JsonUtility.FromJson<AsmdefData>(json);
                asmdefName = data.name;
                // includePlatforms 非空表示平台受限（如仅 Editor），应过滤
                isEditorOnly = data.includePlatforms is { Length: > 0 };
                return !string.IsNullOrEmpty(asmdefName);
            }
            catch
            {
                return false;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  预览 & 校验
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdatePreview()
        {
            string input = _demoNameField?.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(input))
            {
                _previewLabel.text = "Assets/Demos/...";
                _namespaceLabel.text = "...";
                return;
            }

            string demoName = NormalizeDemoName(input);
            _previewLabel.text = $"Assets/Demos/{demoName}/";
            _namespaceLabel.text = demoName;
        }

        private void OnCreateClicked()
        {
            string input = _demoNameField.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(input))
            {
                SetStatus("请输入 Demo 名称", true);
                return;
            }

            if (!Regex.IsMatch(input, @"^[a-zA-Z][a-zA-Z0-9]*$"))
            {
                SetStatus("名称只能包含字母和数字，且必须以字母开头", true);
                return;
            }

            string demoName = NormalizeDemoName(input);
            string demoPath = $"Assets/Demos/{demoName}";

            if (AssetDatabase.IsValidFolder(demoPath))
            {
                SetStatus($"{demoPath} 已存在", true);
                return;
            }

            try
            {
                var renderingMode = (RenderingMode)_renderingModeGroup.value;
                CreateDemo(demoName, demoPath, renderingMode);
                SetStatus($"✓ {demoName} 创建成功!", false);
            }
            catch (Exception e)
            {
                SetStatus($"创建失败: {e.Message}", true);
                Debug.LogException(e);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  Demo 创建逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CreateDemo(string demoName, string demoPath, RenderingMode renderingMode)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Demos"))
                AssetDatabase.CreateFolder("Assets", "Demos");

            AssetDatabase.CreateFolder("Assets/Demos", demoName);
            AssetDatabase.CreateFolder(demoPath, "Scripts");
            AssetDatabase.CreateFolder(demoPath, "Scenes");

            for (int i = 0; i < _folderToggles.Count; i++)
            {
                if (_folderToggles[i].value)
                    AssetDatabase.CreateFolder(demoPath, AssetFolderNames[i]);
            }

            if (_resourcesToggle.value)
            {
                AssetDatabase.CreateFolder(demoPath, "Resources");
                AssetDatabase.CreateFolder($"{demoPath}/Resources", demoName);
            }

            WriteDemoAsmdef(demoName, $"{demoPath}/Scripts/{demoName}.asmdef");

            string scenePath = $"{demoPath}/Scenes/{demoName}.unity";
            SceneSetupHelper.CreateScene(scenePath, renderingMode);

            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private void WriteDemoAsmdef(string demoName, string path)
        {
            var references = new List<string>();
            foreach (var entry in _asmdefToggles)
            {
                if (entry.Toggle.value)
                    references.Add($"GUID:{entry.Guid}");
            }

            var asmdef = new AsmdefData
            {
                name = demoName,
                rootNamespace = demoName,
                references = references.ToArray(),
                includePlatforms = Array.Empty<string>(),
                excludePlatforms = Array.Empty<string>(),
                allowUnsafeCode = false,
                overrideReferences = false,
                precompiledReferences = Array.Empty<string>(),
                autoReferenced = false,
                defineConstraints = Array.Empty<string>(),
                noEngineReferences = false
            };

            // JsonUtility.ToJson — Unity 内置的 JSON 序列化，适用于标注了 [Serializable] 的类
            File.WriteAllText(path, JsonUtility.ToJson(asmdef, true));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  工具方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string NormalizeDemoName(string input)
        {
            return input.EndsWith("Demo", StringComparison.Ordinal) ? input : input + "Demo";
        }

        /// <summary>
        /// 通过切换 USS class 实现状态样式变化。
        /// RemoveFromClassList + AddToClassList 等效于 Web 中 classList.replace。
        /// USS 中定义了 .status-success（绿色）和 .status-error（红色）两组样式。
        /// </summary>
        private void SetStatus(string message, bool isError)
        {
            _statusLabel.text = message;
            _statusLabel.RemoveFromClassList("status-success");
            _statusLabel.RemoveFromClassList("status-error");
            _statusLabel.AddToClassList(isError ? "status-error" : "status-success");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  数据结构
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// ASMDEF 文件的 JSON 映射。
        /// JsonUtility.FromJson 会将 JSON 字段映射到同名 public 字段，忽略多余字段。
        /// </summary>
        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public bool noEngineReferences;
        }

        private readonly struct AsmdefToggleEntry
        {
            public readonly Toggle Toggle;
            public readonly string Name;
            public readonly string Guid;

            public AsmdefToggleEntry(Toggle toggle, string name, string guid)
            {
                Toggle = toggle;
                Name = name;
                Guid = guid;
            }
        }
    }
}
