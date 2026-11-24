using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene node view adapter: appearance only, not purchase authority.</summary>
internal static class ScratchSkillMapNodeView
{
    public static void Render(ScratchHoldToBuy control, ScratchSkillCatalogV2.Entry entry,
        int branch, Color branchColor, int level, bool unavailable, bool locked,
        bool canBuy, bool completed, Sprite[] upgradeSprites)
    {
        Color tint = branchColor;
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
