using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EnemyAttackMotionValidation
{
    const string Pending = "EnemyAttackMotionValidation.Pending";
    static readonly string[] Names = { "Crow", "Rat", "Wraith", "Sloth" };
    static readonly List<string> checks = new List<string>();
    static readonly List<Metric> metrics = new List<Metric>();
    static bool finishing;
    [Serializable] class Metric { public string enemy; public float distance, maximumFrameStep; public int hits, frames; }
    [Serializable] class Result { public bool passed; public string[] checks; public Metric[] metrics; public string error; }
    static string Output
    {
        get { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-attackResult");
            return i < 0 ? Path.Combine(Application.dataPath, "../attack-motion-validation.json") : args[i + 1]; }
    }
    static EnemyAttackMotionValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            Time.captureFramerate = 30;
            new GameObject("AttackMotionCheckHarness").AddComponent<EnemySpriteAnimation>().StartCoroutine(
                Guard(SessionState.GetBool(Pending + ".battle", false) ? ExerciseBattle() : Exercise()));
        };
        Application.logMessageReceived += (message, stack, type) =>
        { if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception)) Finish(false, message + "\n" + stack); };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Unity project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending + ".battle", false);
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    public static void RunBattle()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Unity project.");
        EditorSceneManager.OpenScene("Assets/Scenes/BattleSimulator.unity");
        SessionState.SetBool(Pending + ".battle", true);
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    static IEnumerator Guard(IEnumerator work)
    {
        while (true)
        {
            bool more; try { more = work.MoveNext(); } catch (Exception e) { Finish(false, e.ToString()); yield break; }
            if (!more) break; yield return work.Current;
        }
        Finish(true, null);
    }
    static EnemyCombatant Spawn(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleSimulator/" + name + ".prefab");
        var actor = UnityEngine.Object.Instantiate(prefab).GetComponent<EnemyCombatant>(); actor.Initialize();
        return actor;
    }
    static IEnumerator Exercise()
    {
        var targetRoot = new GameObject("AttackTarget"); var target = targetRoot.AddComponent<PlayerCombatant>();
        target.Initialize();
        foreach (string name in Names)
        {
            float referenceDistance = -1;
            foreach (float gap in new[] { -5f, -15f, 5f })
            {
                var actor = Spawn(name); var view = actor.GetComponent<EnemySpriteAnimation>(); var set = view.animations;
                Check(set != null && set.framesPerSecond == 30 && set.attackLungeDistance <= .16f,
                    name + ": real simulator prefab has the short 30 FPS attack");
                targetRoot.transform.position = actor.transform.position + Vector3.right * gap;
                var visual = actor.transform.Find("EnemyAnimationVisual"); var origin = visual.position;
                var rootPosition = actor.transform.position; var collider = actor.GetComponent<Collider2D>();
                var colliderPosition = collider != null ? collider.bounds.center : rootPosition;
                var bounds = set.artworkBounds;
                var center = new Vector3(((bounds.x + bounds.z) * .5f * set.Portrait.rect.width - set.Portrait.pivot.x) / set.Portrait.pixelsPerUnit,
                    ((bounds.y + bounds.w) * .5f * set.Portrait.rect.height - set.Portrait.pivot.y) / set.Portrait.pixelsPerUnit, 0);
                var cameraCenter = visual.TransformPoint(center);
                float height = (bounds.w - bounds.y) * set.Portrait.rect.height / set.Portrait.pixelsPerUnit * visual.lossyScale.y;
                float size = Mathf.Max(height * .75f, height * (bounds.z - bounds.x) / (bounds.w - bounds.y) * .60f);
                int hits = 0, frame = 0; bool done = false, stationary = true, direction = true;
                float distance = 0, maxStep = 0; var previous = origin;
                var work = view.PlayAttack(target, () => actor != null && !actor.IsDead, () => true, () => hits++);
                view.StartCoroutine(Complete(work, () => done = true));
                while (!done && frame < 120)
                {
                    distance = Mathf.Max(distance, Vector3.Distance(visual.position, origin));
                    maxStep = Mathf.Max(maxStep, Vector3.Distance(visual.position, previous)); previous = visual.position;
                    stationary &= actor.transform.position == rootPosition
                        && (collider == null || collider.bounds.center == colliderPosition);
                    direction &= (visual.position.x - origin.x) * Mathf.Sign(gap) >= -.0001f
                        && Mathf.Abs(visual.position.y - origin.y) < .0001f;
                    if (gap == -5) Capture(cameraCenter, size, name + "/" + frame.ToString("000") + ".png");
                    yield return null; frame++;
                }
                Check(done && hits == 1 && view.CurrentMotion == "Idle", name + ": attack hits once and completes recovery");
                Check(stationary && direction, name + ": actor and collider remain fixed; visual moves only toward target");
                Check(Mathf.Abs(distance - set.attackLungeDistance) < .003f && maxStep < .055f,
                    name + ": bounded travel without a sudden forward or backward jump");
                Check(Vector3.Distance(visual.position, origin) < .0001f, name + ": settles at the starting position before idle");
                if (referenceDistance >= 0) Check(Mathf.Abs(distance - referenceDistance) < .001f,
                    name + ": opponent distance and side do not enlarge the lunge");
                else
                {
                    referenceDistance = distance;
                    metrics.Add(new Metric { enemy = name, distance = distance, maximumFrameStep = maxStep, hits = hits, frames = frame });
                    for (int i = 0; i < 6; i++) { Capture(cameraCenter, size, name + "/" + (frame++).ToString("000") + ".png"); yield return null; }
                }
                UnityEngine.Object.Destroy(actor.gameObject); yield return null;
            }
            var interrupted = Spawn(name); var animation = interrupted.GetComponent<EnemySpriteAnimation>();
            targetRoot.transform.position = new Vector3(-8, 0, 0);
            int interruptedHits = 0;
            animation.StartCoroutine(animation.PlayAttack(target, () => true, () => true, () => interruptedHits++));
            yield return new WaitForSeconds(animation.animations.attackContactFrame / 30f * .65f);
            var visualRoot = interrupted.transform.Find("EnemyAnimationVisual"); var before = visualRoot.position;
            animation.PlayHurt();
            Check(visualRoot.position == before, name + ": being hurt mid-attack preserves its visible location");
            yield return new WaitForSeconds(1.4f);
            Check(interruptedHits == 0 && animation.CurrentMotion == "Idle", name + ": hurt cancels a pending hit and recovers");
            animation.StartCoroutine(animation.PlayAttack(target, () => true, () => true, () => interruptedHits++));
            yield return new WaitForSeconds(animation.animations.attackContactFrame / 30f * .65f);
            before = visualRoot.position; animation.PlayDefeat();
            Check(visualRoot.position == before, name + ": death mid-attack does not teleport the detached visual");
            yield return new WaitForSeconds(2.8f);
            Check(interruptedHits == 0 && visualRoot == null, name + ": death cancels the attack and cleans up its effect");
            UnityEngine.Object.Destroy(interrupted.gameObject); yield return null;
        }
        UnityEngine.Object.Destroy(targetRoot);
    }
    static IEnumerator ExerciseBattle()
    {
        yield return null; yield return null; yield return null;
        var simulator = UnityEngine.Object.FindObjectOfType<BattleSimulator>();
        Check(simulator != null && simulator.Ready && simulator.enemies.Length == 4, "actual BattleSimulator scene loads all four enemies");
        for (int i = 0; i < 4; i++)
        {
            GameObject.Find("Enemy_" + i).GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            var actor = simulator.Enemy; var view = actor.GetComponent<EnemySpriteAnimation>();
            var visual = actor.transform.Find("EnemyAnimationVisual"); var origin = visual.position;
            var actorPosition = actor.transform.position;
            int hp = simulator.Flow.Player.CurrentHP;
            UnityEngine.Object.FindObjectOfType<BattleHudView>().defend.onClick.Invoke();
            yield return null;
            bool seenAttack = false, fixedRoot = true; float distance = 0;
            for (int frame = 0; frame < 180 && simulator.Flow.CurrentState != BattleState.PlayerTurn; frame++)
            {
                seenAttack |= view.CurrentMotion == "Attack";
                distance = Mathf.Max(distance, Vector3.Distance(origin, visual.position));
                fixedRoot &= actor.transform.position == actorPosition;
                yield return null;
            }
            Check(seenAttack && simulator.Flow.CurrentState == BattleState.PlayerTurn && simulator.Flow.Player.CurrentHP < hp,
                Names[i] + ": real defend button triggers the enemy attack, damage and return to the player turn");
            Check(fixedRoot && distance <= view.animations.attackLungeDistance + .001f
                && Vector3.Distance(visual.position, origin) < .0001f,
                Names[i] + ": complete battle turn uses bounded travel and returns to its starting location");
        }
        simulator.SelectEnemy(0); yield return null; yield return null;
        UnityEngine.Object.FindObjectOfType<BattleHudView>().defend.onClick.Invoke();
        for (int i = 0; i < 90 && simulator.Enemy.GetComponent<EnemySpriteAnimation>().CurrentMotion != "Attack"; i++) yield return null;
        simulator.NextEnemy(); yield return null; yield return null;
        Check(simulator.Ready && simulator.Flow.CurrentState == BattleState.PlayerTurn
            && UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1,
            "switching enemies during an attack cancels the old turn and leaves exactly one enemy");
    }
    static IEnumerator Complete(IEnumerator work, Action completed)
    { while (work.MoveNext()) yield return work.Current; completed(); }
    static void Capture(Vector3 position, float size, string file)
    {
        var go = new GameObject("AttackCapture"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = size; camera.cullingMask = ~(1 << 5);
        camera.transform.position = position + new Vector3(0, 0, -20); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(34, 41, 52, 255);
        var rt = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
    }
    static void Check(bool pass, string text)
    { if (!pass) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("ATTACK MOTION CHECK: " + text); }
    static void Finish(bool pass, string error)
    {
        if (finishing) return; finishing = true; SessionState.SetBool(Pending, false); Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = pass, checks = checks.ToArray(), metrics = metrics.ToArray(), error = error }, true));
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
