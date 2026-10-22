"""Install the selected artwork and surgically update only Sloth's map-node overrides."""
from pathlib import Path
import math
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID('31b5bb7c-186c-46f4-8277-9ca3d28813e0')
ART = 'Assets/Sprite/UI/MapArt2K/SlothBossNode.png'
guid = uuid.uuid5(NS, ART).hex
meta = ROOT / (ART + '.meta')
if meta.exists():
    guid = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)[1]
else:
    template = (ROOT / 'Assets/Sprite/UI/MapArt2K/BattleNode.png.meta').read_text(encoding='utf-8')
    template = re.sub(r'^guid: \w+', 'guid: ' + guid, template, flags=re.M)
    template = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid5(NS, ART + ':sprite').hex, template)
    meta.write_text(template, encoding='utf-8')

script = 'Assets/Scripts/Map/Logic/MapBossIntroductionState.cs'
meta = ROOT / (script + '.meta')
if not meta.exists():
    meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(NS, script).hex
        + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
        + '  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')

prefab = ROOT / 'Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab'
text = prefab.read_text(encoding='utf-8')
blocks = list(re.finditer(r'^--- !u!1001 &\d+\n.*?(?=^---|\Z)', text, re.M | re.S))
target = [block for block in blocks if 'value: Node_r1_boss\n' in block[0]]
assert len(target) == 1
block = target[0][0]
source_guid = 'bd83510b5b4f8344ba6563ee7e153e5a'


def override(file_id, property_name, value='', reference='{fileID: 0}'):
    global block
    record = (f'    - target: {{fileID: {file_id}, guid: {source_guid}, type: 3}}\n'
              f'      propertyPath: {property_name}\n      value:{" " + str(value) if value != "" else ""}\n'
              f'      objectReference: {reference}\n')
    pattern = (r'    - target: \{fileID: ' + str(file_id) + ', guid: ' + source_guid + r', type: 3\}\n'
               r'      propertyPath: ' + re.escape(property_name) + r'\n      value:[^\n]*\n      objectReference: [^\n]*\n')
    if re.search(pattern, block):
        block, count = re.subn(pattern, lambda _: record, block)
        assert count == 1
    else:
        block = block.replace('    m_RemovedComponents:', record + '    m_RemovedComponents:', 1)


for axis in ['x', 'y']:
    override(4376349905967442580, 'm_SizeDelta.' + axis, f'{128 * math.sqrt(3):.7f}')
override(7918741964881659582, 'm_Sprite', reference=f'{{fileID: 21300000, guid: {guid}, type: 3}}')
override(7918741964881659582, 'm_Type', '0')
override(7918741964881659582, 'm_PreserveAspect', '1')
text = text[:target[0].start()] + block + text[target[0].end():]
prefab.write_text(text, encoding='utf-8')
print('Installed Sloth shrine: 221.70 x 221.70 UI units, 3x the 128 x 128 ordinary-node area.')
