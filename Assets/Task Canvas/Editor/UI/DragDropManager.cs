using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public static class DragDropManager
    {
        public static bool IsDragging { get; private set; }
        public static string DraggedCardId { get; private set; }
        public static string SourceColumnId { get; private set; }
        public static int DraggedColumnIndex { get; private set; } = -1;

        private static VisualElement _draggedElement;
        private static VisualElement _ghostElement;
        private static VisualElement _placeholder;
        private static VisualElement _root;
        private static VisualElement _columnsContainer;

        private static Action<string, string, int> _onCardDropped;
        private static Action<int, int> _onColumnReorder;

        private static int _currentPlaceholderColumnIndex = -1;
        private static int _currentPlaceholderCardIndex = -1;
        private static string _currentPlaceholderColumnId;
        private static Vector2 _dragOffset;
        private static float _ghostWidth;
        private static float _ghostHeight;
        private static int _originalCardIndex;
        private static ScrollView _mainScrollView;
        private const float AUTO_SCROLL_EDGE = 40f;
        private const float AUTO_SCROLL_SPEED = 1400f;

        private static Vector2 _targetGhostPos;
        private static Vector2 _currentGhostPos;
        private static float _currentTilt;
        private static float _targetTilt;
        private static Vector2 _lastMousePos;
        private static double _lastDragTime = -1;
        private const float SMOOTH_SPEED = 30f;
        private static bool _isInitialized;

        public static void Initialize(
            VisualElement root,
            Action<string, string, int> onCardDropped,
            Action<int, int> onColumnReorder,
            Action onRefresh,
            ScrollView mainScrollView = null
        )
        {
            if (_isInitialized && _root != null)
            {
                _root.UnregisterCallback<MouseMoveEvent>(OnGlobalMouseMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<MouseUpEvent>(OnGlobalMouseUp, TrickleDown.TrickleDown);
                EditorApplication.update -= UpdateSmoothDrag;
            }

            _root = root;
            _onCardDropped = onCardDropped;
            _onColumnReorder = onColumnReorder;
            _mainScrollView = mainScrollView;

            if (_root == null)
            {
                Debug.LogError("[DragDropManager] root is null during Initialize!");
                return;
            }

            _root.RegisterCallback<MouseMoveEvent>(OnGlobalMouseMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<MouseUpEvent>(OnGlobalMouseUp, TrickleDown.TrickleDown);

            EditorApplication.update -= UpdateSmoothDrag;
            EditorApplication.update += UpdateSmoothDrag;
            
            _isInitialized = true;
            if (IsDragging)
            {
                EndDrag();
            }
        }

        public static void Dispose()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<MouseMoveEvent>(OnGlobalMouseMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<MouseUpEvent>(OnGlobalMouseUp, TrickleDown.TrickleDown);
            }
            EditorApplication.update -= UpdateSmoothDrag;
            _isInitialized = false;
            EndDrag();
        }

        private static void UpdateSmoothDrag()
        {
            if (!IsDragging || _ghostElement == null)
            {
                _lastDragTime = -1;
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime;
            if (_lastDragTime < 0)
            {
                deltaTime = 0.016f;
            }
            else
            {
                deltaTime = (float)(currentTime - _lastDragTime);
            }
            _lastDragTime = currentTime;
            deltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.1f);
            _ghostElement.style.left = _targetGhostPos.x;
            _ghostElement.style.top = _targetGhostPos.y;
            float t = 1f - Mathf.Exp(-SMOOTH_SPEED * deltaTime);
            _currentTilt = Mathf.Lerp(_currentTilt, _targetTilt, t);
            _ghostElement.style.rotate = new Rotate(_currentTilt);
            _targetTilt *= 0.9f;
            if (IsDragging && _mainScrollView != null)
            {
                var scrollRect = _mainScrollView.worldBound;
                float mouseX = _targetGhostPos.x + _dragOffset.x;
                float mouseY = _targetGhostPos.y + _dragOffset.y;
                float scrollDeltaX = 0;
                float scrollDeltaY = 0;
                
                if (mouseX < scrollRect.x + AUTO_SCROLL_EDGE)
                {
                    scrollDeltaX = -AUTO_SCROLL_SPEED * deltaTime;
                }
                else if (mouseX > scrollRect.xMax - AUTO_SCROLL_EDGE)
                {
                    scrollDeltaX = AUTO_SCROLL_SPEED * deltaTime;
                }
                
                if (mouseY < scrollRect.y + AUTO_SCROLL_EDGE)
                {
                    scrollDeltaY = -AUTO_SCROLL_SPEED * deltaTime;
                }
                else if (mouseY > scrollRect.yMax - AUTO_SCROLL_EDGE)
                {
                    scrollDeltaY = AUTO_SCROLL_SPEED * deltaTime;
                }

                if (scrollDeltaX != 0 || scrollDeltaY != 0)
                {
                    _mainScrollView.scrollOffset = new Vector2(
                        _mainScrollView.scrollOffset.x + scrollDeltaX,
                        _mainScrollView.scrollOffset.y + scrollDeltaY
                    );
                }
            }
        }

        public static bool IsReady => _isInitialized && _root != null;

        public static void StartCardDrag(
            VisualElement cardElement,
            string cardId,
            string columnId,
            Vector2 grabPosition,
            KanbanCard card,
            KanbanBoard board
        )
        {
            if (IsDragging)
                return;
            if (!IsReady)
            {
                return;
            }
            if (cardElement == null)
            {
                Debug.LogWarning("[DragDropManager] Cannot start drag - cardElement is null");
                return;
            }

            IsDragging = true;
            DraggedCardId = cardId;
            SourceColumnId = columnId;
            DraggedColumnIndex = -1;
            _draggedElement = cardElement;

            _columnsContainer = _root.Q<VisualElement>(className: "columns-container");
            _ghostWidth = cardElement.resolvedStyle.width;
            _ghostHeight = cardElement.resolvedStyle.height;
            if (_ghostWidth <= 0 || float.IsNaN(_ghostWidth))
                _ghostWidth = 250;
            if (_ghostHeight <= 0 || float.IsNaN(_ghostHeight))
                _ghostHeight = 60;

            var cardRect = cardElement.worldBound;
            var rootRect = _root.worldBound;
            if (cardRect.width <= 0 || rootRect.width <= 0)
            {
                Debug.LogWarning("[DragDropManager] Invalid bounds detected, using mouse position as fallback");
                _currentGhostPos = new Vector2(grabPosition.x - rootRect.x - 100, grabPosition.y - rootRect.y - 30);
                _dragOffset = new Vector2(100, 30);
            }
            else
            {
                _dragOffset = new Vector2(grabPosition.x - cardRect.x, grabPosition.y - cardRect.y);
                _currentGhostPos = new Vector2(cardRect.x - rootRect.x, cardRect.y - rootRect.y);
            }
            
            _targetGhostPos = _currentGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = grabPosition;

            cardElement.style.display = DisplayStyle.None;

            CreateCardGhost(card, board, columnId);
            CreateCardPlaceholder();
            if (_ghostElement == null)
            {
                Debug.LogError("[DragDropManager] Failed to create ghost element!");
                EndDrag();
                return;
            }


            var parent = cardElement.parent;
            _originalCardIndex = parent.IndexOf(cardElement);
            parent.Insert(_originalCardIndex, _placeholder);

            _currentPlaceholderColumnId = columnId;
            _currentPlaceholderCardIndex = _originalCardIndex;
        }

        public static void StartColumnDrag(
            VisualElement columnElement,
            int columnIndex,
            Vector2 mousePos,
            string columnTitle
        )
        {
            if (IsDragging)
                return;

            IsDragging = true;
            DraggedCardId = null;
            SourceColumnId = null;
            DraggedColumnIndex = columnIndex;
            _draggedElement = columnElement;

            _columnsContainer = _root.Q<VisualElement>(className: "columns-container");
            _ghostWidth = columnElement.resolvedStyle.width;
            _ghostHeight = columnElement.resolvedStyle.height;

            var colRect = columnElement.worldBound;
            _dragOffset = new Vector2(mousePos.x - colRect.x, mousePos.y - colRect.y);

            _currentGhostPos = new Vector2(colRect.x, colRect.y);
            _targetGhostPos = _currentGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = mousePos;

            CreateColumnGhost(columnTitle);
            CreateColumnPlaceholder();

            if (_columnsContainer != null)
            {
                _columnsContainer.Insert(columnIndex, _placeholder);
            }

            columnElement.style.display = DisplayStyle.None;
            _currentPlaceholderColumnIndex = columnIndex;
        }

        private static void CreateCardGhost(KanbanCard card, KanbanBoard board, string columnId)
        {
            var cardEl = new CardElement(card, board, columnId);
            cardEl.AddToClassList("drag-ghost-card");
            cardEl.style.position   = Position.Absolute;
            cardEl.style.width      = _ghostWidth;
            cardEl.style.opacity    = 0.9f;
            cardEl.pickingMode      = PickingMode.Ignore;

            _ghostElement = cardEl;
            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top  = _currentGhostPos.y;
            _root.Add(_ghostElement);
        }

        private static string TruncateText(string text, int maxLength) => StringUtils.Truncate(text, maxLength);

        private static void CreateColumnGhost(string columnTitle)
        {
            _ghostElement = new VisualElement();
            _ghostElement.AddToClassList("drag-ghost-column");
            _ghostElement.style.position = Position.Absolute;
            _ghostElement.style.width = _ghostWidth;
            _ghostElement.style.height = 80;
            _ghostElement.pickingMode = PickingMode.Ignore;
            _ghostElement.style.opacity = 0.85f;
            _ghostElement.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _ghostElement.style.borderTopLeftRadius = 8;
            _ghostElement.style.borderTopRightRadius = 8;
            _ghostElement.style.borderBottomLeftRadius = 8;
            _ghostElement.style.borderBottomRightRadius = 8;
            _ghostElement.style.paddingTop = 10;
            _ghostElement.style.paddingLeft = 12;

            var title = new Label(columnTitle);
            title.style.fontSize = 14;
            title.style.color = new Color(0.88f, 0.88f, 0.88f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _ghostElement.Add(title);

            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;
            _root.Add(_ghostElement);
        }

        private static void CreateCardPlaceholder()
        {
            _placeholder = new VisualElement();
            _placeholder.AddToClassList("drag-placeholder");
            _placeholder.style.height = _ghostHeight;
            _placeholder.style.marginBottom = 4;
        }

        private static void CreateColumnPlaceholder()
        {
            _placeholder = new VisualElement();
            _placeholder.AddToClassList("drag-placeholder-column");
            _placeholder.style.width = _ghostWidth;
            _placeholder.style.minWidth = 280;
            _placeholder.style.height = _ghostHeight;
            _placeholder.style.marginRight = 12;
        }

        private static void OnGlobalMouseMove(MouseMoveEvent evt)
        {
            if (!IsDragging || _placeholder == null)
                return;

            var rootRect = _root.worldBound;
            _targetGhostPos = new Vector2(
                evt.mousePosition.x - _dragOffset.x - rootRect.x,
                evt.mousePosition.y - _dragOffset.y - rootRect.y
            );

            float deltaX = evt.mousePosition.x - _lastMousePos.x;
            _targetTilt = Mathf.Clamp(deltaX * 0.3f, -5f, 5f);
            _lastMousePos = evt.mousePosition;

            if (DraggedCardId != null)
            {
                UpdateCardPlaceholder(evt.mousePosition);
            }
            else if (DraggedColumnIndex >= 0)
            {
                UpdateColumnPlaceholder(evt.mousePosition);
            }
        }

        private static void UpdateCardPlaceholder(Vector2 mousePos)
        {
            if (_columnsContainer == null)
                return;

            VisualElement targetColumn = null;
            string targetColumnId = null;

            foreach (var child in _columnsContainer.Children())
            {
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;

                var colRect = child.worldBound;
                if (mousePos.x >= colRect.x && mousePos.x <= colRect.xMax)
                {
                    targetColumn = child;
                    targetColumnId = child.userData as string;
                    break;
                }
            }

            if (targetColumn == null || targetColumnId == null)
                return;

            var cardsContainer = targetColumn.Q<VisualElement>(className: "column-cards");
            if (cardsContainer == null)
                return;

            int insertIdx = 0;
            bool foundPosition = false;

            var children = new List<VisualElement>();
            foreach (var c in cardsContainer.Children())
            {
                if (
                    c != _placeholder
                    && c.ClassListContains("kanban-card")
                    && c.style.display != DisplayStyle.None
                )
                {
                    children.Add(c);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                var cardChild = children[i];
                var cardRect = cardChild.worldBound;
                float cardMidY = cardRect.y + cardRect.height * 0.5f;

                if (mousePos.y < cardMidY)
                {
                    insertIdx = i;
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                insertIdx = children.Count;
            }

            if (
                targetColumnId != _currentPlaceholderColumnId
                || insertIdx != _currentPlaceholderCardIndex
            )
            {
                _placeholder.RemoveFromHierarchy();

                int actualInsertIndex = 0;
                int cardsSeen = 0;
                foreach (var c in cardsContainer.Children())
                {
                    if (c == _placeholder)
                        continue;
                    if (cardsSeen == insertIdx)
                        break;
                    if (c.ClassListContains("kanban-card") && c.style.display != DisplayStyle.None)
                    {
                        cardsSeen++;
                    }
                    actualInsertIndex++;
                }

                if (cardsSeen < insertIdx)
                {
                    actualInsertIndex = cardsContainer.childCount;
                }

                cardsContainer.Insert(actualInsertIndex, _placeholder);
                _currentPlaceholderColumnId = targetColumnId;
                _currentPlaceholderCardIndex = insertIdx;
            }
        }

        private static void UpdateColumnPlaceholder(Vector2 mousePos)
        {
            if (_columnsContainer == null)
                return;

            int insertIdx = 0;
            int columnCount = 0;
            foreach (var child in _columnsContainer.Children())
            {
                if (child == _placeholder)
                    continue;
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.ClassListContains("add-column-button"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;
                columnCount++;
            }

            int visibleIdx = 0;
            bool foundPosition = false;
            foreach (var child in _columnsContainer.Children())
            {
                if (child == _placeholder)
                    continue;
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.ClassListContains("add-column-button"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;

                var colRect = child.worldBound;
                if (mousePos.x < colRect.x + colRect.width * 0.3f)
                {
                    insertIdx = visibleIdx;
                    foundPosition = true;
                    break;
                }
                visibleIdx++;
            }

            if (!foundPosition)
            {
                insertIdx = columnCount;
            }

            insertIdx = Mathf.Clamp(insertIdx, 0, columnCount);

            if (insertIdx != _currentPlaceholderColumnIndex)
            {
                _placeholder.RemoveFromHierarchy();

                int actualIdx = 0;
                int counted = 0;
                foreach (var child in _columnsContainer.Children())
                {
                    if (child == _placeholder)
                        continue;
                    if (child.ClassListContains("add-column-button"))
                        continue;
                    if (
                        child.ClassListContains("kanban-column")
                        && child.style.display != DisplayStyle.None
                    )
                    {
                        if (counted == insertIdx)
                            break;
                        counted++;
                    }
                    actualIdx++;
                }

                actualIdx = Mathf.Clamp(actualIdx, 0, _columnsContainer.childCount - 1);
                _columnsContainer.Insert(actualIdx, _placeholder);
                _currentPlaceholderColumnIndex = insertIdx;
            }
        }

        private static void OnGlobalMouseUp(MouseUpEvent evt)
        {
            if (!IsDragging)
                return;

            if (DraggedCardId != null && _currentPlaceholderColumnId != null)
            {
                _onCardDropped?.Invoke(
                    DraggedCardId,
                    _currentPlaceholderColumnId,
                    _currentPlaceholderCardIndex
                );
            }
            else if (DraggedColumnIndex >= 0 && _currentPlaceholderColumnIndex >= 0)
            {
                _onColumnReorder?.Invoke(DraggedColumnIndex, _currentPlaceholderColumnIndex);
            }

            EndDrag();
        }

        public static void EndDrag()
        {
            if (_draggedElement != null)
            {
                _draggedElement.style.display = DisplayStyle.Flex;
                _draggedElement.style.opacity = 1f;
            }

            if (_ghostElement != null)
            {
                _ghostElement.RemoveFromHierarchy();
                _ghostElement = null;
            }

            if (_placeholder != null)
            {
                _placeholder.RemoveFromHierarchy();
                _placeholder = null;
            }

            IsDragging = false;
            DraggedCardId = null;
            SourceColumnId = null;
            DraggedColumnIndex = -1;
            _draggedElement = null;
            _columnsContainer = null;
            _currentPlaceholderColumnIndex = -1;
            _currentPlaceholderCardIndex = -1;
            _currentPlaceholderColumnId = null;
            _originalCardIndex = -1;

        }
    }
}
