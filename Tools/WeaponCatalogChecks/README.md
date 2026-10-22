# Weapon catalogue regression

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/WeaponCatalogChecks/RunUnityChecks.ps1
dotnet run --project Tools/WeaponSkillChecks/WeaponSkillChecks.csproj
dotnet run --project Tools/MapChecks/MapChecks.csproj -- Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.json
```

Unity checks run against copied production scenes and prefabs under
`output/village-intro/weapon-catalog/UnityProject`, without rebuilding them.
They cover the eleven-weapon library, greatsword slash data and category, all
three starting buttons, all stocked shop cards, an actual greatsword purchase,
rejected dual-sword purchases, legacy save migration and the full backpack UI suite.
Test saves use unique paths inside the output directory.

The rule suites independently cover exact damage, slash versus pierce immunity,
skill status riders, repeated legacy migration, equipment and stale purchase input.
