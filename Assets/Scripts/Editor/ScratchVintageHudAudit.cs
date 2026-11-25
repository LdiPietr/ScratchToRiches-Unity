using System.Collections.Generic;
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
        var jolly = so.FindProperty("matchJolly")?.objectReferenceValue as Image;
        if (jolly == null || jolly.sprite == null || jolly.sprite.name != "sym_jolly_hud" || !jolly.preserveAspect)
        { errors++; Debug.LogError("Missing authored Jolly artwork."); }
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
        if (!ValidateRightPanels(hud, out string detail))
        { errors++; Debug.LogError(detail, hud); }
        if (errors == 0) Debug.Log("VINTAGE GAMEPLAY HUD AUDIT PASS");
        else Debug.LogError("VINTAGE GAMEPLAY HUD AUDIT FAILED: " + errors);
    }

    // This checks the actual loaded objects, not just the scene file on disk.
    public static bool ValidateRightPanels(ScratchVintageGameplayHUD hud, out string detail)
    {
        if (hud == null) { detail = "HUD assente nella scena caricata."; return false; }
        var so = new SerializedObject(hud);
        var errors = new List<string>();
        var scene = hud.gameObject.scene;
        if (scene.path != "Assets/Scenes/ScratchToRiches.unity")
            errors.Add("Scena errata: " + scene.path);
        foreach (string field in new[] { "matchSlotFrames", "matchSlotQuestions", "prizeRequirements" })
        {
            var array = so.FindProperty(field);
            int expected = field == "prizeRequirements" ? 4 : 5;
            if (array == null || !array.isArray || array.arraySize != expected)
            { errors.Add(field + ": array non valido"); continue; }
            var unique = new HashSet<Graphic>();
            for (int i = 0; i < expected; i++)
            {
                var graphic = array.GetArrayElementAtIndex(i).objectReferenceValue as Graphic;
                if (graphic == null) { errors.Add(field + "[" + i + "]: riferimento assente"); continue; }
                if (graphic.gameObject.scene != scene || !graphic.transform.IsChildOf(hud.transform) ||
                    !unique.Add(graphic) || graphic.raycastTarget)
                    errors.Add(field + "[" + i + "]: oggetto non valido");
                string expectedName = field == "matchSlotFrames" ? "MatchSlotFrame_" + (i + 1) :
                    field == "matchSlotQuestions" ? "MatchSlotQuestion_" + (i + 1) :
                    "GameplayPrizeRequirement_" + i;
                if (graphic.name != expectedName) errors.Add(field + "[" + i + "]: " + graphic.name);
                if (field == "matchSlotFrames")
                {
                    // Fixed square slots use the complete square artwork, not a 9-slice.
                    // The importer border is irrelevant to a Simple image.
                    const string framePath = "Assets/Art/UI/S02_Classic/Panels/MatchSlotFrame.png";
                    string slot = field + "[" + i + "]";
                    if (!(graphic is Image frame))
                        errors.Add(slot + ": componente Image assente");
                    else
                    {
                        if (frame.sprite == null)
                            errors.Add(slot + ": sprite assente (" + framePath + ")");
                        else if (AssetDatabase.GetAssetPath(frame.sprite) != framePath)
                            errors.Add(slot + ": sprite errato (" + AssetDatabase.GetAssetPath(frame.sprite) + ")");
                        if (frame.type != Image.Type.Simple || !frame.preserveAspect)
                            errors.Add(slot + ": atteso Simple con Preserve Aspect; type=" + frame.type +
                                "; preserveAspect=" + frame.preserveAspect);
                        if (frame.rectTransform.sizeDelta != new Vector2(70f, 70f))
                            errors.Add(slot + ": dimensioni errate " + frame.rectTransform.sizeDelta);
                    }
                }
            }
        }
        foreach (string field in new[] { "prizeLabel", "prizeFooter" })
        {
            var label = so.FindProperty(field)?.objectReferenceValue as TMP_Text;
            if (label == null || label.horizontalAlignment != HorizontalAlignmentOptions.Center)
                errors.Add(field + ": allineamento centrale assente");
        }
        // The lower card occupies y=-440..-742 in the authored right column.
        var footer = so.FindProperty("prizeFooter")?.objectReferenceValue as TMP_Text;
        var refund = so.FindProperty("refundLabel")?.objectReferenceValue as TMP_Text;
        var amounts = so.FindProperty("prizeAmounts");
        if (footer != null && refund != null && amounts != null && amounts.arraySize == 4)
        {
            var f = footer.rectTransform;
            var r = refund.rectTransform;
            var last = amounts.GetArrayElementAtIndex(3).objectReferenceValue as TMP_Text;
            if (f.anchoredPosition.y - f.sizeDelta.y < -730f ||
                r.anchoredPosition.y - r.sizeDelta.y < f.anchoredPosition.y + 4f ||
                (last != null && last.rectTransform.anchoredPosition.y -
                    last.rectTransform.sizeDelta.y < r.anchoredPosition.y + 4f))
                errors.Add("Vincite possibili: margini verticali non validi");
        }
        var divider = so.FindProperty("matchDivider")?.objectReferenceValue as GameObject;
        if (divider == null || divider.name != "MatchDivider" || divider.scene != scene)
            errors.Add("matchDivider: riferimento assente o errato");
        detail = "HUD punto 2 | scena=" + scene.path + " | dirty=" + scene.isDirty +
            " | " + (errors.Count == 0 ? "PASS" : string.Join("; ", errors));
        return errors.Count == 0;
    }

    public static bool ValidateFreshTicket(ScratchGameManager game, out string detail)
    {
        var hud = game.GetComponent<ScratchVintageGameplayHUD>();
        if (!ValidateRightPanels(hud, out detail)) return false;
        var so = new SerializedObject(hud);
        var errors = new List<string>();
        foreach (string field in new[] { "matchSlotFrames", "matchSlotQuestions", "prizeRequirements" })
        {
            var array = so.FindProperty(field);
            for (int i = 0; i < array.arraySize; i++)
            {
                var graphic = array.GetArrayElementAtIndex(i).objectReferenceValue as Graphic;
                bool expected = field == "prizeRequirements" ? game.MaxScratches < i + 2 : i < game.MaxScratches;
                if (graphic.gameObject.activeInHierarchy != expected ||
                    (expected && (!graphic.enabled || graphic.color.a <= .01f)))
                    errors.Add(field + "[" + i + "]: visibilita' " + graphic.gameObject.activeInHierarchy +
                        ", attesa " + expected);
                if (expected && graphic is TMP_Text label &&
                    label.text != (field == "prizeRequirements" ? (i + 2) + " grattate" : "?"))
                    errors.Add(field + "[" + i + "]: testo " + label.text);
            }
        }
        var divider = so.FindProperty("matchDivider").objectReferenceValue as GameObject;
        if (!divider.activeSelf) errors.Add("MatchDivider non visibile: layout instabile");
        var payout = so.FindProperty("payout")?.objectReferenceValue as TMP_Text;
        if (payout == null || !payout.gameObject.activeSelf || payout.text != "0")
            errors.Add("Vincita iniziale: atteso zero visibile");
        detail += " | ticket=" + game.TicketNumber + " | max=" + game.MaxScratches +
            " | " + (errors.Count == 0 ? "VISIBILITY PASS" : string.Join("; ", errors));
        return errors.Count == 0;
    }
    public static bool ValidateMatchProgress(ScratchGameManager game, out string detail)
    {
        var hud = game.GetComponent<ScratchVintageGameplayHUD>();
        if (!ValidateRightPanels(hud, out detail)) return false;
        var so = new SerializedObject(hud);
        var errors = new List<string>();
        var frames = so.FindProperty("matchSlotFrames");
        var questions = so.FindProperty("matchSlotQuestions");
        var symbols = so.FindProperty("matchSymbols");
        var jolly = so.FindProperty("matchJolly").objectReferenceValue as Image;
        int max = game.MaxScratches;
        int revealed = game.TicketVoided ? 0 : game.RevealedCount;
        int visibleSymbols = jolly != null && jolly.gameObject.activeSelf ? 1 : 0;
        int winningFrames = 0;
        float x = 221f - (max * 70f + Mathf.Max(0, max - 1) * 8f) * .5f;
        for (int i = 0; i < frames.arraySize; i++)
        {
            var frame = frames.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            var question = questions.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
            var symbol = symbols.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            bool expected = !game.TicketVoided && i < max;
            if (frame.gameObject.activeSelf != expected)
                errors.Add("frame " + i + ": visibilita'");
            if (expected && Vector2.Distance(frame.rectTransform.anchoredPosition,
                    new Vector2(x + i * 78f, -137f)) > .1f)
                errors.Add("frame " + i + ": posizione variata");
            if (question.gameObject.activeSelf != (expected && i >= revealed && game.TicketActive))
                errors.Add("question " + i + ": visibilita'");
            if (symbol != null && symbol.gameObject.activeSelf) visibleSymbols++;
            if (expected && frame.color.r > .85f && frame.color.g > .7f && frame.color.b < .8f)
                winningFrames++;
        }
        if (visibleSymbols != revealed) errors.Add("simboli visibili=" + visibleSymbols + ", attesi=" + revealed);
        int expectedWinning = !game.TicketVoided && game.CurrentMatch.IsMatch ? game.CurrentMatch.Multiplier : 0;
        if (winningFrames != expectedWinning)
            errors.Add("cornici vincenti=" + winningFrames + ", attese=" + expectedWinning);
        detail += " | " + revealed + "/" + max + " | " +
            (errors.Count == 0 ? "MATCH PROGRESS PASS" : string.Join("; ", errors));
        return errors.Count == 0;
    }

}
