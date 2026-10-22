"""Copy only validation inputs into an isolated Unity project, using local packages."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'output/combat-impact-v1/UnityProject'

def copy(relative):
    source, target = ROOT / relative, PROJECT / relative
    if source.is_dir():
        shutil.copytree(source, target, dirs_exist_ok=True)
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)

for folder in ['Assets/Scripts', 'Assets/Data/EnemyAnimations', 'Assets/Data/EnemyAttacks',
               'Assets/Data/Weapons', 'Assets/Data/Skills', 'Assets/Animation/Enemy',
               'Assets/Prefabs/Enemies', 'Assets/Prefabs/BattleSimulator',
               'Assets/Sprite/Enemies', 'ProjectSettings']:
    copy(folder)
shutil.copytree(ROOT / 'output/fuya-integration/UnityProject/Packages', PROJECT / 'Packages', dirs_exist_ok=True)
editor = PROJECT / 'Assets/Editor'
editor.mkdir(parents=True, exist_ok=True)
shutil.copy2(ROOT / 'Tools/EnemyAnimationChecks/CombatImpactValidation.cs', editor / 'CombatImpactValidation.cs')
print(PROJECT)
