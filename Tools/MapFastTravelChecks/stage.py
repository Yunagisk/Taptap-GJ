from pathlib import Path
import runpy
import shutil

repo = Path(__file__).resolve().parents[2]
staged = runpy.run_path(str(repo / 'Tools/EncounterBalanceSimulation/stage.py'))
for relative in ['Assets/Scenes/MainMap.unity', 'Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab']:
    staged['copy'](repo / relative)
shutil.copy2(Path(__file__).with_name('UnityValidation.cs'), staged['scratch'] / 'Assets/Editor/MapFastTravelValidation.cs')
print('Map native validation dependencies:', len(staged['included']))
