from pathlib import Path
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'output/fuya-integration/UnityProject'
NS = uuid.UUID('31b5bb7c-186c-46f4-8277-9ca3d28813e0')
for name in ['BattleSimulator', 'BattleSimulatorRound']:
    path = f'Assets/Scripts/Battle/UI/{name}.cs'
    meta = ROOT / (path + '.meta')
    if not meta.exists():
        meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid5(NS, path).hex}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')

for relative in ['Assets/Scripts', 'Assets/Sprite', 'Assets/Data', 'Assets/Animation', 'Assets/Fonts', 'Assets/Prefabs/UI', 'Assets/Prefabs/Enemies', 'Assets/Prefabs/BattleSimulator', 'Assets/VFX', 'Assets/Art', 'Assets/Configs', 'Assets/TextMesh Pro']:
    source, target = ROOT / relative, PROJECT / relative
    shutil.copytree(source, target, dirs_exist_ok=True)
    if Path(str(source) + '.meta').exists():
        shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
for scene in ['CombatScene', 'TestScene']:
    for suffix in ['.unity', '.unity.meta']:
        shutil.copy2(ROOT / f'Assets/Scenes/{scene}{suffix}', PROJECT / f'Assets/Scenes/{scene}{suffix}')
shutil.copy2(ROOT / 'Tools/BattleSimulatorChecks/UnityValidation.cs', PROJECT / 'Assets/Editor/BattleSimulatorValidation.cs')
shutil.copy2(ROOT / 'Tools/EnemyAnimationChecks/HurtAshValidation.cs', PROJECT / 'Assets/Editor/EnemyHurtAshValidation.cs')
print('Prepared simulator in the existing isolated Unity project.')
