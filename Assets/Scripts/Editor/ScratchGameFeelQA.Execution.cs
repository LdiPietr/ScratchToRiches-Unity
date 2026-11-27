#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Pattern = ScratchSymbolManager.TestPattern;
using Tier = ScratchSymbolManager.MatchTier;
using Kind = ScratchGameManager.TicketKind;

internal static partial class ScratchGameFeelQA
{
    private static IEnumerator Run()
    {
        string[] names = { "01_classico_zero", "02_coppia_base", "03_tris", "04_poker", "05_cinquina",
            "06_rimborso", "07_jolly_occhio", "08_raggio", "09_passate", "10_ventaglio", "11_titan" };
        Pattern[] patterns = { Pattern.NoMatch, Pattern.Pair, Pattern.Triple, Pattern.Poker, Pattern.Five,
            Pattern.NoMatch, Pattern.FiveJolly, Pattern.Pair, Pattern.Pair, Pattern.Pair, Pattern.Pair };
        Tier[] tiers = { Tier.None, Tier.Pair, Tier.Triple, Tier.Poker, Tier.Five, Tier.None,
            Tier.Five, Tier.Pair, Tier.Pair, Tier.Pair, Tier.Pair };
        int[] extras = { 0,0,0,1,2,0,2,0,0,0,0 };
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            bool titan = i == 10, inspect = i == 6;
            Kind kind = i == 0 ? Kind.Standard : Kind.Fortunato;
            skills.ConfigureGameFeelQA(extras[i], i == 5, inspect, i == 7 ? 5 : 0, i == 8 ? 2 : 0, i == 9, titan);
            if (game.MaxScratches != 3 + extras[i] || (i == 7 && cells.EffectiveBrushRadius <= cells.BrushRadius) ||
                (i == 8 && cells.AdditionalScratchStripes <= 0) || (i == 9 && !cells.FanModeEnabled) ||
                (titan && skills.TitanCharges != 1) || (inspect && (skills.GetLevel("F04") <= 0 || skills.GetLevel("C06") <= 0)))
            { Finish(name + ": configurazione abilità non valida"); yield break; }
            var so = new SerializedObject(symbols);
            so.FindProperty("editorTestPattern").intValue = (int)patterns[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            int before = wallet.Coins, number = game.TicketNumber, streak = wallet.ConsecutiveWinningTickets;
            long earned = wallet.TotalEarned;
            int cost = ScratchGameManager.TicketPrice(kind);
            ResetMetrics();
            rows.Add(name + ";START;kind=" + kind + "|picks=" + game.MaxScratches + "|pattern=" + patterns[i] +
                "|brush=" + cells.EffectiveBrushRadius + "|stripes=" + cells.AdditionalScratchStripes + "|fan=" + cells.FanModeEnabled);
            if (!game.ChooseNextTicket(kind) || wallet.Coins != before - cost || game.TicketNumber != number + 1)
            { Finish(name + ": acquisto"); yield break; }
            yield return Observe(.85f);
            if (!flow.CanScratch || !game.TicketActive) { Finish(name + ": ingresso"); yield break; }
            if (inspect)
            {
                if (!game.TryInspectCell(cells.ActiveCells[0]) || !cells.ActiveCells[0].IsPreviewVisible ||
                    game.SelectedCount != 0 || symbols.JollyCell != cells.ActiveCells[4])
                { Finish(name + ": occhio di falco o Jolly"); yield break; }
                rows.Add(name + ";PASS;inspection without scratch|Jolly in cell 5");
                yield return Observe(.9f);
            }
            int titanBefore = skills.TitanActivationCount, jollyBefore = skills.JollyActivationCount;
            int limit = game.MaxScratches;
            for (int c = 0; c < limit; c++)
            {
                yield return Scratch(cells.ActiveCells[c], titan && c == 0, c == 0);
                if (!running) yield break;
                if (!cells.ActiveCells[c].IsComplete) { Finish(name + ": casella " + c); yield break; }
                if (c < limit - 1) yield return Observe(.38f);
            }
            var match = game.CurrentMatch;
            int payout = game.TicketPayout;
            var policy = ScratchRewardPolicy.Calculate(match, true, game.TicketPrize(kind),
                wallet.BasePrize, cost, streak, skills.GetLevel);
            int expected = (int)Math.Min(policy.RequestedCoins, (long)int.MaxValue - (before - cost));
            if (game.TicketActive || !flow.ShowingResult || game.RevealedCount != limit ||
                match.Tier != tiers[i] || payout != expected || wallet.Coins != before - cost + payout ||
                wallet.TotalEarned != earned + payout || (i == 0 && payout != 0) ||
                (i == 5 && payout <= 0) || (titan && skills.TitanActivationCount != titanBefore + 1) ||
                (inspect && skills.JollyActivationCount != jollyBefore + 1))
            { Finish(name + ": esito, incasso o attivazione non coerente"); yield break; }
            yield return Observe(2.35f);
            int expectedCoins = payout > 0 ? 2 + (int)match.Tier * 2 : 0;
            if (!flow.ResultPopupVisible || juice.PayoutAnimating || peakCoins != expectedCoins ||
                (payout > 0 && (!sawPayout || coinFrames == 0)) || (payout == 0 && sawPayout) ||
                peakFlakes <= 0 || flakeFrames == 0 ||
                walletLabel.text != wallet.Coins.ToString("N0") || !hud.ValidatePhaseVisibility(out _))
            { Finish(name + ": effetti, saldo visuale o popup non conformi"); yield break; }
            if (!ValidateFlakes(name, limit, i == 8, i == 9, titan))
            { Finish(name + ": feedback particellare non conforme"); yield break; }
            rows.Add(name + ";PASS;match=" + match.Tier + "|payout=" + payout + "|wallet=" + wallet.Coins +
                "|peak_active_coins=" + peakCoins + "|peak_visible_coins=" + peakVisibleCoins + "|coin_frames=" + coinFrames +
                "|peak_flakes=" + peakFlakes + "|flake_frames=" + flakeFrames +
                "|first_stamp_pct=" + firstStamp.ToString("F2", CultureInfo.InvariantCulture) + "|first_cell_steps=" + firstSteps +
                "|titan_uses=" + (skills.TitanActivationCount - titanBefore) + "|jolly_uses=" + (skills.JollyActivationCount - jollyBefore));
            passed++;
            flow.ContinueAfterResult();
            yield return Observe(.9f);
            if (!flow.CanShowTicketChoices) { Finish(name + ": ritorno selezione"); yield break; }
            Save();
        }
        Finish("");
    }

    private static IEnumerator Scratch(ScratchCell cell, bool titan, bool first)
    {
        var raw = cell.GetComponentInChildren<RawImage>(true);
        if (raw == null) { Finish("Patina mancante"); yield break; }
        var rt = raw.rectTransform;
        pointer = new PointerEventData(EventSystem.current) { pointerId = -451, button = PointerEventData.InputButton.Left };
        for (int step = -1; step < 360 && !cell.IsComplete; step++)
        {
            int scan = Mathf.Max(0, step) % 180, row = scan / 15, col = scan % 15;
            float u = step < 0 ? .5f : .04f + (row % 2 == 0 ? col : 14-col)*.92f/14;
            float v = step < 0 ? .5f : .07f + row*.86f/11;
            Rect r = rt.rect;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                rt.TransformPoint(new Vector3(Mathf.Lerp(r.xMin,r.xMax,u),Mathf.Lerp(r.yMin,r.yMax,v),0)));
            if (!cell.ContainsScratchPoint(pointer.position, null)) continue;
            if (pressed == null)
            {
                pressed = cell; cell.OnPointerDown(pointer);
                if (first) firstStamp = cell.RevealedPercentage;
                if (titan && !cell.QaActivateTitanDuringGesture())
                { Finish("Titan non attivato durante il gesto"); yield break; }
            }
            else cell.OnDrag(pointer);
            if (first) firstSteps++;
            yield return Observe(.028f);
        }
        Release();
    }

    private static IEnumerator Observe(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        do { yield return new WaitForEndOfFrame(); if (!running) yield break; Sample(); }
        while (Time.realtimeSinceStartup < until);
    }

    private static void ResetMetrics()
    {
        peakCoins = peakVisibleCoins = peakFlakes = flakeFrames = coinFrames = firstSteps = 0;
        firstStamp = 0; sawPayout = false;
        if (juice != null) juice.QaResetFlakeMetrics();
    }

    private static void Sample()
    {
        frameCount++;
        int active = 0, visible = 0, foil = 0;
        foreach (var coin in coins)
            if (coin.gameObject.activeInHierarchy)
            { active++; if (coin.color.a > .05f) visible++; }
        foreach (var flake in flakes)
            if (flake.gameObject.activeInHierarchy && flake.color.a > .05f) foil++;
        peakCoins = Mathf.Max(peakCoins, active);
        peakVisibleCoins = Mathf.Max(peakVisibleCoins, visible);
        peakFlakes = Mathf.Max(peakFlakes, foil);
        if (visible > 0) coinFrames++;
        if (foil > 0) flakeFrames++;
        sawPayout |= juice.PayoutAnimating;
    }

    private static void Release()
    { if (pressed != null && pointer != null) pressed.OnPointerUp(pointer); pressed = null; pointer = null; }

}
#endif
