using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class TagEditPopup : PopupBase
    {
        private KanbanBoard _board;
        private Action _onSave;
        private TextField _nameField;
        private ScrollView _tagsList;

        public TagEditPopup(KanbanBoard board, Action onSave) : base()
        {
            _board = board;
            _onSave = onSave;
            BuildUI();
        }

        private void BuildUI()
        {
            AddTitle("Manage Tags");

            _tagsList = new ScrollView(ScrollViewMode.Vertical);
            _tagsList.style.maxHeight = 200;
            _tagsList.style.marginBottom = 16;
            ContentContainer.Add(_tagsList);
            RefreshTagsList();

            var newSection = new Label("Add New Tag");
            newSection.AddToClassList(StyleClasses.ModalFieldLabel);
            newSection.style.marginTop = 12;
            ContentContainer.Add(newSection);

            var nameGroup = CreateFieldGroup("Tag Name", out _);
            _nameField = new TextField();
            _nameField.AddToClassList(StyleClasses.ModalTextField);
            nameGroup.Add(_nameField);
            ContentContainer.Add(nameGroup);

            var addBtn = CreatePrimaryButton("+ Add Tag", AddTag);
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

        private void RefreshTagsList()
        {
            _tagsList.Clear();

            if (_board.allTags == null)
                return;

            foreach (var tag in _board.allTags)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;

                var tagLabel = new Label($"#{tag}");
                tagLabel.style.flexGrow = 1;
                tagLabel.AddToClassList(StyleClasses.CardTag);
                row.Add(tagLabel);

                var deleteBtn = CreateDangerButton("✕", () => DeleteTag(tag));
                deleteBtn.style.paddingTop = 2;
                deleteBtn.style.paddingRight = 6;
                deleteBtn.style.paddingBottom = 2;
                deleteBtn.style.paddingLeft = 6;
                row.Add(deleteBtn);

                _tagsList.Add(row);
            }
        }

        private void AddTag()
        {
            var tagName = _nameField.value?.Trim().TrimStart('#');
            if (string.IsNullOrWhiteSpace(tagName))
            {
                TaskCanvasWindow.SkipNextFocusReload();
                EditorUtility.DisplayDialog("Error", "Please enter a tag name.", "OK");
                return;
            }

            if (_board.allTags.Contains(tagName))
            {
                TaskCanvasWindow.SkipNextFocusReload();
                EditorUtility.DisplayDialog("Error", "This tag already exists.", "OK");
                return;
            }

            TaskCanvasWindow.SkipNextFocusReload();
            _board.AddTag(tagName);
            BoardSerializer.SaveBoard(_board);
            _nameField.value = "";

            RefreshTagsList();
            _onSave?.Invoke();
        }

        private void DeleteTag(string tag)
        {
            TaskCanvasWindow.SkipNextFocusReload();
            if (
                EditorUtility.DisplayDialog(
                    "Delete Tag",
                    $"Remove tag '#{tag}'? Cards using this tag will keep it.",
                    "Delete",
                    "Cancel"
                )
            )
            {
                _board.allTags.Remove(tag);
                BoardSerializer.SaveBoard(_board);
                RefreshTagsList();
                _onSave?.Invoke();
            }
        }
        public void UpdateBoardReference(KanbanBoard newBoard)
        {
            _board = newBoard;
        }
    }
}

