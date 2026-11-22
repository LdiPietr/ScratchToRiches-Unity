using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Purchase a skill by holding its scene-authored node button for 0.7 seconds.
/// One purchase per press; moving outside or releasing cancels the progress.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchHoldToBuy : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private ScratchSkillTreeUI treeUI;
    [SerializeField, Range(0, 7)] private int nodeSlot;
    [SerializeField, Min(0.1f)] private float holdDuration = 0.7f;
    [SerializeField] private Image progressFill;

    private bool holding;
    private float elapsed;
    private int pressedBranch;
#if UNITY_EDITOR
    private bool qaHolding;
    public bool QaHolding => qaHolding && holding;
    public float QaElapsed => elapsed;
    public float QaDuration => holdDuration;
#endif

    private void Awake()
    {
        if (progressFill != null)
        {
            // A sprite-less Filled Image appears fully drawn in Unity UI.
            // A Simple Image can be revealed with a controlled RectTransform scale.
            progressFill.type = Image.Type.Simple;
            progressFill.raycastTarget = false;
            progressFill.enabled = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || treeUI == null)
            return;
        treeUI.SelectSlot(nodeSlot);
        if (!treeUI.CanHoldPurchase(nodeSlot))
            return;
        holding = true;
        elapsed = 0f;
        pressedBranch = treeUI.BranchIndex;
        UpdateFill();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
#if UNITY_EDITOR
        if (qaHolding) return; // EditorWindow virtual input has no physical UI hover.
#endif
        Cancel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
#if UNITY_EDITOR
        if (qaHolding) return;
#endif
        Cancel();
    }

    private void OnDisable() => Cancel();

#if UNITY_EDITOR
    /// <summary>
    /// QA-only virtual press: uses OnPointerDown and the same purchase logic as a real user.
    /// The EditorWindow supplies monotonic elapsed time so Editor focus does not abort a hold.
    /// Neither this function nor QaTick purchases a skill directly.
    /// </summary>
    public bool QaBeginHold(PointerEventData data)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || treeUI == null ||
            data == null || data.button != PointerEventData.InputButton.Left)
            return false;
        qaHolding = true;
        OnPointerDown(data);
        if (holding) return true;
        qaHolding = false;
        return false;
    }

    public void QaTick(float elapsedSeconds)
    {
        if (!qaHolding || !holding) return;
        TickHold(Mathf.Max(0f, elapsedSeconds));
    }

    public void QaReleaseHold()
    {
        qaHolding = false;
        Cancel();
    }
#endif

    private void Update()
    {
#if UNITY_EDITOR
        if (qaHolding) return; // The test harness drives the hold with EditorApplication time.
#endif
        TickHold(Time.unscaledDeltaTime);
    }

    private void TickHold(float deltaSeconds)
    {
        if (!holding) return;
        if (treeUI == null || treeUI.BranchIndex != pressedBranch ||
            !treeUI.CanHoldPurchase(nodeSlot))
        {
            Cancel();
            return;
        }

        elapsed += deltaSeconds;
        UpdateFill();
        if (elapsed < holdDuration) return;

        // Stop BEFORE purchasing so refreshes caused by wallet and progression
        // updates cannot accidentally buy more than one level for this press.
        holding = false;
        elapsed = 0f;
        UpdateFill();
        treeUI.PurchaseHeldNode(nodeSlot);
    }

    private void UpdateFill()
    {
        if (progressFill != null)
            {
                progressFill.enabled = holding;
                if (holding)
                {
                    float fraction = Mathf.Clamp01(elapsed / holdDuration);
                    RectTransform rect = progressFill.rectTransform;
                    rect.localScale = new Vector3(fraction, 1f, 1f);
                    rect.anchoredPosition = new Vector2(-rect.rect.width * (1f - fraction) * 0.5f, rect.anchoredPosition.y);
                }
            }
    }

    private void Cancel()
    {
#if UNITY_EDITOR
        qaHolding = false;
#endif
        holding = false;
        elapsed = 0f;
        UpdateFill();
    }
}
