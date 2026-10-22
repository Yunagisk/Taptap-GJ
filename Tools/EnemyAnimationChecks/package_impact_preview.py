"""Package exact Unity-rendered attack frames and refresh existing review galleries."""
from pathlib import Path
import json
import re
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'output/combat-impact-v1'
result = json.loads((OUTPUT / 'unity-validation.json').read_text(encoding='utf-8'))
assert result['passed'], result['error']

def chunk(kind, payload):
    return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)

def parts(path):
    data = path.read_bytes()
    offset, output = 8, []
    while offset < len(data):
        length = struct.unpack('>I', data[offset:offset + 4])[0]
        output.append((data[offset + 4:offset + 8], data[offset + 8:offset + 8 + length]))
        offset += length + 12
    return output

clips = {}
for name in ['Crow', 'Rat', 'Wraith', 'Sloth']:
    frames = sorted((OUTPUT / name / 'attack').glob('*.png'))
    assert frames, name
    clips[name] = [p.relative_to(OUTPUT).as_posix() for p in frames]
    pngs = [parts(p) for p in frames]
    width, height = struct.unpack('>II', pngs[0][0][1][:8])
    animation = bytearray(b'\x89PNG\r\n\x1a\n')
    animation += chunk(b'IHDR', pngs[0][0][1])
    animation += chunk(b'acTL', struct.pack('>II', len(pngs), 0))
    sequence = 0
    for index, png in enumerate(pngs):
        animation += chunk(b'fcTL', struct.pack('>IIIIIHHBB', sequence, width, height, 0, 0, 1, 30, 0, 0))
        sequence += 1
        payload = b''.join(value for kind, value in png if kind == b'IDAT')
        if index == 0:
            animation += chunk(b'IDAT', payload)
        else:
            animation += chunk(b'fdAT', struct.pack('>I', sequence) + payload)
            sequence += 1
    animation += chunk(b'IEND', b'')
    (OUTPUT / f'{name}_attack_30fps.png').write_bytes(animation)

gallery = ROOT / 'output/small-monsters-v1/index.html'
html = gallery.read_text(encoding='utf-8')
match = re.search(r'const clips=(\{.*?\}),labels=', html, re.S)
existing = json.loads(match[1])
for name in ['Rat', 'Wraith', 'Sloth']:
    if name in existing:
        existing[name]['attack'] = ['../combat-impact-v1/' + frame for frame in clips[name]]
html = html[:match.start(1)] + json.dumps(existing, ensure_ascii=False) + html[match.end(1):]
html = re.sub(r"a.href=\(species==='Sloth'\?'../sloth-v1/':''\)\+species\+'_'\+k\+'_30fps.png'",
              "a.href=(k==='attack'?'../combat-impact-v1/':species==='Sloth'?'../sloth-v1/':'')+species+'_'+k+'_30fps.png'", html)
html = html.replace("a.href=species+'_'+k+'_30fps.png'",
                    "a.href=(k==='attack'?'../combat-impact-v1/':'')+species+'_'+k+'_30fps.png'")
html = html.replace('攻击镜头展示实际突进距离', '攻击只向前短冲 0.3 单位，命中时轻微震屏约 0.16 秒')
html = html.replace('抬爪蓄力挥击', '短冲抬爪挥击').replace('蓄力扑咬', '短冲扑咬').replace('探爪攻击', '短冲探爪')
gallery.write_text(html, encoding='utf-8')

# Keep the standalone Sloth gallery consistent when it exists.
sloth_gallery = ROOT / 'output/sloth-v1/index.html'
if sloth_gallery.exists():
    html = sloth_gallery.read_text(encoding='utf-8')
    match = re.search(r'const clips=(\{.*?\}),', html, re.S)
    if match:
        existing = json.loads(match[1])
        existing['attack'] = ['../combat-impact-v1/' + frame for frame in clips['Sloth']]
        html = html[:match.start(1)] + json.dumps(existing, ensure_ascii=False) + html[match.end(1):]
        html = html.replace("a.href='Sloth_'+key+'_30fps.png'", "a.href=(key==='attack'?'../combat-impact-v1/':'')+'Sloth_'+key+'_30fps.png'")
        sloth_gallery.write_text(html, encoding='utf-8')
print(json.dumps({'checks': len(result['checks']), 'frames': {key: len(value) for key, value in clips.items()}}, ensure_ascii=False))
