#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reports responsibility of ScratchManualPlaytestRecorderWindow.
public sealed partial class ScratchManualPlaytestRecorderWindow
{
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
