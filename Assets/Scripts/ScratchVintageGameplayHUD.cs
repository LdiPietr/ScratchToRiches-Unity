using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored Vintage Lottery HUD. Reads gameplay state; never changes progression.</summary>
[DisallowMultipleComponent]
public sealed class ScratchVintageGameplayHUD : MonoBehaviour
{
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchSkillTree skills;
    [SerializeField] private ScratchFlowPresentation flow;
    [SerializeField] private TMP_Text chapter, wallet, shopWallet, picks, remaining, ticketNumber, ticketType, cost, basePrize;
    [SerializeField] private GameObject shopWalletPanel;
    [SerializeField] private TMP_Text milestone, milestoneCount, matchName, matchMultiplier, prizeLabel, payout;
    [SerializeField] private TMP_Text pairRule, tripleRule, available, helpPrimary, helpSecondary, classicSerial;
    [SerializeField] private RectTransform progress, ticket;
    [SerializeField] private Image[] indicators;
    [SerializeField] private TMP_Text[] prizeNames = new TMP_Text[4];
    [SerializeField] private TMP_Text[] prizeAmounts = new TMP_Text[4];
    [SerializeField] private Image[] prizeCoins = new Image[4];
    [SerializeField] private GameObject progressTrack;
    [SerializeField] private Button startButton;
    [SerializeField] private Image shadow;
    [SerializeField] private CanvasGroup ticketGroup;
    private static readonly Color Gold = new Color32(234,194,92,255);
    private static readonly Color Cream = new Color32(249,239,216,255);
    private static readonly Color Muted = new Color32(149,168,155,255);
    private static readonly Color Dark = new Color32(32,59,51,255);
    private static readonly Color Reserved = new Color32(119,147,132,255);
    private static readonly Regex CountPattern = new Regex(@"\((\d+)/(\d+)\)");
    private bool subscribed;

    private void Start()
    {
        if (game == null || economy == null || skills == null || flow == null)
        { Debug.LogError("Vintage HUD: managers missing.", this); enabled = false; return; }
        game.Changed += Refresh;
        economy.Changed += Refresh;
        skills.Changed += Refresh;
        subscribed = true;
        Refresh();
    }

    private void OnDestroy()
    {
        if (!subscribed) return;
        game.Changed -= Refresh;
        economy.Changed -= Refresh;
        skills.Changed -= Refresh;
    }

    private void Refresh()
    {
        var kind = game.CurrentTicketKind;
        int max = game.MaxScratches, revealed = game.RevealedCount, selected = game.SelectedCount;
        int unlocked = ScratchRadialProgression.UnlockedChapter(game, skills);
        Set(chapter, "CAPITOLO " + unlocked.ToString("00"));
        Set(wallet, economy.Coins.ToString("N0"));
        Set(shopWallet, economy.Coins.ToString("N0"));
        Set(picks, revealed + "<size=64> /" + max + "</size>");
        int left = Mathf.Max(0, max - revealed);
        Set(remaining, left == 1 ? "1 grattata rimasta" : left + " grattate rimaste");
        for (int i = 0; i < indicators.Length; i++)
        {
            Image im = indicators[i];
            if (im == null) continue;
            im.gameObject.SetActive(i < max);
            if (i >= max) continue;
            im.color = i < revealed ? Gold : i < selected ? Reserved : Dark;
            const float gap = 14f;
            float width = (394f - (max - 1) * gap) / max;
            var rt = im.rectTransform;
            rt.anchoredPosition = new Vector2(24f + i * (width + gap), -204);
            rt.sizeDelta = new Vector2(width, 24);
        }
        Set(ticketNumber, game.TicketNumber == 0 ? "BIGLIETTO —" : "BIGLIETTO " + game.TicketNumber.ToString("000"));
        string name = ScratchGameManager.TicketDisplayName(kind);
        Set(ticketType, name.Substring(0, 1) + name.Substring(1).ToLowerInvariant());
        // Cost and base prize remain internal economy parameters, not gameplay HUD labels.
        if (classicSerial != null)
        {
            classicSerial.gameObject.SetActive(kind == ScratchGameManager.TicketKind.Standard);
            Set(classicSerial, game.TicketNumber == 0 ? "" : game.TicketNumber.ToString("000000"));
        }

        RefreshGoal();
        RefreshMatch();
        int buyable = 0;
        foreach (var entry in skills.Catalog)
            if (skills.CanPurchase(entry.id)) buyable++;
        Set(available, game.TicketActive ? "Completa il biglietto" :
            buyable == 1 ? "1 acquistabile" : buyable + " acquistabili");
        RefreshPossiblePrizes(kind, max);
        RefreshHelp();
    }

    private void RefreshPossiblePrizes(ScratchGameManager.TicketKind kind, int max)
    {
        for (int i = 0; i < 4; i++)
        {
            var match = new ScratchSymbolManager.MatchResult(
                (ScratchSymbolManager.MatchTier)(i + 1), string.Empty, i + 2);
            long award = ScratchRewardPolicy.Calculate(match, true, game.TicketPrize(kind),
                economy.BasePrize, ScratchGameManager.TicketPrice(kind),
                economy.ConsecutiveWinningTickets, skills.GetLevel).RequestedCoins;
            bool attainable = max >= i + 2;
            if (prizeNames != null && i < prizeNames.Length && prizeNames[i] != null)
                prizeNames[i].color = attainable ? Cream : Muted;
            if (prizeAmounts != null && i < prizeAmounts.Length && prizeAmounts[i] != null)
            {
                Set(prizeAmounts[i], award.ToString("N0"));
                prizeAmounts[i].color = attainable ? Gold : Muted;
            }
            if (prizeCoins != null && i < prizeCoins.Length && prizeCoins[i] != null)
                prizeCoins[i].color = new Color(1f, 1f, 1f, attainable ? 1f : .45f);
        }
    }

    private void RefreshGoal()
    {
        string goal = ScratchRadialProgression.NextGoal(game, skills);
        Match count = CountPattern.Match(goal);
        float fraction = 0f;
        string counter = "";
        if (count.Success && int.TryParse(count.Groups[1].Value, out int current) &&
            int.TryParse(count.Groups[2].Value, out int target) && target > 0)
        {
            fraction = Mathf.Clamp01((float)current / target);
            counter = Mathf.Min(current, target) + "/" + target;
            goal = goal.Remove(count.Index, count.Length).Trim();
        }
        if (goal.StartsWith("FINALE:") && !game.DemoFinished)
        {
            goal = "Finale: " + game.DemoFinalGoldTickets + "/" + game.DemoFinalGoldMinTickets +
                " Dorati · " + game.DemoFinalGoldPayout + "/" + game.DemoFinalGoldPayoutTarget +
                " monete · potere " + (game.DemoFinalPowerActivated ? "OK" : "da usare");
            fraction = Mathf.Min(Mathf.Clamp01((float)game.DemoFinalGoldTickets/game.DemoFinalGoldMinTickets),
                Mathf.Clamp01((float)game.DemoFinalGoldPayout/game.DemoFinalGoldPayoutTarget));
            if (!game.DemoFinalPowerActivated) fraction = Mathf.Min(fraction, .95f);
        }
        if (game.DemoFinished) { goal = "Demo completata!"; fraction = 1; }
        // Keep the underlying milestone rules; hide implementation IDs from players.
        goal = Regex.Replace(goal, @"\s*\([A-Z]\d{2}\)", "");
        Match skillGoal = Regex.Match(goal, @"^PER ([A-Z]\d{2}):\s*");
        if (skillGoal.Success)
        {
            var entry = ScratchSkillCatalogV2.Find(skillGoal.Groups[1].Value);
            if (entry != null)
                goal = entry.title + ": " + goal.Substring(skillGoal.Length);
        }
        bool hasProgress = count.Success || goal.StartsWith("Finale:") || game.DemoFinished;
        Set(milestone, goal);
        Set(milestoneCount, counter);
        if (progressTrack != null) progressTrack.SetActive(hasProgress);
        if (progress != null)
        {
            progress.gameObject.SetActive(hasProgress);
            progress.sizeDelta = new Vector2(250 * fraction, 12);
        }
    }

    private void RefreshMatch()
    {
        var match = game.CurrentMatch;
        string name;
        switch (match.Tier)
        {
            case ScratchSymbolManager.MatchTier.Pair: name = "COPPIA"; break;
            case ScratchSymbolManager.MatchTier.Triple: name = "TRIS"; break;
            case ScratchSymbolManager.MatchTier.Poker: name = "POKER"; break;
            case ScratchSymbolManager.MatchTier.Five: name = "CINQUINA"; break;
            default: name = "NESSUNA"; break;
        }
        Set(matchName, name);
        Set(matchMultiplier, match.IsMatch ? "×" + match.Multiplier + " · " + match.Symbol :
            game.WaitingForTicketChoice ? "Biglietto completato" : "Cerca simboli uguali");
        Set(prizeLabel, game.TicketActive ? "Premio provvisorio" : "Ultima vincita");
        long value = game.TicketPayout;
        if (game.TicketActive)
        {
            value = 0;
            if (match.IsMatch && !game.TicketVoided)
            {
                var estimated = ScratchRewardPolicy.Calculate(match, false, game.TicketPrize(game.CurrentTicketKind),
                    economy.BasePrize, ScratchGameManager.TicketPrice(game.CurrentTicketKind),
                    economy.ConsecutiveWinningTickets, skills.GetLevel);
                value = System.Math.Min(estimated.RequestedCoins, (long)int.MaxValue - economy.Coins);
            }
        }
        Set(payout, value.ToString("N0"));
    }

    private void RefreshHelp()
    {
        bool ready = game.State == ScratchGameManager.RoundState.Ready;
        bool live = game.TicketActive;
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (helpPrimary != null) helpPrimary.gameObject.SetActive(!ready);
        if (helpSecondary != null) helpSecondary.gameObject.SetActive(!ready);
        Set(helpPrimary, game.DemoFinished ? "Demo completata" : live ? "Gratta un'altra casella" : "Scegli il prossimo biglietto");
        string detail = live ? "Scelte " + game.SelectedCount + "/" + game.MaxScratches +
            " · Tieni premuto e trascina" : "Apri i potenziamenti o scegli un biglietto";
        if (live && skills.GetLevel("G08") > 0) detail += " · SHIFT: Titan " + skills.TitanCharges;
        if (live && skills.GetLevel("C06") > 0)
            detail += game.InspectionUsedThisTicket ? " · Ispezione usata" : " · Destro/Ctrl: ispeziona";
        if (live && skills.GetLevel("G04") > 0) detail += " · F: ventaglio";
        if (live && game.TicketVoided) detail = "Biglietto di prova: nessun premio";
        Set(helpSecondary, detail);
    }

    private void LateUpdate()
    {
        if (shopWalletPanel != null && shopWalletPanel.activeSelf != flow.ShopOpen)
            shopWalletPanel.SetActive(flow.ShopOpen);
        if (shadow == null || ticket == null || ticketGroup == null) return;
        var rt = shadow.rectTransform;
        rt.sizeDelta = ticket.sizeDelta;
        rt.localScale = ticket.localScale;
        rt.localRotation = ticket.localRotation;
        rt.anchoredPosition = ticket.anchoredPosition + (Vector2)(ticket.localRotation *
            new Vector3(12 * ticket.localScale.x, -14 * ticket.localScale.y, 0));
        shadow.color = new Color(.06f,.11f,.09f,.45f * ticketGroup.alpha);
    }

    private static void Set(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }
}
