using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class ColumnEditPopup : VisualElement
    {
        private KanbanColumn _column;
        private KanbanBoard _board;
        private Action _onSave;
        private bool _isCreateMode;

        private TextField _titleField;

        public ColumnEditPopup(KanbanColumn column, KanbanBoard board, Action onSave)
        {
            _column = column;
            _board = board;
            _onSave = onSave;
            _isCreateMode = column == null;

            if (_isCreateMode)
            {
                _column = new KanbanColumn();
                _column.title = "";
            }

            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            Add(content);

            var title = new Label(_isCreateMode ? "Create Column" : "Edit Column");
            title.AddToClassList("modal-title");
            content.Add(title);

            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("modal-field");
            content.Add(titleGroup);

            var titleLabel = new Label("Title");
            titleLabel.AddToClassList("modal-field-label");
            titleGroup.Add(titleLabel);

            _titleField = new TextField();
            _titleField.value = _column.title ?? "";
            _titleField.AddToClassList("modal-text-field");
            _titleField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Save();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            });
            titleGroup.Add(_titleField);

            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            content.Add(buttons);

            if (!_isCreateMode)
            {
                var deleteBtn = new Button(DeleteColumn);
                deleteBtn.text = "Delete";
                deleteBtn.AddToClassList("modal-button");
                deleteBtn.AddToClassList("modal-button-danger");
                buttons.Add(deleteBtn);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttons.Add(spacer);

            var cancelBtn = new Button(Close);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("modal-button");
            cancelBtn.AddToClassList("modal-button-secondary");
            buttons.Add(cancelBtn);

            var saveBtn = new Button(Save);
            saveBtn.text = _isCreateMode ? "Create" : "Save";
            saveBtn.AddToClassList("modal-button");
            saveBtn.AddToClassList("modal-button-primary");
            buttons.Add(saveBtn);

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                schedule.Execute(() =>
                {
                    _titleField.Focus();
                    if (_isCreateMode)
                    {
                        _titleField.SelectAll();
                    }
                });
            });
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_titleField.value))
            {
                TaskCanvasWindow.SkipNextFocusReload();
                EditorUtility.DisplayDialog("Error", "Please enter a column title.", "OK");
                return;
            }

            _column.title = _titleField.value.Trim();

            if (_isCreateMode)
            {
                string lastSortKey = null;
                if (_board.columns != null && _board.columns.Count > 0)
                {
                    lastSortKey = _board.columns[_board.columns.Count - 1].sortKey;
                }
                _column.sortKey = FractionalIndex.After(lastSortKey);

                if (_board.columns == null)
                {
                    _board.columns = new List<KanbanColumn>();
                }
                _board.columns.Add(_column);
            }

            if (!string.IsNullOrEmpty(_board.boardFolderPath))
            {
                BoardSerializer.SaveColumn(_board.boardFolderPath, _column);
                BoardSerializer.SaveBoard(_board);
            }

            _onSave?.Invoke();
            Close();
        }

        private void DeleteColumn()
        {
            try
            {
                var cardsInColumn = BoardSerializer.GetCardsInColumn(_board.boardFolderPath, _column.id);
                var cardCount = cardsInColumn?.Count ?? 0;
                var message =
                    cardCount > 0
                        ? $"Delete column '{_column.title}' and its {cardCount} card(s)?"
                        : $"Delete column '{_column.title}'?";

                TaskCanvasWindow.SkipNextFocusReload();
                if (EditorUtility.DisplayDialog("Delete Column", message, "Delete", "Cancel"))
                {
                    if (!string.IsNullOrEmpty(_board.boardFolderPath))
                    {
                        if (cardsInColumn != null)
                        {
                            foreach (var card in cardsInColumn)
                            {
                                BoardSerializer.DeleteCard(_board.boardFolderPath, card.id);
                            }
                        }
                        BoardSerializer.DeleteColumn(_board.boardFolderPath, _column.id);
                    }

                    _board.columns.Remove(_column);

                    if (!string.IsNullOrEmpty(_board.boardFolderPath))
                    {
                        BoardSerializer.SaveBoard(_board);
                    }

                    _onSave?.Invoke();
                    Close();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to delete column: {e.Message}");
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
