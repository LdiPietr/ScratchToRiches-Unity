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
              "second preview locked until Fortunato", ref errors);

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
