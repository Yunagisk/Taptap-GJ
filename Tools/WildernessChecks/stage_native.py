"""Stage production scenes for an offline Unity sidequest playthrough, reusing the intro check cache."""
from pathlib import Path
import runpy
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = repo / 'output/village-intro/native-v1/UnityProject'
sys.argv = ['stage.py', str(scratch)]
runpy.run_path(str(repo / 'Tools/VillageIntroChecks/stage.py'), run_name='__main__')
shutil.copy2(Path(__file__).with_name('SidequestBatchChecks.cs'), scratch / 'Assets/Editor/SidequestBatchChecks.cs')
shutil.copy2(Path(__file__).with_name('SidequestPlayChecks.cs'), scratch / 'Assets/SidequestPlayChecks.cs')
(scratch.parent / 'sidequests').mkdir(exist_ok=True)
