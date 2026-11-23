#!/usr/bin/env python3
"""Exact uniform-symbol, no-upgrades ticket model for Scratch to Riches.

This does NOT simulate previews, player strategy, favored symbols, joker, or
streaks: it tests the baseline payout ladder and 3/4/5-pick probabilities.
"""
import itertools
import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
game = (ROOT / "Assets/Scripts/ScratchGameManager.cs").read_text(encoding="utf-8")
catalog = json.loads((ROOT / "Assets/Resources/SkillCatalogV2.json").read_text(encoding="utf-8"))
economy = (ROOT / "Assets/Scripts/ScratchEconomyManager.cs").read_text(encoding="utf-8")

skill = next(x for x in catalog["skills"] if x["id"] == "C11")
assert skill["maxLevel"] == 2 and skill["demoEligible"], "C11 must have two demo perk levels"
assert "3 + extraPickLevel" in game and "3, 5" in game, "C11 pick cap no longer independent"
assert "basePrize = 10" in economy, "Default Classic prize changed; recalibrate model"

def source_value(body, kind):
    pattern = r"case TicketKind\." + kind + r": return (\d+);"
    match = re.search(pattern, body)
    assert match, "Missing " + kind
    return int(match.group(1))

# Prices/prizes are stored in different methods of ScratchGameManager.
prices = game.split("public static int TicketPrice(TicketKind kind)",1)[1].split("public int TicketPrize(TicketKind kind)",1)[0]
prizes = game.split("public int TicketPrize(TicketKind kind)",1)[1].split("public static int TicketSide(TicketKind kind)",1)[0]
tiers = [
    ("CLASSICO", 7, 10, source_value(prices, "Standard")),
    ("FORTUNATO", 7, source_value(prizes, "Fortunato"), source_value(prices, "Fortunato")),
    ("DORATO", 7, source_value(prizes, "Gold"), source_value(prices, "Gold")),
    ("DIAMANTE", 4, source_value(prizes, "Diamond"), source_value(prices, "Diamond")),
]
assert [x[2] for x in tiers] == [10,50,110,250]
assert [x[3] for x in tiers] == [0,10,25,60]

def exact(pool,picks):
    outcomes = Counter()
    for symbols in itertools.product(range(pool),repeat=picks):
        group = max(Counter(symbols).values())
        outcomes[group] += 1
    total = pool ** picks
    win = sum(c for n,c in outcomes.items() if n >= 2)/total
    mult = sum(n*c for n,c in outcomes.items() if n >= 2)/total
    return win,mult

print("Base model: independent ticket tiers, uniform symbols, no preview/skills")
for picks in (3,4,5):
    model=[]
    for name,pool,base,cost in tiers:
        prob,mult=exact(pool,picks)
        model.append((name,prob,mult*base-cost))
        print("%d picks %-9s chance=%5.1f%%  expected net=%7.2f" %
            (picks,name,100*prob,mult*base-cost))
    classic=model[0][2]
    assert model[0][1] < model[1][1] + .00001, "Ticket kind must not give extra picks"
    assert model[1][2] > classic*3.5, "Fortunato must greatly out-earn Classic"
    assert model[2][2] > model[1][2]*2, "Gold must substantially exceed Fortunato"
    assert model[3][2] > model[2][2]*2, "Diamond must substantially exceed Gold"

p3,_=exact(7,3)
p4,_=exact(7,4)
p5,_=exact(7,5)
assert .38 < p3 < .40 and .64 < p4 < .66 and .84 < p5 < .86
assert exact(4,5)[0] == 1, "Five draws from four symbols always form a pair"
print("PICK / ECONOMY MODEL PASS")
