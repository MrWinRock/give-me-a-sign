using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomEditor.Hierarchy.Scripts.Data
{
    [Serializable]
    public class ColorEntry
    {
        public string globalObjectId;
        public int colorType;

        public ColorEntry(string id, int type)
        {
            globalObjectId = id;
            colorType = type;
        }
    }

    [CreateAssetMenu(fileName = "HierarchyColorData", menuName = "Custom Editor/Hierarchy Color Data")]
    public class HierarchyColorData : ScriptableObject
    {
        [SerializeField]
        private List<ColorEntry> colorEntries = new List<ColorEntry>();

        private Dictionary<string, int> _colorCache;

        private void OnEnable()
        {
            BuildCache();
        }

        private void BuildCache()
        {
            _colorCache = new Dictionary<string, int>();
            if (colorEntries != null)
            {
                foreach (var entry in colorEntries)
                {
                    if (!string.IsNullOrEmpty(entry.globalObjectId))
                    {
                        _colorCache[entry.globalObjectId] = entry.colorType;
                    }
                }
            }
        }

        public void SetColor(string globalObjectId, int colorType)
        {
            if (_colorCache == null)
            {
                BuildCache();
            }

            if (_colorCache == null)
            {
                return;
            }

            if (colorType == 0)
            {
                RemoveColor(globalObjectId);
                return;
            }

            if (_colorCache.ContainsKey(globalObjectId))
            {
                _colorCache[globalObjectId] = colorType;
                var entry = colorEntries.Find(e => e.globalObjectId == globalObjectId);
                if (entry != null)
                {
                    entry.colorType = colorType;
                }
            }
            else
            {
                var newEntry = new ColorEntry(globalObjectId, colorType);
                colorEntries.Add(newEntry);
                _colorCache[globalObjectId] = colorType;
            }

            MarkDirty();
        }

        public int GetColor(string globalObjectId)
        {
            if (_colorCache == null)
            {
                BuildCache();
            }

            if (_colorCache != null && _colorCache.TryGetValue(globalObjectId, out int colorType))
            {
                return colorType;
            }

            return 0; 
        }

        public bool HasColor(string globalObjectId)
        {
            if (_colorCache == null)
            {
                BuildCache();
            }

            return _colorCache != null && _colorCache.ContainsKey(globalObjectId);
        }

        public void RemoveColor(string globalObjectId)
        {
            if (_colorCache == null)
            {
                BuildCache();
            }

            if (_colorCache != null && _colorCache.ContainsKey(globalObjectId))
            {
                _colorCache.Remove(globalObjectId);
                colorEntries.RemoveAll(e => e.globalObjectId == globalObjectId);
                MarkDirty();
            }
        }

        public void ClearAll()
        {
            colorEntries.Clear();
            if (_colorCache != null)
            {
                _colorCache.Clear();
            }
            MarkDirty();
        }

        public int GetEntryCount()
        {
            return colorEntries != null ? colorEntries.Count : 0;
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}

