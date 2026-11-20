using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI pointer handling and scratch-mask state for one cell.
/// Scratch settings are owned exclusively by ScratchCellManager on the Canvas.
/// </summary>
public sealed class ScratchCell : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RawImage scratchImage;
    [SerializeField] private TMP_Text symbolText;
    [SerializeField] private TMP_Text indexText;

    private ScratchCellManager manager;
    private ScratchGameManager gameManager;
    private RectTransform scratchRect;
    private Texture2D scratchTexture;
    private bool[] erasedPixels;
    private int erasedPixelCount;
    private bool pointerDown;
    private int activePointerId;
    private bool completed;
    private bool hasPreviousPixel;
    private Vector2Int previousPixel;
    private bool firstStrokeUsed;
    private bool previewActive;
    private const float PreviewCoverOpacity = 0.75f;

    public bool IsComplete => completed;
    public bool HasSymbolText => symbolText != null;

    /// <summary>
    /// Preview skills reveal the real artwork beneath a translucent scratch
    /// cover. No textual symbol hint or cell number is displayed.
    /// The mask still requires 85% real scratching to complete the cell.
    /// </summary>
    public void SetPreview(string symbol)
    {
        previewActive = !string.IsNullOrEmpty(symbol);
        if (indexText != null)
        {
            indexText.text = string.Empty;
            if (indexText.gameObject.activeSelf)
                indexText.gameObject.SetActive(false);
        }
        RefreshCoverOpacity();
    }

    private void RefreshCoverOpacity()
    {
        if (scratchImage != null)
            scratchImage.color = new Color(1f, 1f, 1f,
                previewActive ? PreviewCoverOpacity : 1f);
    }

    /// <summary>The visible symbol lives under the scratch mask; only M3 updates it.</summary>
    public void SetSymbol(string symbol)
    {
        if (symbolText != null)
            symbolText.text = symbol;
        else
            Debug.LogError("ScratchCell is missing its TextMeshPro symbol reference.", this);
    }

    public float RevealedPercentage =>
        erasedPixels == null || erasedPixels.Length == 0
            ? 0f
            : completed ? 100f : (100f * erasedPixelCount / erasedPixels.Length);

    public void SetManager(ScratchCellManager owner)
    {
        manager = owner;
        gameManager = owner != null ? owner.GetComponent<ScratchGameManager>() : null;
    }

    private void Start()
    {
        ResetScratch();
    }

    /// <summary>
    /// Recreate the cover from global settings; safe to call more than once.
    /// </summary>
    public void ResetScratch()
    {
        if (manager == null)
            manager = GetComponentInParent<ScratchCellManager>();

        if (gameManager == null && manager != null)
            gameManager = manager.GetComponent<ScratchGameManager>();

        if (manager == null || scratchImage == null)
        {
            Debug.LogError("ScratchCell requires a ScratchCellManager on the Canvas and an assigned RawImage.", this);
            enabled = false;
            return;
        }

        int resolution = manager.TextureResolution;
        if (scratchTexture == null || scratchTexture.width != resolution)
        {
            if (scratchTexture != null)
                Destroy(scratchTexture);

            scratchTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name + "_ScratchMask"
            };
        }

        int count = resolution * resolution;
        erasedPixels = new bool[count];
        erasedPixelCount = 0;
        completed = false;
        pointerDown = false;
        hasPreviousPixel = false;
        firstStrokeUsed = false;

        Color32 coverColor = manager.CoatingColor;
        Color32[] pixels = new Color32[count];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = coverColor;

        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);

        // Avoid local tint multiplying the manager's global coating color.
        RefreshCoverOpacity();
        scratchImage.texture = scratchTexture;
        scratchRect = scratchImage.rectTransform;
        enabled = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (completed || scratchTexture == null)
            return;

        pointerDown = true;
        activePointerId = eventData.pointerId;
        hasPreviousPixel = false;
        EraseRoutine(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDown || completed || activePointerId != eventData.pointerId)
            return;

        EraseRoutine(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerDown && activePointerId == eventData.pointerId)
        {
            pointerDown = false;
            hasPreviousPixel = false;
        }
    }

    private void OnDisable()
    {
        pointerDown = false;
        hasPreviousPixel = false;
    }

    private void EraseRoutine(PointerEventData eventData)
    {
        if (scratchRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                scratchRect, eventData.position, eventData.pressEventCamera, out Vector2 point))
        {
            hasPreviousPixel = false;
            return;
        }

        Rect rect = scratchRect.rect;
        if (rect.width <= 0f || rect.height <= 0f || !rect.Contains(point))
        {
            // Do not clamp outside touches to the border or join a stroke across the outside.
            hasPreviousPixel = false;
            return;
        }

        // Claim a cell only once the pointer actually scratches inside its bounds.
        // Already selected cells remain scratchable until the round ends.
        if (gameManager != null && !gameManager.TrySelectCell(this))
        {
            hasPreviousPixel = false;
            return;
        }

        int size = scratchTexture.width;
        int pixelX = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x) * size), 0, size - 1);
        int pixelY = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(rect.yMin, rect.yMax, point.y) * size), 0, size - 1);
        Vector2Int current = new Vector2Int(pixelX, pixelY);

        // Interpolate stamps: fast mouse/touch dragging must not skip pixels.
        float spacing = Mathf.Max(1f, manager.BrushRadius * 0.5f);
        int steps = hasPreviousPixel
            ? Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(previousPixel, current) / spacing))
            : 1;

        int radius = manager.EffectiveBrushRadius;
        if (!firstStrokeUsed)
        {
            radius = Mathf.Clamp(radius + manager.FirstStrokeBonus, 1, 128);
            firstStrokeUsed = true;
        }

        bool changed = false;
        int additional = manager.AdditionalScratchStripes;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.x, current.x, t)) : current.x;
            int y = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.y, current.y, t)) : current.y;
            changed |= EraseStamp(x, y, radius);

            // Skill tree double/multi-pass: parallel strokes, clamped inside mask.
            for (int stripe = 1; stripe <= additional; stripe++)
            {
                int offset = Mathf.Max(1, radius) * ((stripe + 1) / 2);
                if ((stripe & 1) == 0) offset = -offset;
                changed |= EraseStamp(x + offset, y, radius);
            }
        }

        previousPixel = current;
        hasPreviousPixel = true;
        if (!changed)
            return;

        scratchTexture.Apply(false);
        if (100f * erasedPixelCount / erasedPixels.Length >= manager.ScratchPercentage)
            Complete();
    }

    private bool EraseStamp(int centerX, int centerY, int radius)
    {
        int radiusSquared = radius * radius;
        int resolution = scratchTexture.width;
        Color32 clear = manager.CoatingColor;
        clear.a = 0;
        bool changed = false;

        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(resolution - 1, centerY + radius); y++)
        {
            int dy = y - centerY;
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(resolution - 1, centerX + radius); x++)
            {
                int dx = x - centerX;
                if (dx * dx + dy * dy > radiusSquared)
                    continue;

                int index = y * resolution + x;
                if (erasedPixels[index])
                    continue;

                erasedPixels[index] = true;
                erasedPixelCount++;
                scratchTexture.SetPixel(x, y, clear);
                changed = true;
            }
        }

        return changed;
    }

    private void Complete()
    {
        completed = true;
        pointerDown = false;
        hasPreviousPixel = false;

        Color32 clear = manager.CoatingColor;
        clear.a = 0;
        Color32[] pixels = new Color32[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);

        if (gameManager != null)
            gameManager.OnCellCompleted(this);
    }

    private void OnDestroy()
    {
        if (scratchTexture == null)
            return;

        if (scratchImage != null && scratchImage.texture == scratchTexture)
            scratchImage.texture = null;

        if (Application.isPlaying)
            Destroy(scratchTexture);
        else
            DestroyImmediate(scratchTexture);
    }
}
