using System;
using TMPro;
using UnityEngine;

/// <summary>
/// M5 wallet: coins are the spendable score, saved between sessions.
/// Each ticket may settle once; skill bonuses are applied only at settlement.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchEconomyManager : MonoBehaviour
{
    private const string WalletKey = "STR_M5_Wallet";
    private const string TotalKey = "STR_M5_TotalEarned";

    [Header("Economy")]
    [SerializeField, Min(0)] private int startingCoins;
    [SerializeField, Min(1)] private int basePrize = 10;
    [SerializeField] private TMP_Text walletValue;

    private int coins;
    private long totalEarned;
    private int lastSettledTicketId;
    private int lastTicketWin;
    private ScratchSkillTree skills;

    public event Action Changed;
    public int Coins => coins;
    public int Score => coins;
    public int BasePrize => basePrize;
    public int LastTicketWin => lastTicketWin;
    public long TotalEarned => totalEarned;

    private void Awake()
    {
        skills = GetComponent<ScratchSkillTree>();
        coins = Mathf.Max(0, PlayerPrefs.GetInt(WalletKey, startingCoins));
        long.TryParse(PlayerPrefs.GetString(TotalKey, "0"), out totalEarned);
        lastSettledTicketId = 0;
        lastTicketWin = 0;
        RefreshWallet();
    }

    public void BeginTicket()
    {
        lastTicketWin = 0;
        Changed?.Invoke();
    }

    /// <summary>One M3 match per ticket, never one reward per scratch gesture.</summary>
    public int SettleTicket(int ticketId, ScratchSymbolManager.MatchResult match, bool allPicksCompleted)
    {
        if (ticketId <= 0 || ticketId <= lastSettledTicketId)
            return 0;

        lastSettledTicketId = ticketId;
        lastTicketWin = 0;

        if (match.IsMatch)
        {
            int rewardBase = Mathf.Max(1, basePrize) + 2 * Level(ScratchSkillTree.SkillId.E1);
            double multiplier = match.Tier == ScratchSymbolManager.MatchTier.Pair
                ? 1.0 + 0.1 * Level(ScratchSkillTree.SkillId.E2)
                : match.Tier == ScratchSymbolManager.MatchTier.Triple
                    ? 1.0 + 0.15 * Level(ScratchSkillTree.SkillId.E3)
                    : match.Tier == ScratchSymbolManager.MatchTier.Poker
                        ? 1.0 + 0.2 * Level(ScratchSkillTree.SkillId.E4) : 1.0;
            long requested = (long)Math.Ceiling(rewardBase * (double)match.Multiplier * multiplier);
            AwardCoins(requested);
        }
        else if (allPicksCompleted)
        {
            AwardCoins(3L * Level(ScratchSkillTree.SkillId.E5));
        }

        Save();
        RefreshWallet();
        return lastTicketWin;
    }

    private int Level(ScratchSkillTree.SkillId id) => skills != null ? skills.GetLevel(id) : 0;

    private void AwardCoins(long requested)
    {
        long actual = Math.Min(Math.Max(0L, requested), (long)int.MaxValue - coins);
        lastTicketWin = (int)actual;
        coins += lastTicketWin;
        totalEarned = Math.Min(long.MaxValue - actual, totalEarned) + actual;
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || amount > coins)
            return false;

        coins -= amount;
        Save();
        RefreshWallet();
        return true;
    }

    public void ResetWallet()
    {
        coins = Mathf.Max(0, startingCoins);
        totalEarned = 0;
        lastTicketWin = 0;
        lastSettledTicketId = 0;
        PlayerPrefs.DeleteKey(WalletKey);
        PlayerPrefs.DeleteKey(TotalKey);
        PlayerPrefs.Save();
        RefreshWallet();
    }

#if UNITY_EDITOR
    [ContextMenu("M5 QA - ADD 100000 COINS (Play Mode only)")]
    private void GrantTestCoins()
    {
        ScratchGameManager game = GetComponent<ScratchGameManager>();
        if (!Application.isPlaying || (game != null && game.State == ScratchGameManager.RoundState.Playing))
        {
            Debug.LogWarning("Test coins can only be granted outside a running game.", this);
            return;
        }
        long granted = Math.Min(100000L, (long)int.MaxValue - coins);
        coins += (int)granted;
        Save();
        RefreshWallet();
    }
#endif

    private void Save()
    {
        PlayerPrefs.SetInt(WalletKey, coins);
        PlayerPrefs.SetString(TotalKey, totalEarned.ToString());
        PlayerPrefs.Save();
    }

    private void RefreshWallet()
    {
        if (walletValue != null)
            walletValue.text = coins.ToString();
        Changed?.Invoke();
    }

    private void OnValidate()
    {
        startingCoins = Mathf.Max(0, startingCoins);
        basePrize = Mathf.Max(1, basePrize);
    }
}
