"""Use the existing isolated validation project; leave the user's Unity session alone."""
from pathlib import Path
import shutil
import runpy

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'output/fuya-integration/UnityProject'
runpy.run_path(str(ROOT / 'Tools/BattleSimulatorChecks/prepare.py'))
shutil.copytree(ROOT / 'Assets/Prefabs', PROJECT / 'Assets/Prefabs', dirs_exist_ok=True)
for relative in ['Assets/Scenes/MainMap.unity', 'Assets/Scenes/MainMap.unity.meta']:
    shutil.copy2(ROOT / relative, PROJECT / relative)
shutil.copy2(ROOT / 'Tools/MapChecks/SlothBossNodeValidation.cs', PROJECT / 'Assets/Editor/SlothBossNodeValidation.cs')
print('Staged the actual MainMap scene and current map prefab.')
