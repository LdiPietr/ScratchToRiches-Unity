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

    private void Awake()
    {
        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;
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

    public void OnPointerUp(PointerEventData eventData) => Cancel();
    public void OnPointerExit(PointerEventData eventData) => Cancel();
    private void OnDisable() => Cancel();

    private void Update()
    {
        if (!holding) return;
        if (treeUI == null || treeUI.BranchIndex != pressedBranch ||
            !treeUI.CanHoldPurchase(nodeSlot))
        {
            Cancel();
            return;
        }

        elapsed += Time.unscaledDeltaTime;
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
            progressFill.fillAmount = holding ? Mathf.Clamp01(elapsed / holdDuration) : 0f;
    }

    private void Cancel()
    {
        holding = false;
        elapsed = 0f;
        UpdateFill();
    }
}
