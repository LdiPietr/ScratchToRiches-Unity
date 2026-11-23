using System;

#if UNITY_EDITOR
/// <summary>
/// Lightweight, opt-in hooks for real human playtests in the Unity Editor.
/// Never changes rewards, choices, RNG, input or timing. This is NOT autoplay.
/// Runtime scripts publish events; the Editor recorder owns the files/video.
/// </summary>
public static class ScratchManualTelemetry
{
    public static event Action<string, string> Logged;
    public static bool IsRecording => Logged != null;

    public static void Emit(string kind, string details)
    {
        Logged?.Invoke(kind, details ?? string.Empty);
    }
}
#endif
