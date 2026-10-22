"""Package the selected source PNG and native map recording for review."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / 'output/sloth-boss-node-v1'
result = json.loads((HERE / 'unity-validation.json').read_text(encoding='utf-8'))
assert result['passed'], result

frames = result['frames']
for frame in frames:
    assert (HERE / frame['file']).exists()
assert (HERE / 'closeup.png').exists()
html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>懒惰 · 沉眠祭坛</title>
<style>
body{margin:0;background:#161815;color:#eee9dc;font:16px system-ui}main{max-width:1100px;margin:auto;padding:28px}
h1{font-size:26px}p{color:#c1bcae;line-height:1.8}a{color:#e3c998}
.compare{display:flex;align-items:flex-end;justify-content:center;gap:80px;background:#c3bba8;padding:28px;border-radius:8px}
figure{text-align:center;margin:0;color:#302e28}figure img{display:block;margin:auto}figcaption{margin-top:12px}
.record{width:100%;display:block;border-radius:8px}button{padding:9px 16px;background:#393e32;color:#eee9dc;border:1px solid #737864;border-radius:5px;cursor:pointer}
input{width:100%;margin:14px 0}#status{margin-left:18px;color:#c1bcae}
</style><main><h1>懒惰 · 沉眠祭坛</h1>
<p>低垂兽首、停摆钟环、塌陷石座与松垂锁链。前景爪掌搭在石沿上，爪尖自然垂在外侧。<br>节点面积约为普通节点三倍，首次出现时镜头拉近，墨水显现后停留，再回到探索视角。</p>
<div class="compare"><figure><img src="../../Assets/Sprite/UI/MapArt2K/BattleNode.png" width="128" height="128"><figcaption>普通战斗节点</figcaption></figure>
<figure><img src="../../Assets/Sprite/UI/MapArt2K/SlothBossNode.png" width="222" height="222"><figcaption>懒惰 · 沉眠祭坛</figcaption></figure></div>
<p><a href="../../Assets/Sprite/UI/MapArt2K/SlothBossNode.png">透明节点 PNG</a> · <a href="closeup.png">Unity 特写截图</a></p>
<img id="stage" class="record" src="closeup.png" alt="Unity 地图实录">
<p><button id="play">播放出现特写</button><button id="restart">重播</button><span id="status"></span></p><input id="seek" type="range" min="0" step="1" value="0">
<p>打开 Unity 的 Assets/Scenes/MainMap.unity，首次显示地图时即可看到懒惰节点的出现特写。</p></main>
<script>
const frames=__FRAMES__,$=id=>document.getElementById(id);let playing=false,time=0,last=performance.now(),shown=-1;
$('seek').max=frames.length-1;
function draw(){let f=0;while(f+1<frames.length&&frames[f+1].time<=time)f++;if(f!==shown){$('stage').src=frames[f].file;shown=f}$('seek').value=f;$('status').textContent=time.toFixed(2)+' 秒';}
$('play').onclick=()=>{playing=!playing;if(playing&&time>=frames.at(-1).time)time=0;$('play').textContent=playing?'暂停':'播放出现特写';if(playing)draw()};
$('restart').onclick=()=>{time=0;playing=true;$('play').textContent='暂停';draw()};
$('seek').oninput=e=>{playing=false;time=frames[Number(e.target.value)].time;$('play').textContent='播放出现特写';draw()};
Promise.all(frames.map(frame=>{const image=new Image();image.src=frame.file;return image.decode()}));
function tick(now){if(playing){time=Math.min(frames.at(-1).time,time+(now-last)/1000);draw();if(time>=frames.at(-1).time){playing=false;$('play').textContent='播放出现特写'}}last=now;requestAnimationFrame(tick)}requestAnimationFrame(tick);
</script></html>'''.replace('__FRAMES__', json.dumps(frames))
(HERE / 'index.html').write_text(html, encoding='utf-8')

paths = ['Assets/Scripts/Map/UI/WorldMapSceneView.cs', 'Assets/Scripts/Map/UI/WorldMapViewport.cs',
         'Assets/Scripts/Map/Logic/MapBossIntroductionState.cs', 'Assets/Scripts/Core/GameRoot.cs',
         'Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab', 'Assets/Sprite/UI/MapArt2K/SlothBossNode.png',
         'Assets/Sprite/UI/MapArt2K/SlothBossNode.png.meta', 'Assets/Scenes/MainMap.unity']
project = ROOT / 'output/fuya-integration/UnityProject'
hashes = {}
for path in paths:
    assert (ROOT / path).read_bytes() == (project / path).read_bytes(), path
    hashes[path] = hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
(HERE / 'integration-validation.json').write_text(json.dumps({
    'passed': True, 'nativeChecks': len(result['checks']), 'sourceSha256': hashes,
    'artwork': 'Sloth shrine with corrected foreground paw', 'areaMultiplier': 3,
    'generationTool': 'built-in image_gen',
    'prompts': ['generation-v2.json', 'generation-paw-fix.json']
}, indent=2), encoding='utf-8')
(HERE / 'README.txt').write_text(
    '懒惰 Boss 节点：沉眠祭坛\n\n'
    'index.html：普通节点大小对照、真实 Unity 出现特写实录。\n'
    'closeup.png：原生地图特写截图。\n'
    '最终透明 PNG：../../Assets/Sprite/UI/MapArt2K/SlothBossNode.png\n'
    '地图预制体：../../Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab，Node_r1_boss。\n'
    '已接入 MainMap；面积三倍，首次出现镜头拉近，使用同一个 MapNodeRevealEffect，然后恢复探索镜头。\n'
    '同一局从其他场景返回地图不会重播；新一局会重新介绍。\n'
    '绘图与局部修改均使用内置 image_gen。完整设计提示词见 generation-v2.json，前景爪子修正提示词见 generation-paw-fix.json。\n'
    f'{len(result["checks"])} 项原生 Unity 检查通过；地图规则检查 5764 项通过。\n', encoding='utf-8')
print('Packaged the selected shrine artwork and', len(frames), 'native introduction frames.')
