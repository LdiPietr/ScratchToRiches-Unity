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
        {
            ScratchSymbolManager.MatchResult match = CurrentMatch;
            string win = CombinationName(match.Tier);
            matchResultText.text = state == RoundState.Finished
                ? "DEMO COMPLETATA! SEI APPENA DIVENTATO INARRESTABILE."
                : state == RoundState.Ready ? "PRONTO A FARE IL COLPACCIO?"
                : ticketActive
                    ? match.IsMatch ? $"{win} x{match.Multiplier} - {match.Symbol}" : "CERCA I SIMBOLI UGUALI!"
                : match.IsMatch ? $"{win} x{match.Multiplier} +{ticketPayout} MONETE"
                : ticketPayout > 0 ? $"PREMIO DI CONSOLAZIONE! +{ticketPayout}"
                : "PECCATO! RITENTA!";
        }

        if (boardSubtitle != null)
            boardSubtitle.text = state == RoundState.Finished
                ? "FINE DEMO - L'ALBERO COMPLETO TI ASPETTA NEL GIOCO FINALE!"
                : state == RoundState.Ready
                ? "Gratta senza tempo limite, migliora le abilita' e sblocca biglietti!"
                : ticketActive
                    ? ticketVoided ? "Questo biglietto di prova non assegna premi."
                    : demoCapstoneActive && !demoFinished
                        ? $"FINALE: {Mathf.Min(demoGoldCompleted, FinalGoldMinTickets)}/{FinalGoldMinTickets} DORATI | INCASSI {demoGoldPayout}/{FinalGoldPayoutTarget} | POTERE {(DemoFinalPowerActivated ? "OK" : "DA USARE")}"
                        : $"{TicketDisplayName(currentTicketKind)} {CurrentGridSide}x{CurrentGridSide} - {MaxScratches} caselle!" +
                          (Level(ScratchSkillTree.SkillId.C6) > 0
                              ? targetedPreviewUsed ? " | ISPEZIONE USATA" : " | DESTRO/CTRL+CLIC: ISPEZIONA"
                              : string.Empty)
                    : demoCapstoneActive && !demoFinished
                        ? $"FINALE: {Mathf.Min(demoGoldCompleted, FinalGoldMinTickets)}/{FinalGoldMinTickets} DORATI | INCASSI {demoGoldPayout}/{FinalGoldPayoutTarget} | POTERE {(DemoFinalPowerActivated ? "OK" : "DA USARE")}"
                        : $"+{ticketPayout} monete! Scegli il prossimo biglietto o apri i potenziamenti.";

        Changed?.Invoke();
    }

    private void UpdateTimerText()
    {
        // The former timer panel has become a scene-authored shop button.
        if (timerValue != null) timerValue.text = "APRI SHOP";
    }
}
