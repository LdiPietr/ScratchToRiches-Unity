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
    private ScratchCellManager matchCells;
    private static readonly Color LockedInk = new Color32(166, 184, 172, 255);
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
                Set(name, PrizeTitles[i] + (locked ? " <size=21>· " + (i + 2) + " grattate</size>" : ""));
                name.color = locked ? LockedInk : obtained ? Gold : Cream;
                name.fontStyle = obtained && !locked ? FontStyles.Bold : FontStyles.Normal;
            }
            if (amount != null)
            {
                Set(amount, award.ToString("N0"));
                amount.color = locked ? LockedInk : Gold;
                amount.fontStyle = obtained && !locked ? FontStyles.Bold : FontStyles.Normal;
            }
            if (coin != null)
            {
                coin.color = new Color(1f, 1f, 1f, locked ? .58f : 1f);
                PositionCoin(coin, amount, 8f);
            }
            if (lockImage != null)
            {
                lockImage.color = LockedInk;
                lockImage.gameObject.SetActive(locked);
            }
        }
        if (quote) ticketRefund = PreviewAward(default, true, kind);
        long refund = game.TicketVoided ? 0 : ticketRefund;
        Set(prizeFooter, game.TicketVoided ? "Biglietto di prova: nessuna vincita" : "Vale solo la combinazione più alta");
        bool hasRefund = refund > 0;
        if (refundLabel != null) refundLabel.gameObject.SetActive(hasRefund);
        if (refundAmount != null) refundAmount.gameObject.SetActive(hasRefund);
        if (refundCoin != null) refundCoin.gameObject.SetActive(hasRefund);
        Set(refundLabel, "Rimborso senza coppia");
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
        bool final = !game.TicketActive && game.TicketNumber > 0;
        bool validMatch = match.IsMatch && !voided;
        int realSymbols = RefreshMatchSymbols(match, validMatch, voided, out bool hasJolly);
        string name = voided ? "ANNULLATO" : refund ? "RIMBORSO" : validMatch
            ? PrizeTitles[(int)match.Tier - 1].ToUpperInvariant()
            : final ? "NON VINCENTE" : game.RevealedCount < 2 ? "IN ATTESA" : "NESSUNA COPPIA";
        string detail = voided ? "Biglietto di prova" : refund ? "Paracadute attivo" : validMatch
            ? realSymbols + " × " + SentenceCase(match.Symbol) + (hasJolly ? " + Jolly" : "")
            : final ? "Nessuna combinazione" : game.RevealedCount < 2
                ? "Gratta almeno due caselle" : "Prosegui la grattatura";
        Set(matchHeader, final ? "RISULTATO BIGLIETTO" : "COMBINAZIONE ATTUALE");
        Set(matchName, name);
        Set(matchMultiplier, detail);
        RefreshPayoutStatus();
        bool paid = value > 0;
        Set(payout, paid ? value.ToString("N0") : final ? "0" : "—");
        payoutRestColor = paid ? Gold : Muted;
        if (payout != null)
        {
            payout.fontSizeMax = paid ? 82 : 44;
            payout.fontSizeMin = paid ? 26 : 32;
        }
        if (prizeLabel != null) prizeLabel.gameObject.SetActive(paid || final);
        if (matchName != null) matchName.color = validMatch || paid ? Gold : Muted;
        if (matchAccent != null) matchAccent.color = validMatch || paid ? Gold : Reserved;
        if (payoutCoin != null)
        {
            payoutCoin.gameObject.SetActive(paid);
            payoutCoin.color = Color.white;
        }
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
        if (paid) PositionCoin(payoutCoin, payout, 10f);
        UpdateWinFeedback();
    }

    // Fixed objects in the scene; only visible, selected and completed cells can populate them.
    private int RefreshMatchSymbols(ScratchSymbolManager.MatchResult match, bool winning,
        bool voided, out bool hasJolly)
    {
        hasJolly = false;
        int count = 0;
        if (matchSymbols == null) return 0;
        for (int i = 0; i < matchSymbols.Length; i++)
            if (matchSymbols[i] != null) matchSymbols[i].gameObject.SetActive(false);
        if (matchJolly != null) matchJolly.gameObject.SetActive(false);
        if (voided || matchCells == null || matchCells.ActiveCells == null) return 0;
        foreach (ScratchCell cell in matchCells.ActiveCells)
        {
            if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                !cell.TryGetVisibleSymbol(out string symbol)) continue;
            if (winning && symbol != match.Symbol && symbol != "JOLLY") continue;
            if (symbol == "JOLLY") { hasJolly = true; continue; }
            Sprite artwork = cell.RevealedArtwork;
            if (artwork == null || count >= matchSymbols.Length) continue;
            Image slot = matchSymbols[count++];
            if (slot == null) continue;
            slot.sprite = artwork;
            slot.color = Color.white;
            slot.gameObject.SetActive(true);
        }
        int total = Mathf.Min(matchSymbols.Length, count + (hasJolly ? 1 : 0));
        float start = 221f - (total * 58f + Mathf.Max(0, total - 1) * 8f) * .5f;
        for (int i = 0; i < count; i++)
            if (matchSymbols[i] != null)
                matchSymbols[i].rectTransform.anchoredPosition = new Vector2(start + i * 66f, -186f);
        if (hasJolly && count < total && matchJolly != null)
        {
            matchJolly.rectTransform.anchoredPosition = new Vector2(start + count * 66f, -186f);
            matchJolly.color = Gold;
            matchJolly.gameObject.SetActive(true);
        }
        return count;
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
            color.a = obtained ? .28f + pulse * .22f : 0f;
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
