using System;

/// <summary>Deterministic payout policy. No scene, persistence, clock or randomness.</summary>
public static class ScratchRewardPolicy
{
    public readonly struct Settlement
    {
        public readonly bool HasAward;
        public readonly long RequestedCoins;
        public readonly int WinningStreak;

        public Settlement(bool hasAward, long requestedCoins, int winningStreak)
        {
            HasAward = hasAward;
            RequestedCoins = requestedCoins;
            WinningStreak = winningStreak;
        }
    }

    public static Settlement Calculate(ScratchSymbolManager.MatchResult match,
        bool allPicksCompleted, int ticketBasePrize, int fallbackBasePrize, int ticketPrice,
        int consecutiveWinningTickets, Func<ScratchSkillTree.SkillId, int> level)
    {
        if (!match.IsMatch)
        {
            int parachuteLevel = level(ScratchSkillTree.SkillId.E5);
            if (allPicksCompleted && parachuteLevel > 0)
                return new Settlement(true, 3L * parachuteLevel +
                    (long)Math.Ceiling(Math.Max(0, ticketPrice) * 0.2), 0);
            return new Settlement(false, 0L, 0);
        }

        int rewardBase = Math.Max(1, ticketBasePrize > 0 ? ticketBasePrize : fallbackBasePrize)
            + 2 * level(ScratchSkillTree.SkillId.E1);
        double multiplier = MatchBonus(match.Tier, level);
        long requested = (long)Math.Ceiling(rewardBase * (double)match.Multiplier * multiplier);
        if (level(ScratchSkillTree.SkillId.E6) > 0 && consecutiveWinningTickets > 0)
            requested = (long)Math.Ceiling(requested *
                (1.0 + 0.05 * Math.Min(4, consecutiveWinningTickets)));
        return new Settlement(true, requested, Math.Min(4, consecutiveWinningTickets + 1));
    }

    private static double MatchBonus(ScratchSymbolManager.MatchTier tier,
        Func<ScratchSkillTree.SkillId, int> level)
    {
        switch (tier)
        {
            case ScratchSymbolManager.MatchTier.Pair:
                return 1.0 + 0.1 * level(ScratchSkillTree.SkillId.E2);
            case ScratchSymbolManager.MatchTier.Triple:
                return 1.0 + 0.15 * level(ScratchSkillTree.SkillId.E3);
            case ScratchSymbolManager.MatchTier.Poker:
            case ScratchSymbolManager.MatchTier.Five:
                return 1.0 + 0.2 * level(ScratchSkillTree.SkillId.E4);
            default:
                return 1.0;
        }
    }
}
