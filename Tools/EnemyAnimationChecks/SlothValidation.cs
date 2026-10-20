// Native playback and render checks in an isolated project; never added to gameplay.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SlothValidation
{
    const string Pending = "SlothValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finishing;
    [Serializable] class Result { public bool passed; public string[] checks; public string error; }
    static string Output
    {
        get
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-slothResult");
            return i >= 0 ? args[i + 1] : Path.Combine(Application.dataPath, "../sloth-result.json");
        }
    }
    static SlothValidation()
    { EditorApplication.playModeStateChanged += Changed; Application.logMessageReceived += Logged; }
    static EnemySpriteAnimationSet Set(string name) => AssetDatabase.LoadAssetAtPath<EnemySpriteAnimationSet>(
        "Assets/Data/EnemyAnimations/" + name + "Animations.asset");
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab");
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch project.");
        try
        {
            var set = Set("Sloth");
            Check(set != null && set.framesPerSecond == 30 && set.artworkProfile == EnemyArtworkProfile.Sloth,
                "Sloth imports the same continuous 30 FPS system as small monsters");
            Check(set.AttackFrameCount == 36 && set.HurtFrameCount == 18 && set.artworkDeathFrames == 42
                && Mathf.RoundToInt(set.idleCycleSeconds * set.framesPerSecond) == 144,
                "144-pose idle, 36-frame attack, 18-frame hurt and 42-frame death configured");
            Check(set.Portrait == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Enemies/懒惰.png")
                && set.preserveArtworkColors && set.artworkMaterial.GetFloat("_ArtworkGamma") == 1,
                "Original Sloth drawing and ungraded palette are preserved");
            Check(set.artworkMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(set.artworkMaterial.shader)
                && set.deathAshMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(set.deathAshMaterial.shader),
                "Original-art and black-ash shaders compile");
            foreach (var path in new[] { "Assets/Prefabs/Enemies/Sloth.prefab", "Assets/Prefabs/BattleSimulator/Sloth.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null && prefab.GetComponent<EnemyCombatant>() != null
                    && prefab.GetComponent<EnemySpriteAnimation>().animations == set, path + " binds the new animation set");
                var data = new SerializedObject(prefab.GetComponent<EnemyCombatant>());
                Check(data.FindProperty("maxHp").intValue == 300 && data.FindProperty("isBoss").boolValue,
                    path + " preserves existing combat values");
            }
            var test = EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
            Check(test.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemySpriteAnimation>(true))
                .Count(view => view.animations == set) == 1, "TestScene resolves exactly one Sloth animation component");
            BuildReviewScene();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var target = new GameObject("SlothCheckTarget"); target.transform.position = new Vector3(-3, 0, 0);
            target.AddComponent<BoxCollider2D>().size = new Vector2(.8f, 2);
            target.AddComponent<PlayerCombatant>();
            SessionState.SetString(Pending + ".checks", string.Join("\n", checks));
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void BuildReviewScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 4;
        camera.transform.position = new Vector3(0, .4f, -20);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .20f);
        var preview = new GameObject("SlothAnimationPreview").AddComponent<SmallMonsterAnimationPreview>();
        preview.ratPrefab = Prefab("Rat"); preview.wraithPrefab = Prefab("Wraith"); preview.slothPrefab = Prefab("Sloth");
        var data = new SerializedObject(preview); data.FindProperty("species").intValue = 2; data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SlothAnimationPreview.unity");
        AssetDatabase.SaveAssets();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear(); checks.AddRange(SessionState.GetString(Pending + ".checks", "").Split('\n'));
        Time.captureFramerate = 30;
        var target = GameObject.Find("SlothCheckTarget").GetComponent<PlayerCombatant>(); target.Initialize();
        var harness = new GameObject("SlothHarness").AddComponent<EnemySpriteAnimation>();
        harness.StartCoroutine(Guard(Exercise(target)));
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
    static IEnumerator Exercise(PlayerCombatant target)
    {
        var root = UnityEngine.Object.Instantiate(Prefab("Sloth"), new Vector3(3, 0, 0), Quaternion.identity);
        var actor = root.GetComponent<EnemyCombatant>(); actor.Initialize();
        var view = root.GetComponent<EnemySpriteAnimation>(); var set = view.animations;
        yield return null;
        var visual = root.transform.Find("EnemyAnimationVisual");
        var idle = root.GetComponentInChildren<EnemyIdleSpriteMesh>();
        Check(idle != null && !root.GetComponent<Animator>().enabled && !root.GetComponent<SpriteRenderer>().enabled,
            "Old 7-frame Animator is disabled; the original-art mesh owns playback");
        Check(BattleHudView.PortraitOf(actor) == set.Portrait, "HUD uses the same original Sloth drawing");
        float paintedHeight = (set.artworkBounds.w - set.artworkBounds.y) * set.Portrait.rect.height
            / set.Portrait.pixelsPerUnit * visual.lossyScale.y;
        Check(Mathf.Abs(paintedHeight - root.GetComponent<BoxCollider2D>().bounds.size.y) < .025f,
            "Original-art animation preserves the old 3.3-unit painted height and collider alignment");
        Vector3 position = root.transform.position;
        Vector3[] previous = null;
        int unique = 0;
        for (int frame = 0; frame < 144; frame++)
        {
            var current = idle.GetComponent<MeshFilter>().sharedMesh.vertices;
            if (previous == null || !Same(previous, current)) unique++;
            previous = current;
            Capture(visual.position, 2.7f, "idle/" + frame.ToString("000") + ".png"); yield return null;
        }
        Check(unique >= 140, "At least 140 distinct mesh poses in the 144-frame idle cycle");
        Check(root.transform.position == position, "Idle animation keeps the actor and targeting collider fixed");
        int hits = 0; bool done = false; Vector3 contact = default;
        var attack = view.PlayAttack(target, () => actor != null && !actor.IsDead, () => true, () =>
        {
            hits++;
            contact = visual.position - view.appearance.transform.TransformPoint(view.visualOffset);
        });
        view.StartCoroutine(Complete(attack, () => done = true));
        for (int frame = 0; !done && frame < 70; frame++)
        { Capture(new Vector3(1, 0, 0), 3.6f, "attack/" + frame.ToString("000") + ".png", 960, 540); yield return null; }
        Check(done && hits == 1 && view.CurrentMotion == "Idle", "Heavy claw swing hits once then recovers to idle");
        Check(Mathf.Abs(contact.x + set.attackLungeDistance) < .005f && Mathf.Abs(contact.y) < .005f,
            "Sloth takes a short horizontal step on the exact contact frame");
        Check(root.transform.position == position, "Attack moves only the visual; actor and collider stay fixed");
        actor.TakeDamage(1); Check(view.CurrentMotion == "Hurt", "Combat damage triggers Sloth recoil");
        for (int frame = 0; frame < set.HurtFrameCount + 3; frame++)
        { Capture(visual.position, 2.7f, "hurt/" + frame.ToString("000") + ".png"); yield return null; }
        Check(view.CurrentMotion == "Idle", "Hurt recovers to idle");
        // Record the readable full collapse before separately testing interruption safety.
        var visible = idle.GetComponent<MeshFilter>().sharedMesh.vertices;
        actor.TakeDamage(actor.CurrentHP);
        var ending = UnityEngine.Object.FindObjectOfType<EnemyDeathSpriteMesh>();
        Check(ending != null && Same(visible, ending.GetComponent<MeshFilter>().sharedMesh.vertices),
            "Death starts from the exact visible mesh, without replacing the drawing");
        Vector3 capturePosition = ending.transform.root.position;
        bool sawAsh = false;
        int deathFrames = Mathf.CeilToInt((set.artworkDeathFrames / 30f + set.deathHoldSeconds + set.deathAshSeconds) * 30) + 3;
        for (int frame = 0; frame < deathFrames; frame++)
        {
            if (ending != null && ending.transform.root.GetComponent<EnemyBlackAshDissolve>() != null) sawAsh = true;
            Capture(capturePosition, 2.7f, "death/" + frame.ToString("000") + ".png"); yield return null;
        }
        Check(sawAsh && ending == null, "Collapse holds on the ground, turns to black ash, then cleans up");
        var next = UnityEngine.Object.Instantiate(Prefab("Sloth"), new Vector3(3, 0, 0), Quaternion.identity);
        var nextActor = next.GetComponent<EnemyCombatant>(); nextActor.Initialize();
        var nextView = next.GetComponent<EnemySpriteAnimation>();
        int hp = target.CurrentHP, damage = nextActor.UpcomingAction.Damage; var turn = nextActor.TakeTurn(target);
        while (turn.MoveNext()) yield return turn.Current;
        Check(target.CurrentHP == hp - damage, "Real Sloth turn delivers its configured damage exactly once");
        int canceled = 0; nextView.StartCoroutine(nextView.PlayAttack(target, () => nextActor != null && !nextActor.IsDead, () => true, () => canceled++));
        yield return new WaitForSeconds(.1f);
        var beforeMesh = next.GetComponentInChildren<EnemyIdleSpriteMesh>().GetComponent<MeshFilter>().sharedMesh.vertices;
        var beforePosition = next.transform.Find("EnemyAnimationVisual").position;
        nextActor.TakeDamage(nextActor.CurrentHP);
        var interrupted = UnityEngine.Object.FindObjectOfType<EnemyDeathSpriteMesh>();
        Check(interrupted != null && Same(beforeMesh, interrupted.GetComponent<MeshFilter>().sharedMesh.vertices)
            && interrupted.transform.root.position == beforePosition, "Death during wind-up preserves pose and position");
        yield return new WaitForSeconds(3);
        Check(canceled == 0, "Death during wind-up cancels pending claw damage");
        foreach (string name in new[] { "Rat", "Wraith" })
        {
            var peer = UnityEngine.Object.Instantiate(Prefab(name), new Vector3(3, 0, 0), Quaternion.identity);
            var peerActor = peer.GetComponent<EnemyCombatant>(); peerActor.Initialize();
            var peerView = peer.GetComponent<EnemySpriteAnimation>();
            int peerHits = 0;
            yield return peerView.PlayAttack(target, () => peerActor != null && !peerActor.IsDead, () => true, () => peerHits++);
            Check(peerHits == 1 && peerView.CurrentMotion == "Idle", name + " existing attack and recovery still work");
            UnityEngine.Object.Destroy(peer); yield return null;
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
        var root = new GameObject("SlothCapture"); var camera = root.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = size;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .20f);
        camera.transform.position = position + new Vector3(0, 0, -20);
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(root);
    }
    static void Check(bool passed, string text)
    { if (!passed) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("SLOTH CHECK: " + text); }
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
