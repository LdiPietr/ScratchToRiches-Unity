using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored M5 presentation. Ticket motions, side HUD and the full-screen
/// post-run curtain do not create UI objects at runtime. The run clock is
/// paused between tickets and while a ticket is flying in/out.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchFlowPresentation : MonoBehaviour
{
    private enum Phase
    {
        Idle, TicketEntering, TicketLive, TicketLeaving,
        Choosing, CurtainDescending, CurtainVisible, CurtainAscending
    }

    [Header("Game references")]
    [SerializeField] private ScratchGameManager game;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchTicketChoiceUI ticketChooser;

    [Header("Moving ticket")]
    [SerializeField] private RectTransform ticketTransform;
    [SerializeField] private CanvasGroup ticketCanvasGroup;
    [SerializeField] private Button primaryActionButton;
    [SerializeField, Range(0.1f, 1.2f)] private float ticketTravelDuration = 0.36f;

    [Header("Full-screen skill curtain")]
    [SerializeField] private GameObject curtainObject;
    [SerializeField] private RectTransform curtainRect;
    [SerializeField] private Button continueButton;
    [SerializeField, Range(0.1f, 1.5f)] private float curtainTravelDuration = 0.55f;

    [Header("Side information")]
    [SerializeField] private TMP_Text leftInformation;
    [SerializeField] private TMP_Text rightInformation;

    private Phase phase = Phase.Idle;
    private Vector2 ticketHome;
    private Vector2 curtainHome;
    private int seenTicketNumber;
    private Coroutine motion;
    private bool subscribed;
    private string previousLeft;
    private string previousRight;

    public bool CanScratch => phase == Phase.TicketLive;
    public bool CanShowTicketChoices => phase == Phase.Choosing;

    private void Awake()
    {
        if (game == null) game = GetComponent<ScratchGameManager>();
        if (economy == null) economy = GetComponent<ScratchEconomyManager>();
        if (ticketChooser == null) ticketChooser = GetComponent<ScratchTicketChoiceUI>();
        if (ticketTransform != null) ticketHome = ticketTransform.anchoredPosition;
        if (curtainRect != null) curtainHome = curtainRect.anchoredPosition;

        if (curtainObject != null) curtainObject.SetActive(false);
        if (ticketCanvasGroup != null)
        {
            ticketCanvasGroup.alpha = 1f;
            ticketCanvasGroup.blocksRaycasts = false;
        }
        if (continueButton != null) continueButton.interactable = false;
    }

    private void Start()
    {
        if (game == null || economy == null || ticketChooser == null ||
            ticketTransform == null || ticketCanvasGroup == null ||
            curtainObject == null || curtainRect == null || continueButton == null)
        {
            Debug.LogError("ScratchFlowPresentation: missing scene references.", this);
            enabled = false;
            return;
        }

        game.Changed += HandleGameChanged;
        economy.Changed += UpdateInformation;
        continueButton.onClick.AddListener(ContinueAfterUpgrades);
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
    }

    private void Update()
    {
        UpdateInformation();
    }

    private void HandleGameChanged()
    {
        UpdateInformation();

        if (game.State == ScratchGameManager.RoundState.Finished)
        {
            if (phase == Phase.TicketLeaving ||
                phase == Phase.CurtainDescending ||
                phase == Phase.CurtainVisible ||
                phase == Phase.CurtainAscending)
                return;

            StopMotion();
            if (phase == Phase.Choosing || phase == Phase.Idle)
                motion = StartCoroutine(DescendCurtain());
            else
                motion = StartCoroutine(FlyOutTicket());
            return;
        }

        if (game.State != ScratchGameManager.RoundState.Playing)
            return;

        if (game.TicketActive && seenTicketNumber != game.TicketNumber)
        {
            seenTicketNumber = game.TicketNumber;
            StopMotion();
            motion = StartCoroutine(FlyInTicket());
        }
        else if (!game.TicketActive &&
            (phase == Phase.TicketLive || phase == Phase.TicketEntering))
        {
            StopMotion();
            motion = StartCoroutine(FlyOutTicket());
        }
    }

    private void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;
    }

    private IEnumerator FlyInTicket()
    {
        phase = Phase.TicketEntering;
        ticketChooser.Refresh();
        ticketCanvasGroup.blocksRaycasts = false;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        Vector2 start = ticketHome + new Vector2(1050f, -130f);
        Quaternion startRotation = Quaternion.Euler(0f, 0f, -9f);
        float t = 0f;
        while (t < ticketTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseOut(Mathf.Clamp01(t / ticketTravelDuration));
            ticketTransform.anchoredPosition = Vector2.LerpUnclamped(start, ticketHome, a);
            ticketTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, Quaternion.identity, a);
            ticketTransform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, a);
            ticketCanvasGroup.alpha = a;
            yield return null;
        }

        ticketTransform.anchoredPosition = ticketHome;
        ticketTransform.localRotation = Quaternion.identity;
        ticketTransform.localScale = Vector3.one;
        ticketCanvasGroup.alpha = 1f;
        phase = Phase.TicketLive;
        ticketCanvasGroup.blocksRaycasts = true;
        if (primaryActionButton != null && game.TicketActive &&
            game.State == ScratchGameManager.RoundState.Playing)
            primaryActionButton.interactable = true;
        motion = null;
    }

    private IEnumerator FlyOutTicket()
    {
        phase = Phase.TicketLeaving;
        ticketCanvasGroup.blocksRaycasts = false;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        ticketChooser.Refresh();
        Vector2 start = ticketTransform.anchoredPosition;
        Quaternion startRotation = ticketTransform.localRotation;
        Vector3 startScale = ticketTransform.localScale;
        float startAlpha = ticketCanvasGroup.alpha;
        float t = 0f;

        while (t < ticketTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseIn(Mathf.Clamp01(t / ticketTravelDuration));
            ticketTransform.anchoredPosition = Vector2.LerpUnclamped(start,
                ticketHome + new Vector2(-1050f, 150f), a);
            ticketTransform.localRotation = Quaternion.SlerpUnclamped(startRotation,
                Quaternion.Euler(0f, 0f, 12f), a);
            ticketTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.78f, a);
            ticketCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, a);
            yield return null;
        }

        ticketCanvasGroup.alpha = 0f;
        motion = null;
        if (game.State == ScratchGameManager.RoundState.Finished)
            motion = StartCoroutine(DescendCurtain());
        else if (game.WaitingForTicketChoice)
        {
            phase = Phase.Choosing;
            ticketChooser.Refresh();
        }
        else
            phase = Phase.Idle;
    }

    private IEnumerator DescendCurtain()
    {
        phase = Phase.CurtainDescending;
        if (primaryActionButton != null) primaryActionButton.interactable = false;
        ticketChooser.Refresh();
        curtainObject.SetActive(true);
        continueButton.interactable = false;
        curtainRect.anchoredPosition = curtainHome + new Vector2(0f, 1200f);
        float t = 0f;
        while (t < curtainTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseOut(Mathf.Clamp01(t / curtainTravelDuration));
            curtainRect.anchoredPosition = Vector2.LerpUnclamped(
                curtainHome + new Vector2(0f, 1200f), curtainHome, a);
            yield return null;
        }
        curtainRect.anchoredPosition = curtainHome;
        phase = Phase.CurtainVisible;
        continueButton.interactable = true;
        motion = null;
    }

    public void ContinueAfterUpgrades()
    {
        if (phase != Phase.CurtainVisible ||
            game.State != ScratchGameManager.RoundState.Finished)
            return;

        StopMotion();
        motion = StartCoroutine(RaiseCurtainAndRestart());
    }

    private IEnumerator RaiseCurtainAndRestart()
    {
        phase = Phase.CurtainAscending;
        continueButton.interactable = false;
        float t = 0f;
        while (t < curtainTravelDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = EaseIn(Mathf.Clamp01(t / curtainTravelDuration));
            curtainRect.anchoredPosition = Vector2.LerpUnclamped(
                curtainHome, curtainHome + new Vector2(0f, 1200f), a);
            yield return null;
        }
        curtainObject.SetActive(false);
        curtainRect.anchoredPosition = curtainHome;
        phase = Phase.Idle;
        motion = null;
        // Ticket numbering restarts from 1 on each run. Without resetting
        // this marker, a single-ticket previous run would skip the new entry.
        seenTicketNumber = 0;
        game.StartNewRun();
    }

    private void UpdateInformation()
    {
        if (game == null || economy == null) return;
        bool clockPaused = game.State == ScratchGameManager.RoundState.Playing &&
            (!game.TicketActive || !CanScratch);
        string left = $"{(clockPaused ? "TEMPO IN PAUSA" : "TEMPO RESIDUO")}\n" +
            $"{game.TimeRemaining:0.0} s\n\n" +
            $"BIGLIETTO  {game.TicketNumber}\n" +
            $"COMPLETATI  {game.TicketsCompleted}\n\n" +
            $"SCELTE  {game.SelectedCount}/{game.MaxScratches}\n" +
            $"RIVELATE  {game.RevealedCount}/{game.MaxScratches}";
        string right = $"IL TUO BIGLIETTO\n{game.CurrentTicketKind.ToString().ToUpperInvariant()}\n\n" +
            $"PREMIO BASE  {game.TicketPrize(game.CurrentTicketKind)}\n" +
            $"ULTIMA VINCITA  +{game.TicketPayout}\n" +
            $"TEMPO GUADAGNATO  +{game.LastTimeBonus:0.0}s\n\n" +
            $"PORTAFOGLIO  {economy.Coins:N0}";

        if (leftInformation != null && left != previousLeft)
        {
            previousLeft = left;
            leftInformation.text = left;
        }
        if (rightInformation != null && right != previousRight)
        {
            previousRight = right;
            rightInformation.text = right;
        }
    }

    private static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseIn(float x) => x * x * x;
}
