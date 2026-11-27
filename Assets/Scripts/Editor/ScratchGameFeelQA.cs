#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Pattern = ScratchSymbolManager.TestPattern;
using Tier = ScratchSymbolManager.MatchTier;
using Kind = ScratchGameManager.TicketKind;

[InitializeOnLoad]
internal static partial class ScratchGameFeelQA
{
    private const string LaunchedKey = "STR.GameFeelQA.Launched";
    private static ScratchGameManager game;
    private static ScratchFlowPresentation flow;
    private static ScratchEconomyManager wallet;
    private static ScratchSkillTree skills;
    private static ScratchCellManager cells;
    private static ScratchSymbolManager symbols;
    private static ScratchWinJuice juice;
    private static ScratchVintageGameplayHUD hud;
    private static Image[] coins, flakes;
    private static TMP_Text walletLabel;
    private static ScratchAutoplayMovieRecorder recorder;
    private static Coroutine routine;
    private static ScratchCell pressed;
    private static PointerEventData pointer;
    private static string directory, video, failure;
    private static bool running, validating, stopOk, previousBackground, leaving;
    private static double deadline, verifyUntil, readyUntil;
    private static int frameCount, passed;
    private static readonly List<string> rows = new List<string>();
    private static int peakCoins, peakVisibleCoins, peakFlakes, flakeFrames, coinFrames;
    private static bool sawPayout;
    private static float firstStamp;
    private static int firstSteps;

    static ScratchGameFeelQA()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                readyUntil = EditorApplication.timeSinceStartup + 20;
            if (change == PlayModeStateChange.ExitingPlayMode && running)
                Finish("Play Mode interrotto", false);
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                leaving = false;
                ScratchGameFeelPrefs.End();
                SessionState.SetBool(LaunchedKey, false);
            }
        };
        AssemblyReloadEvents.beforeAssemblyReload += () => { if (running) Finish("Reload script", false); };
    }

    [MenuItem("Tools/Scratch to Riches/Test Game Feel deterministico (video)")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ScratchTransitionQA.Busy ||
            ScratchAutoplayWindow.AnyTestRunning || ScratchManualTelemetry.IsRecording)
        { EditorUtility.DisplayDialog("Game Feel QA", "Esci dal Play Mode e ferma gli altri test.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/ScratchToRiches.unity" || scene.isDirty ||
            (EditorSettings.enterPlayModeOptionsEnabled &&
             (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0))
        { EditorUtility.DisplayDialog("Game Feel QA", "Apri e salva ScratchToRiches. Scene Reload deve essere attivo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Game Feel QA", "11 biglietti deterministici nella scena reale. Profilo temporaneo in memoria, nessun incasso nel salvataggio normale. Il test entra ed esce dal Play Mode automaticamente. Lascia attiva la Game View.", "Avvia", "Annulla")) return;
        leaving = false;
        ScratchGameFeelPrefs.Begin();
        SessionState.SetBool(LaunchedKey, false);
        readyUntil = EditorApplication.timeSinceStartup + 30;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (leaving) return;
        if (validating)
        {
            try
            {
                long bytes = File.Exists(video) ? new FileInfo(video).Length : 0;
                if (bytes >= 4096 && ScratchAutoplayVideoBudget.TryDuration(video, out double duration))
                {
                    bool ok = stopOk && bytes < 400000000L && string.IsNullOrEmpty(failure) && passed == 11;
                    rows.Add("VIDEO;" + (ok ? "PASS" : "FAIL") + ";bytes=" + bytes + "|seconds=" + duration.ToString("F2", CultureInfo.InvariantCulture));
                    Save();
                    validating = false;
                    if (ok) Debug.Log("GAME FEEL QA PASS: " + directory);
                    else Debug.LogError("GAME FEEL QA FAIL: " + failure + " " + directory);
                    ExitTestPlayMode();
                }
                else if (EditorApplication.timeSinceStartup >= verifyUntil)
                {
                    rows.Add("VIDEO;FAIL;MP4 non finalizzato");
                    Save(); validating = false;
                    Debug.LogError("GAME FEEL QA: MP4 non finalizzato " + directory);
                    ExitTestPlayMode();
                }
            }
            catch (Exception ex)
            { validating = false; Debug.LogError(ex); ExitTestPlayMode(); }
            return;
        }
        if (!ScratchGameFeelPrefs.Active || !EditorApplication.isPlaying) return;
        if (running)
        {
            if (flow == null || EditorApplication.timeSinceStartup >= deadline) Finish("Timeout / scena chiusa");
            return;
        }
        if (SessionState.GetBool(LaunchedKey, false))
        {
            // An unexpected domain reload must not resume or run a second test.
            Debug.LogError("Game Feel QA interrotto: ritorno al profilo normale.");
            ExitTestPlayMode();
            return;
        }
        game = UnityEngine.Object.FindAnyObjectByType<ScratchGameManager>();
        flow = game != null ? game.GetComponent<ScratchFlowPresentation>() : null;
        if (flow == null || !flow.CanShowTicketChoices || EventSystem.current == null)
        {
            if (readyUntil == 0) readyUntil = EditorApplication.timeSinceStartup + 20;
            if (EditorApplication.timeSinceStartup >= readyUntil)
            { Debug.LogError("Game Feel QA: selezione iniziale non disponibile."); ExitTestPlayMode(); }
            return;
        }
        SessionState.SetBool(LaunchedKey, true);
        try { BeginRun(); }
        catch (Exception ex) { if (running) Finish(ex.Message); else { Debug.LogError(ex); ExitTestPlayMode(); } }
    }

    private static void BeginRun()
    {
        wallet = game.GetComponent<ScratchEconomyManager>();
        skills = game.GetComponent<ScratchSkillTree>();
        cells = game.GetComponent<ScratchCellManager>();
        symbols = game.GetComponent<ScratchSymbolManager>();
        juice = game.GetComponent<ScratchWinJuice>();
        hud = game.GetComponent<ScratchVintageGameplayHUD>();
        directory = Path.Combine(Application.persistentDataPath, "GameFeelQA",
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(directory);
        rows.Clear(); rows.Add("case;status;detail");
        rows.Add("PROFILE;INFO;isolated in-memory save|seed=41026|forced symbols only");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        frameCount = passed = 0; failure = "";
        video = null; recorder = null; stopOk = false;
        deadline = EditorApplication.timeSinceStartup + 420;
        running = true;
        if (wallet == null || skills == null || cells == null || symbols == null || juice == null || hud == null ||
            game.gameObject.scene.path != "Assets/Scenes/ScratchToRiches.unity" || wallet.Coins != 500 ||
            wallet.TotalEarned != 0 || skills.GetLevel("C03") != 1)
        { Finish("Profilo QA o riferimenti scena non validi"); return; }
        var so = new SerializedObject(juice);
        coins = Images(so.FindProperty("winCoins"));
        flakes = Images(so.FindProperty("scratchFlakes"));
        walletLabel = so.FindProperty("walletLabel").objectReferenceValue as TMP_Text;
        if (coins.Length != 10 || flakes.Length != 8 || walletLabel == null ||
            Array.Exists(coins, x => x == null) || Array.Exists(flakes, x => x == null))
        { Finish("Pool effetti non conforme"); return; }
        UnityEngine.Random.InitState(41026);
        recorder = new ScratchAutoplayMovieRecorder();
        if (!recorder.Start(directory,1080,30,false,"game_feel",3f,true))
        { Finish("Recorder: " + recorder.LastError); return; }
        video = recorder.OutputPath;
        routine = flow.StartCoroutine(Run());
    }

    private static Image[] Images(SerializedProperty property)
    {
        var result = new Image[property != null && property.isArray ? property.arraySize : 0];
        for (int i = 0; i < result.Length; i++) result[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as Image;
        return result;
    }

    private static void Finish(string reason, bool validate = true)
    {
        if (!running) return;
        running = false; failure = reason;
        Release();
        if (routine != null && flow != null) flow.StopCoroutine(routine);
        routine = null;
        Application.runInBackground = previousBackground;
        stopOk = recorder != null && recorder.Stop();
        recorder = null;
        rows.Add("END;" + (reason.Length == 0 && passed == 11 ? "PASS" : "FAIL") +
            ";cases=" + passed + "/11|frames=" + frameCount + "|" + reason.Replace(';', ','));
        Save();
        validating = validate && !string.IsNullOrEmpty(video);
        verifyUntil = EditorApplication.timeSinceStartup + 45;
        if (!validating)
        {
            Debug.LogError("GAME FEEL QA FAIL: " + reason + " " + directory);
            if (validate && EditorApplication.isPlaying) ExitTestPlayMode();
        }
    }

    private static void ExitTestPlayMode()
    {
        if (!EditorApplication.isPlaying || leaving) return;
        leaving = true;
        EditorApplication.ExitPlaymode();
    }

    private static void Save() => File.WriteAllLines(Path.Combine(directory, "gamefeel_checks.csv"), rows);
}
#endif
