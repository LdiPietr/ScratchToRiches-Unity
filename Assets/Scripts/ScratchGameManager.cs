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
    // Ticket selection changes the grid and payout, not the scratch limit.
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
    [Header("Authored demo ending - hidden until milestone")]
    [SerializeField] private GameObject demoEndOverlay;
    private const string DemoActiveKey = "STR_GDDV2_DemoCapstoneActive";
    private const string DemoGoldKey = "STR_GDDV2_DemoGoldTickets";
    private const string DemoTitanBaseKey = "STR_GDDV2_DemoTitanBaseline";
    private const string DemoJollyBaseKey = "STR_GDDV2_DemoJollyBaseline";
    private const string DemoDoneKey = "STR_GDDV2_DemoFinished";
    private const string DemoGoldPayoutKey = "STR_GDDV2_FinalGoldPayout";
    private const string DemoRuleVersionKey = "STR_GDDV2_FinalRuleVersion";
    private const int DemoRuleVersion = 2;
    private const int FinalGoldMinTickets = 3;
    private const int FinalGoldPayoutTarget = 1800;
    private bool demoCapstoneActive;
    private bool demoFinished;
    private int demoGoldCompleted;
    private int demoGoldPayout;
    private bool targetedPreviewUsed;
    private int demoExcludedTicketId = -1;
    private int titanActivationBaseline;
    private int jollyActivationBaseline;

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
    // Lifetime completions persist between sessions to prevent the radial
    // nucleus from locking again after a new run or editor restart.
    private const string RadialTicketPrefix = "STR_Radial_CompletedTier_";
    private readonly int[] completedByTier = new int[4];
    private int ticketPayout;
    private TicketKind currentTicketKind = TicketKind.Standard;

    public event Action Changed;
    public RoundState State => state;
    public RoundEndReason EndReason => endReason;
    public bool TicketActive => ticketActive;
    public int TicketNumber => ticketNumberInRun;
    public int TicketsCompleted => ticketsCompleted;
    public int CompletedByTier(TicketKind kind)
    {
        int index = (int)kind;
        return index >= 0 && index < completedByTier.Length ? completedByTier[index] : 0;
    }
    public int TicketPayout => ticketPayout;
    public bool DemoFinished => demoFinished;
    public int DemoFinalGoldTickets => demoGoldCompleted;
    public int DemoFinalGoldPayout => demoGoldPayout;
    public int DemoFinalGoldPayoutTarget => FinalGoldPayoutTarget;
    public int DemoFinalGoldMinTickets => FinalGoldMinTickets;
    public bool DemoFinalPowerActivated => skillTree != null &&
        (skillTree.TitanActivationCount > titanActivationBaseline ||
         skillTree.JollyActivationCount > jollyActivationBaseline);
    public bool InspectionUsedThisTicket => targetedPreviewUsed;

    /// <summary>Pure final-goal predicate, also exercised by the Editor scene audit.</summary>
    public static bool FinalChallengeComplete(int finishedGoldTickets, int earnedGoldCoins, bool activated) =>
        finishedGoldTickets >= FinalGoldMinTickets &&
        earnedGoldCoins >= FinalGoldPayoutTarget && activated;

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
            case TicketKind.Fortunato: return 50;
            case TicketKind.Gold: return 110;
            case TicketKind.Diamond: return 250;
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

    // Pick count is global: C11 controls it, independently of ticket tier.
    // Grid dimensions and symbol pools remain ticket-specific.
    public static int PickLimitFromPerk(int extraPickLevel) =>
        Mathf.Clamp(3 + extraPickLevel, 3, 5);

    public int MaxScratches => PickLimitFromPerk(skillTree != null ? skillTree.GetLevel("C11") : 0);

    public ScratchSymbolManager.MatchResult CurrentMatch =>
        !ticketActive ? settledMatch : symbolManager != null ? symbolManager.Evaluate(selectedCells) : default;

    private int Level(ScratchSkillTree.SkillId id) => skillTree != null ? skillTree.GetLevel(id) : 0;

    private void Awake()
    {
        for (int tier = 0; tier < completedByTier.Length; tier++)
            completedByTier[tier] = Mathf.Max(0,
                PlayerPrefs.GetInt(RadialTicketPrefix + tier, 0));
        demoCapstoneActive = PlayerPrefs.GetInt(DemoActiveKey, 0) == 1;
        demoGoldCompleted = Mathf.Max(0, PlayerPrefs.GetInt(DemoGoldKey, 0));
        demoGoldPayout = Mathf.Max(0, PlayerPrefs.GetInt(DemoGoldPayoutKey, 0));
        titanActivationBaseline = Mathf.Max(0, PlayerPrefs.GetInt(DemoTitanBaseKey, 0));
        jollyActivationBaseline = Mathf.Max(0, PlayerPrefs.GetInt(DemoJollyBaseKey, 0));
        demoFinished = PlayerPrefs.GetInt(DemoDoneKey, 0) == 1;
        // Existing unfinished saves used the old "three Gold or either capstone"
        // rule. Restart that milestone once, without touching skill levels/coins.
        if (!demoFinished && PlayerPrefs.GetInt(DemoRuleVersionKey, 0) != DemoRuleVersion)
        {
            demoCapstoneActive = false;
            demoGoldCompleted = demoGoldPayout = 0;
            titanActivationBaseline = jollyActivationBaseline = 0;
            PlayerPrefs.SetInt(DemoActiveKey, 0);
            PlayerPrefs.SetInt(DemoGoldKey, 0);
            PlayerPrefs.SetInt(DemoGoldPayoutKey, 0);
            PlayerPrefs.SetInt(DemoRuleVersionKey, DemoRuleVersion);
            PlayerPrefs.Save();
        }
        if (demoFinished) state = RoundState.Finished;
        if (demoEndOverlay != null) demoEndOverlay.SetActive(demoFinished);
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
        if (!demoCapstoneActive && skillTree != null &&
            skillTree.GetLevel("G08") > 0 && skillTree.GetLevel("F04") > 0)
            OnDemoCapstonePurchased();
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

    public void OnDemoCapstonePurchased()
    {
        // The finale begins only AFTER both late-game powers are owned.
        // Unlocking G08 early must not finish the demo before the player
        // experiences F04; the final Gold performance starts at this moment.
        if (!ScratchSkillTree.DemoEdition || demoCapstoneActive || demoFinished ||
            skillTree == null || skillTree.GetLevel("G08") == 0 ||
            skillTree.GetLevel("F04") == 0)
            return;
        demoCapstoneActive = true;
        demoGoldCompleted = demoGoldPayout = 0;
        demoExcludedTicketId = ticketActive ? currentTicketId : -1;
        titanActivationBaseline = skillTree.TitanActivationCount;
        jollyActivationBaseline = skillTree.JollyActivationCount;
        // F04 may have been bought first; ensure the finale is not left
        // depending on a 10% random joker or on keyboard-only SHIFT input.
        skillTree.RechargeFinalJollyGuarantees();
        PlayerPrefs.SetInt(DemoActiveKey, 1);
        PlayerPrefs.SetInt(DemoGoldKey, 0);
        PlayerPrefs.SetInt(DemoGoldPayoutKey, 0);
        PlayerPrefs.SetInt(DemoTitanBaseKey, titanActivationBaseline);
        PlayerPrefs.SetInt(DemoJollyBaseKey, jollyActivationBaseline);
        PlayerPrefs.SetInt(DemoRuleVersionKey, DemoRuleVersion);
        PlayerPrefs.Save();
        RefreshHud();
    }

    public void ResetDemoMilestone()
    {
        demoCapstoneActive = demoFinished = false;
        demoGoldCompleted = demoGoldPayout = titanActivationBaseline = jollyActivationBaseline = 0;
        demoExcludedTicketId = -1;
        targetedPreviewUsed = false;
        for (int tier = 0; tier < completedByTier.Length; tier++)
        {
            completedByTier[tier] = 0;
            PlayerPrefs.DeleteKey(RadialTicketPrefix + tier);
        }
        foreach (string key in new[] { DemoActiveKey, DemoGoldKey,
            DemoGoldPayoutKey, DemoTitanBaseKey, DemoJollyBaseKey, DemoDoneKey })
            PlayerPrefs.DeleteKey(key);
        if (state == RoundState.Finished) state = RoundState.Ready;
        if (demoEndOverlay != null) demoEndOverlay.SetActive(false);
        PlayerPrefs.SetInt(DemoRuleVersionKey, DemoRuleVersion);
        PlayerPrefs.Save();
        RefreshHud();
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
        targetedPreviewUsed = false;

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

        if (!symbolManager.GenerateRound(SymbolPoolSize(kind), kind))
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
        if (symbolManager.GuaranteedJollyThisTicket && kind == TicketKind.Gold)
            skillTree?.OnGoldenJollyTicketPurchased();
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

    public bool IsCellSelected(ScratchCell cell) =>
        cell != null && selectedCells.Contains(cell);

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

    /// <summary>
    /// C06: one player-chosen, truthful preview per ticket. Right-click on a
    /// covered unpreviewed cell; no scratch selection or hidden-board inspection.
    /// </summary>
    public bool TryInspectCell(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || targetedPreviewUsed ||
            Level(ScratchSkillTree.SkillId.C6) <= 0 || cell == null ||
            !cell.isActiveAndEnabled || cell.IsComplete || cell.IsPreviewVisible ||
            cell.RevealedPercentage > 0f ||
            (presentation != null && !presentation.CanScratch))
            return false;

        bool active = false;
        foreach (ScratchCell candidate in cellManager.ActiveCells)
            if (candidate == cell) { active = true; break; }
        if (!active || symbolManager == null || !symbolManager.TryGetSymbol(cell, out string symbol))
            return false;
        targetedPreviewUsed = true;
        cell.SetPreview(symbol);
        RefreshHud();
        return true;
    }

    public void OnCellCompleted(ScratchCell cell)
    {
        if (state != RoundState.Playing || !ticketActive || !selectedCells.Contains(cell) ||
            cell == null || !cell.IsComplete)
            return;
        // Complete() calls only once; duplicate callbacks after settlement cannot recharge.
        skillTree?.OnCompletedScratchCell();
        if (symbolManager != null && cell == symbolManager.JollyCell)
            skillTree?.OnJollyScratched();
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
                TicketPrize(currentTicketKind), TicketPrice(currentTicketKind));
        }
        else
        {
            ticketPayout = economyManager.SettleTicket(currentTicketId, settledMatch, fullyCompleted,
                TicketPrize(currentTicketKind), TicketPrice(currentTicketKind));
            if (fullyCompleted)
            {
                ticketsCompleted++;
                int tier = (int)currentTicketKind;
                if (tier >= 0 && tier < completedByTier.Length)
                {
                    completedByTier[tier]++;
                    PlayerPrefs.SetInt(RadialTicketPrefix + tier, completedByTier[tier]);
                    PlayerPrefs.Save();
                }
            }
        }
        if (!ticketVoided && fullyCompleted && currentTicketKind == TicketKind.Gold &&
            demoCapstoneActive && !demoFinished && currentTicketId != demoExcludedTicketId)
        {
            demoGoldCompleted++;
            demoGoldPayout = (int)Math.Min(int.MaxValue, (long)demoGoldPayout + Mathf.Max(0, ticketPayout));
            PlayerPrefs.SetInt(DemoGoldKey, demoGoldCompleted);
            PlayerPrefs.SetInt(DemoGoldPayoutKey, demoGoldPayout);
            // Three completed Gold tickets, actual earnings and at least one
            // real late-game activation: skill, not raw waiting, ends the demo.
            if (FinalChallengeComplete(demoGoldCompleted, demoGoldPayout,
                    DemoFinalPowerActivated))
            {
                demoFinished = true;
                state = RoundState.Finished;
                PlayerPrefs.SetInt(DemoDoneKey, 1);
                if (demoEndOverlay != null) demoEndOverlay.SetActive(true);
            }
            PlayerPrefs.Save();
        }
        RefreshHud();
    }

    private void ApplyPreviews()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetPreview(null);

        // C06 is now a separate manual right-click inspection, not five
        // randomly revealed symbols at ticket generation.
        int count = Level(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
            Level(ScratchSkillTree.SkillId.C2) > 0 ? 2 :
            Level(ScratchSkillTree.SkillId.C1) > 0 ? 1 : 0;

        // The joker preview from F04 is an EXTRA free preview. Never waste one
        // of the regular C-branch preview slots on the same cell.
        ScratchCell joker = symbolManager != null ? symbolManager.JollyCell : null;
        List<int> available = new List<int>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != joker) available.Add(i);
        for (int i = 0; i < Mathf.Min(count, cells.Length); i++)
        {
            int chosen = UnityEngine.Random.Range(0, available.Count);
            ScratchCell cell = cells[available[chosen]];
            available.RemoveAt(chosen);
            if (symbolManager.TryGetSymbol(cell, out string symbol))
                cell.SetPreview(symbol);
        }
        if (joker != null && symbolManager.GuaranteedJollyThisTicket)
            joker.SetPreview("JOLLY");
    }

    private void RefreshHud()
    {
        UpdateTimerText();
        if (scratchProgress != null)
        {
            scratchProgress.text = $"SCELTE {selectedCells.Count}/{MaxScratches}   |   GRATTATE {RevealedCount}/{MaxScratches}";
            if (skillTree != null && skillTree.GetLevel("G08") > 0)
                scratchProgress.text += "   |   SHIFT+GRATTA: TITANICO " + skillTree.TitanCharges;
        }

        if (roundActionLabel != null)
            roundActionLabel.text = state == RoundState.Ready ? "SI PARTE!"
                : state == RoundState.Finished ? "FINE DEMO"
                : ticketActive ? "PASSA OLTRE" : "SCEGLI IL BIGLIETTO";

        if (roundActionButton != null)
            roundActionButton.interactable = state == RoundState.Ready || ticketActive;

        if (matchResultText != null)
        {
            ScratchSymbolManager.MatchResult match = CurrentMatch;
            string win = CombinationName(match.Tier);
            matchResultText.text = state == RoundState.Finished
                ? "DEMO COMPLETATA! SEI APPENA DIVENTATO INARRESTABILE."
                : state == RoundState.Ready ? "PRONTO A FARE IL COLPACCIO?"
                : ticketActive
                    ? match.IsMatch ? $"{win} x{match.Multiplier} - {match.Symbol}" : "CERCA I SIMBOLI UGUALI!"
                : match.IsMatch ? $"{win} x{match.Multiplier} +{ticketPayout} MONETE"
                : ticketPayout > 0 ? $"PREMIO DI CONSOLAZIONE! +{ticketPayout}"
                : "PECCATO! RITENTA!";
        }

        if (boardSubtitle != null)
            boardSubtitle.text = state == RoundState.Finished
                ? "FINE DEMO - L'ALBERO COMPLETO TI ASPETTA NEL GIOCO FINALE!"
                : state == RoundState.Ready
                ? "Gratta senza tempo limite, migliora le abilita' e sblocca biglietti!"
                : ticketActive
                    ? ticketVoided ? "Questo biglietto di prova non assegna premi."
                    : demoCapstoneActive && !demoFinished
                        ? $"FINALE: {Mathf.Min(demoGoldCompleted, FinalGoldMinTickets)}/{FinalGoldMinTickets} DORATI | INCASSI {demoGoldPayout}/{FinalGoldPayoutTarget} | POTERE {(DemoFinalPowerActivated ? "OK" : "DA USARE")}"
                        : $"{TicketDisplayName(currentTicketKind)} {CurrentGridSide}x{CurrentGridSide} - {MaxScratches} caselle!" +
                          (Level(ScratchSkillTree.SkillId.C6) > 0
                              ? targetedPreviewUsed ? " | ISPEZIONE USATA" : " | DESTRO/CTRL+CLIC: ISPEZIONA"
                              : string.Empty)
                    : demoCapstoneActive && !demoFinished
                        ? $"FINALE: {Mathf.Min(demoGoldCompleted, FinalGoldMinTickets)}/{FinalGoldMinTickets} DORATI | INCASSI {demoGoldPayout}/{FinalGoldPayoutTarget} | POTERE {(DemoFinalPowerActivated ? "OK" : "DA USARE")}"
                        : $"+{ticketPayout} monete! Scegli il prossimo biglietto o apri i potenziamenti.";

        Changed?.Invoke();
    }

    private void UpdateTimerText()
    {
        // The former timer panel has become a scene-authored shop button.
        if (timerValue != null) timerValue.text = "APRI SHOP";
    }


}
