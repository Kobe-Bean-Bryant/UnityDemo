using System;
using System.Collections.Generic;

namespace TaskCanvas.Editor
{
    public class CardCache
    {
        private readonly Dictionary<string, KanbanCard> _cache = new Dictionary<string, KanbanCard>();
        private readonly LinkedList<string> _accessOrder = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _nodeMap = new Dictionary<string, LinkedListNode<string>>();
        private readonly HashSet<string> _dirtyContent = new HashSet<string>();
        private readonly HashSet<string> _dirtyPosition = new HashSet<string>();
        private readonly HashSet<string> _deletedCards = new HashSet<string>();

        private string _boardFolderPath;
        private int _maxSize;

        public event Action<string> OnCardSaved;
        public event Action<string> OnCardLoaded;

        public CardCache(string boardFolderPath, int maxSize = 100)
        {
            _boardFolderPath = boardFolderPath;
            _maxSize = maxSize;
        }

        public int CachedCount => _cache.Count;
        public int DirtyCount => _dirtyContent.Count + _dirtyPosition.Count;
        public bool HasDirtyCards => _dirtyContent.Count > 0 || _dirtyPosition.Count > 0;

        public KanbanCard GetCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;

            if (_cache.TryGetValue(cardId, out var card))
            {
                MoveToFront(cardId);
                return card;
            }

            card = BoardSerializer.LoadCard(_boardFolderPath, cardId);
            if (card != null)
            {
                AddToCache(cardId, card);
                OnCardLoaded?.Invoke(cardId);
            }

            return card;
        }

        public bool TryGetCached(string cardId, out KanbanCard card)
        {
            if (_cache.TryGetValue(cardId, out card))
            {
                MoveToFront(cardId);
                return true;
            }
            return false;
        }

        public bool IsCached(string cardId)
        {
            return _cache.ContainsKey(cardId);
        }

        public void SetCard(KanbanCard card, string columnId = null, string sortKey = null)
        {
            if (card == null || string.IsNullOrEmpty(card.id))
                return;

            if (columnId != null)
                card.columnId = columnId;

            if (sortKey != null)
                card.sortKey = sortKey;

            AddToCache(card.id, card);
            MarkContentDirty(card.id);
            MarkPositionDirty(card.id);
        }

        public void SetCardContent(KanbanCard card)
        {
            if (card == null || string.IsNullOrEmpty(card.id))
                return;

            if (_cache.TryGetValue(card.id, out var existing))
            {
                existing.title = card.title;
                existing.description = card.description;
                existing.color = card.color;
                existing.priority = card.priority;
                existing.tags = card.tags;
                existing.assigneeIds = card.assigneeIds;
                existing.isCompleted = card.isCompleted;
                existing.updatedAt = card.updatedAt;
                existing.dueDate = card.dueDate;
                existing.isDueDateComplete = card.isDueDateComplete;
                existing.checklist = card.checklist;
                existing.estimatedSeconds = card.estimatedSeconds;
                existing.trackedSeconds = card.trackedSeconds;
                existing.timerStartedAt = card.timerStartedAt;
                existing.attachments = card.attachments;
                existing.unityAssets = card.unityAssets;
                existing.comments = card.comments;
                existing.coverImageGUID = card.coverImageGUID;
                existing.coverImagePath = card.coverImagePath;
            }
            else
            {
                AddToCache(card.id, card);
            }

            MarkContentDirty(card.id);
        }

        public void MoveCard(string cardId, string newColumnId, string newSortKey)
        {
            if (string.IsNullOrEmpty(cardId))
                return;

            var card = GetCard(cardId);
            if (card == null)
                return;

            card.columnId = newColumnId;
            card.sortKey = newSortKey;
            MarkPositionDirty(cardId);
        }

        public void MarkContentDirty(string cardId)
        {
            if (!string.IsNullOrEmpty(cardId) && _cache.ContainsKey(cardId))
            {
                _dirtyContent.Add(cardId);
            }
        }

        public void MarkPositionDirty(string cardId)
        {
            if (!string.IsNullOrEmpty(cardId) && _cache.ContainsKey(cardId))
            {
                _dirtyPosition.Add(cardId);
            }
        }

        public bool IsContentDirty(string cardId) => _dirtyContent.Contains(cardId);
        public bool IsPositionDirty(string cardId) => _dirtyPosition.Contains(cardId);

        public void SaveCard(string cardId)
        {
            if (!_cache.TryGetValue(cardId, out var card))
                return;

            if (_dirtyContent.Contains(cardId))
            {
                BoardSerializer.SaveCardContent(_boardFolderPath, card.ToContent());
                _dirtyContent.Remove(cardId);
            }

            if (_dirtyPosition.Contains(cardId))
            {
                BoardSerializer.SaveCardPosition(_boardFolderPath, cardId, card.ToPosition());
                _dirtyPosition.Remove(cardId);
            }

            OnCardSaved?.Invoke(cardId);
        }

        public void FlushDirtyCards()
        {
            var allDirty = new HashSet<string>(_dirtyContent);
            allDirty.UnionWith(_dirtyPosition);

            foreach (var cardId in allDirty)
            {
                SaveCard(cardId);
            }
        }

        public void DeleteCard(string cardId)
        {
            RemoveFromCache(cardId);
            _dirtyContent.Remove(cardId);
            _dirtyPosition.Remove(cardId);
            _deletedCards.Add(cardId);
            BoardSerializer.DeleteCard(_boardFolderPath, cardId);
        }

        public void Clear()
        {
            FlushDirtyCards();
            _cache.Clear();
            _accessOrder.Clear();
            _nodeMap.Clear();
            _dirtyContent.Clear();
            _dirtyPosition.Clear();
        }

        public void SetBoardPath(string newPath)
        {
            FlushDirtyCards();
            _boardFolderPath = newPath;
        }

        public List<KanbanCard> GetCardsInColumn(string columnId)
        {
            var cards = BoardSerializer.GetCardsInColumn(_boardFolderPath, columnId);
            cards.RemoveAll(c => _deletedCards.Contains(c.id));
            return cards;
        }

        public IEnumerable<string> GetDirtyCardIds()
        {
            var allDirty = new HashSet<string>(_dirtyContent);
            allDirty.UnionWith(_dirtyPosition);
            return allDirty;
        }

        public void InvalidateCard(string cardId)
        {
            RemoveFromCache(cardId);
        }

        public void InvalidateAll()
        {
            _cache.Clear();
            _accessOrder.Clear();
            _nodeMap.Clear();
        }

        public void DiscardLocalChanges()
        {
            _dirtyContent.Clear();
            _dirtyPosition.Clear();
            _deletedCards.Clear();
            InvalidateAll();
        }

        private void AddToCache(string cardId, KanbanCard card)
        {
            if (_cache.ContainsKey(cardId))
            {
                _cache[cardId] = card;
                MoveToFront(cardId);
            }
            else
            {
                _cache[cardId] = card;
                var node = _accessOrder.AddFirst(cardId);
                _nodeMap[cardId] = node;

                while (_cache.Count > _maxSize)
                {
                    EvictLeastRecentlyUsed();
                }
            }
        }

        private void RemoveFromCache(string cardId)
        {
            if (_cache.Remove(cardId))
            {
                if (_nodeMap.TryGetValue(cardId, out var node))
                {
                    _accessOrder.Remove(node);
                    _nodeMap.Remove(cardId);
                }
            }
        }

        private void MoveToFront(string cardId)
        {
            if (_nodeMap.TryGetValue(cardId, out var node))
            {
                _accessOrder.Remove(node);
                _nodeMap[cardId] = _accessOrder.AddFirst(cardId);
            }
        }

        private void EvictLeastRecentlyUsed()
        {
            if (_accessOrder.Count == 0)
                return;

            var lruCardId = _accessOrder.Last.Value;

            if (_dirtyContent.Contains(lruCardId) || _dirtyPosition.Contains(lruCardId))
            {
                SaveCard(lruCardId);
            }

            RemoveFromCache(lruCardId);
        }
    }
}
