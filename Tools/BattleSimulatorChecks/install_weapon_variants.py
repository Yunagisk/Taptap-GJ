"""Register existing weapon drawings at their grips and bind the shared one-hand motion variants."""
from pathlib import Path
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID('31b5bb7c-186c-46f4-8277-9ca3d28813e0')


def meta(path, kind):
    target = ROOT / (path + '.meta')
    if target.exists():
        return re.search(r'^guid: (\w+)', target.read_text(encoding='utf-8-sig'), re.M)[1]
    key = uuid.uuid5(NS, path).hex
    text = f'fileFormatVersion: 2\nguid: {key}\n'
    if kind == 'script':
        text += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    elif kind == 'folder':
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    else:
        text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
    target.write_text(text + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    return key


set_script = meta('Assets/Scripts/Battle/UI/PlayerOneHandWeaponSet.cs', 'script')
meta('Assets/Scripts/Battle/UI/PlayerPoleThrust.cs', 'script')
folder = 'Assets/Sprite/Characters/Player/AnimationSamples/OneHandVariantsV1'
(ROOT / folder).mkdir(exist_ok=True)
meta(folder, 'folder')
template = (ROOT / 'Assets/Sprite/Characters/Player/AnimationSamples/OneHandSwingV1/WeaponTorch.png.meta').read_text(encoding='utf-8')
sprites = {}
for name, source, pivot in [
    ('Flail', 'Assets/Sprite/Items/WeaponIllustrations/Flail.png', (735, 1255)),
    ('FlameTongue', 'Assets/Sprite/Items/WeaponIllustrations/Layers/FlameTongueBody.png', (1024, 1545)),
    ('FlameTongueFlames', 'Assets/Sprite/Items/WeaponIllustrations/Layers/FlameTongueFlames.png', (1024, 1545)),
]:
    path = folder + '/' + name + '.png'
    shutil.copy2(ROOT / source, ROOT / path)
    key = uuid.uuid5(NS, path).hex
    text = re.sub(r'^guid: \w+', 'guid: ' + key, template, flags=re.M)
    text = re.sub(r'spriteID: \w+', 'spriteID: ' + key, text)
    text = re.sub(r'  alignment: \d+', '  alignment: 9', text)
    text = re.sub(r'spritePivot: \{[^}]+\}', f'spritePivot: {{x: {pivot[0] / 2048}, y: {1 - pivot[1] / 2048}}}', text)
    Path(str(ROOT / path) + '.meta').write_text(text, encoding='utf-8')
    sprites[name] = key

asset = folder + '/OneHandWeapons.asset'
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
  m_Name: OneHandWeapons
  m_EditorClassIdentifier: 
  weapons:
  - id: flail
    swing: 1
    sprite: {{fileID: 21300000, guid: {sprites['Flail']}, type: 3}}
    flames: {{fileID: 0}}
    scale: 0.65
    trailHeel: {{x: 3.8, y: 0.2}}
    trailTip: {{x: 4.8, y: 3.6}}
  - id: flame_tongue
    swing: 0
    sprite: {{fileID: 21300000, guid: {sprites['FlameTongue']}, type: 3}}
    flames: {{fileID: 21300000, guid: {sprites['FlameTongueFlames']}, type: 3}}
    scale: 0.55
    trailHeel: {{x: 0, y: 4.7}}
    trailTip: {{x: 0, y: 11.3}}
''', encoding='utf-8')
set_guid = meta(asset, 'asset')
component_guid = re.search(r'^guid: (\w+)', (ROOT / 'Assets/Scripts/Battle/UI/PlayerAttackAnimation.cs.meta').read_text(), re.M)[1]
changed = []
for path in [*(ROOT / 'Assets/Scenes').glob('*.unity'), *(ROOT / 'Assets/Prefabs').rglob('*.prefab')]:
    original = path.read_text(encoding='utf-8-sig')
    blocks = re.split(r'(?=--- !u!)', original)
    for i, block in enumerate(blocks):
        if 'guid: ' + component_guid not in block or re.search(r'^  oneHandWeapons:', block, re.M):
            continue
        block = re.sub(r'(^  poleAnimations:.*\n)', r'\1' + f'  oneHandWeapons: {{fileID: 11400000, guid: {set_guid}, type: 2}}\n', block, flags=re.M)
        blocks[i] = block
    updated = ''.join(blocks)
    if updated != original:
        path.write_text(updated, encoding='utf-8'); changed.append(path.relative_to(ROOT).as_posix())
print('Registered unchanged original drawings and bound one-hand variants:', ', '.join(changed))
