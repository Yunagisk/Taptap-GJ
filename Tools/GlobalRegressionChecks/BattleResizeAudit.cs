#if UNITY_EDITOR
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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleResizeAudit
{
    const string Pending = "BattleResizeAudit.Pending";
    static BattleResizeAudit()
    {
        EditorApplication.playModeStateChanged += change => {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var go = new GameObject("BattleResizeAudit");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BattleResizeAuditRunner>();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated audit only");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class BattleResizeAuditRunner : MonoBehaviour
{
    readonly string output = Path.GetFullPath("../battle-resize");
    readonly List<string> checks = new List<string>();
    readonly List<string> errors = new List<string>();
    readonly List<TextCandidate> candidates = new List<TextCandidate>();
    readonly List<string> observations = new List<string>();
    [Serializable] public sealed class TextCandidate { public string screen, path, text, reason, overflowMode; public float fontPixels; public bool insideScroll; }
    [Serializable] public sealed class Result { public bool passed; public string[] checks, errors, observations; public TextCandidate[] textCandidates; }
    void Awake() { Directory.CreateDirectory(output); Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
    IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
        while (stack.Count > 0)
        {
            object next;
            try { if (!stack.Peek().MoveNext()) { stack.Pop(); continue; } next = stack.Peek().Current; }
            catch (Exception e) { errors.Add(e.ToString()); Finish(false); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish(errors.Count == 0);
    }
    void Finish(bool ok)
    {
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = ok, checks = checks.ToArray(), errors = errors.ToArray(),
            observations = observations.ToArray(), textCandidates = candidates.ToArray() }, true));
        EditorApplication.Exit(ok ? 0 : 1);
    }
    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var template = FindObjectOfType<GameFlowController>().template;
        foreach (bool boss in new[] { false, true })
        {
            var state = NewRunFactory.Create(template.Load(), "sword", 1);
            state.story.guideCompleted = true;
            state.story.stage = MainStoryStage.TutorialCompleted;
            string node = boss ? state.story.slothNodeId
                : template.Load().nodes.Last(n => n.kind == MapNodeKind.Battle).id;
            if (!boss)
            {
                foreach (var cleared in template.Load().nodes.Where(n => n.kind == MapNodeKind.Battle).Take(6))
                    state.map.clearedNodes.Add(cleared.id);
                for (int seed=0;seed<1000;seed++)
                    if (SmallMonsterEncounterRules.Choose(seed,node,6).Length==3) { state.map.randomSeed=seed;break; }
            }
            state.map.currentNodeId = node; state.map.pendingNodeId = node;
            state.map.pendingRequestId = "resize-fixture";
            state.story.slothConfronted = boss;
            GameRoot.I.state=state; GameRoot.I.battleMapTemplate=template;
            GameRoot.I.battleRequestId="resize-fixture";
            yield return Size(2560,1080);
            var loading=SceneManager.LoadSceneAsync("CombatScene");while(!loading.isDone)yield return null;
            yield return null;yield return null;
            var flow=FindObjectOfType<BattleFlowController>();
            Check(flow!=null && flow.Enemies.Count==(boss?1:3),"actual scene creates expected test roster");
            var positions=flow.Enemies.Select(e=>e.transform.position).ToArray();
            foreach (var size in Sizes())
            {
                yield return Size(size.x,size.y);
                CaptureAndScan((boss?"boss-":"triple-")+size.x+"x"+size.y);
                BoundsObservation((boss?"boss-":"triple-")+size.x+"x"+size.y);
                for(int i=0;i<positions.Length;i++)
                    Check((flow.Enemies[i].transform.position-positions[i]).sqrMagnitude<.0001f,"resize preserves enemy positions and animation continuity");
            }
            int round = flow.RoundNumber;
            int health = flow.Enemies[0].CurrentHP;
            flow.PlayerAttack(); flow.AttackTarget(flow.Enemies[0]);
            Check(flow.CurrentState == BattleState.ExecutingAction, "real attack begins before the resize");
            yield return Size(1024, 768);
            float deadline = Time.realtimeSinceStartup + 15;
            while (flow.CurrentState != BattleState.PlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Check(flow.CurrentState == BattleState.PlayerTurn && flow.RoundNumber == round + 1
                && flow.Enemies[0].CurrentHP < health, "resizing during attack preserves hit settlement and reaches the next turn");
            BoundsObservation(boss ? "boss-after-attack-resize" : "triple-after-attack-resize");
            yield return Size(1024,768);
            loading=SceneManager.LoadSceneAsync("CombatScene");while(!loading.isDone)yield return null;
            yield return null;yield return null;
            BoundsObservation(boss?"boss-narrow-reload":"triple-narrow-reload");
            CaptureAndScan(boss?"boss-narrow-reload":"triple-narrow-reload");
        }
    }
    void BoundsObservation(string phase)
    {
        var flow=FindObjectOfType<BattleFlowController>();
        foreach(var enemy in flow.Enemies)
        {
            var view=enemy.GetComponent<EnemySpriteAnimation>();var sprite=view.Portrait;
            var bounds=view.animations.artworkBounds;
            var visual=enemy.transform.Find("EnemyAnimationVisual");
            var points=new List<Vector3>();
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
                points.Add(Camera.main.WorldToViewportPoint(visual.TransformPoint(new Vector3(
                    ((x==0?bounds.x:bounds.z)*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
                    ((y==0?bounds.y:bounds.w)*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0))));
            VerifyBounds(phase+" "+enemy.name,points);
        }
        Check(flow.Player.GetComponent<PlayerAttackAnimation>().TryGetBodyVisual(out var body,out var local),"player painted body is available");
        var corners=new List<Vector3>();
        for(int x=0;x<2;x++)for(int y=0;y<2;y++) corners.Add(Camera.main.WorldToViewportPoint(body.transform.TransformPoint(
            new Vector3(x==0?local.min.x:local.max.x,y==0?local.min.y:local.max.y,0))));
        VerifyBounds(phase+" player",corners);
    }
    void VerifyBounds(string phase,List<Vector3> points)
    {
        float min=points.Min(p=>p.x),max=points.Max(p=>p.x);
        observations.Add(phase+" painted viewport x="+min.ToString("F3")+".."+max.ToString("F3"));
        Check(min>=0 && max<=1,phase+" artwork stays inside the viewport");
    }
    static Vector2Int[] Sizes() => new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080) };
    IEnumerator Size(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 4);
        var size = constructor.Invoke(new[] { Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1), (object)width, height, "Global audit" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);
        yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f);
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == width && Screen.height == height, "actual game view resolution " + width + "x" + height);
    }
    void CaptureAndScan(string name)
    {
        int width = Screen.width, height = Screen.height;
        var camera = Camera.main;
        var canvases = FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(width,height,24); target.Create();
        var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            foreach (var label in FindObjectsOfType<TMP_Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text) || label.color.a < .1f || label.canvasRenderer.GetInheritedAlpha() < .5f) continue;
                label.ForceMeshUpdate();
                bool scroll = label.GetComponentInParent<ScrollRect>() != null;
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
                bool offscreen = points.Min(p => p.x) < -2 || points.Max(p => p.x) > width+2 || points.Min(p => p.y) < -2 || points.Max(p => p.y) > height+2;
                string reason = (label.isTextOverflowing ? "text-overflow;" : "") + (offscreen ? "rect-offscreen;" : "");
                var fontBase = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(Vector3.zero));
                var fontTop = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(Vector3.up * label.fontSize));
                float fontPixels = Vector2.Distance(fontBase, fontTop);
                if (fontPixels < 12) reason += "font-under-12px;";
                if (reason.Length == 0) continue;
                string path = label.name; for(var t=label.transform.parent;t!=null;t=t.parent) path=t.name+"/"+path;
                candidates.Add(new TextCandidate { screen=name,path=path,text=label.text,reason=reason,insideScroll=scroll,fontPixels=fontPixels,overflowMode=label.overflowMode.ToString() });
            }
            checks.Add("captured and scanned installed screen " + name);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i]; }
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            Destroy(pixels);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
