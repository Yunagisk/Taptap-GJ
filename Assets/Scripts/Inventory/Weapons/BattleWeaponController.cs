using UnityEngine;

public class BattleWeaponController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerCombatant player;
    [SerializeField] private PlayerWeaponLoadout weaponLoadout;
    [SerializeField] private BattleFlowController battleFlow;

    private WeaponData[] weapons;
    private int currentIndex = -1;

    public int CurrentIndex => currentIndex;
    public bool CanSwitchWeapon => battleFlow != null && battleFlow.CurrentState == BattleState.PlayerTurn;

    private void Awake()
    {
        if (battleFlow == null) battleFlow = FindObjectOfType<BattleFlowController>();
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (player == null || weaponLoadout == null) return;
        weaponLoadout.RefreshFromRun();
        weapons = weaponLoadout.EquippedWeapons;
        currentIndex = -1;
        if (weapons == null) return;

        var state = SharedRunState();
        if (state != null)
        {
            player.EquipWeapon(null);
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i] != null && weapons[i].WeaponId == state.player.currentWeaponId && TryEquipIndex(i)) return;
        }
        // 初始化先选有效武器，不依赖各组件 Start 的执行先后。
        for (int i = 0; i < weapons.Length; i++)
            if (TryEquipIndex(i)) return;
    }

    // 选目标、执行招式、敌方行动和结算期间均不能换武器。
    public void SwitchWeapon(int index)
    {
        if (!CanSwitchWeapon) return;
        TryEquipIndex(index);
    }

    bool TryEquipIndex(int index)
    {
        if (player == null || weapons == null || index < 0 || index >= weapons.Length || weapons[index] == null)
            return false;
        var state = SharedRunState();
        if (state != null && !EquipmentRules.TrySelectWeapon(state, weapons[index].WeaponId)) return false;
        currentIndex = index;
        player.EquipWeapon(weapons[index]);
        return true;
    }

    static GameState SharedRunState()
    {
        var root = GameRoot.I;
        return root != null && root.state != null && root.state.map != null
            && !string.IsNullOrEmpty(root.state.map.templateId) ? root.state : null;
    }
}
