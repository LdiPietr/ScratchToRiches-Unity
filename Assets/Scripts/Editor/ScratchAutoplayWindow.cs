#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Editor-only end-to-end QA: actual ticket input path, UI hold-to-buy, unlocks,
/// settlements and repeatable human-paced pacing. Does not implement gameplay automation.
/// Requires the game scene to be running in Play Mode.
/// </summary>
public sealed partial class ScratchAutoplayWindow : EditorWindow
{
    private enum Phase
    {
        Idle, WaitLive, Scratch, WaitChoice, OpenShop, WaitShop,
        StartPurchase, WaitPurchase, CloseShop, WaitShopClosed, WaitFarmShopClosed
    }

    // A real-play simulation must never manufacture winning combinations.
    // Forced QA is intentionally repetitive to assert all payout rules.
    private enum AutoplayMode { SimulazioneReale, QACombinazioniForzate, ProgressioneDemoCompleta }

    private static readonly ScratchSymbolManager.TestPattern[][] TestPatterns =
    {
        // Open each tier with a REAL strategic ticket so newly unlocked
        // previews are used immediately and visible in the captured video.
        // Deterministic forced-combination assertions follow afterwards.
        new[] { ScratchSymbolManager.TestPattern.Random, ScratchSymbolManager.TestPattern.Pair,
            ScratchSymbolManager.TestPattern.Triple, ScratchSymbolManager.TestPattern.Random },
        new[] { ScratchSymbolManager.TestPattern.Random, ScratchSymbolManager.TestPattern.Pair,
            ScratchSymbolManager.TestPattern.Triple, ScratchSymbolManager.TestPattern.Poker },
        new[] { ScratchSymbolManager.TestPattern.Random, ScratchSymbolManager.TestPattern.Pair,
            ScratchSymbolManager.TestPattern.Triple, ScratchSymbolManager.TestPattern.Poker,
            ScratchSymbolManager.TestPattern.Five },
        new[] { ScratchSymbolManager.TestPattern.Random, ScratchSymbolManager.TestPattern.Pair,
            ScratchSymbolManager.TestPattern.Triple, ScratchSymbolManager.TestPattern.Poker,
            ScratchSymbolManager.TestPattern.Five }
    };

    private static readonly ScratchSkillTree.SkillId[][] UnlockPlans =
    {
        Array.Empty<ScratchSkillTree.SkillId>(),
        new[] { ScratchSkillTree.SkillId.G1, ScratchSkillTree.SkillId.G1,
            ScratchSkillTree.SkillId.G2, ScratchSkillTree.SkillId.G3,
            ScratchSkillTree.SkillId.C1, ScratchSkillTree.SkillId.C11,
            ScratchSkillTree.SkillId.C3, ScratchSkillTree.SkillId.C11,
            ScratchSkillTree.SkillId.C2 },
        // G04/G07 are not yet implemented in the scene; this legacy functional
        // tester covers ticket mechanics, not the new demo capstone.
        new[] { ScratchSkillTree.SkillId.C4, ScratchSkillTree.SkillId.C5 },
        new[] { ScratchSkillTree.SkillId.C6, ScratchSkillTree.SkillId.E1,
            ScratchSkillTree.SkillId.E1, ScratchSkillTree.SkillId.E1,
            ScratchSkillTree.SkillId.E2, ScratchSkillTree.SkillId.E3,
            ScratchSkillTree.SkillId.E4, ScratchSkillTree.SkillId.E5,
            ScratchSkillTree.SkillId.C7 }
    };

    // A complete, legal player route through every demo-eligible skill, including
    // the minimum extra levels required by prerequisites. No QA coins or forced wins.
    // G08 is now acquired shortly after Gold so it can be USED on many
    // tickets. F04 comes last and starts the authored performance finale.
    // C04 is delayed until G07, well after the two-preview phase.
    private static readonly ScratchSkillTree.SkillId[] FullDemoPlan =
    {
        ScratchSkillTree.SkillId.E1, ScratchSkillTree.SkillId.G1,
        ScratchSkillTree.SkillId.C1, ScratchSkillTree.SkillId.E1,
        ScratchSkillTree.SkillId.G1, ScratchSkillTree.SkillId.E5,
        ScratchSkillTree.SkillId.G2, ScratchSkillTree.SkillId.C11,
        ScratchSkillTree.SkillId.E2, ScratchSkillTree.SkillId.G3,
        ScratchSkillTree.SkillId.C3, ScratchSkillTree.SkillId.C11,
        ScratchSkillTree.SkillId.G5, ScratchSkillTree.SkillId.G4,
        ScratchSkillTree.SkillId.E3, ScratchSkillTree.SkillId.F1,
        ScratchSkillTree.SkillId.C2, ScratchSkillTree.SkillId.G6,
        ScratchSkillTree.SkillId.E4, ScratchSkillTree.SkillId.E6,
        ScratchSkillTree.SkillId.G7, ScratchSkillTree.SkillId.C4,
        ScratchSkillTree.SkillId.C5, ScratchSkillTree.SkillId.G8,
        ScratchSkillTree.SkillId.F2, ScratchSkillTree.SkillId.C6,
        ScratchSkillTree.SkillId.F3, ScratchSkillTree.SkillId.F4
    };

    private bool IsFullDemo => autoplayMode == AutoplayMode.ProgressioneDemoCompleta;
    private ScratchSkillTree.SkillId[] ActivePurchasePlan =>
        IsFullDemo ? FullDemoPlan : UnlockPlans[stage];

    private ScratchGameManager game;
    private ScratchSkillTree skills;
    private ScratchEconomyManager economy;
    private ScratchCellManager cellManager;
    private ScratchSymbolManager symbols;
    private ScratchFlowPresentation flow;
    private ScratchSkillTreeUI treeUI;
    private ScratchHoldToBuy[] holdButtons;
    private ScratchCell activeCell;
    private RawImage activeCoating;
    private Canvas parentCanvas;
    private PointerEventData pointer;
    private System.Random random;
    private ScratchHoldToBuy pressedHold;

    private Phase phase;
    private bool running;
    private AutoplayMode autoplayMode = AutoplayMode.ProgressioneDemoCompleta;
    private bool qaFunds;
    private bool startFresh = true;
    private bool freshSeedEachRun = true;
    private int actualSeed;
    private ScratchSymbolManager.TestPattern activePattern = ScratchSymbolManager.TestPattern.Random;
    private readonly int[] observedRandomResults = new int[5];
    private readonly HashSet<string> uniqueFirstChoices = new HashSet<string>();
    private int qaTitanActivations;
    private int qaC06Inspections;
    private bool titanActivatedThisTicket;
    private int naturalTicketsPlayed;
    private int forcedTicketsPlayed;
    private bool captureFrames;
    private bool recordVideo;
    private bool recordGameAudio;
    private bool videoValidationPending;
    private int videoHeight = 720;
    private int videoFrameRate = 30;
    private double videoValidationDeadline;
    private string videoStatus = "Non richiesto";
    private string videoOutputPath = string.Empty;
    private ScratchAutoplayMovieRecorder movieRecorder;
    private bool realtimeCheckPassed;
    private bool pointerHeld;
    private bool farming;
    // True when the unlocked next ticket exists but the wallet cannot pay its entry price.
    // False during ordinary skill-price farming.
    private bool farmingForTicket;
    private int farmTickets;
    private int informedChoices;
    private int previewChoices;
    private int deliberatelySkippedPreviews;
    private int previewMatchingChoices;
    private int randomExplorationChoices;
    private double lastQaHoldTick;
    private float humanSpeed = 1f;
    private float nextAction;
    private float stateSince;
    private float startedAt;
    private float ticketStartedAt;
    private float cellStartedAt;
    private float lastScreenshotAt;
    private float maxSessionMinutes = 120f;
    private int seed = 20261122;
    private int framesPerSecond = 3;
    private int frameCount;
    private int stage;
    private int patternIndex;
    private int purchaseIndex;
    private int strokePointIndex;
    private int cellIndex;
    private int tickets;
    private int assertionsPassed;
    private int assertionsFailed;
    private int ticketCost;
    private int walletBeforeTicket;
    private int expectedSkillLevel;
    private int pendingPurchasePrice;
    private int streakBeforeTicket;
    private float lastUpgradeAt;
    private readonly List<string> progression = new List<string>();
    private readonly Dictionary<string, float> milestoneSeconds = new Dictionary<string, float>();
    private string lastStatus = "Avvia Play Mode, poi START TEST.";
    private string reportDirectory;
    private readonly List<string> ledger = new List<string>();
    private readonly List<string> checks = new List<string>();
}
#endif
