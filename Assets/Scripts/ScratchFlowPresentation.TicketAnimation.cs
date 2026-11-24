using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TicketAnimation responsibility of ScratchFlowPresentation.
public sealed partial class ScratchFlowPresentation
{
    private IEnumerator FlyInTicket()
    {
        phase = Phase.TicketEntering;
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
            ticketTransform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, a);
            yield return null;
        }

        ticketTransform.anchoredPosition = ticketHome;
        ticketTransform.localRotation = Quaternion.identity;
        ticketTransform.localScale = Vector3.one;
        ticketCanvasGroup.alpha = 1f;
        phase = Phase.TicketLive;
        ticketCanvasGroup.blocksRaycasts = true;
        // No early ticket skip: only scratching all selected cells settles.
        if (primaryActionButton != null)
            primaryActionButton.interactable = false;
        motion = null;
    }

    private IEnumerator FlyOutTicket()
    {
        phase = Phase.TicketLeaving;
        ticketCanvasGroup.blocksRaycasts = false;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        ticketChooser.Refresh();
        Vector2 start = ticketTransform.anchoredPosition;
        Quaternion startRotation = ticketTransform.localRotation;
        Vector3 startScale = ticketTransform.localScale;
        float startAlpha = ticketCanvasGroup.alpha;
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
            ticketCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, a);
            yield return null;
        }

        ticketCanvasGroup.alpha = 0f;
        motion = null;
        if (game.WaitingForTicketChoice)
        {
            phase = Phase.Choosing;
            ticketChooser.Refresh();
        }
        else
            phase = Phase.Idle;
    }
}
