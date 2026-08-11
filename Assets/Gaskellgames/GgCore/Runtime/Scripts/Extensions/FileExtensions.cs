#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class FileExtensions
    {
        /// <summary>
        /// Creates a new native Unity asset in the specified folder. Also creates the parent directory if required!
        /// </summary>
        /// <param name="assetToSave"></param>
        /// <param name="relativeFilePath"> E.g "FileName" not "Assets/Filename"</param>
        /// <param name="pingObject"></param>
        /// <param name="openFileExplorer"></param>
        public static void SaveAssetToFile(Object assetToSave, string relativeFilePath, bool pingObject = true, bool openFileExplorer = false)
        {
            GgLogs.Log(null, GgLogType.Debug, "SaveAssetToFile: AssetPath = {0}/{1}.asset", relativeFilePath, assetToSave.name);
            
            relativeFilePath = relativeFilePath.RemoveAssetFolderIfAtStart();
            CreateFolderPathIfRequired(relativeFilePath);
            
            string filePath = string.Format("Assets/{0}/{1}.asset", relativeFilePath, assetToSave.name);
            AssetDatabase.CreateAsset(assetToSave, filePath);
            AssetDatabase.SaveAssets();
            
            if (pingObject) { EditorGUIUtility.PingObject(assetToSave); }
            if (openFileExplorer)
            {
                string targetPath = string.IsNullOrEmpty(relativeFilePath)
                    ? Application.dataPath
                    : Path.Combine(Application.dataPath, relativeFilePath);
                OpenFileExplorer(targetPath);
            }
        }
        
        /// <summary>
        /// Create a folder directory path, inside the Application.dataPath, if any folders are non-existent.
        /// </summary>
        /// <param name="relativeFilePath"> E.g "FileName" not "Assets/Filename"</param>
        /// <returns></returns>
        public static bool CreateFolderPathIfRequired(string relativeFilePath)
        {
            relativeFilePath = relativeFilePath.RemoveAssetFolderIfAtStart();
            string targetPath = string.IsNullOrEmpty(relativeFilePath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativeFilePath);
            
            if (Directory.Exists(targetPath)) { return false; }
            Directory.CreateDirectory(targetPath);
            return true;
        }
        
        /// <summary>
        /// Open the file explorer at a specified file path or folder path
        /// </summary>
        /// <param name="filePath"></param>
        public static void OpenFileExplorer(string filePath)
        {
            filePath = filePath.Replace(@"/", @"\"); // explorer doesn't like front slashes
            Process.Start("explorer.exe", "/select," + filePath);
        }
        
        /// <summary>
        /// Checks whether a string path is a valid folder path
        /// </summary>
        /// <param name="relativeFolderPath"> E.g "FolderName" not "Assets/FolderName"</param>
        /// <returns></returns>
        public static bool IsFolderPathValid(string relativeFolderPath)
        {
            relativeFolderPath = relativeFolderPath.RemoveAssetFolderIfAtStart();
            string targetPath = string.IsNullOrEmpty(relativeFolderPath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativeFolderPath);

            return Directory.Exists(targetPath);
        }
        
        /// <summary>
        /// Checks whether a string path is a valid file path
        /// </summary>
        /// <param name="relativeFilePath"> E.g "FileName" not "Assets/Filename"</param>
        /// <returns></returns>
        public static bool IsFilePathValid(string relativeFilePath)
        {
            relativeFilePath = relativeFilePath.RemoveAssetFolderIfAtStart();
            string targetPath = string.IsNullOrEmpty(relativeFilePath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativeFilePath);

            return File.Exists(targetPath);
        }
        
        /// <summary>
        /// Gets the relative file path for the users desktop
        /// </summary>
        /// <returns></returns>
        public static string DesktopFilePath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        
        private static string RemoveAssetFolderIfAtStart(this string relativeFilePath)
        {
            string[] folders = relativeFilePath.Split( new char[] {'/'});
            if (folders[0] == "Assets")
            {
                List<string> foldersList = folders.ToList();
                foldersList.RemoveAt(0);
                folders = foldersList.ToArray();
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < folders.Length; i++)
                {
                    string folder = folders[i];
                    sb.Append((0 < i) ? $"/{folder}" : folder);
                }
                relativeFilePath = sb.ToString();
            }
            return relativeFilePath;
        }

    } // class end
}
#endif
