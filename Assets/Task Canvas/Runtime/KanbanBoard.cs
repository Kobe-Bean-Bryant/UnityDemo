using System;
using System.Collections.Generic;

namespace TaskCanvas
{
    [Serializable]
    public class KanbanBoard
    {
        public string boardName = "My Board";
        public List<Assignee> assignees = new List<Assignee>();
        public List<string> allTags = new List<string>();

        [NonSerialized]
        public string boardFolderPath;

        [NonSerialized]
        public List<KanbanColumn> columns;

        public KanbanBoard()
        {
            InitializeDefaults();
        }

        public KanbanBoard(string name)
        {
            boardName = name;
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            if (allTags == null || allTags.Count == 0)
            {
                allTags = new List<string> { "bug", "feature", "urgent" };
            }

            if (assignees == null)
            {
                assignees = new List<Assignee>();
            }

            if (columns == null)
            {
                columns = new List<KanbanColumn>();
            }
        }

        public Assignee GetAssigneeById(string id)
        {
            return assignees.Find(a => a.id == id);
        }

        public void AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && !allTags.Contains(tag))
            {
                allTags.Add(tag);
            }
        }

        public void AddAssignee(Assignee assignee)
        {
            if (assignee != null && !assignees.Exists(a => a.id == assignee.id))
            {
                assignees.Add(assignee);
            }
        }

        public KanbanColumn FindColumnById(string columnId)
        {
            return columns?.Find(c => c.id == columnId);
        }
    }
}
