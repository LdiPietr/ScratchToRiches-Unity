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
    // Author-time UI Image under the grattable RawImage. Legacy TMP fallback stays optional.
    [SerializeField] private Image symbolImage;
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
    private bool titanThisGesture;
    private bool previewActive;
    private bool preparingTicket;
    private string currentSymbolLabel = string.Empty;
    private const float PreviewCoverOpacity = 0.75f;

    public bool IsComplete => completed;
    public bool HasSymbolDisplay => symbolImage != null || symbolText != null;
    public bool IsPreviewVisible => previewActive && !completed;

    /// <summary>
    /// Player-accessible knowledge only. Other hidden symbol labels stay private.
    /// </summary>
    public bool TryGetVisibleSymbol(out string label)
    {
        label = (previewActive || completed) && !preparingTicket ? currentSymbolLabel : string.Empty;
        return !string.IsNullOrEmpty(label);
    }

    /// <summary>
    /// Keep every symbol renderer hidden while swapping ticket geometry, coating
    /// and symbol data. Inactive authored cells participate as well.
    /// </summary>
    public void BeginTicketVisualPreparation()
    {
        preparingTicket = true;
        previewActive = false;
        completed = false;
        currentSymbolLabel = string.Empty;
        if (symbolImage != null) symbolImage.enabled = false;
        if (symbolText != null) symbolText.enabled = false;
        if (indexText != null) indexText.gameObject.SetActive(false);
        if (scratchImage != null)
        {
            scratchImage.enabled = true;
            scratchImage.color = Color.white;
        }
    }

    /// <summary>Only reveal the symbol layer after its opaque scratch mask exists.</summary>
    public bool EndTicketVisualPreparation()
    {
        if (!preparingTicket) return false;
        if (scratchImage == null || scratchTexture == null || scratchImage.texture != scratchTexture)
        {
            Debug.LogError("ScratchCell: cannot reveal symbol layer without a prepared scratch cover.", this);
            return false;
        }
        scratchImage.enabled = true;
        scratchImage.raycastTarget = true;
        RefreshCoverOpacity();
        preparingTicket = false;
        if (symbolImage != null) symbolImage.enabled = symbolImage.sprite != null;
        if (symbolText != null) symbolText.enabled = true;
        UpdateJollyArtwork();
        return true;
    }

    /// <summary>
    /// Preview skills reveal the real artwork beneath a translucent scratch
    /// cover. No textual symbol hint or cell number is displayed.
    /// The mask still requires 85% real scratching to complete the cell.
    /// </summary>
    public void SetPreview(string symbol)
    {
        previewActive = !string.IsNullOrEmpty(symbol);
        UpdateJollyArtwork();
        RefreshCoverOpacity();
    }

    private void RefreshCoverOpacity()
    {
        if (scratchImage != null)
            scratchImage.color = new Color(1f, 1f, 1f,
                previewActive ? PreviewCoverOpacity : 1f);
    }

    /// <summary>
    /// A sprite is shown under the scratch coating; the legacy text renderer
    /// remains a fallback for older test scenes without a sprite reference.
    /// </summary>
    public void SetSymbol(string symbol, Sprite artwork = null)
    {
        currentSymbolLabel = symbol ?? string.Empty;
        UpdateJollyArtwork();
        if (symbolImage != null)
        {
            symbolImage.sprite = artwork;
            symbolImage.enabled = artwork != null && !preparingTicket;
        }
        if (symbolText != null)
        {
            symbolText.text = artwork == null ? symbol : string.Empty;
            symbolText.enabled = !preparingTicket;
        }
        if (symbolImage == null && symbolText == null)
            Debug.LogError("ScratchCell: missing both Image and TextMeshPro symbol renderers.", this);
    }

    private void UpdateJollyArtwork()
    {
        if (indexText == null) return;
        bool jolly = currentSymbolLabel == "JOLLY";
        indexText.text = jolly ? "JOLLY" : string.Empty;
        if (jolly)
        {
            // The authored CellIndex text becomes the joker artwork. Keep it
            // BEHIND the coating, never above it or hidden symbols would leak.
            indexText.transform.SetSiblingIndex(1);
            indexText.rectTransform.anchoredPosition = Vector2.zero;
            indexText.rectTransform.sizeDelta = new Vector2(132f, 60f);
            indexText.alignment = TMPro.TextAlignmentOptions.Center;
            indexText.fontSize = 32f;
            indexText.color = new Color(1f, 0.71f, 0.12f, 1f);
        }
        indexText.gameObject.SetActive(jolly && !preparingTicket);
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
        // Dynamically activated 4x4 / 5x5 cells may have been fully prepared
        // earlier in the same frame by ScratchGameManager. Do not reset their
        // fresh masks or erase a preview when Start runs for the first time.
        if (scratchTexture == null) ResetScratch();
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
        previewActive = false; // The previous ticket's transparency must NEVER survive reset.
        pointerDown = false;
        hasPreviousPixel = false;
        firstStrokeUsed = false;
        titanThisGesture = false;

        Color32[] pixels = manager.GetCoverPixels();
        if (pixels.Length != count)
        {
            Debug.LogError("ScratchCell: coating texture resolution mismatch.", this);
            enabled = false;
            return;
        }

        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);

        // Avoid local tint multiplying the manager's global coating color.
        RefreshCoverOpacity();
        scratchImage.texture = scratchTexture;
        scratchImage.enabled = true;
        scratchImage.raycastTarget = true;
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
        if (manager != null)
            manager.RedirectDragToSelectedCell(this, eventData);
    }

    /// <summary>Continue the SAME physical drag into another already selected cell.</summary>
    public bool ScratchFromSelectedDrag(PointerEventData eventData)
    {
        if (completed || scratchImage == null || scratchTexture == null ||
            gameManager == null || !gameManager.IsCellSelected(this) || scratchRect == null)
            return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                scratchRect, eventData.position, eventData.pressEventCamera, out Vector2 local) ||
            !scratchRect.rect.Contains(local))
            return false;
        hasPreviousPixel = false;
        EraseRoutine(eventData);
        return true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerDown && activePointerId == eventData.pointerId)
        {
            pointerDown = false;
            hasPreviousPixel = false;
            titanThisGesture = false;
        }
    }

    private void OnDisable()
    {
        pointerDown = false;
        hasPreviousPixel = false;
        titanThisGesture = false;
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

        if (!titanThisGesture && pointerDown &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            ScratchSkillTree tree = gameManager != null
                ? gameManager.GetComponent<ScratchSkillTree>() : null;
            titanThisGesture = tree != null && tree.TryConsumeManualTitanCharge();
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
        if (titanThisGesture)
            radius = Mathf.Clamp(Mathf.RoundToInt(radius * 2.5f), 1, 128);
        if (!firstStrokeUsed)
        {
            radius = Mathf.Clamp(radius + manager.FirstStrokeBonus, 1, 128);
            firstStrokeUsed = true;
        }

        bool changed = false;
        int additional = manager.AdditionalScratchStripes +
            (titanThisGesture ? 3 : 0);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.x, current.x, t)) : current.x;
            int y = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.y, current.y, t)) : current.y;
            changed |= EraseStamp(x, y, radius);

            // Skill tree double/multi-pass: parallel strokes, clamped inside mask.
            for (int stripe = 1; stripe <= additional; stripe++)
            {
                int offset = 12 * ((stripe + 1) / 2);
                if ((stripe & 1) == 0) offset = -offset;
                changed |= EraseStamp(x + offset, y, radius);
            }
            if (manager.FanModeEnabled)
            {
                // G04: two converging manual strips around the main track.
                int fanOffset = Mathf.RoundToInt(18f * (1f - t));
                changed |= EraseStamp(x - fanOffset, y, radius);
                changed |= EraseStamp(x + fanOffset, y, radius);
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
        Color32 clear = new Color32(0, 0, 0, 0);
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

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);

        if (gameManager != null)
        {
            // Optional physical-print feedback on the real scratched cell.
            ScratchWinJuice feedback = gameManager.GetComponent<ScratchWinJuice>();
            if (feedback != null) feedback.OnCellRevealed(scratchRect);
            gameManager.OnCellCompleted(this);
        }
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
