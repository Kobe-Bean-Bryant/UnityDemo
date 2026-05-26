using UnityEngine;
using UnityEditor;
using System.IO;

namespace TaskCanvas.Editor
{
    // [CreateAssetMenu(fileName = "TaskCanvasSettings", menuName = "Task Canvas/Settings")] // Optional: normally internal
    public class TaskCanvasSettings : ScriptableObject
    {
        private const string DEFAULT_BOARDS_FOLDER = "Assets/TaskCanvasData";
        private const string SETTINGS_PATH = "Assets/Task Canvas/Editor/Resources/TaskCanvasSettings.asset";

        [SerializeField]
        private string _boardsFolder = DEFAULT_BOARDS_FOLDER;
        
        [SerializeField]
        private int _columnWidth = 280;
        
        [SerializeField]
        private bool _confirmDelete = true;
        
        [SerializeField]
        private string _theme = "Dark";

        public string BoardsFolder
        {
            get => string.IsNullOrEmpty(_boardsFolder) ? DEFAULT_BOARDS_FOLDER : _boardsFolder;
            set
            {
                if (_boardsFolder != value)
                {
                    _boardsFolder = value;
                    Save();
                }
            }
        }
        
        public int ColumnWidth
        {
            get => _columnWidth;
            set
            {
                if (_columnWidth != value)
                {
                    _columnWidth = value;
                    Save();
                }
            }
        }
        
        public bool ConfirmDelete
        {
            get => _confirmDelete;
            set
            {
                if (_confirmDelete != value)
                {
                    _confirmDelete = value;
                    Save();
                }
            }
        }
        
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    Save();
                }
            }
        }

        private static TaskCanvasSettings _instance;
        public static TaskCanvasSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }
                return _instance;
            }
        }

        private static TaskCanvasSettings LoadSettings()
        {
            // Try to load from Resources first (runtime/editor friendly if moved to resources)
            // But since this is Editor only, we can load by path.
            
            // Note: Since the file needs to be in a specific location to look organized, 
            // we'll enforce the path.
            
            var settings = AssetDatabase.LoadAssetAtPath<TaskCanvasSettings>(SETTINGS_PATH);
            if (settings == null)
            {
                settings = CreateInstance<TaskCanvasSettings>();
                
                // Migrate from EditorPrefs if possible (first time setup)
                MigrateFromEditorPrefs(settings);
                
                var directory = Path.GetDirectoryName(SETTINGS_PATH);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }
            
            return settings;
        }

        private static void MigrateFromEditorPrefs(TaskCanvasSettings settings)
        {
            // If the user has local preferences, migrate them to the shared settings 
            // so they don't lose their config on first update.
            
            if (EditorPrefs.HasKey("TaskCanvas_BoardsFolder"))
                settings._boardsFolder = EditorPrefs.GetString("TaskCanvas_BoardsFolder");
                
            if (EditorPrefs.HasKey("TaskCanvas_ColumnWidth"))
                settings._columnWidth = EditorPrefs.GetInt("TaskCanvas_ColumnWidth");
                
            if (EditorPrefs.HasKey("TaskCanvas_ConfirmDelete"))
                settings._confirmDelete = EditorPrefs.GetBool("TaskCanvas_ConfirmDelete");
                
            if (EditorPrefs.HasKey("TaskCanvas_Theme"))
                settings._theme = EditorPrefs.GetString("TaskCanvas_Theme");
        }

        private void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
