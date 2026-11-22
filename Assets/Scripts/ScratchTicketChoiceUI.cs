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
            if (ticketDetails[i] != null)
                ticketDetails[i].text = !unlocked
                    ? "BLOCCATO!\nSBLOCCA NELLE ABILITA'"
                    : $"{ScratchGameManager.TicketSide(kind)}x{ScratchGameManager.TicketSide(kind)} - {ScratchGameManager.TicketSide(kind)} SCELTE\n" +
                      $"COSTO {cost} | PREMIO {prize}";
            if (ticketButtons[i] != null)
                ticketButtons[i].interactable = show && unlocked && economy.Coins >= cost;
        }

        if (statusLabel != null)
            statusLabel.text = $"SCEGLI IL PROSSIMO BIGLIETTO   •   {economy.Coins:N0} MONETE DISPONIBILI";

        if (choicePanel.activeSelf != show)
            choicePanel.SetActive(show);
    }
}
