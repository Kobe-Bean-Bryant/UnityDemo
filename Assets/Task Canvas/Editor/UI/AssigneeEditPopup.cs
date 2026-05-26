using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class AssigneeEditPopup : PopupBase
    {
        private KanbanBoard _board;
        private Action _onSave;
        private TextField _nameField;
        private ColorField _colorField;
        private ScrollView _assigneesList;

        public AssigneeEditPopup(KanbanBoard board, Action onSave) : base()
        {
            _board = board;
            _onSave = onSave;
            BuildUI();
        }

        private void BuildUI()
        {
            AddTitle("Manage Assignees");

            _assigneesList = new ScrollView(ScrollViewMode.Vertical);
            _assigneesList.style.maxHeight = 200;
            _assigneesList.style.marginBottom = 16;
            ContentContainer.Add(_assigneesList);
            RefreshAssigneesList();

            var newSection = new Label("Add New Assignee");
            newSection.AddToClassList(StyleClasses.ModalFieldLabel);
            newSection.style.marginTop = 12;
            ContentContainer.Add(newSection);

            var nameGroup = CreateFieldGroup("Name", out _);
            _nameField = new TextField();
            _nameField.AddToClassList(StyleClasses.ModalTextField);
            nameGroup.Add(_nameField);
            ContentContainer.Add(nameGroup);

            var colorGroup = CreateFieldGroup("Color", out _);
            _colorField = new ColorField();
            _colorField.value = new Color(0.4f, 0.6f, 1f);
            colorGroup.Add(_colorField);
            ContentContainer.Add(colorGroup);

            var addBtn = CreatePrimaryButton("+ Add Assignee", AddAssignee);
            addBtn.style.marginTop = 8;
            ContentContainer.Add(addBtn);

            var buttons = CreateButtonsRow();
            buttons.style.marginTop = 16;
            buttons.Add(CreateSecondaryButton("Done", Close));
            ContentContainer.Add(buttons);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                schedule.Execute(() => _nameField.Focus());
            });
        }

        private void RefreshAssigneesList()
        {
            _assigneesList.Clear();

            foreach (var assignee in _board.assignees)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;

                var colorBox = new VisualElement();
                colorBox.style.width = 16;
                colorBox.style.height = 16;
                colorBox.style.borderTopLeftRadius = 8;
                colorBox.style.borderTopRightRadius = 8;
                colorBox.style.borderBottomLeftRadius = 8;
                colorBox.style.borderBottomRightRadius = 8;
                colorBox.style.backgroundColor = assignee.color;
                colorBox.style.marginRight = 8;
                row.Add(colorBox);

                var nameLabel = new Label(assignee.name);
                nameLabel.style.flexGrow = 1;
                row.Add(nameLabel);

                var deleteBtn = CreateDangerButton("✕", () => DeleteAssignee(assignee));
                deleteBtn.style.paddingTop = 2;
                deleteBtn.style.paddingRight = 6;
                deleteBtn.style.paddingBottom = 2;
                deleteBtn.style.paddingLeft = 6;
                row.Add(deleteBtn);

                _assigneesList.Add(row);
            }
        }

        private void AddAssignee()
        {
            if (string.IsNullOrWhiteSpace(_nameField.value))
            {
                TaskCanvasWindow.SkipNextFocusReload();
                EditorUtility.DisplayDialog("Error", "Please enter a name.", "OK");
                return;
            }

            TaskCanvasWindow.SkipNextFocusReload();
            var assignee = new Assignee(_nameField.value.Trim());
            assignee.color = _colorField.value;
            _board.AddAssignee(assignee);
            BoardSerializer.SaveBoard(_board);

            _nameField.value = "";
            _colorField.value = new Color(0.4f, 0.6f, 1f);

            RefreshAssigneesList();
            _onSave?.Invoke();
        }

        private void DeleteAssignee(Assignee assignee)
        {
            TaskCanvasWindow.SkipNextFocusReload();
            if (
                EditorUtility.DisplayDialog(
                    "Delete Assignee",
                    $"Remove '{assignee.name}'? Cards will keep their assignments.",
                    "Delete",
                    "Cancel"
                )
            )
            {
                _board.assignees.Remove(assignee);
                BoardSerializer.SaveBoard(_board);
                RefreshAssigneesList();
                _onSave?.Invoke();
            }
        }
        public void UpdateBoardReference(KanbanBoard newBoard)
        {
            _board = newBoard;
        }
    }
}

