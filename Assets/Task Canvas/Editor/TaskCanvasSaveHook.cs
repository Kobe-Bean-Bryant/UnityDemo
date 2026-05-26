using UnityEditor;
using UnityEngine;

namespace TaskCanvas.Editor
{
    public class TaskCanvasSaveHook : UnityEditor.AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            var windows = Resources.FindObjectsOfTypeAll<TaskCanvasWindow>();
            foreach (var window in windows)
            {
                if (window != null)
                {
                    window.ForceSave();
                }
            }

            return paths;
        }
    }
}
