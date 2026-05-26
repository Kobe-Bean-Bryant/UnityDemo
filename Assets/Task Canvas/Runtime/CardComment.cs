using System;

namespace TaskCanvas
{
    [Serializable]
    public class CardComment
    {
        public string id;
        public string text;
        public string author;
        public long createdAt;

        public CardComment()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
#if UNITY_EDITOR
            var unityUsername = UnityEditor.CloudProjectSettings.userName;
            author = !string.IsNullOrEmpty(unityUsername) ? unityUsername : Environment.UserName;
#else
            author = Environment.UserName;
#endif
        }

        public CardComment(string text) : this()
        {
            this.text = text;
        }
    }
}
