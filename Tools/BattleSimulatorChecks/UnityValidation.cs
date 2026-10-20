using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleSimulatorValidation
{
    const string Pending = "BattleSimulatorValidation.Pending";
    const string Folder = "Assets/Prefabs/BattleSimulator";
    static readonly List<string> checks = new List<string>();
    static bool finishing;
    [Serializable] class Result { public bool passed; public string[] checks; public string error; }
    static string Output
    {
        get { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-simResult"); return i < 0 ? "simulator-validation.json" : a[i + 1]; }
    }
    static BattleSimulatorValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated validation project.");
        try
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/CombatScene.unity");
            var sourcePlayer = UnityEngine.Object.FindObjectOfType<PlayerCombatant>();
            var sourceEnemy = UnityEngine.Object.FindObjectOfType<EnemyCombatant>();
            var binder = UnityEngine.Object.FindObjectOfType<BattleHudSceneBinder>();
            var hud = binder.hudPrefab;
            var font = hud.healthText.font;
            var library = AssetDatabase.LoadAssetAtPath<WeaponLibrary>("Assets/Data/Weapons/WeaponLibrary.asset");
            Check(sourcePlayer != null && sourceEnemy != null && hud != null && library != null, "production player, enemy, weapon library and HUD resolve");
            var pole = AssetDatabase.LoadAssetAtPath<PlayerHeavyAnimationSet>("Assets/Sprite/Characters/Player/AnimationSamples/TwoHandPoleV1/PoleAnimations.asset");
            Debug.Log("SIMULATOR POLE CONFIG: set=" + (pole != null) + ", motion=" + (pole != null && pole.motion != null)
                + ", body=" + (pole != null ? pole.bodyFrames.Length : -1) + ", hands=" + (pole != null ? pole.handFrames.Length : -1)
                + ", nullBody=" + (pole != null ? pole.bodyFrames.Count(s => s == null) : -1)
                + ", nullHands=" + (pole != null ? pole.handFrames.Count(s => s == null) : -1)
                + ", usable=" + (pole != null && pole.motion != null && JsonUtility.FromJson<PlayerTurnSpriteMotion>(pole.motion.text).IsUsable(pole.bodyFrames.Length))
                + ", spear=" + (pole != null && pole.Find("spear") != null));
            var crow = UnityEngine.Object.Instantiate(sourceEnemy.gameObject);
            crow.name = "Crow";
            StripEnemy(crow);
            SetInt(crow.GetComponent<EnemyCombatant>(), "maxHp", 120);
            PrefabUtility.SaveAsPrefabAsset(crow, Folder + "/Crow.prefab"); UnityEngine.Object.DestroyImmediate(crow);
            foreach (var name in new[] { "Rat", "Wraith" })
            {
                var copy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab"));
                copy.name = name; SetInt(copy.GetComponent<EnemyCombatant>(), "maxHp", 120);
                PrefabUtility.SaveAsPrefabAsset(copy, Folder + "/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(copy);
            }

            var roundRoot = new GameObject("BattleSimulatorRound"); roundRoot.SetActive(false);
            var round = roundRoot.AddComponent<BattleSimulatorRound>();
            var playerRoot = UnityEngine.Object.Instantiate(sourcePlayer.gameObject, roundRoot.transform);
            playerRoot.name = "Player";
            foreach (var component in playerRoot.GetComponents<Component>())
                if (!(component is Transform) && !(component is PlayerCombatant) && !(component is Rigidbody2D)
                    && !(component is Collider2D) && !(component is SpriteRenderer) && !(component is PlayerAttackAnimation))
                    UnityEngine.Object.DestroyImmediate(component);
            foreach (var canvas in playerRoot.GetComponentsInChildren<Canvas>(true)) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            round.player = playerRoot.GetComponent<PlayerCombatant>();
            var capsule = playerRoot.GetComponent<CapsuleCollider2D>();
            var bodyBounds = PaintedBounds(playerRoot.GetComponent<SpriteRenderer>().sprite);
            capsule.offset = bodyBounds.center; capsule.size = bodyBounds.size;
            var manager = new GameObject("BattleFlow"); manager.transform.SetParent(roundRoot.transform, false);
            round.flow = manager.AddComponent<BattleFlowController>();
            var textRoot = new GameObject("InternalRoundText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textRoot.transform.SetParent(manager.transform, false); textRoot.SetActive(false);
            SetObject(round.flow, "roundText", textRoot.GetComponent<TMP_Text>());
            SetObject(round.flow, "player", round.player);
            var roster = new SerializedObject(round.flow); roster.FindProperty("enemies").arraySize = 0; roster.ApplyModifiedPropertiesWithoutUndo();
            var equipment = new GameObject("Equipment"); equipment.transform.SetParent(roundRoot.transform, false);
            round.loadout = equipment.AddComponent<PlayerWeaponLoadout>();
            SetObject(round.loadout, "weaponLibrary", library);
            var loadout = new SerializedObject(round.loadout); loadout.FindProperty("equippedWeapons").arraySize = 4;
            for (int i = 0; i < 4; i++) loadout.FindProperty("equippedWeapons").GetArrayElementAtIndex(i).objectReferenceValue = null;
            loadout.ApplyModifiedPropertiesWithoutUndo();
            round.switching = equipment.AddComponent<BattleWeaponController>();
            SetObject(round.switching, "player", round.player); SetObject(round.switching, "weaponLoadout", round.loadout);
            SetObject(round.switching, "battleFlow", round.flow);
            PrefabUtility.SaveAsPrefabAsset(roundRoot, Folder + "/Round.prefab"); UnityEngine.Object.DestroyImmediate(roundRoot);

            EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
            GameObject slothSource = null;
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                if (AssetDatabase.GetAssetPath(renderer.sprite).Contains("/LanDuo/")) slothSource = renderer.gameObject;
            Check(slothSource != null, "existing Sloth artwork and idle controller resolve");
            var sloth = UnityEngine.Object.Instantiate(slothSource); sloth.name = "Sloth";
            sloth.transform.position = Vector3.zero;
            var slothSprite = sloth.GetComponent<SpriteRenderer>(); slothSprite.color = Color.white;
            var slothBounds = PaintedBounds(slothSprite.sprite);
            float slothScale = 3.3f / slothBounds.size.y;
            sloth.transform.localScale = new Vector3(slothScale, slothScale, 1);
            // Align the existing drawing to the same stage baseline used by animated small monsters.
            var slothCombatant = sloth.AddComponent<EnemyCombatant>(); SetInt(slothCombatant, "maxHp", 300);
            var attacks = new SerializedObject(slothCombatant); attacks.FindProperty("isBoss").boolValue = true;
            var attackList = attacks.FindProperty("attackDatas"); attackList.arraySize = 4;
            for (int i = 0; i < 4; i++) attackList.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyAttackData>("Assets/Data/EnemyAttacks/KBM03/KBM0" + (i + 1) + ".asset");
            attacks.ApplyModifiedPropertiesWithoutUndo();
            var slothCollider = sloth.AddComponent<BoxCollider2D>(); slothCollider.offset = slothBounds.center; slothCollider.size = slothBounds.size;
            PrefabUtility.SaveAsPrefabAsset(sloth, Folder + "/Sloth.prefab"); UnityEngine.Object.DestroyImmediate(sloth);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -20); camera.backgroundColor = new Color32(34, 41, 52, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var simulator = new GameObject("BattleSimulator").AddComponent<BattleSimulator>();
            simulator.weaponLibrary = library; simulator.roundPrefab = AssetDatabase.LoadAssetAtPath<BattleSimulatorRound>(Folder + "/Round.prefab");
            simulator.hudPrefab = hud; simulator.font = font;
            simulator.enemies = new[] { "Crow", "Rat", "Wraith", "Sloth" }.Select((name, i) => new BattleSimulator.EnemyOption {
                label = new[] { "腐鸦", "瘟鼠", "怨影", "懒惰" }[i], prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab") }).ToArray();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BattleSimulator.unity"); AssetDatabase.SaveAssets();
            Check(simulator.enemies.All(e => e.prefab != null) && simulator.roundPrefab != null && !simulator.roundPrefab.gameObject.activeSelf,
                "simulator scene has four enemy choices and an inactive fully linked round template");
            SessionState.SetString(Pending + ".checks", string.Join("\n", checks)); SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void StripEnemy(GameObject root)
    {
        foreach (var component in root.GetComponents<Component>())
            if (!(component is Transform) && !(component is EnemyCombatant) && !(component is Rigidbody2D)
                && !(component is Collider2D) && !(component is SpriteRenderer) && !(component is DamageResistance)
                && !(component is EnemySpriteAnimation)) UnityEngine.Object.DestroyImmediate(component);
        foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
    static Bounds PaintedBounds(Sprite sprite)
    {
        // Sprite vertices can include the full transparent canvas. Scan the source alpha once when building.
        var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels = texture.GetPixels32(); int x0 = texture.width, y0 = texture.height, x1 = 0, y1 = 0;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            if (pixels[y * texture.width + x].a > 24) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        var min = new Vector3((x0 - sprite.pivot.x) / sprite.pixelsPerUnit, (y0 - sprite.pivot.y) / sprite.pixelsPerUnit, 0);
        var max = new Vector3((x1 - sprite.pivot.x) / sprite.pixelsPerUnit, (y1 - sprite.pivot.y) / sprite.pixelsPerUnit, 0);
        UnityEngine.Object.DestroyImmediate(texture); return new Bounds((min + max) * .5f, max - min);
    }
    static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static void SetInt(UnityEngine.Object target, string field, int value)
    { var s = new SerializedObject(target); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear(); checks.AddRange(SessionState.GetString(Pending + ".checks", "").Split('\n'));
        Time.captureFramerate = 30;
        var harness = new GameObject("SimulatorCheckHarness").AddComponent<EnemySpriteAnimation>();
        harness.StartCoroutine(Guard(Exercise()));
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
    static IEnumerator Exercise()
    {
        var simulator = UnityEngine.Object.FindObjectOfType<BattleSimulator>();
        for (int i = 0; i < 10 && !simulator.Ready; i++) yield return null;
        Check(simulator.Ready && simulator.Flow.CurrentState == BattleState.PlayerTurn, "scene starts directly into a playable battle");
        Check(simulator.Weapons.Count == 11 && simulator.Weapons.All(w => w.WeaponId != "dual_swords"), "all eleven weapons are selectable and dual swords are excluded");
        for (int i = 0; i < simulator.Weapons.Count; i++)
        {
            var button = GameObject.Find("Weapon_" + simulator.Weapons[i].WeaponId).GetComponent<Button>(); button.onClick.Invoke();
            for (int frame = 0; frame < 5 && !simulator.Ready; frame++) yield return null;
            Check(simulator.Ready && simulator.Flow.Player.CurrentWeapon == simulator.Weapons[i], simulator.Weapons[i].WeaponId + ": actual button equips the selected weapon");
            Check(UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1 && simulator.Flow.Enemies.Count == 1,
                simulator.Weapons[i].WeaponId + ": switching keeps exactly one live enemy");
            if (simulator.Weapons[i].WeaponId == "spear")
                Debug.Log("SIMULATOR SPEAR BODIES: " + string.Join(",", simulator.Flow.Player.GetComponentsInChildren<PlayerTurnSpriteBody>(true).Select(b => b.name + ":" + b.IsReady)));
            bool supported = simulator.Flow.Player.GetComponent<PlayerAttackAnimation>().Supports(simulator.Flow.Player.CurrentWeapon);
            Check(supported, simulator.Weapons[i].WeaponId + ": weapon-specific artwork and animation are available");
            int hp = simulator.Enemy.CurrentHP;
            UnityEngine.Object.FindObjectOfType<BattleHudView>().attack.onClick.Invoke();
            for (int frame = 0; frame < 180 && simulator.Flow.CurrentState != BattleState.PlayerTurn
                && simulator.Flow.CurrentState != BattleState.Victory; frame++) yield return null;
            Check(simulator.Enemy == null || simulator.Enemy.CurrentHP < hp, simulator.Weapons[i].WeaponId + ": ordinary attack executes and deals damage");
        }
        for (int i = 0; i < simulator.enemies.Length; i++)
        {
            GameObject.Find("Enemy_" + i).GetComponent<Button>().onClick.Invoke();
            for (int frame = 0; frame < 5 && !simulator.Ready; frame++) yield return null;
            Check(simulator.Ready && simulator.Enemy.name == simulator.enemies[i].label
                && UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1, "enemy button " + i + ": replaces rather than adds an enemy");
            int playerHP = simulator.Flow.Player.CurrentHP;
            UnityEngine.Object.FindObjectOfType<BattleHudView>().defend.onClick.Invoke();
            for (int frame = 0; frame < 180 && simulator.Flow.CurrentState != BattleState.PlayerTurn; frame++) yield return null;
            Check(simulator.Flow.CurrentState == BattleState.PlayerTurn && simulator.Flow.Player.CurrentHP < playerHP,
                "enemy " + i + ": defense and enemy turn finish through the real battle flow");
            Capture("enemy-" + i + ".png");
        }
        simulator.SelectEnemy(0); yield return null; yield return null;
        simulator.SelectWeapon(0); yield return null; yield return null;
        Check(simulator.Flow.CanUseWeaponSkill, "new simulations begin with enough resolve to test weapon skills");
        int beforeSkill = simulator.Enemy.CurrentHP;
        UnityEngine.Object.FindObjectOfType<BattleHudView>().skill.onClick.Invoke();
        for (int frame = 0; frame < 180 && simulator.Flow.CurrentState != BattleState.PlayerTurn
            && simulator.Flow.CurrentState != BattleState.Victory; frame++) yield return null;
        Check(simulator.Enemy == null || simulator.Enemy.CurrentHP < beforeSkill, "weapon skill executes against the sole target");
        simulator.RestartBattle(); yield return null; yield return null;
        simulator.Attack(); simulator.NextEnemy(); simulator.NextWeapon();
        yield return null; yield return null;
        Check(simulator.Ready && simulator.Flow.CurrentState == BattleState.PlayerTurn
            && UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1, "switching mid-attack cancels the previous round cleanly");
        simulator.DefeatEnemy(); yield return null;
        for (int frame = 0; frame < 30 && simulator.Flow.CurrentState != BattleState.Victory; frame++) yield return null;
        Check(simulator.Flow.CurrentState == BattleState.Victory,
            "death preview can finish without blocking simulator controls");
        simulator.NextEnemy(); yield return null; yield return null;
        Check(UnityEngine.Object.FindObjectsOfType<EnemyCombatant>().Length == 1
            && UnityEngine.Object.FindObjectsOfType<EnemyDeathSpriteMesh>().Length == 0
            && UnityEngine.Object.FindObjectsOfType<EnemyHurtAshPlayback>().Length == 0,
            "switching during corpse playback removes the previous corpse and keeps one enemy");
        simulator.SelectWeapon(simulator.Weapons.Count - 1); simulator.NextWeapon();
        simulator.SelectEnemy(simulator.enemies.Length - 1); simulator.NextEnemy(); yield return null; yield return null;
        Check(simulator.SelectedEnemy == 0 && simulator.SelectedWeapon == 0, "next buttons wrap through the complete supported catalogs");
        Check(GameRoot.I.state.player.maxHp == 1000 && GameRoot.I.state.map.templateId == null,
            "simulation uses a separate fresh run state");
        Capture("battle-simulator.png");
        var motions = ExerciseMotions(simulator);
        while (motions.MoveNext()) yield return motions.Current;
        var library = simulator.weaponLibrary; var roundPrefab = simulator.roundPrefab;
        var options = simulator.enemies; var hudPrefab = simulator.hudPrefab; var font = simulator.font;
        UnityEngine.Object.Destroy(simulator.gameObject); yield return null; yield return null;
        Check(GameRoot.I == null, "leaving a standalone simulation removes its temporary run");
        var existing = new GameObject("ExistingRun").AddComponent<GameRoot>();
        var saved = existing.state; saved.player.hp = 37; saved.world.sinRemnant = 19;
        var temporary = new GameObject("RestorationCheck"); temporary.SetActive(false);
        var another = temporary.AddComponent<BattleSimulator>(); another.weaponLibrary = library;
        another.roundPrefab = roundPrefab; another.enemies = options; another.hudPrefab = hudPrefab; another.font = font;
        temporary.SetActive(true); yield return null; yield return null;
        Check(existing.state != saved && saved.player.hp == 37 && saved.world.sinRemnant == 19,
            "an existing run stays untouched while the simulation uses separate state");
        another.DefeatEnemy(); yield return null;
        UnityEngine.Object.Destroy(temporary); yield return null; yield return null;
        Check(existing.state == saved && saved.player.hp == 37 && saved.world.sinRemnant == 19,
            "leaving the simulator restores the original run without health or reward changes");
        UnityEngine.Object.Destroy(existing.gameObject);
    }

    static IEnumerator ExerciseMotions(BattleSimulator simulator)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clips = new Dictionary<string, List<string>>();
        foreach (var id in new[] { "spear", "poleaxe", "torch", "flail", "sword", "flame_tongue" })
        {
            int index = simulator.Weapons.ToList().FindIndex(w => w.WeaponId == id);
            simulator.SelectWeapon(index); yield return null; yield return null;
            var player = simulator.Flow.Player; var view = player.GetComponent<PlayerAttackAnimation>();
            simulator.Enemy.gameObject.SetActive(false);
            bool pole = id == "spear" || id == "poleaxe", swing = id == "torch" || id == "flail";
            var body = (PlayerTurnSpriteBody)typeof(PlayerAttackAnimation).GetField(pole ? "poleBody" : "turnBody", flags).GetValue(view);
            var blade = (SpriteRenderer)typeof(PlayerAttackAnimation).GetField("weapon", flags).GetValue(view);
            var root = player.transform.position;
            int hits = 0; bool done = false, sawRelease = false, sawFlame = false;
            Vector3 releaseTip = default, contactTip = default, oldRelease = default, oldContact = default;
            Sprite drawing = blade.sprite;
            var samples = new List<string>(); clips[id] = samples;
            float duration = (float)typeof(PlayerAttackAnimation).GetProperty("AttackDuration", flags).GetValue(view);
            float hitTime = (float)typeof(PlayerAttackAnimation).GetProperty("AttackHitTime", flags).GetValue(view);
            PlayerHeavyAnimationSet.Weapon mount = null;
            if (pole) mount = ((PlayerHeavyAnimationSet)typeof(PlayerAttackAnimation).GetField("poleAnimations", flags).GetValue(view)).Find(id);
            Action hit = () =>
            {
                hits++;
                if (pole)
                {
                    contactTip = blade.transform.TransformPoint(mount.trailTip);
                    float extra = PlayerPoleThrust.Advance(hitTime, duration, body.ReleaseTime(false), hitTime);
                    oldContact = contactTip - player.transform.TransformVector(new Vector3(extra / 100f * 1.18f, 0, 0));
                }
            };
            var work = view.PlayAttack(player.CurrentWeapon, () => true, () => true, hit);
            view.StartCoroutine(Complete(work, () => done = true));
            int frame = 0;
            while (!done && frame < 90)
            {
                samples.Add(body.LastFrameIndex + ":" + body.LastView);
                if (pole && Math.Abs(body.LastFrameTime - body.ReleaseTime(false)) < .0001f)
                {
                    sawRelease = true; releaseTip = blade.transform.TransformPoint(mount.trailTip);
                    float extra = PlayerPoleThrust.Advance(body.ReleaseTime(false), duration, body.ReleaseTime(false), hitTime);
                    oldRelease = releaseTip - player.transform.TransformVector(new Vector3(extra / 100f * 1.18f, 0, 0));
                }
                if (id == "flame_tongue")
                    sawFlame |= player.GetComponentsInChildren<PlayerHeavyFlameMesh>(true)
                        .Any(f => f.GetComponent<MeshRenderer>() != null && f.GetComponent<MeshRenderer>().enabled && f.gameObject.activeInHierarchy);
                CaptureWorld("motions/" + id + "/" + frame.ToString("000") + ".png");
                CheckNoMove(player.transform.position == root, id);
                yield return null; frame++;
            }
            Check(done && hits == 1, id + ": exactly one damage callback and full recovery");
            if (pole)
            {
                float distance = Vector3.Distance(contactTip, releaseTip), previous = Vector3.Distance(oldContact, oldRelease);
                Debug.Log("SIMULATOR THRUST DISTANCE " + id + ": old=" + previous + ", new=" + distance);
                Check(sawRelease && distance > previous * 1.8f && distance > 1.5f,
                    id + ": rendered weapon thrust travels at least 1.8 times farther");
                var mesh = body.GetComponent<MeshFilter>().sharedMesh;
                int rearFoot = 28 * 33 + 8;
                var vertex = mesh.vertices[rearFoot];
                Check(Mathf.Abs(vertex.x - (512 - 1040) / 100f) < .02f, id + ": rear foot remains at its original support point");
            }
            if (id == "flail" || id == "flame_tongue")
            {
                Check(drawing != null && drawing.name == (id == "flail" ? "Flail" : "FlameTongue"), id + ": correct original weapon drawing is mounted");
                string reference = id == "flail" ? "torch" : "sword";
                Check(samples.SequenceEqual(clips[reference]), id + ": body uses the exact same frame sequence as " + reference);
                if (id == "flame_tongue") Check(sawFlame, "flame tongue: separate original flame layer stays animated");
            }
        }
    }

    static IEnumerator Complete(IEnumerator work, Action done)
    { while (work.MoveNext()) yield return work.Current; done(); }
    static void CheckNoMove(bool pass, string id)
    { if (!pass) throw new InvalidOperationException(id + ": animation moved the combat actor root"); }
    static void CaptureWorld(string file)
    {
        var go = new GameObject("WeaponMotionCapture"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 4.5f; camera.cullingMask = ~(1 << 5);
        camera.transform.position = new Vector3(-2.3f, .2f, -20); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(34, 41, 52, 255);
        var rt = new RenderTexture(960, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(960, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); image.Apply();
        string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG()); camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
    }

    static void Capture(string filename)
    {
        var camera = Camera.main; var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases) if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5; }
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output), filename), image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        foreach (var canvas in canvases) if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt);
    }
    static void Check(bool pass, string text)
    { if (!pass) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("SIMULATOR CHECK: " + text); }
    static void Logged(string message, string stack, LogType type)
    { if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception)) Finish(false, message + "\n" + stack); }
    static void Finish(bool pass, string error)
    {
        if (finishing) return; finishing = true; SessionState.SetBool(Pending, false); Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = pass, checks = checks.ToArray(), error = error }, true)); EditorApplication.Exit(pass ? 0 : 1);
    }
}
