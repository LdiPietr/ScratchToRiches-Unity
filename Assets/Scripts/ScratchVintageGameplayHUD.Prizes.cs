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
    private static readonly Color LockedInk = new Color32(181, 198, 185, 255);
    private long ticketRefund;
    private long shownMatchPrize, matchPrizeFrom, matchPrizeTo;
    private float matchPrizeStarted = -10f;

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
                Set(name, PrizeTitles[i]);
                name.color = locked ? LockedInk : obtained ? Gold : Cream;
                name.fontStyle = obtained && !locked ? FontStyles.Bold : FontStyles.Normal;
            }
            TMP_Text requirement = Get(prizeRequirements, i);
            if (requirement != null)
            {
                Set(requirement, (i + 2) + " grattate");
                requirement.color = LockedInk;
                requirement.gameObject.SetActive(locked);
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
        if (refundLabel != null) refundLabel.color = Cream;
        if (refundAmount != null) refundAmount.color = Cream;
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
        bool paid = value > 0;
        bool showAmount = game.TicketActive || final;
        int realSymbols = RefreshMatchSymbols(match, validMatch, voided, out bool hasJolly);
        string name = voided ? "ANNULLATO" : refund ? "RIMBORSO" : validMatch
            ? PrizeTitles[(int)match.Tier - 1].ToUpperInvariant()
            : final ? "NON VINCENTE" : game.RevealedCount < 2 ? "IN ATTESA" : "NESSUNA COPPIA";
        string detail = voided ? "Biglietto di prova" : refund ? "Paracadute attivo" : validMatch
            ? (realSymbols > 1 ? realSymbols + " × " : "") + SentenceCase(match.Symbol) + (hasJolly ? " + Jolly" : "")
            : final ? "Nessuna combinazione" : game.RevealedCount < 2
                ? "Gratta almeno due caselle" : "Prosegui la grattatura";
        Set(matchHeader, final ? "RISULTATO BIGLIETTO" : "COMBINAZIONE ATTUALE");
        Set(matchName, name);
        Set(matchMultiplier, detail);
        RefreshPayoutStatus();
        if (payout != null) payout.gameObject.SetActive(showAmount);
        if (matchDivider != null) matchDivider.SetActive(showAmount);
        payoutRestColor = paid ? Gold : Muted;
        if (payout != null)
        {
            payout.fontSizeMax = paid ? 82 : 44;
            payout.fontSizeMin = paid ? 26 : 32;
        }
        if (prizeLabel != null) prizeLabel.gameObject.SetActive(showAmount);
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
            shownMatchPrize = matchPrizeFrom = matchPrizeTo = 0;
            matchPrizeStarted = -10f;
        }
        bool finalWin = final && value > 0 && !finalWinFeedbackPlayed;
        if (finalWin) finalWinFeedbackPlayed = true;
        if (finalWin || value > feedbackValue || (!voided && (int)match.Tier > (int)feedbackTier))
        {
            feedbackStarted = Time.unscaledTime;
            feedbackDuration = .15f + .10f * (int)match.Tier;
        }
        feedbackTier = voided ? default : match.Tier;
        feedbackValue = value;
        if (final || !paid || value < matchPrizeTo)
        {
            shownMatchPrize = matchPrizeFrom = matchPrizeTo = value;
            matchPrizeStarted = -10f;
        }
        else if (value > matchPrizeTo)
        {
            matchPrizeFrom = shownMatchPrize;
            matchPrizeTo = value;
            matchPrizeStarted = Time.unscaledTime;
        }
        Set(payout, paid || final ? shownMatchPrize.ToString("N0") : "—");
        if (showAmount) CenterPayoutGroup();
        UpdateWinFeedback();
    }

    // Interpolate only the provisional display; final and wallet stay exact.
    private void TickMatchPrize()
    {
        if (payout == null || !payout.gameObject.activeSelf || shownMatchPrize == matchPrizeTo) return;
        float t = Mathf.Clamp01((Time.unscaledTime - matchPrizeStarted) / .25f);
        double eased = 1.0 - (1.0 - t) * (1.0 - t);
        long next = matchPrizeFrom + (long)((matchPrizeTo - matchPrizeFrom) * eased);
        if (t >= 1f) next = matchPrizeTo;
        if (next == shownMatchPrize) return;
        shownMatchPrize = next;
        Set(payout, next.ToString("N0"));
        CenterPayoutGroup();
    }

    // Center the visible glyphs and coin as one optical group, including during the pulse.
    private void CenterPayoutGroup()
    {
        if (payout == null || !payout.gameObject.activeSelf) return;
        payout.ForceMeshUpdate(true);
        if (payout.textInfo.characterCount == 0) return;
        RectTransform rect = payout.rectTransform;
        float scale = rect.localScale.x;
        Bounds bounds = payout.textBounds;
        bool coinVisible = payoutCoin != null && payoutCoin.gameObject.activeSelf;
        float extra = coinVisible ? payoutCoin.rectTransform.rect.width + 10f : 0f;
        float groupWidth = bounds.size.x * scale + extra;
        var pos = rect.anchoredPosition;
        pos.x = 221f - groupWidth * .5f + extra - bounds.min.x * scale;
        rect.anchoredPosition = pos;
        if (coinVisible) PositionCoin(payoutCoin, payout, 10f);
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
        TickMatchSlotFeedback();
        if (prizeHighlights == null) return;
        for (int i = 0; i < prizeHighlights.Length; i++)
        {
            Image highlight = prizeHighlights[i];
            if (highlight == null) continue;
            bool obtained = (int)feedbackTier == i + 1;
            Color color = Gold;
            color.a = obtained ? .64f + pulse * .26f : 0f;
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
        position.y = amountRect.anchoredPosition.y + amount.textBounds.center.y * amountRect.localScale.y
            + coinRect.rect.height * .5f;
        coinRect.anchoredPosition = position;
    }
}
