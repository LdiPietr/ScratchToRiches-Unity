#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Records a REAL human run, never controls the pointer, wallet, RNG or shop.
/// One action log spans all compact MP4 parts. Lives entirely inside Unity.
/// </summary>
public sealed partial class ScratchManualPlaytestRecorderWindow : EditorWindow
{
    private const int SegmentSeconds = 30 * 60;
    // Show the actual last-ticket reward and end screen in the recorded video.
    private const float FinaleVideoHoldSeconds = 10f;
    private const int VideoHeight = 720;
    private const int VideoFps = 24;
    private const float VideoMbps = 1.5f;
    // ChatGPT's general file ceiling is 512 MB. Keep plenty of headroom.
    private const long SafePartBytes = 400L * 1024L * 1024L;
    private const long EarlyRotateBytes = 350L * 1024L * 1024L;

    private sealed class VideoPart
    {
        public string Name;
        public string Path;
        public float Start;
        public float End;
    }

    private readonly List<VideoPart> parts = new List<VideoPart>();
    private ScratchAutoplayMovieRecorder movie;
    private ScratchGameManager game;
    private ScratchEconomyManager economy;
    private ScratchFlowPresentation flow;
    private StreamWriter writer;

    private string reportDirectory = string.Empty;
    private string status = "Pronto per registrare una partita manuale.";
    private string error = string.Empty;
    private bool recording;
    private bool awaitingVideoFinalization;
    private bool lastPauseState;
    private float startTime;
    private float partStartTime;
    private float nextHeartbeat;
    private double finalizationDeadline;
    private int eventCount;
    private int finishedTickets;
    private int startedTickets;
    private int purchasedSkills;
    private float finalDurationSeconds;
    private float finaleDetectedAt = -1f;
    private int hoveredSkills;
    private int inspectedCells;
    private int manualTitanUses;
    private int scratchedJokers;

    [MenuItem("Tools/Scratch to Riches/Registra partita manuale")]
    private static void Open()
    {
        var window = GetWindow<ScratchManualPlaytestRecorderWindow>("STR Playtest");
        window.minSize = new Vector2(450f, 330f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    private void OnDisable()
    {
        if (recording) StopSession("Finestra chiusa o script ricaricati");
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SCRATCH TO RICHES - PLAYTEST MANUALE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Riprende la Game View, NON usa autoplay e non modifica la partita. " +
            "Log CSV unico con acquisti, biglietti, caselle, poteri e shop. " +
            "Video H.264 720p / 24 FPS / 1,5 Mbps, diviso automaticamente " +
            "solo oltre 30 minuti o se il file cresce troppo (limite generale 512 MB).", MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Stato", status, EditorStyles.wordWrappedLabel);
        if (!string.IsNullOrEmpty(error))
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        if (recording)
        {
            EditorGUILayout.LabelField("Tempo", FormatDuration(Time.realtimeSinceStartup - startTime));
            EditorGUILayout.LabelField("Video", "Parte " + parts.Count + " | max 30 min | audio disattivato");
            EditorGUILayout.LabelField("Log", eventCount + " eventi | " + finishedTickets +
                " biglietti | " + purchasedSkills + " acquisti");
        }
        EditorGUILayout.Space(6);
        using (new EditorGUI.DisabledScope(recording || !EditorApplication.isPlaying))
        {
            if (GUILayout.Button("AVVIA VIDEO + LOG PARTITA MANUALE", GUILayout.Height(38f)))
                StartSession();
        }
        if (!recording && !EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Apri la scena e premi Play. Avvia la registrazione prima di SI PARTE!.", MessageType.None);
        using (new EditorGUI.DisabledScope(!recording))
        {
            if (GUILayout.Button("STOP E SALVA VIDEO + LOG", GUILayout.Height(32f)))
                StopSession("Arresto manuale");
        }

        if (!string.IsNullOrEmpty(reportDirectory))
        {
            EditorGUILayout.Space(8);
            if (GUILayout.Button("APRI CARTELLA VIDEO E LOG"))
                EditorUtility.RevealInFinder(reportDirectory);
            EditorGUILayout.SelectableLabel(reportDirectory, GUILayout.Height(32f));
        }
        EditorGUILayout.HelpBox(
            "Una demo di 20-25 min produce normalmente UN SOLO MP4 (~225-281 MB teorici). " +
            "Se un segmento supera 400 MB viene segnalato esplicitamente: " +
            "la dimensione esatta e' verificabile soltanto dopo la codifica.", MessageType.None);
    }
}
#endif
