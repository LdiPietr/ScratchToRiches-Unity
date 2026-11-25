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

// Session responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private void StartTest()
    {
        autoplayMode = AutoplayMode.ProgressioneDemoCompleta;
        qaFunds = false;
        startFresh = true;
        recordVideo = true;
        videoHeight = 720;
        videoFrameRate = 30;
        finishedRunSeconds = 0f;
        videoCompression = null;
        resultVisibleSince = -1f;
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
            EditorUtility.DisplayDialog("Autoplay", "Avvia la scena in Play Mode e lascia il gioco nella selezione iniziale, senza scegliere un biglietto.", "OK");
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

        if (!ScratchVintageHudAudit.ValidateRightPanels(game.GetComponent<ScratchVintageGameplayHUD>(),
                out string hudPreflight))
        {
            Debug.LogError(hudPreflight, game);
            EditorUtility.DisplayDialog("Gameplay & Visual QA", hudPreflight, "OK");
            return;
        }
        Debug.Log(hudPreflight, game);
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
        Check(true, "hud_loaded_scene", hudPreflight);
        previousRunInBackground = Application.runInBackground;
        backgroundOverride = true;
        Application.runInBackground = true;
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
            if (!movieRecorder.Start(reportDirectory, videoHeight, videoFrameRate, recordGameAudio,
                    "autoplay", UploadVideoBitrateMbps, false))
            {
                videoStatus = "ERRORE avvio: " + movieRecorder.LastError;
                Fail(videoStatus);
                return;
            }
            videoOutputPath = movieRecorder.OutputPath;
            videoStatus = "Registrazione H.264 in corso";
            Note("VIDEO_START", "path=" + videoOutputPath + ";fps=" + videoFrameRate +
                ";resolution=" + videoHeight + "p;audio=" + recordGameAudio +
                ";targetMbps=" + UploadVideoBitrateMbps.ToString(CultureInfo.InvariantCulture));
        }

        SetPattern(PlannedPattern(0, 0));
        game.StartNewRun();
        walletBeforeTicket = economy.Coins;
        ticketCost = 0;
        if (!game.WaitingForTicketChoice || !flow.CanShowTicketChoices || game.TicketActive || game.TicketNumber != 0)
        {
            Fail("Selezione iniziale non disponibile o biglietto creato automaticamente.");
            return;
        }
        Check(true, "initial_ticket_selection", "Nessun biglietto generato prima della scelta.");
        ChangePhase(Phase.InitialChoice, 3f);
        lastStatus = "Registrazione della selezione iniziale.";
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
        if (phase != Phase.FinalHold && IsFullDemo && purchaseIndex < FullDemoPlan.Length &&
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
            case Phase.InitialChoice:
                if (!game.ChooseNextTicket(ScratchGameManager.TicketKind.Standard))
                {
                    Fail("Impossibile selezionare il Classico iniziale.");
                    break;
                }
                ChangePhase(Phase.WaitLive, 0f);
                break;
            case Phase.FinalHold:
                Finish(finalSuccess, "Fine demo raggiunta naturalmente.");
                break;
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
}
#endif
