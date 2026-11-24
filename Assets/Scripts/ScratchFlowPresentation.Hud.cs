using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hud responsibility of ScratchFlowPresentation.
public sealed partial class ScratchFlowPresentation
{
    private void UpdateInformation()
    {
        if (game == null || economy == null) return;
        string left = (game.State == ScratchGameManager.RoundState.Ready ? "PRONTO A PARTIRE" : "GIOCO CONTINUO") + "\n" +
            "SENZA LIMITE DI TEMPO\n\n" +
            $"BIGLIETTO N. {(game.TicketNumber == 0 ? "-" : game.TicketNumber.ToString())}\n" +
            $"BIGLIETTI FINITI  {game.TicketsCompleted}\n\n" +
            $"SCELTE  {game.SelectedCount}/{game.MaxScratches}\n" +
            $"GRATTATE  {game.RevealedCount}/{game.MaxScratches}";
        string right = $"IN GIOCO\n{ScratchGameManager.TicketDisplayName(game.CurrentTicketKind)} " +
            $"{game.CurrentGridSide}x{game.CurrentGridSide}\n\n" +
            $"PREMIO BASE  {game.TicketPrize(game.CurrentTicketKind)}\n" +
            $"ULTIMA VINCITA  +{game.TicketPayout}\n" +
            $"BIGLIETTI SBLOCCATI  {(game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato) ? 2 : 1) + (game.IsTicketUnlocked(ScratchGameManager.TicketKind.Gold) ? 1 : 0) + (game.IsTicketUnlocked(ScratchGameManager.TicketKind.Diamond) ? 1 : 0)} / 4\n\n" +
            $"IL TUO BOTTINO  {economy.Coins:N0}";

        if (leftInformation != null && left != previousLeft)
        {
            previousLeft = left;
            leftInformation.text = left;
        }
        if (rightInformation != null && right != previousRight)
        {
            previousRight = right;
            rightInformation.text = right;
        }
    }

    private static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseIn(float x) => x * x * x;
}
