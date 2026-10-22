"""Stage installed tavern assets in a separate project, without rebuilding prefabs."""
from pathlib import Path
import runpy
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = repo / 'output/village-intro/tavern-checks/UnityProject'
sys.argv = ['stage.py', str(scratch)]
runpy.run_path(str(repo / 'Tools/VillageIntroChecks/stage.py'), run_name='__main__')
shutil.copy2(Path(__file__).with_name('TavernBatchChecks.cs'), scratch / 'Assets/Editor/TavernBatchChecks.cs')
shutil.copy2(Path(__file__).with_name('TavernPlayChecks.cs'), scratch / 'Assets/TavernPlayChecks.cs')
print('Tavern checks staged:', scratch)
