# Map checks

Run from the Unity project root with .NET 10:

```powershell
dotnet run --project Tools/MapChecks/MapChecks.csproj -- Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.json
dotnet run --project Tools/MapChecks/MapChecks.csproj -- Assets/Data/EnemyAttacks/Maps/ThreeSinWhitebox.json
```

Without arguments, the project uses the copied ThreeSinWhitebox.json. Both map layouts and WorldMapTemplate assets now live in Assets/Data/EnemyAttacks/Maps; their Unity GUIDs are unchanged.

The harness links the production rules and covers navigation, time and sin, revival, story, equipment, shop, village events, and map reveal presentation data. Dynamic repaired-road checks execute a real wilderness repair choice and verify that the shortcut is revealed once, then excluded from later completion reveals. Presentation data must not change gameplay state or charge extra time.

Run the broader wilderness generation and integration suite separately:

```powershell
dotnet run --project Tools/WildernessChecks/WildernessChecks.csproj
```
