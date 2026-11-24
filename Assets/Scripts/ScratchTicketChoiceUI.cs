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

    private UnityAction[] buttonActions;
    private readonly TMP_Text[] actionLabels = new TMP_Text[4];
    private readonly Image[] actionBackgrounds = new Image[4];
    private static readonly string[] UnlockIds = { "", "C03", "C05", "C07" };
    private static readonly Color Gold = new Color32(234, 194, 92, 255);
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
            int index = i;
            buttonActions[i] = () => Choose(index);
            ticketButtons[i].onClick.AddListener(buttonActions[i]);
        }

        game.Changed += Refresh;
        economy.Changed += Refresh;
        registered = true;
        Refresh();
    }

    private void OnDestroy()
    {
        if (registered)
        {
            game.Changed -= Refresh;
            economy.Changed -= Refresh;
        }

        if (buttonActions != null)
            for (int i = 0; i < buttonActions.Length; i++)
                if (buttonActions[i] != null && ticketButtons[i] != null)
                    ticketButtons[i].onClick.RemoveListener(buttonActions[i]);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= 4 || !game.WaitingForTicketChoice)
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
                ticketDetails[i].text = $"Costo: {(cost == 0 ? "Gratis" : cost.ToString("N0"))}\n" +
                    $"Premio base: {prize:N0}\n{ScratchGameManager.TicketSide(kind) * ScratchGameManager.TicketSide(kind)} caselle";
            if (actionLabels[i] != null)
            {
                actionLabels[i].text = !unlocked ? $"BLOCCATO\n<size=17>Sblocca {UnlockIds[i]}</size>"
                    : !affordable ? "MONETE INSUFFICIENTI" : "SELEZIONA";
                actionLabels[i].color = unlocked && affordable ? Dark : Cream;
            }
            if (actionBackgrounds[i] != null)
                actionBackgrounds[i].color = unlocked && affordable ? Gold : Dark;
            if (ticketButtons[i] != null)
                ticketButtons[i].interactable = show && unlocked && affordable;
        }

        if (statusLabel != null)
            statusLabel.text = $"{economy.Coins:N0} MONETE DISPONIBILI  ·  {game.MaxScratches} GRATTATE PER BIGLIETTO";

        if (choicePanel.activeSelf != show)
            choicePanel.SetActive(show);
    }
}
