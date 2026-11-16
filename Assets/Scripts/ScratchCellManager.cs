using UnityEngine;

/// <summary>
/// Single Inspector configuration for all scratchable cells under the Canvas.
/// Assign the cell list in the scene or use the component context menu to collect it.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchCellManager : MonoBehaviour
{
    [Header("Shared settings for every cell")]
    [SerializeField, Range(1, 128)] private int brushRadius = 10;
    [SerializeField, Range(0.1f, 100f)] private float scratchPercentage = 99.8f;
    [SerializeField, Range(16, 512)] private int textureResolution = 128;
    [SerializeField] private Color coatingColor = Color.white;

    [Header("Cells controlled by this Canvas")]
    [SerializeField] private ScratchCell[] cells = new ScratchCell[0];

    public int BrushRadius => brushRadius;
    public float ScratchPercentage => scratchPercentage;
    public int TextureResolution => textureResolution;
    public Color CoatingColor => coatingColor;

    private void Awake()
    {
        BindCells();
    }

    private void BindCells()
    {
        foreach (ScratchCell cell in cells)
        {
            if (cell != null)
                cell.SetManager(this);
        }
    }

    [ContextMenu("Collect All Cells From Canvas")]
    private void CollectCells()
    {
        cells = GetComponentsInChildren<ScratchCell>(true);
    }

    [ContextMenu("Reset All Cells (Play Mode)")]
    public void ResetAllCells()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Reset All Cells is available in Play Mode.", this);
            return;
        }

        BindCells();
        foreach (ScratchCell cell in cells)
        {
            if (cell != null)
                cell.ResetScratch();
        }
    }

    private void OnValidate()
    {
        brushRadius = Mathf.Clamp(brushRadius, 1, 128);
        scratchPercentage = Mathf.Clamp(scratchPercentage, 0.1f, 100f);
        textureResolution = Mathf.Clamp(textureResolution, 16, 512);
    }
}
