using System;

/// <summary>One authoritative purchase decision, independent of Unity UI.</summary>
public enum ScratchPurchaseStatus
{
    Missing, FullGameOnly, NotImplemented, MaxLevel, NotReady,
    MilestoneLocked, PrerequisiteLocked, InsufficientCoins, Available
}

public static class ScratchPurchasePolicy
{
    public static int Price(int initialPrice, int level, int maxLevel)
    {
        if (level >= maxLevel) return 0;
        double price = initialPrice * Math.Pow(1.5, level);
        return price >= int.MaxValue ? int.MaxValue :
            Math.Max(1, (int)Math.Ceiling(price));
    }

    // Short-circuit the checks. No callback spends coins or changes levels.
    public static ScratchPurchaseStatus Evaluate(bool editionAllowed, bool implemented,
        int level, int maxLevel, bool contextReady, Func<bool> milestoneMet,
        Func<bool> prerequisitesMet, Func<bool> canAfford)
    {
        if (!editionAllowed) return ScratchPurchaseStatus.FullGameOnly;
        if (!implemented) return ScratchPurchaseStatus.NotImplemented;
        if (level >= maxLevel) return ScratchPurchaseStatus.MaxLevel;
        if (!contextReady) return ScratchPurchaseStatus.NotReady;
        if (!milestoneMet()) return ScratchPurchaseStatus.MilestoneLocked;
        if (!prerequisitesMet()) return ScratchPurchaseStatus.PrerequisiteLocked;
        return canAfford() ? ScratchPurchaseStatus.Available :
            ScratchPurchaseStatus.InsufficientCoins;
    }
}
