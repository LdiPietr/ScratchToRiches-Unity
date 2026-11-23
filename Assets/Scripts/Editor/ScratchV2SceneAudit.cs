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
        Check(ScratchGameManager.SymbolPoolSize(ScratchGameManager.TicketKind.Standard) == 7 &&
              ScratchGameManager.SymbolPoolSize(ScratchGameManager.TicketKind.Fortunato) == 6 &&
              ScratchGameManager.SymbolPoolSize(ScratchGameManager.TicketKind.Gold) == 5 &&
              ScratchGameManager.SymbolPoolSize(ScratchGameManager.TicketKind.Diamond) == 4,
              "independent symbol pools 7/6/5/4", ref errors);
        Check(ScratchTicketRules.PickLimit(0, 3) == 3 &&
              ScratchTicketRules.PickLimit(1, 3) == 4 &&
              ScratchTicketRules.PickLimit(2, 4) == 5 &&
              ScratchTicketRules.PickLimit(3, 5) == 6 &&
              ScratchTicketRules.PickLimit(100, 3) == 6,
              "C11 global pick levels 3/4/5/6", ref errors);
        Check(ScratchTicketRules.BasePrize(ScratchGameManager.TicketKind.Standard, 10) == 10 &&
              ScratchTicketRules.BasePrize(ScratchGameManager.TicketKind.Fortunato, 10) == 30 &&
              ScratchTicketRules.BasePrize(ScratchGameManager.TicketKind.Gold, 10) == 55,
              "ticket payout profiles 10/30/55", ref errors);
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
        Check(availableInDemo == 25, "25 demo-eligible / 65 full-game-only nodes", ref errors);
        Check(missingImplementation == 0, "all 25 demo effects implemented", ref errors);
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
              ScratchSkillCatalogV2.Find("C04")?.prerequisites == "C02:1;G07:1",
              "third preview only after continuous scratch G07", ref errors);
        Check(ScratchSkillCatalogV2.Find("C06")?.description.Contains("clic destro") == true &&
              ScratchSkillCatalogV2.Find("C06")?.prerequisites == "C05:1",
              "C06 is player-selected right-click inspection, not five random previews", ref errors);
        Check(ScratchSkillCatalogV2.Find("G08")?.prerequisites == "G07:1;G05:1;C05:1",
              "G08 Titan is a late-game skill only after Golden ticket", ref errors);
        Check(ScratchSkillCatalogV2.Find("C11")?.maxLevel == 3 &&
              ScratchSkillCatalogV2.Find("C11")?.prerequisites == "C01:1" &&
              ScratchSkillCatalogV2.Find("C11")?.demoEligible == true &&
              ScratchSkillCatalogV2.EffectImplemented("C11"),
              "C11 is a purchasable three-level global pick perk", ref errors);
        Check(ScratchGameManager.FinalChallengeComplete(3, 1800, true) &&
              !ScratchGameManager.FinalChallengeComplete(2, 1800, true) &&
              !ScratchGameManager.FinalChallengeComplete(3, 1799, true) &&
              !ScratchGameManager.FinalChallengeComplete(3, 1800, false),
              "earned-performance finale requires tickets, payout, and power", ref errors);

        ScratchSkillTreeUI tree = manager.GetComponent<ScratchSkillTreeUI>();
        Check(tree != null, "skill tree interface component", ref errors);
        if (tree != null)
        {
            SerializedObject so = new SerializedObject(tree);
            SerializedProperty tabs = so.FindProperty("branchButtons");
            SerializedProperty nodes = so.FindProperty("nodeButtons");
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

        if (errors == 0) Debug.Log("V2 AUDIT PASS: 90-node catalog, C11 global pick perk, 7/6/5/4 pools and authored UI wired.");
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
