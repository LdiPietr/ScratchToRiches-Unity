using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public static class ScratchS02VisualAudit
{
    public static void Audit()
    {
        if (SceneManager.GetActiveScene().name != "S02_VintageLottery")
        { Debug.LogError("Open S02_VintageLottery.unity first."); return; }
        int failures = 0;
        var root = GameObject.Find("S02_Canvas");
        if (root == null) { Debug.LogError("Missing canvas"); return; }
        Canvas.ForceUpdateCanvases();
        var scaler = root.GetComponent<CanvasScaler>();
        Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
              scaler.referenceResolution == new Vector2(1920, 1080) &&
              scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, "Canvas uniform fit", ref failures);
        var stage = root.transform.Find("S02_Stage_1920x1080") as RectTransform;
        if (stage == null) { Debug.LogError("Missing stage"); return; }
        Check(stage.sizeDelta == new Vector2(1920, 1080) && stage.anchorMin == Vector2.one * .5f &&
              stage.anchorMax == stage.anchorMin, "16:9 stage", ref failures);
        foreach (var tr in stage.GetComponentsInChildren<RectTransform>(true))
            Check(Mathf.Approximately(tr.localScale.x, tr.localScale.y) &&
                  Mathf.Approximately(tr.localScale.y, tr.localScale.z), "Uniform scale " + tr.name, ref failures);
        int spriteCount = 0;
        foreach (var im in stage.GetComponentsInChildren<Image>(true))
        {
            if (im.sprite == null)
            {
                Check(im.name.StartsWith("PickIndicator_") || im.name == "Divider" || im.name == "MatchDivider" ||
                      im.name == "MatchAccent" || im.name == "ProgressTrack" || im.name == "ProgressFill",
                      "Missing sprite " + im.name, ref failures);
                continue;
            }
            spriteCount++;
            Check(AssetDatabase.GetAssetPath(im.sprite).StartsWith("Assets/Art/UI/S02_Classic/"),
                  "Pack reference " + im.name, ref failures);
            if (im.type == Image.Type.Simple)
            {
                Vector2 s = im.sprite.rect.size, q = im.rectTransform.rect.size;
                Check(im.preserveAspect && q.y > 0 && Mathf.Abs(s.x / s.y - q.x / q.y) < .001f,
                      "Aspect " + im.name, ref failures);
            }
            else if (im.type == Image.Type.Sliced)
                Check(im.sprite.border.sqrMagnitude > 0, "9-slice " + im.name, ref failures);
            else Check(im.type == Image.Type.Tiled && im.name.EndsWith("Bar"), "Tiled bar " + im.name, ref failures);
        }
        Check(spriteCount == 48, "All sprite references", ref failures);
        var grid = stage.Find("Ticket/Grid_3x3");
        if (grid == null) { Debug.LogError("Missing grid"); return; }
        Check(grid.childCount == 9, "Nine cells", ref failures);
        int covered = 0;
        for (int i = 0; i < grid.childCount; i++)
        {
            var cell = grid.GetChild(i) as RectTransform;
            Check(cell != null && cell.sizeDelta == new Vector2(184, 140) &&
                  cell.anchoredPosition == new Vector2(i % 3 * 196, -(i / 3) * 150),
                  "Cell geometry " + i, ref failures);
            if (cell != null && cell.Find("Foil") != null) covered++;
        }
        Check(covered == 7, "Seven covered cells", ref failures);
        foreach (var label in stage.GetComponentsInChildren<TMP_Text>(true))
        {
            var widthAdjustment = new SerializedObject(label).FindProperty("m_charWidthMaxAdj");
            Check(label.font != null && !label.enableAutoSizing &&
                  (widthAdjustment == null || Mathf.Approximately(widthAdjustment.floatValue, 0)),
                  "TMP no compression " + label.name, ref failures);
        }
        if (failures == 0) Debug.Log("S02 VISUAL AUDIT PASS");
        else Debug.LogError("S02 VISUAL AUDIT FAILED: " + failures);
    }

    private static void Check(bool ok, string reason, ref int failures)
    {
        if (ok) return;
        failures++;
        Debug.LogError("S02 VISUAL AUDIT: " + reason);
    }
}
