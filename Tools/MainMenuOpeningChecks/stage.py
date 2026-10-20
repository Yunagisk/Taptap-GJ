"""Stage menu/village dependencies in a separate offline Unity project."""
from pathlib import Path
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
scratch = ROOT / 'output/main-menu-opening/UnityProject'
scratch.mkdir(parents=True, exist_ok=True)
lookup = {}
for path in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig', errors='ignore'), re.M)
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
    meta = Path(str(path) + '.meta')
    if meta.exists():
        shutil.copy2(meta, Path(str(target) + '.meta'))
    if path.suffix in {'.unity', '.prefab', '.asset', '.mat', '.controller', '.anim', '.shader'}:
        for guid in set(re.findall(r'guid: (\w+)', path.read_text(encoding='utf-8-sig', errors='ignore'))):
            if guid in lookup:
                copy(lookup[guid])


for folder in ['Assets/Scripts', 'Assets/Editor']:
    for path in (ROOT / folder).rglob('*'):
        if path.suffix in {'.cs', '.cginc', '.hlsl'}:
            copy(path)
for relative in ['Assets/Scenes/MainMenu.unity', 'Assets/Scenes/Village.unity',
                 'Assets/Scenes/MainMap.unity', 'Assets/Resources/VillageIntroduction.asset',
                 'Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png']:
    copy(ROOT / relative)
for folder in ['Assets/Sprite/UI/MainMenuOpening', 'Assets/TextMesh Pro/Resources', 'Assets/TextMesh Pro/Shaders']:
    for path in (ROOT / folder).rglob('*'):
        if path.is_file() and path.suffix != '.meta':
            copy(path)
shutil.copytree(ROOT / 'ProjectSettings', scratch / 'ProjectSettings', dirs_exist_ok=True)
packages = scratch / 'Packages'
packages.mkdir(exist_ok=True)
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
deps = {key: value for key, value in manifest['dependencies'].items() if key.startswith('com.unity.modules.')}
for name, version in [('com.unity.textmeshpro', '3.0.7'), ('com.unity.ugui', '1.0.0'),
                      ('com.unity.test-framework', '1.1.33'), ('com.unity.ext.nunit', '1.0.6')]:
    shutil.copytree(ROOT / 'Library/PackageCache' / (name + '@' + version), packages / name, dirs_exist_ok=True)
    deps[name] = version
(packages / 'manifest.json').write_text(json.dumps({'dependencies': deps}, indent=2), encoding='utf-8')
shutil.copy2(Path(__file__).with_name('UnityValidation.cs'), scratch / 'Assets/Editor/MainMenuOpeningValidation.cs')
print('Staged', len(included), 'production files in', scratch)
