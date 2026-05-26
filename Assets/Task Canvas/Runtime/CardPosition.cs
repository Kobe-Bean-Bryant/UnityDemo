using System;

namespace TaskCanvas
{
    [Serializable]
    public class CardPosition
    {
        public string columnId;
        public string sortKey;
        public long updatedAt;

        public CardPosition()
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public CardPosition(string columnId, string sortKey) : this()
        {
            this.columnId = columnId;
            this.sortKey = sortKey;
        }

        public void MarkUpdated()
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
