using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored Vintage Lottery HUD. Reads gameplay state; never changes progression.</summary>
[DisallowMultipleComponent]
public sealed partial class ScratchVintageGameplayHUD : MonoBehaviour
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
    [SerializeField] private Image payoutCoin;
    [SerializeField] private Image[] prizeHighlights = new Image[4];
    [SerializeField] private Image[] prizeLocks = new Image[4];
    [SerializeField] private TMP_Text prizeFooter;
    [SerializeField] private Image matchAccent;
    [SerializeField] private TMP_Text matchHeader, refundLabel, refundAmount;
    [SerializeField] private Image refundCoin;
    [SerializeField] private GameObject resultHelpGroup;
    [SerializeField] private TMP_Text resultSummary;
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
    private int indicatorTicket = -1, lastRevealed;
    private float indicatorPulseAt = -10f;
    public RectTransform WalletFeedbackTarget => wallet != null ? wallet.rectTransform : null;

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
        if (indicatorTicket != game.TicketNumber)
        {
            indicatorTicket = game.TicketNumber;
            lastRevealed = 0;
            indicatorPulseAt = -10f;
        }
        if (revealed > lastRevealed) indicatorPulseAt = Time.unscaledTime;
        lastRevealed = revealed;
        int unlocked = ScratchRadialProgression.UnlockedChapter(game, skills);
        Set(chapter, "CAPITOLO " + unlocked.ToString("00"));
        Set(wallet, economy.Coins.ToString("N0"));
        Set(shopWallet, economy.Coins.ToString("N0"));
        Set(picks, revealed + "<size=64> /" + max + "</size>");
        int left = Mathf.Max(0, max - revealed);
        Set(remaining, left == 0 ? "Biglietto completato" : "");
        for (int i = 0; i < indicators.Length; i++)
        {
            Image im = indicators[i];
            if (im == null) continue;
            im.gameObject.SetActive(i < max);
            if (i >= max) continue;
            im.color = i < revealed ? Gold : i < selected ? Reserved : Dark;
            float step = max == 5 ? 88 : 96;
            var rt = im.rectTransform;
            rt.anchoredPosition = new Vector2(i * step, -192);
            rt.sizeDelta = new Vector2(max == 5 ? 68 : 76, 24);
        }
        Set(ticketNumber, game.TicketNumber == 0 ? "BIGLIETTO —" : "BIGLIETTO " + game.TicketNumber.ToString("000"));
        string name = ScratchGameManager.TicketDisplayName(kind);
        Set(ticketType, name.Substring(0, 1) + name.Substring(1).ToLowerInvariant());
        int price = ScratchGameManager.TicketPrice(kind);
        Set(cost, price == 0 ? "Gratis" : price.ToString("N0"));
        // Base prize remains an internal economy parameter.
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

    private void RefreshGoal()
    {
        string raw = ScratchRadialProgression.NextGoal(game, skills);
        string goal = raw;
        string counter = "";
        float fraction = 0f;
        bool showProgress = false;
        Match skillGoal = Regex.Match(raw, @"^PER ([A-Z]\d{2}):\s*");
        string skillId = skillGoal.Success ? skillGoal.Groups[1].Value :
            raw == "ACQUISTA FORTUNATO (C03)" ? "C03" :
            raw == "SBLOCCA DIAMANTE" ? "C07" : null;
        string action = skillGoal.Success ? raw.Substring(skillGoal.Length) : raw;
        string title = skillId == null ? "" : ScratchSkillCatalogV2.Find(skillId)?.title ?? "";

        Match count = CountPattern.Match(action);
        if (game.DemoFinished)
        {
            goal = "Demo completata!";
        }
        else if (raw.StartsWith("FINALE:"))
        {
            goal = game.DemoFinalGoldTickets + "/" + game.DemoFinalGoldMinTickets + " Dorati · " +
                game.DemoFinalGoldPayout.ToString("N0") + "/" + game.DemoFinalGoldPayoutTarget.ToString("N0") +
                " monete\nPotere finale: " + (game.DemoFinalPowerActivated ? "attivato" : "da attivare");
            fraction = Mathf.Min(Mathf.Clamp01((float)game.DemoFinalGoldTickets / Mathf.Max(1, game.DemoFinalGoldMinTickets)),
                Mathf.Clamp01((float)game.DemoFinalGoldPayout / Mathf.Max(1, game.DemoFinalGoldPayoutTarget)));
            if (!game.DemoFinalPowerActivated) fraction = Mathf.Min(fraction, .95f);
            showProgress = true;
        }
        else if (count.Success && int.TryParse(count.Groups[1].Value, out int current) &&
            int.TryParse(count.Groups[2].Value, out int target) && target > 0)
        {
            string task = action.Remove(count.Index, count.Length).Trim();
            goal = SentenceCase(task) + (title.Length == 0 ? "" : "\nPer: " + title);
            counter = Mathf.Clamp(current, 0, target) + "/" + target;
            fraction = Mathf.Clamp01((float)current / target);
            showProgress = true;
        }
        else if (skillId != null && title.Length > 0)
        {
            if (!skills.RequirementsMet(skillId))
                goal = title + "\n" + skills.RequirementText(skillId).Replace("Prima ", "Acquista ").Replace(" livello 1", "");
            else if (!ScratchRadialProgression.Available(skillId, game, skills))
                goal = title + "\n" + SentenceCase(action);
            else if (!skills.CanPurchase(skillId) && economy.Coins < skills.GetPrice(skillId))
            {
                int price = skills.GetPrice(skillId);
                goal = title + "\nRaccogli monete";
                counter = Mathf.Clamp(economy.Coins, 0, price) + "/" + price;
                fraction = Mathf.Clamp01((float)economy.Coins / Mathf.Max(1, price));
                showProgress = true;
            }
            else
                goal = title + "\n" + (skills.CanPurchase(skillId) ? "Nei potenziamenti" : "Non disponibile");
        }
        else if (raw == "COMPLETA 6 DIAMANTI" || raw == "COMPLETA 25 DIAMANTI" || raw == "COMPLETA 70 DIAMANTI")
        {
            int targetDiamonds = raw == "COMPLETA 6 DIAMANTI" ? 6 : raw == "COMPLETA 25 DIAMANTI" ? 25 : 70;
            int completed = game.CompletedByTier(ScratchGameManager.TicketKind.Diamond);
            goal = SentenceCase(raw);
            counter = Mathf.Clamp(completed, 0, targetDiamonds) + "/" + targetDiamonds;
            fraction = Mathf.Clamp01((float)completed / targetDiamonds);
            showProgress = true;
        }
        else
            goal = SentenceCase(raw);

        int newline = goal.IndexOf('\n');
        string headline = newline >= 0 ? goal.Substring(0, newline) : goal;
        string subtitle = newline >= 0 ? goal.Substring(newline + 1) : "";
        Set(milestone, "<color=#EAC25C>" + headline + "</color>" +
            (subtitle.Length == 0 ? "" : "\n<size=23>" + subtitle + "</size>"));
        Set(milestoneCount, counter);
        if (progressTrack != null) progressTrack.SetActive(showProgress);
        if (progress != null)
        {
            progress.gameObject.SetActive(showProgress);
            progress.sizeDelta = new Vector2(280 * fraction, 12);
        }
    }

    private static string SentenceCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
    }

    private void RefreshHelp()
    {
        bool ready = game.State == ScratchGameManager.RoundState.Ready;
        bool live = game.TicketActive;
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (helpPrimary != null) helpPrimary.gameObject.SetActive(!ready);
        if (helpSecondary != null) helpSecondary.gameObject.SetActive(!ready);
        bool result = !live && game.TicketNumber > 0 && !flow.CanShowTicketChoices;
        if (helpPrimary != null) helpPrimary.gameObject.SetActive(!ready && !result);
        if (helpSecondary != null) helpSecondary.gameObject.SetActive(!ready && !result);
        if (resultHelpGroup != null) resultHelpGroup.SetActive(result);
        if (!live && game.TicketNumber > 0)
        {
            long net = (long)game.TicketPayout - ScratchGameManager.TicketPrice(game.CurrentTicketKind);
            string signedNet = net > 0 ? "+" + net.ToString("N0") : net.ToString("N0");
            string outcome = game.TicketPayout > 0 ? "Incassato <color=#EAC25C>" + game.TicketPayout.ToString("N0") + "</color>" : "Nessuna vincita";
            string netColor = net > 0 ? "#EAC25C" : net < 0 ? "#D5A29A" : "#B9C9BE";
            Set(resultSummary, game.TicketVoided ? "Biglietto di prova · nessuna vincita" :
                outcome + " · Bilancio netto: <color=" + netColor + ">" + signedNet + "</color> monete");
        }
        Set(helpPrimary, live ? (game.RevealedCount == 0 ? "Gratta la prima casella" : "Gratta un'altra casella") : "Scegli il prossimo biglietto");
        string detail = live ? "Tieni premuto e trascina" : "Apri i potenziamenti o scegli un biglietto";
        if (live && skills.GetLevel("G08") > 0) detail += " · SHIFT: Titan " + skills.TitanCharges;
        if (live && skills.GetLevel("C06") > 0)
            detail += game.InspectionUsedThisTicket ? " · Ispezione usata" : " · Destro/Ctrl: ispeziona";
        if (live && skills.GetLevel("G04") > 0) detail += " · F: ventaglio";
        if (live && game.TicketVoided) detail = "Biglietto di prova: nessun premio";
        Set(helpSecondary, detail);
    }

    private void LateUpdate()
    {
        UpdateWinFeedback();
        if (indicators != null && lastRevealed > 0 && lastRevealed <= indicators.Length)
        {
            Image last = indicators[lastRevealed - 1];
            if (last != null)
            {
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01((Time.unscaledTime - indicatorPulseAt) / .24f));
                last.color = Color.Lerp(Gold, Cream, pulse * .7f);
            }
        }
        if (resultHelpGroup != null)
        {
            bool result = flow.ShowingResult;
            if (resultHelpGroup.activeSelf != result) resultHelpGroup.SetActive(result);
            if (result && helpPrimary != null) helpPrimary.gameObject.SetActive(false);
            if (result && helpSecondary != null) helpSecondary.gameObject.SetActive(false);
        }
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
