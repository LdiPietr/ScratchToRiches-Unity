// Value types used by the runtime, QA and the pure matching/payout policies.
public sealed partial class ScratchSymbolManager
{
    public enum MatchTier { None, Pair, Triple, Poker, Five }
    public enum TestPattern { Random, Pair, Triple, Poker, Five }

    public readonly struct MatchResult
    {
        public readonly MatchTier Tier;
        public readonly string Symbol;
        public readonly int MatchingCount;
        public readonly int Multiplier;

        public MatchResult(MatchTier tier, string symbol, int count)
        {
            Tier = tier;
            Symbol = symbol;
            MatchingCount = count;
            Multiplier = (int)tier == 0 ? 1 : (int)tier + 1;
        }

        public bool IsMatch => Tier != MatchTier.None;
    }
}
