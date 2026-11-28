#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

internal static partial class ScratchGameFeelQA
{
    private static readonly Vector3[] flakeCorners = new Vector3[4];

    // Sample after a real rendered frame. This verifies renderer eligibility,
    // hierarchy, opacity and projected rect dimensions, not sprite pixel contrast.
    private static void MeasureFlakeRendering(RawImage foil, Comparison result, bool initialTitan)
    {
        if (foil == null || !foil.isActiveAndEnabled || foil.canvasRenderer.absoluteDepth < 0) return;
        int renderable = 0;
        foreach (Image image in flakes)
        {
            if (image == null || !image.isActiveAndEnabled || image.color.a < .35f) continue;
            CanvasRenderer renderer = image.canvasRenderer;
            if (image.canvas != foil.canvas || renderer.absoluteDepth <= foil.canvasRenderer.absoluteDepth)
            { result.wrongDepth++; continue; }
            if (renderer.cull || renderer.GetInheritedAlpha() < .35f) continue;
            image.rectTransform.GetWorldCorners(flakeCorners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, flakeCorners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(null, flakeCorners[1]);
            Vector2 c = RectTransformUtility.WorldToScreenPoint(null, flakeCorners[3]);
            float size = Mathf.Min(Vector2.Distance(a,b), Vector2.Distance(a,c)) * 1080f / Mathf.Max(1,Screen.height);
            result.peakSize1080 = Mathf.Max(result.peakSize1080,size);
            if (size < 10f) continue;
            renderable++;
            if (initialTitan && image.color.r > .75f && image.color.g < .96f &&
                image.color.b < .70f) result.impulseRenderable++;
        }
        if (renderable > 0)
        {
            result.renderFrames++;
            if (result.firstRenderFrame < 0) result.firstRenderFrame = frameCount;
        }
        result.peakRenderable = Mathf.Max(result.peakRenderable,renderable);
    }

    private static bool ValidateFlakes(string name, int reveals, bool parallel, bool fan, bool titan)
    {
        bool ok = juice.QaTrailBursts > 0 && juice.QaRevealBursts == reveals &&
            juice.QaOverlapFrames > 0 &&
            (parallel || titan ? juice.QaParallelBursts > 0 : juice.QaParallelBursts == 0) &&
            (fan ? juice.QaFanBursts > 0 : juice.QaFanBursts == 0) &&
            (titan ? juice.QaTitanActivations == 1 && juice.QaTitanTrails > 0 &&
                juice.QaGoldFrames > 0 && juice.QaPeakGold >= 2 :
                juice.QaTitanActivations == 0 && juice.QaTitanTrails == 0 && juice.QaGoldFrames == 0);
        rows.Add(name + ";FLAKES_" + (ok ? "PASS" : "FAIL") +
            ";trail_bursts=" + juice.QaTrailBursts + "|parallel_bursts=" + juice.QaParallelBursts +
            "|fan_bursts=" + juice.QaFanBursts + "|reveal_bursts=" + juice.QaRevealBursts +
            "|overlap_frames=" + juice.QaOverlapFrames + "|titan_impulses=" + juice.QaTitanActivations +
            "|titan_trails=" + juice.QaTitanTrails + "|gold_frames=" + juice.QaGoldFrames +
            "|peak_gold=" + juice.QaPeakGold + "|pool=24");
        return ok;
    }
}
#endif
