using UnityEngine;
using UnityEngine.UI;

public sealed partial class ScratchCellManager
{
    [Header("Scene-authored Vintage Lottery ticket family")]
    [SerializeField] private Image vintageTicketArtwork;
    [SerializeField] private ScratchVintageTicketArt[] vintageTickets;
    [SerializeField, Range(.2f, 1f)] private float vintageScale = .94f;

    public bool HasVintageArt => vintageTickets != null && vintageTickets.Length == 4;
    public ScratchVintageTicketArt GetVintageArt(ScratchGameManager.TicketKind kind)
        => HasVintageArt ? vintageTickets[(int)kind] : null;

    private void ApplyVintageBoard(ScratchVintageTicketArt art)
    {
        if (vintageTicketArtwork != null)
        {
            vintageTicketArtwork.sprite = art.artwork;
            vintageTicketArtwork.type = Image.Type.Simple;
            vintageTicketArtwork.preserveAspect = true;
            vintageTicketArtwork.rectTransform.sizeDelta = art.size * vintageScale;
        }
        gridRoot.anchorMin = gridRoot.anchorMax = gridRoot.pivot = Vector2.one * .5f;
        gridRoot.anchoredPosition = Vector2.zero;
        gridRoot.sizeDelta = art.size * vintageScale;
        gridRoot.localScale = Vector3.one;
    }
}
