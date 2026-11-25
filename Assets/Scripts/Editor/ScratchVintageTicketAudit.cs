using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class ScratchVintageTicketAudit
{
    public static void Audit()
    {
        var manager = Object.FindAnyObjectByType<ScratchCellManager>(FindObjectsInactive.Include);
        int failures = 0;
        if (manager == null || !manager.HasVintageArt)
        { Debug.LogError("Open ScratchToRiches or ScratchTicketLab first."); return; }
        var cells = manager.GetComponentsInChildren<ScratchCell>(true);
        Check(cells.Length == 25, "25 authored cells", ref failures);
        bool lab = SceneManager.GetActiveScene().name == "ScratchTicketLab";
        Check(!lab || (manager.GetComponent<ScratchTicketLab>() != null &&
              manager.GetComponent<ScratchGameManager>() == null &&
              manager.GetComponent<ScratchEconomyManager>() == null),
              "isolated lab", ref failures);
        var scaler = manager.GetComponent<CanvasScaler>();
        Check(scaler != null && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
              "uniform screen fit", ref failures);
        for (int i = 0; i < 4; i++)
        {
            var kind = (ScratchGameManager.TicketKind)i;
            int side = ScratchGameManager.TicketSide(kind);
            var a = manager.GetVintageArt(kind);
            if (a == null || !a.IsValid(side * side))
            { Check(false, kind + " definition", ref failures); continue; }
            Check(Aspect(a.artwork, a.size) && Aspect(a.frame, a.cellSize) &&
                  Aspect(a.revealed, a.coverSize) && Aspect(a.cover, a.coverSize),
                  kind + " native sprite ratios", ref failures);
            Check(a.cover.texture.isReadable &&
                  (a.mask == null || (a.mask.texture.isReadable && Aspect(a.mask, a.coverSize))),
                  kind + " readable cover and mask", ref failures);
            foreach (Vector2 p in a.cellCenters)
                Check(p.x >= a.cellSize.x/2 && p.y >= a.cellSize.y/2 &&
                      p.x+a.cellSize.x/2 <= a.size.x && p.y+a.cellSize.y/2 <= a.size.y,
                      kind + " cell within artwork", ref failures);
        }
        foreach (var cell in cells)
        {
            var so = new SerializedObject(cell);
            Check(so.FindProperty("vintageRevealedBackground").objectReferenceValue != null &&
                  so.FindProperty("scratchImage").objectReferenceValue != null &&
                  so.FindProperty("symbolImage").objectReferenceValue != null,
                  cell.name + " authored layers", ref failures);
            var rt = (RectTransform)cell.transform;
            Check(Mathf.Approximately(rt.localScale.x,rt.localScale.y),
                  cell.name + " uniform scale", ref failures);
        }
        if (Application.isPlaying)
        {
            var canvas = manager.GetComponent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var cell in manager.ActiveCells)
            {
                var raw = cell.GetComponentInChildren<RawImage>(true);
                if (raw == null || cell.IsComplete) continue;
                var rt = raw.rectTransform;
                Vector2 p = RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(rt.rect.center));
                Check(cell.ContainsScratchPoint(p,camera), cell.name+" center hit",ref failures);
                if (lab && manager.CellCount == 25)
                {
                    Vector2 corner = rt.rect.min + rt.rect.size * .02f;
                    p = RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(corner));
                    Check(!cell.ContainsScratchPoint(p,camera),cell.name+" transparent corner rejected",ref failures);
                }
            }
        }
        if (failures == 0) Debug.Log("VINTAGE TICKET AUDIT PASS");
        else Debug.LogError("VINTAGE TICKET AUDIT FAILED: " + failures);
    }

    private static bool Aspect(Sprite sprite, Vector2 size)
        => sprite != null && size.y > 0 && Mathf.Abs(sprite.rect.width/sprite.rect.height-size.x/size.y)<.001f;

    private static void Check(bool ok, string reason, ref int errors)
    {
        if (ok) return;
        errors++;
        Debug.LogError("VINTAGE TICKET AUDIT: " + reason);
    }
}
