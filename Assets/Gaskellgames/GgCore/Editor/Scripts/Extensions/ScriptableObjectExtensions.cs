#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class ScriptableObjectExtensions
    {
        /// <summary>
        /// Get the instance of a scriptable object in the editor.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetEditorInstance<T>(string packageRefName, string relativeFilepath, string fileName) where T : ScriptableObject
        {
            List<T> assetList = EditorExtensions.GetAllAssetsByType<T>();
            int count = assetList.Count;
            
            // check if duplicate files exist: delete all but one
            if (1 < count)
            {
                GgLogs.Log(null, GgLogType.Debug, "{0} {1}'s in files: Deleting {2} lists.", count, typeof(T).Name, count - 1);
                for (int i = 1; i < count; i++)
                {
                    File.Delete(AssetDatabase.GetAssetPath(assetList[i]));
                }
            }
            
            // check if file exists: if not create one
            if (count == 0)
            {
                GgLogs.Log(null, GgLogType.Debug, "{0} {1}'s in files: Creating new.", count, typeof(T).Name);
                T instance = ScriptableObject.CreateInstance<T>();
                instance.name = fileName;
                if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativeFilepath, out string filePath)) { return null; }
                FileExtensions.SaveAssetToFile(instance, filePath, false, false);
                assetList.Add(instance);
            }
            
            AssetDatabase.Refresh();
            return assetList[0];
        }
        
    } // class end
}
#endif