using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// V2: continuous gameplay. No countdown; a ticket determines its grid and symbol pool.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScratchCellManager))]
[RequireComponent(typeof(ScratchSymbolManager))]
[RequireComponent(typeof(ScratchEconomyManager))]
[RequireComponent(typeof(ScratchSkillTree))]
public sealed class ScratchGameManager : MonoBehaviour
{
    public enum RoundState { Ready, Playing, Finished }
    public enum RoundEndReason { None, AllCellsRevealed, PlayerEnded }
    // Ticket selection changes the payout, never the run timer or scratch limit.
    public enum TicketKind { Standard, Fortunato, Gold, Diamond }

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

    public event Action Changed;
    public RoundState State => state;
    public RoundEndReason EndReason => endReason;
    public bool TicketActive => ticketActive;
    public int TicketNumber => ticketNumberInRun;
    public int TicketsCompleted => ticketsCompleted;
    public int TicketPayout => ticketPayout;
    public TicketKind CurrentTicketKind => currentTicketKind;
    public bool WaitingForTicketChoice => state == RoundState.Playing && !ticketActive && ticketNumberInRun > 0;

    // Friendly display names: internal enum identifiers stay unchanged.
    public static string TicketDisplayName(TicketKind kind)
    {
        switch (kind)
        {
            case TicketKind.Standard: return "CLASSICO";
            case TicketKind.Fortunato: return "FORTUNATO";
            case TicketKind.Gold: return "DORATO";
            case TicketKind.Diamond: return "DIAMANTE";
            default: return "MISTERIOSO";
        }
    }

    private static string CombinationName(ScratchSymbolManager.MatchTier tier)
    {
        switch (tier)
        {
            case ScratchSymbolManager.MatchTier.Pair: return "COPPIA";
            case ScratchSymbolManager.MatchTier.Triple: return "TRIS";
            case ScratchSymbolManager.MatchTier.Poker: return "POKER";
            case ScratchSymbolManager.MatchTier.Five: return "CINQUINA";
            default: return "NESSUNA";
        }
    }

    // Provisional prices; comprehensive economy balancing is a separate phase.
    public static int TicketPrice(TicketKind kind)
    {
        switch (kind)
        {
            case TicketKind.Standard: return 0;
            case TicketKind.Fortunato: return 10;
            case TicketKind.Gold: return 25;
            case TicketKind.Diamond: return 60;
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
            case TicketKind.Diamond: return 70;
            default: return 0;
        }
    }

    public static int TicketSide(TicketKind kind) =>
        kind == TicketKind.Standard ? 3 : kind == TicketKind.Fortunato ? 4 : 5;

    public static int SymbolPoolSize(TicketKind kind) => kind == TicketKind.Diamond ? 4 : 7;

    public bool IsTicketUnlocked(TicketKind kind)
    {
        switch (kind)
        {
            case TicketKind.Standard: return true;
            case TicketKind.Fortunato: return Level(ScratchSkillTree.SkillId.C3) > 0;
            case TicketKind.Gold: return Level(ScratchSkillTree.SkillId.C5) > 0;
            case TicketKind.Diamond: return Level(ScratchSkillTree.SkillId.C7) > 0;
            default: return false;
        }
    }

    public bool CanChooseTicket(TicketKind kind) =>
        IsTicketUnlocked(kind) && economyManager != null && TicketPrice(kind) <= economyManager.Coins;

    public int CurrentGridSide => TicketSide(currentTicketKind);

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

    // A ticket's grid determines both the pick cap and its highest possible match.
    public int MaxScratches => TicketSide(currentTicketKind);

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
    }

    private void Start()
    {
        if (cellManager == null || symbolManager == null || economyManager == null || skillTree == null)
        {
            Debug.LogError("ScratchGameManager: core manager reference missing.", this);
            enabled = false;
            return;
        }
        cellManager.CollectCells();
        cellManager.ConfigureGrid(3);
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (roundActionButton != null)
            roundActionButton.onClick.RemoveListener(OnRoundAction);
    }

    public void OnRoundAction()
    {
        if (presentation != null && presentation.ShopOpen) return;
        if (state == RoundState.Ready) StartNewRun();
        else if (state == RoundState.Playing && ticketActive &&
                 (presentation == null || presentation.CanScratch))
            FinishTicket(RoundEndReason.PlayerEnded);
    }

    /// <summary>Starts the unlimited game session with a free Classic ticket.</summary>
    public void StartNewRun()
    {
        if (!Application.isPlaying || cellManager == null || state != RoundState.Ready)
            return;

        ticketsCompleted = 0;
        state = RoundState.Playing;
        endReason = RoundEndReason.None;
        ticketNumberInRun = 0;
        ticketActive = false;
        BeginTicket(TicketKind.Standard, false);
    }

    /// <summary>
    /// Only the between-ticket selection panel can request the next ticket.
    /// Insufficient funds or missing unlocks never create a ticket or charge coins.
    /// </summary>
    public bool ChooseNextTicket(TicketKind kind)
    {
        if (!WaitingForTicketChoice || !CanChooseTicket(kind))
            return false;
        return BeginTicket(kind, true);
    }

    private bool BeginTicket(TicketKind kind, bool charge)
    {
        if (state != RoundState.Playing || ticketActive || !IsTicketUnlocked(kind))
            return false;

        int cost = charge ? TicketPrice(kind) : 0;
        if (cost > economyManager.Coins || cost == int.MaxValue)
            return false;

        selectedCells.Clear();
        ticketPayout = 0;
        settledMatch = default;
        endReason = RoundEndReason.None;
        ticketVoided = false;

        // Hide all symbol artwork BEFORE any active-cell geometry changes.
        // This prevents the prior or next ticket's symbols from flashing while
        // the coating textures and sprites are being replaced.
        cellManager.BeginTicketVisualPreparation();
        if (!cellManager.ConfigureGrid(TicketSide(kind)))
        {
            cellManager.EndTicketVisualPreparation();
            return false;
        }
        cellManager.PrepareCoverForTicket(kind);
        internalReset = true;
        cellManager.ResetAllCells();
        internalReset = false;

        if (!symbolManager.GenerateRound(SymbolPoolSize(kind)))
        {
            Debug.LogError("V2: could not generate a scratch ticket.", this);
            if (ticketNumberInRun == 0) state = RoundState.Ready;
            cellManager.EndTicketVisualPreparation();
            RefreshHud();
            return false;
        }

        // Generate first, then charge: failed generation never consumes coins.
        // On the main Unity thread no other wallet mutation can interleave.
        if (cost > 0 && !economyManager.TrySpendCoins(cost))
        {
            Debug.LogError("V2: ticket purchase could not be completed.", this);
            cellManager.EndTicketVisualPreparation();
            RefreshHud();
            return false;
        }

        currentTicketKind = kind;
        currentTicketId++;
        ticketNumberInRun++;
        ticketActive = true;
        economyManager.BeginTicket();
        ApplyPreviews();
        if (!cellManager.EndTicketVisualPreparation())
            Debug.LogError("V2: one or more scratch masks are not ready before showing symbols.", this);
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
            if (fullyCompleted) ticketsCompleted++;
        }
        RefreshHud();
    }

    private void ApplyPreviews()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetPreview(null);

        int count = Level(ScratchSkillTree.SkillId.C6) > 0 ? 5 :
            Level(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
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
        UpdateTimerText();
        if (scratchProgress != null)
            scratchProgress.text = $"SCELTE {selectedCells.Count}/{MaxScratches}   |   GRATTATE {RevealedCount}/{MaxScratches}";

        if (roundActionLabel != null)
            roundActionLabel.text = state == RoundState.Ready ? "SI PARTE!"
                : ticketActive ? "PASSA OLTRE" : "SCEGLI IL BIGLIETTO";

        if (roundActionButton != null)
            roundActionButton.interactable = state == RoundState.Ready || ticketActive;

        if (matchResultText != null)
        {
            ScratchSymbolManager.MatchResult match = CurrentMatch;
            string win = CombinationName(match.Tier);
            matchResultText.text = state == RoundState.Ready ? "PRONTO A FARE IL COLPACCIO?"
                : ticketActive
                    ? match.IsMatch ? $"{win} x{match.Multiplier} - {match.Symbol}" : "CERCA I SIMBOLI UGUALI!"
                : match.IsMatch ? $"{win} x{match.Multiplier} +{ticketPayout} MONETE"
                : ticketPayout > 0 ? $"PREMIO DI CONSOLAZIONE! +{ticketPayout}"
                : "PECCATO! RITENTA!";
        }

        if (boardSubtitle != null)
            boardSubtitle.text = state == RoundState.Ready
                ? "Gratta senza tempo limite, migliora le abilita' e sblocca biglietti!"
                : ticketActive
                    ? ticketVoided ? "Questo biglietto di prova non assegna premi."
                    : $"{TicketDisplayName(currentTicketKind)} {CurrentGridSide}x{CurrentGridSide} - {MaxScratches} caselle da grattare!"
                    : $"+{ticketPayout} monete! Scegli il prossimo biglietto o apri i potenziamenti.";

        Changed?.Invoke();
    }

    private void UpdateTimerText()
    {
        // The former timer panel has become a scene-authored shop button.
        if (timerValue != null) timerValue.text = "APRI SHOP";
    }


}
