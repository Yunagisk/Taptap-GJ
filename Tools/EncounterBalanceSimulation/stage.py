"""Stage only production encounter dependencies in a private offline Unity project."""
from pathlib import Path
import json
import re
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
scratch = Path(sys.argv[1]).resolve()
assert scratch.is_relative_to(ROOT / "output/encounter-balance-v1"), scratch
scratch.mkdir(parents=True, exist_ok=True)
lookup = {}
for path in (ROOT / "Assets").rglob("*.meta"):
    match = re.search(r"^guid: (\w+)", path.read_text("utf-8-sig", errors="ignore"), re.M)
    if match:
        lookup[match[1]] = Path(str(path)[:-5])
included = set()


def copy(path):
    if path in included or not path.is_file():
        return
    included.add(path)
    target = scratch / path.relative_to(ROOT)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target)
    meta = Path(str(path) + ".meta")
    if meta.exists():
        shutil.copy2(meta, Path(str(target) + ".meta"))
    if path.suffix in {".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".shader"}:
        for guid in set(re.findall(r"guid: (\w+)", path.read_text("utf-8-sig", errors="ignore"))):
            if guid in lookup:
                copy(lookup[guid])


for path in (ROOT / "Assets/Scripts").rglob("*.cs"):
    copy(path)
# Compile authoring tools too: editor-only errors also block the user's project.
for path in (ROOT / "Assets/Editor").rglob("*.cs"):
    copy(path)
# Custom artwork shaders include adjacent source files by filename, not GUID.
for path in (ROOT / "Assets").rglob("*"):
    if path.is_file() and path.suffix in {".cginc", ".hlsl"}:
        copy(path)
copy(ROOT / "Assets/Scenes/CombatScene.unity")
copy(ROOT / "Assets/Prefabs/UI/EquipmentPanel.prefab")
copy(ROOT / "Assets/Prefabs/Enemies/Sloth.prefab")
copy(ROOT / "Assets/Prefabs/BattleSimulator/Sloth.prefab")
copy(ROOT / "Assets/Data/Weapons/WeaponLibrary.asset")
for path in (ROOT / "Assets/Data/SmallMonsters").glob("*.asset"):
    copy(path)
for path in (ROOT / "Assets/TextMesh Pro/Resources").rglob("*"):
    if path.is_file() and path.suffix != ".meta":
        copy(path)
for path in (ROOT / "Assets/TextMesh Pro/Shaders").rglob("*"):
    if path.is_file() and path.suffix != ".meta":
        copy(path)
copy(ROOT / "Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset")
shutil.copytree(ROOT / "ProjectSettings", scratch / "ProjectSettings", dirs_exist_ok=True)
packages = scratch / "Packages"
packages.mkdir(exist_ok=True)
original = json.loads((ROOT / "Packages/manifest.json").read_text("utf-8-sig"))["dependencies"]
deps = {key: value for key, value in original.items() if key.startswith("com.unity.modules.")}
for name, version in [("com.unity.textmeshpro", "3.0.7"), ("com.unity.ugui", "1.0.0"),
                      ("com.unity.test-framework", "1.1.33"), ("com.unity.ext.nunit", "1.0.6")]:
    source = ROOT / "Library/PackageCache" / f"{name}@{version}"
    assert source.exists(), source
    shutil.copytree(source, packages / name, dirs_exist_ok=True)
    deps[name] = version
(packages / "manifest.json").write_text(json.dumps({"dependencies": deps}, indent=2), "utf-8")
editor = scratch / "Assets/Editor"
editor.mkdir(exist_ok=True)
shutil.copy2(ROOT / "Tools/EncounterBalanceSimulation/UnityValidation.cs", editor / "SmallMonsterValidation.cs")
print(f"Staged {len(included)} production dependencies in {scratch}")
