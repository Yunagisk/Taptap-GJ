using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays the existing four battle slots; switching is delegated to the battle controller.
public class BattleLoadoutView : MonoBehaviour
{
    public Button[] slotButtons;
    public TMP_Text[] slotLabels;
    public PlayerWeaponLoadout loadout;
    public BattleWeaponController controller;
    public BattleFlowController flow;
    public TMP_FontAsset font;

    Color[] originalColors;
    TMP_FontAsset runtimeFont;
    float nextRefresh;

    void Awake()
    {
        int count = slotButtons != null ? slotButtons.Length : 0;
        originalColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            int slot = i;
            var button = slotButtons[i];
            if (button == null) continue;
            var image = button.targetGraphic as Image;
            originalColors[i] = image != null ? image.color : Color.white;
            button.onClick.AddListener(() => SwitchSlot(slot));
        }
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            if (slotLabels != null)
                foreach (var label in slotLabels)
                    if (label != null) label.font = runtimeFont;
        }
    }

    void OnEnable()
    {
        if (flow != null) flow.OnBattleStateChanged += OnBattleStateChanged;
        nextRefresh = 0;
    }

    void Start() { Refresh(); }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        Refresh();
    }

    public void SwitchSlot(int slot)
    {
        var weapons = loadout != null ? loadout.EquippedWeapons : null;
        if (controller == null || flow == null || flow.CurrentState != BattleState.PlayerTurn
            || weapons == null || slot < 0 || slot >= weapons.Length || weapons[slot] == null) return;
        controller.SwitchWeapon(slot);
        Refresh();
    }

    public void Refresh()
    {
        var weapons = loadout != null ? loadout.EquippedWeapons : null;
        bool canSwitch = controller != null && flow != null && flow.CurrentState == BattleState.PlayerTurn;
        for (int i = 0; slotButtons != null && i < slotButtons.Length; i++)
        {
            var weapon = weapons != null && i < weapons.Length ? weapons[i] : null;
            bool selected = weapon != null && controller != null && controller.CurrentIndex == i;
            string name = weapon == null ? "空槽"
                : !string.IsNullOrEmpty(weapon.WeaponName) ? weapon.WeaponName : WeaponTable.NameOf(weapon.WeaponId);
            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
                slotLabels[i].text = name + (selected ? "\n当前" : "");
            var button = slotButtons[i];
            if (button == null) continue;
            button.interactable = weapon != null && canSwitch;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = selected ? new Color32(181, 213, 191, 255)
                    : originalColors != null && i < originalColors.Length ? originalColors[i] : Color.white;
        }
    }

    void OnBattleStateChanged(BattleState state) { Refresh(); }

    void OnDisable()
    {
        if (flow != null) flow.OnBattleStateChanged -= OnBattleStateChanged;
    }

    void OnDestroy()
    {
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures)
            if (texture != null) Destroy(texture);
        Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
