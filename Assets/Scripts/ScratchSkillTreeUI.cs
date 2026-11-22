using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GDD V2: five authored branch tabs and eight authored reusable slot views.
/// Pages expose all 90 catalog nodes without creating UI at runtime.
/// Holding a node purchases only an implemented, demo-eligible effect.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchSkillTreeUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private ScratchSkillTree skills;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchGameManager game;

    [Header("Five authored tree tabs")]
    [SerializeField] private Button[] branchButtons = new Button[5];
    [SerializeField] private TMP_Text[] branchLabels = new TMP_Text[5];
    [SerializeField] private Image[] branchIcons = new Image[5];

    [Header("Eight reusable authored slots per page")]
    [SerializeField] private Button[] nodeButtons = new Button[8];
    [SerializeField] private TMP_Text[] nodeLabels = new TMP_Text[8];
    [SerializeField] private Image[] nodeIcons = new Image[8];
    [SerializeField] private Sprite[] upgradeSprites = new Sprite[32];

    [Header("Authored page navigation")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageLabel;

    [Header("Authored details and purchase display")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedDescription;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseLabel;

    private int branchIndex;
    private int pageIndex;
    private int selectedSlot;
    private bool subscribed;
    private const int PageSize = 8;

    private static readonly Color[] branchColors =
    {
        new Color(0.26f, 0.71f, 1.00f),
        new Color(0.90f, 0.35f, 0.95f),
        new Color(1.00f, 0.72f, 0.29f),
        new Color(0.33f, 0.94f, 0.60f),
        new Color(0.63f, 0.70f, 0.96f)
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
            branchButtons == null || branchButtons.Length != 5 ||
            branchLabels == null || branchLabels.Length != 5 ||
            nodeButtons == null || nodeButtons.Length != PageSize ||
            nodeLabels == null || nodeLabels.Length != PageSize ||
            previousPageButton == null || nextPageButton == null || pageLabel == null)
        {
            Debug.LogError("ScratchSkillTreeUI V2: missing authored tab/page references; run the scene audit.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < ScratchSkillCatalogV2.BranchCount; i++)
        {
            int branch = i;
            branchButtons[i].onClick.AddListener(() => SelectBranch(branch));
        }
        for (int i = 0; i < PageSize; i++)
        {
            int slot = i;
            nodeButtons[i].onClick.AddListener(() => SelectSlot(slot));
        }
        previousPageButton.onClick.AddListener(PreviousPage);
        nextPageButton.onClick.AddListener(NextPage);
        if (purchaseButton != null) purchaseButton.gameObject.SetActive(false);
        skills.Changed += Refresh;
        economy.Changed += Refresh;
        game.Changed += Refresh;
        subscribed = true;
        Refresh();
    }

    private void OnDestroy()
    {
        if (!subscribed) return;
        skills.Changed -= Refresh;
        economy.Changed -= Refresh;
        game.Changed -= Refresh;
        for (int i = 0; i < branchButtons.Length; i++)
            if (branchButtons[i] != null) branchButtons[i].onClick.RemoveAllListeners();
        for (int i = 0; i < nodeButtons.Length; i++)
            if (nodeButtons[i] != null) nodeButtons[i].onClick.RemoveAllListeners();
        if (previousPageButton != null) previousPageButton.onClick.RemoveAllListeners();
        if (nextPageButton != null) nextPageButton.onClick.RemoveAllListeners();
    }

    public int BranchIndex => branchIndex;
    public int PageIndex => pageIndex;

    public void SelectBranch(int branch)
    {
        branchIndex = Mathf.Clamp(branch, 0, ScratchSkillCatalogV2.BranchCount - 1);
        pageIndex = 0;
        selectedSlot = 0;
        Refresh();
    }

    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= PageSize || EntryForSlot(slot) == null) return;
        selectedSlot = slot;
        Refresh();
    }

    private int PageCount => (ScratchSkillCatalogV2.CountForBranch(branchIndex) + PageSize - 1) / PageSize;

    public void PreviousPage()
    {
        if (pageIndex <= 0) return;
        pageIndex--;
        selectedSlot = 0;
        Refresh();
    }

    public void NextPage()
    {
        if (pageIndex >= PageCount - 1) return;
        pageIndex++;
        selectedSlot = 0;
        Refresh();
    }

    private ScratchSkillCatalogV2.Entry EntryForSlot(int slot) =>
        ScratchSkillCatalogV2.At(branchIndex, pageIndex * PageSize + slot);

    private ScratchSkillCatalogV2.Entry SelectedEntry => EntryForSlot(selectedSlot);

    public bool CanHoldPurchase(int slot)
    {
        ScratchSkillCatalogV2.Entry entry = slot >= 0 && slot < PageSize ? EntryForSlot(slot) : null;
        return entry != null && skills != null && skills.CanPurchase(entry.id);
    }

    public void PurchaseHeldNode(int slot)
    {
        if (!CanHoldPurchase(slot)) return;
        SelectSlot(slot);
        ScratchSkillCatalogV2.Entry entry = SelectedEntry;
        if (entry != null && skills.TryPurchase(entry.id)) Refresh();
    }

    public void PurchaseSelected()
    {
        ScratchSkillCatalogV2.Entry entry = SelectedEntry;
        if (entry != null && skills.TryPurchase(entry.id)) Refresh();
    }

    private string Status(ScratchSkillCatalogV2.Entry entry, int level)
    {
        if (entry == null) return string.Empty;
        if (ScratchSkillTree.DemoEdition && !entry.demoEligible)
            return "VERSIONE COMPLETA";
        if (!ScratchSkillCatalogV2.EffectImplemented(entry.id))
            return "NON ANCORA IMPLEMENTATO";
        if (level >= entry.maxLevel) return "AL MASSIMO";
        if (!skills.RequirementsMet(entry.id)) return "REQUISITI MANCANTI";
        if (economy.Coins < skills.GetPrice(entry.id)) return "MONETE INSUFFICIENTI";
        return "TIENI PREMUTO - " + skills.GetPrice(entry.id).ToString("N0");
    }

    public void Refresh()
    {
        if (skills == null || economy == null || game == null ||
            nodeButtons == null || nodeButtons.Length != PageSize)
            return;
        for (int b = 0; b < ScratchSkillCatalogV2.BranchCount; b++)
        {
            if (branchLabels != null && b < branchLabels.Length && branchLabels[b] != null)
            {
                branchLabels[b].text = ScratchSkillCatalogV2.BranchNames[b];
                branchLabels[b].color = b == branchIndex
                    ? new Color(0.30f, 0.19f, 0.10f) : new Color(0.17f, 0.17f, 0.20f);
            }
            if (branchIcons != null && b < branchIcons.Length && branchIcons[b] != null)
                branchIcons[b].color = b == branchIndex ? Color.white :
                    new Color(1f, 1f, 1f, 0.65f);
            if (branchButtons[b] != null && branchButtons[b].targetGraphic is Image image)
                image.color = b == branchIndex
                    ? new Color(1f, 0.91f, 0.68f)
                    : new Color(0.91f, 0.89f, 0.83f);
        }

        for (int i = 0; i < PageSize; i++)
        {
            ScratchSkillCatalogV2.Entry entry = EntryForSlot(i);
            bool exists = entry != null;
            if (nodeButtons[i] != null)
                nodeButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            int level = skills.GetLevel(entry.id);
            bool fullOnly = ScratchSkillTree.DemoEdition && !entry.demoEligible;
            bool implemented = ScratchSkillCatalogV2.EffectImplemented(entry.id);
            bool requirements = skills.RequirementsMet(entry.id);
            if (nodeLabels[i] != null)
                nodeLabels[i].text = entry.id + "  " + entry.title.ToUpperInvariant() +
                    "  " + level + "/" + entry.maxLevel + "\n" + Status(entry, level);
            if (nodeIcons != null && i < nodeIcons.Length && nodeIcons[i] != null)
            {
                // Temporary scene-authored placeholders. The GDD's 90 unique
                // artworks will replace these before publishing the demo.
                int artIndex = branchIndex == 4 ? 7 :
                    branchIndex * 8 + (pageIndex * PageSize + i) % 8;
                if (upgradeSprites != null && artIndex >= 0 &&
                    artIndex < upgradeSprites.Length)
                    nodeIcons[i].sprite = upgradeSprites[artIndex];
                nodeIcons[i].color = fullOnly
                    ? new Color(1f, 1f, 1f, 0.42f)
                    : !implemented ? new Color(1f, 1f, 1f, 0.60f)
                    : !requirements ? new Color(1f, 1f, 1f, 0.72f)
                    : Color.white;
            }
            if (nodeButtons[i] != null && nodeButtons[i].targetGraphic is Image background)
                background.color = i == selectedSlot
                    ? new Color(1f, 0.91f, 0.69f)
                    : fullOnly ? new Color(0.67f, 0.68f, 0.72f)
                    : !implemented ? new Color(0.85f, 0.80f, 0.71f)
                    : level >= entry.maxLevel ? new Color(0.85f, 0.94f, 0.79f)
                    : !requirements ? new Color(0.79f, 0.78f, 0.75f)
                    : Color.white;
        }

        if (pageLabel != null)
            pageLabel.text = "PAGINA " + (pageIndex + 1) + "/" + PageCount +
                "   |   " + ScratchSkillCatalogV2.CountForBranch(branchIndex) + " ABILITA'";
        if (previousPageButton != null) previousPageButton.interactable = pageIndex > 0;
        if (nextPageButton != null) nextPageButton.interactable = pageIndex < PageCount - 1;

        ScratchSkillCatalogV2.Entry selected = SelectedEntry;
        if (selected == null) return;
        int owned = skills.GetLevel(selected.id);
        if (selectedTitle != null)
            selectedTitle.text = selected.id + "   " + selected.title.ToUpperInvariant() +
                "   " + owned + "/" + selected.maxLevel;
        if (selectedDescription != null)
        {
            selectedDescription.text = selected.description;
            if (selected.id == "G04")
                selectedDescription.text += "\nTasto F: attiva/disattiva il ventaglio.";
            else if (selected.id == "G08")
                selectedDescription.text += "\nSHIFT + grattatura: usa una carica Titanico.";
            else if (selected.id == "F04")
                selectedDescription.text += "\nIl JOLLY vale solo se grattato, non basta vederlo.";
        }
        if (statusLabel != null)
        {
            string status = Status(selected, owned);
            if (status == "REQUISITI MANCANTI")
                status = skills.RequirementText(selected.id);
            statusLabel.text = status;
        }
        if (purchaseLabel != null)
            purchaseLabel.text = Status(selected, owned);
        if (purchaseButton != null)
            purchaseButton.interactable = skills.CanPurchase(selected.id);
    }
}
