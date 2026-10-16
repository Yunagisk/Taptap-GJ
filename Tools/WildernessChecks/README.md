# Wilderness checks

Run from the Unity project root:

```powershell
dotnet run --project Tools/WildernessChecks/WildernessChecks.csproj
```

This standalone .NET 10 harness links the actual pure production rules. It does not load a Unity scene, instantiate GameRoot, alter the production map, or write gameplay saves. All resource transactions use fresh GameState instances and copies of the real map layout. The catalog and layouts are copied into the test output directory by the project. The production map assets now live in `Assets/Data/EnemyAttacks/Maps`; the catalog remains in `Assets/Data/Wilderness`.

The expected table is independently transcribed from the supplied design images. Checks cover all 10 templates, 20 choices, 27 branches, every integer probability boundary, and 10,000 fixed seeds for weighted sampling without replacement. First- and conditional second-draw distributions verify that whole templates are removed after selection. All option and encounter results, JSON continuity, one-time prompts, ignored duplicate callbacks, resource bounds, text-only rewards, real camp recovery, and repairable-road constraints are exercised.

System.Text.Json needs a check-only ItemStack converter because the existing production constructor is `(id,n)` while serialized fields are `itemId,count`. This does not change Unity serialization. Native Unity JsonUtility and scene/prefab rendering require separate Unity validation.

To check a catalog/layout pair explicitly, pass their paths after the project argument:

```powershell
dotnet run --project Tools/WildernessChecks/WildernessChecks.csproj -- Assets/Data/Wilderness/WildernessEvents.json Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.json
```


Formal-map integration checks also run against both original 28-node JSON layouts. They retain six Event positions, initialize and persist one weighted no-replacement draw, and traverse actual MapProgression paths for every choice/result branch. Coverage includes movement/choice/encounter time, global resources, stale requests, duplicate settlement, whole-GameState JSON continuation, abandoned visits, legacy migration, unchanged battle/camp/boss behavior, and checkpoint revival. Bridge checks validate complete graphs before accepting candidates, exercise real navigation and visibility after repair/reload, and reject forged road overrides. The centered map excludes its unsafe region-three shortcut because it would bypass required regional battles; the alternative map retains three safe candidates.
