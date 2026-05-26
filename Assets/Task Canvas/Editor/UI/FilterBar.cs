using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class FilterBar : VisualElement
    {
        public event Action OnFiltersChanged;
        public event Action OnAddTagRequested;
        public event Action OnAddAssigneeRequested;

        private KanbanBoard _board;
        private List<string> _activeTagFilters = new List<string>();
        private List<string> _activeAssigneeFilters = new List<string>();
        private int? _activePriorityFilter = null;

        private VisualElement _tagsContainer;
        private VisualElement _assigneesContainer;
        private VisualElement _priorityContainer;
        private Button _clearButton;
        private bool _isExpanded = false;
        private Button _expandButton;

        public List<string> ActiveTagFilters => new List<string>(_activeTagFilters);
        public List<string> ActiveAssigneeFilters => new List<string>(_activeAssigneeFilters);
        public int? ActivePriorityFilter => _activePriorityFilter;

        public FilterBar(KanbanBoard board)
        {
            _board = board;
            AddToClassList(StyleClasses.FilterBar);
            BuildUI();
        }

        private void BuildUI()
        {
            style.flexWrap = Wrap.Wrap;
            style.alignItems = Align.Center;

            var tagsLabel = new Label("Tags:");
            tagsLabel.AddToClassList(StyleClasses.FilterLabel);
            Add(tagsLabel);

            _tagsContainer = new VisualElement();
            _tagsContainer.style.flexDirection = FlexDirection.Row;
            _tagsContainer.style.flexWrap = Wrap.Wrap;
            _tagsContainer.style.flexShrink = 1;
            Add(_tagsContainer);

            var separator = new VisualElement();
            separator.style.width = 16;
            Add(separator);

            var assigneesLabel = new Label("Assignees:");
            assigneesLabel.AddToClassList(StyleClasses.FilterLabel);
            Add(assigneesLabel);

            _assigneesContainer = new VisualElement();
            _assigneesContainer.style.flexDirection = FlexDirection.Row;
            _assigneesContainer.style.flexWrap = Wrap.Wrap;
            _assigneesContainer.style.flexShrink = 1;
            Add(_assigneesContainer);

            var separator2 = new VisualElement();
            separator2.style.width = 16;
            Add(separator2);

            var priorityLabel = new Label("Priority:");
            priorityLabel.AddToClassList(StyleClasses.FilterLabel);
            Add(priorityLabel);

            _priorityContainer = new VisualElement();
            _priorityContainer.style.flexDirection = FlexDirection.Row;
            _priorityContainer.style.flexWrap = Wrap.Wrap;
            _priorityContainer.style.flexShrink = 1;
            Add(_priorityContainer);

            _clearButton = new Button(ClearFilters);
            _clearButton.text = "✕ Clear";
            _clearButton.AddToClassList(StyleClasses.FilterChip);
            _clearButton.AddToClassList(StyleClasses.FilterChipClear);
            _clearButton.style.display = DisplayStyle.None;
            _clearButton.style.marginLeft = 8;
            Add(_clearButton);

            RefreshFilters();
        }

        public void RefreshFilters()
        {
            RefreshTags();
            RefreshAssignees();
            RefreshPriority();
            UpdateClearButtonVisibility();
        }

        private void RefreshTags()
        {
            _tagsContainer.Clear();

            if (_board == null || _board.allTags == null)
                return;

            int maxVisible = 8;
            int count = 0;

            foreach (var tag in _board.allTags)
            {
                if (count >= maxVisible && _board.allTags.Count > maxVisible + 1)
                {
                    var moreChip = new Button(ToggleExpand);
                    moreChip.text = $"+{_board.allTags.Count - maxVisible} more";
                    moreChip.AddToClassList(StyleClasses.FilterChip);
                    moreChip.AddToClassList(StyleClasses.FilterChipMore);
                    _tagsContainer.Add(moreChip);
                    break;
                }

                var chip = new Button(() => ToggleTagFilter(tag));
                chip.text = $"#{tag}";
                chip.AddToClassList(StyleClasses.FilterChip);

                if (_activeTagFilters.Contains(tag))
                    chip.AddToClassList(StyleClasses.Active);

                _tagsContainer.Add(chip);
                count++;
            }

            var addTagBtn = new Button(() => OnAddTagRequested?.Invoke());
            addTagBtn.text = "+";
            addTagBtn.AddToClassList(StyleClasses.FilterChip);
            addTagBtn.AddToClassList(StyleClasses.FilterChipAdd);
            _tagsContainer.Add(addTagBtn);
        }

        private void RefreshAssignees()
        {
            _assigneesContainer.Clear();

            if (_board == null || _board.assignees == null)
                return;

            int maxVisible = 6;
            int count = 0;

            foreach (var assignee in _board.assignees)
            {
                if (count >= maxVisible && _board.assignees.Count > maxVisible + 1)
                {
                    var moreChip = new Button(ToggleExpand);
                    moreChip.text = $"+{_board.assignees.Count - maxVisible} more";
                    moreChip.AddToClassList(StyleClasses.FilterChip);
                    moreChip.AddToClassList(StyleClasses.FilterChipMore);
                    _assigneesContainer.Add(moreChip);
                    break;
                }

                var chip = new Button(() => ToggleAssigneeFilter(assignee.id));
                chip.text = assignee.name;
                chip.AddToClassList(StyleClasses.FilterChip);
                chip.AddToClassList(StyleClasses.AssigneeChip);
                chip.style.borderLeftColor = assignee.color;

                if (_activeAssigneeFilters.Contains(assignee.id))
                    chip.AddToClassList(StyleClasses.Active);

                _assigneesContainer.Add(chip);
                count++;
            }

            var addAssigneeBtn = new Button(() => OnAddAssigneeRequested?.Invoke());
            addAssigneeBtn.text = "+";
            addAssigneeBtn.AddToClassList(StyleClasses.FilterChip);
            addAssigneeBtn.AddToClassList(StyleClasses.FilterChipAdd);
            _assigneesContainer.Add(addAssigneeBtn);
        }

        private void RefreshPriority()
        {
            _priorityContainer.Clear();

            var priorities = new (string name, int value, Color color)[] {
                ("None", 0, new Color(0.35f, 0.35f, 0.38f)),
                ("Low", 1, new Color(0.42f, 0.80f, 0.47f)),
                ("Med", 2, new Color(1f, 0.85f, 0.24f)),
                ("High", 3, new Color(1f, 0.42f, 0.42f))
            };

            foreach (var (name, value, color) in priorities)
            {
                var chip = new Button(() => TogglePriorityFilter(value));
                chip.text = name;
                chip.AddToClassList(StyleClasses.FilterChip);
                chip.style.borderLeftWidth = 3;
                chip.style.borderLeftColor = color;

                if (_activePriorityFilter == value)
                    chip.AddToClassList(StyleClasses.Active);

                _priorityContainer.Add(chip);
            }
        }

        private void TogglePriorityFilter(int priority)
        {
            if (_activePriorityFilter == priority)
                _activePriorityFilter = null;
            else
                _activePriorityFilter = priority;

            RefreshPriority();
            UpdateClearButtonVisibility();
            OnFiltersChanged?.Invoke();
        }

        private void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            RefreshFilters();
        }

        private void ToggleTagFilter(string tag)
        {
            if (_activeTagFilters.Contains(tag))
                _activeTagFilters.Remove(tag);
            else
                _activeTagFilters.Add(tag);

            RefreshTags();
            UpdateClearButtonVisibility();
            OnFiltersChanged?.Invoke();
        }

        private void ToggleAssigneeFilter(string assigneeId)
        {
            if (_activeAssigneeFilters.Contains(assigneeId))
                _activeAssigneeFilters.Remove(assigneeId);
            else
                _activeAssigneeFilters.Add(assigneeId);

            RefreshAssignees();
            UpdateClearButtonVisibility();
            OnFiltersChanged?.Invoke();
        }

        private void ClearFilters()
        {
            _activeTagFilters.Clear();
            _activeAssigneeFilters.Clear();
            _activePriorityFilter = null;
            RefreshFilters();
            OnFiltersChanged?.Invoke();
        }

        private void UpdateClearButtonVisibility()
        {
            bool hasFilters = _activeTagFilters.Count > 0 || _activeAssigneeFilters.Count > 0 || _activePriorityFilter != null;
            _clearButton.style.display = hasFilters ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBoard(KanbanBoard board)
        {
            _board = board;
            _activeTagFilters.Clear();
            _activeAssigneeFilters.Clear();
            _activePriorityFilter = null;
            RefreshFilters();
        }
    }
}
