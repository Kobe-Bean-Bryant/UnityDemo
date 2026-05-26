using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class CardEditPopup : VisualElement
    {
        private KanbanCard _card;
        private KanbanBoard _board;
        private CardCache _cardCache;
        private KanbanColumn _targetColumn;
        private Action<KanbanCard> _onSave;
        private bool _isCreateMode;
        private TextField _titleField;
        private TextField _descriptionField;
        private VisualElement _prioritySelector;
        private int _selectedPriority;
        private long _selectedDueDate;
        private Label _dueDateDisplay;
        private int _trackedSeconds;
        private long _timerStartedAt;
        private Label _trackedLabel;
        private VisualElement _checklistContainer;
        private List<ChecklistItem> _checklistItems = new List<ChecklistItem>();
        private VisualElement _linksContainer;
        private List<CardAttachment> _links = new List<CardAttachment>();
        private VisualElement _unityAssetsContainer;
        private List<UnityAssetReference> _unityAssets = new List<UnityAssetReference>();
        private ScrollView _commentsScroll;
        private TextField _newCommentField;
        private List<CardComment> _comments = new List<CardComment>();
        private VisualElement _tagsContainer;
        private VisualElement _assigneesContainer;
        private List<string> _selectedTags = new List<string>();
        private List<string> _selectedAssignees = new List<string>();
        private string _coverImageGUID;
        private string _coverImagePath;
        private Image _coverPreview;
        
        private bool _isStandalone;
        private bool _isExpanded;
        private VisualElement _leftColContainer;
        private VisualElement _rightExtrasContainer;
        private Button _expandBtn;
        private List<string> _availableBoards;
        private Action<string> _onBoardChanged;
        private DropdownField _boardDropdown;
        private DropdownField _columnDropdown;

        public CardEditPopup(KanbanCard card, KanbanBoard board, CardCache cardCache, KanbanColumn targetColumn, Action<KanbanCard> onSave, bool isStandalone = false, List<string> availableBoards = null, Action<string> onBoardChanged = null)
        {
            _card = card;
            _board = board;
            _cardCache = cardCache;
            _targetColumn = targetColumn;
            _onSave = onSave;
            _isCreateMode = false;
            _isStandalone = isStandalone;
            _availableBoards = availableBoards;
            _onBoardChanged = onBoardChanged;
            
            _isExpanded = !isStandalone;
            
            if (card == null) return; 

            _selectedPriority = _card.priority;
            _selectedTags = new List<string>(_card.tags ?? new List<string>());
            _selectedAssignees = new List<string>(_card.assigneeIds ?? new List<string>());
            _selectedDueDate = _card.dueDate;
            _checklistItems = new List<ChecklistItem>(_card.checklist ?? new List<ChecklistItem>());
            if (_card.attachments != null)
                foreach (var a in _card.attachments)
                    if (a.type == AttachmentType.Link) _links.Add(a);
            _unityAssets = new List<UnityAssetReference>(_card.unityAssets ?? new List<UnityAssetReference>());
            _comments = new List<CardComment>(_card.comments ?? new List<CardComment>());
            _trackedSeconds = _card.trackedSeconds;
            _timerStartedAt = _card.timerStartedAt;
            _coverImageGUID = _card.coverImageGUID;
            _coverImagePath = _card.coverImagePath;

            AddToClassList("modal-overlay");
            BuildUI();
            EditorApplication.update += UpdateTimer;
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            content.style.width = new Length(70, LengthUnit.Percent);
            content.style.minWidth = 600;
            content.style.maxWidth = 1200;
            content.style.maxHeight = new Length(95, LengthUnit.Percent);
            Add(content);
            
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.flexGrow = 1;
            mainRow.style.overflow = Overflow.Hidden;
            content.Add(mainRow);
            var leftScroll = new ScrollView(ScrollViewMode.Vertical);
            leftScroll.style.flexGrow = 1;
            leftScroll.style.flexShrink = 1;
            leftScroll.style.flexBasis = new Length(50, LengthUnit.Percent);
            leftScroll.style.minWidth = 280;
            leftScroll.style.maxWidth = new Length(50, LengthUnit.Percent);
            leftScroll.style.marginRight = 12;
            mainRow.Add(leftScroll);
            var leftCol = leftScroll.contentContainer;
            leftCol.style.overflow = Overflow.Hidden;

            BuildTitleField(leftCol);
            
            var leftExtras = new VisualElement();
            leftCol.Add(leftExtras);
            _leftColContainer = leftExtras;
            if (!_isExpanded) _leftColContainer.style.display = DisplayStyle.None;

            BuildDescriptionField(leftExtras);
            BuildChecklistSection(leftExtras);
            BuildCommentsSection(leftExtras);

            var rightScroll = new ScrollView(ScrollViewMode.Vertical);
            rightScroll.style.flexGrow = 1;
            rightScroll.style.flexShrink = 1;
            rightScroll.style.flexBasis = new Length(50, LengthUnit.Percent);
            rightScroll.style.minWidth = 280;
            mainRow.Add(rightScroll);
            var rightCol = rightScroll.contentContainer;

            if (_isStandalone)
            {
                BuildStandaloneSelectors(rightCol);
                
                _expandBtn = new Button(ToggleExpand);
                _expandBtn.text = _isExpanded ? "Collapse Details" : "Expand Details (+)";
                _expandBtn.AddToClassList("modal-button");
                _expandBtn.style.marginBottom = 12;
                rightCol.Add(_expandBtn);
            }

            _rightExtrasContainer = new VisualElement();
            rightCol.Add(_rightExtrasContainer);
            if (!_isExpanded) _rightExtrasContainer.style.display = DisplayStyle.None;

            BuildCoverImageSection(_rightExtrasContainer);
            BuildPrioritySection(_rightExtrasContainer);
            BuildDueDateSection(_rightExtrasContainer);
            BuildTimeTrackingSection(_rightExtrasContainer);
            BuildLinksSection(_rightExtrasContainer);
            BuildUnityAssetsSection(_rightExtrasContainer);

            BuildTagsSection(_rightExtrasContainer);
            BuildAssigneesSection(_rightExtrasContainer);

            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            buttons.style.marginTop = 12;
            content.Add(buttons);

            var deleteBtn = new Button(DeleteCard) { text = "Delete" };
            deleteBtn.AddToClassList("modal-button");
            deleteBtn.AddToClassList("modal-button-danger");
            buttons.Add(deleteBtn);

            buttons.Add(new VisualElement { style = { flexGrow = 1 } });

            var closeBtn = new Button(SaveAndClose) { text = "Close" };
            closeBtn.AddToClassList("modal-button");
            closeBtn.AddToClassList("modal-button-primary");
            buttons.Add(closeBtn);

            RegisterCallback<ClickEvent>(e => { if (e.target == this) SaveAndClose(); });
            RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Escape)
                {
                    SaveAndClose();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Return)
                {
                    bool shouldSubmit = e.ctrlKey || e.commandKey || _isStandalone;

                    if (shouldSubmit)
                    {
                        var tf = focusController.focusedElement as TextField;
                        
                        if (tf != null && tf.multiline && !e.ctrlKey && !e.commandKey && tf != _titleField)
                        {
                            return;
                        }

                        SaveAndClose();
                        e.StopPropagation();
                    }
                }
            });
            RegisterCallback<AttachToPanelEvent>(_ => { _titleField.Focus(); focusable = true; Focus(); });
        }

        #region Left Column

        private void BuildTitleField(VisualElement parent)
        {
            AddLabel(parent, "Title");
            _titleField = new TextField { multiline = true, value = _card.title ?? "" };
            _titleField.AddToClassList("modal-text-field");
            _titleField.style.whiteSpace = WhiteSpace.Normal;
            _titleField.style.overflow = Overflow.Hidden;
            _titleField.style.fontSize = 16;
            _titleField.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(_titleField);
        }

        private void BuildDescriptionField(VisualElement parent)
        {
            AddLabel(parent, "Description", 8);
            _descriptionField = new TextField { multiline = true, value = _card.description ?? "" };
            _descriptionField.AddToClassList("modal-text-field");
            _descriptionField.style.minHeight = 50;
            _descriptionField.style.whiteSpace = WhiteSpace.Normal;
            _descriptionField.style.overflow = Overflow.Hidden;
            parent.Add(_descriptionField);
        }

        private void BuildChecklistSection(VisualElement parent)
        {
            AddLabel(parent, "Checklist", 12);
            _checklistContainer = new VisualElement { style = { marginTop = 4 } };
            parent.Add(_checklistContainer);
            RefreshChecklist();
        }

        private void BuildCommentsSection(VisualElement parent)
        {
            AddLabel(parent, "Comments", 12);

            _commentsScroll = new ScrollView(ScrollViewMode.Vertical);
            _commentsScroll.style.maxHeight = 120;
            _commentsScroll.style.marginBottom = 4;
            parent.Add(_commentsScroll);
            RefreshComments();

            var inputRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            parent.Add(inputRow);

            _newCommentField = new TextField();
            _newCommentField.style.flexGrow = 1;
            _newCommentField.AddToClassList("modal-text-field");
            _newCommentField.style.whiteSpace = WhiteSpace.Normal;
            _newCommentField.style.overflow = Overflow.Hidden;
            _newCommentField.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Return && !e.shiftKey) { AddComment(); e.StopPropagation(); }
            });
            inputRow.Add(_newCommentField);

            var sendBtn = new Button(AddComment) { text = "Send" };
            sendBtn.style.marginLeft = 4;
            inputRow.Add(sendBtn);
        }

        #endregion
        
        private void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            _expandBtn.text = _isExpanded ? "Collapse Details" : "Expand Details (+)";
            
            if (_leftColContainer != null)
                _leftColContainer.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_rightExtrasContainer != null)
                _rightExtrasContainer.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #region Right Column

        private void BuildPrioritySection(VisualElement parent)
        {
            AddLabel(parent, "Priority");
            _prioritySelector = new VisualElement();
            _prioritySelector.AddToClassList("priority-selector");
            parent.Add(_prioritySelector);

            AddPriorityOption("None", 0, "priority-none");
            AddPriorityOption("Low", 1, "priority-low");
            AddPriorityOption("Medium", 2, "priority-medium");
            AddPriorityOption("High", 3, "priority-high");
        }

        private void BuildDueDateSection(VisualElement parent)
        {
            AddLabel(parent, "Due Date", 12);

            _dueDateDisplay = new Label(GetDueDateText());
            _dueDateDisplay.style.marginBottom = 6;
            UpdateDueDateColor();
            parent.Add(_dueDateDisplay);

            var dateButton = new Button(ShowCalendarPopup);
            dateButton.text = _selectedDueDate > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(_selectedDueDate).LocalDateTime.ToString("MMM d, yyyy")
                : "Select Date...";
            dateButton.AddToClassList("due-date-button");
            dateButton.name = "due-date-button";
            parent.Add(dateButton);

            var shortcutsRow = new VisualElement();
            shortcutsRow.style.flexDirection = FlexDirection.Row;
            shortcutsRow.style.marginTop = 8;
            shortcutsRow.style.flexWrap = Wrap.Wrap;
            parent.Add(shortcutsRow);

            AddDateShortcut(shortcutsRow, "Today", 0);
            AddDateShortcut(shortcutsRow, "Tomorrow", 1);
            AddDateShortcut(shortcutsRow, "+3d", 3);
            AddDateShortcut(shortcutsRow, "+1w", 7);

            var clearBtn = new Button(() => {
                _selectedDueDate = 0;
                _dueDateDisplay.text = "No due date";
                _dueDateDisplay.style.color = new Color(0.5f, 0.5f, 0.5f);
                UpdateDateButton();
            });
            clearBtn.text = "Clear";
            clearBtn.AddToClassList("date-shortcut-btn");
            shortcutsRow.Add(clearBtn);
        }

        private void ShowCalendarPopup()
        {
            DateTime? currentDate = _selectedDueDate > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(_selectedDueDate).LocalDateTime 
                : (DateTime?)null;
            
            var popup = new CalendarPopup(currentDate, selectedDate =>
            {
                if (selectedDate.HasValue)
                {
                    _selectedDueDate = new DateTimeOffset(selectedDate.Value).ToUnixTimeSeconds();
                }
                else
                {
                    _selectedDueDate = 0;
                }
                _dueDateDisplay.text = GetDueDateText();
                UpdateDueDateColor();
                UpdateDateButton();
            });
            
            this.Add(popup);
        }

        private void UpdateDateButton()
        {
            var dateButton = this.Q<Button>("due-date-button");
            if (dateButton != null)
            {
                dateButton.text = _selectedDueDate > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(_selectedDueDate).LocalDateTime.ToString("MMM d, yyyy")
                    : "Select Date...";
            }
        }

        private void BuildTimeTrackingSection(VisualElement parent)
        {
            AddLabel(parent, "Time Tracked", 12);
            var trackerBox = new VisualElement();
            trackerBox.style.backgroundColor = new Color(0.15f, 0.15f, 0.17f);
            trackerBox.style.borderTopLeftRadius = trackerBox.style.borderTopRightRadius = trackerBox.style.borderBottomLeftRadius = trackerBox.style.borderBottomRightRadius = 4;
            trackerBox.style.paddingTop = trackerBox.style.paddingBottom = 8;
            trackerBox.style.paddingLeft = trackerBox.style.paddingRight = 12;
            trackerBox.style.flexDirection = FlexDirection.Row;
            trackerBox.style.alignItems = Align.Center;
            trackerBox.style.justifyContent = Justify.SpaceBetween;
            trackerBox.style.marginBottom = 8;
            parent.Add(trackerBox);

            int displaySeconds = _trackedSeconds;
            if (_timerStartedAt > 0)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                displaySeconds += (int)(now - _timerStartedAt);
            }

            _trackedLabel = new Label(FormatTracked(displaySeconds));
            _trackedLabel.style.fontSize = 16;
            _trackedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _trackedLabel.style.color = new Color(0.8f, 0.9f, 0.8f);
            trackerBox.Add(_trackedLabel);

            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            trackerBox.Add(btnRow);

            var timerBtn = new Button(ToggleTimer) { text = _timerStartedAt > 0 ? "⏸" : "▶" };
            timerBtn.name = "timer-button";
            timerBtn.style.fontSize = 14;
            timerBtn.style.width = 28;
            timerBtn.style.height = 28;
            btnRow.Add(timerBtn);

            var resetBtn = new Button(() => { 
                _trackedSeconds = 0; 
                _timerStartedAt = 0;
                _trackedLabel.text = FormatTracked(0);
                var btn = this.Q<Button>("timer-button");
                if (btn != null) btn.text = "▶";
            }) { text = "↺" };
            resetBtn.style.width = 28;
            resetBtn.style.height = 28;
            resetBtn.style.marginLeft = 4;
            btnRow.Add(resetBtn);
        }

        private void BuildLinksSection(VisualElement parent)
        {
            AddLabel(parent, "Links", 8);
            _linksContainer = new VisualElement { style = { marginBottom = 8 } };
            parent.Add(_linksContainer);
            RefreshLinks();
        }

        private void BuildUnityAssetsSection(VisualElement parent)
        {
            AddLabel(parent, "Unity Assets", 8);
            _unityAssetsContainer = new VisualElement { style = { marginBottom = 8 } };
            parent.Add(_unityAssetsContainer);
            RefreshUnityAssets();
        }

        private void BuildCoverImageSection(VisualElement parent)
        {
            AddLabel(parent, "Cover Image");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;
            parent.Add(row);

            var objField = new ObjectField();
            objField.objectType = typeof(Texture2D);
            objField.allowSceneObjects = false;
            objField.style.flexGrow = 1;

            // Pre-fill with current cover image if any
            Texture2D currentTex = null;
            if (!string.IsNullOrEmpty(_coverImageGUID))
            {
                var p = AssetDatabase.GUIDToAssetPath(_coverImageGUID);
                if (!string.IsNullOrEmpty(p))
                    currentTex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            if (currentTex == null && !string.IsNullOrEmpty(_coverImagePath))
                currentTex = AssetDatabase.LoadAssetAtPath<Texture2D>(_coverImagePath);

            objField.value = currentTex;

            // Small preview thumbnail
            _coverPreview = new Image();
            _coverPreview.scaleMode = ScaleMode.ScaleAndCrop;
            _coverPreview.style.width = 48;
            _coverPreview.style.height = 30;
            _coverPreview.style.marginLeft = 6;
            _coverPreview.style.borderTopLeftRadius = 3;
            _coverPreview.style.borderTopRightRadius = 3;
            _coverPreview.style.borderBottomLeftRadius = 3;
            _coverPreview.style.borderBottomRightRadius = 3;
            _coverPreview.image = currentTex;
            _coverPreview.style.display = currentTex != null ? DisplayStyle.Flex : DisplayStyle.None;

            objField.RegisterValueChangedCallback(e =>
            {
                var tex = e.newValue as Texture2D;
                if (tex != null)
                {
                    var path = AssetDatabase.GetAssetPath(tex);
                    _coverImageGUID = AssetDatabase.AssetPathToGUID(path);
                    _coverImagePath = path;
                    _coverPreview.image = tex;
                    _coverPreview.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _coverImageGUID = null;
                    _coverImagePath = null;
                    _coverPreview.image = null;
                    _coverPreview.style.display = DisplayStyle.None;
                }
            });

            row.Add(objField);
            row.Add(_coverPreview);

            var clearBtn = new Button(() =>
            {
                objField.value = null;
                _coverImageGUID = null;
                _coverImagePath = null;
                _coverPreview.image = null;
                _coverPreview.style.display = DisplayStyle.None;
            });
            clearBtn.text = "✕";
            clearBtn.tooltip = "Clear cover image";
            clearBtn.AddToClassList("item-delete-btn");
            clearBtn.style.marginLeft = 4;
            row.Add(clearBtn);
        }

        private void BuildTagsSection(VisualElement parent)
        {
            AddLabel(parent, "Tags", 8);
            _tagsContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 8 } };
            parent.Add(_tagsContainer);
            RefreshTags();
        }

        private void BuildAssigneesSection(VisualElement parent)
        {
            AddLabel(parent, "Assignees", 8);
            _assigneesContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            parent.Add(_assigneesContainer);
            RefreshAssignees();
        }

        private void OpenAssigneePopup()
        {
            var popup = new AssigneeEditPopup(_board, () => {
                BoardSerializer.SaveBoard(_board);
                RefreshAssignees();
            });
            this.Add(popup);
        }

        #endregion

        #region Refresh Methods

        private void RefreshChecklist()
        {
            _checklistContainer.Clear();
            foreach (var item in _checklistItems)
            {
                var row = new VisualElement();
                row.AddToClassList("checklist-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var check = new Toggle { value = item.isCompleted };
                check.RegisterValueChangedCallback(e => item.isCompleted = e.newValue);
                check.style.marginRight = 4;
                row.Add(check);

                var field = new TextField { value = item.text };
                field.style.width = 260;
                var captured = item;
                field.RegisterValueChangedCallback(e => captured.text = e.newValue);
                row.Add(field);

                var del = new Button(() => { _checklistItems.Remove(item); RefreshChecklist(); }) { text = "x" };
                del.AddToClassList("item-delete-btn");
                del.style.marginLeft = 4;
                row.Add(del);

                _checklistContainer.Add(row);
            }
            var addBtn = new Button(() => { _checklistItems.Add(new ChecklistItem("")); RefreshChecklist(); }) { text = "+ Add Item" };
            addBtn.AddToClassList("add-chip");
            addBtn.style.marginTop = 4;
            _checklistContainer.Add(addBtn);
        }

        private void RefreshComments()
        {
            _commentsScroll.Clear();
            foreach (var c in _comments)
            {
                var el = new Label($"• {c.text}");
                el.style.fontSize = 11;
                el.style.color = new Color(0.75f, 0.75f, 0.75f);
                el.style.whiteSpace = WhiteSpace.Normal;
                el.style.marginBottom = 2;
                _commentsScroll.Add(el);
            }
        }

        private void RefreshLinks()
        {
            _linksContainer.Clear();
            foreach (var link in _links)
            {
                var row = new VisualElement();
                row.AddToClassList("link-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var urlField = new TextField { value = link.url };
                urlField.style.width = 220;
                var capturedLink = link;
                urlField.RegisterValueChangedCallback(e => {
                    capturedLink.url = e.newValue;
                    if (string.IsNullOrEmpty(capturedLink.name) && !string.IsNullOrEmpty(e.newValue))
                        capturedLink.name = e.newValue.Length > 30 ? e.newValue.Substring(0, 30) + "..." : e.newValue;
                });
                row.Add(urlField);

                var linkBtn = new Button(() => {
                    var url = capturedLink.url;
                    if (!string.IsNullOrEmpty(url))
                    {
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                            url = "https://" + url;
                        Application.OpenURL(url);
                    }
                }) { text = "Go" };
                linkBtn.AddToClassList("item-action-btn");
                row.Add(linkBtn);

                var del = new Button(() => { _links.Remove(link); RefreshLinks(); }) { text = "x" };
                del.AddToClassList("item-delete-btn");
                del.style.marginLeft = 8;
                row.Add(del);

                _linksContainer.Add(row);
            }
            var addBtn = new Button(() => { _links.Add(new CardAttachment("", "", AttachmentType.Link)); RefreshLinks(); }) { text = "+ Add Link" };
            addBtn.AddToClassList("add-chip");
            addBtn.style.marginTop = 4;
            _linksContainer.Add(addBtn);
        }

        private void RefreshUnityAssets()
        {
            _unityAssetsContainer.Clear();
            for (int i = 0; i < _unityAssets.Count; i++)
            {
                int index = i;
                var assetRef = _unityAssets[i];
                var container = new VisualElement();
                container.AddToClassList("asset-row");
                
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                container.Add(row);

                var objField = new ObjectField();
                objField.objectType = typeof(UnityEngine.Object);
                objField.allowSceneObjects = true;
                objField.style.width = 270;

                UnityEngine.Object loadedObject = null;
                bool isMissing = false;

                if (!string.IsNullOrEmpty(assetRef.assetPath))
                {
                    loadedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetRef.assetPath);
                    if (loadedObject == null && !string.IsNullOrEmpty(assetRef.assetGUID))
                    {
                        var pathFromGuid = AssetDatabase.GUIDToAssetPath(assetRef.assetGUID);
                        if (!string.IsNullOrEmpty(pathFromGuid))
                            loadedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathFromGuid);
                    }
                }
                else if (assetRef.isSceneObject && !string.IsNullOrEmpty(assetRef.objectPath))
                {
                    loadedObject = FindSceneObject(assetRef.scenePath, assetRef.objectPath);
                }

                if (loadedObject != null)
                {
                    objField.value = loadedObject;
                }
                else if (!string.IsNullOrEmpty(assetRef.assetName))
                {
                    isMissing = true;
                }

                objField.RegisterValueChangedCallback(e => {
                    if (e.newValue != null)
                    {
                        var path = AssetDatabase.GetAssetPath(e.newValue);
                        assetRef.assetName = e.newValue.name;
                        assetRef.assetTypeName = e.newValue.GetType().Name;
                        assetRef.isSceneObject = string.IsNullOrEmpty(path);

                        if (!assetRef.isSceneObject)
                        {
                            assetRef.assetGUID = AssetDatabase.AssetPathToGUID(path);
                            assetRef.assetPath = path;
                            assetRef.scenePath = null;
                            assetRef.objectPath = null;
                        }
                        else
                        {
                            assetRef.assetGUID = null;
                            assetRef.assetPath = null;
                            var go = e.newValue as GameObject;
                            if (go == null && e.newValue is Component comp)
                                go = comp.gameObject;
                            if (go != null)
                            {
                                assetRef.objectPath = GetHierarchyPath(go.transform);
                                assetRef.scenePath = go.scene.path;
                            }
                        }
                    }
                    RefreshUnityAssets();
                });
                row.Add(objField);

                var del = new Button(() => { _unityAssets.RemoveAt(index); RefreshUnityAssets(); }) { text = "x" };
                del.AddToClassList("item-delete-btn");
                del.style.marginLeft = 4;
                row.Add(del);

                if (isMissing)
                {
                    var warning = new Label($"Missing: {assetRef.assetName}");
                    warning.style.color = new Color(0.9f, 0.3f, 0.3f);
                    warning.style.fontSize = 10;
                    warning.style.marginLeft = 2;
                    container.Add(warning);
                }

                _unityAssetsContainer.Add(container);
            }
            var addBtn = new Button(() => { _unityAssets.Add(new UnityAssetReference()); RefreshUnityAssets(); }) { text = "+ Add Asset" };
            addBtn.AddToClassList("add-chip");
            addBtn.style.marginTop = 4;
            _unityAssetsContainer.Add(addBtn);
        }

        private string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private UnityEngine.Object FindSceneObject(string scenePath, string objectPath)
        {
            if (string.IsNullOrEmpty(objectPath))
                return null;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scenePath) && scene.path != scenePath)
                return null;

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                string rootPath = root.name;
                if (objectPath == rootPath)
                    return root;

                if (objectPath.StartsWith(rootPath + "/"))
                {
                    string remaining = objectPath.Substring(rootPath.Length + 1);
                    var found = root.transform.Find(remaining);
                    if (found != null)
                        return found.gameObject;
                }
            }
            return null;
        }

        private void RefreshTags()
        {
            if (_tagsContainer == null) return;
            _tagsContainer.Clear();
            foreach (var tag in _board.allTags)
            {
                var chip = new Button(() => { ToggleTag(tag); RefreshTags(); });
                chip.text = tag;
                chip.AddToClassList("tag-toggle");
                if (_selectedTags.Contains(tag)) chip.AddToClassList("active");
                _tagsContainer.Add(chip);
            }
            var addBtn = new Button(OpenTagPopup) { text = "+" };
            addBtn.AddToClassList("add-chip");
            _tagsContainer.Add(addBtn);
        }

        private void OpenTagPopup()
        {
            var popup = new TagEditPopup(_board, () => {
                RefreshTags();
            });
            this.Add(popup);
        }

        private void RefreshAssignees()
        {
            if (_assigneesContainer == null) return;
            _assigneesContainer.Clear();
            foreach (var a in _board.assignees)
            {
                var chip = new Button(() => { ToggleAssignee(a.id); RefreshAssignees(); });
                chip.text = a.name;
                chip.AddToClassList("filter-chip");
                chip.AddToClassList("assignee-chip");
                chip.style.borderLeftColor = a.color;
                if (_selectedAssignees.Contains(a.id)) chip.AddToClassList("active");
                _assigneesContainer.Add(chip);
            }
            var addBtn = new Button(OpenAssigneePopup) { text = "+" };
            addBtn.AddToClassList("add-chip");
            _assigneesContainer.Add(addBtn);
        }



        #endregion

        #region Helpers

        private void AddLabel(VisualElement parent, string text, float marginTop = 0)
        {
            var label = new Label(text);
            label.AddToClassList("modal-field-label");
            if (marginTop > 0) label.style.marginTop = marginTop;
            parent.Add(label);
        }

        private void AddPriorityOption(string label, int priority, string cssClass)
        {
            var btn = new Button(() => SelectPriority(priority));
            btn.text = label;
            btn.AddToClassList("priority-option");
            btn.AddToClassList(cssClass);
            if (_selectedPriority == priority) btn.AddToClassList("selected");
            btn.userData = priority;
            _prioritySelector.Add(btn);
        }

        private void SelectPriority(int p)
        {
            _selectedPriority = p;
            foreach (var child in _prioritySelector.Children())
                if (child is Button btn)
                    btn.EnableInClassList("selected", (int)btn.userData == p);
        }

        private void AddQuickDateBtn(VisualElement parent, string text, int days)
        {
            var btn = new Button(() => SetDueDate(DateTime.Today.AddDays(days))) { text = text };
            btn.style.flexGrow = 1;
            btn.style.marginRight = 2;
            parent.Add(btn);
        }

        private void SetDueDate(DateTime date)
        {
            _selectedDueDate = new DateTimeOffset(date).ToUnixTimeSeconds();
            _dueDateDisplay.text = GetDueDateText();
            UpdateDueDateColor();
            UpdateDateButton();
        }

        private void UpdateDueDateColor()
        {
            if (_dueDateDisplay == null) return;
            if (_selectedDueDate <= 0)
            {
                _dueDateDisplay.style.color = new Color(0.5f, 0.5f, 0.5f);
                return;
            }
            var date = DateTimeOffset.FromUnixTimeSeconds(_selectedDueDate).LocalDateTime;
            var diff = (date.Date - DateTime.Today).Days;
            if (_card.isCompleted)
            {
                _dueDateDisplay.style.color = diff >= 0 ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
            }
            else
            {
                if (diff < 0) _dueDateDisplay.style.color = new Color(0.9f, 0.3f, 0.3f);
                else if (diff == 0) _dueDateDisplay.style.color = new Color(0.9f, 0.6f, 0.2f);
                else if (diff <= 2) _dueDateDisplay.style.color = new Color(0.9f, 0.85f, 0.2f);
                else _dueDateDisplay.style.color = new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private string GetDueDateText()
        {
            if (_selectedDueDate <= 0) return "No due date";
            var date = DateTimeOffset.FromUnixTimeSeconds(_selectedDueDate).LocalDateTime;
            var diff = (date.Date - DateTime.Today).Days;
            var dayName = date.ToString("ddd");
            if (diff == 0) return $"Today ({dayName}, {date:MMM d})";
            if (diff == 1) return $"Tomorrow ({dayName}, {date:MMM d})";
            if (diff > 0) return $"In {diff} days ({dayName}, {date:MMM d})";
            return $"{-diff} days ago ({dayName}, {date:MMM d})";
        }

        private void ToggleTag(string tag)
        {
            if (_selectedTags.Contains(tag)) _selectedTags.Remove(tag);
            else _selectedTags.Add(tag);
        }

        private void ToggleAssignee(string id)
        {
            if (_selectedAssignees.Contains(id)) _selectedAssignees.Remove(id);
            else _selectedAssignees.Add(id);
        }

        private void AddComment()
        {
            if (string.IsNullOrWhiteSpace(_newCommentField.value)) return;
            _comments.Add(new CardComment(_newCommentField.value.Trim()));
            _newCommentField.value = "";
            RefreshComments();
        }

        private void ToggleTimer()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (_timerStartedAt > 0)
            {
                int elapsed = (int)(now - _timerStartedAt);
                _trackedSeconds += elapsed;
                _timerStartedAt = 0;
            }
            else
            {
                _timerStartedAt = now;
            }
            
            var btn = this.Q<Button>("timer-button");
            if (btn != null) btn.text = _timerStartedAt > 0 ? "⏸" : "▶";
        }

        private void UpdateTimer()
        {
            if (_trackedLabel == null) return;
            
            int displaySeconds = _trackedSeconds;
            if (_timerStartedAt > 0)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                displaySeconds += (int)(now - _timerStartedAt);
            }
            _trackedLabel.text = FormatTracked(displaySeconds);
        }

        private string FormatTracked(int seconds)
        {
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m}m {s}s";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        #endregion

        #region Save/Delete/Close

        private void SaveAndClose()
        {
            if (string.IsNullOrWhiteSpace(_titleField.value))
            {
                if (_isCreateMode)
                {
                    Close();
                    return;
                }
            }

            _card.title = _titleField.value.Trim();
            _card.description = _descriptionField.value.Trim();
            _card.priority = _selectedPriority;
            _card.tags = new List<string>(_selectedTags);
            _card.assigneeIds = new List<string>(_selectedAssignees);
            _card.dueDate = _selectedDueDate;
            _card.checklist = new List<ChecklistItem>(_checklistItems);
            _card.estimatedSeconds = 0;
            _card.trackedSeconds = _trackedSeconds;
            _card.timerStartedAt = _timerStartedAt;
            _card.attachments = new List<CardAttachment>(_links);
            _card.unityAssets = new List<UnityAssetReference>(_unityAssets);
            _card.comments = new List<CardComment>(_comments);
            _card.coverImageGUID = _coverImageGUID;
            _card.coverImagePath = _coverImagePath;
            _card.MarkUpdated();

            EditorApplication.update -= UpdateTimer;
            _onSave?.Invoke(_card);
            RemoveFromHierarchy();
        }

        private void DeleteCard()
        {
            if (EditorUtility.DisplayDialog("Delete", $"Delete '{_card.title}'?", "Delete", "Cancel"))
            {
                _cardCache?.DeleteCard(_card.id);

                EditorApplication.update -= UpdateTimer;
                _onSave?.Invoke(null);
                Close();
            }
        }

        private void Close()
        {
            EditorApplication.update -= UpdateTimer;
            RemoveFromHierarchy();
        }

        private void AddDateShortcut(VisualElement parent, string label, int daysFromToday)
        {
            var btn = new Button(() => {
                var newDate = DateTime.Today.AddDays(daysFromToday);
                _selectedDueDate = (long)new DateTimeOffset(newDate).ToUnixTimeSeconds();
                _dueDateDisplay.text = GetDueDateText();
                UpdateDueDateColor();
                UpdateDateButton();
            });
            btn.text = label;
            btn.AddToClassList("date-shortcut-btn");
            parent.Add(btn);
        }

        private void BuildStandaloneSelectors(VisualElement parent)
        {
            var container = new VisualElement();
            container.style.marginBottom = 16;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.paddingBottom = 8;
            parent.Add(container);

            if (_availableBoards != null && _availableBoards.Count > 0)
            {
                var boardLabel = new Label("Target Board");
                boardLabel.AddToClassList("modal-field-label");
                container.Add(boardLabel);

                int currentIndex = 0;
                var boardNames = new List<string>();
                for(int i=0; i<_availableBoards.Count; i++)
                {
                    var path = _availableBoards[i];
                    var name = System.IO.Path.GetFileName(path);
                    boardNames.Add(name);
                    if (path == _board.boardFolderPath) currentIndex = i;
                }

                _boardDropdown = new DropdownField(boardNames, currentIndex);
                _boardDropdown.AddToClassList("board-dropdown");
                _boardDropdown.style.marginBottom = 8;
                _boardDropdown.style.marginLeft = 0;
                _boardDropdown.style.width = Length.Percent(100);
                _boardDropdown.RegisterValueChangedCallback(evt => {
                    var newIndex = boardNames.IndexOf(evt.newValue);
                    if (newIndex >= 0 && newIndex < _availableBoards.Count)
                    {
                        _onBoardChanged?.Invoke(_availableBoards[newIndex]);
                    }
                });
                container.Add(_boardDropdown);
            }

            var colLabel = new Label("Target Column");
            colLabel.AddToClassList("modal-field-label");
            container.Add(colLabel);

            var colNames = _board.columns.ConvertAll(c => c.title);
            int colIndex = 0;
            if (_targetColumn != null)
                colIndex = _board.columns.IndexOf(_targetColumn);
            
            _columnDropdown = new DropdownField(colNames, colIndex);
            _columnDropdown.style.marginLeft = 0;
            _columnDropdown.style.width = Length.Percent(100);
            _columnDropdown.RegisterValueChangedCallback(evt => {
                var idx = colNames.IndexOf(evt.newValue);
                if (idx >= 0)
                {
                    _targetColumn = _board.columns[idx];
                }
            });
            container.Add(_columnDropdown);
        }

        public void SwitchBoard(KanbanBoard newBoard, KanbanColumn newTargetColumn)
        {
            _board = newBoard;
            _targetColumn = newTargetColumn;

            Clear();
            AddToClassList("modal-overlay");
            BuildUI();
        }

        public KanbanColumn GetTargetColumn()
        {
            return _targetColumn;
        }

        public void UpdateBoardReference(KanbanBoard newBoard, CardCache newCache)
        {
            _board = newBoard;
            _cardCache = newCache;
            
            // Update target column reference from new board
            if (_targetColumn != null)
            {
                _targetColumn = _board.FindColumnById(_targetColumn.id);
            }
            
            // Update card reference from cache if it exists, otherwise keep current (might be new/unsaved)
            if (_card != null && !string.IsNullOrEmpty(_card.id))
            {
                var cachedCard = _cardCache.GetCard(_card.id);
                if (cachedCard != null)
                {
                    _card = cachedCard;
                }
            }

            // Refresh UI components that depend on board data
            RefreshTags();
            RefreshAssignees();
        }

        #endregion
    }
}
