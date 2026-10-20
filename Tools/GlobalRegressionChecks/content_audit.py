from pathlib import Path
import json, re, collections, math

repo = Path(__file__).resolve().parents[2]
out = repo / 'output/global-fixes-20261021'
guid_paths = {}
for root in ['Assets', 'Packages', 'Library/PackageCache']:
    for meta in (repo / root).rglob('*.meta'):
        try:
            m = re.search(r'^guid: (\w+)', meta.read_text('utf-8-sig'), re.M)
            asset = Path(str(meta)[:-5])
            if m and asset.is_file():
                guid_paths[m[1]] = asset
        except (UnicodeError, PermissionError):
            pass

build = (repo / 'ProjectSettings/EditorBuildSettings.asset').read_text('utf-8-sig')
scene_paths = re.findall(r'enabled: 1\s+path: (.+)', build)
roots = [repo / p.strip() for p in scene_paths]
roots += [p for p in (repo / 'Assets/Resources').rglob('*') if p.is_file() and p.suffix != '.meta']
visited, missing = set(), []
todo = list(roots)
while todo:
    path = todo.pop()
    if path in visited or not path.is_file():
        continue
    visited.add(path)
    if path.suffix not in {'.asset', '.unity', '.prefab', '.mat', '.controller', '.anim', '.overrideController', '.shader'}:
        continue
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})', path.read_text('utf-8-sig', errors='ignore'))):
        if guid in guid_paths:
            todo.append(guid_paths[guid])
        elif not guid.startswith('0000000000000000'):
            missing.append({'asset': str(path.relative_to(repo)), 'guid': guid})

catalog = json.loads((repo / 'Assets/Data/Wilderness/WildernessEvents.json').read_text('utf-8-sig'))
active = set(catalog['generationTemplateIds'])
layout = json.loads((repo / 'Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.json').read_text('utf-8-sig'))
nodes = {n['id']: n for n in layout['nodes']}
seen, todo = set(), [layout['villageNodeId']]
while todo:
    key = todo.pop()
    if key in seen or nodes[key]['kind'] == 4:
        continue
    seen.add(key)
    todo += nodes[key]['neighbors']
question_nodes = [n['id'] for n in layout['nodes'] if n['kind'] == 2]
event_rows = []
for t in catalog['templates']:
    event_rows.append({'id': t['id'], 'title': t['title'], 'active': t['id'] in active,
                       'weight': t['weight'], 'rewards': [
                           {k: o[k] for k in ['weaponId','itemId','itemCount','rewardText','repairBridge','encounter'] if o.get(k)}
                           for c in t['choices'] for o in c['outcomes']
                           if any(o.get(k) for k in ['weaponId','itemId','rewardText','repairBridge','encounter'])]})

weapon_rows = []
for p in sorted((repo / 'Assets/Data/Weapons').glob('KBW*.asset')):
    raw = p.read_text('utf-8-sig')
    wid = re.search(r'^  weaponId: (.+)', raw, re.M)[1]
    name = re.search(r'^  weaponName: (.+)', raw, re.M)[1]
    try: name = json.loads(name)
    except ValueError: pass
    weapon_rows.append({'id':wid, 'name':name, 'asset':str(p.relative_to(repo)),
                        'serializedReachable': p in visited,
                        'entry': 'wilderness:' + ('side16' if wid == 'heaven_fire' else 'side17')
                            if wid in {'heaven_fire','flame_tongue'} else 'village weapon shop',
                        'starterReward':wid in {'sword','axe','hammer'}})

dialogue_rows = []
for p in sorted((repo / 'Assets/Data/DialogueData').glob('*.asset')):
    raw = p.read_text('utf-8-sig')
    m = re.search(r'^  dialogueId: (.*)', raw, re.M)
    dialogue_rows.append({'asset':str(p.relative_to(repo)), 'dialogueId':m[1] if m else None,
                           'serializedReachable':p in visited})

data = {'buildScenes':scene_paths, 'missingSerializedReferences':missing,
        'serializedReachableAssetCount':len(visited), 'weapons':weapon_rows,
        'dialogues':dialogue_rows, 'wilderness':event_rows,
        'questionNodes':question_nodes, 'questionNodesReachableBeforeBoss':[n for n in question_nodes if n in seen],
        'eventProbabilityFullExploration':{'eachEvent':6/16,'bothExclusiveWeapons':math.comb(14,4)/math.comb(16,6),
                                         'atLeastOneExclusiveWeapon':1-math.comb(14,6)/math.comb(16,6)},
        'caveat':'Serialized references establish asset availability, not proof of an interactive entry. Active code overrides are reviewed separately.'}
(out / 'content-audit.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps({'assets':len(visited), 'missing':len(missing), 'weapons':len(weapon_rows),
                  'dialogues':len(dialogue_rows), 'unreferencedDialogueAssets':[d['asset'] for d in dialogue_rows if not d['serializedReachable']],
                  'activeEvents':len(active),'inactiveEvents':[e['id'] for e in event_rows if not e['active']],
                  'eventNodes':question_nodes,'reachable':data['questionNodesReachableBeforeBoss']},ensure_ascii=True))
