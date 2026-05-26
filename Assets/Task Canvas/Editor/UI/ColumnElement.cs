using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace TaskCanvas.Editor
{
    public class ColumnElement : VisualElement
    {
        public event Action OnDataChanged;
        public event Action OnRefreshRequested;

        public KanbanColumn Column { get; private set; }
        public KanbanBoard Board { get; private set; }
        public CardCache CardCache { get; private set; }
        public int ColumnIndex { get; private set; }

        private VisualElement _header;
        private Label _titleLabel;
        private TextField _titleField;
        private Label _countLabel;
        private VisualElement _editButton;
        private ScrollView _cardsScroll;
        private VisualElement _cardsContainer;
        private VisualElement _addButton;
        private VisualElement _inlineCardContainer;
        private TextField _inlineCardField;
        private Button _inlineCardConfirmBtn;
        private bool _isCreatingCard;
        private bool _isEditing;
        private bool _potentialDrag;
        private Vector2 _dragStartPos;

        private Func<List<string>> _getActiveTagFilters;
        private Func<List<string>> _getActiveAssigneeFilters;
        private Func<int?> _getActivePriorityFilter;
        private Action<ColumnElement> _onColumnEdit;
        private Action<KanbanCard> _onCardEdit;

        private int _clickCount = 0;
        private double _lastClickTime = 0;
        private static ColumnElement _currentCreatingColumn;
        private Label _addButtonLabel;

        public ColumnElement(
            KanbanColumn column,
            KanbanBoard board,
            CardCache cardCache,
            int columnIndex,
            Func<List<string>> getActiveTagFilters = null,
            Func<List<string>> getActiveAssigneeFilters = null,
            Func<int?> getActivePriorityFilter = null,
            Action<ColumnElement> onColumnEdit = null,
            Action<KanbanCard> onCardEdit = null
        )
        {
            Column = column;
            Board = board;
            CardCache = cardCache;
            ColumnIndex = columnIndex;
            _getActiveTagFilters = getActiveTagFilters;
            _getActiveAssigneeFilters = getActiveAssigneeFilters;
            _getActivePriorityFilter = getActivePriorityFilter;
            _onColumnEdit = onColumnEdit;
            _onCardEdit = onCardEdit;

            userData = column.id;
            AddToClassList(StyleClasses.KanbanColumn);
            BuildUI();
            RefreshCards();
        }

        private void BuildUI()
        {
            _header = new VisualElement();
            _header.AddToClassList(StyleClasses.ColumnHeader);
            _header.RegisterCallback<MouseDownEvent>(OnHeaderMouseDown);
            _header.RegisterCallback<MouseMoveEvent>(OnHeaderMouseMove);
            _header.RegisterCallback<MouseUpEvent>(OnHeaderMouseUp);
            Add(_header);

            _titleLabel = new Label(Column.title);
            _titleLabel.AddToClassList(StyleClasses.ColumnTitle);
            _titleLabel.style.flexGrow = 1;
            _titleLabel.style.flexShrink = 1;
            _titleLabel.style.minWidth = 0;
            _titleLabel.style.overflow = Overflow.Hidden;
            _header.Add(_titleLabel);

            _titleField = new TextField();
            _titleField.AddToClassList(StyleClasses.ColumnTitleField);
            _titleField.style.display = DisplayStyle.None;
            _titleField.style.flexGrow = 1;
            _titleField.style.flexShrink = 1;
            _titleField.style.minWidth = 0;
            _titleField.style.overflow = Overflow.Hidden;
            _titleField.RegisterCallback<FocusOutEvent>(evt => CommitTitleEdit());
            _titleField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitTitleEdit();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelTitleEdit();
                    evt.StopPropagation();
                }
            });
            _header.Add(_titleField);

            _countLabel = new Label("0");
            _countLabel.AddToClassList(StyleClasses.ColumnCount);
            _countLabel.style.flexShrink = 0;
            _header.Add(_countLabel);

            _editButton = new VisualElement();
            _editButton.AddToClassList(StyleClasses.ColumnEditButton);
            var editLabel = new Label("✎");
            editLabel.style.fontSize = 14;
            editLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            editLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _editButton.Add(editLabel);
            _editButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                _onColumnEdit?.Invoke(this);
            });
            _editButton.RegisterCallback<MouseEnterEvent>(evt =>
                editLabel.style.color = new Color(0.9f, 0.9f, 0.9f)
            );
            _editButton.RegisterCallback<MouseLeaveEvent>(evt =>
                editLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _editButton.style.flexShrink = 0;
            _header.Add(_editButton);

            _cardsScroll = new ScrollView(ScrollViewMode.Vertical);
            _cardsScroll.AddToClassList(StyleClasses.ColumnCardsScroll);
            _cardsScroll.style.flexShrink = 0;
            Add(_cardsScroll);

            _cardsContainer = new VisualElement();
            _cardsContainer.AddToClassList(StyleClasses.ColumnCards);
            _cardsScroll.Add(_cardsContainer);

            _addButton = new VisualElement();
            _addButton.AddToClassList(StyleClasses.ColumnAddButton);
            _addButtonLabel = new Label("+ Add Card");
            _addButtonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _addButtonLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _addButton.style.backgroundColor = new Color(0, 0, 0, 0);
            _addButton.Add(_addButtonLabel);
            _addButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                StartInlineCardCreation();
            });
            _addButton.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (DragDropManager.IsDragging) return;
                _addButtonLabel.style.color = new Color(1f, 1f, 1f);
                _addButton.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            });
            _addButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                _addButtonLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                _addButton.style.backgroundColor = new Color(0, 0, 0, 0);
            });
            Add(_addButton);
            _inlineCardContainer = new VisualElement();
            _inlineCardContainer.AddToClassList(StyleClasses.InlineCardContainer);
            _inlineCardContainer.style.display = DisplayStyle.None;
            Add(_inlineCardContainer);

            _inlineCardField = new TextField();
            _inlineCardField.AddToClassList(StyleClasses.InlineCardField);
            _inlineCardField.multiline = true;
            _inlineCardField.RegisterCallback<KeyDownEvent>(OnInlineFieldKeyDown);
            _inlineCardField.RegisterCallback<FocusOutEvent>(OnInlineFieldFocusOut);
            _inlineCardContainer.Add(_inlineCardField);
            var placeholder = new Label("Enter a title for this card...");
            placeholder.AddToClassList(StyleClasses.InlineCardPlaceholder);
            placeholder.pickingMode = PickingMode.Ignore;
            _inlineCardField.Add(placeholder);

            _inlineCardField.RegisterCallback<FocusInEvent>(_ => placeholder.style.display = DisplayStyle.None);
            _inlineCardField.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            });
            var buttonsRow = new VisualElement();
            buttonsRow.AddToClassList(StyleClasses.InlineCardButtons);
            _inlineCardContainer.Add(buttonsRow);

            _inlineCardConfirmBtn = new Button(() => CommitInlineCard(true));
            _inlineCardConfirmBtn.text = "Add Card";
            _inlineCardConfirmBtn.AddToClassList(StyleClasses.InlineCardAddBtn);
            buttonsRow.Add(_inlineCardConfirmBtn);

            var cancelBtn = new Button(CancelInlineCardCreation);
            cancelBtn.text = "×";
            cancelBtn.AddToClassList(StyleClasses.InlineCardCancelBtn);
            buttonsRow.Add(cancelBtn);
        }

        private void OnHeaderMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;
            if (_editButton.Contains(evt.target as VisualElement))
                return;
            if (_isEditing)
                return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastClickTime < 0.3)
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }
            _lastClickTime = currentTime;

            if (_clickCount >= 2)
            {
                StartTitleEdit();
                _clickCount = 0;
                evt.StopPropagation();
                return;
            }

            _potentialDrag = true;
            _dragStartPos = evt.mousePosition;
            evt.StopPropagation();
        }

        private void OnHeaderMouseMove(MouseMoveEvent evt)
        {
            if (!_potentialDrag)
                return;

            var delta = evt.mousePosition - _dragStartPos;
            if (delta.magnitude > 5 && !DragDropManager.IsDragging)
            {
                _potentialDrag = false;
                DragDropManager.StartColumnDrag(this, ColumnIndex, _dragStartPos, Column.title);
            }
        }

        private void OnHeaderMouseUp(MouseUpEvent evt)
        {
            _potentialDrag = false;
        }

        private void StartTitleEdit()
        {
            if (_isEditing)
                return;
            _isEditing = true;

            _titleLabel.style.display = DisplayStyle.None;
            _titleField.style.display = DisplayStyle.Flex;
            _titleField.value = Column.title;

            schedule.Execute(() =>
            {
                _titleField.Focus();
                _titleField.SelectAll();
            });
        }

        private void CommitTitleEdit()
        {
            if (!_isEditing)
                return;
            _isEditing = false;

            var newTitle = _titleField.value.Trim();
            if (!string.IsNullOrEmpty(newTitle) && newTitle != Column.title)
            {
                Column.title = newTitle;
                _titleLabel.text = newTitle;
                OnDataChanged?.Invoke();
            }

            _titleField.style.display = DisplayStyle.None;
            _titleLabel.style.display = DisplayStyle.Flex;
        }

        private void CancelTitleEdit()
        {
            if (!_isEditing)
                return;
            _isEditing = false;

            _titleField.style.display = DisplayStyle.None;
            _titleLabel.style.display = DisplayStyle.Flex;
        }

        private void StartInlineCardCreation()
        {
            if (_isCreatingCard)
                return;
            if (_currentCreatingColumn != null && _currentCreatingColumn != this)
            {
                _currentCreatingColumn.CancelInlineCardCreation();
            }
            _currentCreatingColumn = this;
            _isCreatingCard = true;

            _addButton.style.display = DisplayStyle.None;
            _inlineCardContainer.style.display = DisplayStyle.Flex;
            _inlineCardField.value = "";

            schedule.Execute(() =>
            {
                _inlineCardField.Focus();
                
                // Scroll to the inline card container (input field) to ensure it's visible
                // Note: We don't scroll _cardsScroll because _inlineCardContainer is outside of it.
                
                var parentScroll = this.GetFirstAncestorOfType<ScrollView>();
                while (parentScroll != null)
                {
                    if (parentScroll != _cardsScroll)
                    {
                        parentScroll.ScrollTo(_inlineCardContainer);
                    }
                    parentScroll = parentScroll.GetFirstAncestorOfType<ScrollView>();
                }
            }).ExecuteLater(50);
        }

        private void OnInlineFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopImmediatePropagation();
                CommitInlineCard(true);
                return;
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                CancelInlineCardCreation();
                evt.StopPropagation();
            }
        }

        private void CommitInlineCard(bool continueCreating = false)
        {
            if (!_isCreatingCard)
                return;

            var title = _inlineCardField.value.Replace("\n", "").Replace("\r", "").Trim();
            if (!string.IsNullOrEmpty(title))
            {
                var newCard = new KanbanCard
                {
                    title = title,
                    priority = 0
                };

                var existingCards = CardCache?.GetCardsInColumn(Column.id) ?? new List<KanbanCard>();
                string lastSortKey = existingCards.Count > 0 ? existingCards[existingCards.Count - 1].sortKey : null;
                string newSortKey = FractionalIndex.After(lastSortKey);

                newCard.columnId = Column.id;
                newCard.sortKey = newSortKey;

                if (CardCache != null)
                {
                    CardCache.SetCard(newCard, Column.id, newSortKey);
                    CardCache.FlushDirtyCards();
                }

                OnDataChanged?.Invoke();
                RefreshCards(newCard.id);
            }

            if (continueCreating)
            {
                schedule.Execute(() =>
                {
                    _inlineCardField.value = "";
                    _inlineCardField.Focus();
                }).ExecuteLater(1);
            }
            else
            {
                _isCreatingCard = false;
                _inlineCardContainer.style.display = DisplayStyle.None;
                _addButton.style.display = DisplayStyle.Flex;
                if (_currentCreatingColumn == this)
                    _currentCreatingColumn = null;
            }
        }

        public void CancelInlineCardCreation()
        {
            if (!_isCreatingCard)
                return;
            _isCreatingCard = false;

            _inlineCardContainer.style.display = DisplayStyle.None;
            _addButton.style.display = DisplayStyle.Flex;

            if (_currentCreatingColumn == this)
                _currentCreatingColumn = null;
        }

        private void OnInlineFieldFocusOut(FocusOutEvent evt)
        {
            schedule.Execute(() =>
            {
                if (!_isCreatingCard)
                    return;
                    
                var focused = panel?.focusController?.focusedElement as VisualElement;
                
                if (focused != null && (_inlineCardContainer.Contains(focused) || focused == _inlineCardField))
                    return;

                if (!string.IsNullOrWhiteSpace(_inlineCardField.value))
                {
                    CommitInlineCard(false);
                }
                else
                {
                    CancelInlineCardCreation();
                }
            }).ExecuteLater(100);
        }

        public void RefreshCards(string scrollToCardId = null)
        {
            _cardsContainer.Clear();

            var activeTagFilters = _getActiveTagFilters?.Invoke() ?? new List<string>();
            var activeAssigneeFilters = _getActiveAssigneeFilters?.Invoke() ?? new List<string>();
            var activePriorityFilter = _getActivePriorityFilter?.Invoke();

            int visibleCount = 0;

            var cardsInColumn = CardCache?.GetCardsInColumn(Column.id) ?? new List<KanbanCard>();

            foreach (var card in cardsInColumn)
            {
                if (card == null)
                    continue;

                bool passesTagFilter =
                    activeTagFilters.Count == 0
                    || (card.tags != null && card.tags.Exists(t => activeTagFilters.Contains(t)));
                bool passesAssigneeFilter =
                    activeAssigneeFilters.Count == 0
                    || (card.assigneeIds != null && card.assigneeIds.Exists(a => activeAssigneeFilters.Contains(a)));
                bool passesPriorityFilter =
                    activePriorityFilter == null
                    || card.priority == activePriorityFilter.Value;

                if (!passesTagFilter || !passesAssigneeFilter || !passesPriorityFilter)
                    continue;

                var cardElement = new CardElement(card, Board, Column.id);
                cardElement.OnEditClicked += (ce) => _onCardEdit?.Invoke(ce.Card);
                cardElement.OnCompletionToggled += (ce) =>
                {
                    if (CardCache != null)
                    {
                        CardCache.SetCardContent(ce.Card);
                        CardCache.FlushDirtyCards();
                    }
                    OnDataChanged?.Invoke();
                };
                cardElement.OnDeleteClicked += (ce) =>
                {
                    CardCache?.DeleteCard(ce.Card.id);
                    OnDataChanged?.Invoke();
                    OnRefreshRequested?.Invoke();
                };
                cardElement.OnAssetDropped += (ce) =>
                {
                    if (CardCache != null)
                    {
                        CardCache.SetCardContent(ce.Card);
                        CardCache.FlushDirtyCards();
                    }
                    OnDataChanged?.Invoke();
                };

                _cardsContainer.Add(cardElement);
                visibleCount++;
            }

            _countLabel.text = visibleCount.ToString();

            if (!string.IsNullOrEmpty(scrollToCardId))
            {
                // Use EditorApplication.delayCall to ensure layout is fully updated and robust across frames
                EditorApplication.delayCall += () =>
                {
                    if (this.panel == null) return; // Safety check if element was removed

                    var targetCard = _cardsContainer.Children()
                        .OfType<CardElement>()
                        .FirstOrDefault(c => c.Card.id == scrollToCardId);

                    if (targetCard != null)
                    {
                        // 1. Scroll the column's card list to the new card
                        _cardsScroll.ScrollTo(targetCard);
                        
                        // 2. Scroll the main window (parent scroll)
                        // If we are continuously creating cards, we probably want to see the INPUT FIELD, not just the card.
                        // If the input field is visible, scroll to it. Otherwise scroll to the card.
                        var scrollTarget = (_inlineCardContainer.style.display == DisplayStyle.Flex) 
                            ? _inlineCardContainer 
                            : (VisualElement)targetCard;

                        var parentScroll = this.GetFirstAncestorOfType<ScrollView>();
                        while (parentScroll != null)
                        {
                            if (parentScroll != _cardsScroll)
                            {
                                parentScroll.ScrollTo(scrollTarget);
                            }
                            parentScroll = parentScroll.GetFirstAncestorOfType<ScrollView>();
                        }
                    }
                };
            }
        }
    }
}
