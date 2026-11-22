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
        new Definition(SkillId.C2, "Doppio occhiolino", "Anteprima di due simboli nascosti.", 1, 65, true, new Requirement(SkillId.C1)),
        new Definition(SkillId.C3, "Sblocca Fortunato", "Sblocca il biglietto 4x4, quattro scelte e il poker.", 1, 140, true, new Requirement(SkillId.C2))
        new Definition(SkillId.C4, "Occhio allenato", "Anteprima di tre simboli nascosti.", 1, 240, true, new Requirement(SkillId.C3))
        new Definition(SkillId.C5, "Sblocca Dorato", "Sblocca il biglietto 5x5, cinque scelte e la cinquina.", 1, 850, true, new Requirement(SkillId.C4))
        new Definition(SkillId.C6, "Occhio di falco", "Anteprima di cinque simboli nascosti.", 1, 1800, true, new Requirement(SkillId.C5))
        new Definition(SkillId.C7, "Sblocca Diamante", "Super biglietto 5x5 con soli quattro simboli differenti per volta.", 1, 5000, true, new Requirement(SkillId.C6), new Requirement(SkillId.E4))
        new Definition(SkillId.C8, "Tocco magico", "In futuro: cambia un simbolo nascosto.", 1, 8500, false, new Requirement(SkillId.C7))

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
    }

    public int GetLevel(SkillId id) => levels[(int)id];

    public int GetPrice(SkillId id)
    {
        Definition definition = definitions[(int)id];
        int level = GetLevel(id);
        if (level >= definition.MaxLevel)
            return 0;
        double value = definition.InitialPrice * Math.Pow(1.8, level);
        return value >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(value));
    }

    public bool RequirementsMet(SkillId id)
    {
        foreach (Requirement requirement in definitions[(int)id].Requirements)
            if (GetLevel(requirement.Id) < requirement.Level)
                return false;
        return true;
    }

    public string RequirementText(SkillId id)
    {
        foreach (Requirement requirement in definitions[(int)id].Requirements)
        {
            if (GetLevel(requirement.Id) >= requirement.Level)
                continue;
            return "Prima migliora " + definitions[(int)requirement.Id].Title + " al livello " + requirement.Level + "!";
        }
        return string.Empty;
    }

    public bool CanPurchase(SkillId id)
    {
        Definition definition = definitions[(int)id];
        return definition.Implemented &&
               GetLevel(id) < definition.MaxLevel &&
               RequirementsMet(id) &&
               game != null &&
               economy != null && economy.Coins >= GetPrice(id);
    }

    public bool TryPurchase(SkillId id)
    {
        if (!CanPurchase(id) || !economy.TrySpendCoins(GetPrice(id)))
            return false;
        int index = (int)id;
        levels[index]++;
        PlayerPrefs.SetInt(SavePrefix + index, levels[index]);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    [ContextMenu("RESET ALL M5 PROGRESSION (Play Mode)")]
    public void ResetProgress()
    {
        if (!Application.isPlaying || (game != null && game.TicketActive))
        {
            Debug.LogWarning("Reset progression requires Play Mode outside an active ticket.", this);
            return;
        }
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = 0;
            PlayerPrefs.DeleteKey(SavePrefix + i);
        }
        economy?.ResetWallet();
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
