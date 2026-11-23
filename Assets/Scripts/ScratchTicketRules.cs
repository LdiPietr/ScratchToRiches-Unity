using System;

/// <summary>
/// One source of truth for ticket odds, base payouts and the GLOBAL C11 pick perk.
/// Ticket tiers change board size and prize, never how many cells may be selected.
/// No prizes are guaranteed: the symbol pool is sampled anew for every ticket.
/// </summary>
public static class ScratchTicketRules
{
    public const int StartingChoices = 3;
    public const int MaxExtraChoiceLevels = 3;

    public static int PickLimit(int extraChoiceLevels, int gridSide)
    {
        if (gridSide <= 0) return 0;
        int extra = Math.Max(0, Math.Min(MaxExtraChoiceLevels, extraChoiceLevels));
        return Math.Min(gridSide * gridSide, StartingChoices + extra);
    }

    public static int TicketPrice(ScratchGameManager.TicketKind kind)
    {
        switch (kind)
        {
            case ScratchGameManager.TicketKind.Standard: return 0;
            case ScratchGameManager.TicketKind.Fortunato: return 10;
            case ScratchGameManager.TicketKind.Gold: return 25;
            case ScratchGameManager.TicketKind.Diamond: return 60;
            default: return int.MaxValue;
        }
    }

    public static int BasePrize(ScratchGameManager.TicketKind kind, int classicBasePrize)
    {
        switch (kind)
        {
            case ScratchGameManager.TicketKind.Standard: return Math.Max(1, classicBasePrize);
            case ScratchGameManager.TicketKind.Fortunato: return 30;
            case ScratchGameManager.TicketKind.Gold: return 55;
            case ScratchGameManager.TicketKind.Diamond: return 70;
            default: return 0;
        }
    }

    // The 7 artwork symbols are always possible across different tickets.
    // Premium tickets draw a smaller RANDOM subset per ticket, increasing
    // match probability without manufacturing combinations or guaranteed wins.
    public static int SymbolPoolSize(ScratchGameManager.TicketKind kind)
    {
        switch (kind)
        {
            case ScratchGameManager.TicketKind.Standard: return 7;
            case ScratchGameManager.TicketKind.Fortunato: return 6;
            case ScratchGameManager.TicketKind.Gold: return 5;
            case ScratchGameManager.TicketKind.Diamond: return 4;
            default: return 0;
        }
    }
}
