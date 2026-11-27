#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

// Isolated editor QA storage. Normal play forwards exactly to Unity PlayerPrefs.
// The profile starts before Awake; no real save key is changed by this class.
public static class ScratchGameFeelPrefs
{
    public const string SessionKey = "STR.GameFeelQA.Active";
    private static Dictionary<string, object> sandbox;
    public static bool Active => SessionState.GetBool(SessionKey, false);

    public static void Begin()
    {
        sandbox = null;
        SessionState.SetBool(SessionKey, true);
    }

    public static void End()
    {
        sandbox = null;
        SessionState.SetBool(SessionKey, false);
    }

    private static Dictionary<string, object> Data
    {
        get
        {
            if (sandbox == null)
                sandbox = new Dictionary<string, object>
                {
                    { "STR_M5_Wallet", 500 },
                    { "STR_M5_TotalEarned", "0" },
                    { "STR_GDDV2_Migrated_01", 1 },
                    { "STR_GDDV2_Skill_C03", 1 },
                    { "STR_GDDV2_FinalRuleVersion", 3 }
                };
            return sandbox;
        }
    }

    public static int GetInt(string key, int fallback = 0) => !Active
        ? UnityEngine.PlayerPrefs.GetInt(key, fallback)
        : Data.TryGetValue(key, out object value) && value is int n ? n : fallback;
    public static string GetString(string key, string fallback = "") => !Active
        ? UnityEngine.PlayerPrefs.GetString(key, fallback)
        : Data.TryGetValue(key, out object value) && value is string s ? s : fallback;
    public static void SetInt(string key, int value)
    { if (Active) Data[key] = value; else UnityEngine.PlayerPrefs.SetInt(key, value); }
    public static void SetString(string key, string value)
    { if (Active) Data[key] = value; else UnityEngine.PlayerPrefs.SetString(key, value); }
    public static void DeleteKey(string key)
    { if (Active) Data.Remove(key); else UnityEngine.PlayerPrefs.DeleteKey(key); }
    public static void Save()
    { if (!Active) UnityEngine.PlayerPrefs.Save(); }
}
#endif
