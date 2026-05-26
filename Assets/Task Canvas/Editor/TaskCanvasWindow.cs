using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class TaskCanvasWindow : EditorWindow
    {
        private BoardManager _boardManager;
        
        private VisualElement _root;
        private VisualElement _toolbar;
        private FilterBar _filterBar;
        private ScrollView _mainScrollView;
        private VisualElement _columnsContainer;
        private VisualElement _emptyState;
        private DropdownField _boardDropdown;

        private List<ColumnElement> _columnElements = new List<ColumnElement>();

        private bool _externalChangesPending;
        private bool _isShowingConflictPopup;
        
        private KanbanBoard _currentBoard => _boardManager?.CurrentBoard;
        private CardCache _cardCache => _boardManager?.CardCache;
        private List<string> _boardPaths => _boardManager != null ? new List<string>(_boardManager.BoardPaths) : new List<string>();

        private static bool _skipNextFocusReload;
        
        public static void SkipNextFocusReload() => _skipNextFocusReload = true;

        private static string LastBoardPrefKey => $"TaskCanvas_LastBoard_{Application.dataPath.GetHashCode()}";
        
        public static string BoardsFolder
        {
            get => TaskCanvasSettings.Instance.BoardsFolder;
            set => TaskCanvasSettings.Instance.BoardsFolder = value;
        }

        public string CurrentBoardPath => _currentBoard?.boardFolderPath;

        [MenuItem("Window/Task Canvas")]
        public static void ShowWindow()
        {
            var window = GetWindow<TaskCanvasWindow>();
            var icon = EditorGUIUtility.IconContent("d_Profiler.UIDetails").image;
            window.titleContent = new GUIContent(" Task Canvas", icon);
            window.minSize = new Vector2(600, 400);
            window.Focus();
        }

        [Shortcut("Task Canvas/Open Window", KeyCode.T, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
        public static void OpenWindowShortcut()
        {
            ShowWindow();
        }

        [MenuItem("Tools/Task Canvas/Open Window")]
        public static void OpenWindowFromTools()
        {
            ShowWindow();
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshBoardList();
        }

        private void OnEnable()
        {
            _boardManager = new BoardManager();
            _boardManager.OnBoardChanged += OnBoardManagerBoardChanged;
            _boardManager.OnBoardListChanged += OnBoardManagerListChanged;
            _boardManager.OnExternalChangesDetected += OnFileWatcherDetectedChanges;
            _boardManager.Initialize();
            
            RefreshBoardList();
            LoadLastBoard();

            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnBoardManagerBoardChanged()
        {
            _filterBar?.SetBoard(_currentBoard);
            
            if (_root == null) return;

            // If there's an open card edit popup, update its reference
            var openPopup = _root.Q<CardEditPopup>();
            if (openPopup != null)
            {
                openPopup.UpdateBoardReference(_currentBoard, _cardCache);
            }
            
            // If there's an open assignee edit popup, update its reference
            var openAssigneePopup = _root.Q<AssigneeEditPopup>();
            if (openAssigneePopup != null)
            {
                openAssigneePopup.UpdateBoardReference(_currentBoard);
            }

            // If there's an open tag edit popup, update its reference
            var openTagPopup = _root.Q<TagEditPopup>();
            if (openTagPopup != null)
            {
                openTagPopup.UpdateBoardReference(_currentBoard);
            }
            
            UpdateBoardDropdown(); // Ensure dropdown reflects the new board
            RefreshView();
        }
        
        private void OnBoardManagerListChanged()
        {
            UpdateBoardDropdown();
        }

        private void OnFileWatcherDetectedChanges()
        {
            _externalChangesPending = true;
            EditorApplication.delayCall += HandleExternalChanges;
        }

        public void OnExternalAssetsChanged()
        {
            _externalChangesPending = true;
            EditorApplication.delayCall += HandleExternalChanges;
        }

        private void HandleExternalChanges()
        {
            if (!_externalChangesPending || _isShowingConflictPopup)
                return;

            _externalChangesPending = false;

            if (_cardCache == null || _currentBoard == null)
            {
                ReloadCurrentBoardFromDisk();
                return;
            }

            if (_cardCache.HasDirtyCards)
            {
                ShowConflictResolutionPopup();
            }
            else
            {
                ReloadCurrentBoardFromDisk();
            }
        }

        private void ShowConflictResolutionPopup()
        {
            if (_isShowingConflictPopup)
                return;

            _isShowingConflictPopup = true;

            var conflicts = new List<ConflictResolutionPopup.ConflictInfo>();
            foreach (var cardId in _cardCache.GetDirtyCardIds())
            {
                var card = _cardCache.GetCard(cardId);
                conflicts.Add(new ConflictResolutionPopup.ConflictInfo
                {
                    CardId = cardId,
                    CardTitle = card?.title ?? cardId
                });
            }

            var popup = new ConflictResolutionPopup(conflicts, resolution =>
            {
                _isShowingConflictPopup = false;

                if (resolution == ConflictResolutionPopup.Resolution.KeepLocal)
                {
                    _cardCache.FlushDirtyCards();
                    BoardSerializer.SaveBoard(_currentBoard);
                }
                else
                {
                    _cardCache.DiscardLocalChanges();
                    ReloadCurrentBoardFromDisk();
                }
            });

            _root.Add(popup);
        }

        private void OnFocus()
        {
            RefreshBoardList();
            
            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                if (!Directory.Exists(_currentBoard.boardFolderPath))
                {
                    if (_boardPaths.Count > 0)
                    {
                        LoadBoard(_boardPaths[0]);
                    }
                    else
                    {
                        RefreshView();
                    }
                    return;
                }
            }
            else if (_currentBoard == null && _boardPaths.Count > 0)
            {
                LoadLastBoard();
                return;
            }
            
            if (_skipNextFocusReload)
            {
                _skipNextFocusReload = false;
                return;
            }
            ReloadCurrentBoardFromDisk();
        }

        private void ReloadCurrentBoardFromDisk()
        {
            _boardManager?.ReloadCurrentBoardFromDisk();
        }

        private void OnDisable()
        {
            EditorApplication.quitting -= OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_boardManager != null)
            {
                _boardManager.OnBoardChanged -= OnBoardManagerBoardChanged;
                _boardManager.OnBoardListChanged -= OnBoardManagerListChanged;
                _boardManager.OnExternalChangesDetected -= OnFileWatcherDetectedChanges;
                _boardManager.Dispose();
                _boardManager = null;
            }

            DragDropManager.Dispose();
        }

        public void ForceSave()
        {
            if (_boardManager != null)
            {
                _boardManager.SaveCurrentBoard();
            }
        }

        private void OnEditorQuitting()
        {
            ForceSave();
        }

        private void OnBeforeAssemblyReload()
        {
            ForceSave();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                ForceSave();
            }
        }

        private void OnLostFocus()
        {
            AssetDatabase.Refresh();
        }

        private void BuildUI()
        {
            _root = rootVisualElement;
            _root.Clear();
            _root.AddToClassList("task-canvas-root");

            ThemeManager.ApplyTheme(_root);

            BuildToolbar();

            _filterBar = new FilterBar(_currentBoard);
            _filterBar.OnFiltersChanged += RefreshColumns;
            _filterBar.OnAddTagRequested += ShowAddTagPopup;
            _filterBar.OnAddAssigneeRequested += ShowAddAssigneePopup;
            _root.Add(_filterBar);

            _mainScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _mainScrollView.AddToClassList("main-scroll-view");
            _mainScrollView.style.flexGrow = 1;
            _root.Add(_mainScrollView);

            _columnsContainer = new VisualElement();
            _columnsContainer.AddToClassList("columns-container");
            _columnsContainer.style.alignItems = Align.FlexStart;
            _mainScrollView.Add(_columnsContainer);
            _mainScrollView.RegisterCallback<MouseDownEvent>(OnCanvasMouseDown);
            _mainScrollView.RegisterCallback<MouseMoveEvent>(OnCanvasMouseMove);
            _mainScrollView.RegisterCallback<MouseUpEvent>(OnCanvasMouseUp);

            DragDropManager.Initialize(_root, OnCardDropped, OnColumnReorder, RefreshColumns, _mainScrollView);

            _emptyState = new VisualElement();
            _emptyState.AddToClassList("empty-state");
            var emptyText = new Label(
                "No board selected.\nSelect or create a Kanban Board to get started."
            );
            emptyText.AddToClassList("empty-state-text");
            _emptyState.Add(emptyText);

            var createButton = new Button(CreateNewBoard);
            createButton.text = "+ Create New Board";
            createButton.AddToClassList("toolbar-button");
            _emptyState.Add(createButton);

            _root.Add(_emptyState);

            RefreshView();
        }

        private void BuildToolbar()
        {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("toolbar");
            _root.Add(_toolbar);

            var boardsLabel = new Label("Boards:");
            boardsLabel.AddToClassList("toolbar-label");
            _toolbar.Add(boardsLabel);

            _boardDropdown = new DropdownField();
            _boardDropdown.AddToClassList("board-dropdown");
            _boardDropdown.RegisterValueChangedCallback(evt =>
                OnBoardDropdownChanged(evt.newValue)
            );
            _toolbar.Add(_boardDropdown);

            var newBoardButton = new Button(CreateNewBoard);
            newBoardButton.text = "+ Board";
            newBoardButton.AddToClassList("toolbar-button");
            _toolbar.Add(newBoardButton);

            var deleteBoardButton = new Button(DeleteCurrentBoard);
            deleteBoardButton.text = "Delete Board";
            deleteBoardButton.AddToClassList("toolbar-button");
            _toolbar.Add(deleteBoardButton);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _toolbar.Add(spacer);

            var settingsButton = new Button(ShowSettingsPopup);
            settingsButton.text = "Settings";
            settingsButton.AddToClassList("toolbar-button");
            _toolbar.Add(settingsButton);
        }

        private void ShowSettingsPopup()
        {
            var popup = new SettingsPopup(() => {
                ApplySettings();
                RefreshColumns();
            });
            _root.Add(popup);
        }

        private void ApplySettings()
        {
            if (_columnsContainer == null) return;

            int columnWidth = TaskCanvasSettings.Instance.ColumnWidth;

            foreach (var col in _columnElements)
            {
                col.style.width = columnWidth;
                col.style.minWidth = columnWidth;
            }
        }

        private void RefreshBoardList()
        {
            var names = _boardManager?.RefreshBoardList() ?? new List<string>();
            UpdateBoardDropdown();
        }
        
        private void UpdateBoardDropdown()
        {
            if (_boardDropdown == null)
                return;

            var names = new List<string>();
            foreach (var path in _boardPaths)
            {
                names.Add(BoardSerializer.GetBoardName(path));
            }

            _boardDropdown.choices = names.Count > 0 ? names : new List<string> { "(No boards)" };

            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                var idx = _boardPaths.IndexOf(_currentBoard.boardFolderPath);
                if (idx >= 0 && idx < names.Count)
                {
                    _boardDropdown.SetValueWithoutNotify(names[idx]);
                }
                else
                {
                    _boardDropdown.SetValueWithoutNotify(null);
                }
            }
            else
            {
                _boardDropdown.SetValueWithoutNotify(null);
            }
        }

        private void DeleteCurrentBoard()
        {
            if (_currentBoard == null || string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "No board selected.", "OK");
                return;
            }

            var boardName = BoardSerializer.GetBoardName(_currentBoard.boardFolderPath);
            var columnCount = _currentBoard.columns?.Count ?? 0;

            var message = columnCount > 0
                ? $"Delete board '{boardName}' and all its {columnCount} column(s) and cards?\n\nThis cannot be undone."
                : $"Delete board '{boardName}'?\n\nThis cannot be undone.";

            if (EditorUtility.DisplayDialog("Delete Board", message, "Delete", "Cancel"))
            {
                if (_boardManager.DeleteCurrentBoard())
                {
                    _columnsContainer?.Clear();
                    _columnElements.Clear();
                    // Force a full refresh to update dropdown and empty state
                    RefreshBoardList();
                    RefreshView();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to delete board.", "OK");
                }
            }
        }

        private void OnBoardDropdownChanged(string boardName)
        {
            if (boardName == "(No boards)" || string.IsNullOrEmpty(boardName))
                return;

            var idx = _boardDropdown.choices.IndexOf(boardName);
            if (idx >= 0 && idx < _boardPaths.Count)
            {
                LoadBoard(_boardPaths[idx]);
            }
        }

        private void LoadBoard(string folderPath)
        {
            _boardManager?.LoadBoard(folderPath);
        }

        private void LoadLastBoard()
        {
            _boardManager?.LoadLastBoard();
        }

        private void RefreshView()
        {
            if (_mainScrollView == null || _filterBar == null || _emptyState == null)
                return;

            bool hasBoard = _currentBoard != null;

            _mainScrollView.style.display = hasBoard ? DisplayStyle.Flex : DisplayStyle.None;
            _filterBar.style.display = hasBoard ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyState.style.display = hasBoard ? DisplayStyle.None : DisplayStyle.Flex;

            if (hasBoard)
            {
                RefreshColumns();
            }
        }

        private void RefreshColumns()
        {
            _columnsContainer.Clear();
            _columnElements.Clear();

            if (_currentBoard == null || _currentBoard.columns == null)
                return;

            for (int i = 0; i < _currentBoard.columns.Count; i++)
            {
                var column = _currentBoard.columns[i];

                var columnElement = new ColumnElement(
                    column,
                    _currentBoard,
                    _cardCache,
                    i,
                    () => _filterBar.ActiveTagFilters,
                    () => _filterBar.ActiveAssigneeFilters,
                    () => _filterBar.ActivePriorityFilter,
                    OnColumnEdit,
                    OnCardEdit
                );

                columnElement.OnDataChanged += OnDataChanged;
                columnElement.OnRefreshRequested += () =>
                {
                    PauseFileWatcher();
                    RefreshColumns();
                    ResumeFileWatcher();
                };

                _columnsContainer.Add(columnElement);
                _columnElements.Add(columnElement);
            }

            var addColumnBtn = new Button(ShowAddColumnPopup);
            addColumnBtn.text = "+ Add Column";
            addColumnBtn.AddToClassList("add-column-button");
            _columnsContainer.Add(addColumnBtn);

            ApplySettings();
        }

        private void OnCardEdit(KanbanCard card)
        {
            ShowEditCardPopup(card);
        }

        private void OnColumnEdit(ColumnElement columnElement)
        {
            ShowEditColumnPopup(columnElement.Column);
        }

        private void OnDataChanged()
        {
            if (_currentBoard != null && !string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                BoardSerializer.SaveBoard(_currentBoard);
            }
            _cardCache?.FlushDirtyCards();
        }

        private void OnColumnReorder(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
                return;
            if (_currentBoard.columns == null)
                return;
            if (fromIndex >= _currentBoard.columns.Count || toIndex >= _currentBoard.columns.Count)
                return;

            var column = _currentBoard.columns[fromIndex];

            string beforeKey = null;
            string afterKey = null;

            if (toIndex > 0)
            {
                var idx = toIndex > fromIndex ? toIndex : toIndex - 1;
                if (idx >= 0 && idx < _currentBoard.columns.Count && idx != fromIndex)
                    beforeKey = _currentBoard.columns[idx].sortKey;
            }

            if (toIndex < _currentBoard.columns.Count - 1)
            {
                var idx = toIndex >= fromIndex ? toIndex + 1 : toIndex;
                if (idx >= 0 && idx < _currentBoard.columns.Count && idx != fromIndex)
                    afterKey = _currentBoard.columns[idx].sortKey;
            }

            column.sortKey = FractionalIndex.Between(beforeKey, afterKey);

            BoardSerializer.SaveColumn(_currentBoard.boardFolderPath, column);

            _currentBoard.columns.Sort((a, b) => string.Compare(a.sortKey ?? "", b.sortKey ?? "", StringComparison.Ordinal));

            OnDataChanged();
            RefreshColumns();
        }

        private void OnCardDropped(string cardId, string targetColumnId, int targetIndex)
        {
            if (_currentBoard == null || _cardCache == null)
                return;

            var card = _cardCache.GetCard(cardId);
            if (card == null)
                return;

            var cardsInColumn = _cardCache.GetCardsInColumn(targetColumnId);

            cardsInColumn.RemoveAll(c => c.id == cardId);

            string newSortKey;
            if (cardsInColumn.Count == 0)
            {
                newSortKey = FractionalIndex.First();
            }
            else if (targetIndex <= 0)
            {
                newSortKey = FractionalIndex.Before(cardsInColumn[0].sortKey);
            }
            else if (targetIndex >= cardsInColumn.Count)
            {
                newSortKey = FractionalIndex.After(cardsInColumn[cardsInColumn.Count - 1].sortKey);
            }
            else
            {
                var keyBefore = cardsInColumn[targetIndex - 1].sortKey;
                var keyAfter = cardsInColumn[targetIndex].sortKey;

                if (keyBefore == keyAfter)
                {
                    Debug.LogWarning($"[TaskCanvas] Detected duplicate sort keys ('{keyBefore}') during drag. Healing...");
                    keyAfter = FractionalIndex.After(keyBefore);
                    var cardAfter = cardsInColumn[targetIndex];
                    cardAfter.sortKey = keyAfter;
                    _cardCache.SetCard(cardAfter);
                }

                newSortKey = FractionalIndex.Between(keyBefore, keyAfter);
            }

            _cardCache.MoveCard(cardId, targetColumnId, newSortKey);
            _cardCache.FlushDirtyCards();

            RefreshColumns();
        }

        private void CreateNewBoard()
        {
            var popup = new BoardCreatePopup(
                (boardName, template) =>
                {
                    if (string.IsNullOrWhiteSpace(boardName))
                        return;

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

                    var board = BoardSerializer.CreateNewBoard(folderPath, boardName, template);
                    if (board != null)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            RefreshBoardList();
                            _boardManager?.SetBoard(board);
                        };
                    }
                }
            );

            _root.Add(popup);
        }

        private string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        private void ShowEditCardPopup(KanbanCard card)
        {
            var column = _currentBoard.FindColumnById(card.columnId);

            var popup = new CardEditPopup(
                card,
                _currentBoard,
                _cardCache,
                column,
                (updatedCard) =>
                {
                    if (updatedCard != null)
                    {
                        _cardCache.SetCardContent(updatedCard);
                        _cardCache.FlushDirtyCards();
                    }

                    RefreshColumns();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddColumnPopup()
        {
            ShowInlineColumnCreation();
        }

        private VisualElement _inlineColumnContainer;
        private TextField _inlineColumnField;

        private void ShowInlineColumnCreation()
        {
            if (_inlineColumnContainer != null && _inlineColumnContainer.parent != null)
            {
                _inlineColumnField?.Focus();
                return;
            }

            var addBtn = _columnsContainer.Q<Button>(className: "add-column-button");
            if (addBtn != null)
                addBtn.style.display = DisplayStyle.None;

            _inlineColumnContainer = new VisualElement();
            _inlineColumnContainer.AddToClassList("inline-column-container");
            _inlineColumnContainer.style.width = 280;
            _inlineColumnContainer.style.minWidth = 280;
            _inlineColumnContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.19f);
            _inlineColumnContainer.style.borderTopLeftRadius = 8;
            _inlineColumnContainer.style.borderTopRightRadius = 8;
            _inlineColumnContainer.style.borderBottomLeftRadius = 8;
            _inlineColumnContainer.style.borderBottomRightRadius = 8;
            _inlineColumnContainer.style.paddingTop = 12;
            _inlineColumnContainer.style.paddingBottom = 12;
            _inlineColumnContainer.style.paddingLeft = 12;
            _inlineColumnContainer.style.paddingRight = 12;

            _inlineColumnField = new TextField();
            _inlineColumnField.AddToClassList("inline-card-field");
            _inlineColumnField.style.marginBottom = 8;
            _inlineColumnContainer.Add(_inlineColumnField);

            var placeholder = new Label("Column name...");
            placeholder.AddToClassList("inline-card-placeholder");
            _inlineColumnField.Add(placeholder);
            _inlineColumnField.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            _inlineColumnContainer.Add(buttonsRow);

            var addColumnBtn = new Button(() => CreateColumnInline());
            addColumnBtn.text = "Add";
            addColumnBtn.AddToClassList("inline-card-add-btn");
            buttonsRow.Add(addColumnBtn);

            var cancelBtn = new Button(() => HideInlineColumnCreation());
            cancelBtn.text = "✕";
            cancelBtn.AddToClassList("inline-card-cancel-btn");
            buttonsRow.Add(cancelBtn);

            _columnsContainer.Add(_inlineColumnContainer);

            _inlineColumnField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CreateColumnInline();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    HideInlineColumnCreation();
                    evt.StopPropagation();
                }
            });

            _inlineColumnField.RegisterCallback<FocusOutEvent>(evt =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (_inlineColumnField == null) return;
                    var title = _inlineColumnField.value?.Trim();
                    if (!string.IsNullOrEmpty(title))
                    {
                        CreateColumnInline();
                    }
                    else
                    {
                        HideInlineColumnCreation();
                    }
                };
            });

            foreach (var col in _columnElements)
            {
                col.CancelInlineCardCreation();
            }

            EditorApplication.delayCall += () => _inlineColumnField?.Focus();
        }

        private void CreateColumnInline()
        {
            var title = _inlineColumnField?.value?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                HideInlineColumnCreation();
                return;
            }

            var column = new KanbanColumn();
            column.title = title;

            string lastSortKey = null;
            if (_currentBoard.columns != null && _currentBoard.columns.Count > 0)
            {
                lastSortKey = _currentBoard.columns[_currentBoard.columns.Count - 1].sortKey;
            }
            column.sortKey = FractionalIndex.After(lastSortKey);

            if (_currentBoard.columns == null)
            {
                _currentBoard.columns = new List<KanbanColumn>();
            }
            _currentBoard.columns.Add(column);

            if (!string.IsNullOrEmpty(_currentBoard.boardFolderPath))
            {
                PauseFileWatcher();
                BoardSerializer.SaveColumn(_currentBoard.boardFolderPath, column);
                BoardSerializer.SaveBoard(_currentBoard);
                ResumeFileWatcher();
            }

            HideInlineColumnCreation();
            RefreshColumns();
        }

        private void HideInlineColumnCreation()
        {
            _inlineColumnContainer?.RemoveFromHierarchy();
            _inlineColumnContainer = null;
            _inlineColumnField = null;

            var addBtn = _columnsContainer?.Q<Button>(className: "add-column-button");
            if (addBtn != null)
                addBtn.style.display = DisplayStyle.Flex;
        }

        private void ShowEditColumnPopup(KanbanColumn column)
        {
            var popup = new ColumnEditPopup(
                column,
                _currentBoard,
                () =>
                {
                    PauseFileWatcher();
                    if (_currentBoard.columns.Contains(column))
                    {
                        BoardSerializer.SaveColumn(_currentBoard.boardFolderPath, column);
                    }
                    OnDataChanged();
                    RefreshColumns();
                    ResumeFileWatcher();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddAssigneePopup()
        {
            if (_currentBoard == null)
            {
                EditorUtility.DisplayDialog("No Board", "Please select a board first.", "OK");
                return;
            }

            var popup = new AssigneeEditPopup(
                _currentBoard,
                () =>
                {
                    OnDataChanged();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddTagPopup()
        {
            if (_currentBoard == null)
            {
                EditorUtility.DisplayDialog("No Board", "Please select a board first.", "OK");
                return;
            }

            var popup = new TagEditPopup(
                _currentBoard,
                () =>
                {
                    OnDataChanged();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }

        private bool _isPanning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartScroll;

        private void OnCanvasMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 2)
            {
                _isPanning = true;
                _panStartMouse = evt.mousePosition;
                _panStartScroll = _mainScrollView.scrollOffset;
                evt.StopPropagation();
            }
        }

        private void OnCanvasMouseMove(MouseMoveEvent evt)
        {
            if (_isPanning)
            {
                var delta = evt.mousePosition - _panStartMouse;
                _mainScrollView.scrollOffset = _panStartScroll - delta;
            }
        }

        private void OnCanvasMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 2)
            {
                _isPanning = false;
            }
        }

        public CardCache GetCardCache() => _cardCache;
        public BoardFileWatcher GetFileWatcher() => _boardManager?.FileWatcher;

        public void PauseFileWatcher() => _boardManager?.PauseFileWatcher();
        public void ResumeFileWatcher() => _boardManager?.ResumeFileWatcher();

        public void ForceRefresh()
        {
            RefreshBoardList();
            
            if (_boardPaths.Count > 0)
            {
                LoadBoard(_boardPaths[0]);
            }
            else
            {
                RefreshView();
            }
        }
    }

    public class BoardCreatePopup : PopupBase
    {
        private TextField _nameField;
        private Action<string, BoardTemplate> _onCreate;
        private BoardTemplate _selectedTemplate = BoardTemplate.Kanban;
        private Label _previewLabel;
        private VisualElement _templateButtons;
        private Label _errorLabel;
        private Button _createBtn;

        public BoardCreatePopup(Action<string, BoardTemplate> onCreate) : base()
        {
            _onCreate = onCreate;
            ContentContainer.style.minWidth = 400;
            ContentContainer.style.maxWidth = 450;
            BuildUI();
        }

        private void BuildUI()
        {
            AddTitle("Create New Board");

            var nameGroup = CreateFieldGroup("Board Name", out _);
            _nameField = new TextField();
            _nameField.value = "My Board";
            _nameField.AddToClassList(StyleClasses.ModalTextField);
            _nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    Create();
            });
            _nameField.RegisterValueChangedCallback(evt => ValidateName(evt.newValue));
            nameGroup.Add(_nameField);

            _errorLabel = new Label();
            _errorLabel.style.color = new Color(1f, 0.4f, 0.4f);
            _errorLabel.style.fontSize = 11;
            _errorLabel.style.marginTop = 4;
            _errorLabel.style.display = DisplayStyle.None;
            nameGroup.Add(_errorLabel);
            ContentContainer.Add(nameGroup);

            var templateGroup = new VisualElement();
            templateGroup.AddToClassList(StyleClasses.ModalField);
            templateGroup.style.marginTop = 12;

            var templateLabel = new Label("Template");
            templateLabel.AddToClassList(StyleClasses.ModalFieldLabel);
            templateGroup.Add(templateLabel);

            _templateButtons = new VisualElement();
            _templateButtons.style.flexDirection = FlexDirection.Row;
            _templateButtons.style.flexWrap = Wrap.Wrap;
            _templateButtons.style.marginTop = 4;
            templateGroup.Add(_templateButtons);

            var templates = new (string name, BoardTemplate value)[]
            {
                ("Blank", BoardTemplate.Blank),
                ("Kanban", BoardTemplate.Kanban),
                ("Sprint", BoardTemplate.Sprint),
                ("Bug Tracking", BoardTemplate.BugTracking),
                ("Feature Dev", BoardTemplate.FeatureDevelopment)
            };

            foreach (var (name, value) in templates)
            {
                var btn = new Button(() => SelectTemplate(value)) { text = name };
                btn.AddToClassList("template-btn");
                btn.userData = value;
                if (value == _selectedTemplate) btn.AddToClassList("selected");
                _templateButtons.Add(btn);
            }
            ContentContainer.Add(templateGroup);

            var previewGroup = new VisualElement();
            previewGroup.style.marginTop = 12;
            previewGroup.style.paddingTop = 8;
            previewGroup.style.paddingBottom = 8;
            previewGroup.style.paddingLeft = 10;
            previewGroup.style.paddingRight = 10;
            previewGroup.style.backgroundColor = new Color(0.15f, 0.15f, 0.17f);
            previewGroup.style.borderTopLeftRadius = previewGroup.style.borderTopRightRadius = 
            previewGroup.style.borderBottomLeftRadius = previewGroup.style.borderBottomRightRadius = 4;

            var previewTitle = new Label("Columns:");
            previewTitle.style.fontSize = 11;
            previewTitle.style.color = new Color(0.6f, 0.6f, 0.6f);
            previewGroup.Add(previewTitle);

            _previewLabel = new Label();
            _previewLabel.style.fontSize = 12;
            _previewLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            _previewLabel.style.marginTop = 4;
            _previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewGroup.Add(_previewLabel);
            ContentContainer.Add(previewGroup);

            UpdatePreview();

            var buttons = CreateButtonsRow();
            buttons.style.marginTop = 16;
            buttons.Add(CreateSecondaryButton("Cancel", Close));
            _createBtn = CreatePrimaryButton("Create", Create);
            buttons.Add(_createBtn);
            ContentContainer.Add(buttons);

            RegisterCallback<AttachToPanelEvent>(_ => 
            {
                _nameField.Focus();
                ValidateName(_nameField.value);
            });
        }

        private void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _errorLabel.text = "Name cannot be empty";
                _errorLabel.style.display = DisplayStyle.Flex;
                _createBtn.SetEnabled(false);
                return;
            }

            if (BoardNameExists(name))
            {
                _errorLabel.text = "A board with this name already exists";
                _errorLabel.style.display = DisplayStyle.Flex;
                _createBtn.SetEnabled(false);
                return;
            }

            _errorLabel.style.display = DisplayStyle.None;
            _createBtn.SetEnabled(true);
        }

        private bool BoardNameExists(string name)
        {
            var boardsFolder = TaskCanvasWindow.BoardsFolder;
            if (!Directory.Exists(boardsFolder))
                return false;

            var existingBoards = BoardSerializer.FindBoardFolders(boardsFolder);
            foreach (var boardPath in existingBoards)
            {
                var existingName = BoardSerializer.GetBoardName(boardPath);
                if (string.Equals(existingName, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void SelectTemplate(BoardTemplate template)
        {
            _selectedTemplate = template;

            foreach (var child in _templateButtons.Children())
            { 
                if (child is Button btn)
                {
                    btn.RemoveFromClassList("selected");
                    if ((BoardTemplate)btn.userData == template)
                        btn.AddToClassList("selected");
                }
            }

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var columns = BoardSerializer.GetTemplateColumns(_selectedTemplate);
            if (columns.Count == 0)
            {
                _previewLabel.text = "(Empty - add columns manually)";
            }
            else
            {
                var names = new List<string>();
                foreach (var col in columns)
                    names.Add(col.title);
                _previewLabel.text = string.Join("  →  ", names);
            }
        }

        private void Create()
        {
            var name = _nameField.value?.Trim();
            if (string.IsNullOrWhiteSpace(name) || BoardNameExists(name))
                return;
                
            _onCreate?.Invoke(name, _selectedTemplate);
            Close();
        }
    }
}
