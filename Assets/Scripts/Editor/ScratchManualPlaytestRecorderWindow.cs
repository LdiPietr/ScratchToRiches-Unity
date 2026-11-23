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
public sealed class ScratchManualPlaytestRecorderWindow : EditorWindow
{
    private const int SegmentSeconds = 30 * 60;
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

    private void StartSession()
    {
        if (recording || !EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isPlaying)
            return;
        if (ScratchManualTelemetry.IsRecording)
        {
            error = "Un'altra sessione manuale e' gia' in ascolto.";
            return;
        }
        if (ScratchAutoplayWindow.AnyTestRunning)
        {
            error = "Autoplay QA attivo: fermalo prima della registrazione manuale.";
            return;
        }
        game = UnityEngine.Object.FindAnyObjectByType<ScratchGameManager>();
        if (game == null)
        {
            error = "ScratchGameManager non trovato. Apri la scena ScratchToRiches.";
            return;
        }
        if (game.State == ScratchGameManager.RoundState.Finished)
        {
            error = "Demo gia' conclusa: esci da Play Mode, resetta i progressi e rientra.";
            return;
        }
        economy = game.GetComponent<ScratchEconomyManager>();
        flow = game.GetComponent<ScratchFlowPresentation>();
        if (economy == null)
        {
            error = "ScratchEconomyManager mancante.";
            return;
        }
        error = string.Empty;
        parts.Clear();
        eventCount = finishedTickets = startedTickets = purchasedSkills = hoveredSkills =
            inspectedCells = manualTitanUses = scratchedJokers = 0;
        startTime = Time.realtimeSinceStartup;
        finalDurationSeconds = 0f;
        nextHeartbeat = startTime + 15f;
        lastPauseState = EditorApplication.isPaused;
        awaitingVideoFinalization = false;
        reportDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "Recordings", "Manual",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) +
            "_" + Guid.NewGuid().ToString("N").Substring(0, 6));

        try
        {
            Directory.CreateDirectory(reportDirectory);
            writer = new StreamWriter(Path.Combine(reportDirectory, "events.csv"),
                false, new UTF8Encoding(true)) { AutoFlush = true };
            writer.WriteLine("seconds;utc;event;ticket;kind;wallet;active;details");
            if (!StartVideoPart())
                throw new InvalidOperationException(error);
            recording = true;
            ScratchManualTelemetry.Logged += OnGameEvent;
            WriteEvent("SESSION_START", "seed=natural|mode=manual|resolution=1280x720|fps=24|bitrate_mbps=1.5");
            if (game.TicketNumber > 0)
                WriteEvent("STARTED_MID_GAME", "La registrazione non copre l'inizio della partita.");
            status = "REGISTRAZIONE ATTIVA";
            Debug.Log("STR PLAYTEST: registrazione manuale attiva: " + reportDirectory);
        }
        catch (Exception ex)
        {
            ScratchManualTelemetry.Logged -= OnGameEvent;
            if (movie != null && movie.Started) movie.Stop();
            movie = null;
            if (writer != null) { writer.Dispose(); writer = null; }
            error = "Impossibile avviare video e log: " + ex.Message;
            status = "Avvio fallito";
            recording = false;
            Debug.LogError("STR PLAYTEST: " + error);
        }
        Repaint();
    }

    private bool StartVideoPart()
    {
        int index = parts.Count + 1;
        string name = "part_" + index.ToString("D2", CultureInfo.InvariantCulture);
        movie = new ScratchAutoplayMovieRecorder();
        if (!movie.Start(reportDirectory, VideoHeight, VideoFps, false,
                name, VideoMbps, true))
        {
            error = "Unity Recorder: " + movie.LastError;
            return false;
        }
        partStartTime = Time.realtimeSinceStartup;
        parts.Add(new VideoPart
        {
            Name = name + ".mp4",
            Path = movie.OutputPath,
            Start = partStartTime - startTime,
            End = partStartTime - startTime
        });
        return true;
    }

    private bool EndVideoPart()
    {
        if (movie == null || !movie.Started) return false;
        parts[parts.Count - 1].End = Time.realtimeSinceStartup - startTime;
        bool ok = movie.Stop();
        if (!ok) error = "Finalizzazione " + parts[parts.Count - 1].Name +
                         ": " + movie.LastError;
        movie = null;
        return ok;
    }

    private void RotateVideoPart()
    {
        int old = parts.Count;
        WriteEvent("VIDEO_PART_END", "part=" + old);
        if (!EndVideoPart() || !StartVideoPart())
        {
            StopSession("ERRORE VIDEO - log salvato");
            return;
        }
        WriteEvent("VIDEO_PART_START", "part=" + parts.Count);
        status = "REGISTRAZIONE ATTIVA | parte " + parts.Count;
        Repaint();
    }

    private void Tick()
    {
        if (recording)
        {
            if (!EditorApplication.isPlaying || game == null)
            {
                StopSession("Play Mode terminato");
                return;
            }
            if (game.DemoFinished)
            {
                StopSession("Finale demo completato");
                return;
            }

            bool paused = EditorApplication.isPaused;
            if (paused != lastPauseState)
            {
                WriteEvent(paused ? "EDITOR_PAUSE" : "EDITOR_RESUME", "");
                lastPauseState = paused;
            }
            float now = Time.realtimeSinceStartup;
            if (now >= nextHeartbeat)
            {
                WriteEvent("HEARTBEAT", "shop_open=" + (flow != null && flow.ShopOpen) +
                    "|selected=" + game.SelectedCount + "|revealed=" + game.RevealedCount);
                nextHeartbeat = now + 15f;
            }
            if (movie != null && movie.Started &&
                (now - partStartTime >= SegmentSeconds || IsCurrentVideoGrowingTooLarge()))
                RotateVideoPart();
            Repaint();
        }
        else if (awaitingVideoFinalization)
        {
            VerifyVideoParts();
        }
    }

    private bool IsCurrentVideoGrowingTooLarge()
    {
        try
        {
            return parts.Count > 0 && File.Exists(parts[parts.Count - 1].Path) &&
                new FileInfo(parts[parts.Count - 1].Path).Length >= EarlyRotateBytes;
        }
        catch (IOException) { return false; }
    }

    private void PlayModeChanged(PlayModeStateChange change)
    {
        if (recording && (change == PlayModeStateChange.ExitingPlayMode ||
                          change == PlayModeStateChange.EnteredEditMode))
            StopSession("Play Mode terminato");
    }

    private void OnGameEvent(string kind, string details)
    {
        if (!recording) return;
        switch (kind)
        {
            case "TICKET_START": startedTickets++; break;
            case "TICKET_END":
                if (details != null && details.Contains("|complete=True"))
                    finishedTickets++;
                break;
            case "SKILL_PURCHASED": purchasedSkills++; break;
            case "SKILL_HOVER": hoveredSkills++; break;
            case "C06_INSPECTION": inspectedCells++; break;
            case "TITAN_USED": manualTitanUses++; break;
            case "JOLLY_SCRATCHED": scratchedJokers++; break;
        }
        WriteEvent(kind, details);
    }

    private void WriteEvent(string kind, string details)
    {
        if (writer == null) return;
        float seconds = Time.realtimeSinceStartup - startTime;
        string tier = game != null ? game.CurrentTicketKind.ToString() : "-";
        string safe = (details ?? string.Empty).Replace(';', ',')
            .Replace('\r', ' ').Replace('\n', ' ');
        try
        {
            writer.WriteLine(seconds.ToString("F3", CultureInfo.InvariantCulture) + ";" +
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + ";" +
                kind + ";" + (game != null ? game.TicketNumber : 0) + ";" +
                tier + ";" + (economy != null ? economy.Coins : 0) + ";" +
                (game != null && game.TicketActive) + ";" + safe);
            eventCount++;
        }
        catch (IOException ex)
        {
            error = "Scrittura log fallita: " + ex.Message;
            Debug.LogError("STR PLAYTEST: " + error);
        }
    }

    private void StopSession(string reason)
    {
        if (!recording) return;
        finalDurationSeconds = Time.realtimeSinceStartup - startTime;
        WriteEvent("SESSION_END", "reason=" + reason + "|demo_finished=" +
            (game != null && game.DemoFinished));
        ScratchManualTelemetry.Logged -= OnGameEvent;
        recording = false;
        if (movie != null && movie.Started) EndVideoPart();
        if (writer != null) { writer.Flush(); writer.Dispose(); writer = null; }
        status = "FERMATO: " + reason + " | MP4 in finalizzazione";
        awaitingVideoFinalization = true;
        finalizationDeadline = EditorApplication.timeSinceStartup + 40.0;
        WriteSummary();
        Debug.Log("STR PLAYTEST: " + status + " | " + reportDirectory);
        Repaint();
    }

    private void VerifyVideoParts()
    {
        bool allPresent = parts.Count > 0;
        bool oversize = false;
        foreach (VideoPart part in parts)
        {
            try
            {
                if (!File.Exists(part.Path) ||
                    new FileInfo(part.Path).Length < 4096)
                {
                    allPresent = false;
                    continue;
                }
                if (new FileInfo(part.Path).Length >= SafePartBytes) oversize = true;
            }
            catch (IOException) { allPresent = false; }
        }
        if (!allPresent && EditorApplication.timeSinceStartup < finalizationDeadline)
            return;

        awaitingVideoFinalization = false;
        if (oversize)
        {
            error = "Una clip supera il margine di 400 MB. Verifica video_manifest.csv.";
            status = "ATTENZIONE: video troppo grande, log salvato";
        }
        else if (!allPresent)
        {
            error = "Una clip non e' stata finalizzata entro 40 secondi.";
            status = "ATTENZIONE: video non verificato, log salvato";
        }
        else
            status = "COMPLETATO: video verificati sotto 400 MB + log salvato";
        WriteSummary();
        Repaint();
    }

    private void WriteSummary()
    {
        if (string.IsNullOrEmpty(reportDirectory)) return;
        var lines = new List<string> { "part;start_s;end_s;bytes;size_mb;status" };
        foreach (VideoPart part in parts)
        {
            long bytes = File.Exists(part.Path) ? new FileInfo(part.Path).Length : 0L;
            string state = bytes < 4096 ? "FINALIZZAZIONE_NON_VERIFICATA" :
                bytes >= SafePartBytes ? "TROPPO_GRANDE" : "OK";
            lines.Add(part.Name + ";" +
                part.Start.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                part.End.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                bytes + ";" + (bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture) +
                ";" + state);
        }
        File.WriteAllLines(Path.Combine(reportDirectory, "video_manifest.csv"), lines, Encoding.UTF8);
        var summary = new[]
        {
            "Scratch to Riches - PLAYTEST MANUALE",
            "Durata (s): " + finalDurationSeconds.ToString("F2", CultureInfo.InvariantCulture),
            "Biglietti avviati: " + startedTickets,
            "Biglietti completati: " + finishedTickets,
            "Acquisti: " + purchasedSkills,
            "Hover abilita': " + hoveredSkills,
            "Ispezioni C06: " + inspectedCells,
            "Titanico usato: " + manualTitanUses,
            "Jolly grattati: " + scratchedJokers,
            "Eventi registrati: " + eventCount,
            "Parte video: " + parts.Count,
            "DemoFinished: " + (game != null && game.DemoFinished),
            "Video: H264 MP4 720p 24fps 1.5Mbps, audio off, parti da max 30 min",
            "Limite sicurezza per parte: 400 MB (ChatGPT: massimo generale 512 MB)",
            "Log: events.csv",
            "Manifest: video_manifest.csv",
            "Stato: " + status,
            "Errore: " + error
        };
        File.WriteAllLines(Path.Combine(reportDirectory, "summary.txt"), summary, Encoding.UTF8);
    }

    private static string FormatDuration(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return ((int)t.TotalMinutes).ToString("D2") + ":" + t.Seconds.ToString("D2");
    }
}
#endif
