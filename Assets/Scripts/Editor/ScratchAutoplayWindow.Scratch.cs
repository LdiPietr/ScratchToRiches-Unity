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

// Scratch responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private void ScratchTick(float now)
    {
        if (!game.TicketActive)
        {
            ValidateTicket();
            if (!running) return;
            if (IsFullDemo && game.DemoFinished)
            {
                bool ownedAll = CountOwnedDemoSkills() == 25;
                Check(ownedAll, "all_25_demo_skills_purchased",
                    "Acquistate " + CountOwnedDemoSkills() + "/25 skill.");
                Check(forcedTicketsPlayed == 0 && !qaFunds, "full_demo_natural",
                    "Forzati=" + forcedTicketsPlayed + ";creditoQA=" + qaFunds);
                Check(game.DemoFinalGoldTickets >= game.DemoFinalGoldMinTickets,
                    "eight_final_gold_tickets",
                    "Gold dopo entrambi i capstone=" + game.DemoFinalGoldTickets +
                    "/" + game.DemoFinalGoldMinTickets);
                Check(game.DemoFinalGoldPayout >= game.DemoFinalGoldPayoutTarget,
                    "finale_earned_gold_payout",
                    "Incassi Dorati=" + game.DemoFinalGoldPayout + "/" + game.DemoFinalGoldPayoutTarget);
                Check(game.DemoFinalPowerActivated, "finale_real_activation",
                    "Attivato realmente Titanico o Jolly dopo F04.");
                Check(qaTitanActivations > 0, "titan_tested_in_live_scratch",
                    "Attivazioni Titanico durante gesture reali=" + qaTitanActivations);
                Check(qaC06Inspections > 0, "C06_inspection_exercised",
                    "Anteprime scelte dal giocatore tramite tasto destro=" + qaC06Inspections);
                Check(skills.JollyActivationCount > 0, "real_visible_jolly_scratched",
                    "Jolly realmente grattati=" + skills.JollyActivationCount);
                Check(game.State == ScratchGameManager.RoundState.Finished, "authored_demo_end",
                    "La scena ha completato il milestone finale.");
                ProgressEvent("DEMO_FINISHED", "-", 0, 0, "End scene reached naturally");
                finalSuccess = assertionsFailed == 0 && ownedAll;
                ChangePhase(Phase.FinalHold, 10f);
                return;
            }
            if (IsFullDemo && tickets + farmTickets >= 600)
            {
                Fail("Limite sicurezza: 600 biglietti senza completare la demo.");
                return;
            }
            ChangePhase(Phase.WaitChoice, 0.6f / humanSpeed);
            return;
        }
        if (activeCell == null || activeCoating == null)
        {
            Fail("Casella o copertura mancante durante grattatura.");
            return;
        }
        if (activeCell.IsComplete)
        {
            EndPointer();
            bool hudOk = ScratchVintageHudAudit.ValidateMatchProgress(game, out string hudDetail);
            Check(hudOk, "hud_match_progress", hudDetail);
            if (!hudOk) { Fail(hudDetail); return; }
            Note("CELL_COMPLETE", "cell=" + activeCell.name +
                ";seconds=" + (now - cellStartedAt).ToString("F2", CultureInfo.InvariantCulture) +
                ";percent=" + activeCell.RevealedPercentage.ToString("F1", CultureInfo.InvariantCulture));
            cellIndex++;
            if (cellIndex < game.MaxScratches)
            {
                PrepareCell();
                nextAction = now + (0.22f + (float)random.NextDouble() * 0.45f) / humanSpeed;
            }
            else
                ChangePhase(Phase.WaitChoice, 0.5f / humanSpeed);
            return;
        }
        if (strokePointIndex >= 480)
        {
            Fail("Impossibile completare Cell " + activeCell.name + " all'85%.");
            return;
        }

        // Serpentine human-like movement; each sample drives the same real pointer handlers.
        int scan = strokePointIndex % 180;
        int row = scan / 15;
        int column = scan % 15;
        float u = 0.04f + (row % 2 == 0 ? column : 14 - column) * (0.92f / 14f);
        float v = 0.07f + row * (0.86f / 11f);
        float wobble = ((float)random.NextDouble() - 0.5f) * 0.014f;
        v = Mathf.Clamp(v + wobble, 0.04f, 0.96f);
        EmitPointer(u, v);
        strokePointIndex++;
        nextAction = now + (0.013f + (float)random.NextDouble() * 0.012f) / humanSpeed;
    }

    private void EmitPointer(float u, float v)
    {
        // Start a synthetic drag inside every authored silhouette. Corner
        // samples on circular/hexagonal foils must not consume a pick.
        if (!pointerHeld) { u = .5f; v = .5f; }
        RectTransform rt = activeCoating.rectTransform;
        Rect rect = rt.rect;
        Vector3 local = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, u),
            Mathf.Lerp(rect.yMin, rect.yMax, v), 0f);
        Vector3 world = rt.TransformPoint(local);
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -809,
                button = PointerEventData.InputButton.Left
            };
        pointer.position = screen;
        if (!pointerHeld)
        {
            activeCell.OnPointerDown(pointer);
            pointerHeld = true;
            // Editor equivalent of holding SHIFT during this REAL scratch drag:
            // no free charges and no fabricated wins or completed cells.
            if (IsFullDemo && !titanActivatedThisTicket &&
                skills.GetLevel("G08") > 0 && skills.TitanCharges > 0)
            {
                int before = skills.TitanActivationCount;
                bool activated = activeCell.QaActivateTitanDuringGesture();
                Check(activated && skills.TitanActivationCount == before + 1,
                    "G08_real_titan_activation", "SHIFT-equivalent consumed a real charge.");
                if (activated)
                {
                    titanActivatedThisTicket = true;
                    qaTitanActivations++;
                    Note("G08_TITAN_USED", "cell=" + activeCell.name);
                }
            }
        }
        else activeCell.OnDrag(pointer);
    }

    private void EndPointer()
    {
        if (pointerHeld && activeCell != null && pointer != null)
            activeCell.OnPointerUp(pointer);
        pointerHeld = false;
    }

    private void ValidateTicket()
    {
        EndPointer();
        bool hudOk = ScratchVintageHudAudit.ValidateMatchProgress(game, out string hudDetail);
        Check(hudOk, "hud_match_result", hudDetail);
        if (!hudOk) { Fail(hudDetail); return; }
        var pat = activePattern;
        ScratchSymbolManager.MatchTier expect = (ScratchSymbolManager.MatchTier)(int)pat;
        Check(game.RevealedCount == game.MaxScratches, "fully_scratched",
            "Grattate=" + game.RevealedCount + "/" + game.MaxScratches);
        if (pat != ScratchSymbolManager.TestPattern.Random)
            Check(game.CurrentMatch.Tier == expect, "combination",
                "Attesa=" + expect + " reale=" + game.CurrentMatch.Tier);
        Check(game.TicketPayout == ExpectedPayout(), "payout_formula",
            "Vincita=" + game.TicketPayout + ";formula=" + ExpectedPayout() +
            ";match=" + game.CurrentMatch.Tier);
        Check(economy.Coins - walletBeforeTicket == game.TicketPayout - ticketCost,
            "single_settlement",
            "delta_wallet=" + (economy.Coins - walletBeforeTicket) +
            " expected=" + (game.TicketPayout - ticketCost));
        int afterSettlement = economy.Coins;
        game.OnCellCompleted(activeCell); // A duplicate completion callback must be ignored.
        Check(economy.Coins == afterSettlement, "duplicate_completion_rejected",
            "Saldo immutato dopo una chiamata OnCellCompleted duplicata.");
        if (!farming && stage == 3 && pat != ScratchSymbolManager.TestPattern.Random)
            Check(game.CurrentMatch.IsMatch, "diamond_guaranteed_match",
                "Diamante con 5 simboli grattati.");
        if (farming) farmTickets++;
        else tickets++;
        if (pat == ScratchSymbolManager.TestPattern.Random)
        {
            naturalTicketsPlayed++;
            observedRandomResults[(int)game.CurrentMatch.Tier]++;
        }
        else
            forcedTicketsPlayed++;
        if (IsFullDemo)
            ProgressEvent("TICKET_SETTLED", "-", 0, ticketCost,
                "payout=" + game.TicketPayout + ";match=" + game.CurrentMatch.Tier);
        Note(farming ? "FARM_TICKET_END" : "TICKET_END", "match=" + game.CurrentMatch.Tier + ";gain=" + game.TicketPayout +
            ";net=" + (game.TicketPayout - ticketCost) +
            ";seconds=" + (Time.realtimeSinceStartup - ticketStartedAt).ToString("F2", CultureInfo.InvariantCulture));
        lastStatus = (farming ? "Farm " + farmTickets : "Test " + tickets) +
            ": " + game.CurrentTicketKind + " " + pat + " = " + game.CurrentMatch.Tier +
            " +" + game.TicketPayout;
    }

    private int ExpectedPayout()
    {
        var match = game.CurrentMatch;
        if (!match.IsMatch)
        {
            int level = skills.GetLevel(ScratchSkillTree.SkillId.E5);
            return level > 0 ? 3 * level +
                (int)Math.Ceiling(Math.Max(0, ticketCost) * 0.2) : 0;
        }
        int rewardBase = game.TicketPrize(game.CurrentTicketKind) +
            2 * skills.GetLevel(ScratchSkillTree.SkillId.E1);
        double extra = match.Tier == ScratchSymbolManager.MatchTier.Pair
            ? 1.0 + 0.1 * skills.GetLevel(ScratchSkillTree.SkillId.E2)
            : match.Tier == ScratchSymbolManager.MatchTier.Triple
                ? 1.0 + 0.15 * skills.GetLevel(ScratchSkillTree.SkillId.E3)
                : 1.0 + 0.2 * skills.GetLevel(ScratchSkillTree.SkillId.E4);
        double payout = Math.Ceiling(rewardBase * match.Multiplier * extra);
        if (skills.GetLevel(ScratchSkillTree.SkillId.E6) > 0 && streakBeforeTicket > 0)
            payout = Math.Ceiling(payout * (1.0 + 0.05 * Mathf.Min(4, streakBeforeTicket)));
        return (int)Math.Min(int.MaxValue, payout);
    }
}
#endif
