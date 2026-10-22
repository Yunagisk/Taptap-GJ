from pathlib import Path
import runpy
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = repo / 'output/village-intro/weapon-catalog/UnityProject'
sys.argv = ['stage.py', str(scratch)]
staged = runpy.run_path(str(repo / 'Tools/VillageIntroChecks/stage.py'), run_name='__main__')
for source in (repo / 'Assets/Editor').rglob('*.cs'):
    staged['copy'](source)
for name in ['WeaponCatalogBatchChecks.cs', 'WeaponCatalogPlayChecks.cs']:
    shutil.copy2(Path(__file__).with_name(name), scratch / 'Assets' / ('Editor' if 'Batch' in name else '') / name)
for name in ['InventoryUiBatchChecks.cs', 'InventoryUiPlayChecks.cs']:
    shutil.copy2(repo / 'Tools/InventoryUiChecks' / name, scratch / 'Assets' / ('Editor' if 'Batch' in name else '') / name)
print('Weapon catalogue checks staged:', scratch)
