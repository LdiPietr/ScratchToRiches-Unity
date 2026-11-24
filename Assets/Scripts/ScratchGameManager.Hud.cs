using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hud responsibility of ScratchGameManager.
public sealed partial class ScratchGameManager
{
    private void RefreshHud()
    {
        UpdateTimerText();
        if (scratchProgress != null)
        {
            scratchProgress.text = $"SCELTE {selectedCells.Count}/{MaxScratches}   |   GRATTATE {RevealedCount}/{MaxScratches}";
            if (skillTree != null && skillTree.GetLevel("G08") > 0)
                scratchProgress.text += "   |   SHIFT+GRATTA: TITANICO " + skillTree.TitanCharges;
        }

        if (roundActionLabel != null)
            roundActionLabel.text = state == RoundState.Ready ? "SI PARTE!"
                : state == RoundState.Finished ? "FINE DEMO"
                : ticketActive ? "COMPLETA LE GRATTATE" : "SCEGLI IL BIGLIETTO";

        if (roundActionButton != null)
            roundActionButton.interactable = state == RoundState.Ready;

        if (matchResultText != null)
            matchResultText.text = FormatMatchResult();

        if (boardSubtitle != null)
            boardSubtitle.text = FormatBoardSubtitle();

        Changed?.Invoke();
    }

    private string FormatMatchResult()
    {
        ScratchSymbolManager.MatchResult match = CurrentMatch;
        string win = CombinationName(match.Tier);
        if (state == RoundState.Finished)
            return "DEMO COMPLETATA! SEI APPENA DIVENTATO INARRESTABILE.";
        if (state == RoundState.Ready)
            return "PRONTO A FARE IL COLPACCIO?";
        if (ticketActive)
            return match.IsMatch ? $"{win} x{match.Multiplier} - {match.Symbol}" :
                "CERCA I SIMBOLI UGUALI!";
        if (match.IsMatch)
            return $"{win} x{match.Multiplier} +{ticketPayout} MONETE";
        return ticketPayout > 0 ? $"PREMIO DI CONSOLAZIONE! +{ticketPayout}" :
            "PECCATO! RITENTA!";
    }

    private string FormatBoardSubtitle()
    {
        if (state == RoundState.Finished)
            return "FINE DEMO - L'ALBERO COMPLETO TI ASPETTA NEL GIOCO FINALE!";
        if (state == RoundState.Ready)
            return "Gratta senza tempo limite, migliora le abilita' e sblocca biglietti!";
        if (ticketActive && ticketVoided)
            return "Questo biglietto di prova non assegna premi.";
        if (demoCapstoneActive && !demoFinished)
            return FormatFinalChallenge();
        if (!ticketActive)
            return $"+{ticketPayout} monete! Scegli il prossimo biglietto o apri i potenziamenti.";

        string ticket = $"{TicketDisplayName(currentTicketKind)} {CurrentGridSide}x{CurrentGridSide} - {MaxScratches} caselle!";
        if (Level(ScratchSkillTree.SkillId.C6) > 0)
            ticket += targetedPreviewUsed ? " | ISPEZIONE USATA" :
                " | DESTRO/CTRL+CLIC: ISPEZIONA";
        return ticket;
    }

    private string FormatFinalChallenge()
    {
        return $"FINALE: {Mathf.Min(demoGoldCompleted, FinalGoldMinTickets)}/{FinalGoldMinTickets} DORATI | INCASSI {demoGoldPayout}/{FinalGoldPayoutTarget} | POTERE {(DemoFinalPowerActivated ? "OK" : "DA USARE")}";
    }

    private void UpdateTimerText()
    {
        // The former timer panel has become a scene-authored shop button.
        if (timerValue != null) timerValue.text = "APRI SHOP";
    }
}
