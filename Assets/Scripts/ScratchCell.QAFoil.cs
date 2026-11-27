#if UNITY_EDITOR
using System;
using UnityEngine;

// Read-only access to visible foil geometry for the isolated editor benchmark.
// No symbols, selection, settlement or mask writes are exposed here.
public sealed partial class ScratchCell
{
    public int QaReadRemainingFoil(bool[] remaining, out float physicalCoverage)
    {
        physicalCoverage = 0f;
        if (!ScratchGameFeelPrefs.Active || !Application.isPlaying || scratchTexture == null ||
            scratchablePixels == null || erasedPixels == null || remaining == null ||
            remaining.Length != scratchablePixels.Length || scratchablePixelCount <= 0)
            throw new InvalidOperationException("Foil measurement requires the isolated QA profile and a valid mask.");
        for (int i = 0; i < remaining.Length; i++)
            remaining[i] = scratchablePixels[i] && !erasedPixels[i];
        // Do not use RevealedPercentage: completion clears the displayed texture
        // and reports 100 even when the real removal threshold is only 85.
        physicalCoverage = 100f * erasedPixelCount / scratchablePixelCount;
        return scratchTexture.width;
    }
}
#endif
