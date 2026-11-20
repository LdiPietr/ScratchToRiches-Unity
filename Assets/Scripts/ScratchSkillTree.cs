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
        new Definition(SkillId.G1, "Pennello largo", "+2 px di raggio per livello", 5, 12, true),
        new Definition(SkillId.G2, "Primo colpo", "Primo tocco +4 px per livello", 3, 35, true, new Requirement(SkillId.G1, 2)),
        new Definition(SkillId.G3, "Doppia passata", "Traccia parallela aggiuntiva", 3, 80, true, new Requirement(SkillId.G2)),
        new Definition(SkillId.G4, "Pennello multiplo", "Fino a 2 tracce laterali extra", 2, 175, true, new Requirement(SkillId.G3, 2)),
        new Definition(SkillId.G5, "Turbo", "Raggio +3 px/livello nei primi 3s del biglietto", 3, 300, true, new Requirement(SkillId.G4)),
        new Definition(SkillId.G6, "Assistente", "Grattamento automatico di una casella", 3, 750, false, new Requirement(SkillId.G5, 2)),
        new Definition(SkillId.G7, "Automatico", "Grattamento delle caselle selezionate", 3, 1600, false, new Requirement(SkillId.G6, 2)),
        new Definition(SkillId.G8, "Scratch Storm", "Automazione parallela totale", 1, 5000, false, new Requirement(SkillId.G7, 3)),

        // Caselle — previews plus extra picks (no changes to 4x4 board size).
        new Definition(SkillId.C1, "Occhio fortunato", "Anteprima del simbolo di 1 casella", 1, 25, true),
        new Definition(SkillId.C2, "Doppia anteprima", "Anteprima di 2 caselle totali", 1, 65, true, new Requirement(SkillId.C1)),
        new Definition(SkillId.C3, "Quarta casella", "+1 scelta per biglietto", 1, 140, true, new Requirement(SkillId.C2)),
        new Definition(SkillId.C4, "Ripensamento", "Sostituisci una scelta non completata", 1, 240, false, new Requirement(SkillId.C3)),
        new Definition(SkillId.C5, "Scanner", "Da 3 a 4 simboli in anteprima", 2, 320, true, new Requirement(SkillId.C3)),
        new Definition(SkillId.C6, "Quinta casella", "Un'altra scelta per biglietto", 1, 850, true, new Requirement(SkillId.C5, 2)),
        new Definition(SkillId.C7, "Visione completa", "Tutti i 16 simboli in anteprima", 1, 1800, true, new Requirement(SkillId.C6)),
        new Definition(SkillId.C8, "Manipolazione", "Modifica un simbolo nascosto", 1, 5000, false, new Requirement(SkillId.C7)),

        // Economia — existing M4 payout becomes upgrade-aware.
        new Definition(SkillId.E1, "Premio base", "+2 monete al premio base per livello", 5, 15, true),
        new Definition(SkillId.E2, "Specialista coppie", "+10% al premio di coppia per livello", 3, 45, true, new Requirement(SkillId.E1, 2)),
        new Definition(SkillId.E3, "Specialista tris", "+15% al premio di tris per livello", 3, 110, true, new Requirement(SkillId.E2)),
        new Definition(SkillId.E4, "Poker d'oro", "+20% al premio poker per livello", 3, 300, true, new Requirement(SkillId.E3)),
        new Definition(SkillId.E5, "Consolazione", "+3 monete/livello se completi senza match", 3, 140, true, new Requirement(SkillId.E1, 3)),
        new Definition(SkillId.E6, "Serie vincente", "Bonus su vittorie consecutive", 5, 650, false, new Requirement(SkillId.E4)),
        new Definition(SkillId.E7, "Moltiplicatore globale", "Moltiplica tutte le vincite", 3, 2000, false, new Requirement(SkillId.E6, 3)),
        new Definition(SkillId.E8, "Cascata dorata", "Moltiplicatori crescenti senza limite", 1, 5500, false, new Requirement(SkillId.E7, 3)),

        // Tempo — one global timer; upgrades add time only on real goals.
        new Definition(SkillId.T1, "Primo slancio", "+0.5s al primo biglietto completo", 1, 30, true),
        new Definition(SkillId.T2, "Catena temporale", "+1s al terzo biglietto completo", 1, 100, true, new Requirement(SkillId.T1)),
        new Definition(SkillId.T3, "Ritmo costante", "+1s/livello ogni 4 biglietti completi", 2, 235, true, new Requirement(SkillId.T2)),
        new Definition(SkillId.T4, "Recupero I", "+1s completando 4 caselle", 1, 500, true, new Requirement(SkillId.T3), new Requirement(SkillId.C3)),
        new Definition(SkillId.T5, "Recupero II", "+2s totali per 4 caselle", 1, 950, true, new Requirement(SkillId.T4)),
        new Definition(SkillId.T6, "Recupero III", "+3s totali per 4 caselle", 1, 1800, true, new Requirement(SkillId.T5)),
        new Definition(SkillId.T7, "Recupero avanzato", "+4s totali per 4 caselle", 1, 3500, true, new Requirement(SkillId.T6)),
        new Definition(SkillId.T8, "Recupero estremo", "+5s totali per 5 caselle", 1, 7000, true, new Requirement(SkillId.T7), new Requirement(SkillId.C6))
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
            return "Richiede " + definitions[(int)requirement.Id].Title + " Lv." + requirement.Level;
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
