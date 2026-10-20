"""Standalone Monte Carlo model of the installed small-monster encounters.

This does not modify or execute Unity gameplay. Player weapons/skills are read
from repository assets, as are the installed enemy damage, weights, weaknesses
and HP. Additional HP profiles allow controlled design experiments.
Only standard Python 3.12 libraries are required.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import re
import statistics
from dataclasses import asdict, dataclass, replace
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
KINDS = ("rat", "crow", "wraith")
LABELS = {"rat": "老鼠", "crow": "腐鸦", "wraith": "怨影"}
WEIGHTS = {"rat": (45, 25, 20, 10), "crow": (40, 25, 20, 15),
           "wraith": (35, 30, 20, 15)}
NORMAL_DAMAGE = {"rat": 4, "crow": 3, "wraith": 4}
HEAVY_DAMAGE = {"rat": 8, "wraith": 10}
STATUS_DAMAGE = {"rat": 2, "crow": 0, "wraith": 0}
STATUS_STACKS = {"rat": 2, "crow": 1, "wraith": 1}
SIGNATURE_DAMAGE = {"crow": 2}
LOW_HEALTH_GUARD_WEIGHT = {"rat": 30, "crow": 0, "wraith": 0}
# weak, normal, resistant, immune match DamageReaction enum ordering.
RELATIONS = {"rat": (0, 1, 0, 1), "crow": (2, 0, 1, 1),
             "wraith": (1, 1, 2, 0)}
BREAK_THRESHOLDS = {"rat": 2, "crow": 2, "wraith": 2}
HP_PROFILES = {
    "installed": {"rat": 18, "crow": 18, "wraith": 24},
    "uniform50": {"rat": 50, "crow": 50, "wraith": 50},
    "asset_mix": {"rat": 50, "crow": 120, "wraith": 50},
    "candidate24": {"rat": 24, "crow": 24, "wraith": 30},
    "candidate18": {"rat": 18, "crow": 18, "wraith": 24},
}


def load_installed_balance() -> None:
    """Read the shared production assets; fail if their shape needs new modeling."""
    folder = ROOT / "Assets/Data/EnemyAttacks/SmallMonsters"
    action_paths = {}
    for path in folder.glob("*.asset"):
        guid = re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text("utf-8-sig"), re.M)[1]
        action_paths[guid] = path
    for name in ("Rat", "Crow", "Wraith"):
        kind = name.lower()
        raw = (ROOT / f"Assets/Data/SmallMonsters/{name}.asset").read_text("utf-8-sig")
        def number(text: str, field: str) -> int:
            return int(re.search(rf"^  {field}: (\d+)$", text, re.M)[1])
        HP_PROFILES["installed"][kind] = number(raw, "maxHp")
        LOW_HEALTH_GUARD_WEIGHT[kind] = number(raw, "lowHealthGuardWeight")
        parts = raw.split("  actions:\n", 1)[1].split("  resistances:", 1)[0]
        actions = [action_paths[guid].read_text("utf-8-sig") for guid in re.findall(r"guid: (\w+)", parts)]
        assert len(actions) == 4 and number(raw, "breakThreshold") == 2
        WEIGHTS[kind] = tuple(number(action, "weight") for action in actions)
        NORMAL_DAMAGE[kind] = number(actions[0], "damage")
        assert number(actions[0], "attackCount") == 1
        STATUS_DAMAGE[kind] = number(actions[1], "damage")
        STATUS_STACKS[kind] = number(actions[1], "statusStacks")
        assert number(actions[1], "statusOnHit") == {"rat": 3, "crow": 5, "wraith": 6}[kind]
        assert number(actions[1], "applyStatus") == 1
        assert number(actions[3], "kind") == 2
        if kind != "crow":
            follow = re.search(r"^  followUp: .*guid: (\w+)", actions[2], re.M)[1]
            HEAVY_DAMAGE[kind] = number(action_paths[follow].read_text("utf-8-sig"), "damage")
        else:
            SIGNATURE_DAMAGE[kind] = number(actions[2], "damage")
            assert number(actions[2], "attackCount") == 2
        relations = [1, 1, 1, 1]
        for type_, reaction in re.findall(r"  - damageType: (\d+)\n    damageReaction: (\d+)", raw):
            relations[int(type_)] = int(reaction)
        RELATIONS[kind] = tuple(relations)
ENCOUNTERS = (
    ("rat", "rat"), ("rat", "crow"), ("rat", "wraith"),
    ("crow", "crow"), ("crow", "wraith"), ("rat", "rat", "rat"),
    ("rat", "rat", "crow"), ("rat", "rat", "wraith"),
    ("rat", "crow", "wraith"),
)


@dataclass(frozen=True)
class Weapon:
    id: str
    name: str
    parts: tuple[tuple[int, int], ...]
    hits: int
    cost: int
    stages: tuple[tuple[int, int, tuple[tuple[int, int], ...]], ...]
    ignore_resistance: bool
    # Current four starter skills have no rider except hammer's sunder.
    sunder: int


def read_weapon(number: int) -> Weapon:
    raw = (ROOT / f"Assets/Data/Weapons/KBW{number:02}.asset").read_text("utf-8")
    skill = (ROOT / f"Assets/Data/Skills/KBS{number:02}.asset").read_text("utf-8")

    def value(text: str, key: str) -> str:
        match = re.search(rf"^  {key}: (.*)$", text, re.M)
        if not match:
            raise ValueError(f"Missing asset field {key}")
        return match[1]

    def parts(text: str) -> tuple[tuple[int, int], ...]:
        return tuple((int(t), int(d)) for t, d in re.findall(
            r"- damageType: (\d+)\n\s+damage: (\d+)", text))

    stage_text = skill.split("  stages:\n", 1)[1].split("  statusEffects:", 1)[0]
    stages = []
    for match in re.finditer(
        r"^  - target: (\d+)\n    hitCount: (\d+)\n    damageComponents:\n"
        r"((?:    - damageType: \d+\n      damage: \d+\n)+)", stage_text, re.M
    ):
        stages.append((int(match[1]), int(match[2]), parts(match[3])))
    if not stages:
        raise ValueError("Unsupported skill stages")
    effects = re.findall(
        r"  - applyToSelf: (\d+)\n    status: (\d+)\n    stacks: (\d+)"
        r"\n    requiredDamageType: (\d+)", skill)
    if any((self_, status, type_) != ("0", "5", "2")
           for self_, status, _, type_ in effects):
        raise ValueError("Add explicit modeling before using this skill rider")
    if int(value(skill, "extraBreakOnWeakness")) != 0:
        raise ValueError("Extra break skill not modeled")
    if float(value(skill, "brokenTargetMultiplier")) != 1:
        raise ValueError("Broken-target bonus not modeled")
    return Weapon(
        value(raw, "weaponId"), json.loads(value(raw, "weaponName")),
        parts(raw.split("  damageComponents:\n", 1)[1].split("  attackCount:", 1)[0]),
        int(value(raw, "attackCount")), int(value(skill, "determinationCost")),
        tuple(stages), value(skill, "ignoreResistance") == "1",
        sum(int(stacks) for _, _, stacks, _ in effects),
    )


@dataclass(slots=True)
class Enemy:
    kind: str
    hp: int
    max_hp: int
    progress: int = 0
    protected_through: int = 0
    skip: bool = False
    charged: bool = False
    guard: bool = False
    last_action: str = ""
    status_ready_at: int = 0
    acted: int = 0
    intent: str = ""
    sunder: int = 0
    sunder_applied: int = 0
    breaks: int = 0
    first_break_hp: int = 0


@dataclass(slots=True)
class Player:
    hp: int = 100
    resolve: int = 1
    max_resolve: int = 3
    poison: int = 0
    sunder: int = 0
    sunder_applied: int = 0
    exhaust: int = 0
    exhaust_applied: int = 0


def scaled(amount: int, sunder: bool) -> int:
    return (amount * 6 + 2) // 5 if sunder else amount


def add_break(enemy: Enemy, round_: int) -> None:
    if enemy.hp <= 0 or enemy.protected_through >= round_:
        return
    enemy.progress += 1
    if enemy.progress >= BREAK_THRESHOLDS[enemy.kind]:
        enemy.progress = 0
        enemy.protected_through = round_ + 1
        enemy.skip = True
        enemy.charged = False
        enemy.breaks += 1
        if enemy.breaks == 1:
            enemy.first_break_hp = enemy.hp


def hit_enemy(enemy: Enemy, parts: tuple[tuple[int, int], ...],
              round_: int, ignore_resistance: bool = False) -> int:
    total = 0
    weak = False
    for type_, damage in parts:
        relation = RELATIONS[enemy.kind][type_]
        weak |= damage > 0 and relation == 0
        total += (0 if relation == 3 else damage // 2
                  if relation == 2 and not ignore_resistance else damage)
    total = scaled(total, enemy.sunder > 0)
    if enemy.guard:
        total //= 2
    loss = min(enemy.hp, total)
    enemy.hp -= loss
    if weak:
        add_break(enemy, round_)
    return loss


# Action is (basic | skill | defend, primary target index).
def player_action(player: Player, enemies: list[Enemy], weapon: Weapon,
                  action: tuple[str, int], round_: int) -> None:
    kind, target_index = action
    if kind == "defend":
        if not player.exhaust:
            player.resolve = min(player.max_resolve, player.resolve + 1)
        return
    if kind == "basic":
        target = enemies[target_index]
        for _ in range(weapon.hits):
            if target.hp <= 0:
                break
            hit_enemy(target, weapon.parts, round_)
        if not player.exhaust:
            player.resolve = min(player.max_resolve, player.resolve + 1)
        return
    assert kind == "skill" and not player.exhaust and player.resolve >= weapon.cost
    player.resolve -= weapon.cost
    touched = set()
    for group, hits, parts in weapon.stages:
        targets = list(range(len(enemies))) if group == 1 else [target_index]
        for _ in range(hits):
            for index in targets:
                target = enemies[index]
                if target.hp <= 0:
                    continue
                hit_enemy(target, parts, round_, weapon.ignore_resistance)
                touched.add(index)
    if weapon.sunder:
        for index in touched:
            target = enemies[index]
            if target.hp > 0:
                target.sunder = min(3, target.sunder + weapon.sunder)
                target.sunder_applied = round_


def status_is_useful(player: Player, enemy: Enemy) -> bool:
    if enemy.kind == "rat":
        return player.poison < 2
    if enemy.kind == "crow":
        return player.sunder == 0
    return player.exhaust == 0


def plan_intents(player: Player, enemies: list[Enemy], rng: random.Random) -> None:
    # Shuffle planning order so the one-charge-per-round limit is unbiased.
    order = list(range(len(enemies)))
    rng.shuffle(order)
    charge_chosen = False
    planned_statuses: set[str] = set()
    for index in order:
        enemy = enemies[index]
        if enemy.hp <= 0:
            continue
        if enemy.charged:
            enemy.intent = "heavy"
            continue
        weights = list(WEIGHTS[enemy.kind])
        if LOW_HEALTH_GUARD_WEIGHT[enemy.kind] > 0 and enemy.hp * 10 < enemy.max_hp * 3:
            weights[3] = LOW_HEALTH_GUARD_WEIGHT[enemy.kind]
        if (enemy.acted < enemy.status_ready_at
                or not status_is_useful(player, enemy)
                or enemy.kind in planned_statuses):
            weights[1] = 0
        if enemy.last_action == "guard":
            weights[3] = 0
        if enemy.kind != "crow" and charge_chosen:
            weights[2] = 0
        actions = ("basic", "status", "double" if enemy.kind == "crow" else "charge", "guard")
        roll = rng.randrange(sum(weights))
        for action, weight in zip(actions, weights):
            roll -= weight
            if roll < 0:
                enemy.intent = action
                break
        if enemy.intent == "charge":
            charge_chosen = True
        if enemy.intent == "status":
            planned_statuses.add(enemy.kind)


def enemy_phase(player: Player, enemies: list[Enemy], round_: int,
                defending: bool, trace: list[str] | None = None) -> None:
    def damage(amount: int) -> None:
        value = scaled(amount, player.sunder > 0)
        if defending:
            value //= 2
        player.hp = max(0, player.hp - value)

    # Equal priority: preserve encounter order, matching stable LINQ ordering.
    for enemy in enemies:
        if enemy.hp <= 0 or player.hp <= 0:
            continue
        enemy.guard = False  # Guard expires at start of this enemy's next turn.
        enemy.acted += 1
        if enemy.skip:
            enemy.skip = False
            enemy.charged = False
            enemy.last_action = "skip"
            if trace is not None:
                trace.append(f"{LABELS[enemy.kind]}：破防跳过，取消蓄力")
            continue
        action = enemy.intent
        hp_before = player.hp
        if action == "basic":
            damage(NORMAL_DAMAGE[enemy.kind])
        elif action == "double":
            damage(SIGNATURE_DAMAGE[enemy.kind])
            if player.hp > 0:
                damage(SIGNATURE_DAMAGE[enemy.kind])
        elif action == "heavy":
            damage(HEAVY_DAMAGE[enemy.kind])
            enemy.charged = False
        elif action == "charge":
            enemy.charged = True
        elif action == "guard":
            enemy.guard = True
        elif action == "status":
            damage(STATUS_DAMAGE[enemy.kind])
            if enemy.kind == "rat":
                if player.hp > 0:
                    player.poison = min(3, player.poison + STATUS_STACKS[enemy.kind])
            elif enemy.kind == "crow":
                player.sunder = min(3, player.sunder + STATUS_STACKS[enemy.kind])
                player.sunder_applied = round_
            else:
                player.exhaust = min(3, player.exhaust + STATUS_STACKS[enemy.kind])
                player.exhaust_applied = round_
            # After action n, actions n+1 and n+2 cannot use this status move.
            enemy.status_ready_at = enemy.acted + 2
        else:
            raise AssertionError(action)
        enemy.last_action = action
        if trace is not None:
            trace.append(f"{LABELS[enemy.kind]}：{action}，直接扣血 {hp_before - player.hp}")


def finish_round(player: Player, enemies: list[Enemy], round_: int) -> None:
    for attr in ("sunder", "exhaust"):
        if getattr(player, attr) and getattr(player, attr + "_applied") < round_:
            setattr(player, attr, getattr(player, attr) - 1)
    for enemy in enemies:
        if enemy.sunder and enemy.sunder_applied < round_:
            enemy.sunder -= 1


def preview(enemies: list[Enemy], player: Player, weapon: Weapon,
            action: tuple[str, int], round_: int) -> list[Enemy]:
    copies = [replace(enemy) for enemy in enemies]
    player_action(replace(player), copies, weapon, action, round_)
    return copies


def select_action(player: Player, enemies: list[Enemy], weapon: Weapon,
                  round_: int, focus: int, policy: str,
                  last_defended: bool) -> tuple[str, int]:
    basic = ("basic", focus)
    can_skill = not player.exhaust and player.resolve >= weapon.cost
    basic_result = preview(enemies, player, weapon, basic, round_)
    chosen = basic
    if can_skill:
        skill = ("skill", focus)
        skill_result = preview(enemies, player, weapon, skill, round_)
        basic_loss = sum(a.hp - b.hp for a, b in zip(enemies, basic_result))
        skill_loss = sum(a.hp - b.hp for a, b in zip(enemies, skill_result))
        # Avoid spending resolve for no extra damage/interrupts.
        if (skill_loss > basic_loss
                or sum(e.skip for e in skill_result) > sum(e.skip for e in basic_result)):
            chosen = skill
    if policy == "focus":
        return chosen
    # Reactive policy is a documented heuristic, not an optimal player solver.
    # First try to kill/break an imminent heavy attack. Otherwise defend when
    # >=10 direct damage is forecast; never defend on consecutive rounds.
    heavies = [i for i, enemy in enumerate(enemies)
               if enemy.hp > 0 and enemy.intent == "heavy" and not enemy.skip]
    if not heavies:
        return chosen
    for index in heavies:
        for kind in (("basic", "skill") if can_skill else ("basic",)):
            action = (kind, index)
            outcome = preview(enemies, player, weapon, action, round_)
            if outcome[index].hp <= 0 or outcome[index].skip:
                return action
    projected_player = replace(player)
    projected_enemies = preview(enemies, player, weapon, chosen, round_)
    enemy_phase(projected_player, projected_enemies, round_, False)
    if player.hp - projected_player.hp >= 10 and not last_defended:
        return ("defend", focus)
    return chosen


def simulate(encounter: tuple[str, ...], hp_profile: dict[str, int], weapon: Weapon,
             rng: random.Random, policy: str, record: bool = False) -> dict:
    enemies = [Enemy(kind, hp_profile[kind], hp_profile[kind]) for kind in encounter]
    player = Player()
    focus = -1
    last_defended = False
    trace = [] if record else None
    for round_ in range(1, 121):
        plan_intents(player, enemies, rng)
        if focus < 0 or enemies[focus].hp <= 0:
            priority = {"crow": 0, "rat": 1, "wraith": 2}
            focus = min((i for i, e in enumerate(enemies) if e.hp > 0),
                        key=lambda i: (priority[enemies[i].kind], i))
        action = select_action(player, enemies, weapon, round_, focus, policy, last_defended)
        last_defended = action[0] == "defend"
        if trace is not None:
            trace.append(f"第 {round_} 轮：玩家 {player.hp} 血，决心 {player.resolve}，"
                         f"中毒 {player.poison}，破甲 {player.sunder}，疲惫 {player.exhaust}")
            trace.append("预告：" + "；".join(
                f"{i + 1}.{LABELS[e.kind]}({e.hp}血) {e.intent}"
                for i, e in enumerate(enemies) if e.hp > 0))
            trace.append(f"玩家：{action[0]}，目标 {action[1] + 1}")
        player_action(player, enemies, weapon, action, round_)
        # Unity checks victory before own-turn status damage, then clears statuses.
        if all(enemy.hp <= 0 for enemy in enemies):
            return {"loss": 100 - player.hp, "rounds": round_, "victory": True,
                    "timeout": False, "trace": trace,
                    "breaks": sum(e.breaks for e in enemies),
                    "enemies_broken": sum(e.breaks > 0 for e in enemies)}
        if player.poison:
            player.hp = max(0, player.hp - player.poison)
            player.poison -= 1
        if player.hp > 0:
            enemy_phase(player, enemies, round_, last_defended, trace)
        if player.hp <= 0:
            return {"loss": 100, "rounds": round_, "victory": False,
                    "timeout": False, "trace": trace,
                    "breaks": sum(e.breaks for e in enemies),
                    "enemies_broken": sum(e.breaks > 0 for e in enemies)}
        finish_round(player, enemies, round_)
    return {"loss": 100 - player.hp, "rounds": 120, "victory": False,
            "timeout": True, "trace": trace,
            "breaks": sum(e.breaks for e in enemies),
            "enemies_broken": sum(e.breaks > 0 for e in enemies)}


def self_check(weapons: dict[str, Weapon]) -> None:
    sword = weapons["sword"]
    assert sword.parts == ((0, 10),) and sword.stages == ((0, 1, ((0, 24),)),)
    assert scaled(4, True) == 5 and scaled(2, True) == 2
    crow = Enemy("crow", 50, 50)
    assert hit_enemy(crow, sword.parts, 1) == (5 if RELATIONS["crow"][0] == 2 else 10)
    assert hit_enemy(crow, ((0, 30),), 1, True) == 30
    rat = Enemy("rat", 50, 50, charged=True, intent="heavy")
    hit_enemy(rat, ((0, 1),), 1)
    hit_enemy(rat, ((0, 1),), 1)
    assert rat.skip and not rat.charged and rat.protected_through == 2
    player = Player()
    enemy_phase(player, [rat], 1, False)
    assert player.hp == 100 and not rat.skip
    hit_enemy(rat, ((0, 1),), 2)
    assert rat.progress == 0
    hit_enemy(rat, ((0, 1),), 3)
    assert rat.progress == 1
    guarded = Enemy("rat", 50, 50, guard=True)
    assert hit_enemy(guarded, sword.parts, 1) == 5
    player = Player(sunder=1)
    enemy_phase(player, [Enemy("rat", 10, 10, intent="basic")], 1, True)
    assert player.hp == 100 - scaled(NORMAL_DAMAGE["rat"], True) // 2
    hp = {kind: 1 for kind in KINDS}
    result = simulate(("rat",), hp, sword, random.Random(1), "focus")
    assert result["loss"] == 0 and result["rounds"] == 1
    wraith = Enemy("wraith", 30, 30, intent="status")
    player = Player()
    enemy_phase(player, [wraith], 1, False)
    assert player.exhaust == 1 and wraith.status_ready_at == 3
    finish_round(player, [wraith], 1)
    assert player.exhaust == 1
    old_resolve = player.resolve
    player_action(player, [wraith], sword, ("basic", 0), 2)
    assert player.resolve == old_resolve
    finish_round(player, [wraith], 2)
    assert player.exhaust == 0


def percentile(values: list[int], p: float) -> int:
    return sorted(values)[max(0, math.ceil(len(values) * p) - 1)]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trials", type=int, default=10000)
    parser.add_argument("--seed", type=int, default=20261020)
    parser.add_argument("--profiles", nargs="+", choices=HP_PROFILES,
                        default=["installed"])
    parser.add_argument("--weapons", nargs="+", default=["sword"],
                        choices=["sword", "axe", "hammer", "dual_swords"])
    parser.add_argument("--policies", nargs="+", choices=["focus", "reactive"],
                        default=["focus"])
    parser.add_argument("--quiet", action="store_true")
    parser.add_argument("--crow-pierce", choices=["installed", "resistant", "normal"],
                        default="installed")
    parser.add_argument("--output", type=Path,
                        default=ROOT / "output/encounter-balance-v1/summary.json")
    args = parser.parse_args()
    if args.trials <= 0:
        parser.error("--trials must be positive")
    load_installed_balance()
    if args.crow_pierce == "normal":
        RELATIONS["crow"] = (1, 0, 1, 1)
    elif args.crow_pierce == "resistant":
        RELATIONS["crow"] = (2, 0, 1, 1)
    weapons = {weapon.id: weapon for weapon in map(read_weapon, (1, 4, 5, 7))}
    self_check(weapons)
    rows = []
    for profile in args.profiles:
        for weapon_id in args.weapons:
            for policy in args.policies:
                for combo_index, encounter in enumerate(ENCOUNTERS):
                    # Reproducible seed schedule. Profiles/policies share trial
                    # seeds, but different trajectories consume RNG differently.
                    results = [simulate(encounter, HP_PROFILES[profile], weapons[weapon_id],
                                        random.Random(args.seed + combo_index * 1_000_003 + trial),
                                        policy) for trial in range(args.trials)]
                    losses = [result["loss"] for result in results]
                    wins = [result for result in results if result["victory"]]
                    row = {
                        "profile": profile, "hp": HP_PROFILES[profile],
                        "weapon": weapon_id, "policy": policy,
                        "encounter": encounter,
                        "label": "＋".join(LABELS[kind] for kind in encounter),
                        "trials": args.trials,
                        "mean_loss_all": round(statistics.mean(losses), 3),
                        "mean_loss_victories": round(statistics.mean(
                            result["loss"] for result in wins), 3) if wins else None,
                        "median_loss": percentile(losses, .5),
                        "p90_loss": percentile(losses, .9),
                        "p95_loss": percentile(losses, .95),
                        "death_rate": sum(not r["victory"] and not r["timeout"]
                                          for r in results) / args.trials,
                        "timeouts": sum(r["timeout"] for r in results),
                        "mean_rounds": round(statistics.mean(r["rounds"] for r in results), 3),
                        "mean_breaks": round(statistics.mean(r["breaks"] for r in results), 3),
                        "mean_enemies_broken": round(statistics.mean(r["enemies_broken"] for r in results), 3),
                        "mean_loss_se": round(statistics.stdev(losses) / math.sqrt(args.trials), 4)
                        if args.trials > 1 else None,
                    }
                    rows.append(row)
                    if not args.quiet:
                        print(json.dumps(row, ensure_ascii=True), flush=True)
    examples = []
    for offset in range(3):
        result = simulate(ENCOUNTERS[-1], HP_PROFILES[args.profiles[-1]],
                          weapons[args.weapons[0]], random.Random(args.seed + offset),
                          args.policies[-1], True)
        examples.append({"seed": args.seed + offset, **result})
    output = {
        "model_version": 3, "seed": args.seed, "crow_pierce": args.crow_pierce,
        "scope": "Offline simulation of installed small-monster data, not a replacement for Unity gameplay validation",
        "player": {"hp": 100, "initial_resolve": 1, "resolve_cap": 3,
                   "healing": False, "weapon_switching": False,
                   "other_buffs": False, "all_weaknesses_known": True},
        "weapons_from_assets": [asdict(weapons[id_]) for id_ in args.weapons],
        "enemy_assumptions": {
            "weights_basic_status_signature_guard": WEIGHTS,
            "normal_damage": NORMAL_DAMAGE,
            "heavy_damage": HEAVY_DAMAGE,
            "status_direct_damage_from_assets": STATUS_DAMAGE,
            "status_stacks_from_assets": STATUS_STACKS,
            "rat_status": "poison caps at 3 and loses 1 after dealing stacks damage",
            "crow_status": "sunder 1, no direct damage; direct damage +20%, rounded as StatusRules",
            "wraith_status": "exhaust 1, no direct damage; blocks skills and resolve gains",
            "crow_signature": {"damage_per_hit": SIGNATURE_DAMAGE["crow"], "hits": 2},
            "guard": "50% direct damage reduction until start of next own turn, no consecutive guards",
            "low_health": "rat guard weight becomes 30 below 30% HP",
            "status_cooldown": "two subsequent own actions without status move; skip counts as an action",
            "shared_status_limit": "at most one planned application per status type each round",
            "status_selection": "rat skips at poison>=2; crow/wraith skip if their status is active",
            "charge_limit": "at most one newly selected charge per round; no restriction on an existing heavy",
            "charge_break": "break cancels charged heavy; skips once and protects current+next round",
            "relations_pierce_slash_blunt_fire": RELATIONS,
            "relation_enum": ["weak", "normal", "resistant", "immune"],
            "break_threshold": 2,
            "enemy_order": "encounter order, equal priority 12",
            "hp_profiles": HP_PROFILES,
            "asset_mix_note": "Legacy pre-balance Rat/Wraith prefabs were 50 HP; Crow simulator prefab was 120 HP",
        },
        "policies": {
            "focus": "Focus crow, then rat, then wraith; keep target until death; spend affordable skill when it adds damage/interrupts",
            "reactive": "Focus policy plus try to kill/break an imminent heavy; otherwise defend if forecast direct loss>=10, never consecutive defense",
        },
        "timing": "Plan/lock intents before player turn; player acts first; immediate victory before poison settlement; poison at own turn end; enemies act; duration ticks except application round; clear statuses at victory",
        "limitations": [
            "Base enemy data is loaded from production assets; hypothetical HP/resistance profiles are explicitly labeled",
            "Heuristic player policies are not optimal and do not represent the full player skill distribution",
            "Each trial starts full HP, resolve 1 and no statuses; no consecutive-fight resource depletion",
            "Known weaknesses do not consume a lantern action; first-encounter discovery can cost more",
            "100-HP death truncates loss at 100; average includes deaths, victory-only mean is separate",
            "No uncertainty is sampled for rules/HP; Monte Carlo error only measures random action selection",
        ],
        "self_checks": "passed: asset values, resistance bypass, break protection/cancel, guard, rounding, one-shot encounter, exhaust timing",
        "results": rows, "sample_battles": examples,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2), "utf-8")
    print(f"Saved {len(rows)} scenarios to {args.output}", flush=True)


if __name__ == "__main__":
    main()
