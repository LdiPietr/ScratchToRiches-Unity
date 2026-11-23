#!/usr/bin/env python3
"""Scratch to Riches: static and Unity-report regression checks (standard library only).

Static: python QA/verify_demo_regression.py --static
Unity:  python QA/verify_demo_regression.py --summary summary.txt \
           --checks checks.csv --tickets tickets.csv --progression progression.csv

The baseline is an observed run, NOT an expectation of identical RNG or timestamps.
"""
import argparse
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "QA" / "Baselines" / "DemoV3_2026-11-23.json"
errors = []


def check(condition, message):
    if not condition:
        errors.append(message)


def read_csv(path):
    with Path(path).open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle, delimiter=";"))


def check_static():
    catalog = json.loads((ROOT / "Assets/Resources/SkillCatalogV2.json").read_text(encoding="utf-8-sig"))
    skills = catalog["skills"]
    by_id = {skill["id"]: skill for skill in skills}
    check(len(skills) == 90 and len(by_id) == 90, "Catalog must have 90 unique IDs")
    check(sum(bool(s["demoEligible"]) for s in skills) == 25, "25 demo abilities required")

    required = {
        "C01": "", "C03": "C01:1", "C02": "C03:1",
        "C04": "C02:1", "C05": "C04:1",
        "C06": "C05:1", "C11": "C01:1", "G08": "G07:1;G05:1;C05:1",
    }
    for skill_id, prereq in required.items():
        check(skill_id in by_id and by_id[skill_id]["prerequisites"] == prereq,
              "Changed prerequisites for " + skill_id)

    code = (ROOT / "Assets/Scripts/Editor/ScratchAutoplayWindow.cs").read_text(encoding="utf-8-sig")
    start = code.find("private static readonly ScratchSkillTree.SkillId[] FullDemoPlan =")
    end = code.find("private bool IsFullDemo", start)
    check(start != -1 and end != -1, "FullDemoPlan is missing")
    plan_source = code[start:end] if start >= 0 and end > start else ""
    plan = [m[0] + m[1:].zfill(2) for m in
            re.findall(r"ScratchSkillTree\.SkillId\.([GCEF]\d+)", plan_source)]
    check(len(plan) == 28 and len(set(plan)) == 25, "28 purchases / 25 distinct skills required")
    owned = {}
    for skill_id in plan:
        skill = by_id.get(skill_id)
        if skill is None:
            check(False, "Unknown skill in plan: " + skill_id)
            continue
        for rule in filter(None, skill.get("prerequisites", "").split(";")):
            parent, level = rule.split(":")
            check(owned.get(parent, 0) >= int(level), skill_id + " requires " + rule)
        owned[skill_id] = owned.get(skill_id, 0) + 1
        check(owned[skill_id] <= skill["maxLevel"], "Excess level: " + skill_id)
    sequence = ["C01", "C03", "C02", "G07", "C04", "C05", "G08", "C06", "F04"]
    check(plan.count("C11") == 2 and plan.index("C11") < plan.index("C03"),
          "Two C11 purchases required, first before Fortunato")
    check(all(x in plan for x in sequence) and
          all(plan.index(a) < plan.index(b) for a, b in zip(sequence, sequence[1:])),
          "Major upgrade sequence changed")

    game = (ROOT / "Assets/Scripts/ScratchGameManager.cs").read_text(encoding="utf-8-sig")
    cell = (ROOT / "Assets/Scripts/ScratchCell.cs").read_text(encoding="utf-8-sig")
    check("FinalGoldPayoutTarget = 3800" in game and
          "FinalGoldMinTickets = 8" in game and
          "FinalChallengeComplete(" in game and
          'skillTree.GetLevel("F04") == 0' in game,
          "Performance-based finale changed")
    check("TryInspectCell(ScratchCell cell)" in game and
          "InputButton.Right" in cell and "KeyCode.LeftControl" in cell,
          "Manual C06 preview controls changed")
    check("QaActivateTitanDuringGesture()" in cell and
          "G08_real_titan_activation" in code, "Titan gesture QA removed")
    check("C06_chosen_preview" in code, "C06 QA removed")
    check("global_pick_perk" in code, "Independent pick-cap QA removed")
    check("PickLimitFromPerk(" in game and
          "3 + extraPickLevel" in game and
          "TicketSide(currentTicketKind);" not in game.split("public int MaxScratches =>")[1].split(";")[0],
          "Pick count still tied to ticket tier")
    check('case TicketKind.Fortunato: return 50;' in game and
          'case TicketKind.Gold: return 110;' in game and
          'case TicketKind.Diamond: return 250;' in game,
          "Distinct ticket prizes or prize tiers changed")
    check(by_id["C11"]["maxLevel"] == 2 and by_id["C11"]["demoEligible"]
          and by_id["C11"]["phase"].startswith("Demo"),
          "C11 two-level global perk must be available in demo")
    radial = (ROOT / "Assets/Scripts/ScratchRadialProgression.cs").read_text(encoding="utf-8-sig")
    ui = (ROOT / "Assets/Scripts/ScratchSkillTreeUI.cs").read_text(encoding="utf-8-sig")
    scene = (ROOT / "Assets/Scenes/ScratchToRiches.unity").read_text(encoding="utf-8-sig")
    check("ChapterOf(string id)" in radial and "UnlockedChapter(" in radial and
          "CompletedByTier(" in radial, "Radial chapter milestones missing")
    check(all(('case "' + sid + '": return ' + str(amount) + ';') in radial
              for sid, amount in {"E06": 16, "G07": 19, "C04": 22, "C05": 24,
                                  "G08": 5, "F02": 8, "C06": 11,
                                  "F03": 14, "F04": 17}.items()) and
          "TicketGoalMet(" in radial and "SkillGoal(" in radial,
          "Progressive Fortunato/Gold chapter milestone gates missing")
    check("ScratchRadialProgression.Available(" in
          (ROOT / "Assets/Scripts/ScratchSkillTree.cs").read_text(encoding="utf-8-sig"),
          "Purchase authority bypasses radial chapters")
    navigation = (ROOT / "Assets/Scripts/ScratchRadialMapNavigation.cs").read_text(encoding="utf-8-sig")
    node = (ROOT / "Assets/Scripts/ScratchHoldToBuy.cs").read_text(encoding="utf-8-sig")
    check("OnScroll(PointerEventData data)" in navigation and
          "OnDrag(PointerEventData data)" in navigation and
          "CenterOn(Vector2 positionInContent)" in navigation,
          "Single map lost pan/zoom or centering")
    audit = (ROOT / "Assets/Scripts/Editor/ScratchV2SceneAudit.cs").read_text(encoding="utf-8-sig")
    check("navigation.GetComponentsInChildren<ScratchHoldToBuy>(true)" in ui,
          "Map node discovery must use the authored shop viewport, not manager hierarchy")
    check("Object.FindAnyObjectByType<ScratchRadialLinks>(FindObjectsInactive.Include)" in audit and
          "Object.FindAnyObjectByType<ScratchRadialMapNavigation>(FindObjectsInactive.Include)" in audit,
          "Scene audit must find inactive shop graphics and navigation, not only direct manager children")
    check("HoverSkill(string id)" in ui and "ClearHoverSkill(string id)" in ui and
          "DrawDetails()" in ui and "selectedDescription.text = entry.description" in ui,
          "Fixed bottom hover panel no longer reports actual skills")
    check("stableSkillId" in node and "CancelForMapPan" in node and
          "CanPurchaseSkill(stableSkillId)" in node,
          "Map purchase hold/input detached from stable skill IDs")
    check(scene.count("m_Name: MapSkillNode_") == 90 and
          scene.count("  stableSkillId: ") == 90 and
          "m_Name: RadialMapViewport" in scene and
          "m_Name: RadialMapContent" in scene and
          "m_Name: RadialConnections" in scene and
          "guid: 3312d7739989d2b4e91e6319e9a96d76" in scene and
          "nucleusButton:" in scene and "navigation:" in scene,
          "90 square scene nodes or masked navigable map are missing")
    ids = set(re.findall(r"m_Name: MapSkillNode_([GCEFA]\d{2})", scene))
    check(ids == set(by_id), "Authored 90 square map nodes do not match catalog IDs")
    check("NUCLEUS_WAIT" in code and "MapSkillNode_" in code,
          "Autoplay does not navigate authored map or locked chapters")

    # Verify Unity YAML parentage and identity without relying on an installed
    # Unity editor. 90 authored icons, not 90 runtime-instantiated buttons.
    blocks = {}
    for chunk in re.split(r"(?=^--- !u!\d+ &\d+)", scene, flags=re.MULTILINE):
        match = re.match(r"^--- !u!\d+ &(\d+)", chunk)
        if match:
            key = match.group(1)
            check(key not in blocks, "Duplicate Unity YAML object ID " + key)
            blocks[key] = chunk
    check(len(blocks) >= 2674, "Radial map scene lost authored objects")

    for field in ("component", "m_GameObject", "m_Father"):
        for ref in re.findall(r"^ *" + field + r": \{fileID: (\d+)\}", scene, re.MULTILINE):
            check(ref == "0" or ref in blocks,
                  "Broken authored " + field + " reference: " + ref)

    content = blocks.get("810000000012", "")
    check("m_Father: {fileID: 810000000002}" in content and
          content.count("  - {fileID: ") == 92,
          "Map must contain 90 nodes + nucleus + connecting lines")
    check("m_Father: {fileID: 810000000012}" in blocks.get("374", "") and
          "nucleusButton: {fileID: 800000000010}" in scene,
          "Nucleus must be inside the pannable map")
    check("m_Father: {fileID: 810000000012}" in blocks.get("800000000002", ""),
          "All radial connections must pan together with nodes")
    check("guid: 3312d7739989d2b4e91e6319e9a96d76" in
          blocks.get("810000000005", ""),
          "Viewport must have real uGUI RectMask2D")

    for skill_id in by_id:
        named = [chunk for chunk in blocks.values()
                 if re.search(r"^  m_Name: MapSkillNode_" + skill_id + r"$",
                              chunk, flags=re.MULTILINE)]
        check(len(named) == 1, "Missing/duplicate scene node " + skill_id)
        if not named:
            continue
        root = named[0]
        check(re.search(r"^  m_IsActive: 1$", root, flags=re.MULTILINE) is not None,
              "Authored map node inactive: " + skill_id)
        check("component: {fileID: 2000020085}" not in root,
              "Map node still references legacy shared Button: " + skill_id)
        root_id_match = re.search(r"^--- !u!1 &(\d+)", root)
        if root_id_match:
            root_id = root_id_match.group(1)
            for component_id in re.findall(r"^  - component: \{fileID: (\d+)\}$",
                                           root, flags=re.MULTILINE):
                component = blocks.get(component_id, "")
                check("m_GameObject: {fileID: " + root_id + "}" in component,
                      "Map component owner mismatch: " + skill_id +
                      " / " + component_id)
        rt_match = re.search(r"component: \{fileID: (\d+)\}", root)
        rect = blocks.get(rt_match.group(1), "") if rt_match else ""
        check("m_Father: {fileID: 810000000012}" in rect and
              "m_SizeDelta: {x: 116, y: 116}" in rect,
              "Node not a square inside map: " + skill_id)

    nav = (ROOT / "Assets/Scripts/ScratchRadialMapNavigation.cs").read_text(encoding="utf-8-sig")
    check("RectTransformUtility.ScreenPointToLocalPointInRectangle" in nav and
          "CancelForMapPan()" in nav, "Map drag and hold safety missing")



    # Human playtest capture must stay separate from bot-driven QA and not
    # alter game rules or exceed the upload-safe recording profile.
    manual = (ROOT / "Assets/Scripts/Editor/ScratchManualPlaytestRecorderWindow.cs").read_text(encoding="utf-8-sig")
    telemetry = (ROOT / "Assets/Scripts/ScratchManualTelemetry.cs").read_text(encoding="utf-8-sig")
    recorder = (ROOT / "Assets/Scripts/Editor/ScratchAutoplayMovieRecorder.cs").read_text(encoding="utf-8-sig")
    check("Registra partita manuale" in manual and
          "ScratchAutoplayWindow.AnyTestRunning" in manual and
          "ScratchManualTelemetry.Logged += OnGameEvent" in manual and
          "ScratchManualTelemetry.Logged -= OnGameEvent" in manual,
          "Manual recording must have independent opt-in lifecycle")
    check("SegmentSeconds = 30 * 60" in manual and
          "EarlyRotateBytes = 350L * 1024L * 1024L" in manual and
          "SafePartBytes = 400L * 1024L * 1024L" in manual and
          "VideoHeight = 720" in manual and "VideoFps = 24" in manual and
          "VideoMbps = 1.5f" in manual and
          "FrameRatePlayback.Variable" in recorder and
          "TargetBitRate" in recorder,
          "Manual video must be compact, segmented, time-true and size checked")
    mapui = (ROOT / "Assets/Scripts/ScratchSkillTreeUI.cs").read_text(encoding="utf-8-sig")
    check("JumpToNextPurchase" in mapui and
          "purchasableIds.Sort(ComparePurchasable)" in mapui and
          "availableJumpButton.onClick.AddListener(JumpToNextPurchase)" in mapui and
          "Input.GetKeyDown(KeyCode.Tab)" in mapui and
          "outline.effectColor" in mapui and
          "skills.CanPurchase(e.id)" in mapui and
          "ORA PUOI COMPRARE" in mapui,
          "Human radial map must make actually affordable upgrades findable")
    check("public static event Action<string, string> Logged;" in telemetry and
          "public static void Emit(" in telemetry,
          "Manual event bus missing")
    for script, events in {
        "ScratchGameManager.cs": ["TICKET_START", "CELL_SELECTED", "CELL_COMPLETED",
                                  "C06_INSPECTION", "TICKET_END", "FINAL_CHALLENGE_STARTED"],
        "ScratchSkillTree.cs": ["SKILL_PURCHASED", "TITAN_USED", "JOLLY_SCRATCHED"],
        "ScratchFlowPresentation.cs": ["SHOP_OPEN", "SHOP_CLOSE"],
        "ScratchSkillTreeUI.cs": ["SKILL_HOVER"],
        "ScratchRadialMapNavigation.cs": ["MAP_PAN", "MAP_ZOOM"],
    }.items():
        source = (ROOT / "Assets/Scripts" / script).read_text(encoding="utf-8-sig")
        check(all('ScratchManualTelemetry.Emit("' + name + '"' in source for name in events),
              "Human telemetry hook missing from " + script)
    check(not (ROOT / "Assets/Scripts/Editor/ScratchCellManagerEditor.cs").exists(),
          "Obsolete Inspector reset tool is still present")
    check('Test strategia anteprime")' not in
          (ROOT / "Assets/Scripts/Editor/ScratchAutoplayPreviewStrategy.cs").read_text(encoding="utf-8-sig") and
          'Test riserva biglietti autoplay")' not in
          (ROOT / "Assets/Scripts/Editor/ScratchAutoplayWindow.cs").read_text(encoding="utf-8-sig"),
          "Redundant test menus were not hidden")


def check_unity_logs(summary_path, checks_path, tickets_path, progression_path, base):
    text = Path(summary_path).read_text(encoding="utf-8-sig")
    fields = dict(line.split(": ", 1) for line in text.splitlines() if ": " in line)
    def field(name):
        return fields.get(name, "").strip()
    def number(name):
        return float(field(name).split("/")[0].strip())

    check(field("Stato").startswith("COMPLETATO"), "Unity test did not finish")
    check(field("Modalita'") == "ProgressioneDemoCompleta", "Wrong test mode")
    check(field("DemoFinished reale") == "True", "DemoFinished is false")
    check(field("Credito QA incluso") == "False", "QA coins were enabled")
    check(field("Durata non accelerata artificialmente") == "True", "Speed changed")
    check(field("Acquisti completati") == "28/28", "Not all 28 purchases")
    check(field("Skill demo possedute") == "25/25", "Not all 25 skills")
    check(number("Biglietti con combinazione forzata") == 0, "Forced outcomes detected")
    check(number("Biglietti con combinazione NON forzata") ==
          number("Biglietti giocati totali"), "Not every ticket was natural")
    check(number("Check KO") == 0, "Unity reported failed assertions")
    check(number("Check OK") > 0, "Unity assertions missing")
    check(20 * 60 <= number("Durata reale (s)") <= 25 * 60,
          "Duration outside 20-25 minute demo target")
    check(number("Dorati finali") >= 8, "Insufficient final Gold tickets")
    check(number("Incassi Dorati finali") >= 3800, "Final earnings below 3800")
    check(field("Potere finale attivato") == "True", "Final power not activated")
    check(number("Ispezioni C06") > 0, "C06 was not exercised")
    check(number("Gestualita' Titanico G08") > 0, "G08 was not exercised")
    check(number("Jolly grattati") > 0, "No real joker scratched")

    checks = read_csv(checks_path)
    check(bool(checks) and all(row["result"] == "PASS" for row in checks),
          "checks.csv contains failures or is empty")
    check(len(checks) == int(number("Check OK")), "check count and summary disagree")

    history = read_csv(progression_path)
    purchases = [row for row in history if row["event"] == "SKILL_PURCHASED"]
    settled = [row for row in history if row["event"] == "TICKET_SETTLED"]
    check(len(purchases) == 28 and len(set(row["skill_id"] for row in purchases)) == 25,
          "Progression CSV purchase count mismatch")
    check(len(settled) == int(number("Biglietti giocati totali")),
          "Settled ticket count mismatch")
    check(any(row["event"] == "DEMO_FINISHED" for row in history), "End event missing")
    # These are earned-ticket milestones, not timers. Validate at purchase time,
    # not only against the final totals (which could conceal an early purchase).
    required_fortunato = {"E06": 16, "G07": 19, "C04": 22, "C05": 24}
    required_gold = {"G08": 5, "F02": 8, "C06": 11, "F03": 14, "F04": 17}
    completed = {"Fortunato": 0, "Gold": 0}
    for row in history:
        if row["event"] == "TICKET_SETTLED" and row["kind"] in completed:
            completed[row["kind"]] += 1
        if row["event"] != "SKILL_PURCHASED":
            continue
        sid = row["skill_id"]
        if sid in required_fortunato:
            check(completed["Fortunato"] >= required_fortunato[sid],
                  sid + " bought before Fortunato goal")
        if sid in required_gold:
            check(completed["Gold"] >= required_gold[sid],
                  sid + " bought before Gold goal")
    check(completed["Fortunato"] >= 24, "Fewer than 24 Fortunato tickets")
    check(completed["Gold"] >= 25, "Fewer than 25 total Gold tickets")
    milestones = {row["skill_id"]: float(row["elapsed_s"]) for row in purchases}
    sequence = ["C01", "C03", "C02", "G07", "C04", "C05", "G08", "C06", "F04"]
    check(all(x in milestones for x in sequence) and
          all(milestones[a] < milestones[b] for a, b in zip(sequence, sequence[1:])),
          "Unity milestone order changed")

    events = read_csv(tickets_path)
    finished = [row for row in events if row["event"] in ("TICKET_END", "FARM_TICKET_END")]
    check(len(finished) == len(settled), "tickets.csv settlement count mismatch")
    check(all(row.get("pattern") == "Random" for row in finished),
          "Nonrandom ticket pattern in tickets.csv")
    print("Current: %.2f minutes, %d tickets, %d checks" %
          (number("Durata reale (s)") / 60, len(settled), len(checks)))
    print("Baseline: %.2f minutes, %d tickets, %d checks" %
          (base["duration_seconds"] / 60, base["tickets"]["total"],
           base["checked_assertions"]["passed"]))
    print("Time changes between seeds are NOT themselves regressions.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--static", action="store_true", help="Check source catalog and QA wiring")
    for name in ("summary", "checks", "tickets", "progression"):
        parser.add_argument("--" + name, type=Path)
    args = parser.parse_args()
    base = json.loads(BASELINE.read_text(encoding="utf-8"))
    paths = [args.summary, args.checks, args.tickets, args.progression]
    if args.static or not any(paths):
        check_static()
    if any(paths):
        if not all(paths):
            parser.error("Specify all four Unity report paths together")
        check_unity_logs(*paths, base)
    if errors:
        print("DEMO REGRESSION FAIL:")
        for item in errors:
            print("  - " + item)
        return 1
    print("DEMO REGRESSION PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
