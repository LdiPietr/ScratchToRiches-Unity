using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Premium scratch and payout feedback using existing scene objects only.</summary>
[DisallowMultipleComponent]
public sealed class ScratchWinJuice : MonoBehaviour
{
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private Image ticketArtwork;
    [SerializeField] private TMP_Text matchLabel;
    [SerializeField] private TMP_Text walletLabel;
    [SerializeField] private Image[] winCoins = new Image[0];
    [SerializeField] private Image combinationBackdrop;
    [SerializeField] private Sprite neutralBadge;
    [SerializeField] private Sprite pairBadge;
    [SerializeField] private Sprite tripleBadge;
    [SerializeField] private Sprite pokerBadge;
    [SerializeField] private Image[] scratchFlakes = new Image[0];
    private float flakePulse;
    private Vector2 flakeOrigin;

    private Color cardColor, resultColor, moneyColor;
    private Vector3 resultScale, moneyScale;
    private float scratchPulse, rewardPulse, walletPulse;
    private int revealed, previousMoney;
    private bool waitingForSettlement, subscribed;

    private void Start()
    {
        if (game == null || economy == null || ticketArtwork == null || matchLabel == null || walletLabel == null)
        {
            Debug.LogWarning("ScratchWinJuice: missing scene reference.", this);
            enabled = false;
            return;
        }
        cardColor = ticketArtwork.color;
        resultColor = matchLabel.color;
        moneyColor = walletLabel.color;
        resultScale = matchLabel.rectTransform.localScale;
        moneyScale = walletLabel.rectTransform.localScale;
        revealed = game.RevealedCount;
        previousMoney = economy.Coins;
        foreach (Image coin in winCoins)
            if (coin != null) coin.gameObject.SetActive(false);
        foreach (Image flake in scratchFlakes)
            if (flake != null) flake.gameObject.SetActive(false);
        UpdateMatchBadge();
        waitingForSettlement = game.TicketActive;
        game.Changed += GameChanged;
        economy.Changed += EconomyChanged;
        subscribed = true;
    }

    private void GameChanged()
    {
        if (game.TicketActive)
        {
            waitingForSettlement = true;
            if (game.RevealedCount > revealed) scratchPulse = 1f;
            revealed = game.RevealedCount;
        }
        else if (waitingForSettlement)
        {
            revealed = 0;
            waitingForSettlement = false;
            if (game.TicketPayout > 0)
            {
                rewardPulse = 1f;
                foreach (Image coin in winCoins)
                    if (coin != null) coin.gameObject.SetActive(true);
            }
        }
        UpdateMatchBadge();
    }

    private void UpdateMatchBadge()
    {
        if (combinationBackdrop == null || game == null) return;
        switch (game.CurrentMatch.Tier)
        {
            case ScratchSymbolManager.MatchTier.Pair: combinationBackdrop.sprite = pairBadge; break;
            case ScratchSymbolManager.MatchTier.Triple: combinationBackdrop.sprite = tripleBadge; break;
            case ScratchSymbolManager.MatchTier.Poker:
            case ScratchSymbolManager.MatchTier.Five: combinationBackdrop.sprite = pokerBadge; break;
            default: combinationBackdrop.sprite = neutralBadge; break;
        }
    }

    public void OnCellRevealed(RectTransform cell)
    {
        if (!enabled || ticketArtwork == null || cell == null) return;
        Vector3 local = ticketArtwork.rectTransform.InverseTransformPoint(
            cell.TransformPoint(cell.rect.center));
        flakeOrigin = new Vector2(local.x, local.y);
        flakePulse = 1f;
        foreach (Image flake in scratchFlakes)
            if (flake != null) flake.gameObject.SetActive(true);
    }

    private void EconomyChanged()
    {
        if (economy.Coins > previousMoney) walletPulse = 1f;
        previousMoney = economy.Coins;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        scratchPulse = Mathf.MoveTowards(scratchPulse, 0f, dt / 0.28f);
        rewardPulse = Mathf.MoveTowards(rewardPulse, 0f, dt / 0.70f);
        walletPulse = Mathf.MoveTowards(walletPulse, 0f, dt / 0.50f);
        float a = Mathf.Sin(scratchPulse * Mathf.PI);
        float b = Mathf.Sin(rewardPulse * Mathf.PI);
        float c = Mathf.Sin(walletPulse * Mathf.PI);
        flakePulse = Mathf.MoveTowards(flakePulse, 0f, dt / 0.36f);
        float flakeProgress = 1f - flakePulse;
        for (int i = 0; i < scratchFlakes.Length; i++)
        {
            Image flake = scratchFlakes[i];
            if (flake == null) continue;
            if (flakePulse <= 0f)
            {
                if (flake.gameObject.activeSelf) flake.gameObject.SetActive(false);
                continue;
            }
            float angle = (i + 0.25f) * Mathf.PI * 2f / scratchFlakes.Length;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = 14f + 66f * (1f - (1f - flakeProgress) * (1f - flakeProgress));
            flake.rectTransform.anchoredPosition = flakeOrigin + direction * radius
                + Vector2.down * (22f * flakeProgress * flakeProgress);
            flake.rectTransform.localScale = Vector3.one * (0.6f + 0.36f * flakeProgress);
            flake.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flakePulse * 2.3f));
        }
        // Ten scene-authored coin sprites fly outward and fade at payout.
        float progress = 1f - rewardPulse;
        for (int i = 0; i < winCoins.Length; i++)
        {
            Image coin = winCoins[i];
            if (coin == null) continue;
            if (rewardPulse <= 0f)
            {
                if (coin.gameObject.activeSelf) coin.gameObject.SetActive(false);
                continue;
            }
            if (!coin.gameObject.activeSelf) coin.gameObject.SetActive(true);
            float angle = (i + 0.5f) * Mathf.PI * 2f / winCoins.Length;
            float travel = 55f + 280f * (1f - (1f - progress) * (1f - progress));
            coin.rectTransform.anchoredPosition =
                new Vector2(Mathf.Cos(angle) * travel, Mathf.Sin(angle) * travel + progress * 25f);
            coin.rectTransform.localScale = Vector3.one * (0.72f + 0.42f * progress);
            coin.color = new Color(1f, 1f, 1f, Mathf.Clamp01(rewardPulse * 5f));
        }
        ticketArtwork.color = Color.Lerp(cardColor, new Color(1f, 0.87f, 0.61f, cardColor.a), a * 0.25f);
        matchLabel.color = Color.Lerp(resultColor, new Color(0.27f, 0.50f, 0.23f, resultColor.a), b);
        matchLabel.rectTransform.localScale = resultScale * (1f + b * 0.20f);
        walletLabel.color = Color.Lerp(moneyColor, new Color(1f, 0.83f, 0.38f, moneyColor.a), c);
        walletLabel.rectTransform.localScale = moneyScale * (1f + c * 0.12f);
    }

    private void OnDestroy()
    {
        if (!subscribed) return;
        game.Changed -= GameChanged;
        economy.Changed -= EconomyChanged;
        ticketArtwork.color = cardColor;
        matchLabel.color = resultColor;
        matchLabel.rectTransform.localScale = resultScale;
        walletLabel.color = moneyColor;
        walletLabel.rectTransform.localScale = moneyScale;
        foreach (Image coin in winCoins)
            if (coin != null) coin.gameObject.SetActive(false);
        foreach (Image flake in scratchFlakes)
            if (flake != null) flake.gameObject.SetActive(false);
    }
}
