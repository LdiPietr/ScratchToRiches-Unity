using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rendering responsibility of ScratchSkillTreeUI.
public sealed partial class ScratchSkillTreeUI
{
    public void Refresh()
    {
        if (skills == null || economy == null || game == null) return;
        int chapter = ScratchRadialProgression.UnlockedChapter(game, skills);
        if (nucleusLabel != null)
            nucleusLabel.text = "NUCLEO\nLV " + chapter +
                "\n" + ScratchRadialProgression.ChapterNames[chapter - 1];
        purchasableIds.Clear();
        foreach (ScratchSkillCatalogV2.Entry e in ScratchSkillCatalogV2.All)
            if (skills.CanPurchase(e.id)) purchasableIds.Add(e.id);
        purchasableIds.Sort(ComparePurchasable);
        if (availableFocusIndex >= purchasableIds.Count)
            availableFocusIndex = -1;

        if (pageLabel != null)
        {
            if (purchasableIds.Count == 0)
                pageLabel.text = "<color=#AB6222>NESSUN ACQUISTO ORA</color>  ·  " +
                    ScratchRadialProgression.NextGoal(game, skills) +
                    "  ·  VERDE = ACQUISTABILE";
            else
            {
                string id = purchasableIds[0];
                ScratchSkillCatalogV2.Entry next = ScratchSkillCatalogV2.Find(id);
                pageLabel.text = "● " + purchasableIds.Count +
                    " ACQUISTABILI   ·   " + id + " " +
                    next.title.ToUpperInvariant() + " · " +
                    skills.GetPrice(id).ToString("N0") +
                    " MONETE   ·   CLICCA QUI / TAB PER TROVARLE";
            }
            if (availableJumpButton != null)
                availableJumpButton.interactable = purchasableIds.Count > 0;
        }

        if (authoredMapNodes != null)
        {
            foreach (ScratchHoldToBuy control in authoredMapNodes)
            {
                ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(control.StableSkillId);
                if (entry == null) continue;
                int branch = BranchOf(entry.id);
                int level = skills.GetLevel(entry.id);
                bool unavailable = ScratchSkillTree.DemoEdition && !entry.demoEligible;
                bool locked = !ScratchRadialProgression.Available(entry.id, game, skills) ||
                    !skills.RequirementsMet(entry.id);
                bool canBuy = purchasableIds.Contains(entry.id);
                bool completed = level >= entry.maxLevel;
                ScratchSkillMapNodeView.Render(control, entry, branch, BranchColors[branch],
                    level, unavailable, locked, canBuy, completed, upgradeSprites);
            }
        }
        DrawDetails();
    }
}
