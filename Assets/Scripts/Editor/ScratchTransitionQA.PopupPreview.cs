#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static partial class ScratchTransitionQA
{
    private static bool previewOnly;

    [MenuItem("Tools/Scratch to Riches/Test visivo popup (tutti i risultati)")]
    public static void StartPopupPreview()
    {
        if (Busy || ScratchAutoplayWindow.AnyTestRunning || ScratchManualTelemetry.IsRecording) return;
        game = UnityEngine.Object.FindAnyObjectByType<ScratchGameManager>();
        flow = game != null ? game.GetComponent<ScratchFlowPresentation>() : null;
        hud = game != null ? game.GetComponent<ScratchVintageGameplayHUD>() : null;
        if (!EditorApplication.isPlaying || game == null || flow == null || hud == null ||
            game.gameObject.scene.path != "Assets/Scenes/ScratchToRiches.unity" ||
            !flow.CanShowTicketChoices || !game.WaitingForTicketChoice || game.DemoFinished)
        {
            EditorUtility.DisplayDialog("Test visivo popup", "Apri ScratchToRiches in Play Mode sulla selezione biglietti.", "OK");
            return;
        }
        directory = Path.Combine(Application.persistentDataPath, "TransitionQA",
            "Popup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(directory);
        rows.Clear();
        rows.Add("frame;event;detail");
        rows.Add("0;MODE;SYNTHETIC_VISUAL_FIXTURE|no settlement or save changes");
        frames = tickets = 0;
        previous = failure = "";
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        balanceOnly = false;
        previewOnly = true;
        running = true;
        deadline = EditorApplication.timeSinceStartup + 60;
        routine = flow.StartCoroutine(RunPopupPreview());
    }

    private static IEnumerator RunPopupPreview()
    {
        var wallet = game.GetComponent<ScratchEconomyManager>();
        int balance = wallet.Coins, number = game.TicketNumber, completed = game.TicketsCompleted;
        long earned = wallet.TotalEarned;
        var symbols = new SerializedObject(game.GetComponent<ScratchSymbolManager>()).FindProperty("symbolSprites");
        var so = new SerializedObject(flow);
        if (symbols == null || symbols.arraySize < 7) { Finish("Sprite di test mancanti"); yield break; }
        var tiers = new[] { 0, 0, 0, 1, 2, 3, 4, 4, 0 };
        var payouts = new[] { 0, 0, 8, 104, 180, 240, 300, 999999999, 0 };
        var names = new[] { "nessuna_gratis", "nessuna_pagamento", "rimborso", "coppia", "tris",
            "poker", "cinquina", "cinquina_jolly_importo_grande", "biglietto_prova" };
        var expected = new[] { "NESSUNA VINCITA", "NESSUNA VINCITA", "RIMBORSO PARZIALE", "COPPIA",
            "TRIS", "POKER", "CINQUINA", "CINQUINA", "BIGLIETTO DI PROVA" };
        var expectedNet = new[] { "", "-10", "-2", "+94", "+170", "+230", "+290", "+999,999,989", "" };
        for (int i = 0; i < names.Length; i++)
        {
            var kind = i == 0 ? ScratchGameManager.TicketKind.Standard : ScratchGameManager.TicketKind.Fortunato;
            var tier = (ScratchSymbolManager.MatchTier)tiers[i];
            bool voided = i == 8;
            int count = !voided && tiers[i] > 0 ? tiers[i] + 1 : 0;
            var artwork = new List<Sprite>();
            for (int k = 0; k < count; k++)
            {
                Sprite sprite = i == 7 && k == count - 1
                    ? so.FindProperty("resultJollySprite").objectReferenceValue as Sprite
                    : symbols.GetArrayElementAtIndex(i % 7).objectReferenceValue as Sprite;
                if (sprite == null) { Finish("Sprite fixture mancante"); yield break; }
                artwork.Add(sprite);
            }
            if (!flow.PreviewPopup(kind, tier, payouts[i], voided, artwork, i != 1, i < 3 ? 0 : 6))
            { Finish("Apertura fixture non riuscita"); yield break; }
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            if (!running) yield break;
            if (!Check("popup_fixture")) yield break;
            if (!VerifyPopupFixture(so, expected[i], payouts[i], expectedNet[i], artwork, i != 1) ||
                !CheckPopupPolish(so, tier, payouts[i], voided, i != 1))
            { Finish("Fixture non conforme: " + names[i]); yield break; }
            if (wallet.Coins != balance || wallet.TotalEarned != earned ||
                game.TicketNumber != number || game.TicketsCompleted != completed || game.TicketActive)
            { Finish("La fixture ha modificato il gameplay"); yield break; }
            var snapshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (snapshot == null) { Finish("Screenshot fixture assente"); yield break; }
            string file = "popup_" + (i + 1).ToString("D2") + "_" + names[i] + ".png";
            try
            {
                byte[] bytes = snapshot.EncodeToPNG();
                if (bytes == null || bytes.Length < 24) { Finish("Screenshot fixture vuoto"); yield break; }
                File.WriteAllBytes(Path.Combine(directory, file), bytes);
                rows.Add(frames + ";POPUP_FIXTURE;PASS|" + names[i] + "|icons=" + count + "|" + file);
            }
            finally { UnityEngine.Object.Destroy(snapshot); }
        }
        flow.EndPopupPreview();
        yield return new WaitForEndOfFrame();
        if (!Check("fixture_return")) yield break;
        if (!flow.CanShowTicketChoices || wallet.Coins != balance || wallet.TotalEarned != earned ||
            game.TicketNumber != number || game.TicketsCompleted != completed)
        { Finish("Ripristino selezione o dati non coerente"); yield break; }
        rows.Add(frames + ";PREVIEW_COVERAGE;PASS|9 screenshots|wallet and progression unchanged");
        Finish("");
    }

    // Both the synthetic and real-ticket tests exercise these presentation invariants.
    private static bool CheckPopupPolish(SerializedObject so, ScratchSymbolManager.MatchTier tier,
        int payout, bool voided, bool affordable)
    {
        var outcome = so.FindProperty("resultOutcomeLabel").objectReferenceValue as TMP_Text;
        var amount = so.FindProperty("resultPopupAmount").objectReferenceValue as TMP_Text;
        var repeat = so.FindProperty("repeatTicketLabel").objectReferenceValue as TMP_Text;
        var button = so.FindProperty("repeatTicketButton").objectReferenceValue as Button;
        var popup = so.FindProperty("resultPopup").objectReferenceValue as GameObject;
        if (outcome == null || amount == null || repeat == null || button == null || popup == null) return false;
        var root = popup.GetComponent<Image>();
        var dimmer = popup.transform.Find("ResultBodyDimmer")?.GetComponent<Image>();
        var header = new SerializedObject(hud).FindProperty("gameplayHeader").objectReferenceValue as GameObject;
        if (root == null || !root.raycastTarget || root.color.a > .001f ||
            dimmer == null || dimmer.raycastTarget || Mathf.Abs(dimmer.color.a - .55f) > .01f ||
            header == null || !header.activeInHierarchy) return false;
        foreach (string field in new[] { "resultCardGroup", "rewardHeaderGroup", "rewardValueGroup", "rewardActionsGroup" })
        {
            var group = so.FindProperty(field).objectReferenceValue as CanvasGroup;
            if (group == null || !group.gameObject.activeInHierarchy || Mathf.Abs(group.alpha - 1f) > .01f) return false;
            if (field == "rewardActionsGroup" && (!group.interactable || !group.blocksRaycasts)) return false;
        }
        var headerRect = header.GetComponent<RectTransform>();
        if (headerRect == null) return false;
        var a = new Vector3[4];
        var b = new Vector3[4];
        headerRect.GetWorldCorners(a);
        dimmer.rectTransform.GetWorldCorners(b);
        if (b[1].y > a[0].y + .1f || Mathf.Abs(b[1].y - a[0].y) > .5f) return false;
        int rank = !voided && payout > 0 ? (int)tier : 0;
        float[] titleMax = { 38, 38, 42, 46, 50 };
        float[] amountMax = { 92, 100, 113, 126, 139 };
        if (rank >= 0 && rank < amountMax.Length)
        {
            if (payout >= 100000000) amountMax[rank] = Mathf.Min(amountMax[rank], 76f);
            if (payout >= 1000000000) amountMax[rank] = Mathf.Min(amountMax[rank], 64f);
        }
        if (rank < 0 || rank > 4 || Mathf.Abs(outcome.fontSizeMax - titleMax[rank]) > .01f ||
            Mathf.Abs(amount.fontSizeMax - amountMax[rank]) > .01f) return false;
        var frame = so.FindProperty("rewardGoldFrame").objectReferenceValue as Image;
        var rays = so.FindProperty("rewardGoldRays").objectReferenceValue as Image;
        var divider = so.FindProperty("rewardDivider").objectReferenceValue as Image;
        var coins = so.FindProperty("rewardCoinDecor");
        var sparks = so.FindProperty("rewardSparks");
        if (frame == null || rays == null || divider == null || coins.arraySize != 6 || sparks.arraySize != 8 ||
            frame.gameObject.activeInHierarchy != (rank > 0) || rays.gameObject.activeInHierarchy != (rank > 0) ||
            !divider.gameObject.activeInHierarchy || amount.enableVertexGradient != (rank > 0)) return false;
        for (int i = 0; i < coins.arraySize; i++)
        {
            var image = coins.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (image == null || image.raycastTarget || image.gameObject.activeInHierarchy !=
                (rank > 0 && payout < 10000 && i % 3 < Mathf.Min(3, rank))) return false;
        }
        for (int i = 0; i < sparks.arraySize; i++)
        {
            var image = sparks.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (image == null || image.raycastTarget || image.gameObject.activeInHierarchy != (rank >= 2 && i < (rank - 1) * 2)) return false;
        }
        var shadow = so.FindProperty("rewardAmountShadow").objectReferenceValue as TMP_Text;
        if (shadow == null || shadow.raycastTarget || shadow.gameObject.activeInHierarchy != (rank > 0) ||
            (rank > 0 && (shadow.text != amount.text || amount.color != Color.white))) return false;
        Color expected = affordable ? new Color32(32,51,46,255) : new Color32(249,239,216,255);
        var disabled = button.colors.disabledColor;
        if (repeat.color != expected || button.interactable != affordable ||
            disabled.a < .99f || disabled.r > .45f || disabled.g > .45f || disabled.b > .45f) return false;
        rows.Add(frames + ";POPUP_POLISH;PASS|header clear|rank=" + rank + "|affordable=" + affordable);
        return true;
    }

    private static bool VerifyPopupFixture(SerializedObject so, string expected, int payout,
        string netValue, List<Sprite> artwork, bool affordable)
    {
        TMP_Text GetText(string name) => so.FindProperty(name).objectReferenceValue as TMP_Text;
        var heading = GetText("resultPopupHeading");
        var outcome = GetText("resultOutcomeLabel");
        var amount = GetText("resultPopupAmount");
        var unit = GetText("resultPopupUnit");
        var net = GetText("resultPopupNet");
        var repeat = GetText("repeatTicketLabel");
        var button = so.FindProperty("repeatTicketButton").objectReferenceValue as Button;
        if (heading == null || outcome == null || amount == null || unit == null || net == null ||
            repeat == null || button == null || outcome.text != expected ||
            amount.text != (payout > 0 ? "+" : "") + payout.ToString("N0") || unit.text != "MONETE" ||
            button.interactable != affordable || repeat.text.Contains("SALDO INSUFFICIENTE") == affordable)
            return false;
        if (netValue.Length == 0 ? net.text.Length != 0 :
            !net.text.Contains(netValue == "+999,999,989" ? "+" + 999999989.ToString("N0") : netValue)) return false;
        var icons = so.FindProperty("resultMatchIcons");
        if (icons == null || icons.arraySize != 5) return false;
        for (int i = 0; i < icons.arraySize; i++)
        {
            var icon = icons.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (icon == null || icon.gameObject.activeInHierarchy != (i < artwork.Count) ||
                (i < artwork.Count && icon.sprite != artwork[i])) return false;
        }
        Canvas.ForceUpdateCanvases();
        foreach (var label in new[] { heading, outcome, amount, unit, net, repeat })
        {
            if (string.IsNullOrEmpty(label.text)) continue;
            label.ForceMeshUpdate();
            if (label.isTextOverflowing) return false;
        }
        // The whole amount and its unit share one center axis, independent of digits.
        return Mathf.Abs(amount.rectTransform.anchoredPosition.x - unit.rectTransform.anchoredPosition.x) < .01f &&
            Mathf.Abs(outcome.rectTransform.anchoredPosition.x - amount.rectTransform.anchoredPosition.x) < .01f;
    }
}
#endif
