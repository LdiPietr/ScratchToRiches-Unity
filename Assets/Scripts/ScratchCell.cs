using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI pointer handling and scratch-mask state for one cell.
/// Scratch settings are owned exclusively by ScratchCellManager on the Canvas.
/// </summary>
public sealed partial class ScratchCell : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICanvasRaycastFilter
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
    private bool[] scratchablePixels;
    private int scratchablePixelCount;
    [SerializeField] private Image vintageRevealedBackground;
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
        ResetRevealFeedback();
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
            Vector2 bounds = scratchImage != null ? scratchImage.rectTransform.rect.size : new Vector2(124f,124f);
            indexText.rectTransform.sizeDelta = new Vector2(bounds.x * .72f, bounds.y * .36f);
            indexText.alignment = TMPro.TextAlignmentOptions.Center;
            indexText.fontSize = Mathf.Min(bounds.x, bounds.y) * .25f;
            indexText.color = new Color(1f, 0.71f, 0.12f, 1f);
        }
        indexText.gameObject.SetActive(jolly && !preparingTicket);
    }

    public float RevealedPercentage =>
        erasedPixels == null || erasedPixels.Length == 0
            ? 0f
            : completed ? 100f : (100f * erasedPixelCount / Mathf.Max(1, scratchablePixelCount));

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
        ResetRevealFeedback();
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

        scratchablePixels = new bool[count];
        scratchablePixelCount = 0;
        for (int i = 0; i < count; i++)
        {
            scratchablePixels[i] = pixels[i].a >= 128;
            if (scratchablePixels[i]) scratchablePixelCount++;
        }
        if (scratchablePixelCount == 0)
        {
            Debug.LogError("ScratchCell: empty scratchable shape.", this);
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
