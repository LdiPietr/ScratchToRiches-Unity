#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Capture responsibility of ScratchManualPlaytestRecorderWindow.
public sealed partial class ScratchManualPlaytestRecorderWindow
{
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
                if (finaleDetectedAt < 0f)
                {
                    finaleDetectedAt = Time.realtimeSinceStartup;
                    WriteEvent("DEMO_END_VISIBLE", "video_hold_seconds=" + FinaleVideoHoldSeconds);
                    status = "FINALE RAGGIUNTO - registro altri 10 secondi per analizzare la schermata";
                    Repaint();
                }
                if (Time.realtimeSinceStartup - finaleDetectedAt >= FinaleVideoHoldSeconds)
                {
                    StopSession("Finale demo completato e mostrato per 10 secondi");
                    return;
                }
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
}
#endif
