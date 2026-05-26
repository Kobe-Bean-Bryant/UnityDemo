using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Manages board operations: loading, saving, creating, deleting, and listing boards.
    /// Separates data management concerns from the UI (TaskCanvasWindow).
    /// </summary>
    public class BoardManager
    {
        private const string LastBoardPrefKey = "TaskCanvas_LastBoard";

        private KanbanBoard _currentBoard;
        private CardCache _cardCache;
        private BoardFileWatcher _fileWatcher;
        private List<string> _boardPaths = new List<string>();

        public KanbanBoard CurrentBoard => _currentBoard;
        public CardCache CardCache => _cardCache;
        public BoardFileWatcher FileWatcher => _fileWatcher;
        public IReadOnlyList<string> BoardPaths => _boardPaths;

        public event Action OnBoardChanged;
        public event Action OnBoardListChanged;
        public event Action OnExternalChangesDetected;

        public string BoardsFolder
        {
            get => TaskCanvasSettings.Instance.BoardsFolder;
            set => TaskCanvasSettings.Instance.BoardsFolder = value;
        }

        public void Initialize()
        {
            SetupFileWatcher();
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;

            _cardCache?.FlushDirtyCards();
            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                BoardSerializer.SaveBoard(_currentBoard);
            }
        }

        private void SetupFileWatcher()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = new BoardFileWatcher();
            _fileWatcher.OnAnyFileChanged += () => OnExternalChangesDetected?.Invoke();

            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                _fileWatcher.StartWatching(_currentBoard.boardFolderPath);
            }
        }

        public void PauseFileWatcher() => _fileWatcher?.Pause();
        public void ResumeFileWatcher() => _fileWatcher?.Resume();

        public List<string> RefreshBoardList()
        {
            _boardPaths.Clear();
            var names = new List<string>();

            if (Directory.Exists(BoardsFolder))
            {
                var folders = BoardSerializer.FindBoardFolders(BoardsFolder);
                foreach (var folder in folders)
                {
                    _boardPaths.Add(folder);
                    names.Add(BoardSerializer.GetBoardName(folder));
                }
            }

            var searchPaths = new[] { "Assets", "Assets/Data", "Assets/Resources" };
            foreach (var searchPath in searchPaths)
            {
                if (searchPath == BoardsFolder) continue;
                if (!Directory.Exists(searchPath)) continue;

                var folders = BoardSerializer.FindBoardFolders(searchPath);
                foreach (var folder in folders)
                {
                    if (!_boardPaths.Contains(folder))
                    {
                        _boardPaths.Add(folder);
                        names.Add(BoardSerializer.GetBoardName(folder));
                    }
                }
            }

            OnBoardListChanged?.Invoke();
            return names;
        }

        public bool LoadBoard(string folderPath)
        {
            SaveCurrentBoard();

            var board = BoardSerializer.LoadBoard(folderPath);
            if (board != null)
            {
                SetBoard(board);
                return true;
            }
            return false;
        }

        public void SetBoard(KanbanBoard board)
        {
            _currentBoard = board;
            _cardCache = board != null ? new CardCache(board.boardFolderPath) : null;

            if (_fileWatcher != null)
            {
                _fileWatcher.StopWatching();
                if (board != null && !string.IsNullOrEmpty(board.boardFolderPath))
                {
                    _fileWatcher.StartWatching(board.boardFolderPath);
                }
            }

            if (board != null)
            {
                EditorPrefs.SetString(LastBoardPrefKey, board.boardFolderPath);
            }

            OnBoardChanged?.Invoke();
        }

        public bool LoadLastBoard()
        {
            var lastBoardPath = EditorPrefs.GetString(LastBoardPrefKey, "");
            if (!string.IsNullOrEmpty(lastBoardPath) && Directory.Exists(lastBoardPath))
            {
                return LoadBoard(lastBoardPath);
            }

            if (_boardPaths.Count > 0)
            {
                return LoadBoard(_boardPaths[0]);
            }
            return false;
        }

        public void ReloadCurrentBoardFromDisk()
        {
            if (_currentBoard == null || string.IsNullOrEmpty(_currentBoard.boardFolderPath))
                return;

            if (!Directory.Exists(_currentBoard.boardFolderPath))
                return;

            _cardCache?.FlushDirtyCards();

            var reloadedBoard = BoardSerializer.LoadBoard(_currentBoard.boardFolderPath);
            if (reloadedBoard != null)
            {
                _currentBoard = reloadedBoard;
                _cardCache = new CardCache(reloadedBoard.boardFolderPath);
                OnBoardChanged?.Invoke();
            }
        }

        public KanbanBoard CreateNewBoard(string boardName, BoardTemplate template)
        {
            if (string.IsNullOrWhiteSpace(boardName))
                return null;

            if (!Directory.Exists(BoardsFolder))
            {
                Directory.CreateDirectory(BoardsFolder);
            }

            var safeName = SanitizeFolderName(boardName);
            var folderPath = Path.Combine(BoardsFolder, safeName);

            int counter = 1;
            while (Directory.Exists(folderPath))
            {
                folderPath = Path.Combine(BoardsFolder, $"{safeName}_{counter}");
                counter++;
            }

            // Pause file watcher during creation
            _fileWatcher?.Pause();

            var board = BoardSerializer.CreateNewBoard(folderPath, boardName, template);
            if (board != null)
            {
                // Use delayCall to let Unity finish processing before continuing
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.Refresh();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshBoardList();
                        SetBoard(board);
                        _fileWatcher?.Resume();
                    };
                };
            }
            else
            {
                _fileWatcher?.Resume();
            }

            return board;
        }

        public bool DeleteCurrentBoard()
        {
            if (_currentBoard == null || string.IsNullOrEmpty(_currentBoard.boardFolderPath))
                return false;

            try
            {
                var folderPath = _currentBoard.boardFolderPath;
                
                // Stop watching before deleting
                _fileWatcher?.StopWatching();

                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);

                    var metaFile = folderPath + ".meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                }

                _currentBoard = null;
                _cardCache = null;

                // Use delayCall to let Unity finish processing before continuing
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.Refresh();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshBoardList();

                        if (_boardPaths.Count > 0)
                        {
                            LoadBoard(_boardPaths[0]);
                        }
                        else
                        {
                            EditorPrefs.DeleteKey(LastBoardPrefKey);
                            OnBoardChanged?.Invoke();
                        }

                        SetupFileWatcher();
                    };
                };
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete board: {e.Message}");
                SetupFileWatcher(); // Restart watcher on error
                return false;
            }
        }

        public void SaveCurrentBoard()
        {
            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                _cardCache?.FlushDirtyCards();
                BoardSerializer.SaveBoard(_currentBoard);
            }
        }

        public bool HasDirtyCards() => _cardCache?.HasDirtyCards ?? false;

        public IEnumerable<string> GetDirtyCardIds() => _cardCache?.GetDirtyCardIds() ?? Array.Empty<string>();

        public void FlushDirtyCards() => _cardCache?.FlushDirtyCards();

        public void DiscardLocalChanges() => _cardCache?.DiscardLocalChanges();

        private string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
