using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored, tabbed four-branch skill tree. No runtime UI builders;
/// button, Image and TMP references are all serialized in the Unity scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchSkillTreeUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private ScratchSkillTree skills;
    [SerializeField] private ScratchEconomyManager economy;
    [SerializeField] private ScratchGameManager game;

    [Header("Four tree tabs")]
    [SerializeField] private Button[] branchButtons = new Button[4];
    [SerializeField] private TMP_Text[] branchLabels = new TMP_Text[4];
    [SerializeField] private Image[] branchIcons = new Image[4];

    [Header("Eight nodes of the selected branch")]
    [SerializeField] private Button[] nodeButtons = new Button[8];
    [SerializeField] private TMP_Text[] nodeLabels = new TMP_Text[8];
    [SerializeField] private Image[] nodeIcons = new Image[8];
    // Order: 8 Gratta, 8 Scopri, 8 Incassa, 8 Tempo.
    // Folder names Tickets/Luck are art categories, not gameplay logic.
    [SerializeField] private Sprite[] upgradeSprites = new Sprite[32];

    [Header("Node details and purchase")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedDescription;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseLabel;

    private ScratchSkillTree.Branch selectedBranch;
    private int selectedSlot;
    private bool subscribed;

    private static readonly string[] branchNames =
    {
        "GRATTA!", "SCOPRI!", "INCASSA!", "PIU' TEMPO!"
    };

    private static readonly Color[] branchColors =
    {
        new Color(0.26f, 0.71f, 1f),
        new Color(0.90f, 0.35f, 0.95f),
        new Color(1.00f, 0.72f, 0.29f),
        new Color(0.33f, 0.94f, 0.60f)
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
            branchButtons == null || branchButtons.Length != 4 ||
            nodeButtons == null || nodeButtons.Length != 8 ||
            branchLabels == null || branchLabels.Length != 4 ||
            nodeLabels == null || nodeLabels.Length != 8 || purchaseButton == null)
        {
            Debug.LogError("ScratchSkillTreeUI: incomplete scene references.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            int branch = i;
            branchButtons[i].onClick.AddListener(() => SelectBranch(branch));
        }
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            nodeButtons[i].onClick.AddListener(() => SelectSlot(slot));
        }
        // Purchasing is now performed exclusively via hold gestures on nodes.
        purchaseButton.gameObject.SetActive(false);
        skills.Changed += Refresh;
        economy.Changed += Refresh;
        game.Changed += Refresh;
        subscribed = true;
        Refresh();
    }

    private void OnDestroy()
    {
        if (subscribed)
        {
            skills.Changed -= Refresh;
            economy.Changed -= Refresh;
            game.Changed -= Refresh;
        }
        if (branchButtons != null)
            for (int i = 0; i < branchButtons.Length; i++)
                if (branchButtons[i] != null) branchButtons[i].onClick.RemoveAllListeners();
        if (nodeButtons != null)
            for (int i = 0; i < nodeButtons.Length; i++)
                if (nodeButtons[i] != null) nodeButtons[i].onClick.RemoveAllListeners();
        // The old instant-buy button remains serialized but inactive.
    }

    public void SelectBranch(int branch)
    {
        selectedBranch = (ScratchSkillTree.Branch)Mathf.Clamp(branch, 0, 3);
        selectedSlot = 0;
        Refresh();
    }

    public void SelectSlot(int slot)
    {
        selectedSlot = Mathf.Clamp(slot, 0, 7);
        Refresh();
    }

    public void PurchaseSelected()
    {
        if (skills.TryPurchase(CurrentId))
            Refresh();
    }

    private ScratchSkillTree.SkillId CurrentId =>
        (ScratchSkillTree.SkillId)((int)selectedBranch * 8 + selectedSlot);

    public int BranchIndex => (int)selectedBranch;

    public bool CanHoldPurchase(int slot)
    {
        if (skills == null || slot < 0 || slot >= 8) return false;
        var id = (ScratchSkillTree.SkillId)((int)selectedBranch * 8 + slot);
        return skills.CanPurchase(id);
    }

    public void PurchaseHeldNode(int slot)
    {
        if (!CanHoldPurchase(slot)) return;
        SelectSlot(slot);
        if (skills.TryPurchase(CurrentId)) Refresh();
    }

    public void Refresh()
    {
        if (skills == null || economy == null || game == null ||
            nodeButtons == null || nodeButtons.Length != 8)
            return;

        for (int branch = 0; branch < 4; branch++)
        {
            if (branchIcons != null && branch < branchIcons.Length && branchIcons[branch] != null)
                branchIcons[branch].color = branch == (int)selectedBranch
                    ? Color.white : new Color(1f, 1f, 1f, 0.65f);
            if (branchLabels[branch] != null)
            {
                branchLabels[branch].text = branchNames[branch];
                branchLabels[branch].color = branch == (int)selectedBranch ? new Color(0.42f, 0.19f, 0.10f) : new Color(0.17f, 0.17f, 0.20f);
            }
            if (branchButtons[branch] != null && branchButtons[branch].targetGraphic is Image tabImage)
                tabImage.color = branch == (int)selectedBranch
                    ? new Color(1f, 0.91f, 0.68f) : new Color(0.91f, 0.89f, 0.83f);
        }

        for (int index = 0; index < 8; index++)
        {
            ScratchSkillTree.SkillId id = (ScratchSkillTree.SkillId)((int)selectedBranch * 8 + index);
            ScratchSkillTree.Definition def = ScratchSkillTree.All[(int)id];
            int level = skills.GetLevel(id);
            string state = !def.Implemented ? "IN ARRIVO!"
                : level >= def.MaxLevel ? "AL MASSIMO!"
                : !skills.RequirementsMet(id) ? "DA SBLOCCARE"
                : economy.Coins < skills.GetPrice(id) ? "SERVONO PIU' MONETE"
                : $"TIENI PREMUTO - {skills.GetPrice(id):N0} MONETE";

            if (nodeIcons != null && index < nodeIcons.Length && nodeIcons[index] != null)
            {
                int artworkIndex = (int)id;
                if (upgradeSprites != null && artworkIndex < upgradeSprites.Length)
                    nodeIcons[index].sprite = upgradeSprites[artworkIndex];
                nodeIcons[index].color = !def.Implemented
                    ? new Color(1f, 1f, 1f, 0.38f)
                    : !skills.RequirementsMet(id) ? new Color(1f, 1f, 1f, 0.67f)
                    : Color.white;
            }
            if (nodeLabels[index] != null)
                nodeLabels[index].text = $"{def.Title.ToUpperInvariant()}  {level}/{def.MaxLevel}\n{state}";

            if (nodeButtons[index] != null && nodeButtons[index].targetGraphic is Image background)
            {
                bool selected = index == selectedSlot;
                background.color = selected ? new Color(1f, 0.91f, 0.69f)
                    : level == def.MaxLevel ? new Color(0.85f, 0.94f, 0.79f)
                    : !def.Implemented ? new Color(0.64f, 0.65f, 0.64f)
                    : !skills.RequirementsMet(id) ? new Color(0.79f, 0.78f, 0.75f)
                    : Color.white;
            }
        }

        ScratchSkillTree.Definition current = ScratchSkillTree.All[(int)CurrentId];
        int owned = skills.GetLevel(CurrentId);
        if (selectedTitle != null)
            selectedTitle.text = $"{current.Title.ToUpperInvariant()}   {owned}/{current.MaxLevel}";
        if (selectedDescription != null)
            selectedDescription.text = current.Description + (skills.CanPurchase(CurrentId) ? "\nTIENI PREMUTO PER ACQUISTARE" : string.Empty);

        string status = !current.Implemented ? "PRESTO IN ARRIVO!"
            : owned >= current.MaxLevel ? "POTENZIAMENTO AL MASSIMO!"
            : !skills.RequirementsMet(CurrentId) ? skills.RequirementText(CurrentId)
            : game.State == ScratchGameManager.RoundState.Playing
                ? "FINISCI LA PARTITA E TORNA QUI!"
                : economy.Coins < skills.GetPrice(CurrentId) ? "TI SERVONO PIU' MONETE!"
                : "PUOI PRENDERLO! TIENI PREMUTO.";
        if (statusLabel != null)
            statusLabel.text = status;
        if (purchaseLabel != null)
            purchaseLabel.text = owned >= current.MaxLevel ? "AL MASSIMO!"
                : !current.Implemented ? "IN ARRIVO"
                : $"TIENI PREMUTO  {skills.GetPrice(CurrentId):N0}";
        if (purchaseButton != null)
            purchaseButton.interactable = skills.CanPurchase(CurrentId);
    }
}
