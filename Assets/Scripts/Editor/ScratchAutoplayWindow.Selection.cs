#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// Selection responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private void WaitLive(float now)
    {
        if (!game.TicketActive || !flow.CanScratch) return;
        ScratchGameManager.TicketKind expected = farming
            ? game.CurrentTicketKind
            : (ScratchGameManager.TicketKind)stage;
        Check(game.CurrentTicketKind == expected, "ticket_kind",
            "Atteso=" + expected + " effettivo=" + game.CurrentTicketKind);
        if (autoplayMode != AutoplayMode.QACombinazioniForzate)
            Check(activePattern == ScratchSymbolManager.TestPattern.Random,
                "no_forced_outcome_in_real_play", "Current symbol pattern: " + activePattern);
        Check(cellManager.ActiveCells.Length == game.CurrentGridSide * game.CurrentGridSide,
            "grid_size", game.CurrentGridSide + "x" + game.CurrentGridSide);
        Check(game.MaxScratches == ScratchGameManager.PickLimitFromPerk(skills.GetLevel("C11")),
            "global_pick_perk",
            "Scelte indipendenti dal ticket: " + game.MaxScratches +
            " | C11=" + skills.GetLevel("C11") +
            " | griglia=" + game.CurrentGridSide);
        if (stage == 3 && !farming)
        {
            HashSet<string> unique = new HashSet<string>();
            foreach (ScratchCell sc in cellManager.ActiveCells)
                if (symbols.TryGetSymbol(sc, out string label)) unique.Add(label);
            Check(unique.Count <= 4 && unique.Count > 0,
                "diamond_pool", "Simboli diversi sul biglietto=" + unique.Count);
        }
        int expectedPreviews = skills.GetLevel(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
            skills.GetLevel(ScratchSkillTree.SkillId.C2) > 0 ? 2 :
            skills.GetLevel(ScratchSkillTree.SkillId.C1) > 0 ? 1 : 0;
        int visible = 0;
        int unknownLeaked = 0;
        int incompleteMasks = 0;
        foreach (ScratchCell cell in cellManager.ActiveCells)
        {
            bool canRead = cell.TryGetVisibleSymbol(out _);
            if (cell.IsPreviewVisible) visible++;
            else if (!cell.IsComplete && canRead) unknownLeaked++;
            RawImage coating = cell.GetComponentInChildren<RawImage>(true);
            if (coating == null || !coating.enabled || coating.texture == null ||
                cell.RevealedPercentage > 0f)
                incompleteMasks++;
        }
        if (skills.GetLevel("F04") > 0 && symbols.GuaranteedJollyThisTicket &&
            symbols.JollyCell != null)
            expectedPreviews++;
        Check(visible == expectedPreviews, "preview_count",
            "Anteprime attese=" + expectedPreviews + ", viste=" + visible);
        Check(unknownLeaked == 0, "no_hidden_symbol_leak",
            "Simboli segreti leggibili in caselle non grattate=" + unknownLeaked);
        Check(incompleteMasks == 0, "fresh_coating",
            "Patine mancanti, disabilitate o gia' grattate=" + incompleteMasks);
        Note("PREVIEWS_VISIBLE", "count=" + visible + ";previewSkills=" + expectedPreviews);
        // C06 is now an intentional RIGHT-CLICK on one unpreviewed cell, not
        // two extra automatic previews. Check actual pointer handling.
        if (IsFullDemo && skills.GetLevel("C06") > 0)
        {
            ScratchCell inspectionTarget = null;
            ScratchCell rejectionTarget = null;
            foreach (ScratchCell candidate in cellManager.ActiveCells)
            {
                if (candidate.IsComplete || candidate.IsPreviewVisible) continue;
                if (inspectionTarget == null) inspectionTarget = candidate;
                else if (rejectionTarget == null) rejectionTarget = candidate;
            }
            if (inspectionTarget == null || rejectionTarget == null)
            {
                Fail("Non ci sono due caselle coperte con cui verificare C06.");
                return;
            }
            int selectedBefore = game.SelectedCount;
            int revealedBefore = game.RevealedCount;
            PointerEventData inspect = new PointerEventData(EventSystem.current)
            {
                pointerId = -810, button = PointerEventData.InputButton.Right
            };
            inspectionTarget.OnPointerDown(inspect);
            bool inspected = inspectionTarget.IsPreviewVisible &&
                game.InspectionUsedThisTicket && game.SelectedCount == selectedBefore &&
                game.RevealedCount == revealedBefore &&
                inspectionTarget.RevealedPercentage == 0f;
            Check(inspected, "C06_chosen_preview",
                "Ispezione su " + inspectionTarget.name + ", senza grattare o consumare scelte.");
            rejectionTarget.OnPointerDown(inspect);
            Check(!rejectionTarget.IsPreviewVisible &&
                game.SelectedCount == selectedBefore &&
                game.RevealedCount == revealedBefore,
                "C06_once_per_ticket", "La seconda ispezione viene rifiutata.");
            if (inspected)
            {
                qaC06Inspections++;
                Note("C06_INSPECTED", "cell=" + inspectionTarget.name);
            }
        }
        titanActivatedThisTicket = false;
        ticketStartedAt = now;
        streakBeforeTicket = economy.ConsecutiveWinningTickets;
        cellIndex = 0;
        PrepareCell();
        Note("TICKET_START", game.CurrentGridSide + "x" + game.CurrentGridSide);
        ChangePhase(Phase.Scratch, 0.5f / humanSpeed);
    }

    /// <summary>
    /// Only functional-QA mode uses manufactured combinations. Real simulations
    /// always select Random and can earn any result (including no win).
    /// </summary>
    private ScratchSymbolManager.TestPattern PlannedPattern(int tierIndex, int ticketIndex) =>
        autoplayMode != AutoplayMode.QACombinazioniForzate
            ? ScratchSymbolManager.TestPattern.Random
            : TestPatterns[tierIndex][ticketIndex];

    // Values reflect the actual payout formula, including economic upgrades
    // and the E5 consolation prize. Ticket cost is already sunk at this point.
    private double[] PayoutByMultiplicity()
    {
        double rewardBase = game.TicketPrize(game.CurrentTicketKind) +
            2 * skills.GetLevel(ScratchSkillTree.SkillId.E1);
        int e5 = skills.GetLevel(ScratchSkillTree.SkillId.E5);
        double consolation = e5 > 0 ? 3 * e5 +
            Math.Ceiling(Math.Max(0, ticketCost) * 0.2) : 0;
        double pair = Math.Ceiling(rewardBase * 2 *
            (1.0 + 0.1 * skills.GetLevel(ScratchSkillTree.SkillId.E2)));
        double triple = Math.Ceiling(rewardBase * 3 *
            (1.0 + 0.15 * skills.GetLevel(ScratchSkillTree.SkillId.E3)));
        double betterBonus = 1.0 + 0.2 * skills.GetLevel(ScratchSkillTree.SkillId.E4);
        double poker = Math.Ceiling(rewardBase * 4 * betterBonus);
        double five = Math.Ceiling(rewardBase * 5 * betterBonus);
        if (skills.GetLevel(ScratchSkillTree.SkillId.E6) > 0 && streakBeforeTicket > 0)
        {
            double streakBonus = 1.0 + 0.05 * Mathf.Min(4, streakBeforeTicket);
            pair = Math.Ceiling(pair * streakBonus);
            triple = Math.Ceiling(triple * streakBonus);
            poker = Math.Ceiling(poker * streakBonus);
            five = Math.Ceiling(five * streakBonus);
        }
        return new[] { consolation, consolation, pair, triple, poker, five };
    }

    private static string EvText(double value) => double.IsNegativeInfinity(value) ? "n/a" :
        value.ToString("F3", CultureInfo.InvariantCulture);

    private void PrepareCell()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        bool strategic = activePattern == ScratchSymbolManager.TestPattern.Random;

        if (strategic)
        {
            // F04 shows its guaranteed joker to the player. Choose it by
            // inspecting ONLY player-visible previews, never hidden symbol data.
            ScratchCell visibleJolly = null;
            if (IsFullDemo)
                foreach (ScratchCell candidate in cells)
                    if (!candidate.IsComplete && candidate.IsPreviewVisible &&
                        candidate.TryGetVisibleSymbol(out string visible) && visible == "JOLLY")
                    {
                        visibleJolly = candidate;
                        break;
                    }
            if (visibleJolly != null)
            {
                activeCell = visibleJolly;
                informedChoices++;
                previewChoices++;
                Note("CELL_CHOICE", "mode=VISIBLE_JOLLY;cell=" + activeCell.name);
            }
            else
            {
            int remaining = game.MaxScratches - game.RevealedCount;
            ScratchAutoplayPreviewStrategy.Choice choice =
                ScratchAutoplayPreviewStrategy.Choose(cells, remaining,
                    ScratchGameManager.SymbolPoolSize(game.CurrentTicketKind),
                    PayoutByMultiplicity(), random);
            activeCell = choice.Cell;
            informedChoices++;
            if (choice.UsesPreview) previewChoices++;
            else
            {
                randomExplorationChoices++;
                if (choice.VisiblePreviews > 0) deliberatelySkippedPreviews++;
            }
            if (choice.MatchesRevealed) previewMatchingChoices++;
            Note("CELL_CHOICE", "mode=EXPECTED_VALUE;cell=" + activeCell.name +
                ";visiblePreviews=" + choice.VisiblePreviews +
                ";usedPreview=" + choice.UsesPreview +
                ";matchedRevealed=" + choice.MatchesRevealed +
                ";expectedPayout=" + choice.ExpectedPayout.ToString("F3", CultureInfo.InvariantCulture) +
                ";unknownExpected=" + EvText(choice.ExploreExpectedPayout) +
                ";previewExpected=" + EvText(choice.BestPreviewExpectedPayout) +
                ";decision=" + choice.Reason);
            }
        }
        else
        {
            // A forced deterministic payout test must select the same fixed
            // sequence of positions every time. It is not a strategy benchmark.
            activeCell = cells[cellIndex];
            Note("CELL_CHOICE", "mode=FIXED_QA;cell=" + activeCell.name +
                ";reason=force deterministic combination");
        }

        // First move diversity is an observed statistic, not an imposed pattern.
        if (cellIndex == 0)
            uniqueFirstChoices.Add(game.CurrentTicketKind + ":" + activeCell.name);
        cellStartedAt = Time.realtimeSinceStartup;
        activeCoating = activeCell.GetComponentInChildren<RawImage>(true);
        pointerHeld = false;
        strokePointIndex = 0;
    }
}
#endif
