#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Session responsibility of ScratchManualPlaytestRecorderWindow.
public sealed partial class ScratchManualPlaytestRecorderWindow
{
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
        finaleDetectedAt = -1f;
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
}
#endif
