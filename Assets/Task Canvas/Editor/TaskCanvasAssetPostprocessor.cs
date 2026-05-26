using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TaskCanvas.Editor
{
    public class TaskCanvasAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var allPaths = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths);

            bool taskCanvasFilesChanged = allPaths.Any(path =>
                path.EndsWith("board.json") || 
                (path.Contains("/cards/") && path.EndsWith(".json")) ||
                (path.Contains("/columns/") && path.EndsWith(".json")));

            if (taskCanvasFilesChanged)
            {
                EditorApplication.delayCall += NotifyTaskCanvasWindows;
            }
        }

        private static void NotifyTaskCanvasWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<TaskCanvasWindow>();
            foreach (var window in windows)
            {
                window.OnExternalAssetsChanged();
            }
        }
    }
}
