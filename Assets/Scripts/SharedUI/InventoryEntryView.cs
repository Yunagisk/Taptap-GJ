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

    public void Select(bool selected)
    {
        if (backing != null) backing.color = new Color(0, 0, 0, selected ? .095f : .012f);
        if (border != null) border.color = new Color(.08f, .065f, .04f, selected ? .8f : .16f);
    }
}
