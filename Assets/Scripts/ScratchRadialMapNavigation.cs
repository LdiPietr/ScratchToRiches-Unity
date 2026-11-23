using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pointer-driven navigation of a single authored 90-node skill map.
/// Drag pans; wheel zooms around the cursor; zoom controls are scene-authored.
/// No gameplay purchases or UI objects are created here.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchRadialMapNavigation : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;
    [SerializeField, Range(0.2f, 1f)] private float minimumZoom = 0.27f;
    [SerializeField, Range(0.5f, 2f)] private float maximumZoom = 1.3f;
    [SerializeField, Range(0.25f, 1.2f)] private float initialZoom = 0.66f;
    [SerializeField] private float wheelFactor = 1.15f;

    private bool dragging;
    private float zoom = 1f;
    private Vector2 lastPointer;

    private void Awake()
    {
        if (viewport == null) viewport = GetComponent<RectTransform>();
        // The viewport must also offer a real whole-map overview despite a
        // previously serialized experimental minimum of 0.27.
        minimumZoom = Mathf.Min(minimumZoom, 0.2f);
    }

    private void Start()
    {
        if (content == null || viewport == null)
        {
            Debug.LogError("Radial map: missing authored viewport/content.", this);
            enabled = false;
            return;
        }
        if (zoomInButton != null) zoomInButton.onClick.AddListener(ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);
        // Start by showing the nucleus and the first ring of ALL five
        // branches, while still keeping the icons readable.
        SetZoom(Mathf.Min(initialZoom, 0.52f), Vector2.zero);
        CenterOn(Vector2.zero);
    }

    private void OnDestroy()
    {
        if (zoomInButton != null) zoomInButton.onClick.RemoveListener(ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.RemoveListener(ZoomOut);
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || content == null)
            return;
        // Dragging a node must never accidentally finish a hold-to-buy.
        ScratchHoldToBuy hold = data.pointerPress != null
            ? data.pointerPress.GetComponent<ScratchHoldToBuy>() : null;
        if (hold != null) hold.CancelForMapPan();
        dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, data.position, data.pressEventCamera, out lastPointer);
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || content == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, data.position, data.pressEventCamera, out Vector2 now)) return;
        content.anchoredPosition += now - lastPointer;
        lastPointer = now;
        ClampPosition();
    }

    public void OnEndDrag(PointerEventData data) => dragging = false;

    public void OnScroll(PointerEventData data)
    {
        if (data.scrollDelta.y == 0f || content == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, data.position, data.pressEventCamera, out Vector2 point))
            point = Vector2.zero;
        SetZoom(zoom * Mathf.Pow(wheelFactor, data.scrollDelta.y), point);
    }

    public void ZoomIn() => SetZoom(zoom * 1.2f, Vector2.zero);
    public void ZoomOut() => SetZoom(zoom / 1.2f, Vector2.zero);
    public float Zoom => zoom;

    public void SetZoom(float requested, Vector2 pivotInViewport)
    {
        if (content == null || viewport == null) return;
        float target = Mathf.Clamp(requested, minimumZoom, maximumZoom);
        float oldScale = content.localScale.x;
        if (oldScale <= 0f) oldScale = 1f;
        Vector2 pointInContent = (pivotInViewport - content.anchoredPosition) / oldScale;
        zoom = target;
        content.localScale = Vector3.one * zoom;
        content.anchoredPosition = pivotInViewport - pointInContent * zoom;
        ClampPosition();
    }

    public void CenterOn(Vector2 positionInContent)
    {
        if (content == null) return;
        content.anchoredPosition = -positionInContent * content.localScale.x;
        ClampPosition();
    }

    private void ClampPosition()
    {
        if (content == null || viewport == null) return;
        Vector2 scaled = Vector2.Scale(content.rect.size,
            new Vector2(content.localScale.x, content.localScale.y));
        Vector2 halfExcess = new Vector2(
            Mathf.Max(0f, (scaled.x - viewport.rect.width) * .5f),
            Mathf.Max(0f, (scaled.y - viewport.rect.height) * .5f));
        content.anchoredPosition = new Vector2(
            Mathf.Clamp(content.anchoredPosition.x, -halfExcess.x, halfExcess.x),
            Mathf.Clamp(content.anchoredPosition.y, -halfExcess.y, halfExcess.y));
    }
}
