#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
internal static partial class ScratchTransitionQA
{
    private static ScratchGameManager game;
    private static ScratchFlowPresentation flow;
    private static ScratchVintageGameplayHUD hud;
    private static ScratchAutoplayMovieRecorder movie;
    private static Coroutine routine;
    private static ScratchCell pressed;
    private static PointerEventData pointer;
    private static string directory, output, failure;
    private static double deadline, verifyUntil;
    private static bool running, validating, stopOk, previousBackground, balanceOnly;
    private static int frames, tickets;
    private static string previous;
    private static readonly List<string> rows = new List<string>();
    public static bool Busy => running || validating;

    static ScratchTransitionQA()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        { if (state == PlayModeStateChange.ExitingPlayMode && running) Finish("Play Mode terminato"); };
        AssemblyReloadEvents.beforeAssemblyReload += () => { if (running) Finish("Reload script"); };
    }

    [MenuItem("Tools/Scratch to Riches/Test rapido transizioni")]
    public static void Start() => StartTest(false);

    [MenuItem("Tools/Scratch to Riches/Test saldo insufficiente (senza video)")]
    public static void StartBalance() => StartTest(true);

    private static void StartTest(bool onlyBalance)
    {
        if (Busy || ScratchAutoplayWindow.AnyTestRunning || ScratchManualTelemetry.IsRecording) return;
        game = UnityEngine.Object.FindAnyObjectByType<ScratchGameManager>();
        flow = game != null ? game.GetComponent<ScratchFlowPresentation>() : null;
        hud = game != null ? game.GetComponent<ScratchVintageGameplayHUD>() : null;
        if (!EditorApplication.isPlaying || game == null || flow == null || hud == null ||
            game.gameObject.scene.path != "Assets/Scenes/ScratchToRiches.unity" ||
            !flow.CanShowTicketChoices || !game.WaitingForTicketChoice || EventSystem.current == null)
        {
            EditorUtility.DisplayDialog("Test transizioni", "Apri ScratchToRiches in Play Mode sulla selezione dei biglietti (non sul finale).", "OK");
            return;
        }
        if (onlyBalance && !TryPaidTicket(out _))
        {
            EditorUtility.DisplayDialog("Test saldo", "Serve un biglietto a pagamento sbloccato e acquistabile. Torna alla selezione dopo averlo sbloccato.", "OK");
            return;
        }
        if (onlyBalance)
        {
            if (!EditorUtility.DisplayDialog("Test saldo", "Un biglietto reale senza video. La vincita e il costo reali restano salvati; il saldo di prova viene ripristinato subito.", "Avvia", "Annulla")) return;
        }
        else if (!EditorUtility.DisplayDialog("Test transizioni", "Test breve con tre o quattro biglietti reali, riacquisto diretto, selezione, negozio e controlli economici se disponibili. Il test non resetta i progressi; incassi e un eventuale acquisto C11 legale restano salvati. Procedere?", "Avvia", "Annulla")) return;
        directory = Path.Combine(Application.persistentDataPath, "TransitionQA",
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(directory);
        previewOnly = false;
        balanceOnly = onlyBalance;
        if (!balanceOnly)
        {
            movie = new ScratchAutoplayMovieRecorder();
            if (!movie.Start(directory,1080,24,false,"transizioni",3f,true))
            { Debug.LogError("TRANSITION QA: " + movie.LastError); return; }
            output = movie.OutputPath;
        }
        rows.Clear();
        rows.Add("frame;event;detail");
        frames = tickets = 0;
        previous = failure = "";
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        running = true;
        deadline = EditorApplication.timeSinceStartup + 180;
        routine = flow.StartCoroutine(Run());
    }

    private static void Tick()
    {
        if (running && (!EditorApplication.isPlaying || flow == null || EditorApplication.timeSinceStartup >= deadline))
            Finish("Timeout / scena chiusa");
        if (!validating) return;
        try
        {
            long bytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            if (bytes >= 4096 && ScratchAutoplayVideoBudget.TryDuration(output, out double duration))
            {
                validating = false;
                bool valid = stopOk && bytes < 400000000L && string.IsNullOrEmpty(failure);
                rows.Add(frames + ";VIDEO;" + bytes + " bytes|" + duration.ToString("F2") + " s|" + (valid ? "PASS" : "FAIL"));
                Save();
                if (valid) Debug.Log("TRANSITION QA PASS: " + directory);
                else Debug.LogError("TRANSITION QA FAIL: " + failure + " " + directory);
            }
            else if (EditorApplication.timeSinceStartup >= verifyUntil)
            {
                validating = false;
                rows.Add(frames + ";VIDEO FAIL;MP4 non finalizzato");
                Save();
                Debug.LogError("TRANSITION QA: MP4 non finalizzato " + directory);
            }
        }
        catch (Exception ex) { validating = false; Debug.LogError(ex); }
    }

    private static IEnumerator Run()
    {
        yield return new WaitForEndOfFrame();
        if (!Check("initial_choice")) yield break;
        if (!CheckPopupClassification()) { Finish("Classificazione popup errata"); yield break; }
        yield return Observe(.65f);
        if (!running) yield break;
        bool paidChoice = false, paidRepeat = false, paidBlocked = false;
        int rounds = balanceOnly ? 1 : 3;
        for (int round = 0; round < rounds; round++)
        {
            var kind = ScratchGameManager.TicketKind.Standard;
            if (balanceOnly && !TryPaidTicket(out kind))
            { Finish("Nessun biglietto a pagamento acquistabile"); yield break; }
            if (!balanceOnly && round == 2)
                for (int i = 3; i >= 1; i--)
                    if (game.CanChooseTicket((ScratchGameManager.TicketKind)i) &&
                        ScratchGameManager.TicketPrice((ScratchGameManager.TicketKind)i) > 0)
                    { kind = (ScratchGameManager.TicketKind)i; break; }
            if (round != 1 && round != 3)
            {
                var wallet = game.GetComponent<ScratchEconomyManager>();
                int balance = wallet.Coins, number = game.TicketNumber;
                int price = ScratchGameManager.TicketPrice(kind);
                if (!game.ChooseNextTicket(kind)) { Finish("Scelta non riuscita"); yield break; }
                if (price > 0)
                {
                    if (wallet.Coins != balance - price || game.TicketNumber != number + 1 ||
                        game.CurrentTicketKind != kind)
                    { Finish("Scelta: costo, numero o tipo biglietto errato"); yield break; }
                    paidChoice = true;
                    rows.Add(frames + ";PAID_CHOICE;PASS|" + kind + "|cost=" + price +
                        "|balance=" + balance + "->" + wallet.Coins);
                }
            }
            yield return Observe(.85f);
            if (!running) yield break;
            if (!flow.CanScratch) { Finish("Ingresso biglietto"); yield break; }
            var cells = game.GetComponent<ScratchCellManager>();
            var canvas = game.GetComponent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var cell in cells.ActiveCells)
            {
                if (!game.TicketActive) break;
                var raw = cell.GetComponentInChildren<RawImage>(true);
                if (raw == null) { Finish("Patina assente"); yield break; }
                var rt = raw.rectTransform;
                pointer = new PointerEventData(EventSystem.current) { pointerId = -903, button = PointerEventData.InputButton.Left };
                for (int step = -1; step < 360 && !cell.IsComplete; step++)
                {
                    int scan = Mathf.Max(0, step) % 180;
                    int row = scan / 15, col = scan % 15;
                    float u = step < 0 ? .5f : .04f + (row % 2 == 0 ? col : 14-col)*.92f/14;
                    float v = step < 0 ? .5f : .07f + row*.86f/11;
                    Rect r = rt.rect;
                    pointer.position = RectTransformUtility.WorldToScreenPoint(cam,
                        rt.TransformPoint(new Vector3(Mathf.Lerp(r.xMin,r.xMax,u),Mathf.Lerp(r.yMin,r.yMax,v),0)));
                    if (pressed == null) { pressed = cell; cell.OnPointerDown(pointer); }
                    else cell.OnDrag(pointer);
                    yield return new WaitForEndOfFrame();
                    if (!Check("scratch")) yield break;
                }
                Release();
                if (!cell.IsComplete) { Finish("Casella non completata"); yield break; }
            }
            if (game.TicketActive || !flow.ShowingResult) { Finish("Risultato assente"); yield break; }
            tickets++;
            yield return Observe(2.25f);
            if (!running) yield break;
            if (game.DemoFinished) { Finish("Finale raggiunto durante test; shop non verificato"); yield break; }
            var serializedFlow = new SerializedObject(flow);
            var repeatButton = serializedFlow.FindProperty("repeatTicketButton").objectReferenceValue as Button;
            var upgradeButton = serializedFlow.FindProperty("resultUpgradesButton").objectReferenceValue as Button;
            var upgradeLabel = serializedFlow.FindProperty("resultUpgradesLabel").objectReferenceValue as TMPro.TMP_Text;
            var tree = game.GetComponent<ScratchSkillTree>();
            var economy = game.GetComponent<ScratchEconomyManager>();
            var popup = serializedFlow.FindProperty("resultPopup").objectReferenceValue as GameObject;
            var badge = serializedFlow.FindProperty("resultUpgradeBadge").objectReferenceValue as GameObject;
            var badgeCount = serializedFlow.FindProperty("resultUpgradeCount").objectReferenceValue as TMPro.TMP_Text;
            if (popup == null || !popup.activeInHierarchy ||
                repeatButton == null || upgradeButton == null || upgradeLabel == null ||
                !repeatButton.transform.IsChildOf(popup.transform) ||
                !upgradeButton.transform.IsChildOf(popup.transform) ||
                !repeatButton.gameObject.activeInHierarchy || !upgradeButton.gameObject.activeInHierarchy ||
                repeatButton.interactable != game.CanChooseTicket(game.CurrentTicketKind) ||
                !upgradeButton.interactable || badge == null || badgeCount == null ||
                badge.activeInHierarchy != (tree.PurchasableCount > 0) ||
                badgeCount.text != tree.PurchasableCount.ToString() ||
                flow.ShowingTicketArtwork)
            { Finish("QoL: azioni o conteggio risultato non validi"); yield break; }
            rows.Add(frames + ";QOL_POPUP;PASS|ticket hidden|badge exact");
            if (!CheckPopupAppearance(serializedFlow, popup))
            { Finish("Popup: contenuto, simboli o dimensioni non validi"); yield break; }
            if (!balanceOnly)
            {
                var snapshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (snapshot == null) { Finish("Screenshot popup assente"); yield break; }
                string file = "popup_" + tickets.ToString("D2") + "_" + game.CurrentMatch.Tier + ".png";
                try
                {
                    File.WriteAllBytes(Path.Combine(directory, file), snapshot.EncodeToPNG());
                    rows.Add(frames + ";POPUP_PNG;PASS|" + file + "|" + snapshot.width + "x" + snapshot.height);
                }
                finally { UnityEngine.Object.Destroy(snapshot); }
            }
            int currentPrice = ScratchGameManager.TicketPrice(game.CurrentTicketKind);
            if (balanceOnly)
            {
                string reason = TestBalanceBoundary(economy, repeatButton, serializedFlow, currentPrice);
                Finish(reason);
                yield break;
            }
            if (currentPrice > 0 && economy.Coins < currentPrice)
            {
                int number = game.TicketNumber, balance = economy.Coins;
                var previousKind = game.CurrentTicketKind;
                var repeatLabel = serializedFlow.FindProperty("repeatTicketLabel").objectReferenceValue as TMPro.TMP_Text;
                if (repeatButton.interactable || repeatLabel == null ||
                    !repeatLabel.text.Contains("SALDO INSUFFICIENTE"))
                { Finish("Saldo insufficiente: pulsante o testo errato"); yield break; }
                flow.RepeatAfterResult();
                yield return Observe(.45f);
                if (!running) yield break;
                if (!flow.ResultPopupVisible || !flow.ShowingResult || game.TicketActive ||
                    economy.Coins != balance || game.TicketNumber != number ||
                    game.CurrentTicketKind != previousKind)
                { Finish("Saldo insufficiente: riacquisto non bloccato"); yield break; }
                paidBlocked = true;
                rows.Add(frames + ";PAID_REPEAT_BLOCKED;PASS|" + previousKind +
                    "|cost=" + currentPrice + "|balance=" + balance);
            }
            if (round == 0 || (round == 2 && currentPrice > 0 && repeatButton.interactable))
            {
                int number = game.TicketNumber, balance = economy.Coins;
                var previousKind = game.CurrentTicketKind;
                int price = ScratchGameManager.TicketPrice(previousKind);
                repeatButton.onClick.Invoke();
                flow.RepeatAfterResult(); // a duplicate input must not buy twice
                yield return Observe(1.25f);
                if (!running) yield break;
                if (!flow.CanScratch || !game.TicketActive || game.CurrentTicketKind != previousKind ||
                    game.TicketNumber != number + 1 || economy.Coins != balance - price)
                { Finish("QoL: riacquisto o addebito duplicato"); yield break; }
                rows.Add(frames + ";QOL_REPEAT;PASS");
                if (price > 0)
                {
                    paidRepeat = true;
                    rounds = 4;
                    rows.Add(frames + ";PAID_REPEAT;PASS|" + previousKind + "|cost=" + price +
                        "|balance=" + balance + "->" + economy.Coins);
                }
                continue;
            }
            if (round == 1)
            {
                upgradeButton.onClick.Invoke();
                rows.Add(frames + ";QOL_DIRECT_SHOP;requested");
            }
            else
            {
                flow.ContinueAfterResult();
                yield return Observe(.85f);
                if (!running) yield break;
                if (!flow.CanOpenShop) { Finish("Selezione dopo risultato assente"); yield break; }
                rows.Add(frames + ";QOL_CHOOSE_OTHER;PASS");
                flow.OpenShop();
            }
            yield return Observe(.95f);
            if (!running) yield break;
            if (!flow.ShopVisible) { Finish("Shop non visibile"); yield break; }
            if (round == 1) rows.Add(frames + ";QOL_DIRECT_SHOP;PASS");
            var skills = game.GetComponent<ScratchSkillTree>();
            int before = game.MaxScratches;
            if (round == 1 && skills.CanPurchase("C11"))
            {
                if (!skills.TryPurchase("C11")) { Finish("Acquisto C11 fallito"); yield break; }
                rows.Add(frames + ";C11;" + before + "->" + game.MaxScratches);
                if (game.MaxScratches <= before) { Finish("C11: limite non aumentato"); yield break; }
            }
            else rows.Add(frames + ";C11_SKIP;Non disponibile o secondo passaggio");
            yield return Observe(.8f);
            if (!running) yield break;
            flow.ContinueAfterUpgrades();
            yield return Observe(.95f);
            if (!running) yield break;
            if (!flow.CanShowTicketChoices) { Finish("Ritorno selezione fallito"); yield break; }
        }
        rows.Add(frames + ";ECONOMY_COVERAGE;paid_choice=" + (paidChoice ? "PASS" : "NOT_COVERED") +
            "|paid_repeat=" + (paidRepeat ? "PASS" : "NOT_COVERED") +
            "|insufficient_balance=" + (paidBlocked ? "PASS" : "NOT_COVERED"));
        Finish("");
    }

    private static bool CheckPopupClassification()
    {
        string[] names = { "RIMBORSO", "COPPIA", "TRIS", "POKER", "CINQUINA" };
        for (int i = 0; i < names.Length; i++)
            if (ScratchFlowPresentation.PopupOutcome((ScratchSymbolManager.MatchTier)i, 10, false) != names[i])
                return false;
        if (ScratchFlowPresentation.PopupOutcome(ScratchSymbolManager.MatchTier.None, 0, false) != "NESSUNA VINCITA" ||
            ScratchFlowPresentation.PopupOutcome(ScratchSymbolManager.MatchTier.Five, 0, true) != "BIGLIETTO DI PROVA") return false;
        rows.Add(frames + ";POPUP_CLASSIFICATION;PASS|7 synthetic presentation cases");
        return true;
    }

    private static bool CheckPopupAppearance(SerializedObject so, GameObject popup)
    {
        var outcome = so.FindProperty("resultOutcomeLabel").objectReferenceValue as TMPro.TMP_Text;
        var amount = so.FindProperty("resultPopupAmount").objectReferenceValue as TMPro.TMP_Text;
        var unit = so.FindProperty("resultPopupUnit").objectReferenceValue as TMPro.TMP_Text;
        var net = so.FindProperty("resultPopupNet").objectReferenceValue as TMPro.TMP_Text;
        var heading = so.FindProperty("resultPopupHeading").objectReferenceValue as TMPro.TMP_Text;
        var icons = so.FindProperty("resultMatchIcons");
        var match = game.CurrentMatch;
        int payout = game.TicketPayout;
        if (outcome == null || amount == null || unit == null || net == null || heading == null || icons == null || icons.arraySize != 5 ||
            !outcome.gameObject.activeInHierarchy || !amount.gameObject.activeInHierarchy ||
            !heading.gameObject.activeInHierarchy ||
            outcome.text != ScratchFlowPresentation.PopupOutcome(match.Tier, payout, game.TicketVoided) ||
            heading.text != "BIGLIETTO " + ScratchGameManager.TicketDisplayName(game.CurrentTicketKind) ||
            amount.text != (payout > 0 ? "+" : "") + payout.ToString("N0") ||
            unit.text != "MONETE" ||
            net.text != ScratchFlowPresentation.PopupNet(payout, ScratchGameManager.TicketPrice(game.CurrentTicketKind), game.TicketVoided)) return false;
        bool winning = !game.TicketVoided && payout > 0 && match.IsMatch;
        bool profitable = winning && payout > ScratchGameManager.TicketPrice(game.CurrentTicketKind);
        Color expected = profitable ? new Color32(234,194,92,255) : new Color32(192,205,195,255);
        if (outcome.color != expected || amount.color != expected || unit.color != expected) return false;
        var sprites = new List<Sprite>();
        var jolly = so.FindProperty("resultJollySprite").objectReferenceValue as Sprite;
        if (winning)
            foreach (var cell in game.GetComponent<ScratchCellManager>().ActiveCells)
                if (cell != null && cell.IsComplete && game.IsCellSelected(cell) &&
                    cell.TryGetVisibleSymbol(out string symbol) && (symbol == match.Symbol || symbol == "JOLLY"))
                    sprites.Add(symbol == "JOLLY" ? jolly : cell.RevealedArtwork);
        int count = winning ? Mathf.Min(5, Mathf.Min(match.MatchingCount, sprites.Count)) : 0;
        for (int i = 0; i < icons.arraySize; i++)
        {
            var icon = icons.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (icon == null || !icon.transform.IsChildOf(popup.transform) ||
                icon.gameObject.activeInHierarchy != (i < count) ||
                (i < count && (icon.sprite == null || icon.sprite != sprites[i]))) return false;
        }
        Canvas.ForceUpdateCanvases();
        foreach (var text in new[] { heading, outcome, amount, unit, net })
        {
            if (string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) return false;
        }
        rows.Add(frames + ";POPUP_VISUAL_DATA;PASS|" + outcome.text + "|payout=" + payout + "|icons=" + count);
        return true;
    }

    private static bool TryPaidTicket(out ScratchGameManager.TicketKind kind)
    {
        for (int i = 1; i <= 3; i++)
        {
            kind = (ScratchGameManager.TicketKind)i;
            if (game.CanChooseTicket(kind)) return true;
        }
        kind = ScratchGameManager.TicketKind.Standard;
        return false;
    }

    private static string TestBalanceBoundary(ScratchEconomyManager wallet, Button repeat,
        SerializedObject serializedFlow, int price)
    {
        int original = wallet.Coins, number = game.TicketNumber, payout = game.TicketPayout;
        long earned = wallet.TotalEarned;
        var kind = game.CurrentTicketKind;
        var label = serializedFlow.FindProperty("repeatTicketLabel").objectReferenceValue as TMPro.TMP_Text;
        string reason = "";
        try
        {
            if (price <= 0 || label == null) throw new InvalidOperationException("Prezzo o label non valido");
            SetTestBalance(wallet, price);
            if (!repeat.interactable || !game.CanChooseTicket(kind) ||
                label.text.Contains("SALDO INSUFFICIENTE"))
                throw new InvalidOperationException("Saldo esatto: riacquisto non disponibile");
            rows.Add(frames + ";EXACT_BALANCE;PASS|cost=" + price + "|balance=" + wallet.Coins);
            SetTestBalance(wallet, price - 1);
            if (repeat.interactable || game.CanChooseTicket(kind) ||
                !label.text.Contains("SALDO INSUFFICIENTE"))
                throw new InvalidOperationException("Saldo insufficiente: pulsante o testo errato");
            repeat.onClick.Invoke();
            flow.RepeatAfterResult();
            if (!flow.ResultPopupVisible || !flow.ShowingResult || game.TicketActive ||
                wallet.Coins != price - 1 || game.TicketNumber != number ||
                game.CurrentTicketKind != kind || game.TicketPayout != payout || wallet.TotalEarned != earned)
                throw new InvalidOperationException("Saldo insufficiente: richiesta non bloccata");
            rows.Add(frames + ";PAID_REPEAT_BLOCKED;PASS|" + kind + "|cost=" + price + "|balance=" + wallet.Coins);
        }
        catch (Exception ex) { reason = ex.Message; }
        finally
        {
            try
            {
                SetTestBalance(wallet, original);
                if (wallet.TotalEarned != earned || repeat.interactable != game.CanChooseTicket(kind) ||
                    label == null || label.text.Contains("SALDO INSUFFICIENTE") == game.CanChooseTicket(kind))
                    throw new InvalidOperationException("Stato dopo ripristino non coerente");
                rows.Add(frames + ";BALANCE_RESTORED;PASS|balance=" + wallet.Coins);
            }
            catch (Exception ex) { reason += " | Ripristino: " + ex.Message; }
        }
        return reason;
    }

    private static void SetTestBalance(ScratchEconomyManager wallet, int target)
    {
        int current = wallet.Coins;
        if (current > target) wallet.TrySpendCoins(current - target);
        else if (current < target) wallet.CreditForAutoplayTest(target - current);
        if (wallet.Coins != target) throw new InvalidOperationException("Saldo di test non impostato");
    }

    private static IEnumerator Observe(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        do
        {
            yield return new WaitForEndOfFrame();
            if (!Check("transition")) yield break;
        } while (running && Time.realtimeSinceStartup < until);
    }

    private static bool Check(string context)
    {
        if (!running) return false;
        frames++;
        bool ok = hud.ValidatePhaseVisibility(out string detail);
        if (detail != previous || !ok)
        {
            rows.Add(frames + ";" + (ok ? "PASS" : "FAIL") + ";" + context + "|" + detail);
            previous = detail;
        }
        if (!ok) Finish("Invariante UI: " + detail);
        return ok;
    }

    private static void Release()
    {
        if (pressed != null && pointer != null) pressed.OnPointerUp(pointer);
        pressed = null;
        pointer = null;
    }

    private static void Finish(string reason)
    {
        if (!running) return;
        running = false;
        failure = reason;
        Release();
        if (routine != null && flow != null) flow.StopCoroutine(routine);
        routine = null;
        Application.runInBackground = previousBackground;
        stopOk = movie != null && movie.Stop();
        movie = null;
        rows.Add(frames + ";END;" + tickets + " biglietti|" + (reason.Length == 0 ? "PASS" : reason));
        Save();
        if (previewOnly)
        {
            if (flow != null) flow.EndPopupPreview();
            validating = false;
            previewOnly = false;
            if (reason.Length == 0) Debug.Log("POPUP VISUAL QA PASS: " + directory);
            else Debug.LogError("POPUP VISUAL QA FAIL: " + reason + " " + directory);
            return;
        }
        if (balanceOnly)
        {
            validating = false;
            if (reason.Length == 0) Debug.Log("BALANCE QA PASS: " + directory);
            else Debug.LogError("BALANCE QA FAIL: " + reason + " " + directory);
            return;
        }
        validating = true;
        verifyUntil = EditorApplication.timeSinceStartup + 45;
    }

    private static void Save()
    {
        File.WriteAllLines(Path.Combine(directory, "transition_checks.csv"), rows);
    }
}
#endif
