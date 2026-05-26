using System;
using UnityEngine;

namespace TaskCanvas
{
    [Serializable]
    public class KanbanColumn
    {
        public string id;
        public string title;
        public Color headerColor = new Color(0.3f, 0.5f, 0.9f);
        public string sortKey;

        public KanbanColumn()
        {
            id = Guid.NewGuid().ToString();
            sortKey = FractionalIndex.First();
        }

        public KanbanColumn(string title) : this()
        {
            this.title = title;
        }

        public KanbanColumn(string title, Color color) : this(title)
        {
            this.headerColor = color;
        }
    }
}
