The production entry is `Assets/Scenes/Village.unity`.

The authored shop, inn confirmation, and blackout live in:

- `Assets/Prefabs/UI/VillageShopPanel.prefab`
- `Assets/Prefabs/UI/InnPanel.prefab`
- `Assets/Prefabs/UI/VillageRestTransition.prefab`

Weapon sentences and original artwork UV bounds are editable in the shop's
`VillageShopUI.offers`. Stock, currency, and capacity remain owned by `ShopRules`
and `ItemRules`. The inn spends the remaining units until next morning through
`WorldResourceRules.SpendTime(..., true)`, restores HP and lantern charges, and
replenishes daily stock. Cancel and full-resource checks do not spend time.

The blackout copies the actual map time dial, including its bottom-center anchors,
artwork, half-circle mesh, crescent, and fixed rim. It runs on unscaled time and
blocks village actions until the village background is ready and the veil clears.

Rebuild explicitly with **Tools → Game Flow → Build Village Shop and Inn**.
The builder rejects dirty open scenes; importing scripts does not author scenes.

Run the native checks in a separate, hidden Unity project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/VillageServiceChecks/RunUnityChecks.ps1
```

The runner writes `output/village-services/result.json`, the Unity log, and actual
1920 × 1080 screenshots. It checks purchase settlement, sold-out reopening,
currency and carrying limits, original artwork, icon geometry, pointer input,
confirmation/cancellation, stale and duplicate callbacks, the full blackout,
three departure periods, paused gameplay, native dial placement, morning background,
daily restocking, introduction restrictions, and existing equipment/tavern entry.

After a passing run, `python Tools/VillageServiceChecks/install.py` installs only
the generated service prefabs and village scene. It compares the tested runtime
sources with production, backs up prior assets, and verifies every serialized GUID.
