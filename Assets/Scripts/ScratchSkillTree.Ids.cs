// Stable serialized identifiers shared by the runtime and headless domain checks.
public sealed partial class ScratchSkillTree
{
    public enum Branch { Grattamento, Caselle, Economia, Fortuna }
    public enum SkillId
    {
        G1, G2, G3, G4, G5, G6, G7, G8,
        C1, C2, C3, C4, C5, C6, C7, C8,
        E1, E2, E3, E4, E5, E6, E7, E8,
        F1, F2, F3, F4, F5, F6, F7, F8,
        // New stable demo perk, beyond legacy 32-ID serialized range.
        C11 = 32
    }
}
