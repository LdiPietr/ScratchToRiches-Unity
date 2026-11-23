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
public sealed class ScratchAutoplayWindow : EditorWindow
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

    [MenuItem("Tools/Scratch to Riches/Autoplay QA (test automatico)")]
    private static void ShowWindow()
    {
        GetWindow<ScratchAutoplayWindow>("STR Autoplay QA").minSize = new Vector2(390, 460);
    }

    [MenuItem("Tools/Scratch to Riches/Reset progressi V2 (fuori Play)")]
    private static void ResetProgressMenu()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reset progressi", "Esci da Play Mode prima di resettare: i manager devono ricaricare i dati salvati.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Reset Scratch to Riches V2",
                "Eliminare tutte le monete e i livelli delle 90 abilita' salvati? Questa azione e' irreversibile.", "RESET", "Annulla"))
            return;

        PlayerPrefs.DeleteKey("STR_M5_Wallet");
        PlayerPrefs.DeleteKey("STR_M5_TotalEarned");
        for (int i = 0; i < 32; i++)
        {
            PlayerPrefs.DeleteKey("STR_V2_Skill_" + i);
            PlayerPrefs.DeleteKey("STR_M5_Skill_" + i);
        }
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            PlayerPrefs.DeleteKey("STR_GDDV2_Skill_" + entry.id);
        foreach (string key in new[]
        {
            "STR_GDDV2_Migrated_01", "STR_GDDV2_TitanCharges",
            "STR_GDDV2_TitanProgress", "STR_GDDV2_TitanUsed",
            "STR_GDDV2_JollyGoldBonus", "STR_GDDV2_JollyUsed",
            "STR_GDDV2_WinStreak", "STR_GDDV2_DemoCapstoneActive",
            "STR_GDDV2_DemoGoldTickets", "STR_GDDV2_DemoTitanBaseline",
            "STR_GDDV2_DemoJollyBaseline", "STR_GDDV2_DemoFinished"
        })
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log("STR V2: wallet e 90 abilita' resettati. Avvia Play Mode per iniziare da zero.");
    }

    private void OnEnable()
    {
        EditorApplication.update -= UpdateVideoValidation;
        EditorApplication.update += UpdateTest;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateTest;
        if (running) Finish(false, "Tester chiuso prima del termine.");
        // MP4 finalization can outlive the window: finish validation and update summary.
        if (videoValidationPending)
        {
            EditorApplication.update -= UpdateVideoValidation;
            EditorApplication.update += UpdateVideoValidation;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SCRATCH TO RICHES - AUTOPLAY DEMO V2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(IsFullDemo
            ? "TEST DEMO COMPLETA: parte da zero, acquista tutte le 24 abilita' della demo " +
              "(incluse le dipendenze), gratta biglietti reali fino al finale G08/F04 " +
              "e misura la durata effettiva contro l'obiettivo di 20-25 minuti."
            : "TEST LEGACY: verifica separatamente le tre categorie di biglietti con acquisti e grattate UI.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(running))
        {
            autoplayMode = (AutoplayMode)EditorGUILayout.EnumPopup("Tipo di test", autoplayMode);
            if (IsFullDemo) { qaFunds = false; startFresh = true; }
            using (new EditorGUI.DisabledScope(IsFullDemo))
            {
                qaFunds = EditorGUILayout.ToggleLeft("Credito QA per gli sblocchi (100.000 monete)", qaFunds);
                startFresh = EditorGUILayout.ToggleLeft("Azzera progressi prima del test (solo Play Mode)", startFresh);
            }
            humanSpeed = EditorGUILayout.Slider("Velocita' gesti (1 = umano)", humanSpeed, 0.5f, 4f);
            freshSeedEachRun = EditorGUILayout.ToggleLeft("Nuova partita casuale a ogni avvio", freshSeedEachRun);
            using (new EditorGUI.DisabledScope(freshSeedEachRun))
                seed = EditorGUILayout.IntField("Seed riproducibile", seed);
            maxSessionMinutes = EditorGUILayout.Slider("Timeout massimo (min)", maxSessionMinutes, 5f, 240f);
            recordVideo = EditorGUILayout.ToggleLeft("REGISTRA VIDEO MP4 H.264 (Unity Recorder)", recordVideo);
            if (recordVideo)
            {
                videoHeight = EditorGUILayout.IntPopup("Risoluzione MP4", videoHeight,
                    new[] { "HD 1280x720", "Full HD 1920x1080" }, new[] { 720, 1080 });
                videoFrameRate = EditorGUILayout.IntPopup("FPS MP4", videoFrameRate,
                    new[] { "30 (consigliato)", "60" }, new[] { 30, 60 });
                recordGameAudio = EditorGUILayout.ToggleLeft("Includi audio del gioco", recordGameAudio);
            }
            captureFrames = EditorGUILayout.ToggleLeft("Salva anche PNG di diagnostica (opzionale)", captureFrames);
            if (captureFrames)
                framesPerSecond = EditorGUILayout.IntSlider("FPS PNG", framesPerSecond, 1, 5);
        }

        if (autoplayMode == AutoplayMode.SimulazioneReale)
            EditorGUILayout.HelpBox("SIMULAZIONE REALE: tutti i biglietti hanno simboli casuali. " +
                "Il bot sceglie usando SOLO grattate e anteprime disponibili; nessun tris, poker o premio viene forzato. " +
                "Gli sblocchi seguono ancora un piano di test prestabilito.", MessageType.Info);
        else if (autoplayMode == AutoplayMode.QACombinazioniForzate)
            EditorGUILayout.HelpBox("QA COMBINAZIONI FORZATE: alcune partite impongono coppia, tris, poker e cinquina " +
                "e selezionano le stesse posizioni per verificare i pagamenti. La ripetizione e' INTENZIONALE e non " +
                "misura la fortuna naturale del gioco.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("NESSUN CHEAT: progressi e portafoglio azzerati; 24 skill demo; " +
                "simboli casuali; scelte solo da grattate e anteprime visibili; " +
                "finale con tre veri Dorati e almeno un JOLLY grattato. " +
                "Il test misura la durata, non la forza artificialmente.", MessageType.Info);

        if (qaFunds)
            EditorGUILayout.HelpBox("Credito QA: sblocchi rapidi. Non misura l'economia naturale e le monete restano nel wallet fino al reset.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Senza credito QA il tester grattera' altri biglietti per finanziare ogni acquisto. Potrebbero servire molte ore: aumenta il timeout massimo.", MessageType.Info);
        if (recordVideo)
            EditorGUILayout.HelpBox("Il MP4 viene creato direttamente con Unity Recorder 5.1.8. Nessun FFmpeg richiesto. " +
                "Registra la Game View dall'inizio fino al completamento o all'errore; il file viene finalizzato alla chiusura del test.", MessageType.None);
        if (captureFrames)
            EditorGUILayout.HelpBox("I PNG sono soltanto materiale diagnostico aggiuntivo e possono occupare molto spazio.", MessageType.None);

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || running))
        {
            if (GUILayout.Button("START - TEST COMPLETO", GUILayout.Height(34)))
                StartTest();
        }
        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button("STOP E SALVA REPORT", GUILayout.Height(24)))
                Finish(false, "Interrotto manualmente.");
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Stato: " + lastStatus, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(IsFullDemo
            ? "Biglietti: " + (tickets + farmTickets) + " | Skill acquistate: " +
              purchaseIndex + "/" + FullDemoPlan.Length + " | Gold finale: " +
              (game != null ? game.DemoFinalGoldTickets : 0) + "/" +
              (game != null ? game.DemoFinalGoldMinTickets : 8)
            : "Biglietti test: " + tickets + " / 13  |  Farm: " + farmTickets);
        EditorGUILayout.LabelField("Partite casuali: " + naturalTicketsPlayed +
            " | QA forzate: " + forcedTicketsPlayed);
        EditorGUILayout.LabelField("Primi movimenti diversi: " + uniqueFirstChoices.Count);
        EditorGUILayout.LabelField("Scelte ragionate: " + informedChoices + " | Anteprime usate: " + previewChoices);
        EditorGUILayout.LabelField("Verifiche OK: " + assertionsPassed + "    Errori: " + assertionsFailed);
        EditorGUILayout.LabelField("Video: " + videoStatus, EditorStyles.wordWrappedLabel);
        if (running)
            EditorGUILayout.LabelField(IsFullDemo
                ? "Fase: " + phase + " | " + (ScratchGameManager.TicketKind)stage +
                  " | Upgrade " + (purchaseIndex + 1) + "/" + FullDemoPlan.Length
                : "Fase: " + phase + " | Tier " + (stage + 1) + " | Test " + (patternIndex + 1));
        if (!string.IsNullOrEmpty(reportDirectory))
        {
            if (GUILayout.Button("Apri cartella registrazioni / report"))
                EditorUtility.RevealInFinder(reportDirectory);
            EditorGUILayout.SelectableLabel(reportDirectory, GUILayout.Height(36));
        }

        GUILayout.Space(6);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("RESET COMPLETO PROGRESSI (fuori Play)"))
                ResetProgressMenu();
        }
    }

    public static bool AnyTestRunning
    {
        get
        {
            foreach (ScratchAutoplayWindow window in Resources.FindObjectsOfTypeAll<ScratchAutoplayWindow>())
                if (window != null && window.running) return true;
            return false;
        }
    }

    private void StartTest()
    {
        if (ScratchManualTelemetry.IsRecording)
        {
            EditorUtility.DisplayDialog("Autoplay", "Ferma prima la registrazione manuale.", "OK");
            return;
        }
        game = UnityEngine.Object.FindAnyObjectByType<ScratchGameManager>();
        // A previous completed demo may load in Finished state. Full-demo
        // replay is allowed because the confirmed reset below also clears
        // the authored final milestone and restores Ready state.
        bool completedSavedDemo = IsFullDemo && game != null &&
            game.State == ScratchGameManager.RoundState.Finished;
        if (game == null || (game.State != ScratchGameManager.RoundState.Ready &&
                             !completedSavedDemo))
        {
            EditorUtility.DisplayDialog("Autoplay", "Avvia la scena in Play Mode e lascia il gioco prima di SI PARTE! (stato Ready).", "OK");
            return;
        }
        skills = game.GetComponent<ScratchSkillTree>();
        economy = game.GetComponent<ScratchEconomyManager>();
        cellManager = game.GetComponent<ScratchCellManager>();
        symbols = game.GetComponent<ScratchSymbolManager>();
        flow = game.GetComponent<ScratchFlowPresentation>();
        treeUI = game.GetComponent<ScratchSkillTreeUI>();
        holdButtons = game.GetComponentsInChildren<ScratchHoldToBuy>(true);
        parentCanvas = game.GetComponent<Canvas>();
        if (skills == null || economy == null || cellManager == null || symbols == null ||
            flow == null || treeUI == null || parentCanvas == null ||
            Array.FindAll(holdButtons, node => node != null &&
                !string.IsNullOrEmpty(node.StableSkillId)).Length != 90 ||
            EventSystem.current == null)
        {
            EditorUtility.DisplayDialog("Autoplay", "Scene references incomplete. Run the V2 Scene Audit first.", "OK");
            return;
        }

        if (IsFullDemo)
        {
            qaFunds = false;
            startFresh = true;
            if (!ValidateFullDemoPlan(out string planError))
            {
                EditorUtility.DisplayDialog("Autoplay", "Piano della demo non valido: " + planError, "OK");
                return;
            }
        }

        if (startFresh && !EditorUtility.DisplayDialog("Test da zero",
                "Il test azzerera' il portafoglio e tutti i livelli V2 correnti. Proseguire?", "Procedi", "Annulla"))
            return;

        if (startFresh)
        {
            skills.ResetProgress();
            if (economy.Coins != 0 ||
                skills.GetLevel(ScratchSkillTree.SkillId.C3) != 0 ||
                skills.GetLevel(ScratchSkillTree.SkillId.C5) != 0 ||
                skills.GetLevel(ScratchSkillTree.SkillId.C7) != 0)
            {
                EditorUtility.DisplayDialog("Autoplay", "Reset iniziale non riuscito: controlla i progressi salvati.", "OK");
                return;
            }
        }
        // A fresh run uses a fresh seed. Disable this only to reproduce bugs
        // from the seed recorded in summary.txt/tickets.csv.
        actualSeed = freshSeedEachRun ? Guid.NewGuid().GetHashCode() : seed;
        random = new System.Random(actualSeed);
        UnityEngine.Random.InitState(actualSeed);
        phase = Phase.WaitLive;
        stage = patternIndex = purchaseIndex = cellIndex = strokePointIndex = tickets = 0;
        assertionsPassed = assertionsFailed = frameCount = 0;
        realtimeCheckPassed = pointerHeld = farming = farmingForTicket = false;
        movieRecorder = null;
        videoValidationPending = false;
        videoStatus = recordVideo ? "Avvio in corso" : "Non richiesto";
        videoOutputPath = string.Empty;
        farmTickets = informedChoices = previewChoices = previewMatchingChoices = randomExplorationChoices = 0;
        deliberatelySkippedPreviews = 0;
        naturalTicketsPlayed = forcedTicketsPlayed = 0;
        qaTitanActivations = qaC06Inspections = 0;
        titanActivatedThisTicket = false;
        Array.Clear(observedRandomResults, 0, observedRandomResults.Length);
        uniqueFirstChoices.Clear();
        activePattern = ScratchSymbolManager.TestPattern.Random;
        lastQaHoldTick = 0d;
        pointer = null;
        activeCell = null;
        pressedHold = null;
        ticketCost = 0;
        pendingPurchasePrice = 0;
        streakBeforeTicket = 0;
        startedAt = Time.realtimeSinceStartup;
        lastUpgradeAt = startedAt;
        lastScreenshotAt = startedAt;
        ledger.Clear();
        checks.Clear();
        progression.Clear();
        milestoneSeconds.Clear();
        reportDirectory = Path.Combine(Application.persistentDataPath, "AutoplayTests",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(reportDirectory);
        if (captureFrames) Directory.CreateDirectory(Path.Combine(reportDirectory, "frames"));
        ledger.Add("timestamp;event;ticket;kind;pattern;wallet;cost;payout;match;details");
        checks.Add("timestamp;result;check;details");
        progression.Add("elapsed_s;event;ticket;kind;wallet;earned;skill_id;level;price;details");
        ProgressEvent("START", "-", 0, 0, "Fresh natural run");
        running = true;
        Note("START", "mode=" + autoplayMode + ";seed=" + actualSeed +
            ";freshSeed=" + freshSeedEachRun + ";humanSpeed=" + humanSpeed +
            ";QA_credit=" + qaFunds);
        if (qaFunds)
        {
            int credited = economy.CreditForAutoplayTest(100000);
            Note("QA_CREDIT", "credited=" + credited + ";not_gameplay_income");
        }

        if (recordVideo)
        {
            movieRecorder = new ScratchAutoplayMovieRecorder();
            if (!movieRecorder.Start(reportDirectory, videoHeight, videoFrameRate, recordGameAudio))
            {
                videoStatus = "ERRORE avvio: " + movieRecorder.LastError;
                Fail(videoStatus);
                return;
            }
            videoOutputPath = movieRecorder.OutputPath;
            videoStatus = "Registrazione H.264 in corso";
            Note("VIDEO_START", "path=" + videoOutputPath + ";fps=" + videoFrameRate +
                ";resolution=" + videoHeight + "p;audio=" + recordGameAudio);
        }

        SetPattern(PlannedPattern(0, 0));
        game.StartNewRun();
        walletBeforeTicket = economy.Coins;
        ticketCost = 0;
        ChangePhase(Phase.WaitLive, 0f);
        lastStatus = "Test avviato sul Classico.";
        Repaint();
    }

    private void UpdateTest()
    {
        UpdateVideoValidation();
        if (!running) return;
        float now = Time.realtimeSinceStartup;
        if (!EditorApplication.isPlaying || game == null)
        {
            Finish(false, "Play Mode terminato.");
            return;
        }
        if (now - startedAt > maxSessionMinutes * 60f)
        {
            Fail("Timeout generale: " + maxSessionMinutes + " minuti.");
            return;
        }
        // The demo is designed for 20-25 minutes. Eight real minutes with no
        // successful purchase indicates a progression deadlock / bad economy,
        // not meaningful gameplay. Stop and report rather than farm forever.
        if (IsFullDemo && purchaseIndex < FullDemoPlan.Length &&
            now - lastUpgradeAt > 8f * 60f)
        {
            Fail("Progressione bloccata: nessun upgrade acquistato da oltre 8 minuti. " +
                "Prossimo=" + FullDemoPlan[purchaseIndex] +
                ";wallet=" + economy.Coins + ";target=" + FundsNeededForNextPurchase() +
                ";biglietti=" + (tickets + farmTickets));
            return;
        }
        if (IsFullDemo && purchaseIndex >= FullDemoPlan.Length && !game.DemoFinished &&
            now - lastUpgradeAt > 8f * 60f)
        {
            Fail("Finale demo non raggiunto entro 5 minuti dall'ultima abilita'. " +
                "Dorati finali=" + game.DemoFinalGoldTickets +
                ";incassi=" + game.DemoFinalGoldPayout + "/" + game.DemoFinalGoldPayoutTarget +
                ";potere=" + game.DemoFinalPowerActivated +
                ";wallet=" + economy.Coins);
            return;
        }
        if (now - stateSince > 60f)
        {
            Fail("Timeout fase " + phase + " (possibile blocco UI).");
            return;
        }
        if (!realtimeCheckPassed && now - startedAt > 21f)
        {
            realtimeCheckPassed = true;
            Check(game.State == ScratchGameManager.RoundState.Playing, "no_timer",
                "Partita ancora in corso dopo oltre 20 secondi reali.");
        }
        CaptureFrame(now);
        if (now < nextAction) return;
        try
        {
        switch (phase)
        {
            case Phase.WaitLive: WaitLive(now); break;
            case Phase.Scratch: ScratchTick(now); break;
            case Phase.WaitChoice: WaitChoice(now); break;
            case Phase.OpenShop: BeginShop(now); break;
            case Phase.WaitShop: AwaitShop(now); break;
            case Phase.StartPurchase: StartPurchase(now); break;
            case Phase.WaitPurchase: EndPurchase(now); break;
            case Phase.CloseShop: CloseShop(now); break;
            case Phase.WaitShopClosed: AwaitShopClosed(now); break;
            case Phase.WaitFarmShopClosed: WaitFarmShopClosed(now); break;
        }
        }
        catch (Exception ex)
        {
            Fail("Eccezione durante il test: " + ex);
        }
        if (((int)(now * 3)) % 3 == 0) Repaint();
    }

    private void WaitLive(float now)
    {
        if (!game.TicketActive || !flow.CanScratch) return;
        ScratchGameManager.TicketKind expected = farming
            ? game.CurrentTicketKind
            : (ScratchGameManager.TicketKind)stage;
        Check(game.CurrentTicketKind == expected, "ticket_kind",
            "Atteso=" + expected + " effettivo=" + game.CurrentTicketKind);
        if (autoplayMode != AutoplayMode.QACombinazioniForzate)
            Check(activePattern == ScratchSymbolManager.TestPattern.Random,
                "no_forced_outcome_in_real_play", "Current symbol pattern: " + activePattern);
        Check(cellManager.ActiveCells.Length == game.CurrentGridSide * game.CurrentGridSide,
            "grid_size", game.CurrentGridSide + "x" + game.CurrentGridSide);
        Check(game.MaxScratches == ScratchGameManager.PickLimitFromPerk(skills.GetLevel("C11")),
            "global_pick_perk",
            "Scelte indipendenti dal ticket: " + game.MaxScratches +
            " | C11=" + skills.GetLevel("C11") +
            " | griglia=" + game.CurrentGridSide);
        if (stage == 3 && !farming)
        {
            HashSet<string> unique = new HashSet<string>();
            foreach (ScratchCell sc in cellManager.ActiveCells)
                if (symbols.TryGetSymbol(sc, out string label)) unique.Add(label);
            Check(unique.Count <= 4 && unique.Count > 0,
                "diamond_pool", "Simboli diversi sul biglietto=" + unique.Count);
        }
        int expectedPreviews = skills.GetLevel(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
            skills.GetLevel(ScratchSkillTree.SkillId.C2) > 0 ? 2 :
            skills.GetLevel(ScratchSkillTree.SkillId.C1) > 0 ? 1 : 0;
        int visible = 0;
        int unknownLeaked = 0;
        int incompleteMasks = 0;
        foreach (ScratchCell cell in cellManager.ActiveCells)
        {
            bool canRead = cell.TryGetVisibleSymbol(out _);
            if (cell.IsPreviewVisible) visible++;
            else if (!cell.IsComplete && canRead) unknownLeaked++;
            RawImage coating = cell.GetComponentInChildren<RawImage>(true);
            if (coating == null || !coating.enabled || coating.texture == null ||
                cell.RevealedPercentage > 0f)
                incompleteMasks++;
        }
        if (skills.GetLevel("F04") > 0 && symbols.GuaranteedJollyThisTicket &&
            symbols.JollyCell != null)
            expectedPreviews++;
        Check(visible == expectedPreviews, "preview_count",
            "Anteprime attese=" + expectedPreviews + ", viste=" + visible);
        Check(unknownLeaked == 0, "no_hidden_symbol_leak",
            "Simboli segreti leggibili in caselle non grattate=" + unknownLeaked);
        Check(incompleteMasks == 0, "fresh_coating",
            "Patine mancanti, disabilitate o gia' grattate=" + incompleteMasks);
        Note("PREVIEWS_VISIBLE", "count=" + visible + ";previewSkills=" + expectedPreviews);
        // C06 is now an intentional RIGHT-CLICK on one unpreviewed cell, not
        // two extra automatic previews. Check actual pointer handling.
        if (IsFullDemo && skills.GetLevel("C06") > 0)
        {
            ScratchCell inspectionTarget = null;
            ScratchCell rejectionTarget = null;
            foreach (ScratchCell candidate in cellManager.ActiveCells)
            {
                if (candidate.IsComplete || candidate.IsPreviewVisible) continue;
                if (inspectionTarget == null) inspectionTarget = candidate;
                else if (rejectionTarget == null) rejectionTarget = candidate;
            }
            if (inspectionTarget == null || rejectionTarget == null)
            {
                Fail("Non ci sono due caselle coperte con cui verificare C06.");
                return;
            }
            int selectedBefore = game.SelectedCount;
            int revealedBefore = game.RevealedCount;
            PointerEventData inspect = new PointerEventData(EventSystem.current)
            {
                pointerId = -810, button = PointerEventData.InputButton.Right
            };
            inspectionTarget.OnPointerDown(inspect);
            bool inspected = inspectionTarget.IsPreviewVisible &&
                game.InspectionUsedThisTicket && game.SelectedCount == selectedBefore &&
                game.RevealedCount == revealedBefore &&
                inspectionTarget.RevealedPercentage == 0f;
            Check(inspected, "C06_chosen_preview",
                "Ispezione su " + inspectionTarget.name + ", senza grattare o consumare scelte.");
            rejectionTarget.OnPointerDown(inspect);
            Check(!rejectionTarget.IsPreviewVisible &&
                game.SelectedCount == selectedBefore &&
                game.RevealedCount == revealedBefore,
                "C06_once_per_ticket", "La seconda ispezione viene rifiutata.");
            if (inspected)
            {
                qaC06Inspections++;
                Note("C06_INSPECTED", "cell=" + inspectionTarget.name);
            }
        }
        titanActivatedThisTicket = false;
        ticketStartedAt = now;
        streakBeforeTicket = economy.ConsecutiveWinningTickets;
        cellIndex = 0;
        PrepareCell();
        Note("TICKET_START", game.CurrentGridSide + "x" + game.CurrentGridSide);
        ChangePhase(Phase.Scratch, 0.5f / humanSpeed);
    }

    /// <summary>
    /// Only functional-QA mode uses manufactured combinations. Real simulations
    /// always select Random and can earn any result (including no win).
    /// </summary>
    private ScratchSymbolManager.TestPattern PlannedPattern(int tierIndex, int ticketIndex) =>
        autoplayMode != AutoplayMode.QACombinazioniForzate
            ? ScratchSymbolManager.TestPattern.Random
            : TestPatterns[tierIndex][ticketIndex];

    // Values reflect the actual payout formula, including economic upgrades
    // and the E5 consolation prize. Ticket cost is already sunk at this point.
    private double[] PayoutByMultiplicity()
    {
        double rewardBase = game.TicketPrize(game.CurrentTicketKind) +
            2 * skills.GetLevel(ScratchSkillTree.SkillId.E1);
        int e5 = skills.GetLevel(ScratchSkillTree.SkillId.E5);
        double consolation = e5 > 0 ? 3 * e5 +
            Math.Ceiling(Math.Max(0, ticketCost) * 0.2) : 0;
        double pair = Math.Ceiling(rewardBase * 2 *
            (1.0 + 0.1 * skills.GetLevel(ScratchSkillTree.SkillId.E2)));
        double triple = Math.Ceiling(rewardBase * 3 *
            (1.0 + 0.15 * skills.GetLevel(ScratchSkillTree.SkillId.E3)));
        double betterBonus = 1.0 + 0.2 * skills.GetLevel(ScratchSkillTree.SkillId.E4);
        double poker = Math.Ceiling(rewardBase * 4 * betterBonus);
        double five = Math.Ceiling(rewardBase * 5 * betterBonus);
        if (skills.GetLevel(ScratchSkillTree.SkillId.E6) > 0 && streakBeforeTicket > 0)
        {
            double streakBonus = 1.0 + 0.05 * Mathf.Min(4, streakBeforeTicket);
            pair = Math.Ceiling(pair * streakBonus);
            triple = Math.Ceiling(triple * streakBonus);
            poker = Math.Ceiling(poker * streakBonus);
            five = Math.Ceiling(five * streakBonus);
        }
        return new[] { consolation, consolation, pair, triple, poker, five };
    }

    private static string EvText(double value) => double.IsNegativeInfinity(value) ? "n/a" :
        value.ToString("F3", CultureInfo.InvariantCulture);

    private void PrepareCell()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        bool strategic = activePattern == ScratchSymbolManager.TestPattern.Random;

        if (strategic)
        {
            // F04 shows its guaranteed joker to the player. Choose it by
            // inspecting ONLY player-visible previews, never hidden symbol data.
            ScratchCell visibleJolly = null;
            if (IsFullDemo)
                foreach (ScratchCell candidate in cells)
                    if (!candidate.IsComplete && candidate.IsPreviewVisible &&
                        candidate.TryGetVisibleSymbol(out string visible) && visible == "JOLLY")
                    {
                        visibleJolly = candidate;
                        break;
                    }
            if (visibleJolly != null)
            {
                activeCell = visibleJolly;
                informedChoices++;
                previewChoices++;
                Note("CELL_CHOICE", "mode=VISIBLE_JOLLY;cell=" + activeCell.name);
            }
            else
            {
            int remaining = game.MaxScratches - game.RevealedCount;
            ScratchAutoplayPreviewStrategy.Choice choice =
                ScratchAutoplayPreviewStrategy.Choose(cells, remaining,
                    ScratchGameManager.SymbolPoolSize(game.CurrentTicketKind),
                    PayoutByMultiplicity(), random);
            activeCell = choice.Cell;
            informedChoices++;
            if (choice.UsesPreview) previewChoices++;
            else
            {
                randomExplorationChoices++;
                if (choice.VisiblePreviews > 0) deliberatelySkippedPreviews++;
            }
            if (choice.MatchesRevealed) previewMatchingChoices++;
            Note("CELL_CHOICE", "mode=EXPECTED_VALUE;cell=" + activeCell.name +
                ";visiblePreviews=" + choice.VisiblePreviews +
                ";usedPreview=" + choice.UsesPreview +
                ";matchedRevealed=" + choice.MatchesRevealed +
                ";expectedPayout=" + choice.ExpectedPayout.ToString("F3", CultureInfo.InvariantCulture) +
                ";unknownExpected=" + EvText(choice.ExploreExpectedPayout) +
                ";previewExpected=" + EvText(choice.BestPreviewExpectedPayout) +
                ";decision=" + choice.Reason);
            }
        }
        else
        {
            // A forced deterministic payout test must select the same fixed
            // sequence of positions every time. It is not a strategy benchmark.
            activeCell = cells[cellIndex];
            Note("CELL_CHOICE", "mode=FIXED_QA;cell=" + activeCell.name +
                ";reason=force deterministic combination");
        }

        // First move diversity is an observed statistic, not an imposed pattern.
        if (cellIndex == 0)
            uniqueFirstChoices.Add(game.CurrentTicketKind + ":" + activeCell.name);
        cellStartedAt = Time.realtimeSinceStartup;
        activeCoating = activeCell.GetComponentInChildren<RawImage>(true);
        pointerHeld = false;
        strokePointIndex = 0;
    }

    private void ScratchTick(float now)
    {
        if (!game.TicketActive)
        {
            ValidateTicket();
            if (!running) return;
            if (IsFullDemo && game.DemoFinished)
            {
                bool ownedAll = CountOwnedDemoSkills() == 25;
                Check(ownedAll, "all_25_demo_skills_purchased",
                    "Acquistate " + CountOwnedDemoSkills() + "/25 skill.");
                Check(forcedTicketsPlayed == 0 && !qaFunds, "full_demo_natural",
                    "Forzati=" + forcedTicketsPlayed + ";creditoQA=" + qaFunds);
                Check(game.DemoFinalGoldTickets >= game.DemoFinalGoldMinTickets,
                    "eight_final_gold_tickets",
                    "Gold dopo entrambi i capstone=" + game.DemoFinalGoldTickets +
                    "/" + game.DemoFinalGoldMinTickets);
                Check(game.DemoFinalGoldPayout >= game.DemoFinalGoldPayoutTarget,
                    "finale_earned_gold_payout",
                    "Incassi Dorati=" + game.DemoFinalGoldPayout + "/" + game.DemoFinalGoldPayoutTarget);
                Check(game.DemoFinalPowerActivated, "finale_real_activation",
                    "Attivato realmente Titanico o Jolly dopo F04.");
                Check(qaTitanActivations > 0, "titan_tested_in_live_scratch",
                    "Attivazioni Titanico durante gesture reali=" + qaTitanActivations);
                Check(qaC06Inspections > 0, "C06_inspection_exercised",
                    "Anteprime scelte dal giocatore tramite tasto destro=" + qaC06Inspections);
                Check(skills.JollyActivationCount > 0, "real_visible_jolly_scratched",
                    "Jolly realmente grattati=" + skills.JollyActivationCount);
                Check(game.State == ScratchGameManager.RoundState.Finished, "authored_demo_end",
                    "La scena ha completato il milestone finale.");
                ProgressEvent("DEMO_FINISHED", "-", 0, 0, "End scene reached naturally");
                Finish(assertionsFailed == 0 && ownedAll, "Fine demo raggiunta naturalmente.");
                return;
            }
            if (IsFullDemo && tickets + farmTickets >= 600)
            {
                Fail("Limite sicurezza: 600 biglietti senza completare la demo.");
                return;
            }
            ChangePhase(Phase.WaitChoice, 0.6f / humanSpeed);
            return;
        }
        if (activeCell == null || activeCoating == null)
        {
            Fail("Casella o copertura mancante durante grattatura.");
            return;
        }
        if (activeCell.IsComplete)
        {
            EndPointer();
            Note("CELL_COMPLETE", "cell=" + activeCell.name +
                ";seconds=" + (now - cellStartedAt).ToString("F2", CultureInfo.InvariantCulture) +
                ";percent=" + activeCell.RevealedPercentage.ToString("F1", CultureInfo.InvariantCulture));
            cellIndex++;
            if (cellIndex < game.MaxScratches)
            {
                PrepareCell();
                nextAction = now + (0.22f + (float)random.NextDouble() * 0.45f) / humanSpeed;
            }
            else
                ChangePhase(Phase.WaitChoice, 0.5f / humanSpeed);
            return;
        }
        if (strokePointIndex >= 480)
        {
            Fail("Impossibile completare Cell " + activeCell.name + " all'85%.");
            return;
        }

        // Serpentine human-like movement; each sample drives the same real pointer handlers.
        int scan = strokePointIndex % 180;
        int row = scan / 15;
        int column = scan % 15;
        float u = 0.04f + (row % 2 == 0 ? column : 14 - column) * (0.92f / 14f);
        float v = 0.07f + row * (0.86f / 11f);
        float wobble = ((float)random.NextDouble() - 0.5f) * 0.014f;
        v = Mathf.Clamp(v + wobble, 0.04f, 0.96f);
        EmitPointer(u, v);
        strokePointIndex++;
        nextAction = now + (0.013f + (float)random.NextDouble() * 0.012f) / humanSpeed;
    }

    private void EmitPointer(float u, float v)
    {
        RectTransform rt = activeCoating.rectTransform;
        Rect rect = rt.rect;
        Vector3 local = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, u),
            Mathf.Lerp(rect.yMin, rect.yMax, v), 0f);
        Vector3 world = rt.TransformPoint(local);
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -809,
                button = PointerEventData.InputButton.Left
            };
        pointer.position = screen;
        if (!pointerHeld)
        {
            activeCell.OnPointerDown(pointer);
            pointerHeld = true;
            // Editor equivalent of holding SHIFT during this REAL scratch drag:
            // no free charges and no fabricated wins or completed cells.
            if (IsFullDemo && !titanActivatedThisTicket &&
                skills.GetLevel("G08") > 0 && skills.TitanCharges > 0)
            {
                int before = skills.TitanActivationCount;
                bool activated = activeCell.QaActivateTitanDuringGesture();
                Check(activated && skills.TitanActivationCount == before + 1,
                    "G08_real_titan_activation", "SHIFT-equivalent consumed a real charge.");
                if (activated)
                {
                    titanActivatedThisTicket = true;
                    qaTitanActivations++;
                    Note("G08_TITAN_USED", "cell=" + activeCell.name);
                }
            }
        }
        else activeCell.OnDrag(pointer);
    }

    private void EndPointer()
    {
        if (pointerHeld && activeCell != null && pointer != null)
            activeCell.OnPointerUp(pointer);
        pointerHeld = false;
    }

    private void ValidateTicket()
    {
        EndPointer();
        var pat = activePattern;
        ScratchSymbolManager.MatchTier expect = (ScratchSymbolManager.MatchTier)(int)pat;
        Check(game.RevealedCount == game.MaxScratches, "fully_scratched",
            "Grattate=" + game.RevealedCount + "/" + game.MaxScratches);
        if (pat != ScratchSymbolManager.TestPattern.Random)
            Check(game.CurrentMatch.Tier == expect, "combination",
                "Attesa=" + expect + " reale=" + game.CurrentMatch.Tier);
        Check(game.TicketPayout == ExpectedPayout(), "payout_formula",
            "Vincita=" + game.TicketPayout + ";formula=" + ExpectedPayout() +
            ";match=" + game.CurrentMatch.Tier);
        Check(economy.Coins - walletBeforeTicket == game.TicketPayout - ticketCost,
            "single_settlement",
            "delta_wallet=" + (economy.Coins - walletBeforeTicket) +
            " expected=" + (game.TicketPayout - ticketCost));
        int afterSettlement = economy.Coins;
        game.OnCellCompleted(activeCell); // A duplicate completion callback must be ignored.
        Check(economy.Coins == afterSettlement, "duplicate_completion_rejected",
            "Saldo immutato dopo una chiamata OnCellCompleted duplicata.");
        if (!farming && stage == 3 && pat != ScratchSymbolManager.TestPattern.Random)
            Check(game.CurrentMatch.IsMatch, "diamond_guaranteed_match",
                "Diamante con 5 simboli grattati.");
        if (farming) farmTickets++;
        else tickets++;
        if (pat == ScratchSymbolManager.TestPattern.Random)
        {
            naturalTicketsPlayed++;
            observedRandomResults[(int)game.CurrentMatch.Tier]++;
        }
        else
            forcedTicketsPlayed++;
        if (IsFullDemo)
            ProgressEvent("TICKET_SETTLED", "-", 0, ticketCost,
                "payout=" + game.TicketPayout + ";match=" + game.CurrentMatch.Tier);
        Note(farming ? "FARM_TICKET_END" : "TICKET_END", "match=" + game.CurrentMatch.Tier + ";gain=" + game.TicketPayout +
            ";net=" + (game.TicketPayout - ticketCost) +
            ";seconds=" + (Time.realtimeSinceStartup - ticketStartedAt).ToString("F2", CultureInfo.InvariantCulture));
        lastStatus = (farming ? "Farm " + farmTickets : "Test " + tickets) +
            ": " + game.CurrentTicketKind + " " + pat + " = " + game.CurrentMatch.Tier +
            " +" + game.TicketPayout;
    }

    private int ExpectedPayout()
    {
        var match = game.CurrentMatch;
        if (!match.IsMatch)
        {
            int level = skills.GetLevel(ScratchSkillTree.SkillId.E5);
            return level > 0 ? 3 * level +
                (int)Math.Ceiling(Math.Max(0, ticketCost) * 0.2) : 0;
        }
        int rewardBase = game.TicketPrize(game.CurrentTicketKind) +
            2 * skills.GetLevel(ScratchSkillTree.SkillId.E1);
        double extra = match.Tier == ScratchSymbolManager.MatchTier.Pair
            ? 1.0 + 0.1 * skills.GetLevel(ScratchSkillTree.SkillId.E2)
            : match.Tier == ScratchSymbolManager.MatchTier.Triple
                ? 1.0 + 0.15 * skills.GetLevel(ScratchSkillTree.SkillId.E3)
                : 1.0 + 0.2 * skills.GetLevel(ScratchSkillTree.SkillId.E4);
        double payout = Math.Ceiling(rewardBase * match.Multiplier * extra);
        if (skills.GetLevel(ScratchSkillTree.SkillId.E6) > 0 && streakBeforeTicket > 0)
            payout = Math.Ceiling(payout * (1.0 + 0.05 * Mathf.Min(4, streakBeforeTicket)));
        return (int)Math.Min(int.MaxValue, payout);
    }

    private void WaitChoice(float now)
    {
        if (!game.WaitingForTicketChoice || !flow.CanShowTicketChoices) return;
        if (farming)
        {
            int needed = FarmingTargetCoins();
            // Radial chapters are gated by tickets actually completed, even
            // when the player already has enough currency for the next skill.
            // Stay in real-ticket farming until BOTH gates are satisfied.
            bool chapterReady = !IsFullDemo || purchaseIndex >= FullDemoPlan.Length ||
                ScratchRadialProgression.Available(
                    ScratchSkillTree.StableId(FullDemoPlan[purchaseIndex]), game, skills);
            if (economy.Coins >= needed && chapterReady)
            {
                bool forTicket = farmingForTicket;
                farming = farmingForTicket = false;
                Note("FARM_DONE", "wallet=" + economy.Coins +
                    ";target=" + needed + ";purpose=" + (forTicket ? "TICKET" : "UPGRADE"));
                if (forTicket)
                    ChooseNext(now); // The ticket is now unlocked AND affordable.
                else
                    ChangePhase(Phase.OpenShop,
                        (0.3f + (float)random.NextDouble() * 0.4f) / humanSpeed);
            }
            else
                ChooseFarmTicket(now);
            return;
        }
        if (IsFullDemo)
        {
            ContinueFullDemo(now);
            return;
        }
        patternIndex++;
        if (patternIndex < TestPatterns[stage].Length)
        {
            ChooseNext(now);
            return;
        }
        if (stage == 2)
        {
            Finish(assertionsFailed == 0,
                autoplayMode == AutoplayMode.SimulazioneReale
                    ? "13 biglietti casuali su tre categorie demo; nessuna combinazione forzata."
                    : "Completati tre tier della demo e i test combinazione forzata.");
            return;
        }
        stage++;
        patternIndex = 0;
        purchaseIndex = 0;
        ChangePhase(Phase.OpenShop, (0.5f + (float)random.NextDouble()) / humanSpeed);
    }

    /// <summary>
    /// Unlike the legacy 13-ticket QA, the full-demo run continuously earns
    /// the money for the next planned upgrade and then uses the authored shop.
    /// It never adds currency, forces outcomes, or grants upgrades directly.
    /// </summary>
    private void ContinueFullDemo(float now)
    {
        UpdateFullDemoTicketTier();
        if (purchaseIndex >= FullDemoPlan.Length)
        {
            if (skills.GetLevel("F04") == 0)
            {
                Fail("Piano terminato senza acquisto del capstone F04.");
                return;
            }
            ChooseNext(now);
            return;
        }

        ScratchSkillTree.SkillId next = FullDemoPlan[purchaseIndex];
        if (!skills.RequirementsMet(next))
        {
            Fail("Prerequisiti non soddisfatti per " + next + ": " + skills.RequirementText(next));
            return;
        }
        string nextId = ScratchSkillTree.StableId(next);
        if (!ScratchRadialProgression.Available(nextId, game, skills))
        {
            // A locked nucleus chapter is a gameplay milestone, never an
            // economic deadlock. Play natural tickets until it opens.
            Note("NUCLEUS_WAIT", "next=" + nextId +
                ";chapter=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";goal=" + ScratchRadialProgression.NextGoal(game, skills));
            ChooseNext(now);
            return;
        }
        int required = FundsNeededForNextPurchase();
        if (economy.Coins < required)
        {
            farming = true;
            farmingForTicket = false;
            Note("FARM_START", "next=" + next + ";required=" + required +
                ";wallet=" + economy.Coins + ";remaining_steps=" +
                (FullDemoPlan.Length - purchaseIndex));
            lastStatus = "Progressione " + (purchaseIndex + 1) + "/" + FullDemoPlan.Length +
                ": risparmio per " + next + " (serve " + required + ")";
            ChooseFarmTicket(now);
            return;
        }

        ChangePhase(Phase.OpenShop, (0.3f + (float)random.NextDouble() * 0.3f) / humanSpeed);
    }

    private void UpdateFullDemoTicketTier()
    {
        if (!IsFullDemo) return;
        stage = game.IsTicketUnlocked(ScratchGameManager.TicketKind.Gold) ? 2 :
            game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato) ? 1 : 0;
    }

    private int CountOwnedDemoSkills()
    {
        int count = 0;
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            if (entry.demoEligible && skills.GetLevel(entry.id) > 0) count++;
        return count;
    }

    private void ProgressEvent(string eventName, string skillId, int level, int price, string details)
    {
        float elapsed = Time.realtimeSinceStartup - startedAt;
        if (eventName == "SKILL_PURCHASED" &&
            (skillId == "C03" || skillId == "C05" || skillId == "G08" || skillId == "F04"))
            milestoneSeconds[skillId] = elapsed;
        progression.Add(elapsed.ToString("F2", CultureInfo.InvariantCulture) + ";" +
            Safe(eventName) + ";" + (game != null ? game.TicketNumber.ToString() : "0") + ";" +
            (game != null ? game.CurrentTicketKind.ToString() : "-") + ";" +
            (economy != null ? economy.Coins.ToString() : "-") + ";" +
            (economy != null ? economy.TotalEarned.ToString() : "-") + ";" +
            Safe(skillId) + ";" + level + ";" + price + ";" + Safe(details));
    }

    /// <summary>Validate all 28 purchase steps, 25 unique demo effects and all edges.</summary>
    // Kept for programmatic QA; removed redundant standalone menu entry.
    private static void TestFullDemoPlan()
    {
        if (ValidateFullDemoPlan(out string error))
            Debug.Log("STR FULL DEMO QA: 28 acquisti, 25 skill demo, prerequisiti PASS.");
        else
            Debug.LogError("STR FULL DEMO QA: piano FAIL - " + error);
    }

    private static bool ValidateFullDemoPlan(out string error)
    {
        var owned = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ScratchSkillTree.SkillId id in FullDemoPlan)
        {
            string stableId = ScratchSkillTree.StableId(id);
            ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(stableId);
            if (entry == null || !entry.demoEligible ||
                !ScratchSkillCatalogV2.EffectImplemented(stableId))
            {
                error = "Non acquistabile in demo: " + stableId;
                return false;
            }
            foreach (string raw in (entry.prerequisites ?? "").Split(';'))
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string[] parts = raw.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int required) ||
                    !owned.TryGetValue(parts[0], out int current) || current < required)
                {
                    error = "Prerequisito fuori ordine: " + stableId + " <- " + raw;
                    return false;
                }
            }
            int next = owned.TryGetValue(stableId, out int level) ? level + 1 : 1;
            if (next > entry.maxLevel)
            {
                error = "Superato livello massimo: " + stableId;
                return false;
            }
            owned[stableId] = next;
        }
        var route = new List<ScratchSkillTree.SkillId>(FullDemoPlan);
        if (FullDemoPlan.Length != 28 || owned.Count != 25 ||
            !owned.TryGetValue("C11", out int extraLevels) || extraLevels != 2 ||
            FullDemoPlan[FullDemoPlan.Length - 1] != ScratchSkillTree.SkillId.F4 ||
            !owned.ContainsKey("G08") || !owned.ContainsKey("C05") ||
            !(route.IndexOf(ScratchSkillTree.SkillId.C2) <
              route.IndexOf(ScratchSkillTree.SkillId.G7) &&
              route.IndexOf(ScratchSkillTree.SkillId.G7) <
              route.IndexOf(ScratchSkillTree.SkillId.C4) &&
              route.IndexOf(ScratchSkillTree.SkillId.C5) <
              route.IndexOf(ScratchSkillTree.SkillId.G8) &&
              route.IndexOf(ScratchSkillTree.SkillId.G8) <
              route.IndexOf(ScratchSkillTree.SkillId.F4)))
        {
            error = "Il piano non copre le 25 skill o manca il finale.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void BeginShop(float now)
    {
        if (!flow.CanShowTicketChoices) return;
        if (startFresh && !IsFullDemo)
            Check(!game.IsTicketUnlocked((ScratchGameManager.TicketKind)stage),
                "locked_before_upgrade", "Tier " + stage + " non acquistabile prima dello sblocco.");
        flow.OpenShop();
        Note("SHOP_OPEN", "sblocco=" + (ScratchGameManager.TicketKind)stage);
        ChangePhase(Phase.WaitShop, 0.1f);
    }

    private void AwaitShop(float now)
    {
        if (flow.ShopVisible) ChangePhase(Phase.StartPurchase, 0.25f / humanSpeed);
    }

    private void StartPurchase(float now)
    {
        var plan = ActivePurchasePlan;
        if (purchaseIndex >= plan.Length)
        {
            ChangePhase(Phase.CloseShop, 0.35f / humanSpeed);
            return;
        }

        ScratchSkillTree.SkillId id = plan[purchaseIndex];
        int targetLevel = 1;
        for (int i = 0; i < purchaseIndex; i++)
            if (plan[i] == id) targetLevel++;
        if (skills.GetLevel(id) >= targetLevel)
        {
            purchaseIndex++;
            nextAction = now + 0.1f;
            return;
        }
        // The radial nucleus imposes a second gate independent of price and
        // ordinary skill prerequisites. In particular G08 (chapter 5) must
        // wait for FIVE settled Gold tickets, not just one profitable ticket.
        // Check again on entering the shop: farming may have finished on cash
        // alone after the last ticket, while its chapter is still locked.
        string stableId = ScratchSkillTree.StableId(id);
        if (IsFullDemo && !ScratchRadialProgression.Available(stableId, game, skills))
        {
            farming = true;
            farmingForTicket = false;
            Note("NUCLEUS_FARM", "next=" + stableId +
                ";chapter=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";neededChapter=" + ScratchRadialProgression.ChapterOf(stableId) +
                ";goal=" + ScratchRadialProgression.NextGoal(game, skills));
            lastStatus = "Aspetto sblocco capitolo " +
                ScratchRadialProgression.ChapterOf(stableId) + " per " + stableId;
            flow.ContinueAfterUpgrades();
            ChangePhase(Phase.WaitFarmShopClosed, 0.1f);
            return;
        }

        // The final unlock of each tier must leave enough money to buy at
        // least ONE ticket of the newly unlocked tier. Without this reserve the
        // tester unlocks Fortunato at 140, remains with 1 coin, then aborts.
        int requiredCash = FundsNeededForNextPurchase();
        if (skills.RequirementsMet(id) && economy.Coins < requiredCash)
        {
            farming = true;
            farmingForTicket = false;
            Note("FARM_START", "skill=" + id + ";wallet=" + economy.Coins +
                ";upgradePrice=" + skills.GetPrice(id) +
                ";ticketReserve=" + TicketReserveForCurrentPurchase() +
                ";totalRequired=" + requiredCash);
            lastStatus = "Farming per " + id + ": saldo " + economy.Coins +
                ", necessari " + requiredCash + " (include primo biglietto)";
            flow.ContinueAfterUpgrades();
            ChangePhase(Phase.WaitFarmShopClosed, 0.1f);
            return;
        }
        if (!skills.CanPurchase(id))
        {
            Fail("Non acquistabile " + id + " (wallet=" + economy.Coins +
                ";prezzo=" + skills.GetPrice(id) +
                ";riserva biglietto=" + TicketReserveForCurrentPurchase() +
                ";requisiti=" + skills.RequirementText(id) +
                ";capitoloCorrente=" + ScratchRadialProgression.UnlockedChapter(game, skills) +
                ";capitoloRichiesto=" + ScratchRadialProgression.ChapterOf(stableId) +
                ";nucleoDisponibile=" + ScratchRadialProgression.Available(stableId, game, skills) +
                ";demoEligible=" + (ScratchSkillCatalogV2.Find(stableId)?.demoEligible ?? false) +
                ";implemented=" + ScratchSkillCatalogV2.EffectImplemented(stableId) + ")");
            return;
        }

        // The 90-node UI is now a single map. Focus the *real* authored
        // node, then exercise the unchanged hold-to-buy script via QA input.
        treeUI.RevealSkill(stableId);
        pressedHold = null;
        foreach (ScratchHoldToBuy hold in holdButtons)
        {
            if (hold != null && hold.StableSkillId == stableId &&
                hold.gameObject.name == "MapSkillNode_" + stableId)
            {
                pressedHold = hold;
                break;
            }
        }
        if (pressedHold == null)
        {
            Fail("Hold-to-buy non trovato per " + id);
            return;
        }
        expectedSkillLevel = skills.GetLevel(id) + 1;
        pendingPurchasePrice = skills.GetPrice(id);
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -809, button = PointerEventData.InputButton.Left
            };
        if (!pressedHold.QaBeginHold(pointer))
        {
            Fail("Impossibile iniziare hold su " + id + " (nodeActive=" +
                pressedHold.isActiveAndEnabled + ";canBuy=" + treeUI.CanPurchaseSkill(stableId) + ")");
            return;
        }
        lastQaHoldTick = EditorApplication.timeSinceStartup;
        Note("HOLD_BEGIN", "abilita=" + id + ";prezzo=" + skills.GetPrice(id) +
            ";holdSeconds=" + pressedHold.QaDuration);
        ChangePhase(Phase.WaitPurchase, 0f);
    }

    private void EndPurchase(float now)
    {
        ScratchSkillTree.SkillId id = ActivePurchasePlan[purchaseIndex];
        if (pressedHold == null)
        {
            Fail("Handle del nodo perso durante hold di " + id);
            return;
        }

        double editorNow = EditorApplication.timeSinceStartup;
        float step = Mathf.Clamp((float)(editorNow - lastQaHoldTick), 0f, 0.25f);
        lastQaHoldTick = editorNow;
        pressedHold.QaTick(step);

        if (skills.GetLevel(id) != expectedSkillLevel)
        {
            if (now - stateSince <= 6f) return;
            string details = id + " livello=" + skills.GetLevel(id) +
                " atteso=" + expectedSkillLevel +
                " qaHold=" + pressedHold.QaHolding +
                " elapsed=" + pressedHold.QaElapsed.ToString("F2", CultureInfo.InvariantCulture) +
                "/" + pressedHold.QaDuration +
                " scriptActive=" + pressedHold.isActiveAndEnabled +
                " canBuy=" + skills.CanPurchase(id) +
                " wallet=" + economy.Coins;
            pressedHold.QaReleaseHold();
            Check(false, "hold_to_buy", details);
            Finish(false, "Acquisto fallito: " + details);
            return;
        }

        pressedHold.QaReleaseHold();
        Check(true, "hold_to_buy", id + " livello=" + expectedSkillLevel);
        Note("SKILL_PURCHASED", id.ToString());
        if (IsFullDemo)
        {
            lastUpgradeAt = Time.realtimeSinceStartup;
            ProgressEvent("SKILL_PURCHASED", ScratchSkillTree.StableId(id),
                expectedSkillLevel, pendingPurchasePrice,
                "target_step=" + (purchaseIndex + 1) + "/" + FullDemoPlan.Length);
            if (id == ScratchSkillTree.SkillId.G8)
                Check(!game.DemoFinished && game.DemoFinalGoldTickets == 0,
                    "G08_cannot_end_demo_alone",
                    "Il Titanico deve essere utilizzabile prima del Jolly.");
            if (id == ScratchSkillTree.SkillId.F4)
                Check(!game.DemoFinished && game.DemoFinalGoldTickets == 0 &&
                      game.DemoFinalGoldPayout == 0,
                    "F04_opens_fresh_finale",
                    "Finale inizia dopo entrambi gli sblocchi, con contatori azzerati.");
            UpdateFullDemoTicketTier();
        }
        if ((!IsFullDemo && purchaseIndex == UnlockPlans[stage].Length - 1) ||
            (IsFullDemo && TicketReserveForCurrentPurchase() > 0))
        {
            int reserve = ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
            if (!Check(economy.Coins >= reserve, "next_ticket_cash_reserved",
                "unlocked=" + stage + ";wallet=" + economy.Coins + ";ticketCost=" + reserve))
            {
                pressedHold = null;
                Finish(false, "Acquisto dello sblocco senza fondi per il nuovo biglietto.");
                return;
            }
        }
        pressedHold = null;
        purchaseIndex++;
        ChangePhase(Phase.StartPurchase, (0.2f + (float)random.NextDouble() * 0.3f) / humanSpeed);
    }

    private void CloseShop(float now)
    {
        bool unlocked = game.IsTicketUnlocked((ScratchGameManager.TicketKind)stage);
        if (!Check(unlocked, "unlock", "Biglietto " + stage + " deve essere sbloccato"))
        {
            Fail("Sblocco del biglietto non riuscito.");
            return;
        }
        flow.ContinueAfterUpgrades();
        ChangePhase(Phase.WaitShopClosed, 0.1f);
    }

    private void AwaitShopClosed(float now)
    {
        if (flow.ShopOpen || !flow.CanShowTicketChoices) return;
        Note("SHOP_CLOSE", "nuovo biglietto=" + (ScratchGameManager.TicketKind)stage);
        ChooseNext(now);
    }

    // Reserve a ticket's price only before the FINAL skill of its tier,
    // not for every intermediate purchase.
    private int TicketReserveForCurrentPurchase()
    {
        if (IsFullDemo)
        {
            if (purchaseIndex < 0 || purchaseIndex >= FullDemoPlan.Length) return 0;
            ScratchSkillTree.SkillId id = FullDemoPlan[purchaseIndex];
            if (id == ScratchSkillTree.SkillId.C3)
                return ScratchGameManager.TicketPrice(ScratchGameManager.TicketKind.Fortunato);
            if (id == ScratchSkillTree.SkillId.C5)
                return ScratchGameManager.TicketPrice(ScratchGameManager.TicketKind.Gold);
            return 0;
        }
        if (stage <= 0 || stage >= UnlockPlans.Length ||
            purchaseIndex < 0 || purchaseIndex >= UnlockPlans[stage].Length)
            return 0;
        if (purchaseIndex != UnlockPlans[stage].Length - 1)
            return 0;
        return ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
    }

    // The target must be stable while farm tickets are purchased: ticketCost
    // changes to the FARM ticket's price on every scratch.
    private int FarmingTargetCoins()
    {
        if (farmingForTicket)
            return ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)stage);
        return FundsNeededForNextPurchase();
    }

    private int FundsNeededForNextPurchase()
    {
        if (IsFullDemo)
            return purchaseIndex >= FullDemoPlan.Length ? 0
                : BudgetRequired(skills.GetPrice(FullDemoPlan[purchaseIndex]),
                    TicketReserveForCurrentPurchase());
        if (stage <= 0 || stage >= UnlockPlans.Length ||
            purchaseIndex < 0 || purchaseIndex >= UnlockPlans[stage].Length)
            return 0;
        return BudgetRequired(skills.GetPrice(UnlockPlans[stage][purchaseIndex]),
            TicketReserveForCurrentPurchase());
    }

    private static int BudgetRequired(int skillPrice, int ticketReserve)
    {
        long total = (long)skillPrice + ticketReserve;
        return (int)Math.Min(int.MaxValue, Math.Max(0L, total));
    }

    /// <summary>
    /// Regression test for the wallet=141 / C3=140 / Fortunato=10 failure.
    /// Pure calculations: works outside Play Mode and does not alter saves.
    /// </summary>
    // Kept for programmatic QA; removed redundant standalone menu entry.
    private static void TestTicketBudget()
    {
        int failures = 0;
        // Historical regression: 141 coins, C3 costs 140, Fortunato costs 10.
        // 141 is insufficient, 150 is sufficient. The test remains valid even
        // when the live provisional ticket prices are later rebalanced.
        int skillPrice = 140;
        int nextTicketPrice = 10;
        int insufficientWallet = 141;
        int sufficientWallet = 150;
        if (BudgetRequired(skillPrice, nextTicketPrice) != sufficientWallet) failures++;
        if (insufficientWallet - skillPrice >= nextTicketPrice) failures++;
        if (sufficientWallet - skillPrice < nextTicketPrice) failures++;

        foreach (ScratchGameManager.TicketKind kind in new[]
        {
            ScratchGameManager.TicketKind.Fortunato,
            ScratchGameManager.TicketKind.Gold,
            ScratchGameManager.TicketKind.Diamond
        })
        {
            int currentTicketPrice = ScratchGameManager.TicketPrice(kind);
            if (currentTicketPrice <= 0) failures++;
            if (BudgetRequired(100, currentTicketPrice) != 100 + currentTicketPrice)
                failures++;
        }

        if (failures == 0)
            Debug.Log("STR AUTO QA: reserve budget regression PASS.");
        else
            Debug.LogError("STR AUTO QA: reserve budget regression FAIL " + failures);
    }

    private void WaitFarmShopClosed(float now)
    {
        if (flow.ShopOpen || !flow.CanShowTicketChoices) return;
        ChooseFarmTicket(now);
    }

    private void ChooseFarmTicket(float now)
    {
        // Buy and scratch the best previously unlocked tier to earn real coins.
        // Never jump ahead to an unavailable tier while farming.
        var kind = (ScratchGameManager.TicketKind)Mathf.Max(0, stage - 1);
        if (IsFullDemo)
        {
            // Best unlocked ticket the current wallet can afford. A free
            // Standard ticket always remains available, preventing deadlocks.
            kind = game.CanChooseTicket(ScratchGameManager.TicketKind.Gold)
                ? ScratchGameManager.TicketKind.Gold
                : game.CanChooseTicket(ScratchGameManager.TicketKind.Fortunato)
                    ? ScratchGameManager.TicketKind.Fortunato
                    : ScratchGameManager.TicketKind.Standard;
        }
        if (!game.CanChooseTicket(kind))
            kind = ScratchGameManager.TicketKind.Standard;
        SetPattern(ScratchSymbolManager.TestPattern.Random);
        walletBeforeTicket = economy.Coins;
        ticketCost = ScratchGameManager.TicketPrice(kind);
        if (!game.ChooseNextTicket(kind))
        {
            Fail("Impossibile acquistare il biglietto farm " + kind +
                " con saldo " + economy.Coins);
            return;
        }
        Note("FARM_TICKET_START", "kind=" + kind + ";needed=" +
            FarmingTargetCoins() + ";purpose=" + (farmingForTicket ? "TICKET" : "UPGRADE"));
        ChangePhase(Phase.WaitLive, (0.2f + (float)random.NextDouble() * 0.4f) / humanSpeed);
    }

    private void ChooseNext(float now)
    {
        if (IsFullDemo) UpdateFullDemoTicketTier();
        ScratchGameManager.TicketKind kind = (ScratchGameManager.TicketKind)stage;
        if (stage == 0 && !game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato))
        {
            int before = economy.Coins;
            bool rejected = !game.ChooseNextTicket(ScratchGameManager.TicketKind.Fortunato);
            Check(rejected && economy.Coins == before, "locked_ticket_rejection",
                "Fortunato rifiutato prima di C3 senza consumare monete.");
        }

        SetPattern(PlannedPattern(stage, patternIndex));
        ticketCost = ScratchGameManager.TicketPrice(kind);
        if (!game.IsTicketUnlocked(kind))
        {
            Fail("Il biglietto non e' sbloccato: " + kind);
            return;
        }

        // Protection for later tickets as well: a losing paid ticket must not
        // end the entire run when the wallet can still earn via older tiers.
        if (economy.Coins < ticketCost)
        {
            farming = farmingForTicket = true;
            Note("TICKET_FUNDS_NEEDED", "kind=" + kind + ";wallet=" +
                economy.Coins + ";ticketCost=" + ticketCost);
            ChooseFarmTicket(now);
            return;
        }

        walletBeforeTicket = economy.Coins;
        if (!game.ChooseNextTicket(kind))
        {
            Fail("Selezione biglietto fallita per causa diversa dal saldo: " +
                kind + " wallet=" + economy.Coins);
            return;
        }
        ChangePhase(Phase.WaitLive, (0.4f + (float)random.NextDouble() * 0.6f) / humanSpeed);
    }

    private void SetPattern(ScratchSymbolManager.TestPattern pattern)
    {
        if (autoplayMode != AutoplayMode.QACombinazioniForzate &&
            pattern != ScratchSymbolManager.TestPattern.Random)
            throw new InvalidOperationException("Real-play autoplay cannot force a winning combination.");
        activePattern = pattern;
        SerializedObject target = new SerializedObject(symbols);
        SerializedProperty prop = target.FindProperty("editorTestPattern");
        if (prop == null) throw new InvalidOperationException("Editor test pattern missing.");
        prop.enumValueIndex = (int)pattern;
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private void ChangePhase(Phase next, float delay)
    {
        phase = next;
        stateSince = Time.realtimeSinceStartup;
        nextAction = stateSince + delay;
    }

    private bool Check(bool passed, string checkName, string details)
    {
        if (passed) assertionsPassed++;
        else assertionsFailed++;
        checks.Add(DateTime.UtcNow.ToString("o") + ";" + (passed ? "PASS" : "FAIL") +
            ";" + Safe(checkName) + ";" + Safe(details));
        if (!passed) Debug.LogError("STR AUTO QA: " + checkName + " - " + details);
        return passed;
    }

    private void Note(string kind, string details)
    {
        string num = game == null ? "-" : game.TicketNumber.ToString();
        string tier = game == null ? "-" : game.CurrentTicketKind.ToString();
        string match = game == null ? "-" : game.CurrentMatch.Tier.ToString();
        string wallet = economy == null ? "-" : economy.Coins.ToString();
        string payout = game == null ? "-" : game.TicketPayout.ToString();
        ledger.Add(DateTime.UtcNow.ToString("o") + ";" + Safe(kind) + ";" +
            num + ";" + tier + ";" +
            activePattern + ";" +
            wallet + ";" + ticketCost + ";" + payout + ";" + match + ";" +
            Safe(farming ? "FARM | " + details : details));
    }

    private static string Safe(string s) => (s ?? "").Replace(";", ",").Replace("\n", " ").Replace("\r", " ");

    private void Fail(string why)
    {
        Check(false, "fatal", why);
        Finish(false, why);
    }

    private void CaptureFrame(float now)
    {
        if (!captureFrames || now - lastScreenshotAt < 1f / framesPerSecond || frameCount >= 3600) return;
        string output = Path.Combine(reportDirectory, "frames",
            "frame_" + frameCount.ToString("D6") + ".png");
        ScreenCapture.CaptureScreenshot(output, 1);
        lastScreenshotAt = now;
        frameCount++;
    }

    private void Finish(bool success, string message)
    {
        if (!running) return;
        running = false;
        EndPointer();
        if (pressedHold != null) pressedHold.QaReleaseHold();
        pressedHold = null;
        lastStatus = (success ? "COMPLETATO: " : "FERMATO: ") + message;
        try
        {
            if (symbols != null) SetPattern(ScratchSymbolManager.TestPattern.Random);
            if (recordVideo)
            {
                if (movieRecorder != null && movieRecorder.Started)
                {
                    if (movieRecorder.Stop())
                    {
                        videoStatus = "Finalizzazione MP4 in corso";
                        videoValidationPending = true;
                        videoValidationDeadline = EditorApplication.timeSinceStartup + 20.0;
                        Note("VIDEO_STOP", "output=" + videoOutputPath);
                    }
                    else
                    {
                        videoStatus = "ERRORE finalizzazione: " + movieRecorder.LastError;
                        Check(false, "video_finalize", videoStatus);
                    }
                }
                else if (string.IsNullOrEmpty(videoOutputPath))
                {
                    videoStatus = "ERRORE registrazione non avviata";
                    Check(false, "video_start", videoStatus);
                }
            }
            Note("TEST_END", lastStatus);
            WriteReport();
            Debug.Log("STR AUTO QA: " + lastStatus + " | Report: " + reportDirectory +
                " | Video: " + videoStatus);
        }
        catch (Exception ex)
        {
            Debug.LogError("STR AUTO QA: salvataggio report/MP4 fallito: " + ex);
        }
        Repaint();
    }

    private void UpdateVideoValidation()
    {
        if (!videoValidationPending || string.IsNullOrEmpty(videoOutputPath)) return;

        try
        {
            if (File.Exists(videoOutputPath))
            {
                long length = new FileInfo(videoOutputPath).Length;
                if (length >= 4096)
                {
                    videoStatus = "MP4 creato (" + (length / 1024 / 1024f).ToString("F1",
                        CultureInfo.InvariantCulture) + " MB)";
                    Check(true, "video_mp4_file", "MP4 esistente, bytes=" + length);
                    videoValidationPending = false;
                    EditorApplication.update -= UpdateVideoValidation;
                    WriteReport();
                    Repaint();
                    return;
                }
            }

            if (EditorApplication.timeSinceStartup > videoValidationDeadline)
            {
                videoValidationPending = false;
                EditorApplication.update -= UpdateVideoValidation;
                videoStatus = "ERRORE: MP4 assente o vuoto dopo finalizzazione";
                lastStatus += " | VIDEO NON CREATO";
                Check(false, "video_mp4_file", "File non creato: " + videoOutputPath);
                WriteReport();
                Debug.LogError("STR AUTO QA: " + videoStatus + " | " + videoOutputPath);
                Repaint();
            }
        }
        catch (Exception ex)
        {
            videoValidationPending = false;
            EditorApplication.update -= UpdateVideoValidation;
            videoStatus = "ERRORE verifica MP4: " + ex.Message;
            lastStatus += " | VIDEO NON VERIFICATO";
            Check(false, "video_mp4_file", videoStatus);
            WriteReport();
        }
    }

    private string MilestoneMinute(string skillId)
    {
        return milestoneSeconds.TryGetValue(skillId, out float seconds)
            ? (seconds / 60f).ToString("F2", CultureInfo.InvariantCulture) : "non raggiunto";
    }

    private void WriteReport()
    {
        if (string.IsNullOrEmpty(reportDirectory)) return;
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(Path.Combine(reportDirectory, "tickets.csv"), ledger, Encoding.UTF8);
        File.WriteAllLines(Path.Combine(reportDirectory, "checks.csv"), checks, Encoding.UTF8);
        if (IsFullDemo)
            File.WriteAllLines(Path.Combine(reportDirectory, "progression.csv"), progression, Encoding.UTF8);
        float durationMinutes = (Time.realtimeSinceStartup - startedAt) / 60f;
        string summary = "Scratch to Riches V2 - Auto QA\n" +
            "Stato: " + lastStatus + "\n" +
            "Modalita': " + autoplayMode + "\n" +
            "Seed effettivo: " + actualSeed + "\n" +
            "Seed nuovo a ogni avvio: " + freshSeedEachRun + "\n" +
            "Durata reale (s): " + (Time.realtimeSinceStartup - startedAt).ToString("F2", CultureInfo.InvariantCulture) + "\n" +
            "Biglietti di test: " + tickets +
                (IsFullDemo ? " (nessun numero fisso)" : "/13") + "\n" +
            "Biglietti farm: " + farmTickets + "\n" +
            "Biglietti giocati totali: " + (tickets + farmTickets) + "\n" +
            "Biglietti con combinazione NON forzata: " + naturalTicketsPlayed + "\n" +
            "Biglietti con combinazione forzata: " + forcedTicketsPlayed + "\n" +
            "Primi movimenti distinti (tipo:casella): " + uniqueFirstChoices.Count + "\n" +
            "Risultati SOLO su biglietti casuali: nessuna=" + observedRandomResults[0] +
            ", coppia=" + observedRandomResults[1] +
            ", tris=" + observedRandomResults[2] +
            ", poker=" + observedRandomResults[3] +
            ", cinquina=" + observedRandomResults[4] + "\n" +
            "Check OK: " + assertionsPassed + "\nCheck KO: " + assertionsFailed + "\n" +
            "Saldo finale: " + (economy != null ? economy.Coins.ToString() : "n/a") + "\n" +
            "Credito QA incluso: " + qaFunds + "\n" +
            "Video: " + videoStatus + "\n" +
            "Video file: " + (recordVideo ? videoOutputPath : "Non richiesto") + "\n" +
            "Formato: " + (recordVideo ? "H.264 MP4, " + videoHeight + "p, " + videoFrameRate + " fps" : "N/A") + "\n" +
            "Audio: " + (recordVideo && recordGameAudio) + "\n" +
            "Scelte ragionate (partite casuali): " + informedChoices + "\n" +
            "Scelte con anteprima: " + previewChoices + "\n" +
            "Caselle coperte scelte NONOSTANTE anteprime disponibili: " +
                deliberatelySkippedPreviews + "\n" +
            "Scelte con anteprima che corrisponde a un simbolo grattato: " + previewMatchingChoices + "\n" +
            "Esplorazioni deliberate: " + randomExplorationChoices + "\n" +
            "Frames PNG richiesti: " + frameCount + "\n" +
            (IsFullDemo
                ? "MODALITA' DEMO COMPLETA - 25 SKILL / 28 ACQUISTI\n" +
                  "Acquisti completati: " + purchaseIndex + "/" + FullDemoPlan.Length + "\n" +
                  "Skill demo possedute: " + CountOwnedDemoSkills() + "/25\n" +
                  "DemoFinished reale: " + (game != null && game.DemoFinished) + "\n" +
                  "Dorati finali: " + (game != null ? game.DemoFinalGoldTickets : 0) +
                      "/" + (game != null ? game.DemoFinalGoldMinTickets : 8) + " minimo\n" +
                  "Incassi Dorati finali: " + (game != null ? game.DemoFinalGoldPayout : 0) +
                      "/" + (game != null ? game.DemoFinalGoldPayoutTarget : 3800) + "\n" +
                  "Potere finale attivato: " + (game != null && game.DemoFinalPowerActivated) + "\n" +
                  "Ispezioni C06: " + qaC06Inspections + "\n" +
                  "Gestualita' Titanico G08: " + qaTitanActivations + "\n" +
                  "Jolly grattati: " + (skills != null ? skills.JollyActivationCount : 0) + "\n" +
                  "Durata misurata (min): " + durationMinutes.ToString("F2", CultureInfo.InvariantCulture) + "\n" +
                  "Obiettivo 20-25 min: " + (durationMinutes >= 20f && durationMinutes <= 25f
                      ? "NEL TARGET" : "FUORI TARGET: BILANCIARE") + "\n" +
                  "C03 Fortunato a (min): " + MilestoneMinute("C03") + "\n" +
                  "C05 Dorato a (min): " + MilestoneMinute("C05") + "\n" +
                  "G08 Titanico a (min): " + MilestoneMinute("G08") + "\n" +
                  "F04 Jolly a (min): " + MilestoneMinute("F04") + "\n" +
                  "Durata non accelerata artificialmente: " + (humanSpeed == 1f) + "\n" +
                  "File cronologia: progression.csv\n"
                : string.Empty);
        File.WriteAllText(Path.Combine(reportDirectory, "summary.txt"), summary, Encoding.UTF8);
    }

}
#endif
