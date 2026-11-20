using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// M2-M4: timed rounds, scratch matches and once-per-round coin settlement.
/// Shop purchases and upgrades are reserved for M5.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchCellManager))]
[RequireComponent(typeof(ScratchSymbolManager))]
[RequireComponent(typeof(ScratchEconomyManager))]
public sealed class ScratchGameManager : MonoBehaviour
{
    public enum RoundState { Ready, Playing, Finished }
    public enum RoundEndReason { None, TimeExpired, AllCellsRevealed, PlayerEnded }

    [Header("Round settings")]
    [SerializeField, Min(1f)] private float roundDuration = 20f;
    [SerializeField, Min(1)] private int scratchLimit = 3;

    [Header("Scene references")]
    [SerializeField] private ScratchCellManager cellManager;
    [SerializeField] private ScratchSymbolManager symbolManager;
    [SerializeField] private ScratchEconomyManager economyManager;
    [SerializeField] private TMP_Text timerValue;
    [SerializeField] private TMP_Text scratchProgress;
    [SerializeField] private TMP_Text boardSubtitle;
    [SerializeField] private TMP_Text matchResultText;
    [SerializeField] private Button roundActionButton;
    [SerializeField] private TMP_Text roundActionLabel;

    private readonly HashSet<ScratchCell> selectedCells = new HashSet<ScratchCell>();
    private float timeRemaining;
    private RoundState state = RoundState.Ready;
    private RoundEndReason endReason = RoundEndReason.None;
    private ScratchSymbolManager.MatchResult settledMatch;
    private int currentRoundId;
    private int roundPayout;

    public RoundState State => state;
    public RoundEndReason EndReason => endReason;
    // Freeze the displayed match after settlement. Inspector resets cannot
    // rewrite the outcome or trigger a second payout.
    public ScratchSymbolManager.MatchResult CurrentMatch =>
        state == RoundState.Finished ? settledMatch :
        symbolManager != null ? symbolManager.Evaluate(selectedCells) : default;
    public int RoundPayout => state == RoundState.Finished ? roundPayout : 0;
    public float TimeRemaining => timeRemaining;
    public int SelectedCount => selectedCells.Count;
    public int RevealedCount
    {
        get
        {
            int total = 0;
            foreach (ScratchCell cell in selectedCells)
                if (cell != null && cell.IsComplete)
                    total++;
            return total;
        }
    }

    public int MaxScratches
    {
        get
        {
            int available = cellManager != null ? cellManager.CellCount : scratchLimit;
            return Mathf.Clamp(scratchLimit, 1, Mathf.Max(1, available));
        }
    }

    private void Awake()
    {
        if (cellManager == null)
            cellManager = GetComponent<ScratchCellManager>();

        if (symbolManager == null)
            symbolManager = GetComponent<ScratchSymbolManager>();

        if (economyManager == null)
            economyManager = GetComponent<ScratchEconomyManager>();

        if (roundActionButton == null)
            Debug.LogError("ScratchGameManager: Round Action Button is missing.", this);
        else
            roundActionButton.onClick.AddListener(OnRoundAction);

        timeRemaining = roundDuration;
    }

    private void Start()
    {
        if (cellManager == null || symbolManager == null || economyManager == null)
        {
            Debug.LogError("ScratchGameManager: ScratchCellManager, ScratchSymbolManager and ScratchEconomyManager are required.", this);
            enabled = false;
            return;
        }

        cellManager.CollectCells();
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (roundActionButton != null)
            roundActionButton.onClick.RemoveListener(OnRoundAction);
    }

    private void Update()
    {
        if (state != RoundState.Playing)
            return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (timeRemaining <= 0f)
        {
            FinishRound(RoundEndReason.TimeExpired);
            return;
        }

        UpdateTimerText();
    }

    /// <summary>
    /// Called by the primary UI button. Start, end early, or restart the round.
    /// </summary>
    public void OnRoundAction()
    {
        if (state == RoundState.Playing)
            FinishRound(RoundEndReason.PlayerEnded);
        else
            StartRound();
    }

    public void StartRound()
    {
        if (!Application.isPlaying || cellManager == null)
            return;

        cellManager.CollectCells();
        if (cellManager.CellCount == 0)
        {
            Debug.LogError("ScratchGameManager: No ScratchCell components found on Canvas.", this);
            return;
        }

        selectedCells.Clear();
        settledMatch = default;
        roundPayout = 0;
        timeRemaining = roundDuration;
        endReason = RoundEndReason.None;
        state = RoundState.Playing;
        cellManager.ResetAllCells();

        // A fresh, fixed board is assigned only once per round.
        if (symbolManager == null || !symbolManager.GenerateRound())
        {
            state = RoundState.Ready;
            RefreshHud();
            return;
        }

        // A new identifier only exists after symbol generation succeeds.
        // Keeping it monotonic protects against duplicate settlement.
        currentRoundId++;
        economyManager.BeginRound();
        RefreshHud();
    }

    /// <summary>
    /// Called only after a pointer lands INSIDE a cell; touching a new cell
    /// spends one pick, but repeating a selected cell never spends another.
    /// </summary>
    public bool TrySelectCell(ScratchCell cell)
    {
        if (state != RoundState.Playing || cell == null || !cell.isActiveAndEnabled)
            return false;

        if (!cell.transform.IsChildOf(transform))
            return false;

        if (selectedCells.Contains(cell))
            return true;

        if (selectedCells.Count >= MaxScratches)
            return false;

        selectedCells.Add(cell);
        RefreshHud();
        return true;
    }

    public void OnCellCompleted(ScratchCell cell)
    {
        if (state != RoundState.Playing || !selectedCells.Contains(cell))
            return;

        if (selectedCells.Count == MaxScratches && RevealedCount == MaxScratches)
        {
            FinishRound(RoundEndReason.AllCellsRevealed);
            return;
        }

        RefreshHud();
    }

    /// <summary>
    /// Invoked by ScratchCellManager.ResetAllCells, including its Inspector
    /// button. Resetting the covers also returns the available picks.
    /// The timer continues if an active round was in progress.
    /// </summary>
    public void OnCellsReset()
    {
        selectedCells.Clear();
        RefreshHud();
    }

    public void EndRoundEarly()
    {
        if (state == RoundState.Playing)
            FinishRound(RoundEndReason.PlayerEnded);
    }

    private void FinishRound(RoundEndReason reason)
    {
        if (state != RoundState.Playing)
            return;

        settledMatch = symbolManager.Evaluate(selectedCells);
        state = RoundState.Finished;
        endReason = reason;

        // Time up, all cells revealed and END ROUND all settle the same way.
        // The economy manager rejects any repeat settlement of this round ID.
        roundPayout = economyManager.SettleRound(currentRoundId, settledMatch);
        RefreshHud();
    }

    private void RefreshHud()
    {
        UpdateTimerText();

        if (matchResultText != null)
        {
            ScratchSymbolManager.MatchResult match = CurrentMatch;
            matchResultText.text = state == RoundState.Ready
                ? $"SCRATCH {MaxScratches} TO START"
                : state == RoundState.Finished
                    ? match.IsMatch
                        ? $"{match.Tier.ToString().ToUpperInvariant()} x{match.Multiplier}  +{roundPayout} COINS"
                        : "NO MATCH"
                    : match.IsMatch
                        ? $"{match.Tier.ToString().ToUpperInvariant()} x{match.Multiplier} ({match.Symbol})"
                        : "REVEAL MATCHING SYMBOLS";
        }

        if (scratchProgress != null)
            scratchProgress.text = $"PICKS {selectedCells.Count}/{MaxScratches}    OPEN {RevealedCount}/{MaxScratches}";

        if (roundActionLabel != null)
            roundActionLabel.text = state == RoundState.Playing
                ? "END ROUND"
                : state == RoundState.Ready ? "START ROUND" : "PLAY AGAIN";

        if (boardSubtitle == null)
            return;

        switch (state)
        {
            case RoundState.Ready:
                boardSubtitle.text = $"Press START ROUND. You have {roundDuration:0.#}s to scratch {MaxScratches} cells.";
                break;
            case RoundState.Playing:
                boardSubtitle.text = selectedCells.Count < MaxScratches
                    ? $"Choose {MaxScratches - selectedCells.Count} more cell(s), then finish revealing your picks."
                    : "All picks used. Finish revealing the selected cells!";
                break;
            case RoundState.Finished:
                string reward = roundPayout > 0 ? $"+{roundPayout} COINS" : "NO WIN";
                boardSubtitle.text = endReason == RoundEndReason.TimeExpired
                    ? $"TIME UP - {reward}. PRESS PLAY AGAIN."
                    : endReason == RoundEndReason.AllCellsRevealed
                        ? $"ALL PICKS REVEALED - {reward}. PLAY AGAIN."
                        : $"ROUND ENDED - {reward}. PLAY AGAIN.";
                break;
        }
    }

    private void UpdateTimerText()
    {
        if (timerValue != null)
            timerValue.text = $"{Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)):00}s";
    }

    private void OnValidate()
    {
        roundDuration = Mathf.Max(1f, roundDuration);
        scratchLimit = Mathf.Max(1, scratchLimit);
    }
}
