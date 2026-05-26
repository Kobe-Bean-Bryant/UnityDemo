using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class CardElement : VisualElement
    {
        public event Action<CardElement> OnEditClicked;
        public event Action<CardElement> OnCompletionToggled;
        public event Action<CardElement> OnDeleteClicked;
        public event Action<CardElement> OnAssetDropped;

        public KanbanCard Card { get; private set; }
        public KanbanBoard Board { get; private set; }
        public string ColumnId { get; private set; }

        private VisualElement _mainRow;
        private VisualElement _checkContainer;
        private Label _checkLabel;
        private VisualElement _contentWrapper;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private VisualElement _footer;
        private VisualElement _buttonsContainer;
        private VisualElement _editButton;
        private VisualElement _deleteButton;
        private VisualElement _dropOverlay;
        private Image _coverImage;
        private bool _potentialDrag;
        private bool _didDrag;
        private Vector2 _dragStartPos;
        private bool _isHovered;
        private bool _overlayPolling;
        private double _lastDragUpdateTime;

        private const float MAX_COVER_HEIGHT = 220f;
        private const float CARD_PAD_TOP  = 10f;
        private const float CARD_PAD_SIDE = 12f;
        private const float HOVER_DURATION = 0.4f;
        private const float CHECK_DURATION = 0.3f;
        private const UIAnimator.EaseType SMOOTH_EASE = UIAnimator.EaseType.EaseOut;

        public CardElement(KanbanCard card, KanbanBoard board, string columnId)
        {
            Card = card;
            Board = board;
            ColumnId = columnId;

            AddToClassList(StyleClasses.KanbanCard);
            if (card.isCompleted)
                AddToClassList(StyleClasses.Completed);
            UpdatePriorityClass();

            style.position = Position.Relative;

            BuildUI();
            RegisterCallbacks();

            style.overflow = Overflow.Hidden;
        }

        private void BuildUI()
        {
            _coverImage = new Image();
            _coverImage.scaleMode = ScaleMode.ScaleToFit;
            _coverImage.style.marginTop    = -CARD_PAD_TOP;
            _coverImage.style.marginLeft   = -CARD_PAD_SIDE;
            _coverImage.style.marginRight  = -CARD_PAD_SIDE;
            _coverImage.style.marginBottom = 8;
            _coverImage.style.backgroundColor = new Color(0.11f, 0.11f, 0.12f);
            _coverImage.style.display = DisplayStyle.None;
            Add(_coverImage);
            RefreshCoverImage();

            RegisterCallback<GeometryChangedEvent>(OnCoverGeometryChanged);

            _mainRow = new VisualElement();
            _mainRow.style.flexDirection = FlexDirection.Row;
            _mainRow.style.alignItems = Align.FlexStart;
            Add(_mainRow);

            _checkContainer = new VisualElement();
            _checkContainer.AddToClassList(StyleClasses.CheckContainer);
            _checkContainer.style.width = Card.isCompleted ? 20 : 0;
            _checkContainer.style.marginRight = Card.isCompleted ? 4 : 0;
            _checkContainer.RegisterCallback<MouseEnterEvent>(evt => OnCheckHoverEnter());
            _checkContainer.RegisterCallback<MouseLeaveEvent>(evt => OnCheckHoverLeave());
            _checkContainer.RegisterCallback<MouseUpEvent>(OnCheckClick);
            _mainRow.Add(_checkContainer);

            _checkLabel = new Label(Card.isCompleted ? "✓" : "○");
            _checkLabel.AddToClassList(StyleClasses.CheckLabel);
            _checkLabel.style.opacity = Card.isCompleted ? 1 : 0;
            _checkContainer.Add(_checkLabel);

            _contentWrapper = new VisualElement();
            _contentWrapper.AddToClassList(StyleClasses.CardContent);
            _contentWrapper.style.flexGrow = 1;
            _mainRow.Add(_contentWrapper);

            _titleLabel = new Label(Card.title);
            _titleLabel.AddToClassList(StyleClasses.CardTitle);
            if (Card.isCompleted)
                _titleLabel.AddToClassList(StyleClasses.CompletedText);
            _contentWrapper.Add(_titleLabel);

            if (!string.IsNullOrEmpty(Card.description))
            {
                _descriptionLabel = new Label(TruncateText(Card.description, 80));
                _descriptionLabel.AddToClassList(StyleClasses.CardDescription);
                if (Card.isCompleted)
                    _descriptionLabel.AddToClassList(StyleClasses.CompletedText);
                _contentWrapper.Add(_descriptionLabel);
            }

            _footer = new VisualElement();
            _footer.AddToClassList(StyleClasses.CardFooter);
            _contentWrapper.Add(_footer);

            RefreshFooter();

            _dropOverlay = new VisualElement();
            _dropOverlay.pickingMode = PickingMode.Ignore;
            _dropOverlay.style.display = DisplayStyle.None;
            _dropOverlay.style.position = Position.Absolute;
            _dropOverlay.style.left = 0;
            _dropOverlay.style.top = 0;
            _dropOverlay.style.right = 0;
            _dropOverlay.style.bottom = 0;
            _dropOverlay.style.borderTopLeftRadius = 6;
            _dropOverlay.style.borderTopRightRadius = 6;
            _dropOverlay.style.borderBottomLeftRadius = 6;
            _dropOverlay.style.borderBottomRightRadius = 6;
            _dropOverlay.style.borderTopWidth = 2;
            _dropOverlay.style.borderBottomWidth = 2;
            _dropOverlay.style.borderLeftWidth = 2;
            _dropOverlay.style.borderRightWidth = 2;
            _dropOverlay.style.borderTopColor = new Color(0.29f, 0.62f, 1f, 0.9f);
            _dropOverlay.style.borderBottomColor = new Color(0.29f, 0.62f, 1f, 0.9f);
            _dropOverlay.style.borderLeftColor = new Color(0.29f, 0.62f, 1f, 0.9f);
            _dropOverlay.style.borderRightColor = new Color(0.29f, 0.62f, 1f, 0.9f);
            _dropOverlay.style.backgroundColor = new Color(0.29f, 0.62f, 1f, 0.08f);
            _dropOverlay.style.justifyContent = Justify.Center;
            _dropOverlay.style.alignItems = Align.Center;
            var dropLabel = new Label("+ Add Asset");
            dropLabel.style.color = new Color(0.29f, 0.62f, 1f, 0.9f);
            dropLabel.style.fontSize = 11;
            dropLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dropOverlay.Add(dropLabel);
            Add(_dropOverlay);

            _buttonsContainer = new VisualElement();
            _buttonsContainer.AddToClassList(StyleClasses.CardButtonsContainer);
            _buttonsContainer.style.position = Position.Absolute;
            _buttonsContainer.style.top = 4;
            _buttonsContainer.style.right = 4;
            _buttonsContainer.style.flexDirection = FlexDirection.Row;
            _buttonsContainer.style.alignItems = Align.Center;
            _buttonsContainer.style.opacity = 0;
            _buttonsContainer.style.backgroundColor = new Color(0.18f, 0.18f, 0.19f, 0.95f);
            _buttonsContainer.style.borderTopLeftRadius = 4;
            _buttonsContainer.style.borderTopRightRadius = 4;
            _buttonsContainer.style.borderBottomLeftRadius = 4;
            _buttonsContainer.style.borderBottomRightRadius = 4;
            _buttonsContainer.style.paddingLeft = 4;
            _buttonsContainer.style.paddingRight = 4;
            _buttonsContainer.style.paddingTop = 2;
            _buttonsContainer.style.paddingBottom = 2;
            Add(_buttonsContainer);

            _deleteButton = new VisualElement();
            _deleteButton.AddToClassList(StyleClasses.CardActionButton);
            _deleteButton.style.width = 20;
            _deleteButton.style.height = 20;
            _deleteButton.style.justifyContent = Justify.Center;
            _deleteButton.style.alignItems = Align.Center;
            var deleteLabel = new Label("×");
            deleteLabel.style.fontSize = 16;
            deleteLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            deleteLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            deleteLabel.style.marginTop = -2;
            _deleteButton.Add(deleteLabel);
            _deleteButton.RegisterCallback<MouseUpEvent>(evt =>
            {
                evt.StopPropagation();
                bool confirmDelete = EditorPrefs.GetBool("TaskCanvas_ConfirmDelete", true);
                if (!confirmDelete || EditorUtility.DisplayDialog(
                        "Delete Card",
                        $"Delete '{Card.title}'?",
                        "Delete",
                        "Cancel"
                    ))
                {
                    OnDeleteClicked?.Invoke(this);
                }
            });
            _deleteButton.RegisterCallback<MouseEnterEvent>(evt =>
                deleteLabel.style.color = new Color(1f, 0.4f, 0.4f)
            );
            _deleteButton.RegisterCallback<MouseLeaveEvent>(evt =>
                deleteLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _buttonsContainer.Add(_deleteButton);

            _editButton = new VisualElement();
            _editButton.AddToClassList(StyleClasses.CardActionButton);
            _editButton.style.width = 20;
            _editButton.style.height = 20;
            _editButton.style.justifyContent = Justify.Center;
            _editButton.style.alignItems = Align.Center;
            var editLabel = new Label("✎");
            editLabel.style.fontSize = 12;
            editLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            editLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _editButton.Add(editLabel);
            _editButton.RegisterCallback<MouseUpEvent>(evt =>
            { 
                evt.StopPropagation();
                OnEditClicked?.Invoke(this);
            });
            _editButton.RegisterCallback<MouseEnterEvent>(evt =>
                editLabel.style.color = new Color(1f, 1f, 1f)
            );
            _editButton.RegisterCallback<MouseLeaveEvent>(evt =>
                editLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _buttonsContainer.Add(_editButton);
        }

        private void OnCheckHoverEnter()
        {
            UIAnimator.AnimateScale(_checkLabel, 1.2f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private void OnCheckHoverLeave()
        {
            UIAnimator.AnimateScale(_checkLabel, 1f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private void OnCheckClick(MouseUpEvent evt)
        {
            evt.StopPropagation();
            Card.isCompleted = !Card.isCompleted;
            Card.MarkUpdated();

            if (Card.isCompleted)
            {
                _checkLabel.text = "✓";
                AddToClassList("completed");
                _titleLabel.AddToClassList("completed-text");
                _descriptionLabel?.AddToClassList("completed-text");

                _checkLabel.style.scale = new Scale(new Vector2(0.3f, 0.3f));
                UIAnimator.AnimateScale(
                    _checkLabel,
                    1f,
                    CHECK_DURATION,
                    UIAnimator.EaseType.EaseOut
                );
                UIAnimator.AnimateOpacity(_checkLabel, 1f, 0.15f, UIAnimator.EaseType.EaseOut);
            }
            else
            {
                _checkLabel.text = "○";
                RemoveFromClassList("completed");
                _titleLabel.RemoveFromClassList("completed-text");
                _descriptionLabel?.RemoveFromClassList("completed-text");
                UIAnimator.AnimateOpacity(_checkLabel, 0.4f, 0.15f, UIAnimator.EaseType.EaseOut);
            }

            OnCompletionToggled?.Invoke(this);
        }

        private void OnHoverEnter()
        {
            if (DragDropManager.IsDragging)
                return;
            if (_isHovered)
                return;
            _isHovered = true;

            AddToClassList("card-hover");

            UIAnimator.AnimateWidth(_checkContainer, 20, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateMarginRight(_checkContainer, 4, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateOpacity(_buttonsContainer, 1f, 0.2f, UIAnimator.EaseType.EaseOut);

            if (!Card.isCompleted)
            {
                _checkLabel.text = "○";
            }
            UIAnimator.AnimateOpacity(
                _checkLabel,
                Card.isCompleted ? 1f : 0.4f,
                HOVER_DURATION,
                SMOOTH_EASE
            );
        }

        private void OnHoverLeave()
        {
            if (!_isHovered)
                return;
            _isHovered = false;

            RemoveFromClassList("card-hover");

            UIAnimator.AnimateOpacity(_buttonsContainer, 0f, 0.2f, UIAnimator.EaseType.EaseOut);

            if (Card.isCompleted)
                return;

            UIAnimator.AnimateWidth(_checkContainer, 0, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateMarginRight(_checkContainer, 0, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateOpacity(_checkLabel, 0, HOVER_DURATION, SMOOTH_EASE);
        }

        private string TruncateText(string text, int maxLength) => StringUtils.Truncate(text, maxLength);

        private void RefreshFooter()
        {
            _footer.Clear();
            _footer.style.alignItems = Align.Center;

            if (Card.dueDate > 0)
            {
                var dueDate = DateTimeOffset.FromUnixTimeSeconds(Card.dueDate).LocalDateTime;
                var now = DateTime.Now;
                var daysUntilDue = (dueDate - now).TotalDays;

                var dueBadge = new VisualElement();
                dueBadge.style.flexDirection = FlexDirection.Row;
                dueBadge.style.alignItems = Align.Center;
                dueBadge.style.paddingLeft = 5;
                dueBadge.style.paddingRight = 6;
                dueBadge.style.paddingTop = 3;
                dueBadge.style.paddingBottom = 3;
                dueBadge.style.borderTopLeftRadius = 4;
                dueBadge.style.borderTopRightRadius = 4;
                dueBadge.style.borderBottomLeftRadius = 4;
                dueBadge.style.borderBottomRightRadius = 4;
                dueBadge.style.marginRight = 5;
                dueBadge.style.height = 20;

                Color bgColor;
                if (Card.isCompleted)
                    bgColor = new Color(0.2f, 0.45f, 0.25f);
                else if (Card.isDueDateComplete)
                    bgColor = new Color(0.2f, 0.45f, 0.25f);
                else if (daysUntilDue < 0)
                    bgColor = new Color(0.5f, 0.2f, 0.2f);
                else if (daysUntilDue <= 1)
                    bgColor = new Color(0.6f, 0.35f, 0.15f);
                else if (daysUntilDue <= 3)
                    bgColor = new Color(0.5f, 0.45f, 0.15f);
                else
                    bgColor = new Color(0.25f, 0.25f, 0.28f);

                dueBadge.style.backgroundColor = bgColor;

                var clockIcon = new Label(IconHelper.DueDateIcon);
                clockIcon.style.fontSize = 10;
                clockIcon.style.marginRight = 3;
                clockIcon.style.color = Color.white;
                dueBadge.Add(clockIcon);

                var dateText = new Label(dueDate.ToString("MMM d"));
                dateText.style.fontSize = 11;
                dateText.style.color = Color.white;
                dueBadge.Add(dateText);

                _footer.Add(dueBadge);
            }

            if (Card.checklist != null && Card.checklist.Count > 0)
            {
                int completed = 0;
                foreach (var item in Card.checklist)
                {
                    if (item.isCompleted) completed++;
                }

                var subtasksBadge = new VisualElement();
                subtasksBadge.style.flexDirection = FlexDirection.Row;
                subtasksBadge.style.alignItems = Align.Center;
                subtasksBadge.style.backgroundColor =
                    (Card.isCompleted || completed == Card.checklist.Count)
                        ? new Color(0.2f, 0.45f, 0.25f)
                        : new Color(0.25f, 0.25f, 0.28f);
                subtasksBadge.style.paddingLeft = 5;
                subtasksBadge.style.paddingRight = 6;
                subtasksBadge.style.paddingTop = 3;
                subtasksBadge.style.paddingBottom = 3;
                subtasksBadge.style.borderTopLeftRadius = 4;
                subtasksBadge.style.borderTopRightRadius = 4;
                subtasksBadge.style.borderBottomLeftRadius = 4;
                subtasksBadge.style.borderBottomRightRadius = 4;
                subtasksBadge.style.marginRight = 5;
                subtasksBadge.style.height = 20;

                var checkIcon = new Label(IconHelper.ChecklistIcon);
                checkIcon.style.fontSize = 10;
                checkIcon.style.marginRight = 2;
                checkIcon.style.color = Color.white;
                subtasksBadge.Add(checkIcon);

                var countText = new Label($"{completed}/{Card.checklist.Count}");
                countText.style.fontSize = 11;
                countText.style.color = Color.white;
                subtasksBadge.Add(countText);

                _footer.Add(subtasksBadge);
            }

            int attachmentCount = (Card.attachments?.Count ?? 0);
            if (attachmentCount > 0)
            {
                var attachBadge = new VisualElement();
                attachBadge.style.flexDirection = FlexDirection.Row;
                attachBadge.style.alignItems = Align.Center;
                attachBadge.style.marginRight = 5;
                attachBadge.style.height = 20;

                var icon = new Label(IconHelper.AttachmentIcon);
                icon.style.fontSize = 10;
                icon.style.color = new Color(0.7f, 0.7f, 0.7f);
                attachBadge.Add(icon);

                var count = new Label(attachmentCount.ToString());
                count.style.fontSize = 11;
                count.style.color = new Color(0.7f, 0.7f, 0.7f);
                count.style.marginLeft = 2;
                attachBadge.Add(count);

                _footer.Add(attachBadge);
            }

            if (Card.comments != null && Card.comments.Count > 0)
            {
                var commentBadge = new VisualElement();
                commentBadge.style.flexDirection = FlexDirection.Row;
                commentBadge.style.alignItems = Align.Center;
                commentBadge.style.marginRight = 5;
                commentBadge.style.height = 20;

                var icon = new Label(IconHelper.CommentIcon);
                icon.style.fontSize = 10;
                icon.style.color = new Color(0.7f, 0.7f, 0.7f);
                commentBadge.Add(icon);

                var count = new Label(Card.comments.Count.ToString());
                count.style.fontSize = 11;
                count.style.color = new Color(0.7f, 0.7f, 0.7f);
                count.style.marginLeft = 2;
                commentBadge.Add(count);

                _footer.Add(commentBadge);
            }

            if (Card.unityAssets != null)
            {
                foreach (var assetRef in Card.unityAssets)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(assetRef.assetGUID);
                    if (string.IsNullOrEmpty(assetPath)) assetPath = assetRef.assetPath;

                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    
                    var assetBtn = new VisualElement();
                    assetBtn.AddToClassList("card-tag");
                    assetBtn.style.flexDirection = FlexDirection.Row;
                    assetBtn.style.alignItems = Align.Center;
                    assetBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
                    assetBtn.style.borderTopWidth = 1;
                    assetBtn.style.borderBottomWidth = 1;
                    assetBtn.style.borderLeftWidth = 1;
                    assetBtn.style.borderRightWidth = 1;
                    assetBtn.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
                    assetBtn.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
                    assetBtn.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
                    assetBtn.style.borderRightColor = new Color(0.35f, 0.35f, 0.35f);
                    assetBtn.style.paddingLeft = 4;
                    assetBtn.style.paddingRight = 6;
                    assetBtn.style.paddingTop = 2;
                    assetBtn.style.paddingBottom = 2;

                    if (obj != null)
                    {
                        var icon = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image;
                        if (icon != null)
                        {
                            var iconImg = new Image { image = icon };
                            iconImg.style.width = 14;
                            iconImg.style.height = 14;
                            iconImg.style.marginRight = 4;
                            assetBtn.Add(iconImg);
                        }
                    }

                    var nameLabel = new Label(assetRef.assetName);
                    nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                    nameLabel.style.fontSize = 11;
                    assetBtn.Add(nameLabel);

                    assetBtn.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
                    assetBtn.RegisterCallback<MouseUpEvent>(evt =>
                    {
                        evt.StopPropagation();
                        if (obj != null)
                        {
                            EditorGUIUtility.PingObject(obj);
                            Selection.activeObject = obj;
                        }
                        else
                        {
                            Debug.LogWarning($"TaskCanvas: Asset '{assetRef.assetName}' not found at {assetPath}");
                        }
                    });
                    
                    assetBtn.RegisterCallback<MouseEnterEvent>(evt => assetBtn.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f));
                    assetBtn.RegisterCallback<MouseLeaveEvent>(evt => assetBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f));

                    _footer.Add(assetBtn);
                }
            }
            int tagCount = 0;
            foreach (var tag in Card.tags)
            {
                if (tagCount >= 3)
                {
                    var moreLabel = new Label($"+{Card.tags.Count - 3}");
                    moreLabel.AddToClassList("card-tag");
                    moreLabel.AddToClassList("card-tag-more");
                    _footer.Add(moreLabel);
                    break;
                }

                var tagLabel = new Label($"#{tag}");
                tagLabel.AddToClassList("card-tag");
                tagLabel.style.marginTop = 4;
                _footer.Add(tagLabel);
                tagCount++;
            }
            int assigneeCount = 0;
            foreach (var assigneeId in Card.assigneeIds)
            {
                if (assigneeCount >= 3)
                {
                    var moreAssignee = new VisualElement();
                    moreAssignee.AddToClassList("card-assignee");
                    moreAssignee.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                    var moreLabel = new Label($"+{Card.assigneeIds.Count - 3}");
                    moreLabel.style.color = Color.white;
                    moreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    moreLabel.style.fontSize = 8;
                    moreAssignee.Add(moreLabel);
                    _footer.Add(moreAssignee);
                    break;
                }

                var assignee = Board.GetAssigneeById(assigneeId);
                if (assignee != null)
                {
                    var assigneeElement = new VisualElement();
                    assigneeElement.AddToClassList("card-assignee");
                    assigneeElement.style.backgroundColor = assignee.color;
                    assigneeElement.style.marginTop = 4;

                    var initial = new Label(
                        assignee.name.Length > 0 ? assignee.name[0].ToString().ToUpper() : "?"
                    );
                    initial.style.color = Color.white;
                    initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                    initial.style.fontSize = 10;
                    assigneeElement.Add(initial);

                    assigneeElement.tooltip = assignee.name;
                    _footer.Add(assigneeElement);
                    assigneeCount++;
                }
            }
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseEnterEvent>(evt => OnHoverEnter());
            RegisterCallback<MouseLeaveEvent>(evt => OnHoverLeave());
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragExitedEvent>(OnDragExited);
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_overlayPolling)
                {
                    EditorApplication.update -= PollDragOverlay;
                    _overlayPolling = false;
                }
            });
        }

        private static bool HasProjectObjects()
        {
            return DragAndDrop.objectReferences != null &&
                   DragAndDrop.objectReferences.Length > 0;
        }

        private void ShowDropOverlay(bool show)
        {
            if (_dropOverlay == null) return;
            _dropOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            if (show && !_overlayPolling)
            {
                _overlayPolling = true;
                EditorApplication.update += PollDragOverlay;
            }
            else if (!show && _overlayPolling)
            {
                EditorApplication.update -= PollDragOverlay;
                _overlayPolling = false;
            }
        }

        private void PollDragOverlay()
        {
            if (EditorApplication.timeSinceStartup - _lastDragUpdateTime > 0.15)
                ShowDropOverlay(false);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragDropManager.IsDragging || !HasProjectObjects())
                return;

            _lastDragUpdateTime = EditorApplication.timeSinceStartup;
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            ShowDropOverlay(true);
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (DragDropManager.IsDragging || !HasProjectObjects())
                return;

            DragAndDrop.AcceptDrag();
            ShowDropOverlay(false);

            bool anyAdded = false;
            var existingGuids = new HashSet<string>();
            foreach (var a in Card.unityAssets)
                if (!string.IsNullOrEmpty(a.assetGUID))
                    existingGuids.Add(a.assetGUID);

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj == null) continue;

                var path = AssetDatabase.GetAssetPath(obj);
                var isScene = string.IsNullOrEmpty(path);

                var assetRef = new UnityAssetReference
                {
                    assetName = obj.name,
                    assetTypeName = obj.GetType().Name
                };

                if (!isScene)
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (existingGuids.Contains(guid))
                        continue;

                    assetRef.assetGUID = guid;
                    assetRef.assetPath = path;
                    existingGuids.Add(guid);
                }
                else
                {
                    var go = obj as GameObject;
                    if (go == null && obj is Component comp)
                        go = comp.gameObject;

                    if (go == null) continue;

                    assetRef.isSceneObject = true;
                    assetRef.objectPath = GetHierarchyPath(go.transform);
                    assetRef.scenePath = go.scene.path;
                }

                Card.unityAssets.Add(assetRef);
                anyAdded = true;
            }

            if (anyAdded)
            {
                Card.MarkUpdated();
                RefreshFooter();
                OnAssetDropped?.Invoke(this);
            }

            evt.StopPropagation();
        }

        private void OnDragExited(DragExitedEvent evt)
        {
            ShowDropOverlay(false);
        }

        private string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (_buttonsContainer.Contains(evt.target as VisualElement))
                return;
            if (_checkContainer.Contains(evt.target as VisualElement))
                return;

            if (evt.button == 0)
            {
                _potentialDrag = true;
                _didDrag = false;
                _dragStartPos = evt.mousePosition;
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_potentialDrag)
                return;

            var delta = evt.mousePosition - _dragStartPos;

            if (delta.magnitude > 5 && !DragDropManager.IsDragging)
            {
                _didDrag = true;
                DragDropManager.StartCardDrag(this, Card.id, ColumnId, _dragStartPos, Card, Board);
                _potentialDrag = false;
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (_potentialDrag && !_didDrag && evt.button == 0)
            {
                OnEditClicked?.Invoke(this);
            }
            _potentialDrag = false;
        }

        private void UpdatePriorityClass()
        {
            RemoveFromClassList("priority-none");
            RemoveFromClassList("priority-low");
            RemoveFromClassList("priority-medium");
            RemoveFromClassList("priority-high");

            switch (Card.priority)
            {
                case 0:
                    AddToClassList("priority-none");
                    break;
                case 1:
                    AddToClassList("priority-low");
                    break;
                case 2:
                    AddToClassList("priority-medium");
                    break;
                case 3:
                    AddToClassList("priority-high");
                    break;
            }
        }

        public void Refresh()
        {
            _titleLabel.text = Card.title;
            UpdatePriorityClass();
            RefreshFooter();
            RefreshCoverImage();
        }

        private void RefreshCoverImage()
        {
            if (_coverImage == null) return;

            var tex = LoadCoverTexture();
            if (tex != null)
            {
                _coverImage.image = tex;
                _coverImage.style.display = DisplayStyle.Flex;
                ComputeCoverHeight(tex);
            }
            else
            {
                _coverImage.image = null;
                _coverImage.style.display = DisplayStyle.None;
                _coverImage.style.height = StyleKeyword.Auto;
            }
        }

        private void OnCoverGeometryChanged(GeometryChangedEvent e)
        {
            if (_coverImage == null || _coverImage.image == null) return;
            if (_coverImage.style.display == DisplayStyle.None) return;

            if (Mathf.Abs(e.newRect.width - e.oldRect.width) < 0.5f) return;

            ComputeCoverHeight(_coverImage.image as Texture2D);
        }

        private void ComputeCoverHeight(Texture2D tex)
        {
            if (tex == null || tex.height == 0) return;

            float cardWidth = resolvedStyle.width;
            if (cardWidth <= 1f || float.IsNaN(cardWidth)) return;

            float aspect      = (float)tex.width / tex.height;
            float natHeight   = cardWidth / aspect;
            float finalHeight = Mathf.Min(MAX_COVER_HEIGHT, natHeight);

            _coverImage.style.height = finalHeight;
        }

        private Texture2D LoadCoverTexture()
        {
            string resolvePath = null;

            if (!string.IsNullOrEmpty(Card.coverImageGUID))
                resolvePath = AssetDatabase.GUIDToAssetPath(Card.coverImageGUID);

            if (string.IsNullOrEmpty(resolvePath) && !string.IsNullOrEmpty(Card.coverImagePath))
                resolvePath = Card.coverImagePath;

            if (string.IsNullOrEmpty(resolvePath)) return null;

            string fullPath = System.IO.Path.GetFullPath(resolvePath);
            if (System.IO.File.Exists(fullPath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                var rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                rawTex.filterMode = FilterMode.Bilinear;
                if (rawTex.LoadImage(bytes))
                    return rawTex;
                UnityEngine.Object.DestroyImmediate(rawTex);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(resolvePath);
            if (tex != null) return tex;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(resolvePath))
            {
                if (asset is Texture2D t) return t;
            }

            return null;
        }
    }
}
