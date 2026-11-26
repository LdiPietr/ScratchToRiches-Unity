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
internal static class ScratchTransitionQA
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
    private static bool running, validating, stopOk, previousBackground;
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
    public static void Start()
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
        if (!EditorUtility.DisplayDialog("Test transizioni", "Test breve con tre biglietti reali, riacquisto diretto, selezione e negozio. Il test non resetta i progressi; incassi e un eventuale acquisto C11 legale restano salvati. Procedere?", "Avvia", "Annulla")) return;
        directory = Path.Combine(Application.persistentDataPath, "TransitionQA",
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(directory);
        movie = new ScratchAutoplayMovieRecorder();
        if (!movie.Start(directory,1080,24,false,"transizioni",3f,true))
        { Debug.LogError("TRANSITION QA: " + movie.LastError); return; }
        output = movie.OutputPath;
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
        yield return Observe(.65f);
        if (!running) yield break;
        for (int round = 0; round < 3; round++)
        {
            var kind = ScratchGameManager.TicketKind.Standard;
            if (round == 2)
                for (int i = 3; i >= 1; i--)
                    if (game.CanChooseTicket((ScratchGameManager.TicketKind)i))
                    { kind = (ScratchGameManager.TicketKind)i; break; }
            if (round != 1 && !game.ChooseNextTicket(kind)) { Finish("Scelta non riuscita"); yield break; }
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
            yield return Observe(1.25f);
            if (!running) yield break;
            if (game.DemoFinished) { Finish("Finale raggiunto durante test; shop non verificato"); yield break; }
            var serializedFlow = new SerializedObject(flow);
            var repeatButton = serializedFlow.FindProperty("repeatTicketButton").objectReferenceValue as Button;
            var upgradeButton = serializedFlow.FindProperty("resultUpgradesButton").objectReferenceValue as Button;
            var upgradeLabel = serializedFlow.FindProperty("resultUpgradesLabel").objectReferenceValue as TMPro.TMP_Text;
            var tree = game.GetComponent<ScratchSkillTree>();
            var economy = game.GetComponent<ScratchEconomyManager>();
            if (repeatButton == null || upgradeButton == null || upgradeLabel == null ||
                !repeatButton.gameObject.activeInHierarchy || !upgradeButton.gameObject.activeInHierarchy ||
                repeatButton.interactable != game.CanChooseTicket(game.CurrentTicketKind) ||
                !upgradeButton.interactable || !upgradeLabel.text.Contains(tree.PurchasableCount + " ACQUISTABIL"))
            { Finish("QoL: azioni o conteggio risultato non validi"); yield break; }
            if (round == 0)
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
        Finish("");
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
        validating = true;
        verifyUntil = EditorApplication.timeSinceStartup + 45;
    }

    private static void Save()
    {
        File.WriteAllLines(Path.Combine(directory, "transition_checks.csv"), rows);
    }
}
#endif
