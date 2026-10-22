using UnityEngine;

// The scene only holds references; layout and artwork live in CombatHUD.prefab.
[DefaultExecutionOrder(50)]
public sealed class BattleHudSceneBinder : MonoBehaviour
{
    public BattleHudView hudPrefab;
    public BattleFlowController flow;
    public PlayerWeaponLoadout loadout;
    public BattleWeaponController weapons;
    public GameObject[] legacyRoots;
    BattleHudView instance;
    void Awake()
    {
        if (hudPrefab == null || flow == null || loadout == null || weapons == null)
        { Debug.LogError("Combat HUD references are missing.", this); return; }
        instance = Instantiate(hudPrefab);
        instance.name = "CombatHUD";
        instance.Bind(flow, loadout, weapons);
        foreach (var root in legacyRoots) if (root != null) root.SetActive(false);
        foreach (var old in FindObjectsOfType<BattleLoadoutView>())
            if (old.gameObject.scene == gameObject.scene) old.gameObject.SetActive(false);
        // Remove duplicate world health bars, retaining the independent result overlay.
        foreach (var canvas in FindObjectsOfType<Canvas>())
            if (canvas.gameObject.scene == gameObject.scene && canvas.renderMode == RenderMode.WorldSpace
                && canvas.GetComponentInParent<Combatant>() != null) canvas.gameObject.SetActive(false);
    }
    void OnDestroy() { if (instance != null) Destroy(instance.gameObject); }
}
