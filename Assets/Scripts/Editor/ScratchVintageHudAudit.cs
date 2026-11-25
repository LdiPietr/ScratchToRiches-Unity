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
        if (errors == 0) Debug.Log("VINTAGE GAMEPLAY HUD AUDIT PASS");
        else Debug.LogError("VINTAGE GAMEPLAY HUD AUDIT FAILED: " + errors);
    }
}
