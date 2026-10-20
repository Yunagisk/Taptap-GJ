"""Install deterministic data and precise scene bindings; does not touch other actors."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID('31b5bb7c-186c-46f4-8277-9ca3d28813e0')


def guid(path):
    return uuid.uuid5(NS, path).hex


def meta(path, kind):
    p = ROOT / path
    target = Path(str(p) + '.meta')
    if target.exists():
        return re.search(r'^guid: (\w+)', target.read_text(encoding='utf-8-sig'), re.M)[1]
    content = f'fileFormatVersion: 2\nguid: {guid(path)}\n'
    if kind == 'FolderImporter':
        content += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif kind == 'MonoImporter':
        content += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif kind == 'NativeFormatImporter':
        content += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        content += kind + ':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text(content, encoding='utf-8')
    return guid(path)


for path in ['Assets/Scripts/Battle/UI/EnemyArtworkMotion.cs', 'Assets/Scripts/Battle/UI/SmallMonsterAnimationPreview.cs']:
    meta(path, 'MonoImporter')

artwork = 'Assets/Data/EnemyAnimations/OriginalEnemyArtwork.mat'
source = (ROOT / 'Assets/Data/EnemyAnimations/FuYaArtwork.mat').read_text(encoding='utf-8')
(ROOT / artwork).write_text(source.replace('m_Name: FuYaArtwork', 'm_Name: OriginalEnemyArtwork').replace('_ArtworkGamma: 1.45', '_ArtworkGamma: 1'), encoding='utf-8')
material_guid = meta(artwork, 'NativeFormatImporter')
material_meta = Path(str(ROOT / artwork) + '.meta')
material_meta.write_text(material_meta.read_text(encoding='utf-8').replace('mainObjectFileID: 11400000', 'mainObjectFileID: 2100000'), encoding='utf-8')
animation_script = 'ce81c491ad4d5ee795cb123185e55937'
set_script = 'f13a55a793db53ddad46c68a67f511bf'
ash_guid = '48df0611b8cf599dac0f42ad22c1af73'
monsters = [
    dict(name='Rat', original='老鼠小怪', profile=1, sprite='3205e2bcb6be15b4697739f4c6893465',
         bbox=(44, 473, 1917, 1560), old_bbox=(60, 282, 1214, 986), old_ppu=100,
         go=1285487321, renderer=1285487323, component=917520001,
         attack=21, hit=9, hurt=12, death=27, contact=(.045, .395), cycle=2.4),
    dict(name='Wraith', original='怨影小怪', profile=2, sprite='dd9bdc76ba1b25945b4044bce0dfcef2',
         bbox=(268, 178, 1612, 1642), old_bbox=(44, 24, 1198, 1246), old_ppu=100,
         go=1157243007, renderer=1157243009, component=917520002,
         attack=27, hit=12, hurt=15, death=33, contact=(.015, .635), cycle=3.2),
]


def binding(m, go, renderer, component, scale=1, offset=(0, 0)):
    return f'''--- !u!114 &{component}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {animation_script}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  animations: {{fileID: 11400000, guid: {m['set_guid']}, type: 2}}
  appearance: {{fileID: {renderer}}}
  visualScale: {{x: {scale}, y: {scale}, z: 1}}
  visualOffset: {{x: {offset[0]}, y: {offset[1]}, z: 0}}
'''


for m in monsters:
    x0, y0, x1, y1 = m['bbox']
    b = (x0 / 2048, (2048 - y1) / 2048, x1 / 2048, (2048 - y0) / 2048)
    m['bounds'] = b
    asset = f"Assets/Data/EnemyAnimations/{m['name']}Animations.asset"
    (ROOT / asset).write_text(f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {set_script}, type: 3}}
  m_Name: {m['name']}Animations
  m_EditorClassIdentifier: 
  framesPerSecond: 30
  idleFrames:
  - {{fileID: 21300000, guid: {m['sprite']}, type: 3}}
  artworkMaterial: {{fileID: 2100000, guid: {material_guid}, type: 2}}
  preserveArtworkColors: 1
  portraitIcon: {{fileID: 21300000, guid: {m['sprite']}, type: 3}}
  useDeformedIdle: 1
  artworkProfile: {m['profile']}
  artworkAttackFrames: {m['attack']}
  artworkHurtFrames: {m['hurt']}
  artworkDeathFrames: {m['death']}
  artworkContact: {{x: {m['contact'][0]}, y: {m['contact'][1]}}}
  idleCycleSeconds: {m['cycle']}
  attackFrames: []
  hurtFrames: []
  deathFrames: []
  useDeformedDeath: 0
  hurtAndAshOnDeath: 1
  artworkBounds: {{x: {b[0]}, y: {b[1]}, z: {b[2]}, w: {b[3]}}}
  deathHoldSeconds: 0
  deathFadeSeconds: 0.25
  deathAshMaterial: {{fileID: 2100000, guid: {ash_guid}, type: 2}}
  deathAshSeconds: 1
  attackContactFrame: {m['hit']}
  attackLungeDistance: {0.16 if m['profile'] == 1 else 0.06}
  attackContactPoint: {{x: 0, y: 0}}
''', encoding='utf-8')
    m['set_guid'] = meta(asset, 'NativeFormatImporter')

# Replace only the two components and neutralize only their renderers.
scene_path = ROOT / 'Assets/Scenes/TestScene.unity'
scene = scene_path.read_text(encoding='utf-8-sig')
blocks = re.split(r'(?=--- !u!)', scene)
for m in monsters:
    for i, block in enumerate(blocks):
        if block.startswith(f"--- !u!1 &{m['go']}\n"):
            component = f"  - component: {{fileID: {m['component']}}}\n"
            if component not in block:
                blocks[i] = block.replace('  m_Layer:', component + '  m_Layer:', 1)
        if block.startswith(f"--- !u!212 &{m['renderer']}\n"):
            blocks[i] = re.sub(r'  m_Color: .*', '  m_Color: {r: 1, g: 1, b: 1, a: 1}', block)
    ox0, oy0, ox1, oy1 = m['old_bbox']
    x0, y0, x1, y1 = m['bbox']
    scale = (oy1 - oy0) / (y1 - y0)
    # Align old painted center and baseline while keeping the actor transform intact.
    offset = (((ox0 + ox1) / 2 - 627) / 100 - ((x0 + x1) / 2 - 1024) / 100 * scale,
              (627 - oy1) / 100 - (1024 - y1) / 100 * scale)
    blocks = [b for b in blocks if not b.startswith(f"--- !u!114 &{m['component']}\n")]
    # Insert before SceneRoots, not after its document.
    index = next((i for i, b in enumerate(blocks) if b.startswith('--- !u!1660057539')), len(blocks))
    blocks.insert(index, binding(m, m['go'], m['renderer'], m['component'], scale, offset))
scene_path.write_text(''.join(blocks), encoding='utf-8')

# Prefabs inherit working combat components, then receive species visuals and a basic attack.
combat = (ROOT / 'Assets/Scenes/CombatScene.unity').read_text(encoding='utf-8-sig')
all_blocks = re.split(r'(?=--- !u!)', combat)
actor_ids = [2022034366, 2022034367, 2022034368, 2022034369, 2022034370, 2022034371]
for m in monsters:
    selected = [b for b in all_blocks if re.match(r'--- !u!\d+ &(-?\d+)', b) and int(re.match(r'--- !u!\d+ &(-?\d+)', b)[1]) in actor_ids]
    text = ''.join(selected)
    text = text.replace('  m_Name: Enemy1', f"  m_Name: {m['name']}")
    text = text.replace('  - component: {fileID: 917510001}', '  - component: {fileID: 917520010}')
    text = text.replace('  - component: {fileID: 2022034372}\n', '').replace('  - component: {fileID: 2022034373}\n', '')
    text = re.sub(r'  m_Children:\n(?:  - .*\n)+', '  m_Children: []\n', text)
    text = re.sub(r'  m_LocalPosition: .*', '  m_LocalPosition: {x: 0, y: 0, z: 0}', text)
    text = re.sub(r'  m_LocalScale: .*', '  m_LocalScale: {x: 0.25, y: 0.25, z: 1}', text)
    text = text.replace('  maxHp: 1', '  maxHp: 50')
    # Keep the existing basic strike; no crow-specific cycling riders on these reusable actors.
    text = re.sub(r'(  attackDatas:\n  - .*\n)(?:  - .*\n)+', r'\1', text)
    text = text.replace('fe4191f31cbb0c64794e64e53cbc2e57', m['sprite'])
    b = m['bounds']
    text = re.sub(r'  m_Offset: .*', f"  m_Offset: {{x: {(b[0]+b[2]-1)*10.24}, y: {(b[1]+b[3]-1)*10.24}}}", text)
    text = re.sub(r'  m_Size: .*', f"  m_Size: {{x: {(b[2]-b[0])*20.48}, y: {(b[3]-b[1])*20.48}}}", text)
    text += binding(m, 2022034366, 2022034370, 917520010)
    folder = ROOT / 'Assets/Prefabs/Enemies'
    folder.mkdir(parents=True, exist_ok=True)
    path = f"Assets/Prefabs/Enemies/{m['name']}.prefab"
    (ROOT / path).write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + text, encoding='utf-8')
    meta(path, 'PrefabImporter')

meta('Assets/Prefabs/Enemies', 'FolderImporter')

print('Installed Rat/Wraith sets, combat prefabs and both TestScene bindings.')
