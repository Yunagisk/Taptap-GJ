from pathlib import Path
import runpy
import shutil

repo = Path(__file__).resolve().parents[2]
staged = runpy.run_path(str(repo / 'Tools/MapFastTravelChecks/stage.py'))['staged']
shutil.copy2(Path(__file__).with_name('UnityValidation.cs'), staged['scratch'] / 'Assets/Editor/MapBossCameraValidation.cs')
print('Staged the production map and boss camera checks.')
