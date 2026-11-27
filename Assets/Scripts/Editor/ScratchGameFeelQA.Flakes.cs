#if UNITY_EDITOR
internal static partial class ScratchGameFeelQA
{
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
