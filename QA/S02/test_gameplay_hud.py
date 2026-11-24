from pathlib import Path
import re
import yaml

ROOT = Path(__file__).resolve().parents[2]
scene = (ROOT / "Assets/Scenes/ScratchToRiches.unity").read_text(encoding="utf-8-sig")
blocks = {}
for b in re.split(r"(?=^--- !u!\d+ &\d+)", scene, flags=re.M):
    match = re.match(r"^--- !u!(\d+) &(\d+)", b)
    if not match:
        continue
    cls, id = match.groups()
    assert id not in blocks, id
    # Parse each Unity object independently; strip the Unity tag/header.
    # Unity emits a bare dash for some legacy TMP placeholder strings.
    # Normalize this one Unity scalar for the external YAML parser, not the scene.
    body = re.sub(r"^  m_text: -$", '  m_text: "-"', b.split("\n", 1)[1], flags=re.M)
    try:
        data = yaml.safe_load(body)
    except yaml.YAMLError as exc:
        raise AssertionError(f"Unity object {id}, class {cls}: {exc}") from exc
    blocks[id] = (cls, next(iter(data.values())))
assert len([x for x in blocks.values() if x[0] == "1" and re.fullmatch(r"Cell_\d+", x[1].get("m_Name",""))]) == 25
assert len([x for x in blocks.values() if x[0] == "1" and x[1].get("m_Name","").startswith("MapSkillNode_")]) == 90
hud = blocks["760000090001"][1]
assert hud["m_EditorClassIdentifier"].endswith("ScratchVintageGameplayHUD")
for k, v in hud.items():
    if k.startswith("m_"):
        continue
    if k == "indicators":
        assert len(v) == 5
        for i in v:
            assert blocks[str(i["fileID"])][0] == "114"
        continue
    obj = blocks[str(v["fileID"])]
    expected = "224" if k in ("progress","ticket") else "1" if k == "shopWalletPanel" else "225" if k == "ticketGroup" else "114"
    assert obj[0] == expected, (k,obj[0])
assert blocks["2000000001"][1]["vintageScale"] == 1
assert blocks["476"][1]["m_ScreenMatchMode"] == 1
assert blocks["9900010001"][1]["m_TargetGraphic"]["fileID"] == 760000010973
assert blocks["2000000003"][1]["m_GameObject"]["fileID"] == 357
assert blocks["9100000000"][1]["walletLabel"]["fileID"] == 760000010103
assert blocks["9100000000"][1]["matchLabel"]["fileID"] == 760000010873
for id,(cls,b) in blocks.items():
    if cls == "224":
        p = str(b["m_Father"]["fileID"])
        if p != "0":
            assert int(id) in [c["fileID"] for c in blocks[p][1]["m_Children"]], (id,p)
        for c in (b["m_Children"] or []):
            assert blocks[str(c["fileID"])][1]["m_Father"]["fileID"] == int(id)
    if cls == "1":
        for c in b["m_Component"]:
            assert blocks[str(c["component"]["fileID"])][1]["m_GameObject"]["fileID"] == int(id)
print("PASS: Unity YAML syntax, HUD references, original buttons, 25 cells, 90 skill nodes, uniform scale, hierarchy and ownership")
