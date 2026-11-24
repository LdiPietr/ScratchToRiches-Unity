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

// Farming responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private int TicketReserveForCurrentPurchase()
    {
        if (IsFullDemo)
        {
            if (purchaseIndex < 0 || purchaseIndex >= FullDemoPlan.Length) return 0;
            ScratchSkillTree.SkillId id = FullDemoPlan[purchaseIndex];
            if (id == ScratchSkillTree.SkillId.C3)
                return ScratchGameManager.TicketPrice(ScratchGameManager.TicketKind.Fortunato);
            if (id == ScratchSkillTree.SkillId.C5)
                return ScratchGameManager.TicketPrice(ScratchGameManager.TicketKind.Gold);
            return 0;
        }
        if (stage <= 0 || stage >= UnlockPlans.Length ||
            purchaseIndex < 0 || purchaseIndex >= UnlockPlans[stage].Length)
            return 0;
        if (purchaseIndex != UnlockPlans[stage].Length - 1)
            return 0;
        return ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
    }

    // The target must be stable while farm tickets are purchased: ticketCost
    // changes to the FARM ticket's price on every scratch.
    private int FarmingTargetCoins()
    {
        if (farmingForTicket)
            return ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
        return FundsNeededForNextPurchase();
    }

    private int FundsNeededForNextPurchase()
    {
        if (IsFullDemo)
            return purchaseIndex >= FullDemoPlan.Length ? 0
                : BudgetRequired(skills.GetPrice(FullDemoPlan[purchaseIndex]),
                    TicketReserveForCurrentPurchase());
        if (stage <= 0 || stage >= UnlockPlans.Length ||
            purchaseIndex < 0 || purchaseIndex >= UnlockPlans[stage].Length)
            return 0;
        return BudgetRequired(skills.GetPrice(UnlockPlans[stage][purchaseIndex]),
            TicketReserveForCurrentPurchase());
    }

    private static int BudgetRequired(int skillPrice, int ticketReserve)
    {
        long total = (long)skillPrice + ticketReserve;
        return (int)Math.Min(int.MaxValue, Math.Max(0L, total));
    }

    /// <summary>
    /// Regression test for the wallet=141 / C3=140 / Fortunato=10 failure.
    /// Pure calculations: works outside Play Mode and does not alter saves.
    /// </summary>
    // Kept for programmatic QA; removed redundant standalone menu entry.
    private static void TestTicketBudget()
    {
        int failures = 0;
        // Historical regression: 141 coins, C3 costs 140, Fortunato costs 10.
        // 141 is insufficient, 150 is sufficient. The test remains valid even
        // when the live provisional ticket prices are later rebalanced.
        int skillPrice = 140;
        int nextTicketPrice = 10;
        int insufficientWallet = 141;
        int sufficientWallet = 150;
        if (BudgetRequired(skillPrice, nextTicketPrice) != sufficientWallet) failures++;
        if (insufficientWallet - skillPrice >= nextTicketPrice) failures++;
        if (sufficientWallet - skillPrice < nextTicketPrice) failures++;

        foreach (ScratchGameManager.TicketKind kind in new[]
        {
            ScratchGameManager.TicketKind.Fortunato,
            ScratchGameManager.TicketKind.Gold,
            ScratchGameManager.TicketKind.Diamond
        })
        {
            int currentTicketPrice = ScratchGameManager.TicketPrice(kind);
            if (currentTicketPrice <= 0) failures++;
            if (BudgetRequired(100, currentTicketPrice) != 100 + currentTicketPrice)
                failures++;
        }

        if (failures == 0)
            Debug.Log("STR AUTO QA: reserve budget regression PASS.");
        else
            Debug.LogError("STR AUTO QA: reserve budget regression FAIL " + failures);
    }

    private void WaitFarmShopClosed(float now)
    {
        if (flow.ShopOpen || !flow.CanShowTicketChoices) return;
        ChooseFarmTicket(now);
    }

    private void ChooseFarmTicket(float now)
    {
        // Buy and scratch the best previously unlocked tier to earn real coins.
        // Never jump ahead to an unavailable tier while farming.
        var kind = (ScratchGameManager.TicketKind)Mathf.Max(0, stage - 1);
        if (IsFullDemo)
        {
            // Best unlocked ticket the current wallet can afford. A free
            // Standard ticket always remains available, preventing deadlocks.
            kind = game.CanChooseTicket(ScratchGameManager.TicketKind.Gold)
                ? ScratchGameManager.TicketKind.Gold
                : game.CanChooseTicket(ScratchGameManager.TicketKind.Fortunato)
                    ? ScratchGameManager.TicketKind.Fortunato
                    : ScratchGameManager.TicketKind.Standard;
        }
        if (!game.CanChooseTicket(kind))
            kind = ScratchGameManager.TicketKind.Standard;
        SetPattern(ScratchSymbolManager.TestPattern.Random);
        walletBeforeTicket = economy.Coins;
        ticketCost = ScratchGameManager.TicketPrice(kind);
        if (!game.ChooseNextTicket(kind))
        {
            Fail("Impossibile acquistare il biglietto farm " + kind +
                " con saldo " + economy.Coins);
            return;
        }
        Note("FARM_TICKET_START", "kind=" + kind + ";needed=" +
            FarmingTargetCoins() + ";purpose=" + (farmingForTicket ? "TICKET" : "UPGRADE"));
        ChangePhase(Phase.WaitLive, (0.2f + (float)random.NextDouble() * 0.4f) / humanSpeed);
    }

    private void ChooseNext(float now)
    {
        if (IsFullDemo) UpdateFullDemoTicketTier();
        ScratchGameManager.TicketKind kind = (ScratchGameManager.TicketKind)stage;
        if (stage == 0 && !game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato))
        {
            int before = economy.Coins;
            bool rejected = !game.ChooseNextTicket(ScratchGameManager.TicketKind.Fortunato);
            Check(rejected && economy.Coins == before, "locked_ticket_rejection",
                "Fortunato rifiutato prima di C3 senza consumare monete.");
        }

        SetPattern(PlannedPattern(stage, patternIndex));
        ticketCost = ScratchGameManager.TicketPrice(kind);
        if (!game.IsTicketUnlocked(kind))
        {
            Fail("Il biglietto non e' sbloccato: " + kind);
            return;
        }

        // Protection for later tickets as well: a losing paid ticket must not
        // end the entire run when the wallet can still earn via older tiers.
        if (economy.Coins < ticketCost)
        {
            farming = farmingForTicket = true;
            Note("TICKET_FUNDS_NEEDED", "kind=" + kind + ";wallet=" +
                economy.Coins + ";ticketCost=" + ticketCost);
            ChooseFarmTicket(now);
            return;
        }

        walletBeforeTicket = economy.Coins;
        if (!game.ChooseNextTicket(kind))
        {
            Fail("Selezione biglietto fallita per causa diversa dal saldo: " +
                kind + " wallet=" + economy.Coins);
            return;
        }
        ChangePhase(Phase.WaitLive, (0.4f + (float)random.NextDouble() * 0.6f) / humanSpeed);
    }

    private void SetPattern(ScratchSymbolManager.TestPattern pattern)
    {
        if (autoplayMode != AutoplayMode.QACombinazioniForzate &&
            pattern != ScratchSymbolManager.TestPattern.Random)
            throw new InvalidOperationException("Real-play autoplay cannot force a winning combination.");
        activePattern = pattern;
        SerializedObject target = new SerializedObject(symbols);
        SerializedProperty prop = target.FindProperty("editorTestPattern");
        if (prop == null) throw new InvalidOperationException("Editor test pattern missing.");
        prop.enumValueIndex = (int)pattern;
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private void ChangePhase(Phase next, float delay)
    {
        phase = next;
        stateSince = Time.realtimeSinceStartup;
        nextAction = stateSince + delay;
    }
}
#endif
