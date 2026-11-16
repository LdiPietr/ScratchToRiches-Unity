using UnityEngine;

/// <summary>
/// Global scratch configuration. Attach this to the Canvas containing the
/// ScratchCell components; it finds and controls all child cells.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class ScratchCellManager : MonoBehaviour
{
    [Header("Settings shared by ALL cells")]
    [SerializeField, Range(1, 128)]
    [Tooltip("Brush radius in scratch-texture pixels.")]
    private int brushRadius = 10;

    [SerializeField, Range(0.1f, 100f)]
    [Tooltip("Revealed percentage that instantly clears the remaining cover.")]
    private float scratchPercentage = 99.8f;

    [SerializeField, Range(16, 512)]
    [Tooltip("Texture size (width and height) for every scratch mask. Reset cells to apply changes in Play Mode.")]
    private int textureResolution = 128;

    [SerializeField]
    [Tooltip("Single coating color for all cells. Reset cells to apply changes in Play Mode.")]
    private Color coatingColor = new Color(0.69330716f, 0.7735849f, 0.77050906f, 1f);

    [Header("Auto-collected cells (Canvas children)")]
    [SerializeField] private ScratchCell[] cells = new ScratchCell[0];

    public int BrushRadius => brushRadius;
    public float ScratchPercentage => scratchPercentage;
    public int TextureResolution => textureResolution;
    public Color CoatingColor => coatingColor;
    public int CellCount => cells == null ? 0 : cells.Length;

    private void Reset()
    {
        CollectCells();
    }

    private void Awake()
    {
        CollectCells();
    }

    /// <summary>
    /// Rebuild the list when cells are added, removed or reordered.
    /// Includes inactive cells so later upgrades can use the same manager.
    /// </summary>
    [ContextMenu("Collect All Scratch Cells")]
    public void CollectCells()
    {
        cells = GetComponentsInChildren<ScratchCell>(true);
        foreach (ScratchCell cell in cells)
            cell.SetManager(this);

        if (Application.isPlaying && cells.Length == 0)
            Debug.LogWarning("ScratchCellManager found no ScratchCell components under the Canvas.", this);
    }

    /// <summary>Restore every cover with the current Inspector settings.</summary>
    [ContextMenu("Reset All Cells (Play Mode)")]
    public void ResetAllCells()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Reset All Cells works in Play Mode.", this);
            return;
        }

        CollectCells();
        foreach (ScratchCell cell in cells)
            cell.ResetScratch();
    }

    private void OnValidate()
    {
        brushRadius = Mathf.Clamp(brushRadius, 1, 128);
        scratchPercentage = Mathf.Clamp(scratchPercentage, 0.1f, 100f);
        textureResolution = Mathf.Clamp(textureResolution, 16, 512);
    }
}
