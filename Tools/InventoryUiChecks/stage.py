"""Stage production UI dependencies in an isolated, offline Unity project."""
from pathlib import Path
import json,re,shutil,sys

repo=Path(__file__).resolve().parents[2]
scratch=Path(sys.argv[1]).resolve()
assert scratch.is_relative_to(repo/'output/inventory-category'), scratch
scratch.mkdir(parents=True,exist_ok=True)
lookup={}
for meta in (repo/'Assets').rglob('*.meta'):
    try:
        match=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
        if match:lookup[match[1]]=Path(str(meta)[:-5])
    except (PermissionError,UnicodeDecodeError):pass
included=set()
def copy(path):
    if path in included or not path.is_file():return
    included.add(path)
    relative=path.relative_to(repo);destination=scratch/relative
    destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,destination)
    meta=Path(str(path)+'.meta')
    if meta.is_file():shutil.copy2(meta,Path(str(destination)+'.meta'))
    if path.suffix in {'.prefab','.asset','.mat','.controller','.anim','.shader'}:
        for id in set(re.findall(r'guid: (\w+)',path.read_text(encoding='utf-8-sig',errors='ignore'))):
            if id in lookup:copy(lookup[id])

for path in (repo/'Assets/Scripts').rglob('*'):
    if path.is_file() and path.suffix!='.meta':copy(path)
for name in ['EquipmentPanel','CombatHUD']:copy(repo/('Assets/Prefabs/UI/'+name+'.prefab'))
for path in (repo/'Assets/TextMesh Pro/Resources').rglob('*'):
    if path.is_file() and path.suffix!='.meta':copy(path)
# TMP shaders include adjacent .cginc files by filename rather than by GUID.
for path in (repo/'Assets/TextMesh Pro/Shaders').rglob('*'):
    if path.is_file() and path.suffix!='.meta':copy(path)
shutil.copytree(repo/'ProjectSettings',scratch/'ProjectSettings',dirs_exist_ok=True)
packages=scratch/'Packages';packages.mkdir(exist_ok=True)
dependencies={key:value for key,value in json.loads((repo/'Packages/manifest.json').read_text(encoding='utf-8-sig'))['dependencies'].items() if key.startswith('com.unity.modules.')}
for name,version in [('com.unity.textmeshpro','3.0.7'),('com.unity.ugui','1.0.0'),('com.unity.test-framework','1.1.33'),('com.unity.ext.nunit','1.0.6')]:
    source=repo/'Library/PackageCache'/(name+'@'+version)
    if not source.exists():source=repo/'output/ItemValidation/UnityProject/Packages'/name
    assert source.exists(),source
    shutil.copytree(source,packages/name,dirs_exist_ok=True)
    dependencies[name]=version
(packages/'manifest.json').write_text(json.dumps({'dependencies':dependencies},indent=2),encoding='utf-8')
(scratch/'Assets/Editor').mkdir(exist_ok=True)
shutil.copy2(repo/'Tools/InventoryUiChecks/InventoryUiBatchChecks.cs',scratch/'Assets/Editor/InventoryUiBatchChecks.cs')
shutil.copy2(repo/'Tools/InventoryUiChecks/InventoryUiPlayChecks.cs',scratch/'Assets/InventoryUiPlayChecks.cs')
print('Staged',len(included),'production files and dependencies in',scratch)
