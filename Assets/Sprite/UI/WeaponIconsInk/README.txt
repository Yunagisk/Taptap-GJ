武器 UI 墨绘图标

十二把图标均由 Assets/Sprite/Items/WeaponIllustrations/ 的彩色成品派生，使用内置 image_gen 做墨绘风格适配，保留每把武器的具体造型。
PNG 为 1024×1024 RGBA，原生生成图 1254×1254；仅作透明留白裁切、整体旋转与等比例缩放，没有拉伸武器比例。
普通金属与木柄保留少量淡彩，焰舌保留红热剑身，天火保留熔岩裂纹与金属柄。

使用位置：
  Assets/Prefabs/UI/EquipmentPanel.prefab
    InventoryLayoutView.weaponArt，绑定全部十二把；武器行模板使用 InventoryWeaponInk.mat。
  Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab
    MapPlayerInkHud.weaponIcons，绑定全部十二把；四个动态武器 Image 使用 HudWeaponInk.mat。

两处共用同一套墨绘母图。根目录十二张显示完整武器；Hud/ 内十二张用于小侧栏，其中长柄武器采用头部与上段握柄的特写构图，保留原比例；双剑采用紧凑交叉排列。
InventoryWeaponInk.mat 保留淡彩，HudWeaponInk.mat 降低普通材质色彩、提高墨色对比，并保留部分暖色。
UI/Weapon Ink Icon shader 支持 UI stencil、RectMask2D 裁切、透明度及现有水墨展开的顶点颜色，不需要为每帧生成新材质。
原彩色立绘、本体/火焰分层及旧四张 HUD 图标均保留。当前 UI 使用静态图标，角色持握动作与武器数值不在本次改动范围内。

Unity 工具菜单：
  Tools / Map UI / Install Weapon Ink Icons   显式重新配置图标、材质与两个 Prefab。
  Tools / Map UI / Validate Weapon Ink Icons  检查全部武器绑定、显示比例和展开引用。

本地制作记录（output/ 已由 .gitignore 排除）：
  output/imagegen/weapon-ui-ink/generation.json              完整提示词、输入原件及原生输出路径
  output/imagegen/weapon-ui-ink/Export-Icons.cs / .ps1        画布整理与透明 PNG 导出
  output/imagegen/weapon-ui-ink/export-report.json           尺寸与透明边界检查
  output/imagegen/weapon-ui-ink/previews/ink-icons-overview.png
  output/imagegen/weapon-ui-ink/previews/ui-presentation-preview.png（显示参数参考，非运行截图）

正式提交时包含本目录 PNG、材质、说明、清单、配套 .meta，以及编辑器工具、UI shader 和两个修改后的 Prefab。
