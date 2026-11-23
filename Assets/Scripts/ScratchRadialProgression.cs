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
            case "G02": case "G03": case "C03": case "C11": case "E02": case "E05": return 2;
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
        if (chapter >= 4 && hasGold && gold >= 5) chapter = 5;
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

    // Progression beats raw wealth: the final ring's five skills are released
    // on consecutive earned milestones rather than bought in one shop visit.
    // Fortunato goals likewise keep the fourth ring unfolding over real play.
    public static int RequiredFortunato(string id)
    {
        switch (id)
        {
            case "E06": return 16;
            case "G07": return 19;
            case "C04": return 22;
            case "C05": return 24;
            default: return 0;
        }
    }

    public static int RequiredGold(string id)
    {
        switch (id)
        {
            case "G08": return 5;
            case "F02": return 8;
            case "C06": return 11;
            case "F03": return 14;
            case "F04": return 17;
            default: return 0;
        }
    }

    public static bool TicketGoalMet(string id, int fortunate, int gold) =>
        fortunate >= RequiredFortunato(id) && gold >= RequiredGold(id);

    public static bool Available(string id, ScratchGameManager game, ScratchSkillTree skills)
    {
        if (game == null || skills == null || ChapterOf(id) > UnlockedChapter(game, skills))
            return false;
        // Legacy saves never lose a skill already bought before pacing changed.
        return skills.GetLevel(id) > 0 || TicketGoalMet(id,
            game.CompletedByTier(ScratchGameManager.TicketKind.Fortunato),
            game.CompletedByTier(ScratchGameManager.TicketKind.Gold));
    }

    public static string SkillGoal(string id, ScratchGameManager game, ScratchSkillTree skills)
    {
        if (game == null || skills == null) return "AVVIA LA PARTITA";
        int fortunate = game.CompletedByTier(ScratchGameManager.TicketKind.Fortunato);
        int gold = game.CompletedByTier(ScratchGameManager.TicketKind.Gold);
        int requiredFortunate = RequiredFortunato(id);
        int requiredGold = RequiredGold(id);
        if (requiredFortunate > fortunate)
            return "COMPLETA " + requiredFortunate + " FORTUNATI (" +
                fortunate + "/" + requiredFortunate + ")";
        if (requiredGold > gold)
            return "COMPLETA " + requiredGold + " DORATI (" +
                gold + "/" + requiredGold + ")";
        if (ChapterOf(id) > UnlockedChapter(game, skills))
            return "NUCLEO CAPITOLO " + ChapterOf(id);
        return "PRONTO PER L'ACQUISTO";
    }

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
            case 4:
                foreach (string id in new[] { "E06", "G07", "C04", "C05" })
                    if (skills.GetLevel(id) == 0)
                        return "PER " + id + ": " + SkillGoal(id, game, skills);
                return "COMPLETA 5 DORATI (" +
                    game.CompletedByTier(ScratchGameManager.TicketKind.Gold) + "/5)";
            case 5:
                if (ScratchSkillTree.DemoEdition)
                {
                    foreach (string id in new[] { "G08", "F02", "C06", "F03", "F04" })
                        if (skills.GetLevel(id) == 0)
                            return "PER " + id + ": " + SkillGoal(id, game, skills);
                    return "FINALE: 8 DORATI, 3800 MONETE E POTERE ATTIVATO";
                }
                return skills.GetLevel("C07") == 0
                    ? "SBLOCCA DIAMANTE" : "COMPLETA 6 DIAMANTI";
            case 6: return "COMPLETA 25 DIAMANTI";
            case 7: return "COMPLETA 70 DIAMANTI";
            default: return "NUCLEO COMPLETATO";
        }
    }
}
