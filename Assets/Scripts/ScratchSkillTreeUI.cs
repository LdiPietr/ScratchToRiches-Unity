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

    [Header("Eight nodes of the selected branch")]
    [SerializeField] private Button[] nodeButtons = new Button[8];
    [SerializeField] private TMP_Text[] nodeLabels = new TMP_Text[8];

    [Header("Node details and purchase")]
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedDescription;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseLabel;

    private ScratchSkillTree.Branch selectedBranch;
    private int selectedSlot;
    private bool subscribed;

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
        purchaseButton.onClick.AddListener(PurchaseSelected);
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
        if (purchaseButton != null)
            purchaseButton.onClick.RemoveListener(PurchaseSelected);
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

    public void Refresh()
    {
        if (skills == null || economy == null || game == null ||
            nodeButtons == null || nodeButtons.Length != 8)
            return;

        for (int branch = 0; branch < 4; branch++)
        {
            if (branchLabels[branch] != null)
            {
                branchLabels[branch].text = ((ScratchSkillTree.Branch)branch).ToString().ToUpperInvariant();
                branchLabels[branch].color = branch == (int)selectedBranch ? branchColors[branch] : Color.white;
            }
            if (branchButtons[branch] != null && branchButtons[branch].targetGraphic is Image tabImage)
                tabImage.color = branch == (int)selectedBranch
                    ? new Color(0.22f, 0.24f, 0.40f) : new Color(0.10f, 0.13f, 0.22f);
        }

        for (int index = 0; index < 8; index++)
        {
            ScratchSkillTree.SkillId id = (ScratchSkillTree.SkillId)((int)selectedBranch * 8 + index);
            ScratchSkillTree.Definition def = ScratchSkillTree.All[(int)id];
            int level = skills.GetLevel(id);
            string state = !def.Implemented ? "IN ARRIVO"
                : level >= def.MaxLevel ? "COMPLETATO"
                : !skills.RequirementsMet(id) ? "BLOCCATO"
                : $"{skills.GetPrice(id)} MONETE";

            if (nodeLabels[index] != null)
                nodeLabels[index].text = $"{def.Id}  {def.Title.ToUpperInvariant()}   Lv {level}/{def.MaxLevel}\n{state}";

            if (nodeButtons[index] != null && nodeButtons[index].targetGraphic is Image background)
            {
                bool selected = index == selectedSlot;
                background.color = selected ? new Color(0.22f, 0.28f, 0.40f)
                    : level == def.MaxLevel ? new Color(0.11f, 0.29f, 0.21f)
                    : !def.Implemented ? new Color(0.12f, 0.12f, 0.17f)
                    : new Color(0.12f, 0.16f, 0.27f);
            }
        }

        ScratchSkillTree.Definition current = ScratchSkillTree.All[(int)CurrentId];
        int owned = skills.GetLevel(CurrentId);
        if (selectedTitle != null)
            selectedTitle.text = $"{current.Title.ToUpperInvariant()}   {owned}/{current.MaxLevel}";
        if (selectedDescription != null)
            selectedDescription.text = current.Description;

        string status = !current.Implemented ? "IN ARRIVO - NON ACQUISTABILE"
            : owned >= current.MaxLevel ? "POTENZIAMENTO COMPLETO"
            : !skills.RequirementsMet(CurrentId) ? skills.RequirementText(CurrentId)
            : game.State == ScratchGameManager.RoundState.Playing
                ? "TERMINA LA PARTITA PER ACQUISTARE"
                : economy.Coins < skills.GetPrice(CurrentId) ? "MONETE INSUFFICIENTI"
                : "DISPONIBILE";
        if (statusLabel != null)
            statusLabel.text = status;
        if (purchaseLabel != null)
            purchaseLabel.text = owned >= current.MaxLevel ? "COMPLETO"
                : !current.Implemented ? "PROSSIMAMENTE"
                : $"ACQUISTA  {skills.GetPrice(CurrentId)}";
        if (purchaseButton != null)
            purchaseButton.interactable = skills.CanPurchase(CurrentId);
    }
}
