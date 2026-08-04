
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

        // 'Set All Anomaly Types To Shadow (test)' was removed here. It stamped the same type
        // string onto every prefab, which is exactly how all 7 anomalies ended up sharing the
        // keyword "Shadow" - indistinguishable to the player. Anomaly identity now lives in
        // AnomalyDefinition assets, one per kind, and DataValidator fails the build data if two
        // kinds ever claim the same keyword again.
    }
}
