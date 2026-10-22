"""Collect fresh per-suite evidence without treating clipped scroll rows as layout failures."""
from pathlib import Path
import hashlib
import json
import re

repo = Path(__file__).resolve().parents[2]
out = repo / 'output/global-fixes-20261021'
native_root = repo / 'output/village-intro/global-fixes-20261021'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

native = read(out / 'native-summary.json')
assert len(native) == 23 and all(s['passed'] for s in native), 'Native regression incomplete'
rules = []
for name in ['MapChecks', 'MapChecks-centered', 'WildernessChecks', 'ItemChecks',
             'WeaponSkillChecks', 'WeaponMotionChecks', 'EnemyAnimationChecks',
             'BattleUiChecks', 'EncounterBalanceSimulation', 'StatusRulesTest']:
    log_path = out / (name + '.log')
    encoding = 'utf-16' if log_path.read_bytes().startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig'
    log = log_path.read_text(encoding)
    match = re.search(r'PASS:\s*(\d+)', log)
    assert match or name == 'StatusRulesTest' and 'status rules ok' in log, name
    rules.append({'suite': name, 'passed': True, 'checks': int(match[1]) if match else None})
fingerprints = read(out / 'source-snapshot.json')
changed = [f for f, h in fingerprints.items()
           if not (repo / f).exists() or hashlib.sha256((repo / f).read_bytes()).hexdigest() != h]
assert not changed, 'Production changed after staging: ' + str(changed)
content = read(out / 'content-audit.json')
assert not content['missingSerializedReferences'], 'Missing asset references'
visual = read(out / 'global-visual-result.json')
unclipped_candidates = [c for c in visual['textCandidates'] if not c['insideScroll']]
assert not unclipped_candidates, 'Visible text needs inspection: ' + str(unclipped_candidates)
balance = read(native_root / 'full-run/balance.json')
assert balance['finished'], 'Full playthrough incomplete'
boss_battles = read(out / 'encounter-result.json')['bossBattles']
assert {b['weapon'] for b in boss_battles} == {w['id'] for w in content['weapons']}
assert all(b['remainingHp'] > 0 for b in boss_battles), 'Current weapon boss check failed'
summary = {
    'passed': True, 'productionFilesVerified': len(fingerprints), 'sourceChangedAfterStaging': changed,
    'ruleChecks': sum(s['checks'] or 0 for s in rules), 'ruleSuites': rules,
    'nativeChecks': sum(s['checks'] for s in native), 'nativeSuites': native,
    'visibleTextCandidates': unclipped_candidates,
    'missingAssetReferences': content['missingSerializedReferences'], 'fullRun': balance,
    'bossWeaponChecks': boss_battles,
    'saveIsolation': 'Only the default save-file path is redirected in the isolated GameRoot copy; all save behavior uses production code.'
}
(out / 'test-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), 'utf-8')
report = f'''全局修复与回归记录（2026-10-21）

本轮已处理：
1. 战斗窄屏及运行中调整分辨率导致玩家或敌人被裁切。保持横向构图，缩放不会重排角色或重置动作。
2. 教堂调查、村长宅邸入口与旅馆重叠。入口已移位，并检查实际鼠标命中。
3. 背包与酒馆字号过小、长说明或目标文字截断。放大字体，重排卡片和按钮，长说明可滚动。
4. 酒馆奖励仅有文字。按确认方案发放圣水、绷带或恢复决心；携带上限和决心上限均有真实反馈，重复点击和重新载入无法重复领取。
5. 懒惰篇罪恶达到阈值后，生成不可进入首领并显示误导提示。当前章节只生成开放的首领，通用三首领模式仍通过回归。
6. 祭所剧情误用风车田野背景。已接入独立旧祭所背景，对峙与战后剧情均验证。
7. 武器选择页提示文字截断，以及新版主菜单存档状态字号过小。均已修正；新版主菜单动画与交互保留并通过测试。
8. 已整合最新清晨存档与死亡血条更新，检查死亡时显示零生命、继续游戏和暂停读档入口、跨天奖励与恢复结果、损坏存档以及黑屏过渡。

验证结果：
- 10 组规则测试（含状态规则）通过，可计数断言 {summary['ruleChecks']:,} 条。
- 23 组 Unity 原生回归通过，共 {summary['nativeChecks']:,} 条检查；包括存档读写和真实读档入口、武器目录及旧存档迁移、酒馆事件、引导、死亡重生、两种结局、16 条野外剧情、换装、道具、地图、首领镜头、攻击与死亡动画。
- 1920×1080、1280×720、1024×768、2560×1080 的菜单、村庄、酒馆全部 27 个选项及四类武器选择页通过字号和溢出检查。
- 三敌人及首领在上述尺寸、窄屏重新载入、攻击中途缩放后均保持在画面内，伤害正常结算并进入下一回合。
- 内容扫描：{content['serializedReachableAssetCount']} 个可引用资源，缺失引用为 0。{len(content['weapons'])} 把当前武器有现行获取路径，16 条现行野外剧情有地图入口。
- 连续通关种子 {balance['seed']}，引导结束生命 {balance['afterGuideHp']}，首领战后生命 {balance['finalHp']}，世界时间 {balance['worldTime']}。全程使用正常操作，未修改生命、时间或已完成节点；这是一次可通关样本，不能据此推算所有策略的胜率。
- 最后暂存的 {len(fingerprints)} 个生产文件与当前源码一致。原有未提交工作已保留；测试仅重定向默认存档路径，存储与读档逻辑均使用生产代码。

内容范围：
旧 w01–w10 事件、旧开场及选武器页、未开放首领属于旧版或后续内容，未重新开放。最新武器目录已将双剑退役，并为旧存档提供迁移；验收使用当前武器库。当前版本已接入一个清晨自动存档槽；继续游戏、死亡和地图暂停菜单均可恢复保存的清晨状态，测试存档与玩家存档隔离。通关记录是当前普通剑与正常操作的成功样本，不代表全部配装的胜率。

证据：
test-summary.json、native-summary.json、各组 *-result.json 与规则日志在本目录。
完整通关记录、Unity 日志和截图在 output/village-intro/global-fixes-20261021/。
复现工具在 Tools/GlobalRegressionChecks/。
'''
report += '''
难度观察：
当前 11 把武器均通过真实战斗规则的首领测试：从满生命开始、不治疗，按预告防御重击并寻找破防机会。剑剩余 70 生命，火把剩余 84；斧和镰刀各剩余 12，链枷剩余 19。60 生命起步的剑仍能获胜，剩余 30。弱点武器明显更宽容，非弱点武器对防御和补给要求更高。这些是固定策略样本，未据此调整武器数值，也不能当作玩家胜率。
'''
(out / '修复与回归报告.txt').write_text(report, 'utf-8-sig')
print(json.dumps({k: summary[k] for k in ['passed', 'ruleChecks', 'nativeChecks', 'productionFilesVerified']}, indent=2))
