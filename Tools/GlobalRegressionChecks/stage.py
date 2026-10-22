from pathlib import Path
import runpy, shutil, sys, json, hashlib, argparse, re

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--run-name', default='global-fixes-20261021')
run_name = parser.parse_args().run_name
assert re.fullmatch(r'[a-z0-9][a-z0-9-]*', run_name), 'Use a simple output folder name'
result_root = repo / 'output' / run_name
root = repo / 'output/village-intro' / run_name
scratch = root / 'UnityProject'
assert scratch.resolve().is_relative_to((repo / 'output/village-intro').resolve())
previous_snapshot = result_root / 'source-snapshot.json'
previous_files = set(json.loads(previous_snapshot.read_text('utf-8-sig'))) if previous_snapshot.exists() else set()
# Reusing the import cache must not retain deleted production assets (for
# example a retired weapon). Generated validation scripts remain staged below.
if (scratch / 'Assets').exists():
    for asset in (scratch / 'Assets').rglob('*'):
        if not asset.is_file() or asset.suffix == '.meta':
            continue
        if asset.suffix == '.cs' and str(asset.relative_to(scratch)) not in previous_files:
            continue
        if (repo / asset.relative_to(scratch)).exists():
            continue
        assert asset.resolve().is_relative_to(scratch.resolve())
        asset.unlink()
        meta = Path(str(asset) + '.meta')
        if meta.exists():
            assert meta.resolve().is_relative_to(scratch.resolve())
            meta.unlink()
sys.argv = ['stage.py', str(scratch)]
staged = runpy.run_path(str(repo / 'Tools/VillageIntroChecks/stage.py'), run_name='__main__')
copy = staged['copy']
for folder in ['Assets/Data', 'Assets/Resources', 'Assets/Prefabs', 'Assets/Scenes']:
    for path in (repo / folder).rglob('*'):
        if path.is_file() and path.suffix != '.meta':
            copy(path)
for path in (repo / 'Assets').rglob('*'):
    if path.suffix in {'.anim', '.controller', '.overrideController'}:
        copy(path)

groups = ['SlothChapterChecks', 'InventoryUiChecks', 'VillageServiceChecks', 'VillageAmbienceChecks', 'DialogueUiChecks',
          'DayStartSaveChecks', 'WeaponCatalogChecks', 'TavernChecks']
for group in groups:
    for src in (repo / 'Tools' / group).glob('*.cs'):
        target = scratch / 'Assets' / ('Editor' if 'Batch' in src.name else '') / src.name
        data = src.read_text('utf-8-sig')
        # Exercise the installed scene/prefab, without reauthoring the test copy.
        if group == 'VillageServiceChecks' and 'Batch' in src.name:
            data = data.replace('VillageServicesBuilder.Build();', '')
            data = data.replace('VillageServicesBuilder.ScenePath', '"Assets/Scenes/Village.unity"')
        if group == 'DialogueUiChecks' and 'Batch' in src.name:
            data = data.replace('RpgDialoguePrefabBuilder.Build();', '')
        target.write_text(data, encoding='utf-8')
for name in ['SidequestBatchChecks', 'SidequestPlayChecks']:
    shutil.copy2(repo / 'Tools/WildernessChecks' / (name + '.cs'),
                 scratch / 'Assets' / ('Editor' if 'Batch' in name else '') / (name + '.cs'))
for group, name in [('EncounterBalanceSimulation','SmallMonsterValidation'),
                    ('BattleTargetingChecks','BattleTargetingValidation'),
                    ('MapFastTravelChecks','MapFastTravelValidation'),
                    ('MapMenuChecks','MapMenuValidation'),
                    ('MapBossCameraChecks','MapBossCameraValidation')]:
    source = (repo / 'Tools' / group / 'UnityValidation.cs').read_text('utf-8-sig')
    source = source.replace('MapMenuPolishBuilder.Apply();', '')
    (scratch / 'Assets/Editor' / (name + '.cs')).write_text(source, 'utf-8')
shutil.copy2(repo / 'Tools/BattleTargetingChecks/PlayerStatusNativeChecks.cs', scratch / 'Assets/Editor/PlayerStatusNativeChecks.cs')
for name in ['AttackMotionValidation','HurtAshValidation','SlothValidation']:
    shutil.copy2(repo / 'Tools/EnemyAnimationChecks' / (name + '.cs'), scratch / 'Assets/Editor' / (name + '.cs'))

# Snapshot fingerprints allow later changes to be distinguished from tested content.
for src in (repo / 'Assets/Editor').rglob('*.cs'):
    copy(src)

# The storage backend is unchanged; redirect only its default file path in the
# isolated project so no native suite can read or overwrite the player's slot.
game_root = scratch / 'Assets/Scripts/Core/GameRoot.cs'
source = game_root.read_text('utf-8-sig')
original_path = 'System.IO.Path.Combine(Application.persistentDataPath, "day-start-save-v1.json")'
assert original_path in source, 'Review save-path isolation after GameRoot changes'
game_root.write_text(source.replace(original_path,
    'System.Environment.GetEnvironmentVariable("TAPTAP_GJ_QA_SAVE_PATH")'), 'utf-8')

snapshot = {str(p.relative_to(repo)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in staged['included'] if p.is_file()}
result_root.mkdir(parents=True, exist_ok=True)
(result_root / 'source-snapshot.json').write_text(json.dumps(snapshot, indent=2), 'utf-8')
for name in ['sloth-chapter','sidequests']:
    (root / name).mkdir(exist_ok=True)
for name in ['GlobalVisualAudit', 'BattleResizeAudit']:
    source = (Path(__file__).parent / (name + '.cs')).read_text('utf-8')
    header = source[:source.index('[InitializeOnLoad]')]
    split = source.index('public sealed class ' + name + 'Runner')
    (scratch / 'Assets/Editor' / (name + '.cs')).write_text(source[:split] + '#endif\n', 'utf-8')
    (scratch / 'Assets' / (name + 'Runner.cs')).write_text(header + source[split:], 'utf-8')
for name, folder in [('FullRunBatchChecks.cs', 'Editor'), ('FullRunPlayChecks.cs', ''), ('FullRouteDriver.cs', '')]:
    shutil.copy2(Path(__file__).parent / name, scratch / 'Assets' / folder / name)
source = (repo / 'Tools/MainMenuOpeningChecks/UnityValidation.cs').read_text('utf-8-sig')
source = source.replace('        MainMenuOpeningBuilder.Apply();\n        MainMenuOpeningBuilder.Apply();',
                        '        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");')
(scratch / 'Assets/Editor/MainMenuOpeningValidation.cs').write_text(source, 'utf-8')
shutil.copy2(Path(__file__).parent / 'DayStartSaveValidation.cs', scratch / 'Assets/Editor/DayStartSaveValidation.cs')
print('Audit staged', len(snapshot), 'production files:', scratch)
