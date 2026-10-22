Run `powershell -ExecutionPolicy Bypass -File Tools/DialogueUiChecks/RunUnityChecks.ps1` from the repository root.

The runner copies production dialogue dependencies into an isolated offline Unity project under `output/dialogue-ui`, rebuilds only that project's dialogue prefab, and verifies pagination, delayed choices, keyboard submit/navigation, pointer raycasts, long response scrolling, cancellation, and compatibility with the actual legacy Intro scene. Native PNG previews cover 16:9, 4:3, ultrawide, narration, and system tips.

To update the production prefab in the Editor, use **Tools → Game Flow → Build RPG Dialogue Layout**. Legacy UI copies also receive the layout at runtime. Nodes accept a transparent portrait in `avatar`; `lineKind` distinguishes dialogue, narration, and system tips. Existing speaker values `旁白` and `系统提示` remain supported.

This change supplies the UI layout. It does not import the Word script or change story progression.
