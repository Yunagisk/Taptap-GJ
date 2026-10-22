"""Package native Unity captures, and refresh the attack clips in the existing preview."""
from pathlib import Path
import json
import re
import shutil
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / 'output/enemy-attack-motion-v4'
result = json.loads((HERE / 'unity-validation.json').read_text(encoding='utf-8'))
assert result['passed'], result


def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)


def parts(path):
    data, offset, output = path.read_bytes(), 8, []
    while offset < len(data):
        size = struct.unpack('>I', data[offset:offset + 4])[0]
        output.append((data[offset + 4:offset + 8], data[offset + 8:offset + 8 + size]))
        offset += size + 12
    return output


clips = {}
for name in ['Crow', 'Rat', 'Wraith', 'Sloth']:
    frames = sorted((HERE / name).glob('*.png'))
    clips[name] = [p.relative_to(HERE).as_posix() for p in frames]
    pngs = [parts(p) for p in frames]
    width, height = struct.unpack('>II', pngs[0][0][1][:8])
    output = bytearray(b'\x89PNG\r\n\x1a\n') + chunk(b'IHDR', pngs[0][0][1])
    output += chunk(b'acTL', struct.pack('>II', len(pngs), 0))
    sequence = 0
    for index, png in enumerate(pngs):
        output += chunk(b'fcTL', struct.pack('>IIIIIHHBB', sequence, width, height, 0, 0, 1, 30, 0, 0))
        sequence += 1
        payload = b''.join(value for kind, value in png if kind == b'IDAT')
        output += chunk(b'IDAT' if index == 0 else b'fdAT',
                        payload if index == 0 else struct.pack('>I', sequence) + payload)
        if index:
            sequence += 1
    output += chunk(b'IEND', b'')
    (HERE / f'{name}_attack_30fps.png').write_bytes(output)

(HERE / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<title>怪物攻击 · 位移调整</title><style>
body{margin:0;background:#151a22;color:#eef1f5;font:16px system-ui}main{max-width:980px;margin:auto;padding:24px}
h1{font-size:25px}p{color:#bdc6d4;line-height:1.7}button,select{padding:9px 14px;margin:4px;border:1px solid #64758b;background:#2d394c;color:white;border-radius:6px;cursor:pointer}
.active{background:#506780}img{display:block;width:640px;max-width:100%;margin:15px auto}input{width:100%}a{color:#bcd6f5}
</style><main><h1>怪物攻击 · 位移调整</h1><p>Unity 实际播放，30 FPS。缩短整体前冲，命中稍作停顿，再平稳收回。<br>腐鸦保留俯身啄击；瘟鼠短促探头；怨影以伸爪为主；懒惰重心稳定、以前爪发力。</p>
<nav id="names"></nav><div><button id="play">暂停</button><button id="reset">重播</button>
<select id="speed"><option value="1">正常速度</option><option value=".5">半速</option><option value=".25">四分之一速度</option></select>
<span id="status"></span></div><img id="stage" alt="怪物攻击的 Unity 实录"><input id="seek" type="range" min="0" value="0">
<p><a id="download" download>下载 30 FPS APNG</a> · <a href="../small-monsters-v1/index.html">瘟鼠与怨影全套动作</a></p>
<p>游戏内：打开 Assets/Scenes/BattleSimulator.unity，进入 Play，切换敌人后点击「防御」即可查看敌人攻击。</p></main><script>
const clips=__CLIPS__,names={Crow:'腐鸦',Rat:'瘟鼠',Wraith:'怨影',Sloth:'懒惰'},$=id=>document.getElementById(id);
let name='Crow',playing=true,time=0,speed=1,last=performance.now(),shown=-1;
for(const [key,label] of Object.entries(names)){const b=document.createElement('button');b.id=key;b.textContent=label;b.onclick=()=>choose(key);$('names').append(b)}
function draw(){const frames=clips[name],t=time%(frames.length/30+.5),f=Math.min(frames.length-1,Math.floor(t*30));if(f!==shown){$('stage').src=frames[f];shown=f}$('seek').value=f;$('status').textContent=names[name]+' · '+(f+1)+' / '+frames.length}
function choose(key){name=key;time=0;shown=-1;$('seek').max=clips[name].length-1;$('download').href=name+'_attack_30fps.png';for(const k of Object.keys(names))$(k).classList.toggle('active',k===name);draw()}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放'};$('reset').onclick=()=>choose(name);$('speed').onchange=e=>speed=Number(e.target.value);
$('seek').oninput=e=>{playing=false;$('play').textContent='播放';time=Number(e.target.value)/30;draw()};
Promise.all(Object.values(clips).flat().map(src=>{const i=new Image();i.src=src;return i.decode()})).then(()=>{choose(name);last=performance.now();requestAnimationFrame(tick)});
function tick(now){if(playing)time+=(now-last)/1000*speed;last=now;draw();requestAnimationFrame(tick)}
</script></html>'''.replace('__CLIPS__', json.dumps(clips)), encoding='utf-8')

old = ROOT / 'output/small-monsters-v1'
for name in ['Rat', 'Wraith', 'Sloth']:
    folder = old / name if name != 'Sloth' else ROOT / 'output/sloth-v1'
    destination = folder / 'attack'
    archive = folder / 'attack-before-motion-v4'
    # Retain the old captures once; never merge clip folders with different lengths.
    if not archive.exists():
        destination.rename(archive)
    if destination.exists():
        assert destination.resolve().is_relative_to(folder.resolve())
        shutil.rmtree(destination)
    shutil.copytree(HERE / name, destination)
    if name == 'Sloth':
        shutil.copy2(HERE / 'Sloth_attack_30fps.png', folder / 'Sloth_attack_30fps.png')
        page = folder / 'index.html'
        html = page.read_text(encoding='utf-8')
        match = re.search(r'const clips=(\{.*?\}),labels=', html)
        assert match
        combined = json.loads(match[1])
        combined['attack'] = ['attack/' + p.name for p in sorted(destination.glob('*.png'))]
        html = html[:match.start(1)] + json.dumps(combined) + html[match.end(1):]
        if '整体前冲已缩短' not in html:
            html = html.replace('</h1>', '</h1><p>攻击整体前冲已缩短，重心稳定、以前爪发力。<a href="../enemy-attack-motion-v4/index.html">查看四种怪物的新攻击</a></p>', 1)
        page.write_text(html, encoding='utf-8')
(HERE / 'README.txt').write_text('四种怪物攻击位移调整，Unity 原生 30 FPS 实录。\n'
    '打开 index.html 可切换怪物、逐帧、半速播放。\n'
    f'{len(result["checks"])} 项 Unity 播放检查通过：远近与左右目标、单次命中、平稳回位、受击与死亡打断。\n'
    '瘟鼠与怨影的旧攻击片段已在 small-monsters-v1 中归档并更新。\n', encoding='utf-8')
print({name: len(frames) for name, frames in clips.items()})
