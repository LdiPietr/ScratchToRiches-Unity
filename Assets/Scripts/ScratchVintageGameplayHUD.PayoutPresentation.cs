using UnityEngine;

// Presentation-only balance animation. The real economy settles immediately.
public sealed partial class ScratchVintageGameplayHUD
{
    private int shownWallet, walletFrom, walletTo, walletTicket, payoutCoinCount, arrivedCoins;
    private float payoutAnimationStart, payoutFlight, payoutSpacing, arrivalPulseAt = -10f;
    private bool payoutInFlight;
    private Vector3 payoutScale, walletScale;
    private bool payoutWasPulsing;

    private void InitializePayoutPresentation()
    {
        shownWallet = economy.Coins;
        if (wallet != null)
        {
            walletScale = wallet.rectTransform.localScale;
            Set(wallet, shownWallet.ToString("N0"));
        }
        if (payout != null) payoutScale = payout.rectTransform.localScale;
    }

    // The same timing values drive the authored coin sprites in ScratchWinJuice.
    public void BeginPayoutPresentation(int amount, int coinCount, float started, float flight, float spacing)
    {
        if (!subscribed || amount <= 0 || coinCount <= 0) return;
        walletTo = economy.Coins;
        walletFrom = Mathf.Max(0, walletTo - amount);
        walletTicket = game.TicketNumber;
        payoutCoinCount = coinCount;
        payoutAnimationStart = started;
        payoutFlight = flight;
        payoutSpacing = spacing;
        arrivedCoins = 0;
        arrivalPulseAt = -10f;
        payoutInFlight = true;
        shownWallet = walletFrom;
        Set(wallet, shownWallet.ToString("N0"));
    }

    private void UpdatePayoutPresentation()
    {
        float now = Time.unscaledTime;
        if (payoutInFlight)
        {
            // A new ticket, spend, reset or leaving the result must never show a stale balance.
            if (!flow.ShowingResult || game.TicketNumber != walletTicket || economy.Coins != walletTo)
            {
                payoutInFlight = false;
                arrivalPulseAt = -10f;
            }
            else
            {
                float elapsed = now - payoutAnimationStart;
                int count = elapsed < payoutFlight ? 0 :
                    Mathf.Clamp(1 + Mathf.FloorToInt((elapsed - payoutFlight) /
                        Mathf.Max(.001f, payoutSpacing)), 0, payoutCoinCount);
                if (count > arrivedCoins)
                {
                    arrivedCoins = count;
                    arrivalPulseAt = now;
                }
                // Integer arithmetic gives monotonic, exact counts even at int.MaxValue.
                shownWallet = walletFrom + (int)((long)(walletTo - walletFrom) * arrivedCoins / payoutCoinCount);
                if (arrivedCoins == payoutCoinCount) payoutInFlight = false;
            }
        }
        if (!payoutInFlight) shownWallet = economy.Coins;
        if (wallet != null)
        {
            Set(wallet, shownWallet.ToString("N0"));
            float hit = Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - arrivalPulseAt) / .24f));
            wallet.color = Color.Lerp(Cream, Gold, hit * .85f);
            wallet.rectTransform.localScale = walletScale * (1f + hit * .055f);
        }
        if (payout != null)
        {
            float t = Mathf.Clamp01((now - feedbackStarted) / feedbackDuration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            // No count-up from zero: the actual prize remains readable throughout.
            payout.rectTransform.localScale = payoutScale * (1f + pulse * .065f);
            bool pulsing = pulse > .001f;
            if (pulsing || payoutWasPulsing) PositionCoin(payoutCoin, payout, 12f);
            payoutWasPulsing = pulsing;
        }
    }
}
