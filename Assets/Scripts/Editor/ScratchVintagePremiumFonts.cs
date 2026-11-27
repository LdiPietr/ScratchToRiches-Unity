#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>One-time, edit-mode only. Imports user-selected static font files and assigns TMP assets to authored popup labels.</summary>
internal static class ScratchVintagePremiumFonts
{
    private const string ScenePath = "Assets/Scenes/ScratchToRiches.unity";
    private const string Folder = "Assets/Art/UI/VintagePremium/Fonts";
    private const string Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ÀÁÂÄÇÈÉÊËÌÍÎÏÑÒÓÔÖÙÚÛÜÝàáâäçèéêëìíîïñòóôöùúûüýÿ·–—−’‘“”€…\u00a0\u202f";

    [MenuItem("Tools/Scratch to Riches/Vintage Premium/Importa font popup")]
    private static void ImportPopupFonts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorUtility.DisplayDialog("Vintage Premium", "Apri ScratchToRiches.unity in Edit Mode.", "OK");
            return;
        }
        var flow = UnityEngine.Object.FindAnyObjectByType<ScratchFlowPresentation>(FindObjectsInactive.Include);
        var hud = UnityEngine.Object.FindAnyObjectByType<ScratchVintageGameplayHUD>(FindObjectsInactive.Include);
        if (flow == null || hud == null) throw new InvalidOperationException("Manager della scena assenti.");
        var f = new SerializedObject(flow);
        var h = new SerializedObject(hud);
        string[] fields = { "resultPopupHeading", "resultPopupAmount", "repeatTicketLabel", "resultRepeatCost",
            "nextTicketLabel", "resultUpgradesLabel", "resultUpgradeCount" };
        var texts = new TMP_Text[fields.Length + 1];
        for (int i = 0; i < fields.Length; i++)
            texts[i] = f.FindProperty(fields[i])?.objectReferenceValue as TMP_Text;
        texts[fields.Length] = h.FindProperty("resultSummary")?.objectReferenceValue as TMP_Text;
        foreach (var label in texts)
            if (label == null) throw new InvalidOperationException("Riferimento TMP del popup assente; verifica il pull.");
        // Select both files before touching the project. Static instances are required,
        // not the variable files with axis names in square brackets.
        string displayPath = SelectIfNeeded("FrauncesDisplay", "Fraunces Bold Italic (file statico TTF/OTF)");
        if (displayPath == null) return;
        string bodyPath = SelectIfNeeded("LibreBaskervilleUI", "Libre Baskerville Regular (file statico TTF/OTF)");
        if (bodyPath == null) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/UI/VintagePremium", "Fonts");
        var display = CreateOrLoad("FrauncesDisplay", displayPath);
        var body = CreateOrLoad("LibreBaskervilleUI", bodyPath);
        if (display == null || body == null) return;
        Undo.RecordObjects(texts, "Assign Vintage Premium popup fonts");
        for (int i = 0; i < texts.Length; i++)
        {
            bool hero = i <= 2;
            texts[i].font = hero ? display : body;
            texts[i].fontSharedMaterial = texts[i].font.material;
            texts[i].fontStyle = hero || i == 3 || i == 7 ? FontStyles.Normal : FontStyles.Bold;
            EditorUtility.SetDirty(texts[i]);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        EditorSceneManager.SaveScene(flow.gameObject.scene);
        Debug.Log("VINTAGE PREMIUM FONTS: assigned and saved scene. Run transition QA and inspect the result.");
    }

    private static string SelectIfNeeded(string name, string title)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/" + name + ".asset") != null) return "";
        string path = EditorUtility.OpenFilePanel(title, "", "");
        if (string.IsNullOrEmpty(path)) return null;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if ((ext != ".ttf" && ext != ".otf") || Path.GetFileName(path).Contains("["))
        {
            EditorUtility.DisplayDialog("Font non valido", "Seleziona un'istanza statica TTF oppure OTF.", "OK");
            return null;
        }
        return path;
    }

    private static TMP_FontAsset CreateOrLoad(string name, string source)
    {
        string assetPath = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        string path = Folder + "/" + name + Path.GetExtension(source).ToLowerInvariant();
        if (!File.Exists(path)) File.Copy(source, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (font == null) throw new InvalidOperationException("Importazione font fallita: " + path);
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA,
            2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (asset == null) throw new InvalidOperationException("Creazione atlas TMP fallita: " + name);
        asset.name = name;
        asset.TryAddCharacters(Characters, out string missing);
        // Keep dynamic atlas support for localization; the current Italian/Latin set is already baked.
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning(name + ": caratteri non presenti: " + missing);
        AssetDatabase.CreateAsset(asset, assetPath);
        if (asset.material != null && !AssetDatabase.Contains(asset.material))
        {
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        foreach (var tex in asset.atlasTextures)
            if (tex != null && !AssetDatabase.Contains(tex))
            {
                tex.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }
}
#endif
