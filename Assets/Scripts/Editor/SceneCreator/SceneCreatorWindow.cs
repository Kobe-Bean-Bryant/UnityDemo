using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityDemo.Editor
{
    public class SceneCreatorWindow : EditorWindow
    {
        private DropdownField _demoDropdown;
        private TextField _sceneNameField;
        private Label _previewLabel;
        private RadioButtonGroup _renderingModeGroup;
        private Button _createButton;
        private Label _statusLabel;

        private List<string> _demoNames = new();

        [MenuItem("Tools/DemoTools/Scene Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneCreatorWindow>();
            window.titleContent = new GUIContent("Scene Creator");
            window.minSize = new Vector2(420, 320);
        }

        public void CreateGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Scripts/Editor/SceneCreator/SceneCreatorWindow.uxml");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Scripts/Editor/SceneCreator/SceneCreatorWindow.uss");

            if (uxml == null || uss == null)
            {
                rootVisualElement.Add(new Label("Failed to load UXML or USS. Check file paths."));
                return;
            }

            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            _demoDropdown = rootVisualElement.Q<DropdownField>("demo-dropdown");
            _sceneNameField = rootVisualElement.Q<TextField>("scene-name-field");
            _previewLabel = rootVisualElement.Q<Label>("preview-label");
            _renderingModeGroup = rootVisualElement.Q<RadioButtonGroup>("rendering-mode-group");
            _createButton = rootVisualElement.Q<Button>("create-button");
            _statusLabel = rootVisualElement.Q<Label>("status-label");

            PopulateDemoDropdown();

            _demoDropdown.RegisterValueChangedCallback(_ => UpdatePreview());
            _sceneNameField.RegisterValueChangedCallback(_ => UpdatePreview());
            _createButton.clicked += OnCreateClicked;

            UpdatePreview();
        }

        private void PopulateDemoDropdown()
        {
            _demoNames.Clear();

            string demosRoot = "Assets/Demos";
            if (!AssetDatabase.IsValidFolder(demosRoot))
            {
                _demoDropdown.choices = _demoNames;
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Folder", new[] { demosRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string dirName = Path.GetFileName(path);
                if (path == $"{demosRoot}/{dirName}")
                    _demoNames.Add(dirName);
            }

            _demoNames.Sort();
            _demoDropdown.choices = _demoNames;

            if (_demoNames.Count > 0)
                _demoDropdown.value = _demoNames[0];
        }

        private void UpdatePreview()
        {
            string demo = _demoDropdown?.value ?? "";
            string sceneName = _sceneNameField?.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(demo) || string.IsNullOrEmpty(sceneName))
            {
                _previewLabel.text = "Assets/Demos/.../Scenes/...";
                return;
            }

            _previewLabel.text = $"Assets/Demos/{demo}/Scenes/{sceneName}.unity";
        }

        private void OnCreateClicked()
        {
            string demo = _demoDropdown?.value ?? "";
            string sceneName = _sceneNameField?.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(demo))
            {
                SetStatus("请选择目标 Demo", true);
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                SetStatus("请输入场景名称", true);
                return;
            }

            if (!Regex.IsMatch(sceneName, @"^[a-zA-Z][a-zA-Z0-9_ -]*$"))
            {
                SetStatus("场景名称只能包含字母、数字、空格、下划线和连字符，且必须以字母开头", true);
                return;
            }

            string demoPath = $"Assets/Demos/{demo}";
            string scenesPath = $"{demoPath}/Scenes";
            string scenePath = $"{scenesPath}/{sceneName}.unity";

            if (!AssetDatabase.IsValidFolder(demoPath))
            {
                SetStatus($"Demo 文件夹不存在: {demoPath}", true);
                return;
            }

            if (File.Exists(scenePath))
            {
                SetStatus($"场景已存在: {scenePath}", true);
                return;
            }

            if (!AssetDatabase.IsValidFolder(scenesPath))
                AssetDatabase.CreateFolder(demoPath, "Scenes");

            try
            {
                var renderingMode = (RenderingMode)_renderingModeGroup.value;
                SceneSetupHelper.CreateScene(scenePath, renderingMode);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                SetStatus($"✓ {sceneName} 创建成功!", false);
            }
            catch (Exception e)
            {
                SetStatus($"创建失败: {e.Message}", true);
                Debug.LogException(e);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLabel.text = message;
            _statusLabel.RemoveFromClassList("status-success");
            _statusLabel.RemoveFromClassList("status-error");
            _statusLabel.AddToClassList(isError ? "status-error" : "status-success");
        }
    }
}
