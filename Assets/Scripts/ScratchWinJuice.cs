using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Premium scratch and payout feedback using existing scene objects only.</summary>
[DisallowMultipleComponent]
public sealed partial class ScratchWinJuice : MonoBehaviour
{
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private Image ticketArtwork;
    [SerializeField] private TMP_Text matchLabel;
    [SerializeField] private TMP_Text walletLabel;
    [SerializeField] private Image walletCoin;
    [SerializeField] private Image[] winCoins = new Image[0];
    [SerializeField] private Image combinationBackdrop;
    [SerializeField] private Sprite neutralBadge;
    [SerializeField] private Sprite pairBadge;
    [SerializeField] private Sprite tripleBadge;
    [SerializeField] private Sprite pokerBadge;
    [SerializeField] private Image[] scratchFlakes = new Image[0];

    private Color cardColor, resultColor, moneyColor;
    private Vector3 resultScale, moneyScale;
    private float scratchPulse, rewardPulse, walletPulse;
    private int revealed, previousMoney;
    private int feedbackTicket = -1;
    private ScratchSymbolManager.MatchTier feedbackTier;
    private string feedbackSymbol = string.Empty;
    private ScratchCellManager cells;
    private ScratchVintageGameplayHUD vintageHud;
    private int rewardRank, visibleCoinCount;
    private float rewardDuration = .7f;
    private float rewardStarted, coinFlightDuration;
    private const float CoinSpacing = .075f;
    private ScratchFlowPresentation flow;
    private bool waitingForSettlement, subscribed;
    public bool PayoutAnimating => rewardPulse > 0f;

    private void Start()
    {
        if (game == null || economy == null || ticketArtwork == null || matchLabel == null || walletLabel == null)
        {
            Debug.LogWarning("ScratchWinJuice: missing scene reference.", this);
            enabled = false;
            return;
        }
        cells = game.GetComponent<ScratchCellManager>();
        vintageHud = game.GetComponent<ScratchVintageGameplayHUD>();
        flow = game.GetComponent<ScratchFlowPresentation>();
        cardColor = ticketArtwork.color;
        resultColor = matchLabel.color;
        moneyColor = walletLabel.color;
        resultScale = matchLabel.rectTransform.localScale;
        moneyScale = walletLabel.rectTransform.localScale;
        revealed = game.RevealedCount;
        previousMoney = economy.Coins;
        foreach (Image coin in winCoins)
            if (coin != null) coin.gameObject.SetActive(false);
        InitializeFlakes();
        UpdateMatchBadge();
        waitingForSettlement = game.TicketActive;
        game.Changed += GameChanged;
        economy.Changed += EconomyChanged;
        subscribed = true;
    }

    private void GameChanged()
    {
        if (feedbackTicket != game.TicketNumber)
        {
            feedbackTicket = game.TicketNumber;
            feedbackTier = default;
            feedbackSymbol = string.Empty;
            rewardPulse = 0f;
            ClearFlakes();
        }
        var match = game.CurrentMatch;
        bool validMatch = !game.TicketVoided && match.IsMatch;
        bool stronger = validMatch && (int)match.Tier > (int)feedbackTier;
        bool changedSymbol = validMatch && match.Symbol != feedbackSymbol;
        bool settlement = !game.TicketActive && waitingForSettlement;
        if (cells != null && cells.ActiveCells != null)
        {
            foreach (ScratchCell cell in cells.ActiveCells)
            {
                if (cell == null || !cell.IsComplete) continue;
                bool winner = validMatch && cell.TryGetVisibleSymbol(out string symbol) &&
                    (symbol == match.Symbol || symbol == "JOLLY");
                cell.SetMatchFeedback(winner, (int)match.Tier, stronger || changedSymbol || settlement);
            }
        }
        feedbackTier = validMatch ? match.Tier : default;
        feedbackSymbol = validMatch ? match.Symbol : string.Empty;
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
                rewardRank = validMatch ? (int)match.Tier : 0;
                rewardStarted = Time.unscaledTime;
                coinFlightDuration = .48f + .035f * rewardRank;
                visibleCoinCount = 0;
                int limit = 2 + rewardRank * 2;
                foreach (Image coin in winCoins)
                    if (coin != null && visibleCoinCount < limit) visibleCoinCount++;
                rewardDuration = coinFlightDuration + Mathf.Max(0, visibleCoinCount - 1) * CoinSpacing;
                // Start after the real settlement, never from a speculative quote.
                if (vintageHud != null)
                    vintageHud.BeginPayoutPresentation(game.TicketPayout, visibleCoinCount,
                        rewardStarted, coinFlightDuration, CoinSpacing);
                int ordinal = 0;
                foreach (Image coin in winCoins)
                    if (coin != null)
                    {
                        coin.gameObject.SetActive(ordinal++ < visibleCoinCount);
                        coin.raycastTarget = false;
                        coin.color = new Color(1f, 1f, 1f, 0f);
                    }
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

    private void EconomyChanged()
    {
        if (economy.Coins > previousMoney) walletPulse = 1f;
        previousMoney = economy.Coins;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        scratchPulse = Mathf.MoveTowards(scratchPulse, 0f, dt / 0.28f);
        if (rewardPulse > 0f)
            rewardPulse = flow != null && !flow.ShowingResult ? 0f :
                1f - Mathf.Clamp01((Time.unscaledTime - rewardStarted) / rewardDuration);
        if (cells != null && cells.ActiveCells != null)
            foreach (ScratchCell cell in cells.ActiveCells)
                if (cell != null) cell.TickRevealFeedback(Time.unscaledTime);
        walletPulse = Mathf.MoveTowards(walletPulse, 0f, dt / 0.50f);
        float a = Mathf.Sin(scratchPulse * Mathf.PI);
        float b = Mathf.Sin(rewardPulse * Mathf.PI);
        float c = Mathf.Sin(walletPulse * Mathf.PI);
        TickFlakes(Time.unscaledTime);
        // Existing authored coins travel from the ticket toward the actual HUD
        // balance. No spawned objects, extra payout or changes to economy.
        float elapsed = Time.unscaledTime - rewardStarted;
        int coinOrdinal = 0;
        // The scene-authored coin is the exact arrival point, not the balance text.
        RectTransform target = walletCoin != null ? walletCoin.rectTransform :
            vintageHud != null ? vintageHud.WalletFeedbackTarget : null;
        if (target == null) target = walletLabel.rectTransform;
        for (int i = 0; i < winCoins.Length; i++)
        {
            Image coin = winCoins[i];
            if (coin == null) continue;
            int ordinal = coinOrdinal++;
            if (rewardPulse <= 0f || ordinal >= visibleCoinCount)
            {
                if (coin.gameObject.activeSelf) coin.gameObject.SetActive(false);
                continue;
            }
            RectTransform rt = coin.rectTransform;
            Transform parent = rt.parent;
            float p = Mathf.Clamp01((elapsed - ordinal * CoinSpacing) / coinFlightDuration);
            Vector3 origin = parent.InverseTransformPoint(ticketArtwork.rectTransform.TransformPoint(
                ticketArtwork.rectTransform.rect.center));
            Vector3 destination = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            float fan = (ordinal - (visibleCoinCount - 1) * .5f) * 23f;
            Vector3 control = Vector3.Lerp(origin, destination, .42f) +
                new Vector3(fan, 110f + Mathf.Abs(fan) * .3f, 0f);
            float ease = p * p * (3f - 2f * p);
            rt.localPosition = Vector3.Lerp(Vector3.Lerp(origin, control, ease),
                Vector3.Lerp(control, destination, ease), ease);
            rt.localScale = Vector3.one * Mathf.Lerp(1.20f, .82f, p);
            coin.color = new Color(1f, 1f, 1f, p >= 1f ? 0f :
                Mathf.Clamp01(p * 18f) * Mathf.Clamp01((1f - p) * 15f));
        }
        ticketArtwork.color = Color.Lerp(cardColor, new Color(1f, 0.87f, 0.61f, cardColor.a), a * 0.06f);
        // The vintage HUD owns result hierarchy and color; avoid two writers.
        if (vintageHud == null || vintageHud.MatchFeedbackLabel != matchLabel)
        {
            matchLabel.color = Color.Lerp(resultColor, new Color(1f, .81f, .39f, resultColor.a), b);
            matchLabel.rectTransform.localScale = resultScale * (1f + b * 0.20f);
        }
        // Vintage HUD owns its visible wallet's timing and color.
        if (vintageHud == null || vintageHud.WalletFeedbackTarget != walletLabel.rectTransform)
        {
            walletLabel.color = Color.Lerp(moneyColor, new Color(1f, 0.83f, 0.38f, moneyColor.a), c);
            walletLabel.rectTransform.localScale = moneyScale * (1f + c * 0.12f);
        }
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
