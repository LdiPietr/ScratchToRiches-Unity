#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Editor-only MP4 recording for the Scratch to Riches QA runner.
/// Unity Recorder 5.1.8 is a project dependency; FFmpeg and external tools are not needed.
/// </summary>
internal sealed class ScratchAutoplayMovieRecorder
{
    private RecorderController controller;
    private RecorderControllerSettings controllerSettings;
    private MovieRecorderSettings movieSettings;
    private bool started;

    public string OutputPath { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public bool Started => started;

    public bool Start(string destinationDirectory, int outputHeight, int framesPerSecond, bool withAudio)
    {
        return Start(destinationDirectory, outputHeight, framesPerSecond,
            withAudio, "autoplay", 0f, false);
    }

    /// <summary>
    /// Manual playtest variant: custom bitrate and true real-time playback.
    /// An independent controller per part lets the caller split long captures.
    /// Target bitrate is not a strict byte cap; caller must validate final MP4.
    /// </summary>
    public bool Start(string destinationDirectory, int outputHeight, int framesPerSecond,
        bool withAudio, string baseName, float targetBitrateMbps, bool realTime)
    {
        LastError = string.Empty;
        if (started)
        {
            LastError = "Un altro video del tester e' gia' in registrazione.";
            return false;
        }
        if (!Application.isPlaying)
        {
            LastError = "Unity Recorder richiede Play Mode.";
            return false;
        }
        if (outputHeight != 720 && outputHeight != 1080)
        {
            LastError = "Sono supportati solo 720p e 1080p.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            if (string.IsNullOrEmpty(baseName) || baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Nome video non valido.");
            string outputWithoutExtension = Path.Combine(destinationDirectory, baseName);
            OutputPath = outputWithoutExtension + ".mp4";
            if (File.Exists(OutputPath))
                throw new IOException("File di output esistente: " + OutputPath);

            movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "ScratchToRiches_QA_H264";
            movieSettings.Enabled = true;
            movieSettings.CaptureAudio = withAudio;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = targetBitrateMbps > 0f
                    ? CoreEncoderSettings.VideoEncodingQuality.Custom
                    : CoreEncoderSettings.VideoEncodingQuality.High,
                TargetBitRate = targetBitrateMbps > 0f ? targetBitrateMbps : 4f
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = outputHeight == 720 ? 1280 : 1920,
                OutputHeight = outputHeight
            };
            // RecorderSettings.OutputFile accepts an absolute path WITHOUT extension.
            movieSettings.OutputFile = outputWithoutExtension.Replace('\\', '/');
            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = framesPerSecond;
            controllerSettings.FrameRatePlayback = realTime
                ? FrameRatePlayback.Variable : FrameRatePlayback.Constant;
            controllerSettings.CapFrameRate = !realTime;
            controllerSettings.ExitPlayMode = false;

            controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            if (!controller.StartRecording())
                throw new InvalidOperationException("Unity Recorder ha rifiutato l'avvio. Controlla la Console.");

            started = true;
            Debug.Log("STR AUTO QA: MP4 H.264 avviato: " + OutputPath);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (controller != null)
            {
                try { controller.StopRecording(); }
                catch (Exception stopError) { Debug.LogWarning("STR AUTO QA: " + stopError.Message); }
            }
            controller = null;
            started = false;
            Debug.LogError("STR AUTO QA: errore avvio MP4: " + ex);
            return false;
        }
    }

    /// <summary>
    /// Finalizes the MP4, including when a QA assertion fails or the user stops the test.
    /// Caller should check OutputPath afterwards: a failed encoder must not appear successful.
    /// </summary>
    public bool Stop()
    {
        if (!started) return false;
        try
        {
            controller.StopRecording();
            Debug.Log("STR AUTO QA: registrazione terminata: " + OutputPath);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogError("STR AUTO QA: finalizzazione MP4 fallita: " + ex);
            return false;
        }
        finally
        {
            started = false;
            controller = null;
        }
    }
}
#endif
