using System.IO;
using UnityEditor;
using UnityEngine;

namespace alpoLib.Data.Editor
{
    public static class AssetPathHelper
    {
        public static string GetNearestFolderPathFromSelection()
        {
            const string defaultAssetPath = "Assets";
            var targetPath = string.Empty;
            var currentObject = Selection.activeObject;
            if (currentObject)
                targetPath = AssetDatabase.GetAssetPath(currentObject);
            if (string.IsNullOrEmpty(targetPath))
                targetPath = defaultAssetPath;

            var directory = targetPath;
            // directory
            if (Directory.Exists(targetPath))
            {
            }
            // file
            else if (File.Exists(targetPath))
            {
                directory = Path.GetDirectoryName(targetPath);
            }
            
            return directory;
        }
    }
}