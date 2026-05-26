using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class QuickCreatePanel : VisualElement
    {
        public event Action<KanbanCard> OnCardCreated;
        public event Action OnClose;
        public event Action<bool> OnOptionsToggled;

        private KanbanBoard _board;
        private KanbanColumn _targetColumn;
        private List<string> _boardPaths;
        private Action<string> _onBoardChange;

        private TextField _titleField;
        private DropdownField _boardDropdown;
        private DropdownField _columnDropdown;
        private VisualElement _optionsContainer;
        private Button _toggleOptionsBtn;
        private Button _createBtn;
        private bool _isExpanded = false;

        private int _selectedPriority = 0;
        private List<string> _selectedTags = new List<string>();
        private List<string> _selectedAssignees = new List<string>();
        private long _dueDate = 0;
        private int _selectedDueDays = -99;
        private List<UnityAssetReference> _selectedAssets = new List<UnityAssetReference>();

        public QuickCreatePanel(
            KanbanBoard board,
            List<string> boardPaths,
            Action<string> onBoardChange
        )
        {
            _board = board;
            _boardPaths = boardPaths;
            _onBoardChange = onBoardChange;
            _targetColumn = board?.columns?.Count > 0 ? board.columns[0] : null;

            AddToClassList(StyleClasses.QuickCreatePanel);
            BuildUI();
        }

        private void BuildUI()
        {
            var header = new VisualElement();
            header.AddToClassList(StyleClasses.QuickCreateHeader);
            Add(header);

            var title = new Label("Quick Create");
            title.AddToClassList(StyleClasses.QuickCreateTitle);
            header.Add(title);

            var closeBtn = new Button(() => OnClose?.Invoke()) { text = "×" };
            closeBtn.AddToClassList(StyleClasses.QuickCreateClose);
            header.Add(closeBtn);

            var selectorsCol = new VisualElement();
            selectorsCol.AddToClassList(StyleClasses.QuickCreateSelectors);
            Add(selectorsCol);

            var boardNames = new List<string>();
            foreach (var path in _boardPaths)
                boardNames.Add(BoardSerializer.GetBoardName(path));

            _boardDropdown = new DropdownField("Board", boardNames, 0);
            _boardDropdown.AddToClassList(StyleClasses.QuickCreateDropdown);
            if (_board != null)
            {
                var idx = _boardPaths.IndexOf(_board.boardFolderPath);
                if (idx >= 0) _boardDropdown.index = idx;
            }
            _boardDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = _boardDropdown.index;
                if (idx >= 0 && idx < _boardPaths.Count)
                    _onBoardChange?.Invoke(_boardPaths[idx]);
            });
            selectorsCol.Add(_boardDropdown);

            var columnNames = new List<string>();
            if (_board?.columns != null)
            {
                foreach (var col in _board.columns)
                    columnNames.Add(col.title);
            }
            _columnDropdown = new DropdownField("Column", columnNames.Count > 0 ? columnNames : new List<string> { "(No columns)" }, 0);
            _columnDropdown.AddToClassList(StyleClasses.QuickCreateDropdown);
            _columnDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_board?.columns != null && _columnDropdown.index >= 0 && _columnDropdown.index < _board.columns.Count)
                    _targetColumn = _board.columns[_columnDropdown.index];
            });
            selectorsCol.Add(_columnDropdown);

            _titleField = new TextField();
            _titleField.AddToClassList(StyleClasses.QuickCreateInput);
            _titleField.multiline = false;
            var placeholder = new Label("Card title...");
            placeholder.AddToClassList(StyleClasses.QuickCreatePlaceholder);
            _titleField.Add(placeholder);
            _titleField.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _titleField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (!evt.shiftKey)
                    {
                        CreateCard();
                        evt.StopPropagation();
                    }
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    OnClose?.Invoke();
                    evt.StopPropagation();
                }
            });
            Add(_titleField);

            _optionsContainer = new VisualElement();
            _optionsContainer.AddToClassList(StyleClasses.QuickCreateOptions);
            _optionsContainer.style.display = DisplayStyle.None;
            Add(_optionsContainer);

            BuildOptionsContent();

            var actionsRow = new VisualElement();
            actionsRow.AddToClassList(StyleClasses.QuickCreateActions);
            Add(actionsRow);

            _toggleOptionsBtn = new Button(ToggleOptions) { text = "Options" };
            _toggleOptionsBtn.AddToClassList(StyleClasses.QuickCreateBtnSecondary);
            actionsRow.Add(_toggleOptionsBtn);

            _createBtn = new Button(CreateCard) { text = "Create Card" };
            _createBtn.AddToClassList(StyleClasses.QuickCreateBtnPrimary);
            actionsRow.Add(_createBtn);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                schedule.Execute(() => _titleField.Focus()).ExecuteLater(50);
            });
        }

        private void BuildOptionsContent()
        {
            var divider = new VisualElement();
            divider.AddToClassList(StyleClasses.QuickCreateDivider);
            _optionsContainer.Add(divider);

            var priorityRow = new VisualElement();
            priorityRow.AddToClassList(StyleClasses.QuickCreateOptionRow);
            _optionsContainer.Add(priorityRow);

            var priorityLabel = new Label("Priority");
            priorityLabel.AddToClassList(StyleClasses.QuickCreateOptionLabel);
            priorityRow.Add(priorityLabel);

            var priorityButtons = new VisualElement();
            priorityButtons.style.flexDirection = FlexDirection.Row;
            priorityRow.Add(priorityButtons);

            var priorities = new (string name, int value, Color color)[] {
                ("None", 0, new Color(0.35f, 0.35f, 0.38f)),
                ("Low", 1, new Color(0.42f, 0.80f, 0.47f)),
                ("Med", 2, new Color(1f, 0.85f, 0.24f)),
                ("High", 3, new Color(1f, 0.42f, 0.42f))
            };

            foreach (var (name, value, color) in priorities)
            {
                var btn = new Button(() => SelectPriority(value, priorityButtons)) { text = name };
                btn.AddToClassList(StyleClasses.QuickCreatePriorityBtn);
                btn.style.borderLeftWidth = 3;
                btn.style.borderLeftColor = color;
                btn.userData = value;
                if (value == _selectedPriority) btn.AddToClassList("selected");
                priorityButtons.Add(btn);
            }

            BuildTagsRow();

            BuildAssigneesRow();

            BuildDueDateRow();

            BuildUnityAssetsRow();
        }

        private void BuildTagsRow()
        {
            var row = new VisualElement();
            row.AddToClassList(StyleClasses.QuickCreateOptionRow);
            row.name = "tags-row";
            _optionsContainer.Add(row);

            var label = new Label("Tags");
            label.AddToClassList(StyleClasses.QuickCreateOptionLabel);
            row.Add(label);

            var tagsContainer = new VisualElement();
            tagsContainer.style.flexDirection = FlexDirection.Row;
            tagsContainer.style.flexWrap = Wrap.Wrap;
            tagsContainer.name = "tags-container";
            row.Add(tagsContainer);

            RefreshTags(tagsContainer);
        }

        private void RefreshTags(VisualElement container)
        {
            container.Clear();
            if (_board?.allTags == null) return;

            foreach (var tag in _board.allTags)
            {
                var btn = new Button(() => ToggleTag(tag, container)) { text = $"#{tag}" };
                btn.AddToClassList(StyleClasses.QuickCreateTag);
                if (_selectedTags.Contains(tag)) btn.AddToClassList("selected");
                container.Add(btn);
            }
        }

        private void ToggleTag(string tag, VisualElement container)
        {
            if (_selectedTags.Contains(tag))
                _selectedTags.Remove(tag);
            else
                _selectedTags.Add(tag);
            RefreshTags(container);
        }

        private void BuildAssigneesRow()
        {
            var row = new VisualElement();
            row.AddToClassList(StyleClasses.QuickCreateOptionRow);
            row.name = "assignees-row";
            _optionsContainer.Add(row);

            var label = new Label("Assignees");
            label.AddToClassList(StyleClasses.QuickCreateOptionLabel);
            row.Add(label);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.name = "assignees-container";
            row.Add(container);

            RefreshAssignees(container);
        }

        private void RefreshAssignees(VisualElement container)
        {
            container.Clear();
            if (_board?.assignees == null) return;

            foreach (var assignee in _board.assignees)
            {
                var btn = new Button(() => ToggleAssignee(assignee.id, container)) { text = assignee.name };
                btn.AddToClassList(StyleClasses.QuickCreateAssignee);
                btn.style.borderLeftWidth = 3;
                btn.style.borderLeftColor = assignee.color;
                if (_selectedAssignees.Contains(assignee.id)) btn.AddToClassList("selected");
                container.Add(btn);
            }
        }

        private void ToggleAssignee(string id, VisualElement container)
        {
            if (_selectedAssignees.Contains(id))
                _selectedAssignees.Remove(id);
            else
                _selectedAssignees.Add(id);
            RefreshAssignees(container);
        }

        private void BuildDueDateRow()
        {
            var row = new VisualElement();
            row.AddToClassList(StyleClasses.QuickCreateOptionRow);
            _optionsContainer.Add(row);

            var label = new Label("Due Date");
            label.AddToClassList(StyleClasses.QuickCreateOptionLabel);
            row.Add(label);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            row.Add(container);

            var shortcuts = new (string label, int days)[] {
                ("Today", 0), ("+3 Days", 3), ("+1 Week", 7), ("Clear", -1)
            };

            foreach (var (text, days) in shortcuts)
            {
                var btn = new Button(() => SetDueDate(days, container)) { text = text };
                btn.AddToClassList(StyleClasses.QuickCreateDateBtn);
                btn.userData = days;
                if (_selectedDueDays == days) btn.AddToClassList("selected");
                container.Add(btn);
            }
        }

        private void BuildUnityAssetsRow()
        {
            var row = new VisualElement();
            row.AddToClassList(StyleClasses.QuickCreateOptionRow);
            row.name = "assets-row";
            _optionsContainer.Add(row);

            var label = new Label("Assets");
            label.AddToClassList(StyleClasses.QuickCreateOptionLabel);
            row.Add(label);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.flexGrow = 1;
            container.name = "assets-container";
            row.Add(container);

            RefreshAssets(container);
        }

        private void RefreshAssets(VisualElement container)
        {
            container.Clear();

            foreach (var asset in _selectedAssets.ToArray())
            {
                var assetRef = asset;
                var assetName = string.IsNullOrEmpty(assetRef.assetPath) 
                    ? "(empty)" 
                    : System.IO.Path.GetFileName(assetRef.assetPath);

                var chip = new Button(() =>
                {
                    _selectedAssets.Remove(assetRef);
                    RefreshAssets(container);
                });
                chip.text = $"{assetName} ×";
                chip.AddToClassList(StyleClasses.QuickCreateTag);
                chip.AddToClassList("selected");
                chip.style.marginRight = 4;
                chip.style.marginBottom = 4;
                container.Add(chip);
            }

            var objField = new ObjectField();
            objField.objectType = typeof(UnityEngine.Object);
            objField.allowSceneObjects = true;
            objField.style.width = 150;
            objField.style.height = 20;
            objField.label = "";

            objField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null)
                {
                    var path = AssetDatabase.GetAssetPath(evt.newValue);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    
                    bool exists = false;
                    foreach (var existing in _selectedAssets)
                    {
                        if (existing.assetGUID == guid)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists && !string.IsNullOrEmpty(guid))
                    {
                        var assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                        _selectedAssets.Add(new UnityAssetReference { 
                            assetGUID = guid, 
                            assetPath = path,
                            assetName = assetName
                        });
                        RefreshAssets(container);
                    }
                    else
                    {
                        objField.value = null;
                    }
                }
            });

            container.Add(objField);
        }


        private void SetDueDate(int days, VisualElement container)
        {
            _selectedDueDays = days;
            if (days < 0)
                _dueDate = 0;
            else
                _dueDate = new DateTimeOffset(DateTime.Today.AddDays(days)).ToUnixTimeSeconds();

            foreach (var child in container.Children())
            {
                if (child is Button btn && btn.userData != null)
                {
                    btn.RemoveFromClassList("selected");
                    if ((int)btn.userData == days)
                        btn.AddToClassList("selected");
                }
            }
        }

        private void SelectPriority(int priority, VisualElement container)
        {
            _selectedPriority = priority;
            foreach (var child in container.Children())
            {
                if (child is Button btn)
                {
                    btn.RemoveFromClassList("selected");
                    if ((int)btn.userData == priority)
                        btn.AddToClassList("selected");
                }
            }
        }

        private void ToggleOptions()
        {
            _isExpanded = !_isExpanded;
            _optionsContainer.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _toggleOptionsBtn.text = _isExpanded ? "⚙ Less" : "⚙ More";
            OnOptionsToggled?.Invoke(_isExpanded);
        }

        private void CreateCard()
        {
            var title = _titleField.value?.Trim();
            if (string.IsNullOrEmpty(title)) return;

            var card = new KanbanCard
            {
                title = title,
                priority = _selectedPriority,
                tags = new List<string>(_selectedTags),
                assigneeIds = new List<string>(_selectedAssignees),
                dueDate = _dueDate,
                columnId = _targetColumn?.id ?? "",
                sortKey = FractionalIndex.After(null),
                unityAssets = new List<UnityAssetReference>(_selectedAssets)
            };

            OnCardCreated?.Invoke(card);
        }

        public void SwitchBoard(KanbanBoard newBoard, KanbanColumn targetColumn)
        {
            _board = newBoard;
            _targetColumn = targetColumn ?? (newBoard?.columns?.Count > 0 ? newBoard.columns[0] : null);

            var columnNames = new List<string>();
            if (_board?.columns != null)
            {
                foreach (var col in _board.columns)
                    columnNames.Add(col.title);
            }
            _columnDropdown.choices = columnNames.Count > 0 ? columnNames : new List<string> { "(No columns)" };
            _columnDropdown.index = 0;

            var tagsContainer = _optionsContainer.Q("tags-container");
            if (tagsContainer != null) RefreshTags(tagsContainer);

            var assigneesContainer = _optionsContainer.Q("assignees-container");
            if (assigneesContainer != null) RefreshAssignees(assigneesContainer);

            _selectedTags.Clear();
            _selectedAssignees.Clear();
        }

        public KanbanColumn GetTargetColumn() => _targetColumn;

        public void ResetState()
        {
            if (_isExpanded)
            {
                _isExpanded = false;
                _optionsContainer.style.display = DisplayStyle.None;
                _toggleOptionsBtn.text = "Options";
            }

            _titleField.value = "";
            _selectedPriority = 0;
            _selectedTags.Clear();
            _selectedAssignees.Clear();
            _selectedAssets.Clear();
            _dueDate = 0;
            _selectedDueDays = -99;
        }
    }
}
