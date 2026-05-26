using System;

namespace TaskCanvas
{
    public enum AttachmentType
    {
        Link,
        File
    }

    [Serializable]
    public class CardAttachment
    {
        public string id;
        public string name;
        public string url;
        public AttachmentType type;
        public long addedAt;

        public CardAttachment()
        {
            id = Guid.NewGuid().ToString();
            addedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public CardAttachment(string name, string url, AttachmentType type) : this()
        {
            this.name = name;
            this.url = url;
            this.type = type;
        }
    }
}
