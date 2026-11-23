using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M5: four independent skill branches with explicit prerequisites.
/// Implemented skills affect gameplay; planned skills cannot be purchased.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchEconomyManager))]
public sealed class ScratchSkillTree : MonoBehaviour
{
    public enum Branch { Grattamento, Caselle, Economia, Fortuna }
    public enum SkillId
    {
        G1, G2, G3, G4, G5, G6, G7, G8,
        C1, C2, C3, C4, C5, C6, C7, C8,
        E1, E2, E3, E4, E5, E6, E7, E8,
        F1, F2, F3, F4, F5, F6, F7, F8
    }

    public readonly struct Requirement
    {
        public readonly SkillId Id;
        public readonly int Level;
        public Requirement(SkillId id, int level = 1) { Id = id; Level = level; }
    }

    public sealed class Definition
    {
        public readonly SkillId Id;
        public readonly string Title;
        public readonly string Description;
        public readonly int MaxLevel;
        public readonly int InitialPrice;
        public readonly bool Implemented;
        public readonly Requirement[] Requirements;
        public Branch Branch => (Branch)((int)Id / 8);

        public Definition(SkillId id, string title, string description,
            int maxLevel, int initialPrice, bool implemented, params Requirement[] requirements)
        {
            Id = id;
            Title = title;
            Description = description;
            MaxLevel = maxLevel;
            InitialPrice = initialPrice;
            Implemented = implemented;
            Requirements = requirements ?? Array.Empty<Requirement>();
        }
    }

    private static readonly Definition[] definitions =
    {
        // Grattamento — manual efficiency first, automation later.
        new Definition(SkillId.G1, "Pennello XXL", "Una grattata più larga! +2 pixel per livello.", 5, 12, true),
        new Definition(SkillId.G2, "Partenza sprint", "Il primo tocco gratta 4 pixel in più per livello.", 3, 35, true, new Requirement(SkillId.G1, 2)),
        new Definition(SkillId.G3, "Grattata doppia", "Una passata in più per ogni livello sbloccato!", 3, 80, true, new Requirement(SkillId.G2)),
        new Definition(SkillId.G4, "Super pennello", "Fino a 2 strisce extra in un solo movimento!", 2, 175, true, new Requirement(SkillId.G3, 2)),
        new Definition(SkillId.G5, "Primo colpo", "Sulla prima casella scelta, +3 pixel per livello al pennello.", 3, 300, true, new Requirement(SkillId.G4)),
        new Definition(SkillId.G6, "Grattino aiutante", "Un piccolo aiutante gratta una casella per te!", 3, 750, false, new Requirement(SkillId.G5, 2)),
        new Definition(SkillId.G7, "Pilota automatico", "Le caselle che scegli si grattano da sole!", 3, 1600, false, new Requirement(SkillId.G6, 2)),
        new Definition(SkillId.G8, "Tempesta d'argento", "Una valanga di grattate su più caselle!", 1, 5000, false, new Requirement(SkillId.G7, 3)),

        // Caselle — previews plus extra picks (no changes to 4x4 board size).
        new Definition(SkillId.C1, "Occhiolino fortunato", "Anteprima di un simbolo nascosto.", 1, 25, true),
        new Definition(SkillId.C2, "Doppio occhiolino", "Anteprima di due simboli nascosti.", 1, 65, true, new Requirement(SkillId.C3)),
        new Definition(SkillId.C3, "Sblocca Fortunato", "Sblocca il biglietto 4x4, quattro scelte e il poker.", 1, 140, true, new Requirement(SkillId.C1)),
        new Definition(SkillId.C4, "Occhio allenato", "Anteprima di tre simboli nascosti.", 1, 240, true, new Requirement(SkillId.C2), new Requirement(SkillId.G7)),
        new Definition(SkillId.C5, "Sblocca Dorato", "Sblocca il biglietto 5x5, cinque scelte e la cinquina.", 1, 850, true, new Requirement(SkillId.C4)),
        new Definition(SkillId.C6, "Occhio di falco", "Scegli una casella da ispezionare con il clic destro, una volta per biglietto.", 1, 1800, true, new Requirement(SkillId.C5)),
        new Definition(SkillId.C7, "Sblocca Diamante", "Super biglietto 5x5 con soli quattro simboli differenti per volta.", 1, 5000, true, new Requirement(SkillId.C6), new Requirement(SkillId.E4)),
        new Definition(SkillId.C8, "Tocco magico", "In futuro: cambia un simbolo nascosto.", 1, 8500, false, new Requirement(SkillId.C7)),

        // Economia — existing M4 payout becomes upgrade-aware.
        new Definition(SkillId.E1, "Pioggia di monete", "+2 monete al premio base per ogni livello.", 5, 15, true),
        new Definition(SkillId.E2, "Doppietta d'oro", "Le coppie pagano il 10% in più per livello!", 3, 45, true, new Requirement(SkillId.E1, 2)),
        new Definition(SkillId.E3, "Tris fortunato", "I tris pagano il 15% in più per livello!", 3, 110, true, new Requirement(SkillId.E2)),
        new Definition(SkillId.E4, "Poker da sogno", "I poker pagano il 20% in più per livello!", 3, 300, true, new Requirement(SkillId.E3)),
        new Definition(SkillId.E5, "Premio di consolazione", "Finisci senza vincere? +3 monete per livello!", 3, 140, true, new Requirement(SkillId.E1, 3)),
        new Definition(SkillId.E6, "Scia vincente", "Più biglietti vincenti di fila, più guadagni!", 5, 650, false, new Requirement(SkillId.E4)),
        new Definition(SkillId.E7, "Super moltiplicatore", "Le tue vincite diventano ancora più ricche!", 3, 2000, false, new Requirement(SkillId.E6, 3)),
        new Definition(SkillId.E8, "Cascata di monete", "Le vincite consecutive fanno crescere il bottino!", 1, 5500, false, new Requirement(SkillId.E7, 3)),

        // Fortuna is visible in the planned progression, not purchasable yet.
        new Definition(SkillId.F1, "Istinto fortunato", "Futuro: migliora il valore medio dei biglietti.", 1, 900, false),
        new Definition(SkillId.F2, "Simboli affini", "Futuro: aumenta la probabilita' di una coppia.", 1, 1400, false, new Requirement(SkillId.F1)),
        new Definition(SkillId.F3, "Tris favorito", "Futuro: distribuzioni favorevoli al tris.", 1, 2500, false, new Requirement(SkillId.F2)),
        new Definition(SkillId.F4, "Poker scelto", "Futuro: distribuzioni favorevoli al poker.", 1, 4000, false, new Requirement(SkillId.F3)),
        new Definition(SkillId.F5, "Cinquina d'oro", "Futuro: distribuzioni favorevoli alla cinquina.", 1, 7000, false, new Requirement(SkillId.F4)),
        new Definition(SkillId.F6, "Riscrivi il destino", "Futuro: migliora le probabilita' del biglietto.", 1, 12000, false, new Requirement(SkillId.F5)),
        new Definition(SkillId.F7, "Jackpot imminente", "Futuro: bonus per vincite speciali.", 1, 20000, false, new Requirement(SkillId.F6)),
        new Definition(SkillId.F8, "Fortuna assoluta", "Futuro game changer di endgame.", 1, 35000, false, new Requirement(SkillId.F7))
    };

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
        jollyGoldGuarantees = Mathf.Clamp(PlayerPrefs.GetInt(JollyBonusKey, 0), 0, 3);
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
        if (entry == null || GetLevel(stableId) >= entry.maxLevel) return 0;
        double price = entry.initialPrice * Math.Pow(1.5, GetLevel(stableId));
        return price >= int.MaxValue ? int.MaxValue :
            Math.Max(1, (int)Math.Ceiling(price));
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

    public bool CanPurchase(SkillId id) => CanPurchase(StableId(id));

    public bool CanPurchase(string stableId)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
        return entry != null && (!DemoEdition || entry.demoEligible) &&
            ScratchSkillCatalogV2.EffectImplemented(stableId) &&
            GetLevel(stableId) < entry.maxLevel &&
            RequirementsMet(stableId) &&
            game != null && economy != null &&
            economy.Coins >= GetPrice(stableId);
    }

    public bool TryPurchase(SkillId id) => TryPurchase(StableId(id));

    public bool TryPurchase(string stableId)
    {
        if (!CanPurchase(stableId) || !economy.TrySpendCoins(GetPrice(stableId)))
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
            // Three authentic Golden tickets with a guaranteed visible joker.
            jollyGoldGuarantees = 3;
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
        Changed?.Invoke();
        return true;
    }

    public int JollyGoldGuaranteesRemaining =>
        GetLevel("F04") > 0 ? jollyGoldGuarantees : 0;
    public int JollyActivationCount => jollyUsedCount;

    /// <summary>
    /// Begin the genuine final challenge with three guaranteed Golden jokers,
    /// even if F04 was bought well before G08 and its initial uses ran out.
    /// Called only once by the authored demo milestone.
    /// </summary>
    public void RechargeFinalJollyGuarantees()
    {
        if (GetLevel("F04") == 0) return;
        jollyGoldGuarantees = Math.Max(jollyGoldGuarantees, 3);
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
