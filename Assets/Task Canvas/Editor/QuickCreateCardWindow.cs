using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class QuickCreateCardWindow : EditorWindow
    {
        private QuickCreatePanel _panel;
        private KanbanBoard _currentBoard;
        private string _lastBoardPathPref = "TaskCanvas_LastQuickBoard";

        [MenuItem("Tools/Task Canvas/Quick Create Card")]
        public static void OpenWindow()
        {
            var window = GetWindow<QuickCreateCardWindow>(true, "Quick Create Card", true);
            window.minSize = new Vector2(520, 280);
            window.maxSize = new Vector2(600, 600);
            
            if (window._panel != null)
            {
                window._panel.ResetState();
                var rect = window.position;
                rect.height = 280;
                window.position = rect;
            }
            
            var main = EditorGUIUtility.GetMainWindowPosition();
            var w = 540;
            var h = 280;
            var x = main.x + (main.width - w) / 2;
            var y = Mathf.Max(0, main.y + (main.height - h) / 2 - 100);
            window.position = new Rect(x, y, w, h);
            
            window.Show();
            window.Focus();
        }

        [Shortcut("Task Canvas/Quick Create", KeyCode.T, ShortcutModifiers.Action)]
        public static void OpenWindowShortcut()
        {
            OpenWindow();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            LoadBoard();
            BuildUI();
        }

        private void LoadBoard()
        {
            var availableBoards = GetAvailableBoards();
            if (availableBoards.Count == 0) return;

            var mainWindow = GetOpenTaskCanvasWindow();
            if (mainWindow != null && !string.IsNullOrEmpty(mainWindow.CurrentBoardPath))
            {
                var openBoardPath = mainWindow.CurrentBoardPath;
                if (availableBoards.Contains(openBoardPath))
                {
                    _currentBoard = BoardSerializer.LoadBoard(openBoardPath);
                    EditorPrefs.SetString(_lastBoardPathPref, openBoardPath);
                    return;
                }
            }

            var lastPath = EditorPrefs.GetString(_lastBoardPathPref, "");
            var targetPath = availableBoards.Contains(lastPath) ? lastPath : availableBoards[0];

            _currentBoard = BoardSerializer.LoadBoard(targetPath);
        }

        private TaskCanvasWindow GetOpenTaskCanvasWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<TaskCanvasWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        private List<string> GetAvailableBoards()
        {
            var boardsFolder = TaskCanvasWindow.BoardsFolder;
            if (!AssetDatabase.IsValidFolder(boardsFolder))
                return new List<string>();

            return AssetDatabase.GetSubFolders(boardsFolder).ToList();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            ThemeManager.ApplyTheme(rootVisualElement);

            if (_currentBoard == null)
            {
                var errLabel = new Label($"No boards found. Create a board first in the main TaskCanvas window.");
                errLabel.style.paddingTop = 20;
                errLabel.style.paddingBottom = 20;
                errLabel.style.paddingLeft = 20;
                errLabel.style.paddingRight = 20;
                errLabel.style.color = new Color(0.8f, 0.5f, 0.5f);
                rootVisualElement.Add(errLabel);
                return;
            }

            _panel = new QuickCreatePanel(
                _currentBoard,
                GetAvailableBoards(),
                OnBoardChange
            );

            _panel.OnCardCreated += SaveCardAndClose;
            _panel.OnClose += Close;
            _panel.OnOptionsToggled += OnOptionsToggled;

            rootVisualElement.Add(_panel);
        }

        private void OnOptionsToggled(bool expanded)
        {
            var rect = position;
            if (expanded)
            {
                rect.height = 480;
            }
            else
            {
                rect.height = 280;
            }
            position = rect;
        }

        private void OnBoardChange(string newPath)
        {
            EditorPrefs.SetString(_lastBoardPathPref, newPath);
            _currentBoard = BoardSerializer.LoadBoard(newPath);
            var newCol = _currentBoard?.columns?.Count > 0 ? _currentBoard.columns[0] : null;
            _panel?.SwitchBoard(_currentBoard, newCol);
        }
        
        private void SaveCardAndClose(KanbanCard card)
        {
            if (_currentBoard == null) 
            {
                Close();
                return;
            }

            var targetCol = _panel?.GetTargetColumn();
            if (targetCol == null && _currentBoard.columns?.Count > 0) 
                targetCol = _currentBoard.columns[0];
             
            if (targetCol != null)
            {
                card.columnId = targetCol.id;
                
                var cardsFolder = Path.Combine(_currentBoard.boardFolderPath, "cards");
                string lastSortKey = null;
                if (Directory.Exists(cardsFolder))
                {
                    var cardFiles = Directory.GetFiles(cardsFolder, "*.json");
                    foreach (var file in cardFiles)
                    {
                        var json = File.ReadAllText(file);
                        var existingCard = JsonUtility.FromJson<KanbanCard>(json);
                        if (existingCard != null && existingCard.columnId == targetCol.id)
                        {
                            if (lastSortKey == null || string.Compare(existingCard.sortKey, lastSortKey) > 0)
                                lastSortKey = existingCard.sortKey;
                        }
                    }
                }
                card.sortKey = FractionalIndex.After(lastSortKey);
            }
            else
            {
                Debug.LogWarning("TaskCanvas: No columns found in board.");
            }
             
            BoardSerializer.SaveCard(_currentBoard.boardFolderPath, card);
            AssetDatabase.Refresh();
             
            Close();
        }
    }
}
