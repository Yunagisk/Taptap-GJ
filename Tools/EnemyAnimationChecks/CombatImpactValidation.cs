using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CombatImpactValidation
{
    const string Pending = "CombatImpactValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finishing;
    static int hits;
    static Camera camera;
    static Vector3 renderedOffset;
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../unity-validation.json"));
    [Serializable] class Result { public bool passed; public string[] checks; public string error; }

    static CombatImpactValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        Time.captureFramerate = 30;
        DamageSystem.DirectHit += CountHit;
        new GameObject("ImpactCheckHarness").AddComponent<EnemySpriteAnimation>().StartCoroutine(Guard(Exercise()));
    }
    static void CountHit() => hits++;
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
    static EnemyCombatant Spawn(string name, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleSimulator/" + name + ".prefab");
        Check(prefab != null, name + ": runtime prefab resolves");
        var actor = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<EnemyCombatant>();
        actor.Initialize(); return actor;
    }
    static IEnumerator Exercise()
    {
        camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 3.6f;
        camera.transform.position = new Vector3(1, 0, -20);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .20f);
        var player = new GameObject("Player").AddComponent<PlayerCombatant>(); player.Initialize();
        player.transform.position = new Vector3(-3, 0, 0);
        player.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(.8f, 2f);

        foreach (string name in new[] { "Crow", "Rat", "Wraith", "Sloth" })
        {
            var actor = Spawn(name, new Vector3(3, 0, 0));
            var view = actor.GetComponent<EnemySpriteAnimation>();
            var visual = actor.transform.Find("EnemyAnimationVisual");
            var position = actor.transform.position; var rest = visual.position;
            int before = hits, delivered = 0, frame = 0; bool done = false;
            float peak = 0f, vertical = 0f, hitTravel = 0f;
            view.StartCoroutine(Complete(view.PlayAttack(player, () => true, () => true, () =>
            {
                delivered++; hitTravel = rest.x - visual.position.x;
                DamageSystem.DealDamage(new EnemyAttackInfo(actor, player, 1));
            }), () => done = true));
            while (!done && frame < 80)
            {
                peak = Mathf.Max(peak, rest.x - visual.position.x);
                vertical = Mathf.Max(vertical, Mathf.Abs(rest.y - visual.position.y));
                Capture(name + "/attack/" + frame.ToString("000") + ".png");
                frame++; yield return null;
            }
            Check(done && delivered == 1 && hits == before + 1, name + ": distant attack still hits exactly once");
            Check(Mathf.Abs(hitTravel - .3f) < .002f && peak <= .302f && vertical < .001f,
                name + ": visual lunges only 0.3 world units horizontally at impact");
            Check(actor.transform.position == position && (visual.position - rest).sqrMagnitude < .000001f,
                name + ": actor/collider stay fixed and visual fully recovers");
            UnityEngine.Object.Destroy(actor.gameObject); yield return new WaitForSeconds(.2f);
        }
        Check(camera.GetComponent<BattleCameraShake>() != null, "Damage automatically attaches the effect to the battle camera");

        // Reversed/scaled actors and a vertically separated target use the same world-space step.
        var reversed = Spawn("Rat", new Vector3(-1, 0, 0));
        reversed.transform.localScale *= 2;
        player.transform.position = new Vector3(4, 3, 0);
        var reverseView = reversed.GetComponent<EnemySpriteAnimation>();
        var reverseVisual = reversed.transform.Find("EnemyAnimationVisual"); var reverseRest = reverseVisual.position;
        Vector3 reverseTravel = default;
        yield return reverseView.PlayAttack(player, () => true, () => true, () => reverseTravel = reverseVisual.position - reverseRest);
        Check(Mathf.Abs(reverseTravel.x - .3f) < .002f && Mathf.Abs(reverseTravel.y) < .001f,
            "Mirrored direction and doubled actor scale keep a 0.3-unit horizontal step");
        player.transform.position = reversed.transform.position + Vector3.right * .2f;
        yield return reverseView.PlayAttack(player, () => true, () => true, () => reverseTravel = reverseVisual.position - reverseRest);
        Check(reverseTravel.x > 0 && reverseTravel.x <= .051f, "Nearby targets limit the step instead of being crossed");
        bool alive = true; int canceledHits = 0;
        reverseView.StartCoroutine(reverseView.PlayAttack(player, () => alive, () => true, () => canceledHits++));
        yield return null; alive = false; yield return null;
        Check(canceledHits == 0 && reverseView.CurrentMotion == "Idle" && (reverseVisual.position - reverseRest).sqrMagnitude < .000001f,
            "Canceled wind-up restores the visual and does not produce a hit");
        UnityEngine.Object.Destroy(reversed.gameObject);
        player.transform.position = new Vector3(-3, 0, 0);

        // Exercise production actions rather than invoking only the effect directly.
        var enemy = Spawn("Sloth", new Vector3(3, 0, 0));
        var other = Spawn("Sloth", new Vector3(7, 0, 0));
        var sword = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/KBW01.asset");
        player.EquipWeapon(sword); int count = hits;
        yield return new PlayerAttackAction().Execute(player, enemy);
        Check(hits == count + 1, "Player basic attack produces one impact notification");
        foreach (var id in new[] { "04", "05", "11" })
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/KBW" + id + ".asset");
            player.EquipWeapon(weapon); player.AddDetermination(4); count = hits;
            yield return new SkillAction(new[] { enemy, other }).Execute(player, enemy);
            int expected = id == "04" ? 4 : id == "05" ? 2 : 1;
            Check(hits == count + expected, "Skill " + id + ": combo/area/mixed segment impacts reach the shared effect");
        }

        yield return new WaitForSeconds(.2f);
        var shake = camera.GetComponent<BattleCameraShake>();
        Render(); Check(renderedOffset == Vector3.zero, "Shake settles to exactly zero after its brief duration");
        BattleCameraShake.PlayHit(); Render(); float single = renderedOffset.magnitude;
        float stamp = (float)typeof(BattleCameraShake).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shake);
        for (int i = 0; i < 20; i++) BattleCameraShake.PlayHit();
        Render(); Check(single > 0 && Mathf.Abs(renderedOffset.magnitude - single) < .000001f,
            "Same-frame area hits coalesce without increasing camera displacement");
        Check(single < camera.orthographicSize * .0061f, "Screen shake is capped to a few pixels at normal resolution");
        var basePosition = camera.transform.position;
        for (int i = 0; i < 4; i++)
        { camera.transform.position += Vector3.right * .1f; Render(); yield return null; }
        Check((camera.transform.position - basePosition - Vector3.right * .4f).sqrMagnitude < .000001f,
            "Camera movement during shake does not accumulate offsets");
        BattleCameraShake.PlayHit();
        Camera.CameraCallback disable = current => { if (current == camera) shake.enabled = false; };
        Camera.onPreRender += disable; Render(); Camera.onPreRender -= disable;
        Check(camera.transform.position.z == -20, "Disabling the effect mid-render restores the camera");
        shake.enabled = true; yield return new WaitForSeconds(.2f);
        count = hits; enemy.LoseRaw(1); Render();
        Check(hits == count && renderedOffset == Vector3.zero, "Damage-over-time/raw health loss does not cause attack shake");
        var resistance = enemy.GetComponent<DamageResistance>();
        if (resistance == null) resistance = enemy.gameObject.AddComponent<DamageResistance>();
        resistance.SetStageRules(new[] { new DamageResistance.DamageRule
            { damageType = PlayerDamageType.Pierce, damageReaction = DamageReaction.Immune } });
        DamageSystem.DealDamage(new PlayerAttackInfo(player, enemy, new[] { new DamageComponent(PlayerDamageType.Pierce, 10) }));
        Render(); Check(hits == count && renderedOffset == Vector3.zero, "Immune zero-damage hits do not trigger a false impact");
        DamageSystem.DealDamage(new PlayerAttackInfo(player, enemy, new[] { new DamageComponent(PlayerDamageType.Blunt, 9999) }));
        Render(); Check(hits == count + 1 && renderedOffset.sqrMagnitude > 0, "Lethal direct damage still triggers its impact");
        camera.gameObject.SetActive(false);
        var replacement = new GameObject("Replacement Camera").AddComponent<Camera>(); replacement.tag = "MainCamera";
        DamageSystem.DealDamage(new EnemyAttackInfo(other, player, 1));
        Check(replacement.GetComponent<BattleCameraShake>() != null, "A replacement main camera receives later battle impacts");
    }
    static IEnumerator Complete(IEnumerator work, Action done)
    { while (work.MoveNext()) yield return work.Current; done(); }
    static void Render()
    {
        Vector3 baseline = camera.transform.position;
        Camera.CameraCallback inspect = current => { if (current == camera) renderedOffset = current.transform.position - baseline; };
        Camera.onPreRender += inspect; camera.Render(); Camera.onPreRender -= inspect;
        Check((camera.transform.position - baseline).sqrMagnitude < .00000001f, "Render restores the camera position");
    }
    static void Capture(string relative)
    {
        var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt);
    }
    static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
        if (!checks.Contains(message)) checks.Add(message);
        Debug.Log("IMPACT CHECK: " + message);
    }
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
