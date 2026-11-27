using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Mask responsibility of ScratchCell.
public sealed partial class ScratchCell
{
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

        int size = scratchTexture.width;
        int pixelX = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x) * size), 0, size - 1);
        int pixelY = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(rect.yMin, rect.yMax, point.y) * size), 0, size - 1);
        if (!IsScratchablePixel(pixelX, pixelY))
        {
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
            ActivateTitanForGesture(new Vector2((pixelX + .5f) / size, (pixelY + .5f) / size));

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
        Vector2? main = null, left = null, right = null, fanLeft = null, fanRight = null;
        int additional = manager.AdditionalScratchStripes +
            (titanThisGesture ? 3 : 0);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.x, current.x, t)) : current.x;
            int y = hasPreviousPixel ? Mathf.RoundToInt(Mathf.Lerp(previousPixel.y, current.y, t)) : current.y;
            changed |= EraseStamp(x, y, radius, ref main);

            // Skill tree double/multi-pass: parallel strokes, clamped inside mask.
            for (int stripe = 1; stripe <= additional; stripe++)
            {
                int offset = 12 * ((stripe + 1) / 2);
                if ((stripe & 1) == 0) offset = -offset;
                if (offset < 0) changed |= EraseStamp(x + offset, y, radius, ref left);
                else changed |= EraseStamp(x + offset, y, radius, ref right);
            }
            if (manager.FanModeEnabled)
            {
                // G04: two converging manual strips around the main track.
                int fanOffset = Mathf.RoundToInt(18f * (1f - t));
                changed |= EraseStamp(x - fanOffset, y, radius, ref fanLeft);
                changed |= EraseStamp(x + fanOffset, y, radius, ref fanRight);
            }
        }

        previousPixel = current;
        hasPreviousPixel = true;
        if (!changed)
            return;

        scratchTexture.Apply(false);
        // Only actual removed foil contributes to the cosmetic emission origins.
        if (gameManager != null)
        {
            var feedback = gameManager.GetComponent<ScratchWinJuice>();
            if (feedback != null)
                feedback.OnScratchMaterialRemoved(scratchRect, main, left, right, fanLeft, fanRight,
                    radius / (float)Mathf.Max(1, manager.BrushRadius), titanThisGesture);
        }
        if (100f * erasedPixelCount / Mathf.Max(1, scratchablePixelCount) >= manager.ScratchPercentage)
            Complete();
    }

    private bool EraseStamp(int centerX, int centerY, int radius, ref Vector2? foil)
    {
        int resolution = scratchTexture.width;
        // Keep an approximately circular brush on screen and equal fractional
        // removal per stamp on all silhouettes; retain the existing skill radius.
        float aspect = scratchRect.rect.width / Mathf.Max(1f, scratchRect.rect.height);
        float area = Mathf.Sqrt((float)scratchablePixelCount / (resolution * resolution));
        float rx = Mathf.Max(1f, radius * area / Mathf.Sqrt(aspect));
        float ry = Mathf.Max(1f, radius * area * Mathf.Sqrt(aspect));
        int extentX = Mathf.CeilToInt(rx), extentY = Mathf.CeilToInt(ry);
        Color32 clear = new Color32(0, 0, 0, 0);
        bool changed = false;
        int sumX = 0, sumY = 0, count = 0;

        for (int y = Mathf.Max(0, centerY - extentY); y <= Mathf.Min(resolution - 1, centerY + extentY); y++)
        {
            int dy = y - centerY;
            for (int x = Mathf.Max(0, centerX - extentX); x <= Mathf.Min(resolution - 1, centerX + extentX); x++)
            {
                int dx = x - centerX;
                if (dx * dx / (rx * rx) + dy * dy / (ry * ry) > 1f)
                    continue;

                int index = y * resolution + x;
                if (!scratchablePixels[index] || erasedPixels[index])
                    continue;

                erasedPixels[index] = true;
                erasedPixelCount++;
                sumX += x; sumY += y; count++;
                scratchTexture.SetPixel(x, y, clear);
                changed = true;
            }
        }

        if (count > 0)
            foil = new Vector2((sumX / (float)count + .5f) / resolution,
                (sumY / (float)count + .5f) / resolution);
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

        PlayRevealFeedback();
        if (gameManager != null)
        {
            // Optional physical-print feedback on the real scratched cell.
            ScratchWinJuice feedback = gameManager.GetComponent<ScratchWinJuice>();
            if (feedback != null) feedback.OnCellRevealed(scratchRect, titanThisGesture);
            gameManager.OnCellCompleted(this);
        }
    }
}
