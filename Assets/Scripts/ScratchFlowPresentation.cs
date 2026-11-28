using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored ticket animations and player-invoked shop overlay. No countdown.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class ScratchFlowPresentation : MonoBehaviour
{
    private enum Phase
    {
        Idle, TicketEntering, TicketLive, TicketResult, TicketLeaving,
        Choosing, CurtainDescending, CurtainVisible, CurtainAscending
    }

    [Header("Game references")]
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchTicketChoiceUI ticketChooser;

    [Header("Moving ticket")]
    [SerializeField] private Image ticketArtwork;
    [SerializeField] private Sprite[] ticketArtworkSprites = new Sprite[3];
    [SerializeField] private TMP_Text ticketTypeLabel;
    [SerializeField] private Image[] cellFrameImages = new Image[16];
    [SerializeField] private Sprite[] cellFrameSprites = new Sprite[3];
    private int appliedCellTicket = -1;
    [SerializeField] private RectTransform ticketTransform;
    [SerializeField] private CanvasGroup ticketCanvasGroup;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button nextTicketButton;
    [SerializeField] private TMP_Text nextTicketLabel;
    [SerializeField] private Button repeatTicketButton, resultUpgradesButton;
    [SerializeField] private TMP_Text repeatTicketLabel, resultUpgradesLabel;
    private ScratchSkillTree resultSkills;
    [Header("Authored result popup")]
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private RectTransform resultPopupPanel;
    [SerializeField] private Image resultDimmer;
    [SerializeField] private CanvasGroup resultCardGroup, rewardHeaderGroup, rewardValueGroup, rewardActionsGroup;
    [SerializeField] private TMP_Text resultPopupHeading, resultPopupAmount;
    [SerializeField] private TMP_Text resultOutcomeLabel, resultPopupUnit, resultPopupNet;
    [SerializeField] private Image[] resultMatchIcons = new Image[5];
    [SerializeField] private Sprite resultJollySprite;
    [SerializeField] private GameObject resultUpgradeBadge;
    [SerializeField] private TMP_Text resultUpgradeCount;
    private float resultDepartureStarted = -1f;
    public bool ShowingTicketArtwork => ShowingTicketHud && !ResultPopupVisible;
    private ScratchWinJuice resultJuice;
    private float resultReadyAt, resultOpenedAt;
    private Vector2 nextTicketHome;
    public bool HasResultPopup => resultPopup != null;
    public bool ResultPopupVisible => resultPopup != null && resultPopup.activeInHierarchy &&
        (resultPopupPanel == null || resultPopupPanel.gameObject.activeInHierarchy);
    [SerializeField, Range(0.1f, 1.2f)] private float ticketTravelDuration = 0.36f;

    [Header("Full-screen skill curtain")]
    [SerializeField] private GameObject curtainObject;
    [SerializeField] private GameObject shopContent;
    [SerializeField] private RectTransform curtainRect;
    [SerializeField] private Button continueButton;
    [SerializeField, Range(0.1f, 1.5f)] private float curtainTravelDuration = 0.55f;

    [Header("Side information")]
    [SerializeField] private TMP_Text leftInformation;
    [SerializeField] private TMP_Text rightInformation;

    private Phase phase = Phase.Idle;
    private Phase phaseBeforeShop = Phase.Idle;
    private Vector2 ticketHome;
    private Vector2 curtainHome;
    private int seenTicketNumber;
    private Coroutine motion;
    private bool subscribed;
    private string previousLeft;
    private string previousRight;

    public bool CanScratch => phase == Phase.TicketLive;
    public bool ShowingResult => phase == Phase.TicketResult;
    public bool CanAcknowledgeResult => !previewingResult && ShowingResult && !game.TicketActive &&
        (!HasResultPopup || (ResultPopupVisible && (rewardActionsGroup == null || rewardActionsGroup.interactable)));
    public bool CanOpenShop => game != null && !game.TicketActive && game.WaitingForTicketChoice &&
        phase == Phase.Choosing;
    public bool ShopOpen => phase == Phase.CurtainDescending || phase == Phase.CurtainVisible || phase == Phase.CurtainAscending;
    public bool ShopVisible => phase == Phase.CurtainVisible;
    public bool ShopContentVisible => ShopVisible && shopContent != null && shopContent.activeInHierarchy;
    public bool CanShowTicketChoices => phase == Phase.Choosing;
    public bool ShowingSelectionBackdrop => phase == Phase.Choosing ||
        phase == Phase.CurtainDescending || phase == Phase.CurtainAscending;
    public bool ShowingTicketHud => phase == Phase.TicketEntering || phase == Phase.TicketLive ||
        phase == Phase.TicketResult || phase == Phase.TicketLeaving;

    private void Awake()
    {
        if (game == null) game = GetComponent<ScratchGameManager>();
        if (economy == null) economy = GetComponent<ScratchEconomyManager>();
        if (ticketChooser == null) ticketChooser = GetComponent<ScratchTicketChoiceUI>();
        if (ticketTransform != null) ticketHome = ticketTransform.anchoredPosition;
        if (curtainRect != null) curtainHome = curtainRect.anchoredPosition;

        if (ticketArtwork != null && ticketArtworkSprites != null &&
            ticketArtworkSprites.Length >= 3)
            ticketArtwork.sprite = ticketArtworkSprites[0];
        ApplyCellFrames(ScratchGameManager.TicketKind.Standard);
        UpdateTicketTypeLabel(ScratchGameManager.TicketKind.Standard);
        if (shopContent != null) shopContent.SetActive(true);
        if (curtainObject != null) curtainObject.SetActive(false);
        if (ticketCanvasGroup != null)
        {
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
        }
        if (continueButton != null) continueButton.interactable = false;
        resultSkills = GetComponent<ScratchSkillTree>();
        resultJuice = GetComponent<ScratchWinJuice>();
        if (nextTicketButton != null)
            nextTicketHome = ((RectTransform)nextTicketButton.transform).anchoredPosition;
        HideResultActions();
    }

    private void Start()
    {
        if (game == null || economy == null || ticketChooser == null ||
            ticketTransform == null || ticketCanvasGroup == null ||
            curtainObject == null || shopContent == null || curtainRect == null || continueButton == null || shopButton == null || nextTicketButton == null || nextTicketLabel == null ||
            !shopContent.transform.IsChildOf(curtainRect))
        {
            Debug.LogError("ScratchFlowPresentation: missing scene references.", this);
            enabled = false;
            return;
        }

        game.Changed += HandleGameChanged;
        economy.Changed += UpdateInformation;
        continueButton.onClick.AddListener(ContinueAfterUpgrades);
        shopButton.onClick.AddListener(OpenShop);
        nextTicketButton.onClick.AddListener(ContinueAfterResult);
        if (repeatTicketButton != null) repeatTicketButton.onClick.AddListener(RepeatAfterResult);
        if (resultUpgradesButton != null) resultUpgradesButton.onClick.AddListener(OpenUpgradesAfterResult);
        economy.Changed += RefreshResultActions;
        if (resultSkills != null) resultSkills.Changed += RefreshResultActions;
        subscribed = true;
        UpdateInformation();
        HandleGameChanged();
    }

    private void OnDestroy()
    {
        if (!subscribed) return;
        game.Changed -= HandleGameChanged;
        economy.Changed -= UpdateInformation;
        continueButton.onClick.RemoveListener(ContinueAfterUpgrades);
        shopButton.onClick.RemoveListener(OpenShop);
        nextTicketButton.onClick.RemoveListener(ContinueAfterResult);
        if (repeatTicketButton != null) repeatTicketButton.onClick.RemoveListener(RepeatAfterResult);
        if (resultUpgradesButton != null) resultUpgradesButton.onClick.RemoveListener(OpenUpgradesAfterResult);
        economy.Changed -= RefreshResultActions;
        if (resultSkills != null) resultSkills.Changed -= RefreshResultActions;
    }

    private void Update()
    {
        UpdateInformation();
        TickResultPopup();
        if (shopButton != null) shopButton.interactable = CanOpenShop;
    }

    private void HandleGameChanged()
    {
        UpdateInformation();

        if (ShopOpen || previewingResult) return;
        if (ShowingResult) RefreshResultActions();
        if (!game.TicketActive && (phase == Phase.TicketLive || phase == Phase.TicketEntering) && game.State != ScratchGameManager.RoundState.Ready)
        {
            StopMotion();
            ShowTicketResult();
            return;
        }

        if (game.State == ScratchGameManager.RoundState.Ready)
        {
            StopMotion();
            seenTicketNumber = 0;
            HideResultActions();
            phase = Phase.Choosing;
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
            ticketChooser.Refresh();
            return;
        }

        if (game.State != ScratchGameManager.RoundState.Playing)
            return;

        if (game.WaitingForTicketChoice && phase == Phase.Idle)
        {
            phase = Phase.Choosing;
            ticketCanvasGroup.alpha = 0f;
            ticketCanvasGroup.blocksRaycasts = false;
            ticketChooser.Refresh();
            return;
        }

        if (game.TicketActive && seenTicketNumber != game.TicketNumber)
        {
            seenTicketNumber = game.TicketNumber;
            // Swap artwork before the ticket flies in, never mid-scratch.
            if (ticketArtwork != null && ticketArtworkSprites != null &&
                ticketArtworkSprites.Length >= 3)
                ticketArtwork.sprite =
                    ticketArtworkSprites[Mathf.Clamp((int)game.CurrentTicketKind, 0, ticketArtworkSprites.Length - 1)];
            ApplyCellFrames(game.CurrentTicketKind);
            UpdateTicketTypeLabel(game.CurrentTicketKind);
            StopMotion();
            motion = StartCoroutine(FlyInTicket());
        }

    }

    private void UpdateTicketTypeLabel(ScratchGameManager.TicketKind kind)
    {
        if (ticketTypeLabel != null)
            ticketTypeLabel.text = "BIGLIETTO " + ScratchGameManager.TicketDisplayName(kind);
    }

    private void ApplyCellFrames(ScratchGameManager.TicketKind kind)
    {
        // Diamond temporarily uses the gold frame artwork; its mechanics are unique.
        int index = Mathf.Clamp((int)kind, 0, 2);
        if (cellFrameSprites == null || cellFrameSprites.Length != 3) return;
        ScratchCellManager manager = GetComponent<ScratchCellManager>();
        if (manager == null || manager.HasVintageArt) return;
        foreach (ScratchCell cell in manager.ActiveCells)
        {
            Image frame = cell.GetComponent<Image>();
            if (frame != null) frame.sprite = cellFrameSprites[index];
        }
        appliedCellTicket = index;
    }

    private void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;
    }
}
