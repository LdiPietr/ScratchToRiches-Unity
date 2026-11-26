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
                ? nextTicketHome : new Vector2(246f, -395f);
            if (resultPopupHeading != null)
                resultPopupHeading.text = "BIGLIETTO " + ScratchGameManager.TicketDisplayName(game.CurrentTicketKind);
            if (resultPopupAmount != null)
            {
                resultPopupAmount.text = game.TicketPayout > 0
                    ? "+" + game.TicketPayout.ToString("N0") + " <size=34>MONETE</size>"
                    : "<size=50>NESSUNA VINCITA</size>";
                resultPopupAmount.color = game.TicketPayout > 0
                    ? new Color32(234, 194, 92, 255) : new Color32(249, 239, 216, 255);
            }
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
            resultUpgradesLabel.text = "POTENZIAMENTI" +
                "\n<size=24>" + (count == 1 ? "1 ACQUISTABILE" : count + " ACQUISTABILI") + "</size>";
            resultUpgradesButton.gameObject.SetActive(available);
            resultUpgradesButton.interactable = available;
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
        resultReadyAt = Time.unscaledTime + .24f;
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

        ticketCanvasGroup.alpha = 0f;
        motion = null;
        if (game.WaitingForTicketChoice)
        {
            phase = Phase.Choosing;
            // Reuse the standard transactional purchase path. No new charge logic.
            if (action == ResultAction.Repeat && game.ChooseNextTicket(kind))
                yield break;
            if (action == ResultAction.Upgrades)
            {
                OpenShop();
                yield break;
            }
            ticketChooser.Refresh();
        }
        else
            phase = Phase.Idle;
    }
}
