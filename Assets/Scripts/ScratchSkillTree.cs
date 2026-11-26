using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M5: four independent skill branches with explicit prerequisites.
/// Implemented skills affect gameplay; planned skills cannot be purchased.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchEconomyManager))]
public sealed partial class ScratchSkillTree : MonoBehaviour
{
    public const string SavePrefix = "STR_V2_Skill_";
    private readonly int[] levels = new int[32];
    private Dictionary<string, int> stableLevels;
    private const string StableSavePrefix = "STR_GDDV2_Skill_";
    private const string MigratedKey = "STR_GDDV2_Migrated_01";
    private const string TitanChargesKey = "STR_GDDV2_TitanCharges";
    private const string TitanProgressKey = "STR_GDDV2_TitanProgress";
    private const string TitanUsedKey = "STR_GDDV2_TitanUsed";
    private const string JollyBonusKey = "STR_GDDV2_JollyGoldBonus";
    private const string JollyUsedKey = "STR_GDDV2_JollyUsed";
    private int jollyGoldGuarantees;
    private int jollyUsedCount;
    private int titanCharges;
    private int titanChargeProgress;
    private int titanUsedCount;
    public const bool DemoEdition = true;
    private ScratchEconomyManager economy;
    private ScratchGameManager game;

    public event Action Changed;
    public static IReadOnlyList<Definition> All => definitions;

    private void Awake()
    {
        economy = GetComponent<ScratchEconomyManager>();
        game = GetComponent<ScratchGameManager>();
        for (int i = 0; i < levels.Length; i++)
            levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(SavePrefix + i, 0), 0, definitions[i].MaxLevel);
        EnsureStableLevels();
    }

    public static string StableId(SkillId legacyId)
    {
        string name = legacyId.ToString();
        if (name.Length < 2) return string.Empty;
        return name.Substring(0, 1) + int.Parse(name.Substring(1)).ToString("D2");
    }

    private void EnsureStableLevels()
    {
        if (stableLevels != null) return;
        stableLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        titanCharges = Mathf.Clamp(PlayerPrefs.GetInt(TitanChargesKey, 0), 0, 3);
        titanChargeProgress = Mathf.Clamp(PlayerPrefs.GetInt(TitanProgressKey, 0), 0, 4);
        titanUsedCount = Mathf.Max(0, PlayerPrefs.GetInt(TitanUsedKey, 0));
        jollyGoldGuarantees = Mathf.Clamp(PlayerPrefs.GetInt(JollyBonusKey, 0), 0, 8);
        jollyUsedCount = Mathf.Max(0, PlayerPrefs.GetInt(JollyUsedKey, 0));
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            stableLevels.Add(entry.id, Mathf.Clamp(
                PlayerPrefs.GetInt(StableSavePrefix + entry.id, 0), 0, entry.maxLevel));

        // One-time import. Existing V2 values take precedence, and legacy
        // unimplemented skills (notably old G6/G7/G8) are never imported.
        if (PlayerPrefs.GetInt(MigratedKey, 0) == 0)
        {
            string[] oldImplemented =
            {
                "G01", "G02", "G03", "G04", "G05",
                "C01", "C02", "C03", "C04", "C05", "C06", "C07",
                "E01", "E02", "E03", "E04", "E05"
            };
            foreach (string id in oldImplemented)
            {
                ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(id);
                int legacy = ((int)(id[0] == 'G' ? 0 : id[0] == 'C' ? 1 :
                    id[0] == 'E' ? 2 : 3) * 8) + int.Parse(id.Substring(1)) - 1;
                int value = Mathf.Clamp(PlayerPrefs.GetInt(SavePrefix + legacy, 0),
                    0, entry.maxLevel);
                if (stableLevels[id] == 0 && value > 0)
                {
                    stableLevels[id] = value;
                    PlayerPrefs.SetInt(StableSavePrefix + id, value);
                }
            }
            PlayerPrefs.SetInt(MigratedKey, 1);
            PlayerPrefs.Save();
        }
    }

    public IReadOnlyList<ScratchSkillCatalogV2.Entry> Catalog => ScratchSkillCatalogV2.All;

    public int GetLevel(SkillId id) => GetLevel(StableId(id));

    public int GetLevel(string stableId)
    {
        EnsureStableLevels();
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        // A saved former full-game unlock cannot bypass the demo's edition lock.
        if (entry == null || (DemoEdition && !entry.demoEligible)) return 0;
        return stableLevels.TryGetValue(stableId, out int value) ? value : 0;
    }

    public int GetPrice(SkillId id) => GetPrice(StableId(id));

    public int GetPrice(string stableId)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        return entry == null ? 0 : ScratchPurchasePolicy.Price(
            entry.initialPrice, GetLevel(stableId), entry.maxLevel);
    }

    public bool RequirementsMet(SkillId id) => RequirementsMet(StableId(id));

    public bool RequirementsMet(string stableId)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        if (entry == null) return false;
        foreach (string raw in (entry.prerequisites ?? "").Split(';'))
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string[] parts = raw.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int required) ||
                GetLevel(parts[0]) < required)
                return false;
        }
        return true;
    }

    public string RequirementText(SkillId id) => RequirementText(StableId(id));

    public string RequirementText(string stableId)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        if (entry == null) return "ABILITA' NON TROVATA";
        foreach (string raw in (entry.prerequisites ?? "").Split(';'))
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string[] parts = raw.Split(':');
            if (GetLevel(parts[0]) < int.Parse(parts[1]))
            {
                ScratchSkillCatalogV2.Entry previous = ScratchSkillCatalogV2.Find(parts[0]);
                return "Prima " + previous.title + " livello " + parts[1];
            }
        }
        return string.Empty;
    }

    // Count individually affordable next levels, not a simulated shopping cart.
    public int PurchasableCount
    {
        get
        {
            if (game == null || game.TicketActive || game.DemoFinished) return 0;
            int count = 0;
            foreach (var entry in Catalog)
                if (CanPurchase(entry.id)) count++;
            return count;
        }
    }

    public bool CanPurchase(SkillId id) => CanPurchase(StableId(id));

    public bool CanPurchase(string stableId) =>
        GetPurchaseStatus(stableId) == ScratchPurchaseStatus.Available;

    /// <summary>Read-only decision shared by purchases and explanatory UI.</summary>
    public ScratchPurchaseStatus GetPurchaseStatus(string stableId)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        if (entry == null) return ScratchPurchaseStatus.Missing;
        return ScratchPurchasePolicy.Evaluate(!DemoEdition || entry.demoEligible,
            ScratchSkillCatalogV2.EffectImplemented(stableId), GetLevel(stableId),
            entry.maxLevel, game != null && economy != null,
            () => ScratchRadialProgression.Available(stableId, game, this),
            () => RequirementsMet(stableId),
            () => economy.Coins >= GetPrice(stableId));
    }

    public bool TryPurchase(SkillId id) => TryPurchase(StableId(id));

    public bool TryPurchase(string stableId)
    {
        // A live ticket must keep its original scratch limit and economy rules.
        if (game == null || game.TicketActive || !CanPurchase(stableId))
            return false;
        int paidPrice = GetPrice(stableId);
        if (!economy.TrySpendCoins(paidPrice))
            return false;
        EnsureStableLevels();
        stableLevels[stableId]++;
        PlayerPrefs.SetInt(StableSavePrefix + stableId, stableLevels[stableId]);
        if (stableId == "G08" && stableLevels[stableId] == 1)
        {
            // V2 capstone: one charge is available IMMEDIATELY on purchase.
            titanCharges = Mathf.Max(1, titanCharges);
            titanChargeProgress = 0;
            PlayerPrefs.SetInt(TitanChargesKey, titanCharges);
            PlayerPrefs.SetInt(TitanProgressKey, titanChargeProgress);
        }
        if (stableId == "F04" && stableLevels[stableId] == 1)
        {
            // The eight-ticket finale lets the player enjoy the new Joker
            // without extending its duration by artificial idle time.
            jollyGoldGuarantees = 8;
            PlayerPrefs.SetInt(JollyBonusKey, jollyGoldGuarantees);
        }
        // Keep legacy indices mirrored while gameplay and QA transition to IDs.
        if (stableId.Length == 3 && stableId[0] != 'A' &&
            int.TryParse(stableId.Substring(1), out int number) && number <= 8)
        {
            int group = stableId[0] == 'G' ? 0 : stableId[0] == 'C' ? 1 :
                stableId[0] == 'E' ? 2 : 3;
            int index = group * 8 + number - 1;
            if (index >= 0 && index < levels.Length)
            {
                levels[index] = stableLevels[stableId];
                PlayerPrefs.SetInt(SavePrefix + index, levels[index]);
            }
        }
        PlayerPrefs.Save();
        if ((stableId == "G08" || stableId == "F04") && game != null)
            game.OnDemoCapstonePurchased();
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("SKILL_PURCHASED", "id=" + stableId +
            "|level=" + stableLevels[stableId] + "|price=" + paidPrice +
            "|wallet=" + economy.Coins);
#endif
        Changed?.Invoke();
        return true;
    }
}
