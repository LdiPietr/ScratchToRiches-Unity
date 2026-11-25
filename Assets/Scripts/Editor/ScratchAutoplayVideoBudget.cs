#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

// Optional whole-file fallback; never trims the recorded run.
internal sealed class ScratchAutoplayVideoBudget
{
    private const long Limit = 400000000L;
    private readonly string original, temporary;
    private readonly double duration;
    private readonly bool audio;
    private Process process;
    private double startedAt;

    public ScratchAutoplayVideoBudget(string path, double seconds, bool withAudio)
    {
        original = path;
        temporary = path + ".compact.mp4";
        duration = seconds;
        audio = withAudio;
    }

    public bool Start(out string error)
    {
        error = string.Empty;
        try
        {
            string ffmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (string.IsNullOrWhiteSpace(ffmpeg)) ffmpeg = "ffmpeg";
            if (File.Exists(temporary)) File.Delete(temporary);
            // 335 MB total target leaves encoder/container margin below 400 MB.
            int kbps = (int)(335000000d * 8d / duration / 1000d) - (audio ? 96 : 0);
            if (kbps < 80) { error = "Durata troppo lunga per un MP4 leggibile nel budget."; return false; }
            kbps = Math.Min(1450, kbps);
            string k = kbps.ToString(CultureInfo.InvariantCulture) + "k";
            string args = "-nostdin -hide_banner -loglevel error -y -i " + Quote(original) +
                " -map 0:v:0 -c:v libx264 -preset fast -pix_fmt yuv420p -b:v " + k +
                " -maxrate " + k + " -bufsize " + (kbps * 2).ToString(CultureInfo.InvariantCulture) + "k" +
                (audio ? " -map 0:a? -c:a aac -b:a 96k" : " -an") +
                " -movflags +faststart " + Quote(temporary);
            process = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null) { error = "Impossibile avviare FFmpeg."; return false; }
            startedAt = UnityEditor.EditorApplication.timeSinceStartup;
            return true;
        }
        catch (Exception ex)
        {
            error = "FFmpeg non disponibile o non avviabile: " + ex.Message;
            return false;
        }
    }

    public bool TryFinish(out bool ok, out string detail)
    {
        ok = false;
        detail = string.Empty;
        if (process == null) { detail = "Ricompressione non avviata."; return true; }
        try
        {
            if (!process.HasExited)
            {
                if (UnityEditor.EditorApplication.timeSinceStartup - startedAt < 3600d) return false;
                process.Kill();
                detail = "Timeout ricompressione; originale preservato.";
                return true;
            }
            if (process.ExitCode != 0 || !File.Exists(temporary) ||
                new FileInfo(temporary).Length > Limit || new FileInfo(temporary).Length < 4096 ||
                !TryDuration(temporary, out double seconds) ||
                Math.Abs(seconds - duration) > 1.0)
            {
                detail = "Ricompressione non conforme (dimensione/durata); originale preservato.";
                return true;
            }
            // Replace only after size AND full duration have passed validation.
            File.Replace(temporary, original, null);
            ok = true;
            detail = "Ricompressione completa, durata preservata.";
            return true;
        }
        catch (Exception ex)
        {
            detail = "Ricompressione: " + ex.Message + " | Originale preservato.";
            return true;
        }
        finally
        {
            // A false return is the pending state; leave the running process alone.
            if (ok || detail.Length != 0)
            {
                process.Dispose();
                process = null;
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            }
        }
    }

    private static string Quote(string path) => "\"" + path + "\"";

    // Read finalized MP4 movie-header duration; works with moov at either end.
    public static bool TryDuration(string path, out double seconds)
    {
        seconds = 0;
        try
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(file))
            {
                while (file.Position + 8 <= file.Length)
                {
                    if (!Box(reader, file.Length, out long end, out uint type)) return false;
                    if (type == 0x6d6f6f76) // moov
                    {
                        while (file.Position + 8 <= end)
                        {
                            if (!Box(reader, end, out long childEnd, out uint child)) return false;
                            if (child == 0x6d766864) // mvhd
                            {
                                byte version = reader.ReadByte();
                                if (version > 1) return false;
                                file.Position += version == 1 ? 19 : 11;
                                uint scale = U32(reader);
                                ulong ticks = version == 1 ? U64(reader) : U32(reader);
                                if (file.Position > childEnd || scale == 0 || ticks == 0) return false;
                                seconds = (double)ticks / scale;
                                return seconds > 0 && !double.IsInfinity(seconds);
                            }
                            file.Position = childEnd;
                        }
                        return false;
                    }
                    file.Position = end;
                }
            }
        }
        catch (IOException) { }
        catch (ArgumentException) { }
        return false;
    }

    private static bool Box(BinaryReader r, long limit, out long end, out uint type)
    {
        long start = r.BaseStream.Position;
        ulong size = U32(r);
        type = U32(r);
        if (size == 1) size = U64(r);
        if (size == 0) size = (ulong)(limit - start);
        if (size < (ulong)(r.BaseStream.Position - start) || size > (ulong)(limit - start))
        { end = 0; return false; }
        end = start + (long)size;
        return true;
    }

    private static uint U32(BinaryReader r) =>
        ((uint)r.ReadByte() << 24) | ((uint)r.ReadByte() << 16) | ((uint)r.ReadByte() << 8) | r.ReadByte();
    private static ulong U64(BinaryReader r) => ((ulong)U32(r) << 32) | U32(r);
}
#endif
