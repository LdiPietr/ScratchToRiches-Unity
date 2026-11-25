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

// Progression responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private void WaitChoice(float now)
    {
        if (flow.CanAcknowledgeResult)
        {
            if (resultVisibleSince < 0f) resultVisibleSince = now;
            if (now - resultVisibleSince >= 2.5f)
            {
                flow.ContinueAfterResult();
                resultVisibleSince = -1f;
                Note("RESULT_CONTINUED", "Risultato visibile per almeno 2.5 secondi");
            }
            return;
        }
        if (!game.WaitingForTicketChoice || !flow.CanShowTicketChoices) return;
        resultVisibleSince = -1f;
        if (farming)
        {
            int needed = FarmingTargetCoins();
            // Radial chapters are gated by tickets actually completed, even
            // when the player already has enough currency for the next skill.
            // Stay in real-ticket farming until BOTH gates are satisfied.
            bool chapterReady = !IsFullDemo || purchaseIndex >= FullDemoPlan.Length ||
                ScratchRadialProgression.Available(
                    ScratchSkillTree.StableId(FullDemoPlan[purchaseIndex]), game, skills);
            if (economy.Coins >= needed && chapterReady)
            {
                bool forTicket = farmingForTicket;
                farming = farmingForTicket = false;
                Note("FARM_DONE", "wallet=" + economy.Coins +
                    ";target=" + needed + ";purpose=" + (forTicket ? "TICKET" : "UPGRADE"));
                if (forTicket)
                    ChooseNext(now); // The ticket is now unlocked AND affordable.
                else
                    ChangePhase(Phase.OpenShop,
                        (0.3f + (float)random.NextDouble() * 0.4f) / humanSpeed);
            }
            else
                ChooseFarmTicket(now);
            return;
        }
        if (IsFullDemo)
        {
            ContinueFullDemo(now);
            return;
        }
        patternIndex++;
        if (patternIndex < TestPatterns[stage].Length)
        {
            ChooseNext(now);
            return;
        }
        if (stage == 2)
        {
            Finish(assertionsFailed == 0,
                autoplayMode == AutoplayMode.SimulazioneReale
                    ? "13 biglietti casuali su tre categorie demo; nessuna combinazione forzata."
                    : "Completati tre tier della demo e i test combinazione forzata.");
            return;
        }
        stage++;
        patternIndex = 0;
        purchaseIndex = 0;
        ChangePhase(Phase.OpenShop, (0.5f + (float)random.NextDouble()) / humanSpeed);
    }

    /// <summary>
    /// Unlike the legacy 13-ticket QA, the full-demo run continuously earns
    /// the money for the next planned upgrade and then uses the authored shop.
    /// It never adds currency, forces outcomes, or grants upgrades directly.
    /// </summary>
    private void ContinueFullDemo(float now)
    {
        UpdateFullDemoTicketTier();
        if (purchaseIndex >= FullDemoPlan.Length)
        {
            if (skills.GetLevel("F04") == 0)
            {
                Fail("Piano terminato senza acquisto del capstone F04.");
                return;
            }
            ChooseNext(now);
            return;
        }

        ScratchSkillTree.SkillId next = FullDemoPlan[purchaseIndex];
        if (!skills.RequirementsMet(next))
        {
            Fail("Prerequisiti non soddisfatti per " + next + ": " + skills.RequirementText(next));
            return;
        }
        string nextId = ScratchSkillTree.StableId(next);
        if (!ScratchRadialProgression.Available(nextId, game, skills))
        {
            // A locked nucleus chapter is a gameplay milestone, never an
            // economic deadlock. Play natural tickets until it opens.
            Note("NUCLEUS_WAIT", "next=" + nextId +
                ";chapter=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";goal=" + ScratchRadialProgression.NextGoal(game, skills));
            ChooseNext(now);
            return;
        }
        int required = FundsNeededForNextPurchase();
        if (economy.Coins < required)
        {
            farming = true;
            farmingForTicket = false;
            Note("FARM_START", "next=" + next + ";required=" + required +
                ";wallet=" + economy.Coins + ";remaining_steps=" +
                (FullDemoPlan.Length - purchaseIndex));
            lastStatus = "Progressione " + (purchaseIndex + 1) + "/" + FullDemoPlan.Length +
                ": risparmio per " + next + " (serve " + required + ")";
            ChooseFarmTicket(now);
            return;
        }

        ChangePhase(Phase.OpenShop, (0.3f + (float)random.NextDouble() * 0.3f) / humanSpeed);
    }

    private void UpdateFullDemoTicketTier()
    {
        if (!IsFullDemo) return;
        stage = game.IsTicketUnlocked(ScratchGameManager.TicketKind.Gold) ? 2 :
            game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato) ? 1 : 0;
    }

    private int CountOwnedDemoSkills()
    {
        int count = 0;
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            if (entry.demoEligible && skills.GetLevel(entry.id) > 0) count++;
        return count;
    }

    private void ProgressEvent(string eventName, string skillId, int level, int price, string details)
    {
        float elapsed = Time.realtimeSinceStartup - startedAt;
        if (eventName == "SKILL_PURCHASED" &&
            (skillId == "C03" || skillId == "C05" || skillId == "G08" || skillId == "F04"))
            milestoneSeconds[skillId] = elapsed;
        progression.Add(elapsed.ToString("F2", CultureInfo.InvariantCulture) + ";" +
            Safe(eventName) + ";" + (game != null ? game.TicketNumber.ToString() : "0") + ";" +
            (game != null ? game.CurrentTicketKind.ToString() : "-") + ";" +
            (economy != null ? economy.Coins.ToString() : "-") + ";" +
            (economy != null ? economy.TotalEarned.ToString() : "-") + ";" +
            Safe(skillId) + ";" + level + ";" + price + ";" + Safe(details));
    }

    /// <summary>Validate all 28 purchase steps, 25 unique demo effects and all edges.</summary>
    // Kept for programmatic QA; removed redundant standalone menu entry.
    private static void TestFullDemoPlan()
    {
        if (ValidateFullDemoPlan(out string error))
            Debug.Log("STR FULL DEMO QA: 28 acquisti, 25 skill demo, prerequisiti PASS.");
        else
            Debug.LogError("STR FULL DEMO QA: piano FAIL - " + error);
    }

    private static bool ValidateFullDemoPlan(out string error)
    {
        var owned = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ScratchSkillTree.SkillId id in FullDemoPlan)
        {
            string stableId = ScratchSkillTree.StableId(id);
            ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
            if (entry == null || !entry.demoEligible ||
                !ScratchSkillCatalogV2.EffectImplemented(stableId))
            {
                error = "Non acquistabile in demo: " + stableId;
                return false;
            }
            foreach (string raw in (entry.prerequisites ?? "").Split(';'))
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string[] parts = raw.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int required) ||
                    !owned.TryGetValue(parts[0], out int current) || current < required)
                {
                    error = "Prerequisito fuori ordine: " + stableId + " <- " + raw;
                    return false;
                }
            }
            int next = owned.TryGetValue(stableId, out int level) ? level + 1 : 1;
            if (next > entry.maxLevel)
            {
                error = "Superato livello massimo: " + stableId;
                return false;
            }
            owned[stableId] = next;
        }
        var route = new List<ScratchSkillTree.SkillId>(FullDemoPlan);
        if (FullDemoPlan.Length != 28 || owned.Count != 25 ||
            !owned.TryGetValue("C11", out int extraLevels) || extraLevels != 2 ||
            FullDemoPlan[FullDemoPlan.Length - 1] != ScratchSkillTree.SkillId.F4 ||
            !owned.ContainsKey("G08") || !owned.ContainsKey("C05") ||
            !(route.IndexOf(ScratchSkillTree.SkillId.C2) <
              route.IndexOf(ScratchSkillTree.SkillId.G7) &&
              route.IndexOf(ScratchSkillTree.SkillId.G7) <
              route.IndexOf(ScratchSkillTree.SkillId.C4) &&
              route.IndexOf(ScratchSkillTree.SkillId.C5) <
              route.IndexOf(ScratchSkillTree.SkillId.G8) &&
              route.IndexOf(ScratchSkillTree.SkillId.G8) <
              route.IndexOf(ScratchSkillTree.SkillId.F4)))
        {
            error = "Il piano non copre le 25 skill o manca il finale.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}
#endif
