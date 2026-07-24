using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CustomEditor.Hierarchy.Scripts.Data
{
    public static class HierarchyColorDataManager
    {
        private const string DataPath = "Assets/CustomEditor/Hierarchy/Scripts/Data/HierarchyColorData.asset";
        private static HierarchyColorData _instance;

        public static HierarchyColorData Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<HierarchyColorData>(DataPath);

                    if (_instance == null)
                    {
                        _instance = ScriptableObject.CreateInstance<HierarchyColorData>();

                        string directory = System.IO.Path.GetDirectoryName(DataPath);
                        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                        {
                            System.IO.Directory.CreateDirectory(directory);
                        }

                        AssetDatabase.CreateAsset(_instance, DataPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log($"Created new HierarchyColorData asset at {DataPath}");
                    }
                }
#endif
                return _instance;
            }
        }

        public static void SetColor(string globalObjectId, int colorType)
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                Instance.SetColor(globalObjectId, colorType);
                AssetDatabase.SaveAssets();
            }
#endif
        }

        public static int GetColor(string globalObjectId)
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                return Instance.GetColor(globalObjectId);
            }
#endif
            return 0;
        }

        public static bool HasColor(string globalObjectId)
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                return Instance.HasColor(globalObjectId);
            }
#endif
            return false;
        }

        public static void RemoveColor(string globalObjectId)
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                Instance.RemoveColor(globalObjectId);
                AssetDatabase.SaveAssets();
            }
#endif
        }

        public static void ClearAll()
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                Instance.ClearAll();
                AssetDatabase.SaveAssets();
            }
#endif
        }

        public static int GetEntryCount()
        {
#if UNITY_EDITOR
            if (Instance != null)
            {
                return Instance.GetEntryCount();
            }
#endif
            return 0;
        }

        public static void ReloadData()
        {
#if UNITY_EDITOR
            _instance = null;
            _ = Instance;
#endif
        }
    }
}

