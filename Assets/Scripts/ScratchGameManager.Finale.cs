#if UNITY_EDITOR
using PlayerPrefs = ScratchGameFeelPrefs;
#endif
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Finale responsibility of ScratchGameManager.
public sealed partial class ScratchGameManager
{
    public void OnDemoCapstonePurchased()
    {
        // The finale begins only AFTER both late-game powers are owned.
        // Unlocking G08 early must not finish the demo before the player
        // experiences F04; the final Gold performance starts at this moment.
        if (!ScratchSkillTree.DemoEdition || demoCapstoneActive || demoFinished ||
            skillTree == null || skillTree.GetLevel("G08") == 0 ||
            skillTree.GetLevel("F04") == 0)
            return;
        demoCapstoneActive = true;
        demoGoldCompleted = demoGoldPayout = 0;
        demoExcludedTicketId = ticketActive ? currentTicketId : -1;
        titanActivationBaseline = skillTree.TitanActivationCount;
        jollyActivationBaseline = skillTree.JollyActivationCount;
        // F04 may have been bought first; ensure the finale is not left
        // depending on a 10% random joker or on keyboard-only SHIFT input.
        skillTree.RechargeFinalJollyGuarantees();
        PlayerPrefs.SetInt(DemoActiveKey, 1);
        PlayerPrefs.SetInt(DemoGoldKey, 0);
        PlayerPrefs.SetInt(DemoGoldPayoutKey, 0);
        PlayerPrefs.SetInt(DemoTitanBaseKey, titanActivationBaseline);
        PlayerPrefs.SetInt(DemoJollyBaseKey, jollyActivationBaseline);
        PlayerPrefs.SetInt(DemoRuleVersionKey, DemoRuleVersion);
        PlayerPrefs.Save();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("FINAL_CHALLENGE_STARTED", "gold_target=" +
            FinalGoldMinTickets + "|coins_target=" + FinalGoldPayoutTarget);
#endif
        RefreshHud();
    }

    public void ResetDemoMilestone()
    {
        demoCapstoneActive = demoFinished = false;
        demoGoldCompleted = demoGoldPayout = titanActivationBaseline = jollyActivationBaseline = 0;
        demoExcludedTicketId = -1;
        targetedPreviewUsed = false;
        for (int tier = 0; tier < completedByTier.Length; tier++)
        {
            completedByTier[tier] = 0;
            PlayerPrefs.DeleteKey(RadialTicketPrefix + tier);
        }
        foreach (string key in new[] { DemoActiveKey, DemoGoldKey,
            DemoGoldPayoutKey, DemoTitanBaseKey, DemoJollyBaseKey, DemoDoneKey })
            PlayerPrefs.DeleteKey(key);
        if (state == RoundState.Finished) state = RoundState.Ready;
        if (demoEndOverlay != null) demoEndOverlay.SetActive(false);
        PlayerPrefs.SetInt(DemoRuleVersionKey, DemoRuleVersion);
        PlayerPrefs.Save();
        RefreshHud();
    }
}
