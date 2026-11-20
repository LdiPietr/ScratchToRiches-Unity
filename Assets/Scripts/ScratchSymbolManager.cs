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
    public enum MatchTier { None, Pair, Triple, Poker }
    public enum TestPattern { Random, Pair, Triple, Poker }

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
    /// One call per new round; all 16 symbols stay unchanged until the next round.
    /// Ensures that two consecutive RANDOM full boards cannot be identical.
    /// </summary>
    public bool GenerateRound()
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

        ScratchCell[] cells = GetComponentsInChildren<ScratchCell>(true);
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

        int[] layout = new int[cells.Length];
        for (int i = 0; i < layout.Length; i++)
            layout[i] = UnityEngine.Random.Range(0, symbolLabels.Length);

#if UNITY_EDITOR
        bool forced = editorTestPattern != TestPattern.Random;
        if (forced)
            ApplyEditorTestPattern(layout, editorTestPattern);
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
                layout[0] = (layout[0] + 1 + UnityEngine.Random.Range(0, symbolLabels.Length - 1))
                    % symbolLabels.Length;
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
    /// Four-or-more identical fully revealed symbols cap at Poker x4.
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

        MatchTier tier = bestCount >= 4 ? MatchTier.Poker
            : bestCount == 3 ? MatchTier.Triple
            : bestCount == 2 ? MatchTier.Pair
            : MatchTier.None;

        return new MatchResult(tier,
            tier == MatchTier.None || bestSymbol < 0 ? string.Empty : symbolLabels[bestSymbol],
            bestCount);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Deterministic first four cells for QA. Use Scratch Limit = 4 for Poker.
    /// All other cells retain their random labels.
    /// </summary>
    private static void ApplyEditorTestPattern(int[] layout, TestPattern pattern)
    {
        if (layout.Length < 4)
            return;

        layout[0] = 0;
        layout[1] = 0;
        layout[2] = pattern == TestPattern.Pair ? 1 : 0;
        layout[3] = pattern == TestPattern.Poker ? 0 : 2;
    }
#endif
}
