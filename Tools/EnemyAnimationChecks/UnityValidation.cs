// Copied into an isolated Unity test project by Run-Unity.ps1; never installed in gameplay.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FuYaIntegrationValidation
{
    const string Pending = "FuYaIntegrationValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finishing;

    [Serializable] class Result { public bool passed; public string[] checks; public string error; }

    static FuYaIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }

    static string Output
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-fuyaResult");
            return index >= 0 ? args[index + 1] : Path.Combine(Application.dataPath, "../unity-validation.json");
        }
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this validator in its isolated batch project.");
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<EnemySpriteAnimationSet>("Assets/Data/EnemyAnimations/FuYaAnimations.asset");
            Check(set != null && set.idleFrames.Length == 30 && set.attackFrames.Length == 18 && set.hurtFrames.Length == 9,
                "Unity imports all 57 authored sprite references");
            Sprite source = null;
            Vector3 scale = Vector3.one, offset = Vector3.zero;
            foreach (string scene in new[] { "CombatScene", "CombatAnimationPreview", "TestScene" })
            {
                var loaded = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                EnemySpriteAnimation view = null;
                foreach (var root in loaded.GetRootGameObjects())
                    if (root.GetComponent<EnemySpriteAnimation>() != null) view = root.GetComponent<EnemySpriteAnimation>();
                Check(view != null && view.animations == set && view.appearance != null, scene + " has a configured animation component");
                if (scene == "CombatScene") { source = view.appearance.sprite; scale = view.visualScale; offset = view.visualOffset; }
            }
            Check(source != null, "production crow sprite resolves in Unity");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var enemy = new GameObject("AnimationCheckEnemy");
            enemy.SetActive(false);
            enemy.transform.position = new Vector3(5, 0, 0);
            var renderer = enemy.AddComponent<SpriteRenderer>(); renderer.sprite = source;
            enemy.AddComponent<EnemyCombatant>();
            var animation = enemy.AddComponent<EnemySpriteAnimation>();
            animation.animations = set; animation.appearance = renderer;
            animation.visualScale = scale; animation.visualOffset = offset;
            enemy.SetActive(true);
            var player = new GameObject("AnimationCheckPlayer");
            player.transform.position = new Vector3(-5, 0, 0);
            player.AddComponent<BoxCollider2D>(); player.AddComponent<PlayerCombatant>();
            SessionState.SetString(Pending + ".sceneChecks", string.Join("\n", checks));
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear();
        checks.AddRange(SessionState.GetString(Pending + ".sceneChecks", "").Split('\n'));
        Time.captureFramerate = 30;
        var enemy = GameObject.Find("AnimationCheckEnemy").GetComponent<EnemyCombatant>();
        var player = GameObject.Find("AnimationCheckPlayer").GetComponent<PlayerCombatant>();
        enemy.Initialize(); player.Initialize();
        // Keep the harness alive when the last check deliberately destroys the enemy.
        var harness = new GameObject("AnimationCheckHarness").AddComponent<EnemySpriteAnimation>();
        harness.StartCoroutine(Guard(Exercise(enemy, player)));
    }

    static IEnumerator Guard(IEnumerator work)
    {
        while (true)
        {
            bool more;
            try { more = work.MoveNext(); }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            if (!more) break;
            yield return work.Current;
        }
        Finish(true, null);
    }

    static IEnumerator Exercise(EnemyCombatant enemy, PlayerCombatant player)
    {
        var view = enemy.GetComponent<EnemySpriteAnimation>();
        var set = view.animations;
        yield return null;
        Check(view.CurrentMotion == "Idle" && view.CurrentSprite != null && !view.appearance.enabled,
            "runtime replaces the old still with animated idle");
        Sprite first = view.CurrentSprite;
        var idleMesh = enemy.GetComponentInChildren<MeshFilter>();
        Vector3[] initialVertices = idleMesh != null ? idleMesh.sharedMesh.vertices : null;
        yield return new WaitForSeconds(.12f);
        Debug.Log("FUYA IDLE: stable=" + set.useDeformedIdle + ", mesh=" + (idleMesh != null)
            + ", drivers=" + enemy.GetComponentsInChildren<EnemyIdleSpriteMesh>().Length
            + ", frame=" + view.CurrentSprite.name + ", motion=" + view.CurrentMotion);
        if (idleMesh != null)
        {
            Vector3[] vertices = idleMesh.sharedMesh.vertices;
            bool moved = false;
            for (int i = 0; i < vertices.Length; i++) if (vertices[i] != initialVertices[i]) moved = true;
            Check(moved && idleMesh.GetComponent<MeshRenderer>().enabled,
                "configured stable idle advances its visible breathing mesh");
        }
        else Check(view.CurrentSprite != first, "idle advances through actual sprite drawings");
        Vector3 actorPosition = enemy.transform.position;
        int hits = 0, health = player.CurrentHP;
        float hitTime = 0f;
        player.OnHpChanged += actor =>
        {
            if (actor.CurrentHP < health)
            {
                hits++; hitTime = Time.time;
                if (actor.CurrentHP > 0)
                {
                    Check(view.CurrentSprite == set.attackFrames[9], "damage occurs on the tenth attack drawing");
                    Vector3 beak = enemy.transform.Find("EnemyAnimationVisual").TransformPoint(set.attackContactPoint);
                    Bounds target = player.GetComponent<Collider2D>().bounds;
                    Vector3 expected = target.center + Vector3.right * target.extents.x;
                    Check(Vector3.Distance(beak, expected) < .01f, "beak reaches the player at contact");
                }
            }
            health = actor.CurrentHP;
        };
        var attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        var serialized = new SerializedObject(attack);
        serialized.FindProperty("damage").intValue = 6;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        float start = Time.time;
        enemy.StartCoroutine(new EnemyAttackAction().Execute(enemy, player, attack));
        yield return new WaitForSeconds(.15f);
        Check(player.CurrentHP == 100 && view.CurrentMotion == "Attack", "wind-up does not deal instant damage");
        yield return new WaitForSeconds(.2f);
        Check(player.CurrentHP == 94 && hits == 1 && hitTime - start >= .299f, "one attack delivers exactly one timed hit");
        yield return new WaitForSeconds(.35f);
        Check(view.CurrentMotion == "Idle" && enemy.transform.position == actorPosition,
            "attack recovers to idle without moving the battle collider");
        enemy.TakeDamage(0);
        Check(view.CurrentMotion == "Idle", "zero damage does not flinch");
        enemy.TakeDamage(1);
        Check(view.CurrentMotion == "Hurt" && view.CurrentSprite == set.hurtFrames[0], "direct damage starts hurt immediately");
        yield return new WaitForSeconds(.1f);
        Check(view.CurrentMotion == "Hurt" && view.CurrentSprite != set.hurtFrames[0], "hurt advances through its sprite drawings");
        yield return new WaitForSeconds(.3f);
        Check(view.CurrentMotion == "Idle", "hurt returns to idle");
        enemy.LoseRaw(1);
        Check(view.CurrentMotion == "Idle", "status damage does not trigger a direct-hit flinch");
        enemy.StartCoroutine(new EnemyAttackAction().Execute(enemy, player, attack));
        yield return new WaitForSeconds(.1f);
        player.TakeDamage(1000);
        int afterManualHit = hits;
        yield return new WaitForSeconds(.6f);
        Check(hits == afterManualHit && view.CurrentMotion == "Idle", "target death cancels a wind-up without extra damage");
        var replacement = new GameObject("ReplacementPlayer");
        replacement.transform.position = new Vector3(-5, 0, 0);
        replacement.AddComponent<BoxCollider2D>();
        var player2 = replacement.AddComponent<PlayerCombatant>(); player2.Initialize();
        int deaths = 0; enemy.OnDied += actor => deaths++;
        enemy.StartCoroutine(new EnemyAttackAction().Execute(enemy, player2, attack));
        yield return new WaitForSeconds(.1f);
        enemy.TakeDamage(1000);
        var ending = GameObject.Find("DefeatedEnemyVisual");
        Check(ending != null && ending.GetComponent<SpriteRenderer>().sprite == set.hurtFrames[0],
            "lethal damage detaches a visible hurt animation");
        yield return new WaitForSeconds(.08f);
        Check(enemy == null && ending != null && ending.GetComponent<SpriteRenderer>().sprite != set.hurtFrames[0]
            && player2.CurrentHP == 100 && deaths == 1, "dead attacker cannot hit; death is paid once while hurt remains visible");
        yield return new WaitForSeconds(.28f);
        Check(ending == null, "lethal hurt visual cleans itself up after .3 seconds");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message); Debug.Log("FUYA CHECK: " + message);
    }
    static void Logged(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(Pending, false) && (type == LogType.Exception || type == LogType.Error))
            Finish(false, message + "\n" + stack);
    }
    static void Finish(bool passed, string error)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool(Pending, false);
        File.WriteAllText(Output, JsonUtility.ToJson(new Result{passed=passed,checks=checks.ToArray(),error=error},true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
