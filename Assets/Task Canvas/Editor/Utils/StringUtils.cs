namespace TaskCanvas.Editor
{
    public static class StringUtils
    {
        public static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}
