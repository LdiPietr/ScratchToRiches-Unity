using System;
using TMPro;
using UnityEngine;

/// <summary>
/// M4 coin wallet. The wallet balance IS the score; rewards settle once per
/// finished round. Ticket purchasing and upgrades belong to M5.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchEconomyManager : MonoBehaviour
{
    [Header("Economy")]
    [SerializeField, Min(0)] private int startingCoins = 0;
    [SerializeField, Min(1)]
    [Tooltip("Standard ticket base payout: Pair x2, Triple x3, Poker x4.")]
    private int basePrize = 10;

    [Header("UI")]
    [SerializeField] private TMP_Text walletValue;

    private int coins;
    private int lastSettledRoundId;
    private int lastRoundWin;

    public int Coins => coins;
    public int Score => coins;
    public int BasePrize => basePrize;
    public int LastRoundWin => lastRoundWin;

    private void Awake()
    {
        // Runtime/session wallet, retained between rounds but not after a
        // fresh Play Mode session or application restart.
        coins = Mathf.Max(0, startingCoins);
        lastSettledRoundId = 0;
        lastRoundWin = 0;

        if (walletValue == null)
            Debug.LogError("ScratchEconomyManager needs WalletValue (TMP) assigned.", this);

        RefreshWallet();
    }

    public void BeginRound()
    {
        lastRoundWin = 0;
        RefreshWallet();
    }

    /// <summary>
    /// Award a completed round exactly once, including timeout and early end.
    /// Only M3's best fully-revealed match is eligible for coins.
    /// </summary>
    public int SettleRound(int roundId, ScratchSymbolManager.MatchResult result)
    {
        if (roundId <= 0 || roundId <= lastSettledRoundId)
            return 0;

        lastSettledRoundId = roundId;
        lastRoundWin = 0;

        if (result.IsMatch)
        {
            long requested = (long)Mathf.Max(1, basePrize) * result.Multiplier;
            long freeCapacity = (long)int.MaxValue - coins;
            lastRoundWin = (int)Math.Min(requested, freeCapacity);
            coins += lastRoundWin;
        }

        RefreshWallet();
        return lastRoundWin;
    }

    /// <summary>
    /// For the future M5 shop. No ticket cost is charged by the M4 round flow.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || amount > coins)
            return false;

        coins -= amount;
        RefreshWallet();
        return true;
    }

    private void RefreshWallet()
    {
        if (walletValue != null)
            walletValue.text = coins.ToString();
    }

    private void OnValidate()
    {
        startingCoins = Mathf.Max(0, startingCoins);
        basePrize = Mathf.Max(1, basePrize);
    }
}
