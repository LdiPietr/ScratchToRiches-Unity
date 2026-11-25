using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ScratchVintageGameplayHUD
{
    private readonly List<ScratchCell> displayedCells = new List<ScratchCell>(5);
    private readonly bool[] slotWinning = new bool[5];
    private readonly float[] slotRevealAt = new float[5];
    private int artworkTicket = -1, jollySlot = -1;
    private static readonly Color EmptyFrame = new Color(.57f, .69f, .62f, .92f);
    private static readonly Color NeutralFrame = new Color(.47f, .58f, .52f, .78f);
    private static readonly Color WinningFrame = new Color(.92f, .76f, .36f, 1f);

    // Stable reveal order; no previews or inspection data may populate the HUD.
    private int RefreshMatchSymbols(ScratchSymbolManager.MatchResult match, bool winning,
        bool voided, out bool hasJolly)
    {
        hasJolly = false;
        int matching = 0;
        if (matchSymbols == null) return 0;
        if (artworkTicket != game.TicketNumber || game.RevealedCount == 0)
        {
            artworkTicket = game.TicketNumber;
            displayedCells.Clear();
            for (int i = 0; i < slotRevealAt.Length; i++) slotRevealAt[i] = -10f;
        }
        if (!voided && matchCells != null && matchCells.ActiveCells != null)
        {
            foreach (ScratchCell cell in matchCells.ActiveCells)
            {
                if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                    !cell.TryGetVisibleSymbol(out _) || displayedCells.Contains(cell) ||
                    displayedCells.Count >= matchSymbols.Length) continue;
                slotRevealAt[displayedCells.Count] = Time.unscaledTime;
                displayedCells.Add(cell);
            }
        }
        // A completed ticket keeps its original width even if the player
        // purchases an extra pick before choosing the next ticket.
        int total = voided ? 0 : game.TicketActive
            ? Mathf.Clamp(game.MaxScratches, 0, matchSymbols.Length)
            : game.TicketNumber > 0 ? displayedCells.Count : 0;
        int discovered = voided ? 0 : Mathf.Min(total, displayedCells.Count);
        const float slotWidth = 70f, gap = 8f;
        float start = 221f - (total * slotWidth + Mathf.Max(0, total - 1) * gap) * .5f;
        jollySlot = -1;
        if (matchJolly != null) matchJolly.gameObject.SetActive(false);
        for (int i = 0; i < matchSymbols.Length; i++)
        {
            Vector2 position = new Vector2(start + i * (slotWidth + gap), -137f);
            Image symbolImage = matchSymbols[i];
            if (symbolImage != null) symbolImage.gameObject.SetActive(false);
            slotWinning[i] = false;
            bool revealed = i < discovered;
            if (revealed && displayedCells[i].TryGetVisibleSymbol(out string symbol))
            {
                bool jolly = symbol == "JOLLY";
                slotWinning[i] = winning && (symbol == match.Symbol || jolly);
                if (slotWinning[i] && !jolly) matching++;
                Image artwork = jolly ? matchJolly : symbolImage;
                if (jolly) { jollySlot = i; hasJolly = true; }
                else if (artwork != null) artwork.sprite = displayedCells[i].RevealedArtwork;
                if (artwork != null && artwork.sprite != null)
                {
                    FitMatchArtwork(artwork, position);
                    artwork.color = new Color(1f, 1f, 1f, winning && !slotWinning[i] ? .78f : 1f);
                    artwork.gameObject.SetActive(true);
                }
            }
            Image frame = Get(matchSlotFrames, i);
            TMP_Text question = Get(matchSlotQuestions, i);
            if (frame != null)
            {
                frame.rectTransform.anchoredPosition = position;
                frame.color = slotWinning[i] ? WinningFrame : revealed ? NeutralFrame : EmptyFrame;
                frame.gameObject.SetActive(i < total);
            }
            if (question != null)
            {
                question.rectTransform.anchoredPosition = position;
                question.color = Cream;
                question.gameObject.SetActive(i < total && !revealed && game.TicketActive);
            }
        }
        return matching;
    }

    // Animate only authored artwork; optical centering is retained during pop.
    private void TickMatchSlotFeedback()
    {
        if (matchSymbols == null) return;
        float now = Time.unscaledTime;

        for (int i = 0; i < matchSymbols.Length; i++)
        {
            Image frame = Get(matchSlotFrames, i);
            if (frame == null || !frame.gameObject.activeSelf) continue;
            float reveal = Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - slotRevealAt[i]) / .15f));
            Image artwork = i == jollySlot ? matchJolly : matchSymbols[i];
            bool visible = artwork != null && artwork.gameObject.activeSelf;
            Color rest = slotWinning[i] ? WinningFrame : visible ? NeutralFrame : EmptyFrame;
            int rank = (int)feedbackTier;
            float winPulse = MatchWinPulse(rank >= 3 ? i * .025f : 0f);
            frame.color = Color.Lerp(rest, Cream, slotWinning[i] ? winPulse * (.40f + .06f * rank) : reveal * .25f);
            if (visible)
                FitMatchArtwork(artwork, frame.rectTransform.anchoredPosition,
                    1f + Mathf.Max(reveal * .085f, slotWinning[i] ? winPulse * (.035f + .013f * (int)feedbackTier) : 0f));
        }
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

    // Per-symbol optical sizes compensate for silhouette weight, not PNG size.
    private static float MatchOpticalSize(Sprite sprite)
    {
        switch (sprite.name)
        {
            case "sym_lucky7": return 52f;
            case "sym_crown": return 56f;
            case "sym_jolly_hud": return 58f;
            default: return 54f;
        }
    }

    private static void FitMatchArtwork(Image image, Vector2 slotTopLeft, float emphasis = 1f)
    {
        if (image == null || image.sprite == null) return;
        const float frameSize = 70f;
        float visibleSize = MatchOpticalSize(image.sprite);
        Rect alpha = MatchArtworkBounds(image.sprite);
        Vector2 source = image.sprite.rect.size;
        float factor = visibleSize * emphasis / Mathf.Max(source.x * alpha.width, source.y * alpha.height);
        Vector2 size = source * factor;
        RectTransform rt = image.rectTransform;
        rt.sizeDelta = size;
        rt.anchoredPosition = slotTopLeft + new Vector2(frameSize * .5f - alpha.center.x * size.x,
            -frameSize * .5f + alpha.center.y * size.y);
        image.preserveAspect = true;
    }
}
