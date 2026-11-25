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
public sealed partial class ScratchGameManager : MonoBehaviour
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
    private const int DemoRuleVersion = 3;
    private const int FinalGoldMinTickets = 8;
    private const int FinalGoldPayoutTarget = 3800;
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
    public bool TicketVoided => ticketVoided;
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
    public bool WaitingForTicketChoice => !ticketActive &&
        (state == RoundState.Ready || state == RoundState.Playing);

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

    public void ShowDemoEnding()
    {
        if (demoFinished && demoEndOverlay != null) demoEndOverlay.SetActive(true);
    }

    public void OnRoundAction()
    {
        if (presentation != null && presentation.ShopOpen) return;
        // Tickets finish automatically after every selected cell is scratched.
        // Early exit used to leave chapter counters unchanged while paying
        // partial winnings, misleading human playtesters about progression.
        if (state == RoundState.Ready) StartNewRun();
    }
}
