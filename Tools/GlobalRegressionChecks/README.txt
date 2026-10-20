Run from the repository root with Unity 2022.3.62f3c1 installed:

python Tools/GlobalRegressionChecks/stage.py
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GlobalRegressionChecks/RunNative.ps1

To keep evidence for a later change separate, use the same run name for both:
python Tools/GlobalRegressionChecks/stage.py --run-name monster-buff-20261021
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GlobalRegressionChecks/RunNative.ps1 -RunName monster-buff-20261021 -Only intro,encounter,targeting,attack-motion,battle-resize,full-run

Run a subset after changing a related area:
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GlobalRegressionChecks/RunNative.ps1 -Only inventory,global-visual,battle-resize

The stage is isolated under output/village-intro/global-fixes-20261021/UnityProject.
No validator reauthors the installed game scenes or prefabs before testing them.
Only GameRoot's default save path is redirected in the staged copy. Each native
process receives a unique path in its output directory, so the player's real slot
cannot be read or overwritten and suites do not share saved-game state.
GlobalVisualAudit supplies appeared-event fixtures for layout coverage; deterministic
generation and reward settlement are also tested independently in Tools/MapChecks.
BattleResizeAudit supplies ordinary progression and boss fixtures for composition
coverage. FullRunPlayChecks and FullRouteDriver perform the full route using real
UI/progression/combat actions, without editing health, clocks, or cleared nodes.

Rule-suite logs are collected separately under output/global-fixes-20261021/.
python Tools/GlobalRegressionChecks/content_audit.py
python Tools/GlobalRegressionChecks/summarize.py

The summary checks all 23 native suite statuses, rule logs, staged source hashes,
content references, visible text candidates, and the completed route record.

When reusing the stage, deleted production assets are removed from its Assets
folder before import. This prevents retired weapons from surviving in the cache
and being counted as available game content.
