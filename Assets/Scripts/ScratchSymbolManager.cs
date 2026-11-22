using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M3: Generates one fixed symbol for every scratch cell per round and
/// evaluates matching symbols from FULLY revealed, selected cells only.
/// Does not pay coins or modify M2 round rules.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchCellManager))]
public sealed class ScratchSymbolManager : MonoBehaviour
{
    public enum MatchTier { None, Pair, Triple, Poker, Five }
    public enum TestPattern { Random, Pair, Triple, Poker, Five }

    public readonly struct MatchResult
    {
        public readonly MatchTier Tier;
        public readonly string Symbol;
        public readonly int MatchingCount;
        public readonly int Multiplier;

        public MatchResult(MatchTier tier, string symbol, int count)
        {
            Tier = tier;
            Symbol = symbol;
            MatchingCount = count;
            Multiplier = (int)tier == 0 ? 1 : (int)tier + 1;
        }

        public bool IsMatch => Tier != MatchTier.None;
    }

    [Header("Seven winning symbols, in matching artwork order")]
    [SerializeField] private string[] symbolLabels =
    {
        "SETTE FORTUNATO", "MONETA", "DIAMANTE", "STELLA",
        "CORONA", "QUADRIFOGLIO", "SACCHETTO DI MONETE"
    };
    [SerializeField] private Sprite[] symbolSprites = new Sprite[7];

#if UNITY_EDITOR
    [Header("Editor-only deterministic test (Cell 01-04)")]
    [SerializeField] private TestPattern editorTestPattern = TestPattern.Random;
#endif

    [Header("Scene reference")]
    [SerializeField] private ScratchCellManager cellManager;

    private readonly Dictionary<ScratchCell, int> currentSymbols = new Dictionary<ScratchCell, int>();
    private int[] previousLayout;

    public int SymbolCount => symbolLabels == null ? 0 : symbolLabels.Length;

    public bool TryGetSymbol(ScratchCell cell, out string label)
    {
        label = string.Empty;
        if (cell == null || !currentSymbols.TryGetValue(cell, out int id) ||
            symbolLabels == null || id < 0 || id >= symbolLabels.Length)
            return false;
        label = symbolLabels[id];
        return true;
    }

    /// <summary>
    /// One call per new ticket; all active symbols remain fixed until the next ticket.
    /// Ensures that two consecutive RANDOM full boards cannot be identical.
    /// </summary>
    public bool GenerateRound(int poolSize = 7)
    {
        if (cellManager == null)
            cellManager = GetComponent<ScratchCellManager>();

        if (cellManager == null)
        {
            Debug.LogError("ScratchSymbolManager needs ScratchCellManager on Canvas.", this);
            return false;
        }

        if (symbolLabels == null || symbolLabels.Length != 7 ||
            symbolSprites == null || symbolSprites.Length != symbolLabels.Length)
        {
            Debug.LogError("ScratchSymbolManager: seven names and seven matching sprite references are required.", this);
            return false;
        }

        for (int i = 0; i < symbolSprites.Length; i++)
        {
            if (symbolSprites[i] == null)
            {
                Debug.LogError($"ScratchSymbolManager: missing sprite at index {i} ({symbolLabels[i]}).", this);
                return false;
            }
        }

        HashSet<string> unique = new HashSet<string>();
        foreach (string label in symbolLabels)
        {
            if (string.IsNullOrWhiteSpace(label) || !unique.Add(label))
            {
                Debug.LogError("Every symbol label must be unique and non-empty.", this);
                return false;
            }
        }

        ScratchCell[] cells = cellManager.ActiveCells;
        if (cells.Length == 0)
        {
            Debug.LogError("ScratchSymbolManager found no scratch cells.", this);
            return false;
        }

        foreach (ScratchCell cell in cells)
        {
            if (cell == null || !cell.HasSymbolDisplay)
            {
                Debug.LogError("Every ScratchCell needs an Image or TextMeshPro symbol reference.", this);
                return false;
            }
        }

        if (poolSize != 4 && poolSize != 7)
        {
            Debug.LogError("Unsupported symbol pool size.", this);
            return false;
        }
        // Diamond randomly selects four distinct symbols from the existing seven.
        int[] pool = { 0, 1, 2, 3, 4, 5, 6 };
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int pick = UnityEngine.Random.Range(0, i + 1);
            int temp = pool[i]; pool[i] = pool[pick]; pool[pick] = temp;
        }
        int[] layout = new int[cells.Length];
        for (int i = 0; i < layout.Length; i++)
            layout[i] = pool[UnityEngine.Random.Range(0, poolSize)];

#if UNITY_EDITOR
        bool forced = editorTestPattern != TestPattern.Random;
        if (forced)
            ApplyEditorTestPattern(layout, editorTestPattern, pool);
#else
        bool forced = false;
#endif

        if (!forced && previousLayout != null && previousLayout.Length == layout.Length)
        {
            bool identical = true;
            for (int i = 0; i < layout.Length; i++)
                if (layout[i] != previousLayout[i])
                {
                    identical = false;
                    break;
                }

            if (identical)
                layout[0] = pool[(Array.IndexOf(pool, layout[0]) + 1) % poolSize];
        }

        currentSymbols.Clear();
        for (int i = 0; i < cells.Length; i++)
        {
            currentSymbols.Add(cells[i], layout[i]);
            cells[i].SetSymbol(symbolLabels[layout[i]], symbolSprites[layout[i]]);
        }

        previousLayout = (int[])layout.Clone();
        return true;
    }

    /// <summary>
    /// Unfinished scratches and untouched cells never contribute to a match.
    /// Five identical completed symbols produce Cinquina x5.
    /// </summary>
    public MatchResult Evaluate(IEnumerable<ScratchCell> selectedCells)
    {
        if (selectedCells == null)
            return new MatchResult(MatchTier.None, string.Empty, 0);

        Dictionary<int, int> counts = new Dictionary<int, int>();
        int bestSymbol = -1;
        int bestCount = 0;

        foreach (ScratchCell cell in selectedCells)
        {
            if (cell == null || !cell.IsComplete || !currentSymbols.TryGetValue(cell, out int symbol))
                continue;

            counts.TryGetValue(symbol, out int count);
            count++;
            counts[symbol] = count;

            if (count > bestCount)
            {
                bestCount = count;
                bestSymbol = symbol;
            }
        }

        MatchTier tier = bestCount >= 5 ? MatchTier.Five
            : bestCount == 4 ? MatchTier.Poker
            : bestCount == 3 ? MatchTier.Triple
            : bestCount == 2 ? MatchTier.Pair
            : MatchTier.None;

        return new MatchResult(tier,
            tier == MatchTier.None || bestSymbol < 0 ? string.Empty : symbolLabels[bestSymbol],
            bestCount);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Deterministic first three, four or five cells for QA.
    /// All other cells retain their random labels.
    /// </summary>
    private static void ApplyEditorTestPattern(int[] layout, TestPattern pattern, int[] pool)
    {
        if (layout.Length < 3) return;
        layout[0] = pool[0];
        layout[1] = pool[0];
        layout[2] = pattern == TestPattern.Pair ? pool[1] : pool[0];
        if (layout.Length >= 4)
            layout[3] = pattern == TestPattern.Poker || pattern == TestPattern.Five ? pool[0] : pool[2];
        if (layout.Length >= 5)
            layout[4] = pattern == TestPattern.Five ? pool[0] :
                pattern == TestPattern.Pair ? pool[3] : pool[1];
    }
#endif
}
