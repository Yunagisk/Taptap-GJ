using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        else RefreshCombatLabels(null);
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
        RefreshCombatLabels(weapons[index]);
    }

    void RefreshCombatLabels(WeaponData current)
    {
        var panel = GameObject.Find("WeaponsPanel");
        if (panel != null)
        {
            var buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                int slot = PersistentSlot(buttons[i]);
                if (slot < 0) continue;
                var label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                WeaponData data = weapons != null && slot < weapons.Length ? weapons[slot] : null;
                label.fontSize = 16;
                label.text = data == null ? "空" : data.WeaponName + "\n" + WeaponStatTable.AttackBrief(data.WeaponId);
                buttons[i].interactable = data != null;
            }
        }

        var attack = GameObject.Find("AttackButton");
        if (attack == null) return;
        var attackLabel = attack.GetComponentInChildren<TMP_Text>(true);
        if (attackLabel == null || current == null) return;
        attackLabel.text = "攻击\n" + WeaponStatTable.AttackBrief(current.WeaponId);
    }

    static int PersistentSlot(Button button)
    {
        if (button == null) return -1;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var callsField = typeof(UnityEngine.Events.UnityEventBase).GetField("m_PersistentCalls", flags);
        if (callsField == null) return -1;
        object group = callsField.GetValue(button.onClick);
        if (group == null) return -1;
        var listField = group.GetType().GetField("m_Calls", flags);
        var list = listField != null ? listField.GetValue(group) as System.Collections.IList : null;
        if (list == null) return -1;
        for (int i = 0; i < list.Count; i++)
        {
            object call = list[i];
            var method = call.GetType().GetField("m_MethodName", flags);
            if (method == null || (string)method.GetValue(call) != "SwitchWeapon") continue;
            var argsField = call.GetType().GetField("m_Arguments", flags);
            if (argsField == null) continue;
            object args = argsField.GetValue(call);
            var intArg = args.GetType().GetField("m_IntArgument", flags);
            if (intArg == null) continue;
            return (int)intArg.GetValue(args);
        }
        return -1;
    }
}
