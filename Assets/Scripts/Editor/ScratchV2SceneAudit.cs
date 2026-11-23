#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only validation of five-tab GDD V2 demo scene wiring.
/// This does not run gameplay and never modifies the authored scene.
/// </summary>
public static class ScratchV2SceneAudit
{
    [MenuItem("Tools/Scratch to Riches/Audit V2 Scene")]
    private static void AuditScene()
    {
        int errors = 0;
        ScratchCellManager manager = Object.FindAnyObjectByType<ScratchCellManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("V2 AUDIT: ScratchCellManager not found in the open scene.");
            return;
        }

        ScratchCell[] cells = manager.GetComponentsInChildren<ScratchCell>(true);
        Check(cells.Length == 25, "25 authored scratch cells", ref errors);
        for (int i = 1; i <= 25; i++)
        {
            string expected = "Cell_" + i.ToString("00");
            bool found = false;
            foreach (ScratchCell cell in cells)
                if (cell.name == expected && cell.HasSymbolDisplay) { found = true; break; }
            Check(found, expected + " sprite renderer", ref errors);
        }

        ScratchGameManager game = manager.GetComponent<ScratchGameManager>();
        ScratchTicketChoiceUI chooser = manager.GetComponent<ScratchTicketChoiceUI>();
        ScratchFlowPresentation flow = manager.GetComponent<ScratchFlowPresentation>();
        ScratchSymbolManager symbols = manager.GetComponent<ScratchSymbolManager>();
        Check(game != null && chooser != null && flow != null && symbols != null,
            "all core managers", ref errors);
        if (chooser != null)
        {
            SerializedObject so = new SerializedObject(chooser);
            Check(so.FindProperty("ticketButtons").arraySize == 4 &&
                  so.FindProperty("ticketDetails").arraySize == 4,
                  "four ticket card references", ref errors);
            for (int i = 0; i < 4; i++)
                Check(so.FindProperty("ticketButtons").GetArrayElementAtIndex(i).objectReferenceValue != null,
                      "ticket choice button " + (i + 1), ref errors);
        }

        if (flow != null)
        {
            SerializedObject so = new SerializedObject(flow);
            Check(so.FindProperty("shopButton").objectReferenceValue != null,
                  "clickable shop button", ref errors);
            SerializedProperty contentProperty = so.FindProperty("shopContent");
            GameObject shopPanel = contentProperty == null
                ? null : contentProperty.objectReferenceValue as GameObject;
            Check(shopPanel != null, "assigned skill tree panel", ref errors);
            if (shopPanel != null)
            {
                Check(shopPanel.activeSelf, "skill tree panel activeSelf (curtain parent may remain hidden)", ref errors);
                Check(shopPanel.name == "SkillTreePanel", "expected skill panel reference", ref errors);
                SerializedProperty curtainProperty = so.FindProperty("curtainObject");
                GameObject curtain = curtainProperty.objectReferenceValue as GameObject;
                Check(curtain != null && shopPanel.transform.IsChildOf(curtain.transform),
                    "skill tree panel parented to shop curtain", ref errors);
                ScratchHoldToBuy[] holds = shopPanel.GetComponentsInChildren<ScratchHoldToBuy>(true);
                Check(holds.Length == 8, "eight scratch skill node hold controls", ref errors);
                for (int i = 0; i < holds.Length; i++)
                    Check(holds[i].gameObject.activeSelf,
                        "skill hold node local activeSelf: " + holds[i].gameObject.name, ref errors);
            }
        }
        if (symbols != null)
        {
            SerializedObject so = new SerializedObject(symbols);
            SerializedProperty sprites = so.FindProperty("symbolSprites");
            Check(sprites.arraySize == 7, "seven source symbols", ref errors);
            for (int i = 0; i < sprites.arraySize; i++)
                Check(sprites.GetArrayElementAtIndex(i).objectReferenceValue != null,
                    "symbol artwork " + (i + 1), ref errors);
        }

        Check(ScratchGameManager.TicketSide(ScratchGameManager.TicketKind.Standard) == 3 &&
              ScratchGameManager.TicketSide(ScratchGameManager.TicketKind.Fortunato) == 4 &&
              ScratchGameManager.TicketSide(ScratchGameManager.TicketKind.Gold) == 5 &&
              ScratchGameManager.TicketSide(ScratchGameManager.TicketKind.Diamond) == 5,
              "ticket grid sides 3/4/5/5", ref errors);
        Check(ScratchGameManager.SymbolPoolSize(ScratchGameManager.TicketKind.Diamond) == 4,
              "Diamond four-symbol pool", ref errors);
        Check(ScratchSkillCatalogV2.All.Count == 90,
              "GDD V2 contains exactly 90 catalog entries", ref errors);
        int availableInDemo = 0;
        int missingImplementation = 0;
        int implementedOutsideDemo = 0;
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
        {
            if (entry.demoEligible)
            {
                availableInDemo++;
                if (!ScratchSkillCatalogV2.EffectImplemented(entry.id))
                    missingImplementation++;
            }
            else if (ScratchSkillCatalogV2.EffectImplemented(entry.id))
                implementedOutsideDemo++;
        }
        Check(availableInDemo == 24, "24 demo-eligible / 66 full-game-only nodes", ref errors);
        Check(missingImplementation == 0, "all 24 demo effects implemented", ref errors);
        Check(implementedOutsideDemo == 0, "no full-game-only upgrade enabled", ref errors);
        Check(ScratchSkillTree.DemoEdition, "demo edition gating enabled", ref errors);

        // C01 stays early, C03 unlocks Fortunato, then C02 adds the
        // powerful second preview. Keep this order and its requirements.
        Check(ScratchSkillCatalogV2.At(1, 0)?.id == "C01" &&
              ScratchSkillCatalogV2.At(1, 1)?.id == "C03" &&
              ScratchSkillCatalogV2.At(1, 2)?.id == "C02" &&
              ScratchSkillCatalogV2.At(1, 3)?.id == "C04",
              "ticket skill UI order C01-C03-C02-C04", ref errors);
        Check(ScratchSkillCatalogV2.Find("C03")?.prerequisites == "C01:1" &&
              ScratchSkillCatalogV2.Find("C02")?.prerequisites == "C03:1" &&
              ScratchSkillCatalogV2.Find("C04")?.prerequisites == "C02:1",
              "C04 keeps local C02 prerequisite; radial chapter 4 replaces G07", ref errors);
        Check(ScratchRadialProgression.ChapterOf("C01") == 1 &&
              ScratchRadialProgression.ChapterOf("C03") == 2 &&
              ScratchRadialProgression.ChapterOf("C04") == 4 &&
              ScratchRadialProgression.ChapterOf("G08") == 5 &&
              ScratchRadialProgression.ChapterOf("A08") == 7,
              "radial nucleus chapter assignment", ref errors);
        Check(ScratchRadialProgression.ChapterFromMilestones(0, 0, 0, 0, false, false, false, true) == 1 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 0, 0, 0, false, false, false, true) == 2 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 2, 0, 0, true, false, false, true) == 3 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 0, 0, true, false, false, true) == 4 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 3, 0, true, true, false, true) == 5 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 3, 6, true, true, true, false) == 6 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 3, 25, true, true, true, false) == 7 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 3, 70, true, true, true, false) == 8,
              "radial chapters unlock strictly from real gameplay milestones", ref errors);
        Check(ScratchSkillCatalogV2.Find("C06")?.description.Contains("clic destro") == true &&
              ScratchSkillCatalogV2.Find("C06")?.prerequisites == "C05:1",
              "C06 is player-selected right-click inspection, not five random previews", ref errors);
        Check(ScratchSkillCatalogV2.Find("G08")?.prerequisites == "G07:1;G05:1;C05:1",
              "G08 Titan is a late-game skill only after Golden ticket", ref errors);
        Check(ScratchGameManager.FinalChallengeComplete(3, 1800, true) &&
              !ScratchGameManager.FinalChallengeComplete(2, 1800, true) &&
              !ScratchGameManager.FinalChallengeComplete(3, 1799, true) &&
              !ScratchGameManager.FinalChallengeComplete(3, 1800, false),
              "earned-performance finale requires tickets, payout, and power", ref errors);

        ScratchSkillTreeUI tree = manager.GetComponent<ScratchSkillTreeUI>();
        Check(tree != null, "skill tree interface component", ref errors);
        ScratchRadialLinks links = manager.GetComponentInChildren<ScratchRadialLinks>(true);
        Check(links != null && !links.raycastTarget,
              "authored radial spokes graphic does not intercept clicks", ref errors);
        if (tree != null)
        {
            SerializedObject so = new SerializedObject(tree);
            SerializedProperty tabs = so.FindProperty("branchButtons");
            SerializedProperty nodes = so.FindProperty("nodeButtons");
            SerializedProperty nucleus = so.FindProperty("nucleusButton");
            SerializedProperty nucleusLabel = so.FindProperty("nucleusLabel");
            SerializedProperty connections = so.FindProperty("radialLinks");
            Check(nucleus != null && nucleus.objectReferenceValue != null &&
                  nucleusLabel != null && nucleusLabel.objectReferenceValue != null &&
                  connections != null && connections.objectReferenceValue == links,
                  "authored radial nucleus button, label and connectors wired", ref errors);
            Check(tabs != null && tabs.arraySize == 5, "five authored skill tabs", ref errors);
            Check(nodes != null && nodes.arraySize == 8, "eight authored reusable skill slots", ref errors);
            if (tabs != null && tabs.arraySize == 5)
                for (int i = 0; i < tabs.arraySize; i++)
                    Check(tabs.GetArrayElementAtIndex(i).objectReferenceValue != null,
                        "authored tab " + (i + 1), ref errors);
            foreach (string field in new[] { "previousPageButton", "nextPageButton", "pageLabel" })
            {
                SerializedProperty prop = so.FindProperty(field);
                Check(prop != null && prop.objectReferenceValue != null,
                    "authored page navigation: " + field, ref errors);
            }
        }

        if (errors == 0) Debug.Log("V2 AUDIT PASS: 90-node catalog, five tabs, paging, authored cells and ticket UI wired.");
        else Debug.LogError("V2 AUDIT FAILED: " + errors + " error(s). See Console for details.");
    }

    private static void Check(bool condition, string description, ref int errors)
    {
        if (condition) return;
        errors++;
        Debug.LogError("V2 AUDIT: " + description + " FAILED");
    }
}
#endif
