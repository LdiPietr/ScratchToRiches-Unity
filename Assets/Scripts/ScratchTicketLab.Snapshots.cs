using System.Collections;
using UnityEngine;

public sealed partial class ScratchTicketLab
{
#if UNITY_EDITOR
    public static System.Action<string, Texture2D> SaveSnapshot;
#endif
    private IEnumerator Snapshot(string phase)
    {
#if UNITY_EDITOR
        if (!recordAutomatically || SaveSnapshot == null) yield break;
        // Capture the Game View after Canvas and IMGUI have rendered this state.
        yield return new WaitForEndOfFrame();
        Texture2D image = null;
        try
        {
            image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) Debug.LogError("LAB PNG: capture returned null.");
            else SaveSnapshot(phase, image);
        }
        catch (System.Exception ex) { Debug.LogError("LAB PNG: " + ex.Message); }
        finally { if (image != null) Destroy(image); }
#else
        yield break;
#endif
    }
}
