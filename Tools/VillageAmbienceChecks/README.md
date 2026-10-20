# Village ambience

`Assets/Scenes/Village.unity` uses the three original images in
`Assets/Sprite/UI/VillageAmbience/`. The files are exact copies of the supplied JPGs:

| Game clock | Source | Imported asset |
| --- | --- | --- |
| Morning (`world.time % 3 == 0`) | 白天.jpg | VillageMorning.jpg |
| Afternoon (`world.time % 3 == 1`) | 下午.jpg | VillageAfternoon.jpg |
| Night (`world.time % 3 == 2`) | 晚上.jpg | VillageNight.jpg |

Select `Canvas/幕布` and edit `VillageAmbienceView` to toggle smoke, fog, window
lights, leaves and birds independently. During Play mode, its
inspector has Morning / Afternoon / Night / Follow game preview buttons. Preview
does not change gameplay time. Background changes crossfade over 1.25 seconds.

Smoke starts at three chimney mouths. Fog stays mainly around the village
boundary and is strongest at night. Occasional birds and leaves use procedural
geometry. There are no walking villagers or ambient audio sources.
Night windows receive a subtle independently varying warm glaze. The painted
window light remains part of the original background; this version does not turn
windows completely off. Trees, doors and awnings remain part of the original art.

Effects use a 1920 x 1080 illustration coordinate system and follow the actual
displayed image, including aspect-preserving letterboxing. They do not receive
pointer input and use unscaled time so menus can pause gameplay without freezing
the ambience. Cloud texture and pooled geometry are created locally at runtime,
with no extra plugin or network service. The runtime texture is released on
destruction.

Run the native checks with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/VillageAmbienceChecks/RunUnityChecks.ps1
```

The runner launches Unity hidden in an isolated project under
`output/village-ambience/UnityProject`. It leaves the working editor alone and
checks the real village scene, imports, game clock, fades, paused animation,
rendered geometry, effect switches, aspect ratio, preview isolation, absence of audio,
and inn pointer/action behavior. Results and three rendered PNGs are written to
`output/village-ambience/`.
