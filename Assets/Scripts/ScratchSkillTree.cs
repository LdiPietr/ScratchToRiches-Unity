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
    public enum Branch { Grattamento, Caselle, Economia, Tempo }
    public enum SkillId
    {
        G1, G2, G3, G4, G5, G6, G7, G8,
        C1, C2, C3, C4, C5, C6, C7, C8,
        E1, E2, E3, E4, E5, E6, E7, E8,
        T1, T2, T3, T4, T5, T6, T7, T8
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
        new Definition(SkillId.G5, "Scatto turbo", "Nei primi 3 secondi il pennello cresce di 3 pixel per livello.", 3, 300, true, new Requirement(SkillId.G4)),
        new Definition(SkillId.G6, "Grattino aiutante", "Un piccolo aiutante gratta una casella per te!", 3, 750, false, new Requirement(SkillId.G5, 2)),
        new Definition(SkillId.G7, "Pilota automatico", "Le caselle che scegli si grattano da sole!", 3, 1600, false, new Requirement(SkillId.G6, 2)),
        new Definition(SkillId.G8, "Tempesta d'argento", "Una valanga di grattate su più caselle!", 1, 5000, false, new Requirement(SkillId.G7, 3)),

        // Caselle — previews plus extra picks (no changes to 4x4 board size).
        new Definition(SkillId.C1, "Occhiolino fortunato", "Una casella lascia intravedere il simbolo nascosto!", 1, 25, true),
        new Definition(SkillId.C2, "Doppio occhiolino", "Due caselle svelano un pochino il loro segreto!", 1, 65, true, new Requirement(SkillId.C1)),
        new Definition(SkillId.C3, "Quarta chance", "Ora puoi grattare 4 caselle per biglietto!", 1, 140, true, new Requirement(SkillId.C2)),
        new Definition(SkillId.C4, "Cambio idea!", "Puoi cambiare una casella che non hai ancora grattato.", 1, 240, false, new Requirement(SkillId.C3)),
        new Definition(SkillId.C5, "Vista da falco", "Intravedrai prima 3, poi 4 simboli nascosti!", 2, 320, true, new Requirement(SkillId.C3)),
        new Definition(SkillId.C6, "Quinta chance", "Ora puoi grattare ben 5 caselle per biglietto!", 1, 850, true, new Requirement(SkillId.C5, 2)),
        new Definition(SkillId.C7, "Tutto in vista!", "Tutte le 16 caselle diventano semitrasparenti!", 1, 1800, true, new Requirement(SkillId.C6)),
        new Definition(SkillId.C8, "Tocco magico", "Cambia un simbolo nascosto per tentare il colpaccio!", 1, 5000, false, new Requirement(SkillId.C7)),

        // Economia — existing M4 payout becomes upgrade-aware.
        new Definition(SkillId.E1, "Pioggia di monete", "+2 monete al premio base per ogni livello.", 5, 15, true),
        new Definition(SkillId.E2, "Doppietta d'oro", "Le coppie pagano il 10% in più per livello!", 3, 45, true, new Requirement(SkillId.E1, 2)),
        new Definition(SkillId.E3, "Tris fortunato", "I tris pagano il 15% in più per livello!", 3, 110, true, new Requirement(SkillId.E2)),
        new Definition(SkillId.E4, "Poker da sogno", "I poker pagano il 20% in più per livello!", 3, 300, true, new Requirement(SkillId.E3)),
        new Definition(SkillId.E5, "Premio di consolazione", "Finisci senza vincere? +3 monete per livello!", 3, 140, true, new Requirement(SkillId.E1, 3)),
        new Definition(SkillId.E6, "Scia vincente", "Più biglietti vincenti di fila, più guadagni!", 5, 650, false, new Requirement(SkillId.E4)),
        new Definition(SkillId.E7, "Super moltiplicatore", "Le tue vincite diventano ancora più ricche!", 3, 2000, false, new Requirement(SkillId.E6, 3)),
        new Definition(SkillId.E8, "Cascata di monete", "Le vincite consecutive fanno crescere il bottino!", 1, 5500, false, new Requirement(SkillId.E7, 3)),

        // Tempo — one global timer; upgrades add time only on real goals.
        new Definition(SkillId.T1, "Mezzo secondo d'oro", "Primo biglietto completo: +0,5 secondi!", 1, 30, true),
        new Definition(SkillId.T2, "Terzo colpo!", "Completa 3 biglietti e guadagni +1 secondo!", 1, 100, true, new Requirement(SkillId.T1)),
        new Definition(SkillId.T3, "Ritmo da campione", "Ogni 4 biglietti completi: +1 secondo per livello!", 2, 235, true, new Requirement(SkillId.T2)),
        new Definition(SkillId.T4, "Un secondo in tasca", "Completa 4 caselle e recuperi 1 secondo!", 1, 500, true, new Requirement(SkillId.T3), new Requirement(SkillId.C3)),
        new Definition(SkillId.T5, "Ancora un po'!", "Completa 4 caselle e recuperi 2 secondi totali!", 1, 950, true, new Requirement(SkillId.T4)),
        new Definition(SkillId.T6, "Tempo prezioso", "Completa 4 caselle e recuperi 3 secondi totali!", 1, 1800, true, new Requirement(SkillId.T5)),
        new Definition(SkillId.T7, "Quattro secondi!", "Completa 4 caselle e recuperi 4 secondi totali!", 1, 3500, true, new Requirement(SkillId.T6)),
        new Definition(SkillId.T8, "Cinque secondi d'oro", "Completa 5 caselle e recuperi 5 secondi totali!", 1, 7000, true, new Requirement(SkillId.T7), new Requirement(SkillId.C6))
    };

    public const string SavePrefix = "STR_M5_Skill_";
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
               game != null && game.State == ScratchGameManager.RoundState.Finished &&
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
        if (!Application.isPlaying || (game != null && game.State == ScratchGameManager.RoundState.Playing))
        {
            Debug.LogWarning("Reset progression requires Play Mode outside a running game.", this);
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
