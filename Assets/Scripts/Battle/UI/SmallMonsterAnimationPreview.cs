using System.Collections;
using UnityEngine;

// Review scene controls. Combat prefabs use the same damage/attack/death callbacks as the game.
public sealed class SmallMonsterAnimationPreview : MonoBehaviour
{
    public GameObject ratPrefab, wraithPrefab, slothPrefab;
    EnemyCombatant enemy;
    PlayerCombatant target;
    [SerializeField] int species;
    string message = "待机";
    GUIStyle label, button;

    void Start()
    {
        var player = new GameObject("PreviewTarget");
        player.transform.position = new Vector3(-4f, -1f, 0);
        player.AddComponent<BoxCollider2D>().size = new Vector2(.8f, 2f);
        target = player.AddComponent<PlayerCombatant>(); target.Initialize();
        ResetActor();
    }

    void ResetActor()
    {
        StopAllCoroutines();
        if (enemy != null) Destroy(enemy.gameObject);
        EnemyDefeatSpritePlayback.ClearIn(gameObject.scene);
        var prefab = species == 0 ? ratPrefab : species == 1 ? wraithPrefab : slothPrefab;
        if (prefab == null) return;
        var actor = Instantiate(prefab, new Vector3(3f, 0, 0), Quaternion.identity);
        enemy = actor.GetComponent<EnemyCombatant>(); enemy.Initialize();
        var set = actor.GetComponent<EnemySpriteAnimation>().animations;
        float foot = (set.artworkBounds.y * set.Portrait.rect.height - set.Portrait.pivot.y)
            / set.Portrait.pixelsPerUnit * actor.transform.localScale.y;
        actor.transform.position += Vector3.up * (-2f - foot);
        message = "待机";
    }

    IEnumerator Attack()
    {
        if (enemy == null || enemy.IsDead) yield break;
        var actor = enemy;
        message = "攻击：蓄力 → 命中 → 收势";
        yield return actor.GetComponent<EnemySpriteAnimation>().PlayAttack(target,
            () => actor != null && !actor.IsDead, () => target != null,
            () => { BattleCameraShake.PlayHit(); message = "短冲命中 + 轻微震屏"; });
        message = "待机";
    }

    void Hurt()
    {
        if (enemy != null && !enemy.IsDead) { enemy.TakeDamage(1); message = "受击"; }
    }

    void Death()
    {
        if (enemy != null && !enemy.IsDead) { enemy.TakeDamage(enemy.CurrentHP); message = "受击 + 灰烬消散"; }
    }

    IEnumerator All()
    {
        yield return new WaitForSeconds(1f);
        yield return Attack();
        yield return new WaitForSeconds(.4f);
        Hurt(); yield return new WaitForSeconds(1f);
        Death();
    }

    void OnGUI()
    {
        if (label == null)
        {
            var font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
        }
        GUI.Label(new Rect(20, 15, 850, 32), "敌人动作预览 · 30 FPS · 原画颜色", label);
        if (GUI.Button(new Rect(20, 55, 110, 36), "瘟鼠", button)) { species = 0; ResetActor(); }
        if (GUI.Button(new Rect(140, 55, 110, 36), "怨影", button)) { species = 1; ResetActor(); }
        if (slothPrefab != null && GUI.Button(new Rect(260, 55, 110, 36), "懒惰", button)) { species = 2; ResetActor(); }
        if (GUI.Button(new Rect(20, 102, 100, 36), "待机 / 重置", button)) ResetActor();
        if (GUI.Button(new Rect(130, 102, 90, 36), "攻击", button)) StartCoroutine(Attack());
        if (GUI.Button(new Rect(230, 102, 90, 36), "受击", button)) Hurt();
        if (GUI.Button(new Rect(330, 102, 90, 36), "死亡", button)) Death();
        if (GUI.Button(new Rect(430, 102, 120, 36), "全套演示", button)) { ResetActor(); StartCoroutine(All()); }
        GUI.Label(new Rect(20, 151, 850, 32), message, label);
    }
}
