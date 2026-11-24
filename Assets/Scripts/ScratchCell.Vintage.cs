using UnityEngine;
using UnityEngine.UI;

public sealed partial class ScratchCell
{
    public void ApplyVintageVisuals(ScratchVintageTicketArt art, int index, float scale)
    {
        var rect = (RectTransform)transform;
        rect.localScale = Vector3.one;
        rect.sizeDelta = art.cellSize * scale;
        Vector2 p = art.cellCenters[index] - art.size * .5f;
        rect.anchoredPosition = new Vector2(p.x, -p.y) * scale;
        var frame = GetComponent<Image>();
        if (frame != null)
        {
            frame.sprite = art.frame;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.raycastTarget = false;
            frame.color = Color.white;
        }
        if (vintageRevealedBackground != null)
        {
            vintageRevealedBackground.sprite = art.revealed;
            vintageRevealedBackground.preserveAspect = true;
            vintageRevealedBackground.raycastTarget = false;
            vintageRevealedBackground.color = Color.white;
            SetRect(vintageRevealedBackground.rectTransform, art.coverSize * scale);
            vintageRevealedBackground.transform.SetAsFirstSibling();
        }
        if (symbolImage != null)
        {
            symbolImage.preserveAspect = true;
            symbolImage.raycastTarget = false;
            SetRect(symbolImage.rectTransform, Vector2.one * (art.symbolSize * scale));
        }
        if (scratchImage != null)
        {
            SetRect(scratchImage.rectTransform, art.coverSize * scale);
            scratchImage.uvRect = new Rect(0,0,1,1);
            scratchImage.transform.SetAsLastSibling();
        }
    }

    private static void SetRect(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * .5f;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private bool IsScratchablePixel(int x, int y)
    {
        if (scratchablePixels == null || scratchTexture == null) return false;
        int n = scratchTexture.width;
        return x >= 0 && y >= 0 && x < n && y < n && scratchablePixels[y*n+x];
    }

    public bool ContainsScratchPoint(Vector2 screen, Camera camera)
    {
        RectTransform rt = scratchRect != null ? scratchRect :
            scratchImage != null ? scratchImage.rectTransform : null;
        if (rt == null || scratchTexture == null || completed ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, camera, out Vector2 p) ||
            !rt.rect.Contains(p)) return false;
        Rect r = rt.rect;
        int n = scratchTexture.width;
        int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(r.xMin,r.xMax,p.x)*n),0,n-1);
        int y = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(r.yMin,r.yMax,p.y)*n),0,n-1);
        return IsScratchablePixel(x,y);
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        => ContainsScratchPoint(sp,eventCamera);
}
