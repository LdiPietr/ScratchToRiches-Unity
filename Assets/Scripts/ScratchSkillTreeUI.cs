using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EXPERIMENTAL: one navigable, scroll-zoomable map with 90 scene-authored
/// square skill icons. Every hover displays full details in the fixed panel
/// beneath the map; holding the hovered icon buys exactly one level.
/// This component never instantiates any UI or GameObject at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchSkillTreeUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private ScratchSkillTree skills;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchGameManager game;

    // Keep old scene references serialized but inactive. Branch navigation and
    // pagination have been replaced with one 90-node authored map.
    [Header("Legacy authored controls (inactive in map mode)")]
    [SerializeField] private Button[] branchButtons = new Button[5];
    [SerializeField] private TMP_Text[] branchLabels = new TMP_Text[5];
    [SerializeField] private Image[] branchIcons = new Image[5];
    [SerializeField] private Button[] nodeButtons = new Button[8];
    [SerializeField] private TMP_Text[] nodeLabels = new TMP_Text[8];
    [SerializeField] private Image[] nodeIcons = new Image[8];
    [SerializeField] private Sprite[] upgradeSprites = new Sprite[32];
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageLabel;

    [Header("Fixed hover information panel")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedDescription;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseLabel;

    [Header("Authored map")]
    [SerializeField] private Button nucleusButton;
    [SerializeField] private TMP_Text nucleusLabel;
    [SerializeField] private ScratchRadialLinks radialLinks;
    [SerializeField] private ScratchRadialMapNavigation navigation;

    private ScratchHoldToBuy[] authoredMapNodes;
    private readonly Dictionary<string, ScratchHoldToBuy> byId =
        new Dictionary<string, ScratchHoldToBuy>(StringComparer.Ordinal);
    private string hoveredId;
    private int branchIndex;
    private bool subscribed;
    // All 90 authored nodes remain visible; this index is ONLY a navigation aid.
    private readonly List<string> purchasableIds = new List<string>(25);
    private int availableFocusIndex = -1;
    private Button availableJumpButton;
    private ScratchFlowPresentation flow;

    private static readonly Color[] BranchColors =
    {
        new Color(0.28f, 0.58f, 0.87f), // Grattatura: blue
        new Color(0.88f, 0.66f, 0.31f), // Biglietti: gold
        new Color(0.31f, 0.75f, 0.48f), // Economia: green
        new Color(0.83f, 0.39f, 0.64f), // Fortuna: magenta
        new Color(0.55f, 0.48f, 0.85f)  // Automazione: violet
    };

    private void Awake()
    {
        if (skills == null) skills = GetComponent<ScratchSkillTree>();
        if (economy == null) economy = GetComponent<ScratchEconomyManager>();
        if (game == null) game = GetComponent<ScratchGameManager>();
    }

    private void Start()
    {
        if (skills == null || economy == null || game == null ||
            navigation == null || nucleusButton == null || nucleusLabel == null ||
            selectedTitle == null || selectedDescription == null || statusLabel == null)
        {
            Debug.LogError("Scratch radial map: missing authored references. Run Audit V2 Scene.", this);
            enabled = false;
            return;
        }

        // The map is under the shop Canvas, not under the GameManager
        // transform that holds this script. Search the explicitly assigned
        // authored viewport, otherwise zero controls are discovered.
        var all = navigation.GetComponentsInChildren<ScratchHoldToBuy>(true);
        var nodes = new List<ScratchHoldToBuy>(90);
        foreach (ScratchHoldToBuy control in all)
        {
            if (string.IsNullOrEmpty(control.StableSkillId)) continue;
            if (byId.ContainsKey(control.StableSkillId))
            {
                Debug.LogError("Duplicate radial skill control: " + control.StableSkillId, control);
                enabled = false;
                return;
            }
            byId.Add(control.StableSkillId, control);
            nodes.Add(control);
        }
        authoredMapNodes = nodes.ToArray();
        if (authoredMapNodes.Length != 90)
        {
            Debug.LogError("Scratch radial map: expected 90 authored icon controls, found " +
                authoredMapNodes.Length, this);
            enabled = false;
            return;
        }

        nucleusButton.onClick.AddListener(GoToCore);
        flow = game.GetComponent<ScratchFlowPresentation>();
        // Reuse the already-authored top label as a CLICKABLE availability
        // navigator. Adds no GameObject, no extra node and no new scene asset.
        if (pageLabel != null)
        {
            pageLabel.gameObject.SetActive(true);
            pageLabel.rectTransform.anchoredPosition = new Vector2(0f, 375f);
            pageLabel.rectTransform.sizeDelta = new Vector2(1020f, 50f);
            pageLabel.fontSize = 21f;
            pageLabel.alignment = TextAlignmentOptions.Center;
            pageLabel.raycastTarget = true;
            pageLabel.color = new Color(0.12f, 0.36f, 0.23f, 1f);
            availableJumpButton = pageLabel.GetComponent<Button>();
            if (availableJumpButton == null)
                availableJumpButton = pageLabel.gameObject.AddComponent<Button>();
            availableJumpButton.transition = Selectable.Transition.None;
            availableJumpButton.targetGraphic = pageLabel;
            availableJumpButton.onClick.AddListener(JumpToNextPurchase);
        }
        // The original fixed info panel was too small in recorded human play.
        // Adaptive sizing preserves the authored layout without replacing it.
        selectedTitle.enableAutoSizing = true;
        selectedTitle.fontSizeMax = 22f;
        selectedTitle.fontSizeMin = 16f;
        selectedDescription.enableAutoSizing = true;
        selectedDescription.fontSizeMax = 18f;
        selectedDescription.fontSizeMin = 13f;
        statusLabel.enableAutoSizing = true;
        statusLabel.fontSizeMax = 20f;
        statusLabel.fontSizeMin = 14f;
        skills.Changed += Refresh;
        economy.Changed += Refresh;
        game.Changed += Refresh;
        subscribed = true;
        if (purchaseButton != null) purchaseButton.gameObject.SetActive(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (availableJumpButton != null)
            availableJumpButton.onClick.RemoveListener(JumpToNextPurchase);
        if (!subscribed) return;
        skills.Changed -= Refresh;
        economy.Changed -= Refresh;
        game.Changed -= Refresh;
        nucleusButton.onClick.RemoveListener(GoToCore);
    }

    private void Update()
    {
        // Keyboard shortcut is active only when the shop is actually visible.
        if (flow != null && flow.ShopVisible && Input.GetKeyDown(KeyCode.Tab))
            JumpToNextPurchase();
    }

    /// <summary>
    /// Find the next actually affordable node (not merely chapter-unlocked).
    /// Never purchases automatically. Reuses the original radial camera.
    /// The top banner is also clickable for players who do not use hotkeys.
    /// </summary>
    public void JumpToNextPurchase()
    {
        if (purchasableIds.Count == 0) return;
        int currentlySelected = purchasableIds.IndexOf(hoveredId);
        if (currentlySelected >= 0) availableFocusIndex = currentlySelected;
        availableFocusIndex = (availableFocusIndex + 1) % purchasableIds.Count;
        string id = purchasableIds[availableFocusIndex];
        // A map zoomed out to show all 90 nodes makes the target unreadable.
        // Restore a practical inspection zoom ONLY when using the finder.
        navigation.SetZoom(Mathf.Max(navigation.Zoom, 0.95f), Vector2.zero);
        RevealSkill(id);
#if UNITY_EDITOR
        ScratchManualTelemetry.Emit("AVAILABLE_JUMP", "id=" + id +
            "|available=" + purchasableIds.Count);
#endif
    }

    private static bool IsMajorUnlock(string id) =>
        id == "C03" || id == "C05" || id == "G08" || id == "F04";

    // New skills first, major unlocks ahead of extra levels. No forced build.
    private int ComparePurchasable(string a, string b)
    {
        int newA = skills.GetLevel(a) == 0 ? 0 : 1;
        int newB = skills.GetLevel(b) == 0 ? 0 : 1;
        if (newA != newB) return newA.CompareTo(newB);
        int mainA = IsMajorUnlock(a) ? 0 : 1;
        int mainB = IsMajorUnlock(b) ? 0 : 1;
        if (mainA != mainB) return mainA.CompareTo(mainB);
        return string.CompareOrdinal(a, b);
    }

    // Compatibility APIs for legacy editor tools; no page or tab changes.
    public int BranchIndex => branchIndex;
    public int PageIndex => 0;
    public bool RadialOverview => true;
    public void OpenOverview() => GoToCore();
    public void SelectBranch(int branch) => branchIndex = Mathf.Clamp(branch, 0, 4);
    public void PreviousPage() { }
    public void NextPage() { }

    public void SelectSlot(int slot)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.At(branchIndex, slot);
        if (entry != null) HoverSkill(entry.id);
    }

    public bool CanHoldPurchase(int slot)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.At(branchIndex, slot);
        return entry != null && CanPurchaseSkill(entry.id);
    }

    public void PurchaseHeldNode(int slot)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.At(branchIndex, slot);
        if (entry != null) PurchaseHeldSkill(entry.id);
    }

    public void PurchaseSelected()
    {
        if (!string.IsNullOrEmpty(hoveredId)) PurchaseHeldSkill(hoveredId);
    }

    public bool CanPurchaseSkill(string id) =>
        skills != null && skills.CanPurchase(id);

    public void PurchaseHeldSkill(string id)
    {
        if (!CanPurchaseSkill(id)) return;
        if (skills.TryPurchase(id)) HoverSkill(id);
    }

    public void HoverSkill(string id)
    {
        if (ScratchSkillCatalogV2.Find(id) == null) return;
#if UNITY_EDITOR
        if (hoveredId != id)
            ScratchManualTelemetry.Emit("SKILL_HOVER", "id=" + id);
#endif
        hoveredId = id;
        DrawDetails();
    }

    public void ClearHoverSkill(string id)
    {
        if (hoveredId != id) return;
        hoveredId = null;
        DrawDetails();
    }

    public void RevealSkill(string id)
    {
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(id);
        if (entry == null) return;
        hoveredId = id;
        branchIndex = BranchOf(id);
        navigation.CenterOn(ScratchRadialLinks.PositionOf(id));
        DrawDetails();
    }

    private void GoToCore()
    {
        hoveredId = null;
        availableFocusIndex = -1;
        navigation.CenterOn(Vector2.zero);
        DrawDetails();
    }

    private static int BranchOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        switch (id[0])
        {
            case 'G': return 0;
            case 'C': return 1;
            case 'E': return 2;
            case 'F': return 3;
            case 'A': return 4;
            default: return 0;
        }
    }

    private string SkillStatus(ScratchSkillCatalogV2.Entry entry)
    {
        int level = skills.GetLevel(entry.id);
        if (ScratchSkillTree.DemoEdition && !entry.demoEligible)
            return "VERSIONE COMPLETA · VISIBILE, NON ACQUISTABILE";
        if (!ScratchSkillCatalogV2.EffectImplemented(entry.id))
            return "EFFETTO NON ANCORA IMPLEMENTATO";
        if (level >= entry.maxLevel) return "POTENZIAMENTO COMPLETATO";
        if (!ScratchRadialProgression.Available(entry.id, game, skills))
            return "BLOCCATO · " +
                ScratchRadialProgression.SkillGoal(entry.id, game, skills);
        if (!skills.RequirementsMet(entry.id))
            return "BLOCCATO · " + skills.RequirementText(entry.id);
        if (economy.Coins < skills.GetPrice(entry.id))
            return "SERVONO " + skills.GetPrice(entry.id).ToString("N0") +
                " MONETE · SALDO " + economy.Coins.ToString("N0");
        return "ACQUISTABILE · COSTO " + skills.GetPrice(entry.id).ToString("N0") +
            " MONETE · TIENI PREMUTO 0,7 S";
    }

    private void DrawDetails()
    {
        if (skills == null || economy == null || game == null ||
            selectedTitle == null || selectedDescription == null || statusLabel == null)
            return;
        ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(hoveredId);
        if (entry == null)
        {
            int chapter = ScratchRadialProgression.UnlockedChapter(game, skills);
            selectedTitle.text = "NUCLEO · CAPITOLO " + chapter +
                " · " + ScratchRadialProgression.ChapterNames[chapter - 1];
            selectedDescription.text =
                "BORDO VERDE = PUOI COMPRARE. CLICCA LA BARRA IN ALTO " +
                "OPPURE PREMI TAB PER RAGGIUNGERE IL PROSSIMO NODO ACQUISTABILE. " +
                "TIENI PREMUTO L'ICONA PER 0,7 S.";
            if (purchasableIds.Count > 0)
            {
                string nextId = purchasableIds[0];
                ScratchSkillCatalogV2.Entry next = ScratchSkillCatalogV2.Find(nextId);
                statusLabel.text = "ACQUISTABILI: " + purchasableIds.Count +
                    " · PROSSIMO: " + nextId + " " + next.title.ToUpperInvariant() +
                    " · " + skills.GetPrice(nextId).ToString("N0") + " MONETE";
                statusLabel.color = new Color(0.39f, 1f, 0.46f);
            }
            else
            {
                statusLabel.text = "NESSUN ACQUISTO ORA · " +
                    ScratchRadialProgression.NextGoal(game, skills);
                statusLabel.color = new Color(1f, 0.79f, 0.38f);
            }
        }
        else
        {
            int level = skills.GetLevel(entry.id);
            selectedTitle.text = entry.id + " · " + entry.title.ToUpperInvariant() +
                " · LV " + level + "/" + entry.maxLevel +
                " · " + entry.branch.ToUpperInvariant();
            selectedDescription.text = entry.description;
            if (entry.id == "G04") selectedDescription.text += "  [F: VENTAGLIO]";
            if (entry.id == "G08") selectedDescription.text += "  [SHIFT + GRATTA: TITANICO]";
            if (entry.id == "C06") selectedDescription.text += "  [DESTRO/CTRL+CLIC: ISPEZIONA]";
            string selectedStatus = SkillStatus(entry);
            bool selectedBuy = skills.CanPurchase(entry.id);
            // Remind the player of their actual affordable next step EVEN
            // when inspecting a locked or already-completed node.
            if (!selectedBuy && purchasableIds.Count > 0)
            {
                string id = purchasableIds[0];
                statusLabel.text = "● ORA PUOI COMPRARE " + id +
                    " (" + skills.GetPrice(id).ToString("N0") +
                    ") · TAB PER TROVARLO  |  " + selectedStatus;
            }
            else
                statusLabel.text = selectedStatus;
            statusLabel.color = selectedBuy || purchasableIds.Count > 0
                ? new Color(0.39f, 1f, 0.46f)
                : skills.GetLevel(entry.id) >= entry.maxLevel
                    ? new Color(0.49f, 0.83f, 1f)
                    : new Color(1f, 0.79f, 0.38f);
        }
        statusLabel.fontStyle = FontStyles.Bold;
        if (purchaseLabel != null) purchaseLabel.text = statusLabel.text;
    }

    public void Refresh()
    {
        if (skills == null || economy == null || game == null) return;
        int chapter = ScratchRadialProgression.UnlockedChapter(game, skills);
        if (nucleusLabel != null)
            nucleusLabel.text = "NUCLEO\nLV " + chapter +
                "\n" + ScratchRadialProgression.ChapterNames[chapter - 1];
        purchasableIds.Clear();
        foreach (ScratchSkillCatalogV2.Entry e in ScratchSkillCatalogV2.All)
            if (skills.CanPurchase(e.id)) purchasableIds.Add(e.id);
        purchasableIds.Sort(ComparePurchasable);
        if (availableFocusIndex >= purchasableIds.Count)
            availableFocusIndex = -1;

        if (pageLabel != null)
        {
            if (purchasableIds.Count == 0)
                pageLabel.text = "<color=#AB6222>NESSUN ACQUISTO ORA</color>  ·  " +
                    ScratchRadialProgression.NextGoal(game, skills) +
                    "  ·  VERDE = ACQUISTABILE";
            else
            {
                string id = purchasableIds[0];
                ScratchSkillCatalogV2.Entry next = ScratchSkillCatalogV2.Find(id);
                pageLabel.text = "● " + purchasableIds.Count +
                    " ACQUISTABILI   ·   " + id + " " +
                    next.title.ToUpperInvariant() + " · " +
                    skills.GetPrice(id).ToString("N0") +
                    " MONETE   ·   CLICCA QUI / TAB PER TROVARLE";
            }
            if (availableJumpButton != null)
                availableJumpButton.interactable = purchasableIds.Count > 0;
        }

        if (authoredMapNodes != null)
        {
            foreach (ScratchHoldToBuy control in authoredMapNodes)
            {
                ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.Find(control.StableSkillId);
                if (entry == null) continue;
                int branch = BranchOf(entry.id);
                int level = skills.GetLevel(entry.id);
                bool unavailable = ScratchSkillTree.DemoEdition && !entry.demoEligible;
                bool locked = !ScratchRadialProgression.Available(entry.id, game, skills) ||
                    !skills.RequirementsMet(entry.id);
                bool canBuy = purchasableIds.Contains(entry.id);
                bool completed = level >= entry.maxLevel;
                Color tint = BranchColors[branch];
                // Preserve branch identity, while making affordance unambiguous.
                if (unavailable) tint = Color.Lerp(tint, Color.black, .76f);
                else if (locked) tint = Color.Lerp(tint, Color.gray, .66f);
                else if (completed) tint = Color.Lerp(tint, Color.white, .30f);
                else if (canBuy) tint = Color.Lerp(tint, Color.white, .16f);
                Image backdrop = control.GetComponent<Image>();
                if (backdrop != null)
                {
                    backdrop.color = tint;
                    // A bright green border, not a different branch-colored icon.
                    // Add only a UI effect to an existing authored square node.
                    Outline outline = backdrop.GetComponent<Outline>();
                    if (canBuy && outline == null)
                        outline = backdrop.gameObject.AddComponent<Outline>();
                    if (outline != null)
                    {
                        outline.enabled = canBuy;
                        if (canBuy)
                        {
                            outline.effectColor = new Color(0.25f, 1f, 0.32f, 1f);
                            outline.effectDistance = new Vector2(4f, 4f);
                            outline.useGraphicAlpha = false;
                        }
                    }
                }
                Transform iconTransform = control.transform.Find("Icon");
                if (iconTransform != null)
                {
                    Image icon = iconTransform.GetComponent<Image>();
                    if (icon != null)
                    {
                        int ord = 1;
                        if (entry.id.Length >= 2)
                            int.TryParse(entry.id.Substring(1), out ord);
                        int artIndex = branch == 4 ? 7 :
                            branch * 8 + Mathf.Clamp((ord - 1) % 8, 0, 7);
                        if (upgradeSprites != null && artIndex < upgradeSprites.Length)
                            icon.sprite = upgradeSprites[artIndex];
                        icon.color = unavailable ? new Color(1, 1, 1, .24f)
                            : locked ? new Color(1, 1, 1, .54f)
                            : Color.white;
                    }
                }
                TMP_Text smallLabel = control.GetComponentInChildren<TMP_Text>(true);
                if (smallLabel != null)
                {
                    smallLabel.text = (canBuy ? "● " : "") + entry.id +
                        (level > 0 ? "  " + level + "/" + entry.maxLevel : "");
                    smallLabel.color = canBuy ? new Color(.27f, 1f, .41f)
                        : unavailable ? new Color(.61f, .62f, .65f)
                        : Color.white;
                    smallLabel.fontStyle = canBuy ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }
        DrawDetails();
    }
}
