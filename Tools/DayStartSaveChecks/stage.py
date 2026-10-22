from pathlib import Path
import re
import runpy
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = repo / 'output/village-intro/day-start-save/UnityProject'
sys.argv = ['stage.py', str(scratch)]
runpy.run_path(str(repo / 'Tools/VillageIntroChecks/stage.py'), run_name='__main__')
for name in ['DayStartSaveBatchChecks.cs', 'DayStartSavePlayChecks.cs']:
    shutil.copy2(Path(__file__).with_name(name), scratch / 'Assets' / ('Editor' if 'Batch' in name else '') / name)
# Keep validation writes away from the real player's persistent-data directory.
settings = scratch / 'ProjectSettings/ProjectSettings.asset'
text = settings.read_text('utf-8-sig')
text = re.sub(r'^  productName:.*$', '  productName: Taptap-DayStartSave-Validation', text, flags=re.M)
settings.write_text(text, 'utf-8')
print(scratch)
