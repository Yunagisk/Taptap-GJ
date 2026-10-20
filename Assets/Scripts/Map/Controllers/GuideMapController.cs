using TMPro;
using UnityEngine;

public sealed class GuideMapController : MonoBehaviour
{
    WorldMapController owner;
    VillageIntroContent content;
    public GuideDialogueView View { get; private set; }
    TMP_Text goal;
    string markedNodeId;
    UnityEngine.UI.Image marker;
    public TMP_Text TargetLabel { get; private set; }
    public bool IsOpen => View != null && View.IsOpen;
    public static GuideMapController Attach(WorldMapController map)
    {
        if (!VillageIntroRules.IsRestricted(GameRoot.I.state)) return null;
        // This presentation must not inherit the map Canvas's scaled coordinate space.
        var host = new GameObject("GuideMapPresentation");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, map.gameObject.scene);
        var controller = host.AddComponent<GuideMapController>(); controller.owner = map;
        controller.content = Resources.Load<VillageIntroContent>(VillageIntroController.ContentResource);
        controller.View = GuideDialogueView.Create(controller.transform, controller.content);
        var canvas = GuideUI.Canvas(controller.transform, "GuideMapObjective", 40);
        var card = GuideUI.Image("Card", canvas.transform, new Color32(245, 243, 235, 245));
        GuideUI.Center(card.rectTransform, new Vector2(0, 390), new Vector2(800, 84)); card.raycastTarget = false;
        controller.goal = GuideUI.Label("Goal", card.transform, "", Vector2.zero, new Vector2(770, 70), controller.content.font);
        return controller;
    }
    void Start()
    {
        var state = GameRoot.I.state;
        if (state.story.guideEquipmentReady && !state.story.guideOutsideReady
            && state.story.stage == MainStoryStage.CommissionAccepted)
            View.Play(content.outsideDialogue, content.outsideBackground, _ => owner.RefreshGuide());
        owner.RefreshGuide();
    }
    void Update()
    {
        goal.text = VillageIntroRules.NeedsReport(GameRoot.I.state)
            ? "首次清剿完成 · 点击埃尔南多，返回向哈罗德复命。"
            : "东城门外 · 点击发亮的旧磨坊小路节点。";
        goal.transform.parent.gameObject.SetActive(!IsOpen);
        RefreshTarget();
    }
    void RefreshTarget()
    {
        var state = GameRoot.I.state;
        bool report = VillageIntroRules.NeedsReport(state);
        string nodeId = report ? owner.Progression.Layout.villageNodeId : state.story.tutorialNodeId;
        if (marker == null || markedNodeId != nodeId)
        {
            var target = owner.GetComponent<WorldMapSceneView>()?.NodeTransform(nodeId);
            if (target == null) return;
            if (marker != null) Destroy(marker.gameObject);
            marker = GuideUI.Image("GuideDestination", target, new Color32(248, 245, 232, 250));
            marker.raycastTarget = false;
            GuideUI.Center(marker.rectTransform, new Vector2(0, report ? 150 : 85), new Vector2(260, 68));
            TargetLabel = GuideUI.Label("Label", marker.transform, report ? "埃尔南多\n返回复命" : "旧磨坊小路\n点击前往",
                Vector2.zero, new Vector2(250, 62), content.font, 25);
            markedNodeId = nodeId;
        }
        marker.gameObject.SetActive(!IsOpen && !owner.IsEnteringNode && owner.GuideAllowsNode(nodeId));
    }
    public bool PresentEncounter(MapNodeRequest request)
    {
        if (!VillageIntroRules.IsTutorialBattle(GameRoot.I.state, request?.nodeId)) return false;
        if (IsOpen) return true;
        View.Play(content.encounterDialogue, content.outsideBackground, _ => owner.EnterPreparedGuideBattle(request));
        return true;
    }
}
