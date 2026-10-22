from pathlib import Path
import runpy
import shutil

repo = Path(__file__).resolve().parents[2]
staged = runpy.run_path(str(repo / 'Tools/MapFastTravelChecks/stage.py'))['staged']
staged['copy'](repo / 'Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png')
shutil.copy2(Path(__file__).with_name('UnityValidation.cs'), staged['scratch'] / 'Assets/Editor/MapMenuValidation.cs')
print('Map menu validation dependencies:', len(staged['included']))
