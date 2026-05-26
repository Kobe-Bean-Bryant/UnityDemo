using UnityEngine;

namespace TaskCanvas.Editor
{
    public static class IconHelper
    {
        private static bool? _supportsEmoji;

        public static bool SupportsEmoji
        {
            get
            {
                if (_supportsEmoji == null)
                {
#if UNITY_2023_2_OR_NEWER
                    _supportsEmoji = true;
#else
                    _supportsEmoji = false;
#endif
                }
                return _supportsEmoji.Value;
            }
        }

        public static string DueDateIcon => SupportsEmoji ? "📅" : "Due";
        public static string ChecklistIcon => SupportsEmoji ? "☑" : "✓";
        public static string AttachmentIcon => SupportsEmoji ? "📎" : "@";
        public static string CommentIcon => SupportsEmoji ? "💬" : "#";
    }
}
