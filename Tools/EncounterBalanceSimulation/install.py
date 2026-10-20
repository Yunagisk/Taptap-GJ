"""Install balanced small-monster assets without rebuilding unrelated scenes/UI."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID("9a2dfae4-66a0-4df0-bcff-86e1cdb97608")


def meta(relative, importer="MonoImporter"):
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    target = Path(str(path) + ".meta")
    if target.exists():
        return re.search(r"^guid: (\w+)", target.read_text("utf-8-sig"), re.M)[1]
    guid = uuid.uuid5(NS, relative).hex
    body = f"fileFormatVersion: 2\nguid: {guid}\n"
    if importer == "Folder":
        body += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif importer == "MonoImporter":
        body += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
    elif importer == "NativeFormatImporter":
        body += "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
    else:
        body += importer + ":\n  externalObjects: {}\n"
    target.write_text(body + "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", "utf-8")
    return guid


def ref(guid):
    return f"{{fileID: 11400000, guid: {guid}, type: 2}}" if guid else "{fileID: 0}"


def asset(relative, script, name, fields):
    (ROOT / relative).write_text(f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
{fields}""", "utf-8")
    return meta(relative, "NativeFormatImporter")


def block(text, id_):
    match = re.search(rf"(?m)^--- !u!\d+ &{id_}\n", text)
    assert match, id_
    end = text.find("\n--- !u!", match.end())
    return text[match.start():end + 1 if end >= 0 else len(text)]


def set_field(text, key, value):
    pattern = rf"(?m)^  {key}:.*$"
    return re.sub(pattern, f"  {key}: {value}", text) if re.search(pattern, text) else text.rstrip() + f"\n  {key}: {value}\n"


for path in ["Assets/Scripts/Battle/Data/SmallMonsterData.cs", "Assets/Scripts/Battle/AI/WeightedEnemyActionSelector.cs",
             "Assets/Scripts/Battle/Flow/SmallMonsterEncounterRules.cs", "Assets/Scripts/Battle/Flow/SmallMonsterEncounterSetup.cs"]:
    meta(path)
for folder in ["Assets/Data/EnemyAttacks/SmallMonsters", "Assets/Data/SmallMonsters"]:
    (ROOT / folder).mkdir(exist_ok=True)
    meta(folder, "Folder")

attack_script = meta("Assets/Scripts/Battle/Data/EnemyAttackData.cs")
definition_script = meta("Assets/Scripts/Battle/Data/SmallMonsterData.cs")
definitions = {}
for kind, name, hp, low_guard, weak, weights, normal, heavy, status in [
    ("Rat", "老鼠", 36, 30, [(0, 0), (2, 0)], [45, 20, 20, 15], 2, 4, (3, 1, 0, "瘟咬")),
    ("Crow", "腐鸦", 36, 0, [(1, 0)], [45, 20, 20, 15], 1, 0, (5, 1, 0, "腐鸣")),
    ("Wraith", "怨影", 44, 0, [(2, 2), (3, 0)], [45, 20, 20, 15], 2, 6, (6, 1, 0, "怨咒")),
]:
    follow = None
    if heavy:
        follow = asset(f"Assets/Data/EnemyAttacks/SmallMonsters/{kind}Heavy.asset", attack_script,
                       kind + "Heavy", f"  attackName: {'猛扑' if kind == 'Rat' else '重抓'}\n"
                       f"  damageType: {1 if kind == 'Rat' else 0}\n  kind: 0\n  weight: 0\n  damage: {heavy}\n  attackCount: 1\n  hitInterval: 0.3\n  applyStatus: 0\n")
    actions = []
    for suffix, action_name, action_kind, damage, count, weight, rider in [
        ("Basic", {"Rat": "啃咬", "Crow": "啄击", "Wraith": "阴爪"}[kind], 0, normal, 1, weights[0], False),
        ("Status", status[3], 0, status[2], 1, weights[1], True),
        ("Signature", "连啄" if kind == "Crow" else "伏身" if kind == "Rat" else "凝怨",
         0 if kind == "Crow" else 1, 1 if kind == "Crow" else 0, 2 if kind == "Crow" else 1, weights[2], False),
        ("Guard", {"Rat": "蜷缩", "Crow": "敛翼", "Wraith": "雾化"}[kind], 2, 0, 1, weights[3], False),
    ]:
        fields = f"  attackName: {action_name}\n  kind: {action_kind}\n  weight: {weight}\n  followUp: {ref(follow if action_kind == 1 else None)}\n"
        fields += f"  damageType: {2 if kind == 'Rat' else 3}\n" if rider else f"  damageType: {1 if kind in ('Rat', 'Crow') else 0}\n"
        fields += f"  damage: {damage}\n  attackCount: {count}\n  hitInterval: 0.3\n  applyStatus: {int(rider)}\n"
        fields += f"  statusOnHit: {status[0]}\n  statusStacks: {status[1] if rider else 0}\n"
        actions.append(asset(f"Assets/Data/EnemyAttacks/SmallMonsters/{kind}{suffix}.asset", attack_script, kind + suffix, fields))
    fields = f"  kind: {['Rat', 'Crow', 'Wraith'].index(kind)}\n  displayName: {name}\n  maxHp: {hp}\n  breakThreshold: 2\n  lowHealthGuardWeight: {low_guard}\n  actions:\n"
    fields += "".join(f"  - {ref(id_)}\n" for id_ in actions)
    fields += "  resistances:\n" + "".join(f"  - damageType: {type_}\n    damageReaction: {reaction}\n" for type_, reaction in weak)
    definitions[kind] = (asset(f"Assets/Data/SmallMonsters/{kind}.asset", definition_script, kind, fields), hp, weak)

crow_path = ROOT / "Assets/Prefabs/Enemies/Crow.prefab"
if not crow_path.exists():
    crow_path.write_text((ROOT / "Assets/Prefabs/BattleSimulator/Crow.prefab").read_text("utf-8"), "utf-8")
meta("Assets/Prefabs/Enemies/Crow.prefab", "PrefabImporter")
enemy_script = meta("Assets/Scripts/Battle/Combatants/EnemyCombatant.cs")
resistance_script = meta("Assets/Scripts/Battle/Damage/DamageResistance.cs")


def configure_enemy(text, kind, combatant_id=None):
    definition, hp, rules = definitions[kind]
    if combatant_id is None:
        match = next(m for m in re.finditer(r"(?ms)^--- !u!114 &(\d+)\n.*?(?=^---|\Z)", text)
                     if f"guid: {enemy_script}," in m[0])
        combatant_id = match[1]
    old = block(text, combatant_id)
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", old)[1]
    changed = set_field(set_field(old, "maxHp", hp), "definition", ref(definition))
    changed = set_field(changed, "breakThreshold", 2)
    text = text.replace(old, changed)
    resistance = next((m for m in re.finditer(r"(?ms)^--- !u!114 &(\d+)\n.*?(?=^---|\Z)", text)
                       if f"guid: {resistance_script}," in m[0] and f"m_GameObject: {{fileID: {go}}}" in m[0]), None)
    rule_text = "  damageRules:\n" + "".join(f"  - damageType: {type_}\n    damageReaction: {reaction}\n" for type_, reaction in rules)
    if resistance:
        old = resistance[0]
        text = text.replace(old, old.split("  damageRules:", 1)[0] + rule_text)
    else:
        id_ = "9971001027"
        old = block(text, go)
        text = text.replace(old, old.replace("  m_Layer:", f"  - component: {{fileID: {id_}}}\n  m_Layer:", 1))
        text += f"""--- !u!114 &{id_}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {resistance_script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{rule_text}"""
    return text


for kind in definitions:
    for folder in ["Enemies", "BattleSimulator"]:
        path = ROOT / f"Assets/Prefabs/{folder}/{kind}.prefab"
        path.write_text(configure_enemy(path.read_text("utf-8"), kind), "utf-8")

scene_path = ROOT / "Assets/Scenes/CombatScene.unity"
scene = configure_enemy(scene_path.read_text("utf-8"), "Crow", "2022034367")
component = "917540001"
if f"--- !u!114 &{component}\n" not in scene:
    old = block(scene, "1879433835")
    scene = scene.replace(old, old.replace("  m_Layer:", f"  - component: {{fileID: {component}}}\n  m_Layer:", 1))
    scene += f"""--- !u!114 &{component}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1879433835}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {meta('Assets/Scripts/Battle/Flow/SmallMonsterEncounterSetup.cs')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  flow: {{fileID: 1879433836}}
"""
    for key, kind in [("ratPrefab", "Rat"), ("crowPrefab", "Crow"), ("wraithPrefab", "Wraith")]:
        path = ROOT / f"Assets/Prefabs/Enemies/{kind}.prefab"
        root_id = re.search(r"^--- !u!1 &(\d+)", path.read_text("utf-8"), re.M)[1]
        scene += f"  {key}: {{fileID: {root_id}, guid: {meta(str(path.relative_to(ROOT)).replace(chr(92), '/'), 'PrefabImporter')}, type: 3}}\n"
scene_path.write_text(scene, "utf-8")

# Match the runtime row layout in the reusable HUD prefab as well.
hud_path = ROOT / "Assets/Prefabs/UI/CombatHUD.prefab"
hud = hud_path.read_text("utf-8")
for id_, x, width, pivot in [(1181, 132, 92, 0), (1185, 230, 120, 0)] + [(1189 + i * 8, 240 + i * 34, 28, .5) for i in range(4)]:
    old = block(hud, id_)
    changed = set_field(old, "m_AnchorMin", "{x: 0, y: 1}")
    changed = set_field(changed, "m_AnchorMax", "{x: 0, y: 1}")
    changed = set_field(changed, "m_AnchoredPosition", f"{{x: {x}, y: -82}}")
    changed = set_field(changed, "m_SizeDelta", f"{{x: {width}, y: 28}}")
    changed = set_field(changed, "m_Pivot", f"{{x: {pivot}, y: 0.5}}")
    hud = hud.replace(old, changed)
old = block(hud, 1183)
hud = hud.replace(old, set_field(old, "m_text", '"怪物弱点"'))
hud_path.write_text(hud, "utf-8")
print("Installed 3 definitions, 14 actions, balanced prefabs, production encounter setup and weakness layout.")
