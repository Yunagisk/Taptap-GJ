"""Copy dialogue dependencies into an offline Unity project for native UI checks."""
from pathlib import Path
import json
import re
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = Path(sys.argv[1]).resolve()
assert scratch.is_relative_to(repo / 'output/dialogue-ui'), scratch
scratch.mkdir(parents=True, exist_ok=True)
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
    if meta.is_file():
        shutil.copy2(meta, Path(str(target) + '.meta'))
    if path.suffix in {'.prefab', '.unity', '.asset', '.mat', '.shader'}:
        for guid in set(re.findall(r'guid: (\w+)', path.read_text(encoding='utf-8-sig', errors='ignore'))):
            if guid in lookup:
                copy(lookup[guid])

for path in (repo / 'Assets/Scripts').rglob('*.cs'):
    copy(path)
for relative in [
    'Assets/Prefabs/UI/DialoguePanel.prefab',
    'Assets/Scenes/Intro.unity',
    'Assets/Fonts/Huiwen.asset',
    'Assets/Sprite/UI/VillageAmbience/VillageAfternoon.jpg',
    'Assets/Sprite/UI/TavernTemp/TavernChief-v1.png',
    'Assets/Editor/GameFlow/P0ScenePrefabBuilder.cs',
    'Assets/Editor/GameFlow/RpgDialoguePrefabBuilder.cs',
]:
    copy(repo / relative)
for folder in ['Assets/TextMesh Pro/Resources', 'Assets/TextMesh Pro/Shaders']:
    for path in (repo / folder).rglob('*'):
        if path.is_file() and path.suffix != '.meta':
            copy(path)
shutil.copytree(repo / 'ProjectSettings', scratch / 'ProjectSettings', dirs_exist_ok=True)
packages = scratch / 'Packages'
packages.mkdir(exist_ok=True)
manifest = json.loads((repo / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
dependencies = {k: v for k, v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
for name, version in [('com.unity.textmeshpro', '3.0.7'), ('com.unity.ugui', '1.0.0'),
                      ('com.unity.test-framework', '1.1.33'), ('com.unity.ext.nunit', '1.0.6')]:
    source = repo / 'Library/PackageCache' / (name + '@' + version)
    assert source.exists(), source
    shutil.copytree(source, packages / name, dirs_exist_ok=True)
    dependencies[name] = version
(packages / 'manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf-8')
(scratch / 'Assets/Editor').mkdir(exist_ok=True)
shutil.copy2(repo / 'Tools/DialogueUiChecks/DialogueUiBatchChecks.cs', scratch / 'Assets/Editor/DialogueUiBatchChecks.cs')
shutil.copy2(repo / 'Tools/DialogueUiChecks/DialogueUiPlayChecks.cs', scratch / 'Assets/DialogueUiPlayChecks.cs')
print('Staged', len(included), 'production files in', scratch)
