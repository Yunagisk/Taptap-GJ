using UnityEngine;

public class PlayerWeaponLoadout : MonoBehaviour
{
    [SerializeField] private WeaponData[] equippedWeapons = new WeaponData[4];
    [SerializeField] private WeaponLibrary weaponLibrary;

    public WeaponData[] EquippedWeapons => equippedWeapons;

    void Awake()
    {
        RefreshFromRun();
    }

    // 直接运行战斗场景时保留测试装备，地图战斗只使用本局四槽装备。
    public void RefreshFromRun()
    {
        var state = SharedRunState();
        if (state == null) return;
        EquipmentRules.EnsureInitialized(state);
        equippedWeapons = new WeaponData[EquipmentRules.SlotCount];
        if (weaponLibrary == null)
        {
            Debug.LogError("地图战斗的 PlayerWeaponLoadout 未绑定 WeaponLibrary。", this);
            return;
        }
        for (int i = 0; i < equippedWeapons.Length; i++)
            equippedWeapons[i] = weaponLibrary.Find(state.inventory.equippedWeaponIds[i]);
    }

    // 局外装备只能选择已拥有武器，购入武器不会自动覆盖装备槽。
    public void EquipWeapon(int slotIndex, WeaponData weapon)
    {
        if (slotIndex < 0 || slotIndex >= EquipmentRules.SlotCount) return;
        var state = SharedRunState();
        if (state != null)
        {
            if (!EquipmentRules.TryEquip(state, slotIndex, weapon != null ? weapon.WeaponId : string.Empty)) return;
            RefreshFromRun();
            return;
        }
        if (equippedWeapons == null || slotIndex >= equippedWeapons.Length) return;
        equippedWeapons[slotIndex] = weapon;
    }

    static GameState SharedRunState()
    {
        var root = GameRoot.I;
        return root != null && root.state != null && root.state.map != null
            && !string.IsNullOrEmpty(root.state.map.templateId) ? root.state : null;
    }
}
