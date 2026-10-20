using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class VillageShopCard : MonoBehaviour
{
    public Button button;
    public Image backing;
    public InventoryInkGraphic frame;
    public RawImage artwork;
    public VillageServiceIcon fallback;
    public TMP_Text title, price, stock;

    public void Present(VillageShopUI.Offer offer, string name, int amount, bool selected, bool available)
    {
        title.text = name;
        price.text = amount.ToString();
        stock.text = available ? "×1" : "售罄";
        backing.color = new Color(.98f, .92f, .76f, selected ? .52f : .18f);
        frame.color = new Color(.36f, .24f, .12f, selected ? 1 : .35f);
        frame.stroke = selected ? 2.8f : 1.2f;
        frame.SetVerticesDirty();
        VillageShopUI.SetArtwork(artwork, fallback, offer);
        artwork.color = new Color(1, 1, 1, available ? 1 : .28f);
        fallback.color = new Color(.23f, .19f, .14f, available ? 1 : .28f);
    }
}
