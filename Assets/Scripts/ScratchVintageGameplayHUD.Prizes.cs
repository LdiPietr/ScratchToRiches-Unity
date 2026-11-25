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
    private float feedbackStarted = -10f;
    private Color payoutRestColor = Gold;

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
        for (int i = 0; i < 4; i++)
        {
            var tier = (ScratchSymbolManager.MatchTier)(i + 1);
            var hypothetical = new ScratchSymbolManager.MatchResult(tier, string.Empty, i + 2);
            long award = PreviewAward(hypothetical, true, kind);
            bool locked = max < i + 2;
            bool obtained = !game.TicketVoided && game.CurrentMatch.Tier == tier;
            TMP_Text name = Get(prizeNames, i);
            TMP_Text amount = Get(prizeAmounts, i);
            Image coin = Get(prizeCoins, i);
            Image lockImage = Get(prizeLocks, i);
            if (name != null)
            {
                Set(name, PrizeTitles[i] + (locked ? " <size=17>· " + (i + 2) + " grattate</size>" : ""));
                name.color = locked ? Muted : obtained ? Gold : Cream;
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
        long refund = PreviewAward(default, true, kind);
        Set(prizeFooter, game.TicketVoided ? "Biglietto di prova: nessuna vincita" :
            refund > 0 ? "Solo la più alta · Rimborso " + refund.ToString("N0") :
            "Vale solo la combinazione più alta");
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
            : game.WaitingForTicketChoice ? "Biglietto completato" : "Cerca simboli uguali";
        Set(matchName, name);
        Set(matchMultiplier, detail);
        Set(prizeLabel, game.TicketActive ? "Vincita attuale" : "Ultima vincita");
        Set(payout, value.ToString("N0"));
        payoutRestColor = value > 0 ? Gold : Cream;
        if (matchName != null) matchName.color = match.IsMatch && !voided || refund ? Gold : Cream;
        if (feedbackTicket != game.TicketNumber)
        {
            feedbackTicket = game.TicketNumber;
            feedbackTier = default;
            feedbackValue = 0;
            feedbackStarted = -10f;
        }
        if (value > feedbackValue || (!voided && (int)match.Tier > (int)feedbackTier))
            feedbackStarted = Time.unscaledTime;
        feedbackTier = voided ? default : match.Tier;
        feedbackValue = value;
        PositionCoin(payoutCoin, payout, 12f);
        UpdateWinFeedback();
    }

    private void UpdateWinFeedback()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - feedbackStarted) / .48f);
        float pulse = Mathf.Sin(t * Mathf.PI);
        if (payout != null)
            payout.color = Color.Lerp(payoutRestColor, Cream, pulse * .85f);
        if (prizeHighlights == null) return;
        for (int i = 0; i < prizeHighlights.Length; i++)
        {
            Image highlight = prizeHighlights[i];
            if (highlight == null) continue;
            bool obtained = (int)feedbackTier == i + 1;
            Color color = Gold;
            color.a = obtained ? .13f + pulse * .22f : 0f;
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
        float left = amountRect.anchoredPosition.x + amount.textBounds.min.x;
        var position = coinRect.anchoredPosition;
        position.x = left - coinRect.rect.width - gap;
        coinRect.anchoredPosition = position;
    }
}
