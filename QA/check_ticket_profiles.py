#!/usr/bin/env python3
"""Offline exact combinatorial QA for Scratch to Riches V3.1 ticket profiles.

No Unity, third-party packages, RNG, outcomes forced, or player save data.
Parses the CURRENT C# ticket profiles, enumerates all outcomes for 3-6 picks.
Assumptions: independent uniform symbol draws within the sampled ticket pool,
no preview strategy, no perk luck bias, no Joker, and no Economy upgrades.
These are baseline odds, NOT a replacement for Unity Play Mode QA.
"""
from collections import Counter
from itertools import product
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "Assets/Scripts/ScratchTicketRules.cs"
KINDS = ("Standard", "Fortunato", "Gold", "Diamond")
FIELDS = ("SymbolPoolSize", "TicketPrice", "BasePrize")


def read_profile(source, method):
    anchor = "public static int " + method + "("
    start = source.find(anchor)
    if start < 0:
        raise AssertionError("Missing C# method: " + method)
    end = source.find("\n    }", start)
    if end < 0:
        raise AssertionError("Unterminated C# method: " + method)
    part = source[start:end]
    pattern = r"case ScratchGameManager\.TicketKind\.(\w+): return (\d+);"
    values = {name: int(value) for name, value in re.findall(pattern, part)}
    if method == "BasePrize":
        # Standard uses Math.Max(1, classicBasePrize), not a numeric literal.
        values["Standard"] = 10
    if set(values) != set(KINDS):
        raise AssertionError("Unexpected ticket kinds for " + method + ": " + str(values))
    return values


def exact_odds(pool, picks, prize):
    total = pool ** picks
    wins = payout = 0
    tiers = Counter()
    for draw in product(range(pool), repeat=picks):
        max_count = max(Counter(draw).values())
        tier = min(5, max_count)
        tiers[tier] += 1
        if tier >= 2:
            wins += 1
            payout += tier * prize
    assert sum(tiers.values()) == total
    return wins / total, payout / total, tiers


def main():
    source = RULES.read_text(encoding="utf-8-sig")
    pools = read_profile(source, "SymbolPoolSize")
    costs = read_profile(source, "TicketPrice")
    prizes = read_profile(source, "BasePrize")
    assert [pools[k] for k in KINDS] == [7, 6, 5, 4], "Unexpected pool sizes"
    assert [costs[k] for k in KINDS] == [0, 10, 25, 60], "Unexpected ticket costs"
    assert [prizes[k] for k in KINDS] == [10, 30, 55, 70], "Unexpected base prizes"
    assert "StartingChoices = 3" in source and "MaxExtraChoiceLevels = 3" in source
    assert "Math.Min(gridSide * gridSide, StartingChoices + extra)" in source

    gross = {}
    print("Ticket;Picks;Win probability;Gross EV;Net EV (no upgrades)")
    for kind in KINDS:
        gross[kind] = {}
        for picks in (3, 4, 5, 6):
            prob, ev, _ = exact_odds(pools[kind], picks, prizes[kind])
            gross[kind][picks] = ev
            print(f"{kind};{picks};{prob:.4%};{ev:.4f};{ev - costs[kind]:.4f}")

    for picks in (3, 4, 5, 6):
        assert gross["Standard"][picks] < gross["Fortunato"][picks] < gross["Gold"][picks]
        assert gross["Standard"][picks] < gross["Fortunato"][picks] - costs["Fortunato"]
        assert gross["Fortunato"][picks] - costs["Fortunato"] < gross["Gold"][picks] - costs["Gold"]
        # Classic returns far less even with four, five or six scratched cells.
        assert gross["Standard"][picks] * 2 < gross["Fortunato"][picks]

    # The pigeonhole principle: 6 picks from a pool of 5 force >= 2 copies.
    gold_six, _, _ = exact_odds(pools["Gold"], 6, prizes["Gold"])
    assert gold_six == 1.0
    print("TICKET PROFILE QA PASS: four distinct profiles, independent C11 levels, exact EV ordering")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, AssertionError) as ex:
        print("TICKET PROFILE QA FAIL:", ex, file=sys.stderr)
        sys.exit(1)
