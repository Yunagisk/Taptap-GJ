# Small-monster damage tuning

Installed values from 2026-10-21:

| Enemy | Basic | Signature / charged follow-up | HP |
| --- | ---: | ---: | ---: |
| Rat | 4 | 8 after charging | 36 |
| Crow | 3 | 2 per hit, 2 hits | 36 |
| Wraith | 4 | 10 after charging | 44 |

Damage lives in `Assets/Data/EnemyAttacks/SmallMonsters`. The species assets,
overworld prefabs and simulator prefabs use those shared attacks. Weights,
status effects, weaknesses, HP and the one-charge-per-round limit are retained.
`install.py` generates these values and a separate two-damage
`GuideWraithBasic.asset` for the mandatory teaching encounter.

Run the asset-driven model:

```powershell
python Tools/EncounterBalanceSimulation/simulate.py --trials 1000 --weapons sword axe --policies focus reactive --quiet --output output/monster-buff-20261021/after-simulation.json
```

Every model trial begins at full HP with one resolve and uses one weapon,
without healing or switching. Its fixed policies estimate encounter pressure;
they do not estimate human win rates or continuous-run resource depletion.

Run actual Unity damage, teaching, targeting, animation and continuous-route checks:

```powershell
python Tools/GlobalRegressionChecks/stage.py --run-name monster-buff-20261021
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GlobalRegressionChecks/RunNative.ps1 -RunName monster-buff-20261021 -Only save-load,intro,encounter,targeting,attack-motion,battle-resize,full-run
```

The encounter validator executes every installed attack with and without real
player defense, verifies positive chip damage on damaging moves, and checks that
the teaching action remains independent. The full route uses actual game actions,
including the inn and camp, without editing HP, clocks or completion flags.
