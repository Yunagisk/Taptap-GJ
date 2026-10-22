using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Restyles the existing menu in place, retaining all of its gameplay callbacks.</summary>
public static class MainMenuOpeningBuilder
{
    const string Art = "Assets/Sprite/UI/MainMenuOpening/";

    [MenuItem("Tools/Game Flow/Apply Main Menu Opening")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(GameFlowSceneBuilder.MenuPath);
        var flow = Object.FindObjectOfType<GameFlowController>();
        if (flow == null || !flow.isMainMenu) throw new System.InvalidOperationException("The main menu controller is missing.");
        var opening = flow.GetComponent<MainMenuOpening>();
        if (opening == null) opening = flow.gameObject.AddComponent<MainMenuOpening>();
        var old = flow.transform.Find("ScreenBackground");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        old = flow.transform.Find("OpeningArtwork");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        old = flow.mainPage.transform.Find("MenuBand");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var black = Rect("OpeningArtwork", flow.transform);
        Stretch(black);
        black.SetAsFirstSibling();
        var blackImage = black.gameObject.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        var artwork = Rect("Artwork16x9", black);
        Stretch(artwork);
        var fit = artwork.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 1920f / 1080;
        opening.cross = CutoutLayer("Cross", artwork, "CrossCutout.png", new Vector2(-2,-21), new Vector2(516,680), "CrossReveal.mat");
        opening.background = Layer("RedBackground", artwork, "Background.jpg", "BackgroundReveal.mat");
        opening.title = CutoutLayer("Title", artwork, "TitleCutout.png", new Vector2(15,-76), new Vector2(850,350));
        // The saved scene itself must start black, before any runtime initialization runs.
        opening.cross.enabled = opening.background.enabled = false;
        opening.cross.color = opening.title.color = new Color(1,1,1,0);
        opening.blackHold = .2f;
        opening.crossFade = 1f;
        opening.crossHold = 0;
        opening.backgroundFade = .85f;
        opening.titleDelay = 0;
        opening.titleFade = .5f;
        opening.menuFade = .25f;
        opening.crossRiseDistance = 160f;
        opening.crossOpacityPower = 4f;
        foreach (var image in new[] { opening.cross, opening.background })
        {
            image.material.SetFloat("_Reveal",0);
            EditorUtility.SetDirty(image.material);
        }

        var content = (RectTransform)flow.mainPage.transform.Find("MenuContent");
        old = content.Find("GameTitle");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        content.anchorMin = content.anchorMax = new Vector2(.5f, .105f);
        content.pivot = new Vector2(.5f, .5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1040, 112);
        content.localScale = Vector3.one;
        opening.menu = content.GetComponent<CanvasGroup>();
        if (opening.menu == null) opening.menu = content.gameObject.AddComponent<CanvasGroup>();
        opening.menu.alpha = 0;
        opening.menu.interactable = opening.menu.blocksRaycasts = false;
        string[] names = { "Btn_NewGame", "Btn_Continue", "Btn_Settings", "Btn_Quit" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = content.Find(names[i]).GetComponent<Button>();
            StyleButton(button, i);
            if (i == 0) opening.firstButton = button;
        }
        var status = content.Find("ContinueStatus").GetComponent<TMP_Text>();
        Box(status.rectTransform, new Vector2(.5f,.5f), new Vector2(-130,-43), new Vector2(230,32));
        status.fontSize = 22;
        status.color = new Color32(150,126,122,255);
        status.alignment = TextAlignmentOptions.Center;
        status.enableWordWrapping = false;
        status.raycastTarget = false;

        old = flow.mainPage.transform.Find("SkipOpening");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var skip = Rect("SkipOpening", flow.mainPage.transform);
        Stretch(skip);
        var hit = skip.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        var skipButton = skip.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = hit;
        skipButton.transition = Selectable.Transition.None;
        skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        UnityEventTools.AddPersistentListener(skipButton.onClick, opening.SkipOpening);
        opening.skipSurface = skip.gameObject;
        var hint = Rect("SkipHint", skip);
        Box(hint, new Vector2(1,0), new Vector2(-240,46), new Vector2(400,36));
        var hintLabel = hint.gameObject.AddComponent<TextMeshProUGUI>();
        hintLabel.font = flow.font;
        hintLabel.text = "点击或按任意键跳过";
        hintLabel.fontSize = 18;
        hintLabel.color = new Color32(160,141,137,255);
        hintLabel.alignment = TextAlignmentOptions.MidlineRight;
        hintLabel.raycastTarget = false;
        opening.skipHint = hint.gameObject.AddComponent<CanvasGroup>();
        opening.skipHint.alpha = 0;
        opening.skipHint.interactable = opening.skipHint.blocksRaycasts = false;
        skip.gameObject.SetActive(false);

        flow.menuPaper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png");
        GameFlowMenuStyle.Apply(flow);
        // The fixed menu area continues to use the existing responsive panel fitter.
        flow.fittedPanels = flow.fittedPanels.Where(panel => panel != null).Distinct().ToArray();
        Camera.main.backgroundColor = Color.black;
        flow.mainPage.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void StyleButton(Button button, int index)
    {
        Box((RectTransform)button.transform, Vector2.one * .5f, new Vector2((index-1.5f)*260,6), new Vector2(230,62));
        var image = button.GetComponent<Image>();
        image.sprite = null;
        image.color = Color.white;
        var colors = button.colors;
        colors.normalColor = new Color32(30,9,10,145);
        colors.highlightedColor = new Color32(118,30,27,230);
        colors.selectedColor = new Color32(92,24,23,210);
        colors.pressedColor = new Color32(161,43,33,245);
        colors.disabledColor = new Color32(20,12,13,100);
        colors.fadeDuration = .18f;
        button.colors = colors;
        var navigation = new Navigation { mode = Navigation.Mode.Horizontal };
        button.navigation = navigation;
        if (button.GetComponent<MainMenuButtonFocus>() == null) button.gameObject.AddComponent<MainMenuButtonFocus>();
        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        Stretch(label.rectTransform);
        label.fontSize = 30;
        label.color = index == 1 ? new Color32(119,103,100,255) : new Color32(228,215,199,255);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.enableWordWrapping = false;
        label.characterSpacing = 8;
        label.raycastTarget = false;
        foreach (string name in new[] { "UpperRule", "LowerRule" })
        {
            var existing = button.transform.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var rule = Rect(name, button.transform);
            Box(rule, new Vector2(.5f, name == "UpperRule" ? 1 : 0), Vector2.zero, new Vector2(230,1));
            var line = rule.gameObject.AddComponent<Image>();
            line.color = new Color32(149,103,84,index == 1 ? (byte)45 : (byte)135);
            line.raycastTarget = false;
        }
    }

    static RawImage Layer(string name, Transform parent, string texture, string material)
    {
        var rect = Rect(name, parent);
        Stretch(rect);
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + texture);
        image.material = AssetDatabase.LoadAssetAtPath<Material>(Art + material);
        image.raycastTarget = false;
        if (image.texture == null || image.material == null) throw new System.InvalidOperationException("Missing menu artwork: " + name);
        return image;
    }

    static RawImage CutoutLayer(string name, Transform parent, string filename, Vector2 position, Vector2 size, string materialName = null)
    {
        string path = Art + filename;
        var image = Rect(name,parent).gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        image.raycastTarget = false;
        if (image.texture == null) throw new System.InvalidOperationException("Missing transparent artwork: " + name);
        if (materialName != null) image.material = AssetDatabase.LoadAssetAtPath<Material>(Art + materialName);
        // Each PNG owns its silhouette alpha; the cross also animates its overall opacity.
        var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
        source.LoadImage(File.ReadAllBytes(path));
        try
        {
            var pixels = source.GetPixels32();
            int left = source.width, bottom = source.height, right = 0, top = 0;
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    if (pixels[y*source.width+x].a >= 32)
                    {
                        left = Mathf.Min(left,x); right = Mathf.Max(right,x);
                        bottom = Mathf.Min(bottom,y); top = Mathf.Max(top,y);
                    }
            left = Mathf.Max(0,left-3); bottom = Mathf.Max(0,bottom-3);
            right = Mathf.Min(source.width,right+4); top = Mathf.Min(source.height,top+4);
            image.uvRect = new Rect((float)left/source.width,(float)bottom/source.height,
                (float)(right-left)/source.width,(float)(top-bottom)/source.height);
            // Retain the position and dimensions of each subject in the supplied composition.
            Box(image.rectTransform,Vector2.one*.5f,position,size);
        }
        finally { Object.DestroyImmediate(source); }
        return image;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Box(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
