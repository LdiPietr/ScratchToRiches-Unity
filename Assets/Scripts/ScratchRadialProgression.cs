using UnityEngine;

/// <summary>
/// EXPERIMENTAL branch-only progression: the nucleus opens successive radial
/// rings from actual completed tickets, not play time or RNG outcomes.
/// All chapter gates complement, never replace, catalog skill prerequisites.
/// Does not create any UI or GameObjects.
/// </summary>
public static class ScratchRadialProgression
{
    public static readonly string[] ChapterNames =
    {
        "PRIMI GUADAGNI", "PRIMO SALTO", "SPECIALIZZAZIONE",
        "GRANDI BIGLIETTI", "POTENZA FINALE",
        "NUOVI ORIZZONTI", "AUTOMAZIONE", "ENDGAME"
    };

    // Explicit demo classification. This is NOT based on the textual time
    // estimates in JSON, which may be adjusted without changing gameplay.
    public static int ChapterOf(string id)
    {
        switch (id)
        {
            case "G01": case "C01": case "E01": return 1;
            case "G02": case "G03": case "C03": case "E02": case "E05": return 2;
            case "G04": case "G05": case "C02": case "E03": case "F01": return 3;
            case "G06": case "G07": case "C04": case "C05": case "E04": case "E06": return 4;
            case "G08": case "C06": case "F02": case "F03": case "F04": return 5;
        }
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(id);
        if (entry == null) return int.MaxValue;
        switch (entry.phase)
        {
            case "Completo · mid": return 6;
            case "Completo · late": return 7;
            case "Completo · endgame": return 8;
        }
        return int.MaxValue;
    }

    /// <summary>
    /// Pure and deterministic milestone calculation, exercised in the scene audit.
    /// Normal tickets count only when fully scratched and actually settled.
    /// </summary>
    public static int ChapterFromMilestones(int standard, int fortunate, int gold,
        int diamond, bool hasFortunato, bool hasGold, bool hasDiamond, bool demo)
    {
        int chapter = 1;
        if (standard >= 3) chapter = 2;
        if (chapter >= 2 && hasFortunato && fortunate >= 2) chapter = 3;
        if (chapter >= 3 && fortunate >= 9) chapter = 4;
        if (chapter >= 4 && hasGold && gold >= 3) chapter = 5;
        if (!demo && chapter >= 5 && hasDiamond && diamond >= 6) chapter = 6;
        if (!demo && chapter >= 6 && diamond >= 25) chapter = 7;
        if (!demo && chapter >= 7 && diamond >= 70) chapter = 8;
        return chapter;
    }

    public static int UnlockedChapter(ScratchGameManager game, ScratchSkillTree skills)
    {
        if (game == null || skills == null) return 1;
        int chapter = ChapterFromMilestones(
            game.CompletedByTier(ScratchGameManager.TicketKind.Standard),
            game.CompletedByTier(ScratchGameManager.TicketKind.Fortunato),
            game.CompletedByTier(ScratchGameManager.TicketKind.Gold),
            game.CompletedByTier(ScratchGameManager.TicketKind.Diamond),
            skills.GetLevel("C03") > 0, skills.GetLevel("C05") > 0,
            skills.GetLevel("C07") > 0, ScratchSkillTree.DemoEdition);

        // Existing saves do not lose access to purchased skill levels. A
        // chapter cannot regress merely because a legacy run lacked counters.
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            if (skills.GetLevel(entry.id) > 0)
                chapter = Mathf.Max(chapter, ChapterOf(entry.id));
        return Mathf.Clamp(chapter, 1, ScratchSkillTree.DemoEdition ? 5 : 8);
    }

    public static bool Available(string id, ScratchGameManager game, ScratchSkillTree skills) =>
        ChapterOf(id) <= UnlockedChapter(game, skills);

    public static string NextGoal(ScratchGameManager game, ScratchSkillTree skills)
    {
        if (game == null || skills == null) return "AVVIA LA PARTITA";
        switch (UnlockedChapter(game, skills))
        {
            case 1: return "COMPLETA 3 CLASSICI (" + game.CompletedByTier(ScratchGameManager.TicketKind.Standard) + "/3)";
            case 2: return skills.GetLevel("C03") == 0
                ? "ACQUISTA FORTUNATO (C03)"
                : "COMPLETA 2 FORTUNATI (" + game.CompletedByTier(ScratchGameManager.TicketKind.Fortunato) + "/2)";
            case 3: return "COMPLETA 9 FORTUNATI (" + game.CompletedByTier(ScratchGameManager.TicketKind.Fortunato) + "/9)";
            case 4: return skills.GetLevel("C05") == 0
                ? "ACQUISTA DORATO (C05)"
                : "COMPLETA 3 DORATI (" + game.CompletedByTier(ScratchGameManager.TicketKind.Gold) + "/3)";
            case 5: return ScratchSkillTree.DemoEdition ? "NUCLEO DEMO COMPLETATO"
                : skills.GetLevel("C07") == 0
                    ? "SBLOCCA DIAMANTE" : "COMPLETA 6 DIAMANTI";
            case 6: return "COMPLETA 25 DIAMANTI";
            case 7: return "COMPLETA 70 DIAMANTI";
            default: return "NUCLEO COMPLETATO";
        }
    }
}
