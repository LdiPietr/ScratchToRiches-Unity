using UnityEngine;
using System;

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
    private float scratchPercentage = 85f;

    [SerializeField, Range(16, 512)]
    [Tooltip("Texture size (width and height) for every scratch mask. Reset cells to apply changes in Play Mode.")]
    private int textureResolution = 128;

    [SerializeField]
    [Tooltip("Single coating color for all cells. Reset cells to apply changes in Play Mode.")]
    private Color coatingColor = new Color(0.77f, 0.79f, 0.82f, 1f);

    [Header("Art: scene-linked scratch foils")]
    [SerializeField] private Sprite brushedCover;
    [SerializeField] private Sprite premiumCover;

    private Sprite activeCover;
    private Sprite cachedSource;
    private int cachedResolution = -1;
    private Color32[] cachedPixels;

    /// <summary>Classic uses brushed foil; Lucky and Golden use premium foil.</summary>
    public void PrepareCoverForTicket(ScratchGameManager.TicketKind kind)
    {
        activeCover = kind == ScratchGameManager.TicketKind.Standard
            ? brushedCover : premiumCover;
    }

    /// <summary>
    /// Cache an opaque art sample for active scratch masks.
    /// The source PNG supplies the RGB details; mask erasing retains the
    /// original alpha behavior and the 85 percent completion threshold.
    /// </summary>
    public Color32[] GetCoverPixels()
    {
        Sprite sprite = activeCover != null ? activeCover : brushedCover;
        if (cachedPixels != null && cachedResolution == textureResolution &&
            cachedSource == sprite)
            return cachedPixels;

        int resolution = textureResolution;
        cachedPixels = new Color32[resolution * resolution];
        cachedResolution = resolution;
        cachedSource = sprite;
        Color32 backing = coatingColor;
        backing.a = 255;

        if (sprite == null)
        {
            for (int i = 0; i < cachedPixels.Length; i++)
                cachedPixels[i] = backing;
            return cachedPixels;
        }

        Texture2D texture = sprite.texture;
        if (texture == null || !texture.isReadable)
        {
            Debug.LogWarning("Scratch cover texture is not readable; using solid coating. Enable Read/Write in the PNG import settings.", this);
            for (int i = 0; i < cachedPixels.Length; i++)
                cachedPixels[i] = backing;
            return cachedPixels;
        }

        Rect rect = sprite.textureRect;
        Color32[] source = texture.GetPixels32();
        int texWidth = texture.width;
        int texHeight = texture.height;
        for (int y = 0; y < resolution; y++)
        {
            int sy = Mathf.Clamp(Mathf.FloorToInt(rect.yMin + (y + 0.5f) *
                rect.height / resolution), 0, texHeight - 1);
            for (int x = 0; x < resolution; x++)
            {
                int sx = Mathf.Clamp(Mathf.FloorToInt(rect.xMin + (x + 0.5f) *
                    rect.width / resolution), 0, texWidth - 1);
                Color32 pixel = source[sy * texWidth + sx];
                float a = pixel.a / 255f;
                cachedPixels[y * resolution + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(backing.r, pixel.r, a)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(backing.g, pixel.g, a)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(backing.b, pixel.b, a)), 255);
            }
        }
        return cachedPixels;
    }

    [Header("Auto-collected cells (Canvas children)")]
    [SerializeField] private ScratchCell[] cells = new ScratchCell[0];
    [SerializeField] private RectTransform gridRoot;
    private ScratchCell[] activeCells = Array.Empty<ScratchCell>();
    public ScratchCell[] ActiveCells => activeCells;
    private bool preparingTicket;


    private ScratchSkillTree skills;
    private ScratchGameManager game;
    private bool fanModeEnabled = true;

    private int SkillLevel(ScratchSkillTree.SkillId id)
    {
        if (skills == null) skills = GetComponent<ScratchSkillTree>();
        return skills != null ? skills.GetLevel(id) : 0;
    }

    public int BrushRadius => brushRadius;
    public int EffectiveBrushRadius
    {
        get
        {
            if (game == null) game = GetComponent<ScratchGameManager>();
            int radius = brushRadius + 2 * SkillLevel(ScratchSkillTree.SkillId.G1);
            if (game != null && game.TicketActive && game.SelectedCount <= 1)
                radius += 3 * SkillLevel(ScratchSkillTree.SkillId.G5);
            return Mathf.Clamp(radius, 1, 128);
        }
    }

    public int FirstStrokeBonus => 4 * SkillLevel(ScratchSkillTree.SkillId.G2);
    public int AdditionalScratchStripes =>
        Mathf.Clamp(SkillLevel(ScratchSkillTree.SkillId.G3), 0, 2);
    public bool FanModeEnabled =>
        SkillLevel(ScratchSkillTree.SkillId.G4) > 0 && fanModeEnabled;
    public bool ContinuousDragEnabled =>
        SkillLevel(ScratchSkillTree.SkillId.G7) > 0;

    private void Update()
    {
        // F toggles the manual fan; it never scratches autonomously.
        if (Application.isPlaying && SkillLevel(ScratchSkillTree.SkillId.G4) > 0 &&
            Input.GetKeyDown(KeyCode.F))
            fanModeEnabled = !fanModeEnabled;
    }

    public void RedirectDragToSelectedCell(ScratchCell origin,
        UnityEngine.EventSystems.PointerEventData pointer)
    {
        if (!ContinuousDragEnabled || game == null || pointer == null) return;
        foreach (ScratchCell other in activeCells)
        {
            if (other == null || other == origin || other.IsComplete ||
                !game.IsCellSelected(other)) continue;
            if (other.ScratchFromSelectedDrag(pointer)) return;
        }
    }
    // G06 Bordo pulito: 85 -> 80 -> 75 percent; still requires a manual gesture.
    public float ScratchPercentage =>
        Mathf.Clamp(scratchPercentage - 5f * SkillLevel(ScratchSkillTree.SkillId.G6), 1f, 100f);
    public int TextureResolution => textureResolution;
    public Color CoatingColor => coatingColor;
    public int CellCount => activeCells.Length;

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
        Array.Sort(cells, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (ScratchCell cell in cells)
            cell.SetManager(this);

        if (Application.isPlaying && cells.Length == 0)
            Debug.LogWarning("ScratchCellManager found no ScratchCell components under the Canvas.", this);
    }

    public void BeginTicketVisualPreparation()
    {
        CollectCells();
        preparingTicket = true;
        foreach (ScratchCell cell in cells) cell.BeginTicketVisualPreparation();
    }

    public bool EndTicketVisualPreparation()
    {
        if (!preparingTicket) return false;
        bool allReady = true;
        foreach (ScratchCell cell in activeCells)
            if (!cell.EndTicketVisualPreparation()) allReady = false;
        preparingTicket = false;
        return allReady;
    }

    /// <summary>Activates only authored scene cells. No runtime UI creation.</summary>
    public bool ConfigureGrid(int side)
    {
        if (side < 3 || side > 5) return false;
        CollectCells();
        int required = side * side;
        if (cells.Length < required)
        {
            Debug.LogError($"Grid {side}x{side} needs {required} authored cells, found {cells.Length}.", this);
            return false;
        }
        if (gridRoot == null) gridRoot = cells[0].transform.parent as RectTransform;
        if (gridRoot == null) return false;
        const float total = 532f;
        const float gap = 12f;
        float size = (total - (side - 1) * gap) / side;
        float step = size + gap;
        activeCells = new ScratchCell[required];
        for (int i = 0; i < cells.Length; i++)
        {
            bool show = i < required;
            if (show)
            {
                RectTransform rect = cells[i].GetComponent<RectTransform>();
                rect.SetParent(gridRoot, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i % side - (side - 1) * 0.5f) * step,
                    ((side - 1) * 0.5f - i / side) * step);
                rect.localScale = Vector3.one * (size / 124f);
                activeCells[i] = cells[i];
            }
            if (cells[i].gameObject.activeSelf != show)
                cells[i].gameObject.SetActive(show);
        }
        return true;
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

        foreach (ScratchCell cell in activeCells)
            cell.ResetScratch();

        // Inspector resets in Play Mode must also clear round pick counters.
        ScratchGameManager gameManager = GetComponent<ScratchGameManager>();
        if (gameManager != null)
            gameManager.OnCellsReset();
    }

    private void OnValidate()
    {
        brushRadius = Mathf.Clamp(brushRadius, 1, 128);
        scratchPercentage = Mathf.Clamp(scratchPercentage, 0.1f, 100f);
        textureResolution = Mathf.Clamp(textureResolution, 16, 512);
    }
}
