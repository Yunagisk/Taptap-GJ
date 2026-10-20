"""Idempotently install balance v2, without rebuilding scenes or UI."""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]


def edit(path, fields):
    raw = path.read_text("utf-8-sig")
    for key, value in fields.items():
        pattern = rf"^  {key}:.*$"
        if re.search(pattern, raw, re.M):
            raw = re.sub(pattern, lambda m: f"  {key}: {value}", raw, flags=re.M)
        else:
            raw = raw.rstrip() + f"\n  {key}: {value}\n"
    path.write_text(raw, "utf-8")


def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text("utf-8-sig"), re.M)[1]


def ref(path):
    return "{fileID: 11400000, guid: " + guid(path) + ", type: 2}"


def main():
    report = {"weapons": [], "monsters": []}
    basic = [[12], [16], [13], [5], [14], [6], [14], [5], [10], [7, 6], [7, 5], [7, 5]]
    skills = [[30], [36], [22], [5], [12], [9], [24], [10], [14], [8, 6], [12, 8], [10, 8]]
    for i in range(1, 13):
        for category, prefix, values in [("Weapons", "KBW", basic), ("Skills", "KBS", skills)]:
            path = ROOT / f"Assets/Data/{category}/{prefix}{i:02}.asset"
            raw = path.read_text("utf-8-sig")
            old = values[i - 1]
            new = [(n * 4 + 2) // 5 for n in old]
            current = [int(n) for n in re.findall(r"^\s+damage: (\d+)$", raw, re.M)]
            assert current in (old, new), (path, current)
            numbers = iter(new)
            raw = re.sub(r"^(\s+damage: )\d+$", lambda m: m[1] + str(next(numbers)), raw, flags=re.M)
            if category == "Skills" and current == old:
                match = re.search(r"^  skillDescription: (.+)$", raw, re.M)
                desc = json.loads(match[1])
                desc = re.sub(r"(穿刺|劈砍|钝击|火焰)(\d+)",
                              lambda m: m[1] + str((int(m[2]) * 4 + 2) // 5), desc)
                raw = raw[:match.start(1)] + json.dumps(desc, ensure_ascii=True) + raw[match.end(1):]
            path.write_text(raw, "utf-8")
            report["weapons"].append({"asset": str(path.relative_to(ROOT)), "old_components": old, "new_components": new})

    for name, hp in [("Rat", 36), ("Crow", 36), ("Wraith", 44)]:
        edit(ROOT / f"Assets/Data/SmallMonsters/{name}.asset", {"maxHp": hp})
        for suffix, weight in [("Basic", 45), ("Status", 20), ("Signature", 20), ("Guard", 15)]:
            fields = {"weight": weight}
            if suffix == "Basic":
                fields["damage"] = {"Rat": 2, "Crow": 1, "Wraith": 2}[name]
            if suffix == "Status" and name == "Rat":
                fields.update(damage=0, statusStacks=1)
            if suffix == "Signature" and name == "Crow":
                fields["damage"] = 1
            edit(ROOT / f"Assets/Data/EnemyAttacks/SmallMonsters/{name}{suffix}.asset", fields)
        if name != "Crow":
            edit(ROOT / f"Assets/Data/EnemyAttacks/SmallMonsters/{name}Heavy.asset", {"damage": 4 if name == "Rat" else 6})
        for group in ("Enemies", "BattleSimulator"):
            edit(ROOT / f"Assets/Prefabs/{group}/{name}.prefab", {"maxHp": hp})
        report["monsters"].append({"kind": name, "hp": hp})

    path = ROOT / "Assets/Scenes/CombatScene.unity"
    raw = path.read_text("utf-8-sig")
    match = re.search(r"^--- !u!114 &2022034367\n.*?(?=^---|\Z)", raw, re.M | re.S)
    assert match
    block = re.sub(r"^  maxHp: \d+$", "  maxHp: 36", match[0], flags=re.M)
    path.write_text(raw[:match.start()] + block + raw[match.end():], "utf-8")

    folder = ROOT / "Assets/Data/EnemyAttacks/KBM03"
    moves = [
        ("KBM01", dict(attackName="倦怠爪击", damageType=0, kind=0, damage=5, applyStatus=0, statusStacks=0)),
        ("KBM02", dict(attackName="昏沉吐息", damageType=3, kind=0, damage=0, applyStatus=1, statusOnHit=6, statusStacks=1)),
        ("KBM03", dict(attackName="崩压重击", damageType=0, kind=0, damage=12, applyStatus=0, statusStacks=0)),
        ("KBM04", dict(attackName="蜷身休憩", damageType=0, kind=2, damage=0, applyStatus=0, statusStacks=0)),
    ]
    for name, fields in moves:
        fields.update(weight=1, followUp="{fileID: 0}")
        edit(folder / f"{name}.asset", fields)
    path = folder / "SlothCharge.asset"
    charge_guid = uuid.uuid5(uuid.UUID("9a2dfae4-66a0-4df0-bcff-86e1cdb97608"), path.relative_to(ROOT).as_posix()).hex
    path.write_text((folder / "KBM03.asset").read_text("utf-8").replace("m_Name: KBM03", "m_Name: SlothCharge"), "utf-8")
    Path(str(path) + ".meta").write_text(
        f"fileFormatVersion: 2\nguid: {charge_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", "utf-8")
    edit(path, dict(attackName="沉身蓄力", kind=1, damage=0, followUp=ref(folder / "KBM03.asset")))
    resistance = guid(ROOT / "Assets/Scripts/Battle/Damage/DamageResistance.cs")
    for group in ("Enemies", "BattleSimulator"):
        path = ROOT / f"Assets/Prefabs/{group}/Sloth.prefab"
        raw = path.read_text("utf-8-sig")
        match = next(m for m in re.finditer(r"^--- !u!114 &(\d+)\n.*?(?=^---|\Z)", raw, re.M | re.S)
                     if "guid: 0e636f26f1d66244c80cec728813d3e7" in m[0])
        block = match[0]
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)[1]
        actions = "  attackDatas:\n" + "".join("  - " + ref(folder / f"{name}.asset") + "\n"
                                              for name in ("KBM01", "KBM02", "SlothCharge", "KBM04"))
        block = re.sub(r"  attackDatas:\n(?:  - .*\n)+", actions, block)
        block = re.sub(r"^  breakThreshold: \d+$", "  breakThreshold: 3", block, flags=re.M)
        if "  displayName:" not in block:
            block = block.rstrip() + "\n  displayName: 懒惰\n"
        raw = raw[:match.start()] + block + raw[match.end():]
        if f"guid: {resistance}" not in raw:
            component = "9971001027"
            match = re.search(rf"^--- !u!1 &{go}\n.*?(?=^---|\Z)", raw, re.M | re.S)
            block = match[0].replace("  m_Layer:", f"  - component: {{fileID: {component}}}\n  m_Layer:", 1)
            raw = raw[:match.start()] + block + raw[match.end():]
            raw += f"""--- !u!114 &{component}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {resistance}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  damageRules:
  - damageType: 0
    damageReaction: 0
  - damageType: 3
    damageReaction: 0
"""
        path.write_text(raw, "utf-8")
    output = ROOT / "output/monster-balance-v2/changes.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print("Installed balance v2: 12 weapons/skills, 3 species, both Sloth prefabs.")


if __name__ == "__main__":
    main()
