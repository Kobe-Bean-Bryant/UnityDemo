using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public static class ThemeManager
    {
        private static StyleSheet _darkStyleSheet;

        public static StyleSheet GetCurrentStyleSheet()
        {
            LoadStyleSheet();
            return _darkStyleSheet;
        }

        private static void LoadStyleSheet()
        {
            if (_darkStyleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("Styles-Dark t:StyleSheet");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _darkStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }
        }

        public static void ApplyTheme(VisualElement root)
        {
            LoadStyleSheet();

            if (_darkStyleSheet != null && !root.styleSheets.Contains(_darkStyleSheet))
            {
                root.styleSheets.Add(_darkStyleSheet);
            }
        }
    }
}
