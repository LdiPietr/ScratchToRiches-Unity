using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Details responsibility of ScratchSkillTreeUI.
public sealed partial class ScratchSkillTreeUI
{
    private string SkillStatus(ScratchSkillCatalogV2.Entry entry)
    {
        int level = skills.GetLevel(entry.id);
        if (ScratchSkillTree.DemoEdition && !entry.demoEligible)
            return "VERSIONE COMPLETA · VISIBILE, NON ACQUISTABILE";
        if (!ScratchSkillCatalogV2.EffectImplemented(entry.id))
            return "EFFETTO NON ANCORA IMPLEMENTATO";
        if (level >= entry.maxLevel) return "POTENZIAMENTO COMPLETATO";
        if (!ScratchRadialProgression.Available(entry.id, game, skills))
            return "BLOCCATO · " +
                ScratchRadialProgression.SkillGoal(entry.id, game, skills);
        if (!skills.RequirementsMet(entry.id))
            return "BLOCCATO · " + skills.RequirementText(entry.id);
        if (economy.Coins < skills.GetPrice(entry.id))
            return "SERVONO " + skills.GetPrice(entry.id).ToString("N0") +
                " MONETE · SALDO " + economy.Coins.ToString("N0");
        return "ACQUISTABILE · COSTO " + skills.GetPrice(entry.id).ToString("N0") +
            " MONETE · TIENI PREMUTO 0,7 S";
    }

    private void DrawDetails()
    {
        if (skills == null || economy == null || game == null ||
            selectedTitle == null || selectedDescription == null || statusLabel == null)
            return;
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(hoveredId);
        if (entry == null)
        {
            int chapter = ScratchRadialProgression.UnlockedChapter(game, skills);
            selectedTitle.text = "NUCLEO · CAPITOLO " + chapter +
                " · " + ScratchRadialProgression.ChapterNames[chapter - 1];
            selectedDescription.text =
                "BORDO VERDE = PUOI COMPRARE. CLICCA LA BARRA IN ALTO " +
                "OPPURE PREMI TAB PER RAGGIUNGERE IL PROSSIMO NODO ACQUISTABILE. " +
                "TIENI PREMUTO L'ICONA PER 0,7 S.";
            if (purchasableIds.Count > 0)
            {
                string nextId = purchasableIds[0];
                ScratchSkillCatalogV2.Entry next = ScratchSkillCatalogV2.Find(nextId);
                statusLabel.text = "ACQUISTABILI: " + purchasableIds.Count +
                    " · PROSSIMO: " + nextId + " " + next.title.ToUpperInvariant() +
                    " · " + skills.GetPrice(nextId).ToString("N0") + " MONETE";
                statusLabel.color = new Color(0.39f, 1f, 0.46f);
            }
            else
            {
                statusLabel.text = "NESSUN ACQUISTO ORA · " +
                    ScratchRadialProgression.NextGoal(game, skills);
                statusLabel.color = new Color(1f, 0.79f, 0.38f);
            }
        }
        else
        {
            int level = skills.GetLevel(entry.id);
            selectedTitle.text = entry.id + " · " + entry.title.ToUpperInvariant() +
                " · LV " + level + "/" + entry.maxLevel +
                " · " + entry.branch.ToUpperInvariant();
            selectedDescription.text = entry.description;
            if (entry.id == "G04") selectedDescription.text += "  [F: VENTAGLIO]";
            if (entry.id == "G08") selectedDescription.text += "  [SHIFT + GRATTA: TITANICO]";
            if (entry.id == "C06") selectedDescription.text += "  [DESTRO/CTRL+CLIC: ISPEZIONA]";
            string selectedStatus = SkillStatus(entry);
            bool selectedBuy = skills.CanPurchase(entry.id);
            // Remind the player of their actual affordable next step EVEN
            // when inspecting a locked or already-completed node.
            if (!selectedBuy && purchasableIds.Count > 0)
            {
                string id = purchasableIds[0];
                statusLabel.text = "● ORA PUOI COMPRARE " + id +
                    " (" + skills.GetPrice(id).ToString("N0") +
                    ") · TAB PER TROVARLO  |  " + selectedStatus;
            }
            else
                statusLabel.text = selectedStatus;
            statusLabel.color = selectedBuy || purchasableIds.Count > 0
                ? new Color(0.39f, 1f, 0.46f)
                : skills.GetLevel(entry.id) >= entry.maxLevel
                    ? new Color(0.49f, 0.83f, 1f)
                    : new Color(1f, 0.79f, 0.38f);
        }
        statusLabel.fontStyle = FontStyles.Bold;
        if (purchaseLabel != null) purchaseLabel.text = statusLabel.text;
    }
}
