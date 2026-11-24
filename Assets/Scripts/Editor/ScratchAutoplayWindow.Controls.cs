#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// Controls responsibility of ScratchAutoplayWindow.
public sealed partial class ScratchAutoplayWindow
{
    [MenuItem("Tools/Scratch to Riches/Autoplay QA (test automatico)")]
    private static void ShowWindow()
    {
        GetWindow<ScratchAutoplayWindow>("STR Autoplay QA").minSize = new Vector2(390, 460);
    }

    [MenuItem("Tools/Scratch to Riches/Reset progressi V2 (fuori Play)")]
    private static void ResetProgressMenu()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reset progressi", "Esci da Play Mode prima di resettare: i manager devono ricaricare i dati salvati.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Reset Scratch to Riches V2",
                "Eliminare tutte le monete e i livelli delle 90 abilita' salvati? Questa azione e' irreversibile.", "RESET", "Annulla"))
            return;

        PlayerPrefs.DeleteKey("STR_M5_Wallet");
        PlayerPrefs.DeleteKey("STR_M5_TotalEarned");
        for (int i = 0; i < 32; i++)
        {
            PlayerPrefs.DeleteKey("STR_V2_Skill_" + i);
            PlayerPrefs.DeleteKey("STR_M5_Skill_" + i);
        }
        foreach (ScratchSkillCatalogV2.Entry entry in ScratchSkillCatalogV2.All)
            PlayerPrefs.DeleteKey("STR_GDDV2_Skill_" + entry.id);
        // A human playtest must start at chapter 1, even after QA autoplay.
        for (int tier = 0; tier < 4; tier++)
            PlayerPrefs.DeleteKey("STR_Radial_CompletedTier_" + tier);
        foreach (string key in new[]
        {
            "STR_GDDV2_Migrated_01", "STR_GDDV2_TitanCharges",
            "STR_GDDV2_TitanProgress", "STR_GDDV2_TitanUsed",
            "STR_GDDV2_JollyGoldBonus", "STR_GDDV2_JollyUsed",
            "STR_GDDV2_WinStreak", "STR_GDDV2_DemoCapstoneActive",
            "STR_GDDV2_DemoGoldTickets", "STR_GDDV2_FinalGoldPayout",
            "STR_GDDV2_FinalRuleVersion", "STR_GDDV2_DemoTitanBaseline",
            "STR_GDDV2_DemoJollyBaseline", "STR_GDDV2_DemoFinished"
        })
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log("STR V2: wallet, 90 abilita', contatori Radial e finale resettati. Avvia Play Mode per iniziare da zero.");
    }

    private void OnEnable()
    {
        EditorApplication.update -= UpdateVideoValidation;
        EditorApplication.update += UpdateTest;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateTest;
        if (running) Finish(false, "Tester chiuso prima del termine.");
        // MP4 finalization can outlive the window: finish validation and update summary.
        if (videoValidationPending)
        {
            EditorApplication.update -= UpdateVideoValidation;
            EditorApplication.update += UpdateVideoValidation;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SCRATCH TO RICHES - AUTOPLAY DEMO V2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(IsFullDemo
            ? "TEST DEMO COMPLETA: parte da zero, acquista tutte le 24 abilita' della demo " +
              "(incluse le dipendenze), gratta biglietti reali fino al finale G08/F04 " +
              "e misura la durata effettiva contro l'obiettivo di 20-25 minuti."
            : "TEST LEGACY: verifica separatamente le tre categorie di biglietti con acquisti e grattate UI.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(running))
        {
            autoplayMode = (AutoplayMode)EditorGUILayout.EnumPopup("Tipo di test", autoplayMode);
            if (IsFullDemo) { qaFunds = false; startFresh = true; }
            using (new EditorGUI.DisabledScope(IsFullDemo))
            {
                qaFunds = EditorGUILayout.ToggleLeft("Credito QA per gli sblocchi (100.000 monete)", qaFunds);
                startFresh = EditorGUILayout.ToggleLeft("Azzera progressi prima del test (solo Play Mode)", startFresh);
            }
            humanSpeed = EditorGUILayout.Slider("Velocita' gesti (1 = umano)", humanSpeed, 0.5f, 4f);
            freshSeedEachRun = EditorGUILayout.ToggleLeft("Nuova partita casuale a ogni avvio", freshSeedEachRun);
            using (new EditorGUI.DisabledScope(freshSeedEachRun))
                seed = EditorGUILayout.IntField("Seed riproducibile", seed);
            maxSessionMinutes = EditorGUILayout.Slider("Timeout massimo (min)", maxSessionMinutes, 5f, 240f);
            recordVideo = EditorGUILayout.ToggleLeft("REGISTRA VIDEO MP4 H.264 (Unity Recorder)", recordVideo);
            if (recordVideo)
            {
                videoHeight = EditorGUILayout.IntPopup("Risoluzione MP4", videoHeight,
                    new[] { "HD 1280x720", "Full HD 1920x1080" }, new[] { 720, 1080 });
                videoFrameRate = EditorGUILayout.IntPopup("FPS MP4", videoFrameRate,
                    new[] { "30 (consigliato)", "60" }, new[] { 30, 60 });
                recordGameAudio = EditorGUILayout.ToggleLeft("Includi audio del gioco", recordGameAudio);
            }
            captureFrames = EditorGUILayout.ToggleLeft("Salva anche PNG di diagnostica (opzionale)", captureFrames);
            if (captureFrames)
                framesPerSecond = EditorGUILayout.IntSlider("FPS PNG", framesPerSecond, 1, 5);
        }

        if (autoplayMode == AutoplayMode.SimulazioneReale)
            EditorGUILayout.HelpBox("SIMULAZIONE REALE: tutti i biglietti hanno simboli casuali. " +
                "Il bot sceglie usando SOLO grattate e anteprime disponibili; nessun tris, poker o premio viene forzato. " +
                "Gli sblocchi seguono ancora un piano di test prestabilito.", MessageType.Info);
        else if (autoplayMode == AutoplayMode.QACombinazioniForzate)
            EditorGUILayout.HelpBox("QA COMBINAZIONI FORZATE: alcune partite impongono coppia, tris, poker e cinquina " +
                "e selezionano le stesse posizioni per verificare i pagamenti. La ripetizione e' INTENZIONALE e non " +
                "misura la fortuna naturale del gioco.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("NESSUN CHEAT: progressi e portafoglio azzerati; 24 skill demo; " +
                "simboli casuali; scelte solo da grattate e anteprime visibili; " +
                "finale con tre veri Dorati e almeno un JOLLY grattato. " +
                "Il test misura la durata, non la forza artificialmente.", MessageType.Info);

        if (qaFunds)
            EditorGUILayout.HelpBox("Credito QA: sblocchi rapidi. Non misura l'economia naturale e le monete restano nel wallet fino al reset.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Senza credito QA il tester grattera' altri biglietti per finanziare ogni acquisto. Potrebbero servire molte ore: aumenta il timeout massimo.", MessageType.Info);
        if (recordVideo)
            EditorGUILayout.HelpBox("Il MP4 viene creato direttamente con Unity Recorder 5.1.8. Nessun FFmpeg richiesto. " +
                "Registra la Game View dall'inizio fino al completamento o all'errore; il file viene finalizzato alla chiusura del test.", MessageType.None);
        if (captureFrames)
            EditorGUILayout.HelpBox("I PNG sono soltanto materiale diagnostico aggiuntivo e possono occupare molto spazio.", MessageType.None);

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || running))
        {
            if (GUILayout.Button("START - TEST COMPLETO", GUILayout.Height(34)))
                StartTest();
        }
        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button("STOP E SALVA REPORT", GUILayout.Height(24)))
                Finish(false, "Interrotto manualmente.");
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Stato: " + lastStatus, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(IsFullDemo
            ? "Biglietti: " + (tickets + farmTickets) + " | Skill acquistate: " +
              purchaseIndex + "/" + FullDemoPlan.Length + " | Gold finale: " +
              (game != null ? game.DemoFinalGoldTickets : 0) + "/" +
              (game != null ? game.DemoFinalGoldMinTickets : 8)
            : "Biglietti test: " + tickets + " / 13  |  Farm: " + farmTickets);
        EditorGUILayout.LabelField("Partite casuali: " + naturalTicketsPlayed +
            " | QA forzate: " + forcedTicketsPlayed);
        EditorGUILayout.LabelField("Primi movimenti diversi: " + uniqueFirstChoices.Count);
        EditorGUILayout.LabelField("Scelte ragionate: " + informedChoices + " | Anteprime usate: " + previewChoices);
        EditorGUILayout.LabelField("Verifiche OK: " + assertionsPassed + "    Errori: " + assertionsFailed);
        EditorGUILayout.LabelField("Video: " + videoStatus, EditorStyles.wordWrappedLabel);
        if (running)
            EditorGUILayout.LabelField(IsFullDemo
                ? "Fase: " + phase + " | " + (ScratchGameManager.TicketKind)stage +
                  " | Upgrade " + (purchaseIndex + 1) + "/" + FullDemoPlan.Length
                : "Fase: " + phase + " | Tier " + (stage + 1) + " | Test " + (patternIndex + 1));
        if (!string.IsNullOrEmpty(reportDirectory))
        {
            if (GUILayout.Button("Apri cartella registrazioni / report"))
                EditorUtility.RevealInFinder(reportDirectory);
            EditorGUILayout.SelectableLabel(reportDirectory, GUILayout.Height(36));
        }

        GUILayout.Space(6);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("RESET COMPLETO PROGRESSI (fuori Play)"))
                ResetProgressMenu();
        }
    }

    public static bool AnyTestRunning
    {
        get
        {
            foreach (ScratchAutoplayWindow window in Resources.FindObjectsOfTypeAll<ScratchAutoplayWindow>())
                if (window != null && window.running) return true;
            return false;
        }
    }
}
#endif
