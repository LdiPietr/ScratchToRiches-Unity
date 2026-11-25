using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ScratchVintageHudAudit
{
    public static void Audit()
    {
        var hud = Object.FindAnyObjectByType<ScratchVintageGameplayHUD>(FindObjectsInactive.Include);
        if (hud == null) { Debug.LogError("Open ScratchToRiches first."); return; }
        var so = new SerializedObject(hud);
        var it = so.GetIterator();
        int errors = 0;
        while (it.NextVisible(true))
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null)
            { errors++; Debug.LogError("HUD missing reference: " + it.propertyPath); }
        }
        var stage = hud.transform.Find("VintageGameplayStage") as RectTransform;
        if (stage == null || stage.sizeDelta != new Vector2(1920,1080))
        { errors++; Debug.LogError("HUD stage missing or incorrectly sized."); }
        var indicators = so.FindProperty("indicators");
        if (indicators.arraySize != 5) { errors++; Debug.LogError("HUD must contain five authored pick indicators."); }
        var symbols = so.FindProperty("matchSymbols");
        if (symbols == null || symbols.arraySize != 5)
        { errors++; Debug.LogError("HUD must contain five authored match symbol slots."); }
        else
        {
            for (int i = 0; i < symbols.arraySize; i++)
            {
                var image = symbols.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                if (image == null || image.raycastTarget || !image.preserveAspect)
                { errors++; Debug.LogError("Invalid authored match symbol slot " + i); }
            }
        }
        if (so.FindProperty("matchJolly")?.objectReferenceValue == null)
        { errors++; Debug.LogError("Missing authored Jolly slot."); }
        foreach (var spec in new[] { "matchSlotFrames", "matchSlotQuestions", "prizeRequirements" })
        {
            var array = so.FindProperty(spec);
            int expected = spec == "prizeRequirements" ? 4 : 5;
            if (array == null || array.arraySize != expected)
            { errors++; Debug.LogError("Invalid authored array: " + spec); continue; }
            for (int i = 0; i < expected; i++)
            {
                var graphic = array.GetArrayElementAtIndex(i).objectReferenceValue as Graphic;
                if (graphic == null || graphic.raycastTarget)
                { errors++; Debug.LogError("Invalid authored graphic: " + spec + " " + i); }
            }
        }
        var names = so.FindProperty("prizeNames");
        for (int i = 0; i < names.arraySize; i++)
        {
            var label = names.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
            if (label == null || label.enableAutoSizing || !Mathf.Approximately(label.fontSize, 28f))
            { errors++; Debug.LogError("Prize names must have the same fixed font size."); }
        }
        if (errors == 0) Debug.Log("VINTAGE GAMEPLAY HUD AUDIT PASS");
        else Debug.LogError("VINTAGE GAMEPLAY HUD AUDIT FAILED: " + errors);
    }
}
