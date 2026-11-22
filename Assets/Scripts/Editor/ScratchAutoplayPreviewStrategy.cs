#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>
/// Human-style decision policy for RANDOM and farming tickets only.
/// Consumes only information that the player can actually see:
/// revealed symbols and skill-previewed symbols. Never reads the hidden
/// ticket map or accesses ScratchSymbolManager.TryGetSymbol.
/// Fixed-pattern QA tickets continue to select the deterministic test cells.
/// </summary>
internal static class ScratchAutoplayPreviewStrategy
{
    internal readonly struct Choice
    {
        internal readonly ScratchCell Cell;
        internal readonly bool UsesPreview;
        internal readonly int VisiblePreviews;
        internal readonly bool MatchesRevealed;
        internal readonly string Reason;

        internal Choice(ScratchCell cell, bool usesPreview, int visiblePreviews,
            bool matchesRevealed, string reason)
        {
            Cell = cell;
            UsesPreview = usesPreview;
            VisiblePreviews = visiblePreviews;
            MatchesRevealed = matchesRevealed;
            Reason = reason;
        }
    }

    internal static Choice Choose(IReadOnlyList<ScratchCell> cells, int remainingPicks, Random rng)
    {
        if (cells == null || remainingPicks < 1 || rng == null)
            throw new ArgumentException("Invalid scratch strategy input.");

        var knownCompleted = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiblePreviews = new Dictionary<string, List<ScratchCell>>(StringComparer.Ordinal);
        var unknown = new List<ScratchCell>();
        int previewCount = 0;

        foreach (ScratchCell cell in cells)
        {
            if (cell == null || !cell.isActiveAndEnabled) continue;
            string visibleLabel;
            if (cell.IsComplete)
            {
                if (!cell.TryGetVisibleSymbol(out visibleLabel))
                    throw new InvalidOperationException("Revealed cell has no public symbol: " + cell.name);
                knownCompleted.TryGetValue(visibleLabel, out int count);
                knownCompleted[visibleLabel] = count + 1;
            }
            else if (cell.TryGetVisibleSymbol(out visibleLabel))
            {
                if (!cell.IsPreviewVisible)
                    throw new InvalidOperationException("Unrevealed cell leaked hidden data: " + cell.name);
                if (!visiblePreviews.TryGetValue(visibleLabel, out List<ScratchCell> same))
                {
                    same = new List<ScratchCell>();
                    visiblePreviews.Add(visibleLabel, same);
                }
                same.Add(cell);
                previewCount++;
            }
            else
                unknown.Add(cell);
        }

        // Highest priority: complete an existing pair/tris/poker using a visible match.
        int bestKnown = 0;
        List<ScratchCell> best = new List<ScratchCell>();
        string bestLabel = string.Empty;
        foreach (var entry in visiblePreviews)
        {
            int existing = knownCompleted.TryGetValue(entry.Key, out int count) ? count : 0;
            if (existing > bestKnown)
            {
                bestKnown = existing;
                best.Clear();
                best.AddRange(entry.Value);
                bestLabel = entry.Key;
            }
            else if (existing == bestKnown && existing > 0)
                best.AddRange(entry.Value);
        }
        if (best.Count > 0)
        {
            ScratchCell selected = best[rng.Next(best.Count)];
            return new Choice(selected, true, previewCount, true,
                "ANTEPRIMA: " + bestLabel + " coincide con un simbolo gia' scoperto");
        }

        // If at least two previews show the same symbol, take them together
        // provided enough picks remain for the guaranteed pair.
        int largestGroup = 1;
        best.Clear();
        foreach (var entry in visiblePreviews)
        {
            if (entry.Value.Count < 2 || remainingPicks < 2) continue;
            if (entry.Value.Count > largestGroup)
            {
                largestGroup = entry.Value.Count;
                best.Clear();
                best.AddRange(entry.Value);
                bestLabel = entry.Key;
            }
            else if (entry.Value.Count == largestGroup)
                best.AddRange(entry.Value);
        }
        if (best.Count > 0)
            return new Choice(best[rng.Next(best.Count)], true, previewCount, false,
                "ANTEPRIMA: " + largestGroup + " caselle con lo stesso simbolo " + bestLabel);

        // With a last remaining pick and an already revealed symbol, a preview
        // known to be different cannot make a pair. Exploring has a chance to.
        if (knownCompleted.Count > 0 && remainingPicks == 1 && unknown.Count > 0)
            return new Choice(unknown[rng.Next(unknown.Count)], false, previewCount, false,
                "ESPLORA: anteprime non corrispondenti, cerco un match nell'ignoto");

        // With an initial or otherwise unconstrained pick, use a public preview.
        if (visiblePreviews.Count > 0)
        {
            var candidates = new List<ScratchCell>();
            foreach (var entry in visiblePreviews) candidates.AddRange(entry.Value);
            ScratchCell selected = candidates[rng.Next(candidates.Count)];
            selected.TryGetVisibleSymbol(out string label);
            return new Choice(selected, true, previewCount, false,
                "ANTEPRIMA: scelgo un simbolo gia' visibile " + label);
        }

        if (unknown.Count > 0)
            return new Choice(unknown[rng.Next(unknown.Count)], false, previewCount, false,
                "ESPLORA: nessuna anteprima disponibile");

        throw new InvalidOperationException("No unselected scratchable cells remain.");
    }
}
#endif
