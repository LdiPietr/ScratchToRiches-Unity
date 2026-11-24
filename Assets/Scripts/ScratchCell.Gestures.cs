using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Gestures responsibility of ScratchCell.
public sealed partial class ScratchCell
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || completed || scratchTexture == null)
            return;
        if (eventData.button == PointerEventData.InputButton.Right ||
            (eventData.button == PointerEventData.InputButton.Left &&
             (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))))
        {
            // C06: choose a preview without scratching. CTRL+left click is
            // a fallback for WebGL browsers that intercept right mouse clicks.
            gameManager?.TryInspectCell(this);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        pointerDown = true;
        activePointerId = eventData.pointerId;
        hasPreviousPixel = false;
        EraseRoutine(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDown || completed || activePointerId != eventData.pointerId ||
            eventData.button != PointerEventData.InputButton.Left)
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
            !scratchRect.rect.Contains(local) || !ContainsScratchPoint(eventData.position, eventData.pressEventCamera))
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

    // The same charge consumption drives real SHIFT input and Editor QA.
    private bool ActivateTitanForGesture()
    {
        if (titanThisGesture || !pointerDown || completed)
            return false;
        ScratchSkillTree tree = gameManager != null
            ? gameManager.GetComponent<ScratchSkillTree>() : null;
        titanThisGesture = tree != null && tree.TryConsumeManualTitanCharge();
        return titanThisGesture;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only equivalent of holding SHIFT during a real pointer drag.
    /// Exercises actual charge consumption and buffed scratch stamps.
    /// Does not grant charges, fake winnings or complete the cell.
    /// </summary>
    public bool QaActivateTitanDuringGesture() => ActivateTitanForGesture();
#endif
}
