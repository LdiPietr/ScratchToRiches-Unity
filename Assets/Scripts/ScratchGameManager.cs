using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M2: 20-second rounds and a maximum number of distinct scratchable cells.
/// Payouts, symbols and shop rules deliberately belong to later milestones.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchCellManager))]
public sealed class ScratchGameManager : MonoBehaviour
{
    public enum RoundState { Ready, Playing, Finished }
    public enum RoundEndReason { None, TimeExpired, AllCellsRevealed, PlayerEnded }

    [Header("Round settings")]
    [SerializeField, Min(1f)] private float roundDuration = 20f;
    [SerializeField, Min(1)] private int scratchLimit = 3;

    [Header("Scene references")]
    [SerializeField] private ScratchCellManager cellManager;
    [SerializeField] private Text timerValue;
    [SerializeField] private Text scratchProgress;
    [SerializeField] private Text boardSubtitle;
    [SerializeField] private Button roundActionButton;
    [SerializeField] private Text roundActionLabel;

    private readonly HashSet<ScratchCell> selectedCells = new HashSet<ScratchCell>();
    private float timeRemaining;
    private RoundState state = RoundState.Ready;
    private RoundEndReason endReason = RoundEndReason.None;

    public RoundState State => state;
    public RoundEndReason EndReason => endReason;
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

        if (roundActionButton == null)
            Debug.LogError("ScratchGameManager: Round Action Button is missing.", this);
        else
            roundActionButton.onClick.AddListener(OnRoundAction);

        timeRemaining = roundDuration;
    }

    private void Start()
    {
        if (cellManager == null)
        {
            Debug.LogError("ScratchGameManager: ScratchCellManager is missing.", this);
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
        timeRemaining = roundDuration;
        endReason = RoundEndReason.None;
        state = RoundState.Playing;
        cellManager.ResetAllCells();
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

        state = RoundState.Finished;
        endReason = reason;
        RefreshHud();
    }

    private void RefreshHud()
    {
        UpdateTimerText();

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
                boardSubtitle.text = endReason == RoundEndReason.TimeExpired
                    ? "TIME UP! Press PLAY AGAIN to start a new round."
                    : endReason == RoundEndReason.AllCellsRevealed
                        ? "ALL PICKS REVEALED! Press PLAY AGAIN."
                        : "ROUND ENDED! Press PLAY AGAIN.";
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
