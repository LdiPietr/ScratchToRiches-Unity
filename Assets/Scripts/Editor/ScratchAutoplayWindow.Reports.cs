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

// Reports responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    private bool Check(bool passed, string checkName, string details)
    {
        if (passed) assertionsPassed++;
        else assertionsFailed++;
        checks.Add(DateTime.UtcNow.ToString("o") + ";" + (passed ? "PASS" : "FAIL") +
            ";" + Safe(checkName) + ";" + Safe(details));
        if (!passed) Debug.LogError("STR AUTO QA: " + checkName + " - " + details);
        return passed;
    }

    private void Note(string kind, string details)
    {
        string num = game == null ? "-" : game.TicketNumber.ToString();
        string tier = game == null ? "-" : game.CurrentTicketKind.ToString();
        string match = game == null ? "-" : game.CurrentMatch.Tier.ToString();
        string wallet = economy == null ? "-" : economy.Coins.ToString();
        string payout = game == null ? "-" : game.TicketPayout.ToString();
        ledger.Add(DateTime.UtcNow.ToString("o") + ";" + Safe(kind) + ";" +
            num + ";" + tier + ";" +
            activePattern + ";" +
            wallet + ";" + ticketCost + ";" + payout + ";" + match + ";" +
            Safe(farming ? "FARM | " + details : details));
    }

    private static string Safe(string s) => (s ?? "").Replace(";", ",").Replace("\n", " ").Replace("\r", " ");

    private void Fail(string why)
    {
        Check(false, "fatal", why);
        Finish(false, why);
    }

    private void CaptureFrame(float now)
    {
        if (!captureFrames || now - lastScreenshotAt < 1f / framesPerSecond || frameCount >= 3600) return;
        string output = Path.Combine(reportDirectory, "frames",
            "frame_" + frameCount.ToString("D6") + ".png");
        ScreenCapture.CaptureScreenshot(output, 1);
        lastScreenshotAt = now;
        frameCount++;
    }

    private void Finish(bool success, string message)
    {
        if (!running) return;
        running = false;
        EndPointer();
        if (pressedHold != null) pressedHold.QaReleaseHold();
        pressedHold = null;
        lastStatus = (success ? "COMPLETATO: " : "FERMATO: ") + message;
        try
        {
            if (symbols != null) SetPattern(ScratchSymbolManager.TestPattern.Random);
            if (recordVideo)
            {
                if (movieRecorder != null && movieRecorder.Started)
                {
                    if (movieRecorder.Stop())
                    {
                        videoStatus = "Finalizzazione MP4 in corso";
                        videoValidationPending = true;
                        videoValidationDeadline = EditorApplication.timeSinceStartup + 20.0;
                        Note("VIDEO_STOP", "output=" + videoOutputPath);
                    }
                    else
                    {
                        videoStatus = "ERRORE finalizzazione: " + movieRecorder.LastError;
                        Check(false, "video_finalize", videoStatus);
                    }
                }
                else if (string.IsNullOrEmpty(videoOutputPath))
                {
                    videoStatus = "ERRORE registrazione non avviata";
                    Check(false, "video_start", videoStatus);
                }
            }
            Note("TEST_END", lastStatus);
            WriteReport();
            Debug.Log("STR AUTO QA: " + lastStatus + " | Report: " + reportDirectory +
                " | Video: " + videoStatus);
        }
        catch (Exception ex)
        {
            Debug.LogError("STR AUTO QA: salvataggio report/MP4 fallito: " + ex);
        }
        Repaint();
    }

    private void UpdateVideoValidation()
    {
        if (!videoValidationPending || string.IsNullOrEmpty(videoOutputPath)) return;

        try
        {
            if (File.Exists(videoOutputPath))
            {
                long length = new FileInfo(videoOutputPath).Length;
                if (length >= 4096)
                {
                    videoStatus = "MP4 creato (" + (length / 1024 / 1024f).ToString("F1",
                        CultureInfo.InvariantCulture) + " MB)";
                    Check(true, "video_mp4_file", "MP4 esistente, bytes=" + length);
                    videoValidationPending = false;
                    EditorApplication.update -= UpdateVideoValidation;
                    WriteReport();
                    Repaint();
                    return;
                }
            }

            if (EditorApplication.timeSinceStartup > videoValidationDeadline)
            {
                videoValidationPending = false;
                EditorApplication.update -= UpdateVideoValidation;
                videoStatus = "ERRORE: MP4 assente o vuoto dopo finalizzazione";
                lastStatus += " | VIDEO NON CREATO";
                Check(false, "video_mp4_file", "File non creato: " + videoOutputPath);
                WriteReport();
                Debug.LogError("STR AUTO QA: " + videoStatus + " | " + videoOutputPath);
                Repaint();
            }
        }
        catch (Exception ex)
        {
            videoValidationPending = false;
            EditorApplication.update -= UpdateVideoValidation;
            videoStatus = "ERRORE verifica MP4: " + ex.Message;
            lastStatus += " | VIDEO NON VERIFICATO";
            Check(false, "video_mp4_file", videoStatus);
            WriteReport();
        }
    }

    private string MilestoneMinute(string skillId)
    {
        return milestoneSeconds.TryGetValue(skillId, out float seconds)
            ? (seconds / 60f).ToString("F2", CultureInfo.InvariantCulture) : "non raggiunto";
    }

    private void WriteReport()
    {
        if (string.IsNullOrEmpty(reportDirectory)) return;
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(Path.Combine(reportDirectory, "tickets.csv"), ledger, Encoding.UTF8);
        File.WriteAllLines(Path.Combine(reportDirectory, "checks.csv"), checks, Encoding.UTF8);
        if (IsFullDemo)
            File.WriteAllLines(Path.Combine(reportDirectory, "progression.csv"), progression, Encoding.UTF8);
        float durationMinutes = (Time.realtimeSinceStartup - startedAt) / 60f;
        string summary = "Scratch to Riches V2 - Auto QA\n" +
            "Stato: " + lastStatus + "\n" +
            "Modalita': " + autoplayMode + "\n" +
            "Seed effettivo: " + actualSeed + "\n" +
            "Seed nuovo a ogni avvio: " + freshSeedEachRun + "\n" +
            "Durata reale (s): " + (Time.realtimeSinceStartup - startedAt).ToString("F2", CultureInfo.InvariantCulture) + "\n" +
            "Biglietti di test: " + tickets +
                (IsFullDemo ? " (nessun numero fisso)" : "/13") + "\n" +
            "Biglietti farm: " + farmTickets + "\n" +
            "Biglietti giocati totali: " + (tickets + farmTickets) + "\n" +
            "Biglietti con combinazione NON forzata: " + naturalTicketsPlayed + "\n" +
            "Biglietti con combinazione forzata: " + forcedTicketsPlayed + "\n" +
            "Primi movimenti distinti (tipo:casella): " + uniqueFirstChoices.Count + "\n" +
            "Risultati SOLO su biglietti casuali: nessuna=" + observedRandomResults[0] +
            ", coppia=" + observedRandomResults[1] +
            ", tris=" + observedRandomResults[2] +
            ", poker=" + observedRandomResults[3] +
            ", cinquina=" + observedRandomResults[4] + "\n" +
            "Check OK: " + assertionsPassed + "\nCheck KO: " + assertionsFailed + "\n" +
            "Saldo finale: " + (economy != null ? economy.Coins.ToString() : "n/a") + "\n" +
            "Credito QA incluso: " + qaFunds + "\n" +
            "Video: " + videoStatus + "\n" +
            "Video file: " + (recordVideo ? videoOutputPath : "Non richiesto") + "\n" +
            "Formato: " + (recordVideo ? "H.264 MP4, " + videoHeight + "p, " + videoFrameRate + " fps" : "N/A") + "\n" +
            "Audio: " + (recordVideo && recordGameAudio) + "\n" +
            "Scelte ragionate (partite casuali): " + informedChoices + "\n" +
            "Scelte con anteprima: " + previewChoices + "\n" +
            "Caselle coperte scelte NONOSTANTE anteprime disponibili: " +
                deliberatelySkippedPreviews + "\n" +
            "Scelte con anteprima che corrisponde a un simbolo grattato: " + previewMatchingChoices + "\n" +
            "Esplorazioni deliberate: " + randomExplorationChoices + "\n" +
            "Frames PNG richiesti: " + frameCount + "\n" +
            (IsFullDemo
                ? "MODALITA' DEMO COMPLETA - 25 SKILL / 28 ACQUISTI\n" +
                  "Acquisti completati: " + purchaseIndex + "/" + FullDemoPlan.Length + "\n" +
                  "Skill demo possedute: " + CountOwnedDemoSkills() + "/25\n" +
                  "DemoFinished reale: " + (game != null && game.DemoFinished) + "\n" +
                  "Dorati finali: " + (game != null ? game.DemoFinalGoldTickets : 0) +
                      "/" + (game != null ? game.DemoFinalGoldMinTickets : 8) + " minimo\n" +
                  "Incassi Dorati finali: " + (game != null ? game.DemoFinalGoldPayout : 0) +
                      "/" + (game != null ? game.DemoFinalGoldPayoutTarget : 3800) + "\n" +
                  "Potere finale attivato: " + (game != null && game.DemoFinalPowerActivated) + "\n" +
                  "Ispezioni C06: " + qaC06Inspections + "\n" +
                  "Gestualita' Titanico G08: " + qaTitanActivations + "\n" +
                  "Jolly grattati: " + (skills != null ? skills.JollyActivationCount : 0) + "\n" +
                  "Durata misurata (min): " + durationMinutes.ToString("F2", CultureInfo.InvariantCulture) + "\n" +
                  "Obiettivo 20-25 min: " + (durationMinutes >= 20f && durationMinutes <= 25f
                      ? "NEL TARGET" : "FUORI TARGET: BILANCIARE") + "\n" +
                  "C03 Fortunato a (min): " + MilestoneMinute("C03") + "\n" +
                  "C05 Dorato a (min): " + MilestoneMinute("C05") + "\n" +
                  "G08 Titanico a (min): " + MilestoneMinute("G08") + "\n" +
                  "F04 Jolly a (min): " + MilestoneMinute("F04") + "\n" +
                  "Durata non accelerata artificialmente: " + (humanSpeed == 1f) + "\n" +
                  "File cronologia: progression.csv\n"
                : string.Empty);
        File.WriteAllText(Path.Combine(reportDirectory, "summary.txt"), summary, Encoding.UTF8);
    }
}
#endif
