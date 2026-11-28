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
public sealed partial class ScratchSkillTreeUI : MonoBehaviour
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
    [SerializeField] private Button availableJumpButton;
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
        // The availability navigator and typography are serialized in the scene.
        if (availableJumpButton == null || pageLabel == null ||
            availableJumpButton.gameObject != pageLabel.gameObject)
        {
            Debug.LogError("Scratch radial map: missing authored availability button.", this);
            nucleusButton.onClick.RemoveListener(GoToCore);
            enabled = false;
            return;
        }
        availableJumpButton.onClick.AddListener(JumpToNextPurchase);
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
}
