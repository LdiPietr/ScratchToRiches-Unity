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
        ResetRewardPresentation();
        foreach (Button button in new[] { nextTicketButton, repeatTicketButton, resultUpgradesButton })
        {
            if (button == null) continue;
            button.interactable = false;
            button.gameObject.SetActive(false);
        }
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
        resultReadyAt = Time.unscaledTime + .18f;
        resultDepartureStarted = -1f;
        if (!HasResultPopup) RefreshResultActions();
        ticketChooser.Refresh();
    }

    private void TickResultPopup()
    {
        if (!HasResultPopup || !ShowingResult || previewingResult) return;
        if (!ResultPopupVisible)
        {
            if (Time.unscaledTime < resultReadyAt ||
                (resultJuice != null && resultJuice.PayoutAnimating)) return;
            if (resultDepartureStarted < 0f)
            {
                resultDepartureStarted = Time.unscaledTime;
                // Activate the authored modal shield/dimmer, not its card yet.
                if (resultPopupPanel != null) resultPopupPanel.gameObject.SetActive(false);
                resultPopup.SetActive(true);
            }
            float travel = Mathf.Clamp01((Time.unscaledTime - resultDepartureStarted) / .36f);
            float eased = travel * travel;
            // Toss the complete layered ticket clear of the viewport. Do not
            // fade a layered CanvasGroup: that would expose covered symbols.
            ticketTransform.anchoredPosition = ticketHome + new Vector2(1450f, -850f) * eased;
            ticketTransform.localRotation = Quaternion.Euler(0f, 0f, -16f * eased);
            ticketTransform.localScale = Vector3.one * Mathf.Lerp(TicketDisplayScale, .88f, eased);
            SetResultDimmer(.55f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(travel / .68f)));
            if (travel < 1f) return;
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
            resultOpenedAt = Time.unscaledTime;
            if (resultPopupPanel != null) resultPopupPanel.gameObject.SetActive(true);
            ApplyRewardEntrance(0f);
            RefreshResultActions();
        }
        float duration = rewardVisualRank == 0 ? .26f : .32f + .035f * (rewardVisualRank - 1);
        ApplyRewardEntrance(Mathf.Clamp01((Time.unscaledTime - resultOpenedAt) / duration));
    }

    private void SetResultDimmer(float alpha)
    {
        if (resultDimmer == null) return;
        Color color = resultDimmer.color;
        color.a = alpha;
        resultDimmer.color = color;
    }

    private static void SetRewardGroup(CanvasGroup group, float alpha, bool input = false)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = input;
        group.blocksRaycasts = input;
    }

    private void ApplyRewardEntrance(float t)
    {
        float body = EaseOut(Mathf.Clamp01(t / .74f));
        float rise = t < .72f ? Mathf.Lerp(.92f, 1.025f, EaseOut(t / .72f)) :
            Mathf.Lerp(1.025f, 1f, Mathf.SmoothStep(0f, 1f, (t - .72f) / .28f));
        if (resultPopupPanel != null) resultPopupPanel.localScale = Vector3.one * rise;
        SetRewardGroup(resultCardGroup, body, true);
        SetRewardGroup(rewardHeaderGroup, EaseOut(Mathf.Clamp01((t - .06f) / .55f)));
        float value = EaseOut(Mathf.Clamp01((t - .18f) / .58f));
        SetRewardGroup(rewardValueGroup, value);
        UpdateRewardDecoration(value);
        if (rewardValueGroup != null)
        {
            float pop = rewardVisualRank > 0 ? (.02f + .018f * rewardVisualRank) * Mathf.Sin(Mathf.PI * value) : 0f;
            rewardValueGroup.transform.localScale = Vector3.one * (.96f + .04f * value + pop);
        }
        float actions = EaseOut(Mathf.Clamp01((t - .38f) / .62f));
        SetRewardGroup(rewardActionsGroup, actions, t >= 1f);
        if (rewardActionsGroup != null)
            ((RectTransform)rewardActionsGroup.transform).anchoredPosition = Vector2.down * (12f * (1f - actions));
    }

    private void ResetRewardPresentation()
    {
        SetResultDimmer(0f);
        ResetRewardDecoration();
        if (resultPopupPanel != null)
        {
            resultPopupPanel.gameObject.SetActive(false);
            resultPopupPanel.localScale = Vector3.one;
        }
        SetRewardGroup(resultCardGroup, 0f);
        SetRewardGroup(rewardHeaderGroup, 0f);
        SetRewardGroup(rewardValueGroup, 0f);
        SetRewardGroup(rewardActionsGroup, 0f);
        if (rewardValueGroup != null) rewardValueGroup.transform.localScale = Vector3.one;
        if (rewardActionsGroup != null)
            ((RectTransform)rewardActionsGroup.transform).anchoredPosition = Vector2.zero;
        resultDepartureStarted = -1f;
    }

    private void ResetDepartedTicket()
    {
        ticketCanvasGroup.alpha = 0f;
        ticketCanvasGroup.blocksRaycasts = false;
        ticketTransform.anchoredPosition = ticketHome;
        ticketTransform.localRotation = Quaternion.identity;
        ticketTransform.localScale = Vector3.one * TicketDisplayScale;
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
            ResetDepartedTicket();
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
        ResetDepartedTicket();
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
