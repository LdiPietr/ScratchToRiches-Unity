using System;
using UnityEngine;

[Serializable]
public sealed class ScratchVintageTicketArt
{
    public Sprite artwork;
    public Sprite frame;
    public Sprite revealed;
    public Sprite cover;
    public Sprite mask;
    public Vector2 size;
    public Vector2 cellSize;
    public Vector2 coverSize;
    public float symbolSize;
    // Centers in native ticket coordinates, measured from top-left.
    public Vector2[] cellCenters;

    public bool IsValid(int count) =>
        artwork != null && frame != null && revealed != null && cover != null &&
        size.x > 0 && size.y > 0 && cellSize.x > 0 && cellSize.y > 0 &&
        coverSize.x > 0 && coverSize.y > 0 && symbolSize > 0 &&
        cellCenters != null && cellCenters.Length == count &&
        (count == 9 || mask != null);
}
