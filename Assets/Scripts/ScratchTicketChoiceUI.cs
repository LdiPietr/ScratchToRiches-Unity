using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Displays four scene-authored ticket cards. Locked tickets remain visible.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchTicketChoiceUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchFlowPresentation presentation;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button[] ticketButtons = new Button[4];
    [SerializeField] private TMP_Text[] ticketDetails = new TMP_Text[4];
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Sprite chooseSprite, lockedSprite;
    [SerializeField] private RectTransform selectionHeader, selectionUpgradeGroup;
    [SerializeField] private RectTransform[] walletParts;
    private Transform upgradeHome;
    private Vector2 upgradeHomePosition;
    private Vector3 upgradeHomeScale;
    private int upgradeHomeSibling;
    private Vector2[] walletHomePositions;
    private Vector2[] walletHomeSizes;
    private readonly TMP_Text[,] prizeLabels = new TMP_Text[4, 4];
    private static readonly Color Muted = new Color32(149, 168, 155, 255);
    private static readonly Color Gold = new Color32(234, 194, 92, 255);
    private bool headerMoved;
    private ScratchSkillTree skills;
    private readonly TMP_Text[,] prizeValues = new TMP_Text[4, 4];
    private readonly GameObject[] locks = new GameObject[4];

    private UnityAction[] buttonActions;
    private readonly TMP_Text[] actionLabels = new TMP_Text[4];
    private readonly Image[] actionBackgrounds = new Image[4];
    private static readonly string[] UnlockIds = { "", "C03", "C05", "C07" };
    private static readonly Color Dark = new Color32(32, 59, 51, 255);
    private static readonly Color Cream = new Color32(249, 239, 216, 255);
    private bool registered;

    private void Awake()
    {
        if (game == null) game = GetComponent<ScratchGameManager>();
        if (economy == null) economy = GetComponent<ScratchEconomyManager>();
        if (presentation == null) presentation = GetComponent<ScratchFlowPresentation>();
        // The overlay must never cover the starting board.
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private void Start()
    {
        if (game == null || economy == null || choicePanel == null ||
            ticketButtons == null || ticketButtons.Length != 4 ||
            ticketDetails == null || ticketDetails.Length != 4)
        {
            Debug.LogError("ScratchTicketChoiceUI: missing managers or card references.", this);
            enabled = false;
            return;
        }

        skills = GetComponent<ScratchSkillTree>();
        if (selectionHeader != null && selectionUpgradeGroup != null && walletParts != null)
        {
            upgradeHome = selectionUpgradeGroup.parent;
            upgradeHomePosition = selectionUpgradeGroup.anchoredPosition;
            upgradeHomeScale = selectionUpgradeGroup.localScale;
            upgradeHomeSibling = selectionUpgradeGroup.GetSiblingIndex();
            walletHomePositions = new Vector2[walletParts.Length];
            walletHomeSizes = new Vector2[walletParts.Length];
            for (int j = 0; j < walletParts.Length; j++)
            {
                if (walletParts[j] == null) continue;
                walletHomePositions[j] = walletParts[j].anchoredPosition;
                walletHomeSizes[j] = walletParts[j].sizeDelta;
            }
        }
        buttonActions = new UnityAction[4];
        for (int i = 0; i < 4; i++)
        {
            if (ticketButtons[i] == null || ticketDetails[i] == null)
            {
                Debug.LogError("ScratchTicketChoiceUI: ticket card reference is missing.", this);
                enabled = false;
                return;
            }

            var card = ticketButtons[i].transform;
            actionLabels[i] = card.Find("SelectHint")?.GetComponent<TMP_Text>();
            actionBackgrounds[i] = card.Find("ChoiceActionBackground")?.GetComponent<Image>();
            locks[i] = card.Find("ChoiceLock")?.gameObject;
            for (int j = 0; j < 4; j++)
            {
                prizeValues[i, j] = card.Find("PrizeValue_" + j)?.GetComponent<TMP_Text>();
                prizeLabels[i, j] = card.Find("PrizeLabel_" + j)?.GetComponent<TMP_Text>();
            }
            int index = i;
            buttonActions[i] = () => Choose(index);
            ticketButtons[i].onClick.AddListener(buttonActions[i]);
        }

        game.Changed += Refresh;
        economy.Changed += Refresh;
        if (skills != null) skills.Changed += Refresh;
        registered = true;
        Refresh();
    }

    private void OnDestroy()
    {
        if (registered)
        {
            game.Changed -= Refresh;
            economy.Changed -= Refresh;
            if (skills != null) skills.Changed -= Refresh;
        }

        if (buttonActions != null)
            for (int i = 0; i < buttonActions.Length; i++)
                if (buttonActions[i] != null && ticketButtons[i] != null)
                    ticketButtons[i].onClick.RemoveListener(buttonActions[i]);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= 4 || !game.WaitingForTicketChoice ||
            (presentation != null && !presentation.CanShowTicketChoices))
            return;

        game.ChooseNextTicket((ScratchGameManager.TicketKind)index);
        Refresh();
    }

    public void Refresh()
    {
        if (game == null || economy == null || choicePanel == null || ticketButtons == null)
            return;

        bool show = game.WaitingForTicketChoice &&
            (presentation == null || presentation.CanShowTicketChoices);
        for (int i = 0; i < 4; i++)
        {
            var kind = (ScratchGameManager.TicketKind)i;
            int cost = ScratchGameManager.TicketPrice(kind);
            int prize = game.TicketPrize(kind);
            bool unlocked = game.IsTicketUnlocked(kind);
            bool affordable = economy.Coins >= cost;
            if (ticketDetails[i] != null)
                ticketDetails[i].text = cost == 0 ? "Gratis" : cost.ToString("N0");

            for (int j = 0; j < 4; j++)
            {
                TMP_Text value = prizeValues[i, j];
                if (value == null) continue;
                var match = new ScratchSymbolManager.MatchResult(
                    (ScratchSymbolManager.MatchTier)(j + 1), "", j + 2);
                long award = ScratchRewardPolicy.Calculate(match, true, prize, economy.BasePrize,
                    cost, economy.ConsecutiveWinningTickets, Level).RequestedCoins;
                bool attainable = game.MaxScratches >= j + 2;
                value.text = award.ToString("N0");
                value.color = attainable ? Gold : Muted;
                if (prizeLabels[i, j] != null)
                    prizeLabels[i, j].color = attainable ? Cream : Muted;
            }
            bool selectable = unlocked && affordable;
            if (actionLabels[i] != null)
            {
                var unlock = ScratchSkillCatalogV2.Find(UnlockIds[i]);
                bool fullGameOnly = ScratchSkillTree.DemoEdition && unlock != null && !unlock.demoEligible;
                actionLabels[i].text = !unlocked ? (fullGameOnly
                    ? "GIOCO COMPLETO\n<size=19>Disponibile nella versione finale</size>"
                    : "BLOCCATO\n<size=19>Sblocca nei potenziamenti</size>")
                    : !affordable ? "MONETE INSUFFICIENTI" : "SCEGLI";
                actionLabels[i].color = selectable ? Dark : Cream;
                actionLabels[i].rectTransform.anchoredPosition = new Vector2(unlocked ? 0 : 10, -326);
                actionLabels[i].fontSize = unlocked && affordable ? 28 : 23;
            }
            if (actionBackgrounds[i] != null)
            {
                actionBackgrounds[i].sprite = selectable ? chooseSprite : lockedSprite;
                actionBackgrounds[i].color = Color.white;
                if (!selectable) actionBackgrounds[i].overrideSprite = null;
            }
            if (locks[i] != null) locks[i].SetActive(!unlocked);
            if (ticketButtons[i] != null)
                ticketButtons[i].interactable = show && unlocked && affordable;
        }

        if (statusLabel != null) statusLabel.gameObject.SetActive(false);
        SyncHeader(show);

        if (choicePanel.activeSelf != show)
            choicePanel.SetActive(show);
    }
    private int Level(ScratchSkillTree.SkillId id) => skills != null ? skills.GetLevel(id) : 0;

    // Reuse the live wallet and upgrade button. No duplicated shop event or UI object.
    private void SyncHeader(bool show)
    {
        if (upgradeHome == null || walletHomePositions == null || headerMoved == show) return;
        headerMoved = show;
        if (show)
        {
            selectionUpgradeGroup.SetParent(selectionHeader, false);
            selectionUpgradeGroup.anchoredPosition = new Vector2(1400, -13);
            selectionUpgradeGroup.localScale = Vector3.one * .72f;
        }
        else
        {
            selectionUpgradeGroup.SetParent(upgradeHome, false);
            selectionUpgradeGroup.SetSiblingIndex(upgradeHomeSibling);
            selectionUpgradeGroup.anchoredPosition = upgradeHomePosition;
            selectionUpgradeGroup.localScale = upgradeHomeScale;
        }
        for (int i = 0; i < walletParts.Length; i++)
        {
            if (walletParts[i] == null) continue;
            walletParts[i].anchoredPosition = walletHomePositions[i] + (show ? Vector2.left * 460 : Vector2.zero);
            walletParts[i].sizeDelta = show && i == 2 ? new Vector2(310, walletHomeSizes[i].y) : walletHomeSizes[i];
        }
    }
}
