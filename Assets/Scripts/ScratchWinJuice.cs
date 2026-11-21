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
            if (game.TicketPayout > 0) rewardPulse = 1f;
        }
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
        ticketArtwork.color = Color.Lerp(cardColor, new Color(1f, 0.87f, 0.61f, cardColor.a), a * 0.25f);
        matchLabel.color = Color.Lerp(resultColor, new Color(1f, 0.97f, 0.49f, resultColor.a), b);
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
    }
}
