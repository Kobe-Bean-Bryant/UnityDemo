using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskCanvas
{
    [Serializable]
    public class KanbanCard
    {
        public string id;
        public string title;
        public string description;
        public Color color = new Color(0.5f, 0.7f, 1f);
        public int priority;
        public List<string> tags = new List<string>();
        public List<string> assigneeIds = new List<string>();
        public bool isCompleted;
        public long createdAt;
        public long updatedAt;
        public long dueDate;
        public bool isDueDateComplete;
        public List<ChecklistItem> checklist = new List<ChecklistItem>();
        public int estimatedSeconds;
        public int trackedSeconds;
        public long timerStartedAt;
        public List<CardAttachment> attachments = new List<CardAttachment>();
        public List<UnityAssetReference> unityAssets = new List<UnityAssetReference>();
        public List<CardComment> comments = new List<CardComment>();
        public string coverImageGUID;
        public string coverImagePath;

        [NonSerialized]
        public string columnId;

        [NonSerialized]
        public string sortKey;

        public KanbanCard()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            updatedAt = createdAt;
        }

        public KanbanCard(string title) : this()
        {
            this.title = title;
        }

        public void MarkUpdated()
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public CardContent ToContent()
        {
            return new CardContent
            {
                id = id,
                title = title,
                description = description,
                color = color,
                priority = priority,
                tags = new List<string>(tags ?? new List<string>()),
                assigneeIds = new List<string>(assigneeIds ?? new List<string>()),
                isCompleted = isCompleted,
                createdAt = createdAt,
                updatedAt = updatedAt,
                dueDate = dueDate,
                isDueDateComplete = isDueDateComplete,
                checklist = checklist != null ? new List<ChecklistItem>(checklist) : new List<ChecklistItem>(),
                estimatedSeconds = estimatedSeconds,
                trackedSeconds = trackedSeconds,
                timerStartedAt = timerStartedAt,
                attachments = attachments != null ? new List<CardAttachment>(attachments) : new List<CardAttachment>(),
                unityAssets = unityAssets != null ? new List<UnityAssetReference>(unityAssets) : new List<UnityAssetReference>(),
                comments = comments != null ? new List<CardComment>(comments) : new List<CardComment>(),
                coverImageGUID = coverImageGUID,
                coverImagePath = coverImagePath
            };
        }

        public CardPosition ToPosition()
        {
            return new CardPosition
            {
                columnId = columnId,
                sortKey = sortKey,
                updatedAt = updatedAt
            };
        }

        public static KanbanCard FromContentAndPosition(CardContent content, CardPosition position)
        {
            if (content == null) return null;

            var card = new KanbanCard
            {
                id = content.id,
                title = content.title,
                description = content.description,
                color = content.color,
                priority = content.priority,
                tags = content.tags ?? new List<string>(),
                assigneeIds = content.assigneeIds ?? new List<string>(),
                isCompleted = content.isCompleted,
                createdAt = content.createdAt,
                updatedAt = content.updatedAt,
                dueDate = content.dueDate,
                isDueDateComplete = content.isDueDateComplete,
                checklist = content.checklist ?? new List<ChecklistItem>(),
                estimatedSeconds = content.estimatedSeconds,
                trackedSeconds = content.trackedSeconds,
                timerStartedAt = content.timerStartedAt,
                attachments = content.attachments ?? new List<CardAttachment>(),
                unityAssets = content.unityAssets ?? new List<UnityAssetReference>(),
                comments = content.comments ?? new List<CardComment>(),
                coverImageGUID = content.coverImageGUID,
                coverImagePath = content.coverImagePath
            };

            if (position != null)
            {
                card.columnId = position.columnId;
                card.sortKey = position.sortKey;
            }

            return card;
        }
    }
}
