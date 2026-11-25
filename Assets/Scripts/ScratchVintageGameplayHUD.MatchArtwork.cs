using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ScratchVintageGameplayHUD
{
    // Only completed, selected cells populate the authored slots. No previews leak.
    private int RefreshMatchSymbols(ScratchSymbolManager.MatchResult match, bool winning,
        bool voided, bool hasAmount, out bool hasJolly)
    {
        hasJolly = false;
        int count = 0;
        if (matchSymbols == null) return 0;
        for (int i = 0; i < matchSymbols.Length; i++)
            if (matchSymbols[i] != null) matchSymbols[i].gameObject.SetActive(false);
        if (matchJolly != null) matchJolly.gameObject.SetActive(false);
        if (!voided && matchCells != null && matchCells.ActiveCells != null)
        {
            foreach (ScratchCell cell in matchCells.ActiveCells)
            {
                if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                    !cell.TryGetVisibleSymbol(out string symbol)) continue;
                if (winning && symbol != match.Symbol && symbol != "JOLLY") continue;
                if (symbol == "JOLLY") { hasJolly = true; continue; }
                Sprite artwork = cell.RevealedArtwork;
                if (artwork == null || count >= matchSymbols.Length) continue;
                Image slot = matchSymbols[count++];
                if (slot == null) continue;
                slot.sprite = artwork;
                slot.color = Color.white;
                slot.gameObject.SetActive(true);
            }
        }
        int discovered = Mathf.Min(matchSymbols.Length, count + (hasJolly ? 1 : 0));
        int total = !voided && game.TicketActive && !winning
            ? Mathf.Clamp(game.MaxScratches, discovered, matchSymbols.Length) : discovered;
        const float slotWidth = 70f, gap = 8f;
        float start = 221f - (total * slotWidth + Mathf.Max(0, total - 1) * gap) * .5f;
        float y = hasAmount ? -137f : -208f;
        for (int i = 0; i < matchSymbols.Length; i++)
        {
            Vector2 position = new Vector2(start + i * (slotWidth + gap), y);
            if (i < count && matchSymbols[i] != null)
                FitMatchArtwork(matchSymbols[i], position);
            Image frame = Get(matchSlotFrames, i);
            TMP_Text question = Get(matchSlotQuestions, i);
            bool empty = i >= discovered && i < total;
            if (frame != null)
            {
                frame.rectTransform.anchoredPosition = position;
                frame.color = empty ? new Color(.47f, .58f, .52f, .85f) :
                    winning ? new Color(.92f, .76f, .36f, .83f) : new Color(.47f, .58f, .52f, .65f);
                frame.gameObject.SetActive(i < total);
            }
            if (question != null)
            {
                question.rectTransform.anchoredPosition = position;
                question.color = LockedInk;
                question.gameObject.SetActive(empty);
            }
        }
        if (hasJolly && count < discovered && matchJolly != null)
        {
            FitMatchArtwork(matchJolly, new Vector2(start + count * (slotWidth + gap), y));
            matchJolly.color = Color.white;
            matchJolly.gameObject.SetActive(true);
        }
        return count;
    }


    // Source PNG alpha bounds (alpha >= 40/255), measured offline at 512 x 512.
    // Rectangles are normalized; retain aspect and center actual visible artwork.
    // Only authored RectTransforms are adjusted. Source ticket artwork is untouched.
    private static Rect MatchArtworkBounds(Sprite sprite)
    {
        switch (sprite.name)
        {
            case "sym_lucky7": return new Rect(80f / 512, 72f / 512, 368f / 512, 388f / 512);
            case "sym_coin": return new Rect(57f / 512, 49f / 512, 398f / 512, 422f / 512);
            case "sym_diamond": return new Rect(32f / 512, 76f / 512, 448f / 512, 384f / 512);
            case "sym_star": return new Rect(28f / 512, 32f / 512, 456f / 512, 436f / 512);
            case "sym_crown": return new Rect(35f / 512, 47f / 512, 442f / 512, 372f / 512);
            case "sym_clover": return new Rect(38f / 512, 34f / 512, 436f / 512, 440f / 512);
            case "sym_coinbag": return new Rect(48f / 512, 48f / 512, 416f / 512, 428f / 512);
            case "sym_jolly_hud": return new Rect(4f / 128, 5f / 128, 124f / 128, 111f / 128);
            default: return new Rect(0f, 0f, 1f, 1f);
        }
    }

    private static void FitMatchArtwork(Image image, Vector2 slotTopLeft)
    {
        if (image == null || image.sprite == null) return;
        const float frameSize = 70f, visibleSize = 54f;
        Rect alpha = MatchArtworkBounds(image.sprite);
        Vector2 source = image.sprite.rect.size;
        float factor = visibleSize / Mathf.Max(source.x * alpha.width, source.y * alpha.height);
        Vector2 size = source * factor;
        RectTransform rt = image.rectTransform;
        rt.sizeDelta = size;
        rt.anchoredPosition = slotTopLeft + new Vector2(frameSize * .5f - alpha.center.x * size.x,
            -frameSize * .5f + alpha.center.y * size.y);
        image.preserveAspect = true;
    }
}
