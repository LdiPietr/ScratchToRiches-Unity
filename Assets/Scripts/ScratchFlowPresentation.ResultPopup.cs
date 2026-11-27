using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. All popup objects and default geometry are authored in the scene.
public sealed partial class ScratchFlowPresentation
{
    private bool previewingResult;

    private void RefreshResultActions()
    {
        if (!CanAcknowledgeResult) return;
        var kind = game.CurrentTicketKind;
        bool available = !game.DemoFinished;
        if (HasResultPopup)
        {
            var match = game.CurrentMatch;
            var sprites = new List<Sprite>(5);
            if (!game.TicketVoided && game.TicketPayout > 0 && match.IsMatch)
            {
                var cells = game.GetComponent<ScratchCellManager>();
                if (cells != null && cells.ActiveCells != null)
                    foreach (ScratchCell cell in cells.ActiveCells)
                    {
                        if (sprites.Count >= match.MatchingCount || sprites.Count >= 5) break;
                        if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                            !cell.TryGetVisibleSymbol(out string symbol) ||
                            (symbol != match.Symbol && symbol != "JOLLY")) continue;
                        Sprite sprite = symbol == "JOLLY" ? resultJollySprite : cell.RevealedArtwork;
                        if (sprite != null) sprites.Add(sprite);
                    }
            }
            ApplyPopupOutcome(kind, match.Tier, game.TicketPayout, game.TicketVoided, sprites);
        }
        RefreshResultButtons(kind, available, game.CanChooseTicket(kind),
            resultSkills != null ? resultSkills.PurchasableCount : 0);
    }

    private void RefreshResultButtons(ScratchGameManager.TicketKind kind, bool available, bool affordable, int count)
    {
        nextTicketLabel.text = available ? "CAMBIA BIGLIETTO" : "VEDI FINALE";
        if (HasResultPopup)
            ((RectTransform)nextTicketButton.transform).anchoredPosition = available
                ? nextTicketHome : new Vector2(246f, -390f);
        nextTicketButton.gameObject.SetActive(true);
        nextTicketButton.interactable = true;
        if (repeatTicketButton != null && repeatTicketLabel != null)
        {
            int cost = ScratchGameManager.TicketPrice(kind);
            repeatTicketLabel.text = "RIACQUISTA " + ScratchGameManager.TicketDisplayName(kind) +
                "\n<size=24>" + (cost == 0 ? "GRATIS" : cost.ToString("N0") + " MONETE") +
                (affordable ? "" : " · SALDO INSUFFICIENTE") + "</size>";
            repeatTicketButton.gameObject.SetActive(available);
            repeatTicketButton.interactable = available && affordable;
        }
        if (resultUpgradesButton != null && resultUpgradesLabel != null)
        {
            resultUpgradesLabel.text = resultUpgradeCount != null ? "POTENZIAMENTI" :
                "POTENZIAMENTI\n<size=24>" + (count == 1 ? "1 ACQUISTABILE" : count + " ACQUISTABILI") + "</size>";
            if (resultUpgradeCount != null) resultUpgradeCount.text = count.ToString();
            if (resultUpgradeBadge != null) resultUpgradeBadge.SetActive(available && count > 0);
            resultUpgradesButton.gameObject.SetActive(available);
            resultUpgradesButton.interactable = available;
        }
    }

    public static string PopupOutcome(ScratchSymbolManager.MatchTier tier, int payout, bool voided)
    {
        if (voided) return "BIGLIETTO DI PROVA";
        if (payout <= 0) return "NESSUNA VINCITA";
        switch (tier)
        {
            case ScratchSymbolManager.MatchTier.Pair: return "COPPIA";
            case ScratchSymbolManager.MatchTier.Triple: return "TRIS";
            case ScratchSymbolManager.MatchTier.Poker: return "POKER";
            case ScratchSymbolManager.MatchTier.Five: return "CINQUINA";
            default: return "RIMBORSO";
        }
    }

    public static string PopupNet(int payout, int cost, bool voided)
    {
        if (voided || cost == 0) return "";
        long net = (long)payout - cost;
        string value = (net > 0 ? "+" : "") + net.ToString("N0");
        string color = net > 0 ? "#EAC25C" : net < 0 ? "#D5A29A" : "#B9C9BE";
        return "Bilancio netto: <color=" + color + ">" + value + "</color> monete";
    }

    private void ApplyPopupOutcome(ScratchGameManager.TicketKind kind, ScratchSymbolManager.MatchTier tier,
        int payout, bool voided, IList<Sprite> sprites)
    {
        int cost = ScratchGameManager.TicketPrice(kind);
        bool winning = !voided && payout > 0 && tier != ScratchSymbolManager.MatchTier.None;
        Color accent = winning && payout > cost ? new Color32(234,194,92,255) : new Color32(192,205,195,255);
        if (resultPopupHeading != null)
            resultPopupHeading.text = "BIGLIETTO " + ScratchGameManager.TicketDisplayName(kind);
        if (resultOutcomeLabel != null)
        {
            resultOutcomeLabel.text = PopupOutcome(tier, payout, voided);
            resultOutcomeLabel.color = accent;
            SetPopupY(resultOutcomeLabel.rectTransform, winning ? -83f : -137f);
        }
        if (resultPopupAmount != null)
        {
            resultPopupAmount.text = (payout > 0 ? "+" : "") + payout.ToString("N0");
            resultPopupAmount.color = accent;
            SetPopupY(resultPopupAmount.rectTransform, winning ? -209f : -192f);
        }
        if (resultPopupUnit != null)
        {
            resultPopupUnit.text = "MONETE";
            resultPopupUnit.color = accent;
            SetPopupY(resultPopupUnit.rectTransform, winning ? -303f : -286f);
        }
        if (resultPopupNet != null) resultPopupNet.text = PopupNet(payout, cost, voided);
        if (resultMatchIcons == null) return;
        int count = winning && sprites != null ? Mathf.Min(resultMatchIcons.Length, sprites.Count) : 0;
        for (int i = 0; i < resultMatchIcons.Length; i++)
        {
            Image icon = resultMatchIcons[i];
            if (icon == null) continue;
            bool show = i < count && sprites[i] != null;
            icon.gameObject.SetActive(show);
            if (!show) continue;
            icon.sprite = sprites[i];
            icon.rectTransform.anchoredPosition =
                new Vector2(420f - (count * 72f + (count - 1) * 8f) * .5f + i * 80f, -132f);
        }
    }

    private static void SetPopupY(RectTransform rect, float y)
    {
        Vector2 position = rect.anchoredPosition;
        if (position.y != y) rect.anchoredPosition = new Vector2(position.x, y);
    }

#if UNITY_EDITOR
    // The visual fixture reuses the real renderer, but does not mutate tickets,
    // skills, wallet or save data. Inputs cannot acknowledge a synthetic result.
    public bool PreviewPopup(ScratchGameManager.TicketKind kind, ScratchSymbolManager.MatchTier tier,
        int payout, bool voided, IList<Sprite> sprites, bool affordable, int upgrades)
    {
        if (!Application.isPlaying || !HasResultPopup || game.TicketActive ||
            (!previewingResult && (!CanShowTicketChoices || !game.WaitingForTicketChoice))) return false;
        previewingResult = true;
        phase = Phase.TicketResult;
        ticketCanvasGroup.alpha = 0f;
        ticketCanvasGroup.blocksRaycasts = false;
        resultPopup.SetActive(true);
        if (resultPopupPanel != null) resultPopupPanel.localScale = Vector3.one;
        ApplyPopupOutcome(kind, tier, payout, voided, sprites);
        RefreshResultButtons(kind, true, affordable, upgrades);
        ticketChooser.Refresh();
        return true;
    }

    public void EndPopupPreview()
    {
        if (!previewingResult) return;
        previewingResult = false;
        HideResultActions();
        phase = Phase.Choosing;
        ticketChooser.Refresh();
    }
#endif
}
