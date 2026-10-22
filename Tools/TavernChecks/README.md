# Tavern checks

Run from the repository root with the project's Unity editor installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/TavernChecks/RunUnityChecks.ps1
```

The runner copies production scripts and the installed scene/prefab into an isolated
project under `output/village-intro/tavern-checks/UnityProject`. It never rebuilds
production assets, and redirects dawn saves to the test output directory.

Native pointer events exercise all 27 choices across all 13 timed events, completed
and expired detail cleanup, keyboard focus, repeated input, reopening, dawn saves,
reward limits, free previews, waiting, main and side conversations, cancellation,
and the current chapter investigation. The output includes `result.json`,
`unity.log`, and before/after images of the reported scapegoat event.

The separate `Tools/MapChecks` rule suite verifies generation, persistence, all
choice values and rewards, one-use bonuses, invalid callbacks and map integration.
