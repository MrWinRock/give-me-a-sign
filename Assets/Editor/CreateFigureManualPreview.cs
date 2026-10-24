#if UNITY_EDITOR
using System.IO;
using GameLogic.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a small portrait of the Figure model into Assets/Sprites/FieldManual/Figure_Preview.png and
/// assigns it as Anomaly_Figure.manualImage. Runs once automatically while the image is missing;
/// also under Tools/Give Me A Sign/Create Figure Manual Preview (re-run to refresh after changing the model).
/// </summary>
public static class CreateFigureManualPreview
{
    const string PngPath = "Assets/Sprites/FieldManual/Figure_Preview.png";
    const string DefPath = "Assets/Settings/Anomalies/Anomaly_Figure.asset";
    const string TriedKey = "CreateFigureManualPreview.tried";

    [InitializeOnLoadMethod]
    static void Auto()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(TriedKey, false)) return;
            var def = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(DefPath);
            if (def == null || def.manualImage != null || def.prefab == null) return;
            SessionState.SetBool(TriedKey, true);
            Build();
        };
    }

    [MenuItem("Tools/Give Me A Sign/Create Figure Manual Preview")]
    static void Build()
    {
        var def = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(DefPath);
        if (def == null || def.prefab == null) { Debug.LogWarning("CreateFigureManualPreview: Anomaly_Figure or its prefab is missing."); return; }

        GameObject model = null, camGo = null;
        var rt = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            model = Object.Instantiate(def.prefab);
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.position = new Vector3(5000f, 5000f, 0f);

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogWarning("CreateFigureManualPreview: the Figure prefab has no renderers."); return; }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            camGo = new GameObject("FigurePreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.size.y, b.size.x) * 0.5f * 1.12f;
            cam.transform.position = b.center + new Vector3(0f, 0f, -20f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f); // dark CCTV-style backdrop
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PngPath));
            File.WriteAllBytes(PngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (model != null) Object.DestroyImmediate(model);
        }

        AssetDatabase.ImportAsset(PngPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(PngPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();

        def.manualImage = AssetDatabase.LoadAssetAtPath<Sprite>(PngPath);
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        Debug.Log("CreateFigureManualPreview: saved " + PngPath + " and assigned it to Anomaly_Figure. Open the image to check it is not blank.");
    }
}
#endif
