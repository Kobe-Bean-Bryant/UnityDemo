using System;

namespace TaskCanvas
{
    [Serializable]
    public class ChecklistItem
    {
        public string id;
        public string text;
        public bool isCompleted;

        public ChecklistItem()
        {
            id = Guid.NewGuid().ToString();
        }

        public ChecklistItem(string text) : this()
        {
            this.text = text;
        }
    }
}
