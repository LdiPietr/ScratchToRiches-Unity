#if UNITY_EDITOR
using PlayerPrefs = ScratchGameFeelPrefs;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

// Reset responsibility of ScratchSkillTree.
public sealed partial class ScratchSkillTree
{
    [ContextMenu("RESET ALL M5 PROGRESSION (Play Mode)")]
    public void ResetProgress()
    {
        if (!Application.isPlaying || (game != null && game.TicketActive))
        {
            Debug.LogWarning("Reset progression requires Play Mode outside an active ticket.", this);
            return;
        }
        EnsureStableLevels();
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
        {
            stableLevels[entry.id] = 0;
            PlayerPrefs.DeleteKey(StableSavePrefix + entry.id);
        }
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = 0;
            PlayerPrefs.DeleteKey(SavePrefix + i);
        }
        PlayerPrefs.SetInt(MigratedKey, 1);
        titanCharges = titanChargeProgress = titanUsedCount = 0;
        jollyGoldGuarantees = jollyUsedCount = 0;
        PlayerPrefs.DeleteKey(JollyBonusKey);
        PlayerPrefs.DeleteKey(JollyUsedKey);
        PlayerPrefs.DeleteKey(TitanChargesKey);
        PlayerPrefs.DeleteKey(TitanProgressKey);
        PlayerPrefs.DeleteKey(TitanUsedKey);
        economy?.ResetWallet();
        game?.ResetDemoMilestone();
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
