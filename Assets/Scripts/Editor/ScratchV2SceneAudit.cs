#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only validation of authored one-map GDD V2 demo scene wiring.
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
                var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                int mapCount = 0;
                foreach (ScratchHoldToBuy hold in holds)
                {
                    if (string.IsNullOrEmpty(hold.StableSkillId)) continue;
                    mapCount++;
                    Check(ids.Add(hold.StableSkillId), "unique square node " + hold.StableSkillId, ref errors);
                    Check(hold.gameObject.activeSelf && hold.name == "MapSkillNode_" + hold.StableSkillId,
                        "active authored square node " + hold.StableSkillId, ref errors);
                    RectTransform rt = hold.GetComponent<RectTransform>();
                    Check(rt != null && Mathf.Abs(rt.rect.width - rt.rect.height) < 0.1f,
                        "square node proportions " + hold.StableSkillId, ref errors);
                    Check(hold.GetComponent<UnityEngine.UI.Image>() != null &&
                          hold.GetComponentInChildren<TMPro.TMP_Text>(true) != null,
                        "authored node art and label " + hold.StableSkillId, ref errors);
                }
                Check(mapCount == 90 && ids.Count == 90, "all ninety map nodes present", ref errors);
                foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
                    Check(ids.Contains(entry.id), "map contains " + entry.id, ref errors);
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
        Check(ScratchGameManager.PickLimitFromPerk(0) == 3 &&
              ScratchGameManager.PickLimitFromPerk(1) == 4 &&
              ScratchGameManager.PickLimitFromPerk(2) == 5 &&
              ScratchGameManager.PickLimitFromPerk(3) == 5,
              "C11 unlocks global picks 3/4/5 regardless of ticket", ref errors);
        Check(ScratchSkillCatalogV2.Find("C11")?.demoEligible == true &&
              ScratchSkillCatalogV2.Find("C11")?.maxLevel == 2 &&
              ScratchSkillCatalogV2.Find("C11")?.prerequisites == "C01:1" &&
              ScratchSkillCatalogV2.EffectImplemented("C11"),
              "C11 permanent extra scratch perk authored in catalog", ref errors);
        if (game != null)
            Check(game.TicketPrize(ScratchGameManager.TicketKind.Fortunato) == 50 &&
                  game.TicketPrize(ScratchGameManager.TicketKind.Gold) == 110 &&
                  game.TicketPrize(ScratchGameManager.TicketKind.Diamond) == 250 &&
                  game.TicketPrize(ScratchGameManager.TicketKind.Standard) <
                  game.TicketPrize(ScratchGameManager.TicketKind.Fortunato) / 3,
                  "ticket prizes strongly separated from independent scratch count", ref errors);

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
        Check(availableInDemo == 25, "25 demo-eligible / 65 full-game-only nodes", ref errors);
        Check(missingImplementation == 0, "all 25 demo effects implemented", ref errors);
        Check(implementedOutsideDemo == 0, "no full-game-only upgrade enabled", ref errors);
        Check(ScratchSkillTree.DemoEdition, "demo edition gating enabled", ref errors);

        // C01 stays early, C03 unlocks Fortunato, then C02 adds the
        // powerful second preview. Keep this order and its requirements.
        Check(ScratchSkillCatalogV2.At(1, 0)?.id == "C01" &&
              ScratchSkillCatalogV2.At(1, 1)?.id == "C03" &&
              ScratchSkillCatalogV2.At(1, 2)?.id == "C02" &&
              ScratchSkillCatalogV2.At(1, 3)?.id == "C11" &&
              ScratchSkillCatalogV2.At(1, 10)?.id == "C04",
              "early ticket skill layout C01-C03-C02-C11, C04 shifted outward", ref errors);
        Check(ScratchSkillCatalogV2.Find("C03")?.prerequisites == "C01:1" &&
              ScratchSkillCatalogV2.Find("C02")?.prerequisites == "C03:1" &&
              ScratchSkillCatalogV2.Find("C04")?.prerequisites == "C02:1",
              "C04 keeps local C02 prerequisite; radial chapter 4 replaces G07", ref errors);
        Check(ScratchRadialProgression.ChapterOf("C01") == 1 &&
              ScratchRadialProgression.ChapterOf("C03") == 2 &&
              ScratchRadialProgression.ChapterOf("C11") == 2 &&
              ScratchRadialProgression.ChapterOf("C04") == 4 &&
              ScratchRadialProgression.ChapterOf("G08") == 5 &&
              ScratchRadialProgression.ChapterOf("A08") == 7,
              "radial nucleus chapter assignment", ref errors);
        Check(ScratchRadialProgression.ChapterFromMilestones(0, 0, 0, 0, false, false, false, true) == 1 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 0, 0, 0, false, false, false, true) == 2 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 2, 0, 0, true, false, false, true) == 3 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 0, 0, true, false, false, true) == 4 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 5, 0, true, true, false, true) == 5 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 4, 0, true, true, false, true) == 4 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 5, 6, true, true, true, false) == 6 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 5, 25, true, true, true, false) == 7 &&
              ScratchRadialProgression.ChapterFromMilestones(3, 9, 5, 70, true, true, true, false) == 8,
              "radial chapters unlock strictly from real gameplay milestones", ref errors);
        Check(ScratchRadialProgression.RequiredFortunato("E06") == 16 &&
              ScratchRadialProgression.RequiredFortunato("G07") == 19 &&
              ScratchRadialProgression.RequiredFortunato("C04") == 22 &&
              ScratchRadialProgression.RequiredFortunato("C05") == 24 &&
              ScratchRadialProgression.RequiredGold("G08") == 5 &&
              ScratchRadialProgression.RequiredGold("F02") == 8 &&
              ScratchRadialProgression.RequiredGold("C06") == 11 &&
              ScratchRadialProgression.RequiredGold("F03") == 14 &&
              ScratchRadialProgression.RequiredGold("F04") == 17 &&
              !ScratchRadialProgression.TicketGoalMet("G08", 24, 4) &&
              ScratchRadialProgression.TicketGoalMet("G08", 24, 5) &&
              !ScratchRadialProgression.TicketGoalMet("C05", 23, 0) &&
              ScratchRadialProgression.TicketGoalMet("C05", 24, 0),
              "Demo pacing: progressive ring 4/5 milestones", ref errors);
        Check(ScratchSkillCatalogV2.Find("C06")?.description.Contains("clic destro") == true &&
              ScratchSkillCatalogV2.Find("C06")?.prerequisites == "C05:1",
              "C06 is player-selected right-click inspection, not five random previews", ref errors);
        Check(ScratchSkillCatalogV2.Find("G08")?.prerequisites == "G07:1;G05:1;C05:1",
              "G08 Titan is a late-game skill only after Golden ticket", ref errors);
        Check(ScratchGameManager.FinalChallengeComplete(8, 3800, true) &&
              !ScratchGameManager.FinalChallengeComplete(7, 3800, true) &&
              !ScratchGameManager.FinalChallengeComplete(8, 3799, true) &&
              !ScratchGameManager.FinalChallengeComplete(8, 3800, false),
              "earned-performance finale requires tickets, payout, and power", ref errors);

        ScratchSkillTreeUI tree = manager.GetComponent<ScratchSkillTreeUI>();
        Check(tree != null, "skill tree interface component", ref errors);
        ScratchRadialLinks links = Object.FindAnyObjectByType<ScratchRadialLinks>(FindObjectsInactive.Include);
        Check(links != null && !links.raycastTarget,
              "authored radial spokes graphic does not intercept clicks", ref errors);
        if (tree != null)
        {
            SerializedObject so = new SerializedObject(tree);
            SerializedProperty nucleus = so.FindProperty("nucleusButton");
            SerializedProperty nucleusLabel = so.FindProperty("nucleusLabel");
            SerializedProperty connections = so.FindProperty("radialLinks");
            SerializedProperty navigation = so.FindProperty("navigation");
            Check(nucleus != null && nucleus.objectReferenceValue != null &&
                  nucleusLabel != null && nucleusLabel.objectReferenceValue != null &&
                  connections != null && connections.objectReferenceValue == links &&
                  navigation != null && navigation.objectReferenceValue != null,
                  "authored map nucleus, spokes and pan/zoom wired", ref errors);
            foreach (string field in new[]
                { "selectedTitle", "selectedDescription", "statusLabel" })
            {
                SerializedProperty prop = so.FindProperty(field);
                Check(prop != null && prop.objectReferenceValue != null,
                    "fixed bottom hover details: " + field, ref errors);
            }
            ScratchRadialMapNavigation mapNav =
                Object.FindAnyObjectByType<ScratchRadialMapNavigation>(FindObjectsInactive.Include);
            Check(mapNav != null && mapNav.GetComponent<UnityEngine.UI.RectMask2D>() != null,
                  "navigable map has a scene-authored clipping viewport", ref errors);
            if (mapNav != null)
            {
                SerializedObject nav = new SerializedObject(mapNav);
                foreach (string field in new[] { "viewport", "content",
                    "zoomInButton", "zoomOutButton" })
                    Check(nav.FindProperty(field) != null &&
                          nav.FindProperty(field).objectReferenceValue != null,
                          "pan/zoom reference: " + field, ref errors);
            }
        }

        CheckDomainRules(ref errors);
        if (errors == 0) Debug.Log("V2 AUDIT PASS: single navigable 90-icon skill map, bottom hover, pan/zoom and ticket UI wired.");
        else Debug.LogError("V2 AUDIT FAILED: " + errors + " error(s). See Console for details.");
    }

    private static void CheckDomainRules(ref int errors)
    {
        string[] labels = { "A", "B", "C" };
        var pair = ScratchMatchEvaluator.Evaluate(new[] { 0, 1, 0 }, 7, labels);
        var tie = ScratchMatchEvaluator.Evaluate(new[] { 1, 0, 1, 0 }, 7, labels);
        var joker = ScratchMatchEvaluator.Evaluate(new[] { 2, 2, 7 }, 7, labels);
        var loneJoker = ScratchMatchEvaluator.Evaluate(new[] { 7 }, 7, labels);
        Check(pair.Tier == ScratchSymbolManager.MatchTier.Pair && pair.Symbol == "A" &&
              tie.Symbol == "B" && joker.Tier == ScratchSymbolManager.MatchTier.Triple &&
              loneJoker.Tier == ScratchSymbolManager.MatchTier.None,
              "pure matching policy: pair, tie, revealed joker, lone joker", ref errors);

        var reward = ScratchRewardPolicy.Calculate(pair, true, 50, 10, 10, 0, _ => 0);
        var streak = ScratchRewardPolicy.Calculate(pair, true, 50, 10, 10, 4,
            id => id == ScratchSkillTree.SkillId.E6 ? 1 : 0);
        var loss = ScratchRewardPolicy.Calculate(loneJoker, true, 50, 10, 10, 4,
            id => id == ScratchSkillTree.SkillId.E5 ? 1 : 0);
        var incomplete = ScratchRewardPolicy.Calculate(loneJoker, false, 50, 10, 10, 4,
            id => id == ScratchSkillTree.SkillId.E5 ? 1 : 0);
        Check(reward.RequestedCoins == 100 && reward.WinningStreak == 1 &&
              streak.RequestedCoins == 120 && streak.WinningStreak == 4 &&
              loss.RequestedCoins == 5 && loss.WinningStreak == 0 && !incomplete.HasAward,
              "pure reward policy: base, streak, consolation, incomplete ticket", ref errors);
    }

    private static void Check(bool condition, string description, ref int errors)
    {
        if (condition) return;
        errors++;
        Debug.LogError("V2 AUDIT: " + description + " FAILED");
    }
}
#endif
