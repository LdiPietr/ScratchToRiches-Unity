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
            ScratchSkillTree.SkillId.C1, ScratchSkillTree.SkillId.C2,
            ScratchSkillTree.SkillId.C3 },
        new[] { ScratchSkillTree.SkillId.G3, ScratchSkillTree.SkillId.G4,
            ScratchSkillTree.SkillId.G5, ScratchSkillTree.SkillId.C4,
            ScratchSkillTree.SkillId.C5 },
        new[] { ScratchSkillTree.SkillId.C6, ScratchSkillTree.SkillId.E1,
            ScratchSkillTree.SkillId.E1, ScratchSkillTree.SkillId.E1,
            ScratchSkillTree.SkillId.E2, ScratchSkillTree.SkillId.E3,
            ScratchSkillTree.SkillId.E4, ScratchSkillTree.SkillId.E5,
            ScratchSkillTree.SkillId.C7 }
    };

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
    private bool qaFunds = true;
    private bool startFresh = true;
    private bool captureFrames;
    private bool recordVideo = true;
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
    private int farmTickets;
    private int informedChoices;
    private int previewChoices;
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
    private float maxSessionMinutes = 90f;
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
    private string lastStatus = "Avvia Play Mode, poi START TEST.";
    private string reportDirectory;
    private readonly List<string> ledger = new List<string>();
    private readonly List<string> checks = new List<string>();

    [MenuItem("Tools/Scratch to Riches/Autoplay e registrazione V2")]
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
                "Eliminare tutte le monete e i 32 livelli salvati? Questa azione e' irreversibile.", "RESET", "Annulla"))
            return;

        PlayerPrefs.DeleteKey("STR_M5_Wallet");
        PlayerPrefs.DeleteKey("STR_M5_TotalEarned");
        for (int i = 0; i < 32; i++)
        {
            PlayerPrefs.DeleteKey("STR_V2_Skill_" + i);
            PlayerPrefs.DeleteKey("STR_M5_Skill_" + i);
        }
        PlayerPrefs.Save();
        Debug.Log("STR V2: wallet e progressione resettati. Avvia Play Mode per iniziare da zero.");
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
        EditorGUILayout.LabelField("TEST COMPLETO DEI QUATTRO BIGLIETTI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Avvia la scena in Play Mode. Il test usa vere grattate UI e acquisti tramite pressione prolungata. " +
            "Si ferma da solo dopo Classico, Fortunato, Dorato e Diamante.", MessageType.Info);

        using (new EditorGUI.DisabledScope(running))
        {
            qaFunds = EditorGUILayout.ToggleLeft("Credito QA per i test (100.000 monete)", qaFunds);
            startFresh = EditorGUILayout.ToggleLeft("Azzera progressi prima del test (solo Play Mode)", startFresh);
            humanSpeed = EditorGUILayout.Slider("Velocita' gesti (1 = umano)", humanSpeed, 0.5f, 4f);
            seed = EditorGUILayout.IntField("Seed dei gesti", seed);
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

        if (qaFunds)
            EditorGUILayout.HelpBox("Credito QA: sblocchi rapidi. Queste monete non sono guadagni reali e restano nel wallet fino al reset.", MessageType.Warning);
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
        EditorGUILayout.LabelField("Biglietti test: " + tickets + " / 18  |  Farm: " + farmTickets);
        EditorGUILayout.LabelField("Scelte ragionate: " + informedChoices + " | Anteprime usate: " + previewChoices);
        EditorGUILayout.LabelField("Verifiche OK: " + assertionsPassed + "    Errori: " + assertionsFailed);
        EditorGUILayout.LabelField("Video: " + videoStatus, EditorStyles.wordWrappedLabel);
        if (running)
            EditorGUILayout.LabelField("Fase: " + phase + " | Tier " + (stage + 1) + " | Test " + (patternIndex + 1));
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

    private void StartTest()
    {
        game = UnityEngine.Object.FindFirstObjectByType<ScratchGameManager>();
        if (game == null || game.State != ScratchGameManager.RoundState.Ready)
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
            flow == null || treeUI == null || parentCanvas == null || holdButtons.Length != 8 ||
            EventSystem.current == null)
        {
            EditorUtility.DisplayDialog("Autoplay", "Scene references incomplete. Run the V2 Scene Audit first.", "OK");
            return;
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
        random = new System.Random(seed);
        UnityEngine.Random.InitState(seed);
        phase = Phase.WaitLive;
        stage = patternIndex = purchaseIndex = cellIndex = strokePointIndex = tickets = 0;
        assertionsPassed = assertionsFailed = frameCount = 0;
        realtimeCheckPassed = pointerHeld = farming = false;
        movieRecorder = null;
        videoValidationPending = false;
        videoStatus = recordVideo ? "Avvio in corso" : "Non richiesto";
        videoOutputPath = string.Empty;
        farmTickets = informedChoices = previewChoices = previewMatchingChoices = randomExplorationChoices = 0;
        lastQaHoldTick = 0d;
        pointer = null;
        activeCell = null;
        pressedHold = null;
        ticketCost = 0;
        startedAt = Time.realtimeSinceStartup;
        lastScreenshotAt = startedAt;
        ledger.Clear();
        checks.Clear();
        reportDirectory = Path.Combine(Application.persistentDataPath, "AutoplayTests",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(reportDirectory);
        if (captureFrames) Directory.CreateDirectory(Path.Combine(reportDirectory, "frames"));
        ledger.Add("timestamp;event;ticket;kind;pattern;wallet;cost;payout;match;details");
        checks.Add("timestamp;result;check;details");
        running = true;
        Note("START", "seed=" + seed + ";humanSpeed=" + humanSpeed + ";QA=" + qaFunds);
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

        SetPattern(TestPatterns[0][0]);
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
        Check(cellManager.ActiveCells.Length == game.CurrentGridSide * game.CurrentGridSide,
            "grid_size", game.CurrentGridSide + "x" + game.CurrentGridSide);
        Check(game.MaxScratches == game.CurrentGridSide, "pick_cap", "MaxScelte=" + game.MaxScratches);
        if (stage == 3 && !farming)
        {
            HashSet<string> unique = new HashSet<string>();
            foreach (ScratchCell sc in cellManager.ActiveCells)
                if (symbols.TryGetSymbol(sc, out string label)) unique.Add(label);
            Check(unique.Count <= 4 && unique.Count > 0,
                "diamond_pool", "Simboli diversi sul biglietto=" + unique.Count);
        }
        int expectedPreviews = skills.GetLevel(ScratchSkillTree.SkillId.C6) > 0 ? 5 :
            skills.GetLevel(ScratchSkillTree.SkillId.C4) > 0 ? 3 :
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
        Check(visible == expectedPreviews, "preview_count",
            "Anteprime attese=" + expectedPreviews + ", viste=" + visible);
        Check(unknownLeaked == 0, "no_hidden_symbol_leak",
            "Simboli segreti leggibili in caselle non grattate=" + unknownLeaked);
        Check(incompleteMasks == 0, "fresh_coating",
            "Patine mancanti, disabilitate o gia' grattate=" + incompleteMasks);
        Note("PREVIEWS_VISIBLE", "count=" + visible + ";previewSkills=" + expectedPreviews);
        ticketStartedAt = now;
        cellIndex = 0;
        PrepareCell();
        Note("TICKET_START", game.CurrentGridSide + "x" + game.CurrentGridSide);
        ChangePhase(Phase.Scratch, 0.5f / humanSpeed);
    }

    private void PrepareCell()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        bool strategic = farming ||
            TestPatterns[stage][patternIndex] == ScratchSymbolManager.TestPattern.Random;

        if (strategic)
        {
            int remaining = game.MaxScratches - game.RevealedCount;
            ScratchAutoplayPreviewStrategy.Choice choice =
                ScratchAutoplayPreviewStrategy.Choose(cells, remaining, random);
            activeCell = choice.Cell;
            informedChoices++;
            if (choice.UsesPreview) previewChoices++;
            else randomExplorationChoices++;
            if (choice.MatchesRevealed) previewMatchingChoices++;
            Note("CELL_CHOICE", "mode=STRATEGIC;cell=" + activeCell.name +
                ";visiblePreviews=" + choice.VisiblePreviews +
                ";usedPreview=" + choice.UsesPreview +
                ";matchedRevealed=" + choice.MatchesRevealed +
                ";decision=" + choice.Reason);
        }
        else
        {
            // A forced deterministic payout test must select the same fixed
            // sequence of positions every time. It is not a strategy benchmark.
            activeCell = cells[cellIndex];
            Note("CELL_CHOICE", "mode=FIXED_QA;cell=" + activeCell.name +
                ";reason=force deterministic combination");
        }

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
        var pat = farming ? ScratchSymbolManager.TestPattern.Random : TestPatterns[stage][patternIndex];
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
            return 3 * skills.GetLevel(ScratchSkillTree.SkillId.E5);
        int rewardBase = game.TicketPrize(game.CurrentTicketKind) +
            2 * skills.GetLevel(ScratchSkillTree.SkillId.E1);
        double extra = match.Tier == ScratchSymbolManager.MatchTier.Pair
            ? 1.0 + 0.1 * skills.GetLevel(ScratchSkillTree.SkillId.E2)
            : match.Tier == ScratchSymbolManager.MatchTier.Triple
                ? 1.0 + 0.15 * skills.GetLevel(ScratchSkillTree.SkillId.E3)
                : 1.0 + 0.2 * skills.GetLevel(ScratchSkillTree.SkillId.E4);
        return (int)Math.Ceiling(rewardBase * match.Multiplier * extra);
    }

    private void WaitChoice(float now)
    {
        if (!game.WaitingForTicketChoice || !flow.CanShowTicketChoices) return;
        if (farming)
        {
            ScratchSkillTree.SkillId needed = UnlockPlans[stage][purchaseIndex];
            if (economy.Coins >= skills.GetPrice(needed))
            {
                farming = false;
                Note("FARM_DONE", "wallet=" + economy.Coins + ";skill=" + needed);
                ChangePhase(Phase.OpenShop, (0.3f + (float)random.NextDouble() * 0.4f) / humanSpeed);
            }
            else
                ChooseFarmTicket(now);
            return;
        }
        patternIndex++;
        if (patternIndex < TestPatterns[stage].Length)
        {
            ChooseNext(now);
            return;
        }
        if (stage == 3)
        {
            Finish(assertionsFailed == 0, "Completati tutti e 4 i biglietti e i test combinazione.");
            return;
        }
        stage++;
        patternIndex = 0;
        purchaseIndex = 0;
        ChangePhase(Phase.OpenShop, (0.5f + (float)random.NextDouble()) / humanSpeed);
    }

    private void BeginShop(float now)
    {
        if (!flow.CanShowTicketChoices) return;
        if (startFresh)
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
        var plan = UnlockPlans[stage];
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
        if (!skills.CanPurchase(id))
        {
            if (!qaFunds && skills.RequirementsMet(id) && economy.Coins < skills.GetPrice(id))
            {
                farming = true;
                Note("FARM_START", "skill=" + id + ";wallet=" + economy.Coins +
                    ";required=" + skills.GetPrice(id));
                lastStatus = "Farming naturale per " + id + ": servono " + skills.GetPrice(id) +
                    " monete (saldo " + economy.Coins + ")";
                flow.ContinueAfterUpgrades();
                ChangePhase(Phase.WaitFarmShopClosed, 0.1f);
                return;
            }
            Fail("Non acquistabile " + id + " (wallet=" + economy.Coins +
                ";prezzo=" + skills.GetPrice(id) + ";requisiti=" + skills.RequirementText(id) + ")");
            return;
        }

        int branch = (int)id / 8;
        int slot = (int)id % 8;
        treeUI.SelectBranch(branch);
        pressedHold = null;
        foreach (ScratchHoldToBuy hold in holdButtons)
        {
            if (hold != null && hold.gameObject.name == "SkillNode_" + (slot + 1).ToString("00"))
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
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -809, button = PointerEventData.InputButton.Left
            };
        if (!pressedHold.QaBeginHold(pointer))
        {
            Fail("Impossibile iniziare hold su " + id + " (nodeActive=" +
                pressedHold.isActiveAndEnabled + ";canBuy=" + treeUI.CanHoldPurchase(slot) + ")");
            return;
        }
        lastQaHoldTick = EditorApplication.timeSinceStartup;
        Note("HOLD_BEGIN", "abilita=" + id + ";prezzo=" + skills.GetPrice(id) +
            ";holdSeconds=" + pressedHold.QaDuration);
        ChangePhase(Phase.WaitPurchase, 0f);
    }

    private void EndPurchase(float now)
    {
        ScratchSkillTree.SkillId id = UnlockPlans[stage][purchaseIndex];
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
            skills.GetPrice(UnlockPlans[stage][purchaseIndex]));
        ChangePhase(Phase.WaitLive, (0.2f + (float)random.NextDouble() * 0.4f) / humanSpeed);
    }

    private void ChooseNext(float now)
    {
        ScratchGameManager.TicketKind kind = (ScratchGameManager.TicketKind)stage;
        if (stage == 0 && !game.IsTicketUnlocked(ScratchGameManager.TicketKind.Fortunato))
        {
            int before = economy.Coins;
            bool rejected = !game.ChooseNextTicket(ScratchGameManager.TicketKind.Fortunato);
            Check(rejected && economy.Coins == before, "locked_ticket_rejection",
                "Fortunato rifiutato prima di C3 senza consumare monete.");
        }

        SetPattern(TestPatterns[stage][patternIndex]);
        walletBeforeTicket = economy.Coins;
        ticketCost = ScratchGameManager.TicketPrice(kind);
        if (!game.ChooseNextTicket(kind))
        {
            Fail("Selezione biglietto fallita: " + kind + " wallet=" + economy.Coins);
            return;
        }
        ChangePhase(Phase.WaitLive, (0.4f + (float)random.NextDouble() * 0.6f) / humanSpeed);
    }

    private void SetPattern(ScratchSymbolManager.TestPattern pattern)
    {
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
        int safeStage = Mathf.Clamp(stage, 0, 3);
        int safePattern = Mathf.Clamp(patternIndex, 0, TestPatterns[safeStage].Length - 1);
        string num = game == null ? "-" : game.TicketNumber.ToString();
        string tier = game == null ? "-" : game.CurrentTicketKind.ToString();
        string match = game == null ? "-" : game.CurrentMatch.Tier.ToString();
        string wallet = economy == null ? "-" : economy.Coins.ToString();
        string payout = game == null ? "-" : game.TicketPayout.ToString();
        ledger.Add(DateTime.UtcNow.ToString("o") + ";" + Safe(kind) + ";" +
            num + ";" + tier + ";" +
            (farming ? "Random" : TestPatterns[safeStage][safePattern].ToString()) + ";" +
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

    private void WriteReport()
    {
        if (string.IsNullOrEmpty(reportDirectory)) return;
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(Path.Combine(reportDirectory, "tickets.csv"), ledger, Encoding.UTF8);
        File.WriteAllLines(Path.Combine(reportDirectory, "checks.csv"), checks, Encoding.UTF8);
        string summary = "Scratch to Riches V2 - Auto QA\n" +
            "Stato: " + lastStatus + "\n" +
            "Seed: " + seed + "\n" +
            "Durata reale (s): " + (Time.realtimeSinceStartup - startedAt).ToString("F2", CultureInfo.InvariantCulture) + "\n" +
            "Biglietti di test: " + tickets + "/18\n" +
            "Biglietti farm: " + farmTickets + "\n" +
            "Biglietti giocati totali: " + (tickets + farmTickets) + "\n" +
            "Check OK: " + assertionsPassed + "\nCheck KO: " + assertionsFailed + "\n" +
            "Saldo finale: " + (economy != null ? economy.Coins.ToString() : "n/a") + "\n" +
            "Credito QA incluso: " + qaFunds + "\n" +
            "Video: " + videoStatus + "\n" +
            "Video file: " + (recordVideo ? videoOutputPath : "Non richiesto") + "\n" +
            "Formato: " + (recordVideo ? "H.264 MP4, " + videoHeight + "p, " + videoFrameRate + " fps" : "N/A") + "\n" +
            "Audio: " + (recordVideo && recordGameAudio) + "\n" +
            "Scelte ragionate (partite casuali): " + informedChoices + "\n" +
            "Scelte con anteprima: " + previewChoices + "\n" +
            "Scelte con anteprima che corrisponde a un simbolo grattato: " + previewMatchingChoices + "\n" +
            "Esplorazioni deliberate: " + randomExplorationChoices + "\n" +
            "Frames PNG richiesti: " + frameCount + "\n";
        File.WriteAllText(Path.Combine(reportDirectory, "summary.txt"), summary, Encoding.UTF8);
    }

}
#endif
