using System.Collections.Generic;
using MatchTier = ScratchSymbolManager.MatchTier;
using MatchResult = ScratchSymbolManager.MatchResult;

/// <summary>Pure matching rules; the adapter determines which cells are revealed.</summary>
public static class ScratchMatchEvaluator
{
    public static MatchResult Evaluate(IEnumerable<int> revealedSymbols, int jollyId, string[] symbolLabels)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        int bestSymbol = -1;
        int bestCount = 0;
        int scratchedJollies = 0;

        foreach (int symbol in revealedSymbols)
        {
            if (symbol == jollyId)
            {
                scratchedJollies++;
                continue;
            }
            counts.TryGetValue(symbol, out int count);
            count++;
            counts[symbol] = count;

            if (count > bestCount)
            {
                bestCount = count;
                bestSymbol = symbol;
            }
        }

        // A scratched joker counts as the strongest other revealed symbol,
        // but the un-scratched joker never counts.
        if (bestSymbol >= 0) bestCount += scratchedJollies;
        MatchTier tier = bestCount >= 5 ? MatchTier.Five
            : bestCount == 4 ? MatchTier.Poker
            : bestCount == 3 ? MatchTier.Triple
            : bestCount == 2 ? MatchTier.Pair
            : MatchTier.None;

        return new MatchResult(tier,
            tier == MatchTier.None || bestSymbol < 0 ? string.Empty : symbolLabels[bestSymbol],
            bestCount);
    }
}
