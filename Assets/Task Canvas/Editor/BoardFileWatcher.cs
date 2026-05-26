using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaskCanvas.Editor
{
    public class BoardFileWatcher : IDisposable
    {
        private FileSystemWatcher _watcher;
        private string _boardFolderPath;
        private bool _isDisposed;

        private readonly HashSet<string> _pendingContentChanges = new HashSet<string>();
        private readonly HashSet<string> _pendingPositionChanges = new HashSet<string>();
        private readonly HashSet<string> _pendingChangedColumns = new HashSet<string>();
        private bool _pendingBoardChange;
        private double _lastChangeTime;
        private const double DEBOUNCE_SECONDS = 0.2;
        private bool _updateScheduled;

        public event Action OnBoardChanged;
        public event Action<string> OnColumnChanged;
        public event Action<string> OnCardContentChanged;
        public event Action<string> OnCardPositionChanged;
        public event Action OnAnyFileChanged;

        private bool _isPaused;

        public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;
        public string WatchedPath => _boardFolderPath;
        public bool IsPaused => _isPaused;

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
            lock (_pendingContentChanges)
            {
                _pendingContentChanges.Clear();
                _pendingPositionChanges.Clear();
                _pendingChangedColumns.Clear();
                _pendingBoardChange = false;
            }
        }

        public void StartWatching(string boardFolderPath)
        {
            if (string.IsNullOrEmpty(boardFolderPath) || !Directory.Exists(boardFolderPath))
                return;

            StopWatching();

            _boardFolderPath = boardFolderPath;

            try
            {
                _watcher = new FileSystemWatcher(boardFolderPath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    Filter = "*.json",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
                _watcher.Renamed += OnFileRenamed;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TaskCanvas] Failed to start file watcher: {e.Message}");
            }
        }

        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
            }

            _pendingContentChanges.Clear();
            _pendingPositionChanges.Clear();
            _pendingChangedColumns.Clear();
            _pendingBoardChange = false;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            QueueChange(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            QueueChange(e.FullPath);
        }

        private void QueueChange(string fullPath)
        {
            if (_isPaused)
                return;

            if (string.IsNullOrEmpty(fullPath) || !fullPath.EndsWith(".json"))
                return;

            var relativePath = GetRelativePath(fullPath);
            if (string.IsNullOrEmpty(relativePath))
                return;

            lock (_pendingContentChanges)
            {
                if (relativePath == "board.json")
                {
                    _pendingBoardChange = true;
                }
                else if (relativePath.StartsWith("columns" + Path.DirectorySeparatorChar))
                {
                    var columnId = Path.GetFileNameWithoutExtension(relativePath);
                    _pendingChangedColumns.Add(columnId);
                }
                else if (relativePath.StartsWith("cards" + Path.DirectorySeparatorChar))
                {
                    var fileName = Path.GetFileName(relativePath);

                    if (fileName.EndsWith("_content.json"))
                    {
                        var cardId = fileName.Substring(0, fileName.Length - "_content.json".Length);
                        _pendingContentChanges.Add(cardId);
                    }
                    else if (fileName.EndsWith("_position.json"))
                    {
                        var cardId = fileName.Substring(0, fileName.Length - "_position.json".Length);
                        _pendingPositionChanges.Add(cardId);
                    }
                }

                _lastChangeTime = EditorApplication.timeSinceStartup;

                if (!_updateScheduled)
                {
                    _updateScheduled = true;
                    EditorApplication.delayCall += ProcessPendingChanges;
                }
            }
        }

        private void ProcessPendingChanges()
        {
            _updateScheduled = false;

            if (EditorApplication.timeSinceStartup - _lastChangeTime < DEBOUNCE_SECONDS)
            {
                EditorApplication.delayCall += ProcessPendingChanges;
                _updateScheduled = true;
                return;
            }

            List<string> contentChanges;
            List<string> positionChanges;
            List<string> changedColumns;
            bool boardChanged;

            lock (_pendingContentChanges)
            {
                contentChanges = new List<string>(_pendingContentChanges);
                positionChanges = new List<string>(_pendingPositionChanges);
                changedColumns = new List<string>(_pendingChangedColumns);
                boardChanged = _pendingBoardChange;

                _pendingContentChanges.Clear();
                _pendingPositionChanges.Clear();
                _pendingChangedColumns.Clear();
                _pendingBoardChange = false;
            }

            try
            {
                if (boardChanged)
                {
                    OnBoardChanged?.Invoke();
                }

                foreach (var columnId in changedColumns)
                {
                    OnColumnChanged?.Invoke(columnId);
                }

                foreach (var cardId in contentChanges)
                {
                    OnCardContentChanged?.Invoke(cardId);
                }

                foreach (var cardId in positionChanges)
                {
                    OnCardPositionChanged?.Invoke(cardId);
                }

                if (boardChanged || changedColumns.Count > 0 || contentChanges.Count > 0 || positionChanges.Count > 0)
                {
                    OnAnyFileChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaskCanvas] Error processing file changes: {e.Message}");
            }
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(_boardFolderPath))
                return null;

            var normalizedFull = Path.GetFullPath(fullPath);
            var normalizedBase = Path.GetFullPath(_boardFolderPath);

            if (normalizedFull.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                var relative = normalizedFull.Substring(normalizedBase.Length);
                if (relative.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    relative = relative.Substring(1);
                return relative;
            }

            return null;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            StopWatching();
        }
    }
}
