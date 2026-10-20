Run from the project root:

```powershell
dotnet run --project Tools/MapChecks/MapChecks.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/InventoryUiChecks/RunUnityChecks.ps1
```

The pure rules checks cover category migration, replacement, ownership, and persistent unread notifications. The native checks instantiate the production prefabs, exercise category selection and scrolling, verify numeric enemy intents and separate status riders, and render screenshots. Unity runs hidden in an isolated project under `output/inventory-category`; the open editor and its scenes are not modified.
