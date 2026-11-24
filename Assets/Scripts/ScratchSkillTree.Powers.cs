using System;
using System.Collections.Generic;
using UnityEngine;

// Powers responsibility of ScratchSkillTree.
public sealed partial class ScratchSkillTree
{
    public int JollyGoldGuaranteesRemaining =>
        GetLevel("F04") > 0 ? jollyGoldGuarantees : 0;
    public int JollyActivationCount => jollyUsedCount;

    /// <summary>
    /// Begin the genuine final challenge with eight guaranteed Golden jokers,
    /// even if F04 was bought well before G08 and its initial uses ran out.
    /// Called only once by the authored demo milestone.
    /// </summary>
    public void RechargeFinalJollyGuarantees()
    {
        if (GetLevel("F04") == 0) return;
        jollyGoldGuarantees = Math.Max(jollyGoldGuarantees, 8);
        PlayerPrefs.SetInt(JollyBonusKey, jollyGoldGuarantees);
        PlayerPrefs.Save();
        // The enclosing skill purchase and GameManager already refresh UI.
        // Avoid a nested shop refresh while a hold-to-buy is still completing.
    }

    public void OnGoldenJollyTicketPurchased()
    {
        if (GetLevel("F04") <= 0 || jollyGoldGuarantees <= 0) return;
        jollyGoldGuarantees--;
        PlayerPrefs.SetInt(JollyBonusKey, jollyGoldGuarantees);
        PlayerPrefs.Save();
    }

    public void OnJollyScratched()
    {
        if (GetLevel("F04") <= 0) return;
        jollyUsedCount++;
        PlayerPrefs.SetInt(JollyUsedKey, jollyUsedCount);
        PlayerPrefs.Save();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("JOLLY_SCRATCHED", "total=" + jollyUsedCount);
#endif
        Changed?.Invoke();
    }

    public int TitanCharges => GetLevel("G08") > 0 ? titanCharges : 0;
    public int TitanActivationCount => titanUsedCount;

    /// <summary>Manual input only; pressing SHIFT while scratching consumes one charge.</summary>
    public bool TryConsumeManualTitanCharge()
    {
        if (GetLevel("G08") == 0 || titanCharges <= 0) return false;
        titanCharges--;
        titanUsedCount++;
        PlayerPrefs.SetInt(TitanChargesKey, titanCharges);
        PlayerPrefs.SetInt(TitanUsedKey, titanUsedCount);
        PlayerPrefs.Save();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("TITAN_USED", "total=" + titanUsedCount +
            "|charges_left=" + titanCharges);
#endif
        Changed?.Invoke();
        return true;
    }

    public void OnCompletedScratchCell()
    {
        if (GetLevel("G08") == 0) return;
        titanChargeProgress++;
        if (titanChargeProgress >= 5)
        {
            titanChargeProgress = 0;
            titanCharges = Mathf.Min(3, titanCharges + 1);
        }
        PlayerPrefs.SetInt(TitanProgressKey, titanChargeProgress);
        PlayerPrefs.SetInt(TitanChargesKey, titanCharges);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
