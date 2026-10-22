using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EnemyHurtAshValidation
{
    const string Pending = "EnemyHurtAshValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finishing;
    [Serializable] class Result { public bool passed; public string[] checks; public string error; }
    static string Output
    {
        get
        {
            var args = Environment.GetCommandLineArgs();
            foreach (string option in new[] { "-deathResult", "-smallResult", "-fuyaResult" })
            { int i = Array.IndexOf(args, option); if (i >= 0) return args[i + 1]; }
            return Path.Combine(Application.dataPath, "../hurt-ash-validation.json");
        }
    }
    static EnemyHurtAshValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleSimulator/" + name + ".prefab");
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Unity project.");
        try
        {
            foreach (string name in new[] { "Crow", "Rat", "Wraith" })
            {
                var prefab = Prefab(name); var set = prefab.GetComponent<EnemySpriteAnimation>().animations;
                Check(set.hurtAndAshOnDeath && set.HasHurt && set.HasDeath && !set.useDeformedDeath && set.deathHoldSeconds == 0,
                    name + ": configured for hurt-and-ash with no collapse or hold");
                Check(set.deathAshMaterial != null && set.deathAshSeconds > 0
                    && !ShaderUtil.ShaderHasError(set.deathAshMaterial.shader), name + ": ash material compiles");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true); SessionState.SetString(Pending + ".checks", string.Join("\n", checks));
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear(); checks.AddRange(SessionState.GetString(Pending + ".checks", "").Split('\n'));
        Time.captureFramerate = 30;
        new GameObject("HurtAshCheckHarness").AddComponent<EnemySpriteAnimation>().StartCoroutine(Guard(Exercise()));
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

    static EnemyCombatant Spawn(string name, Vector3 position)
    {
        var actor = UnityEngine.Object.Instantiate(Prefab(name), position, Quaternion.identity).GetComponent<EnemyCombatant>();
        actor.Initialize(); return actor;
    }
    static IEnumerator Exercise()
    {
        foreach (string name in new[] { "Crow", "Rat", "Wraith" })
        {
            var actor = Spawn(name, Vector3.zero); var view = actor.GetComponent<EnemySpriteAnimation>(); var set = view.animations;
            var control = Spawn(name, new Vector3(100, 0, 0)); var controlView = control.GetComponent<EnemySpriteAnimation>();
            yield return null;
            var visual = actor.transform.Find("EnemyAnimationVisual"); var source = visual.GetComponent<SpriteRenderer>();
            var controlSource = control.transform.Find("EnemyAnimationVisual").GetComponent<SpriteRenderer>();
            var mesh = actor.GetComponentInChildren<EnemyIdleSpriteMesh>();
            Vector3[] previous = mesh.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 position = visual.position; float size = set.Portrait.rect.height / set.Portrait.pixelsPerUnit * visual.lossyScale.y * .55f;
            controlView.PlayHurt();
            actor.TakeDamage(actor.CurrentHP);
            var ending = UnityEngine.Object.FindObjectOfType<EnemyHurtAshPlayback>(); var ash = ending.GetComponent<EnemyBlackAshDissolve>();
            Check(ending != null && ash != null && ending.Elapsed == 0 && ash.Progress == 0,
                name + ": hurt playback and ash exist on the lethal-hit frame");
            Check(ending.transform.position == position && UnityEngine.Object.FindObjectsOfType<EnemyDeathSpriteMesh>().Length == 0,
                name + ": death keeps its location and never creates a flattening mesh");
            if (set.UsesArtworkMotion)
                Check(Same(previous, mesh.GetComponent<MeshFilter>().sharedMesh.vertices), name + ": death begins from the exact visible pose");
            else Check(source.sprite == set.hurtFrames[0], name + ": crow starts its authored hurt drawing");
            actor.TakeDamage(999);
            Check(UnityEngine.Object.FindObjectsOfType<EnemyHurtAshPlayback>().Length == 1,
                name + ": repeated lethal damage cannot create duplicate playback");

            // Drive the ordinary victory observer; it must wait for the new playback type.
            var resultRoot = new GameObject("ResultCheck"); resultRoot.SetActive(false);
            var flow = resultRoot.AddComponent<BattleFlowController>(); flow.enabled = false;
            var result = resultRoot.AddComponent<BattleResultUI>();
            var serialized = new SerializedObject(result); serialized.FindProperty("font").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Zhuque SDF.asset"); serialized.ApplyModifiedPropertiesWithoutUndo();
            resultRoot.SetActive(true);
            typeof(BattleFlowController).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(flow, new object[] { BattleState.Victory });
            var overlay = resultRoot.transform.Find("BattleResultCanvas").gameObject;
            Check(!overlay.activeSelf, name + ": victory waits while hurt and ash play together");
            bool overlaps = false, matchesHurt = true, followsTexture = true, staysAtLocation = true, waitsUntilEnd = true;
            int frame = 0, count = Mathf.CeilToInt(Mathf.Max(ending.HurtSeconds, set.deathAshSeconds) * 30) + 3;
            while (frame < count)
            {
                if (ending != null)
                {
                    overlaps |= ending.IsPlayingHurt && ending.Elapsed > 0 && ash.Progress > 0;
                    staysAtLocation &= ending.transform.position == position;
                    waitsUntilEnd &= !overlay.activeSelf;
                    if (ending.Elapsed < ending.HurtSeconds - .034f)
                    {
                        if (set.UsesArtworkMotion) matchesHurt &= Same(mesh.GetComponent<MeshFilter>().sharedMesh.vertices,
                            control.GetComponentInChildren<EnemyIdleSpriteMesh>().GetComponent<MeshFilter>().sharedMesh.vertices);
                        else
                        {
                            matchesHurt &= source.sprite == controlSource.sprite;
                            var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block);
                            followsTexture &= block.GetTexture("_MainTex") == source.sprite.texture;
                        }
                    }
                }
                Capture(position, size, name + "/" + frame.ToString("000") + ".png");
                yield return null; frame++;
            }
            Check(overlaps && matchesHurt, name + ": real nonlethal and lethal hurt animations match while ash is progressing");
            Check(followsTexture, name + ": ash shading follows the current hurt drawing without freezing the first texture");
            Check(staysAtLocation && waitsUntilEnd && ending == null && overlay.activeSelf,
                name + ": effect cleans up and only then releases victory");
            Check(actor == null, name + ": logical enemy is removed independently of the visual effect");
            UnityEngine.Object.Destroy(control.gameObject); UnityEngine.Object.Destroy(resultRoot); yield return null;
        }

        var targetRoot = new GameObject("InterruptTarget"); targetRoot.transform.position = new Vector3(-5, 0, 0);
        var target = targetRoot.AddComponent<PlayerCombatant>(); targetRoot.AddComponent<BoxCollider2D>(); target.Initialize();
        foreach (string name in new[] { "Crow", "Rat", "Wraith" })
        {
            var actor = Spawn(name, new Vector3(3, 0, 0)); var view = actor.GetComponent<EnemySpriteAnimation>();
            int hits = 0; view.StartCoroutine(view.PlayAttack(target, () => actor != null && !actor.IsDead, () => true, () => hits++));
            yield return new WaitForSeconds(view.animations.attackContactFrame / 30f * .75f);
            Vector3 position = actor.transform.Find("EnemyAnimationVisual").position;
            actor.LoseRaw(actor.CurrentHP);
            var ending = UnityEngine.Object.FindObjectOfType<EnemyHurtAshPlayback>();
            Check(ending != null && ending.transform.position == position && ending.GetComponent<EnemyBlackAshDissolve>() != null,
                name + ": status death during attack starts hurt and ash in place");
            yield return new WaitForSeconds(1.15f);
            Check(hits == 0 && ending == null, name + ": lethal interruption cancels the pending attack and finishes ash");
        }
        UnityEngine.Object.Destroy(targetRoot); yield return null;

        var simulatorRoot = new GameObject("SimulatorCheck"); simulatorRoot.SetActive(false);
        var simulator = simulatorRoot.AddComponent<BattleSimulator>();
        simulator.roundPrefab = AssetDatabase.LoadAssetAtPath<BattleSimulatorRound>("Assets/Prefabs/BattleSimulator/Round.prefab");
        simulator.weaponLibrary = AssetDatabase.LoadAssetAtPath<WeaponLibrary>("Assets/Data/Weapons/WeaponLibrary.asset");
        simulator.hudPrefab = AssetDatabase.LoadAssetAtPath<BattleHudView>("Assets/Prefabs/UI/CombatHUD.prefab"); simulator.font = simulator.hudPrefab.healthText.font;
        simulator.enemies = new[] { "Crow", "Rat", "Wraith" }.Select(name => new BattleSimulator.EnemyOption { label = name, prefab = Prefab(name) }).ToArray();
        simulatorRoot.SetActive(true); yield return null; yield return null; yield return null;
        foreach (int index in new[] { 0, 1, 2 })
        {
            simulator.SelectEnemy(index); yield return null; yield return null;
            simulator.DefeatEnemy(); yield return null;
            Check(UnityEngine.Object.FindObjectsOfType<EnemyHurtAshPlayback>().Length == 1, "simulator enemy " + index + ": death button uses the new playback");
            simulator.NextEnemy(); yield return null; yield return null;
            Check(simulator.Ready && UnityEngine.Object.FindObjectsOfType<EnemyHurtAshPlayback>().Length == 0
                && UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1,
                "simulator enemy " + index + ": switching removes the old ash and leaves one enemy");
        }
        UnityEngine.Object.Destroy(simulatorRoot); yield return null; yield return null;
    }

    static bool Same(Vector3[] a, Vector3[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > .000001f) return false; return true; }
    static void Capture(Vector3 position, float size, string file)
    {
        var go = new GameObject("HurtAshCapture"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = size; camera.cullingMask = ~(1 << 5);
        camera.transform.position = position + new Vector3(0, 0, -20); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(34, 41, 52, 255);
        var rt = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
    }
    static void Check(bool pass, string text)
    { if (!pass) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("HURT ASH CHECK: " + text); }
    static void Logged(string message, string stack, LogType type)
    { if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception)) Finish(false, message + "\n" + stack); }
    static void Finish(bool pass, string error)
    {
        if (finishing) return; finishing = true; SessionState.SetBool(Pending, false); Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = pass, checks = checks.ToArray(), error = error }, true)); EditorApplication.Exit(pass ? 0 : 1);
    }
}
