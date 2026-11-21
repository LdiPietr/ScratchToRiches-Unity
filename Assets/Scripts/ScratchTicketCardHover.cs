using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Responsive ticket-card hover/press, without runtime UI builders.</summary>
[DisallowMultipleComponent]
public sealed class ScratchTicketCardHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.055f;
    [SerializeField, Min(1f)] private float speed = 12f;
    private Vector3 restingScale;
    private bool hovered, pressed;

    private void Awake() { restingScale = transform.localScale; }
    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        if (restingScale != Vector3.zero) transform.localScale = restingScale;
    }
    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        if (restingScale != Vector3.zero) transform.localScale = restingScale;
    }
    public void OnPointerEnter(PointerEventData data) { hovered = true; }
    public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) pressed = true;
    }
    public void OnPointerUp(PointerEventData data) { pressed = false; }

    private void Update()
    {
        float target = pressed ? 0.975f : hovered ? hoverScale : 1f;
        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, restingScale * target, t);
    }
}
