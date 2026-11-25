#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exact finite-horizon decision policy for a human-like player, used on
/// RANDOM tickets only. The policy reads ONLY fully scratched and explicitly
/// previewed symbols via ScratchCell.TryGetVisibleSymbol().
///
/// The 4-7 symbol labels within each ticket pool are exchangeable, so unseen labels are represented
/// as a single "new label" outcome in the recurrence. For the Diamond's
/// randomly chosen four-of-seven pool, the posterior predictive probability
/// of each already-observed symbol is 1/4; total probability of a new symbol
/// is (4 - distinctObserved)/4. No secret Diamond pool is accessed.
/// </summary>
internal static class ScratchAutoplayPreviewStrategy
{
    private const int SymbolTypes = 7;
    private const double TieTolerance = 0.0000001;

    internal readonly struct Choice
    {
        internal readonly ScratchCell Cell;
        internal readonly bool UsesPreview;
        internal readonly int VisiblePreviews;
        internal readonly bool MatchesRevealed;
        internal readonly string Reason;
        internal readonly double ExpectedPayout;
        internal readonly double ExploreExpectedPayout;
        internal readonly double BestPreviewExpectedPayout;

        internal Choice(ScratchCell cell, bool preview, int previews, bool match,
            string reason, double expected, double explore, double bestPreview)
        {
            Cell = cell;
            UsesPreview = preview;
            VisiblePreviews = previews;
            MatchesRevealed = match;
            Reason = reason;
            ExpectedPayout = expected;
            ExploreExpectedPayout = explore;
            BestPreviewExpectedPayout = bestPreview;
        }
    }

    private readonly struct Candidate
    {
        internal readonly int Index; // -1 is an entirely unknown (opaque) cell.
        internal readonly int Weight;
        internal readonly double Expected;

        internal Candidate(int index, int weight, double expected)
        {
            Index = index;
            Weight = weight;
            Expected = expected;
        }
    }

    /// <summary>
    /// Maximizes expected FINAL payout, not how many previews are consumed.
    /// Tie-breaking samples uniformly among all equally valuable cells.
    /// </summary>
    internal static Choice Choose(IReadOnlyList<ScratchCell> cells, int remainingPicks,
        int activeSymbolPoolSize, double[] payoutByMultiplicity, System.Random rng)
    {
        if (cells == null || rng == null || remainingPicks < 1 ||
            (activeSymbolPoolSize < 4 || activeSymbolPoolSize > 7) ||
            payoutByMultiplicity == null || payoutByMultiplicity.Length < 6)
            throw new ArgumentException("Invalid strategy parameters.");

        int[] revealed = new int[SymbolTypes];
        int scratchedJollies = 0;
        int[] previews = new int[SymbolTypes];
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        var visibleCells = new List<ScratchCell>[SymbolTypes];
        for (int i = 0; i < SymbolTypes; i++)
            visibleCells[i] = new List<ScratchCell>();
        var opaqueCells = new List<ScratchCell>();
        int visibleCount = 0;

        foreach (ScratchCell cell in cells)
        {
            if (cell == null || !cell.isActiveAndEnabled) continue;

            string symbol;
            if (cell.IsComplete)
            {
                if (!cell.TryGetVisibleSymbol(out symbol))
                    throw new InvalidOperationException("Completed cell lacks visible symbol: " + cell.name);
                // Joker is a wildcard, not an eighth normal pool symbol.
                if (symbol == "JOLLY") { scratchedJollies++; continue; }
                int id = IdOf(symbol, labels);
                revealed[id]++;
            }
            else if (cell.TryGetVisibleSymbol(out symbol))
            {
                if (!cell.IsPreviewVisible)
                    throw new InvalidOperationException("Hidden symbol leaked from " + cell.name);
                int id = IdOf(symbol, labels);
                previews[id]++;
                visibleCells[id].Add(cell);
                visibleCount++;
            }
            else
                opaqueCells.Add(cell);
        }

        int knownDistinct = labels.Count;
        // Approximate an already-scratched Joker by crediting the strongest
        // visible normal symbol. This uses only public scratch/preview data.
        if (scratchedJollies > 0 && knownDistinct > 0)
        {
            int strongest = 0;
            for (int i = 1; i < knownDistinct; i++)
                if (revealed[i] > revealed[strongest]) strongest = i;
            revealed[strongest] += scratchedJollies;
        }
        if (knownDistinct > activeSymbolPoolSize)
            throw new InvalidOperationException("Visible symbols exceed the ticket's distinct symbol pool.");
        if (remainingPicks > visibleCount + opaqueCells.Count)
            throw new InvalidOperationException("More picks remain than scratchable cells.");

        var memo = new Dictionary<long, double>();
        var candidates = new List<Candidate>();
        double bestPreview = double.NegativeInfinity;
        double explore = double.NegativeInfinity;

        for (int i = 0; i < knownDistinct; i++)
        {
            if (previews[i] <= 0) continue;
            double score = ScorePreview(revealed, previews, knownDistinct, opaqueCells.Count,
                remainingPicks, activeSymbolPoolSize, payoutByMultiplicity, memo, i);
            bestPreview = Math.Max(bestPreview, score);
            candidates.Add(new Candidate(i, visibleCells[i].Count, score));
        }

        if (opaqueCells.Count > 0)
        {
            explore = ScoreOpaque(revealed, previews, knownDistinct, opaqueCells.Count,
                remainingPicks, activeSymbolPoolSize, payoutByMultiplicity, memo);
            candidates.Add(new Candidate(-1, opaqueCells.Count, explore));
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("No incomplete cells can be scratched.");

        double best = double.NegativeInfinity;
        foreach (Candidate candidate in candidates)
            best = Math.Max(best, candidate.Expected);

        // Choose fairly between equally valuable paths. If the strategies are
        // equivalent, a transparent preview is NOT automatically preferred.
        int totalTiedCells = 0;
        foreach (Candidate candidate in candidates)
            if (candidate.Expected >= best - TieTolerance)
                totalTiedCells += candidate.Weight;

        int draw = rng.Next(totalTiedCells);
        Candidate selected = default;
        bool chosen = false;
        foreach (Candidate candidate in candidates)
        {
            if (candidate.Expected < best - TieTolerance) continue;
            if (draw < candidate.Weight)
            {
                selected = candidate;
                chosen = true;
                break;
            }
            draw -= candidate.Weight;
        }
        if (!chosen)
            throw new InvalidOperationException("Failed to choose an optimal action.");

        bool usingPreview = selected.Index >= 0;
        ScratchCell picked = usingPreview
            ? visibleCells[selected.Index][rng.Next(visibleCells[selected.Index].Count)]
            : opaqueCells[rng.Next(opaqueCells.Count)];
        bool matches = usingPreview && revealed[selected.Index] > 0;
        string action = usingPreview ? "ANTEPRIMA" : "COPERTA";
        string comparison = "valore_atteso_" + action + "=" + Format(selected.Expected) +
            ", migliore_anteprima=" + Format(bestPreview) +
            ", coperta=" + Format(explore);
        string explanation = (matches ? "Simbolo gia' scoperto; " :
            usingPreview ? "Combinazione nota utile; " :
            visibleCount == 0 ? "Nessuna anteprima disponibile; " :
            "Rischio calcolato: non gratto automaticamente le anteprime; ") +
            comparison;

        return new Choice(picked, usingPreview, visibleCount, matches,
            explanation, best, explore, bestPreview);
    }

    private static int IdOf(string name, Dictionary<string, int> labels)
    {
        if (labels.TryGetValue(name, out int id)) return id;
        id = labels.Count;
        if (id >= SymbolTypes)
            throw new InvalidOperationException("More than seven distinct visible symbols.");
        labels.Add(name, id);
        return id;
    }

    private static string Format(double value) => double.IsNegativeInfinity(value)
        ? "n/a" : value.ToString("F2", CultureInfo.InvariantCulture);

    private static double ScorePreview(int[] revealed, int[] previews, int known,
        int opaqueCount, int remaining, int pool, double[] payouts,
        Dictionary<long, double> memo, int index)
    {
        previews[index]--;
        revealed[index]++;
        double value = Solve(revealed, previews, known, opaqueCount, remaining - 1, pool, payouts, memo);
        revealed[index]--;
        previews[index]++;
        return value;
    }

    private static double ScoreOpaque(int[] revealed, int[] previews, int known,
        int opaqueCount, int remaining, int pool, double[] payouts,
        Dictionary<long, double> memo)
    {
        if (opaqueCount <= 0) return double.NegativeInfinity;
        double average = 0d;
        for (int i = 0; i < known; i++)
        {
            revealed[i]++;
            average += Solve(revealed, previews, known, opaqueCount - 1, remaining - 1,
                pool, payouts, memo) / pool;
            revealed[i]--;
        }
        if (known < pool)
        {
            // Newly discovered symbols are exchangeable. Aggregating all
            // unseen types here is exact, even for the 4-of-7 Diamond.
            revealed[known]++;
            average += (pool - known) / (double)pool *
                Solve(revealed, previews, known + 1, opaqueCount - 1,
                    remaining - 1, pool, payouts, memo);
            revealed[known]--;
        }
        return average;
    }

    private static double Solve(int[] revealed, int[] previews, int known,
        int opaqueCount, int remaining, int pool, double[] payouts,
        Dictionary<long, double> memo)
    {
        if (remaining <= 0 || (opaqueCount == 0 && !HasPreviews(previews, known)))
            return TerminalPayout(revealed, payouts);

        // 8^14 * 8 * 26 * 7 fits in 64-bit; six picks require a radix above six.
        long key = 0L;
        for (int i = 0; i < SymbolTypes; i++) key = key * 8L + revealed[i];
        for (int i = 0; i < SymbolTypes; i++) key = key * 8L + previews[i];
        key = ((key * 8L + known) * 26L + opaqueCount) * 7L + remaining;

        if (memo.TryGetValue(key, out double cached))
            return cached;

        double best = double.NegativeInfinity;
        for (int i = 0; i < known; i++)
        {
            if (previews[i] <= 0) continue;
            best = Math.Max(best, ScorePreview(revealed, previews, known, opaqueCount,
                remaining, pool, payouts, memo, i));
        }
        if (opaqueCount > 0)
            best = Math.Max(best, ScoreOpaque(revealed, previews, known, opaqueCount,
                remaining, pool, payouts, memo));

        if (double.IsNegativeInfinity(best))
            best = TerminalPayout(revealed, payouts);
        memo.Add(key, best);
        return best;
    }

    private static bool HasPreviews(int[] previews, int known)
    {
        for (int i = 0; i < known; i++)
            if (previews[i] > 0) return true;
        return false;
    }

    private static double TerminalPayout(int[] revealed, double[] payouts)
    {
        int highest = 0;
        for (int i = 0; i < SymbolTypes; i++)
            highest = Math.Max(highest, revealed[i]);
        return payouts[Math.Min(highest, 5)];
    }

    /// <summary>Pure, fast Editor validation; no Unity scene or player save required.</summary>
    internal static void RunDecisionTests()
    {
        double[] payout = { 0, 0, 2, 3, 4, 5 };
        int failed = 0;
        Test("Anteprima diversa, un tentativo: meglio coperta",
            new[] {1,0,0,0,0,0,0}, new[] {0,1,0,0,0,0,0}, 2, 8, 1, 7,
            payout, true, ref failed);
        Test("Anteprima uguale, un tentativo: meglio anteprima",
            new[] {1,0,0,0,0,0,0}, new[] {1,0,0,0,0,0,0}, 1, 8, 1, 7,
            payout, false, ref failed);
        Test("Due anteprime uguali, due tentativi: coppia garantita",
            new int[7], new[] {2,0,0,0,0,0,0}, 1, 7, 2, 7,
            payout, false, ref failed);
        Test("Una sola anteprima, due tentativi: conviene esplorare",
            new int[7], new[] {1,0,0,0,0,0,0}, 1, 8, 2, 7,
            payout, true, ref failed);
        Test("Diamante: anteprima diversa, ultima mossa",
            new[] {1,0,0,0,0,0,0}, new[] {0,1,0,0,0,0,0}, 2, 20, 1, 4,
            payout, true, ref failed);
        TestTie("Prima mossa con una anteprima, un tentativo: non privilegiarla",
            new int[7], new[] {1,0,0,0,0,0,0}, 1, 8, 1, 7, payout, ref failed);

        if (failed == 0)
            Debug.Log("STR PREVIEW STRATEGY PASS: tutte le 6 decisioni teoriche sono corrette.");
        else
            Debug.LogError("STR PREVIEW STRATEGY FAIL: " + failed + " casi non superati.");
    }

    private static void TestTie(string title, int[] revealed, int[] previews, int known,
        int opaque, int picks, int pool, double[] payouts, ref int fails)
    {
        var memo = new Dictionary<long, double>();
        double preview = ScorePreview(revealed, previews, known,
            opaque, picks, pool, payouts, memo, 0);
        double covered = ScoreOpaque(revealed, previews, known, opaque, picks, pool, payouts, memo);
        if (Math.Abs(preview - covered) <= TieTolerance) return;
        fails++;
        Debug.LogError("STR PREVIEW STRATEGY FAIL: " + title +
            " | coperta=" + Format(covered) + ", anteprima=" + Format(preview));
    }

    private static void Test(string title, int[] revealed, int[] previews, int known,
        int opaque, int picks, int pool, double[] payouts, bool expectOpaque, ref int fails)
    {
        var memo = new Dictionary<long, double>();
        double bestPreview = double.NegativeInfinity;
        for (int i = 0; i < known; i++)
            if (previews[i] > 0)
                bestPreview = Math.Max(bestPreview, ScorePreview(revealed, previews, known,
                    opaque, picks, pool, payouts, memo, i));
        double exploration = ScoreOpaque(revealed, previews, known,
            opaque, picks, pool, payouts, memo);
        bool actual = exploration > bestPreview + TieTolerance;
        bool ok = expectOpaque ? actual : bestPreview > exploration + TieTolerance;
        if (!ok)
        {
            fails++;
            Debug.LogError("STR PREVIEW STRATEGY FAIL: " + title + " | coperta=" +
                Format(exploration) + ", anteprima=" + Format(bestPreview));
        }
    }
}
#endif
