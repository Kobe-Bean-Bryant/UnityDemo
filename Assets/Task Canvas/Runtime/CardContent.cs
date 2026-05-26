using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskCanvas
{
    [Serializable]
    public class CardContent
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

        public CardContent()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            updatedAt = createdAt;
        }

        public CardContent(string title) : this()
        {
            this.title = title;
        }

        public void MarkUpdated()
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
