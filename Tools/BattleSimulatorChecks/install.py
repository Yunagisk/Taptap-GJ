"""Install only the simulator assets generated and exercised in the existing isolated project."""
from pathlib import Path
import hashlib
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'output/fuya-integration/UnityProject'
OUTPUT = ROOT / (sys.argv[1] if len(sys.argv) > 1 else 'output/battle-simulator-v1')
result = json.loads((OUTPUT / 'unity-validation.json').read_text(encoding='utf-8'))
assert result['passed'], result['error']
for script in ['BattleSimulator.cs', 'BattleSimulatorRound.cs']:
    relative = 'Assets/Scripts/Battle/UI/' + script
    assert (ROOT / relative).read_bytes() == (PROJECT / relative).read_bytes(), 'Validation source changed: ' + relative
relative = 'Assets/Scripts/Battle/Flow/BattleFlowController.cs'
assert (ROOT / relative).read_bytes() == (PROJECT / relative).read_bytes(), 'Battle flow changed after validation'
for relative in ['Assets/Prefabs/BattleSimulator', 'Assets/Prefabs/BattleSimulator.meta',
                 'Assets/Scenes/BattleSimulator.unity', 'Assets/Scenes/BattleSimulator.unity.meta']:
    source, target = PROJECT / relative, ROOT / relative
    if source.is_dir():
        shutil.copytree(source, target, dirs_exist_ok=True)
    else:
        shutil.copy2(source, target)

files = list((ROOT / 'Assets/Prefabs/BattleSimulator').rglob('*')) + [
    ROOT / 'Assets/Scenes/BattleSimulator.unity', ROOT / 'Assets/Scripts/Battle/UI/BattleSimulator.cs',
    ROOT / 'Assets/Scripts/Battle/UI/BattleSimulatorRound.cs', ROOT / 'Assets/Scripts/Battle/Flow/BattleFlowController.cs',
    ROOT / 'Assets/Scripts/Battle/UI/PlayerAttackAnimation.cs', ROOT / 'Assets/Scripts/Battle/UI/PlayerTurnSpriteBody.cs',
    ROOT / 'Assets/Scripts/Battle/UI/PlayerPoleThrust.cs', ROOT / 'Assets/Scripts/Battle/UI/PlayerOneHandWeaponSet.cs',
    ROOT / 'Assets/Scripts/Battle/UI/PlayerSwordEffects.cs',
    *list((ROOT / 'Assets/Sprite/Characters/Player/AnimationSamples/OneHandVariantsV1').glob('*'))]
manifest = {'passed': True, 'unityChecks': len(result['checks']), 'sha256': {
    p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.is_file()}}
(OUTPUT / 'integration-validation.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
(OUTPUT / 'README.txt').write_text(f'战斗模拟场景\n\n打开 Assets/Scenes/BattleSimulator.unity 后点击 Play。\n上方 11 个武器按钮和 4 个敌人按钮可直接切换，下一把武器/下一个敌人按钮可循环切换。双刀不参与。\n切换会重开当前对局；场上始终只有一个敌人。\n底部攻击、技能会自动选中唯一敌人，防御会推进敌人回合。\n另有查看敌人死亡、重新开战按钮。\n模拟使用独立状态，退出后恢复原局。\n\n敌人：腐鸦、瘟鼠、怨影、懒惰。懒惰沿用现有待机和战斗行为。\n链枷复用火把挥击；焰舌复用剑的刺击，均挂载各自原立绘。焰舌火焰独立播放。\n长矛/斧枪前刺增加后拉与上身、双手一起前送，后脚保持支撑，实际尖端行程约为之前的 2.4 倍。\n\n验证：{len(result["checks"])} 项 Unity 运行检查通过。实际 HUD 按钮执行所有武器普攻，四个敌人均走完防御/敌人回合；验证中途切换、死亡清理、循环按钮、原局状态还原及动作复用/刺击距离。\n截图：battle-simulator.png 以及 enemy-0.png 至 enemy-3.png。motions 下为实际 Unity 动作逐帧实录。\n', encoding='utf-8')
print(f'Installed Assets/Scenes/BattleSimulator.unity and 5 linked simulator prefabs; {len(result["checks"])} runtime checks passed.')
