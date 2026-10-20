using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Authored inactive templates are cloned into the two inventory scroll areas.
public sealed class InventoryEntryView : MonoBehaviour
{
    public Button button;
    public Image backing, icon;
    public TMP_Text title, annotation, fallback;
    public InventoryInkGraphic border;
    VillageServiceIcon itemSymbol;

    public void PresentItemIcon(Sprite sprite, string itemId)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        fallback.text = "";
        fallback.gameObject.SetActive(false);
        bool drawn = sprite == null && (itemId == ItemTable.Bandage || itemId == ItemTable.HolyWater);
        if (drawn && itemSymbol == null)
        {
            var symbol = new GameObject("ItemSymbol", typeof(RectTransform), typeof(CanvasRenderer), typeof(VillageServiceIcon));
            symbol.layer = icon.gameObject.layer;
            symbol.transform.SetParent(icon.transform, false);
            var rect = (RectTransform)symbol.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            itemSymbol = symbol.GetComponent<VillageServiceIcon>();
            itemSymbol.raycastTarget = false;
            itemSymbol.color = new Color32(58, 43, 29, 255);
        }
        if (itemSymbol != null)
        {
            itemSymbol.enabled = drawn;
            itemSymbol.symbol = itemId == ItemTable.Bandage
                ? VillageServiceIcon.Symbol.Bandage : VillageServiceIcon.Symbol.Bottle;
            itemSymbol.SetVerticesDirty();
        }
    }

    public void Select(bool selected)
    {
        if (backing != null) backing.color = new Color(0, 0, 0, selected ? .095f : .012f);
        if (border != null) border.color = new Color(.08f, .065f, .04f, selected ? .8f : .16f);
    }
}
