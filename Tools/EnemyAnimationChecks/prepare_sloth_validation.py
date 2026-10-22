"""Prepare a separate native Unity project without opening or changing the user's editor."""
from pathlib import Path
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'output/sloth-v1/UnityProject'
PROJECT.mkdir(parents=True, exist_ok=True)


def copy(relative):
    source, target = ROOT / relative, PROJECT / relative
    if source.is_dir():
        shutil.copytree(source, target, dirs_exist_ok=True)
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


for folder in ['Assets/Scripts', 'Assets/Data/EnemyAnimations', 'Assets/Data/EnemyAttacks',
               'Assets/Data/Weapons', 'Assets/Data/Skills', 'Assets/Animation/Enemy',
               'Assets/Prefabs/Enemies', 'Assets/Sprite/Enemies/LanDuo', 'ProjectSettings']:
    copy(folder)
for name in ['懒惰', '瘟鼠小怪', '怨影小怪']:
    copy(f'Assets/Sprite/Enemies/{name}.png')
    copy(f'Assets/Sprite/Enemies/{name}.png.meta')
for relative in ['Assets/Scenes/TestScene.unity', 'Assets/Scenes/TestScene.unity.meta',
                 'Assets/Prefabs/BattleSimulator/Sloth.prefab', 'Assets/Prefabs/BattleSimulator/Sloth.prefab.meta']:
    copy(relative)
# Local embedded packages avoid any network dependency.
package_source = ROOT / 'output/fuya-integration/UnityProject/Packages'
shutil.copytree(package_source, PROJECT / 'Packages', dirs_exist_ok=True)
editor = PROJECT / 'Assets/Editor'
editor.mkdir(exist_ok=True)
shutil.copy2(ROOT / 'Tools/EnemyAnimationChecks/SlothValidation.cs', editor / 'SlothValidation.cs')
print(PROJECT)
