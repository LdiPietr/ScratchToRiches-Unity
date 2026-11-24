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

// Purchases responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private void BeginShop(float now)
    {
        if (!flow.CanShowTicketChoices) return;
        if (startFresh && !IsFullDemo)
            Check(!game.IsTicketUnlocked((ScratchGameManager.TicketKind)stage),
                "locked_before_upgrade", "Tier " + stage + " non acquistabile prima dello sblocco.");
        flow.OpenShop();
        Note("SHOP_OPEN", "sblocco=" + (ScratchGameManager.TicketKind)stage);
        ChangePhase(Phase.WaitShop, 0.1f);
    }

    private void AwaitShop(float now)
    {
        if (flow.ShopVisible) ChangePhase(Phase.StartPurchase, 0.25f / humanSpeed);
    }

    private void StartPurchase(float now)
    {
        var plan = ActivePurchasePlan;
        if (purchaseIndex >= plan.Length)
        {
            ChangePhase(Phase.CloseShop, 0.35f / humanSpeed);
            return;
        }

        ScratchSkillTree.SkillId id = plan[purchaseIndex];
        int targetLevel = 1;
        for (int i = 0; i < purchaseIndex; i++)
            if (plan[i] == id) targetLevel++;
        if (skills.GetLevel(id) >= targetLevel)
        {
            purchaseIndex++;
            nextAction = now + 0.1f;
            return;
        }
        // The radial nucleus imposes a second gate independent of price and
        // ordinary skill prerequisites. In particular G08 (chapter 5) must
        // wait for FIVE settled Gold tickets, not just one profitable ticket.
        // Check again on entering the shop: farming may have finished on cash
        // alone after the last ticket, while its chapter is still locked.
        string stableId = ScratchSkillTree.StableId(id);
        if (IsFullDemo && !ScratchRadialProgression.Available(stableId, game, skills))
        {
            farming = true;
            farmingForTicket = false;
            Note("NUCLEUS_FARM", "next=" + stableId +
                ";chapter=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";neededChapter=" + ScratchRadialProgression.ChapterOf(stableId) +
                ";goal=" + ScratchRadialProgression.NextGoal(game, skills));
            lastStatus = "Aspetto sblocco capitolo " +
                ScratchRadialProgression.ChapterOf(stableId) + " per " + stableId;
            flow.ContinueAfterUpgrades();
            ChangePhase(Phase.WaitFarmShopClosed, 0.1f);
            return;
        }

        // The final unlock of each tier must leave enough money to buy at
        // least ONE ticket of the newly unlocked tier. Without this reserve the
        // tester unlocks Fortunato at 140, remains with 1 coin, then aborts.
        int requiredCash = FundsNeededForNextPurchase();
        if (skills.RequirementsMet(id) && economy.Coins < requiredCash)
        {
            farming = true;
            farmingForTicket = false;
            Note("FARM_START", "skill=" + id + ";wallet=" + economy.Coins +
                ";upgradePrice=" + skills.GetPrice(id) +
                ";ticketReserve=" + TicketReserveForCurrentPurchase() +
                ";totalRequired=" + requiredCash);
            lastStatus = "Farming per " + id + ": saldo " + economy.Coins +
                ", necessari " + requiredCash + " (include primo biglietto)";
            flow.ContinueAfterUpgrades();
            ChangePhase(Phase.WaitFarmShopClosed, 0.1f);
            return;
        }
        if (!skills.CanPurchase(id))
        {
            Fail("Non acquistabile " + id + " (wallet=" + economy.Coins +
                ";prezzo=" + skills.GetPrice(id) +
                ";riserva biglietto=" + TicketReserveForCurrentPurchase() +
                ";requisiti=" + skills.RequirementText(id) +
                ";capitoloCorrente=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";capitoloRichiesto=" + ScratchRadialProgression.ChapterOf(stableId) +
                ";nucleoDisponibile=" + ScratchRadialProgression.Available(stableId, game, skills) +
                ";demoEligible=" + (ScratchSkillCatalogV2.Find(stableId)?.demoEligible ?? false) +
                ";implemented=" + ScratchSkillCatalogV2.EffectImplemented(stableId) + ")");
            return;
        }

        // The 90-node UI is now a single map. Focus the *real* authored
        // node, then exercise the unchanged hold-to-buy script via QA input.
        treeUI.RevealSkill(stableId);
        pressedHold = null;
        foreach (ScratchHoldToBuy hold in holdButtons)
        {
            if (hold != null && hold.StableSkillId == stableId &&
                hold.gameObject.name == "MapSkillNode_" + stableId)
            {
                pressedHold = hold;
                break;
            }
        }
        if (pressedHold == null)
        {
            Fail("Hold-to-buy non trovato per " + id);
            return;
        }
        expectedSkillLevel = skills.GetLevel(id) + 1;
        pendingPurchasePrice = skills.GetPrice(id);
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -809, button = PointerEventData.InputButton.Left
            };
        if (!pressedHold.QaBeginHold(pointer))
        {
            Fail("Impossibile iniziare hold su " + id + " (nodeActive=" +
                pressedHold.isActiveAndEnabled + ";canBuy=" + treeUI.CanPurchaseSkill(stableId) + ")");
            return;
        }
        lastQaHoldTick = EditorApplication.timeSinceStartup;
        Note("HOLD_BEGIN", "abilita=" + id + ";prezzo=" + skills.GetPrice(id) +
            ";holdSeconds=" + pressedHold.QaDuration);
        ChangePhase(Phase.WaitPurchase, 0f);
    }

    private void EndPurchase(float now)
    {
        ScratchSkillTree.SkillId id = ActivePurchasePlan[purchaseIndex];
        if (pressedHold == null)
        {
            Fail("Handle del nodo perso durante hold di " + id);
            return;
        }

        double editorNow = EditorApplication.timeSinceStartup;
        float step = Mathf.Clamp((float)(editorNow - lastQaHoldTick), 0f, 0.25f);
        lastQaHoldTick = editorNow;
        pressedHold.QaTick(step);

        if (skills.GetLevel(id) != expectedSkillLevel)
        {
            if (now - stateSince <= 6f) return;
            string details = id + " livello=" + skills.GetLevel(id) +
                " atteso=" + expectedSkillLevel +
                " qaHold=" + pressedHold.QaHolding +
                " elapsed=" + pressedHold.QaElapsed.ToString("F2", CultureInfo.InvariantCulture) +
                "/" + pressedHold.QaDuration +
                " scriptActive=" + pressedHold.isActiveAndEnabled +
                " canBuy=" + skills.CanPurchase(id) +
                " wallet=" + economy.Coins;
            pressedHold.QaReleaseHold();
            Check(false, "hold_to_buy", details);
            Finish(false, "Acquisto fallito: " + details);
            return;
        }

        pressedHold.QaReleaseHold();
        Check(true, "hold_to_buy", id + " livello=" + expectedSkillLevel);
        Note("SKILL_PURCHASED", id.ToString());
        if (IsFullDemo)
        {
            lastUpgradeAt = Time.realtimeSinceStartup;
            ProgressEvent("SKILL_PURCHASED", ScratchSkillTree.StableId(id),
                expectedSkillLevel, pendingPurchasePrice,
                "target_step=" + (purchaseIndex + 1) + "/" + FullDemoPlan.Length);
            if (id == ScratchSkillTree.SkillId.G8)
                Check(!game.DemoFinished && game.DemoFinalGoldTickets == 0,
                    "G08_cannot_end_demo_alone",
                    "Il Titanico deve essere utilizzabile prima del Jolly.");
            if (id == ScratchSkillTree.SkillId.F4)
                Check(!game.DemoFinished && game.DemoFinalGoldTickets == 0 &&
                      game.DemoFinalGoldPayout == 0,
                    "F04_opens_fresh_finale",
                    "Finale inizia dopo entrambi gli sblocchi, con contatori azzerati.");
            UpdateFullDemoTicketTier();
        }
        if ((!IsFullDemo && purchaseIndex == UnlockPlans[stage].Length - 1) ||
            (IsFullDemo && TicketReserveForCurrentPurchase() > 0))
        {
            int reserve = ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
            if (!Check(economy.Coins >= reserve, "next_ticket_cash_reserved",
                "unlocked=" + stage + ";wallet=" + economy.Coins + ";ticketCost=" + reserve))
            {
                pressedHold = null;
                Finish(false, "Acquisto dello sblocco senza fondi per il nuovo biglietto.");
                return;
            }
        }
        pressedHold = null;
        purchaseIndex++;
        ChangePhase(Phase.StartPurchase, (0.2f + (float)random.NextDouble() * 0.3f) / humanSpeed);
    }

    private void CloseShop(float now)
    {
        bool unlocked = game.IsTicketUnlocked((ScratchGameManager.TicketKind)stage);
        if (!Check(unlocked, "unlock", "Biglietto " + stage + " deve essere sbloccato"))
        {
            Fail("Sblocco del biglietto non riuscito.");
            return;
        }
        flow.ContinueAfterUpgrades();
        ChangePhase(Phase.WaitShopClosed, 0.1f);
    }

    private void AwaitShopClosed(float now)
    {
        if (flow.ShopOpen || !flow.CanShowTicketChoices) return;
        Note("SHOP_CLOSE", "nuovo biglietto=" + (ScratchGameManager.TicketKind)stage);
        ChooseNext(now);
    }

    // Reserve a ticket's price only before the FINAL skill of its tier,
    // not for every intermediate purchase.
}
#endif
