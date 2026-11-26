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
    [MenuItem("Tools/Scratch to Riches/Gameplay & Visual QA")]
    private static void ShowWindow()
    {
        GetWindow<ScratchAutoplayWindow>("Gameplay & Visual QA").minSize = new Vector2(390, 460);
    }

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
        EditorGUILayout.LabelField("GAMEPLAY & VISUAL QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Demo naturale da zero, senza credito o vincite forzate. MP4 obbligatorio; Unity continua anche in background. Il test attende la schermata risultati prima di proseguire.", MessageType.Info);
        using (new EditorGUI.DisabledScope(running || videoValidationPending))
        {
            humanSpeed = EditorGUILayout.Slider("Velocita' gesti (1 = umano)", humanSpeed, 0.5f, 4f);
            freshSeedEachRun = EditorGUILayout.ToggleLeft("Nuova partita casuale a ogni avvio", freshSeedEachRun);
            using (new EditorGUI.DisabledScope(freshSeedEachRun))
                seed = EditorGUILayout.IntField("Seed riproducibile", seed);
            maxSessionMinutes = EditorGUILayout.Slider("Timeout massimo (min)", maxSessionMinutes, 5f, 240f);
            EditorGUILayout.LabelField("MP4 per condivisione", "720p / 30 FPS / 1,45 Mbps");
            EditorGUILayout.HelpBox("Run completa, budget 400 MB. Audio facoltativo. Se necessario, ricompressione con FFmpeg disponibile nel PATH.", MessageType.Info);
            recordGameAudio = EditorGUILayout.ToggleLeft("Includi audio del gioco", recordGameAudio);
            captureFrames = EditorGUILayout.ToggleLeft("Salva anche PNG di diagnostica (opzionale)", captureFrames);
            if (captureFrames)
                framesPerSecond = EditorGUILayout.IntSlider("FPS PNG", framesPerSecond, 1, 5);
            if (GUILayout.Button("CONTROLLI PRELIMINARI (Console)"))
            {
                ScratchV2SceneAudit.AuditScene();
                ScratchS02VisualAudit.Audit();
                ScratchVintageHudAudit.Audit();
                ScratchVintageTicketAudit.Audit();
                ScratchAutoplayPreviewStrategy.RunDecisionTests();
                TestFullDemoPlan();
                TestTicketBudget();
            }
            if (GUILayout.Button("Registrazione manuale (opzionale)"))
                ScratchManualPlaytestRecorderWindow.Open();
        }

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || running || videoValidationPending))
        {
            if (GUILayout.Button("START - TEST COMPLETO", GUILayout.Height(34)))
                StartTest();
            if (GUILayout.Button("TEST RAPIDO TRANSIZIONI", GUILayout.Height(30)))
                ScratchTransitionQA.Start();
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
