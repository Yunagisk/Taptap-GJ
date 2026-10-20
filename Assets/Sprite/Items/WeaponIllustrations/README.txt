武器成品立绘（2026-10-18 修线与材质调整）

本目录为十二把彩色武器的透明成品立绘。沿用原成品的造型、比例、灰钢、棕色柄、米白缠布、绿宝石与深色线稿风格。
本轮对轮廓毛边、零碎杂线和小毛刺做轻度修整，保留刻意设计的尖角、刃口缺口、链环空隙和装饰。
焰舌剑身改为红热金属，带橙黄高温亮部；天火锤头改为炭黑色，表面碎裂、裂隙中呈熔岩质感，整体仍为完整武器。
天火的长柄与底部多面握把随后改为深灰钢材质，带银色边缘反光；绿色尾端宝石和独立火焰层保留。

武器 ID 与整图：
sword         Sword.png          剑
greatsword    Greatsword.png      大剑
spear         Spear.png          长矛
axe           Axe.png            斧
sickle        Sickle.png         长柄镰刀
hammer        Hammer.png         锤
flail         Flail.png          链枷
torch         Torch.png          火把
poleaxe       Poleaxe.png        斧枪
flame_tongue  FlameTongue.png     焰舌
heaven_fire   HeavenFire.png      天火

Layers/ 内分别提供 FlameTongue、HeavenFire、Torch 的 Body 和 Flames。
叠合时先画 Flames，再画 Body；两层使用相同的位置、旋转、缩放与画布中心。
全部成品与图层为 2048×2048 RGBA，保留整张画布的配准，不可分别紧裁后直接叠合。
静态整图由对应两层叠合得到；火焰层供后续动画使用，当前尚未制作或接入循环帧及动画控制器。

生成与导出：
使用内置 image_gen 分别编辑每把武器/图层，并检查整图、分层叠合及深浅底边缘。
原生生成尺寸为 1254×1254，经等比例重采样、对齐原画布后导出 2048×2048，并非原生 2K 生成。
导出不额外绘制线条或修改生成的透明通道。现有资产 .meta / GUID 保留，新补入的原有五类成品另配 .meta。
原美术 ZIP、制作参考原件、原地图 HUD 黑白图标保留。此目录尚未绑定角色动画或修改武器数值。

本轮制作记录：
output/imagegen/weapon-refinement/
  generation.json                  完整逐图提示词与输入/输出路径
  inputs/                          修改前的冻结副本
  sources/                         本轮原生生成图
  Export-Refined.cs / .ps1          画布配准与导出脚本
  export-report.json / validation.json
  previews/weapons-overview.png     十二把成品总览
  previews/material-before-after.png  焰舌、天火材质前后对比
  previews/flame-layers.png         三把火焰武器的分层叠合
上轮补全记录保留于 output/imagegen/weapon-completion/。
天火金属柄的最新原图、提示词、前后对比和导出脚本位于 output/imagegen/heaven-fire-metal-handle/；重新制作时应在旧版导出之后运行该目录 Export.ps1。

output/ 是本地制作与预览记录，已由 .gitignore 排除；应提交本目录的成品、说明、清单和配套 .meta。
