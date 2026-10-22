"""Install only the service prefabs and village scene produced by a passing native run."""
from pathlib import Path
import hashlib
import json
import re
import shutil

repo = Path(__file__).resolve().parents[2]
output = repo / 'output/village-services'
scratch = output / 'UnityProject'
result = json.loads((output / 'result.json').read_text(encoding='utf-8-sig'))
assert result['passed'] and result['runtimeErrors'] == 0, 'Native service validation did not pass'
for relative, expected in json.loads((output / 'production-inputs.json').read_text(encoding='utf8')).items():
    path = repo / relative
    actual = hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None
    staged_path = scratch / relative
    already_installed = staged_path.exists() and actual == hashlib.sha256(staged_path.read_bytes()).hexdigest()
    assert actual == expected or already_installed, f'Asset changed since staging; merge the latest version before installation: {relative}'
sources = [repo / 'Assets/Scripts/Village/VillageScreen.cs', repo / 'Assets/Scripts/Map/UI/MapTimeDialView.cs',
           repo / 'Assets/Editor/GameFlow/VillageServicesBuilder.cs']
sources += [repo / 'Assets/Scripts/Village' / name for name in [
    'VillageShopUI.cs','VillageShopCard.cs','VillageInnUI.cs','VillageInnRules.cs',
    'VillageRestTransition.cs','VillageServiceIcon.cs','VillageServiceWindow.cs']]
for source in sources:
    staged = scratch / source.relative_to(repo)
    assert source.read_bytes() == staged.read_bytes(), f'Production changed since validation: {source}'
paths = ['Assets/Prefabs/UI/VillageShopPanel.prefab', 'Assets/Prefabs/UI/InnPanel.prefab',
         'Assets/Prefabs/UI/VillageRestTransition.prefab', 'Assets/Scenes/Village.unity']
backup = output / 'before-install'
installed = {}
lookup = {}
metadata_paths = list((repo / 'Assets').rglob('*.meta'))
for package in ['com.unity.ugui@1.0.0','com.unity.textmeshpro@3.0.7']:
    metadata_paths += list((repo / 'Library/PackageCache' / package).rglob('*.meta'))
metadata_paths += [Path(str(scratch / relative)+'.meta') for relative in paths]
for metadata in metadata_paths:
    try:
        match = re.search(r'^guid: (\w+)', metadata.read_text(encoding='utf-8-sig'), re.M)
        if match:
            lookup[match[1]] = metadata
    except (UnicodeDecodeError, PermissionError, FileNotFoundError):
        pass
# Resolve references before changing production, including built-in UI package scripts.
for relative in paths:
    for guid in set(re.findall(r'guid: (\w+)', (scratch / relative).read_text(encoding='utf-8-sig'))):
        assert guid.startswith('0000000000000000') or guid in lookup, f'Unresolved GUID {guid} in {relative}'
for relative in paths:
    source = scratch / relative
    target = repo / relative
    assert source.is_file(), source
    if target.exists():
        old = backup / relative
        if not old.exists():
            old.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, old)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    if relative.endswith('.prefab'):
        metadata = Path(str(source)+'.meta')
        assert metadata.exists(), metadata
        shutil.copy2(metadata, Path(str(target)+'.meta'))
    installed[relative] = hashlib.sha256(target.read_bytes()).hexdigest()

for relative in paths:
    for guid in set(re.findall(r'guid: (\w+)', (repo / relative).read_text(encoding='utf-8-sig'))):
        assert guid.startswith('0000000000000000') or guid in lookup, f'Unresolved GUID {guid} in {relative}'
(output / 'installed.json').write_text(json.dumps({'checks':len(result['checks']), 'files':installed},indent=2),encoding='utf8')
print('Installed Village.unity and three service prefabs; all asset GUIDs resolve.')
