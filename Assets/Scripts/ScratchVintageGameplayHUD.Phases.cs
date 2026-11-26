using UnityEngine;

public sealed partial class ScratchVintageGameplayHUD
{
    // Phase, not stale game state, owns presentation visibility. Both wallets
    // are siblings of the curtain, so the curtain cannot hide them by itself.
    private void SyncPhaseVisibility()
    {
        bool board = flow.ShowingTicketHud;
        bool help = board && !flow.ShowingResult && game.TicketActive;
        Visible(gameplayHeader, board || flow.ShowingSelectionBackdrop);
        Visible(gameplayLeft, board);
        Visible(gameplayRight, board);
        Visible(gameplayFooter, board);
        Visible(shopWalletPanel, flow.ShopVisible);
        Visible(resultHelpGroup, flow.ShowingResult);
        if (helpPrimary != null) Visible(helpPrimary.gameObject, help);
        if (helpSecondary != null) Visible(helpSecondary.gameObject, help);
    }

    private static void Visible(GameObject target, bool show)
    {
        if (target != null && target.activeSelf != show) target.SetActive(show);
    }

#if UNITY_EDITOR
    public bool ValidatePhaseVisibility(out string detail)
    {
        bool board = flow.ShowingTicketHud;
        bool header = board || flow.ShowingSelectionBackdrop;
        var chooser = game.GetComponent<ScratchTicketChoiceUI>();
        bool ok = chooser != null && chooser.ChoicePanelVisible == (flow.ShowingSelectionBackdrop && game.WaitingForTicketChoice && !game.TicketActive) &&
            gameplayHeader != null && gameplayLeft != null && gameplayRight != null &&
            gameplayFooter != null && shopWalletPanel != null && resultHelpGroup != null &&
            gameplayHeader.activeInHierarchy == header &&
            gameplayLeft.activeInHierarchy == board && gameplayRight.activeInHierarchy == board &&
            gameplayFooter.activeInHierarchy == board &&
            shopWalletPanel.activeInHierarchy == flow.ShopVisible &&
            resultHelpGroup.activeInHierarchy == flow.ShowingResult &&
            !(gameplayHeader.activeInHierarchy && shopWalletPanel.activeInHierarchy) &&
            (ticketGroup == null || (board ? ticketGroup.alpha > .999f : ticketGroup.alpha < .001f)) &&
            (game.TicketActive || game.TicketNumber == 0 || displayedPickLimit == game.RevealedCount) &&
            (picks == null || picks.text == game.RevealedCount + "<size=64> /" + displayedPickLimit + "</size>");
        detail = "board=" + board + "|header=" + header + "|shop=" + flow.ShopOpen +
            "|shopVisible=" + flow.ShopVisible + "|result=" + flow.ShowingResult +
            "|picks=" + game.RevealedCount + "/" + displayedPickLimit;
        return ok;
    }
#endif

}
