"""Bind Sloth to the same continuous original-art animation system as Rat and Wraith."""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID('31b5bb7c-186c-46f4-8277-9ca3d28813e0')


def meta(relative, importer='NativeFormatImporter', file_id=11400000):
    path = ROOT / (relative + '.meta')
    if path.exists():
        return re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)[1]
    guid = uuid.uuid5(NS, relative).hex
    content = f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n'
    if importer == 'NativeFormatImporter':
        content += f'  mainObjectFileID: {file_id}\n'
    content += '  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    path.write_text(content, encoding='utf-8')
    return guid


def painted_bounds(relative):
    # Read-only alpha measurement; no raster asset is altered or synthesized.
    with Image.open(ROOT / relative) as image:
        return image.size, image.getchannel('A').point(lambda alpha: 255 if alpha >= 48 else 0).getbbox()


new_size, new_box = painted_bounds('Assets/Sprite/Enemies/懒惰.png')
old_size, old_box = painted_bounds('Assets/Sprite/Enemies/LanDuo/1.png')
source_meta = (ROOT / 'Assets/Sprite/Enemies/懒惰.png.meta').read_text(encoding='utf-8-sig')
source_guid = re.search(r'^guid: (\w+)', source_meta, re.M)[1]
# Unity retains the source world size when maxTextureSize scales the imported texture:
# sprite pixelsPerUnit is scaled along with sprite.rect. Use source pixels here.
new_pixels = max(new_size)
bounds = (new_box[0] / new_size[0], 1 - new_box[3] / new_size[1],
          new_box[2] / new_size[0], 1 - new_box[1] / new_size[1])
old_height = old_box[3] - old_box[1]
scale = old_height / ((bounds[3] - bounds[1]) * new_pixels)
old_center = ((old_box[0] + old_box[2] - old_size[0]) / 200,
              (old_size[1] / 2 - old_box[3]) / 100)
offset = (old_center[0] - ((bounds[0] + bounds[2] - 1) * new_pixels / 200) * scale,
          old_center[1] - ((bounds[1] - .5) * new_pixels / 100) * scale)
asset_path = 'Assets/Data/EnemyAnimations/SlothAnimations.asset'
set_script = re.search(r'^guid: (\w+)', (ROOT / 'Assets/Scripts/Battle/UI/EnemySpriteAnimationSet.cs.meta').read_text(), re.M)[1]
animation_script = re.search(r'^guid: (\w+)', (ROOT / 'Assets/Scripts/Battle/UI/EnemySpriteAnimation.cs.meta').read_text(), re.M)[1]
material_guid = meta('Assets/Data/EnemyAnimations/OriginalEnemyArtwork.mat')
ash_guid = meta('Assets/Data/EnemyAnimations/FuYaBlackAsh.mat')
(ROOT / asset_path).write_text(f'''%YAML 1.1
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
  m_Name: SlothAnimations
  m_EditorClassIdentifier: 
  framesPerSecond: 30
  idleFrames:
  - {{fileID: 21300000, guid: {source_guid}, type: 3}}
  artworkMaterial: {{fileID: 2100000, guid: {material_guid}, type: 2}}
  preserveArtworkColors: 1
  portraitIcon: {{fileID: 21300000, guid: {source_guid}, type: 3}}
  useDeformedIdle: 1
  artworkProfile: 3
  artworkAttackFrames: 36
  artworkHurtFrames: 18
  artworkDeathFrames: 42
  artworkContact: {{x: 0.135, y: 0.245}}
  idleCycleSeconds: 4.8
  attackFrames: []
  hurtFrames: []
  deathFrames: []
  useDeformedDeath: 1
  artworkBounds: {{x: {bounds[0]}, y: {bounds[1]}, z: {bounds[2]}, w: {bounds[3]}}}
  deathHoldSeconds: 0.15
  deathFadeSeconds: 0.25
  deathAshMaterial: {{fileID: 2100000, guid: {ash_guid}, type: 2}}
  deathAshSeconds: 1
  attackContactFrame: 18
  attackLungeDistance: 0.04
  attackContactPoint: {{x: 0, y: 0}}
''', encoding='utf-8')
set_guid = meta(asset_path)


def bind(relative, go, renderer, component):
    path = ROOT / relative
    text = path.read_text(encoding='utf-8-sig')
    blocks = re.split(r'(?=--- !u!)', text)
    for i, block in enumerate(blocks):
        if block.startswith(f'--- !u!1 &{go}\n'):
            ref = f'  - component: {{fileID: {component}}}\n'
            if ref not in block:
                blocks[i] = block.replace('  m_Layer:', ref + '  m_Layer:', 1)
    blocks = [block for block in blocks if not block.startswith(f'--- !u!114 &{component}\n')]
    content = f'''--- !u!114 &{component}
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
  animations: {{fileID: 11400000, guid: {set_guid}, type: 2}}
  appearance: {{fileID: {renderer}}}
  visualScale: {{x: {scale}, y: {scale}, z: 1}}
  visualOffset: {{x: {offset[0]}, y: {offset[1]}, z: 0}}
'''
    index = next((i for i, block in enumerate(blocks) if block.startswith('--- !u!1660057539')), len(blocks))
    blocks.insert(index, content)
    path.write_text(''.join(blocks), encoding='utf-8')


bind('Assets/Scenes/TestScene.unity', 747181105, 747181107, 917530001)
bind('Assets/Prefabs/BattleSimulator/Sloth.prefab', 1859371728394112153, 210276560750786730, 917530002)
prefab = 'Assets/Prefabs/Enemies/Sloth.prefab'
(ROOT / prefab).write_bytes((ROOT / 'Assets/Prefabs/BattleSimulator/Sloth.prefab').read_bytes())
meta(prefab, 'PrefabImporter')
output = ROOT / 'output/sloth-v1'
output.mkdir(parents=True, exist_ok=True)
(output / 'asset-validation.json').write_text(json.dumps({
    'fps': 30, 'idleCycleFrames': 144, 'attackFrames': 36, 'hurtFrames': 18,
    'deathFrames': 42, 'source': 'Assets/Sprite/Enemies/懒惰.png',
    'sourceSha256': hashlib.sha256((ROOT / 'Assets/Sprite/Enemies/懒惰.png').read_bytes()).hexdigest(),
    'oldPaintedBounds': old_box, 'newPaintedBounds': new_box,
    'visualScale': scale, 'visualOffset': offset,
    'bindings': ['TestScene', 'BattleSimulator/Sloth', 'Enemies/Sloth']
}, ensure_ascii=False, indent=2), encoding='utf-8')
print('Installed Sloth: 30 FPS, 144-pose idle, 36-frame attack, 18-frame hurt, 42-frame death and ash.')
