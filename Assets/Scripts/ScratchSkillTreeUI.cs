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

        var all = GetComponentsInChildren<ScratchHoldToBuy>(true);
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
        // Reuse the old authored page indicator as a static color legend.
        // No page navigation or extra UI elements are created.
        if (pageLabel != null)
        {
            pageLabel.gameObject.SetActive(true);
            pageLabel.rectTransform.anchoredPosition = new Vector2(0f, 375f);
            pageLabel.rectTransform.sizeDelta = new Vector2(1020f, 32f);
            pageLabel.fontSize = 17.5f;
            pageLabel.alignment = TextAlignmentOptions.Center;
            pageLabel.raycastTarget = false;
            pageLabel.text =
                "<color=#56A9FF>■ GRATTA</color>    " +
                "<color=#E2B655>■ BIGLIETTI</color>    " +
                "<color=#70D491>■ ECONOMIA</color>    " +
                "<color=#EA8BBC>■ FORTUNA</color>    " +
                "<color=#AC93FF>■ AUTOMAZIONE</color>";
        }
        skills.Changed += Refresh;
        economy.Changed += Refresh;
        game.Changed += Refresh;
        subscribed = true;
        if (purchaseButton != null) purchaseButton.gameObject.SetActive(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (!subscribed) return;
        skills.Changed -= Refresh;
        economy.Changed -= Refresh;
        game.Changed -= Refresh;
        nucleusButton.onClick.RemoveListener(GoToCore);
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
            return "BLOCCATO · NUCLEO CAPITOLO " +
                ScratchRadialProgression.ChapterOf(entry.id);
        if (!skills.RequirementsMet(entry.id))
            return "BLOCCATO · " + skills.RequirementText(entry.id);
        if (economy.Coins < skills.GetPrice(entry.id))
            return "SERVONO " + skills.GetPrice(entry.id).ToString("N0") +
                " MONETE · SALDO " + economy.Coins.ToString("N0");
        return "DISPONIBILE · TIENI PREMUTA L'ICONA (0,7 S)";
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
                "SORVOLA UN'ICONA PER LEGGERE IL POTENZIAMENTO. " +
                "TRASCINA PER ESPLORARE · ROTELLA PER ZOOM · TIENI PREMUTO PER ACQUISTARE.";
            statusLabel.text = "PROSSIMO TRAGUARDO: " +
                ScratchRadialProgression.NextGoal(game, skills);
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
            statusLabel.text = SkillStatus(entry) + " · " + entry.kind.ToUpperInvariant();
        }
        if (purchaseLabel != null) purchaseLabel.text = statusLabel.text;
    }

    public void Refresh()
    {
        if (skills == null || economy == null || game == null) return;
        int chapter = ScratchRadialProgression.UnlockedChapter(game, skills);
        if (nucleusLabel != null)
            nucleusLabel.text = "NUCLEO\nLV " + chapter +
                "\n" + ScratchRadialProgression.ChapterNames[chapter - 1];
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
                Color tint = BranchColors[branch];
                if (unavailable) tint = Color.Lerp(tint, Color.gray, .60f);
                else if (locked) tint = Color.Lerp(tint, Color.gray, .43f);
                else if (level >= entry.maxLevel) tint = Color.Lerp(tint, Color.white, .25f);
                Image backdrop = control.GetComponent<Image>();
                if (backdrop != null) backdrop.color = tint;
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
                        icon.color = unavailable ? new Color(1, 1, 1, .45f)
                            : locked ? new Color(1, 1, 1, .7f) : Color.white;
                    }
                }
                TMP_Text smallLabel = control.GetComponentInChildren<TMP_Text>(true);
                if (smallLabel != null)
                    smallLabel.text = entry.id + (level > 0 ? "  " + level + "/" + entry.maxLevel : "");
            }
        }
        DrawDetails();
    }
}
