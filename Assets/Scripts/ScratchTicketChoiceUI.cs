using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Displays the three scene-authored ticket cards BETWEEN tickets only.
/// Never pauses the global clock or changes the Skill Tree panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchTicketChoiceUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchFlowPresentation presentation;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button[] ticketButtons = new Button[3];
    [SerializeField] private TMP_Text[] ticketDetails = new TMP_Text[3];
    [SerializeField] private TMP_Text statusLabel;

    private UnityAction[] buttonActions;
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
            ticketButtons == null || ticketButtons.Length != 3 ||
            ticketDetails == null || ticketDetails.Length != 3)
        {
            Debug.LogError("ScratchTicketChoiceUI: missing managers or card references.", this);
            enabled = false;
            return;
        }

        buttonActions = new UnityAction[3];
        for (int i = 0; i < 3; i++)
        {
            if (ticketButtons[i] == null || ticketDetails[i] == null)
            {
                Debug.LogError("ScratchTicketChoiceUI: ticket card reference is missing.", this);
                enabled = false;
                return;
            }

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
        if (index < 0 || index >= 3 || !game.WaitingForTicketChoice)
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
        for (int i = 0; i < 3; i++)
        {
            var kind = (ScratchGameManager.TicketKind)i;
            int cost = ScratchGameManager.TicketPrice(kind);
            int prize = game.TicketPrize(kind);
            if (ticketDetails[i] != null)
                ticketDetails[i].text = cost == 0
                    ? $"FREE  /  BASE PRIZE {prize}"
                    : $"COST {cost}  /  BASE PRIZE {prize}";

            if (ticketButtons[i] != null)
                ticketButtons[i].interactable = show && economy.Coins >= cost;
        }

        if (statusLabel != null)
            statusLabel.text = $"YOUR COINS: {economy.Coins}  /  THE TIMER KEEPS RUNNING!";

        if (choicePanel.activeSelf != show)
            choicePanel.SetActive(show);
    }
}
