using UnityEngine;
using UnityEngine.UI;

public sealed partial class ScratchCell
{
    private Image feedbackFrame;
    private Color feedbackFrameRest, feedbackPaperRest;
    private Vector3 feedbackSymbolRest, feedbackJollyRest;
    private bool feedbackCaptured, feedbackMatched;
    private float revealStarted = -10f, matchStarted = -10f;
    private int feedbackRank;

    // Only already-authored frame, paper and revealed symbol participate.
    public void PlayRevealFeedback()
    {
        if (!completed) return;
        CaptureFeedbackRest();
        revealStarted = Time.unscaledTime;
    }

    public void SetMatchFeedback(bool matched, int rank, bool replay)
    {
        if (!completed || preparingTicket) return;
        if (matched)
        {
            CaptureFeedbackRest();
            if (!feedbackMatched || replay) matchStarted = Time.unscaledTime;
        }
        feedbackMatched = matched;
        feedbackRank = matched ? Mathf.Clamp(rank, 1, 4) : 0;
    }

    private void CaptureFeedbackRest()
    {
        if (feedbackCaptured) return;
        feedbackFrame = GetComponent<Image>();
        if (feedbackFrame != null) feedbackFrameRest = feedbackFrame.color;
        if (vintageRevealedBackground != null) feedbackPaperRest = vintageRevealedBackground.color;
        if (symbolImage != null) feedbackSymbolRest = symbolImage.rectTransform.localScale;
        if (indexText != null) feedbackJollyRest = indexText.rectTransform.localScale;
        feedbackCaptured = true;
    }

    public void TickRevealFeedback(float now)
    {
        if (!feedbackCaptured || !completed || preparingTicket) return;
        float reveal = Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - revealStarted) / .32f));
        float match = Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - matchStarted) /
            (.34f + .08f * feedbackRank)));
        float emphasis = feedbackMatched ? match * (.055f + .012f * feedbackRank) : 0f;
        float scale = 1f + Mathf.Max(reveal * .12f, emphasis);
        if (symbolImage != null) symbolImage.rectTransform.localScale = feedbackSymbolRest * scale;
        if (indexText != null && currentSymbolLabel == "JOLLY")
            indexText.rectTransform.localScale = feedbackJollyRest * scale;
        Color gold = new Color(1f, .78f, .34f, 1f);
        if (feedbackFrame != null)
        {
            gold.a = feedbackFrameRest.a;
            feedbackFrame.color = Color.Lerp(feedbackFrameRest, gold,
                feedbackMatched ? .46f + match * .44f : reveal * .32f);
        }
        if (vintageRevealedBackground != null)
        {
            Color paper = new Color(1f, .88f, .59f, feedbackPaperRest.a);
            vintageRevealedBackground.color = Color.Lerp(feedbackPaperRest, paper,
                feedbackMatched ? .22f + match * .28f : reveal * .19f);
        }
    }

    private void ResetRevealFeedback()
    {
        if (feedbackCaptured)
        {
            if (feedbackFrame != null) feedbackFrame.color = feedbackFrameRest;
            if (vintageRevealedBackground != null) vintageRevealedBackground.color = feedbackPaperRest;
            if (symbolImage != null) symbolImage.rectTransform.localScale = feedbackSymbolRest;
            if (indexText != null) indexText.rectTransform.localScale = feedbackJollyRest;
        }
        feedbackCaptured = feedbackMatched = false;
        feedbackRank = 0;
        revealStarted = matchStarted = -10f;
    }

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
