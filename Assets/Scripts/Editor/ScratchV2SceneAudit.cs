#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only validation of scene wiring and the four-ticket V2 design.
/// This does not run gameplay and never modifies the authored scene.
/// </summary>
public static class ScratchV2SceneAudit
{
    [MenuItem("Tools/Scratch to Riches/Audit V2 Scene")]
    private static void AuditScene()
    {
        int errors = 0;
        ScratchCellManager manager = Object.FindFirstObjectByType<ScratchCellManager>(FindObjectsInactive.Include);
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
        Check(ScratchSkillTree.All.Count == 32 &&
              ScratchSkillTree.All[(int)ScratchSkillTree.SkillId.C3].Implemented &&
              ScratchSkillTree.All[(int)ScratchSkillTree.SkillId.C5].Implemented &&
              ScratchSkillTree.All[(int)ScratchSkillTree.SkillId.C7].Implemented,
              "unlock skills in the 32-node tree", ref errors);

        if (errors == 0) Debug.Log("V2 AUDIT PASS: authored cells, symbols, four tickets, unlocks and shop are wired.");
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
