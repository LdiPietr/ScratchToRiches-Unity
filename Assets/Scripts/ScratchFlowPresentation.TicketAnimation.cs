using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TicketAnimation responsibility of ScratchFlowPresentation.
public sealed partial class ScratchFlowPresentation
{
    private const float TicketDisplayScale = .97f;
    private enum ResultAction { Choose, Repeat, Upgrades }

    private void HideResultActions()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        foreach (Button button in new[] { nextTicketButton, repeatTicketButton, resultUpgradesButton })
        {
            if (button == null) continue;
            button.interactable = false;
            button.gameObject.SetActive(false);
        }
    }

    private void RefreshResultActions()
    {
        if (!CanAcknowledgeResult) return;
        bool available = !game.DemoFinished;
        nextTicketLabel.text = available ? "CAMBIA BIGLIETTO" : "VEDI FINALE";
        if (HasResultPopup)
        {
            ((RectTransform)nextTicketButton.transform).anchoredPosition = available
                ? nextTicketHome : new Vector2(246f, -381f);
            if (resultPopupHeading != null)
                resultPopupHeading.text = "BIGLIETTO " + ScratchGameManager.TicketDisplayName(game.CurrentTicketKind);
            RefreshPopupOutcome();
        }
        nextTicketButton.gameObject.SetActive(true);
        nextTicketButton.interactable = true;
        if (repeatTicketButton != null && repeatTicketLabel != null)
        {
            var kind = game.CurrentTicketKind;
            int cost = ScratchGameManager.TicketPrice(kind);
            bool affordable = game.CanChooseTicket(kind);
            repeatTicketLabel.text = "RIACQUISTA " + ScratchGameManager.TicketDisplayName(kind) +
                "\n<size=28>" + (cost == 0 ? "GRATIS" : cost.ToString("N0") + " MONETE") +
                (affordable ? "" : " · SALDO INSUFFICIENTE") + "</size>";
            repeatTicketButton.gameObject.SetActive(available);
            repeatTicketButton.interactable = available && affordable;
        }
        if (resultUpgradesButton != null && resultUpgradesLabel != null)
        {
            int count = resultSkills != null ? resultSkills.PurchasableCount : 0;
            resultUpgradesLabel.text = resultUpgradeCount != null ? "POTENZIAMENTI" :
                "POTENZIAMENTI\n<size=24>" + (count == 1 ? "1 ACQUISTABILE" : count + " ACQUISTABILI") + "</size>";
            if (resultUpgradeCount != null) resultUpgradeCount.text = count.ToString();
            if (resultUpgradeBadge != null) resultUpgradeBadge.SetActive(available && count > 0);
            resultUpgradesButton.gameObject.SetActive(available);
            resultUpgradesButton.interactable = available;
        }
    }

    // Pure presentation classification; never modifies settlement or ticket rules.
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

    private void RefreshPopupOutcome()
    {
        var match = game.CurrentMatch;
        int payout = game.TicketPayout;
        bool winning = !game.TicketVoided && payout > 0 && match.IsMatch;
        bool profitable = winning && payout > ScratchGameManager.TicketPrice(game.CurrentTicketKind);
        Color accent = profitable ? new Color32(234, 194, 92, 255) : new Color32(192, 205, 195, 255);
        if (resultOutcomeLabel != null)
        {
            resultOutcomeLabel.text = PopupOutcome(match.Tier, payout, game.TicketVoided);
            resultOutcomeLabel.color = accent;
        }
        if (resultPopupAmount != null)
        {
            resultPopupAmount.text = (payout > 0 ? "+" : "") + payout.ToString("N0") + " <size=30>MONETE</size>";
            resultPopupAmount.color = accent;
        }
        if (resultMatchIcons == null) return;
        foreach (Image icon in resultMatchIcons)
            if (icon != null) icon.gameObject.SetActive(false);
        if (!winning) return;
        var cells = game.GetComponent<ScratchCellManager>();
        if (cells == null || cells.ActiveCells == null) return;
        int count = 0;
        foreach (ScratchCell cell in cells.ActiveCells)
        {
            if (count >= resultMatchIcons.Length || count >= match.MatchingCount) break;
            if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                !cell.TryGetVisibleSymbol(out string symbol) ||
                (symbol != match.Symbol && symbol != "JOLLY")) continue;
            Sprite artwork = symbol == "JOLLY" ? resultJollySprite : cell.RevealedArtwork;
            Image icon = resultMatchIcons[count];
            if (icon == null || artwork == null) continue;
            icon.sprite = artwork;
            icon.gameObject.SetActive(true);
            count++;
        }
        for (int i = 0; i < count; i++)
            resultMatchIcons[i].rectTransform.anchoredPosition =
                new Vector2(420f - (count * 72f + (count - 1) * 8f) * .5f + i * 80f, -132f);
    }

    public void RepeatAfterResult() => LeaveResult(ResultAction.Repeat);
    public void OpenUpgradesAfterResult() => LeaveResult(ResultAction.Upgrades);

    private void ShowTicketResult()
    {
        phase = Phase.TicketResult;
        ticketTransform.anchoredPosition = ticketHome;
        ticketTransform.localRotation = Quaternion.identity;
        ticketTransform.localScale = Vector3.one * TicketDisplayScale;
        ticketCanvasGroup.alpha = 1f;
        ticketCanvasGroup.blocksRaycasts = false;
        HideResultActions();
        resultReadyAt = Time.unscaledTime + .24f;
        resultDepartureStarted = -1f;
        if (!HasResultPopup) RefreshResultActions();
        ticketChooser.Refresh();
    }

    private void TickResultPopup()
    {
        if (!HasResultPopup || !ShowingResult) return;
        if (!ResultPopupVisible)
        {
            if (Time.unscaledTime < resultReadyAt ||
                (resultJuice != null && resultJuice.PayoutAnimating)) return;
            // The payout animation owns the ticket until all coins arrive.
            // Then move the fully opaque ticket away before showing the modal.
            if (resultDepartureStarted < 0f) resultDepartureStarted = Time.unscaledTime;
            float travel = Mathf.Clamp01((Time.unscaledTime - resultDepartureStarted) / .30f);
            float eased = EaseIn(travel);
            ticketTransform.anchoredPosition = ticketHome + Vector2.down * (920f * eased);
            ticketTransform.localRotation = Quaternion.Euler(0f, 0f, 3f * eased);
            ticketTransform.localScale = Vector3.one * Mathf.Lerp(TicketDisplayScale, .84f, eased);
            if (travel < 1f) return;
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
            resultOpenedAt = Time.unscaledTime;
            if (resultPopupPanel != null) resultPopupPanel.localScale = Vector3.one * .96f;
            resultPopup.SetActive(true);
            RefreshResultActions();
        }
        if (resultPopupPanel != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - resultOpenedAt) / .18f);
            resultPopupPanel.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, EaseOut(t));
        }
    }

    public void ContinueAfterResult() => LeaveResult(ResultAction.Choose);

    private void LeaveResult(ResultAction action)
    {
        if (!CanAcknowledgeResult) return;
        if (action != ResultAction.Choose && game.DemoFinished) return;
        if (action == ResultAction.Repeat && !game.CanChooseTicket(game.CurrentTicketKind)) return;
        HideResultActions();
        if (game.DemoFinished)
        {
            phase = Phase.Idle;
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
            game.ShowDemoEnding();
            return;
        }
        StopMotion();
        if (HasResultPopup)
        {
            // The ticket already left before the popup opened. Do not fly it out twice.
            phase = Phase.TicketLeaving;
            CompleteTicketDeparture(action, game.CurrentTicketKind);
        }
        else
            motion = StartCoroutine(FlyOutTicket(action, game.CurrentTicketKind));
    }

    private IEnumerator FlyInTicket()
    {
        phase = Phase.TicketEntering;
        HideResultActions();
        ticketChooser.Refresh();
        ticketCanvasGroup.blocksRaycasts = false;
        // Keep the ticket fully opaque from its first visible frame. Fading a
        // CanvasGroup containing BOTH the silver scratch coat and the hidden
        // symbol images makes each layer translucent independently, exposing
        // the symbols through the foil until the fly-in finishes.
        // Animate only movement, rotation and scale, never group opacity.
        ticketCanvasGroup.alpha = 1f;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        Vector2 start = ticketHome + new Vector2(0f, -35f);
        Quaternion startRotation = Quaternion.Euler(0f, 0f, -3f);
        float t = 0f;
        while (t < ticketTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseOut(Mathf.Clamp01(t / ticketTravelDuration));
            ticketTransform.anchoredPosition = Vector2.LerpUnclamped(start, ticketHome, a);
            ticketTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, Quaternion.identity, a);
            ticketTransform.localScale = Vector3.one * Mathf.Lerp(0.84f, TicketDisplayScale, a);
            yield return null;
        }

        ticketTransform.anchoredPosition = ticketHome;
        ticketTransform.localRotation = Quaternion.identity;
        ticketTransform.localScale = Vector3.one * TicketDisplayScale;
        ticketCanvasGroup.alpha = 1f;
        phase = Phase.TicketLive;
        ticketCanvasGroup.blocksRaycasts = true;
        // No early ticket skip: only scratching all selected cells settles.
        if (primaryActionButton != null)
            primaryActionButton.interactable = false;
        motion = null;
    }

    private IEnumerator FlyOutTicket(ResultAction action, ScratchGameManager.TicketKind kind)
    {
        phase = Phase.TicketLeaving;
        ticketCanvasGroup.blocksRaycasts = false;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        ticketChooser.Refresh();
        Vector2 start = ticketTransform.anchoredPosition;
        Quaternion startRotation = ticketTransform.localRotation;
        Vector3 startScale = ticketTransform.localScale;
        float t = 0f;

        while (t < ticketTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseIn(Mathf.Clamp01(t / ticketTravelDuration));
            ticketTransform.anchoredPosition = Vector2.LerpUnclamped(start,
                ticketHome + new Vector2(0f, -35f), a);
            ticketTransform.localRotation = Quaternion.SlerpUnclamped(startRotation,
                Quaternion.Euler(0f, 0f, 3f), a);
            ticketTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.78f, a);
            // Keep the layered printed ticket opaque until it leaves; fading
            // individual CanvasGroup children exposes covered symbols.
            ticketCanvasGroup.alpha = 1f;
            yield return null;
        }

        CompleteTicketDeparture(action, kind);
    }

    private void CompleteTicketDeparture(ResultAction action, ScratchGameManager.TicketKind kind)
    {
        ticketCanvasGroup.alpha = 0f;
        motion = null;
        if (game.WaitingForTicketChoice)
        {
            phase = Phase.Choosing;
            // Reuse the standard transactional purchase path. No new charge logic.
            if (action == ResultAction.Repeat && game.ChooseNextTicket(kind))
                return;
            if (action == ResultAction.Upgrades)
            {
                OpenShop();
                return;
            }
            ticketChooser.Refresh();
        }
        else
            phase = Phase.Idle;
    }
}
