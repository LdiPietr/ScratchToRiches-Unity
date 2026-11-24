using System;
using System.Collections.Generic;
using Match = ScratchSymbolManager.MatchResult;
using Tier = ScratchSymbolManager.MatchTier;
using Skill = ScratchSkillTree.SkillId;

// Executes the actual production C# policies, without stubs, Unity or NuGet packages.
// The independent reference functions preserve the pre-refactor formulas.
internal static class Program
{
    private static int assertions;
    private static readonly string[] Labels = { "0", "1", "2", "3", "4", "5", "6" };

    private static void Main()
    {
        for (int count = 0; count <= 5; count++)
        {
            int variants = (int)Math.Pow(8, count);
            for (int code = 0; code < variants; code++)
            {
                var symbols = new int[count];
                int digits = code;
                for (int i = 0; i < count; i++)
                {
                    symbols[i] = digits % 8;
                    digits /= 8;
                }
                Match actual = ScratchMatchEvaluator.Evaluate(symbols, 7, Labels);
                Match expected = ReferenceMatch(symbols);
                Assert(actual.Tier == expected.Tier && actual.Symbol == expected.Symbol &&
                    actual.MatchingCount == expected.MatchingCount &&
                    actual.Multiplier == expected.Multiplier, "Combination changed");
            }
        }
        var rng = new Random(9379);
        int[] bases = { 0, 10, 50, 110, 250 };
        int[] costs = { 0, 10, 25, 60 };
        for (int n = 0; n < 50000; n++)
        {
            int[] levels = new int[33];
            for (int i = (int)Skill.E1; i <= (int)Skill.E6; i++)
                levels[i] = rng.Next(0, 6);
            int Level(Skill id) => levels[(int)id];
            Tier tier = (Tier)rng.Next(0, 5);
            Match match = new Match(tier, "0", (int)tier + 1);
            int prize = bases[rng.Next(bases.Length)];
            int price = costs[rng.Next(costs.Length)];
            int streak = rng.Next(0, 5);
            bool complete = rng.Next(2) == 0;
            var expected = ReferenceReward(match, complete, prize, 10, price, streak, Level);
            var actual = ScratchRewardPolicy.Calculate(match, complete, prize, 10, price, streak, Level);
            Assert(actual.HasAward == expected.HasAward &&
                actual.RequestedCoins == expected.RequestedCoins &&
                actual.WinningStreak == expected.WinningStreak, "Reward changed");
        }
        Assert(ScratchPurchasePolicy.Price(19, 0, 5) == 19, "Price first level");
        Assert(ScratchPurchasePolicy.Price(19, 1, 5) == 29, "Price rounding");
        Assert(ScratchPurchasePolicy.Price(512, 1, 1) == 0, "Max level price");
        Assert(ScratchPurchasePolicy.Price(int.MaxValue, 1, 2) == int.MaxValue, "Price saturation");
        for (int bits = 0; bits < 128; bits++)
        {
            bool Flag(int n) => (bits & (1 << n)) != 0;
            bool edition = Flag(0), implemented = Flag(1), maxed = Flag(2);
            bool ready = Flag(3), gate = Flag(4), prereq = Flag(5), coins = Flag(6);
            bool expected = edition && implemented && !maxed && ready && gate && prereq && coins;
            var state = ScratchPurchasePolicy.Evaluate(edition, implemented, maxed ? 1 : 0,
                1, ready, () => gate, () => prereq, () => coins);
            Assert((state == ScratchPurchaseStatus.Available) == expected, "Purchase decision changed");
        }
        Console.WriteLine("DOMAIN REGRESSION PASS: " + assertions + " deterministic checks");
    }

    private static Match ReferenceMatch(int[] symbols)
    {
        var counts = new Dictionary<int, int>();
        int bestSymbol = -1, bestCount = 0, jokers = 0;
        foreach (int symbol in symbols)
        {
            if (symbol == 7) { jokers++; continue; }
            counts.TryGetValue(symbol, out int count);
            counts[symbol] = ++count;
            if (count > bestCount) { bestCount = count; bestSymbol = symbol; }
        }
        if (bestSymbol >= 0) bestCount += jokers;
        Tier tier = bestCount >= 5 ? Tier.Five : bestCount == 4 ? Tier.Poker :
            bestCount == 3 ? Tier.Triple : bestCount == 2 ? Tier.Pair : Tier.None;
        return new Match(tier, tier == Tier.None || bestSymbol < 0 ? string.Empty :
            Labels[bestSymbol], bestCount);
    }

    private static ScratchRewardPolicy.Settlement ReferenceReward(Match match,
        bool allPicksCompleted, int ticketBasePrize, int basePrize, int ticketPrice,
        int consecutiveWinningTickets, Func<Skill, int> Level)
    {
        if (match.IsMatch)
        {
            int rewardBase = Math.Max(1, ticketBasePrize > 0 ? ticketBasePrize : basePrize)
                + 2 * Level(Skill.E1);
            double multiplier = match.Tier == Tier.Pair ? 1.0 + 0.1 * Level(Skill.E2)
                : match.Tier == Tier.Triple ? 1.0 + 0.15 * Level(Skill.E3)
                : (match.Tier == Tier.Poker || match.Tier == Tier.Five)
                    ? 1.0 + 0.2 * Level(Skill.E4) : 1.0;
            long requested = (long)Math.Ceiling(rewardBase * (double)match.Multiplier * multiplier);
            if (Level(Skill.E6) > 0 && consecutiveWinningTickets > 0)
                requested = (long)Math.Ceiling(requested *
                    (1.0 + 0.05 * Math.Min(4, consecutiveWinningTickets)));
            return new ScratchRewardPolicy.Settlement(true, requested,
                Math.Min(4, consecutiveWinningTickets + 1));
        }
        int parachuteLevel = Level(Skill.E5);
        return allPicksCompleted && parachuteLevel > 0
            ? new ScratchRewardPolicy.Settlement(true, 3L * parachuteLevel +
                (long)Math.Ceiling(Math.Max(0, ticketPrice) * 0.2), 0)
            : new ScratchRewardPolicy.Settlement(false, 0L, 0);
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message + " at check " + assertions);
    }
}
