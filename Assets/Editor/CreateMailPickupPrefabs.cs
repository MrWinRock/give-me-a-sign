#if UNITY_EDITOR
using GameLogic;
using GameLogic.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the clickable document prefabs (Paper / Book models + MailPickup) into Assets/Prefabs/Mail.
/// Runs once automatically when the prefabs are missing; also under Tools/Give Me A Sign/Create Mail Pickup Prefabs.
/// </summary>
public static class CreateMailPickupPrefabs
{
    const string Folder = "Assets/Prefabs/Mail";
    const string FbxDir = "Assets/Sprites/Anomaly/3D/";
    const string MailDir = "Assets/Settings/Mail/";

    struct Spec { public string prefab, model, mail; public float scale; public Vector3 center; public Vector2 size; }

    static readonly Spec[] Specs =
    {
        P("Paper_Hider", "Paper", "Mail_007_Hider"),
        P("Paper_LoudMouth", "Paper", "Mail_006_LoudMouth"),
        P("Paper_Watcher", "Paper", "Mail_008_Watcher"),
        P("Paper_Demon", "Paper", "Mail_003_Demon"),
        P("Book_TrueManifest", "Book", "Mail_002_TrueManifest"),
        P("Book_TheBeginning", "Book", "Mail_005_TheBeginning"),
        P("Book_DayOfEvolution", "Book", "Mail_004_DayOfEvolution"),
    };

    // Scale/center come from the FBX vertex bounds (Paper ~2x1.3, Book ~4x2.8 raw units, 0.01 unit factor).
    static Spec P(string prefab, string model, string mail)
    {
        bool paper = model == "Paper";
        return new Spec
        {
            prefab = prefab, model = model, mail = mail,
            scale = paper ? 50f : 25f,
            center = paper ? new Vector3(-2.36f, -11.6f, 4.64f) : Vector3.zero,
            size = paper ? new Vector2(1f, 0.65f) : new Vector2(1f, 0.7f),
        };
    }

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (var s in Specs)
                if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{s.prefab}.prefab")) { Build(); return; }
        };
    }

    [MenuItem("Tools/Give Me A Sign/Create Mail Pickup Prefabs")]
    static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Mail");
        int made = 0;
        foreach (var s in Specs)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{FbxDir}{s.model}.fbx");
            var mail = AssetDatabase.LoadAssetAtPath<MailData>($"{MailDir}{s.mail}.asset");
            if (fbx == null || mail == null) { Debug.LogWarning($"Mail prefab '{s.prefab}' skipped: missing model or MailData."); continue; }

            var root = new GameObject(s.prefab);
            var box = root.AddComponent<BoxCollider2D>();
            box.size = s.size;
            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerName = "Anamoly";
            group.sortingOrder = 2;
            var pickup = root.AddComponent<MailPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("mailData").objectReferenceValue = mail;
            so.ApplyModifiedPropertiesWithoutUndo();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * s.scale;
            model.transform.localPosition = -s.center * (s.scale * 0.01f);

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{s.prefab}.prefab");
            Object.DestroyImmediate(root);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"CreateMailPickupPrefabs: created {made} prefab(s) in {Folder}.");
    }
}
#endif
