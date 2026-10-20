using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SmallMonsterValidation
{
    const string Pending = "SmallMonsterValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finishing;
    [Serializable] class Result { public bool passed; public string[] checks; public string error; }
    static string Output
    {
        get
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-smallResult");
            return i >= 0 ? args[i + 1] : Path.Combine(Application.dataPath, "../small-monster-validation.json");
        }
    }
    static SmallMonsterValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }

    public static void Run()
    {
        if (Set("Rat") != null && Set("Rat").hurtAndAshOnDeath) { EnemyHurtAshValidation.Run(); return; }
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch project.");
        try
        {
            foreach (var name in new[] { "Rat", "Wraith" })
            {
                var set = Set(name);
                Check(set != null && set.framesPerSecond == 30 && set.HasAttack && set.HasHurt && set.HasDeath,
                    name + ": all states configured at 30 FPS");
                Check(set.artworkMaterial != null && set.artworkMaterial.GetFloat("_ArtworkGamma") == 1
                    && set.preserveArtworkColors && set.Portrait == set.portraitIcon,
                    name + ": original texture and ungraded palette used for every motion and HUD");
                Check(!ShaderUtil.ShaderHasError(set.artworkMaterial.shader)
                    && !ShaderUtil.ShaderHasError(set.deathAshMaterial.shader), name + ": artwork and ash shaders compile");
                var prefab = Prefab(name);
                Check(prefab != null && prefab.GetComponent<EnemyCombatant>() != null
                    && prefab.GetComponent<EnemySpriteAnimation>().animations == set
                    && prefab.GetComponent<BoxCollider2D>() != null, name + ": combat prefab resolves all required components");
                var serialized = new SerializedObject(prefab.GetComponent<EnemyCombatant>());
                Check(serialized.FindProperty("attackDatas").GetArrayElementAtIndex(0).objectReferenceValue != null,
                    name + ": prefab has a usable attack definition");
            }
            var test = EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
            foreach (var name in new[] { "Rat", "Wraith" })
            {
                EnemySpriteAnimation found = null;
                foreach (var root in test.GetRootGameObjects())
                    foreach (var view in root.GetComponentsInChildren<EnemySpriteAnimation>(true))
                        if (view.animations == Set(name)) found = view;
                Check(found != null && found.appearance != null && found.appearance.color == Color.white,
                    name + ": TestScene is bound with neutral renderer color");
            }
            BuildReviewScene();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var target = new GameObject("SmallMonsterTarget");
            target.transform.position = new Vector3(-3f, 0, 0);
            target.AddComponent<BoxCollider2D>().size = new Vector2(.8f, 2f);
            target.AddComponent<PlayerCombatant>();
            SessionState.SetString(Pending + ".checks", string.Join("\n", checks));
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static EnemySpriteAnimationSet Set(string name) => AssetDatabase.LoadAssetAtPath<EnemySpriteAnimationSet>(
        "Assets/Data/EnemyAnimations/" + name + "Animations.asset");
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab");

    static void BuildReviewScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 4;
        camera.transform.position = new Vector3(0, .4f, -20);
        camera.backgroundColor = new Color(.12f, .15f, .20f); camera.clearFlags = CameraClearFlags.SolidColor;
        var preview = new GameObject("SmallMonsterAnimationPreview").AddComponent<SmallMonsterAnimationPreview>();
        preview.ratPrefab = Prefab("Rat"); preview.wraithPrefab = Prefab("Wraith");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SmallMonsterAnimationPreview.unity");
        AssetDatabase.SaveAssets();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear(); checks.AddRange(SessionState.GetString(Pending + ".checks", "").Split('\n'));
        Time.captureFramerate = 30;
        var target = GameObject.Find("SmallMonsterTarget").GetComponent<PlayerCombatant>(); target.Initialize();
        var harness = new GameObject("SmallMonsterHarness").AddComponent<EnemySpriteAnimation>();
        harness.StartCoroutine(Guard(Exercise(target)));
    }

    static IEnumerator Guard(IEnumerator work)
    {
        while (true)
        {
            bool more;
            try { more = work.MoveNext(); }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            if (!more) break; yield return work.Current;
        }
        Finish(true, null);
    }

    static IEnumerator Exercise(PlayerCombatant target)
    {
        foreach (var name in new[] { "Rat", "Wraith" })
        {
            var root = UnityEngine.Object.Instantiate(Prefab(name), new Vector3(3, 0, 0), Quaternion.identity);
            var actor = root.GetComponent<EnemyCombatant>(); actor.Initialize();
            var view = root.GetComponent<EnemySpriteAnimation>(); var set = view.animations;
            yield return null;
            var visual = root.transform.Find("EnemyAnimationVisual");
            var idle = root.GetComponentInChildren<EnemyIdleSpriteMesh>();
            var renderer = visual.GetComponent<SpriteRenderer>();
            Check(view.CurrentSprite == set.Portrait && idle != null && !renderer.enabled,
                name + ": runtime displays original drawing through the animated mesh");
            Check(renderer.color == Color.white && BattleHudView.PortraitOf(actor) == set.Portrait,
                name + ": renderer and HUD preserve the original artwork");
            Vector3[] first = idle.GetComponent<MeshFilter>().sharedMesh.vertices;
            int idleFrames = name == "Rat" ? 144 : 96;
            for (int frame = 0; frame < idleFrames; frame++)
            {
                Capture(visual.position, 2.85f, name + "/idle/" + frame.ToString("000") + ".png");
                yield return null;
            }
            Check(!Same(first, idle.GetComponent<MeshFilter>().sharedMesh.vertices), name + ": idle breath/cloth advances");
            Vector3 actorPosition = root.transform.position;
            int hits = 0; bool done = false; Vector3 contactPoint = default;
            var attack = view.PlayAttack(target, () => actor != null && !actor.IsDead, () => true, () =>
            {
                hits++;
                contactPoint = visual.position - view.appearance.transform.TransformPoint(view.visualOffset);
            });
            view.StartCoroutine(Complete(attack, () => done = true));
            for (int frame = 0; !done && frame < 60; frame++)
            {
                Capture(new Vector3(1, 0, 0), 3.6f, name + "/attack/" + frame.ToString("000") + ".png", 960, 540);
                yield return null;
            }
            Check(done && hits == 1 && view.CurrentMotion == "Idle", name + ": attack hits once and recovers to idle");
            Check(Mathf.Abs(contactPoint.x + set.attackLungeDistance) < .005f
                && Mathf.Abs(contactPoint.y) < .005f,
                name + ": short horizontal lunge reaches its peak on the contact frame");
            Check(root.transform.position == actorPosition, name + ": attack moves visuals and keeps targeting collider fixed");
            actor.TakeDamage(1);
            Check(view.CurrentMotion == "Hurt", name + ": nonlethal combat damage starts hurt animation");
            for (int frame = 0; frame < set.HurtFrameCount + 2; frame++)
            {
                Capture(visual.position, 2.85f, name + "/hurt/" + frame.ToString("000") + ".png"); yield return null;
            }
            Check(view.CurrentMotion == "Idle", name + ": hurt returns to idle");
            // Interrupt a lunge; death must start from the mesh currently visible on screen.
            int canceledHits = 0;
            view.StartCoroutine(view.PlayAttack(target, () => actor != null && !actor.IsDead, () => true, () => canceledHits++));
            yield return new WaitForSeconds(.1f);
            Vector3[] before = idle.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 beforePosition = visual.position;
            actor.TakeDamage(actor.CurrentHP);
            var ending = UnityEngine.Object.FindObjectOfType<EnemyDeathSpriteMesh>();
            Check(actor.IsDead && ending != null && ending.Drawing == set.Portrait,
                name + ": combat death detaches the same texture immediately");
            Check(Same(before, ending.GetComponent<MeshFilter>().sharedMesh.vertices)
                && ending.transform.root.position == beforePosition,
                name + ": death preserves the interrupted visible pose and position");
            Vector3 capturePosition = ending.transform.root.position;
            int deathFrames = Mathf.CeilToInt((set.artworkDeathFrames / 30f + set.deathHoldSeconds + set.deathAshSeconds) * 30) + 2;
            bool sawAsh = false;
            for (int frame = 0; frame < deathFrames; frame++)
            {
                if (ending != null && ending.transform.root.GetComponent<EnemyBlackAshDissolve>() != null) sawAsh = true;
                Capture(capturePosition, 2.85f, name + "/death/" + frame.ToString("000") + ".png"); yield return null;
            }
            Check(sawAsh && ending == null, name + ": corpse becomes black ash and cleans up after playback");
            Check(canceledHits == 0, name + ": dying during wind-up cancels pending damage");
            // Exercise real action execution rather than only the visual callback.
            var next = UnityEngine.Object.Instantiate(Prefab(name), new Vector3(3, 0, 0), Quaternion.identity);
            var nextActor = next.GetComponent<EnemyCombatant>(); nextActor.Initialize();
            int hp = target.CurrentHP;
            var turn = nextActor.TakeTurn(target);
            while (turn.MoveNext()) yield return turn.Current;
            Check(target.CurrentHP == hp - nextActor.UpcomingAction.Damage,
                name + ": real enemy turn delivers exactly one configured hit through the animation");
            UnityEngine.Object.Destroy(next); yield return null;
        }
    }

    static IEnumerator Complete(IEnumerator work, Action done)
    { while (work.MoveNext()) yield return work.Current; done(); }
    static bool Same(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > .00000001f) return false;
        return true;
    }

    static void Capture(Vector3 position, float size, string file, int width = 640, int height = 640)
    {
        var root = new GameObject("SmallMonsterCapture"); var camera = root.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = size;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .20f);
        camera.transform.position = position + new Vector3(0, 0, -20);
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(root);
    }

    static void Check(bool passed, string text)
    { if (!passed) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("SMALL MONSTER CHECK: " + text); }
    static void Logged(string message, string stack, LogType type)
    { if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception)) Finish(false, message + "\n" + stack); }
    static void Finish(bool passed, string error)
    {
        if (finishing) return; finishing = true; SessionState.SetBool(Pending, false);
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = passed, checks = checks.ToArray(), error = error }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
