using UnityEngine;

public class BattleWeaponController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerCombatant player;
    [SerializeField] private PlayerWeaponLoadout weaponLoadout;
    [SerializeField] private WeaponLibrary weaponLibrary;

    private WeaponData[] weapons;
    private int currentIndex;

    public int CurrentIndex => currentIndex;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        ApplyRunLoadout();
        weapons = weaponLoadout.EquippedWeapons;
        int start = IndexOfCurrent();
        if (start < 0)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                start = i;
                break;
            }
        }
        if (start >= 0 && weapons[start] != null) SwitchWeapon(start);
    }

    void ApplyRunLoadout()
    {
        if (GameRoot.I == null || GameRoot.I.state == null || weaponLoadout == null) return;
        var state = GameRoot.I.state;
        EquipRules.Ensure(state.player);
        WeaponLibrary library = Library();
        if (library == null) return;
        for (int i = 0; i < EquipRules.SlotCount; i++)
        {
            string id = EquipRules.Slot(state.player, i);
            WeaponData data = null;
            if (!string.IsNullOrEmpty(id) && library != null) data = library.Find(id);
            weaponLoadout.EquipWeapon(i, data);
        }
    }

    WeaponLibrary Library()
    {
        if (weaponLibrary != null) return weaponLibrary;
        weaponLibrary = Resources.Load<WeaponLibrary>("WeaponLibrary");
#if UNITY_EDITOR
        if (weaponLibrary == null)
            weaponLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponLibrary>("Assets/Data/Weapons/WeaponLibrary.asset");
#endif
        if (weaponLibrary == null) Debug.LogError("战斗找不到 WeaponLibrary，无法按装配结果换上武器。");
        return weaponLibrary;
    }

    int IndexOfCurrent()
    {
        if (weapons == null || GameRoot.I == null || GameRoot.I.state == null) return -1;
        string current = GameRoot.I.state.player.currentWeaponId;
        if (string.IsNullOrEmpty(current)) return -1;
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i] != null && weapons[i].WeaponId == current) return i;
        return -1;
    }

    // 局内切换武器
    public void SwitchWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length) return;
        if (weapons[index] == null) return;

        currentIndex = index;
        player.EquipWeapon(weapons[index]);
        if (GameRoot.I != null && GameRoot.I.state != null)
            GameRoot.I.state.player.currentWeaponId = weapons[index].WeaponId;
    }
}
