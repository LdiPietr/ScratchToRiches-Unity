using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates scene-authored prize labels and visual feedback. No UI construction.</summary>
public sealed partial class ScratchVintageGameplayHUD
{
    private static readonly string[] PrizeTitles = { "Coppia", "Tris", "Poker", "Cinquina" };
    private int feedbackTicket = -1;
    private ScratchSymbolManager.MatchTier feedbackTier;
    private long feedbackValue;
    private bool finalWinFeedbackPlayed;
    private float feedbackStarted = -10f;
    private float feedbackDuration = .48f;
    private Color payoutRestColor = Gold;
    private int tableTicket = -1;
    private readonly long[] ticketQuotes = new long[4];
    private long ticketRefund;

    private long PreviewAward(ScratchSymbolManager.MatchResult match, bool complete,
        ScratchGameManager.TicketKind kind)
    {
        if (game.TicketVoided) return 0;
        long requested = ScratchRewardPolicy.Calculate(match, complete, game.TicketPrize(kind),
            economy.BasePrize, ScratchGameManager.TicketPrice(kind),
            economy.ConsecutiveWinningTickets, skills.GetLevel).RequestedCoins;
        return System.Math.Min(requested, (long)int.MaxValue - economy.Coins);
    }

    private void RefreshPossiblePrizes(ScratchGameManager.TicketKind kind, int max)
    {
        bool quote = game.TicketActive || tableTicket != game.TicketNumber;
        if (quote) tableTicket = game.TicketNumber;
        for (int i = 0; i < 4; i++)
        {
            var tier = (ScratchSymbolManager.MatchTier)(i + 1);
            var hypothetical = new ScratchSymbolManager.MatchResult(tier, string.Empty, i + 2);
            if (quote) ticketQuotes[i] = PreviewAward(hypothetical, true, kind);
            long award = !game.TicketActive && game.CurrentMatch.Tier == tier ? game.TicketPayout : ticketQuotes[i];
            if (game.TicketVoided) award = 0;
            bool locked = max < i + 2;
            bool obtained = !game.TicketVoided && game.CurrentMatch.Tier == tier;
            TMP_Text name = Get(prizeNames, i);
            TMP_Text amount = Get(prizeAmounts, i);
            Image coin = Get(prizeCoins, i);
            Image lockImage = Get(prizeLocks, i);
            if (name != null)
            {
                Set(name, PrizeTitles[i] + (locked ? " <size=17>· " + (i + 2) + " grattate</size>" : ""));
                name.color = locked ? Muted : obtained || i >= 2 ? Gold : Cream;
                name.fontStyle = !locked && (obtained || i == 3) ? FontStyles.Bold : FontStyles.Normal;
            }
            if (amount != null)
            {
                Set(amount, award.ToString("N0"));
                amount.color = locked ? Muted : Gold;
            }
            if (coin != null)
            {
                coin.color = new Color(1f, 1f, 1f, locked ? .45f : 1f);
                PositionCoin(coin, amount, 8f);
            }
            if (lockImage != null) lockImage.gameObject.SetActive(locked);
        }
        if (quote) ticketRefund = PreviewAward(default, true, kind);
        long refund = game.TicketVoided ? 0 : ticketRefund;
        Set(prizeFooter, game.TicketVoided ? "Biglietto di prova: nessuna vincita" : "Vale solo la combinazione più alta");
        bool hasRefund = refund > 0;
        if (refundLabel != null) refundLabel.gameObject.SetActive(hasRefund);
        if (refundAmount != null) refundAmount.gameObject.SetActive(hasRefund);
        if (refundCoin != null) refundCoin.gameObject.SetActive(hasRefund);
        Set(refundAmount, refund.ToString("N0"));
        PositionCoin(refundCoin, refundAmount, 8f);
    }

    private void RefreshMatch()
    {
        var match = game.CurrentMatch;
        long value = game.TicketActive
            ? PreviewAward(match, game.RevealedCount >= game.MaxScratches, game.CurrentTicketKind)
            : game.TicketPayout;
        bool voided = game.TicketVoided;
        if (voided) value = 0;
        bool refund = !match.IsMatch && value > 0;
        string name = voided ? "ANNULLATO" : refund ? "RIMBORSO" : match.IsMatch
            ? PrizeTitles[(int)match.Tier - 1].ToUpperInvariant()
            : game.TicketActive && game.RevealedCount < 2 ? "IN ATTESA" : "NESSUNA";
        string detail = voided ? "Biglietto di prova" : refund ? "Paracadute attivo" : match.IsMatch
            ? match.Multiplier + " × " + SentenceCase(match.Symbol)
            : !game.TicketActive && game.TicketNumber > 0 ? "Biglietto completato" : "Cerca simboli uguali";
        bool final = !game.TicketActive && game.TicketNumber > 0;
        if (final && !match.IsMatch && !voided && !refund) name = "NON VINCENTE";
        Set(matchHeader, final ? "RISULTATO BIGLIETTO" : "COMBINAZIONE ATTUALE");
        Set(matchName, name);
        Set(matchMultiplier, detail);
        RefreshPayoutStatus();
        Set(payout, value.ToString("N0"));
        payoutRestColor = value > 0 ? Gold : Muted;
        if (matchName != null) matchName.color = value > 0 ? Gold : Muted;
        if (matchAccent != null) matchAccent.color = value > 0 ? Gold : Reserved;
        if (payoutCoin != null) payoutCoin.color = new Color(1f, 1f, 1f, value > 0 ? 1f : .45f);
        if (feedbackTicket != game.TicketNumber)
        {
            feedbackTicket = game.TicketNumber;
            feedbackTier = default;
            feedbackValue = 0;
            finalWinFeedbackPlayed = false;
            feedbackStarted = -10f;
        }
        bool finalWin = final && value > 0 && !finalWinFeedbackPlayed;
        if (finalWin) finalWinFeedbackPlayed = true;
        if (finalWin || value > feedbackValue || (!voided && (int)match.Tier > (int)feedbackTier))
        {
            feedbackStarted = Time.unscaledTime;
            feedbackDuration = .34f + .10f * (int)match.Tier;
        }
        feedbackTier = voided ? default : match.Tier;
        feedbackValue = value;
        PositionCoin(payoutCoin, payout, 12f);
        UpdateWinFeedback();
    }

    private void UpdateWinFeedback()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - feedbackStarted) / feedbackDuration);
        float pulse = Mathf.Sin(t * Mathf.PI);
        if (payout != null)
            payout.color = Color.Lerp(payoutRestColor, Cream, pulse * .85f);
        if (matchName != null && feedbackValue > 0)
            matchName.color = Color.Lerp(Gold, Cream, pulse * .65f);
        if (matchAccent != null && feedbackValue > 0)
            matchAccent.color = Color.Lerp(Gold, Cream, pulse * .7f);
        if (prizeHighlights == null) return;
        for (int i = 0; i < prizeHighlights.Length; i++)
        {
            Image highlight = prizeHighlights[i];
            if (highlight == null) continue;
            bool obtained = (int)feedbackTier == i + 1;
            Color color = Gold;
            color.a = obtained ? .23f + pulse * .25f : 0f;
            highlight.color = color;
        }
    }

    private static T Get<T>(T[] entries, int index) where T : Object =>
        entries != null && index >= 0 && index < entries.Length ? entries[index] : null;

    private static void PositionCoin(Image coin, TMP_Text amount, float gap)
    {
        if (coin == null || amount == null) return;
        amount.ForceMeshUpdate(true);
        if (amount.textInfo.characterCount == 0) return;
        var coinRect = coin.rectTransform;
        var amountRect = amount.rectTransform;
        if (coinRect.parent != amountRect.parent) return;
        float left = amountRect.anchoredPosition.x + amount.textBounds.min.x * amountRect.localScale.x;
        var position = coinRect.anchoredPosition;
        position.x = left - coinRect.rect.width - gap;
        coinRect.anchoredPosition = position;
    }
}
