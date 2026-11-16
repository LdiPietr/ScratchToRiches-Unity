using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles input and its own scratch mask. All gameplay settings come from
/// ScratchCellManager on the parent Canvas, never from individual cells.
/// </summary>
public sealed class ScratchCell : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RawImage scratchImage;

    private ScratchCellManager manager;
    private RectTransform scratchRect;
    private Texture2D scratchTexture;
    private bool[] erasedPixels;
    private int erasedPixelCount;
    private bool pointerDown;
    private int activePointerId;
    private bool completed;

    public void SetManager(ScratchCellManager owner)
    {
        manager = owner;
    }

    private void Start()
    {
        ResetScratch();
    }

    /// <summary>Restore the full cover, using current manager settings.</summary>
    public void ResetScratch()
    {
        if (manager == null)
            manager = GetComponentInParent<ScratchCellManager>();

        if (manager == null || scratchImage == null)
        {
            Debug.LogError("ScratchCell requires a parent ScratchCellManager and a Scratch Image.", this);
            enabled = false;
            return;
        }

        int resolution = manager.TextureResolution;
        if (scratchTexture == null || scratchTexture.width != resolution)
        {
            if (scratchTexture != null)
                Destroy(scratchTexture);

            scratchTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            scratchTexture.wrapMode = TextureWrapMode.Clamp;
            scratchTexture.filterMode = FilterMode.Bilinear;
            scratchTexture.name = name + "_ScratchMask";
        }

        int count = resolution * resolution;
        erasedPixels = new bool[count];
        erasedPixelCount = 0;
        completed = false;
        pointerDown = false;

        Color32 cover = manager.CoatingColor;
        Color32[] pixels = new Color32[count];
        for (int i = 0; i < count; i++)
            pixels[i] = cover;

        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);
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
        EraseRoutine(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDown || activePointerId != eventData.pointerId || completed)
            return;

        EraseRoutine(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
            pointerDown = false;
    }

    private void OnDisable()
    {
        pointerDown = false;
    }

    private void EraseRoutine(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                scratchRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Rect rect = scratchRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        int pixelX = Mathf.Clamp(Mathf.FloorToInt(u * scratchTexture.width), 0, scratchTexture.width - 1);
        int pixelY = Mathf.Clamp(Mathf.FloorToInt(v * scratchTexture.height), 0, scratchTexture.height - 1);
        EraseCircle(pixelX, pixelY);
    }

    private void EraseCircle(int pixelX, int pixelY)
    {
        int radius = manager.BrushRadius;
        int radiusSquared = radius * radius;
        int resolution = scratchTexture.width;
        int minX = Mathf.Max(0, pixelX - radius);
        int maxX = Mathf.Min(resolution - 1, pixelX + radius);
        int minY = Mathf.Max(0, pixelY - radius);
        int maxY = Mathf.Min(resolution - 1, pixelY + radius);
        Color32 clear = manager.CoatingColor;
        clear.a = 0;
        bool changed = false;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - pixelY;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - pixelX;
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

        if (!changed)
            return;

        scratchTexture.Apply(false);
        if (100f * erasedPixelCount / erasedPixels.Length >= manager.ScratchPercentage)
            Complete();
    }

    private void Complete()
    {
        completed = true;
        pointerDown = false;

        Color32 clear = manager.CoatingColor;
        clear.a = 0;
        Color32[] pixels = new Color32[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply(false);
    }

    private void OnDestroy()
    {
        if (scratchTexture != null)
        {
            if (scratchImage != null && scratchImage.texture == scratchTexture)
                scratchImage.texture = null;
            Destroy(scratchTexture);
        }
    }
