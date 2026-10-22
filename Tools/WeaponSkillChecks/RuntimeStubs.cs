// Only Unity hosting, animation playback and unrelated item/enemy turns are stubbed.
// Damage, skills, statuses, resolve, targeting and turn flow use production sources.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public static void Destroy(object obj) { } }
    public class Component : Object
    {
        public GameObject gameObject;
        public string name => gameObject?.name ?? GetType().Name;
        public T GetComponent<T>() where T : class => gameObject?.GetComponent<T>();
    }
    public class MonoBehaviour : Component
    {
        public readonly List<IEnumerator> Pending = new();
        public object StartCoroutine(IEnumerator routine)
        {
            // Unity runs the coroutine synchronously until the first yield.
            if (routine.MoveNext()) Pending.Add(Continue(routine));
            return routine;
        }
        static IEnumerator Continue(IEnumerator routine)
        {
            yield return routine.Current;
            while (routine.MoveNext()) yield return routine.Current;
        }
    }
    public class ScriptableObject : Object { }
    public class GameObject
    {
        readonly Dictionary<Type, object> components = new();
        public string name;
        public GameObject(string name = "test") { this.name = name; }
        public T AddComponent<T>() where T : Component, new()
        { var item = new T { gameObject = this }; components[typeof(T)] = item; return item; }
        public T GetComponent<T>() where T : class
        { foreach (var value in components.Values) if (value is T item) return item; return null; }
    }
    public class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    public static class Debug { public static void Log(object value) { } public static void LogWarning(object value) { } }
    public static class Random { public static int Range(int first, int last) => first; }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Clamp(int n, int a, int b) => Math.Clamp(n, a, b);
        public static int FloorToInt(float n) => (int)Math.Floor(n);
    }
    public class SerializeField : Attribute { }
    public class HideInInspector : Attribute { }
    public class TextArea : Attribute { }
    public class Header : Attribute { public Header(string text) { } }
    public class Tooltip : Attribute { public Tooltip(string text) { } }
    public class Min : Attribute { public Min(float n) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenu : Attribute { public string fileName; public string menuName; }
}
namespace UnityEngine.SceneManagement { }
namespace TMPro { public class TMP_Text { public string text; } }

public class GameRoot : UnityEngine.MonoBehaviour
{
    public static GameRoot I;
    public GameState state = new();
    public BattleFlowController activeBattle;
}
public class TestGameState { public PlayerState player = new(); public TestMapState map = new(); }
public class GameState : TestGameState { }
// Narrative presentation is exercised by the native guide playthrough, not this combat rules host.
public sealed class GuideBattleController
{
    public static GuideBattleController Attach(BattleFlowController battle) => null;
    public bool Allows(string action) => true;
    public string Instruction => "";
    public void ReadyForPlayer() { }
    public void ActionCompleted(string action) { }
}
public class TestMapState { public string templateId; }
public class PlayerState
{
    public int hp = 100, maxHp = 100, resolve, maxResolve = StatusRules.DefaultMaxResolve;
    public int nextAttackBonusPercent, nextDirectHitReductionPercent;
    public List<StatusSnapshot> ordinaryStatuses = new();
}
public class EnemyCombatant : Combatant
{
    public int Priority => 10;
    public BreakGauge Break { get; set; } = new(99);
    public IEnumerator TakeTurn(PlayerCombatant player) { yield break; }
    public void PrepareIntent(PlayerCombatant player, EnemyIntentBudget budget) { }
}
public class EnemyIntentBudget { }
public class PlayerDefenseEffects : UnityEngine.MonoBehaviour { }
public class PlayerAttackAnimation : UnityEngine.MonoBehaviour
{
    public int Plays, Impacts;
    public Action BeforeHit;
    public bool Supports(WeaponData weapon) => true;
    public void ShowImpact(Combatant target) { Impacts++; }
    public IEnumerator PlayAttack(WeaponData weapon, Func<bool> alive, Func<bool> hasTarget, Action hit)
    {
        Plays++;
        BeforeHit?.Invoke();
        if (alive() && hasTarget()) hit();
        yield return null;
    }
}
public struct ItemResult
{
    public bool ok; public string message;
    public ItemResult(bool ok, string message) { this.ok = ok; this.message = message; }
}
public static class ItemTable { public const string Lantern = "lantern"; public static string CanonicalId(string id) => id; }
public class ItemAction : IBattleAction
{
    public string ItemId; public ItemResult Result => new(true, "");
    public ItemAction(GameState state, string id) { ItemId = id; }
    public static ItemResult Validate(GameState state, string id, PlayerCombatant player,
        EnemyCombatant target = null, bool selectingTarget = false) => new(true, "");
    public IEnumerator Execute(Combatant user, Combatant target) { yield break; }
}
