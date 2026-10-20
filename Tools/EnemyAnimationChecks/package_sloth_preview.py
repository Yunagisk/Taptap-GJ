"""Package unchanged native Unity renders as 30 FPS preview clips and APNG downloads."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / 'output/sloth-v1'
result = json.loads((HERE / 'unity-validation.json').read_text(encoding='utf-8'))
assert result['passed'], result


def chunk(kind, payload):
    return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)


def parts(path):
    raw = path.read_bytes()
    offset, chunks = 8, []
    while offset < len(raw):
        size = struct.unpack('>I', raw[offset:offset + 4])[0]
        chunks.append((raw[offset + 4:offset + 8], raw[offset + 8:offset + 8 + size]))
        offset += size + 12
    return chunks


clips = {}
for state in ['idle', 'attack', 'hurt', 'death']:
    frames = sorted((HERE / state).glob('*.png'))
    assert frames, state
    clips[state] = [path.relative_to(HERE).as_posix() for path in frames]
    pngs = [parts(path) for path in frames]
    width, height = struct.unpack('>II', pngs[0][0][1][:8])
    apng = bytearray(b'\x89PNG\r\n\x1a\n')
    apng += chunk(b'IHDR', pngs[0][0][1])
    apng += chunk(b'acTL', struct.pack('>II', len(pngs), 0))
    sequence = 0
    for index, png in enumerate(pngs):
        apng += chunk(b'fcTL', struct.pack('>IIIIIHHBB', sequence, width, height, 0, 0, 1, 30, 0, 0))
        sequence += 1
        data = b''.join(value for kind, value in png if kind == b'IDAT')
        if index == 0:
            apng += chunk(b'IDAT', data)
        else:
            apng += chunk(b'fdAT', struct.pack('>I', sequence) + data)
            sequence += 1
    apng += chunk(b'IEND', b'')
    (HERE / f'Sloth_{state}_30fps.png').write_bytes(apng)

html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>懒惰 · 30 FPS 全套动作</title>
<style>body{margin:0;background:#151a22;color:#eef1f5;font:16px system-ui}main{max-width:1040px;margin:auto;padding:24px}h1{font-size:25px;margin:0 0 10px}p,small{color:#b8c4d3;line-height:1.65}button,select{padding:9px 14px;margin:4px;border:1px solid #64758b;background:#2d394c;color:white;border-radius:6px;cursor:pointer}.active{border-color:#c3d7ec;background:#445b76}.row{display:flex;align-items:center;flex-wrap:wrap;gap:6px}#status{margin-left:auto;font-variant-numeric:tabular-nums}#stage{display:block;max-width:100%;width:auto;height:520px;object-fit:contain;margin:14px auto;background:#1f2633}input{width:100%}a{color:#abccee}#downloads{display:flex;gap:18px;flex-wrap:wrap}</style>
<main><h1>懒惰 · 30 FPS 全套动作</h1><p>沉重呼吸、抬爪蓄力与挥击、受击后缩、倒伏及黑粉消散。与瘟鼠、怨影使用相同的原画网格动画方式，保留原画颜色。<br>待机一轮 144 个姿态，攻击 36 帧，受击 18 帧，死亡倒伏 42 帧。下面是 Unity 实际渲染，非重新绘制的逐帧素材。</p>
<div class="row" id="states"></div><div class="row"><button id="play">暂停</button><button id="restart">重播</button><select id="speed"><option value="1">正常速度</option><option value=".5">半速</option><option value=".25">四分之一速度</option></select><span id="status">正在加载</span></div>
<img id="stage" alt="懒惰 Unity 动画实录"><input id="scrub" type="range" min="0" value="0"><div id="downloads"></div><p><a href="../small-monsters-v1/index.html">对照瘟鼠与怨影</a></p><p><small>Unity 预览：Assets/Scenes/SlothAnimationPreview.unity。TestScene 与 BattleSimulator 的懒惰均已接入。</small></p></main>
<script>
const clips=__CLIPS__,labels={idle:'待机',attack:'攻击',hurt:'受击',death:'死亡 + 黑粉消散'},$=id=>document.getElementById(id);let state='idle',playing=true,time=0,last=performance.now(),speed=1,shown=-1,ready=false;
for(const [key,label]of Object.entries(labels)){const b=document.createElement('button');b.id=key;b.textContent=label;b.onclick=()=>choose(key);$('states').append(b);const a=document.createElement('a');a.href='Sloth_'+key+'_30fps.png';a.download='';a.textContent=label+' APNG';$('downloads').append(a)}
function draw(){if(!ready)return;const frames=clips[state],hold=state==='death'?.6:state==='idle'?0:.4,t=time%(frames.length/30+hold),f=Math.min(frames.length-1,Math.floor(t*30));if(f!==shown){$('stage').src=frames[f];shown=f}$('scrub').value=f;$('status').textContent=labels[state]+' · '+(f+1)+' / '+frames.length;window.slothPreview={state,frame:f,playing}}
function choose(next){state=next;time=0;shown=-1;playing=true;$('play').textContent='暂停';$('scrub').max=clips[state].length-1;for(const k of Object.keys(labels))$(k).classList.toggle('active',k===state);draw()}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放'};$('restart').onclick=()=>choose(state);$('speed').onchange=e=>speed=Number(e.target.value);$('scrub').oninput=e=>{playing=false;$('play').textContent='播放';time=Number(e.target.value)/30;draw()};
Promise.all(Object.values(clips).flat().map(url=>{const i=new Image();i.src=url;return i.decode()})).then(()=>{ready=true;choose(state)});function tick(now){if(ready&&playing)time+=(now-last)/1000*speed;last=now;draw();requestAnimationFrame(tick)}requestAnimationFrame(tick);
</script></html>'''.replace('__CLIPS__', json.dumps(clips))
(HERE / 'index.html').write_text(html, encoding='utf-8')

# Keep the user's already-open small-monster preview useful for side-by-side review.
catalog = ROOT / 'output/small-monsters-v1/index.html'
text = catalog.read_text(encoding='utf-8')
match = re.search(r'const clips=(\{.*?\}),labels=', text)
if match:
    combined = json.loads(match[1])
    combined['Sloth'] = {state: ['../sloth-v1/' + frame for frame in frames] for state, frames in clips.items()}
    text = text[:match.start(1)] + json.dumps(combined) + text[match.end(1):]
    text = text.replace("names={Rat:'瘟鼠',Wraith:'怨影'}", "names={Rat:'瘟鼠',Wraith:'怨影',Sloth:'懒惰'}")
    text = text.replace('瘟鼠与怨影 · 全套动作', '小怪与懒惰 · 全套动作')
    text = text.replace("a.href=species+'_'+k+'_30fps.png'", "a.href=(species==='Sloth'?'../sloth-v1/':'')+species+'_'+k+'_30fps.png'")
    if '懒惰：沉重呼吸与低头' not in text:
        text = text.replace(":'怨影：悬浮与衣摆起伏", ":species==='Sloth'?'懒惰：沉重呼吸与低头 → 抬爪蓄力挥击 → 受击后缩 → 倒伏 → 黑粉消散。':'怨影：悬浮与衣摆起伏")
    catalog.write_text(text, encoding='utf-8')

project = HERE / 'UnityProject'
for relative in ['Assets/Scenes/SlothAnimationPreview.unity', 'Assets/Scenes/SlothAnimationPreview.unity.meta']:
    shutil.copy2(project / relative, ROOT / relative)
(HERE / 'README.txt').write_text('懒惰动画 · 30 FPS\n\nindex.html：Unity 实录，可切换动作、暂停、拖帧与慢放。\nSloth_*_30fps.png：无损 APNG，每帧 1/30 秒。\n待机144姿态/4.8秒；攻击36帧/1.2秒；受击18帧/0.6秒；死亡42帧/1.4秒，随后停留0.15秒、黑粉消散1秒。\n沿用原画网格变形，保留原画颜色；并非新增逐帧手绘。\n\nUnity 场景：Assets/Scenes/SlothAnimationPreview.unity\nTestScene、BattleSimulator/Sloth、Enemies/Sloth 均绑定新动画。\n原始绘画、旧序列和战斗数值保留。\n', encoding='utf-8')
manifest = {'passed': True, 'unityChecks': len(result['checks']), 'clips': {key: len(value) for key, value in clips.items()},
            'sourceSha256': hashlib.sha256((ROOT / 'Assets/Sprite/Enemies/懒惰.png').read_bytes()).hexdigest()}
(HERE / 'integration-validation.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(manifest)
