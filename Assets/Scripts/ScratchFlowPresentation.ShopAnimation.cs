using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ShopAnimation responsibility of ScratchFlowPresentation.
public sealed partial class ScratchFlowPresentation
{
    public void OpenShop()
    {
        if (!CanOpenShop)
            return;
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("SHOP_OPEN", "phase=" + phase);
#endif
        phaseBeforeShop = phase;
        StopMotion();
        motion = StartCoroutine(DescendCurtain());
    }

    private IEnumerator DescendCurtain()
    {
        phase = Phase.CurtainDescending;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        ticketChooser.Refresh();
        if (!shopContent.activeSelf) shopContent.SetActive(true);
        curtainObject.SetActive(true);
        continueButton.interactable = false;
        curtainRect.anchoredPosition = curtainHome + new Vector2(0f, 1200f);
        float t = 0f;
        while (t < curtainTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseOut(Mathf.Clamp01(t / curtainTravelDuration));
            curtainRect.anchoredPosition = Vector2.LerpUnclamped(
                curtainHome + new Vector2(0f, 1200f), curtainHome, a);
            yield return null;
        }
        curtainRect.anchoredPosition = curtainHome;
        phase = Phase.CurtainVisible;
        continueButton.interactable = true;
        motion = null;
    }

    public void ContinueAfterUpgrades()
    {
        if (phase != Phase.CurtainVisible) return;
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("SHOP_CLOSE", "from=radial_map");
#endif
        StopMotion();
        motion = StartCoroutine(RaiseCurtainAndReturn());
    }

    private IEnumerator RaiseCurtainAndReturn()
    {
        phase = Phase.CurtainAscending;
        continueButton.interactable = false;
        float t = 0f;
        while (t < curtainTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseIn(Mathf.Clamp01(t / curtainTravelDuration));
            curtainRect.anchoredPosition = Vector2.LerpUnclamped(
                curtainHome, curtainHome + new Vector2(0f, 1200f), a);
            yield return null;
        }
        curtainObject.SetActive(false);
        curtainRect.anchoredPosition = curtainHome;
        phase = phaseBeforeShop;
        ticketCanvasGroup.blocksRaycasts = phase == Phase.TicketLive;
        if (primaryActionButton != null)
            primaryActionButton.interactable = game.State == ScratchGameManager.RoundState.Ready;
        ticketChooser.Refresh();
        motion = null;
    }
}
