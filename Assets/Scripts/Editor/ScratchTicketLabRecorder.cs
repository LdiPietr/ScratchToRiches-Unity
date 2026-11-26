#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Recorder lifecycle for the isolated ScratchTicketLab. Only rotates at a
// ticket/cell boundary, so no pointer gesture is cut in two.
[InitializeOnLoad]
internal static class ScratchTicketLabRecorder
{
    // Decimal MB: every approved upload must be strictly below 400,000,000 bytes.
    internal const long UploadLimit = 400000000L;
    internal const long RotateBytes = 300000000L;
    private const double MaxPartSeconds = 8 * 60;
    private const int Height = 1080, Fps = 24;
    private const float Mbps = 3f;
    private sealed class Part
    {
        public string File, Ticket, State = "PENDING", Detail = "";
        public double Started, Duration;
        public long Bytes;
        public double Deadline;
        public ScratchAutoplayVideoBudget Fallback;
        public bool StopOk;
    }

    private static readonly List<Part> parts = new List<Part>();
    private static ScratchAutoplayMovieRecorder movie;
    private static Part active;
    private static string directory;
    private static bool session, finalizing, failed;
    private static int partNumber, ticketPart;
    private static string ticket;

    static ScratchTicketLabRecorder()
    {
        ScratchTicketLab.BeginVideoTicket = BeginTicket;
        ScratchTicketLab.VideoCheckpoint = Checkpoint;
        ScratchTicketLab.FinishVideo = Finish;
        EditorApplication.update += Verify;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload += Finish;
        EditorApplication.quitting += Finish;
    }

    private static bool BeginTicket(string name)
    {
        if (!session)
        {
            if (finalizing || ScratchAutoplayWindow.AnyTestRunning || ScratchManualTelemetry.IsRecording)
            { Debug.LogError("LAB VIDEO: altra registrazione o finalizzazione in corso."); return false; }
            try
            {
                directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Recordings", "TicketLab", DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                    "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            { Debug.LogError("LAB VIDEO: " + ex.Message); return false; }
            parts.Clear();
            partNumber = 0;
            failed = false;
            session = true;
        }
        else if (!StopPart()) { Finish(); return false; }
        ticket = name;
        ticketPart = 0;
        if (StartPart()) return true;
        Finish();
        return false;
    }

    private static bool Checkpoint()
    {
        if (!session || active == null || movie == null || !movie.Started) return false;
        bool rotate = EditorApplication.timeSinceStartup - active.Started >= MaxPartSeconds;
        try
        {
            if (File.Exists(active.File) && new FileInfo(active.File).Length >= RotateBytes)
                rotate = true;
        }
        catch (IOException) { }
        if (!rotate) return true;
        if (StopPart() && StartPart()) return true;
        Finish();
        return false;
    }

    private static bool StartPart()
    {
        string name = (++partNumber).ToString("D3") + "_" + ticket +
                      "_parte_" + (++ticketPart).ToString("D2");
        movie = new ScratchAutoplayMovieRecorder();
        if (!movie.Start(directory, Height, Fps, false, name, Mbps, true))
        {
            failed = true;
            Debug.LogError("LAB VIDEO: " + movie.LastError);
            movie = null;
            return false;
        }
        active = new Part { File = movie.OutputPath, Ticket = ticket,
            Started = EditorApplication.timeSinceStartup };
        parts.Add(active);
        Debug.Log("LAB VIDEO: " + active.File);
        return true;
    }

    private static bool StopPart()
    {
        if (active == null || movie == null) return false;
        active.StopOk = movie.Stop();
        active.Deadline = EditorApplication.timeSinceStartup + 45;
        if (!active.StopOk)
        {
            active.State = "FAIL";
            active.Detail = movie.LastError;
            failed = true;
        }
        bool ok = active.StopOk;
        active = null;
        movie = null;
        WriteManifest();
        return ok;
    }

    private static void Finish()
    {
        if (!session) return;
        session = false;
        if (active != null) StopPart();
        finalizing = parts.Count > 0;
        WriteManifest();
        if (!finalizing) Debug.LogError("LAB VIDEO: nessuna clip registrata.");
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) Finish();
    }

    private static void Verify()
    {
        // Process closed segments even while the next ticket is being captured.
        if (!session && !finalizing) return;
        foreach (Part p in parts)
        {
            if (p == active || p.State == "OK" || p.State == "FAIL") continue;
            try
            {
                if (p.Fallback != null)
                {
                    if (!p.Fallback.TryFinish(out bool ok, out string detail)) continue;
                    p.Fallback = null;
                    p.Detail = detail;
                    if (!ok) { Reject(p, detail); continue; }
                }
                p.Bytes = File.Exists(p.File) ? new FileInfo(p.File).Length : 0;
                if (p.Bytes < 4096 || !ScratchAutoplayVideoBudget.TryDuration(p.File, out p.Duration))
                {
                    if (EditorApplication.timeSinceStartup >= p.Deadline)
                        Reject(p, "MP4 non finalizzato o non leggibile");
                    continue;
                }
                if (p.Bytes >= UploadLimit)
                {
                    // Target bitrate is not a guaranteed bound. Preserve the original
                    // if the existing optional FFmpeg fallback cannot meet the cap.
                    p.Fallback = new ScratchAutoplayVideoBudget(p.File, p.Duration, false);
                    if (!p.Fallback.Start(out string error))
                    { p.Fallback = null; Reject(p, error); }
                    else p.State = "COMPACTING";
                    continue;
                }
                p.State = "OK";
                WriteManifest();
            }
            catch (Exception ex) { Reject(p, ex.Message); }
        }
        if (!finalizing || session) return;
        foreach (Part p in parts)
            if (p.State != "OK" && p.State != "FAIL") return;
        finalizing = false;
        WriteManifest();
        if (failed) Debug.LogError("LAB VIDEO: una o piu clip non sono caricabili; leggi video_manifest.csv. " + directory);
        else Debug.Log("LAB VIDEO PASS: tutte le clip verificate < 400 MB. " + directory);
    }

    private static void Reject(Part p, string detail)
    {
        p.State = "FAIL";
        p.Detail = detail;
        failed = true;
        WriteManifest();
        Debug.LogError("LAB VIDEO: " + Path.GetFileName(p.File) + ": " + detail);
    }

    private static void WriteManifest()
    {
        if (string.IsNullOrEmpty(directory)) return;
        try
        {
            var rows = new List<string> { "file;ticket;bytes;seconds;status;detail" };
            foreach (Part p in parts)
            {
                string detail = p.Detail.Replace(';', ',').Replace('\r',' ').Replace('\n',' ');
                rows.Add(Path.GetFileName(p.File) + ";" + p.Ticket + ";" + p.Bytes + ";" +
                    p.Duration.ToString("F2", CultureInfo.InvariantCulture) + ";" + p.State + ";" + detail);
            }
            File.WriteAllLines(Path.Combine(directory, "video_manifest.csv"), rows, Encoding.UTF8);
        }
        catch (Exception ex) { Debug.LogError("LAB VIDEO manifest: " + ex.Message); }
    }
}
#endif
