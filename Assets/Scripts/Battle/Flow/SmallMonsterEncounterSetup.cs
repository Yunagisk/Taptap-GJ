using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class SmallMonsterEncounterSetup : MonoBehaviour
{
    public BattleFlowController flow;
    public GameObject ratPrefab, crowPrefab, wraithPrefab, slothPrefab;
    bool configured;
    Camera encounterCamera;
    float authoredCameraSize;
    const float CompositionAspect = 16f / 9f;
    public bool IsSlothEncounter { get; private set; }
    public bool GroundAligned { get; private set; }
    public float GroundHeight { get; private set; } = -1.9f;

    void Awake() { ConfigureEncounter(); }
    void Start() { AlignGround(); }
    void LateUpdate()
    {
        FrameCamera();
        if (!GroundAligned) AlignGround();
    }

    // Keep the authored horizontal composition on narrow windows. Resizing the
    // view must not move combatants or reset an attack/hurt animation in flight.
    void FrameCamera()
    {
        var camera = Camera.main;
        if (camera == null || !camera.orthographic) return;
        if (encounterCamera != camera)
        {
            encounterCamera = camera;
            authoredCameraSize = camera.orthographicSize;
        }
        float size = authoredCameraSize * Mathf.Max(1, CompositionAspect / Mathf.Max(.01f, camera.aspect));
        if (!Mathf.Approximately(camera.orthographicSize, size)) camera.orthographicSize = size;
    }

    void AlignGround()
    {
        if (!configured || flow == null || flow.Player == null) return;
        var art = flow.Player.GetComponent<PlayerAttackAnimation>();
        if (art == null || !art.TryGetBodyVisual(out var appearance, out var local)) return;
        float foot = float.PositiveInfinity;
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                foot = Mathf.Min(foot, appearance.transform.TransformPoint(new Vector3(
                    x == 0 ? local.min.x : local.max.x, y == 0 ? local.min.y : local.max.y, 0)).y);
        float shift = foot - GroundHeight;
        foreach (var enemy in flow.Enemies)
            if (enemy != null) enemy.transform.position += Vector3.up * shift;
        GroundHeight = foot;
        GroundAligned = true;
        var background = FindObjectOfType<BattleBackground>();
        if (background != null) { background.groundHeight = foot; background.FitToCamera(); }
    }

    public void ConfigureEncounter()
    {
        if (configured || flow == null || flow.Player == null) return;
        var root = GameRoot.I;
        int seed = root != null ? root.state.map.randomSeed : 0;
        string nodeId = root != null ? root.state.map.pendingNodeId : null;
        if (root != null && root.battleMapTemplate != null
            && !string.IsNullOrEmpty(root.battleRequestId))
        {
            var map = new MapProgression(root.battleMapTemplate.Load(), root.state.map,
                root.state.world, seed);
            var pending = map.GetPendingRequest();
            IsSlothEncounter = pending != null && pending.requestId == root.battleRequestId
                && SlothChapterRules.IsSlothRequest(pending);
        }
        int cleared = 0;
        if (root != null && root.battleMapTemplate != null)
            foreach (var node in root.battleMapTemplate.Load().nodes)
                if (node.kind == MapNodeKind.Battle && root.state.map.clearedNodes.Contains(node.id)) cleared++;
        var kinds = SmallMonsterEncounterRules.Choose(seed, nodeId, cleared);
        bool guide = root != null && VillageIntroRules.IsTutorialBattle(root.state, nodeId);
        var guideContent = guide ? Resources.Load<VillageIntroContent>(VillageIntroController.ContentResource) : null;
        if (guide) kinds = new[] { SmallMonsterKind.Wraith };
        var prefabs = new GameObject[IsSlothEncounter ? 1 : kinds.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            prefabs[i] = IsSlothEncounter ? slothPrefab : kinds[i] == SmallMonsterKind.Rat ? ratPrefab
                : kinds[i] == SmallMonsterKind.Crow ? crowPrefab : wraithPrefab;
            if (prefabs[i] == null)
            {
                Debug.LogError(IsSlothEncounter ? "Sloth encounter prefab is missing."
                    : "Small-monster encounter prefab is missing.", this);
                return;
            }
        }
        configured = true;
        foreach (var old in flow.Enemies)
            if (old != null)
            {
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
        var roster = new List<EnemyCombatant>();
        for (int i = 0; i < prefabs.Length; i++)
        {
            var actor = Instantiate(prefabs[i]);
            SceneManager.MoveGameObjectToScene(actor, gameObject.scene);
            actor.name = IsSlothEncounter ? "Sloth" : kinds[i].ToString() + "_" + (i + 1);
            var enemy = actor.GetComponent<EnemyCombatant>();
            if (guideContent != null) enemy.ConfigureGuide(guideContent.tutorialEnemy);
            var targeting = actor.GetComponent<EnemyTargetSelector>();
            if (targeting == null) targeting = actor.AddComponent<EnemyTargetSelector>();
            targeting.Bind(flow, enemy);
            roster.Add(enemy);
        }
        FrameCamera();
        Position(roster, kinds, IsSlothEncounter);
        flow.ConfigureParticipants(flow.Player, roster);
    }

    static void Position(List<EnemyCombatant> roster, SmallMonsterKind[] kinds, bool sloth)
    {
        const float gap = .45f;
        var widths = new float[roster.Count];
        var offsets = new Vector2[roster.Count];
        float totalWidth = gap * (roster.Count - 1);
        for (int i = 0; i < roster.Count; i++)
        {
            var view = roster[i].GetComponent<EnemySpriteAnimation>();
            if (view == null || view.animations == null || view.animations.Portrait == null) continue;
            var sprite = view.animations.Portrait;
            var b = view.animations.artworkBounds;
            float width = sprite.rect.width / sprite.pixelsPerUnit;
            float height = sprite.rect.height / sprite.pixelsPerUnit;
            float desiredHeight = sloth ? 3.8f
                : kinds[i] == SmallMonsterKind.Rat ? (roster.Count == 3 ? 1.3f : 1.65f)
                : kinds[i] == SmallMonsterKind.Crow ? 2f : 2.45f;
            float scale = desiredHeight / ((b.w - b.y) * height * view.visualScale.y);
            offsets[i] = new Vector2(
                (view.visualOffset.x + ((b.x + b.z) * .5f * width
                    - sprite.pivot.x / sprite.pixelsPerUnit) * view.visualScale.x) * scale,
                (view.visualOffset.y + (b.y * height
                    - sprite.pivot.y / sprite.pixelsPerUnit) * view.visualScale.y) * scale);
            widths[i] = (b.z - b.x) * width * Mathf.Abs(view.visualScale.x * scale);
            totalWidth += widths[i];
            roster[i].transform.localScale = new Vector3(scale, scale, 1);
        }
        // Fit the painted formation against the right edge at any aspect ratio.
        // This also keeps heads and labels clear of the now smaller enemy cards.
        var camera = Camera.main;
        // Ultrawide screens add breathing room instead of spawning enemies far
        // beyond the normal composition, where a later resize would hide them.
        float right = camera != null ? camera.transform.position.x
            + camera.orthographicSize * Mathf.Min(camera.aspect, CompositionAspect) * .95f : 8.4f;
        float left = right - totalWidth;
        for (int i = 0; i < roster.Count; i++)
        {
            float center = left + widths[i] * .5f;
            roster[i].transform.position = new Vector3(center - offsets[i].x, -1.9f - offsets[i].y, 0);
            left += widths[i] + gap;
        }
    }
}
