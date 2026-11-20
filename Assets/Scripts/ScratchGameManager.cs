using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M5: one 20-second RUN holds many scratch tickets. Completing or skipping a
/// ticket NEVER resets the run timer; only NEW RUN does.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchCellManager))]
[RequireComponent(typeof(ScratchSymbolManager))]
[RequireComponent(typeof(ScratchEconomyManager))]
[RequireComponent(typeof(ScratchSkillTree))]
public sealed class ScratchGameManager : MonoBehaviour
{
    public enum RoundState { Ready, Playing, Finished }
    public enum RoundEndReason { None, TimeExpired, AllCellsRevealed, PlayerEnded }
    // Ticket selection changes the payout, never the run timer or scratch limit.
    public enum TicketKind { Standard, Fortunato, Gold }

    [Header("Global run settings")]
    [SerializeField, Min(1f)] private float roundDuration = 20f;
    [SerializeField, Min(1)] private int scratchLimit = 3;

    [Header("Scene references")]
    [SerializeField] private ScratchCellManager cellManager;
    [SerializeField] private ScratchSymbolManager symbolManager;
    [SerializeField] private ScratchEconomyManager economyManager;
    [SerializeField] private ScratchSkillTree skillTree;
    [SerializeField] private TMP_Text timerValue;
    [SerializeField] private TMP_Text scratchProgress;
    [SerializeField] private TMP_Text boardSubtitle;
    [SerializeField] private TMP_Text matchResultText;
    [SerializeField] private Button roundActionButton;
    [SerializeField] private TMP_Text roundActionLabel;
    [SerializeField] private GameObject skillTreePanel;
    [SerializeField] private ScratchFlowPresentation presentation;

    private readonly HashSet<ScratchCell> selectedCells = new HashSet<ScratchCell>();
    private float timeRemaining;
    private float ticketOpenedAt;
    private RoundState state = RoundState.Ready;
    private RoundEndReason endReason;
    private ScratchSymbolManager.MatchResult settledMatch;
    private bool ticketActive;
    private bool ticketVoided;
    private bool internalReset;
    // Monotonic ticket ID protects economy settlement across multiple runs.
    private int currentTicketId;
    private int ticketNumberInRun;
    private int ticketsCompleted;
    private int ticketPayout;
    private TicketKind currentTicketKind = TicketKind.Standard;
    private float lastTimeBonus;

    public event Action Changed;
    public RoundState State => state;
    public RoundEndReason EndReason => endReason;
    public float TimeRemaining => timeRemaining;
    public float TicketElapsed => ticketActive ? Mathf.Max(0f, ticketOpenedAt - timeRemaining) : 0f;
    public bool TicketActive => ticketActive;
    public int TicketNumber => ticketNumberInRun;
    public int TicketsCompleted => ticketsCompleted;
    public int TicketPayout => ticketPayout;
    public float LastTimeBonus => lastTimeBonus;
    public TicketKind CurrentTicketKind => currentTicketKind;
    public bool WaitingForTicketChoice => state == RoundState.Playing && !ticketActive
        && ticketNumberInRun > 0 && timeRemaining > 0f;

    public static int TicketPrice(TicketKind kind)
    {
        switch (kind)
        {
            case TicketKind.Standard: return 0;
            case TicketKind.Fortunato: return 20;
            case TicketKind.Gold: return 65;
            default: return int.MaxValue;
        }
    }

    public int TicketPrize(TicketKind kind)
    {
        switch (kind)
        {
            case TicketKind.Standard: return economyManager != null ? economyManager.BasePrize : 10;
            case TicketKind.Fortunato: return 25;
            case TicketKind.Gold: return 45;
            default: return 0;
        }
    }

    public int SelectedCount => selectedCells.Count;
    public int RevealedCount
    {
        get
        {
            int count = 0;
            foreach (ScratchCell cell in selectedCells)
                if (cell != null && cell.IsComplete)
                    count++;
            return count;
        }
    }

    public int MaxScratches
    {
        get
        {
            int extra = Level(ScratchSkillTree.SkillId.C3) > 0 ? 1 : 0;
            if (Level(ScratchSkillTree.SkillId.C6) > 0) extra++;
            int available = cellManager != null ? cellManager.CellCount : scratchLimit;
            return Mathf.Clamp(scratchLimit + extra, 1, Mathf.Max(1, available));
        }
    }

    public ScratchSymbolManager.MatchResult CurrentMatch =>
        !ticketActive ? settledMatch : symbolManager != null ? symbolManager.Evaluate(selectedCells) : default;

    private int Level(ScratchSkillTree.SkillId id) => skillTree != null ? skillTree.GetLevel(id) : 0;

    private void Awake()
    {
        if (cellManager == null) cellManager = GetComponent<ScratchCellManager>();
        if (symbolManager == null) symbolManager = GetComponent<ScratchSymbolManager>();
        if (economyManager == null) economyManager = GetComponent<ScratchEconomyManager>();
        if (skillTree == null) skillTree = GetComponent<ScratchSkillTree>();
        if (presentation == null) presentation = GetComponent<ScratchFlowPresentation>();

        if (roundActionButton != null)
            roundActionButton.onClick.AddListener(OnRoundAction);
        else
            Debug.LogError("ScratchGameManager: RoundActionButton missing.", this);
        timeRemaining = roundDuration;
    }

    private void Start()
    {
        if (cellManager == null || symbolManager == null || economyManager == null || skillTree == null)
        {
            Debug.LogError("ScratchGameManager: M5 manager reference missing.", this);
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

        // Critically: this runs even BETWEEN tickets.
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (timeRemaining <= 0f)
        {
            if (ticketActive)
                FinishTicket(RoundEndReason.TimeExpired);
            state = RoundState.Finished;
            RefreshHud();
            return;
        }
        UpdateTimerText();
    }

    public void OnRoundAction()
    {
        // The post-run curtain owns restart, including the upward animation.
        if (state == RoundState.Finished && presentation != null)
            return;
        // Do not permit END TICKET before its entry animation has finished.
        if (state == RoundState.Playing && ticketActive &&
            presentation != null && !presentation.CanScratch)
            return;
        if (state != RoundState.Playing) StartNewRun();
        else if (ticketActive) FinishTicket(RoundEndReason.PlayerEnded);
        // Between tickets the player MUST select a ticket in the choice panel.
        // The main action button never silently starts another Standard ticket.
    }

    /// <summary>Only a new RUN resets the timer to 20 seconds.</summary>
    public void StartNewRun()
    {
        if (!Application.isPlaying || cellManager == null || state == RoundState.Playing)
            return;

        timeRemaining = roundDuration;
        ticketsCompleted = 0;
        state = RoundState.Playing;
        endReason = RoundEndReason.None;
        ticketNumberInRun = 0;
        ticketActive = false;
        BeginTicket(TicketKind.Standard, false); // First ticket remains free by default.
    }

    /// <summary>
    /// Only the between-ticket selection panel can request the next ticket.
    /// Insufficient funds or time never create a new ticket or charge coins.
    /// </summary>
    public bool ChooseNextTicket(TicketKind kind)
    {
        if (!WaitingForTicketChoice || TicketPrice(kind) == int.MaxValue)
            return false;
        return BeginTicket(kind, true);
    }

    private bool BeginTicket(TicketKind kind, bool charge)
    {
        if (state != RoundState.Playing || ticketActive || timeRemaining <= 0f)
            return false;

        int cost = charge ? TicketPrice(kind) : 0;
        if (cost > economyManager.Coins || cost == int.MaxValue)
            return false;

        selectedCells.Clear();
        ticketPayout = 0;
        lastTimeBonus = 0f;
        settledMatch = default;
        endReason = RoundEndReason.None;
        ticketVoided = false;

        internalReset = true;
        cellManager.ResetAllCells();
        internalReset = false;

        if (!symbolManager.GenerateRound())
        {
            Debug.LogError("M5: could not generate a scratch ticket.", this);
            state = RoundState.Finished;
            RefreshHud();
            return false;
        }

        // Generate first, then charge: failed generation never consumes coins.
        // On the main Unity thread no other wallet mutation can interleave.
        if (cost > 0 && !economyManager.TrySpendCoins(cost))
        {
            Debug.LogError("M5: ticket purchase could not be completed.", this);
            state = RoundState.Finished;
            RefreshHud();
            return false;
        }

        currentTicketKind = kind;
        currentTicketId++;
        ticketNumberInRun++;
        ticketActive = true;
        ticketOpenedAt = timeRemaining;
        economyManager.BeginTicket();
        ApplyPreviews();
        RefreshHud();
        return true;
    }

    public bool TrySelectCell(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || cell == null || !cell.isActiveAndEnabled)
            return false;
        // No input while an animated ticket is arriving or leaving.
        if (presentation != null && !presentation.CanScratch)
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
        if (state != RoundState.Playing || !ticketActive || !selectedCells.Contains(cell))
            return;
        if (selectedCells.Count == MaxScratches && RevealedCount == MaxScratches)
            FinishTicket(RoundEndReason.AllCellsRevealed);
        else
            RefreshHud();
    }

    public void OnCellsReset()
    {
        selectedCells.Clear();

        // The Inspector reset must not allow free rerolls of the same ticket.
        if (!internalReset && ticketActive)
            ticketVoided = true;
        RefreshHud();
    }

    public void EndRoundEarly()
    {
        if (state == RoundState.Playing && ticketActive)
            FinishTicket(RoundEndReason.PlayerEnded);
    }

    private void FinishTicket(RoundEndReason reason)
    {
        if (state != RoundState.Playing || !ticketActive)
            return;

        bool fullyCompleted = selectedCells.Count == MaxScratches && RevealedCount == MaxScratches;
        settledMatch = symbolManager.Evaluate(selectedCells);
        ticketActive = false;
        endReason = reason;

        // A manual Inspector reset voids the ticket, preventing free rerolls.
        if (ticketVoided)
        {
            settledMatch = default;
            ticketPayout = economyManager.SettleTicket(currentTicketId, settledMatch, false,
                TicketPrize(currentTicketKind));
        }
        else
        {
            ticketPayout = economyManager.SettleTicket(currentTicketId, settledMatch, fullyCompleted,
                TicketPrize(currentTicketKind));
            if (fullyCompleted && reason != RoundEndReason.TimeExpired && timeRemaining > 0f)
            {
                ticketsCompleted++;
                lastTimeBonus = ComputeTimeBonus();
                timeRemaining += lastTimeBonus;
            }
        }
        RefreshHud();
    }

    private float ComputeTimeBonus()
    {
        float bonus = 0f;
        if (ticketsCompleted == 1 && Level(ScratchSkillTree.SkillId.T1) > 0)
            bonus += 0.5f;
        if (ticketsCompleted == 3 && Level(ScratchSkillTree.SkillId.T2) > 0)
            bonus += 1f;
        if (ticketsCompleted % 4 == 0)
            bonus += Level(ScratchSkillTree.SkillId.T3);

        // T4-T8 replace one another; never add +1+2+3+4+5.
        int recoveredFromPicks = Level(ScratchSkillTree.SkillId.T8) > 0 && MaxScratches >= 5 ? 5 :
            Level(ScratchSkillTree.SkillId.T7) > 0 && MaxScratches >= 4 ? 4 :
            Level(ScratchSkillTree.SkillId.T6) > 0 && MaxScratches >= 4 ? 3 :
            Level(ScratchSkillTree.SkillId.T5) > 0 && MaxScratches >= 4 ? 2 :
            Level(ScratchSkillTree.SkillId.T4) > 0 && MaxScratches >= 4 ? 1 : 0;
        bonus += recoveredFromPicks;
        return bonus;
    }

    private void ApplyPreviews()
    {
        ScratchCell[] cells = GetComponentsInChildren<ScratchCell>(true);
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetPreview(null);

        int count = Level(ScratchSkillTree.SkillId.C7) > 0 ? cells.Length :
            Level(ScratchSkillTree.SkillId.C5) > 0 ? 2 + Level(ScratchSkillTree.SkillId.C5) :
            Level(ScratchSkillTree.SkillId.C2) > 0 ? 2 :
            Level(ScratchSkillTree.SkillId.C1) > 0 ? 1 : 0;

        // Unique random positions, not always the top-left corner.
        List<int> available = new List<int>(cells.Length);
        for (int i = 0; i < cells.Length; i++) available.Add(i);
        for (int i = 0; i < Mathf.Min(count, cells.Length); i++)
        {
            int chosen = UnityEngine.Random.Range(0, available.Count);
            ScratchCell cell = cells[available[chosen]];
            available.RemoveAt(chosen);
            if (symbolManager.TryGetSymbol(cell, out string symbol))
                cell.SetPreview(symbol);
        }
    }

    private void RefreshHud()
    {
        // The four skill branches are a post-run shop ONLY: no access during
        // the current ticket, between tickets, or before the first run.
        bool shouldShowTree = state == RoundState.Finished;
        if (skillTreePanel != null && skillTreePanel.activeSelf != shouldShowTree)
            skillTreePanel.SetActive(shouldShowTree);

        UpdateTimerText();
        if (scratchProgress != null)
            scratchProgress.text = $"TICKET {Mathf.Max(1, ticketNumberInRun)}   PICKS {selectedCells.Count}/{MaxScratches}   OPEN {RevealedCount}/{MaxScratches}";

        if (roundActionLabel != null)
            roundActionLabel.text = state == RoundState.Ready ? "START RUN"
                : state == RoundState.Finished ? "NEW RUN"
                : ticketActive ? "END TICKET" : "CHOOSE TICKET";

        // The choice overlay owns the next-ticket action; disable the old shortcut.
        if (roundActionButton != null)
            roundActionButton.interactable = state != RoundState.Playing || ticketActive;

        if (matchResultText != null)
        {
            ScratchSymbolManager.MatchResult match = CurrentMatch;
            matchResultText.text = state == RoundState.Ready ? "20 SECONDS - MULTIPLE TICKETS"
                : ticketActive ? match.IsMatch ? $"{match.Tier.ToString().ToUpperInvariant()} x{match.Multiplier} ({match.Symbol})" : "SCRATCH MATCHING SYMBOLS"
                : match.IsMatch ? $"{match.Tier.ToString().ToUpperInvariant()} x{match.Multiplier}   +{ticketPayout} COINS"
                : $"NO MATCH   +{ticketPayout} COINS";
        }

        if (boardSubtitle != null)
        {
            boardSubtitle.text = state == RoundState.Ready
                ? "Start a run: 20s total, as many tickets as you can finish."
                : state == RoundState.Finished
                    ? $"TIME UP! {ticketsCompleted} tickets completed. Start a new run."
                    : ticketActive
                        ? ticketVoided ? "TEST RESET - THIS TICKET WILL NOT PAY."
                            : $"{currentTicketKind} ticket {ticketNumberInRun}: finish {MaxScratches} picks to earn the full bonus."
                        : $"Earned +{ticketPayout} coins, +{lastTimeBonus:0.0}s. CHOOSE YOUR NEXT TICKET!";
        }
        Changed?.Invoke();
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
