using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tickets responsibility of ScratchGameManager.
public sealed partial class ScratchGameManager
{
    /// <summary>Starts the unlimited game session with a free Classic ticket.</summary>
    public void StartNewRun()
    {
        if (!Application.isPlaying || cellManager == null || state != RoundState.Ready)
            return;

        ticketsCompleted = 0;
        state = RoundState.Playing;
        endReason = RoundEndReason.None;
        ticketNumberInRun = 0;
        ticketActive = false;
        BeginTicket(TicketKind.Standard, false);
    }

    /// <summary>
    /// Only the between-ticket selection panel can request the next ticket.
    /// Insufficient funds or missing unlocks never create a ticket or charge coins.
    /// </summary>
    public bool ChooseNextTicket(TicketKind kind)
    {
        if (!WaitingForTicketChoice || !CanChooseTicket(kind))
            return false;
        return BeginTicket(kind, true);
    }

    private bool BeginTicket(TicketKind kind, bool charge)
    {
        if (state != RoundState.Playing || ticketActive || !IsTicketUnlocked(kind))
            return false;

        int cost = charge ? TicketPrice(kind) : 0;
        if (cost > economyManager.Coins || cost == int.MaxValue)
            return false;

        selectedCells.Clear();
        ticketPayout = 0;
        settledMatch = default;
        endReason = RoundEndReason.None;
        ticketVoided = false;
        targetedPreviewUsed = false;

        // Hide all symbol artwork BEFORE any active-cell geometry changes.
        // This prevents the prior or next ticket's symbols from flashing while
        // the coating textures and sprites are being replaced.
        cellManager.BeginTicketVisualPreparation();
        if (!cellManager.ConfigureGrid(TicketSide(kind)))
        {
            cellManager.EndTicketVisualPreparation();
            return false;
        }
        cellManager.PrepareCoverForTicket(kind);
        internalReset = true;
        cellManager.ResetAllCells();
        internalReset = false;

        if (!symbolManager.GenerateRound(SymbolPoolSize(kind), kind))
        {
            Debug.LogError("V2: could not generate a scratch ticket.", this);
            if (ticketNumberInRun == 0) state = RoundState.Ready;
            cellManager.EndTicketVisualPreparation();
            RefreshHud();
            return false;
        }

        // Generate first, then charge: failed generation never consumes coins.
        // On the main Unity thread no other wallet mutation can interleave.
        if (cost > 0 && !economyManager.TrySpendCoins(cost))
        {
            Debug.LogError("V2: ticket purchase could not be completed.", this);
            cellManager.EndTicketVisualPreparation();
            RefreshHud();
            return false;
        }

        currentTicketKind = kind;
        if (symbolManager.GuaranteedJollyThisTicket && kind == TicketKind.Gold)
            skillTree?.OnGoldenJollyTicketPurchased();
        currentTicketId++;
        ticketNumberInRun++;
        ticketActive = true;
        economyManager.BeginTicket();
        ApplyPreviews();
        if (!cellManager.EndTicketVisualPreparation())
            Debug.LogError("V2: one or more scratch masks are not ready before showing symbols.", this);
        RefreshHud();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("TICKET_START", "ticket=" + ticketNumberInRun +
            "|kind=" + kind + "|cost=" + cost + "|picks=" + MaxScratches +
            "|grid=" + TicketSide(kind));
#endif
        return true;
    }

    public bool IsCellSelected(ScratchCell cell) =>
        cell != null && selectedCells.Contains(cell);

    public bool TrySelectCell(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || cell == null || !cell.isActiveAndEnabled)
            return false;
        // No input while an animated ticket is arriving or leaving.
        if (presentation != null && !presentation.CanScratch)
            return false;
        if (!cell.transform.IsChildOf(transform))
            return false;
        if (selectedCells.Contains(cell))
            return true;
        if (selectedCells.Count >= MaxScratches)
            return false;

        selectedCells.Add(cell);
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("CELL_SELECTED", "cell=" + cell.name +
            "|pick=" + selectedCells.Count + "|limit=" + MaxScratches);
#endif
        RefreshHud();
        return true;
    }

    /// <summary>
    /// C06: one player-chosen, truthful preview per ticket. Right-click on a
    /// covered unpreviewed cell; no scratch selection or hidden-board inspection.
    /// </summary>
    public bool TryInspectCell(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || targetedPreviewUsed ||
            Level(ScratchSkillTree.SkillId.C6) <= 0 || cell == null ||
            !cell.isActiveAndEnabled || cell.IsComplete || cell.IsPreviewVisible ||
            cell.RevealedPercentage > 0f ||
            (presentation != null && !presentation.CanScratch))
            return false;

        bool active = false;
        foreach (ScratchCell candidate in cellManager.ActiveCells)
            if (candidate == cell) { active = true; break; }
        if (!active || symbolManager == null || !symbolManager.TryGetSymbol(cell, out string symbol))
            return false;
        targetedPreviewUsed = true;
        cell.SetPreview(symbol);
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("C06_INSPECTION", "cell=" + cell.name);
#endif
        RefreshHud();
        return true;
    }

    public void OnCellCompleted(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || !selectedCells.Contains(cell) ||
            cell == null || !cell.IsComplete)
            return;
        // Complete() calls only once; duplicate callbacks after settlement cannot recharge.
        skillTree?.OnCompletedScratchCell();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("CELL_COMPLETED", "cell=" + cell.name +
            "|revealed_percent=" + cell.RevealedPercentage.ToString("F0") +
            "|selected=" + selectedCells.Count + "|limit=" + MaxScratches);
#endif
        if (symbolManager != null && cell == symbolManager.JollyCell)
            skillTree?.OnJollyScratched();
        if (selectedCells.Count == MaxScratches && RevealedCount == MaxScratches)
            FinishTicket(RoundEndReason.AllCellsRevealed);
        else
            RefreshHud();
    }

    public void OnCellsReset()
    {
        selectedCells.Clear();

        // The Inspector reset must not allow free rerolls of the same ticket.
        if (!internalReset && ticketActive)
            ticketVoided = true;
        RefreshHud();
    }

    // Legacy public scene/API entry point deliberately disabled: no early exit.
    // Keep the method for serialization compatibility; it cannot void progress.
    public void EndRoundEarly() { }

    private void FinishTicket(RoundEndReason reason)
    {
        if (state != RoundState.Playing || !ticketActive)
            return;

        bool fullyCompleted = selectedCells.Count == MaxScratches && RevealedCount == MaxScratches;
        settledMatch = symbolManager.Evaluate(selectedCells);
        ticketActive = false;
        endReason = reason;

        // A manual Inspector reset voids the ticket, preventing free rerolls.
        if (ticketVoided)
        {
            settledMatch = default;
            ticketPayout = economyManager.SettleTicket(currentTicketId, settledMatch, false,
                TicketPrize(currentTicketKind), TicketPrice(currentTicketKind));
        }
        else
        {
            ticketPayout = economyManager.SettleTicket(currentTicketId, settledMatch, fullyCompleted,
                TicketPrize(currentTicketKind), TicketPrice(currentTicketKind));
            if (fullyCompleted)
            {
                ticketsCompleted++;
                int tier = (int)currentTicketKind;
                if (tier >= 0 && tier < completedByTier.Length)
                {
                    completedByTier[tier]++;
                    PlayerPrefs.SetInt(RadialTicketPrefix + tier, completedByTier[tier]);
                    PlayerPrefs.Save();
                }
            }
        }
        if (!ticketVoided && fullyCompleted && currentTicketKind == TicketKind.Gold &&
            demoCapstoneActive && !demoFinished && currentTicketId != demoExcludedTicketId)
        {
            demoGoldCompleted++;
            demoGoldPayout = (int)Math.Min(int.MaxValue, (long)demoGoldPayout + Mathf.Max(0, ticketPayout));
            PlayerPrefs.SetInt(DemoGoldKey, demoGoldCompleted);
            PlayerPrefs.SetInt(DemoGoldPayoutKey, demoGoldPayout);
            // Eight completed Gold tickets, actual earnings and at least one
            // real late-game activation: skill, not raw waiting, ends the demo.
            if (FinalChallengeComplete(demoGoldCompleted, demoGoldPayout,
                    DemoFinalPowerActivated))
            {
                demoFinished = true;
                state = RoundState.Finished;
                PlayerPrefs.SetInt(DemoDoneKey, 1);
                if (demoEndOverlay != null) demoEndOverlay.SetActive(true);
            }
            PlayerPrefs.Save();
        }
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("TICKET_END", "ticket=" + ticketNumberInRun +
            "|kind=" + currentTicketKind + "|complete=" + fullyCompleted +
            "|void=" + ticketVoided + "|payout=" + ticketPayout +
            "|match=" + settledMatch.Tier + "|wallet=" + economyManager.Coins +
            "|demo_finished=" + demoFinished);
#endif
        RefreshHud();
    }

    private void ApplyPreviews()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetPreview(null);

        // C06 is now a separate manual right-click inspection, not five
        // randomly revealed symbols at ticket generation.
        int count = Level(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
            Level(ScratchSkillTree.SkillId.C2) > 0 ? 2 :
            Level(ScratchSkillTree.SkillId.C1) > 0 ? 1 : 0;

        // The joker preview from F04 is an EXTRA free preview. Never waste one
        // of the regular C-branch preview slots on the same cell.
        ScratchCell joker = symbolManager != null ? symbolManager.JollyCell : null;
        List<int> available = new List<int>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != joker) available.Add(i);
        for (int i = 0; i < Mathf.Min(count, cells.Length); i++)
        {
            int chosen = UnityEngine.Random.Range(0, available.Count);
            ScratchCell cell = cells[available[chosen]];
            available.RemoveAt(chosen);
            if (symbolManager.TryGetSymbol(cell, out string symbol))
                cell.SetPreview(symbol);
        }
        if (joker != null && symbolManager.GuaranteedJollyThisTicket)
            joker.SetPreview("JOLLY");
    }
}
