"""Stage the real village and its service authoring in an isolated offline Unity project."""
from pathlib import Path
import json
import hashlib
import re
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = (repo / 'output/village-services/UnityProject').resolve()
assert scratch.is_relative_to(repo / 'output/village-services')
scratch.mkdir(parents=True, exist_ok=True)
tracked_inputs = ['Assets/Scenes/Village.unity','Assets/Prefabs/UI/InnPanel.prefab',
                  'Assets/Prefabs/UI/VillageShopPanel.prefab','Assets/Prefabs/UI/VillageRestTransition.prefab']
input_hashes = {relative: hashlib.sha256((repo / relative).read_bytes()).hexdigest()
                if (repo / relative).exists() else None for relative in tracked_inputs}
(scratch.parent / 'production-inputs.json').write_text(json.dumps(input_hashes,indent=2),encoding='utf8')
lookup = {}
for meta in (repo / 'Assets').rglob('*.meta'):
    try:
        match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            lookup[match[1]] = Path(str(meta)[:-5])
    except (PermissionError, UnicodeDecodeError):
        pass
included = set()


def copy(path):
    if path in included or not path.is_file():
        return
    included.add(path)
    target = scratch / path.relative_to(repo)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target)
    meta = Path(str(path) + '.meta')
    if meta.exists():
        shutil.copy2(meta, Path(str(target) + '.meta'))
    if path.suffix in {'.prefab', '.unity', '.asset', '.mat', '.shader'}:
        for guid in set(re.findall(r'guid: (\w+)', path.read_text(encoding='utf-8-sig', errors='ignore'))):
            if guid in lookup:
                copy(lookup[guid])


for path in (repo / 'Assets/Scripts').rglob('*.cs'):
    copy(path)
for relative in ['Assets/Scenes/Village.unity', 'Assets/Resources/VillageIntroduction.asset',
                 'Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab',
                 'Assets/Editor/GameFlow/VillageServicesBuilder.cs',
                 'Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png',
                 'Assets/Sprite/UI/ResourceIcons2K/SinRemnant.png',
                 'Assets/Sprite/UI/ResourceIcons2K/Lantern.png']:
    copy(repo / relative)
for path in (repo / 'Assets/Sprite/Items/WeaponIllustrations').glob('*.png'):
    copy(path)
for folder in ['Assets/TextMesh Pro/Resources', 'Assets/TextMesh Pro/Shaders']:
    for path in (repo / folder).rglob('*'):
        if path.is_file() and path.suffix != '.meta':
            copy(path)
shutil.copytree(repo / 'ProjectSettings', scratch / 'ProjectSettings', dirs_exist_ok=True)
packages = scratch / 'Packages'
packages.mkdir(exist_ok=True)
manifest = json.loads((repo / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
dependencies = {key: value for key, value in manifest['dependencies'].items() if key.startswith('com.unity.modules.')}
for name, version in [('com.unity.textmeshpro', '3.0.7'), ('com.unity.ugui', '1.0.0'),
                      ('com.unity.test-framework', '1.1.33'), ('com.unity.ext.nunit', '1.0.6')]:
    source = repo / 'Library/PackageCache' / (name + '@' + version)
    assert source.exists(), source
    if not (packages / name / 'package.json').exists():
        shutil.copytree(source, packages / name, dirs_exist_ok=True)
    dependencies[name] = version
(packages / 'manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf-8')
(scratch / 'Assets/Editor').mkdir(exist_ok=True)
shutil.copy2(Path(__file__).with_name('VillageServiceBatchChecks.cs'), scratch / 'Assets/Editor/VillageServiceBatchChecks.cs')
shutil.copy2(Path(__file__).with_name('VillageServicePlayChecks.cs'), scratch / 'Assets/VillageServicePlayChecks.cs')
print('Staged', len(included), 'production files in', scratch)
