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
        var currentSet = AssetDatabase.LoadAssetAtPath<EnemySpriteAnimationSet>("Assets/Data/EnemyAnimations/FuYaAnimations.asset");
        if (currentSet != null && currentSet.hurtAndAshOnDeath) { EnemyHurtAshValidation.Run(); return; }
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this validator in its isolated batch project.");
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<EnemySpriteAnimationSet>("Assets/Data/EnemyAnimations/FuYaAnimations.asset");
            Check(set != null && set.idleFrames.Length == 30 && set.attackFrames.Length == 18 && set.hurtFrames.Length == 9
                && set.HasDeath && set.deathFrames.Length == 24,
                "Unity imports all 81 authored sprite references including 24 death drawings");
            var ashShader = set.deathAshMaterial != null ? set.deathAshMaterial.shader : null;
            if (ashShader != null && (!ashShader.isSupported || ShaderUtil.ShaderHasError(ashShader)))
                throw new InvalidOperationException("Black ash shader: " + ashShader.name + ", supported=" + ashShader.isSupported
                    + "; " + string.Join("; ", Array.ConvertAll(ShaderUtil.GetShaderMessages(ashShader), m => m.message + " at " + m.line)));
            Check(set.deathAshMaterial != null && ashShader != null && ashShader.isSupported
                && !ShaderUtil.ShaderHasError(ashShader) && set.deathAshSeconds > 0f,
                "black-ash material resolves to a supported shader");
            Check(set.useDeformedDeath && set.preserveArtworkColors && set.artworkMaterial != null && set.portraitIcon != null,
                "crow uses continuous death, reference tone and the original portrait");
            var artworkShader = set.artworkMaterial.shader;
            if (ShaderUtil.ShaderHasError(artworkShader))
                throw new InvalidOperationException(string.Join("; ", Array.ConvertAll(ShaderUtil.GetShaderMessages(artworkShader), m => m.message)));
            Check(artworkShader.isSupported && Mathf.Abs(set.artworkMaterial.GetFloat("_ArtworkGamma") - 1.45f) < .001f,
                "reference artwork tone material compiles");
            Sprite source = null;
            Vector3 scale = Vector3.one, offset = Vector3.zero;
            foreach (string scene in new[] { "CombatScene", "CombatAnimationPreview", "HeavyAnimationPreview", "TestScene" })
            {
                var loaded = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                EnemySpriteAnimation view = null;
                foreach (var root in loaded.GetRootGameObjects())
                    if (root.GetComponent<EnemySpriteAnimation>() != null
                        && root.GetComponent<EnemySpriteAnimation>().animations == set) view = root.GetComponent<EnemySpriteAnimation>();
                Check(view != null && view.animations == set && view.appearance != null, scene + " has a configured animation component");
                Check(view.appearance.color == Color.white, scene + " has neutral artwork color");
                if (scene == "CombatScene") { source = view.appearance.sprite; scale = view.visualScale; offset = view.visualOffset; }
            }
            Check(source != null, "production crow sprite resolves in Unity");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var enemy = new GameObject("AnimationCheckEnemy");
            enemy.SetActive(false);
            enemy.transform.position = new Vector3(5, 0, 0);
            var renderer = enemy.AddComponent<SpriteRenderer>(); renderer.sprite = source;
            renderer.color = Color.white;
            enemy.AddComponent<EnemyCombatant>();
            var animation = enemy.AddComponent<EnemySpriteAnimation>();
            animation.animations = set; animation.appearance = renderer;
            animation.visualScale = scale; animation.visualOffset = offset;
            enemy.SetActive(true);
            CaptureAt(enemy.transform.position + offset, set.Portrait.rect.height / set.Portrait.pixelsPerUnit * scale.y * .51f,
                "original-art.png");
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
        Check(BattleHudView.PortraitOf(enemy) == set.portraitIcon, "HUD uses the original illustration");
        Capture(enemy.transform.Find("EnemyAnimationVisual").gameObject, "idle.png");
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
                    Capture(enemy.transform.Find("EnemyAnimationVisual").gameObject, "attack-contact.png");
                    var visual = enemy.transform.Find("EnemyAnimationVisual");
                    Vector3 rest = view.appearance.transform.TransformPoint(view.visualOffset);
                    Check(Mathf.Abs(visual.position.x - rest.x + set.attackLungeDistance) < .01f
                        && Mathf.Abs(visual.position.y - rest.y) < .001f,
                        "crow takes a short horizontal step on the contact frame");
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
        Capture(enemy.transform.Find("EnemyAnimationVisual").gameObject, "hurt.png");
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
        yield return new WaitForSeconds(.23f);
        Vector3 visiblePosition = enemy.transform.Find("EnemyAnimationVisual").position;
        Color tint = view.appearance.color;
        Sprite deathDrawing = view.CurrentSprite;
        float paintedFootY = (set.artworkBounds.y * set.Portrait.rect.height - set.Portrait.pivot.y) / set.Portrait.pixelsPerUnit;
        float expectedGroundY = view.appearance.transform.TransformPoint(view.visualOffset
            + new Vector3(0f, paintedFootY * view.visualScale.y, 0f)).y;
        enemy.TakeDamage(1000);
        var ending = GameObject.Find("DefeatedEnemyVisual");
        var deathMesh = ending != null ? ending.GetComponentInChildren<EnemyDeathSpriteMesh>() : null;
        Check(deathMesh != null && deathMesh.Drawing == deathDrawing && deathMesh.Progress == 0f,
            "death starts from the exact visible drawing without a pose swap");
        Check(Vector3.Distance(ending.transform.position, visiblePosition) < .001f,
            "death keeps the current visible position even during a lunge");
        view.PlayHurt(); view.PlayDefeat();
        Check(ending.GetComponent<SpriteRenderer>().sprite == deathDrawing,
            "repeated reactions cannot interrupt or restart death");
        Check(ending.GetComponent<SpriteRenderer>().color == tint,
            "death preserves the scene tint");
        GameObject.Find("AnimationCheckHarness").GetComponent<EnemySpriteAnimation>()
            .StartCoroutine(CapturePlayback(ending, "death-from-attack"));
        Capture(ending, "death-start.png");
        yield return new WaitForSeconds(.08f);
        Check(enemy == null && ending != null && deathMesh.Progress > 0f
            && player2.CurrentHP == 100 && deaths == 1, "logical death is immediate, fires once and cancels the pending attack");
        yield return new WaitForSeconds(.32f);
        Capture(ending, "death-collapse.png");
        Check(ending != null && deathMesh.Progress > .2f && deathMesh.Progress < .8f
            && ending.GetComponent<SpriteRenderer>().sprite == deathDrawing,
            "collapse advances smoothly while keeping one drawing");
        yield return new WaitForSeconds(.36f);
        Check(ending != null && deathMesh.Progress == 1f
            && ending.GetComponent<SpriteRenderer>().color.a == tint.a,
            "dead pose remains fully visible for its configured hold");
        Capture(ending, "death-rest.png");
        Vector3 actualGround = deathMesh.transform.TransformPoint(deathMesh.Point(new Vector2(.5f, set.artworkBounds.y)));
        Check(Mathf.Abs(actualGround.y - expectedGroundY) < .001f,
            "corpse lands on the painted foot line instead of the transparent canvas edge");
        Vector3[] corpseVertices = deathMesh.GetComponent<MeshFilter>().sharedMesh.vertices;
        yield return new WaitForSeconds(.2f);
        var ash = ending != null ? ending.GetComponent<EnemyBlackAshDissolve>() : null;
        Check(ash != null && ash.Progress > 0f && ash.Progress < .3f
            && ending.GetComponent<SpriteRenderer>().color == tint,
            "after the death hold the corpse blackens without a whole-sprite alpha fade");
        Check(SameVertices(corpseVertices, deathMesh.GetComponent<MeshFilter>().sharedMesh.vertices),
            "the held corpse does not change shape when ash begins");
        Capture(ending, "ash-blackening.png");
        yield return new WaitForSeconds(.25f);
        var dust = ending.transform.Find("BlackAshDust");
        int grainVertices = dust != null ? dust.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;
        Check(dust != null && dust.GetComponent<MeshRenderer>().enabled
            && grainVertices >= 30000 && grainVertices < 65535,
            "disintegration emits a fine grid of sprite-masked black grains");
        Check(ash.Progress > .3f && ash.Progress < .7f
            && !ShaderUtil.ShaderHasError(set.deathAshMaterial.shader),
            "corpse dissolution and grain motion advance with a valid shader");
        Capture(ending, "ash-shedding.png");
        yield return new WaitForSeconds(.4f);
        Check(ending != null && ash.Progress > .65f, "drifting powder outlives the disappearing corpse");
        Capture(ending, "ash-drifting.png");
        yield return new WaitForSeconds(.4f);
        Check(ending == null && player2.CurrentHP == 100 && deaths == 1,
            "death and black-ash visuals clean up together without another hit");

        var resultRoot = new GameObject("DeathValidationResultUI");
        var flow = resultRoot.AddComponent<BattleFlowController>(); flow.enabled = false;
        resultRoot.AddComponent<BattleResultUI>();
        var resultCanvas = resultRoot.transform.Find("BattleResultCanvas").gameObject;
        var poisoned = CreateEnemy(set, new Color(.7057624f, .8867924f, .6567283f, 1f));
        Check(poisoned.transform.Find("EnemyAnimationVisual").GetComponent<SpriteRenderer>().color == Color.white,
            "legacy green renderer tint cannot recolor the original-art palette");
        Vector3[] idleBeforeDeath = poisoned.GetComponentInChildren<EnemyIdleSpriteMesh>().GetComponent<MeshFilter>().sharedMesh.vertices;
        poisoned.LoseRaw(1000);
        ending = GameObject.Find("DefeatedEnemyVisual");
        var idleDeath = ending != null ? ending.GetComponentInChildren<EnemyDeathSpriteMesh>() : null;
        Check(idleDeath != null && SameVertices(idleBeforeDeath, idleDeath.GetComponent<MeshFilter>().sharedMesh.vertices),
            "idle death continues the current breathing pose without a first-frame jump");
        GameObject.Find("AnimationCheckHarness").GetComponent<EnemySpriteAnimation>()
            .StartCoroutine(CapturePlayback(ending, "frames"));
        SetState(flow, BattleState.Victory);
        Check(!resultCanvas.activeSelf, "victory overlay waits for the last enemy's death animation");
        yield return new WaitForSeconds(.5f);
        Check(ending != null && !resultCanvas.activeSelf, "victory does not obscure the collapse mid-animation");
        yield return new WaitForSeconds(.8f);
        Check(ending != null && !resultCanvas.activeSelf,
            "victory continues to wait while the black powder disperses");
        yield return new WaitForSeconds(.8f);
        Check(ending == null, "status-death visual also cleans itself up");
        Check(resultCanvas.activeSelf, "victory overlay appears after death playback finishes");
        SetState(flow, BattleState.PlayerTurn);
        SetState(flow, BattleState.Victory);
        Check(resultCanvas.activeSelf, "victory without a death visual still shows immediately");

        var fallback = UnityEngine.Object.Instantiate(set);
        fallback.deathFrames = null;
        var legacy = CreateEnemy(fallback, tint);
        legacy.TakeDamage(1000);
        ending = GameObject.Find("DefeatedEnemyVisual");
        Check(ending != null && ending.GetComponent<SpriteRenderer>().sprite == set.hurtFrames[0],
            "sets without death drawings keep the existing hurt fallback");
        SetState(flow, BattleState.PlayerTurn);
        SetState(flow, BattleState.Defeat);
        Check(resultCanvas.activeSelf, "player defeat remains immediate even with an enemy death visual");
        yield return new WaitForSeconds(.4f);
        Check(ending == null, "legacy fallback keeps its original short lifetime");
        UnityEngine.Object.Destroy(fallback);
        UnityEngine.Object.Destroy(resultRoot);
    }

    static void SetState(BattleFlowController flow, BattleState state)
    {
        typeof(BattleFlowController).GetMethod("SetState", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic).Invoke(flow, new object[] { state });
    }

    static bool SameVertices(Vector3[] left, Vector3[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if ((left[i] - right[i]).sqrMagnitude > .00000001f) return false;
        return true;
    }

    static EnemyCombatant CreateEnemy(EnemySpriteAnimationSet set, Color tint)
    {
        var root = new GameObject("DeathValidationEnemy"); root.SetActive(false);
        var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = set.Portrait; renderer.color = tint;
        var enemy = root.AddComponent<EnemyCombatant>();
        var view = root.AddComponent<EnemySpriteAnimation>(); view.animations = set; view.appearance = renderer;
        root.SetActive(true); enemy.Initialize(); return enemy;
    }

    static void Capture(GameObject visual, string file)
    {
        var renderer = visual.GetComponent<SpriteRenderer>();
        float size = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit * visual.transform.lossyScale.y * .51f;
        CaptureAt(visual.transform.position, size, file);
    }

    static IEnumerator CapturePlayback(GameObject visual, string folder)
    {
        var renderer = visual.GetComponent<SpriteRenderer>();
        Vector3 position = visual.transform.position;
        float size = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit * visual.transform.lossyScale.y * .51f;
        int frame = 0;
        Sprite drawing = renderer.sprite;
        bool sameDrawing = true;
        while (visual != null && frame < 120)
        {
            sameDrawing &= renderer.sprite == drawing;
            CaptureAt(position, size, folder + "/frame-" + frame.ToString("000") + ".png");
            frame++;
            yield return null;
        }
        Check(sameDrawing, folder + " retains the same texture for the entire death and ash sequence");
        CaptureAt(position, size, folder + "/frame-" + frame.ToString("000") + ".png");
    }

    static void CaptureAt(Vector3 position, float size, string file)
    {
        var root = new GameObject("DeathCaptureCamera"); var camera = root.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(49f / 255, 77f / 255, 121f / 255);
        camera.orthographicSize = size;
        camera.transform.position = position + new Vector3(0, 0, -20);
        var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(root);
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
