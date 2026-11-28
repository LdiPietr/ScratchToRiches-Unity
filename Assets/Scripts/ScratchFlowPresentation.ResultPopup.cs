using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. All popup objects and default geometry are authored in the scene.
public sealed partial class ScratchFlowPresentation
{
    private bool previewingResult;
    [Header("Scene-authored reward celebration")]
    [SerializeField] private Image rewardGoldFrame, rewardGoldRays, rewardDivider;
    [SerializeField] private Image[] rewardCoinDecor = new Image[0];
    [SerializeField] private Image[] rewardSparks = new Image[0];
    [SerializeField] private TMP_Text rewardAmountShadow;
    private int rewardVisualRank;
    private bool rewardShowCoins;

    private void SetRewardStyle(int rank, int payout)
    {
        rewardVisualRank = Mathf.Clamp(rank, 0, 4);
        rewardShowCoins = rank > 0 && payout < 10000;
        UpdateRewardDecoration(1f);
    }

    private static void RewardTint(Image image, float alpha)
    {
        if (image == null) return;
        image.gameObject.SetActive(alpha > .001f);
        Color tint = image.color;
        tint.a = Mathf.Clamp01(alpha);
        image.color = tint;
    }

    private void UpdateRewardDecoration(float entrance)
    {
        int rank = rewardVisualRank;
        float strength = rank == 0 ? 0f : rank == 1 ? .22f : rank == 2 ? .45f : rank == 3 ? .75f : 1f;
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entrance));
        float pulse = 1f + (.12f + rank * .09f) * Mathf.Sin(Mathf.PI * appear);
        RewardTint(rewardGoldFrame, strength * appear * pulse);
        RewardTint(rewardGoldRays, strength * .85f * appear * pulse);
        if (rewardGoldRays != null)
            rewardGoldRays.rectTransform.localScale = Vector3.one * (.78f + .22f * appear + .04f * rank);
        RewardTint(rewardDivider, .48f + strength * .35f);
        for (int i = 0; i < rewardCoinDecor.Length; i++)
        {
            float delay = (i % 3) * .10f;
            float coinIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((entrance - delay) / (1f - delay)));
            RewardTint(rewardCoinDecor[i], rewardShowCoins && i % 3 < Mathf.Min(3, rank)
                ? coinIn : 0f);
            if (rewardCoinDecor[i] != null)
                rewardCoinDecor[i].rectTransform.localScale = Vector3.one * (.88f + .12f * coinIn);
        }
        for (int i = 0; i < rewardSparks.Length; i++)
        {
            Image spark = rewardSparks[i];
            bool show = rank >= 2 && i < (rank - 1) * 2;
            float sparkle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((entrance - (i / 2) * .10f) / (1f - (i / 2) * .10f)));
            RewardTint(spark, show ? (.65f + .08f * rank) * sparkle * pulse : 0f);
            if (spark != null)
                spark.rectTransform.localScale = Vector3.one * (.65f + .35f * sparkle + .10f * rank * Mathf.Sin(Mathf.PI * sparkle));
        }
    }

    private void ResetRewardDecoration()
    {
        rewardVisualRank = 0;
        rewardShowCoins = false;
        UpdateRewardDecoration(0f);
    }

    private void RefreshResultActions()
    {
        if (previewingResult || !ShowingResult || game.TicketActive ||
            (HasResultPopup && !ResultPopupVisible)) return;
        var kind = game.CurrentTicketKind;
        bool available = !game.DemoFinished;
        if (HasResultPopup)
        {
            var match = game.CurrentMatch;
            var sprites = new List<Sprite>(5);
            if (!game.TicketVoided && game.TicketPayout > 0 && match.IsMatch)
            {
                var cells = game.GetComponent<ScratchCellManager>();
                if (cells != null && cells.ActiveCells != null)
                    foreach (ScratchCell cell in cells.ActiveCells)
                    {
                        if (sprites.Count >= match.MatchingCount || sprites.Count >= 5) break;
                        if (cell == null || !cell.IsComplete || !game.IsCellSelected(cell) ||
                            !cell.TryGetVisibleSymbol(out string symbol) ||
                            (symbol != match.Symbol && symbol != "JOLLY")) continue;
                        Sprite sprite = symbol == "JOLLY" ? resultJollySprite : cell.RevealedArtwork;
                        if (sprite != null) sprites.Add(sprite);
                    }
            }
            ApplyPopupOutcome(kind, match.Tier, game.TicketPayout, game.TicketVoided, sprites);
        }
        RefreshResultButtons(kind, available, game.CanChooseTicket(kind),
            resultSkills != null ? resultSkills.PurchasableCount : 0);
    }

    private void RefreshResultButtons(ScratchGameManager.TicketKind kind, bool available, bool affordable, int count)
    {
        nextTicketLabel.text = available ? "CAMBIA BIGLIETTO" : "VEDI FINALE";
        if (HasResultPopup)
            ((RectTransform)nextTicketButton.transform).anchoredPosition = available
                ? nextTicketHome : new Vector2(246f, -424f);
        nextTicketButton.gameObject.SetActive(true);
        nextTicketButton.interactable = true;
        if (repeatTicketButton != null && repeatTicketLabel != null)
        {
            int cost = ScratchGameManager.TicketPrice(kind);
            repeatTicketLabel.text = "RIACQUISTA " + ScratchGameManager.TicketDisplayName(kind) +
                "\n<size=24>" + (cost == 0 ? "GRATIS" : cost.ToString("N0") + " MONETE") +
                (affordable ? "" : " · SALDO INSUFFICIENTE") + "</size>";
            // A disabled gold surface is darkened by the authored Button ColorBlock.
            // Keep its explanation legible rather than dimming the dark active label.
            repeatTicketLabel.color = affordable
                ? new Color32(32, 51, 46, 255) : new Color32(249, 239, 216, 255);
            repeatTicketButton.gameObject.SetActive(available);
            repeatTicketButton.interactable = available && affordable;
        }
        if (resultUpgradesButton != null && resultUpgradesLabel != null)
        {
            resultUpgradesLabel.text = resultUpgradeCount != null ? "POTENZIAMENTI" :
                "POTENZIAMENTI\n<size=24>" + (count == 1 ? "1 ACQUISTABILE" : count + " ACQUISTABILI") + "</size>";
            if (resultUpgradeCount != null) resultUpgradeCount.text = count.ToString();
            if (resultUpgradeBadge != null) resultUpgradeBadge.SetActive(available && count > 0);
            resultUpgradesButton.gameObject.SetActive(available);
            resultUpgradesButton.interactable = available;
        }
    }

    public static string PopupOutcome(ScratchSymbolManager.MatchTier tier, int payout, bool voided)
    {
        if (voided) return "BIGLIETTO DI PROVA";
        if (payout <= 0) return "NESSUNA VINCITA";
        switch (tier)
        {
            case ScratchSymbolManager.MatchTier.Pair: return "COPPIA";
            case ScratchSymbolManager.MatchTier.Triple: return "TRIS";
            case ScratchSymbolManager.MatchTier.Poker: return "POKER";
            case ScratchSymbolManager.MatchTier.Five: return "CINQUINA";
            default: return "RIMBORSO";
        }
    }

    public static string PopupNet(int payout, int cost, bool voided)
    {
        if (voided || cost == 0) return "";
        long net = (long)payout - cost;
        string value = (net > 0 ? "+" : "") + net.ToString("N0");
        string color = net > 0 ? "#EAC25C" : net < 0 ? "#D5A29A" : "#B9C9BE";
        return "Bilancio netto: <color=" + color + ">" + value + "</color> monete";
    }

    private void ApplyPopupOutcome(ScratchGameManager.TicketKind kind, ScratchSymbolManager.MatchTier tier,
        int payout, bool voided, IList<Sprite> sprites)
    {
        int cost = ScratchGameManager.TicketPrice(kind);
        bool winning = !voided && payout > 0 && tier != ScratchSymbolManager.MatchTier.None;
        bool refund = !voided && payout > 0 && tier == ScratchSymbolManager.MatchTier.None;
        Color accent = winning && payout > cost ? new Color32(234,194,92,255) : refund ? new Color32(238,221,178,255) : new Color32(192,205,195,255);
        int rank = winning ? Mathf.Clamp((int)tier, 1, 4) : 0;
        SetRewardStyle(rank, payout);
        float titleMax = rank == 0 ? 38f : 38f + (rank - 1) * 4f;
        float amountMax = rank == 0 ? 92f : 100f + (rank - 1) * 13f;
        if (payout >= 100000000) amountMax = Mathf.Min(amountMax, 76f);
        if (payout >= 1000000000) amountMax = Mathf.Min(amountMax, 64f);
        if (resultPopupHeading != null)
            resultPopupHeading.text = "BIGLIETTO " + ScratchGameManager.TicketDisplayName(kind);
        if (resultOutcomeLabel != null)
        {
            resultOutcomeLabel.text = refund && payout < cost ? "RIMBORSO PARZIALE" : PopupOutcome(tier, payout, voided);
            resultOutcomeLabel.color = accent;
            resultOutcomeLabel.fontSizeMax = titleMax;
            SetPopupY(resultOutcomeLabel.rectTransform, winning ? -83f : -137f);
        }
        if (resultPopupAmount != null)
        {
            resultPopupAmount.text = (payout > 0 ? "+" : "") + payout.ToString("N0");
            // White base avoids multiplying the gold vertex gradient twice.
            resultPopupAmount.color = winning ? Color.white : accent;
            resultPopupAmount.enableVertexGradient = winning;
            if (winning)
                resultPopupAmount.colorGradient = new VertexGradient(
                    new Color32(255, 249, 210, 255), new Color32(255, 249, 210, 255),
                    new Color32(244, (byte)(205 - rank * 6), 60, 255), new Color32(244, (byte)(205 - rank * 6), 60, 255));
            resultPopupAmount.fontSizeMax = amountMax;
            SetPopupY(resultPopupAmount.rectTransform, winning ? -214f : -186f);
            if (rewardAmountShadow != null)
            {
                rewardAmountShadow.gameObject.SetActive(winning);
                rewardAmountShadow.text = resultPopupAmount.text;
                rewardAmountShadow.fontSizeMax = amountMax;
                SetPopupY(rewardAmountShadow.rectTransform, -218f);
            }
        }
        if (resultPopupUnit != null)
        {
            resultPopupUnit.text = "MONETE";
            resultPopupUnit.color = accent;
            SetPopupY(resultPopupUnit.rectTransform, winning ? -338f : -305f);
        }
        if (resultPopupNet != null) resultPopupNet.text = PopupNet(payout, cost, voided);
        if (resultMatchIcons == null) return;
        int count = winning && sprites != null ? Mathf.Min(resultMatchIcons.Length, sprites.Count) : 0;
        for (int i = 0; i < resultMatchIcons.Length; i++)
        {
            Image icon = resultMatchIcons[i];
            if (icon == null) continue;
            bool show = i < count && sprites[i] != null;
            icon.gameObject.SetActive(show);
            if (!show) continue;
            icon.sprite = sprites[i];
            icon.rectTransform.anchoredPosition =
                new Vector2(420f - (count * 84f + (count - 1) * 10f) * .5f + i * 94f, -134f);
        }
    }

    private static void SetPopupY(RectTransform rect, float y)
    {
        Vector2 position = rect.anchoredPosition;
        if (position.y != y) rect.anchoredPosition = new Vector2(position.x, y);
    }

#if UNITY_EDITOR
    // The visual fixture reuses the real renderer, but does not mutate tickets,
    // skills, wallet or save data. Inputs cannot acknowledge a synthetic result.
    public bool PreviewPopup(ScratchGameManager.TicketKind kind, ScratchSymbolManager.MatchTier tier,
        int payout, bool voided, IList<Sprite> sprites, bool affordable, int upgrades)
    {
        if (!Application.isPlaying || !HasResultPopup || game.TicketActive ||
            (!previewingResult && (!CanShowTicketChoices || !game.WaitingForTicketChoice))) return false;
        previewingResult = true;
        phase = Phase.TicketResult;
        ticketCanvasGroup.alpha = 0f;
        ticketCanvasGroup.blocksRaycasts = false;
        resultPopup.SetActive(true);
        if (resultPopupPanel != null) resultPopupPanel.gameObject.SetActive(true);
        SetResultDimmer(.55f);
        ApplyPopupOutcome(kind, tier, payout, voided, sprites);
        ApplyRewardEntrance(1f);
        RefreshResultButtons(kind, true, affordable, upgrades);
        ticketChooser.Refresh();
        return true;
    }

    public void EndPopupPreview()
    {
        if (!previewingResult) return;
        previewingResult = false;
        HideResultActions();
        phase = Phase.Choosing;
        ticketChooser.Refresh();
    }
#endif
}
