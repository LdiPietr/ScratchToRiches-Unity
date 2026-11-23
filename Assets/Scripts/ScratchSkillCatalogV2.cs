using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative 90-node catalog imported from GDD V2.
/// Catalog browsing is not the same as permission to buy a skill.
/// No UI is created at runtime. Art and controls live in the authored scene.
/// </summary>
public static class ScratchSkillCatalogV2
{
    [Serializable]
    public sealed class Entry
    {
        public string id;
        public string branch;
        public string title;
        public string description;
        public int maxLevel;
        public int initialPrice;
        public string prerequisites;
        public string phase;
        public string kind;
        public bool demoEligible;
        public string source;
        public string test;
        public string note;
    }

    [Serializable]
    private sealed class Root
    {
        public int version;
        public int skillCount;
        public Entry[] skills;
    }

    private static Entry[] entries;
    private static Dictionary<string, Entry> byId;
    private static readonly string[] branchPrefixes = { "G", "C", "E", "F", "A" };
    public static readonly string[] BranchNames =
        { "GRATTA!", "BIGLIETTI!", "INCASSA!", "FORTUNA!", "AUTOMAZIONE" };

    public static IReadOnlyList<Entry> All
    {
        get { EnsureLoaded(); return entries; }
    }

    public static int BranchCount => 5;

    public static int CountForBranch(int branch) =>
        branch >= 0 && branch < BranchCount ? (branch == 4 ? 10 : 20) : 0;

    public static Entry At(int branch, int position)
    {
        EnsureLoaded();
        if (branch < 0 || branch >= BranchCount || position < 0 ||
            position >= CountForBranch(branch))
            return null;
        // Fortunato (C03) now comes before the second preview (C02).
        // Keep stable skill IDs unchanged so existing saves remain valid.
        if (branch == 1 && position == 1) return Find("C03");
        if (branch == 1 && position == 2) return Find("C02");
        return Find(branchPrefixes[branch] + (position + 1).ToString("D2"));
    }

    public static Entry Find(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out Entry value)
            ? value : null;
    }

    /// <summary>
    /// Staged implementation gate. A demo-eligible skill with no gameplay
    /// implementation is visibly marked NOT READY and cannot take money.
    /// Only update this list together with a tested gameplay implementation.
    /// </summary>
    private static readonly HashSet<string> implementedEffects =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "G01", "G02", "G03", "G04", "G05", "G06", "G07", "G08",
            "C01", "C02", "C03", "C04", "C05", "C06",
            "E01", "E02", "E03", "E04", "E05", "E06",
            "F01", "F02", "F03", "F04"
        };

    public static bool EffectImplemented(string id) => implementedEffects.Contains(id);

    public static void EnsureLoaded()
    {
        if (entries != null) return;
        TextAsset asset = Resources.Load<TextAsset>("SkillCatalogV2");
        if (asset == null)
            throw new InvalidOperationException(
                "Missing Assets/Resources/SkillCatalogV2.json. No skill purchases are allowed.");

        Root root = JsonUtility.FromJson<Root>(asset.text);
        if (root == null || root.version != 2 || root.skills == null ||
            root.skills.Length != 90 || root.skillCount != 90)
            throw new InvalidOperationException("Invalid GDD V2 skill catalog.");

        var ids = new Dictionary<string, Entry>(StringComparer.Ordinal);
        int demo = 0;
        int[] branches = new int[5];
        for (int i = 0; i < root.skills.Length; i++)
        {
            Entry entry = root.skills[i];
            if (entry == null || string.IsNullOrEmpty(entry.id) ||
                string.IsNullOrEmpty(entry.title) || entry.maxLevel <= 0 ||
                entry.initialPrice <= 0 || ids.ContainsKey(entry.id))
                throw new InvalidOperationException("Invalid skill at row " + i);
            ids.Add(entry.id, entry);
            if (entry.demoEligible) demo++;
            for (int branch = 0; branch < 5; branch++)
                if (entry.id.StartsWith(branchPrefixes[branch], StringComparison.Ordinal))
                    branches[branch]++;
        }
        if (demo != 24 || branches[0] != 20 || branches[1] != 20 ||
            branches[2] != 20 || branches[3] != 20 || branches[4] != 10)
            throw new InvalidOperationException("Catalog must have 90 nodes and exactly 24 demo-eligible nodes.");

        // Verify all requirement edges and reject circular prerequisites.
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Entry entry in root.skills)
            CheckPrerequisites(entry.id, ids, state);

        byId = ids;
        entries = root.skills;
    }

    private static void CheckPrerequisites(string id, Dictionary<string, Entry> ids,
        Dictionary<string, int> state)
    {
        if (state.TryGetValue(id, out int visit))
        {
            if (visit == 1) throw new InvalidOperationException("Circular skill prerequisite: " + id);
            if (visit == 2) return;
        }
        state[id] = 1;
        foreach (string raw in (ids[id].prerequisites ?? "").Split(';'))
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string[] parts = raw.Split(':');
            if (parts.Length != 2 || !ids.TryGetValue(parts[0], out Entry prereq) ||
                !int.TryParse(parts[1], out int level) ||
                level < 1 || level > prereq.maxLevel)
                throw new InvalidOperationException("Invalid prerequisite " + raw + " for " + id);
            CheckPrerequisites(parts[0], ids, state);
        }
        state[id] = 2;
    }
}
