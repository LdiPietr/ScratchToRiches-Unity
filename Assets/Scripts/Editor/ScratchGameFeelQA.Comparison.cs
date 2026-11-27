#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Pattern = ScratchSymbolManager.TestPattern;
using Tier = ScratchSymbolManager.MatchTier;
using Kind = ScratchGameManager.TicketKind;

internal static partial class ScratchGameFeelQA
{
    private sealed class Comparison
    {
        public int events, idleEvents;
        public float distance, elapsed, first, postTitanDelta, physical;
        public readonly int[] milestoneEvents = new int[3];
        public readonly float[] milestoneDistance = new float[3];
        public readonly float[] milestoneTime = new float[3];
    }

    private static readonly string[] ComparisonNames =
        { "base", "raggio", "passate", "ventaglio", "titan" };
    private static readonly Vector2[] ComparisonStarts =
        { new Vector2(.5f,.5f), new Vector2(.28f,.28f), new Vector2(.72f,.72f) };

    private static string F(float x) => x.ToString("F3", CultureInfo.InvariantCulture);

    private static IEnumerator RunComparison()
    {
        var results = new Comparison[5,3];
        // Interleave abilities within each replicate: same art, mask, cell,
        // chosen symbols, start point and pointer travel cap.
        for (int repeat = 0; repeat < 3; repeat++)
        for (int mode = 0; mode < 5; mode++)
        {
            string name = "r" + (repeat + 1) + "_" + ComparisonNames[mode];
            bool titan = mode == 4;
            skills.ConfigureGameFeelQA(0, false, false, mode == 1 ? 5 : 0,
                mode == 2 ? 2 : 0, mode == 3, titan);
            if (game.MaxScratches != 3 || (mode == 1 && cells.EffectiveBrushRadius <= cells.BrushRadius) ||
                (mode == 2 && cells.AdditionalScratchStripes <= 0) || (mode == 3 && !cells.FanModeEnabled) ||
                (titan && skills.TitanCharges != 1))
            { Finish(name + ": configurazione abilita non valida"); yield break; }
            var so = new SerializedObject(symbols);
            so.FindProperty("editorTestPattern").intValue = (int)Pattern.Pair;
            so.ApplyModifiedPropertiesWithoutUndo();
            int before = wallet.Coins, number = game.TicketNumber, streak = wallet.ConsecutiveWinningTickets;
            long earned = wallet.TotalEarned;
            int cost = ScratchGameManager.TicketPrice(Kind.Fortunato);
            int titanBefore = skills.TitanActivationCount;
            ResetMetrics();
            rows.Add(name + ";START;brush=" + cells.EffectiveBrushRadius + "|stripes=" +
                cells.AdditionalScratchStripes + "|fan=" + cells.FanModeEnabled +
                "|titan=" + titan + "|start=" + F(ComparisonStarts[repeat].x) + "," + F(ComparisonStarts[repeat].y));
            if (!game.ChooseNextTicket(Kind.Fortunato) || wallet.Coins != before - cost || game.TicketNumber != number + 1)
            { Finish(name + ": acquisto"); yield break; }
            yield return Observe(.85f);
            if (!running) yield break;
            if (!flow.CanScratch || !game.TicketActive) { Finish(name + ": ingresso"); yield break; }

            var result = new Comparison();
            yield return ScratchAdaptive(cells.ActiveCells[0], ComparisonStarts[repeat], titan, result);
            if (!running) yield break;
            if (!cells.ActiveCells[0].IsComplete || result.physical + .01f < cells.ScratchPercentage ||
                result.events <= 0 || (titan && skills.TitanActivationCount != titanBefore + 1))
            { Finish(name + ": misurazione incompleta"); yield break; }
            results[mode,repeat] = result;
            rows.Add(name + ";BENCHMARK;events=" + result.events +
                "|idle_events=" + result.idleEvents + "|distance_cell_short_sides=" + F(result.distance) +
                "|elapsed_s=" + F(result.elapsed) + "|first_stamp_pct=" + F(result.first) +
                "|post_titan_delta_pct=" + (titan ? F(result.postTitanDelta) : "NA") +
                "|physical_end_pct=" + F(result.physical) +
                "|events25=" + result.milestoneEvents[0] + "|events50=" + result.milestoneEvents[1] +
                "|events75=" + result.milestoneEvents[2] +
                "|distance25=" + F(result.milestoneDistance[0]) + "|distance50=" + F(result.milestoneDistance[1]) +
                "|distance75=" + F(result.milestoneDistance[2]) +
                "|seconds25=" + F(result.milestoneTime[0]) + "|seconds50=" + F(result.milestoneTime[1]) +
                "|seconds75=" + F(result.milestoneTime[2]));
            Save();
            yield return Observe(.4f);
            // Not benchmarked: complete the actual ticket with the validated
            // fixed path, keeping settlement and visual transitions in the test.
            for (int c = 1; c < 3; c++)
            {
                yield return Scratch(cells.ActiveCells[c], false, false);
                if (!running) yield break;
                if (!cells.ActiveCells[c].IsComplete) { Finish(name + ": casella " + c); yield break; }
                if (c == 1) yield return Observe(.38f);
            }
            var match = game.CurrentMatch;
            var policy = ScratchRewardPolicy.Calculate(match, true, game.TicketPrize(Kind.Fortunato),
                wallet.BasePrize, cost, streak, skills.GetLevel);
            int expected = (int)Math.Min(policy.RequestedCoins, (long)int.MaxValue - (before - cost));
            if (game.TicketActive || !flow.ShowingResult || game.RevealedCount != 3 ||
                match.Tier != Tier.Pair || game.TicketPayout != expected ||
                wallet.Coins != before - cost + expected || wallet.TotalEarned != earned + expected)
            { Finish(name + ": incasso non coerente"); yield break; }
            yield return Observe(2.35f);
            if (!flow.ResultPopupVisible || juice.PayoutAnimating ||
                peakCoins != 4 || peakFlakes <= 0 || !sawPayout ||
                walletLabel.text != wallet.Coins.ToString("N0") || !hud.ValidatePhaseVisibility(out _))
            { Finish(name + ": feedback o popup non coerente"); yield break; }
            flow.ContinueAfterResult();
            yield return Observe(.9f);
            if (!running) yield break;
            if (!flow.CanShowTicketChoices) { Finish(name + ": ritorno selezione"); yield break; }
            passed++;
            rows.Add(name + ";PASS;real ticket|payout=" + expected + "|wallet=" + wallet.Coins);
            Save();
        }

        float baseEvents = Median(results[0,0].events, results[0,1].events, results[0,2].events);
        float baseDistance = Median(results[0,0].distance, results[0,1].distance, results[0,2].distance);
        for (int mode = 0; mode < 5; mode++)
        {
            Comparison a = results[mode,0], b = results[mode,1], c = results[mode,2];
            float events = Median(a.events,b.events,c.events);
            float distance = Median(a.distance,b.distance,c.distance);
            rows.Add(ComparisonNames[mode] + ";MEDIAN;n=3|events=" + F(events) +
                "|distance=" + F(distance) + "|elapsed_s=" + F(Median(a.elapsed,b.elapsed,c.elapsed)) +
                "|events_saved_pct=" + F(100f * (1f-events/Mathf.Max(1f,baseEvents))) +
                "|distance_saved_pct=" + (baseDistance > 0f ? F(100f * (1f-distance/baseDistance)) : "NA"));
        }
        Finish("");
    }

    private static float Median(float a, float b, float c) =>
        a + b + c - Mathf.Min(a,Mathf.Min(b,c)) - Mathf.Max(a,Mathf.Max(b,c));

    private static IEnumerator ScratchAdaptive(ScratchCell cell, Vector2 start, bool titan, Comparison result)
    {
        var raw = cell.GetComponentInChildren<RawImage>(true);
        if (raw == null) { Finish("Patina mancante"); yield break; }
        var rt = raw.rectTransform;
        int resolution = cells.TextureResolution;
        var remaining = new bool[resolution * resolution];
        if (cell.QaReadRemainingFoil(remaining, out float coverage) != resolution)
        { Finish("Risoluzione maschera diversa"); yield break; }

        Vector2 ScreenPoint(Vector2 uv)
        {
            Rect rect = rt.rect;
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(
                new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,uv.x),Mathf.Lerp(rect.yMin,rect.yMax,uv.y),0)));
        }
        float width = Vector2.Distance(ScreenPoint(Vector2.zero), ScreenPoint(Vector2.right));
        float height = Vector2.Distance(ScreenPoint(Vector2.zero), ScreenPoint(Vector2.up));
        float shortSide = Mathf.Min(width,height);
        if (shortSide <= 1f) { Finish("Geometria patina non valida"); yield break; }
        Vector2 scale = new Vector2(width/shortSide,height/shortSide);
        float stride = 1f/16f;
        Vector2 current = NearestFoil(remaining,resolution,start,scale);
        if (current.x < 0f) { Finish("Patina vuota prima della grattatura"); yield break; }
        pointer = new PointerEventData(EventSystem.current) { pointerId = -452, button = PointerEventData.InputButton.Left };
        pointer.position = ScreenPoint(current);
        if (!cell.ContainsScratchPoint(pointer.position,null)) { Finish("Punto iniziale fuori maschera"); yield break; }
        float started = Time.realtimeSinceStartup;
        pressed = cell;
        cell.OnPointerDown(pointer);
        result.events = 1;
        cell.QaReadRemainingFoil(remaining,out coverage);
        result.first = coverage;
        CaptureMilestones(result,coverage,started);
        if (titan && !cell.QaActivateTitanDuringGesture())
        { Finish("Titan non attivato"); yield break; }
        yield return Observe(.028f);

        int stalled = 0;
        while (running && !cell.IsComplete && result.events < 600)
        {
            Vector2 target = NearestFoil(remaining,resolution,current,scale);
            if (target.x < 0f) { Finish("Patina esaurita senza completamento"); yield break; }
            float gap = Vector2.Scale(target-current,scale).magnitude;
            Vector2 next = Vector2.Lerp(current,target,gap > stride ? stride/gap : 1f);
            Vector2 nextScreen = ScreenPoint(next);
            if (!cell.ContainsScratchPoint(nextScreen,null))
            { Finish("Traiettoria adattiva fuori maschera"); yield break; }
            result.distance += Vector2.Distance(pointer.position,nextScreen)/shortSide;
            pointer.position = nextScreen;
            current = next;
            float previousCoverage = coverage;
            cell.OnDrag(pointer);
            result.events++;
            cell.QaReadRemainingFoil(remaining,out coverage);
            if (titan && result.events == 2) result.postTitanDelta = coverage-previousCoverage;
            if (coverage <= previousCoverage + .00001f) { result.idleEvents++; stalled++; }
            else stalled = 0;
            CaptureMilestones(result,coverage,started);
            if (stalled > 16) { Finish("Percorso adattivo senza progresso"); yield break; }
            yield return Observe(.028f);
        }
        result.elapsed = Time.realtimeSinceStartup-started;
        result.physical = coverage;
        Release();
        if (running && !cell.IsComplete) Finish("Limite movimenti adattivi");
    }

    private static Vector2 NearestFoil(bool[] remaining, int size, Vector2 from, Vector2 scale)
    {
        // Only remaining opaque material, not symbols or simulated brush results.
        // Every mode uses identical target selection and movement constraints.
        float best = float.PositiveInfinity;
        Vector2 target = new Vector2(-1,-1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (!remaining[y*size+x]) continue;
            Vector2 uv = new Vector2((x+.5f)/size,(y+.5f)/size);
            float distance = Vector2.Scale(uv-from,scale).sqrMagnitude;
            if (distance < best) { best = distance; target = uv; }
        }
        return target;
    }

    private static void CaptureMilestones(Comparison result, float coverage, float started)
    {
        for (int i = 0; i < 3; i++)
            if (result.milestoneEvents[i] == 0 && coverage >= 25f*(i+1))
            {
                result.milestoneEvents[i] = result.events;
                result.milestoneDistance[i] = result.distance;
                result.milestoneTime[i] = Time.realtimeSinceStartup-started;
            }
    }
}
#endif
