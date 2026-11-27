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
        nextTicketLabel.text = available ? "Biglietti" : "Vedi finale";
        if (resultTicketsIcon != null) resultTicketsIcon.gameObject.SetActive(available);
        if (HasResultPopup)
        {
            ((RectTransform)nextTicketButton.transform).anchoredPosition = available
                ? nextTicketHome : new Vector2(246f, -395f);
            UpdatePremiumReward();
        }
        nextTicketButton.gameObject.SetActive(true);
        nextTicketButton.interactable = true;
        if (repeatTicketButton != null && repeatTicketLabel != null)
        {
            var kind = game.CurrentTicketKind;
            int cost = ScratchGameManager.TicketPrice(kind);
            bool affordable = game.CanChooseTicket(kind);
            string ticketName = ScratchGameManager.TicketDisplayName(kind);
            if (!string.IsNullOrEmpty(ticketName))
                ticketName = char.ToUpperInvariant(ticketName[0]) + ticketName.Substring(1).ToLowerInvariant();
            string price = cost == 0 ? "Gratis" : cost.ToString("N0") + " monete";
            repeatTicketLabel.text = resultRepeatCost != null ? "Gioca ancora" :
                "Gioca ancora\n<size=28>" + ticketName + " · " + price + "</size>";
            if (resultRepeatCost != null)
                resultRepeatCost.text = ticketName + " · " + price +
                    (affordable ? "" : " · Saldo insufficiente");
            repeatTicketButton.gameObject.SetActive(available);
            repeatTicketButton.interactable = available && affordable;
        }
        if (resultUpgradesButton != null && resultUpgradesLabel != null)
        {
            int count = resultSkills != null ? resultSkills.PurchasableCount : 0;
            resultUpgradesLabel.text = resultUpgradeCount != null ? "Potenziamenti" :
                "POTENZIAMENTI\n<size=24>" + (count == 1 ? "1 ACQUISTABILE" : count + " ACQUISTABILI") + "</size>";
            if (resultUpgradeCount != null) resultUpgradeCount.text = count.ToString();
            if (resultUpgradeBadge != null) resultUpgradeBadge.SetActive(available && count > 0);
            resultUpgradesButton.gameObject.SetActive(available);
            resultUpgradesButton.interactable = available;
        }
    }

    private void UpdatePremiumReward()
    {
        bool paid = game.TicketPayout > 0 && !game.TicketVoided;
        string heading = game.TicketVoided ? "Biglietto di prova" : !paid ? "Nessuna vincita" :
            !game.CurrentMatch.IsMatch ? "Rimborso" :
            game.CurrentMatch.Tier == ScratchSymbolManager.MatchTier.Five ? "Vincita straordinaria!" :
            game.CurrentMatch.Tier == ScratchSymbolManager.MatchTier.Poker ? "Grande vincita!" :
            game.CurrentMatch.Tier == ScratchSymbolManager.MatchTier.Triple ? "Ottima vincita!" : "Hai vinto!";
        if (resultPopupHeading != null) resultPopupHeading.text = heading;
        if (resultPopupAmount == null) return;
        resultPopupAmount.text = paid ? "+" + game.TicketPayout.ToString("N0") : "0";
        resultPopupAmount.color = paid ? new Color32(234, 194, 92, 255) : new Color32(185, 199, 185, 255);
        if (resultRewardCoin == null) return;
        resultRewardCoin.gameObject.SetActive(paid);
        RectTransform amount = resultPopupAmount.rectTransform;
        amount.sizeDelta = new Vector2(620f, 108f);
        amount.anchoredPosition = new Vector2(paid ? 160f : 110f, -120f);
        if (!paid) return;
        // Center coin and rendered amount as one group, including long payouts.
        resultPopupAmount.ForceMeshUpdate(true, true);
        float width = Mathf.Min(620f, resultPopupAmount.textBounds.size.x);
        resultRewardCoin.rectTransform.anchoredPosition =
            new Vector2(470f - width * .5f - 98f, -133f);
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
