using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class SettingsPopup : PopupBase
    {
        private Action _onSettingsChanged;
        private DropdownField _themeDropdown;
        private SliderInt _columnWidthSlider;
        private Toggle _confirmDeleteToggle;

        public SettingsPopup(Action onSettingsChanged) : base()
        {
            _onSettingsChanged = onSettingsChanged;
            ContentContainer.style.minWidth = 320;
            ContentContainer.style.maxWidth = 400;
            BuildUI();
        }

        private void BuildUI()
        {
            AddTitle("Settings");

            BuildThemeSetting();
            BuildBoardsFolderSetting();
            BuildColumnWidthSetting();
            BuildConfirmDeleteSetting();
            BuildWindowShortcutSetting();
            BuildShortcutSetting();

            var buttons = CreateButtonsRow();
            buttons.Add(CreatePrimaryButton("Close", Close));
            ContentContainer.Add(buttons);
        }

        private void BuildThemeSetting()
        {
            var group = CreateFieldGroup("Theme", out _);

            var options = new List<string> { "Dark" };
            _themeDropdown = new DropdownField(options, 0);
            _themeDropdown.value = TaskCanvasSettings.Instance.Theme;
            _themeDropdown.RegisterValueChangedCallback(evt =>
            {
                TaskCanvasSettings.Instance.Theme = evt.newValue;
                _onSettingsChanged?.Invoke();
            });
            group.Add(_themeDropdown);
            ContentContainer.Add(group);
        }

        private void BuildBoardsFolderSetting()
        {
            var group = CreateFieldGroup("Boards Folder", out _);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.maxWidth = 360;
            group.Add(row);

            var textField = new TextField();
            textField.value = TaskCanvasWindow.BoardsFolder;
            textField.style.flexGrow = 1;
            textField.style.marginRight = 4;
            textField.style.maxWidth = 320;
            textField.RegisterValueChangedCallback(evt =>
            {
                HandleBoardsFolderChange(evt.previousValue, evt.newValue);
            });
            row.Add(textField);

            var browseBtn = new Button(() =>
            {
                var selected = EditorUtility.OpenFolderPanel("Select Boards Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    var dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                    {
                        var oldPath = TaskCanvasWindow.BoardsFolder;
                        var relativePath = "Assets" + selected.Substring(dataPath.Length);
                        if (relativePath != oldPath)
                        {
                            textField.SetValueWithoutNotify(relativePath);
                            HandleBoardsFolderChange(oldPath, relativePath);
                        }
                    }
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            row.Add(browseBtn);

            ContentContainer.Add(group);
        }

        private void HandleBoardsFolderChange(string oldPath, string newPath)
        {
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath) || oldPath == newPath)
            {
                TaskCanvasWindow.BoardsFolder = newPath;
                _onSettingsChanged?.Invoke();
                return;
            }

            var existingBoards = BoardSerializer.FindBoardFolders(oldPath);
            
            if (existingBoards.Count > 0)
            {
                TaskCanvasWindow.SkipNextFocusReload();
                int choice = EditorUtility.DisplayDialogComplex(
                    "Move Boards",
                    $"Found {existingBoards.Count} board(s) in the old location.\n\nDo you want to move them to the new location?",
                    "Move Boards",
                    "Cancel",
                    "Don't Move"
                );

                if (choice == 1)
                {
                    return;
                }

                if (choice == 0)
                {
                    try
                    {
                        if (!System.IO.Directory.Exists(newPath))
                        {
                            System.IO.Directory.CreateDirectory(newPath);
                        }

                        foreach (var boardFolder in existingBoards)
                        {
                            var boardName = System.IO.Path.GetFileName(boardFolder);
                            var destPath = System.IO.Path.Combine(newPath, boardName);
                            
                            int counter = 1;
                            while (System.IO.Directory.Exists(destPath))
                            {
                                destPath = System.IO.Path.Combine(newPath, $"{boardName}_{counter}");
                                counter++;
                            }

                            CopyDirectory(boardFolder, destPath);
                            System.IO.Directory.Delete(boardFolder, true);
                            
                            var metaFile = boardFolder + ".meta";
                            if (System.IO.File.Exists(metaFile))
                            {
                                System.IO.File.Delete(metaFile);
                            }
                        }
                        
                        AssetDatabase.Refresh();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to move boards: {e.Message}");
                    }
                }
            }

            TaskCanvasWindow.BoardsFolder = newPath;
            _onSettingsChanged?.Invoke();
            
            var windows = Resources.FindObjectsOfTypeAll<TaskCanvasWindow>();
            foreach (var window in windows)
            {
                window.ForceRefresh();
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            System.IO.Directory.CreateDirectory(destDir);

            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
            {
                var fileName = System.IO.Path.GetFileName(file);
                var destFile = System.IO.Path.Combine(destDir, fileName);
                System.IO.File.Copy(file, destFile, true);
            }

            foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
            {
                var dirName = System.IO.Path.GetFileName(dir);
                var destSubDir = System.IO.Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }

        private void BuildColumnWidthSetting()
        {
            var group = new VisualElement();
            group.AddToClassList(StyleClasses.ModalField);

            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.justifyContent = Justify.SpaceBetween;
            group.Add(labelRow);

            var label = new Label("Column Width");
            label.AddToClassList(StyleClasses.ModalFieldLabel);
            labelRow.Add(label);

            var valueLabel = new Label();
            valueLabel.AddToClassList(StyleClasses.ModalFieldLabel);
            labelRow.Add(valueLabel);

            int currentWidth = TaskCanvasSettings.Instance.ColumnWidth;
            valueLabel.text = $"{currentWidth}px";

            _columnWidthSlider = new SliderInt(220, 400);
            _columnWidthSlider.value = currentWidth;
            _columnWidthSlider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = $"{evt.newValue}px";
                TaskCanvasSettings.Instance.ColumnWidth = evt.newValue;
                _onSettingsChanged?.Invoke();
            });
            group.Add(_columnWidthSlider);
            ContentContainer.Add(group);
        }

        private void BuildConfirmDeleteSetting()
        {
            var group = new VisualElement();
            group.AddToClassList(StyleClasses.ModalField);
            group.style.flexDirection = FlexDirection.Row;
            group.style.justifyContent = Justify.SpaceBetween;
            group.style.alignItems = Align.Center;

            var label = new Label("Confirm Before Delete");
            label.AddToClassList(StyleClasses.ModalFieldLabel);
            group.Add(label);

            _confirmDeleteToggle = new Toggle();
            _confirmDeleteToggle.value = TaskCanvasSettings.Instance.ConfirmDelete;
            _confirmDeleteToggle.RegisterValueChangedCallback(evt =>
            {
                TaskCanvasSettings.Instance.ConfirmDelete = evt.newValue;
            });
            group.Add(_confirmDeleteToggle);
            ContentContainer.Add(group);
        }

        private void BuildWindowShortcutSetting()
        {
            var group = new VisualElement();
            group.AddToClassList(StyleClasses.ModalField);

            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
            group.Add(topRow);

            var label = new Label("Open Window Shortcut");
            label.AddToClassList(StyleClasses.ModalFieldLabel);
            topRow.Add(label);

            var binding = ShortcutManager.instance.GetShortcutBinding("Task Canvas/Open Window");
            var keyCombo = binding.ToString();
            if (string.IsNullOrEmpty(keyCombo)) keyCombo = "Unbound";

            var valueLabel = new Label(keyCombo);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new Color(0.5f, 0.8f, 1f);
            topRow.Add(valueLabel);
            ContentContainer.Add(group);
        }

        private void BuildShortcutSetting()
        {
            var group = new VisualElement();
            group.AddToClassList(StyleClasses.ModalField);

            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
            group.Add(topRow);

            var label = new Label("Quick Create Shortcut");
            label.AddToClassList(StyleClasses.ModalFieldLabel);
            topRow.Add(label);

            var binding = ShortcutManager.instance.GetShortcutBinding("Task Canvas/Quick Create");
            var keyCombo = binding.ToString();
            if (string.IsNullOrEmpty(keyCombo)) keyCombo = "Unbound";

            var valueLabel = new Label(keyCombo);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = new Color(0.5f, 0.8f, 1f);
            topRow.Add(valueLabel);

            var editBtn = new Button(() => {
                EditorApplication.ExecuteMenuItem("Edit/Shortcuts...");
            });
            editBtn.text = "Change in Shortcut Manager";
            editBtn.AddToClassList("extras-button");
            editBtn.style.marginTop = 4;
            editBtn.style.fontSize = 11;
            group.Add(editBtn);
            ContentContainer.Add(group);
        }
    }
}

