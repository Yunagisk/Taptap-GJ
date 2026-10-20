"""Stage production Sloth chapter assets into the existing isolated Unity check cache."""
from pathlib import Path
import runpy
import shutil
import sys

repo = Path(__file__).resolve().parents[2]
scratch = repo / "output/village-intro/native-v1/UnityProject"
sys.argv = ["stage.py", str(scratch)]
stage = runpy.run_path(str(repo / "Tools/VillageIntroChecks/stage.py"), run_name="__main__")
# Runtime chapter content and the boss prefab are loaded through Resources.
for path in (repo / "Assets/Resources").rglob("*"):
    if path.is_file() and path.suffix != ".meta":
        stage["copy"](path)
shutil.copy2(Path(__file__).with_name("SlothChapterBatchChecks.cs"),
             scratch / "Assets/Editor/SlothChapterBatchChecks.cs")
shutil.copy2(Path(__file__).with_name("SlothChapterPlayChecks.cs"),
             scratch / "Assets/SlothChapterPlayChecks.cs")
(scratch.parent / "sloth-chapter").mkdir(exist_ok=True)
print("Sloth chapter staged; screenshots and results go to", scratch.parent / "sloth-chapter")
