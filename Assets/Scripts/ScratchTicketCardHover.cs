using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Responsive ticket-card hover/press, without runtime UI builders.</summary>
[DisallowMultipleComponent]
public sealed class ScratchTicketCardHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.055f;
    [SerializeField, Min(1f)] private float speed = 12f;
    [SerializeField] private Sprite cardNormal, cardHover, actionHover;
    private Button button;
    private Image cardImage, actionImage;
    private Vector3 restingScale;
    private bool hovered, pressed;

    private void Awake()
    {
        restingScale = transform.localScale;
        button = GetComponent<Button>();
        cardImage = GetComponent<Image>();
        actionImage = transform.Find("ChoiceActionBackground")?.GetComponent<Image>();
    }
    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        if (restingScale != Vector3.zero) transform.localScale = restingScale;
        ResetSprites();
    }
    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        if (restingScale != Vector3.zero) transform.localScale = restingScale;
        ResetSprites();
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
        bool usable = button == null || button.interactable;
        if (cardImage != null && cardNormal != null && cardHover != null)
            cardImage.sprite = hovered && usable ? cardHover : cardNormal;
        if (actionImage != null)
            actionImage.overrideSprite = hovered && usable && actionHover != null ? actionHover : null;
        float target = !usable ? 1f : pressed ? 0.975f : hovered ? hoverScale : 1f;
        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, restingScale * target, t);
    }
    private void ResetSprites()
    {
        if (cardImage != null && cardNormal != null) cardImage.sprite = cardNormal;
        if (actionImage != null) actionImage.overrideSprite = null;
    }
}
