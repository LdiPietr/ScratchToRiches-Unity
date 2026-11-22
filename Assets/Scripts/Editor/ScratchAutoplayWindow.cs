#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        StartPurchase, WaitPurchase, CloseShop, WaitShopClosed
    }

    private static readonly ScratchSymbolManager.TestPattern[][] TestPatterns =
    {
        new[] { ScratchSymbolManager.TestPattern.Pair, ScratchSymbolManager.TestPattern.Triple,
            ScratchSymbolManager.TestPattern.Random, ScratchSymbolManager.TestPattern.Random },
        new[] { ScratchSymbolManager.TestPattern.Pair, ScratchSymbolManager.TestPattern.Triple,
            ScratchSymbolManager.TestPattern.Poker, ScratchSymbolManager.TestPattern.Random },
        new[] { ScratchSymbolManager.TestPattern.Pair, ScratchSymbolManager.TestPattern.Triple,
            ScratchSymbolManager.TestPattern.Poker, ScratchSymbolManager.TestPattern.Five,
            ScratchSymbolManager.TestPattern.Random },
        new[] { ScratchSymbolManager.TestPattern.Pair, ScratchSymbolManager.TestPattern.Triple,
            ScratchSymbolManager.TestPattern.Poker, ScratchSymbolManager.TestPattern.Five,
            ScratchSymbolManager.TestPattern.Random }
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
    private bool realtimeCheckPassed;
    private bool pointerHeld;
    private float humanSpeed = 1f;
    private float nextAction;
    private float stateSince;
    private float startedAt;
    private float ticketStartedAt;
    private float cellStartedAt;
    private float lastScreenshotAt;
    private float maxSessionMinutes = 20f;
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
        EditorApplication.update += UpdateTest;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateTest;
        if (running) Finish(false, "Tester chiuso prima del termine.");
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
            maxSessionMinutes = EditorGUILayout.Slider("Timeout massimo (min)", maxSessionMinutes, 5f, 60f);
            captureFrames = EditorGUILayout.ToggleLeft("Registra sequenza PNG + tenta MP4 (FFmpeg)", captureFrames);
            if (captureFrames) framesPerSecond = EditorGUILayout.IntSlider("FPS registrazione", framesPerSecond, 1, 5);
        }

        if (qaFunds)
            EditorGUILayout.HelpBox("Il credito QA accelera gli sblocchi ma NON vale come guadagno reale. Il test altera i salvataggi: usa RESET per tornare a zero.", MessageType.Warning);
        if (captureFrames)
            EditorGUILayout.HelpBox("Salva frame PNG della Game View. Se FFmpeg e' nel PATH tenta anche il montaggio MP4; altrimenti conserva i PNG.", MessageType.None);

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
        EditorGUILayout.LabelField("Biglietti conclusi: " + tickets + " / 18");
        EditorGUILayout.LabelField("Verifiche OK: " + assertionsPassed + "    Errori: " + assertionsFailed);
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
        realtimeCheckPassed = pointerHeld = false;
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
        ScratchGameManager.TicketKind expected = (ScratchGameManager.TicketKind)stage;
        Check(game.CurrentTicketKind == expected, "ticket_kind",
            "Atteso=" + expected + " effettivo=" + game.CurrentTicketKind);
        Check(cellManager.ActiveCells.Length == game.CurrentGridSide * game.CurrentGridSide,
            "grid_size", game.CurrentGridSide + "x" + game.CurrentGridSide);
        Check(game.MaxScratches == game.CurrentGridSide, "pick_cap", "MaxScelte=" + game.MaxScratches);
        if (stage == 3)
        {
            HashSet<string> unique = new HashSet<string>();
            foreach (ScratchCell sc in cellManager.ActiveCells)
                if (symbols.TryGetSymbol(sc, out string label)) unique.Add(label);
            Check(unique.Count <= 4 && unique.Count > 0,
                "diamond_pool", "Simboli diversi sul biglietto=" + unique.Count);
        }
        ticketStartedAt = now;
        cellIndex = 0;
        PrepareCell();
        Note("TICKET_START", game.CurrentGridSide + "x" + game.CurrentGridSide);
        ChangePhase(Phase.Scratch, 0.5f / humanSpeed);
    }

    private void PrepareCell()
    {
        ScratchCell[] cells = cellManager.ActiveCells;
        int index = TestPatterns[stage][patternIndex] == ScratchSymbolManager.TestPattern.Random
            ? random.Next(0, cells.Length)
            : cellIndex;
        if (TestPatterns[stage][patternIndex] == ScratchSymbolManager.TestPattern.Random)
        {
            // Random ticket: try unselected, incomplete cells.
            List<ScratchCell> remaining = new List<ScratchCell>();
            foreach (ScratchCell sc in cells)
                if (!sc.IsComplete && sc != activeCell) remaining.Add(sc);
            if (remaining.Count > 0) index = Array.IndexOf(cells, remaining[random.Next(remaining.Count)]);
        }
        activeCell = cells[index];
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
        var pat = TestPatterns[stage][patternIndex];
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
        if (stage == 3 && pat != ScratchSymbolManager.TestPattern.Random)
            Check(game.CurrentMatch.IsMatch, "diamond_guaranteed_match",
                "Diamante con 5 simboli grattati.");
        tickets++;
        Note("TICKET_END", "match=" + game.CurrentMatch.Tier + ";gain=" + game.TicketPayout +
            ";net=" + (game.TicketPayout - ticketCost) +
            ";seconds=" + (Time.realtimeSinceStartup - ticketStartedAt).ToString("F2", CultureInfo.InvariantCulture));
        lastStatus = "Biglietto " + tickets + ": " + game.CurrentTicketKind +
            " " + pat + " = " + game.CurrentMatch.Tier + " +" + game.TicketPayout;
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
        pressedHold.OnPointerDown(pointer);
        Note("HOLD_BEGIN", "abilita=" + id + ";prezzo=" + skills.GetPrice(id));
        ChangePhase(Phase.WaitPurchase, 0.95f);
    }

    private void EndPurchase(float now)
    {
        ScratchSkillTree.SkillId id = UnlockPlans[stage][purchaseIndex];
        if (pressedHold != null) pressedHold.OnPointerUp(pointer);
        if (!Check(skills.GetLevel(id) == expectedSkillLevel, "hold_to_buy",
            id + " livello=" + skills.GetLevel(id) + " atteso=" + expectedSkillLevel))
        {
            Fail("Acquisto fallito " + id);
            return;
        }
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
            num + ";" + tier + ";" + TestPatterns[safeStage][safePattern] + ";" +
            wallet + ";" + ticketCost + ";" + payout + ";" + match + ";" + Safe(details));
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
        if (pressedHold != null && pointer != null) pressedHold.OnPointerUp(pointer);
        pressedHold = null;
        lastStatus = (success ? "COMPLETATO: " : "FERMATO: ") + message;
        try
        {
            if (symbols != null) SetPattern(ScratchSymbolManager.TestPattern.Random);
            Note("TEST_END", lastStatus);
            File.WriteAllLines(Path.Combine(reportDirectory, "tickets.csv"), ledger, Encoding.UTF8);
            File.WriteAllLines(Path.Combine(reportDirectory, "checks.csv"), checks, Encoding.UTF8);
            string summary = "Scratch to Riches V2 - Auto QA\n" +
                "Stato: " + lastStatus + "\n" +
                "Seed: " + seed + "\n" +
                "Durata reale (s): " + (Time.realtimeSinceStartup - startedAt).ToString("F2", CultureInfo.InvariantCulture) + "\n" +
                "Biglietti: " + tickets + "/18\n" +
                "Check OK: " + assertionsPassed + "\nCheck KO: " + assertionsFailed + "\n" +
                "Saldo finale: " + (economy != null ? economy.Coins.ToString() : "n/a") + "\n" +
                "Credito QA incluso: " + qaFunds + "\n" +
                "Frames PNG richiesti: " + frameCount + "\n";
            File.WriteAllText(Path.Combine(reportDirectory, "summary.txt"), summary, Encoding.UTF8);
            if (captureFrames && frameCount > 1) TryEncodeMp4();
            Debug.Log("STR AUTO QA: " + lastStatus + " | Report: " + reportDirectory);
        }
        catch (Exception ex)
        {
            Debug.LogError("STR AUTO QA: salvataggio report fallito: " + ex);
        }
        Repaint();
    }

    private void TryEncodeMp4()
    {
        string frames = Path.Combine(reportDirectory, "frames", "frame_%06d.png");
        string mp4 = Path.Combine(reportDirectory, "autoplay.mp4");
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-hide_banner -loglevel error -y -framerate " + framesPerSecond +
                    " -i \"" + frames + "\" -c:v libx264 -pix_fmt yuv420p -movflags +faststart \"" + mp4 + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process process = Process.Start(info);
            if (process != null)
                Debug.Log("STR AUTO QA: FFmpeg avviato; verifica autoplay.mp4 nella cartella report quando termina.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("STR AUTO QA: FFmpeg non disponibile (" + ex.Message +
                "). Sequenza PNG conservata, utilizzabile per montare il video.");
        }
    }
}
#endif
