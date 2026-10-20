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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MainMenuOpeningValidation
{
    const string Pending = "MainMenuOpeningChecks.Pending";
    static readonly List<string> checks = new List<string>();
    static readonly string Output = Path.GetFullPath("../result.json");
    static bool finished;
    [Serializable] sealed class Result { public bool passed; public string[] checks; public string error; }
    static MainMenuOpeningValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project only.");
        MainMenuOpeningBuilder.Apply();
        MainMenuOpeningBuilder.Apply();
        Check(Object.FindObjectsOfType<MainMenuOpening>().Length == 1, "installer is repeatable and retains one opening");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/GameFlow/MainMenuArtwork.shader");
        Check(shader != null && !ShaderUtil.ShaderHasError(shader), "artwork shader compiles");
        SetGameSize(1920,1080);
        var opening = Object.FindObjectOfType<MainMenuOpening>();
        Check(!opening.cross.enabled && !opening.background.enabled && opening.cross.color.a == 0
            && opening.title.color.a == 0 && opening.menu.alpha == 0 && !opening.menu.interactable,
            "saved scene has no visible cross, background, title or menu before Play");
        Capture("00-saved-startup.png");
        CheckFrameBlack("00-saved-startup.png");
        SessionState.SetBool(Pending,true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
        {
            var runner = new GameObject("MainMenuOpeningChecks");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<BattleSimulatorRound>().StartCoroutine(Guard(Exercise()));
        }
    }

    static IEnumerator Guard(IEnumerator work)
    {
        while (!finished)
        {
            bool more;
            try { more = work.MoveNext(); }
            catch (Exception error) { Finish(error); yield break; }
            if (!more) { Finish(null); yield break; }
            yield return work.Current;
        }
    }

    static IEnumerator Exercise()
    {
        yield return null;
        var flow = Object.FindObjectOfType<GameFlowController>();
        var opening = flow.GetComponent<MainMenuOpening>();
        Time.timeScale = 0;
        Capture("00-first-runtime-frame.png");
        CheckFrameBlack("00-first-runtime-frame.png");
        // Exercise the same delay that a slow scene/font load can introduce on startup.
        opening.ReplayOpening();
        yield return null;
        System.Threading.Thread.Sleep(700);
        yield return null;
        Check(opening.Elapsed<=.11f, "a long startup frame cannot jump straight to a visible cross");
        Capture("00-after-startup-stall.png");
        CheckFrameBlack("00-after-startup-stall.png");
        Check(opening.IsPlaying && opening.menu.alpha == 0 && !opening.menu.interactable,
            "opening starts from black with menu interaction blocked");
        Check(opening.background.texture.width == 1920
            && ((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(opening.title.texture))).DoesSourceTextureHaveAlpha(),
            "background retains its original resolution and title uses a PNG with real alpha");
        Check(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(opening.cross.texture))).DoesSourceTextureHaveAlpha()
            && opening.cross.texture != opening.background.texture,
            "cross uses its own transparent PNG instead of masking a copy of the red background");
        while (opening.Elapsed < opening.blackHold*.4f) yield return null;
        Capture("01-black.png");
        CheckFrameBlack("01-black.png");
        Check(opening.crossHold==0, "cross reveal flows directly into the background without a separate static-logo hold");
        Check(Vector2.Distance(opening.cross.rectTransform.anchoredPosition,opening.CrossRestPosition+Vector2.down*opening.crossRiseDistance)<.01f,
            "invisible cross starts below its final position");
        Check(opening.Duration <= 3, "complete opening takes at most three seconds");
        CheckCrossOpacityProgress(opening);
        while (opening.Elapsed < opening.blackHold+opening.crossFade*.35f) yield return null;
        Vector2 earlyCross = opening.cross.rectTransform.anchoredPosition;
        float earlyOpacity = opening.cross.material.GetFloat("_Reveal");
        Check(earlyCross.y<opening.CrossRestPosition.y && earlyOpacity>0 && earlyOpacity<1,
            "complete cross is partially transparent while still below its destination");
        while (opening.Elapsed < opening.blackHold+opening.crossFade*.65f) yield return null;
        Check(opening.cross.rectTransform.anchoredPosition.y>earlyCross.y
            && opening.cross.rectTransform.anchoredPosition.y<opening.CrossRestPosition.y
            && opening.cross.material.GetFloat("_Reveal")>earlyOpacity,
            "cross moves upward and becomes more opaque over the same interval");
        while (opening.Elapsed < opening.BackgroundStartsAt-.15f) yield return null;
        Check(opening.cross.enabled && opening.cross.color.a > .95f && !opening.background.enabled && opening.title.color.a == 0,
            "cross fades in alone before the red background and title");
        Capture("02-cross.png");
        while (opening.Elapsed < opening.blackHold+opening.crossFade) yield return null;
        Check(Vector2.Distance(opening.cross.rectTransform.anchoredPosition,opening.CrossRestPosition)<.01f
            && opening.cross.material.GetFloat("_Reveal")==1,
            "cross reaches its resting position exactly when it becomes fully opaque");
        Capture("16-cross-arrived.png");
        while (opening.Elapsed < opening.BackgroundStartsAt+opening.backgroundFade*.55f) yield return null;
        Check(opening.background.enabled && opening.title.color.a == 0 && opening.menu.alpha == 0,
            "red background emerges from the center before the title");
        Capture("03-background-emerging.png");
        while (opening.Elapsed < opening.TitleStartsAt+opening.titleFade*.7f) yield return null;
        Check(opening.background.material.GetFloat("_Reveal") > .999f && opening.title.color.a > .5f && opening.menu.alpha == 0,
            "title appears after the background has fully emerged");
        Capture("04-title.png");
        while (opening.IsPlaying) yield return null;
        yield return null; yield return null; yield return null;
        Check(opening.menu.alpha == 1 && opening.menu.interactable && opening.menu.blocksRaycasts && !opening.skipSurface.activeSelf,
            "opening completes while time is paused, then enables the existing menu");
        Check(!flow.continueButton.interactable, "continue retains the existing unavailable-save state");
        Check(opening.title.material.shader.name == "UI/Default" && opening.background.material.GetFloat("_Reveal") == 1,
            "final artwork uses ordinary alpha blending for the title and a fully revealed background");
        Check(AssetDatabase.LoadAssetAtPath<Material>("Assets/Sprite/UI/MainMenuOpening/BackgroundReveal.mat").GetFloat("_Reveal") == 0,
            "animation leaves the saved material unchanged");
        Check(AssetDatabase.LoadAssetAtPath<Material>("Assets/Sprite/UI/MainMenuOpening/CrossReveal.mat").GetFloat("_Reveal") == 0,
            "upward animation leaves the saved cross material unchanged");
        Capture("05-main-menu-1920x1080.png");
        CaptureTitleAlpha(opening);
        CaptureCrossAlpha(opening);
        Check(EventSystem.current.currentSelectedGameObject == null, "menu starts with no automatically selected red button");
        var first = opening.firstButton;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(first.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.25f);
        Check(ColourClose(first.targetGraphic.canvasRenderer.GetColor(),first.colors.highlightedColor), "new game highlights while the pointer is over it");
        first.Select();
        ExecuteEvents.Execute(first.gameObject,pointer,ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(.25f);
        Check(EventSystem.current.currentSelectedGameObject == null && ColourClose(first.targetGraphic.canvasRenderer.GetColor(),first.colors.normalColor),
            "pointer exit clears mouse selection and restores the normal button colour");
        var content = opening.menu.transform;
        foreach (string name in new[] { "Btn_NewGame", "Btn_Settings", "Btn_Quit" })
            CheckRaycast(content.Find(name).GetComponent<Button>());

        SetGameSize(1440,1080); yield return null; yield return null;
        Capture("06-main-menu-4x3.png");
        foreach (var button in content.GetComponentsInChildren<Button>().Where(b => b.IsInteractable())) CheckRaycast(button);
        CheckMenuWithinScreen(opening);
        SetGameSize(2560,1080); yield return null; yield return null;
        Capture("07-main-menu-ultrawide.png");
        CheckMenuWithinScreen(opening);
        SetGameSize(1920,1080); yield return null; yield return null;

        Click(content.Find("Btn_Settings").GetComponent<Button>()); yield return null;
        Check(flow.settingsOverlay.activeSelf, "existing settings button opens its original overlay");
        flow.volumeSlider.value = .37f;
        Check(Mathf.Abs(AudioListener.volume-.37f)<.001f && flow.volumeText.text == "37%", "volume slider still controls master audio");
        CheckRaycast(flow.settingsOverlay.transform.Find("Window/Btn_SettingsDone").GetComponent<Button>());
        Capture("08-settings.png");
        flow.HandleBack();
        Check(!flow.settingsOverlay.activeSelf, "Escape routing closes settings");
        Click(content.Find("Btn_Quit").GetComponent<Button>()); yield return null;
        Check(flow.confirmOverlay.activeSelf && flow.confirmationText.text.Contains("退出"), "quit retains its confirmation");
        CheckRaycast(flow.confirmOverlay.transform.Find("Window/Btn_ConfirmCancel").GetComponent<Button>());
        Capture("09-quit-confirmation.png");
        Click(flow.confirmOverlay.transform.Find("Window/Btn_ConfirmCancel").GetComponent<Button>());

        opening.ReplayOpening(); yield return null;
        Check(Vector2.Distance(opening.cross.rectTransform.anchoredPosition,opening.CrossRestPosition+Vector2.down*opening.crossRiseDistance)<.01f,
            "replay restarts below the destination without accumulating a position offset");
        Click(opening.skipSurface.GetComponent<Button>());
        Check(Vector2.Distance(opening.cross.rectTransform.anchoredPosition,opening.CrossRestPosition)<.01f
            && opening.cross.material.GetFloat("_Reveal")==1,
            "skip completes both the rise and the opacity together");
        Check(!opening.IsPlaying && opening.menu.alpha == 1 && !opening.menu.interactable && !flow.IsLoading,
            "clicking skip reveals the final menu without also starting a game");
        yield return null; yield return null; yield return null;
        CheckRaycast(opening.firstButton);
        Time.timeScale = 1;
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null; yield return null; yield return null;
        flow = Object.FindObjectOfType<GameFlowController>();
        opening = flow.GetComponent<MainMenuOpening>();
        Check(!opening.IsPlaying && opening.menu.alpha == 1, "returning to the title within the session does not replay the intro");
        Check(EventSystem.current.currentSelectedGameObject == null, "returning to title does not leave new game selected");
        Click(opening.firstButton);
        float deadline = Time.realtimeSinceStartup+30;
        while (SceneManager.GetActiveScene().name != "Village" && Time.realtimeSinceStartup < deadline) yield return null;
        Check(SceneManager.GetActiveScene().name == "Village", "new game still loads the production village introduction");
    }

    static void CheckMenuWithinScreen(MainMenuOpening opening)
    {
        foreach (var button in opening.menu.GetComponentsInChildren<Button>())
        {
            var bounds = ScreenBounds((RectTransform)button.transform);
            Check(bounds.xMin>=0 && bounds.xMax<=Screen.width && bounds.yMin>=0 && bounds.yMax<=Screen.height,
                button.name+" remains inside "+Screen.width+"x"+Screen.height);
        }
    }

    static bool ColourClose(Color a, Color b)
    {
        return Mathf.Abs(a.r-b.r)<.02f && Mathf.Abs(a.g-b.g)<.02f && Mathf.Abs(a.b-b.b)<.02f && Mathf.Abs(a.a-b.a)<.02f;
    }

    static void CheckFrameBlack(string filename)
    {
        var frame = new Texture2D(2,2,TextureFormat.RGB24,false);
        try
        {
            frame.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Output),filename)));
            Check(frame.GetPixels32().All(pixel => pixel.r<=1 && pixel.g<=1 && pixel.b<=1),filename+" is entirely black with no visible cross");
        }
        finally { Object.DestroyImmediate(frame); }
    }

    static void CheckWholeCrossVisible(MainMenuOpening opening)
    {
        var pixels = new Texture2D(2,2,TextureFormat.RGB24,false);
        try
        {
            pixels.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Output),"12-cross-rising-faint.png")));
            var bounds = ScreenBounds(opening.cross.rectTransform);
            int bottomLit = Count(.02f,.3f), topLit = Count(.7f,.98f);
            Check(bottomLit>40 && topLit>40, "rendered faint cross shows both its crown and lower tip at the same time");

            int Count(float from, float to)
            {
                int lit = 0;
                for (int y = Mathf.CeilToInt(bounds.yMin+bounds.height*from); y < bounds.yMin+bounds.height*to; y++)
                    for (int x = Mathf.CeilToInt(bounds.xMin); x < bounds.xMax; x++)
                    {
                        var color = pixels.GetPixel(x,y);
                        if (Mathf.Max(color.r,color.g,color.b)>.06f) lit++;
                    }
                return lit;
            }
        }
        finally { Object.Destroy(pixels); }
    }

    static void CheckCrossOpacityProgress(MainMenuOpening opening)
    {
        var apply = typeof(MainMenuOpening).GetMethod("ApplyTime",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            // Compare actual rendered brightness with an opaque cross at the very same
            // position. Linear colour-space blending makes alpha alone a poor visual check.
            Sample(.5f,"12-cross-rising-faint.png","17-cross-halfway-opaque-reference.png",.4f);
            CheckWholeCrossVisible(opening);
            Sample(.75f,"13-cross-rising.png","18-cross-three-quarters-opaque-reference.png",.8f);
            Sample(.9f,"19-cross-approaching-stop.png","20-cross-near-stop-opaque-reference.png",.98f);
            apply.Invoke(opening,new object[] { opening.blackHold+opening.crossFade });
            Check(opening.cross.material.GetFloat("_Reveal")==1
                && Vector2.Distance(opening.cross.rectTransform.anchoredPosition,opening.CrossRestPosition)<.01f,
                "only the stopping point completes both the cross shape and its opacity");
        }
        finally { apply.Invoke(opening,new object[] { opening.Elapsed }); }

        void Sample(float phase, string filename, string reference, float maxBrightness)
        {
            apply.Invoke(opening,new object[] { opening.blackHold+opening.crossFade*phase });
            float opacity = opening.cross.material.GetFloat("_Reveal");
            Check(opacity>0 && opacity<1 && opening.cross.rectTransform.anchoredPosition.y<opening.CrossRestPosition.y,
                "cross is still translucent and below its destination at "+phase+" of its rise");
            Capture(filename);
            opening.cross.material.SetFloat("_Reveal",1);
            Capture(reference);
            opening.cross.material.SetFloat("_Reveal",opacity);
            float ratio = Brightness(filename)/Brightness(reference);
            Check(ratio<maxBrightness, "rendered cross brightness at "+phase+" of its rise is "+ratio.ToString("F3")+" of its opaque appearance");
        }

        float Brightness(string filename)
        {
            var pixels = new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                pixels.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Output),filename)));
                var bounds = ScreenBounds(opening.cross.rectTransform);
                double sum = 0;
                for (int y = Mathf.CeilToInt(bounds.yMin); y<bounds.yMax; y++)
                    for (int x = Mathf.CeilToInt(bounds.xMin); x<bounds.xMax; x++)
                    {
                        var pixel = pixels.GetPixel(x,y);
                        sum += Mathf.Max(pixel.r,pixel.g,pixel.b);
                    }
                return (float)sum;
            }
            finally { Object.Destroy(pixels); }
        }
    }

    static void CaptureCrossAlpha(MainMenuOpening opening)
    {
        opening.background.enabled = false;
        opening.cross.enabled = true;
        opening.title.enabled = false;
        opening.menu.alpha = 0;
        Capture("14-cross-on-black.png");
        var checker = new GameObject("Cross alpha preview",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        checker.transform.SetParent(opening.cross.transform.parent,false);
        checker.transform.SetSiblingIndex(opening.cross.transform.GetSiblingIndex());
        var rect = checker.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        texture.SetPixels(new[] { Color.gray,new Color(.18f,.18f,.18f),new Color(.18f,.18f,.18f),Color.gray }); texture.Apply();
        checker.texture = texture; checker.uvRect = new Rect(0,0,40,22.5f); checker.raycastTarget = false;
        Capture("15-cross-alpha-checkerboard.png");
        Object.DestroyImmediate(checker.gameObject); Object.Destroy(texture);
        opening.background.enabled = true; opening.cross.enabled = false; opening.title.enabled = true; opening.menu.alpha = 1;
    }

    static void CaptureTitleAlpha(MainMenuOpening opening)
    {
        opening.background.enabled = false;
        opening.menu.alpha = 0;
        Capture("10-title-on-black.png");
        var checker = new GameObject("Alpha preview",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        checker.transform.SetParent(opening.title.transform.parent,false);
        checker.transform.SetSiblingIndex(opening.title.transform.GetSiblingIndex());
        var rect = checker.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        texture.SetPixels(new[] { Color.gray,new Color(.18f,.18f,.18f),new Color(.18f,.18f,.18f),Color.gray }); texture.Apply();
        checker.texture = texture; checker.uvRect = new Rect(0,0,40,22.5f); checker.raycastTarget = false;
        Capture("11-title-alpha-checkerboard.png");
        Object.DestroyImmediate(checker.gameObject); Object.Destroy(texture);
        opening.background.enabled = true; opening.menu.alpha = 1;
    }

    static void SetGameSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes,new[] { Enum.Parse(method.GetParameters()[0].ParameterType,"Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var ctor = sizeType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).First(c => c.GetParameters().Length == 4);
        var size = ctor.Invoke(new[] { Enum.ToObject(ctor.GetParameters()[0].ParameterType,1),(object)width,height,"Opening checks" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);
    }

    static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }

    static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },ExecuteEvents.pointerClickHandler);

    static void CheckRaycast(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = ScreenBounds((RectTransform)button.transform).center },results);
        Check(results.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject) == button.gameObject,
            button.name+" is reachable through the real UI raycast stack");
    }

    static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        var target = camera.targetTexture; var active = RenderTexture.active;
        var render = new RenderTexture(Screen.width,Screen.height,24); render.Create();
        var pixels = new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = render;
            Canvas.ForceUpdateCanvases();
            foreach (var label in Object.FindObjectsOfType<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output),name),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; RenderTexture.active = active;
            for (int i=0;i<canvases.Length;i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
            Canvas.ForceUpdateCanvases(); Object.Destroy(pixels); render.Release(); Object.Destroy(render);
        }
    }

    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
    static void Logged(string message, string stack, LogType type)
    {
        if (!finished && SessionState.GetBool(Pending,false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Finish(new Exception(message+"\n"+stack));
    }
    static void Finish(Exception error)
    {
        if (finished) return;
        finished = true; SessionState.SetBool(Pending,false); Time.timeScale = 1;
        File.WriteAllText(Output,JsonUtility.ToJson(new Result { passed = error == null, checks = checks.ToArray(), error = error?.ToString() },true));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
