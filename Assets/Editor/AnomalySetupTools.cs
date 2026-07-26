using System.IO;
using GameLogic;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// One-off setup helpers for the Anomaly Incident Report fields: creates the shared
    /// AnomalyOptionsCatalog asset if it doesn't exist yet, and can batch-fill every
    /// Anomaly prefab's "Correct Anomaly Type" with a single test value.
    /// </summary>
    public static class AnomalySetupTools
    {
        private const string CatalogFolder = "Assets/Settings";
        private const string CatalogPath = CatalogFolder + "/AnomalyOptions.asset";

        [MenuItem("Tools/Give Me A Sign/Create Anomaly Options Catalog (if missing)")]
        public static void CreateCatalogIfMissing()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnomalyOptionsCatalog>(CatalogPath);
            if (existing != null)
            {
                Debug.Log($"AnomalySetupTools: catalog already exists at {CatalogPath}");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(CatalogFolder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var catalog = ScriptableObject.CreateInstance<AnomalyOptionsCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"AnomalySetupTools: created catalog at {CatalogPath}");
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        /// <summary>
        /// Sets every Anomaly prefab's Correct Anomaly Type to "Shadow" for testing the
        /// voice-matching flow end to end before real per-anomaly names are decided.
        /// Correct Location Name is left untouched (Incident Report Manager's
        /// "Require Correct Location" is off by default, so it isn't checked yet anyway).
        /// </summary>
        [MenuItem("Tools/Give Me A Sign/Set All Anomaly Types To Shadow (test)")]
        public static void SetAllAnomalyTypesToShadow()
        {
            CreateCatalogIfMissing();

            const string prefabFolder = "Assets/Prefabs";
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
            int updated = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var anomaly = contents.GetComponentInChildren<Anomaly>(true);
                    if (anomaly == null) continue;

                    anomaly.correctAnomalyType = "Shadow";
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    updated++;
                    Debug.Log($"AnomalySetupTools: set correctAnomalyType = \"Shadow\" on {Path.GetFileName(path)}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            Debug.Log($"AnomalySetupTools: updated {updated} Anomaly prefab(s).");
        }
    }
}
